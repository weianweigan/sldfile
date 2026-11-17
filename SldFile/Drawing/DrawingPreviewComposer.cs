using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Drawing;

internal static class DrawingPreviewComposer
{
    internal static void AppendViews(DrawingPreviewDefinition preview, NativeDrawingDisplayArchive display,
        DrawingViewCacheArchive cache, string[] names)
    {
        // DisplayLists and VBLists serialize corresponding ordered view arrays.
        // Use the saved display transforms, independent of unsupported objects
        // in the Definition feature graph (revision/BOM tables, topology, etc.).
        if (preview.SheetIndex != 0 || cache.NativeBuckets?.Views.Length != display.Views.Length)
        {
            preview.AddIssue("ViewCacheBinding", "Saved display/view-cache arrays cannot be paired. " + cache.DecodeFailure, "Contents/VBLists");
            return;
        }
        var geometry = new List<DrawingPreviewPrimitive>();
        var views = new List<DrawingPreviewViewDefinition>();
        for (int index = 0; index < display.Views.Length; index++)
        {
            var view = display.Views[index];
            var firstTransform = view.FirstTransform;
            var secondTransform = view.SecondTransform;
            string name = names.Length == display.Views.Length && !string.IsNullOrEmpty(names[index]) ? names[index] : $"ViewBucket_{index}";
            var bounds = new[] { Math.Max(0, view.InnerBounds[0]), Math.Max(0, view.InnerBounds[1]),
                Math.Min(preview.PaperWidth, view.InnerBounds[2]), Math.Min(preview.PaperHeight, view.InnerBounds[3]) };
            if (bounds[0] >= bounds[2] || bounds[1] >= bounds[3]) continue; // saved off-sheet/inactive views
            var edges = cache.Fragments.Where(e => e.ViewIndex == index && e.Model is NativeDrawingBucketPolyModel).ToList();
            var bucket = cache.NativeBuckets!.Views[index]?.Payload as NativeDrawingViewBucketModel;
            if (bucket == null) continue;
            if (view.Masks.Length > 0) preview.AddIssue("ViewMasks", "General view masks are not mapped.", name);
            if (view.BrokenVectors.Length > 0) preview.AddIssue("BrokenView", "Broken-view displacement vectors are not mapped.", name);
            var vb = cache.NativeBuckets;
            if ((index < vb.VisibleAtoms.Length && vb.VisibleAtoms[index].Length > 0)
                || (index < vb.VisibleSurfaceAtoms.Length && vb.VisibleSurfaceAtoms[index].Length > 0)
                || (index < vb.HiddenBodies.Length && vb.HiddenBodies[index].Length > 0)
                || (index < vb.HiddenComponents.Length && vb.HiddenComponents[index].Length > 0)
                || (index < vb.OtherHiddenComponents.Length && vb.OtherHiddenComponents[index].Length > 0))
                preview.AddIssue("VisibilitySelection", "Saved visibility selections require verified entity/component association.", name);
            var matrix = new double[9];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    for (int k = 0; k < 3; k++) matrix[row * 3 + col] += firstTransform.Matrix[row * 3 + k] * secondTransform.Matrix[k * 3 + col];
            views.Add(new DrawingPreviewViewDefinition { Name = name, Scale = firstTransform.Scale * secondTransform.Scale,
                Matrix = matrix, Translation = Transform(firstTransform.Translation, secondTransform),
                ClipMin = new DrawingPreviewPoint(bounds[0], bounds[1]), ClipMax = new DrawingPreviewPoint(bounds[2], bounds[3]) });
            // Shaded section/detail caches can contain only hatch lines, or no
            // polylines at all. Their actual cut/body surfaces are saved in the
            // per-view tessellation association, not in another view's edges.
            if (bucket.Bucket.PolygonSets.Take(4).All(set => set == null || set.Length == 0))
            {
                int before = geometry.Count;
                AppendSurfaceContours(geometry, cache.NativeBuckets, index, name, firstTransform, secondTransform, bounds,
                    view.DetailCircle.Select(p => Transform(Transform(p, firstTransform), secondTransform)).ToArray());
                if (geometry.Count > before) preview.AddIssue("SurfaceAppearance", "Surface contours use saved tessellation; materials and lighting are not mapped.", name);
                else preview.AddIssue("MissingSurfaceCache", "No usable saved tessellation or contour cache was found.", name);
            }
            foreach (var edge in edges)
            {
                var polyline = (NativeDrawingBucketPolyModel)edge.Model;
                DrawingPreviewPoint Project(DrawingPreviewPoint p) => Transform(edge.ViewLocalCoordinates ? p : Transform(p, firstTransform), secondTransform);
                var points = polyline.GetPoints().Select(Project).ToArray();
                DrawingPreviewPrimitive Create(string suffix, IReadOnlyList<DrawingPreviewPoint> path) => new DrawingPreviewPrimitive
                {
                    Id = $"VB:{edge.Offset:X8}:{suffix}", Kind = DrawingPreviewPrimitiveKind.Line, ViewName = name,
                    Points = path, LineStyle = polyline.HasLineVisual != 0 && !string.IsNullOrEmpty(polyline.LineStyle)
                        ? polyline.LineStyle! : edge.Hidden ? "HIDDEN" : "CONTINUOUS",
                    ColorRgb = polyline.HasLineVisual != 0 ? ColorRgb(polyline.LineColor) : 0,
                    LineWidth = polyline.HasLineVisual != 0 && polyline.LineWidth.Width > 0
                        ? polyline.LineWidth.Width : edge.Hidden ? 0.00018 : 0.00025
                };
                if (polyline.HasArcData && TryNativeArc(points, Project(polyline.ArcStart), bounds, out var arc, out double direction))
                {
                    var primitive = Create("A", arc);
                    primitive.Kind = DrawingPreviewPrimitiveKind.Arc;
                    primitive.Normal = new DrawingPreviewPoint(0, 0, direction);
                    geometry.Add(primitive);
                    continue;
                }
                // Keep each connected run intact. Clipping can split a path,
                // but must never join separate runs across an outside interval.
                var run = new List<DrawingPreviewPoint>();
                int runIndex = 0;
                void Flush()
                {
                    if (run.Count > 1) geometry.Add(Create((++runIndex).ToString(), run.ToArray()));
                    run.Clear();
                }
                for (int i = 1; i < points.Length; i++)
                {
                    var first = points[i - 1]; var last = points[i];
                    if (!Clip(bounds, ref first, ref last)) { Flush(); continue; }
                    if (run.Count != 0 && !Same(run[run.Count - 1], first)) Flush();
                    if (run.Count == 0) run.Add(first);
                    if (!Same(run[run.Count - 1], last)) run.Add(last);
                    if (!Same(last, points[i])) Flush();
                }
                Flush();
            }
        }
        geometry.AddRange(preview.Primitives);
        preview.Primitives = geometry.AsReadOnly(); preview.Views = views.AsReadOnly();
        if (geometry.Any(p => p.Kind != DrawingPreviewPrimitiveKind.Text && p.LineStyle.Length > 0
            && !p.LineStyle.Equals("CONTINUOUS", StringComparison.OrdinalIgnoreCase)))
            preview.AddIssue("LineStyleScale", "Native dash segment units are not verified; named dash styles use approximate spacing.", "Contents/DisplayLists");
    }

