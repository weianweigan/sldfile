using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Mfc;

/// <summary>
/// Serializable point item in a sketch point table (corresponds to SolidWorks sgPointHandle / point records).
/// </summary>
public sealed class SketchPointModel : IMfcSerializable
{
    public const string ClassName = "sgPointHandle";

    public uint Id { get; set; }
    public int TableIndex { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public int SourceOffset { get; set; }

    public SketchPointModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            uint id = Id;
            ar.U32(ref id);

            int tidx = TableIndex;
            ar.I32(ref tidx);

            double x = X;
            ar.F64(ref x);

            double y = Y;
            ar.F64(ref y);

            double z = Z;
            ar.F64(ref z);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            if (ar.Remaining >= 4)
            {
                uint id = 0;
                ar.U32(ref id);
                Id = id;
            }

            if (ar.Remaining >= 4)
            {
                int tidx = 0;
                ar.I32(ref tidx);
                TableIndex = tidx;
            }

            if (ar.Remaining >= 8)
            {
                double x = 0;
                ar.F64(ref x);
                X = x;
            }

            if (ar.Remaining >= 8)
            {
                double y = 0;
                ar.F64(ref y);
                Y = y;
            }

            if (ar.Remaining >= 8)
            {
                double z = 0;
                ar.F64(ref z);
                Z = z;
            }
        }
    }
}

/// <summary>
/// Serializable segment item (line, arc, circle, ellipse, spline, text).
/// </summary>
public sealed class SketchSegmentModel : IMfcSerializable
{
    public const string ClassName = "sgLineHandle";
    public const string DimTextClassName = "moDimText_c";
    public const string SplineListClassName = "modifSplineList_c";

    public uint Id { get; set; }
    public int SourceOffset { get; set; }
    public SketchSegmentKind Kind { get; set; } = SketchSegmentKind.Line;
    public bool IsConstruction { get; set; }
    public int StartPointIndex { get; set; } = -1;
    public int EndPointIndex { get; set; } = -1;
    public int CenterPointIndex { get; set; } = -1;
    public int MajorPointIndex { get; set; } = -1;
    public int MinorPointIndex { get; set; } = -1;
    public double? Radius { get; set; }
    public bool? IsCircle { get; set; }
    public int? Direction { get; set; }
    public string? Text { get; set; }

    public SketchPointModel? StartPoint { get; set; }
    public SketchPointModel? EndPoint { get; set; }
    public SketchPointModel? CenterPoint { get; set; }
    public SketchPointModel? MajorPoint { get; set; }
    public SketchPointModel? MinorPoint { get; set; }

    public SketchSegmentModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            uint id = Id;
            ar.U32(ref id);

            int k = (int)Kind;
            ar.I32(ref k);

            bool c = IsConstruction;
            ar.Boolean(ref c);

            int sp = StartPointIndex;
            ar.I32(ref sp);

            int ep = EndPointIndex;
            ar.I32(ref ep);

            int cp = CenterPointIndex;
            ar.I32(ref cp);

            int maj = MajorPointIndex;
            ar.I32(ref maj);

            int min = MinorPointIndex;
            ar.I32(ref min);

            bool hasRad = Radius.HasValue;
            ar.Boolean(ref hasRad);
            if (hasRad)
            {
                double rad = Radius!.Value;
                ar.F64(ref rad);
            }

            bool hasCirc = IsCircle.HasValue;
            ar.Boolean(ref hasCirc);
            if (hasCirc)
            {
                bool circ = IsCircle!.Value;
                ar.Boolean(ref circ);
            }

            bool hasDir = Direction.HasValue;
            ar.Boolean(ref hasDir);
            if (hasDir)
            {
                int dir = Direction!.Value;
                ar.I32(ref dir);
            }

