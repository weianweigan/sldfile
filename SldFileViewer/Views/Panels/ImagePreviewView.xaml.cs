using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SldFileViewer.ViewModels;

namespace SldFileViewer.Views.Panels;

public partial class ImagePreviewView : UserControl
{
    private Point _lastMousePos;
    private bool _isPanning;
    private ImagePreviewViewModel? _subscribedVm;

    public ImagePreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.RequestFitToWindow -= OnFitToWindow;
            _subscribedVm.RequestResetTo100 -= OnResetTo100;
            _subscribedVm.RequestZoomDelta -= OnZoomDelta;
            _subscribedVm = null;
        }

        if (e.NewValue is ImagePreviewViewModel vm)
        {
            _subscribedVm = vm;
            vm.RequestFitToWindow += OnFitToWindow;
            vm.RequestResetTo100 += OnResetTo100;
            vm.RequestZoomDelta += OnZoomDelta;
            Dispatcher.BeginInvoke(new Action(OnFitToWindow), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        OnFitToWindow();
    }

    private void OnFitToWindow()
    {
        if (DataContext is not ImagePreviewViewModel vm || !vm.HasImage) return;
        double imgW = vm.PixelWidth;
        double imgH = vm.PixelHeight;
        if (imgW <= 0 || imgH <= 0) return;

        double availW = Math.Max(ViewportContainer.ActualWidth - 48, 100);
        double availH = Math.Max(ViewportContainer.ActualHeight - 48, 100);

        double fitScale = Math.Min(availW / imgW, availH / imgH);
        if (fitScale <= 0) fitScale = 1.0;

        ImgScale.ScaleX = fitScale;
        ImgScale.ScaleY = fitScale;
        ImgTranslate.X = 0;
        ImgTranslate.Y = 0;

        vm.UpdateZoom(fitScale);
    }

    private void OnResetTo100()
    {
        ImgScale.ScaleX = 1.0;
        ImgScale.ScaleY = 1.0;
        ImgTranslate.X = 0;
        ImgTranslate.Y = 0;

        if (DataContext is ImagePreviewViewModel vm)
        {
            vm.UpdateZoom(1.0);
        }
    }

    private void OnZoomDelta(double factor)
    {
        Point center = new Point(ViewportContainer.ActualWidth / 2.0, ViewportContainer.ActualHeight / 2.0);
        ZoomByFactor(factor, center);
    }

    private void ZoomByFactor(double factor, Point anchor)
    {
        double oldScale = ImgScale.ScaleX;
        double newScale = Math.Clamp(oldScale * factor, 0.02, 30.0);
        double ratio = newScale / oldScale;

        double cx = ViewportContainer.ActualWidth / 2.0;
        double cy = ViewportContainer.ActualHeight / 2.0;

        double dx = anchor.X - (cx + ImgTranslate.X);
        double dy = anchor.Y - (cy + ImgTranslate.Y);

        ImgTranslate.X -= dx * (ratio - 1.0);
        ImgTranslate.Y -= dy * (ratio - 1.0);

        ImgScale.ScaleX = newScale;
        ImgScale.ScaleY = newScale;

        if (DataContext is ImagePreviewViewModel vm)
        {
            vm.UpdateZoom(newScale);
        }
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not ImagePreviewViewModel vm || !vm.HasImage) return;

        double factor = e.Delta > 0 ? 1.2 : 0.833;
        Point mousePos = e.GetPosition(ViewportContainer);
        ZoomByFactor(factor, mousePos);
        e.Handled = true;
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ImagePreviewViewModel vm || !vm.HasImage) return;

        if (e.ClickCount == 2)
        {
            OnFitToWindow();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left || e.ChangedButton == MouseButton.Right)
        {
            _isPanning = true;
            _lastMousePos = e.GetPosition(ViewportContainer);
            ViewportContainer.CaptureMouse();
            ViewportContainer.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            Point currentPos = e.GetPosition(ViewportContainer);
            Vector delta = currentPos - _lastMousePos;
            ImgTranslate.X += delta.X;
            ImgTranslate.Y += delta.Y;
            _lastMousePos = currentPos;
            e.Handled = true;
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ViewportContainer.ReleaseMouseCapture();
            ViewportContainer.Cursor = Cursors.Arrow;
            e.Handled = true;
        }
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.PreviousSize.Width <= 0 || e.PreviousSize.Height <= 0)
        {
            OnFitToWindow();
        }
    }
}
