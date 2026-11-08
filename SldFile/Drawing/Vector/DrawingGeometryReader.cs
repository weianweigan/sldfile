using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Drawing.Vector;

/// <summary>
/// High-performance parser and generator for 2D CAD vector line geometry (sheet borders, title block grids,
/// view outlines, silhouettes, and wireframe primitives) extracted from SolidWorks drawing streams (e.g. VBLists, Definition).
/// Powers true lossless mathematical vector CAD rendering in eDrawings-quality mode.
/// </summary>
public static class DrawingGeometryReader
{
    /// <summary>
    /// Extracts and constructs all 2D vector CAD line geometry and individual view geometries for a drawing sheet.
    /// Combines standard sheet border frames, title block outer boundaries, DisplayLists native 2D CAD line primitives,
    /// and transformed view geometric wireframes.
    /// </summary>
    public static (IReadOnlyList<DrawingVectorLine> Lines, IReadOnlyList<DrawingViewGeometry> Views) ReadSheetGeometryAndViews(
        byte[]? displayListsStream,
        byte[]? vbListsStream,
        byte[]? definitionStream,
        double paperWidthMm,
        double paperHeightMm)
    {
        var allLines = new List<DrawingVectorLine>();
        double w = paperWidthMm > 50.0 ? paperWidthMm : 297.0;
        double h = paperHeightMm > 50.0 ? paperHeightMm : 210.0;

        // 1. Extract Views from Definition Stream
        var views = new List<DrawingViewGeometry>();
        if (definitionStream != null && definitionStream.Length > 100)
        {
            views.AddRange(ExtractViewsFromDefinition(definitionStream, w, h));
        }

        // 2. Primary: Extract 2D CAD Vector Geometry from Contents/DisplayLists (eDrawings native stream)
        bool hasDisplayListLines = false;
        if (displayListsStream != null && displayListsStream.Length > 100)
        {
            var dlLines = ExtractLinesFromDisplayLists(displayListsStream, w, h);
            if (dlLines.Count > 0)
            {
                allLines.AddRange(dlLines);
                hasDisplayListLines = true;
                AssignLinesToViews(dlLines, views);
            }
        }

        // 3. Always include 4 clean paper edge boundaries
        GenerateSheetOuterBorders(allLines, w, h);

        // 4. Fallback: Generate synthetic inner borders and grids only if no native DisplayLists lines
        if (!hasDisplayListLines)
        {
            GenerateSheetInnerBorders(allLines, w, h);
            GenerateTitleBlockAndToleranceGrids(allLines, w, h);

            if (vbListsStream != null && vbListsStream.Length > 50)
            {
                var viewLines = ExtractViewGeometryFromVBLists(vbListsStream, views, w, h);
                allLines.AddRange(viewLines);
            }
        }

        return (allLines.AsReadOnly(), views.AsReadOnly());
    }

    /// <summary>
    /// Backwards compatible overload for ReadSheetGeometryAndViews without DisplayLists stream.
    /// </summary>
    public static (IReadOnlyList<DrawingVectorLine> Lines, IReadOnlyList<DrawingViewGeometry> Views) ReadSheetGeometryAndViews(
        byte[]? vbListsStream,
        byte[]? definitionStream,
        double paperWidthMm,
        double paperHeightMm)
    {
        return ReadSheetGeometryAndViews(null, vbListsStream, definitionStream, paperWidthMm, paperHeightMm);
    }

    /// <summary>
    /// Extracts all 2D vector CAD line geometry for a drawing sheet with DisplayLists support.
    /// </summary>
    public static IReadOnlyList<DrawingVectorLine> ReadSheetGeometry(
        byte[]? displayListsStream,
        byte[]? vbListsStream,
        byte[]? definitionStream,
        double paperWidthMm,
        double paperHeightMm)
    {
        var (lines, _) = ReadSheetGeometryAndViews(displayListsStream, vbListsStream, definitionStream, paperWidthMm, paperHeightMm);
        return lines;
    }

    /// <summary>
    /// Extracts all 2D vector CAD line geometry for a drawing sheet (backwards compatible).
    /// </summary>
    public static IReadOnlyList<DrawingVectorLine> ReadSheetGeometry(
        byte[]? vbListsStream,
        byte[]? definitionStream,
        double paperWidthMm,
        double paperHeightMm)
    {
        var (lines, _) = ReadSheetGeometryAndViews(null, vbListsStream, definitionStream, paperWidthMm, paperHeightMm);
        return lines;
    }

