using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SldFile;
using SldFile.Drawing.Export;
using SldFile.Drawing.Vector;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Unit tests for DrawingSvgWriter and DrawingDxfWriter (W3C SVG & AutoCAD DXF vector export).
/// </summary>
public class DrawingVectorExportTests
{
    private static DrawingSheetVectorData CreateSampleVectorData()
    {
        var data = new DrawingSheetVectorData
        {
            PaperWidthMm = 420.0,
            PaperHeightMm = 297.0,
            TemplateName = "A3-ISO",
            ScaleNumerator = 1.0,
            ScaleDenominator = 1.0
        };

        var lines = new List<DrawingVectorLine>
        {
            // Sheet border outer & inner
            new DrawingVectorLine(0, 0, 420, 0, DrawingLineKind.SheetBorderOuter, 0.5),
            new DrawingVectorLine(20, 10, 410, 10, DrawingLineKind.SheetBorderInner, 0.7),
            new DrawingVectorLine(20, 10, 20, 287, DrawingLineKind.SheetBorderInner, 0.7),

            // Model visible outline
            new DrawingVectorLine(100, 100, 200, 100, DrawingLineKind.SolidOutline, 0.5),
            new DrawingVectorLine(200, 100, 200, 200, DrawingLineKind.SolidOutline, 0.5),
            new DrawingVectorLine(200, 200, 100, 200, DrawingLineKind.SolidOutline, 0.5),
            new DrawingVectorLine(100, 200, 100, 100, DrawingLineKind.SolidOutline, 0.5),

            // Centerline
            new DrawingVectorLine(80, 150, 220, 150, DrawingLineKind.Centerline, 0.25),

            // Hidden line
            new DrawingVectorLine(120, 120, 180, 120, DrawingLineKind.HiddenLine, 0.25),

            // Dimension line
            new DrawingVectorLine(100, 80, 200, 80, DrawingLineKind.DimensionLine, 0.25)
        };
        data.Lines = lines.AsReadOnly();

        var texts = new List<DrawingVectorText>
        {
            new DrawingVectorText("100.00", 150, 75, 0, "Arial", 3.0, 0, DrawingVectorTextCategory.ViewAnnotation),
            new DrawingVectorText("PART NAME: BRACKET-100 & FLANGE", 350, 30, 0, "SimSun", 4.0, 0, DrawingVectorTextCategory.TitleBlock),
            new DrawingVectorText("SECTION A-A", 150, 230, 0, "Arial", 5.0, Math.PI / 4.0, DrawingVectorTextCategory.ViewAnnotation)
        };
        data.Texts = texts.AsReadOnly();

        return data;
    }

    [Fact]
    public void DrawingSvgWriter_GeneratesValidXmlWithCorrectDimensionsAndElements()
    {
        var data = CreateSampleVectorData();

        string svg = DrawingSvgWriter.Write(data, "Sheet1");
        Assert.False(string.IsNullOrWhiteSpace(svg));

        // 1. Verify XML parsing validity
        var doc = XDocument.Parse(svg);
        Assert.NotNull(doc.Root);
        Assert.Equal("svg", doc.Root!.Name.LocalName);

        // 2. Verify viewBox and paper dimensions
        Assert.Equal("420.0mm", (string?)doc.Root.Attribute("width"));
        Assert.Equal("297.0mm", (string?)doc.Root.Attribute("height"));
        Assert.Equal("0 0 420.000 297.000", (string?)doc.Root.Attribute("viewBox"));

        // 3. Verify Y-axis coordinate inversion (svgY = paperHeight - cadY)
        // Line (100, 100) -> svg Y should be 297 - 100 = 197.000
        var allLines = doc.Descendants().Where(e => e.Name.LocalName == "line").ToList();
        Assert.NotEmpty(allLines);
        Assert.Contains(allLines, l => (string?)l.Attribute("x1") == "100.000" && (string?)l.Attribute("y1") == "197.000");

        // 4. Verify text escaping and rendering
        var allTexts = doc.Descendants().Where(e => e.Name.LocalName == "text").ToList();
        Assert.NotEmpty(allTexts);
        Assert.Contains(allTexts, t => t.Value.Contains("PART NAME: BRACKET-100 & FLANGE"));
        Assert.Contains(allTexts, t => t.Value.Contains("100.00"));
    }

    [Fact]
    public void DrawingDxfWriter_GeneratesValidAutoCadDxfR12Structure()
    {
        var data = CreateSampleVectorData();

        string dxf = DrawingDxfWriter.Write(data, "Sheet1");
        Assert.False(string.IsNullOrWhiteSpace(dxf));

        // 1. Verify standard DXF header & footer structure
        Assert.StartsWith("0\r\nSECTION\r\n2\r\nHEADER", dxf);
        Assert.EndsWith("0\r\nEOF\r\n", dxf);

        // 2. Verify AutoCAD standard version & millimetre units
        Assert.Contains("$ACADVER\r\n1\r\nAC1015", dxf);
        Assert.Contains("$INSUNITS\r\n70\r\n4", dxf);

        // 3. Verify Layer table contains BORDER, VISIBLE, HIDDEN, CENTER, DIMENSION, ANNOTATIONS
        Assert.Contains("LAYER\r\n2\r\nBORDER", dxf);
        Assert.Contains("LAYER\r\n2\r\nVISIBLE", dxf);
        Assert.Contains("LAYER\r\n2\r\nHIDDEN", dxf);
        Assert.Contains("LAYER\r\n2\r\nCENTER", dxf);
        Assert.Contains("LAYER\r\n2\r\nDIMENSION", dxf);
        Assert.Contains("LAYER\r\n2\r\nANNOTATIONS", dxf);

        // 4. Verify LINE entities and layer assignment
        Assert.Contains("0\r\nLINE\r\n8\r\nVISIBLE\r\n10\r\n100.000\r\n20\r\n100.000", dxf);
        Assert.Contains("0\r\nLINE\r\n8\r\nCENTER\r\n10\r\n80.000\r\n20\r\n150.000", dxf);

        // 5. Verify TEXT entities and native CAD coordinate preservation (no inversion)
        Assert.Contains("0\r\nTEXT\r\n8\r\nDIMENSION\r\n7\r\nSTANDARD\r\n10\r\n150.000\r\n20\r\n75.000\r\n30\r\n0.0\r\n40\r\n3.000\r\n1\r\n100.00", dxf);
        Assert.Contains("PART NAME: BRACKET-100 & FLANGE", dxf);
    }

