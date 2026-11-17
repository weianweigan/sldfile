using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SldFile.Drawing;

/// <summary>
/// AutoCAD R12 (AC1009) DXF writer, Kasa least-squares circle/arc fitter, and DXF verification reader,
/// ported from sldprt-drwgeom.cpp.
/// </summary>
public static class DrawingPreviewDxfWriter
{
    public sealed class FittedArcOrCircle
    {
        public bool IsFullCircle { get; set; }
        public double CenterXMm { get; set; }
        public double CenterYMm { get; set; }
        public double RadiusMm { get; set; }
        public double StartAngleDeg { get; set; }
        public double EndAngleDeg { get; set; }
        public double MaxRadialErrorMm { get; set; }
        public string Layer { get; set; } = "VIEW_VISIBLE";
    }

    public sealed class DxfReadbackSummary
    {
        public int LineCount { get; set; }
        public int ArcCount { get; set; }
        public int CircleCount { get; set; }
        public int TextCount { get; set; }
        public double MinXMm { get; set; } = double.PositiveInfinity;
        public double MinYMm { get; set; } = double.PositiveInfinity;
        public double MaxXMm { get; set; } = double.NegativeInfinity;
        public double MaxYMm { get; set; } = double.NegativeInfinity;
        public HashSet<string> Layers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static string Write(
        DrawingPreviewDefinition definition,
        double scaleDenominator = 1.0,
        double frameMarginHorizontalMm = 0.0,
        double frameMarginVerticalMm = 0.0,
        bool fitPolylineArcs = true)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        double widthMm = definition.PaperWidth > 0 ? definition.PaperWidth * 1000.0 : 297.0;
        double heightMm = definition.PaperHeight > 0 ? definition.PaperHeight * 1000.0 : 210.0;
        double dimScale = scaleDenominator > 1e-6 ? scaleDenominator : 1.0;

        var sb = new StringBuilder(65536);

        // 1. HEADER section (AC1009 / AutoCAD R12)
        sb.Append("  0\nSECTION\n  2\nHEADER\n");
        sb.Append("  9\n$ACADVER\n  1\nAC1009\n");
        sb.Append("  9\n$INSUNITS\n 70\n4\n");
        sb.Append("  9\n$MEASUREMENT\n 70\n1\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  9\n$DIMSCALE\n 40\n{0:F6}\n", dimScale);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  9\n$LTSCALE\n 40\n{0:F6}\n", Math.Max(1.0, dimScale * 0.5));
        sb.Append("  9\n$LIMMIN\n 10\n0.0\n 20\n0.0\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  9\n$LIMMAX\n 10\n{0:F6}\n 20\n{1:F6}\n", widthMm, heightMm);
        sb.Append("  9\n$EXTMIN\n 10\n0.0\n 20\n0.0\n 30\n0.0\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  9\n$EXTMAX\n 10\n{0:F6}\n 20\n{1:F6}\n 30\n0.0\n", widthMm, heightMm);
        sb.Append("  0\nENDSEC\n");

        // 2. TABLES section (LTYPE & LAYER)
        sb.Append("  0\nSECTION\n  2\nTABLES\n");
        sb.Append("  0\nTABLE\n  2\nLTYPE\n 70\n3\n");
        WriteLtype(sb, "CONTINUOUS", "Solid line", Array.Empty<double>());
        WriteLtype(sb, "DASHED", "Hidden line", new[] { 3.0, -1.5 });
        WriteLtype(sb, "CENTER", "Center line", new[] { 8.0, -2.0, 2.0, -2.0 });
        sb.Append("  0\nENDTAB\n");

        sb.Append("  0\nTABLE\n  2\nLAYER\n 70\n7\n");
        WriteLayer(sb, "0", 7, "CONTINUOUS");
        WriteLayer(sb, "FRAME", 7, "CONTINUOUS");
        WriteLayer(sb, "TITLE", 3, "CONTINUOUS");
        WriteLayer(sb, "VIEW_VISIBLE", 7, "CONTINUOUS");
        WriteLayer(sb, "VIEW_HIDDEN", 8, "DASHED");
        WriteLayer(sb, "VIEW_CENTER", 1, "CENTER");
        WriteLayer(sb, "TEXT", 2, "CONTINUOUS");
        sb.Append("  0\nENDTAB\n");
        sb.Append("  0\nENDSEC\n");

        // 3. ENTITIES section
        sb.Append("  0\nSECTION\n  2\nENTITIES\n");

