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

        // Preview PNG
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
}
