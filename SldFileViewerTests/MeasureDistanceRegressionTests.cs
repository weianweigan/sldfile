using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using SldFileViewer.Models;
using Xunit;

namespace SldFileViewerTests;

public class MeasureDistanceRegressionTests
{
    private static MeasureElement Circle(Point3D center, double radius, Vector3D? normal = null) =>
        new(center, normal ?? new Vector3D(0, 0, 1), radius, new List<Point3D>());

    [Fact]
    public void NestedAndAxiallySeparatedCirclesUseCircumferenceDistances()
    {
        var outer = Circle(new Point3D(), 10);
        var nested = new MeasureResult(outer, Circle(new Point3D(), 2));
        Assert.Equal(8, nested.MinDistance!.Value, 8);
        Assert.Equal(12, nested.MaxDistance!.Value, 8);
        var separated = new MeasureResult(outer, Circle(new Point3D(0, 0, 5), 10));
        Assert.Equal(5, separated.MinDistance!.Value, 8);
        Assert.Equal(Math.Sqrt(425), separated.MaxDistance!.Value, 8);
    }

    [Fact]
    public void PointOnCircleAxisMeasuresDistanceToCircumference()
    {
        var result = new MeasureResult(Circle(new Point3D(), 3), new MeasureElement(new Point3D(0, 0, 4)));
        Assert.Equal(5, result.MinDistance!.Value, 8);
        Assert.Equal(5, result.MaxDistance!.Value, 8);
    }

    [Fact]
    public void SkewCirclesDoNotClaimUnsupportedEdgeDistances()
    {
        var result = new MeasureResult(Circle(new Point3D(), 3),
            Circle(new Point3D(0, 0, 4), 2, new Vector3D(1, 0, 0)));
        Assert.Equal(4, result.Value, 8);
        Assert.Null(result.MinDistance);
        Assert.Null(result.MaxDistance);
    }

    [Fact]
    public void CircleAndEdgeReportCenterDistanceInsteadOfZeroPlanarDistance()
    {
        var result = new MeasureResult(Circle(new Point3D(), 3),
            new MeasureElement(new Point3D(4, -1, 0), new Point3D(4, 1, 0)));
        Assert.Equal(4, result.Value, 8);
        Assert.Contains("中心距", result.MainText);
    }
}
