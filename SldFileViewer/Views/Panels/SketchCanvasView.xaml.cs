using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SldFile;
using SldFile.Sketches;
using SldFileViewer.Models;
using SldFileViewer.ViewModels;

namespace SldFileViewer.Views.Panels;

public partial class SketchCanvasView : UserControl
{
    public SketchCanvasView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private SketchCanvasViewModel? VM => DataContext as SketchCanvasViewModel;

    private bool _renderPending;

    private void RequestRender()
    {
        if (_renderPending) return;
        _renderPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _renderPending = false;
            RenderSketch();
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged += OnViewModelPropertyChanged;
            VM.RequestFitView += OnRequestFitView;
        }

        SketchCanvas.ViewportChanged += (s, ev) => RequestRender();
        SketchCanvas.SizeChanged += (s, ev) => RequestRender();

        RequestRender();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.PropertyChanged -= OnViewModelPropertyChanged;
            VM.RequestFitView -= OnRequestFitView;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SketchCanvasViewModel.Sketch) ||
            e.PropertyName == nameof(SketchCanvasViewModel.ShowPoints) ||
            e.PropertyName == nameof(SketchCanvasViewModel.ShowConstraints) ||
            e.PropertyName == nameof(SketchCanvasViewModel.ShowDimensions) ||
            e.PropertyName == nameof(SketchCanvasViewModel.ShowConstruction) ||
            e.PropertyName == nameof(SketchCanvasViewModel.SelectedSegment) ||
            e.PropertyName == nameof(SketchCanvasViewModel.SelectedPoint))
        {
            RequestRender();
        }
    }

    private void OnRequestFitView()
    {
        SketchCanvas.ResetView();
        RequestRender();
    }

    private void RenderSketch()
    {
        try
        {
            RenderSketchCore();
        }
        catch { }
    }

    private void RenderSketchCore()
    {
        SketchCanvas.Children.Clear();
        var vm = VM;
        if (vm == null || vm.Sketch == null) return;

        // Pens and Brushes
        var normalPen = new Pen(TryFindResource("Brush.Sketch.NormalSegment") as Brush ?? Brushes.Cyan, 2.0);
        var selectedPen = new Pen(TryFindResource("Brush.Sketch.Selected") as Brush ?? Brushes.DeepSkyBlue, 3.5);
        var constructionPen = new Pen(TryFindResource("Brush.Sketch.ConstructionSegment") as Brush ?? Brushes.Orange, 1.5)
        {
            DashStyle = DashStyles.Dash
        };

        var pointBrush = TryFindResource("Brush.Sketch.Point") as Brush ?? Brushes.Gold;
        var selectedPointBrush = TryFindResource("Brush.Sketch.Selected") as Brush ?? Brushes.DeepSkyBlue;
        var constraintBg = TryFindResource("Brush.Sketch.ConstraintBadge") as Brush ?? Brushes.Green;
        var dimBrush = TryFindResource("Brush.Sketch.Dimension") as Brush ?? Brushes.LightSkyBlue;

        // 1. Draw Segments
        foreach (var seg in vm.Segments)
        {
            if (seg.IsConstruction && !vm.ShowConstruction) continue;

            Pen pen = seg.IsSelected ? selectedPen : (seg.IsConstruction ? constructionPen : normalPen);

            if (seg.Kind == SketchSegmentKind.Line && seg.Start != null && seg.End != null)
            {
                Point p1 = SketchCanvas.WorldToScreen(seg.Start.WorldX, seg.Start.WorldY);
                Point p2 = SketchCanvas.WorldToScreen(seg.End.WorldX, seg.End.WorldY);

                var line = new Line
                {
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = pen.Brush,
                    StrokeThickness = pen.Thickness,
                    StrokeDashArray = seg.IsConstruction ? new DoubleCollection { 4, 2 } : null,
                    ToolTip = $"直线 #{seg.Id} (起点: #{seg.Start.Id}, 终点: #{seg.End.Id})"
                };

                line.MouseEnter += (s, e) => line.StrokeThickness = pen.Thickness + 1.5;
                line.MouseLeave += (s, e) => line.StrokeThickness = pen.Thickness;
                line.MouseDown += (s, e) => { vm.SelectedSegment = seg; e.Handled = true; };

                SketchCanvas.Children.Add(line);
            }
            else if (seg.Kind == SketchSegmentKind.Arc || seg.Kind == SketchSegmentKind.Line)
            {
                if (seg.Center != null && seg.Radius.HasValue)
                {
                    Point centerScreen = SketchCanvas.WorldToScreen(seg.Center.WorldX, seg.Center.WorldY);
                    double scale = SketchCanvas.CalculateBaseScale() * SketchCanvas.Zoom;
                    double rScreen = seg.Radius.Value * scale;
                    if (rScreen < 0.1 || double.IsNaN(rScreen) || double.IsInfinity(rScreen)) continue;

                    if (seg.Start != null && seg.End != null)
                    {
                        Point pStart = SketchCanvas.WorldToScreen(seg.Start.WorldX, seg.Start.WorldY);
                        Point pEnd = SketchCanvas.WorldToScreen(seg.End.WorldX, seg.End.WorldY);

                        var pathGeo = new PathGeometry();
                        var fig = new PathFigure { StartPoint = pStart, IsClosed = false };
                        fig.Segments.Add(new ArcSegment
                        {
                            Point = pEnd,
                            Size = new Size(rScreen, rScreen),
                            SweepDirection = SweepDirection.Clockwise,
                            IsLargeArc = false
                        });
                        pathGeo.Figures.Add(fig);

                        var arcPath = new Path
                        {
                            Data = pathGeo,
                            Stroke = pen.Brush,
                            StrokeThickness = pen.Thickness,
                            StrokeDashArray = seg.IsConstruction ? new DoubleCollection { 4, 2 } : null,
                            ToolTip = $"圆弧 #{seg.Id} (半径: {seg.Radius.Value * 1000:0.##} mm)"
                        };
                        arcPath.MouseDown += (s, e) => { vm.SelectedSegment = seg; e.Handled = true; };
                        SketchCanvas.Children.Add(arcPath);
                    }
                    else
                    {
                        // Full Circle
                        var ellipse = new Ellipse
                        {
                            Width = rScreen * 2,
                            Height = rScreen * 2,
                            Stroke = pen.Brush,
                            StrokeThickness = pen.Thickness,
                            StrokeDashArray = seg.IsConstruction ? new DoubleCollection { 4, 2 } : null,
                            ToolTip = $"圆 #{seg.Id} (半径: {seg.Radius.Value * 1000:0.##} mm)"
                        };
                        Canvas.SetLeft(ellipse, centerScreen.X - rScreen);
                        Canvas.SetTop(ellipse, centerScreen.Y - rScreen);
                        ellipse.MouseDown += (s, e) => { vm.SelectedSegment = seg; e.Handled = true; };
                        SketchCanvas.Children.Add(ellipse);
                    }
                }
            }
        }

        // 2. Draw Points
        if (vm.ShowPoints)
        {
            foreach (var pt in vm.Points)
            {
                Point screenPt = SketchCanvas.WorldToScreen(pt.WorldX, pt.WorldY);
                double size = pt.IsSelected ? 8.0 : 5.0;

                var pointShape = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = pt.IsSelected ? selectedPointBrush : pointBrush,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    ToolTip = $"点 #{pt.Id} (X: {pt.WorldX * 1000:0.##} mm, Y: {pt.WorldY * 1000:0.##} mm)"
                };

                Canvas.SetLeft(pointShape, screenPt.X - size / 2.0);
                Canvas.SetTop(pointShape, screenPt.Y - size / 2.0);

                pointShape.MouseDown += (s, e) => { vm.SelectedPoint = pt; e.Handled = true; };
                SketchCanvas.Children.Add(pointShape);
            }
        }

        // 3. Draw Constraint Badges
        if (vm.ShowConstraints)
        {
            int relationIndex = 0;
            var placedPositions = new List<Point>();

            foreach (var rel in vm.Sketch.Relations)
            {
                Point badgePos;
                var segments = rel.GetSegments();
                var points = rel.GetPoints();

                if (segments.Count > 0 && segments[0].StartPoint != null && segments[0].EndPoint != null)
                {
                    var seg = segments[0];
                    Point p1 = SketchCanvas.WorldToScreen(seg.StartPoint!.X, seg.StartPoint.Y);
                    Point p2 = SketchCanvas.WorldToScreen(seg.EndPoint!.X, seg.EndPoint.Y);
                    Point mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);

                    double dx = p2.X - p1.X;
                    double dy = p2.Y - p1.Y;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 1e-4)
                    {
                        double nx = -dy / len;
                        double ny = dx / len;
                        badgePos = new Point(mid.X + nx * 14 - 8, mid.Y + ny * 14 - 8);
                    }
                    else
                    {
                        badgePos = new Point(mid.X - 8, mid.Y - 16);
                    }
                }
                else if (points.Count > 0)
                {
                    var pt = points[0];
                    Point p = SketchCanvas.WorldToScreen(pt.X, pt.Y);
                    badgePos = new Point(p.X + 8, p.Y - 16);
                }
                else
                {
                    // Fallback to segment by index or canvas margin
                    var seg = vm.Segments.ElementAtOrDefault(relationIndex % Math.Max(vm.Segments.Count, 1));
                    if (seg?.Start != null && seg.End != null)
                    {
                        Point p1 = SketchCanvas.WorldToScreen(seg.Start.WorldX, seg.Start.WorldY);
                        Point p2 = SketchCanvas.WorldToScreen(seg.End.WorldX, seg.End.WorldY);
                        badgePos = new Point((p1.X + p2.X) / 2.0 - 8, (p1.Y + p2.Y) / 2.0 - 16);
                    }
                    else
                    {
                        badgePos = new Point(40 + (relationIndex * 24) % 300, 40 + (relationIndex / 12) * 24);
                    }
                }

                // Avoid badge collisions by shifting overlapping badges
                bool collision;
                int shiftAttempts = 0;
                do
                {
                    collision = false;
                    foreach (var placed in placedPositions)
                    {
                        if (Math.Abs(placed.X - badgePos.X) < 18 && Math.Abs(placed.Y - badgePos.Y) < 18)
                        {
                            collision = true;
                            badgePos = new Point(badgePos.X + 18, badgePos.Y);
                            break;
                        }
                    }
                    shiftAttempts++;
                } while (collision && shiftAttempts < 10);

                placedPositions.Add(badgePos);

                var badge = CreateConstraintBadge(rel.Kind, constraintBg);
                string entityNames = string.Join(", ", rel.GetEntities().Select(e => e is SketchSegment s ? $"线段 #{s.Id}" : e is SketchPoint p ? $"点 #{p.Id}" : e.ToString()));
                badge.ToolTip = string.IsNullOrEmpty(entityNames)
                    ? $"约束: {rel.Kind} (代码: {rel.TypeCode})"
                    : $"约束: {rel.Kind} [{entityNames}]";

                Canvas.SetLeft(badge, badgePos.X);
                Canvas.SetTop(badge, badgePos.Y);
                SketchCanvas.Children.Add(badge);
                relationIndex++;
            }
        }

        // 4. Draw Dimensions
        if (vm.ShowDimensions)
        {
            int dimIndex = 0;
            foreach (var dim in vm.Dimensions)
            {
                var seg = vm.Segments.ElementAtOrDefault(dimIndex % Math.Max(vm.Segments.Count, 1));
                Point dimPos;
                if (seg?.Start != null && seg.End != null)
                {
                    Point p1 = SketchCanvas.WorldToScreen(seg.Start.WorldX, seg.Start.WorldY);
                    Point p2 = SketchCanvas.WorldToScreen(seg.End.WorldX, seg.End.WorldY);
                    dimPos = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0 + 12);
                }
                else
                {
                    dimPos = new Point(50, 80 + dimIndex * 20);
                }

                var textBlock = new TextBlock
                {
                    Text = dim.DisplayText,
                    Foreground = dimBrush,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Background = new SolidColorBrush(Color.FromArgb(160, 20, 22, 26)),
                    Padding = new Thickness(3, 1, 3, 1)
                };

                Canvas.SetLeft(textBlock, dimPos.X);
                Canvas.SetTop(textBlock, dimPos.Y);
                SketchCanvas.Children.Add(textBlock);
                dimIndex++;
            }
        }
    }

    private FrameworkElement CreateConstraintBadge(SketchRelationKind kind, Brush bg)
    {
        string symbol = kind switch
        {
            SketchRelationKind.Horizontal => "—",
            SketchRelationKind.Vertical => "|",
            SketchRelationKind.Coincident => "⧈",
            SketchRelationKind.Concentric => "◎",
            SketchRelationKind.Tangent => "○―",
            SketchRelationKind.Parallel => "//",
            SketchRelationKind.Perpendicular => "∟",
            SketchRelationKind.EqualLength => "=",
            SketchRelationKind.Fixed => "⚓",
            _ => "C"
        };

        var border = new Border
        {
            Background = bg,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(3, 1, 3, 1),
            Cursor = Cursors.Hand
        };

        var text = new TextBlock
        {
            Text = symbol,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = text;
        return border;
    }
}
