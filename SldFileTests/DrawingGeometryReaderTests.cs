using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Drawing;

using Xunit;

namespace SldFileTests;

public class DrawingGeometryReaderTests
{
    [Fact]
    public void MissingDisplayCache_DoesNotInventBordersOrFitArbitraryCoordinates()
    {
        byte[] arbitrary = new byte[256];
        BitConverter.GetBytes(0.1).CopyTo(arbitrary, 24);
        BitConverter.GetBytes(0.2).CopyTo(arbitrary, 32);
        var sheet = Assert.Single(DrawingReader.ReadSheets(null, Array.Empty<string>(), _ => null, definitionStream: arbitrary, displayListsStream: arbitrary));
        Assert.Empty(sheet.Preview.Primitives);
    }

    [Fact]
    public void RealDrawing_UsesSavedLinesArcsFilledTrianglesAndText()
    {
        using var drawing = new SldDrawing(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        var preview = drawing.GetDrawingPreview();
        // The native symbol cache contains already placed geometry, with its
        // ownership retained in the public identity.
        Assert.Equal(798, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Line && !p.Id.StartsWith("VB:")));
        Assert.DoesNotContain(preview.Primitives, p => p.Id == "00023E93");
        Assert.Contains(preview.Primitives, p => p.Id == "SYM:00023DD9/00023E93");
        Assert.Equal(217, preview.Primitives.Count(p => p.Id.StartsWith("VB:")));
        Assert.Equal(5, preview.Views.Count);
        Assert.Equal(36, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Arc && !p.Id.StartsWith("VB:")));
        Assert.Equal(105, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Triangle && !p.Id.StartsWith("VB:")));
        Assert.Equal(117, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Text));
        var line = preview.Primitives.First(p => p.Kind == DrawingPreviewPrimitiveKind.Line && !p.Id.StartsWith("VB:"));
        Assert.Equal(0.283626419254328, line.Points[0].X, 13);
        Assert.Equal(0.004688370382817192, line.Points[0].Y, 13);
        Assert.Equal(0x191924u, line.ColorRgb);
        Assert.Contains(preview.Primitives, p => p.Text.Contains("技术要求"));
        Assert.False(preview.IsResolved);
        Assert.Equal(drawing.RawStreams["Contents/DisplayLists"].Length, preview.DecodedByteCount + preview.PreservedByteCount);
    }
}