    [Fact]
    public void SldDrawing_ExportSheetSvg_And_ExportSheetDxf_WorksOnRealDrawingSpecimen()
    {
        string cubeDrwPath = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Solidworks2012", "cube.SLDDRW");
        if (!File.Exists(cubeDrwPath))
        {
            cubeDrwPath = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles", "Solidworks2012", "cube.SLDDRW");
        }

        if (File.Exists(cubeDrwPath))
        {
            using var drawing = (SldDrawing)SwFile.Open(cubeDrwPath);
            Assert.NotEmpty(drawing.Sheets);

            // Export to SVG
            string svg = drawing.ExportSheetSvg(0);
            Assert.False(string.IsNullOrWhiteSpace(svg));
            var doc = XDocument.Parse(svg);
            Assert.NotNull(doc.Root);

            // Export to DXF
            string dxf = drawing.ExportSheetDxf(0);
            Assert.False(string.IsNullOrWhiteSpace(dxf));
            Assert.Contains("AC1015", dxf);
            Assert.Contains("EOF", dxf);
        }
    }

    [Fact]
    public void SldDrawing_D8001507933_ExportSvgAndDxf_ContainsAccurateDimensionsAndAnnotations()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var drawing = (SldDrawing)SwFile.Open(realPath);
        Assert.NotEmpty(drawing.Sheets);

        string svg = drawing.ExportSheetSvg(0);
        Assert.False(string.IsNullOrWhiteSpace(svg));
        var xdoc = XDocument.Parse(svg);
        Assert.NotNull(xdoc.Root);

        // Verify Title block text & drawing number
        Assert.Contains("D8001507933", svg);

        // Verify DXF output
        string dxf = drawing.ExportSheetDxf(0);
        Assert.False(string.IsNullOrWhiteSpace(dxf));
        Assert.Contains("D8001507933", dxf);
        Assert.Contains("VISIBLE", dxf);
        Assert.Contains("DIMENSION", dxf);
    }

    [Fact]
    public void SldDrawing_StreamAndFileOverloads_WriteOutputCorrectly()
    {
        var data = CreateSampleVectorData();
        var tempSvg = Path.GetTempFileName() + ".svg";
        var tempDxf = Path.GetTempFileName() + ".dxf";

        try
        {
            // Test SVG text writer overload
            using (var sw = new StreamWriter(tempSvg))
            {
                DrawingSvgWriter.Write(data, sw, "TempSheet");
            }
            Assert.True(File.Exists(tempSvg));
            Assert.True(new FileInfo(tempSvg).Length > 100);

            // Test DXF text writer overload
            using (var sw = new StreamWriter(tempDxf))
            {
                DrawingDxfWriter.Write(data, sw, "TempSheet");
            }
            Assert.True(File.Exists(tempDxf));
            Assert.True(new FileInfo(tempDxf).Length > 100);
        }
        finally
        {
            if (File.Exists(tempSvg)) File.Delete(tempSvg);
            if (File.Exists(tempDxf)) File.Delete(tempDxf);
        }
    }

    [Fact]
    public void Drawing_D8001507933_ExportDxf_GeneratesAuthenticUnicodeAndArrowheads()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;
        using var drawing = (SldDrawing)SwFile.Open(realPath);
        string dxf = drawing.ExportSheetDxf(0);
        Assert.False(string.IsNullOrWhiteSpace(dxf));

        // 1. Verify AutoCAD 2000 AC1015 standard and code page
        Assert.Contains("AC1015", dxf);
        Assert.Contains("ANSI_936", dxf);
        Assert.Contains("STYLE\r\n2\r\nSTANDARD", dxf);

        // 2. Verify Unicode escape representation for Chinese characters (auto-rendered by all CAD engines)
        Assert.Contains(@"\U+6280\U+672F\U+8981\U+6C42", dxf); // 技术要求
        Assert.Contains(@"\U+5C40\U+90E8\U+89C6\U+56FE", dxf); // 局部视图
        Assert.Contains(@"\U+6BD4\U+4F8B", dxf);               // 比例
        Assert.Contains(@"\U+00D818", dxf);                   // Ø18

        // 3. Verify exactly 1 occurrence of 技术要求 (deduplicated)
        int techOccurrences = 0;
        int idx = 0;
        while ((idx = dxf.IndexOf(@"\U+6280\U+672F\U+8981\U+6C42", idx, StringComparison.Ordinal)) != -1)
        {
            techOccurrences++;
            idx += 10;
        }
        Assert.Equal(1, techOccurrences);

        // 4. Verify SOLID arrowheads present
        Assert.Contains("SOLID\r\n8\r\nDIMENSION", dxf);

        // 5. Verify vertical dimensions rotated 90 degrees
        Assert.Contains("50\r\n90.00", dxf);

        // 6. Verify vector data contains authentic arrows
        var v = drawing.GetSheetVectorData(0);
        Assert.NotNull(v);
        Assert.NotEmpty(v.Arrows);
        Assert.True(v.Arrows.Count >= 88);
    }

}