    private static uint ColorRgb(int color) => ((uint)(color & 255) << 16) | (uint)(color & 0xFF00) | ((uint)color >> 16 & 255);
    private static bool Same(DrawingPreviewPoint a, DrawingPreviewPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < 1e-12;

    private static void AppendSurfaceContours(List<DrawingPreviewPrimitive> target, NativeDrawingVbArchive archive, int viewIndex,
        string name, NativeDrawingTransformModel first, NativeDrawingTransformModel second, double[] bounds, DrawingPreviewPoint[] mask)
    {
        if (viewIndex >= archive.ViewIndices.Length) return;
        int tessellation = archive.ViewIndices[viewIndex];
        if (tessellation < 0 || tessellation >= archive.Tessellations.Length) return;
        var source = archive.Tessellations[tessellation];
        var faces = new List<List<DrawingPreviewPrimitive>>();
        int faceIndex = 0;
        void Part(NativeDrawingPartTessModel part, Func<DrawingPreviewPoint, DrawingPreviewPoint> toModel)
        {
            foreach (var body in part.Bodies.Select(n => n?.Payload).OfType<NativeDrawingBodyTessModel>())
            foreach (var face in body.Faces.Select(n => n?.Payload).OfType<NativeDrawingFaceTessModel>())
            {
                var facet = face.Facet;
                var strips = facet.Channels[0].Words; var positions = facet.Channels[1];
                if (positions.ElementSize != 12 || strips.Sum(n => (long)n) != positions.Count) continue;
                int currentFace = faceIndex++, start = 0, triangle = 0;
                var items = new List<DrawingPreviewPrimitive>();
                var boundary = new Dictionary<(DrawingPreviewPoint, DrawingPreviewPoint), int>();
                DrawingPreviewPoint Point(int i) => Transform(Transform(toModel(new DrawingPreviewPoint(
                    positions.Real(i * 3), positions.Real(i * 3 + 1), positions.Real(i * 3 + 2))), first), second);
                void Edge(DrawingPreviewPoint a, DrawingPreviewPoint b)
                {
                    // Independently clipped adjacent triangles can differ at
                    // an intersection by a few floating-point ulps.
                    a = new DrawingPreviewPoint(Math.Round(a.X, 10), Math.Round(a.Y, 10), Math.Round(a.Z, 10));
                    b = new DrawingPreviewPoint(Math.Round(b.X, 10), Math.Round(b.Y, 10), Math.Round(b.Z, 10));
                    var reverse = (b, a);
                    if (boundary.ContainsKey(reverse)) boundary[reverse]++;
                    else { var key = (a, b); boundary[key] = boundary.TryGetValue(key, out int n) ? n + 1 : 1; }
                }
                foreach (uint strip in strips)
                {
                    for (int i = 0; i + 2 < strip; i++)
                    {
                        var a = Point(start + i); var b = Point(start + i + (i % 2 == 0 ? 1 : 2));
                        var c = Point(start + i + (i % 2 == 0 ? 2 : 1));
                        if ((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X) <= 1e-16) continue;
                        var polygon = ClipPolygon(new[] { a, b, c }, bounds);
                        if (mask.Length > 2) polygon = ClipConvex(polygon, mask);
                        if (polygon.Count < 3) continue;
                        for (int j = 0; j < polygon.Count; j++) Edge(polygon[j], polygon[(j + 1) % polygon.Count]);
                        items.Add(new DrawingPreviewPrimitive { Id = $"VB:M:{viewIndex}:{currentFace}:F{triangle++}", ViewName = name,
                            Kind = DrawingPreviewPrimitiveKind.Triangle, Points = polygon, ColorRgb = 0xFFFFFF, LineWidth = 0 });
                    }
                    start += (int)strip;
                }
                int edgeIndex = 0;
                foreach (var edge in boundary.Where(e => e.Value == 1))
                {
                    var a = edge.Key.Item1; var b = edge.Key.Item2;
                    if (Same(a, b) || !Clip(bounds, ref a, ref b)) continue;
                    items.Add(new DrawingPreviewPrimitive { Id = $"VB:M:{viewIndex}:{currentFace}:E{edgeIndex++}", ViewName = name,
                        Kind = DrawingPreviewPrimitiveKind.Line, Points = new[] { a, b }, LineWidth = 0.00018 });
                }
                if (items.Count > 0) faces.Add(items);
            }
        }
        void Assembly(NativeDrawingObjectGraph.Node? node, Func<DrawingPreviewPoint, DrawingPreviewPoint> parent)
        {
            if (node?.Payload is NativeDrawingPartTessModel part) { Part(part, parent); return; }
            if (node?.Payload is not NativeDrawingAssemblyTessModel assembly) return;
            DrawingPreviewPoint ToModel(DrawingPreviewPoint p) => parent(Transform(p, assembly.Component.Transform));
            Assembly(assembly.Part, ToModel);
            foreach (var child in assembly.Children) Assembly(child, ToModel);
        }
        if (source.DocumentType != 0) Part(source.Part, p => p);
        else foreach (var node in source.Assembly) Assembly(node, p => p);
        var fills = faces.SelectMany(f => f).Where(p => p.Kind == DrawingPreviewPrimitiveKind.Triangle).ToArray();
        target.AddRange(fills);
        foreach (var edge in faces.SelectMany(f => f).Where(p => p.Kind == DrawingPreviewPrimitiveKind.Line))
        {
            int run = 0;
            foreach (var interval in VisibleIntervals(edge.Points[0], edge.Points[1], fills.Select(p => p.Points)))
            {
                var visible = edge.Clone(); visible.Id += $":{run++}";
                visible.Points = new[] { Interpolate(edge.Points[0], edge.Points[1], interval.Start),
                    Interpolate(edge.Points[0], edge.Points[1], interval.End) };
                target.Add(visible);
            }
        }
    }

    // Clip a projected edge against each saved surface, then subtract only the
    // interval where that surface lies in front. Average face depth is insufficient
    // for intersecting/slanted faces and can erase visible portions of an edge.
    internal static IReadOnlyList<(double Start, double End)> VisibleIntervals(DrawingPreviewPoint a, DrawingPreviewPoint b,
        IEnumerable<IReadOnlyList<DrawingPreviewPoint>> surfaces)
    {
        var visible = new List<(double Start, double End)> { (0, 1) };
        foreach (var polygon in surfaces)
        {
            if (polygon.Count < 3 || polygon.Max(p => p.X) < Math.Min(a.X, b.X) || polygon.Min(p => p.X) > Math.Max(a.X, b.X)
                || polygon.Max(p => p.Y) < Math.Min(a.Y, b.Y) || polygon.Min(p => p.Y) > Math.Max(a.Y, b.Y)) continue;
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                var p = polygon[0]; var q = polygon[i]; var r = polygon[i + 1];
                double Cross(DrawingPreviewPoint u, DrawingPreviewPoint v, DrawingPreviewPoint w) =>
                    (v.X - u.X) * (w.Y - u.Y) - (v.Y - u.Y) * (w.X - u.X);
                double area = Cross(p, q, r);
                if (Math.Abs(area) < 1e-18) continue;
                double start = 0, end = 1;
                bool Restrict(double from, double to)
                {
                    if (from < 0 && to < 0) return false;
                    if (from < 0) start = Math.Max(start, from / (from - to));
                    if (to < 0) end = Math.Min(end, from / (from - to));
                    return start < end;
                }
                double Side(DrawingPreviewPoint u, DrawingPreviewPoint v, DrawingPreviewPoint w) => Cross(u, v, w) / area;
                if (!Restrict(Side(p, q, a), Side(p, q, b)) || !Restrict(Side(q, r, a), Side(q, r, b))
                    || !Restrict(Side(r, p, a), Side(r, p, b))) continue;
                double Depth(DrawingPreviewPoint w) => Side(q, r, w) * p.Z + Side(r, p, w) * q.Z + Side(p, q, w) * r.Z;
                if (!Restrict(Depth(a) - a.Z - 1e-8, Depth(b) - b.Z - 1e-8)) continue;
                var next = new List<(double Start, double End)>();
                foreach (var interval in visible)
                {
                    if (end <= interval.Start || start >= interval.End) { next.Add(interval); continue; }
                    if (start > interval.Start) next.Add((interval.Start, start));
                    if (end < interval.End) next.Add((end, interval.End));
                }
                visible = next;
                if (visible.Count == 0) return visible;
            }
        }
        return visible;
    }