            bool hasTxt = !string.IsNullOrEmpty(Text);
            ar.Boolean(ref hasTxt);
            if (hasTxt)
            {
                string? txt = Text;
                ar.WChar(ref txt);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            if (ar.Remaining >= 4)
            {
                uint id = 0;
                ar.U32(ref id);
                Id = id;
            }

            if (ar.Remaining >= 4)
            {
                int k = 0;
                ar.I32(ref k);
                Kind = (SketchSegmentKind)k;
            }

            if (ar.Remaining >= 1)
            {
                bool c = false;
                ar.Boolean(ref c);
                IsConstruction = c;
            }

            if (ar.Remaining >= 4)
            {
                int sp = -1;
                ar.I32(ref sp);
                StartPointIndex = sp;
            }

            if (ar.Remaining >= 4)
            {
                int ep = -1;
                ar.I32(ref ep);
                EndPointIndex = ep;
            }

            if (ar.Remaining >= 4)
            {
                int cp = -1;
                ar.I32(ref cp);
                CenterPointIndex = cp;
            }

            if (ar.Remaining >= 4)
            {
                int maj = -1;
                ar.I32(ref maj);
                MajorPointIndex = maj;
            }

            if (ar.Remaining >= 4)
            {
                int min = -1;
                ar.I32(ref min);
                MinorPointIndex = min;
            }

            if (ar.Remaining >= 1)
            {
                bool hasRad = false;
                ar.Boolean(ref hasRad);
                if (hasRad && ar.Remaining >= 8)
                {
                    double rad = 0;
                    ar.F64(ref rad);
                    Radius = rad;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasCirc = false;
                ar.Boolean(ref hasCirc);
                if (hasCirc && ar.Remaining >= 1)
                {
                    bool circ = false;
                    ar.Boolean(ref circ);
                    IsCircle = circ;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasDir = false;
                ar.Boolean(ref hasDir);
                if (hasDir && ar.Remaining >= 4)
                {
                    int dir = 0;
                    ar.I32(ref dir);
                    Direction = dir;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasTxt = false;
                ar.Boolean(ref hasTxt);
                if (hasTxt && ar.Remaining >= 2)
                {
                    string? txt = null;
                    ar.WChar(ref txt);
                    Text = txt;
                }
            }
        }
    }
}

/// <summary>
/// Serializable entity reference in a sketch constraint relation.
/// </summary>
public sealed class SketchEntityReferenceModel : IMfcSerializable
{
    public ushort ClassReference { get; set; }
    public ushort Index { get; set; }
    public int SourceOffset { get; set; }
    public object? Target { get; set; }
    public SketchFeatureModel? OwnerSketch { get; set; }

    public SketchEntityReferenceModel() { }

    public SketchSegmentModel? AsSegment() => Target as SketchSegmentModel;
    public SketchPointModel? AsPoint() => Target as SketchPointModel;

    public void Serialize(MfcArchive ar)
    {
        ushort cref = ClassReference;
        ar.U16(ref cref);
        ClassReference = cref;

        ushort idx = Index;
        ar.U16(ref idx);
        Index = idx;
    }
}

/// <summary>
/// Serializable sketch geometric constraint relation (corresponds to SolidWorks moSketchRelMgr_c).
/// </summary>
public sealed class SketchRelationModel : IMfcSerializable
{
    public const string ClassName = "moSketchRelMgr_c";

    public int TypeCode { get; set; }
    public int SourceOffset { get; set; }
    public SketchRelationKind Kind
    {
        get => (SketchRelationKind)TypeCode;
        set => TypeCode = (int)value;
    }
    
    public string Layout { get; set; } = "Common";
    private List<SketchEntityReferenceModel> _references = new();
    public IReadOnlyList<SketchEntityReferenceModel> References
    {
        get => _references.AsReadOnly();
        set => _references = value != null ? new List<SketchEntityReferenceModel>(value) : new List<SketchEntityReferenceModel>();
    }
    public SketchFeatureModel? OwnerSketch { get; set; }

    public SketchRelationModel() { }

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

    public IReadOnlyList<SketchPointModel> GetPoints()
    {
        var list = new List<SketchPointModel>();
        foreach (var r in References)
        {
            if (r.Target is SketchPointModel pt)
                list.Add(pt);
        }
        return list.AsReadOnly();
    }

    public IReadOnlyList<SketchSegmentModel> GetSegments()
    {
        var list = new List<SketchSegmentModel>();
        foreach (var r in References)
        {
            if (r.Target is SketchSegmentModel seg)
                list.Add(seg);
        }
        return list.AsReadOnly();
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            int tc = TypeCode;
            ar.I32(ref tc);

            string? layout = Layout;
            ar.WChar(ref layout);

            uint count = (uint)References.Count;
            ar.Count(ref count);
            foreach (var r in References)
                r.Serialize(ar);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            if (ar.Remaining >= 4)
            {
                int tc = 0;
                ar.I32(ref tc);
                TypeCode = tc;
            }

            if (ar.Remaining >= 2)
            {
                string? layout = null;
                ar.WChar(ref layout);
                if (layout != null) Layout = layout;
            }

            if (ar.Remaining >= 2)
            {
                uint count = 0;
                ar.Count(ref count);
                _references.Clear();
                for (int i = 0; i < count && ar.Remaining >= 4; i++)
                {
                    var r = new SketchEntityReferenceModel();
                    r.Serialize(ar);
                    _references.Add(r);
                }
            }
        }
    }
}

/// <summary>
/// 统一草图尺寸对象模型 (Unified Sketch Dimension Object Model).
/// 继承自通用尺寸模型 <see cref="DimensionModel"/>，聚合了底层参数驱动层 (moLengthParameter_c / moAngleParameter_c) 与显示几何布局层 (moDisplayDim_c 族系)，并关联草图约束与实体引用.
/// </summary>
public sealed class SketchDimensionModel : DimensionModel
{
    public const string ClassName = "moLengthParameter_c";

