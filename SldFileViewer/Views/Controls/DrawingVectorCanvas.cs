using System;
using System.Windows;
using System.Windows.Media;
using SldFile.Drawing.Vector;

namespace SldFileViewer.Views.Controls;

/// <summary>
/// High-performance WPF vector rendering canvas that overlays lossless CAD vector typography
/// and graphics on top of drawing preview viewports.
/// </summary>
public sealed class DrawingVectorCanvas : FrameworkElement
{
    public static readonly DependencyProperty VectorDataProperty =
        DependencyProperty.Register(
            nameof(VectorData),
            typeof(DrawingSheetVectorData),
            typeof(DrawingVectorCanvas),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsOverlayEnabledProperty =
        DependencyProperty.Register(
            nameof(IsOverlayEnabled),
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

    public bool IsOverlayEnabled
    {
        get => (bool)GetValue(IsOverlayEnabledProperty);
        set => SetValue(IsOverlayEnabledProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
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

        if (!IsOverlayEnabled || VectorData == null || !VectorData.HasVectorData)
            return;

        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var defaultTypeface = new Typeface(new FontFamily("SimSun, Microsoft YaHei, Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        foreach (var text in VectorData.Texts)
        {
            if (string.IsNullOrWhiteSpace(text.CleanText))
                continue;

            // Filter out internal unresolved property macros
            if (text.CleanText.StartsWith("$PRPSHEET", StringComparison.OrdinalIgnoreCase))
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

            if (Math.Abs(text.AngleRad) > 1e-4)
            {
                double angleDeg = -text.AngleRad * (180.0 / Math.PI);
                dc.PushTransform(new RotateTransform(angleDeg, text.PixelX, text.PixelY));
                dc.DrawText(ft, new Point(drawX, drawY));
                dc.Pop();
            }
            else
            {
                dc.DrawText(ft, new Point(drawX, drawY));
            }
        }
    }
}
