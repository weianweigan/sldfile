using System;
using System.Collections.Generic;

namespace SldFile.Sketches;

public sealed class SketchDimension
{
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Length in meters when known; 
    /// null when source units or dimension kind are not established.
    /// </summary>
    public double? SystemValue { get; internal set; }

    /// <summary>
    /// BinaryParameter or KeywordSummary. 
    /// A keyword value is a summary, not a reconstructed driver object.
    /// </summary>
    public string ValueSource { get; internal set; } = string.Empty;

    public string? DisplayValue { get; internal set; }

    public int SourceOffset { get; internal set; }

    /// <summary>
    /// Owning sketch feature that defines this dimension.
    /// </summary>
    public SketchFeature? OwnerSketch { get; internal set; }

    /// <summary>
    /// Driving sketch relation associated with this dimension (e.g. Distance or Radius relation).
    /// </summary>
    public SketchRelation? Relation { get; internal set; }

    /// <summary>
    /// Returns all geometry entities (points, segments) attached to or driven by this dimension.
    /// </summary>
    public IReadOnlyList<object> GetAttachedEntities()
    {
        if (Relation != null && Relation.References.Count > 0)
        {
            var list = new List<object>();
            foreach (var r in Relation.References)
            {
                if (r.Target != null)
                {
                    list.Add(r.Target);
                }
            }
            if (list.Count > 0)
                return list.AsReadOnly();
        }

        // If not directly linked via Relation, try resolving via OwnerSketch
        if (OwnerSketch != null)
        {
            foreach (var rel in OwnerSketch.Relations)
            {
                if ((rel.Kind == SketchRelationKind.Distance || rel.Kind == SketchRelationKind.Radius || rel.Kind == SketchRelationKind.Diameter)
                    && rel.References.Count > 0)
                {
                    var list = new List<object>();
                    foreach (var r in rel.References)
                    {
                        if (r.Target != null)
                            list.Add(r.Target);
                    }
                    if (list.Count > 0)
                        return list.AsReadOnly();
                }
            }
        }

        return Array.Empty<object>();
    }

    /// <summary>Returns attached sketch points.</summary>
    public IReadOnlyList<SketchPoint> GetAttachedPoints()
    {
        var points = new List<SketchPoint>();
        foreach (var entity in GetAttachedEntities())
        {
            if (entity is SketchPoint pt)
                points.Add(pt);
        }
        return points.AsReadOnly();
    }

    /// <summary>Returns attached sketch segments.</summary>
    public IReadOnlyList<SketchSegment> GetAttachedSegments()
    {
        var segments = new List<SketchSegment>();
        foreach (var entity in GetAttachedEntities())
        {
            if (entity is SketchSegment seg)
                segments.Add(seg);
        }
        return segments.AsReadOnly();
    }
}
