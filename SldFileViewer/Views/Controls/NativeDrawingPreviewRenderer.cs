using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SldFile.Drawing;

namespace SldFileViewer.Views.Controls;

/// <summary>Renders saved commands in sheet coordinates without fitting or inventing model geometry.</summary>
internal static class NativeDrawingPreviewRenderer
{
    internal static void Draw(DrawingContext dc, DrawingPreviewDefinition preview, DrawingOverlayMode mode,
        double width, double height, double pixelsPerDip, bool hasRaster)
    {
        if (preview.PaperWidth <= 0 || preview.PaperHeight <= 0 || width <= 0 || height <= 0) return;
        // One uniform transform for every primitive. The native paper origin is lower left.
        double inset = hasRaster ? Math.Max(4, height * 0.018) : 0;
        double scale = Math.Min((width - (hasRaster ? 10 : 0)) / preview.PaperWidth, (height - 2 * inset) / preview.PaperHeight);
        double left = (width - preview.PaperWidth * scale) / 2;
        double bottom = (height + preview.PaperHeight * scale) / 2;
        Point Map(DrawingPreviewPoint p) => new(left + p.X * scale, bottom - p.Y * scale);
        if (!hasRaster)
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
        foreach (var primitive in preview.Primitives)
        {
            var brush = new SolidColorBrush(Color.FromRgb((byte)(primitive.ColorRgb >> 16), (byte)(primitive.ColorRgb >> 8), (byte)primitive.ColorRgb));
            brush.Freeze();
            var pen = new Pen(brush, Math.Max(0.1, primitive.LineWidth * scale))
            {
                StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round
            };
            var dash = primitive.GetDashPattern();
            if (dash.Count > 0) pen.DashStyle = new DashStyle(dash.Select(d => d * scale / pen.Thickness), 0);
            pen.Freeze();
            if (primitive.Kind == DrawingPreviewPrimitiveKind.Text)
            {
                if (mode == DrawingOverlayMode.SheetNotesOnly && !primitive.IsSheetLevelNote) continue;
                var p = Map(primitive.Points[0]);
                FormattedText Format(string value) => new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily(primitive.FontName), FontStyles.Normal,
                        primitive.IsBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                    Math.Max(0.1, primitive.TextHeight * scale), brush, pixelsPerDip);
                dc.PushTransform(new RotateTransform(-primitive.Angle * 180 / Math.PI, p.X, p.Y));
                var text = Format(primitive.Text);
                var cells = primitive.GetTextElements();
                if (cells.Count > 0)
                {
                    foreach (var cell in cells)
                    {
                        var glyph = Format(cell.Text);
                        double x = p.X + cell.Offset * scale;
                        double stretch = cell.Advance > 0 && glyph.WidthIncludingTrailingWhitespace > 0
                            ? cell.Advance * scale / glyph.WidthIncludingTrailingWhitespace : 1;
                        dc.PushTransform(new ScaleTransform(stretch, 1, x, p.Y));
                        dc.DrawText(glyph, new Point(x, p.Y - text.Height));
                        dc.Pop();
                    }
                }
                else
                {
                    dc.PushTransform(new ScaleTransform(primitive.WidthFactor, 1, p.X, p.Y));
                    dc.DrawText(text, new Point(p.X, p.Y - text.Height));
                    dc.Pop();
                }
                dc.Pop();
                continue;
            }
            if (mode == DrawingOverlayMode.SheetNotesOnly) continue;
            var points = primitive.Points;
            if (primitive.Kind == DrawingPreviewPrimitiveKind.Line)
            {
                var geometry = new StreamGeometry();
                bool closed = points.Count > 2 && (Map(points[0]) - Map(points[points.Count - 1])).Length < 1e-7;
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(Map(points[0]), false, closed);
                    int count = closed ? points.Count - 1 : points.Count;
                    for (int i = 1; i < count; i++) ctx.LineTo(Map(points[i]), true, false);
                }
                geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
            }
            else if (primitive.Kind == DrawingPreviewPrimitiveKind.Triangle)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(Map(points[0]), true, true);
                    for (int i = 1; i < points.Count; i++) ctx.LineTo(Map(points[i]), true, false);
                }
                geometry.Freeze(); dc.DrawGeometry(brush, null, geometry);
            }
            else if (primitive.Kind == DrawingPreviewPrimitiveKind.Arc)
            {
                var start = Map(points[0]); var end = Map(points[1]); var center = Map(points[2]);
                double radius = (start - center).Length;
                if (radius <= 0) continue;
                if ((end - start).Length < 1e-7)
                    dc.DrawEllipse(null, pen, center, radius, radius);
                else
                {
                    double a = Math.Atan2(points[0].Y - points[2].Y, points[0].X - points[2].X);
                    double b = Math.Atan2(points[1].Y - points[2].Y, points[1].X - points[2].X);
                    bool clockwise = primitive.Normal.Z < 0;
                    double sweep = clockwise ? a - b : b - a;
                    sweep = (sweep % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
                    var geometry = new StreamGeometry();
                    using (var ctx = geometry.Open())
                    {
                        ctx.BeginFigure(start, false, false);
                        ctx.ArcTo(end, new Size(radius, radius), 0, sweep > Math.PI,
                            clockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true, false);
                    }
                    geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
                }
            }
        }
        dc.Pop();
    }
}
