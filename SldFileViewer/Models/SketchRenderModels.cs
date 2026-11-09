using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SldFile.Mfc;

namespace SldFileViewer.Models;

public partial class RenderPoint : ObservableObject
{
    public uint Id { get; init; }
    public int TableIndex { get; init; }
    public double WorldX { get; init; }
    public double WorldY { get; init; }
    public double WorldZ { get; init; }

    public string XInMm => $"{WorldX * 1000.0:0.##}";
    public string YInMm => $"{WorldY * 1000.0:0.##}";
    public string ZInMm => $"{WorldZ * 1000.0:0.##}";
    public string CoordMm => $"({WorldX * 1000.0:0.##}, {WorldY * 1000.0:0.##})";

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
    public bool IsCircle { get; init; }
    public int Direction { get; init; } = 1;

    public string KindName => Kind switch
    {
        SketchSegmentKind.Line => "直线 (Line)",
        SketchSegmentKind.Arc => (IsCircle || (Radius.HasValue && (Start == null || End == null || (Math.Abs(Start.WorldX - End.WorldX) < 1e-5 && Math.Abs(Start.WorldY - End.WorldY) < 1e-5)))) ? "整圆 (Circle)" : "圆弧 (Arc)",
        SketchSegmentKind.Ellipse => "椭圆 (Ellipse)",
        SketchSegmentKind.Spline => "样条 (Spline)",
        SketchSegmentKind.Text => "文本 (Text)",
        _ => "未知 (Unknown)"
    };

    public string StartPointDisplay => Start != null
        ? $"#{Start.Id} ({Start.WorldX * 1000.0:0.##}, {Start.WorldY * 1000.0:0.##})"
        : "-";

    public string EndPointDisplay => End != null
        ? $"#{End.Id} ({End.WorldX * 1000.0:0.##}, {End.WorldY * 1000.0:0.##})"
        : "-";

    public string CenterPointDisplay => Center != null
        ? $"#{Center.Id} ({Center.WorldX * 1000.0:0.##}, {Center.WorldY * 1000.0:0.##})"
        : "-";

    public double? Length
    {
        get
        {
            if (Kind == SketchSegmentKind.Line && Start != null && End != null)
            {
                double dx = End.WorldX - Start.WorldX;
                double dy = End.WorldY - Start.WorldY;
                return Math.Sqrt(dx * dx + dy * dy);
            }
            if (Radius.HasValue)
            {
                bool closed = IsCircle || (Start == null || End == null || (Math.Abs(Start.WorldX - End.WorldX) < 1e-5 && Math.Abs(Start.WorldY - End.WorldY) < 1e-5));
                if (closed) return 2.0 * Math.PI * Radius.Value;
                if (Center != null && Start != null && End != null)
                {
                    double aStart = Math.Atan2(Start.WorldY - Center.WorldY, Start.WorldX - Center.WorldX);
                    double aEnd = Math.Atan2(End.WorldY - Center.WorldY, End.WorldX - Center.WorldX);
                    int dir = Direction != 0 ? Direction : 1;
                    double sweep = dir >= 0 ? (aEnd - aStart) : (aStart - aEnd);
                    while (sweep < 0) sweep += 2 * Math.PI;
                    while (sweep >= 2 * Math.PI) sweep -= 2 * Math.PI;
                    return Radius.Value * sweep;
                }
                return 2.0 * Math.PI * Radius.Value;
            }
            return null;
        }
    }

    public string LengthDisplay => Length.HasValue ? $"{Length.Value * 1000.0:0.##} mm" : "-";
    public string RadiusDisplay
    {
        get
        {
            if (!Radius.HasValue) return "-";
            bool closed = IsCircle || (Start == null || End == null || (Math.Abs(Start.WorldX - End.WorldX) < 1e-5 && Math.Abs(Start.WorldY - End.WorldY) < 1e-5));
            return closed
                ? $"R {Radius.Value * 1000.0:0.##} mm (φ {Radius.Value * 2000.0:0.##} mm)"
                : $"R {Radius.Value * 1000.0:0.##} mm";
        }
    }

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

public partial class RenderRelationItem : ObservableObject
{
    public SketchRelationKind Kind { get; init; }
    public int TypeCode { get; init; }
    public string TypeName { get; init; } = string.Empty;
    public string Layout { get; init; } = string.Empty;
    public int EntityCount { get; init; }
    public string EntitiesText { get; init; } = string.Empty;
}

public partial class RenderDimensionItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string DimensionType { get; set; } = "线性尺寸";
    public double? ValueInMeters { get; init; }
    public string FormattedValue { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string ValueSource { get; init; } = string.Empty;
    public string TargetEntity { get; set; } = string.Empty;

    public double? TextX { get; set; }
    public double? TextY { get; set; }
    public double? DirectionX { get; set; }
    public double? DirectionY { get; set; }

    public string TextPositionDisplay => (TextX.HasValue && TextY.HasValue) ? $"({TextX.Value * 1000.0:0.##}, {TextY.Value * 1000.0:0.##}) mm" : "-";
    public string DirectionDisplay => (DirectionX.HasValue && DirectionY.HasValue) ? $"({DirectionX.Value:0.##}, {DirectionY.Value:0.##})" : "-";

    // Geometric attachment for canvas rendering
    public bool HasAttachment { get; set; }
    public SketchDimensionType DimensionTypeEnum { get; set; } = SketchDimensionType.Linear;
    public Point P1 { get; set; }
    public Point P2 { get; set; }
    public Point? Center { get; set; }
    public double? Radius { get; set; }
    public double OffsetPixels { get; set; } = 30.0;

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
