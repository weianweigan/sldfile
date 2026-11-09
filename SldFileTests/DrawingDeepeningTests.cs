using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

public class DrawingDeepeningTests
{
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public DrawingDeepeningTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DumpMfcViewHierarchies()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"==================================================================");
                _output.WriteLine($"VIEW: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength}, Index={v.Index}");
                // Print all direct and indirect children
                var children = doc.AllElements.Where(e => e.Parent == v).ToList();
                _output.WriteLine($"  Direct Children ({children.Count}):");
                foreach (var c in children)
                {
                    _output.WriteLine($"    [{c.ClassName}] TagOff=0x{c.TagOffset:X}, Len={c.DataLength}");
                }
            }
        }
    }

    [Fact]
    public void InspectUnfoldedViewRange()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var inRange = doc.AllElements
            .Where(e => e.TagOffset >= 0x358E9 && e.TagOffset < 0x60853)
            .ToList();
        
        _output.WriteLine($"Elements in UnfoldedView range: {inRange.Count}");
        foreach (var e in inRange)
        {
            _output.WriteLine($"  [{e.ClassName}] TagOff=0x{e.TagOffset:X}, Len={e.DataLength}, Parent=[{e.Parent?.ClassName}]");
        }
    }

    [Fact]
    public void DumpKeyWordsXml()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("swXmlContents/KeyWords", out var kwBytes)) return;

        string xml = System.Text.Encoding.UTF8.GetString(kwBytes);
        int idx = xml.IndexOf('<');
        if (idx >= 0) xml = xml.Substring(idx);
        _output.WriteLine(xml);
    }

    [Fact]
    public void DumpViewDetailedFields()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                byte[] vBytes = new byte[v.DataLength];
                Array.Copy(defBytes, v.TagOffset, vBytes, 0, v.DataLength);

                for (int off = 0; off <= v.DataLength - 8; off += 4)
                {
                    double d = BitConverter.ToDouble(vBytes, off);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) >= 1e-4 && Math.Abs(d) <= 1000.0)
                    {
                        _output.WriteLine($"    +0x{off:X2} (rel): double = {d}");
                    }
                }
            }
        }
    }

    [Fact]
    public void DumpViewProperties()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                var reader = v.CreateReader();
                // Find all WChars in this element
                while (reader.Remaining > 3)
                {
                    if (reader.IsWCharPrefix)
                    {
                        int off = reader.Offset;
                        string s = reader.ReadWChar() ?? "";
                        if (!string.IsNullOrEmpty(s))
                            _output.WriteLine($"    WChar @ +0x{off:X}: '{s}'");
                    }
                    else
                    {
                        reader.Skip(1);
                    }
                }
            }
        }
    }

    [Fact]
    public void ListAllMfcClassesInDefinition()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var classCounts = doc.AllElements
            .GroupBy(e => string.IsNullOrEmpty(e.ClassName) ? "<anonymous>" : e.ClassName)
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"Total MFC elements: {doc.AllElements.Count}");
        foreach (var g in classCounts)
        {
            _output.WriteLine($"  {g.Key,-35} : {g.Count()} elements");
        }
    }

    [Fact]
    public void PrintAllStreamsAndViews()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        _output.WriteLine($"Streams count: {swFile.RawStreams.Count}");
        foreach (var kvp in swFile.RawStreams.OrderBy(k => k.Key))
        {
            _output.WriteLine($"  {kvp.Key,-40} : {kvp.Value.Length,8:N0} bytes");
        }
    }

    [Fact]
    public void InspectElementsBetweenViews()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var inRange = doc.AllElements
            .Where(e => e.TagOffset >= 0x2649B && e.TagOffset < 0x358E9)
            .ToList();
        
        _output.WriteLine($"Elements in range: {inRange.Count}");
        foreach (var e in inRange)
        {
            _output.WriteLine($"  [{e.ClassName}] TagOff=0x{e.TagOffset:X}, Len={e.DataLength}, Parent=[{e.Parent?.ClassName}]");
        }
    }

    [Fact]
    public void InspectViewTransformsAndGeometryInDefinition()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                byte[] vBytes = new byte[v.DataLength];
                Array.Copy(defBytes, v.TagOffset, vBytes, 0, v.DataLength);

                for (int off = 0; off <= v.DataLength - 8; off += 4)
                {
                    double d = BitConverter.ToDouble(vBytes, off);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) > 1e-4 && Math.Abs(d) < 10.0)
                    {
                        _output.WriteLine($"    +0x{off:X2}: double={d:F6}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Drawing_RealDrawingFile_D8001507933_VerifiesMfcViewsAndHighDefPreview()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        
        // 1. Verify Definition stream contains MFC drawing views
        Assert.True(swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes));
        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        Assert.NotEmpty(doc.FindAll("moDrSheet"));
        Assert.NotEmpty(doc.FindAll("moAbsoluteView"));
        Assert.NotEmpty(doc.FindAll("moUnfoldedView"));
        Assert.NotEmpty(doc.FindAll("moDetailView"));
        Assert.NotEmpty(doc.FindAll("moView_c"));

        // 2. Verify Preview DIB stream dimensions (1420 x 710)
        var bestSheet = swFile.GetBestSheetPreviewImage(0);
        Assert.NotNull(bestSheet);
        var (sw, sh) = SwFile.GetImageDimensions(bestSheet);
        Assert.Equal(1420, sw);
        Assert.Equal(710, sh);

        // 3. Verify Sheets and Views extraction
        var sheets = swFile.GetDrawingSheets();
        Assert.NotEmpty(sheets);
        var sheet0 = sheets[0];
        Assert.Equal("图纸1", sheet0.Name);
        Assert.True(sheet0.Views.Count >= 4);

        // 4. Verify MFC view deserialization details
        Assert.Contains(sheet0.Views, v => v.MfcClassName == "moAbsoluteView");
        Assert.Contains(sheet0.Views, v => v.MfcClassName == "moUnfoldedView");
        Assert.Contains(sheet0.Views, v => v.MfcClassName == "moDetailView");

        // 5. Verify Technical Notes and Title Block
        Assert.NotEmpty(sheet0.TechnicalNotes);
        Assert.Contains(sheet0.TechnicalNotes, n => n.Contains("锐边倒角去毛刺"));
        Assert.NotEmpty(sheet0.TitleBlockProperties);
        Assert.Contains(sheet0.TitleBlockProperties.Keys, k => k.Contains("比例"));
    }

    [Fact]
    public void Drawing_2026_GetDrawingSheets_ReturnsSheetsAndViews()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        var sheets = file.GetDrawingSheets();

        Assert.NotNull(sheets);
        Assert.Single(sheets);

        var sheet = sheets[0];
        Assert.Equal("Sheet1", sheet.Name);
        Assert.Equal(0, sheet.SheetIndex);

        // Preview PNG (backward compatibility)
        Assert.NotNull(sheet.PreviewPng);
        Assert.True(sheet.PreviewPng!.Length > 500);
        Assert.Equal(PngMagic[0], sheet.PreviewPng[0]);
        Assert.Equal(PngMagic[1], sheet.PreviewPng[1]);
        Assert.Equal(PngMagic[2], sheet.PreviewPng[2]);
        Assert.Equal(PngMagic[3], sheet.PreviewPng[3]);

        // Views
        Assert.NotEmpty(sheet.Views);
        Assert.Contains(sheet.Views, v => v.Name.Contains("Drawing View"));
        Assert.All(sheet.Views, v => Assert.Contains("cube.sldprt", v.ReferencedModelPath, System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Drawing_2026_GetSheetPreviewPng_ReturnsValidPng()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        byte[]? png = file.GetSheetPreviewPng(0);

        Assert.NotNull(png);
        Assert.True(png!.Length > 500);
        Assert.Equal(PngMagic[0], png[0]);
        Assert.Equal(PngMagic[1], png[1]);
        Assert.Equal(PngMagic[2], png[2]);
        Assert.Equal(PngMagic[3], png[3]);
    }

    [Fact]
    public void Drawing_GetBestPreviewImage_ReturnsHighResolutionBitmap()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        byte[]? bestImage = file.GetBestPreviewImage(0);

        Assert.NotNull(bestImage);
        var (width, height) = SwFile.GetImageDimensions(bestImage);
        // High-res preview bitmap is 2850x1436
        Assert.True(width >= 1920, $"Expected high resolution width >= 1920, but was {width}");
        Assert.True(height >= 1080, $"Expected high resolution height >= 1080, but was {height}");
    }

    [Fact]
    public void Drawing_GetBestSheetPreviewImage_ReturnsHighResForSheet0()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        byte[]? bestSheetImg = file.GetBestSheetPreviewImage(0);

        Assert.NotNull(bestSheetImg);
        var (width, height) = SwFile.GetImageDimensions(bestSheetImg);
        Assert.True(width >= 1920);
        Assert.True(height >= 1080);
    }

    [Fact]
    public void Drawing_Sheets_ContainBothPreviewPngAndHighResPreview()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        var sheets = file.GetDrawingSheets();

        Assert.NotNull(sheets);
        Assert.NotEmpty(sheets);
        var sheet0 = sheets[0];

        // PreviewPng is backward-compatible 640x480 PNG
        Assert.NotNull(sheet0.PreviewPng);
        var (pngW, pngH) = SwFile.GetImageDimensions(sheet0.PreviewPng);
        Assert.Equal(640, pngW);
        Assert.Equal(480, pngH);

        // HighResPreview is 2850x1436 BMP
        Assert.NotNull(sheet0.HighResPreview);
        var (highW, highH) = SwFile.GetImageDimensions(sheet0.HighResPreview);
        Assert.True(highW >= 1920);
        Assert.True(highH >= 1080);

        // BestPreview resolves to the high-res one
        Assert.Same(sheet0.HighResPreview, sheet0.BestPreview);
    }

    [Fact]
    public void SwFile_GetImageDimensions_ParsesPngAndBmpAccurately()
    {
        // 1. Check invalid/null
        Assert.Equal((0, 0), SwFile.GetImageDimensions(null));
        Assert.Equal((0, 0), SwFile.GetImageDimensions(new byte[10]));

        // 2. Synthesize a small 100x50 PNG header
        byte[] pngHeader = new byte[24];
        pngHeader[0] = 0x89; pngHeader[1] = 0x50; pngHeader[2] = 0x4E; pngHeader[3] = 0x47;
        pngHeader[16] = 0; pngHeader[17] = 0; pngHeader[18] = 0; pngHeader[19] = 100; // width 100
        pngHeader[20] = 0; pngHeader[21] = 0; pngHeader[22] = 0; pngHeader[23] = 50;  // height 50
        Assert.Equal((100, 50), SwFile.GetImageDimensions(pngHeader));

        // 3. Synthesize a small 320x240 BMP header
        byte[] bmpHeader = new byte[26];
        bmpHeader[0] = 0x42; bmpHeader[1] = 0x4D; // "BM"
        BitConverter.GetBytes(320).CopyTo(bmpHeader, 18);
        BitConverter.GetBytes(240).CopyTo(bmpHeader, 22);
        Assert.Equal((320, 240), SwFile.GetImageDimensions(bmpHeader));
    }
}
