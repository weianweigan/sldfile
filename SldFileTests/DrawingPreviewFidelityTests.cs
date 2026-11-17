using System.Xml.Linq;
using SldFile;
using SldFile.Drawing;
using SldFile.Mfc;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests;

public class DrawingPreviewFidelityTests(ITestOutputHelper output)
{
    [Fact]
    public void NativePolyline_RendersAndEditsAllVerticesWithoutChangingItsWireType()
    {
        var item = new NativeDrawingDisplayItemModel { Type = 5, MemoryFootprint = 128 };
        item.Points.AddRange(new[] { new DrawingPreviewPoint(0, 0), new DrawingPreviewPoint(.1, .1), new DrawingPreviewPoint(.2, 0) });
        item.Integers.AddRange(new[] { 3, 0 });
        var record = Assert.IsType<DrawingPreviewRecordModel>(NativeDrawingPreviewAdapter.Read(item));
        Assert.Equal(3, record.Points.Length);
        record.Points[1] = new DrawingPreviewPoint(.1, .2);
        NativeDrawingPreviewAdapter.WriteGeometry(record, item);
        using var writer = MfcArchive.CreateWriter(); writer.DocumentVersion = 18000; item.Serialize(writer);
        using var reader = MfcArchive.CreateReader(writer.ToArray()); reader.DocumentVersion = 18000;
        var reloaded = new NativeDrawingDisplayItemModel(); reloaded.Serialize(reader);
        Assert.Equal(5, reloaded.Type); Assert.Equal(3, reloaded.Points.Count);
        Assert.Equal(.2, reloaded.Points[1].Y);
    }

    [Theory]
    [MemberData(nameof(DrawingCorpusPreviewTests.Drawings), MemberType = typeof(DrawingCorpusPreviewTests))]
    public void Corpus_ExposesConcreteLimitationsAndNativeLineStyles(string relative, int _)
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", relative));
        var preview = file.GetDrawingPreview();
        Assert.False(preview.IsResolved);
        Assert.NotEmpty(preview.Issues);
        Assert.NotEmpty(preview.NativeLineStyles);
        Assert.Contains(preview.NativeLineStyles, s => s.Name == "HIDDEN" && s.Segments.SequenceEqual(new[] { .25, -.125 }));
        Assert.All(preview.Issues, i => Assert.True(i.Occurrences > 0 && i.Source.Length > 0 && i.Message.Length > 0));
        foreach (var stream in file.SerializeDrawingPreviewStreams()) Assert.Equal(file.RawStreams[stream.Key], stream.Value);
        foreach (var issue in preview.Issues) output.WriteLine($"{issue.Code}: {issue.Source} @ {issue.FirstOffset:X8} x{issue.Occurrences}: {issue.Message}");
    }

    [Fact]
    public void SkippedCommand_IsReportedWithoutResettingThePen()
    {
        var model = new DrawingPreviewStreamModel();
        model.Segments.Add(new() { Record = new DrawingPreviewRecordModel { CommandType = 12, Value = "CONTINUOUS", Width = .00025f } });
        model.Segments.Add(new() { NativeRecord = new NativeDrawingDisplayItemModel { Type = 28, Offset = 123 } });
        model.Segments.Add(new() { Record = new DrawingPreviewRecordModel { CommandType = 4,
            Points = new[] { new DrawingPreviewPoint(0, 0), new DrawingPreviewPoint(1, 1) } } });
        var preview = model.ToDefinition(0, 1);
        Assert.Single(preview.Primitives);
        var issue = Assert.Single(preview.Issues);
        Assert.Equal(28, issue.CommandType); Assert.Equal(123, issue.FirstOffset);
        Assert.False(preview.IsResolved);
        model.Segments.RemoveAt(1);
        Assert.True(model.ToDefinition(0, 1).IsResolved);
    }

    [Fact]
    public void TextWidths_PositionUnicodeElementsIndividuallyInSvg()
    {
        var primitive = new DrawingPreviewPrimitive { Kind = DrawingPreviewPrimitiveKind.Text,
            Points = new[] { new DrawingPreviewPoint(.01, .02) }, Text = "Ae\u0301😀", WidthFactor = .8,
            CharacterAdvances = new[] { .001, .002, 0, .003, 0 } };
        var cells = primitive.GetTextElements();
        Assert.Equal(new[] { "A", "e\u0301", "😀" }, cells.Select(c => c.Text));
        Assert.Equal(new[] { 0, .001, .003 }, cells.Select(c => c.Offset));
        var svg = XDocument.Parse(DrawingPreviewSvgWriter.Write(new() { Primitives = new[] { primitive } }, null));
        XNamespace ns = "http://www.w3.org/2000/svg";
        var spans = svg.Descendants(ns + "tspan").ToArray();
        Assert.Equal(3, spans.Length);
        Assert.Equal(.01 + .003 / .8, (double)spans[2].Attribute("x")!, 12);
        Assert.Equal(.003 / .8, (double)spans[2].Attribute("textLength")!, 12);
        primitive.CharacterAdvances = new[] { double.NaN };
        Assert.Empty(primitive.GetTextElements());
    }

    [Fact]
    public void Occlusion_SplitsOnlyTheCoveredPartOfAnEdge()
    {
        var a = new DrawingPreviewPoint(0, .25, 0); var b = new DrawingPreviewPoint(1, .25, 0);
        var triangle = new[] { new DrawingPreviewPoint(.25, 0, 1), new DrawingPreviewPoint(.75, 0, 1), new DrawingPreviewPoint(.5, 1, 1) };
        var visible = DrawingPreviewComposer.VisibleIntervals(a, b, new[] { triangle });
        Assert.Equal(2, visible.Count);
        Assert.Equal(.3125, visible[0].End, 10); Assert.Equal(.6875, visible[1].Start, 10);
        var coplanar = triangle.Select(p => new DrawingPreviewPoint(p.X, p.Y, 0)).ToArray();
        Assert.Equal((0d, 1d), Assert.Single(DrawingPreviewComposer.VisibleIntervals(a, b, new[] { coplanar })));
        var behind = triangle.Select(p => new DrawingPreviewPoint(p.X, p.Y, -1)).ToArray();
        Assert.Equal((0d, 1d), Assert.Single(DrawingPreviewComposer.VisibleIntervals(a, b, new[] { behind })));
    }

    [Fact]
    public void SlantedSurface_ClipsAtTheDepthCrossing()
    {
        var triangle = new[] { new DrawingPreviewPoint(0, 0, -1), new DrawingPreviewPoint(1, 0, 1), new DrawingPreviewPoint(0, 1, -1) };
        var visible = DrawingPreviewComposer.VisibleIntervals(new(0, .1, 0), new(1, .1, 0), new[] { triangle });
        Assert.Equal(2, visible.Count);
        Assert.InRange(visible[0].End, .5, .500001);
        Assert.Equal(.9, visible[1].Start, 10);
    }
}