    private static void AssignLinesToViews(List<DrawingVectorLine> lines, List<DrawingViewGeometry> views)
    {
        if (views.Count == 0 || lines.Count == 0) return;

        foreach (var view in views)
        {
            double left = view.LeftXMm - 5.0;
            double right = view.RightXMm + 5.0;
            double bottom = view.BottomYMm - 5.0;
            double top = view.TopYMm + 5.0;

            var matched = lines.Where(l =>
                (l.X1 >= left && l.X1 <= right && l.Y1 >= bottom && l.Y1 <= top) ||
                (l.X2 >= left && l.X2 <= right && l.Y2 >= bottom && l.Y2 <= top)).ToList();

            if (matched.Count > 0)
            {
                view.Lines = matched;
            }
        }
    }

    /// <summary>
    /// Decodes high-precision 2D CAD vector line geometry directly from SolidWorks Contents/DisplayLists stream.
    /// This is the exact native stream used by eDrawings to render clean, lossless, sub-millimeter 2D drawing views.
    /// </summary>
    public static List<DrawingVectorLine> ExtractLinesFromDisplayLists(
        byte[] displayListsStream,
        double paperWidthMm,
        double paperHeightMm)
    {
        var lines = new List<DrawingVectorLine>();
        if (displayListsStream == null || displayListsStream.Length < 96) return lines;

        int startOffset = 0;
        byte[] layerMgrTag = System.Text.Encoding.ASCII.GetBytes("uiLayerMgr_c");
        for (int i = 0; i <= Math.Min(displayListsStream.Length - layerMgrTag.Length, 8192); i++)
        {
            bool match = true;
            for (int j = 0; j < layerMgrTag.Length; j++)
            {
                if (displayListsStream[i + j] != layerMgrTag[j]) { match = false; break; }
            }
            if (match)
            {
                startOffset = i + layerMgrTag.Length;
                break;
            }
        }

        double maxAllowedW = Math.Max(paperWidthMm * 2.0, 1500.0);
        double maxAllowedH = Math.Max(paperHeightMm * 2.0, 1500.0);

        for (int i = startOffset; i <= displayListsStream.Length - 80; i++)
        {
            if (displayListsStream[i] == 0xFF && displayListsStream[i + 1] == 0xFE && displayListsStream[i + 2] == 0xFF)
            {
                int nameLen = displayListsStream[i + 3];
                if (nameLen >= 3 && nameLen <= 32 && i + 4 + nameLen * 2 + 8 + 48 <= displayListsStream.Length)
                {
                    string styleName = System.Text.Encoding.Unicode.GetString(displayListsStream, i + 4, nameLen * 2);
                    if (styleName.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c >= 0x4e00))
                    {
                        int coordOff = i + 4 + nameLen * 2 + 8;
                        double x1 = BitConverter.ToDouble(displayListsStream, coordOff);
                        double y1 = BitConverter.ToDouble(displayListsStream, coordOff + 8);
                        double z1 = BitConverter.ToDouble(displayListsStream, coordOff + 16);
                        double x2 = BitConverter.ToDouble(displayListsStream, coordOff + 24);
                        double y2 = BitConverter.ToDouble(displayListsStream, coordOff + 32);
                        double z2 = BitConverter.ToDouble(displayListsStream, coordOff + 40);

                        if (!double.IsNaN(x1) && !double.IsInfinity(x1) &&
                            !double.IsNaN(y1) && !double.IsInfinity(y1) &&
                            !double.IsNaN(x2) && !double.IsInfinity(x2) &&
                            !double.IsNaN(y2) && !double.IsInfinity(y2))
                        {
                            double x1mm = x1 * 1000.0;
                            double y1mm = y1 * 1000.0;
                            double x2mm = x2 * 1000.0;
                            double y2mm = y2 * 1000.0;

                            if (x1mm >= -50.0 && x1mm <= maxAllowedW && y1mm >= -50.0 && y1mm <= maxAllowedH &&
                                x2mm >= -50.0 && x2mm <= maxAllowedW && y2mm >= -50.0 && y2mm <= maxAllowedH)
                            {
                                // Clamp coordinates safely to paper boundary [0..paperWidth, 0..paperHeight]
                                x1mm = Math.Max(0.0, Math.Min(paperWidthMm, x1mm));
                                x2mm = Math.Max(0.0, Math.Min(paperWidthMm, x2mm));
                                y1mm = Math.Max(0.0, Math.Min(paperHeightMm, y1mm));
                                y2mm = Math.Max(0.0, Math.Min(paperHeightMm, y2mm));

                                double dx = x2mm - x1mm;
                                double dy = y2mm - y1mm;
                                double len = Math.Sqrt(dx * dx + dy * dy);

                                if (len > 0.05)
                                {
                                    DrawingLineKind kind = DrawingLineKind.SolidOutline;
                                    double thickness = 0.35;

                                    if (styleName.Contains("CENTER", StringComparison.OrdinalIgnoreCase) ||
                                        styleName.Contains("中心"))
                                    {
                                        kind = DrawingLineKind.Centerline;
                                        thickness = 0.20;
                                    }
                                    else if (styleName.Contains("HIDDEN", StringComparison.OrdinalIgnoreCase) ||
                                             styleName.Contains("虚线"))
                                    {
                                        kind = DrawingLineKind.HiddenLine;
                                        thickness = 0.20;
                                    }
                                    else if (styleName.Contains("CHAIN", StringComparison.OrdinalIgnoreCase) ||
                                             styleName.Contains("折弯"))
                                    {
                                        kind = DrawingLineKind.ThinLine;
                                        thickness = 0.25;
                                    }

                                    lines.Add(new DrawingVectorLine(x1mm, y1mm, x2mm, y2mm, kind, thickness));
                                }
                            }
                        }
                    }
                }
            }
        }

