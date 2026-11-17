using System.Windows;
using System.Windows.Media;
using SldFile.Drawing;

namespace SldFileViewer.Views.Controls;
public enum DrawingOverlayMode
{
    Off = 0,
    SheetNotesOnly = 1,
    AllAnnotations = 2,
    PureVectorCad = 3
}

/// <summary>Renders the native sheet preview with one coordinate transform.</summary>
public sealed class DrawingVectorCanvas : FrameworkElement
{
    public static readonly DependencyProperty PreviewProperty = DependencyProperty.Register(
        nameof(Preview), typeof(DrawingPreviewDefinition), typeof(DrawingVectorCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public DrawingPreviewDefinition? Preview { get => (DrawingPreviewDefinition?)GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }

    public static readonly DependencyProperty HasRasterImageProperty = DependencyProperty.Register(
        nameof(HasRasterImage), typeof(bool), typeof(DrawingVectorCanvas),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool HasRasterImage { get => (bool)GetValue(HasRasterImageProperty); set => SetValue(HasRasterImageProperty, value); }

    public static readonly DependencyProperty OverlayModeProperty = DependencyProperty.Register(
        nameof(OverlayMode), typeof(DrawingOverlayMode), typeof(DrawingVectorCanvas),
        new FrameworkPropertyMetadata(DrawingOverlayMode.PureVectorCad, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((DrawingVectorCanvas)d).IsOverlayEnabled = (DrawingOverlayMode)e.NewValue != DrawingOverlayMode.Off));
    public DrawingOverlayMode OverlayMode { get => (DrawingOverlayMode)GetValue(OverlayModeProperty); set => SetValue(OverlayModeProperty, value); }

    public static readonly DependencyProperty IsOverlayEnabledProperty = DependencyProperty.Register(
        nameof(IsOverlayEnabled), typeof(bool), typeof(DrawingVectorCanvas),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) =>
        {
            var canvas = (DrawingVectorCanvas)d;
            if (!(bool)e.NewValue) canvas.OverlayMode = DrawingOverlayMode.Off;
            else if (canvas.OverlayMode == DrawingOverlayMode.Off) canvas.OverlayMode = DrawingOverlayMode.PureVectorCad;
        }));
    public bool IsOverlayEnabled { get => (bool)GetValue(IsOverlayEnabledProperty); set => SetValue(IsOverlayEnabledProperty, value); }

    public DrawingVectorCanvas()
    {
        IsHitTestVisible = false;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!IsOverlayEnabled || OverlayMode == DrawingOverlayMode.Off || Preview?.Primitives.Count is not > 0) return;
        NativeDrawingPreviewRenderer.Draw(dc, Preview, OverlayMode, ActualWidth, ActualHeight,
            VisualTreeHelper.GetDpi(this).PixelsPerDip, HasRasterImage && OverlayMode != DrawingOverlayMode.PureVectorCad);
    }
}
