using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>
/// Derives the connected boundary traversal order (loop order) of a region edge
/// set from the sketch geometry.
///
/// Data source: decoded sketch segments (SketchFeature.Segments) with
/// StartPoint/EndPoint. The region boundary may use sub-portions of sketch
/// segments (e.g. the L-shape uses only the (0.005,0.01)-(0.01,0.01) part of a
/// 0-0.01 sketch line), so the resolver builds pieces from segment/segment
/// intersections and searches for the unique cycle that uses every selected
/// segment exactly once.
///
/// Native loop orders recovered from the region chooser groups
/// (group g = [e[N-g], e[N-2-g], e[N-1-g]]) are all clockwise:
///   select_region (L)        [4,3,6,7,8,5]
///   select_region_overlap    [3,4,5,6]
///   SelectionModes/region    [6,7,8,5]
/// The resolver orients the cycle clockwise and returns a rotation starting at
/// the smallest selected edge id (or a caller-preferred edge). The template
/// writer can rotate further to the native start edge for byte-identical
/// re-emission.
///
/// Ambiguous inputs return null: missing geometry, arcs/splines without line
/// endpoints, overlapping collinear edges, no closed cycle, or more than one
/// candidate cycle.
/// </summary>
internal static class SketchRegionLoopOrderResolver
{
    private const double Epsilon = 1e-9;
    private const int MaxSearchSteps = 200_000;

    /// <summary>Resolves the loop order from the owning sketch feature.</summary>
    internal static IReadOnlyList<uint>? Resolve(
        SketchFeatureModel? sketch,
        IReadOnlyCollection<uint> regionEdgeIds,
        uint? preferredStartEdge = null)
    {
        if (sketch == null || regionEdgeIds == null) return null;
        return Resolve(sketch.Segments, regionEdgeIds, preferredStartEdge);
    }

    /// <summary>Resolves the loop order from decoded sketch segments.</summary>
    internal static IReadOnlyList<uint>? Resolve(
        IReadOnlyList<SketchSegmentModel>? segments,
        IReadOnlyCollection<uint> regionEdgeIds,
        uint? preferredStartEdge = null)
    {
        if (segments == null || regionEdgeIds == null) return null;
        var ids = regionEdgeIds.Distinct().ToList();
        if (ids.Count < 3 || ids.Count != regionEdgeIds.Count) return null;
        if (ids.Any(id => id == 0)) return null;

        var selected = new List<SegmentGeometry>();
        foreach (uint id in ids)
        {
            var segment = segments.FirstOrDefault(s => s.Id == id);
            if (segment == null) return null;
            if (!TryReadLine(segment, out var a, out var b)) return null;
            selected.Add(new SegmentGeometry(id, a, b));
        }

        // Collect intersection parameters for every selected segment.
        var cuts = selected.Select(_ => new List<double> { 0.0, 1.0 }).ToList();
        for (int i = 0; i < selected.Count; i++)
        {
            for (int j = i + 1; j < selected.Count; j++)
            {
                if (!TryIntersect(selected[i], selected[j], out double ti, out double tj, out bool collinear))
                    return null; // overlapping collinear edges are ambiguous
                if (double.IsNaN(ti)) continue; // parallel, no intersection
                cuts[i].Add(ti);
                cuts[j].Add(tj);
            }
        }

        // Build pieces per segment.
        var piecesBySegment = new List<Piece>[selected.Count];
        for (int i = 0; i < selected.Count; i++)
        {
            var ts = NormalizeParameters(cuts[i]);
            var pieces = new List<Piece>();
            for (int k = 0; k + 1 < ts.Count; k++)
            {
                if (ts[k + 1] - ts[k] <= Epsilon) continue;
                var p0 = selected[i].PointAt(ts[k]);
                var p1 = selected[i].PointAt(ts[k + 1]);
                if (Distance(p0, p1) <= Epsilon) continue;
                pieces.Add(new Piece(selected[i].Id, p0, p1));
            }
            if (pieces.Count == 0) return null;
            piecesBySegment[i] = pieces;
        }

        var used = new HashSet<uint>();
        IReadOnlyList<uint>? result = null;
        IReadOnlyList<Vec3>? resultVertices = null;
        int steps = 0;
        for (int firstPiece = 0; firstPiece < piecesBySegment[0].Count; firstPiece++)
        {
            var first = piecesBySegment[0][firstPiece];
            var order = new List<uint>();
            var vertices = new List<Vec3>();
            used.Add(first.SegmentId);
            order.Add(first.SegmentId);
            vertices.Add(first.Start);
            vertices.Add(first.End);
            if (SearchCycle(piecesBySegment, used, order, vertices, first.End, first.Start,
                    selected.Count, ref steps, ref result, ref resultVertices))
                break;
            used.Clear();
        }
        if (result == null || resultVertices == null) return null;
        return OrientAndRotate(result, resultVertices, preferredStartEdge);
    }