    private static DrawingPreviewPoint Interpolate(DrawingPreviewPoint a, DrawingPreviewPoint b, double t) =>
        new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));

    private static IReadOnlyList<DrawingPreviewPoint> ClipPolygon(IEnumerable<DrawingPreviewPoint> points, double[] bounds)
    {
        var polygon = points.ToList();
        for (int edge = 0; edge < 4 && polygon.Count > 0; edge++)
        {
            bool x = edge < 2, lower = edge == 0 || edge == 2;
            double limit = bounds[edge == 0 ? 0 : edge == 1 ? 2 : edge == 2 ? 1 : 3];
            double Coordinate(DrawingPreviewPoint p) => x ? p.X : p.Y;
            bool Inside(DrawingPreviewPoint p) => lower ? Coordinate(p) >= limit : Coordinate(p) <= limit;
            var clipped = new List<DrawingPreviewPoint>(); var previous = polygon[polygon.Count - 1];
            foreach (var current in polygon)
            {
                if (Inside(current) != Inside(previous))
                {
                    double t = (limit - Coordinate(previous)) / (Coordinate(current) - Coordinate(previous));
                    clipped.Add(Interpolate(previous, current, t));
                }
                if (Inside(current)) clipped.Add(current);
                previous = current;
            }
            polygon = clipped;
        }
        return polygon;
    }

    private static IReadOnlyList<DrawingPreviewPoint> ClipConvex(IReadOnlyList<DrawingPreviewPoint> points, DrawingPreviewPoint[] mask)
    {
        var polygon = points.ToList();
        double area = 0;
        for (int i = 0; i < mask.Length; i++) area += mask[i].X * mask[(i + 1) % mask.Length].Y - mask[i].Y * mask[(i + 1) % mask.Length].X;
        double sign = Math.Sign(area);
        if (sign == 0) return polygon;
        for (int i = 0; i < mask.Length && polygon.Count > 0; i++)
        {
            var a = mask[i]; var b = mask[(i + 1) % mask.Length];
            double Side(DrawingPreviewPoint p) => sign * ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X));
            var clipped = new List<DrawingPreviewPoint>(); var previous = polygon[polygon.Count - 1];
            double previousSide = Side(previous);
            foreach (var current in polygon)
            {
                double currentSide = Side(current);
                if ((currentSide >= 0) != (previousSide >= 0))
                {
                    double t = previousSide / (previousSide - currentSide);
                    clipped.Add(Interpolate(previous, current, t));
                }
                if (currentSide >= 0) clipped.Add(current);
                previous = current; previousSide = currentSide;
            }
            polygon = clipped;
        }
        return polygon;
    }

    private static DrawingPreviewPoint Transform(DrawingPreviewPoint p, NativeDrawingTransformModel t) => new DrawingPreviewPoint(
        t.Scale * (p.X * t.Matrix[0] + p.Y * t.Matrix[3] + p.Z * t.Matrix[6]) + t.Translation.X,
        t.Scale * (p.X * t.Matrix[1] + p.Y * t.Matrix[4] + p.Z * t.Matrix[7]) + t.Translation.Y,
        t.Scale * (p.X * t.Matrix[2] + p.Y * t.Matrix[5] + p.Z * t.Matrix[8]) + t.Translation.Z);

    // In the observed gcBucket arc triple, the first point is the native
    // circle origin (the historical wire-field name is ArcStart). Do not fit
    // arbitrary polylines: require that origin and every projected sample to
    // agree on a circle. Edge-on/oblique circles and clipped arcs stay polylines.
    private static bool TryNativeArc(DrawingPreviewPoint[] samples, DrawingPreviewPoint center, double[] bounds,
        out DrawingPreviewPoint[] arc, out double direction)
    {
        arc = Array.Empty<DrawingPreviewPoint>(); direction = 1;
        if (samples.Length < 3) return false;
        double Radius(DrawingPreviewPoint p) => Math.Sqrt((p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y));
        bool Inside(DrawingPreviewPoint p) => p.X >= bounds[0] - 1e-12 && p.X <= bounds[2] + 1e-12 &&
            p.Y >= bounds[1] - 1e-12 && p.Y <= bounds[3] + 1e-12;
        double radius = Radius(samples[0]);
        if (!(radius > 1e-12) || samples.Any(p => !Inside(p) || Math.Abs(Radius(p) - radius) > Math.Max(1e-11, radius * 1e-6))) return false;
        double Angle(DrawingPreviewPoint p) => Math.Atan2(p.Y - center.Y, p.X - center.X);
        double sweep = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            double delta = Angle(samples[i]) - Angle(samples[i - 1]);
            delta = Math.Atan2(Math.Sin(delta), Math.Cos(delta));
            if (Math.Abs(delta) < 1e-12) continue;
            if (sweep != 0 && Math.Sign(delta) != Math.Sign(sweep)) return false;
            sweep += delta;
        }
        if (Math.Abs(sweep) < 1e-8 || Math.Abs(sweep) > 2 * Math.PI + 1e-6) return false;
        direction = Math.Sign(sweep);
        // Sample points alone do not prove the continuous arc is inside the
        // clip box. Check every cardinal extremum lying on its angular interval.
        for (int i = 0; i < 4; i++)
        {
            double angle = i * Math.PI / 2;
            double delta = (direction * (angle - Angle(samples[0])) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
            if (delta <= Math.Abs(sweep) + 1e-12 && !Inside(new DrawingPreviewPoint(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)))) return false;
        }
        if (Same(samples[0], samples[samples.Length - 1]) && Math.Abs(Math.Abs(sweep) - 2 * Math.PI) > 1e-6) return false;
        arc = new[] { samples[0], samples[samples.Length - 1], center };
        return true;
    }

    private static bool Clip(double[] bounds, ref DrawingPreviewPoint first, ref DrawingPreviewPoint last)
    {
        double dx = last.X - first.X, dy = last.Y - first.Y;
        double[] p = { -dx, dx, -dy, dy }, q = { first.X - bounds[0], bounds[2] - first.X, first.Y - bounds[1], bounds[3] - first.Y };
        double start = 0, end = 1;
        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-15) { if (q[i] < 0) return false; continue; }
            double r = q[i] / p[i];
            if (p[i] < 0) start = Math.Max(start, r); else end = Math.Min(end, r);
            if (start > end) return false;
        }
        var origin = first;
        first = Interpolate(origin, last, start);
        last = Interpolate(origin, last, end);
        return Math.Abs(last.X - first.X) + Math.Abs(last.Y - first.Y) > 1e-12;
    }
}