    public int RelationIndex { get; set; } = -1;
    public SketchFeatureModel? OwnerSketch { get; set; }
    public SketchRelationModel? Relation { get; set; }

    public IReadOnlyList<object> GetAttachedEntities()
    {
        if (Relation != null) return Relation.GetEntities();
        return Array.Empty<object>();
    }

    public IReadOnlyList<SketchPointModel> GetAttachedPoints()
    {
        if (Relation != null) return Relation.GetPoints();
        return Array.Empty<SketchPointModel>();
    }

    public IReadOnlyList<SketchSegmentModel> GetAttachedSegments()
    {
        if (Relation != null) return Relation.GetSegments();
        return Array.Empty<SketchSegmentModel>();
    }

    public SketchDimensionModel() { }

    public SketchDimensionModel(MoParameterModel parameter, MoDisplayDimModel? displayDim = null)
        : base(parameter, displayDim) { }

    public SketchDimensionModel(DimensionModel other) : base(other) { }

    public SketchDimensionModel(SketchDimensionModel other) : base(other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        RelationIndex = other.RelationIndex;
        OwnerSketch = other.OwnerSketch;
        Relation = other.Relation;
    }

    public SketchDimensionModel Clone() => new(this);
    public override FeatureDimension ToFeature() => new FeatureDimension(this);

    public override void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            string? name = Name;
            ar.WChar(ref name);

            int dType = (int)DimensionType;
            ar.I32(ref dType);

            bool hasSys = SystemValue.HasValue;
            ar.Boolean(ref hasSys);
            if (hasSys)
            {
                double val = SystemValue!.Value;
                ar.F64(ref val);
            }

            bool hasDisp = DisplayValue != null;
            ar.Boolean(ref hasDisp);
            if (hasDisp)
            {
                string? disp = DisplayValue;
                ar.WChar(ref disp);
            }

            string? src = ValueSource;
            ar.WChar(ref src);

            int relIdx = RelationIndex;
            ar.I32(ref relIdx);

            // Serialization of position and direction vectors
            bool hasTextPos = TextX.HasValue && TextY.HasValue;
            ar.Boolean(ref hasTextPos);
            if (hasTextPos)
            {
                double tx = TextX!.Value, ty = TextY!.Value, tz = TextZ ?? 0.0;
                ar.F64(ref tx); ar.F64(ref ty); ar.F64(ref tz);
            }

            bool hasDir = DirectionX.HasValue && DirectionY.HasValue;
            ar.Boolean(ref hasDir);
            if (hasDir)
            {
                double dx = DirectionX!.Value, dy = DirectionY!.Value, dz = DirectionZ ?? 0.0;
                ar.F64(ref dx); ar.F64(ref dy); ar.F64(ref dz);
            }

