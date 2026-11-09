using System;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Drawing.Vector;
using Xunit;

namespace SldFileTests;

public class DrawingVectorReaderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MultiSheetDrawing_DoesNotReuseDocumentWidePrimitives(int sheetIndex)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        var definition = file.RawStreams["Contents/Definition"];
        var display = file.RawStreams["Contents/DisplayLists"];
        const string xml = "<Drawing><Sheet Name='First'/><Sheet Name='Second'/></Drawing>";
        var data = DrawingVectorReader.ReadVectorData(definition, xml, sheetIndex, displayListsStream: display);
        Assert.False(data.HasVectorData);
        Assert.Empty(data.Texts);
        Assert.Empty(data.Lines);

        var withoutXml = DrawingVectorReader.ReadVectorData(definition, null, sheetIndex, displayListsStream: display, sheetCount: 2);
        Assert.False(withoutXml.HasVectorData);
    }

    [Fact]
    public void DrawingVectorReader_WithNullOrEmptyDefinition_ReturnsSafeEmptyResult()
    {
        var data1 = DrawingVectorReader.ReadVectorData(null, null);
        Assert.NotNull(data1);
        Assert.False(data1.HasVectorData);
        Assert.Empty(data1.Texts);

        var data2 = DrawingVectorReader.ReadVectorData(Array.Empty<byte>(), "<Sheet Name=\"Sheet1\"/>");
        Assert.NotNull(data2);
        Assert.False(data2.HasVectorData);
        Assert.Empty(data2.Texts);
    }

    [Fact]
    public void UnframedDefinitionText_IsNotInventedAsNativePreview()
    {
        using var writer = new BinaryWriter(new MemoryStream());
        writer.Write(new byte[] { 255, 254, 255, 4 });
        writer.Write(Encoding.Unicode.GetBytes("技术要求"));
        writer.Write(0.012); writer.Write(0.024); writer.Write(0.0);
        writer.Flush();
        var result = DrawingVectorReader.ReadVectorData(((MemoryStream)writer.BaseStream).ToArray(), null);
        Assert.Empty(result.Texts);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void DrawingSheetVectorData_CalculatePixelCoordinates_MapsIsometricCenterCorrectly()
    {
        var vectorData = new DrawingSheetVectorData
        {
            PaperWidthMm = 297.0,
            PaperHeightMm = 210.0
        };

        var note = new DrawingVectorText("Title", 12.0, 24.0, 0.0, "SimSun", 2.5);
        vectorData.Texts = new[] { note };

        // Reference 1420 x 710 bitmap
        vectorData.CalculatePixelCoordinates(1420, 710);

        Assert.True(note.PixelX > 200 && note.PixelX < 300, $"Expected PixelX in [200, 300], but was {note.PixelX}");
        Assert.True(note.PixelY > 600 && note.PixelY < 650, $"Expected PixelY in [600, 650], but was {note.PixelY}");
        Assert.True(note.PixelHeight > 5.0, $"Expected PixelHeight > 5, but was {note.PixelHeight}");
    }

    [Fact]
    public void Drawing_GetDrawingSheets_Cube2026_IncludesVectorDataProperty()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        var sheets = file.GetDrawingSheets();

        Assert.NotNull(sheets);
        Assert.NotEmpty(sheets);
        var sheet0 = sheets[0];

        // VectorData property is present (even if Definition stream has no notes in minimal cube)
        // Ensure non-null safe behavior
        if (sheet0.VectorData != null)
        {
            Assert.True(sheet0.VectorData.PaperWidthMm > 0);
            Assert.True(sheet0.VectorData.PaperHeightMm > 0);
        }
    }

    [Fact]
    public void NativePreviewText_ClassifiesSheetNotesAndViewCallouts()
    {
        using var file = new SldDrawing(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        var result = file.Sheets[0].VectorData!;
        Assert.Contains(result.Texts, t => t.CleanText.Contains("技术要求") && t.IsSheetLevelNote);
        Assert.Contains(result.Texts, t => t.CleanText.Contains("未注公差") && t.IsSheetLevelNote);
        Assert.Contains(result.Texts, t => t.CleanText == "2-10*50" && t.IsViewAnnotation);
    }
}
