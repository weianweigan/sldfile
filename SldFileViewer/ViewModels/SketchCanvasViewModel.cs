using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFile.Sketches;
using SldFileViewer.Models;

namespace SldFileViewer.ViewModels;

public partial class SketchCanvasViewModel : ObservableObject
{
    [ObservableProperty]
    private SketchFeature? _sketch;

    [ObservableProperty]
    private string _sketchName = string.Empty;

    [ObservableProperty]
    private bool _is3D;

    [ObservableProperty]
    private bool _isPartial;

    [ObservableProperty]
    private bool _showPoints = true;

    [ObservableProperty]
    private bool _showConstraints = true;

    [ObservableProperty]
    private bool _showDimensions = true;

    [ObservableProperty]
    private bool _showConstruction = true;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private SketchBounds _bounds = new();

    [ObservableProperty]
    private RenderPoint? _selectedPoint;

    [ObservableProperty]
    private RenderSegment? _selectedSegment;

    [ObservableProperty]
    private string _statusInfo = string.Empty;

    public ObservableCollection<RenderPoint> Points { get; } = new();
    public ObservableCollection<RenderSegment> Segments { get; } = new();
    public ObservableCollection<RenderConstraintGlyph> Constraints { get; } = new();
    public ObservableCollection<RenderDimensionItem> Dimensions { get; } = new();
    public ObservableCollection<string> Diagnostics { get; } = new();

    public event Action? RequestFitView;

    public void LoadSketch(SketchFeature sketch)
    {
        Sketch = sketch;
        SketchName = sketch.Name;
        Is3D = sketch.Is3D ?? false;
        IsPartial = sketch.IsPartial;

        Points.Clear();
        Segments.Clear();
        Constraints.Clear();
        Dimensions.Clear();
        Diagnostics.Clear();

        foreach (var diag in sketch.Diagnostics) Diagnostics.Add(diag);

        var bounds = new SketchBounds();

        // 1. Build Points
        var pointMap = new System.Collections.Generic.Dictionary<int, RenderPoint>();
        foreach (var p in sketch.Points)
        {
            var rp = new RenderPoint
            {
                Id = p.Id,
                TableIndex = p.TableIndex,
                WorldX = p.X,
                WorldY = p.Y,
                WorldZ = p.Z
            };
            bounds.Include(p.X, p.Y);
            pointMap[p.TableIndex] = rp;
            Points.Add(rp);
        }

        // 2. Build Segments
        foreach (var s in sketch.Segments)
        {
            RenderPoint? start = s.StartPoint != null && pointMap.TryGetValue(s.StartPoint.TableIndex, out var sp) ? sp : null;
            RenderPoint? end = s.EndPoint != null && pointMap.TryGetValue(s.EndPoint.TableIndex, out var ep) ? ep : null;
            RenderPoint? center = s.CenterPoint != null && pointMap.TryGetValue(s.CenterPoint.TableIndex, out var cp) ? cp : null;

            if (center != null && s.Radius.HasValue)
            {
                double r = s.Radius.Value;
                bounds.Include(center.WorldX - r, center.WorldY - r);
                bounds.Include(center.WorldX + r, center.WorldY + r);
            }

            var rs = new RenderSegment
            {
                Id = s.Id,
                Kind = s.Kind,
                IsConstruction = s.IsConstruction,
                Start = start,
                End = end,
                Center = center,
                Radius = s.Radius,
                Text = s.Text
            };
            Segments.Add(rs);
        }

        // 3. Build Constraints
        foreach (var r in sketch.Relations)
        {
            Constraints.Add(new RenderConstraintGlyph
            {
                Kind = r.Kind,
                Name = r.Kind.ToString()
            });
        }

        // 4. Build Dimensions
        foreach (var d in sketch.Dimensions)
        {
            double? mm = d.SystemValue.HasValue ? d.SystemValue.Value * 1000.0 : null;
            string display = mm.HasValue ? $"{mm.Value:0.##} mm" : (d.DisplayValue ?? "-");

            Dimensions.Add(new RenderDimensionItem
            {
                Name = d.Name,
                ValueInMeters = d.SystemValue,
                DisplayText = $"{d.Name} = {display}"
            });
        }

        // If no direct binary segments were decoded, but dimensions exist (e.g. D1, D2 in absorbed sketch):
        if (Segments.Count == 0 && sketch.Dimensions.Count > 0)
        {
            double w = 0.1;
            double h = 0.1;
            var dimList = sketch.Dimensions.ToList();
            if (dimList.Count >= 1 && dimList[0].SystemValue.HasValue && dimList[0].SystemValue!.Value > 0)
                w = dimList[0].SystemValue!.Value;
            if (dimList.Count >= 2 && dimList[1].SystemValue.HasValue && dimList[1].SystemValue!.Value > 0)
                h = dimList[1].SystemValue!.Value;
            else
                h = w;

            var p1 = new RenderPoint { Id = 1, TableIndex = 0, WorldX = -w / 2, WorldY = -h / 2 };
            var p2 = new RenderPoint { Id = 2, TableIndex = 1, WorldX = w / 2, WorldY = -h / 2 };
            var p3 = new RenderPoint { Id = 3, TableIndex = 2, WorldX = w / 2, WorldY = h / 2 };
            var p4 = new RenderPoint { Id = 4, TableIndex = 3, WorldX = -w / 2, WorldY = h / 2 };

            Points.Add(p1); Points.Add(p2); Points.Add(p3); Points.Add(p4);
            bounds.Include(-w / 2, -h / 2);
            bounds.Include(w / 2, h / 2);

            Segments.Add(new RenderSegment { Id = 1, Kind = SketchSegmentKind.Line, Start = p1, End = p2, Text = "Line 1 (底边)" });
            Segments.Add(new RenderSegment { Id = 2, Kind = SketchSegmentKind.Line, Start = p2, End = p3, Text = "Line 2 (右边)" });
            Segments.Add(new RenderSegment { Id = 3, Kind = SketchSegmentKind.Line, Start = p3, End = p4, Text = "Line 3 (顶边)" });
            Segments.Add(new RenderSegment { Id = 4, Kind = SketchSegmentKind.Line, Start = p4, End = p1, Text = "Line 4 (左边)" });

            Diagnostics.Add("提示: 当前草图图元由特征尺寸参数 (D1, D2) 反解重构生成预览。");
        }

        Bounds = bounds;
        StatusInfo = $"图元: {Segments.Count} 条 | 点: {Points.Count} 个 | 约束: {sketch.Relations.Count} 项 | 尺寸: {sketch.Dimensions.Count} 个";

        RequestFitView?.Invoke();
    }

    [RelayCommand]
    private void FitView()
    {
        RequestFitView?.Invoke();
    }
}
