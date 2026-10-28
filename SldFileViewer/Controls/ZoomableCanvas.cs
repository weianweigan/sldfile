using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SldFileViewer.Models;

namespace SldFileViewer.Controls;

public sealed class ZoomableCanvas : Canvas
{
    private Point _lastMousePosition;
    private bool _isPanning;

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ZoomableCanvas),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PanOffsetProperty =
        DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(ZoomableCanvas),
            new FrameworkPropertyMetadata(new Point(0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WorldBoundsProperty =
        DependencyProperty.Register(nameof(WorldBounds), typeof(SketchBounds), typeof(ZoomableCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public Point PanOffset
    {
        get => (Point)GetValue(PanOffsetProperty);
        set => SetValue(PanOffsetProperty, value);
    }

    public SketchBounds? WorldBounds
    {
        get => (SketchBounds?)GetValue(WorldBoundsProperty);
        set => SetValue(WorldBoundsProperty, value);
    }

    public event EventHandler? ViewportChanged;

    public ZoomableCanvas()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)); // transparent hit-testable
    }

    public Point WorldToScreen(double worldX, double worldY)
    {
        var bounds = WorldBounds;
        double cx = bounds?.CenterX ?? 0;
        double cy = bounds?.CenterY ?? 0;

        double baseScale = CalculateBaseScale();
        double scale = baseScale * Zoom;

        double sx = (worldX - cx) * scale + (ActualWidth / 2.0) + PanOffset.X;
        double sy = (ActualHeight / 2.0) - (worldY - cy) * scale + PanOffset.Y;

        return new Point(sx, sy);
    }

    public Point ScreenToWorld(Point screen)
    {
        var bounds = WorldBounds;
        double cx = bounds?.CenterX ?? 0;
        double cy = bounds?.CenterY ?? 0;

        double baseScale = CalculateBaseScale();
        double scale = baseScale * Zoom;
        if (Math.Abs(scale) < 1e-7) return new Point(0, 0);

        double wx = (screen.X - (ActualWidth / 2.0) - PanOffset.X) / scale + cx;
        double wy = cy - (screen.Y - (ActualHeight / 2.0) - PanOffset.Y) / scale;

        return new Point(wx, wy);
    }

    public double CalculateBaseScale()
    {
        var bounds = WorldBounds;
        if (bounds == null || bounds.Width <= 0 || bounds.Height <= 0) return 2000.0; // default 2000px/meter

        double margin = 60.0;
        double availW = Math.Max(ActualWidth - margin, 100);
        double availH = Math.Max(ActualHeight - margin, 100);

        double scaleX = availW / bounds.Width;
        double scaleY = availH / bounds.Height;

        return Math.Min(scaleX, scaleY);
    }

    public void ResetView()
    {
        Zoom = 1.0;
        PanOffset = new Point(0, 0);
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        double zoomFactor = e.Delta > 0 ? 1.2 : 0.833;
        Point mousePos = e.GetPosition(this);

        // Zoom toward mouse cursor
        double oldZoom = Zoom;
        double newZoom = Math.Clamp(oldZoom * zoomFactor, 0.05, 50.0);

        double ratio = newZoom / oldZoom;
        double dx = mousePos.X - (ActualWidth / 2.0) - PanOffset.X;
        double dy = mousePos.Y - (ActualHeight / 2.0) - PanOffset.Y;

        PanOffset = new Point(
            PanOffset.X - dx * (ratio - 1.0),
            PanOffset.Y - dy * (ratio - 1.0)
        );

        Zoom = newZoom;
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.Modifiers == ModifierKeys.Alt))
        {
            _isPanning = true;
            _lastMousePosition = e.GetPosition(this);
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Right && e.ClickCount == 2)
        {
            ResetView();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            Point currentPos = e.GetPosition(this);
            Vector delta = currentPos - _lastMousePosition;
            PanOffset = new Point(PanOffset.X + delta.X, PanOffset.Y + delta.Y);
            _lastMousePosition = currentPos;
            InvalidateVisual();
            ViewportChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
        {
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Draw background gradient
        var bgBrush = TryFindResource("Brush.Viewport.Gradient") as Brush ?? Brushes.DarkSlateGray;
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // Draw CAD origin crosshair (0,0 in world coordinates)
        Point originScreen = WorldToScreen(0, 0);
        var originPenX = new Pen(new SolidColorBrush(Color.FromRgb(230, 80, 80)), 1.5);
        var originPenY = new Pen(new SolidColorBrush(Color.FromRgb(80, 200, 80)), 1.5);

        // X axis segment
        dc.DrawLine(originPenX, originScreen, new Point(originScreen.X + 30, originScreen.Y));
        // Y axis segment (points UP in CAD)
        dc.DrawLine(originPenY, originScreen, new Point(originScreen.X, originScreen.Y - 30));

        // Origin label
        var originText = new FormattedText("Origin (0,0)", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.Gray, 1.0);
        dc.DrawText(originText, new Point(originScreen.X + 6, originScreen.Y + 4));
    }
}
