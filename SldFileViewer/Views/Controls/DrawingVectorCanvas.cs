using System;
using System.Windows;
using System.Windows.Media;
using SldFile.Drawing.Vector;

namespace SldFileViewer.Views.Controls;

/// <summary>
/// Rendering mode for CAD vector typography overlay on drawing preview.
/// </summary>
public enum DrawingOverlayMode
{
    /// <summary>
    /// Vector overlay is completely disabled (original raster preview).
    /// </summary>
    Off = 0,

    /// <summary>
    /// Recommended default: lossless vector rendering for sheet format, title block,
    /// tolerance tables and technical notes only. Filters out view dimensions and leader callouts
    /// to completely eliminate visual ghosting over 2D model geometry.
    /// </summary>
    SheetNotesOnly = 1,

    /// <summary>
    /// Overlays all extracted CAD text elements, including drawing view dimensions and callouts.
    /// </summary>
    AllAnnotations = 2
}

/// <summary>
/// High-performance WPF vector rendering canvas that overlays lossless CAD vector typography
/// and graphics on top of drawing preview viewports with anti-ghosting background erasure.
/// </summary>
public sealed class DrawingVectorCanvas : FrameworkElement
{
    private static readonly Brush TitleBlockBgBrush;
    private static readonly Brush PaperBgBrush;

    static DrawingVectorCanvas()
    {
        var tbBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        tbBrush.Freeze();
        TitleBlockBgBrush = tbBrush;

        var pBrush = new SolidColorBrush(Color.FromRgb(255, 251, 240));
        pBrush.Freeze();
        PaperBgBrush = pBrush;
    }

    public static readonly DependencyProperty VectorDataProperty =
        DependencyProperty.Register(
            nameof(VectorData),
            typeof(DrawingSheetVectorData),
            typeof(DrawingVectorCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayModeProperty =
        DependencyProperty.Register(
            nameof(OverlayMode),
            typeof(DrawingOverlayMode),
            typeof(DrawingVectorCanvas),
            new FrameworkPropertyMetadata(
                DrawingOverlayMode.SheetNotesOnly,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnOverlayModeChanged));

    public static readonly DependencyProperty IsOverlayEnabledProperty =
        DependencyProperty.Register(
            nameof(IsOverlayEnabled),
            typeof(bool),
            typeof(DrawingVectorCanvas),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnIsOverlayEnabledChanged));

    public static readonly DependencyProperty EnableBackgroundErasingProperty =
        DependencyProperty.Register(
            nameof(EnableBackgroundErasing),
            typeof(bool),
            typeof(DrawingVectorCanvas),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextBrushProperty =
        DependencyProperty.Register(
            nameof(TextBrush),
            typeof(Brush),
            typeof(DrawingVectorCanvas),
            new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public DrawingSheetVectorData? VectorData
    {
        get => (DrawingSheetVectorData?)GetValue(VectorDataProperty);
        set => SetValue(VectorDataProperty, value);
    }

    public DrawingOverlayMode OverlayMode
    {
        get => (DrawingOverlayMode)GetValue(OverlayModeProperty);
        set => SetValue(OverlayModeProperty, value);
    }

    public bool IsOverlayEnabled
    {
        get => (bool)GetValue(IsOverlayEnabledProperty);
        set => SetValue(IsOverlayEnabledProperty, value);
    }

    public bool EnableBackgroundErasing
    {
        get => (bool)GetValue(EnableBackgroundErasingProperty);
        set => SetValue(EnableBackgroundErasingProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    private static void OnOverlayModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DrawingVectorCanvas canvas)
        {
            var newMode = (DrawingOverlayMode)e.NewValue;
            bool shouldBeEnabled = (newMode != DrawingOverlayMode.Off);
            if (canvas.IsOverlayEnabled != shouldBeEnabled)
            {
                canvas.IsOverlayEnabled = shouldBeEnabled;
            }
        }
    }

    private static void OnIsOverlayEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DrawingVectorCanvas canvas)
        {
            bool isEnabled = (bool)e.NewValue;
            if (!isEnabled && canvas.OverlayMode != DrawingOverlayMode.Off)
            {
                canvas.OverlayMode = DrawingOverlayMode.Off;
            }
            else if (isEnabled && canvas.OverlayMode == DrawingOverlayMode.Off)
            {
                canvas.OverlayMode = DrawingOverlayMode.SheetNotesOnly;
            }
        }
    }

    public DrawingVectorCanvas()
    {
        IsHitTestVisible = false;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (!IsOverlayEnabled || OverlayMode == DrawingOverlayMode.Off || VectorData == null || !VectorData.HasVectorData)
            return;

        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var defaultTypeface = new Typeface(new FontFamily("SimSun, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        foreach (var text in VectorData.Texts)
        {
            if (string.IsNullOrWhiteSpace(text.CleanText))
                continue;

            // Filter out system metadata, unplaced tokens, or layer definitions
            if (text.Category == DrawingVectorTextCategory.Metadata)
                continue;

            // In SheetNotesOnly mode (default), filter out view dimensions and callouts to eliminate ghosting
            if (OverlayMode == DrawingOverlayMode.SheetNotesOnly && !text.IsSheetLevelNote)
                continue;

            // Filter out system placeholders or unplaced text
            if (text.PixelX <= 0 && text.PixelY <= 0)
                continue;

            Typeface typeface = defaultTypeface;
            if (!string.IsNullOrWhiteSpace(text.FontName))
            {
                typeface = new Typeface(new FontFamily($"{text.FontName}, SimSun, Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            }

            // Target font emSize roughly matches the pixel height in SolidWorks title block
            double emSize = Math.Max(6.0, text.PixelHeight * 0.95);

            var ft = new FormattedText(
                text.CleanText,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                emSize,
                TextBrush,
                pixelsPerDip);

            // Anchor point adjustment: SolidWorks drawing text anchor is top-left aligned
            double drawX = text.PixelX + 4.0;
            double drawY = text.PixelY + 2.0;

            bool hasRotation = Math.Abs(text.AngleRad) > 1e-4;

            if (hasRotation)
            {
                double angleDeg = -text.AngleRad * (180.0 / Math.PI);
                dc.PushTransform(new RotateTransform(angleDeg, text.PixelX, text.PixelY));
            }

            // Anti-ghosting background mask: erase blurry raster text from preview bitmap underneath
            if (EnableBackgroundErasing && text.IsSheetLevelNote)
            {
                Brush bgBrush = (text.Category == DrawingVectorTextCategory.TitleBlock) ? TitleBlockBgBrush : PaperBgBrush;
                double padX = 1.5;
                double padY = 0.5;
                dc.DrawRectangle(bgBrush, null, new Rect(drawX - padX, drawY - padY, ft.WidthIncludingTrailingWhitespace + padX * 2, ft.Height + padY * 2));
            }

            dc.DrawText(ft, new Point(drawX, drawY));

            if (hasRotation)
            {
                dc.Pop();
            }
        }
    }
}
