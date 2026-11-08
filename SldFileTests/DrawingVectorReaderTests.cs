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
    public void DrawingVectorReader_ParsesSynthesizedMfcStreamAccurately()
    {
        // Synthesize an MFC wide string note:
        // [0xFF, 0xFE, 0xFF, char_count] + UTF-16LE text + X(double) + Y(double) + Z(double)
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // leading padding

        // Marker
        ms.WriteByte(0xFF);
        ms.WriteByte(0xFE);
        ms.WriteByte(0xFF);

        string testText = "技术要求";
        byte[] textBytes = Encoding.Unicode.GetBytes(testText);
        ms.WriteByte((byte)testText.Length); // 4
        ms.Write(textBytes, 0, textBytes.Length);

        // Coordinates: X = 0.012 m (12mm), Y = 0.024 m (24mm), Z = 0.0 m
        ms.Write(BitConverter.GetBytes(0.012), 0, 8);
        ms.Write(BitConverter.GetBytes(0.024), 0, 8);
        ms.Write(BitConverter.GetBytes(0.000), 0, 8);

        // Font block
        ms.Write(new byte[] { 0x01, 0x00, 0x00, 0x00 });
        ms.WriteByte(0xFF);
        ms.WriteByte(0xFE);
        ms.WriteByte(0xFF);
        string fontName = "新宋体";
        byte[] fontBytes = Encoding.Unicode.GetBytes(fontName);
        ms.WriteByte((byte)fontName.Length); // 3
        ms.Write(fontBytes, 0, fontBytes.Length);
        ms.Write(BitConverter.GetBytes(0.0025), 0, 8); // height = 2.5mm
        ms.Write(BitConverter.GetBytes(0.0), 0, 8);      // angle = 0

        byte[] streamData = ms.ToArray();
        var result = DrawingVectorReader.ReadVectorData(streamData, null);

        Assert.NotNull(result);
        Assert.True(result.HasVectorData);
        Assert.Single(result.Texts);

        var note = result.Texts[0];
        Assert.Equal("技术要求", note.CleanText);
        Assert.Equal(12.0, Math.Round(note.X, 1));
        Assert.Equal(24.0, Math.Round(note.Y, 1));
        Assert.Equal("新宋体", note.FontName);
        Assert.Equal(2.5, Math.Round(note.HeightMm, 1));
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
    public void DrawingVectorReader_ClassifiesTextsAccurately_PreventingViewGhosting()
    {
        var ms = new MemoryStream();
        void WriteText(string txt, double xM, double yM)
        {
            ms.WriteByte(0xFF); ms.WriteByte(0xFE); ms.WriteByte(0xFF);
            byte[] bytes = Encoding.Unicode.GetBytes(txt);
            ms.WriteByte((byte)txt.Length);
            ms.Write(bytes, 0, bytes.Length);
            ms.Write(BitConverter.GetBytes(xM), 0, 8);
            ms.Write(BitConverter.GetBytes(yM), 0, 8);
            ms.Write(BitConverter.GetBytes(0.0), 0, 8);
            ms.Write(new byte[16]);
        }

        WriteText("技术要求", 0.012, 0.024);
        WriteText("Drawing No./图号", 0.227, 0.021);
        WriteText("Tolerance not marked/未注公差", 0.252, 0.049);
        WriteText("2-10*50", 0.220, 0.137);

        var result = DrawingVectorReader.ReadVectorData(ms.ToArray(), null);
        Assert.NotNull(result);
        Assert.Equal(4, result.Texts.Count);

        var tech = result.Texts.First(t => t.CleanText == "技术要求");
        Assert.Equal(DrawingVectorTextCategory.TechnicalNote, tech.Category);
        Assert.True(tech.IsSheetLevelNote);

        var title = result.Texts.First(t => t.CleanText.Contains("Drawing No"));
        Assert.Equal(DrawingVectorTextCategory.TitleBlock, title.Category);
        Assert.True(title.IsSheetLevelNote);

        var tol = result.Texts.First(t => t.CleanText.Contains("未注公差"));
        Assert.Equal(DrawingVectorTextCategory.ToleranceTable, tol.Category);
        Assert.True(tol.IsSheetLevelNote);

        var view = result.Texts.First(t => t.CleanText == "2-10*50");
        Assert.Equal(DrawingVectorTextCategory.ViewAnnotation, view.Category);
        Assert.False(view.IsSheetLevelNote);
    }
}
