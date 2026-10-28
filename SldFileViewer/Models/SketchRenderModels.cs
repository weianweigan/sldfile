using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SldFile.Sketches;

namespace SldFileViewer.Models;

public partial class RenderPoint : ObservableObject
{
    public uint Id { get; init; }
    public int TableIndex { get; init; }
    public double WorldX { get; init; }
    public double WorldY { get; init; }
    public double WorldZ { get; init; }

    [ObservableProperty]
    private Point _screenPoint;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHovered;
}

public partial class RenderSegment : ObservableObject
{
    public uint Id { get; init; }
    public SketchSegmentKind Kind { get; init; }
    public bool IsConstruction { get; init; }
    public RenderPoint? Start { get; init; }
    public RenderPoint? End { get; init; }
    public RenderPoint? Center { get; init; }
    public double? Radius { get; init; }
    public string? Text { get; init; }

    [ObservableProperty]
    private Geometry? _cachedGeometry;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHovered;
}

public partial class RenderConstraintGlyph : ObservableObject
{
    public SketchRelationKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public Geometry? Icon { get; init; }

    [ObservableProperty]
    private Point _screenPosition;

    [ObservableProperty]
    private bool _isHovered;
}

public partial class RenderDimensionItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public double? ValueInMeters { get; init; }
    public string DisplayText { get; init; } = string.Empty;

    [ObservableProperty]
    private Point _screenPosition;
}

public sealed class SketchBounds
{
    public double MinX { get; set; } = double.PositiveInfinity;
    public double MaxX { get; set; } = double.NegativeInfinity;
    public double MinY { get; set; } = double.PositiveInfinity;
    public double MaxY { get; set; } = double.NegativeInfinity;

    public double Width => double.IsInfinity(MaxX - MinX) ? 0 : Math.Max(MaxX - MinX, 0.001);
    public double Height => double.IsInfinity(MaxY - MinY) ? 0 : Math.Max(MaxY - MinY, 0.001);
    public double CenterX => (MinX + MaxX) / 2.0;
    public double CenterY => (MinY + MaxY) / 2.0;

    public void Include(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y)) return;
        if (x < MinX) MinX = x;
        if (x > MaxX) MaxX = x;
        if (y < MinY) MinY = y;
        if (y > MaxY) MaxY = y;
    }
}