        // Outer sheet boundary
        WriteLineEntity(sb, "FRAME", "CONTINUOUS", 0, 0, widthMm, 0);
        WriteLineEntity(sb, "FRAME", "CONTINUOUS", widthMm, 0, widthMm, heightMm);
        WriteLineEntity(sb, "FRAME", "CONTINUOUS", widthMm, heightMm, 0, heightMm);
        WriteLineEntity(sb, "FRAME", "CONTINUOUS", 0, heightMm, 0, 0);

        if (frameMarginHorizontalMm > 0.5 && frameMarginVerticalMm > 0.5 &&
            frameMarginHorizontalMm * 2 < widthMm && frameMarginVerticalMm * 2 < heightMm)
        {
            double x0 = frameMarginHorizontalMm, y0 = frameMarginVerticalMm;
            double x1 = widthMm - frameMarginHorizontalMm, y1 = heightMm - frameMarginVerticalMm;
            WriteLineEntity(sb, "FRAME", "CONTINUOUS", x0, y0, x1, y0);
            WriteLineEntity(sb, "FRAME", "CONTINUOUS", x1, y0, x1, y1);
            WriteLineEntity(sb, "FRAME", "CONTINUOUS", x1, y1, x0, y1);
            WriteLineEntity(sb, "FRAME", "CONTINUOUS", x0, y1, x0, y0);
        }

        foreach (var p in definition.Primitives)
        {
            string style = p.LineStyle ?? string.Empty;
            bool isHidden = style.IndexOf("HIDDEN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            style.IndexOf("DASH", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isCenter = style.IndexOf("CENTER", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            style.IndexOf("PHANTOM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            style.IndexOf("点画", StringComparison.OrdinalIgnoreCase) >= 0;

            string ltype = isCenter ? "CENTER" : isHidden ? "DASHED" : "CONTINUOUS";
            string layer = isCenter ? "VIEW_CENTER" : isHidden ? "VIEW_HIDDEN" : "VIEW_VISIBLE";

            switch (p.Kind)
            {
                case DrawingPreviewPrimitiveKind.Line:
                    if (p.Points.Count >= 4 && fitPolylineArcs &&
                        TryFitCircleOrArc(p.Points.Select(pt => (pt.X * 1000.0, pt.Y * 1000.0)).ToList(), out var fitted) &&
                        fitted != null)
                    {
                        if (fitted.IsFullCircle)
                            WriteCircleEntity(sb, layer, ltype, fitted.CenterXMm, fitted.CenterYMm, fitted.RadiusMm);
                        else
                            WriteArcEntity(sb, layer, ltype, fitted.CenterXMm, fitted.CenterYMm, fitted.RadiusMm, fitted.StartAngleDeg, fitted.EndAngleDeg);
                        break;
                    }
                    for (int i = 1; i < p.Points.Count; i++)
                    {
                        double x1 = p.Points[i - 1].X * 1000.0;
                        double y1 = p.Points[i - 1].Y * 1000.0;
                        double x2 = p.Points[i].X * 1000.0;
                        double y2 = p.Points[i].Y * 1000.0;
                        if (Math.Abs(x2 - x1) + Math.Abs(y2 - y1) < 1e-6) continue;
                        WriteLineEntity(sb, layer, ltype, x1, y1, x2, y2);
                    }
                    break;

                case DrawingPreviewPrimitiveKind.Arc:
                    if (p.Points.Count >= 3)
                    {
                        double cx = p.Points[2].X * 1000.0;
                        double cy = p.Points[2].Y * 1000.0;
                        double sx = p.Points[0].X * 1000.0;
                        double sy = p.Points[0].Y * 1000.0;
                        double ex = p.Points[1].X * 1000.0;
                        double ey = p.Points[1].Y * 1000.0;
                        double r = Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy));
                        if (r > 1e-4)
                        {
                            double chord = Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy));
                            if (chord < 1e-4)
                            {
                                WriteCircleEntity(sb, layer, ltype, cx, cy, r);
                            }
                            else
                            {
                                double a0 = NormalizeDegrees(Math.Atan2(sy - cy, sx - cx) * 180.0 / Math.PI);
                                double a1 = NormalizeDegrees(Math.Atan2(ey - cy, ex - cx) * 180.0 / Math.PI);
                                if (p.Normal.Z < 0)
                                {
                                    (a0, a1) = (a1, a0);
                                }
                                WriteArcEntity(sb, layer, ltype, cx, cy, r, a0, a1);
                            }
                        }
                    }
                    break;

