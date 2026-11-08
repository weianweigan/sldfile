using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

public class DrawingDeepeningTests
{
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };

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