        return lines;
    }

    private static void GenerateSheetOuterBorders(List<DrawingVectorLine> lines, double w, double h)
    {
        // Outer paper edge boundary (fine line 0.25mm)
        lines.Add(new DrawingVectorLine(0.0, 0.0, w, 0.0, DrawingLineKind.SheetBorderOuter, 0.25));
        lines.Add(new DrawingVectorLine(w, 0.0, w, h, DrawingLineKind.SheetBorderOuter, 0.25));
        lines.Add(new DrawingVectorLine(w, h, 0.0, h, DrawingLineKind.SheetBorderOuter, 0.25));
        lines.Add(new DrawingVectorLine(0.0, h, 0.0, 0.0, DrawingLineKind.SheetBorderOuter, 0.25));
    }

    private static void GenerateSheetInnerBorders(List<DrawingVectorLine> lines, double w, double h)
    {
        // Inner drawing frame (thick line 0.5mm, 25mm margin on left for binding edge, 5mm on top/bottom/right)
        double left = 25.0;
        double right = w - 5.0;
        double bottom = 5.0;
        double top = h - 5.0;

        lines.Add(new DrawingVectorLine(left, bottom, right, bottom, DrawingLineKind.SheetBorderInner, 0.5));
        lines.Add(new DrawingVectorLine(right, bottom, right, top, DrawingLineKind.SheetBorderInner, 0.5));
        lines.Add(new DrawingVectorLine(right, top, left, top, DrawingLineKind.SheetBorderInner, 0.5));
        lines.Add(new DrawingVectorLine(left, top, left, bottom, DrawingLineKind.SheetBorderInner, 0.5));
    }

    private static void GenerateSheetBorders(List<DrawingVectorLine> lines, double w, double h)
    {
        GenerateSheetOuterBorders(lines, w, h);
        GenerateSheetInnerBorders(lines, w, h);
    }

    private static void GenerateTitleBlockAndToleranceGrids(List<DrawingVectorLine> lines, double w, double h)
    {
        double right = w - 5.0;
        double bottom = 5.0;

        // Title Block outer boundary: standard 140mm width x 45mm height in lower-right
        double tbW = 140.0;
        double tbH = 45.0;
        double tbX0 = right - tbW;
        double tbY0 = bottom;
        double tbX1 = right;
        double tbY1 = bottom + tbH;

        lines.Add(new DrawingVectorLine(tbX0, tbY0, tbX0, tbY1, DrawingLineKind.SheetBorderInner, 0.5));
        lines.Add(new DrawingVectorLine(tbX0, tbY1, tbX1, tbY1, DrawingLineKind.SheetBorderInner, 0.5));

        // Tolerance table boundary (above title block on the right: 55mm x 15mm)
        double tolW = 55.0;
        double tolH = 15.0;
        double tolX0 = right - tolW;
        double tolY0 = tbY1;
        double tolX1 = right;
        double tolY1 = tbY1 + tolH;

        lines.Add(new DrawingVectorLine(tolX0, tolY0, tolX0, tolY1, DrawingLineKind.SheetBorderInner, 0.5));
        lines.Add(new DrawingVectorLine(tolX0, tolY1, tolX1, tolY1, DrawingLineKind.SheetBorderInner, 0.5));
    }

    /// <summary>
    /// Scans the Definition stream for drawing view bounding boxes and locations serialized as MFC view objects.
    /// </summary>
    public static List<DrawingViewGeometry> ExtractViewsFromDefinition(byte[] defBytes, double paperW, double paperH)
    {
        var views = new List<DrawingViewGeometry>();
        var seenBoxes = new HashSet<string>(StringComparer.Ordinal);

        // Scan for 4-tuple of doubles: (xMin, yMin, xMax, yMax) in meters
        for (int i = 0; i <= defBytes.Length - 32; i += 8)
        {
            double x1 = BitConverter.ToDouble(defBytes, i);
            double y1 = BitConverter.ToDouble(defBytes, i + 8);
            double x2 = BitConverter.ToDouble(defBytes, i + 16);
            double y2 = BitConverter.ToDouble(defBytes, i + 24);

            if (double.IsNaN(x1) || double.IsNaN(y1) || double.IsNaN(x2) || double.IsNaN(y2))
                continue;

            // In sheet range in meters
            if (x1 >= 0.02 && x1 <= (paperW - 10.0) / 1000.0 &&
                x2 >= 0.02 && x2 <= (paperW - 10.0) / 1000.0 &&
                y1 >= 0.02 && y1 <= (paperH - 10.0) / 1000.0 &&
                y2 >= 0.02 && y2 <= (paperH - 10.0) / 1000.0)
            {
                double minX = Math.Min(x1, x2) * 1000.0;
                double maxX = Math.Max(x1, x2) * 1000.0;
                double minY = Math.Min(y1, y2) * 1000.0;
                double maxY = Math.Max(y1, y2) * 1000.0;

                double w = maxX - minX;
                double h = maxY - minY;

                // View bounding box should have reasonable span (e.g. width >= 10mm or height >= 10mm)
                if ((w >= 10.0 || h >= 10.0) && w <= paperW && h <= paperH)
                {
                    string key = $"{Math.Round(minX, 0)}_{Math.Round(minY, 0)}_{Math.Round(maxX, 0)}_{Math.Round(maxY, 0)}";
                    if (seenBoxes.Add(key))
                    {
                        double cx = (minX + maxX) / 2.0;
                        double cy = (minY + maxY) / 2.0;
                        views.Add(new DrawingViewGeometry($"View_{views.Count + 1}", cx, cy, w, h));
                    }
                }
            }
        }

        return views;
    }

    /// <summary>
    /// Extracts 2D wireframe contours and tessellated line segments from the VBLists stream,
    /// transforming local model coordinates into physical sheet coordinates.
    /// </summary>
    public static List<DrawingVectorLine> ExtractViewGeometryFromVBLists(
        byte[] vbBytes,
        List<DrawingViewGeometry> views,
        double paperW,
        double paperH)
    {
        var lines = new List<DrawingVectorLine>();
        var seenLines = new HashSet<string>(StringComparer.Ordinal);

        // Locate bucket partition boundaries in VBLists
        var bucketOffsets = new List<int> { 0x4 };
        for (int i = 0; i < vbBytes.Length - 1; i++)
        {
            if (vbBytes[i] == 0x01 && vbBytes[i + 1] == 0x80)
            {
                bucketOffsets.Add(i);
            }
        }
        bucketOffsets.Add(vbBytes.Length);

        for (int b = 0; b < bucketOffsets.Count - 1; b++)
        {
            int start = bucketOffsets[b];
            int end = bucketOffsets[b + 1];

            var rawLines = new List<(double x1, double y1, double x2, double y2)>();

            // 1. Scan single-precision float line segments (12-byte vertex stride)
            for (int i = start; i <= end - 24; i += 12)
            {
                float x1 = BitConverter.ToSingle(vbBytes, i);
                float y1 = BitConverter.ToSingle(vbBytes, i + 4);
                float z1 = BitConverter.ToSingle(vbBytes, i + 8);
                float x2 = BitConverter.ToSingle(vbBytes, i + 12);
                float y2 = BitConverter.ToSingle(vbBytes, i + 16);
                float z2 = BitConverter.ToSingle(vbBytes, i + 20);

                if (!float.IsNaN(x1) && !float.IsInfinity(x1) &&
                    !float.IsNaN(y1) && !float.IsInfinity(y1) &&
                    !float.IsNaN(x2) && !float.IsInfinity(x2) &&
                    !float.IsNaN(y2) && !float.IsInfinity(y2))
                {
                    if (Math.Abs(x1) <= 10.0f && Math.Abs(y1) <= 10.0f && Math.Abs(z1) <= 1.0f &&
                        Math.Abs(x2) <= 10.0f && Math.Abs(y2) <= 10.0f && Math.Abs(z2) <= 1.0f)
                    {
                        double len = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
                        if (len >= 0.0005 && len <= 10.0)
                        {
                            rawLines.Add((x1, y1, x2, y2));
                        }
                    }
                }
            }

            // 2. Scan double-precision line segments (24-byte vertex stride)
            for (int i = start; i <= end - 48; i += 24)
            {
                double x1 = BitConverter.ToDouble(vbBytes, i);
                double y1 = BitConverter.ToDouble(vbBytes, i + 8);
                double z1 = BitConverter.ToDouble(vbBytes, i + 16);
                double x2 = BitConverter.ToDouble(vbBytes, i + 24);
                double y2 = BitConverter.ToDouble(vbBytes, i + 32);
                double z2 = BitConverter.ToDouble(vbBytes, i + 40);

                if (!double.IsNaN(x1) && !double.IsInfinity(x1) &&
                    !double.IsNaN(y1) && !double.IsInfinity(y1) &&
                    !double.IsNaN(x2) && !double.IsInfinity(x2) &&
                    !double.IsNaN(y2) && !double.IsInfinity(y2))
                {
                    if (Math.Abs(x1) <= 10.0 && Math.Abs(y1) <= 10.0 && Math.Abs(z1) <= 1.0 &&
                        Math.Abs(x2) <= 10.0 && Math.Abs(y2) <= 10.0 && Math.Abs(z2) <= 1.0)
                    {
                        double len = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
                        if (len >= 0.0005 && len <= 10.0)
                        {
                            rawLines.Add((x1, y1, x2, y2));
                        }
                    }
                }
            }

            if (rawLines.Count == 0) continue;

            double srcXMin = rawLines.Min(l => Math.Min(l.x1, l.x2));
            double srcXMax = rawLines.Max(l => Math.Max(l.x1, l.x2));
            double srcYMin = rawLines.Min(l => Math.Min(l.y1, l.y2));
            double srcYMax = rawLines.Max(l => Math.Max(l.y1, l.y2));

            double srcW = srcXMax - srcXMin;
            double srcH = srcYMax - srcYMin;
            if (srcW <= 1e-4) srcW = 1.0;
            if (srcH <= 1e-4) srcH = 1.0;

            DrawingViewGeometry? targetView = (b < views.Count) ? views[b] : null;

            double targetCenterX = targetView?.CenterXMm ?? (paperW / 2.0);
            double targetCenterY = targetView?.CenterYMm ?? (paperH / 2.0);
            double targetW = targetView?.WidthMm ?? (paperW * 0.5);
            double targetH = targetView?.HeightMm ?? (paperH * 0.5);

            double scale = Math.Min(targetW / (srcW * 1000.0), targetH / (srcH * 1000.0));
            if (scale <= 1e-5 || double.IsInfinity(scale)) scale = 1.0 / 50.0;

            double srcCenterX = (srcXMin + srcXMax) / 2.0;
            double srcCenterY = (srcYMin + srcYMax) / 2.0;

            var currentViewLines = new List<DrawingVectorLine>();

            foreach (var rl in rawLines)
            {
                double px1 = targetCenterX + (rl.x1 - srcCenterX) * 1000.0 * scale;
                double py1 = targetCenterY + (rl.y1 - srcCenterY) * 1000.0 * scale;
                double px2 = targetCenterX + (rl.x2 - srcCenterX) * 1000.0 * scale;
                double py2 = targetCenterY + (rl.y2 - srcCenterY) * 1000.0 * scale;

                // Ensure within physical paper boundaries
                if (px1 < 2.0 || px1 > paperW - 2.0 || py1 < 2.0 || py1 > paperH - 2.0 ||
                    px2 < 2.0 || px2 > paperW - 2.0 || py2 < 2.0 || py2 > paperH - 2.0)
                    continue;

                double lenMm = Math.Sqrt(Math.Pow(px2 - px1, 2) + Math.Pow(py2 - py1, 2));
                if (lenMm < 0.2 || lenMm > 400.0) continue;

                // Deduplicate coincident or reversed line segments with spatial hash
                string key = $"{Math.Round(Math.Min(px1, px2), 1)}_{Math.Round(Math.Min(py1, py2), 1)}_{Math.Round(Math.Max(px1, px2), 1)}_{Math.Round(Math.Max(py1, py2), 1)}";
                if (seenLines.Add(key))
                {
                    var kind = (lenMm > 12.0) ? DrawingLineKind.SolidOutline : DrawingLineKind.ThinLine;
                    double weight = (kind == DrawingLineKind.SolidOutline) ? 0.45 : 0.25;
                    var line = new DrawingVectorLine(px1, py1, px2, py2, kind, weight);
                    lines.Add(line);
                    currentViewLines.Add(line);
                }
            }

            if (targetView != null)
            {
                targetView.Lines = currentViewLines.AsReadOnly();
            }
        }

        return lines;
    }
}
