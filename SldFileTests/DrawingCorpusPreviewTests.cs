using System.Xml.Linq;
using SldFile;
using SldFile.Drawing;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class DrawingCorpusPreviewTests
{
    public static IEnumerable<object[]> Drawings => new[] {
        new object[] { "D8001507933/D8001507933.SLDDRW", 5 },
        new object[] { "MacroBenchmark/carrier.slddrw", 7 },
        new object[] { "MacroBenchmark/cavity.slddrw", 6 },
        new object[] { "MacroBenchmark/punch_holder.slddrw", 5 },
        new object[] { "MacroBenchmark/Mold/bezel moldbase.slddrw", 4 }
    };

    [Theory]
    [MemberData(nameof(Drawings))]
    public void EverySavedVisibleView_HasContoursInItsSvg(string relative, int expectedViews)
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", relative));
        var sheet = Assert.Single(file.Sheets);
        var preview = file.GetDrawingPreview();
        Assert.Equal(expectedViews, preview.Views.Count);
        Assert.Equal(preview.Views.Count, sheet.Preview.Views.Count);
        foreach(var view in preview.Views)
        {
            var contours = preview.Primitives.Where(p => p.ViewName == view.Name && p.Kind == DrawingPreviewPrimitiveKind.Line).ToArray();
            Assert.NotEmpty(contours);
            Assert.All(contours.SelectMany(p=>p.Points), point =>
            {
                Assert.InRange(point.X, view.ClipMin.X - 1e-9, view.ClipMax.X + 1e-9);
                Assert.InRange(point.Y, view.ClipMin.Y - 1e-9, view.ClipMax.Y + 1e-9);
            });
        }
        string target = Path.Combine(AppContext.BaseDirectory, "TestResults", "DrawingCorpus", Path.GetFileNameWithoutExtension(relative) + ".svg");
        file.ExportSheetSvg(0, target);
        var svg = XDocument.Load(target);
        XNamespace ns="http://www.w3.org/2000/svg";
        Assert.Empty(svg.Descendants(ns+"image"));
        foreach(var view in preview.Views)
            Assert.Contains(svg.Descendants(), element => (string?)element.Attribute("data-view") == view.Name && element.Name == ns+"line");
        var transformed = file.GetDrawingPreview(0, true);
        Assert.Equal(preview.Primitives.Where(p=>p.Id.StartsWith("VB:")).Select(p=>p.Id), transformed.Primitives.Where(p=>p.Id.StartsWith("VB:")).Select(p=>p.Id));
        if(relative.EndsWith("/cavity.slddrw", StringComparison.OrdinalIgnoreCase))
        {
            // Native saved section/detail modes use tessellations 1/0/1 for
            // views 3/4/5. A hatch-only cache must still produce their borders.
            foreach(int index in new[]{3,4,5})
                Assert.Contains(preview.Primitives, p=>p.ViewName==preview.Views[index].Name && p.Id.StartsWith($"VB:M:{index}:") && p.Kind==DrawingPreviewPrimitiveKind.Line);
        }
    }

    [Fact]
    public void SavedContours_DoNotDependOnTheDefinitionFeatureGraph()
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        var sheets = DrawingReader.ReadSheets(null, new[]{"Sheet1"}, _=>null,
            displayListsStream:file.GetDrawingPreviewStreamBytes("Contents/DisplayLists"),
            vbListsStream:file.GetDrawingPreviewStreamBytes("Contents/VBLists"), documentVersion:file.GetNativeDrawingDocumentVersion());
        Assert.Equal(5, Assert.Single(sheets).Preview.Views.Count);
        Assert.Contains(sheets[0].Preview.Primitives,p=>p.Id.StartsWith("VB:"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SurfaceFallback_UsesItsOwnTessellation_AndSavedDetailMask(bool clip)
    {
        NativeDrawingVbArchive.Tessellation Mesh(float x)
        {
            var face = new NativeDrawingFaceTessModel();
            face.Facet.Channels[0].Words = new uint[] { 3 };
            face.Facet.Channels[1].ElementSize = 12; face.Facet.Channels[1].Count = 3;
            face.Facet.Channels[1].Words = new[] { x, .1f, 0, x + .3f, .1f, 0, x, .4f, 0 }.Select(v => unchecked((uint)BitConverter.SingleToInt32Bits(v))).ToArray();
            var body = new NativeDrawingBodyTessModel { Faces = new[] { new NativeDrawingObjectGraph.Node { Payload = face } } };
            return new NativeDrawingVbArchive.Tessellation { DocumentType = 1, Part = new NativeDrawingPartTessModel
                { Bodies = new[] { new NativeDrawingObjectGraph.Node { Payload = body } } } };
        }
        var source = new NativeDrawingVbArchive { Views = new[] { new NativeDrawingObjectGraph.Node { Payload = new NativeDrawingViewBucketModel() } },
            ViewIndices = new[] { 1 }, Tessellations = new[] { Mesh(.1f), Mesh(.6f) } };
        var view = new NativeDrawingDisplayViewModel { InnerBounds = new double[] { 0, 0, 1, 1 } };
        if (clip) view.DetailCircle = new[] { new DrawingPreviewPoint(.6, .1), new DrawingPreviewPoint(.75, .1), new DrawingPreviewPoint(.75, .25), new DrawingPreviewPoint(.6, .25) };
        var preview = new DrawingPreviewDefinition { PaperWidth = 1, PaperHeight = 1 };
        DrawingPreviewComposer.AppendViews(preview, new NativeDrawingDisplayArchive { Views = new[] { view } },
            new DrawingViewCacheArchive { NativeBuckets = source }, new[] { "own body" });
        Assert.Contains(preview.Primitives, p => p.Kind == DrawingPreviewPrimitiveKind.Line);
        Assert.All(preview.Primitives.SelectMany(p => p.Points), p => Assert.True(p.X >= .6 - 1e-7));
        if (clip) Assert.All(preview.Primitives.SelectMany(p => p.Points), p => Assert.True(p.X <= .75 + 1e-9 && p.Y <= .25 + 1e-9));
    }
}
