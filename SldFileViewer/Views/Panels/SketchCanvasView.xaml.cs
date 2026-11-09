using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SldFile;
using SldFile.Mfc;
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
            else if (seg.Kind == SketchSegmentKind.Arc)
            {
                if (seg.Center != null && seg.Radius.HasValue)
                {
                    Point centerScreen = SketchCanvas.WorldToScreen(seg.Center.WorldX, seg.Center.WorldY);
                    double scale = SketchCanvas.CalculateBaseScale() * SketchCanvas.Zoom;
                    double rScreen = seg.Radius.Value * scale;
                    if (rScreen < 0.1 || double.IsNaN(rScreen) || double.IsInfinity(rScreen)) continue;

                    // 判断是否为闭合整圆 (Full Circle)
                    bool isClosedCircle = seg.IsCircle ||
                                          (seg.Start == null || seg.End == null) ||
                                          (seg.Start != null && seg.End != null && Math.Sqrt(Math.Pow(seg.Start.WorldX - seg.End.WorldX, 2) + Math.Pow(seg.Start.WorldY - seg.End.WorldY, 2)) < 1e-5);

                    if (isClosedCircle)
                    {
                        // 绘制完整圆 (Full Circle)
                        var ellipseGeo = new EllipseGeometry(centerScreen, rScreen, rScreen);
                        var circlePath = new Path
                        {
                            Data = ellipseGeo,
                            Stroke = pen.Brush,
                            StrokeThickness = pen.Thickness,
                            StrokeDashArray = seg.IsConstruction ? new DoubleCollection { 4, 2 } : null,
                            ToolTip = $"整圆 #{seg.Id} (半径: {seg.Radius.Value * 1000:0.##} mm, 直径: {seg.Radius.Value * 2000:0.##} mm)"
                        };
                        circlePath.MouseEnter += (s, e) => circlePath.StrokeThickness = pen.Thickness + 1.5;
                        circlePath.MouseLeave += (s, e) => circlePath.StrokeThickness = pen.Thickness;
                        circlePath.MouseDown += (s, e) => { vm.SelectedSegment = seg; e.Handled = true; };
                        SketchCanvas.Children.Add(circlePath);

                        // 绘制圆心十字中心标记 (Center Mark)
                        if (vm.ShowPoints && rScreen >= 4.0)
                        {
                            double cmSize = Math.Min(6.0, Math.Max(3.0, rScreen * 0.15));
                            var crossGeo = new GeometryGroup();
                            crossGeo.Children.Add(new LineGeometry(new Point(centerScreen.X - cmSize, centerScreen.Y), new Point(centerScreen.X + cmSize, centerScreen.Y)));
                            crossGeo.Children.Add(new LineGeometry(new Point(centerScreen.X, centerScreen.Y - cmSize), new Point(centerScreen.X, centerScreen.Y + cmSize)));
                            var crossPath = new Path
                            {
                                Data = crossGeo,
                                Stroke = pen.Brush,
                                StrokeThickness = 1.0,
                                IsHitTestVisible = false
                            };
                            SketchCanvas.Children.Add(crossPath);
                        }
                    }
                    else if (seg.Start != null && seg.End != null)
                    {
                        // 局部圆弧 (Arc)
                        Point pStart = SketchCanvas.WorldToScreen(seg.Start.WorldX, seg.Start.WorldY);
                        Point pEnd = SketchCanvas.WorldToScreen(seg.End.WorldX, seg.End.WorldY);

                        // 精确计算圆弧扫描角度与方向
                        double aStart = Math.Atan2(seg.Start.WorldY - seg.Center.WorldY, seg.Start.WorldX - seg.Center.WorldX);
                        double aEnd = Math.Atan2(seg.End.WorldY - seg.Center.WorldY, seg.End.WorldX - seg.Center.WorldX);

                        // SW 默认 Direction: +1 为逆时针 (CCW), -1 为顺时针 (CW)
                        int dir = seg.Direction != 0 ? seg.Direction : 1;
                        double sweepAngle;
                        if (dir >= 0)
                        {
                            sweepAngle = aEnd - aStart;
                            while (sweepAngle < 0) sweepAngle += 2 * Math.PI;
                            while (sweepAngle >= 2 * Math.PI) sweepAngle -= 2 * Math.PI;
                        }
                        else
                        {
                            sweepAngle = aStart - aEnd;
                            while (sweepAngle < 0) sweepAngle += 2 * Math.PI;
                            while (sweepAngle >= 2 * Math.PI) sweepAngle -= 2 * Math.PI;
                        }

                        bool isLargeArc = sweepAngle > Math.PI;
                        // 因为屏幕坐标系 Y 轴向下，世界坐标系逆时针 (CCW, dir >= 0) 在屏幕上对应顺时针 (Clockwise)
                        SweepDirection sweepDir = (dir >= 0) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

                        var pathGeo = new PathGeometry();
                        var fig = new PathFigure { StartPoint = pStart, IsClosed = false };
                        fig.Segments.Add(new ArcSegment
                        {
                            Point = pEnd,
                            Size = new Size(rScreen, rScreen),
                            SweepDirection = sweepDir,
                            IsLargeArc = isLargeArc
                        });
                        pathGeo.Figures.Add(fig);

                        var arcPath = new Path
                        {
                            Data = pathGeo,
                            Stroke = pen.Brush,
                            StrokeThickness = pen.Thickness,
                            StrokeDashArray = seg.IsConstruction ? new DoubleCollection { 4, 2 } : null,
                            ToolTip = $"圆弧 #{seg.Id} (半径: {seg.Radius.Value * 1000:0.##} mm, 夹角: {sweepAngle * 180.0 / Math.PI:0.#}°)"
                        };
                        arcPath.MouseEnter += (s, e) => arcPath.StrokeThickness = pen.Thickness + 1.5;
                        arcPath.MouseLeave += (s, e) => arcPath.StrokeThickness = pen.Thickness;
                        arcPath.MouseDown += (s, e) => { vm.SelectedSegment = seg; e.Handled = true; };
                        SketchCanvas.Children.Add(arcPath);
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
                string entityNames = string.Join(", ", rel.GetEntities().Select(e => e is SketchSegmentModel s ? $"线段 #{s.Id}" : e is SketchPointModel p ? $"点 #{p.Id}" : e.ToString()));
                badge.ToolTip = string.IsNullOrEmpty(entityNames)
                    ? $"约束: {rel.Kind} (代码: {rel.TypeCode})"
                    : $"约束: {rel.Kind} [{entityNames}]";

                Canvas.SetLeft(badge, badgePos.X);
                Canvas.SetTop(badge, badgePos.Y);
                SketchCanvas.Children.Add(badge);
                relationIndex++;
            }
        }

        // 4. Draw Dimensions (Engineering CAD Style with Extension Lines & Arrowheads)
        if (vm.ShowDimensions)
        {
            int unattachedIndex = 0;
            foreach (var dim in vm.Dimensions)
            {
                if (dim.HasAttachment)
                {
                    if ((dim.DimensionTypeEnum == SketchDimensionType.Radius || dim.DimensionTypeEnum == SketchDimensionType.Diameter) && dim.Center != null && dim.Radius.HasValue)
                    {
                        Point centerScreen = SketchCanvas.WorldToScreen(dim.Center.Value.X, dim.Center.Value.Y);
                        double scale = SketchCanvas.CalculateBaseScale() * SketchCanvas.Zoom;
                        double rScreen = dim.Radius.Value * scale;
                        if (rScreen > 1.0 && !double.IsNaN(rScreen) && !double.IsInfinity(rScreen))
                        {
                            bool isDia = dim.DimensionTypeEnum == SketchDimensionType.Diameter;
                            DrawRadialDimension(SketchCanvas, centerScreen, rScreen, Math.PI / 4.0, dim.DisplayText, dimBrush, isDia);
                        }
                    }
                    else if (dim.DimensionTypeEnum == SketchDimensionType.Linear)
                    {
                        Point p1Screen = SketchCanvas.WorldToScreen(dim.P1.X, dim.P1.Y);
                        Point p2Screen = SketchCanvas.WorldToScreen(dim.P2.X, dim.P2.Y);
                        DrawLinearDimension(SketchCanvas, p1Screen, p2Screen, dim.DisplayText, dim.OffsetPixels, dimBrush);
                    }
                }
                else
                {
                    // Unattached / Global dimension callout badge
                    Point dimPos = new Point(30, 70 + unattachedIndex * 26);
                    var textBlock = new TextBlock
                    {
                        Text = dim.DisplayText,
                        Foreground = dimBrush,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold
                    };
                    var border = new Border
                    {
                        Child = textBlock,
                        Background = new SolidColorBrush(Color.FromArgb(200, 24, 28, 36)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(80, 80, 160, 240)),
                        BorderThickness = new Thickness(0.8),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(6, 2, 6, 2)
                    };
                    Canvas.SetLeft(border, dimPos.X);
                    Canvas.SetTop(border, dimPos.Y);
                    SketchCanvas.Children.Add(border);
                    unattachedIndex++;
                }
            }
        }
    }

    private void DrawLinearDimension(Canvas canvas, Point p1, Point p2, string text, double offsetPixels, Brush brush)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1.0) return;

        double ux = dx / len;
        double uy = dy / len;
        double nx = -uy;
        double ny = ux;

        Point d1 = new Point(p1.X + nx * offsetPixels, p1.Y + ny * offsetPixels);
        Point d2 = new Point(p2.X + nx * offsetPixels, p2.Y + ny * offsetPixels);

        double sign = Math.Sign(offsetPixels);
        if (sign == 0) sign = 1;

        // 1. Extension lines (引线 / 界线)
        Point ext1Start = new Point(p1.X + nx * sign * 2.0, p1.Y + ny * sign * 2.0);
        Point ext1End = new Point(d1.X + nx * sign * 4.0, d1.Y + ny * sign * 4.0);
        Point ext2Start = new Point(p2.X + nx * sign * 2.0, p2.Y + ny * sign * 2.0);
        Point ext2End = new Point(d2.X + nx * sign * 4.0, d2.Y + ny * sign * 4.0);

        canvas.Children.Add(new Line { X1 = ext1Start.X, Y1 = ext1Start.Y, X2 = ext1End.X, Y2 = ext1End.Y, Stroke = brush, StrokeThickness = 1.0 });
        canvas.Children.Add(new Line { X1 = ext2Start.X, Y1 = ext2Start.Y, X2 = ext2End.X, Y2 = ext2End.Y, Stroke = brush, StrokeThickness = 1.0 });

        // 2. Measure text
        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var border = new Border
        {
            Child = textBlock,
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 28, 36)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 80, 160, 240)),
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1)
        };
        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size textSize = border.DesiredSize;

        Point mid = new Point((d1.X + d2.X) / 2.0, (d1.Y + d2.Y) / 2.0);

        // 3. Dimension line
        double halfText = textSize.Width / 2.0 + 3.0;
        if (len > halfText * 2.0 + 20.0)
        {
            Point line1End = new Point(mid.X - ux * halfText, mid.Y - uy * halfText);
            Point line2Start = new Point(mid.X + ux * halfText, mid.Y + uy * halfText);

            canvas.Children.Add(new Line { X1 = d1.X, Y1 = d1.Y, X2 = line1End.X, Y2 = line1End.Y, Stroke = brush, StrokeThickness = 1.0 });
            canvas.Children.Add(new Line { X1 = line2Start.X, Y1 = line2Start.Y, X2 = d2.X, Y2 = d2.Y, Stroke = brush, StrokeThickness = 1.0 });

            Canvas.SetLeft(border, mid.X - textSize.Width / 2.0);
            Canvas.SetTop(border, mid.Y - textSize.Height / 2.0);
        }
        else
        {
            canvas.Children.Add(new Line { X1 = d1.X, Y1 = d1.Y, X2 = d2.X, Y2 = d2.Y, Stroke = brush, StrokeThickness = 1.0 });

            Canvas.SetLeft(border, mid.X - textSize.Width / 2.0);
            Canvas.SetTop(border, mid.Y + ny * sign * 14.0 - textSize.Height / 2.0);
        }

        // 4. Arrowheads (尺寸箭头)
        double arrowLen = 8.0;
        double arrowWidth = 3.0;
        bool insideArrows = len > arrowLen * 3.0;

        DrawArrowhead(canvas, d1, insideArrows ? new Vector(ux, uy) : new Vector(-ux, -uy), arrowLen, arrowWidth, brush);
        DrawArrowhead(canvas, d2, insideArrows ? new Vector(-ux, -uy) : new Vector(ux, uy), arrowLen, arrowWidth, brush);

        canvas.Children.Add(border);
    }

    private void DrawRadialDimension(Canvas canvas, Point center, double radius, double angleRad, string text, Brush brush, bool isDiameter = false)
    {
        double cos = Math.Cos(angleRad);
        double sin = Math.Sin(angleRad);

        Point arcPt = new Point(center.X + radius * cos, center.Y + radius * sin);
        Point elbowPt = new Point(center.X + (radius + 24.0) * cos, center.Y + (radius + 24.0) * sin);
        double shoulderLen = cos >= 0 ? 30.0 : -30.0;
        Point shoulderPt = new Point(elbowPt.X + shoulderLen, elbowPt.Y);

        if (isDiameter)
        {
            // 对侧穿心线与双向箭头
            Point oppArcPt = new Point(center.X - radius * cos, center.Y - radius * sin);
            canvas.Children.Add(new Line { X1 = oppArcPt.X, Y1 = oppArcPt.Y, X2 = arcPt.X, Y2 = arcPt.Y, Stroke = brush, StrokeThickness = 0.8 });
            DrawArrowhead(canvas, oppArcPt, new Vector(-cos, -sin), 8.0, 3.0, brush);
        }

        canvas.Children.Add(new Line { X1 = arcPt.X, Y1 = arcPt.Y, X2 = elbowPt.X, Y2 = elbowPt.Y, Stroke = brush, StrokeThickness = 1.0 });
        canvas.Children.Add(new Line { X1 = elbowPt.X, Y1 = elbowPt.Y, X2 = shoulderPt.X, Y2 = shoulderPt.Y, Stroke = brush, StrokeThickness = 1.0 });

        Vector dir = new Vector(cos, sin);
        DrawArrowhead(canvas, arcPt, dir, 8.0, 3.0, brush);

        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = brush,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        var border = new Border
        {
            Child = textBlock,
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 28, 36)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 80, 160, 240)),
            BorderThickness = new Thickness(0.8),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1)
        };
        border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double tx = cos >= 0 ? elbowPt.X + 2 : shoulderPt.X + 2;
        Canvas.SetLeft(border, tx);
        Canvas.SetTop(border, elbowPt.Y - border.DesiredSize.Height - 2);

        canvas.Children.Add(border);
    }

    private static void DrawArrowhead(Canvas canvas, Point tip, Vector dir, double length, double halfWidth, Brush brush)
    {
        Point baseCenter = new Point(tip.X + dir.X * length, tip.Y + dir.Y * length);
        Vector normal = new Vector(-dir.Y, dir.X);

        Point wing1 = new Point(baseCenter.X + normal.X * halfWidth, baseCenter.Y + normal.Y * halfWidth);
        Point wing2 = new Point(baseCenter.X - normal.X * halfWidth, baseCenter.Y - normal.Y * halfWidth);

        var polygon = new Polygon
        {
            Points = new PointCollection { tip, wing1, wing2 },
            Fill = brush,
            Stroke = brush,
            StrokeThickness = 0.5
        };
        canvas.Children.Add(polygon);
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
