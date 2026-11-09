using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using SketchFeature = SldFile.Mfc.SketchFeatureModel;
using SketchPoint = SldFile.Mfc.SketchPointModel;
using SketchSegment = SldFile.Mfc.SketchSegmentModel;
using SketchRelation = SldFile.Mfc.SketchRelationModel;
using SketchDimension = SldFile.Mfc.SketchDimensionModel;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Region boundary loop-order resolver evidence.
///
/// Native loop orders recovered from the region chooser groups
/// (group g = [e[N-g], e[N-2-g], e[N-1-g]], clockwise):
///   select_region (L)       [4,3,6,7,8,5]
///   select_region_overlap   [3,4,5,6]
///   SelectionModes/region   [6,7,8,5]
/// The resolver derives the connected cycle from decoded sketch segment
/// endpoints/intersections, orients it clockwise and rotates it to the smallest
/// edge id (or a preferred start edge).
/// </summary>
public class TopologyRegionLoopOrderTests
{
    [Theory]
    [InlineData("SelectionModes/region", new uint[] { 6, 7, 8, 5 })]
    [InlineData("SelectionAuthoring/select_region", new uint[] { 4, 3, 6, 7, 8, 5 })]
    [InlineData("SelectionAuthoring/select_region_resave", new uint[] { 4, 3, 6, 7, 8, 5 })]
    [InlineData("SelectionAuthoring/select_region_overlap", new uint[] { 3, 4, 5, 6 })]
    [InlineData("SelectionAuthoring/select_region_overlap_resave", new uint[] { 3, 4, 5, 6 })]
    public void Resolver_DerivesClockwiseCycleMatchingNativeLoopOrder(
        string relativePath, uint[] nativeLoopOrder)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        var definition = file.Features.Select(f => f.Model)
            .OfType<ExtrudeFeatureModel>().FirstOrDefault(d => d.SelectedRegions.Count > 0);
        Assert.NotNull(definition);
        var edges = definition!.SelectedRegions[0].EdgeIds;
        var sketch = FindSketch(file, edges);
        Assert.NotNull(sketch);

        var resolved = SketchRegionLoopOrderResolver.Resolve(sketch, edges);
        Assert.NotNull(resolved);
        Assert.True(IsSameCyclicOrder(nativeLoopOrder, resolved!),
            $"resolved [{string.Join(",", resolved)}] is not a cyclic rotation of native [{string.Join(",", nativeLoopOrder)}]");
        Assert.Equal(resolved!.Min(), resolved[0]); // deterministic start = smallest id

