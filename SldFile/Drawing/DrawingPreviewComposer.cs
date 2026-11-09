using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Drawing;

internal static class DrawingPreviewComposer
{
    internal static DrawingViewCacheArchive ReadCache(byte[]? bytes, bool definition, string[] names)
    {
        var model = new DrawingViewCacheArchive { IsDefinition = definition, ViewNames = names };
        using var ar = MfcArchive.CreateReader(bytes ?? Array.Empty<byte>()); model.Serialize(ar);
        return model;
    }

    internal static void AppendViews(DrawingPreviewDefinition preview, byte[]? definition, byte[]? buckets, string[] names)
    {
        if (preview.SheetIndex != 0 || names.Length == 0) return;
        var cache = ReadCache(buckets, false, names);
        // A detail view may use a presentation label in KeyWords while VBLists
        // references its native object name. Match that serialized name exactly.
        var nativeNames = cache.Fragments.Where(f => f.Owner.Length > 0).Select(f => f.Owner).Distinct().ToArray();
        var placements = ReadCache(definition, true, nativeNames.Length > 0 ? nativeNames : names);
        var geometry = new List<DrawingPreviewPrimitive>();
        var views = new List<DrawingPreviewViewDefinition>();
        foreach (var fragment in placements.Fragments)
        {
            var placement = (DrawingViewPlacementModel)fragment.Model;
            var owned = cache.Fragments.Where(e => e.Owner == placement.Name && e.Model is DrawingCachedPolylineModel).ToList();
            var edges = owned.Where(e => ((DrawingCachedPolylineModel)e.Model).Active).ToList();
            if (owned.Count == 0) continue;
            views.Add(new DrawingPreviewViewDefinition { Name = placement.Name, Scale = placement.Scale,
                Matrix = placement.Matrix.ToArray(), Translation = placement.Translation,
                ClipMin = new DrawingPreviewPoint(placement.Bounds[4], placement.Bounds[5]),
                ClipMax = new DrawingPreviewPoint(placement.Bounds[6], placement.Bounds[7]) });
            if (edges.Count == 0)
                AppendFaces(geometry, cache, placement);
            foreach (var edge in edges)
            {
                var polyline = (DrawingCachedPolylineModel)edge.Model;
                for (int i = 1; i < polyline.Points.Length; i++)
                {
                    var first = placement.Project(polyline.Points[i - 1]); var last = placement.Project(polyline.Points[i]);
                    if (!Clip(placement.Bounds, ref first, ref last)) continue;
                    geometry.Add(new DrawingPreviewPrimitive { Id = $"VB:{edge.Offset:X8}:{i}", Kind = DrawingPreviewPrimitiveKind.Line,
                        ViewName = placement.Name,
                        Points = new[] { first, last }, LineStyle = polyline.Hidden ? "HIDDEN" : "CONTINUOUS", ColorRgb = 0,
                        LineWidth = polyline.Hidden ? 0.00018 : 0.00025 });
                }
            }
        }
        geometry.AddRange(preview.Primitives);
        preview.Primitives = geometry.AsReadOnly(); preview.Views = views.AsReadOnly();
    }

    private static void AppendFaces(List<DrawingPreviewPrimitive> geometry, DrawingViewCacheArchive cache, DrawingViewPlacementModel placement)
    {
        // Mesh ownership across several referenced documents is not established.
        // Never substitute another part's faces merely because they share a projection.
        if (cache.Fragments.Where(f => f.Model is DrawingCachedPolylineModel).Select(f => f.ReferencedDocument).Distinct().Count() != 1) return;
        var faces = new List<(double Depth, DrawingPreviewPrimitive Primitive)>();
        foreach (var fragment in cache.Fragments.Where(f => f.Model is DrawingTessellationModel))
        {
            var mesh = (DrawingTessellationModel)fragment.Model;
            int start = 0;
            foreach (uint strip in mesh.Strips)
            {
                for (int i = 0; i < strip - 2; i++)
                {
                    var a = mesh.Point(start + i); var b = mesh.Point(start + i + (i % 2 == 0 ? 1 : 2));
                    var c = mesh.Point(start + i + (i % 2 == 0 ? 2 : 1));
                    var pa = placement.Project(a); var pb = placement.Project(b); var pc = placement.Project(c);
                    double area = (pb.X - pa.X) * (pc.Y - pa.Y) - (pb.Y - pa.Y) * (pc.X - pa.X);
                    if (area <= 1e-15) continue; // Native back-face winding.
                    var polygon = new List<DrawingPreviewPoint> { pa, pb, pc };
                    for (int edge = 0; edge < 4 && polygon.Count > 0; edge++)
                    {
                        bool x = edge < 2; double bound = placement.Bounds[edge == 0 ? 4 : edge == 1 ? 6 : edge == 2 ? 5 : 7];
                        bool lower = edge == 0 || edge == 2;
                        double Coordinate(DrawingPreviewPoint p) => x ? p.X : p.Y;
                        bool Inside(DrawingPreviewPoint p) => lower ? Coordinate(p) >= bound : Coordinate(p) <= bound;
                        var clipped = new List<DrawingPreviewPoint>(); var previous = polygon.Last();
                        foreach (var current in polygon)
                        {
                            if (Inside(current) != Inside(previous))
                            {
                                double t = (bound - Coordinate(previous)) / (Coordinate(current) - Coordinate(previous));
                                clipped.Add(new DrawingPreviewPoint(previous.X + t * (current.X - previous.X), previous.Y + t * (current.Y - previous.Y)));
                            }
                            if (Inside(current)) clipped.Add(current);
                            previous = current;
                        }
                        polygon = clipped;
                    }
                    if (polygon.Count < 3) continue;
                    double Depth(DrawingPreviewPoint p) => p.X * placement.Matrix[2] + p.Y * placement.Matrix[5] + p.Z * placement.Matrix[8];
                    faces.Add(((Depth(a) + Depth(b) + Depth(c)) / 3, new DrawingPreviewPrimitive
                    {
                        Id = $"VB:F:{fragment.Offset:X8}:{start + i}", Kind = DrawingPreviewPrimitiveKind.Triangle,
                        ViewName = placement.Name,
                        Points = polygon, ColorRgb = 0xD9D9D9
                    }));
                }
                start += (int)strip;
            }
        }
        geometry.AddRange(faces.OrderBy(f => f.Depth).Select(f => f.Primitive));
    }

    private static bool Clip(double[] bounds, ref DrawingPreviewPoint first, ref DrawingPreviewPoint last)
    {
        double dx = last.X - first.X, dy = last.Y - first.Y;
        double[] p = { -dx, dx, -dy, dy }, q = { first.X - bounds[4], bounds[6] - first.X, first.Y - bounds[5], bounds[7] - first.Y };
        double start = 0, end = 1;
        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-15) { if (q[i] < 0) return false; continue; }
            double r = q[i] / p[i];
            if (p[i] < 0) start = Math.Max(start, r); else end = Math.Min(end, r);
            if (start > end) return false;
        }
        last = new DrawingPreviewPoint(first.X + end * dx, first.Y + end * dy);
        first = new DrawingPreviewPoint(first.X + start * dx, first.Y + start * dy);
        return Math.Abs(last.X - first.X) + Math.Abs(last.Y - first.Y) > 1e-12;
    }
}
