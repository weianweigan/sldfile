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

        // If no direct binary segments were decoded, but dimensions exist (e.g. D1, D2 in absorbed sketch):
        if (sketch.Segments.Count == 0 && sketch.Dimensions.Count > 0)
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

            var p1 = sketch.AddPoint(-w / 2, -h / 2);
            var p2 = sketch.AddPoint(w / 2, -h / 2);
            var p3 = sketch.AddPoint(w / 2, h / 2);
            var p4 = sketch.AddPoint(-w / 2, h / 2);

            sketch.AddLine(p1, p2);
            sketch.AddLine(p2, p3);
            sketch.AddLine(p3, p4);
            sketch.AddLine(p4, p1);

            Diagnostics.Add("提示: 当前草图图元由特征尺寸参数 (D1, D2) 反解重构生成几何线框。");
        }

        // Infer constraints/relations if none exist from binary stream
        InferRelationsIfEmpty(sketch);

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

        Bounds = bounds;
        StatusInfo = $"图元: {Segments.Count} 条 | 点: {Points.Count} 个 | 约束: {sketch.Relations.Count} 项 | 尺寸: {sketch.Dimensions.Count} 个";

        RequestFitView?.Invoke();
    }

    private static void InferRelationsIfEmpty(SketchFeature sketch)
    {
        if (sketch.Relations.Count > 0)
            return;

        var lines = sketch.Segments.Where(s => s.Kind == SketchSegmentKind.Line && s.StartPoint != null && s.EndPoint != null).ToList();
        var arcs = sketch.Segments.Where(s => (s.Kind == SketchSegmentKind.Arc || s.Kind == SketchSegmentKind.Ellipse) && s.CenterPoint != null).ToList();

        // 1. Horizontal and Vertical lines
        foreach (var line in lines)
        {
            double dx = Math.Abs(line.EndPoint!.X - line.StartPoint!.X);
            double dy = Math.Abs(line.EndPoint.Y - line.StartPoint.Y);
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) continue;

            if (dy / len < 1e-3 || dy < 1e-5)
            {
                sketch.AddHorizontal(line);
            }
            else if (dx / len < 1e-3 || dx < 1e-5)
            {
                sketch.AddVertical(line);
            }
        }

        // 2. Coincident points and Perpendicular/Parallel/EqualLength between lines
        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                var l1 = lines[i];
                var l2 = lines[j];

                SketchPoint? pA = null;
                SketchPoint? pB = null;

                if (PointsMatch(l1.StartPoint, l2.StartPoint)) { pA = l1.StartPoint; pB = l2.StartPoint; }
                else if (PointsMatch(l1.StartPoint, l2.EndPoint)) { pA = l1.StartPoint; pB = l2.EndPoint; }
                else if (PointsMatch(l1.EndPoint, l2.StartPoint)) { pA = l1.EndPoint; pB = l2.StartPoint; }
                else if (PointsMatch(l1.EndPoint, l2.EndPoint)) { pA = l1.EndPoint; pB = l2.EndPoint; }

                if (pA != null && pB != null)
                {
                    sketch.AddCoincident(pA, pB);

                    // Check perpendicularity at shared vertex
                    double dx1 = l1.EndPoint!.X - l1.StartPoint!.X;
                    double dy1 = l1.EndPoint.Y - l1.StartPoint.Y;
                    double dx2 = l2.EndPoint!.X - l2.StartPoint!.X;
                    double dy2 = l2.EndPoint.Y - l2.StartPoint.Y;
                    double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                    double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                    if (len1 > 1e-6 && len2 > 1e-6)
                    {
                        double cosTheta = (dx1 * dx2 + dy1 * dy2) / (len1 * len2);
                        if (Math.Abs(cosTheta) < 1e-3)
                        {
                            sketch.AddPerpendicular(l1, l2);
                        }
                    }
                }
                else
                {
                    // Check parallel and equal length
                    double dx1 = l1.EndPoint!.X - l1.StartPoint!.X;
                    double dy1 = l1.EndPoint.Y - l1.StartPoint.Y;
                    double dx2 = l2.EndPoint!.X - l2.StartPoint!.X;
                    double dy2 = l2.EndPoint.Y - l2.StartPoint.Y;
                    double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                    double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                    if (len1 > 1e-6 && len2 > 1e-6)
                    {
                        double cross = Math.Abs((dx1 * dy2 - dy1 * dx2) / (len1 * len2));
                        if (cross < 1e-3)
                        {
                            sketch.AddParallel(l1, l2);
                        }

                        if (Math.Abs(len1 - len2) < 1e-5)
                        {
                            sketch.AddEqualLength(l1, l2);
                        }
                    }
                }
            }
        }

        // 3. Concentric arcs
        for (int i = 0; i < arcs.Count; i++)
        {
            for (int j = i + 1; j < arcs.Count; j++)
            {
                if (PointsMatch(arcs[i].CenterPoint, arcs[j].CenterPoint))
                {
                    sketch.AddConcentric(arcs[i], arcs[j]);
                }
            }
        }

        static bool PointsMatch(SketchPoint? a, SketchPoint? b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            return Math.Abs(a.X - b.X) < 1e-5 && Math.Abs(a.Y - b.Y) < 1e-5 && Math.Abs(a.Z - b.Z) < 1e-5;
        }
    }

    [RelayCommand]
    private void FitView()
    {
        RequestFitView?.Invoke();
    }
}
