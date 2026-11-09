using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SldFile.Drawing;

internal static class DrawingPreviewSvgWriter
{
    internal static string Write(DrawingPreviewDefinition preview, byte[]? fallback)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        var root = new XElement(svg + "svg", new XAttribute("version", "1.1"),
            new XAttribute("width", N(preview.PaperWidth * 1000) + "mm"),
            new XAttribute("height", N(preview.PaperHeight * 1000) + "mm"),
            new XAttribute("viewBox", $"0 0 {N(preview.PaperWidth)} {N(preview.PaperHeight)}"));
        root.Add(new XElement(svg + "metadata", preview.ResolutionMessage));
        root.Add(new XElement(svg + "rect", new XAttribute("width", "100%"), new XAttribute("height", "100%"), new XAttribute("fill", "white")));
        if (!preview.IsResolved && fallback?.Length > 0)
        {
            string mime = fallback[0] == 0x42 ? "image/bmp" : "image/png";
            root.Add(new XElement(svg + "image", new XAttribute("width", N(preview.PaperWidth)), new XAttribute("height", N(preview.PaperHeight)),
                new XAttribute("preserveAspectRatio", "xMidYMid meet"),
                new XAttribute("href", $"data:{mime};base64,{Convert.ToBase64String(fallback)}")));
        }
        var geometry = new XElement(svg + "g", new XAttribute("id", "views"),
            new XAttribute("transform", $"translate(0 {N(preview.PaperHeight)}) scale(1 -1)"));
        var notes = new XElement(svg + "g", new XAttribute("id", "notes"));
        root.Add(new XElement(svg + "g", new XAttribute("id", "sheet-format")), geometry,
            new XElement(svg + "g", new XAttribute("id", "dimensions")),
            new XElement(svg + "g", new XAttribute("id", "balloons")), notes);
        foreach (var p in preview.Primitives)
        {
            string color = "#" + p.ColorRgb.ToString("X6", CultureInfo.InvariantCulture);
            var points = p.Points;
            XElement element;
            if (p.Kind == DrawingPreviewPrimitiveKind.Text)
            {
                double x = points[0].X, y = preview.PaperHeight - points[0].Y;
                element = new XElement(svg + "text", new XAttribute("x", N(x)), new XAttribute("y", N(y)),
                    new XAttribute("font-family", p.FontName), new XAttribute("font-size", N(p.TextHeight)),
                    new XAttribute("fill", color),
                    new XAttribute("transform", $"translate({N(x)} {N(y)}) rotate({N(-p.Angle * 180 / Math.PI)}) scale({N(p.WidthFactor)} 1) translate({N(-x)} {N(-y)})"), p.Text);
                notes.Add(element); continue;
            }
            if (p.Kind == DrawingPreviewPrimitiveKind.Line)
                element = new XElement(svg + "line", new XAttribute("x1", N(points[0].X)), new XAttribute("y1", N(points[0].Y)),
                    new XAttribute("x2", N(points[1].X)), new XAttribute("y2", N(points[1].Y)));
            else if (p.Kind == DrawingPreviewPrimitiveKind.Triangle)
                element = new XElement(svg + "polygon", new XAttribute("points", string.Join(" ", points.Select(pt => N(pt.X) + "," + N(pt.Y)))), new XAttribute("fill", color));
            else
            {
                var s = points[0]; var e = points[1]; var c = points[2];
                double radius = Math.Sqrt((s.X - c.X) * (s.X - c.X) + (s.Y - c.Y) * (s.Y - c.Y));
                if (Math.Abs(s.X - e.X) + Math.Abs(s.Y - e.Y) < 1e-10)
                    element = new XElement(svg + "circle", new XAttribute("cx", N(c.X)), new XAttribute("cy", N(c.Y)), new XAttribute("r", N(radius)));
                else
                {
                    bool ccw = p.Normal.Z >= 0;
                    double delta = Math.Atan2(e.Y - c.Y, e.X - c.X) - Math.Atan2(s.Y - c.Y, s.X - c.X);
                    if (!ccw) delta = -delta;
                    delta = (delta % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
                    element = new XElement(svg + "path", new XAttribute("d", $"M {N(s.X)} {N(s.Y)} A {N(radius)} {N(radius)} 0 {(delta > Math.PI ? 1 : 0)} {(ccw ? 1 : 0)} {N(e.X)} {N(e.Y)}"));
                }
            }
            element.Add(new XAttribute("stroke", color), new XAttribute("stroke-width", N(p.LineWidth)));
            if (p.Kind != DrawingPreviewPrimitiveKind.Triangle) element.Add(new XAttribute("fill", "none"));
            if (p.LineStyle.IndexOf("CENTER", StringComparison.OrdinalIgnoreCase) >= 0)
                element.Add(new XAttribute("stroke-dasharray", ".003 .001 .0005 .001"));
            else if (p.LineStyle.IndexOf("HIDDEN", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                element.Add(new XAttribute("stroke-dasharray", ".002 .001"));
                element.Add(new XAttribute("class", "hidden-line"));
            }
            geometry.Add(element);
        }
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + root.ToString();
    }
}
