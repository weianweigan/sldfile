using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Drawing.Vector;

namespace SldFile.Drawing.Export;

/// <summary>
/// Generates industry-standard AutoCAD ASCII DXF (AC1009 / R12) exchange files from 2D CAD drawing sheets.
/// Compatible with all major CAD, CAM, laser cutting, and CNC machining software.
/// </summary>
public static class DrawingDxfWriter
{
    /// <summary>
    /// Writes the drawing sheet vector data as an AutoCAD DXF string.
    /// </summary>
    public static string Write(DrawingSheetVectorData vectorData, string sheetName = "Sheet1")
    {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb, CultureInfo.InvariantCulture);
        Write(vectorData, writer, sheetName);
        return sb.ToString();
    }

    /// <summary>
    /// Writes the drawing sheet vector data to the specified TextWriter in AutoCAD ASCII DXF format.
    /// </summary>
    public static void Write(DrawingSheetVectorData vectorData, TextWriter writer, string sheetName = "Sheet1")
    {
        if (vectorData == null) throw new ArgumentNullException(nameof(vectorData));
        if (writer == null) throw new ArgumentNullException(nameof(writer));

        double widthMm = vectorData.PaperWidthMm > 10.0 ? vectorData.PaperWidthMm : 297.0;
        double heightMm = vectorData.PaperHeightMm > 10.0 ? vectorData.PaperHeightMm : 210.0;

        // 1. HEADER SECTION
        writer.WriteLine("0");
        writer.WriteLine("SECTION");
        writer.WriteLine("2");
        writer.WriteLine("HEADER");

        writer.WriteLine("9");
        writer.WriteLine("$ACADVER");
        writer.WriteLine("1");
        writer.WriteLine("AC1015"); // AutoCAD 2000 standard (supports \U+ Unicode escapes & DWGCODEPAGE)

        writer.WriteLine("9");
        writer.WriteLine("$DWGCODEPAGE");
        writer.WriteLine("3");
        writer.WriteLine("ANSI_936");

        writer.WriteLine("9");
        writer.WriteLine("$INSUNITS");
        writer.WriteLine("70");
        writer.WriteLine("4"); // 4 = Millimeters

        writer.WriteLine("9");
        writer.WriteLine("$EXTMIN");
        writer.WriteLine("10");
        writer.WriteLine("0.0");
        writer.WriteLine("20");
        writer.WriteLine("0.0");
        writer.WriteLine("30");
        writer.WriteLine("0.0");

        writer.WriteLine("9");
        writer.WriteLine("$EXTMAX");
        writer.WriteLine("10");
        writer.WriteLine(widthMm.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("20");
        writer.WriteLine(heightMm.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("30");
        writer.WriteLine("0.0");

        writer.WriteLine("0");
        writer.WriteLine("ENDSEC");

        // 2. TABLES SECTION (Linetypes and Layers)
        writer.WriteLine("0");
        writer.WriteLine("SECTION");
        writer.WriteLine("2");
        writer.WriteLine("TABLES");

        // 2.1 Linetype Table
        writer.WriteLine("0");
        writer.WriteLine("TABLE");
        writer.WriteLine("2");
        writer.WriteLine("LTYPE");
        writer.WriteLine("70");
        writer.WriteLine("3");

        WriteLinetype(writer, "CONTINUOUS", "Solid line");
        WriteLinetype(writer, "HIDDEN", "Hidden __ __ __", 5.0, new[] { 3.0, -2.0 });
        WriteLinetype(writer, "CENTER", "Center ____ _ ____ _", 14.0, new[] { 9.0, -1.5, 2.0, -1.5 });

        writer.WriteLine("0");
        writer.WriteLine("ENDTAB");

        // 2.2 Layer Table
        writer.WriteLine("0");
        writer.WriteLine("TABLE");
        writer.WriteLine("2");
        writer.WriteLine("LAYER");
        writer.WriteLine("70");
        writer.WriteLine("7");

        WriteLayer(writer, "0", 7, "CONTINUOUS");
        WriteLayer(writer, "BORDER", 7, "CONTINUOUS");      // 7 = White/Black
        WriteLayer(writer, "VISIBLE", 7, "CONTINUOUS");     // Model solid outline
        WriteLayer(writer, "THIN", 7, "CONTINUOUS");        // Internal features/hatching
        WriteLayer(writer, "HIDDEN", 8, "HIDDEN");          // 8 = Gray
        WriteLayer(writer, "CENTER", 1, "CENTER");          // 1 = Red
        WriteLayer(writer, "DIMENSION", 5, "CONTINUOUS");   // 5 = Blue
        WriteLayer(writer, "ANNOTATIONS", 7, "CONTINUOUS"); // Notes & Text

        writer.WriteLine("0");
        writer.WriteLine("ENDTAB");

        // 2.3 Style Table (Text Styles for universal Chinese & CAD annotations)
        writer.WriteLine("0");
        writer.WriteLine("TABLE");
        writer.WriteLine("2");
        writer.WriteLine("STYLE");
        writer.WriteLine("70");
        writer.WriteLine("1");

        writer.WriteLine("0");
        writer.WriteLine("STYLE");
        writer.WriteLine("2");
        writer.WriteLine("STANDARD");
        writer.WriteLine("70");
        writer.WriteLine("0");
        writer.WriteLine("40");
        writer.WriteLine("0.0"); // 0 = flexible variable height
        writer.WriteLine("41");
        writer.WriteLine("1.0"); // Width factor
        writer.WriteLine("50");
        writer.WriteLine("0.0");
        writer.WriteLine("71");
        writer.WriteLine("0");
        writer.WriteLine("42");
        writer.WriteLine("2.5");
        writer.WriteLine("3");
        writer.WriteLine("simplex.shx");
        writer.WriteLine("4");
        writer.WriteLine("gbcbig.shx");
        writer.WriteLine("1001");
        writer.WriteLine("ACAD");
        writer.WriteLine("1000");
        writer.WriteLine("SimSun");
        writer.WriteLine("1071");
        writer.WriteLine("0");

        writer.WriteLine("0");
        writer.WriteLine("ENDTAB");

        writer.WriteLine("0");
        writer.WriteLine("ENDSEC");

        // 3. BLOCKS SECTION (Empty for simple entity interchange)
        writer.WriteLine("0");
        writer.WriteLine("SECTION");
        writer.WriteLine("2");
        writer.WriteLine("BLOCKS");
        writer.WriteLine("0");
        writer.WriteLine("ENDSEC");

        // 4. ENTITIES SECTION
        writer.WriteLine("0");
        writer.WriteLine("SECTION");
        writer.WriteLine("2");
        writer.WriteLine("ENTITIES");

        // 4.1 Write Vector Lines
        if (vectorData.Lines != null)
        {
            foreach (var line in vectorData.Lines)
            {
                string layer = line.Kind switch
                {
                    DrawingLineKind.SheetBorderOuter or DrawingLineKind.SheetBorderInner or DrawingLineKind.TableGrid => "BORDER",
                    DrawingLineKind.SolidOutline => "VISIBLE",
                    DrawingLineKind.Centerline => "CENTER",
                    DrawingLineKind.HiddenLine => "HIDDEN",
                    DrawingLineKind.DimensionLine => "DIMENSION",
                    _ => "THIN"
                };

                WriteLineEntity(writer, layer, line.X1, line.Y1, line.X2, line.Y2);
            }
        }

        // 4.2 Write Vector Arrowheads (SOLID entities on DIMENSION layer)
        if (vectorData.Arrows != null)
        {
            foreach (var arrow in vectorData.Arrows)
            {
                WriteArrowSolid(writer, "DIMENSION", arrow.TipX, arrow.TipY, arrow.DirX, arrow.DirY, arrow.LengthMm, arrow.HalfWidthMm);
            }
        }

        // 4.3 Write Vector Text Entities
        if (vectorData.Texts != null)
        {
            foreach (var text in vectorData.Texts)
            {
                if (string.IsNullOrWhiteSpace(text.CleanText)) continue;
                if (text.Category == DrawingVectorTextCategory.Metadata) continue;

                string layer = (text.Category == DrawingVectorTextCategory.ViewAnnotation) ? "DIMENSION" : "ANNOTATIONS";
                double height = text.HeightMm > 0.5 ? text.HeightMm : 2.5;
                double rotationDeg = text.AngleRad * (180.0 / Math.PI);

                WriteTextEntity(writer, layer, text.CleanText, text.X, text.Y, height, rotationDeg);
            }
        }

        writer.WriteLine("0");
        writer.WriteLine("ENDSEC");

        // 5. EOF MARKER
        writer.WriteLine("0");
        writer.WriteLine("EOF");
    }

    private static void WriteLineEntity(TextWriter writer, string layer, double x1, double y1, double x2, double y2)
    {
        writer.WriteLine("0");
        writer.WriteLine("LINE");
        writer.WriteLine("8");
        writer.WriteLine(layer);
        writer.WriteLine("10");
        writer.WriteLine(x1.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("20");
        writer.WriteLine(y1.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("30");
        writer.WriteLine("0.0");
        writer.WriteLine("11");
        writer.WriteLine(x2.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("21");
        writer.WriteLine(y2.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("31");
        writer.WriteLine("0.0");
    }

    private static void WriteTextEntity(TextWriter writer, string layer, string text, double x, double y, double height, double rotationDeg)
    {
        writer.WriteLine("0");
        writer.WriteLine("TEXT");
        writer.WriteLine("8");
        writer.WriteLine(layer);
        writer.WriteLine("7");
        writer.WriteLine("STANDARD");
        writer.WriteLine("10");
        writer.WriteLine(x.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("20");
        writer.WriteLine(y.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("30");
        writer.WriteLine("0.0");
        writer.WriteLine("40");
        writer.WriteLine(height.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("1");
        writer.WriteLine(SanitizeDxfText(text));

        if (Math.Abs(rotationDeg) > 1e-3)
        {
            writer.WriteLine("50");
            writer.WriteLine(rotationDeg.ToString("F2", CultureInfo.InvariantCulture));
        }
    }

    private static void WriteArrowSolid(TextWriter writer, string layer, double tipX, double tipY, double dirX, double dirY, double lengthMm, double halfWidthMm)
    {
        double len = lengthMm > 0.5 ? lengthMm : 3.5;
        double halfW = halfWidthMm > 0.1 ? halfWidthMm : 0.45;

        // Base center point
        double baseX = tipX - dirX * len;
        double baseY = tipY - dirY * len;

        // Perpendicular vector
        double perpX = -dirY;
        double perpY = dirX;

        double v1X = baseX + perpX * halfW;
        double v1Y = baseY + perpY * halfW;

        double v2X = baseX - perpX * halfW;
        double v2Y = baseY - perpY * halfW;

        writer.WriteLine("0");
        writer.WriteLine("SOLID");
        writer.WriteLine("8");
        writer.WriteLine(layer);
        writer.WriteLine("10");
        writer.WriteLine(tipX.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("20");
        writer.WriteLine(tipY.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("30");
        writer.WriteLine("0.0");
        writer.WriteLine("11");
        writer.WriteLine(v1X.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("21");
        writer.WriteLine(v1Y.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("31");
        writer.WriteLine("0.0");
        writer.WriteLine("12");
        writer.WriteLine(v2X.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("22");
        writer.WriteLine(v2Y.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("32");
        writer.WriteLine("0.0");
        writer.WriteLine("13");
        writer.WriteLine(v2X.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("23");
        writer.WriteLine(v2Y.ToString("F3", CultureInfo.InvariantCulture));
        writer.WriteLine("33");
        writer.WriteLine("0.0");
    }

    private static string SanitizeDxfText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var clean = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        var sb = new StringBuilder(clean.Length * 2);
        foreach (char c in clean)
        {
            if (c > 127)
            {
                // AutoCAD universal Unicode escape sequence \U+XXXX (works across all AutoCAD & CAD versions)
                sb.AppendFormat("\\U+{0:X4}", (int)c);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static void WriteLayer(TextWriter writer, string name, int colorCode, string linetype)
    {
        writer.WriteLine("0");
        writer.WriteLine("LAYER");
        writer.WriteLine("2");
        writer.WriteLine(name);
        writer.WriteLine("70");
        writer.WriteLine("0");
        writer.WriteLine("62");
        writer.WriteLine(colorCode.ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("6");
        writer.WriteLine(linetype);
    }

    private static void WriteLinetype(TextWriter writer, string name, string description, double totalLength = 0.0, double[]? dashes = null)
    {
        writer.WriteLine("0");
        writer.WriteLine("LTYPE");
        writer.WriteLine("2");
        writer.WriteLine(name);
        writer.WriteLine("70");
        writer.WriteLine("0");
        writer.WriteLine("3");
        writer.WriteLine(description);
        writer.WriteLine("72");
        writer.WriteLine("65"); // Alignment code 'A'
        writer.WriteLine("73");
        writer.WriteLine((dashes?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
        writer.WriteLine("40");
        writer.WriteLine(totalLength.ToString("F3", CultureInfo.InvariantCulture));

        if (dashes != null)
        {
            foreach (var d in dashes)
            {
                writer.WriteLine("49");
                writer.WriteLine(d.ToString("F3", CultureInfo.InvariantCulture));
            }
        }
    }
}