                case DrawingPreviewPrimitiveKind.Text:
                    if (p.Points.Count > 0 && !string.IsNullOrWhiteSpace(p.Text))
                    {
                        double tx = p.Points[0].X * 1000.0;
                        double ty = p.Points[0].Y * 1000.0;
                        double th = Math.Max(1.0, p.TextHeight * 1000.0);
                        double rotDeg = p.Angle * 180.0 / Math.PI;
                        WriteTextEntity(sb, "TEXT", tx, ty, th, p.Text, rotDeg);
                    }
                    break;
            }
        }

        sb.Append("  0\nENDSEC\n  0\nEOF\n");
        return sb.ToString();
    }

    /// <summary>
    /// Kasa algebraic least-squares circle/arc fitting ported from sldprt-drwgeom.cpp (FitCircleArc).
    /// </summary>
    public static bool TryFitCircleOrArc(IReadOnlyList<(double X, double Y)> ptsMm, out FittedArcOrCircle? result)
    {
        result = null;
        if (ptsMm == null || ptsMm.Count < 6) return false;

        int n = ptsMm.Count;
        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += ptsMm[i].X; my += ptsMm[i].Y; }
        mx /= n; my /= n;

        double suu = 0, suv = 0, svv = 0, suuu = 0, svvv = 0, suvv = 0, svuu = 0;
        for (int i = 0; i < n; i++)
        {
            double u = ptsMm[i].X - mx;
            double v = ptsMm[i].Y - my;
            suu += u * u;
            suv += u * v;
            svv += v * v;
            suuu += u * u * u;
            svvv += v * v * v;
            suvv += u * v * v;
            svuu += v * u * u;
        }

        double det = suu * svv - suv * suv;
        if (Math.Abs(det) < 1e-9) return false;

        double rhsU = 0.5 * (suuu + suvv);
        double rhsV = 0.5 * (svvv + svuu);
        double uc = (rhsU * svv - rhsV * suv) / det;
        double vc = (suu * rhsV - suv * rhsU) / det;
        double cx = mx + uc;
        double cy = my + vc;

        double rSum = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = ptsMm[i].X - cx, dy = ptsMm[i].Y - cy;
            rSum += Math.Sqrt(dx * dx + dy * dy);
        }
        double radius = rSum / n;
        if (radius < 0.25 || radius > 5000.0) return false;

        double maxErr = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = ptsMm[i].X - cx, dy = ptsMm[i].Y - cy;
            double err = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - radius);
            if (err > maxErr) maxErr = err;
        }
        if (maxErr > Math.Max(0.05, radius * 0.015)) return false;

        double startToEnd = Math.Sqrt(
            (ptsMm[n - 1].X - ptsMm[0].X) * (ptsMm[n - 1].X - ptsMm[0].X) +
            (ptsMm[n - 1].Y - ptsMm[0].Y) * (ptsMm[n - 1].Y - ptsMm[0].Y));

        double totalSweep = 0;
        for (int i = 1; i < n; i++)
        {
            double aPrev = Math.Atan2(ptsMm[i - 1].Y - cy, ptsMm[i - 1].X - cx);
            double aCurr = Math.Atan2(ptsMm[i].Y - cy, ptsMm[i].X - cx);
            double da = aCurr - aPrev;
            while (da <= -Math.PI) da += 2 * Math.PI;
            while (da > Math.PI) da -= 2 * Math.PI;
            totalSweep += da;
        }

        bool fullCircle = startToEnd <= Math.Max(0.2, radius * 0.05) && Math.Abs(Math.Abs(totalSweep) - 2 * Math.PI) < 0.35;
        double aStart = NormalizeDegrees(Math.Atan2(ptsMm[0].Y - cy, ptsMm[0].X - cx) * 180.0 / Math.PI);
        double aEnd = NormalizeDegrees(Math.Atan2(ptsMm[n - 1].Y - cy, ptsMm[n - 1].X - cx) * 180.0 / Math.PI);
        if (totalSweep < 0)
        {
            (aStart, aEnd) = (aEnd, aStart);
        }

        result = new FittedArcOrCircle
        {
            IsFullCircle = fullCircle,
            CenterXMm = cx,
            CenterYMm = cy,
            RadiusMm = radius,
            StartAngleDeg = aStart,
            EndAngleDeg = aEnd,
            MaxRadialErrorMm = maxErr
        };
        return true;
    }

    /// <summary>
    /// Reads back a generated DXF string and summarizes entity counts and geometric bounds (ported from ReadDxf in sldprt-drwgeom.cpp).
    /// </summary>
    public static DxfReadbackSummary ReadDxfSummary(string dxfContent)
    {
        var summary = new DxfReadbackSummary();
        if (string.IsNullOrWhiteSpace(dxfContent)) return summary;

        using var reader = new StringReader(dxfContent);
        bool inEntities = false;
        string currentEntity = string.Empty;
        while (true)
        {
            string? codeLine = reader.ReadLine();
            string? valLine = reader.ReadLine();
            if (codeLine == null || valLine == null) break;

            if (!int.TryParse(codeLine.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                continue;
            string val = valLine.Trim();

            if (code == 2 && val == "ENTITIES") { inEntities = true; continue; }
            if (code == 0 && val == "ENDSEC") { inEntities = false; currentEntity = string.Empty; continue; }
            if (!inEntities) continue;

            if (code == 0)
            {
                currentEntity = val;
                if (val == "LINE") summary.LineCount++;
                else if (val == "ARC") summary.ArcCount++;
                else if (val == "CIRCLE") summary.CircleCount++;
                else if (val == "TEXT") summary.TextCount++;
                continue;
            }

            if (code == 8 && !string.IsNullOrEmpty(val))
            {
                summary.Layers.Add(val);
            }
            else if ((code == 10 || code == 11) && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double x))
            {
                if (x < summary.MinXMm) summary.MinXMm = x;
                if (x > summary.MaxXMm) summary.MaxXMm = x;
            }
            else if ((code == 20 || code == 21) && double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                if (y < summary.MinYMm) summary.MinYMm = y;
                if (y > summary.MaxYMm) summary.MaxYMm = y;
            }
        }
        return summary;
    }

    private static double NormalizeDegrees(double deg)
    {
        double r = deg % 360.0;
        return r < 0 ? r + 360.0 : r;
    }

    private static void WriteLtype(StringBuilder sb, string name, string desc, double[] pattern)
    {
        double total = pattern.Sum(v => Math.Abs(v));
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  0\nLTYPE\n  2\n{0}\n 70\n0\n  3\n{1}\n 72\n65\n 73\n{2}\n 40\n{3:F4}\n",
            name, desc, pattern.Length, total);
        foreach (double seg in pattern)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, " 49\n{0:F4}\n", seg);
        }
    }

    private static void WriteLayer(StringBuilder sb, string name, int colorIndex, string ltype)
    {
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  0\nLAYER\n  2\n{0}\n 70\n0\n 62\n{1}\n  6\n{2}\n",
            name, colorIndex, ltype);
    }

    private static void WriteLineEntity(StringBuilder sb, string layer, string ltype, double x1, double y1, double x2, double y2)
    {
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  0\nLINE\n  8\n{0}\n  6\n{1}\n 10\n{2:F6}\n 20\n{3:F6}\n 30\n0.0\n 11\n{4:F6}\n 21\n{5:F6}\n 31\n0.0\n",
            layer, ltype, x1, y1, x2, y2);
    }

    private static void WriteCircleEntity(StringBuilder sb, string layer, string ltype, double cx, double cy, double r)
    {
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  0\nCIRCLE\n  8\n{0}\n  6\n{1}\n 10\n{2:F6}\n 20\n{3:F6}\n 30\n0.0\n 40\n{4:F6}\n",
            layer, ltype, cx, cy, r);
    }

    private static void WriteArcEntity(StringBuilder sb, string layer, string ltype, double cx, double cy, double r, double startDeg, double endDeg)
    {
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  0\nARC\n  8\n{0}\n  6\n{1}\n 10\n{2:F6}\n 20\n{3:F6}\n 30\n0.0\n 40\n{4:F6}\n 50\n{5:F6}\n 51\n{6:F6}\n",
            layer, ltype, cx, cy, r, startDeg, endDeg);
    }

    private static void WriteTextEntity(StringBuilder sb, string layer, double x, double y, double height, string text, double rotationDeg)
    {
        string clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (clean.Length == 0) return;
        sb.AppendFormat(CultureInfo.InvariantCulture,
            "  0\nTEXT\n  8\n{0}\n 10\n{1:F6}\n 20\n{2:F6}\n 30\n0.0\n 40\n{3:F6}\n  1\n{4}\n 50\n{5:F6}\n",
            layer, x, y, height, clean, rotationDeg);
    }
}
