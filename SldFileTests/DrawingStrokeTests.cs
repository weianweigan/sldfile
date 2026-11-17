using System.Xml.Linq;
using SldFile;
using SldFile.Drawing;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class DrawingStrokeTests
{
    private static DrawingPreviewDefinition Compose(DrawingPreviewPoint[] points, bool hasArc = false, DrawingPreviewPoint center = default)
    {
        var view = new NativeDrawingDisplayViewModel { InnerBounds = new double[] { 0, 0, 1, 1 } };
        var placements = new NativeDrawingDisplayArchive { Views = new[] { view } };
        var polygon = new NativeDrawingBucketPolyModel
        {
            Flags = hasArc ? 0x321u : 0x301u, PointCount = points.Length,
            Coordinates = points.SelectMany(p => new[] { p.X, p.Y, p.Z }).ToArray(), ArcStart = center,
            HasLineVisual = 1, LineStyle = "CENTER", LineColor = 0x00332211,
            LineWidth = new NativeDrawingLineWidthModel { Width = 0.0008f }
        };
        var cache = new DrawingViewCacheArchive { NativeBuckets = new NativeDrawingVbArchive
            { Views = new[] { new NativeDrawingObjectGraph.Node { Payload = new NativeDrawingViewBucketModel() } } } };
        cache.Fragments.Add(new DrawingViewCacheArchive.Fragment { Owner = "view", Model = polygon, Offset = 0x100 });
        var preview = new DrawingPreviewDefinition { PaperWidth = 1, PaperHeight = 1 };
        DrawingPreviewComposer.AppendViews(preview, placements, cache, new[] { "view" });
        return preview;
    }

    [Fact]
    public void ConnectedCorners_KeepTheirVerticesAndNativeStyle_InSvg()
    {
        var points = new[] { new DrawingPreviewPoint(.1, .2), new DrawingPreviewPoint(.5, .2), new DrawingPreviewPoint(.5, .8) };
        var preview = Compose(points);
        var path = Assert.Single(preview.Primitives);
        Assert.Equal(points, path.Points);
        Assert.Equal(0x112233u, path.ColorRgb);
        Assert.Equal("CENTER", path.LineStyle);
        Assert.Equal(.0008, path.LineWidth, 8);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var polyline = Assert.Single(XDocument.Parse(DrawingPreviewSvgWriter.Write(preview, null)).Descendants(ns + "polyline"));
        Assert.Equal("round", (string?)polyline.Attribute("stroke-linecap"));
        Assert.Equal("round", (string?)polyline.Attribute("stroke-linejoin"));
        Assert.Equal(3, ((string)polyline.Attribute("points")!).Split(' ').Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCircleOrigin_RestoresAnAnalyticArc_InEitherDirection(bool reverse)
    {
        var points = Enumerable.Range(0, 9).Select(i =>
        {
            double angle = (reverse ? 8 - i : i) * Math.PI / 16;
            return new DrawingPreviewPoint(.5 + .25 * Math.Cos(angle), .5 + .25 * Math.Sin(angle));
        }).ToArray();
        var path = Assert.Single(Compose(points, true, new DrawingPreviewPoint(.5, .5)).Primitives);
        Assert.Equal(DrawingPreviewPrimitiveKind.Arc, path.Kind);
        Assert.Equal(points[0], path.Points[0]);
        Assert.Equal(points[^1], path.Points[1]);
        Assert.Equal(new DrawingPreviewPoint(.5, .5), path.Points[2]);
        Assert.Equal(reverse ? -1 : 1, path.Normal.Z);
    }

    [Fact]
    public void MissingOrInconsistentNativeCircleData_DoesNotInventAnArc()
    {
        var points = new[] { new DrawingPreviewPoint(.1, .2), new DrawingPreviewPoint(.5, .2), new DrawingPreviewPoint(.5, .8) };
        Assert.Equal(DrawingPreviewPrimitiveKind.Line, Assert.Single(Compose(points, true, new DrawingPreviewPoint(.5, .5)).Primitives).Kind);
        var circleSamples = Enumerable.Range(0, 9).Select(i => new DrawingPreviewPoint(.5 + .25 * Math.Cos(i * Math.PI / 16), .5 + .25 * Math.Sin(i * Math.PI / 16))).ToArray();
        Assert.Equal(circleSamples, Assert.Single(Compose(circleSamples).Primitives).Points);
    }

    [Fact]
    public void Clipping_SeparatesOutsideIntervals_WithoutDrawingABridge()
    {
        var preview = Compose(new[] { new DrawingPreviewPoint(.1, .2), new DrawingPreviewPoint(.5, .2),
            new DrawingPreviewPoint(1.2, .2), new DrawingPreviewPoint(1.2, .8),
            new DrawingPreviewPoint(.5, .8), new DrawingPreviewPoint(.1, .8) });
        Assert.Equal(2, preview.Primitives.Count);
        Assert.All(preview.Primitives[0].Points, p => Assert.Equal(.2, p.Y));
        Assert.All(preview.Primitives[1].Points, p => Assert.Equal(.8, p.Y));
        Assert.All(preview.Primitives, path => Assert.Equal(3, path.Points.Count));
    }

    [Fact]
    public void ProjectedCacheArcEdits_AreRejectedAtomically()
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"), readOnly: false);
        var original = file.SerializeDrawingPreviewStreams();
        var preview = file.GetDrawingPreview();
        preview.Primitives.First(p => p.Id.StartsWith("VB:") && p.Kind == DrawingPreviewPrimitiveKind.Arc).Normal = new DrawingPreviewPoint(0, 0, -100);
        Assert.Throws<NotSupportedException>(() => file.SetDrawingPreview(preview));
        foreach (var pair in original) Assert.Equal(pair.Value, file.SerializeDrawingPreviewStreams()[pair.Key]);
    }
}
