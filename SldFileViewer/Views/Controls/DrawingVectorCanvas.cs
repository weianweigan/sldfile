using System;
using System.Windows;
using System.Windows.Media;
using SldFile.Drawing.Vector;

namespace SldFileViewer.Views.Controls;

/// <summary>
/// Rendering mode for CAD vector typography and geometry on drawing preview.
/// </summary>
public enum DrawingOverlayMode
{
    /// <summary>
    /// Vector overlay is completely disabled (original raster preview).
    /// </summary>
    Off = 0,

    /// <summary>
    /// Lossless vector typography for sheet format, title block, tolerance tables and technical notes only.
    /// Filters out view dimensions to eliminate visual ghosting over raster view geometry.
    /// </summary>
    SheetNotesOnly = 1,

    /// <summary>
    /// Overlays all extracted CAD text elements, including drawing view dimensions and callouts.
    /// </summary>
    AllAnnotations = 2,

    /// <summary>
    /// Recommended eDrawings-quality mode: 100% pure vector CAD rendering.
    /// Completely replaces raster preview bitmap with mathematical vector geometry (lines, borders, grids)
    /// and typography. Delivers infinite lossless zoom with zero pixelation and zero ghosting.
    /// </summary>
    PureVectorCad = 3
}

/// <summary>
/// High-performance WPF vector rendering canvas that overlays lossless CAD vector typography
/// and renders pure 2D vector CAD wireframe geometry (sheet borders, title block grids, view geometry)
/// delivering eDrawings-level clarity at infinite zoom.
/// </summary>
public sealed class DrawingVectorCanvas : FrameworkElement
{
    private static readonly Brush TitleBlockBgBrush;
    private static readonly Brush PaperBgBrush;

    // Pre-allocated frozen pens for optimal 60fps rendering
    private static readonly Pen ThickOutlinePen;
    private static readonly Pen ThinLinePen;
    private static readonly Pen CenterlinePen;
    private static readonly Pen BorderOuterPen;
    private static readonly Pen TableGridPen;

    static DrawingVectorCanvas()
    {
        var tbBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        tbBrush.Freeze();
        TitleBlockBgBrush = tbBrush;

        var pBrush = new SolidColorBrush(Color.FromRgb(255, 253, 240));
        pBrush.Freeze();
        PaperBgBrush = pBrush;

        // Pens
        var blackBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
        blackBrush.Freeze();

        var thickPen = new Pen(blackBrush, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        thickPen.Freeze();
        ThickOutlinePen = thickPen;

        var thinPen = new Pen(blackBrush, 0.9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        thinPen.Freeze();
        ThinLinePen = thinPen;

        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 0.8);
        gridPen.Brush.Freeze();
        gridPen.Freeze();
        TableGridPen = gridPen;

        var borderOuter = new Pen(blackBrush, 0.75);
        borderOuter.Freeze();
        BorderOuterPen = borderOuter;

        // Centerline with dash-dot pattern (ISO / GB CAD standard)
        var redOrGrey = new SolidColorBrush(Color.FromRgb(180, 40, 40));
        redOrGrey.Freeze();
        var cPen = new Pen(redOrGrey, 0.85)
        {
            DashStyle = new DashStyle(new double[] { 10, 3, 2, 3 }, 0)
        };
        cPen.Freeze();
        CenterlinePen = cPen;
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
                DrawingOverlayMode.PureVectorCad,
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
                canvas.OverlayMode = DrawingOverlayMode.PureVectorCad;
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

        bool isPureVector = (OverlayMode == DrawingOverlayMode.PureVectorCad);

        // 0. Only when vector lines provide complete model coverage (>= 2000 lines) can we draw solid paper background.
        // Otherwise, raster preview must remain visible underneath to preserve model geometry (beams, sections, bends, holes).
        if (isPureVector && VectorData.HasLines && VectorData.Lines.Count >= 2000)
        {
            double pWidth = (ActualWidth > 0) ? ActualWidth : (Width > 0 ? Width : 1420);
            double pHeight = (ActualHeight > 0) ? ActualHeight : (Height > 0 ? Height : 710);
            dc.DrawRectangle(PaperBgBrush, null, new Rect(0, 0, pWidth, pHeight));
        }

        // 1. Render 2D CAD Vector Geometry Lines (Borders, View Wireframe)
        // TableGrid and inner binding margins are skipped to prevent crossing title block text or creating spurious lines over the real sheet.
        if ((isPureVector || OverlayMode == DrawingOverlayMode.AllAnnotations) && VectorData.HasLines)
        {
            foreach (var line in VectorData.Lines)
            {
                if (line.PixelX1 <= 0 && line.PixelY1 <= 0 && line.PixelX2 <= 0 && line.PixelY2 <= 0)
                    continue;

                // Avoid synthetic grid lines that cut through real drawing title block text
                if (line.Kind == DrawingLineKind.TableGrid)
                    continue;

                // Avoid synthetic 25mm binding margin vertical line if it creates a spurious border over drawing content
                if (line.Kind == DrawingLineKind.SheetBorderInner && Math.Abs(line.X1 - 25.0) < 1e-2 && Math.Abs(line.X2 - 25.0) < 1e-2)
                    continue;

                Pen pen = line.Kind switch
                {
                    DrawingLineKind.SheetBorderOuter => BorderOuterPen,
                    DrawingLineKind.SheetBorderInner => ThickOutlinePen,
                    DrawingLineKind.TableGrid => TableGridPen,
                    DrawingLineKind.SolidOutline => ThickOutlinePen,
                    DrawingLineKind.Centerline => CenterlinePen,
                    _ => ThinLinePen
                };

                dc.DrawLine(pen, new Point(line.PixelX1, line.PixelY1), new Point(line.PixelX2, line.PixelY2));
            }
        }

        // 3. Render 2D CAD Vector Typography (Title block, tolerance, notes, dimensions)
        foreach (var text in VectorData.Texts)
        {
            if (string.IsNullOrWhiteSpace(text.CleanText))
                continue;

            // Filter out system metadata, unplaced tokens, or layer definitions
            if (text.Category == DrawingVectorTextCategory.Metadata)
                continue;

            // In SheetNotesOnly mode, filter out view dimensions and callouts to eliminate ghosting
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

            // In overlay mode, erase blurry raster text from preview bitmap underneath
            if (!isPureVector && EnableBackgroundErasing && text.IsSheetLevelNote)
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
