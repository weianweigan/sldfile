using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFile.Mfc;
using SldFileViewer.Models;

namespace SldFileViewer.ViewModels;

public partial class SketchCanvasViewModel : ObservableObject
{
    [ObservableProperty]
    private SketchFeatureModel? _sketch;

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
    public ObservableCollection<RenderRelationItem> Relations { get; } = new();
    public ObservableCollection<RenderDimensionItem> Dimensions { get; } = new();
    public ObservableCollection<string> Diagnostics { get; } = new();

    public event Action? RequestFitView;

    public void LoadSketch(SketchFeatureModel sketch)
    {
        Sketch = sketch;
        SketchName = sketch.Name;
        Is3D = sketch.Is3D ?? false;
        IsPartial = sketch.IsPartial;

        Points.Clear();
        Segments.Clear();
        Constraints.Clear();
        Relations.Clear();
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

            bool isCircle = s.IsCircle == true;
            if (!isCircle && s.Kind == SketchSegmentKind.Arc)
            {
                if (start == null || end == null)
                {
                    isCircle = true;
                }
                else
                {
                    double dx = start.WorldX - end.WorldX;
                    double dy = start.WorldY - end.WorldY;
                    if (Math.Sqrt(dx * dx + dy * dy) < 1e-5)
                    {
                        isCircle = true;
                    }
                }
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
                Text = s.Text,
                IsCircle = isCircle,
                Direction = s.Direction ?? 1
            };
            Segments.Add(rs);
        }

        // 3. Build Constraints & Relations
        foreach (var r in sketch.Relations)
        {
            Constraints.Add(new RenderConstraintGlyph
            {
                Kind = r.Kind,
                Name = r.Kind.ToString()
            });

            var entities = r.GetEntities();
            string entStr;
            if (entities.Count > 0)
            {
                entStr = string.Join(", ", entities.Select(e => e switch
                {
                    SketchSegmentModel seg => $"{seg.Kind} #{seg.Id}",
                    SketchPointModel pt => $"点 #{pt.Id} ({pt.X * 1000.0:0.#}, {pt.Y * 1000.0:0.#})",
                    _ => e.ToString() ?? ""
                }));
            }
            else
            {
                entStr = r.References.Count > 0 ? $"实体引用 ({r.References.Count} 项)" : "-";
            }

            string kindCn = r.Kind switch
            {
                SketchRelationKind.Horizontal => "水平 (Horizontal)",
                SketchRelationKind.Vertical => "垂直 (Vertical)",
                SketchRelationKind.Coincident => "重合 (Coincident)",
                SketchRelationKind.Concentric => "同心 (Concentric)",
                SketchRelationKind.Parallel => "平行 (Parallel)",
                SketchRelationKind.Perpendicular => "垂直相交 (Perpendicular)",
                SketchRelationKind.Tangent => "相切 (Tangent)",
                SketchRelationKind.EqualLength => "等长 (Equal)",
                SketchRelationKind.Symmetric => "对称 (Symmetric)",
                SketchRelationKind.Fixed => "固定 (Fixed)",
                SketchRelationKind.Midpoint => "中点 (Midpoint)",
                SketchRelationKind.Intersection => "交点 (Intersection)",
                SketchRelationKind.Distance => "距离尺寸 (Distance)",
                SketchRelationKind.Angle => "角度尺寸 (Angle)",
                SketchRelationKind.Diameter => "直径尺寸 (Diameter)",
                SketchRelationKind.Radius => "半径尺寸 (Radius)",
                SketchRelationKind.SketchOffset => "等距实体 (Offset)",
                SketchRelationKind.Patterned => "阵列 (Patterned)",
                _ => r.Kind.ToString()
            };

            Relations.Add(new RenderRelationItem
            {
                Kind = r.Kind,
                TypeCode = r.TypeCode,
                TypeName = kindCn,
                Layout = r.Layout,
                EntityCount = r.References.Count,
                EntitiesText = entStr
            });
        }

