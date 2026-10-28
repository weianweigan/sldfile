using System;
using System.Collections.Generic;

namespace SldFile.Sketches;

public sealed class SketchRelation
{
    public int TypeCode { get; internal set; }
    public SketchRelationKind Kind => (SketchRelationKind)TypeCode;
    public int SourceOffset { get; internal set; }

    /// <summary>Serialization layout identifier, not a SolidWorks operation history entry.</summary>
    public string Layout { get; internal set; } = string.Empty;

    public IReadOnlyList<SketchEntityReference> References { get; internal set; } = Array.Empty<SketchEntityReference>();

    /// <summary>Owning sketch feature context.</summary>
    public SketchFeature? OwnerSketch { get; internal set; }

    /// <summary>
    /// Returns all resolved entities (<see cref="SketchPoint"/> or <see cref="SketchSegment"/>) involved in this relation.
    /// </summary>
    public IReadOnlyList<object> GetEntities()
    {
        var list = new List<object>();
        foreach (var r in References)
        {
            if (r.Target != null)
                list.Add(r.Target);
        }
        return list.AsReadOnly();
    }

    /// <summary>Returns points involved in this relation.</summary>
    public IReadOnlyList<SketchPoint> GetPoints()
    {
        var list = new List<SketchPoint>();
        foreach (var r in References)
        {
            if (r.Target is SketchPoint pt)
                list.Add(pt);
        }
        return list.AsReadOnly();
    }

    /// <summary>Returns segments involved in this relation.</summary>
    public IReadOnlyList<SketchSegment> GetSegments()
    {
        var list = new List<SketchSegment>();
        foreach (var r in References)
        {
            if (r.Target is SketchSegment seg)
                list.Add(seg);
        }
        return list.AsReadOnly();
    }
}