            bool hasNorm = NormalX.HasValue && NormalY.HasValue;
            ar.Boolean(ref hasNorm);
            if (hasNorm)
            {
                double nx = NormalX!.Value, ny = NormalY!.Value, nz = NormalZ ?? 0.0;
                ar.F64(ref nx); ar.F64(ref ny); ar.F64(ref nz);
            }

            bool hasWitness = WitnessPoint1X.HasValue && WitnessPoint1Y.HasValue;
            ar.Boolean(ref hasWitness);
            if (hasWitness)
            {
                double w1x = WitnessPoint1X!.Value, w1y = WitnessPoint1Y!.Value, w1z = WitnessPoint1Z ?? 0.0;
                double w2x = WitnessPoint2X ?? 0.0, w2y = WitnessPoint2Y ?? 0.0, w2z = WitnessPoint2Z ?? 0.0;
                ar.F64(ref w1x); ar.F64(ref w1y); ar.F64(ref w1z);
                ar.F64(ref w2x); ar.F64(ref w2y); ar.F64(ref w2z);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            if (ar.Remaining >= 2)
            {
                string? name = null;
                ar.WChar(ref name);
                if (name != null) Name = name;
            }

            if (ar.Remaining >= 4)
            {
                int dType = 0;
                ar.I32(ref dType);
                DimensionType = (SketchDimensionType)dType;
            }

            if (ar.Remaining >= 1)
            {
                bool hasSys = false;
                ar.Boolean(ref hasSys);
                if (hasSys && ar.Remaining >= 8)
                {
                    double val = 0;
                    ar.F64(ref val);
                    SystemValue = val;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasDisp = false;
                ar.Boolean(ref hasDisp);
                if (hasDisp && ar.Remaining >= 2)
                {
                    string? disp = null;
                    ar.WChar(ref disp);
                    DisplayValue = disp;
                }
            }

            if (ar.Remaining >= 2)
            {
                string? src = null;
                ar.WChar(ref src);
                if (src != null) ValueSource = src;
            }

            if (ar.Remaining >= 4)
            {
                int relIdx = -1;
                ar.I32(ref relIdx);
                RelationIndex = relIdx;
            }

            if (ar.Remaining >= 1)
            {
                bool hasTextPos = false;
                ar.Boolean(ref hasTextPos);
                if (hasTextPos && ar.Remaining >= 24)
                {
                    double tx = 0, ty = 0, tz = 0;
                    ar.F64(ref tx); ar.F64(ref ty); ar.F64(ref tz);
                    TextX = tx; TextY = ty; TextZ = tz;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasDir = false;
                ar.Boolean(ref hasDir);
                if (hasDir && ar.Remaining >= 24)
                {
                    double dx = 0, dy = 0, dz = 0;
                    ar.F64(ref dx); ar.F64(ref dy); ar.F64(ref dz);
                    DirectionX = dx; DirectionY = dy; DirectionZ = dz;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasNorm = false;
                ar.Boolean(ref hasNorm);
                if (hasNorm && ar.Remaining >= 24)
                {
                    double nx = 0, ny = 0, nz = 0;
                    ar.F64(ref nx); ar.F64(ref ny); ar.F64(ref nz);
                    NormalX = nx; NormalY = ny; NormalZ = nz;
                }
            }

            if (ar.Remaining >= 1)
            {
                bool hasWitness = false;
                ar.Boolean(ref hasWitness);
                if (hasWitness && ar.Remaining >= 48)
                {
                    double w1x = 0, w1y = 0, w1z = 0;
                    double w2x = 0, w2y = 0, w2z = 0;
                    ar.F64(ref w1x); ar.F64(ref w1y); ar.F64(ref w1z);
                    ar.F64(ref w2x); ar.F64(ref w2y); ar.F64(ref w2z);
                    WitnessPoint1X = w1x; WitnessPoint1Y = w1y; WitnessPoint1Z = w1z;
                    WitnessPoint2X = w2x; WitnessPoint2Y = w2y; WitnessPoint2Z = w2z;
                }
            }
        }
    }
}

/// <summary>
/// Unified strongly-typed model for moProfileFeature_c / mo3DProfileFeature_c sketch features.
/// Implements <see cref="IMfcFeatureModel"/> to serialize sketch geometry, points, segments, relations, and dimensions.
/// </summary>
public sealed class SketchFeatureModel : IMfcFeatureModel
{
    public const string ClassName2D = "moProfileFeature_c";
    public const string ClassName3D = "mo3DProfileFeature_c";
    public const string ClassNameOrigin = "moOriginProfileFeature_c";
    public const string LegacyClassName = "sgSketch";

    public const string FeatureClassName = ClassName2D;
    public const uint DefaultTypeId = 0x49u; // Standard profile feature persistent TypeId

    public string MfcClassName { get; set; } = ClassName2D;
    public string ClassName => MfcClassName;
    public uint TypeId { get; set; } = DefaultTypeId;
    public string Name { get; set; } = "Sketch1";
    public int? Id { get; set; }
    public int ConfigurationIndex { get; set; }
    public bool? Is3D { get; set; }
    public string SourceStream { get; set; } = string.Empty;
    public int SourceOffset { get; set; }
    public int HeaderOffset { get => SourceOffset; set => SourceOffset = value; }

    private readonly List<string> _diagnostics = new();
    private readonly List<SketchPointModel> _points = new();
    private readonly List<SketchSegmentModel> _segments = new();
    private readonly List<SketchRelationModel> _relations = new();
    private readonly List<SketchDimensionModel> _dimensions = new();
    private readonly List<SketchDimensionModel> _parameters = new();

    public IReadOnlyList<string> Diagnostics
    {
        get => _diagnostics.AsReadOnly();
        set { _diagnostics.Clear(); if (value != null) _diagnostics.AddRange(value); }
    }
    public bool IsPartial => Diagnostics.Count != 0;

    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public byte[] GetRawData() => (byte[])RawData.Clone();

    public IReadOnlyList<SketchPointModel> Points
    {
        get => _points.AsReadOnly();
        set { _points.Clear(); if (value != null) _points.AddRange(value); }
    }

    public IReadOnlyList<SketchSegmentModel> Segments
    {
        get => _segments.AsReadOnly();
        set { _segments.Clear(); if (value != null) _segments.AddRange(value); }
    }

    public IReadOnlyList<SketchRelationModel> Relations
    {
        get => _relations.AsReadOnly();
        set { _relations.Clear(); if (value != null) _relations.AddRange(value); }
    }

    public IReadOnlyList<SketchDimensionModel> Dimensions
    {
        get => _dimensions.AsReadOnly();
        set { _dimensions.Clear(); if (value != null) _dimensions.AddRange(value); }
    }

    public IReadOnlyList<SketchDimensionModel> Parameters
    {
        get => _parameters.AsReadOnly();
        set { _parameters.Clear(); if (value != null) _parameters.AddRange(value); }
    }

    public SketchFeatureModel() { }

    public SketchFeatureModel(string name)
    {
        Name = name ?? string.Empty;
    }


    #region Entity Authoring Methods

    /// <summary>Adds a point to the sketch point table.</summary>
    public SketchPointModel AddPoint(double x, double y, double z = 0)
    {
        uint nextId = _points.Count > 0 ? _points.Max(p => p.Id) + 1 : 1;
        var pt = new SketchPointModel
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
    public SketchSegmentModel AddLine(SketchPointModel start, SketchPointModel end, bool isConstruction = false)
    {
        if (start == null) throw new ArgumentNullException(nameof(start));
        if (end == null) throw new ArgumentNullException(nameof(end));
        uint nextId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 1;
        var seg = new SketchSegmentModel
        {
            Id = nextId,
            Kind = SketchSegmentKind.Line,
            StartPoint = start,
            EndPoint = end,
            StartPointIndex = start.TableIndex,
            EndPointIndex = end.TableIndex,
            IsConstruction = isConstruction
        };
        _segments.Add(seg);
        return seg;
    }

    /// <summary>Adds a line segment with newly created endpoints.</summary>
    public SketchSegmentModel AddLine(double x1, double y1, double x2, double y2, bool isConstruction = false)
    {
        var p1 = AddPoint(x1, y1);
        var p2 = AddPoint(x2, y2);
        return AddLine(p1, p2, isConstruction);
    }

    /// <summary>Adds a circular arc with center, start and end points.</summary>
    public SketchSegmentModel AddArc(SketchPointModel center, SketchPointModel start, SketchPointModel end, int direction = 1, bool isConstruction = false)
    {
        if (center == null) throw new ArgumentNullException(nameof(center));
        if (start == null) throw new ArgumentNullException(nameof(start));
        if (end == null) throw new ArgumentNullException(nameof(end));

        double radius = Math.Sqrt((start.X - center.X) * (start.X - center.X) +
                                  (start.Y - center.Y) * (start.Y - center.Y) +
                                  (start.Z - center.Z) * (start.Z - center.Z));

        uint nextId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 1;
        var seg = new SketchSegmentModel
        {
            Id = nextId,
            Kind = SketchSegmentKind.Arc,
            CenterPoint = center,
            StartPoint = start,
            EndPoint = end,
            CenterPointIndex = center.TableIndex,
            StartPointIndex = start.TableIndex,
            EndPointIndex = end.TableIndex,
            Radius = radius,
            IsCircle = ReferenceEquals(start, end),
            Direction = direction,
            IsConstruction = isConstruction
        };
        _segments.Add(seg);
        return seg;
    }

    /// <summary>Adds a full circle given a center point and radius.</summary>
    public SketchSegmentModel AddCircle(SketchPointModel center, double radius, bool isConstruction = false)
    {
        if (center == null) throw new ArgumentNullException(nameof(center));
        var perimeterPoint = AddPoint(center.X + radius, center.Y, center.Z);
        uint nextId = _segments.Count > 0 ? _segments.Max(s => s.Id) + 1 : 1;
        var seg = new SketchSegmentModel
        {
            Id = nextId,
            Kind = SketchSegmentKind.Arc,
            CenterPoint = center,
            StartPoint = perimeterPoint,
            EndPoint = perimeterPoint,
            CenterPointIndex = center.TableIndex,
            StartPointIndex = perimeterPoint.TableIndex,
            EndPointIndex = perimeterPoint.TableIndex,
            Radius = radius,
            IsCircle = true,
            Direction = 1,
            IsConstruction = isConstruction
        };
        _segments.Add(seg);
        return seg;
    }

    /// <summary>Adds a full circle given center coordinates and radius.</summary>
    public SketchSegmentModel AddCircle(double centerX, double centerY, double radius, bool isConstruction = false)
    {
        var center = AddPoint(centerX, centerY);
        return AddCircle(center, radius, isConstruction);
    }

    #endregion

    #region Relation & Constraint Authoring Methods

    /// <summary>
    /// Adds a geometric relation/constraint between the specified entities.
    /// </summary>
    public SketchRelationModel AddRelation(SketchRelationKind kind, params object[] entities)
    {
        if (entities == null || entities.Length == 0)
            throw new ArgumentException("At least one entity is required for a relation.", nameof(entities));

        var refs = new List<SketchEntityReferenceModel>();
        foreach (var entity in entities)
        {
            if (entity is SketchPointModel pt)
            {
                refs.Add(new SketchEntityReferenceModel
                {
                    ClassReference = 0x80AD,
                    Index = (ushort)pt.TableIndex,
                    Target = pt,
                    OwnerSketch = this
                });
            }
            else if (entity is SketchSegmentModel seg)
            {
                int segIdx = _segments.IndexOf(seg);
                if (segIdx < 0) segIdx = _segments.Count;
                ushort classRef = (seg.Kind == SketchSegmentKind.Arc || seg.Kind == SketchSegmentKind.Ellipse) ? (ushort)0x80C1 : (ushort)0x80BD;
                refs.Add(new SketchEntityReferenceModel
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

        var rel = new SketchRelationModel
        {
            TypeCode = (int)kind,
            Layout = (kind == SketchRelationKind.Distance || kind == SketchRelationKind.Radius || kind == SketchRelationKind.Diameter) ? "Dimension" : "Common",
            References = refs,
            OwnerSketch = this
        };

        _relations.Add(rel);
        return rel;
    }

    public SketchRelationModel AddHorizontal(SketchSegmentModel line) => AddRelation(SketchRelationKind.Horizontal, line);
    public SketchRelationModel AddVertical(SketchSegmentModel line) => AddRelation(SketchRelationKind.Vertical, line);
    public SketchRelationModel AddParallel(SketchSegmentModel line1, SketchSegmentModel line2) => AddRelation(SketchRelationKind.Parallel, line1, line2);
    public SketchRelationModel AddPerpendicular(SketchSegmentModel line1, SketchSegmentModel line2) => AddRelation(SketchRelationKind.Perpendicular, line1, line2);
    public SketchRelationModel AddCoincident(object entity1, object entity2) => AddRelation(SketchRelationKind.Coincident, entity1, entity2);
    public SketchRelationModel AddConcentric(SketchSegmentModel arc1, SketchSegmentModel arc2) => AddRelation(SketchRelationKind.Concentric, arc1, arc2);
    public SketchRelationModel AddTangent(SketchSegmentModel arc, SketchSegmentModel line) => AddRelation(SketchRelationKind.Tangent, arc, line);
    public SketchRelationModel AddEqualLength(SketchSegmentModel line1, SketchSegmentModel line2) => AddRelation(SketchRelationKind.EqualLength, line1, line2);
    public SketchRelationModel AddFixed(object entity) => AddRelation(SketchRelationKind.Fixed, entity);

    public SketchDimensionModel AddDistanceDimension(string name, double distance)
    {
        var dim = new SketchDimensionModel
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

    public SketchDimensionModel AddDistanceDimension(SketchPointModel p1, SketchPointModel p2, string name, double distance)
    {
        var rel = AddRelation(SketchRelationKind.Distance, p1, p2);
        var dim = new SketchDimensionModel
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

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref refTag);

            ushort schema = 1;
            ar.U16(ref schema);

            string? name = Name;
            ar.WChar(ref name);

            bool hasId = Id.HasValue;
            ar.Boolean(ref hasId);
            if (hasId)
            {
                int id = Id!.Value;
                ar.I32(ref id);
            }

            uint typeId = TypeId;
            ar.U32(ref typeId);

            bool is3d = Is3D ?? false;
            ar.Boolean(ref is3d);

            // Points
            uint pCount = (uint)Points.Count;
            ar.Count(ref pCount);
            foreach (var pt in Points)
                pt.Serialize(ar);

            // Segments
            uint sCount = (uint)Segments.Count;
            ar.Count(ref sCount);
            foreach (var seg in Segments)
                seg.Serialize(ar);

            // Relations
            uint rCount = (uint)Relations.Count;
            ar.Count(ref rCount);
            foreach (var rel in Relations)
                rel.Serialize(ar);

            // Dimensions
            uint dCount = (uint)Dimensions.Count;
            ar.Count(ref dCount);
            foreach (var dim in Dimensions)
                dim.Serialize(ar);

            // Parameters
            uint paramCount = (uint)Parameters.Count;
            ar.Count(ref paramCount);
            foreach (var param in Parameters)
                param.Serialize(ar);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) Name = name!;

            if (ar.Remaining >= 1)
            {
                bool hasId = false;
                ar.Boolean(ref hasId);
                if (hasId && ar.Remaining >= 4)
                {
                    int id = 0;
                    ar.I32(ref id);
                    Id = id;
                }
            }

            if (ar.Remaining >= 4)
            {
                uint typeId = 0;
                ar.U32(ref typeId);
                TypeId = typeId;
            }

            if (ar.Remaining >= 1)
            {
                bool is3d = false;
                ar.Boolean(ref is3d);
                Is3D = is3d;
            }

            // Points
            if (ar.Remaining >= 2)
            {
                uint pCount = 0;
                ar.Count(ref pCount);
                _points.Clear();
                for (int i = 0; i < pCount && ar.Remaining > 0; i++)
                {
                    var pt = new SketchPointModel();
                    pt.Serialize(ar);
                    _points.Add(pt);
                }
            }

            // Segments
            if (ar.Remaining >= 2)
            {
                uint sCount = 0;
                ar.Count(ref sCount);
                _segments.Clear();
                for (int i = 0; i < sCount && ar.Remaining > 0; i++)
                {
                    var seg = new SketchSegmentModel();
                    seg.Serialize(ar);
                    _segments.Add(seg);
                }
            }

            // Relations
            if (ar.Remaining >= 2)
            {
                uint rCount = 0;
                ar.Count(ref rCount);
                _relations.Clear();
                for (int i = 0; i < rCount && ar.Remaining > 0; i++)
                {
                    var rel = new SketchRelationModel();
                    rel.Serialize(ar);
                    _relations.Add(rel);
                }
            }

            // Dimensions
            if (ar.Remaining >= 2)
            {
                uint dCount = 0;
                ar.Count(ref dCount);
                _dimensions.Clear();
                for (int i = 0; i < dCount && ar.Remaining > 0; i++)
                {
                    var dim = new SketchDimensionModel();
                    dim.Serialize(ar);
                    _dimensions.Add(dim);
                }
            }

            // Parameters
            if (ar.Remaining >= 2)
            {
                uint paramCount = 0;
                ar.Count(ref paramCount);
                _parameters.Clear();
                for (int i = 0; i < paramCount && ar.Remaining > 0; i++)
                {
                    var p = new SketchDimensionModel();
                    p.Serialize(ar);
                    _parameters.Add(p);
                }
            }

            BindGraph();
        }
    }

    /// <summary>
    /// Resolves point references on segments and relations using the loaded point table.
    /// </summary>
    public void BindGraph()
    {
        foreach (var seg in Segments)
        {
            if (seg.StartPointIndex >= 0 && seg.StartPointIndex < Points.Count)
                seg.StartPoint = Points[seg.StartPointIndex];
            if (seg.EndPointIndex >= 0 && seg.EndPointIndex < Points.Count)
                seg.EndPoint = Points[seg.EndPointIndex];
            if (seg.CenterPointIndex >= 0 && seg.CenterPointIndex < Points.Count)
                seg.CenterPoint = Points[seg.CenterPointIndex];
            if (seg.MajorPointIndex >= 0 && seg.MajorPointIndex < Points.Count)
                seg.MajorPoint = Points[seg.MajorPointIndex];
            if (seg.MinorPointIndex >= 0 && seg.MinorPointIndex < Points.Count)
                seg.MinorPoint = Points[seg.MinorPointIndex];
        }

        foreach (var rel in Relations)
        {
            rel.OwnerSketch = this;
            foreach (var rf in rel.References)
            {
                rf.OwnerSketch = this;
                if (rf.Index < Points.Count && (rf.ClassReference == 0x80AD || rel.Kind == SketchRelationKind.Distance))
                {
                    rf.Target = Points[rf.Index];
                }
                else if (rf.Index < Segments.Count)
                {
                    rf.Target = Segments[rf.Index];
                }
                else if (rf.Index < Points.Count)
                {
                    rf.Target = Points[rf.Index];
                }
            }
        }

        var distanceRelations = Relations.Where(r => r.Kind == SketchRelationKind.Distance || r.Kind == SketchRelationKind.Radius || r.Kind == SketchRelationKind.Diameter).ToList();
        for (int i = 0; i < Dimensions.Count; i++)
        {
            var dim = Dimensions[i];
            dim.OwnerSketch = this;
            if (i < distanceRelations.Count)
            {
                dim.Relation = distanceRelations[i];
            }
        }

        foreach (var p in Parameters)
        {
            p.OwnerSketch = this;
        }
    }

    /// <summary>
    /// Parses all sketch features from a ResolvedFeatures stream into typed IMfcSerializable SketchFeatureModel objects.
    /// </summary>
    public static List<SketchFeatureModel> ReadAllFromResolvedFeatures(byte[] bytes, byte[]? keywordsXml = null, string? streamPath = null, int config = 0, LengthUnit? lengthUnit = null)
    {
        return SketchBinaryReader.Read(bytes, keywordsXml, streamPath ?? string.Empty, config, lengthUnit).ToList();
    }
}