    private static bool SearchCycle(List<Piece>[] piecesBySegment, HashSet<uint> used, List<uint> order,
        List<Vec3> vertices, Vec3 current, Vec3 start, int targetSegmentCount, ref int steps,
        ref IReadOnlyList<uint>? result, ref IReadOnlyList<Vec3>? resultVertices)
    {
        if (++steps > MaxSearchSteps) return false;
        if (used.Count == targetSegmentCount)
        {
            if (Distance(current, start) <= Epsilon && order.Count == targetSegmentCount)
            {
                result = order.ToArray();
                resultVertices = vertices.ToArray();
                return true;
            }
            return false;
        }
        for (int i = 0; i < piecesBySegment.Length; i++)
        {
            foreach (var piece in piecesBySegment[i])
            {
                if (used.Contains(piece.SegmentId)) continue;
                Vec3 next;
                if (SamePoint(piece.Start, current)) next = piece.End;
                else if (SamePoint(piece.End, current)) next = piece.Start;
                else continue;
                used.Add(piece.SegmentId);
                order.Add(piece.SegmentId);
                vertices.Add(next);
                if (SearchCycle(piecesBySegment, used, order, vertices, next, start, targetSegmentCount,
                        ref steps, ref result, ref resultVertices))
                    return true;
                vertices.RemoveAt(vertices.Count - 1);
                order.RemoveAt(order.Count - 1);
                used.Remove(piece.SegmentId);
            }
        }
        return false;
    }

    private static IReadOnlyList<uint> OrientAndRotate(IReadOnlyList<uint> order,
        IReadOnlyList<Vec3> vertices, uint? preferredStartEdge)
    {
        if (SignedArea(vertices) > 0)
            order = order.Reverse().ToArray();
        uint startEdge = preferredStartEdge.HasValue && order.Contains(preferredStartEdge.Value)
            ? preferredStartEdge.Value
            : order.Min();
        int startIndex = order.ToList().IndexOf(startEdge);
        var rotated = new List<uint>(order.Count);
        for (int i = 0; i < order.Count; i++) rotated.Add(order[(startIndex + i) % order.Count]);
        return rotated.AsReadOnly();
    }

    private static double SignedArea(IReadOnlyList<Vec3> polygon)
    {
        double sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return sum / 2.0;
    }

    private static bool TryReadLine(SketchSegmentModel segment, out Vec3 a, out Vec3 b)
    {
        a = default;
        b = default;
        if (segment.Kind == SketchSegmentKind.Arc ||
            segment.Kind == SketchSegmentKind.Ellipse ||
            segment.Kind == SketchSegmentKind.Spline ||
            segment.Kind == SketchSegmentKind.Text)
            return false;
        var start = segment.StartPoint;
        var end = segment.EndPoint;
        if (start == null || end == null) return false;
        a = new Vec3(start.X, start.Y, start.Z);
        b = new Vec3(end.X, end.Y, end.Z);
        return Distance(a, b) > Epsilon;
    }

    /// <summary>
    /// Segment/segment intersection in 2D (Z is ignored for the classification).
    /// Returns false for overlapping collinear segments (ambiguous boundary).
    /// NaN parameters mean parallel without overlap.
    /// </summary>
    private static bool TryIntersect(SegmentGeometry a, SegmentGeometry b,
        out double ta, out double tb, out bool collinear)
    {
        ta = double.NaN;
        tb = double.NaN;
        collinear = false;
        double ax = a.B.X - a.A.X, ay = a.B.Y - a.A.Y;
        double bx = b.B.X - b.A.X, by = b.B.Y - b.A.Y;
        double denominator = ax * by - ay * bx;
        double qx = b.A.X - a.A.X, qy = b.A.Y - a.A.Y;
        if (Math.Abs(denominator) <= Epsilon)
        {
            // Parallel: reject overlapping collinear segments as ambiguous.
            double cross = qx * ay - qy * ax;
            if (Math.Abs(cross) <= Epsilon)
            {
                // Project b endpoints onto a to check for overlap.
                double len2 = ax * ax + ay * ay;
                if (len2 > Epsilon)
                {
                    double t0 = (qx * ax + qy * ay) / len2;
                    double t1 = ((b.B.X - a.A.X) * ax + (b.B.Y - a.A.Y) * ay) / len2;
                    double lo = Math.Max(0, Math.Min(t0, t1));
                    double hi = Math.Min(1, Math.Max(t0, t1));
                    if (hi - lo > 1e-6) return false; // overlapping collinear: ambiguous
                }
            }
            collinear = false;
            return true;
        }
        ta = (qx * by - qy * bx) / denominator;
        tb = (qx * ay - qy * ax) / denominator;
        if (ta < -Epsilon || ta > 1 + Epsilon || tb < -Epsilon || tb > 1 + Epsilon)
        {
            ta = double.NaN;
            tb = double.NaN;
        }
        else
        {
            ta = Math.Max(0, Math.Min(1, ta));
            tb = Math.Max(0, Math.Min(1, tb));
        }
        return true;
    }

    private static List<double> NormalizeParameters(List<double> values)
    {
        values.Sort();
        var result = new List<double>();
        foreach (double value in values)
        {
            if (result.Count == 0 || value - result[result.Count - 1] > Epsilon)
                result.Add(value);
        }
        return result;
    }

    private static bool SamePoint(Vec3 a, Vec3 b) => Distance(a, b) <= 1e-7;

    private static double Distance(Vec3 a, Vec3 b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private readonly struct Vec3
    {
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }

    private sealed class SegmentGeometry
    {
        public SegmentGeometry(uint id, Vec3 a, Vec3 b)
        {
            Id = id;
            A = a;
            B = b;
        }

        public uint Id { get; }
        public Vec3 A { get; }
        public Vec3 B { get; }

        public Vec3 PointAt(double t) =>
            new Vec3(A.X + (B.X - A.X) * t, A.Y + (B.Y - A.Y) * t, A.Z + (B.Z - A.Z) * t);
    }

    private sealed class Piece
    {
        public Piece(uint segmentId, Vec3 start, Vec3 end)
        {
            SegmentId = segmentId;
            Start = start;
            End = end;
        }

        public uint SegmentId { get; }
        public Vec3 Start { get; }
        public Vec3 End { get; }
    }
}