        // 4. Build Dimensions
        var usedSegmentIds = new HashSet<uint>();
        int dimIdx = 0;
        foreach (var d in sketch.Dimensions)
        {
            double? valInMeters = d.SystemValue;
            string displayVal = d.DisplayValue ?? string.Empty;
            bool isAngle = displayVal.Contains("°");

            if (!valInMeters.HasValue && !string.IsNullOrEmpty(displayVal))
            {
                string numStr = displayVal.Replace("mm", "").Replace("m", "").Replace("°", "").Trim();
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
                {
                    if (!isAngle)
                    {
                        valInMeters = parsedVal > 1.0 ? parsedVal / 1000.0 : parsedVal;
                    }
                }
            }

            string formattedVal;
            if (isAngle)
                formattedVal = displayVal;
            else if (valInMeters.HasValue)
                formattedVal = $"{valInMeters.Value * 1000.0:0.##} mm";
            else
                formattedVal = !string.IsNullOrEmpty(displayVal) ? displayVal : "-";

            string dimTypeStr = d.DimensionType switch
            {
                SketchDimensionType.Angle => "角度尺寸",
                SketchDimensionType.Diameter => "直径尺寸",
                SketchDimensionType.Radius => "半径尺寸",
                SketchDimensionType.ArcLength => "弧长尺寸",
                SketchDimensionType.Ordinate => "坐标尺寸",
                SketchDimensionType.Chamfer => "倒角尺寸",
                _ => isAngle ? "角度尺寸" : "线性尺寸"
            };

            var item = new RenderDimensionItem
            {
                Name = d.Name,
                DimensionType = dimTypeStr,
                DimensionTypeEnum = d.DimensionType != SketchDimensionType.Unknown ? d.DimensionType : (isAngle ? SketchDimensionType.Angle : SketchDimensionType.Linear),
                ValueInMeters = valInMeters,
                FormattedValue = formattedVal,
                DisplayText = $"{d.Name} = {formattedVal}",
                ValueSource = d.ValueSource == "BinaryParameter" ? "特征参数 (Binary)" : (d.ValueSource == "DisplayDimension" ? "显示尺寸 (DisplayDim)" : "配置参数 (Keyword)"),
                TextX = d.TextX,
                TextY = d.TextY,
                DirectionX = d.DirectionX,
                DirectionY = d.DirectionY
            };

            // Match with geometry
            RenderSegment? matchedSeg = null;

            // 1. Try relation attached entities first
            if (d.Relation != null)
            {
                var relSegs = d.Relation.GetSegments();
                if (relSegs.Count > 0)
                {
                    matchedSeg = Segments.FirstOrDefault(s => s.Id == relSegs[0].Id);
                }
            }

            // 2. Try match segment by length / value
            if (matchedSeg == null && !isAngle && valInMeters.HasValue && valInMeters.Value > 0)
            {
                matchedSeg = Segments.FirstOrDefault(s => !usedSegmentIds.Contains(s.Id) && s.Start != null && s.End != null && s.Length.HasValue &&
                    Math.Abs(s.Length.Value - valInMeters.Value) < Math.Max(valInMeters.Value * 0.05, 0.002));

                if (matchedSeg == null)
                {
                    matchedSeg = Segments.FirstOrDefault(s => !usedSegmentIds.Contains(s.Id) && s.Start != null && s.End != null &&
                        (Math.Abs(Math.Abs(s.End.WorldX - s.Start.WorldX) - valInMeters.Value) < Math.Max(valInMeters.Value * 0.05, 0.002) ||
                         Math.Abs(Math.Abs(s.End.WorldY - s.Start.WorldY) - valInMeters.Value) < Math.Max(valInMeters.Value * 0.05, 0.002)));
                }

                if (matchedSeg == null)
                {
                    matchedSeg = Segments.FirstOrDefault(s => !usedSegmentIds.Contains(s.Id) && s.Radius.HasValue &&
                        (Math.Abs(s.Radius.Value - valInMeters.Value) < 0.002 || Math.Abs(s.Radius.Value * 2.0 - valInMeters.Value) < 0.002));
                    if (matchedSeg != null && matchedSeg.Radius.HasValue)
                    {
                        item.DimensionType = matchedSeg.Radius.Value * 2.0 <= valInMeters.Value * 1.05 ? "直径尺寸" : "半径尺寸";
                    }
                }
            }

            // 3. Fallback to unused visible segments
            if (matchedSeg == null)
            {
                matchedSeg = Segments.FirstOrDefault(s => !usedSegmentIds.Contains(s.Id) && s.Start != null && s.End != null && !s.IsConstruction);
            }

            if (d.WitnessPoint1X.HasValue && d.WitnessPoint2X.HasValue)
            {
                item.HasAttachment = true;
                item.P1 = new Point(d.WitnessPoint1X.Value, d.WitnessPoint1Y!.Value);
                item.P2 = new Point(d.WitnessPoint2X.Value, d.WitnessPoint2Y!.Value);
                item.OffsetPixels = 24.0 + (dimIdx % 3) * 16.0;
                if (d.CenterX.HasValue && d.Radius.HasValue)
                {
                    item.Center = new Point(d.CenterX.Value, d.CenterY ?? 0.0);
                    item.Radius = d.Radius.Value;
                }
                if (matchedSeg != null)
                {
                    usedSegmentIds.Add(matchedSeg.Id);
                    item.TargetEntity = $"{matchedSeg.KindName} #{matchedSeg.Id}";
                }
                else
                {
                    item.TargetEntity = item.DimensionTypeEnum switch
                    {
                        SketchDimensionType.Radius => "圆弧半径",
                        SketchDimensionType.Diameter => "圆直径",
                        SketchDimensionType.Angle => "角度基准",
                        _ => "几何测量端点"
                    };
                }
            }
            else if (matchedSeg != null)
            {
                usedSegmentIds.Add(matchedSeg.Id);
                item.HasAttachment = true;
                item.TargetEntity = $"{matchedSeg.KindName} #{matchedSeg.Id}";

                if (matchedSeg.Kind == SketchSegmentKind.Arc)
                {
                    bool isDiameter = valInMeters.HasValue && matchedSeg.Radius.HasValue && Math.Abs(matchedSeg.Radius.Value * 2.0 - valInMeters.Value) < Math.Abs(matchedSeg.Radius.Value - valInMeters.Value);
                    item.DimensionTypeEnum = isDiameter ? SketchDimensionType.Diameter : SketchDimensionType.Radius;
                    item.DimensionType = isDiameter ? "直径尺寸" : "半径尺寸";
                    item.Center = matchedSeg.Center != null ? new Point(matchedSeg.Center.WorldX, matchedSeg.Center.WorldY) : null;
                    item.Radius = matchedSeg.Radius;
                }
                else if (matchedSeg.Start != null && matchedSeg.End != null)
                {
                    item.DimensionTypeEnum = SketchDimensionType.Linear;
                    item.P1 = new Point(matchedSeg.Start.WorldX, matchedSeg.Start.WorldY);
                    item.P2 = new Point(matchedSeg.End.WorldX, matchedSeg.End.WorldY);
                    item.OffsetPixels = 24.0 + (dimIdx % 3) * 16.0;
                }
            }
            else
            {
                item.TargetEntity = "草图全局尺寸";
                item.HasAttachment = false;
            }

            Dimensions.Add(item);
            dimIdx++;
        }

        Bounds = bounds;
        StatusInfo = $"图元: {Segments.Count} 条 | 点: {Points.Count} 个 | 约束: {sketch.Relations.Count} 项 | 尺寸: {sketch.Dimensions.Count} 个";

        RequestFitView?.Invoke();
    }

    private static void InferRelationsIfEmpty(SketchFeatureModel sketch)
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

                SketchPointModel? pA = null;
                SketchPointModel? pB = null;

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

        static bool PointsMatch(SketchPointModel? a, SketchPointModel? b)
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
