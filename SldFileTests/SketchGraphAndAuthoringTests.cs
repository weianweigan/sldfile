using System;
using System.Linq;
using Xunit;
using SldFile;
using SldFile.Sketches;

namespace SldFileTests;

public class SketchGraphAndAuthoringTests
{
    private static SwFile Open(string name) => SwFile.Open(TestFileProvider.Resolve("Sketches2026", name + ".SLDPRT"));

    [Fact]
    public void ParallelRelation_ResolvesReferencedSegmentsDirectly()
    {
        using var file = Open("parallel");
        var sketch = Assert.Single(file.GetSketches());
        var rel = Assert.Single(sketch.Relations);

        Assert.Equal(SketchRelationKind.Parallel, rel.Kind);
        Assert.Equal(2, rel.References.Count);

        // Verify direct navigation via AsSegment()
        var seg0 = rel.References[0].AsSegment();
        var seg1 = rel.References[1].AsSegment();

        Assert.NotNull(seg0);
        Assert.NotNull(seg1);
        Assert.NotSame(seg0, seg1);
        Assert.Equal(SketchSegmentKind.Line, seg0.Kind);
        Assert.Equal(SketchSegmentKind.Line, seg1.Kind);

        // Verify helper method
        var segments = rel.GetSegments();
        Assert.Equal(2, segments.Count);
        Assert.Contains(seg0, segments);
        Assert.Contains(seg1, segments);
    }

    [Fact]
    public void ConcentricRelation_ResolvesReferencedArcsDirectly()
    {
        using var file = Open("concentric");
        var sketch = Assert.Single(file.GetSketches());
        var rel = Assert.Single(sketch.Relations);

        Assert.Equal(SketchRelationKind.Concentric, rel.Kind);
        Assert.Equal(2, rel.References.Count);

        var arc0 = rel.References[0].AsSegment();
        var arc1 = rel.References[1].AsSegment();

        Assert.NotNull(arc0);
        Assert.NotNull(arc1);
        Assert.Equal(SketchSegmentKind.Arc, arc0.Kind);
        Assert.Equal(SketchSegmentKind.Arc, arc1.Kind);
        Assert.Equal(2, rel.GetSegments().Count);
    }

    [Fact]
    public void CoincidentPointLine_ResolvesBothPointAndSegment()
    {
        using var file = Open("coincident_point_line");
        var sketch = Assert.Single(file.GetSketches());
        var rel = Assert.Single(sketch.Relations);

        Assert.Equal(SketchRelationKind.Coincident, rel.Kind);
        var points = rel.GetPoints();
        var segments = rel.GetSegments();

        Assert.Single(points);
        Assert.Single(segments);
        Assert.Equal(SketchSegmentKind.Line, segments[0].Kind);
    }

    [Fact]
    public void Dimension_ResolvesAssociatedRelationAndAttachedEntities()
    {
        using var file = Open("dimension10");
        var sketch = Assert.Single(file.GetSketches());
        var dim = Assert.Single(sketch.Dimensions);

        Assert.Equal("D1", dim.Name);
        Assert.NotNull(dim.Relation);
        Assert.Equal(SketchRelationKind.Distance, dim.Relation.Kind);

        var attached = dim.GetAttachedEntities();
        Assert.Equal(2, attached.Count);

        var attachedPoints = dim.GetAttachedPoints();
        Assert.Equal(2, attachedPoints.Count);
        Assert.Contains(sketch.Points[0], attachedPoints);
        Assert.Contains(sketch.Points[1], attachedPoints);
    }

    [Fact]
    public void Authoring_AddPointsAndSegments_BuildsValidSketchGraph()
    {
        var sketch = new SketchFeature { Name = "CustomSketch" };

        var p1 = sketch.AddPoint(0, 0);
        var p2 = sketch.AddPoint(0.05, 0);
        var p3 = sketch.AddPoint(0.05, 0.03);

        Assert.Equal(3, sketch.Points.Count);
        Assert.Equal(0, p1.TableIndex);
        Assert.Equal(1, p2.TableIndex);
        Assert.Equal(2, p3.TableIndex);

        var l1 = sketch.AddLine(p1, p2);
        var l2 = sketch.AddLine(p2, p3);

        Assert.Equal(2, sketch.Segments.Count);
        Assert.Same(p1, l1.StartPoint);
        Assert.Same(p2, l1.EndPoint);
        Assert.Same(p2, l2.StartPoint);
        Assert.Same(p3, l2.EndPoint);

        var circle = sketch.AddCircle(0.02, 0.01, 0.005);
        Assert.Equal(3, sketch.Segments.Count);
        Assert.True(circle.IsCircle);
        Assert.Equal(0.005, circle.Radius);
    }

    [Fact]
    public void Authoring_AddRelationsAndDimensions_EstablishesBidirectionalNavigation()
    {
        var sketch = new SketchFeature { Name = "ConstrainedSketch" };

        var p1 = sketch.AddPoint(0, 0);
        var p2 = sketch.AddPoint(0.05, 0);
        var line1 = sketch.AddLine(p1, p2);

        var p3 = sketch.AddPoint(0, 0.02);
        var p4 = sketch.AddPoint(0.05, 0.02);
        var line2 = sketch.AddLine(p3, p4);

        // Add constraints
        var hRel = sketch.AddHorizontal(line1);
        Assert.Equal(SketchRelationKind.Horizontal, hRel.Kind);
        Assert.Same(line1, hRel.References[0].AsSegment());

        var pRel = sketch.AddParallel(line1, line2);
        Assert.Equal(SketchRelationKind.Parallel, pRel.Kind);
        Assert.Equal(2, pRel.GetSegments().Count);
        Assert.Contains(line1, pRel.GetSegments());
        Assert.Contains(line2, pRel.GetSegments());

        // Add distance dimension
        var dim = sketch.AddDistanceDimension(p1, p2, "D1", 0.05);
        Assert.Equal("D1", dim.Name);
        Assert.Equal(0.05, dim.SystemValue);
        Assert.NotNull(dim.Relation);
        Assert.Equal(2, dim.GetAttachedPoints().Count);
        Assert.Contains(p1, dim.GetAttachedPoints());
        Assert.Contains(p2, dim.GetAttachedPoints());
    }

    [Fact]
    public void Authoring_AllConstraintTypes_CanBeProgrammaticallyAdded()
    {
        var sketch = new SketchFeature { Name = "AllConstraintsSketch" };
        var l1 = sketch.AddLine(0, 0, 1, 0);
        var l2 = sketch.AddLine(0, 0, 0, 1);
        var c1 = sketch.AddCircle(2, 2, 0.5);
        var c2 = sketch.AddCircle(2, 2, 0.8);
        var pt = sketch.AddPoint(0, 0);

        sketch.AddHorizontal(l1);
        sketch.AddVertical(l2);
        sketch.AddPerpendicular(l1, l2);
        sketch.AddEqualLength(l1, l2);
        sketch.AddCoincident(pt, l1);
        sketch.AddConcentric(c1, c2);
        sketch.AddTangent(c1, l1);
        sketch.AddFixed(l1);

        Assert.Equal(8, sketch.Relations.Count);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Horizontal);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Vertical);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Perpendicular);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.EqualLength);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Coincident);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Concentric);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Tangent);
        Assert.Contains(sketch.Relations, r => r.Kind == SketchRelationKind.Fixed);
    }
}
