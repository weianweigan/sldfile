using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Sketches;

/// <summary>
/// Sketch content model. Supports both read-only deserialized state and programmatic authoring of sketch entities & relations.
/// Coordinates and length dimensions use meters in the sketch coordinate system.
/// </summary>
public sealed class SketchFeature
{
    private readonly List<SketchPoint> _points = new();
    private readonly List<SketchSegment> _segments = new();
    private readonly List<SketchRelation> _relations = new();
    private readonly List<SketchDimension> _dimensions = new();
    private readonly List<SketchDimension> _parameters = new();
    private readonly List<string> _diagnostics = new();

    public string Name { get; internal set; } = string.Empty;
    public int? Id { get; internal set; }
    public int ConfigurationIndex { get; internal set; }
    public bool? Is3D { get; internal set; }
    public string SourceStream { get; internal set; } = string.Empty;
    public int SourceOffset { get; internal set; }

    public SketchFeature() { }

    public SketchFeature(string name)
    {
        Name = name ?? string.Empty;
    }

    /// <summary>True when the source contains data whose full meaning is not decoded. Never implies solver completeness.</summary>
    public bool IsPartial => Diagnostics.Count != 0;

    public IReadOnlyList<SketchPoint> Points
    {
        get => _points.AsReadOnly();
        internal set
        {
            _points.Clear();
            if (value != null) _points.AddRange(value);
        }
    }

    public IReadOnlyList<SketchSegment> Segments
    {
        get => _segments.AsReadOnly();
        internal set
        {
            _segments.Clear();
            if (value != null) _segments.AddRange(value);
        }
    }

    public IReadOnlyList<SketchRelation> Relations
    {
        get => _relations.AsReadOnly();
        internal set
        {
            _relations.Clear();
            if (value != null) _relations.AddRange(value);
        }
    }

    public IReadOnlyList<SketchDimension> Dimensions
    {
        get => _dimensions.AsReadOnly();
        internal set
        {
            _dimensions.Clear();
            if (value != null) _dimensions.AddRange(value);
        }
    }

    /// <summary>Decoded binary length parameters, including hidden operation parameters which COM does not expose as display dimensions.</summary>
    public IReadOnlyList<SketchDimension> Parameters
    {
        get => _parameters.AsReadOnly();
        internal set
        {
            _parameters.Clear();
            if (value != null) _parameters.AddRange(value);
        }
    }

    public IReadOnlyList<string> Diagnostics
    {
        get => _diagnostics.AsReadOnly();
        internal set
        {
            _diagnostics.Clear();
            if (value != null) _diagnostics.AddRange(value);
        }
    }

    internal byte[] RawData = Array.Empty<byte>();

    /// <summary>Returns a defensive copy including undecoded data in this sketch's source range.</summary>
    public byte[] GetRawData() => (byte[])RawData.Clone();

    #region Entity Authoring Methods

    /// <summary>Adds a point to the sketch point table.</summary>
    public SketchPoint AddPoint(double x, double y, double z = 0)
    {
        uint nextId = _points.Count > 0 ? _points.Max(p => p.Id) + 1 : 1;
        var pt = new SketchPoint
        {
            Id = nextId,
            TableIndex = _points.Count,
            X = x,
            Y = y,
            Z = z
        };
        _points.Add(pt);
        return pt;
    }

    /// <summary>Adds a line segment between two existing points.</summary>
    public SketchSegment AddLine(SketchPoint start, SketchPoint end, bool isConstruction = false)
    {
        if (start == null) throw new ArgumentNullException(nameof(start));
        if (end == null) throw new ArgumentNullException(nameof(end));
        uint nextId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 1;
        var seg = new SketchSegment
        {
            Id = nextId,
            Kind = SketchSegmentKind.Line,
            StartPoint = start,
            EndPoint = end,
            IsConstruction = isConstruction
        };
        _segments.Add(seg);
        return seg;
    }

    /// <summary>Adds a line segment with newly created endpoints.</summary>
    public SketchSegment AddLine(double x1, double y1, double x2, double y2, bool isConstruction = false)
    {
        var p1 = AddPoint(x1, y1);
        var p2 = AddPoint(x2, y2);
        return AddLine(p1, p2, isConstruction);
    }

    /// <summary>Adds a circular arc with center, start and end points.</summary>
    public SketchSegment AddArc(SketchPoint center, SketchPoint start, SketchPoint end, int direction = 1, bool isConstruction = false)
    {
        if (center == null) throw new ArgumentNullException(nameof(center));
        if (start == null) throw new ArgumentNullException(nameof(start));
        if (end == null) throw new ArgumentNullException(nameof(end));

        double radius = Math.Sqrt((start.X - center.X) * (start.X - center.X) +
                                  (start.Y - center.Y) * (start.Y - center.Y) +
                                  (start.Z - center.Z) * (start.Z - center.Z));

        uint nextId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 1;
        var seg = new SketchSegment
        {
            Id = nextId,
            Kind = SketchSegmentKind.Arc,
            CenterPoint = center,
            StartPoint = start,
            EndPoint = end,
            Radius = radius,
            IsCircle = ReferenceEquals(start, end),
            Direction = direction,
            IsConstruction = isConstruction
        };
        _segments.Add(seg);
        return seg;
    }

