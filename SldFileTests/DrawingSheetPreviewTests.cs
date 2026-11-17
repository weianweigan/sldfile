using System.Text;
using SldFile;
using SldFile.Drawing;
using Xunit;

namespace SldFileTests;

public class DrawingSheetPreviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void MultiSheetDrawing_DoesNotReuseDocumentWidePrimitives(int index)
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        var definition = file.GetDrawingPreviewStreamBytes("Contents/Definition");
        var display = file.GetDrawingPreviewStreamBytes("Contents/DisplayLists");
        const string xml = "<Drawing><Sheet Name='First'/><Sheet Name='Second'/></Drawing>";
        var sheets = DrawingReader.ReadSheets(xml, Array.Empty<string>(), _ => null,
            definitionStream: definition, displayListsStream: display, documentVersion: file.GetNativeDrawingDocumentVersion());
        Assert.Equal(2, sheets.Count);
        Assert.Empty(sheets[index].Preview.Primitives);
        var withoutXml = DrawingReader.ReadSheets(null, new[] { "First", "Second" }, _ => null,
            definitionStream: definition, displayListsStream: display, documentVersion: file.GetNativeDrawingDocumentVersion());
        Assert.Empty(withoutXml[index].Preview.Primitives);
    }

    [Fact]
    public void EmptyNativeStreams_ReturnAnEmptySheetPreview()
    {
        var sheet = Assert.Single(DrawingReader.ReadSheets(null, Array.Empty<string>(), _ => null));
        Assert.Empty(sheet.Preview.Primitives);
        Assert.True(sheet.Preview.PaperWidth > 0 && sheet.Preview.PaperHeight > 0);
    }

    [Fact]
    public void UnframedDefinitionText_IsNotInventedAsNativePreview()
    {
        using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, Encoding.UTF8, true))
        {
            writer.Write(new byte[] { 255, 254, 255, 4 }); writer.Write(Encoding.Unicode.GetBytes("技术要求"));
            writer.Write(.012); writer.Write(.024); writer.Write(0.0);
        }
        var sheet = Assert.Single(DrawingReader.ReadSheets(null, Array.Empty<string>(), _ => null, definitionStream: data.ToArray()));
        Assert.Empty(sheet.Preview.Primitives);
    }

    [Fact]
    public void Cube2026_SheetsExposeTheNativePreviewDirectly()
    {
        using var file = new SldDrawing(TestFileProvider.CubeDrawing2026);
        var sheet = Assert.Single(file.Sheets);
        Assert.NotEmpty(sheet.Preview.Primitives);
        Assert.True(sheet.Preview.PaperWidth > 0 && sheet.Preview.PaperHeight > 0);
    }

    [Fact]
    public void NativePreviewText_RetainsSheetFormatOwnership()
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        var preview = file.Sheets[0].Preview;
        Assert.Contains(preview.Primitives, p => p.Text.Contains("技术要求") && p.IsSheetLevelNote);
        Assert.Contains(preview.Primitives, p => p.Text == "2-10*50" && !p.IsSheetLevelNote);
    }

    [Fact]
    public void GetDrawingPreview_ReturnsDetachedEditableGeometry()
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        var cached = file.Sheets[0].Preview;
        var edit = file.GetDrawingPreview();
        Assert.NotSame(cached, edit);
        Assert.NotSame(cached.Primitives[0], edit.Primitives[0]);
        Assert.NotSame(cached.Primitives[0].Points, edit.Primitives[0].Points);
        edit.Primitives[0].Points = new[] { new DrawingPreviewPoint(10, 20) };
        Assert.Equal(cached.Primitives[0].Points, file.GetDrawingPreview().Primitives[0].Points);
    }
}
