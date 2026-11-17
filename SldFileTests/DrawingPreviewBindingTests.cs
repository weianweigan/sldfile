using System.Text.Json;
using System.Xml.Linq;
using SldFile;
using SldFile.Drawing;
using Xunit;

namespace SldFileTests;

public class DrawingPreviewBindingTests
{
    [Fact]
    public void D800Svg_ContainsAllFiveViews_WithNativeTransformsAndClippedGeometry()
    {
        using var file = new SldDrawing(TestFileProvider.Resolve("Drawings", "D8001507933/D8001507933.SLDDRW"));
        var preview = file.GetDrawingPreview();
        Assert.Equal(5, preview.Views.Count);
        Assert.Equal(217, preview.Primitives.Count(p => p.Id.StartsWith("VB:")));
        Assert.Contains(preview.Primitives, p => p.Id.StartsWith("VB:") && p.Kind == DrawingPreviewPrimitiveKind.Arc);
        Assert.Contains(preview.Primitives, p => p.Kind == DrawingPreviewPrimitiveKind.Line && p.Points.Count > 2);
        // Independent SolidWorks COM readback includes the sheet as element 0.
        using var reference = JsonDocument.Parse(File.ReadAllText(TestFileProvider.Resolve("Drawings", "D8001507933/preview-com-reference.json")));
        for (int i = 0; i < preview.Views.Count; i++)
        {
            var view = preview.Views[i];
            var expected = reference.RootElement[i + 1];
            var matrix = expected.GetProperty("matrix").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            Assert.Equal(expected.GetProperty("scale").GetDouble(), view.Scale);
            Assert.Equal(matrix.Take(9), view.Matrix);
            Assert.Equal(matrix[9], view.Translation.X, 13);
            Assert.Equal(matrix[10], view.Translation.Y, 13);
            var lines = preview.Primitives.Where(p => p.ViewName == view.Name).ToArray();
            Assert.NotEmpty(lines);
            Assert.All(lines, line => Assert.All(line.Points, point =>
            {
                Assert.InRange(point.X, view.ClipMin.X - 1e-12, view.ClipMax.X + 1e-12);
                Assert.InRange(point.Y, view.ClipMin.Y - 1e-12, view.ClipMax.Y + 1e-12);
            }));
        }
        // Detail cache coordinates already include scale; check its actual
        // outline to catch both double scaling and use of the model origin.
        var detail = preview.Primitives.Where(p => p.ViewName == preview.Views[4].Name).SelectMany(p => p.Points).ToArray();
        Assert.Contains(detail, p => Math.Abs(p.X - 0.1225153054154129) < 1e-12 && Math.Abs(p.Y - 0.06585865242292437) < 1e-12);
        Assert.Contains(detail, p => Math.Abs(p.X - 0.08883099211814116) < 1e-12 && Math.Abs(p.Y - 0.06585865242292437) < 1e-12);

        string output = Path.Combine(AppContext.BaseDirectory, "TestResults", "D8001507933.fixed.svg");
        file.ExportSheetSvg(0, output);
        var svg = XDocument.Load(output);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Empty(svg.Descendants(ns + "image"));
        Assert.All(svg.Descendants(ns + "text"), text => Assert.Equal("text-after-edge", (string?)text.Attribute("dominant-baseline")));
        Assert.Contains(svg.Descendants(ns + "tspan"), text => text.Attribute("textLength") != null);
        var geometry = svg.Descendants(ns + "g").Single(g => (string?)g.Attribute("id") == "views");
        Assert.Equal(preview.Primitives.Count(p => p.Kind != DrawingPreviewPrimitiveKind.Text), geometry.Elements().Count());
        Assert.NotEmpty(geometry.Elements(ns + "line"));
        Assert.NotEmpty(geometry.Elements(ns + "polyline"));
        Assert.All(geometry.Elements(), element => Assert.Equal("round", (string?)element.Attribute("stroke-linecap")));
        Assert.Equal(5, file.Sheets[0].Preview.Views.Count);
    }

    [Fact]
    public void EmptySvg_UsesRasterFallback_AndTextOnlySvgDoesNot()
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        var preview = new DrawingPreviewDefinition();
        byte[] raster = { 0x42, 0x4D };
        Assert.Single(XDocument.Parse(DrawingPreviewSvgWriter.Write(preview, raster)).Descendants(ns + "image"));
        preview.Primitives = new[] { new DrawingPreviewPrimitive { Kind = DrawingPreviewPrimitiveKind.Text,
            Text = "25", Points = new[] { new DrawingPreviewPoint(0.1, 0.1) } } };
        Assert.Empty(XDocument.Parse(DrawingPreviewSvgWriter.Write(preview, raster)).Descendants(ns + "image"));
    }
}