    /// <summary>Adds a full circle given a center point and radius.</summary>
    public SketchSegment AddCircle(SketchPoint center, double radius, bool isConstruction = false)
    {
        if (center == null) throw new ArgumentNullException(nameof(center));
        var perimeterPoint = AddPoint(center.X + radius, center.Y, center.Z);
        uint nextId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 1;
        var seg = new SketchSegment
        {
            Id = nextId,
            Kind = SketchSegmentKind.Arc,
            CenterPoint = center,
            StartPoint = perimeterPoint,
            EndPoint = perimeterPoint,
            Radius = radius,
            IsCircle = true,
            Direction = 1,
            IsConstruction = isConstruction
        };
        _segments.Add(seg);
        return seg;
    }

    /// <summary>Adds a full circle given center coordinates and radius.</summary>
    public SketchSegment AddCircle(double centerX, double centerY, double radius, bool isConstruction = false)
    {
        var center = AddPoint(centerX, centerY);
        return AddCircle(center, radius, isConstruction);
    }

    #endregion

    #region Relation & Constraint Authoring Methods

    /// <summary>
    /// Adds a geometric relation/constraint between the specified entities.
    /// </summary>
    public SketchRelation AddRelation(SketchRelationKind kind, params object[] entities)
    {
        if (entities == null || entities.Length == 0)
            throw new ArgumentException("At least one entity is required for a relation.", nameof(entities));

        var refs = new List<SketchEntityReference>();
        foreach (var entity in entities)
        {
            if (entity is SketchPoint pt)
            {
                refs.Add(new SketchEntityReference
                {
                    ClassReference = 0x80AD,
                    Index = (ushort)pt.TableIndex,
                    Target = pt,
                    OwnerSketch = this
                });
            }
            else if (entity is SketchSegment seg)
            {
                int segIdx = _segments.IndexOf(seg);
                if (segIdx < 0) segIdx = _segments.Count;
                ushort classRef = (seg.Kind == SketchSegmentKind.Arc || seg.Kind == SketchSegmentKind.Ellipse) ? (ushort)0x80C1 : (ushort)0x80BD;
                refs.Add(new SketchEntityReference
                {
                    ClassReference = classRef,
                    Index = (ushort)segIdx,
                    Target = seg,
                    OwnerSketch = this
                });
            }
            else
            {
                throw new ArgumentException($"Unsupported relation entity type: {entity?.GetType().Name}", nameof(entities));
            }
        }

        var rel = new SketchRelation
        {
            TypeCode = (int)kind,
            Layout = (kind == SketchRelationKind.Distance || kind == SketchRelationKind.Radius || kind == SketchRelationKind.Diameter) ? "Dimension" : "Common",
            References = refs.AsReadOnly(),
            OwnerSketch = this
        };

        _relations.Add(rel);
        return rel;
    }

    /// <summary>Adds a Horizontal constraint to a line segment.</summary>
    public SketchRelation AddHorizontal(SketchSegment line) => AddRelation(SketchRelationKind.Horizontal, line);

    /// <summary>Adds a Vertical constraint to a line segment.</summary>
    public SketchRelation AddVertical(SketchSegment line) => AddRelation(SketchRelationKind.Vertical, line);

    /// <summary>Adds a Parallel constraint between two lines.</summary>
    public SketchRelation AddParallel(SketchSegment line1, SketchSegment line2) => AddRelation(SketchRelationKind.Parallel, line1, line2);

    /// <summary>Adds a Perpendicular constraint between two lines.</summary>
    public SketchRelation AddPerpendicular(SketchSegment line1, SketchSegment line2) => AddRelation(SketchRelationKind.Perpendicular, line1, line2);

    /// <summary>Adds a Coincident constraint between entities (e.g. point-on-line or point-on-point).</summary>
    public SketchRelation AddCoincident(object entity1, object entity2) => AddRelation(SketchRelationKind.Coincident, entity1, entity2);

    /// <summary>Adds a Concentric constraint between two arcs/circles.</summary>
    public SketchRelation AddConcentric(SketchSegment arc1, SketchSegment arc2) => AddRelation(SketchRelationKind.Concentric, arc1, arc2);

    /// <summary>Adds a Tangent constraint between an arc/circle and a line.</summary>
    public SketchRelation AddTangent(SketchSegment arc, SketchSegment line) => AddRelation(SketchRelationKind.Tangent, arc, line);

    /// <summary>Adds an EqualLength constraint between two segments.</summary>
    public SketchRelation AddEqualLength(SketchSegment line1, SketchSegment line2) => AddRelation(SketchRelationKind.EqualLength, line1, line2);

    /// <summary>Adds a Fixed constraint to an entity.</summary>
    public SketchRelation AddFixed(object entity) => AddRelation(SketchRelationKind.Fixed, entity);

    /// <summary>
    /// Adds a general named distance/parameter dimension without entity binding.
    /// </summary>
    public SketchDimension AddDistanceDimension(string name, double distance)
    {
        var dim = new SketchDimension
        {
            Name = name,
            SystemValue = distance,
            DisplayValue = distance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValueSource = "Metadata",
            OwnerSketch = this
        };
        _dimensions.Add(dim);
        return dim;
    }

    /// <summary>
    /// Adds a distance dimension between two sketch points and registers the associated relation.
    /// </summary>
    public SketchDimension AddDistanceDimension(SketchPoint p1, SketchPoint p2, string name, double distance)
    {
        var rel = AddRelation(SketchRelationKind.Distance, p1, p2);
        var dim = new SketchDimension
        {
            Name = name,
            SystemValue = distance,
            DisplayValue = distance.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ValueSource = "CodeAdded",
            OwnerSketch = this,
            Relation = rel
        };
        _dimensions.Add(dim);
        return dim;
    }

    #endregion
}