        // Preferred start reproduces the exact native rotation.
        var exact = SketchRegionLoopOrderResolver.Resolve(sketch, edges, nativeLoopOrder[0]);
        Assert.Equal(nativeLoopOrder, exact);
    }

    [Theory]
    [InlineData("SelectionModes/region", new uint[] { 6, 7, 8, 5 })]
    [InlineData("SelectionAuthoring/select_region", new uint[] { 4, 3, 6, 7, 8, 5 })]
    [InlineData("SelectionAuthoring/select_region_overlap", new uint[] { 3, 4, 5, 6 })]
    public void Resolver_FromSketchReproducesNativeChooserPayload(string relativePath, uint[] nativeLoopOrder)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = SldFile.Mfc.MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] nativePayload = stream[chooser.DataStart..ChooserPayloadEnd(doc, chooser, extrusion.TagOffset)];
        var template = RegionChooserTemplate.Capture(nativePayload);
        Assert.NotNull(template);
        Assert.Equal(nativeLoopOrder, template!.NativeLoopOrder);

        var definition = file.Features.Select(f => f.Model)
            .OfType<ExtrudeFeatureModel>().FirstOrDefault(d => d.SelectedRegions.Count > 0);
        var edges = definition!.SelectedRegions[0].EdgeIds;
        var sketch = FindSketch(file, edges);
        byte[] payload = template.BuildPayloadFromSketch(sketch, edges);
        Assert.Equal(nativePayload, payload);
    }

    [Fact]
    public void Resolver_SyntheticSquareOrientationIsClockwise()
    {
        // Square edges: 1 bottom (0,0)->(0.01,0), 4 right, 3 top, 2 left.
        var segments = new[]
        {
            Segment(1, 0, 0, 0.01, 0),
            Segment(2, 0, 0, 0, 0.01),
            Segment(3, 0, 0.01, 0.01, 0.01),
            Segment(4, 0.01, 0, 0.01, 0.01)
        };
        var order = SketchRegionLoopOrderResolver.Resolve(segments, new uint[] { 1, 2, 3, 4 });
        Assert.NotNull(order);
        // Clockwise traversal starting at the smallest edge (1).
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, order);
        Assert.True(IsSameCyclicOrder(new uint[] { 2, 3, 4, 1 }, order!));
    }

    [Fact]
    public void Resolver_UnknownOrAmbiguousInputsReturnNull()
    {
        var square = new[]
        {
            Segment(1, 0, 0, 0.01, 0),
            Segment(2, 0, 0, 0, 0.01),
            Segment(3, 0, 0.01, 0.01, 0.01),
            Segment(4, 0.01, 0, 0.01, 0.01)
        };
        Assert.Null(SketchRegionLoopOrderResolver.Resolve((SketchFeature?)null, new uint[] { 1, 2, 3, 4 }));
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(square, new uint[] { 1, 2, 3 }));
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(square, new uint[] { 1, 2, 3, 99 }));
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(square, new uint[] { 1, 1, 2, 3 }));
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(square, new uint[] { 1, 2 }));
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(square, Array.Empty<uint>()));

        // Two disjoint open pairs: not a closed cycle.
        var disconnected = new[]
        {
            Segment(1, 0, 0, 0.01, 0),
            Segment(2, 0.01, 0, 0.01, 0.01),
            Segment(3, 1, 1, 1.01, 1),
            Segment(4, 1.01, 1, 1.01, 1.01)
        };
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(disconnected, new uint[] { 1, 2, 3, 4 }));

        // Overlapping collinear edges are ambiguous.
        var overlapping = new[]
        {
            Segment(1, 0, 0, 0.01, 0),
            Segment(2, 0.005, 0, 0.02, 0),
            Segment(3, 0.02, 0, 0.02, 0.01),
            Segment(4, 0.02, 0.01, 0, 0.01),
            Segment(5, 0, 0.01, 0, 0)
        };
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(overlapping, new uint[] { 1, 2, 3, 4, 5 }));

        // Non-line segment kinds are rejected.
        var arc = new[]
        {
            Segment(1, 0, 0, 0.01, 0),
            Segment(2, 0, 0, 0, 0.01),
            Segment(3, 0, 0.01, 0.01, 0.01),
            Segment(4, 0.01, 0, 0.01, 0.01)
        };
        arc[0].Kind = SketchSegmentKind.Arc;
        Assert.Null(SketchRegionLoopOrderResolver.Resolve(arc, new uint[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void Resolver_DoesNotModifySketchSegments()
    {
        var segments = new[]
        {
            Segment(1, 0, 0, 0.01, 0),
            Segment(2, 0, 0, 0, 0.01),
            Segment(3, 0, 0.01, 0.01, 0.01),
            Segment(4, 0.01, 0, 0.01, 0.01)
        };
        var before = segments.Select(s => (s.Id, s.StartPoint!.X, s.StartPoint.Y, s.EndPoint!.X, s.EndPoint.Y)).ToArray();
        Assert.NotNull(SketchRegionLoopOrderResolver.Resolve(segments, new uint[] { 1, 2, 3, 4 }));
        var after = segments.Select(s => (s.Id, s.StartPoint!.X, s.StartPoint.Y, s.EndPoint!.X, s.EndPoint.Y)).ToArray();
        Assert.Equal(before, after);
    }

    private static SketchFeature? FindSketch(SwFile file, IReadOnlyList<uint> edges)
    {
        return file.Sketches.FirstOrDefault(sketch =>
            edges.All(id => sketch.Segments.Any(segment => segment.Id == id)));
    }

    private static bool IsSameCyclicOrder(uint[] expected, IReadOnlyList<uint> actual)
    {
        if (actual.Count != expected.Length) return false;
        for (int start = 0; start < expected.Length; start++)
        {
            bool match = true;
            for (int i = 0; i < expected.Length; i++)
            {
                if (actual[i] != expected[(start + i) % expected.Length]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private static SketchSegment Segment(uint id, double x1, double y1, double x2, double y2) =>
        new SketchSegment
        {
            Id = id,
            Kind = SketchSegmentKind.Line,
            StartPoint = new SketchPoint { X = x1, Y = y1 },
            EndPoint = new SketchPoint { X = x2, Y = y2 }
        };

    private static int ChooserPayloadEnd(SldFile.Mfc.MfcDocument doc,
        SldFile.Mfc.MfcElement chooser, int fallback)
    {
        var elements = doc.AllElements;
        for (int i = 0; i < elements.Count; i++)
        {
            if (!ReferenceEquals(elements[i], chooser)) continue;
            return i + 1 < elements.Count ? elements[i + 1].TagOffset : fallback;
        }
        return fallback;
    }

    private static string SamplePath(string relativePath)
    {
        string relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var direct = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", relative + ".SLDPRT");
        if (File.Exists(direct)) return direct;
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", relative + ".SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample Features2026/{relative}.SLDPRT not found");
    }
}
