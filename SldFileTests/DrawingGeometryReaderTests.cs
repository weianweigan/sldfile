using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Drawing;
using SldFile.Drawing.Vector;
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
        var (lines, views) = DrawingGeometryReader.ReadSheetGeometryAndViews(arbitrary, arbitrary, 420, 297);
        Assert.Empty(lines);
        Assert.Empty(views);
        Assert.Empty(DrawingGeometryReader.ReadSheetGeometry(null, null, 420, 297));
    }

    [Fact]
    public void RealDrawing_UsesSavedLinesArcsFilledTrianglesAndText()
    {
        using var drawing = new SldDrawing(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        var preview = drawing.GetDrawingPreview();
        Assert.Equal(798, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Line && !p.Id.StartsWith("VB:")));
        Assert.Contains(preview.Primitives, p => p.Id.StartsWith("VB:"));
        Assert.NotEmpty(preview.Views);
        Assert.Equal(35, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Arc));
        Assert.Equal(105, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Triangle && !p.Id.StartsWith("VB:")));
        Assert.Equal(5, preview.Views.Count);
        var front = preview.Views.Single(v => v.Name == "工程图视图1");
        Assert.Equal(0.0847937938859705, front.Translation.X, 13);
        Assert.Equal(0.05, front.Scale, 13);
        var detail = preview.Views.Single(v => v.Name == "工程图视图5");
        Assert.Equal(-0.010826851948478766, detail.Translation.X, 13);
        Assert.Equal(0.5, detail.Scale, 13);
        Assert.Equal(117, preview.Primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Text));
        var line = preview.Primitives.First(p => p.Kind == DrawingPreviewPrimitiveKind.Line && !p.Id.StartsWith("VB:"));
        Assert.Equal(0.283626419254328, line.Points[0].X, 13);
        Assert.Equal(0.004688370382817192, line.Points[0].Y, 13);
        Assert.Equal(0x191924u, line.ColorRgb);
        Assert.Contains(preview.Primitives, p => p.Text.Contains("技术要求"));
        Assert.DoesNotContain(drawing.Sheets[0].VectorData!.Lines, p => p.Kind == DrawingLineKind.SheetBorderOuter);
        Assert.False(preview.IsResolved);
        Assert.Equal(drawing.RawStreams["Contents/DisplayLists"].Length, preview.DecodedByteCount + preview.PreservedByteCount);
    }
}
