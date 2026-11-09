using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFile;

public sealed class FlangeCustomBendAllowanceData
{
    public int? Type { get; set; }
    public double? KFactor { get; set; }
    public double? BendAllowance { get; set; }
    public double? BendDeduction { get; set; }
    public string? BendTableFile { get; set; }
    public string? BendCalculationTableFile { get; set; }
}

public sealed class EdgeFlangeParameterResolution
{
    public bool Edge { get; internal set; }
    public bool Edges { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool BendRadius { get; internal set; }
    public bool BendAngle { get; internal set; }
    public bool OffsetType { get; internal set; }
    public bool OffsetReference { get; internal set; }
    public bool ReverseOffset { get; internal set; }
    public bool OffsetDistance { get; internal set; }
    public bool OffsetDimType { get; internal set; }
    public bool PositionType { get; internal set; }
    public bool UsePositionTrimSideBends { get; internal set; }
    public bool UsePositionOffset { get; internal set; }
    public bool PositionOffsetType { get; internal set; }
    public bool PositionOffsetReference { get; internal set; }
    public bool PositionOffsetDistance { get; internal set; }
    public bool ReversePositionOffset { get; internal set; }
    public bool AutoReliefType { get; internal set; }
    public bool UseReliefRatio { get; internal set; }
    public bool ReliefRatio { get; internal set; }
    public bool ReliefWidth { get; internal set; }
    public bool ReliefDepth { get; internal set; }
    public bool ReliefTearType { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool UseDefaultBendRelief { get; internal set; }
    public bool GapDistance { get; internal set; }
    public bool AngleReference { get; internal set; }
    public bool LockAngle { get; internal set; }
    public bool PerpendicularToFace { get; internal set; }
    public bool NormalToFlangePlane { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool EdgeCount { get; internal set; }
    public bool PositionReferenceType { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moEdgeFlange_c (Sheet Metal Edge Flange).
/// Implements IMfcFeatureModel and IMfcSerializable with 100% symmetric reading and writing.
/// Aligned with SolidWorks IEdgeFlangeFeatureData.
/// </summary>
public sealed class EdgeFlangeFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moEdgeFlange_c";
    public const uint ObservedSerializedTypeId = 0x67;
    public const uint FreshSingleEdgeSerializedTypeId = 0x75;
    public const uint SwFeatureId = 37;

    public static readonly int[] BendAngleSlots = { 0x1785, 0x199F };
    public static readonly int[] BendRadiusSlots = { 0xEBD, 0xEED, 0xF05, 0xFD5, 0x10BF, 0x10D7 };
    public static readonly int[] PrefixOffsetDistanceSlots = { 0x4FF5, 0x5097, 0x5139, 0x51DB };
    public static readonly int[] OffsetDistanceSlots = { 0x4B78 };
    public const int PositionTypeSlot = 0x488D;
    public const int ReliefWidthSlot = 0x4875;
    public const int ReliefDepthSlot = 0x487D;
    public static readonly int[] ReliefDepthDisplaySlots = { 0x2467, 0x2A44 };
    public static readonly int[] LazyRadiusCaches = { 0x17B5, 0x17E5, 0x1842, 0x18AD, 0x18E5, 0x1B68, 0x1B78, 0x31B1 };

    internal static bool IsEdgeFlangeNode(MfcElement element)
    {
        if (element == null || element.ClassName != SerializedClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == ObservedSerializedTypeId ||
               header.TypeId == FreshSingleEdgeSerializedTypeId;
    }

    internal static IFeatureData? Parse(MfcElement element)
    {
        if (!IsEdgeFlangeNode(element))
            return null;
        return null;
    }

    internal static IFeatureData? Parse(MfcDocument doc, MfcElement element, int windowStart, int windowEnd) =>
        FlangeFeatureParsers.ParseEdgeFlange(doc, element, windowStart, windowEnd);


    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "边线-法兰1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? ObservedSerializedTypeId;
        set => SerializedTypeId = value;
    }

    public EdgeFlangeParameterResolution ParameterResolution { get; } = new();

    // Selections
    public EntityReference? Edge { get; set; }
    public IReadOnlyList<EntityReference>? Edges { get; set; }
    public int? EdgeCount { get; set; }

    // Dimensions & Angles
    public double? BendRadius { get; set; }
    public bool? UseDefaultBendRadius { get; set; }
    public double? BendAngle { get; set; }
    public double? OffsetDistance { get; set; }
    public int? OffsetType { get; set; }
    public EntityReference? OffsetReference { get; set; }
    public bool? ReverseOffset { get; set; }
    public int? OffsetDimType { get; set; }

    // Position & Relief
    public int? PositionType { get; set; }
    public bool? UsePositionTrimSideBends { get; set; }
    public bool? UsePositionOffset { get; set; }
    public int? PositionOffsetType { get; set; }
    public EntityReference? PositionOffsetReference { get; set; }
    public double? PositionOffsetDistance { get; set; }
    public bool? ReversePositionOffset { get; set; }
    public int? PositionReferenceType { get; set; }

    public int? AutoReliefType { get; set; }
    public bool? UseReliefRatio { get; set; }
    public double? ReliefRatio { get; set; }
    public double? ReliefWidth { get; set; }
    public double? ReliefDepth { get; set; }
    public int? ReliefTearType { get; set; }
    public bool? UseDefaultBendRelief { get; set; }
    public bool? UseDefaultBendAllowance { get; set; }
    public double? GapDistance { get; set; }

    public EntityReference? AngleReference { get; set; }
    public bool? LockAngle { get; set; }
    public bool? PerpendicularToFace { get; set; }
    public bool? NormalToFlangePlane { get; set; }
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    public byte[]? RawBlock { get; set; }

    public EdgeFlangeFeatureModel() { }

    public EdgeFlangeFeatureModel(double bendAngle, double bendRadius, double? offsetDistance = null)
    {
        BendAngle = bendAngle;
        BendRadius = bendRadius;
        OffsetDistance = offsetDistance;
        ParameterResolution.BendAngle = true;
        ParameterResolution.BendRadius = true;
        MarkResolved(nameof(BendAngle));
        MarkResolved(nameof(BendRadius));
        if (offsetDistance.HasValue)
        {
            ParameterResolution.OffsetDistance = true;
            MarkResolved(nameof(OffsetDistance));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (BendAngle.HasValue && block.Length > 0x199F + 8)
                    MfcParameterReader.PatchDoubleSlots(block, BendAngleSlots, BendAngle.Value);
                if (BendRadius.HasValue && block.Length > 0x10D7 + 8)
                    MfcParameterReader.PatchDoubleSlots(block, BendRadiusSlots, BendRadius.Value);
                if (OffsetDistance.HasValue && block.Length > 0x4B78 + 8)
                    MfcParameterReader.PatchDoubleSlots(block, OffsetDistanceSlots, OffsetDistance.Value);
                if (PositionType.HasValue && block.Length > PositionTypeSlot + 4)
                    BitConverter.GetBytes((uint)PositionType.Value).CopyTo(block, PositionTypeSlot);
                if (ReliefWidth.HasValue && block.Length > ReliefWidthSlot + 8)
                    BitConverter.GetBytes(ReliefWidth.Value).CopyTo(block, ReliefWidthSlot);
                if (ReliefDepth.HasValue && block.Length > ReliefDepthSlot + 8)
                    BitConverter.GetBytes(ReliefDepth.Value).CopyTo(block, ReliefDepthSlot);

                ar.WriteBytes(block);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            string? name = FeatureName ?? "边线-法兰1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : ObservedSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            var edgeList = Edges ?? (Edge != null ? new[] { Edge } : Array.Empty<EntityReference>());
            ushort count = (ushort)edgeList.Count;
            ar.U16(ref count);
            foreach (var edge in edgeList)
            {
                ushort edgeTag = 0;
                ar.ClassTagOrRef("moCompEdge_c", ref edgeTag);
                ushort edgeSchema = 0x8004; ar.U16(ref edgeSchema);
                uint edgeId = edge.EntityId; ar.U32(ref edgeId);
            }

            double angle = BendAngle ?? (Math.PI / 2.0); ar.F64(ref angle);
            double radius = BendRadius ?? 0.002; ar.F64(ref radius);
            bool defaultRad = UseDefaultBendRadius ?? false; ar.Boolean(ref defaultRad);
            double offsetDist = OffsetDistance ?? 0.01; ar.F64(ref offsetDist);
            int posType = PositionType ?? 1; ar.I32(ref posType);
            bool usePosOffset = UsePositionOffset ?? false; ar.Boolean(ref usePosOffset);
            double posOffsetDist = PositionOffsetDistance ?? 0.01; ar.F64(ref posOffsetDist);

            double relRatio = ReliefRatio ?? 0.5; ar.F64(ref relRatio);
            bool useRelRatio = UseReliefRatio ?? true; ar.Boolean(ref useRelRatio);
            double relWidth = ReliefWidth ?? 0.001; ar.F64(ref relWidth);
            double relDepth = ReliefDepth ?? 0.001; ar.F64(ref relDepth);
            bool defRelief = UseDefaultBendRelief ?? false; ar.Boolean(ref defRelief);
            double gap = GapDistance ?? 0.001; ar.F64(ref gap);

            bool lockAng = LockAngle ?? true; ar.Boolean(ref lockAng);
            bool normalFlange = NormalToFlangePlane ?? true; ar.Boolean(ref normalFlange);
            bool perpFace = PerpendicularToFace ?? false; ar.Boolean(ref perpFace);
            bool revOff = ReverseOffset ?? false; ar.Boolean(ref revOff);
            bool trimSide = UsePositionTrimSideBends ?? false; ar.Boolean(ref trimSide);
            bool defAllowance = UseDefaultBendAllowance ?? true; ar.Boolean(ref defAllowance);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;

                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    EdgeCount = count;
                    var edges = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort edgeTag = 0;
                        ar.ReadClassTag(out string edgeCls, out edgeTag, out _);
                        ushort edgeSchema = 0; ar.U16(ref edgeSchema);
                        uint edgeId = 0; ar.U32(ref edgeId);
                        edges.Add(new EntityReference { EntityId = edgeId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{edgeId}" });
                    }
                    Edges = edges.AsReadOnly();
                    if (edges.Count > 0) Edge = edges[0];
                    ParameterResolution.Edge = true;
                    ParameterResolution.Edges = true;
                    ParameterResolution.EdgeCount = true;
                    MarkResolved(nameof(Edge));
                    MarkResolved(nameof(Edges));
                    MarkResolved(nameof(EdgeCount));
                }

                if (ar.Remaining >= 8) { double ang = 0; ar.F64(ref ang); BendAngle = ang; ParameterResolution.BendAngle = true; MarkResolved(nameof(BendAngle)); }
                if (ar.Remaining >= 8) { double rad = 0; ar.F64(ref rad); BendRadius = rad; ParameterResolution.BendRadius = true; MarkResolved(nameof(BendRadius)); }
                if (ar.Remaining >= 1) { bool dr = false; ar.Boolean(ref dr); UseDefaultBendRadius = dr; ParameterResolution.UseDefaultBendRadius = true; MarkResolved(nameof(UseDefaultBendRadius)); }
                if (ar.Remaining >= 8) { double off = 0; ar.F64(ref off); OffsetDistance = off; ParameterResolution.OffsetDistance = true; MarkResolved(nameof(OffsetDistance)); }
                if (ar.Remaining >= 4) { int pt = 0; ar.I32(ref pt); PositionType = pt; ParameterResolution.PositionType = true; MarkResolved(nameof(PositionType)); }
                if (ar.Remaining >= 1) { bool upo = false; ar.Boolean(ref upo); UsePositionOffset = upo; ParameterResolution.UsePositionOffset = true; MarkResolved(nameof(UsePositionOffset)); }
                if (ar.Remaining >= 8) { double pod = 0; ar.F64(ref pod); PositionOffsetDistance = pod; ParameterResolution.PositionOffsetDistance = true; MarkResolved(nameof(PositionOffsetDistance)); }
                if (ar.Remaining >= 8) { double rr = 0; ar.F64(ref rr); ReliefRatio = rr; ParameterResolution.ReliefRatio = true; MarkResolved(nameof(ReliefRatio)); }
                if (ar.Remaining >= 1) { bool urr = false; ar.Boolean(ref urr); UseReliefRatio = urr; ParameterResolution.UseReliefRatio = true; MarkResolved(nameof(UseReliefRatio)); }
                if (ar.Remaining >= 8) { double rw = 0; ar.F64(ref rw); ReliefWidth = rw; ParameterResolution.ReliefWidth = true; MarkResolved(nameof(ReliefWidth)); }
                if (ar.Remaining >= 8) { double rd = 0; ar.F64(ref rd); ReliefDepth = rd; ParameterResolution.ReliefDepth = true; MarkResolved(nameof(ReliefDepth)); }
                if (ar.Remaining >= 1) { bool udbr = false; ar.Boolean(ref udbr); UseDefaultBendRelief = udbr; ParameterResolution.UseDefaultBendRelief = true; MarkResolved(nameof(UseDefaultBendRelief)); }
                if (ar.Remaining >= 8) { double gd = 0; ar.F64(ref gd); GapDistance = gd; ParameterResolution.GapDistance = true; MarkResolved(nameof(GapDistance)); }
                if (ar.Remaining >= 1) { bool la = false; ar.Boolean(ref la); LockAngle = la; ParameterResolution.LockAngle = true; MarkResolved(nameof(LockAngle)); }
                if (ar.Remaining >= 1) { bool nfp = false; ar.Boolean(ref nfp); NormalToFlangePlane = nfp; ParameterResolution.NormalToFlangePlane = true; MarkResolved(nameof(NormalToFlangePlane)); }
                if (ar.Remaining >= 1) { bool pf = false; ar.Boolean(ref pf); PerpendicularToFace = pf; ParameterResolution.PerpendicularToFace = true; MarkResolved(nameof(PerpendicularToFace)); }
                if (ar.Remaining >= 1) { bool ro = false; ar.Boolean(ref ro); ReverseOffset = ro; ParameterResolution.ReverseOffset = true; MarkResolved(nameof(ReverseOffset)); }
                if (ar.Remaining >= 1) { bool tsb = false; ar.Boolean(ref tsb); UsePositionTrimSideBends = tsb; ParameterResolution.UsePositionTrimSideBends = true; MarkResolved(nameof(UsePositionTrimSideBends)); }
                if (ar.Remaining >= 1) { bool dba = false; ar.Boolean(ref dba); UseDefaultBendAllowance = dba; ParameterResolution.UseDefaultBendAllowance = true; MarkResolved(nameof(UseDefaultBendAllowance)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class MiterFlangeParameterResolution
{
    public bool Edges { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool BendRadius { get; internal set; }
    public bool PositionType { get; internal set; }
    public bool UsePositionTrimSideBends { get; internal set; }
    public bool GapDistance { get; internal set; }
    public bool StartOffset { get; internal set; }
    public bool EndOffset { get; internal set; }
    public bool ReliefType { get; internal set; }
    public bool UseReliefRatio { get; internal set; }
    public bool ReliefRatio { get; internal set; }
    public bool ReliefWidth { get; internal set; }
    public bool ReliefDepth { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool UseDefaultBendRelief { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool EdgeCount { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moSMMiteredFlange_c (Sheet Metal Miter Flange).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IMiterFlangeFeatureData.
/// </summary>
public sealed class MiterFlangeFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moMiterFlange_c";
    public const string ModernMfcClassName = "moSMMiteredFlange_c";
    public const string LegacyClassName = "moMiterFlange_c";
    public const uint ObservedSerializedTypeId = 0x68;
    public const uint SwFeatureId = 104;

    internal static IFeatureData? Parse(MfcElement element)
    {
        if (element == null || (element.ClassName != SerializedClassName && element.ClassName != ModernMfcClassName))
            return null;
        return null;
    }

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "斜接-法兰1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public MiterFlangeParameterResolution ParameterResolution { get; } = new();

    public IReadOnlyList<EntityReference>? Edges { get; set; }
    public int? EdgeCount { get; set; }
    public bool? UseDefaultBendRadius { get; set; }
    public double? BendRadius { get; set; }
    public int? PositionType { get; set; }
    public bool? UsePositionTrimSideBends { get; set; }
    public double? GapDistance { get; set; }
    public double? StartOffset { get; set; }
    public double? EndOffset { get; set; }
    public int? ReliefType { get; set; }
    public bool? UseReliefRatio { get; set; }
    public double? ReliefRatio { get; set; }
    public double? ReliefWidth { get; set; }
    public double? ReliefDepth { get; set; }
    public bool? UseDefaultBendAllowance { get; set; }
    public bool? UseDefaultBendRelief { get; set; }
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    public byte[]? RawBlock { get; set; }

    public MiterFlangeFeatureModel() { }

    public MiterFlangeFeatureModel(double bendRadius, double gapDistance = 0.001, double startOffset = 0.0, double endOffset = 0.0)
    {
        BendRadius = bendRadius;
        GapDistance = gapDistance;
        StartOffset = startOffset;
        EndOffset = endOffset;
        ParameterResolution.BendRadius = true;
        ParameterResolution.GapDistance = true;
        ParameterResolution.StartOffset = true;
        ParameterResolution.EndOffset = true;
        MarkResolved(nameof(BendRadius));
        MarkResolved(nameof(GapDistance));
        MarkResolved(nameof(StartOffset));
        MarkResolved(nameof(EndOffset));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "斜接-法兰1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : ObservedSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            var edgeList = Edges ?? Array.Empty<EntityReference>();
            ushort count = (ushort)edgeList.Count;
            ar.U16(ref count);
            foreach (var edge in edgeList)
            {
                ushort edgeTag = 0;
                ar.ClassTagOrRef("moCompEdge_c", ref edgeTag);
                ushort edgeSchema = 0x8004; ar.U16(ref edgeSchema);
                uint edgeId = edge.EntityId; ar.U32(ref edgeId);
            }

            double rad = BendRadius ?? 0.002; ar.F64(ref rad);
            bool defRad = UseDefaultBendRadius ?? true; ar.Boolean(ref defRad);
            int pos = PositionType ?? 1; ar.I32(ref pos);
            bool trim = UsePositionTrimSideBends ?? false; ar.Boolean(ref trim);
            double gap = GapDistance ?? 0.001; ar.F64(ref gap);
            double startOff = StartOffset ?? 0.0; ar.F64(ref startOff);
            double endOff = EndOffset ?? 0.0; ar.F64(ref endOff);
            int relType = ReliefType ?? 1; ar.I32(ref relType);
            double relWidth = ReliefWidth ?? 0.001; ar.F64(ref relWidth);
            double relDepth = ReliefDepth ?? 0.001; ar.F64(ref relDepth);
            double relRatio = ReliefRatio ?? 0.5; ar.F64(ref relRatio);
            bool useRelRatio = UseReliefRatio ?? true; ar.Boolean(ref useRelRatio);
            bool defRelief = UseDefaultBendRelief ?? true; ar.Boolean(ref defRelief);
            bool defAllowance = UseDefaultBendAllowance ?? true; ar.Boolean(ref defAllowance);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName || cls == ModernMfcClassName || cls == LegacyClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    EdgeCount = count;
                    var list = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort edgeTag = 0; ar.ReadClassTag(out string edgeCls, out edgeTag, out _);
                        ushort edgeSchema = 0; ar.U16(ref edgeSchema);
                        uint edgeId = 0; ar.U32(ref edgeId);
                        list.Add(new EntityReference { EntityId = edgeId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{edgeId}" });
                    }
                    Edges = list.AsReadOnly();
                    ParameterResolution.Edges = true;
                    ParameterResolution.EdgeCount = true;
                    MarkResolved(nameof(Edges));
                    MarkResolved(nameof(EdgeCount));
                }

                if (ar.Remaining >= 8) { double rad = 0; ar.F64(ref rad); BendRadius = rad; ParameterResolution.BendRadius = true; MarkResolved(nameof(BendRadius)); }
                if (ar.Remaining >= 1) { bool dr = false; ar.Boolean(ref dr); UseDefaultBendRadius = dr; ParameterResolution.UseDefaultBendRadius = true; MarkResolved(nameof(UseDefaultBendRadius)); }
                if (ar.Remaining >= 4) { int pos = 0; ar.I32(ref pos); PositionType = pos; ParameterResolution.PositionType = true; MarkResolved(nameof(PositionType)); }
                if (ar.Remaining >= 1) { bool trim = false; ar.Boolean(ref trim); UsePositionTrimSideBends = trim; ParameterResolution.UsePositionTrimSideBends = true; MarkResolved(nameof(UsePositionTrimSideBends)); }
                if (ar.Remaining >= 8) { double gap = 0; ar.F64(ref gap); GapDistance = gap; ParameterResolution.GapDistance = true; MarkResolved(nameof(GapDistance)); }
                if (ar.Remaining >= 8) { double so = 0; ar.F64(ref so); StartOffset = so; ParameterResolution.StartOffset = true; MarkResolved(nameof(StartOffset)); }
                if (ar.Remaining >= 8) { double eo = 0; ar.F64(ref eo); EndOffset = eo; ParameterResolution.EndOffset = true; MarkResolved(nameof(EndOffset)); }
                if (ar.Remaining >= 4) { int rt = 0; ar.I32(ref rt); ReliefType = rt; ParameterResolution.ReliefType = true; MarkResolved(nameof(ReliefType)); }
                if (ar.Remaining >= 8) { double rw = 0; ar.F64(ref rw); ReliefWidth = rw; ParameterResolution.ReliefWidth = true; MarkResolved(nameof(ReliefWidth)); }
                if (ar.Remaining >= 8) { double rd = 0; ar.F64(ref rd); ReliefDepth = rd; ParameterResolution.ReliefDepth = true; MarkResolved(nameof(ReliefDepth)); }
                if (ar.Remaining >= 8) { double rr = 0; ar.F64(ref rr); ReliefRatio = rr; ParameterResolution.ReliefRatio = true; MarkResolved(nameof(ReliefRatio)); }
                if (ar.Remaining >= 1) { bool urr = false; ar.Boolean(ref urr); UseReliefRatio = urr; ParameterResolution.UseReliefRatio = true; MarkResolved(nameof(UseReliefRatio)); }
                if (ar.Remaining >= 1) { bool dbr = false; ar.Boolean(ref dbr); UseDefaultBendRelief = dbr; ParameterResolution.UseDefaultBendRelief = true; MarkResolved(nameof(UseDefaultBendRelief)); }
                if (ar.Remaining >= 1) { bool dba = false; ar.Boolean(ref dba); UseDefaultBendAllowance = dba; ParameterResolution.UseDefaultBendAllowance = true; MarkResolved(nameof(UseDefaultBendAllowance)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class HemParameterResolution
{
    public bool Angle { get; internal set; }
    public bool BendPosition { get; internal set; }
    public bool Edges { get; internal set; }
    public bool GapDistance { get; internal set; }
    public bool Length { get; internal set; }
    public bool MiterGap { get; internal set; }
    public bool Radius { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool Type { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool EdgeCount { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moHem_c (Sheet Metal Hem).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IHemFeatureData.
/// </summary>
public sealed class HemFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moHem_c";
    public const uint ObservedSerializedTypeId = 0x67;
    public const uint SwFeatureId = 103;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "褶边1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? ObservedSerializedTypeId;
        set => SerializedTypeId = value;
    }

    public HemParameterResolution ParameterResolution { get; } = new();

    public int? Type { get; set; }
    public int? BendPosition { get; set; }
    public IReadOnlyList<EntityReference>? Edges { get; set; }
    public int? EdgeCount { get; set; }
    public bool? ReverseDirection { get; set; }
    public double? Length { get; set; }
    public double? GapDistance { get; set; }
    public double? Angle { get; set; }
    public double? Radius { get; set; }
    public double? MiterGap { get; set; }
    public bool? UseDefaultBendAllowance { get; set; }
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    public byte[]? RawBlock { get; set; }

    public HemFeatureModel() { }

    public HemFeatureModel(int type, double length, double gapDistance)
    {
        Type = type;
        Length = length;
        GapDistance = gapDistance;
        ParameterResolution.Type = true;
        ParameterResolution.Length = true;
        ParameterResolution.GapDistance = true;
        MarkResolved(nameof(Type));
        MarkResolved(nameof(Length));
        MarkResolved(nameof(GapDistance));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "褶边1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : ObservedSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            ushort count = (ushort)(Edges?.Count ?? 0);
            ar.U16(ref count);
            if (Edges != null)
            {
                foreach (var edge in Edges)
                {
                    ushort edgeTag = 0; ar.ClassTagOrRef("moCompEdge_c", ref edgeTag);
                    ushort edgeSchema = 0x8004; ar.U16(ref edgeSchema);
                    uint edgeId = edge.EntityId; ar.U32(ref edgeId);
                }
            }

            int t = Type ?? 0; ar.I32(ref t);
            int bp = BendPosition ?? 1; ar.I32(ref bp);
            double len = Length ?? 0.005; ar.F64(ref len);
            double rad = Radius ?? 0.001; ar.F64(ref rad);
            double gap = GapDistance ?? 0.0005; ar.F64(ref gap);
            double ang = Angle ?? Math.PI; ar.F64(ref ang);
            double mg = MiterGap ?? 0.0005; ar.F64(ref mg);
            bool rev = ReverseDirection ?? false; ar.Boolean(ref rev);
            bool defAllow = UseDefaultBendAllowance ?? true; ar.Boolean(ref defAllow);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    EdgeCount = count;
                    var list = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort edgeTag = 0; ar.ReadClassTag(out string edgeCls, out edgeTag, out _);
                        ushort edgeSchema = 0; ar.U16(ref edgeSchema);
                        uint edgeId = 0; ar.U32(ref edgeId);
                        list.Add(new EntityReference { EntityId = edgeId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{edgeId}" });
                    }
                    Edges = list.AsReadOnly();
                    ParameterResolution.Edges = true;
                    ParameterResolution.EdgeCount = true;
                    MarkResolved(nameof(Edges));
                    MarkResolved(nameof(EdgeCount));
                }

                if (ar.Remaining >= 4) { int t = 0; ar.I32(ref t); Type = t; ParameterResolution.Type = true; MarkResolved(nameof(Type)); }
                if (ar.Remaining >= 4) { int bp = 0; ar.I32(ref bp); BendPosition = bp; ParameterResolution.BendPosition = true; MarkResolved(nameof(BendPosition)); }
                if (ar.Remaining >= 8) { double len = 0; ar.F64(ref len); Length = len; ParameterResolution.Length = true; MarkResolved(nameof(Length)); }
                if (ar.Remaining >= 8) { double rad = 0; ar.F64(ref rad); Radius = rad; ParameterResolution.Radius = true; MarkResolved(nameof(Radius)); }
                if (ar.Remaining >= 8) { double gap = 0; ar.F64(ref gap); GapDistance = gap; ParameterResolution.GapDistance = true; MarkResolved(nameof(GapDistance)); }
                if (ar.Remaining >= 8) { double ang = 0; ar.F64(ref ang); Angle = ang; ParameterResolution.Angle = true; MarkResolved(nameof(Angle)); }
                if (ar.Remaining >= 8) { double mg = 0; ar.F64(ref mg); MiterGap = mg; ParameterResolution.MiterGap = true; MarkResolved(nameof(MiterGap)); }
                if (ar.Remaining >= 1) { bool rev = false; ar.Boolean(ref rev); ReverseDirection = rev; ParameterResolution.ReverseDirection = true; MarkResolved(nameof(ReverseDirection)); }
                if (ar.Remaining >= 1) { bool dba = false; ar.Boolean(ref dba); UseDefaultBendAllowance = dba; ParameterResolution.UseDefaultBendAllowance = true; MarkResolved(nameof(UseDefaultBendAllowance)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class JogParameterResolution
{
    public bool BendRadius { get; internal set; }
    public bool DimensionPositionType { get; internal set; }
    public bool FixedFace { get; internal set; }
    public bool FixedPoint { get; internal set; }
    public bool FixProjectedLength { get; internal set; }
    public bool JogAngle { get; internal set; }
    public bool JogPositionType { get; internal set; }
    public bool OffsetDistance { get; internal set; }
    public bool OffsetReference { get; internal set; }
    public bool OffsetType { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool ReverseOffset { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool OffsetReferenceType { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moJog_c (Sheet Metal Jog).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IJogFeatureData.
/// </summary>
public sealed class JogFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moJog_c";
    public const uint ObservedSerializedTypeId = 0x6B;
    public const uint TutorialSpecimenTypeId = 0x159;
    public const uint SwFeatureId = 107;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "转折1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public JogParameterResolution ParameterResolution { get; } = new();

    public double? BendRadius { get; set; }
    public int? DimensionPositionType { get; set; }
    public EntityReference? FixedFace { get; set; }
    public double[]? FixedPoint { get; set; }
    public bool? FixProjectedLength { get; set; }
    public double? JogAngle { get; set; }
    public int? JogPositionType { get; set; }
    public double? OffsetDistance { get; set; }
    public EntityReference? OffsetReference { get; set; }
    public int? OffsetType { get; set; }
    public bool? ReverseDirection { get; set; }
    public bool? ReverseOffset { get; set; }
    public bool? UseDefaultBendAllowance { get; set; }
    public bool? UseDefaultBendRadius { get; set; }
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }
    public int? OffsetReferenceType { get; set; }

    public byte[]? RawBlock { get; set; }

    public JogFeatureModel() { }

    public JogFeatureModel(double offsetDistance, double jogAngle = Math.PI / 2.0, double bendRadius = 0.002, EntityReference? fixedFace = null)
    {
        OffsetDistance = offsetDistance;
        JogAngle = jogAngle;
        BendRadius = bendRadius;
        FixedFace = fixedFace;
        ParameterResolution.OffsetDistance = true;
        ParameterResolution.JogAngle = true;
        ParameterResolution.BendRadius = true;
        MarkResolved(nameof(OffsetDistance));
        MarkResolved(nameof(JogAngle));
        MarkResolved(nameof(BendRadius));
        if (fixedFace != null)
        {
            ParameterResolution.FixedFace = true;
            MarkResolved(nameof(FixedFace));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "转折1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : ObservedSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            bool hasFace = FixedFace != null;
            ar.Boolean(ref hasFace);
            if (hasFace)
            {
                ushort faceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref faceTag);
                ushort faceSchema = 0x8004; ar.U16(ref faceSchema);
                uint faceId = FixedFace!.EntityId; ar.U32(ref faceId);
            }

            double rad = BendRadius ?? 0.002; ar.F64(ref rad);
            double ang = JogAngle ?? (Math.PI / 2.0); ar.F64(ref ang);
            double off = OffsetDistance ?? 0.01; ar.F64(ref off);
            int dimPos = DimensionPositionType ?? 1; ar.I32(ref dimPos);
            int jogPos = JogPositionType ?? 1; ar.I32(ref jogPos);
            int offType = OffsetType ?? 1; ar.I32(ref offType);
            bool revDir = ReverseDirection ?? false; ar.Boolean(ref revDir);
            bool revOff = ReverseOffset ?? false; ar.Boolean(ref revOff);
            bool fixProj = FixProjectedLength ?? false; ar.Boolean(ref fixProj);
            bool defAllow = UseDefaultBendAllowance ?? true; ar.Boolean(ref defAllow);
            bool defRad = UseDefaultBendRadius ?? true; ar.Boolean(ref defRad);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                bool hasFace = false; ar.Boolean(ref hasFace);
                if (hasFace && ar.Remaining >= 6)
                {
                    ushort faceTag = 0; ar.ReadClassTag(out string faceCls, out faceTag, out _);
                    ushort faceSchema = 0; ar.U16(ref faceSchema);
                    uint faceId = 0; ar.U32(ref faceId);
                    FixedFace = new EntityReference { EntityId = faceId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{faceId}" };
                    ParameterResolution.FixedFace = true;
                    MarkResolved(nameof(FixedFace));
                }

                if (ar.Remaining >= 8) { double rad = 0; ar.F64(ref rad); BendRadius = rad; ParameterResolution.BendRadius = true; MarkResolved(nameof(BendRadius)); }
                if (ar.Remaining >= 8) { double ang = 0; ar.F64(ref ang); JogAngle = ang; ParameterResolution.JogAngle = true; MarkResolved(nameof(JogAngle)); }
                if (ar.Remaining >= 8) { double off = 0; ar.F64(ref off); OffsetDistance = off; ParameterResolution.OffsetDistance = true; MarkResolved(nameof(OffsetDistance)); }
                if (ar.Remaining >= 4) { int dp = 0; ar.I32(ref dp); DimensionPositionType = dp; ParameterResolution.DimensionPositionType = true; MarkResolved(nameof(DimensionPositionType)); }
                if (ar.Remaining >= 4) { int jp = 0; ar.I32(ref jp); JogPositionType = jp; ParameterResolution.JogPositionType = true; MarkResolved(nameof(JogPositionType)); }
                if (ar.Remaining >= 4) { int ot = 0; ar.I32(ref ot); OffsetType = ot; ParameterResolution.OffsetType = true; MarkResolved(nameof(OffsetType)); }
                if (ar.Remaining >= 1) { bool rd = false; ar.Boolean(ref rd); ReverseDirection = rd; ParameterResolution.ReverseDirection = true; MarkResolved(nameof(ReverseDirection)); }
                if (ar.Remaining >= 1) { bool ro = false; ar.Boolean(ref ro); ReverseOffset = ro; ParameterResolution.ReverseOffset = true; MarkResolved(nameof(ReverseOffset)); }
                if (ar.Remaining >= 1) { bool fp = false; ar.Boolean(ref fp); FixProjectedLength = fp; ParameterResolution.FixProjectedLength = true; MarkResolved(nameof(FixProjectedLength)); }
                if (ar.Remaining >= 1) { bool da = false; ar.Boolean(ref da); UseDefaultBendAllowance = da; ParameterResolution.UseDefaultBendAllowance = true; MarkResolved(nameof(UseDefaultBendAllowance)); }
                if (ar.Remaining >= 1) { bool dr = false; ar.Boolean(ref dr); UseDefaultBendRadius = dr; ParameterResolution.UseDefaultBendRadius = true; MarkResolved(nameof(UseDefaultBendRadius)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class LoftedBendParameterResolution
{
    public bool BendLineControlOption { get; internal set; }
    public bool Direction { get; internal set; }
    public bool FacetingOption { get; internal set; }
    public bool FacetValue { get; internal set; }
    public bool FormedMethod { get; internal set; }
    public bool MaximumDeviation { get; internal set; }
    public bool NumberOfBendLines { get; internal set; }
    public bool Profiles { get; internal set; }
    public bool ReferToEndPoint { get; internal set; }
    public bool SymmetricThickness { get; internal set; }
    public bool Thickness { get; internal set; }
    public bool ProfileCount { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moLoftedBend_c (Sheet Metal Lofted Bend).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with ILoftedBendsFeatureData.
/// </summary>
public sealed class LoftedBendFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moLoftedBend_c";
    public const uint ObservedSerializedTypeId = 0x72;
    public const uint SwFeatureId = 114;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "放样折弯1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? ObservedSerializedTypeId;
        set => SerializedTypeId = value;
    }

    public LoftedBendParameterResolution ParameterResolution { get; } = new();

    public int? BendLineControlOption { get; set; }
    public bool? Direction { get; set; }
    public int? FacetingOption { get; set; }
    public double? FacetValue { get; set; }
    public bool? FormedMethod { get; set; }
    public double? MaximumDeviation { get; set; }
    public int? NumberOfBendLines { get; set; }
    public IReadOnlyList<EntityReference>? Profiles { get; set; }
    public bool? ReferToEndPoint { get; set; }
    public bool? SymmetricThickness { get; set; }
    public double? Thickness { get; set; }
    public int? ProfileCount { get; set; }

    public byte[]? RawBlock { get; set; }

    public LoftedBendFeatureModel() { }

    public LoftedBendFeatureModel(double thickness, int numberOfBendLines = 2, IEnumerable<EntityReference>? profiles = null)
    {
        Thickness = thickness;
        NumberOfBendLines = numberOfBendLines;
        if (profiles != null)
        {
            var list = new List<EntityReference>(profiles);
            Profiles = list.AsReadOnly();
            ProfileCount = list.Count;
            ParameterResolution.Profiles = true;
            ParameterResolution.ProfileCount = true;
            MarkResolved(nameof(Profiles));
            MarkResolved(nameof(ProfileCount));
        }
        ParameterResolution.Thickness = true;
        ParameterResolution.NumberOfBendLines = true;
        MarkResolved(nameof(Thickness));
        MarkResolved(nameof(NumberOfBendLines));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "放样折弯1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : ObservedSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            ushort count = (ushort)(Profiles?.Count ?? 0);
            ar.U16(ref count);
            if (Profiles != null)
            {
                foreach (var prof in Profiles)
                {
                    ushort profTag = 0; ar.ClassTagOrRef("moSketchChain_c", ref profTag);
                    ushort profSchema = 0x8004; ar.U16(ref profSchema);
                    uint entId = prof.EntityId; ar.U32(ref entId);
                }
            }

            double th = Thickness ?? 0.001; ar.F64(ref th);
            int bl = NumberOfBendLines ?? 2; ar.I32(ref bl);
            bool formed = FormedMethod ?? false; ar.Boolean(ref formed);
            int ctrl = BendLineControlOption ?? 0; ar.I32(ref ctrl);
            bool dir = Direction ?? false; ar.Boolean(ref dir);
            int facetOpt = FacetingOption ?? 0; ar.I32(ref facetOpt);
            double facetVal = FacetValue ?? 0.001; ar.F64(ref facetVal);
            double maxDev = MaximumDeviation ?? 0.0005; ar.F64(ref maxDev);
            bool endPoint = ReferToEndPoint ?? false; ar.Boolean(ref endPoint);
            bool symThick = SymmetricThickness ?? false; ar.Boolean(ref symThick);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    ProfileCount = count;
                    var list = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort profTag = 0; ar.ReadClassTag(out string profCls, out profTag, out _);
                        ushort profSchema = 0; ar.U16(ref profSchema);
                        uint entId = 0; ar.U32(ref entId);
                        list.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.SketchContour, Name = $"moSketchChain_c:{entId}" });
                    }
                    Profiles = list.AsReadOnly();
                    ParameterResolution.Profiles = true;
                    ParameterResolution.ProfileCount = true;
                    MarkResolved(nameof(Profiles));
                    MarkResolved(nameof(ProfileCount));
                }

                if (ar.Remaining >= 8) { double th = 0; ar.F64(ref th); Thickness = th; ParameterResolution.Thickness = true; MarkResolved(nameof(Thickness)); }
                if (ar.Remaining >= 4) { int bl = 0; ar.I32(ref bl); NumberOfBendLines = bl; ParameterResolution.NumberOfBendLines = true; MarkResolved(nameof(NumberOfBendLines)); }
                if (ar.Remaining >= 1) { bool f = false; ar.Boolean(ref f); FormedMethod = f; ParameterResolution.FormedMethod = true; MarkResolved(nameof(FormedMethod)); }
                if (ar.Remaining >= 4) { int c = 0; ar.I32(ref c); BendLineControlOption = c; ParameterResolution.BendLineControlOption = true; MarkResolved(nameof(BendLineControlOption)); }
                if (ar.Remaining >= 1) { bool d = false; ar.Boolean(ref d); Direction = d; ParameterResolution.Direction = true; MarkResolved(nameof(Direction)); }
                if (ar.Remaining >= 4) { int fo = 0; ar.I32(ref fo); FacetingOption = fo; ParameterResolution.FacetingOption = true; MarkResolved(nameof(FacetingOption)); }
                if (ar.Remaining >= 8) { double fv = 0; ar.F64(ref fv); FacetValue = fv; ParameterResolution.FacetValue = true; MarkResolved(nameof(FacetValue)); }
                if (ar.Remaining >= 8) { double md = 0; ar.F64(ref md); MaximumDeviation = md; ParameterResolution.MaximumDeviation = true; MarkResolved(nameof(MaximumDeviation)); }
                if (ar.Remaining >= 1) { bool ep = false; ar.Boolean(ref ep); ReferToEndPoint = ep; ParameterResolution.ReferToEndPoint = true; MarkResolved(nameof(ReferToEndPoint)); }
                if (ar.Remaining >= 1) { bool st = false; ar.Boolean(ref st); SymmetricThickness = st; ParameterResolution.SymmetricThickness = true; MarkResolved(nameof(SymmetricThickness)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class NormalCutParameterResolution
{
    public bool AutoPropagation { get; internal set; }
    public bool CutDirection { get; internal set; }
    public bool LayerAdjustmentValue { get; internal set; }
    public bool LinkToKFactor { get; internal set; }
    public bool NormalCutParameters { get; internal set; }
    public bool OffsetPlaneReference { get; internal set; }
    public bool OptimizeGeometry { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moSMNormalCut_c (Sheet Metal Normal Cut).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with ISMNormalCutFeatureData.
/// </summary>
public sealed class NormalCutFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moSMNormalCut_c";
    public const uint ObservedSerializedTypeId = 0x73;
    public const uint AlternateSerializedTypeId = 0x87;
    public const uint SwFeatureId = 115;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "法向切除1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public NormalCutParameterResolution ParameterResolution { get; } = new();

    public bool? AutoPropagation { get; set; }
    public DirectionReference? CutDirection { get; set; }
    public double? LayerAdjustmentValue { get; set; }
    public bool? LinkToKFactor { get; set; }
    public int? NormalCutParameters { get; set; }
    public EntityReference? OffsetPlaneReference { get; set; }
    public bool? OptimizeGeometry { get; set; }

    public byte[]? RawBlock { get; set; }

    public NormalCutFeatureModel() { }

    public NormalCutFeatureModel(bool autoPropagation = true, bool optimizeGeometry = true, double layerAdjustmentValue = 0.0)
    {
        AutoPropagation = autoPropagation;
        OptimizeGeometry = optimizeGeometry;
        LayerAdjustmentValue = layerAdjustmentValue;
        ParameterResolution.AutoPropagation = true;
        ParameterResolution.OptimizeGeometry = true;
        ParameterResolution.LayerAdjustmentValue = true;
        MarkResolved(nameof(AutoPropagation));
        MarkResolved(nameof(OptimizeGeometry));
        MarkResolved(nameof(LayerAdjustmentValue));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "法向切除1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : ObservedSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            bool autoProp = AutoPropagation ?? true; ar.Boolean(ref autoProp);
            bool optGeom = OptimizeGeometry ?? true; ar.Boolean(ref optGeom);
            bool linkK = LinkToKFactor ?? false; ar.Boolean(ref linkK);
            double layerAdj = LayerAdjustmentValue ?? 0.0; ar.F64(ref layerAdj);
            int cutParams = NormalCutParameters ?? 0; ar.I32(ref cutParams);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 1) { bool ap = false; ar.Boolean(ref ap); AutoPropagation = ap; ParameterResolution.AutoPropagation = true; MarkResolved(nameof(AutoPropagation)); }
                if (ar.Remaining >= 1) { bool og = false; ar.Boolean(ref og); OptimizeGeometry = og; ParameterResolution.OptimizeGeometry = true; MarkResolved(nameof(OptimizeGeometry)); }
                if (ar.Remaining >= 1) { bool lk = false; ar.Boolean(ref lk); LinkToKFactor = lk; ParameterResolution.LinkToKFactor = true; MarkResolved(nameof(LinkToKFactor)); }
                if (ar.Remaining >= 8) { double la = 0; ar.F64(ref la); LayerAdjustmentValue = la; ParameterResolution.LayerAdjustmentValue = true; MarkResolved(nameof(LayerAdjustmentValue)); }
                if (ar.Remaining >= 4) { int cp = 0; ar.I32(ref cp); NormalCutParameters = cp; ParameterResolution.NormalCutParameters = true; MarkResolved(nameof(NormalCutParameters)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class FlatPatternParameterResolution
{
    public bool BreakCornerRadius { get; internal set; }
    public bool BreakCornerType { get; internal set; }
    public bool CornerTreatment { get; internal set; }
    public bool CornerTrimRatioToThickness { get; internal set; }
    public bool CornerTrimReliefDistance { get; internal set; }
    public bool CornerTrimReliefType { get; internal set; }
    public bool ExcludedFaces { get; internal set; }
    public bool FixedFace { get; internal set; }
    public bool FixedFace2 { get; internal set; }
    public bool MergeFace { get; internal set; }
    public bool ShowSlitInCornerRelief { get; internal set; }
    public bool SimplifyBends { get; internal set; }
    public bool UseRatioToThickness { get; internal set; }
    public bool AddCornerTrim { get; internal set; }
    public bool BreakCorners { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moFlatPattern_c (Sheet Metal Flat Pattern).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IFlatPatternFeatureData.
/// </summary>
public sealed class FlatPatternFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moFlatPattern_c";
    public const uint LegacySerializedTypeId = 0x5D;
    public const uint CurrentSerializedTypeId = 0x5E;
    public const uint Specimen066TypeId = 0x66;
    public const uint SwFeatureId = 38;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "平板型式1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public FlatPatternParameterResolution ParameterResolution { get; } = new();

    public double? BreakCornerRadius { get; set; }
    public int? BreakCornerType { get; set; }
    public bool? CornerTreatment { get; set; }
    public double? CornerTrimRatioToThickness { get; set; }
    public double? CornerTrimReliefDistance { get; set; }
    public int? CornerTrimReliefType { get; set; }
    public IReadOnlyList<EntityReference>? ExcludedFaces { get; set; }
    public EntityReference? FixedFace { get; set; }
    public EntityReference? FixedFace2 { get; set; }
    public bool? MergeFace { get; set; }
    public bool? ShowSlitInCornerRelief { get; set; }
    public bool? SimplifyBends { get; set; }
    public bool? UseRatioToThickness { get; set; }
    public bool? AddCornerTrim { get; set; }
    public bool? BreakCorners { get; set; }

    public byte[]? RawBlock { get; set; }

    public FlatPatternFeatureModel() { }

    public FlatPatternFeatureModel(EntityReference? fixedFace = null, bool cornerTreatment = true, bool mergeFace = true, bool simplifyBends = false)
    {
        FixedFace = fixedFace;
        CornerTreatment = cornerTreatment;
        MergeFace = mergeFace;
        SimplifyBends = simplifyBends;
        ParameterResolution.CornerTreatment = true;
        ParameterResolution.MergeFace = true;
        ParameterResolution.SimplifyBends = true;
        MarkResolved(nameof(CornerTreatment));
        MarkResolved(nameof(MergeFace));
        MarkResolved(nameof(SimplifyBends));
        if (fixedFace != null)
        {
            ParameterResolution.FixedFace = true;
            MarkResolved(nameof(FixedFace));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "平板型式1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : CurrentSerializedTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            bool hasFace = FixedFace != null;
            ar.Boolean(ref hasFace);
            if (hasFace)
            {
                ushort faceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref faceTag);
                ushort faceSchema = 0x8004; ar.U16(ref faceSchema);
                uint faceId = FixedFace!.EntityId; ar.U32(ref faceId);
            }

            bool cornerTreatment = CornerTreatment ?? true; ar.Boolean(ref cornerTreatment);
            bool merge = MergeFace ?? true; ar.Boolean(ref merge);
            bool simplify = SimplifyBends ?? false; ar.Boolean(ref simplify);
            bool breakCorners = BreakCorners ?? false; ar.Boolean(ref breakCorners);
            int breakType = BreakCornerType ?? 0; ar.I32(ref breakType);
            double breakRad = BreakCornerRadius ?? 0.001; ar.F64(ref breakRad);
            bool addTrim = AddCornerTrim ?? false; ar.Boolean(ref addTrim);
            int trimReliefType = CornerTrimReliefType ?? 0; ar.I32(ref trimReliefType);
            double trimDist = CornerTrimReliefDistance ?? 0.001; ar.F64(ref trimDist);
            double trimRatio = CornerTrimRatioToThickness ?? 0.5; ar.F64(ref trimRatio);
            bool useRatio = UseRatioToThickness ?? false; ar.Boolean(ref useRatio);
            bool showSlit = ShowSlitInCornerRelief ?? false; ar.Boolean(ref showSlit);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                bool hasFace = false; ar.Boolean(ref hasFace);
                if (hasFace && ar.Remaining >= 6)
                {
                    ushort faceTag = 0; ar.ReadClassTag(out string faceCls, out faceTag, out _);
                    ushort faceSchema = 0; ar.U16(ref faceSchema);
                    uint faceId = 0; ar.U32(ref faceId);
                    FixedFace = new EntityReference { EntityId = faceId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{faceId}" };
                    ParameterResolution.FixedFace = true;
                    MarkResolved(nameof(FixedFace));
                }

                if (ar.Remaining >= 1) { bool ct = false; ar.Boolean(ref ct); CornerTreatment = ct; ParameterResolution.CornerTreatment = true; MarkResolved(nameof(CornerTreatment)); }
                if (ar.Remaining >= 1) { bool mf = false; ar.Boolean(ref mf); MergeFace = mf; ParameterResolution.MergeFace = true; MarkResolved(nameof(MergeFace)); }
                if (ar.Remaining >= 1) { bool sb = false; ar.Boolean(ref sb); SimplifyBends = sb; ParameterResolution.SimplifyBends = true; MarkResolved(nameof(SimplifyBends)); }
                if (ar.Remaining >= 1) { bool bc = false; ar.Boolean(ref bc); BreakCorners = bc; ParameterResolution.BreakCorners = true; MarkResolved(nameof(BreakCorners)); }
                if (ar.Remaining >= 4) { int bct = 0; ar.I32(ref bct); BreakCornerType = bct; ParameterResolution.BreakCornerType = true; MarkResolved(nameof(BreakCornerType)); }
                if (ar.Remaining >= 8) { double bcr = 0; ar.F64(ref bcr); BreakCornerRadius = bcr; ParameterResolution.BreakCornerRadius = true; MarkResolved(nameof(BreakCornerRadius)); }
                if (ar.Remaining >= 1) { bool at = false; ar.Boolean(ref at); AddCornerTrim = at; ParameterResolution.AddCornerTrim = true; MarkResolved(nameof(AddCornerTrim)); }
                if (ar.Remaining >= 4) { int ctr = 0; ar.I32(ref ctr); CornerTrimReliefType = ctr; ParameterResolution.CornerTrimReliefType = true; MarkResolved(nameof(CornerTrimReliefType)); }
                if (ar.Remaining >= 8) { double ctd = 0; ar.F64(ref ctd); CornerTrimReliefDistance = ctd; ParameterResolution.CornerTrimReliefDistance = true; MarkResolved(nameof(CornerTrimReliefDistance)); }
                if (ar.Remaining >= 8) { double ctr = 0; ar.F64(ref ctr); CornerTrimRatioToThickness = ctr; ParameterResolution.CornerTrimRatioToThickness = true; MarkResolved(nameof(CornerTrimRatioToThickness)); }
                if (ar.Remaining >= 1) { bool urt = false; ar.Boolean(ref urt); UseRatioToThickness = urt; ParameterResolution.UseRatioToThickness = true; MarkResolved(nameof(UseRatioToThickness)); }
                if (ar.Remaining >= 1) { bool ssc = false; ar.Boolean(ref ssc); ShowSlitInCornerRelief = ssc; ParameterResolution.ShowSlitInCornerRelief = true; MarkResolved(nameof(ShowSlitInCornerRelief)); }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

public sealed class FoldsParameterResolution
{
    public bool Bends { get; internal set; }
    public bool FixedFace { get; internal set; }
    public bool BendsCount { get; internal set; }
}

/// <summary>
/// Strongly-typed MFC serialization model for moFold_c (Sheet Metal Fold).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IFoldsFeatureData.
/// </summary>
public sealed class FoldFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moFold_c";
    public const uint SerializedTypeIdConstant = 0x5C;
    public const uint SwFeatureId = 92;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "折叠1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public FoldsParameterResolution ParameterResolution { get; } = new();

    public IReadOnlyList<EntityReference>? Bends { get; set; }
    public EntityReference? FixedFace { get; set; }
    public int? BendsCount { get; set; }

    public byte[]? RawBlock { get; set; }

    public FoldFeatureModel() { }

    public FoldFeatureModel(EntityReference? fixedFace = null, IEnumerable<EntityReference>? bends = null)
    {
        FixedFace = fixedFace;
        if (fixedFace != null)
        {
            ParameterResolution.FixedFace = true;
            MarkResolved(nameof(FixedFace));
        }
        if (bends != null)
        {
            var list = new List<EntityReference>(bends);
            Bends = list.AsReadOnly();
            BendsCount = list.Count;
            ParameterResolution.Bends = true;
            ParameterResolution.BendsCount = true;
            MarkResolved(nameof(Bends));
            MarkResolved(nameof(BendsCount));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "折叠1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : SerializedTypeIdConstant; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            bool hasFace = FixedFace != null;
            ar.Boolean(ref hasFace);
            if (hasFace)
            {
                ushort faceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref faceTag);
                ushort faceSchema = 0x8004; ar.U16(ref faceSchema);
                uint faceId = FixedFace!.EntityId; ar.U32(ref faceId);
            }

            ushort count = (ushort)(Bends?.Count ?? (BendsCount ?? 0));
            ar.U16(ref count);
            if (Bends != null)
            {
                foreach (var bend in Bends)
                {
                    ushort bendTag = 0; ar.ClassTagOrRef("moCompEdge_c", ref bendTag);
                    ushort bendSchema = 0x8004; ar.U16(ref bendSchema);
                    uint bendId = bend.EntityId; ar.U32(ref bendId);
                }
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                bool hasFace = false; ar.Boolean(ref hasFace);
                if (hasFace && ar.Remaining >= 6)
                {
                    ushort faceTag = 0; ar.ReadClassTag(out string faceCls, out faceTag, out _);
                    ushort faceSchema = 0; ar.U16(ref faceSchema);
                    uint faceId = 0; ar.U32(ref faceId);
                    FixedFace = new EntityReference { EntityId = faceId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{faceId}" };
                    ParameterResolution.FixedFace = true;
                    MarkResolved(nameof(FixedFace));
                }

                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    BendsCount = count;
                    var list = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort bendTag = 0; ar.ReadClassTag(out string bendCls, out bendTag, out _);
                        ushort bendSchema = 0; ar.U16(ref bendSchema);
                        uint bendId = 0; ar.U32(ref bendId);
                        list.Add(new EntityReference { EntityId = bendId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{bendId}" });
                    }
                    Bends = list.AsReadOnly();
                    ParameterResolution.Bends = true;
                    ParameterResolution.BendsCount = true;
                    MarkResolved(nameof(Bends));
                    MarkResolved(nameof(BendsCount));
                }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}

/// <summary>
/// Strongly-typed MFC serialization model for moUnFold_c (Sheet Metal UnFold).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IFoldsFeatureData.
/// </summary>
public sealed class UnFoldFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string SerializedClassName = "moUnFold_c";
    public const uint SerializedTypeIdConstant = 0x5C;
    public const uint SwFeatureId = 92;

    public string ClassName => SerializedClassName;
    public string FeatureName { get; set; } = "展开1";
    public double Version { get; set; } = 2.0;

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public FoldsParameterResolution ParameterResolution { get; } = new();

    public IReadOnlyList<EntityReference>? Bends { get; set; }
    public EntityReference? FixedFace { get; set; }
    public int? BendsCount { get; set; }

    public byte[]? RawBlock { get; set; }

    public UnFoldFeatureModel() { }

    public UnFoldFeatureModel(EntityReference? fixedFace = null, IEnumerable<EntityReference>? bends = null)
    {
        FixedFace = fixedFace;
        if (fixedFace != null)
        {
            ParameterResolution.FixedFace = true;
            MarkResolved(nameof(FixedFace));
        }
        if (bends != null)
        {
            var list = new List<EntityReference>(bends);
            Bends = list.AsReadOnly();
            BendsCount = list.Count;
            ParameterResolution.Bends = true;
            ParameterResolution.BendsCount = true;
            MarkResolved(nameof(Bends));
            MarkResolved(nameof(BendsCount));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                ar.WriteBytes(RawBlock);
                return;
            }

            ushort refTag = 0;
            ar.ClassTagOrRef(SerializedClassName, ref refTag);
            ushort schema = 0x8004; ar.U16(ref schema);
            string? name = FeatureName ?? "展开1"; ar.WChar(ref name);
            double ver = Version > 0 ? Version : 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : SerializedTypeIdConstant; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? empty = string.Empty; ar.WChar(ref empty);

            bool hasFace = FixedFace != null;
            ar.Boolean(ref hasFace);
            if (hasFace)
            {
                ushort faceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref faceTag);
                ushort faceSchema = 0x8004; ar.U16(ref faceSchema);
                uint faceId = FixedFace!.EntityId; ar.U32(ref faceId);
            }

            ushort count = (ushort)(Bends?.Count ?? (BendsCount ?? 0));
            ar.U16(ref count);
            if (Bends != null)
            {
                foreach (var bend in Bends)
                {
                    ushort bendTag = 0; ar.ClassTagOrRef("moCompEdge_c", ref bendTag);
                    ushort bendSchema = 0x8004; ar.U16(ref bendSchema);
                    uint bendId = bend.EntityId; ar.U32(ref bendId);
                }
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == SerializedClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name)) FeatureName = name!;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                bool hasFace = false; ar.Boolean(ref hasFace);
                if (hasFace && ar.Remaining >= 6)
                {
                    ushort faceTag = 0; ar.ReadClassTag(out string faceCls, out faceTag, out _);
                    ushort faceSchema = 0; ar.U16(ref faceSchema);
                    uint faceId = 0; ar.U32(ref faceId);
                    FixedFace = new EntityReference { EntityId = faceId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{faceId}" };
                    ParameterResolution.FixedFace = true;
                    MarkResolved(nameof(FixedFace));
                }

                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    BendsCount = count;
                    var list = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort bendTag = 0; ar.ReadClassTag(out string bendCls, out bendTag, out _);
                        ushort bendSchema = 0; ar.U16(ref bendSchema);
                        uint bendId = 0; ar.U32(ref bendId);
                        list.Add(new EntityReference { EntityId = bendId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{bendId}" });
                    }
                    Bends = list.AsReadOnly();
                    ParameterResolution.Bends = true;
                    ParameterResolution.BendsCount = true;
                    MarkResolved(nameof(Bends));
                    MarkResolved(nameof(BendsCount));
                }
            }
            else
            {
                ar.Position = startPos;
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}
// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/EdgeFlangeAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured edge-flange subsystem for the controlled single-edge specimen
/// (FlangeOptions/edge_flange_90_len10.SLDPRT, ResolvedFeatures version 0x74).
/// <para>
/// The block starts at the edge-selection wrapper <c>sgExtEnt_c</c> (0x525A) and
/// runs to the end of the stream. It contains the moCompEdge_c/moEdgeRef_c
/// selection block, moAbsoluteRefPlnData_c, moEdgeFlange_c, moOneBend_c, the
/// angular/radius/scaler dimension records, moMultiEdgeFlangeDataPerEdge_c,
/// moLinearEdgeGeom_c, the flat-pattern template, per-body sheet metal data,
/// the flat pattern and fold objects. Prefix (sketch + base flange) + this suffix
/// reconstructs the native stream byte-for-byte. The block is gzip/base64 encoded
/// only to keep the source readable; its SHA-256 is verified before use. The
/// slots below were measured from the six FlangeOptions variants and their
/// resaves; derived geometry caches remain lazy and are expected to be
/// recomputed by the native rebuild.
/// </para>
/// </summary>
internal static class EdgeFlangeAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x525A;
    internal const string MeasuredPrefixSha256 =
        "3BCBEFE3F2AEA3E455D9EF1BC9761CBB9038852CF04A5A4F4E2F379A73DA0C02";
    internal const string MeasuredSuffixSha256 =
        "65FD9D42650ACDDC3185FD813330D3C6489724647843F5FFF11A04522F3F26EB";

    /// <summary>Full-sample ResolvedFeatures folder count at stream offset 4.</summary>
    internal const ushort MeasuredFullFolderCount = 24;

    /// <summary>Controlled sketch child-count marker (67 00 00 00 + u32) in the prefix.</summary>
    internal const int SketchChildCountMarkerOffset = 0x236F;
    internal const uint MeasuredSketchChildCount = 22;

    /// <summary>Measured values before any authoring patch.</summary>
    internal const double MeasuredBendAngle = Math.PI / 2.0;
    internal const double MeasuredBendRadius = 0.002;
    internal const double MeasuredOffsetDistance = 0.01;
    internal const double MeasuredReliefWidth = 0.001;
    internal const double MeasuredReliefDepth = 0.001;
    internal const uint MeasuredPositionType = 1;

    /// <summary>
    /// Proven authoring slots (relative to the suffix start). The angle slot is
    /// the named D4 value plus its observed copy; the radius slots are the
    /// EdgeFlange D1 value plus its immediate feature/display copies. Every slot
    /// is checked against the measured value before it is patched.
    /// </summary>
    internal static readonly int[] BendAngleSlots = { 0x1785, 0x199F };
    internal static readonly int[] BendRadiusSlots = { 0xEBD, 0xEED, 0xF05, 0xFD5, 0x10BF, 0x10D7 };

    /// <summary>
    /// OffsetDistance value-slots. The first four are display copies in the
    /// hash-pinned prefix (absolute prefix offsets, measured 0.01; the length-20
    /// native sample changes each to 0.02). The last is the named D7 value in the
    /// suffix (absolute 0x9DD2, measured 0.01; native length-20 = 0.02).
    /// Geometry/extent caches that also move with the length stay lazy.
    /// </summary>
    internal static readonly int[] PrefixOffsetDistanceSlots = { 0x4FF5, 0x5097, 0x5139, 0x51DB };
    internal static readonly int[] OffsetDistanceSlots = { 0x4B78 };

    /// <summary>
    /// PositionType (u32) in the anonymous relief/position block; measured 1,
    /// native pos_outer = 2 and pos_bend_outside = 3 at this slot.
    /// </summary>
    internal const int PositionTypeSlot = 0x488D;

    /// <summary>
    /// ReliefWidth/ReliefDepth doubles in the anonymous relief block (absolute
    /// 0x9ACF/0x9AD7). D8/D9 are display copies of the relief depth (absolute
    /// 0x76C1/0x7C9E, measured 0.001; relief_custom = 0.0025). The width has no
    /// named display copy.
    /// </summary>
    internal const int ReliefWidthSlot = 0x4875;
    internal const int ReliefDepthSlot = 0x487D;
    internal static readonly int[] ReliefDepthDisplaySlots = { 0x2467, 0x2A44 };

    /// <summary>
    /// Radius/angle-derived copies and bbox caches that are intentionally left at
    /// the measured values (lazy). Native rebuild is expected to recompute them.
    /// </summary>
    internal static readonly int[] LazyRadiusCaches =
        { 0x17B5, 0x17E5, 0x1842, 0x18AD, 0x18E5, 0x1B68, 0x1B78, 0x31B1 };

    private static readonly byte[] SuffixGzipBytes = SheetMetalAuthoringSegments.EdgeFlange0;

    private static readonly Lazy<byte[]> Suffix = new(Decompress, isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress()
    {
        byte[] gzip = SuffixGzipBytes;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, MeasuredSuffixSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured edge-flange suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/EdgeFlangeFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Writable edge-flange combinations, based on the measured single-edge specimen
/// (FlangeOptions/edge_flange_90_len10.SLDPRT, version 0x74) and the five
/// single-variable variants.
/// <para>
/// The complete native edge-flange subsystem (selection block, moEdgeFlange_c,
/// moOneBend_c, dimensions, per-edge/linear-edge data and the downstream sheet
/// metal objects) is stored as a hash-pinned measured suffix. Authoring is
/// restricted to the exact stripped prefix (SHA-256), one edge, BendAngle/
/// BendRadius, and the verified OffsetDistance, PositionType (1/2/3) and relief
/// width/depth combinations. Every other field must be null (unresolved/use
/// template) or equal the measured native default from create4.json/create12.json;
/// anything else is rejected before the stream is touched. Derived geometry and
/// display caches are preserved as lazy values and must be recomputed by the
/// native rebuild.
/// </para>
/// </summary>
internal static class EdgeFlangeFeatureBuilder
{
    internal const string DefaultFeatureName = "EdgeFlange";
    internal const ushort SupportedStreamVersion = 0x74;

    internal static void ValidateDefinition(EdgeFlangeFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.BendAngle.HasValue ||
            double.IsNaN(def.BendAngle.Value) || double.IsInfinity(def.BendAngle.Value) ||
            def.BendAngle.Value <= 0.0 || def.BendAngle.Value > Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(def.BendAngle),
                $"Edge flange bend angle must be a finite value in (0, pi] radians, but received " +
                $"{def.BendAngle?.ToString() ?? "null"}.");
        }

        if (!def.BendRadius.HasValue ||
            double.IsNaN(def.BendRadius.Value) || double.IsInfinity(def.BendRadius.Value) ||
            def.BendRadius.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.BendRadius),
                $"Edge flange bend radius must be a positive finite number, but received " +
                $"{def.BendRadius?.ToString() ?? "null"}.");
        }

        // Verified variant fields: OffsetDistance from the length-20 sample,
        // PositionType 1/2/3 from the position samples, and relief width/depth
        // from relief_custom. Relief width and depth were only varied together.
        ValidatePositiveOptional(def.OffsetDistance, nameof(def.OffsetDistance));
        if (def.PositionType.HasValue &&
            (def.PositionType.Value < 1 || def.PositionType.Value > 3))
        {
            throw new ArgumentOutOfRangeException(nameof(def.PositionType),
                $"Edge flange position type must be 1 (MaterialInside), 2 (MaterialOutside) " +
                $"or 3 (BendOutside), but received {def.PositionType.Value}.");
        }
        if (def.ReliefWidth.HasValue != def.ReliefDepth.HasValue)
        {
            throw new NotSupportedException(
                "Edge flange authoring supports relief width/depth only as the verified pair " +
                "from edge_flange_relief_custom; setting only one of them is not verified. " +
                "No stream changes have been made.");
        }
        ValidatePositiveOptional(def.ReliefWidth, nameof(def.ReliefWidth));
        ValidatePositiveOptional(def.ReliefDepth, nameof(def.ReliefDepth));

        RequireNullOrEquals(def.EdgeCount, 1, nameof(def.EdgeCount));

        // Persistent selections are not authorable in this combination.
        RequireNull(def.Edge, nameof(def.Edge));
        RequireNull(def.Edges, nameof(def.Edges));
        RequireNull(def.OffsetReference, nameof(def.OffsetReference));
        RequireNull(def.PositionOffsetReference, nameof(def.PositionOffsetReference));
        RequireNull(def.AngleReference, nameof(def.AngleReference));
        RequireNull(def.CustomBendAllowance, nameof(def.CustomBendAllowance));

        // Measured native defaults from create4.json targetDefinition.
        RequireNullOrEquals(def.UseDefaultBendRadius, false, nameof(def.UseDefaultBendRadius));
        RequireNullOrEquals(def.OffsetType, 1, nameof(def.OffsetType));
        RequireNullOrEquals(def.PositionOffsetType, 1, nameof(def.PositionOffsetType));

        if (def.UsePositionOffset == true)
        {
            // Measured position-offset templates: PositionType stays 1, the
            // distance is exactly the two native samples 0.005/0.010, and the
            // fixed offset length remains 0.010.
            RequireNullOrEquals(def.PositionType, 1, nameof(def.PositionType));
            if (!def.PositionOffsetDistance.HasValue)
            {
                throw new NotSupportedException(
                    "Edge flange position-offset authoring requires PositionOffsetDistance=0.005 or 0.010. " +
                    "No stream changes have been made.");
            }
            if (!EdgeFlangeOffsetAuthoringSegments.IsSupportedDistance(def.PositionOffsetDistance.Value))
            {
                throw new NotSupportedException(
                    $"Edge flange position-offset authoring supports only PositionOffsetDistance " +
                    $"0.005 or 0.010, but received {def.PositionOffsetDistance.Value:R}. " +
                    "No stream changes have been made.");
            }
        }
        else
        {
            RequireNullOrEquals(def.UsePositionOffset, false, nameof(def.UsePositionOffset));
            RequireNullOrEquals(def.PositionOffsetDistance, 0.01, nameof(def.PositionOffsetDistance));
        }

        RequireNullOrEquals(def.UseDefaultBendAllowance, true, nameof(def.UseDefaultBendAllowance));
        RequireNullOrEquals(def.UseDefaultBendRelief, false, nameof(def.UseDefaultBendRelief));
        RequireNullOrEquals(def.AutoReliefType, 1, nameof(def.AutoReliefType));
        RequireNullOrEquals(def.ReliefTearType, 1, nameof(def.ReliefTearType));
        RequireNullOrEquals(def.ReliefRatio, 0.5, nameof(def.ReliefRatio));
        RequireNullOrEquals(def.UseReliefRatio, true, nameof(def.UseReliefRatio));
        RequireNullOrEquals(def.GapDistance, 0.001, nameof(def.GapDistance));
        RequireNullOrEquals(def.LockAngle, true, nameof(def.LockAngle));
        RequireNullOrEquals(def.NormalToFlangePlane, true, nameof(def.NormalToFlangePlane));
        RequireNullOrEquals(def.PerpendicularToFace, false, nameof(def.PerpendicularToFace));
        RequireNullOrEquals(def.ReverseOffset, false, nameof(def.ReverseOffset));
        RequireNullOrEquals(def.UsePositionTrimSideBends, false, nameof(def.UsePositionTrimSideBends));

        // Fields not present in the native snapshot stay unresolved: only null is accepted.
        RequireNull(def.OffsetDimType, nameof(def.OffsetDimType));
        RequireNull(def.ReversePositionOffset, nameof(def.ReversePositionOffset));
    }

    internal static byte[] Build(
        byte[] currentStream, EdgeFlangeFeatureModel def, string featureName)
    {
        ValidateDefinition(def);

        if (def.UsePositionOffset == true)
            return BuildPositionOffset(currentStream, def, featureName);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Edge flange authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; changing the name would change the measured block layout. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Edge flange authoring is only supported for ResolvedFeatures streams version " +
                $"0x{SupportedStreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }

        if (currentStream.Length != EdgeFlangeAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Edge flange authoring requires the stripped sketch+base-flange prefix of the controlled " +
                $"specimen ({EdgeFlangeAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream.Length} bytes. No stream changes have been made.");
        }

        string prefixHash = Sha256Hex(currentStream);
        if (!string.Equals(prefixHash, EdgeFlangeAuthoringSegments.MeasuredPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Edge flange authoring requires the controlled base/edge specimen: prefix SHA-256 " +
                $"{prefixHash.Substring(0, 12)} does not match the measured signature. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moEdgeFlange_c") != 0)
        {
            throw new NotSupportedException(
                "Edge flange authoring requires a stripped baseline without an existing edge flange. " +
                "No stream changes have been made.");
        }

        byte[] suffix = EdgeFlangeAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        int suffixStart = currentStream.Length;
        foreach (int slot in EdgeFlangeAuthoringSegments.BendAngleSlots)
        {
            PatchDouble(newStream, suffixStart + slot,
                EdgeFlangeAuthoringSegments.MeasuredBendAngle, def.BendAngle!.Value, "bend angle");
        }
        foreach (int slot in EdgeFlangeAuthoringSegments.BendRadiusSlots)
        {
            PatchDouble(newStream, suffixStart + slot,
                EdgeFlangeAuthoringSegments.MeasuredBendRadius, def.BendRadius!.Value, "bend radius");
        }

        if (def.OffsetDistance.HasValue)
        {
            foreach (int slot in EdgeFlangeAuthoringSegments.PrefixOffsetDistanceSlots)
            {
                PatchDouble(newStream, slot,
                    EdgeFlangeAuthoringSegments.MeasuredOffsetDistance,
                    def.OffsetDistance.Value, "offset distance");
            }
            foreach (int slot in EdgeFlangeAuthoringSegments.OffsetDistanceSlots)
            {
                PatchDouble(newStream, suffixStart + slot,
                    EdgeFlangeAuthoringSegments.MeasuredOffsetDistance,
                    def.OffsetDistance.Value, "offset distance");
            }
        }

        if (def.PositionType.HasValue)
        {
            PatchUInt32(newStream, suffixStart + EdgeFlangeAuthoringSegments.PositionTypeSlot,
                EdgeFlangeAuthoringSegments.MeasuredPositionType,
                checked((uint)def.PositionType.Value), "position type");
        }

        if (def.ReliefWidth.HasValue && def.ReliefDepth.HasValue)
        {
            PatchDouble(newStream, suffixStart + EdgeFlangeAuthoringSegments.ReliefWidthSlot,
                EdgeFlangeAuthoringSegments.MeasuredReliefWidth,
                def.ReliefWidth.Value, "relief width");
            PatchDouble(newStream, suffixStart + EdgeFlangeAuthoringSegments.ReliefDepthSlot,
                EdgeFlangeAuthoringSegments.MeasuredReliefDepth,
                def.ReliefDepth.Value, "relief depth");
            foreach (int slot in EdgeFlangeAuthoringSegments.ReliefDepthDisplaySlots)
            {
                PatchDouble(newStream, suffixStart + slot,
                    EdgeFlangeAuthoringSegments.MeasuredReliefDepth,
                    def.ReliefDepth.Value, "relief depth display");
            }
        }

        // Restore the measured folder count and sketch child count. The stripped
        // baseline may keep or reset them; the authored stream must match the
        // native full sample because prefix + measured suffix reconstructs it.
        BitConverter.GetBytes(EdgeFlangeAuthoringSegments.MeasuredFullFolderCount).CopyTo(newStream, 4);
        BitConverter.GetBytes(EdgeFlangeAuthoringSegments.MeasuredSketchChildCount)
            .CopyTo(newStream, EdgeFlangeAuthoringSegments.SketchChildCountMarkerOffset + 4);

        return newStream;
    }

    private static byte[] BuildPositionOffset(
        byte[] currentStream, EdgeFlangeFeatureModel def, string featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Edge flange authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; changing the name would change the measured block layout. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != EdgeFlangeOffsetAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring is only supported for ResolvedFeatures streams " +
                $"version 0x{EdgeFlangeOffsetAuthoringSegments.SupportedStreamVersion:X2}, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");
        }

        if (currentStream.Length != EdgeFlangeOffsetAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring requires the stripped base prefix of the controlled " +
                $"position-offset specimen ({EdgeFlangeOffsetAuthoringSegments.MeasuredPrefixLength} bytes), " +
                $"but received {currentStream.Length} bytes. No stream changes have been made.");
        }

        double distance = def.PositionOffsetDistance!.Value;
        string prefixHash = Sha256Hex(currentStream);
        string expectedPrefix = EdgeFlangeOffsetAuthoringSegments.GetPrefixSha256(distance);
        if (!string.Equals(prefixHash, expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring for PositionOffsetDistance={distance:R} requires " +
                $"the matching controlled stripped base specimen: prefix SHA-256 " +
                $"{prefixHash.Substring(0, 12)} does not match {expectedPrefix.Substring(0, 12)}. " +
                "No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) !=
            EdgeFlangeOffsetAuthoringSegments.MeasuredFullFolderCount)
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring requires the measured folder count " +
                $"{EdgeFlangeOffsetAuthoringSegments.MeasuredFullFolderCount}. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moEdgeFlange_c") != 0)
        {
            throw new NotSupportedException(
                "Edge flange authoring requires a stripped baseline without an existing edge flange. " +
                "No stream changes have been made.");
        }

        byte[] suffix = EdgeFlangeOffsetAuthoringSegments.GetSuffix(distance);
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        return newStream;
    }

    private static void PatchDouble(byte[] data, int offset, double expected, double value, string slotName)
    {
        if (offset < 0 || offset + 8 > data.Length)
            throw new InvalidOperationException($"Edge-flange slot {slotName} at 0x{offset:X} is outside the stream.");
        double current = BitConverter.ToDouble(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Cannot author an edge flange: measured {slotName} slot at 0x{offset:X} contains " +
                $"{current:R}, expected {expected:R}. The target is not the controlled specimen. " +
                "No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void PatchUInt32(byte[] data, int offset, uint expected, uint value, string slotName)
    {
        if (offset < 0 || offset + 4 > data.Length)
            throw new InvalidOperationException($"Edge-flange slot {slotName} at 0x{offset:X} is outside the stream.");
        uint current = BitConverter.ToUInt32(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Cannot author an edge flange: measured {slotName} slot at 0x{offset:X} contains " +
                $"{current}, expected {expected}. The target is not the controlled specimen. " +
                "No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void ValidatePositiveOptional(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(property,
                $"Edge flange {property} must be a positive finite number, but received " +
                $"{value.Value:R}.");
        }
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Edge flange authoring does not yet support {property}={value.Value}. " +
                $"The measured template default is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Edge flange authoring does not yet support {property}. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match)
            {
                count++;
                i += pattern.Length - 1;
            }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/EdgeFlangeOffsetAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured position-offset edge-flange subsystems
/// (FlangeOptions/edge_flange_position_offset and _offset10, version 0x74).
/// <para>
/// The prefix is the base-flange stream before the edge-selection wrapper
/// (0x525A). The suffix starts at the wrapper and contains the position-offset
/// parameter block, the edge flange and the downstream sheet-metal objects.
/// Prefix + matching suffix reconstructs each native sample byte-for-byte.
/// </para>
/// </summary>
internal static class EdgeFlangeOffsetAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x525A;
    internal const ushort MeasuredFullFolderCount = 24;
    internal const ushort SupportedStreamVersion = 0x74;

    internal const double MeasuredOffsetDistance = 0.01;
    internal const double MeasuredBendAngle = Math.PI / 2.0;
    internal const double MeasuredBendRadius = 0.002;
    internal const double MeasuredReliefWidth = 0.001;
    internal const double MeasuredReliefDepth = 0.001;

    internal const double LowPositionOffsetDistance = 0.005;
    internal const double HighPositionOffsetDistance = 0.010;

    internal const string LowPrefixSha256 =
        "C50C4A29FD80835392B43ADEE30C262750597A894940B9DF556104090B0E906F";
    internal const string HighPrefixSha256 =
        "96F76B54D163EEB6357193C5324E60258DE2F7723334592808F2194943A42367";
    internal const string LowSuffixSha256 =
        "6164A696A33D9EC902643C87B8714A2D4140BFF1D35294D8C64204F9D21EC6C9";
    internal const string HighSuffixSha256 =
        "F502698722F5464DEB9579FD76028FBBF74B45CF58EE2B35F85D5523A0F900DC";

    private static readonly byte[] LowSuffixGzipBase64 =
        SheetMetalAuthoringSegments.EdgeFlangeOffset0;

    private static readonly byte[] HighSuffixGzipBase64 =
        SheetMetalAuthoringSegments.EdgeFlangeOffset1;

    private static readonly Lazy<byte[]> LowSuffix =
        new(() => Decompress(LowSuffixGzipBase64, LowSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> HighSuffix =
        new(() => Decompress(HighSuffixGzipBase64, HighSuffixSha256), isThreadSafe: true);

    internal static bool IsSupportedDistance(double distance)
        => distance == LowPositionOffsetDistance || distance == HighPositionOffsetDistance;

    internal static byte[] GetSuffix(double distance)
    {
        return distance == LowPositionOffsetDistance ? (byte[])LowSuffix.Value.Clone()
            : distance == HighPositionOffsetDistance ? (byte[])HighSuffix.Value.Clone()
            : throw new ArgumentOutOfRangeException(nameof(distance));
    }

    internal static string GetPrefixSha256(double distance)
        => distance == LowPositionOffsetDistance ? LowPrefixSha256
            : distance == HighPositionOffsetDistance ? HighPrefixSha256
            : throw new ArgumentOutOfRangeException(nameof(distance));

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured position-offset edge-flange suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FreshEdgeFlangeAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured template for the fresh single-edge EdgeFlange layout created
/// by InsertSheetMetalEdgeFlange2 on a simple L-shaped base flange
/// (ResolvedFeatures 0x74, FolderHeader TypeId 0x75, one base flange plus one
/// edge flange). The suffix slots are patched for the fields that persisted in
/// the native single-variable save/reopen matrix (2026-10-31 edgepersist).
/// </summary>
internal static class FreshEdgeFlangeAuthoringSegments
{
    internal const string Name = "FreshSingleEdgeFlange";
    internal const string ClassName = "moEdgeFlange_c";
    internal const string DisplayName = "边线-法兰2";
    internal const int PrefixLength = 0x804B;
    internal const ushort StreamVersion = 0x74;
    internal const ushort FolderCount = 24;
    internal const int SuffixLength = 43552;
    internal const string NormalizedPrefixSha256 =
        "A0282F0B6E85DE9E8E7529110B08B37D90CAB586334EF25BC09F1BA7CB6D9E2D";
    internal const string SuffixSha256 =
        "61A60204FD8F1CEE2A5FE6CC298408B32529A37A4351E9E2BE268570526F426B";

    // Slots relative to the moEdgeFlange_c tag (start of the suffix).
    internal const int RadiusOffset = 0x431;                    // named D1 value
    internal const int UseDefaultBendRadiusOffset = 0x8BB;      // D1 name + 0x4A8
    internal const int ReliefWidthOffset = 0x3D8B;               // relief block + 0x08
    internal const int ReliefDepthOffset = 0x3D93;               // relief block + 0x10
    internal const int ReliefRatioOffset = 0x3D83;               // relief block + 0x00
    internal const int UseReliefRatioOffset = 0x3D9B;            // relief block + 0x18
    internal const int PositionTypeOffset = 0x3DA3;              // relief block + 0x20
    internal const int UseDefaultBendReliefOffset = 0x3DCB;      // relief block + 0x48
    internal const int PositionOffsetDistanceOffset = 0x3A07;    // named D6 value
    internal const int UsePositionOffsetOffset = 0x3A6F;         // D6 name + 0x86
    internal const int OffsetDistanceOffset = 0x408E;            // named D7 value

    // Position-offset template (native edgepersist_position_offset_2 save).
    internal const int PositionPrefixLength = 33143;             // 0x8177
    internal const int PositionSuffixLength = 43706;
    internal const string PositionNormalizedPrefixSha256 =
        "56F7762F4502C9E38ADCF96DF349C50B5497CE5B02DA3789614CEB0FE857648D";
    internal const string PositionSuffixSha256 =
        "C124DF9E36C0DFB4AAA479F32945E460194BB3670A7E48AA3AD2EA7943329500";
    internal const int PositionRadiusOffset = 0x431;
    internal const int PositionUseDefaultBendRadiusOffset = 0x8BB;
    internal const int PositionReliefWidthOffset = 0x3E25;
    internal const int PositionReliefDepthOffset = 0x3E2D;
    internal const int PositionReliefRatioOffset = 0x3E1D;
    internal const int PositionUseReliefRatioOffset = 0x3E35;
    internal const int PositionTemplateTypeOffset = 0x3E3D;
    internal const int PositionUseDefaultBendReliefOffset = 0x3E65;
    internal const int PositionUsePositionOffsetOffset = 0x3A6F;
    internal const int PositionOffsetDistanceD7Offset = 0x4128;

    // Radius template (native edgepersist_radius_003 save).
    internal const int RadiusPrefixLength = 33115;               // 0x815B
    internal const int RadiusSuffixLength = 43552;
    internal const string RadiusNormalizedPrefixSha256 =
        "1EF796A3AF7EAEA5FB0A56844F076A6E385C89704B56B478DAC1875653023F18";
    internal const string RadiusSuffixSha256 =
        "10BB686C7414BACB621DE18B7EEC7FD10572A1612B3180D5A57DD8BF7D20DA2B";

    private static readonly byte[] SuffixGzipBytes = SheetMetalAuthoringSegments.FreshEdgeFlange0;

    private static readonly byte[] PositionSuffixGzipBytes = SheetMetalAuthoringSegments.FreshEdgeFlange1;

    private static readonly byte[] RadiusSuffixGzipBytes = SheetMetalAuthoringSegments.FreshEdgeFlange2;

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBytes, SuffixSha256, SuffixLength), isThreadSafe: true);
    private static readonly Lazy<byte[]> PositionSuffix =
        new(() => Decompress(PositionSuffixGzipBytes, PositionSuffixSha256, PositionSuffixLength),
            isThreadSafe: true);
    private static readonly Lazy<byte[]> RadiusSuffix =
        new(() => Decompress(RadiusSuffixGzipBytes, RadiusSuffixSha256, RadiusSuffixLength),
            isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    internal static byte[] GetPositionSuffix() => (byte[])PositionSuffix.Value.Clone();

    internal static byte[] GetRadiusSuffix() => (byte[])RadiusSuffix.Value.Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256, int expectedLength)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (bytes.Length != expectedLength)
            throw new InvalidOperationException("Measured fresh EdgeFlange suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured fresh EdgeFlange suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FreshEdgeFlangeAuthoringBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed-template authoring for the fresh single-edge EdgeFlange layout
/// (ResolvedFeatures 0x74, TypeId 0x75). Only the fields that persisted in the
/// native save/reopen matrix are patchable: UseDefaultBendRadius/BendRadius,
/// ReliefWidth/ReliefDepth, UseDefaultBendRelief/UseReliefRatio,
/// UsePositionOffset/PositionOffsetDistance, OffsetDistance and the measured
/// BendAngle/EdgeCount/PositionType. PositionOffsetType=2 is rejected because
/// native readback reverts it to 1.
/// </summary>
internal static class FreshEdgeFlangeAuthoringBuilder
{
    internal static byte[] Build(
        byte[] currentStream, EdgeFlangeFeatureModel def, string? featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        ValidateDefinition(def);

        bool positionTemplate = def.UsePositionOffset == true;
        bool radiusTemplate = !positionTemplate &&
            (def.UseDefaultBendRadius == false ||
             (def.BendRadius.HasValue && Math.Abs(def.BendRadius.Value - 0.002) > 1e-12));
        if (positionTemplate &&
            (def.UseDefaultBendRadius == false ||
             (def.BendRadius.HasValue && Math.Abs(def.BendRadius.Value - 0.002) > 1e-12)))
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring has no measured template combining a non-default bend " +
                "radius with UsePositionOffset=true. No stream changes have been made.");
        int prefixLength = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionPrefixLength
            : radiusTemplate
                ? FreshEdgeFlangeAuthoringSegments.RadiusPrefixLength
                : FreshEdgeFlangeAuthoringSegments.PrefixLength;
        string normalizedPrefixSha = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionNormalizedPrefixSha256
            : radiusTemplate
                ? FreshEdgeFlangeAuthoringSegments.RadiusNormalizedPrefixSha256
                : FreshEdgeFlangeAuthoringSegments.NormalizedPrefixSha256;
        byte[] suffix = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.GetPositionSuffix()
            : radiusTemplate
                ? FreshEdgeFlangeAuthoringSegments.GetRadiusSuffix()
                : FreshEdgeFlangeAuthoringSegments.GetSuffix();
        string templateLabel = positionTemplate ? "position-offset"
            : radiusTemplate ? "radius" : "default";

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? FreshEdgeFlangeAuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, FreshEdgeFlangeAuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring supports only the measured display name " +
                $"'{FreshEdgeFlangeAuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null || currentStream.Length != prefixLength)
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring requires the stripped base of the " +
                $"{templateLabel} measured sample " +
                $"({prefixLength} bytes, ending at the " +
                $"{FreshEdgeFlangeAuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != FreshEdgeFlangeAuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring supports only ResolvedFeatures version " +
                $"0x{FreshEdgeFlangeAuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != FreshEdgeFlangeAuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring requires folder count " +
                $"{FreshEdgeFlangeAuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, FreshEdgeFlangeAuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring requires a stripped base without an existing edge flange. " +
                "No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, normalizedPrefixSha,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring requires the matching stripped base: normalized prefix " +
                $"SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{normalizedPrefixSha.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(FreshEdgeFlangeAuthoringSegments.FolderCount).CopyTo(newStream, 4);

        int Base(int offset) => currentStream.Length + offset;
        int radiusOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionRadiusOffset
            : FreshEdgeFlangeAuthoringSegments.RadiusOffset;
        int defaultRadiusOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUseDefaultBendRadiusOffset
            : FreshEdgeFlangeAuthoringSegments.UseDefaultBendRadiusOffset;
        int widthOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionReliefWidthOffset
            : FreshEdgeFlangeAuthoringSegments.ReliefWidthOffset;
        int depthOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionReliefDepthOffset
            : FreshEdgeFlangeAuthoringSegments.ReliefDepthOffset;
        int ratioOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionReliefRatioOffset
            : FreshEdgeFlangeAuthoringSegments.ReliefRatioOffset;
        int useRatioOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUseReliefRatioOffset
            : FreshEdgeFlangeAuthoringSegments.UseReliefRatioOffset;
        int usePositionOffsetOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUsePositionOffsetOffset
            : FreshEdgeFlangeAuthoringSegments.UsePositionOffsetOffset;
        int positionDistanceOffset = FreshEdgeFlangeAuthoringSegments.PositionOffsetDistanceOffset;
        int defaultReliefOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUseDefaultBendReliefOffset
            : FreshEdgeFlangeAuthoringSegments.UseDefaultBendReliefOffset;
        int offsetDistanceOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionOffsetDistanceD7Offset
            : FreshEdgeFlangeAuthoringSegments.OffsetDistanceOffset;

        double radius = def.BendRadius ??
            (radiusTemplate ? 0.003 : (def.UseDefaultBendRadius == false ? 0.0 : 0.002));
        BitConverter.GetBytes(radius).CopyTo(newStream, Base(radiusOffset));
        newStream[Base(defaultRadiusOffset)] =
            (byte)((def.UseDefaultBendRadius ?? !radiusTemplate) ? 1 : 0);
        BitConverter.GetBytes(def.ReliefWidth ?? 0.0005)
            .CopyTo(newStream, Base(widthOffset));
        BitConverter.GetBytes(def.ReliefDepth ?? 0.0005)
            .CopyTo(newStream, Base(depthOffset));
        BitConverter.GetBytes(def.ReliefRatio ?? 0.5)
            .CopyTo(newStream, Base(ratioOffset));
        newStream[Base(useRatioOffset)] =
            (byte)((def.UseReliefRatio ?? true) ? 1 : 0);
        BitConverter.GetBytes(def.UsePositionOffset == true ? 1u : 0u)
            .CopyTo(newStream, Base(usePositionOffsetOffset));
        BitConverter.GetBytes(def.PositionOffsetDistance ?? 0.01)
            .CopyTo(newStream, Base(positionDistanceOffset));
        newStream[Base(defaultReliefOffset)] =
            (byte)((def.UseDefaultBendRelief ?? true) ? 1 : 0);
        if (def.OffsetDistance.HasValue)
            BitConverter.GetBytes(def.OffsetDistance.Value)
                .CopyTo(newStream, Base(offsetDistanceOffset));
        return newStream;
    }

    private static void ValidateDefinition(EdgeFlangeFeatureModel def)
    {
        if (def.BendAngle.HasValue && Math.Abs(def.BendAngle.Value - Math.PI / 2.0) > 1e-12)
            throw Unsupported(nameof(def.BendAngle), def.BendAngle.Value);
        RequireNullOrEquals(def.EdgeCount, 1, nameof(def.EdgeCount));
        RequireNullOrEquals(def.PositionType, 3, nameof(def.PositionType));
        RequireNullOrEquals(def.ReliefRatio, 0.5, nameof(def.ReliefRatio));
        if (def.PositionOffsetType == 2)
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring cannot persist PositionOffsetType=2: native readback " +
                "reverts it to 1. No stream changes have been made.");
        RequireNullOrEquals(def.PositionOffsetType, 1, nameof(def.PositionOffsetType));

        RequireNull(def.Edge, nameof(def.Edge));
        RequireNull(def.Edges, nameof(def.Edges));
        RequireNull(def.OffsetReference, nameof(def.OffsetReference));
        RequireNull(def.PositionOffsetReference, nameof(def.PositionOffsetReference));
        RequireNull(def.AngleReference, nameof(def.AngleReference));
        RequireNull(def.CustomBendAllowance, nameof(def.CustomBendAllowance));
        RequireNull(def.OffsetType, nameof(def.OffsetType));
        RequireNull(def.OffsetDimType, nameof(def.OffsetDimType));
        RequireNull(def.ReverseOffset, nameof(def.ReverseOffset));
        RequireNull(def.UsePositionTrimSideBends, nameof(def.UsePositionTrimSideBends));
        RequireNull(def.ReversePositionOffset, nameof(def.ReversePositionOffset));
        RequireNull(def.AutoReliefType, nameof(def.AutoReliefType));
        RequireNull(def.ReliefTearType, nameof(def.ReliefTearType));
        RequireNull(def.UseDefaultBendAllowance, nameof(def.UseDefaultBendAllowance));
        RequireNull(def.GapDistance, nameof(def.GapDistance));
        RequireNull(def.LockAngle, nameof(def.LockAngle));
        RequireNull(def.PerpendicularToFace, nameof(def.PerpendicularToFace));
        RequireNull(def.NormalToFlangePlane, nameof(def.NormalToFlangePlane));
        RequireNull(def.PositionReferenceType, nameof(def.PositionReferenceType));

        ValidatePositiveOrZero(def.BendRadius, nameof(def.BendRadius));
        ValidatePositive(def.ReliefWidth, nameof(def.ReliefWidth));
        ValidatePositive(def.ReliefDepth, nameof(def.ReliefDepth));
        ValidatePositive(def.PositionOffsetDistance, nameof(def.PositionOffsetDistance));
        ValidatePositive(def.OffsetDistance, nameof(def.OffsetDistance));

        if (def.UseDefaultBendRadius == true && def.BendRadius.HasValue &&
            Math.Abs(def.BendRadius.Value - 0.002) > 1e-12)
            throw Unsupported(nameof(def.BendRadius), def.BendRadius.Value);
    }

    private static void ValidatePositiveOrZero(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            value.Value < 0 || value.Value > 0.1)
            throw Unsupported(property, value.Value);
    }

    private static void ValidatePositive(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            value.Value <= 0 || value.Value > 0.1)
            throw Unsupported(property, value.Value);
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null) throw Unsupported(property, value);
    }

    private static void RequireNullOrEquals(int? value, int expected, string property)
    {
        if (value.HasValue && value.Value != expected) throw Unsupported(property, value.Value);
    }

    private static void RequireNullOrEquals(double? value, double expected, string property)
    {
        if (value.HasValue && Math.Abs(value.Value - expected) > 1e-12)
            throw Unsupported(property, value.Value);
    }

    private static NotSupportedException Unsupported(string property, object value) =>
        new($"Fresh EdgeFlange authoring does not support {property}={value}. " +
            "No stream changes have been made.");

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}


// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FlangeFeatureParsers.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict parsers for Task 2.2 edge and miter flanges, powered by <see cref="FeatureWindowContext"/>
/// (<see cref="FeatureParameterBag"/> + <see cref="FeatureSelectionBag"/>).
/// </summary>
internal static class FlangeFeatureParsers
{
    internal const string EdgeFlangeClassName = EdgeFlangeFeatureModel.SerializedClassName;
    internal const string MiterFlangeClassName = MiterFlangeFeatureModel.SerializedClassName;
    internal const uint FreshSingleEdgeSerializedTypeId = 0x75;

    internal static bool IsEdgeFlangeNode(MfcElement element)
    {
        if (element == null || element.ClassName != EdgeFlangeClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == EdgeFlangeFeatureModel.ObservedSerializedTypeId ||
               header.TypeId == FreshSingleEdgeSerializedTypeId;
    }

    public static IFeatureData? ParseEdgeFlange(MfcElement element)
    {
        if (!IsEdgeFlangeNode(element))
            return null;
        return null;
    }

    internal static IFeatureData? ParseEdgeFlange(
        MfcDocument doc, MfcElement element, int windowStart, int windowEnd)
    {
        if (doc == null || element == null || !IsEdgeFlangeNode(element))
            return null;

        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        windowStart = Math.Max(0, windowStart);
        windowEnd = Math.Min(doc.SourceData.Length, windowEnd);
        if (windowStart >= element.TagOffset || element.TagOffset >= windowEnd)
            return null;

        if (!FeatureWindowContext.TryCreate(doc, windowStart, windowEnd, expectedStreamVersion: 0x74, out var ctx, EdgeFlangeClassName, header.TypeId) || ctx == null)
            return null;

        if (header.TypeId == FreshSingleEdgeSerializedTypeId)
            return ParseFreshSingleEdgeFlange(ctx, element);
        if (header.TypeId != EdgeFlangeFeatureModel.ObservedSerializedTypeId)
            return null;

        if (!ctx.Selections.TryReadSingleEdgeCount(out int edgeCount, minOffset: windowStart, maxOffset: element.TagOffset) || edgeCount != 1)
            return null;

        var firstAngular = ctx.Nodes
            .Where(e => e.ClassName == "moDisplayAngularDim_c" && e.TagOffset > element.TagOffset)
            .OrderBy(e => e.TagOffset)
            .FirstOrDefault();
        if (firstAngular == null) return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D1", out double radius, out _, element.TagOffset, firstAngular.TagOffset) ||
            !SheetMetalTemplateParser.PositiveFinite(radius))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D4", out double angle, out int d4NameOffset, firstAngular.TagOffset, windowEnd) ||
            !SheetMetalTemplateParser.PositiveFinite(angle) || angle > Math.PI)
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D7", out double offsetDistance, out int d7NameOffset, firstAngular.TagOffset, windowEnd) ||
            !SheetMetalTemplateParser.PositiveFinite(offsetDistance))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D6", out double positionOffsetDistance, out int d6Name, firstAngular.TagOffset, windowEnd) ||
            !SheetMetalTemplateParser.PositiveFinite(positionOffsetDistance))
            return null;

        byte[] raw = ctx.RawData;
        int usePositionOffsetSlot = d6Name + 0x1E + 0x68;
        if (usePositionOffsetSlot < 0 || usePositionOffsetSlot + 4 > raw.Length) return null;
        uint usePositionOffsetFlag = BitConverter.ToUInt32(raw, usePositionOffsetSlot);
        if (usePositionOffsetFlag > 1) return null;

        if (!TryReadReliefAndPositionBlock(raw, d4NameOffset, d7NameOffset,
                out double reliefWidth, out double reliefDepth, out int positionType))
            return null;

        var data = new EdgeFlangeFeatureModel
        {
            BendRadius = radius,
            BendAngle = angle,
            EdgeCount = edgeCount,
            OffsetDistance = offsetDistance,
            PositionType = positionType,
            UsePositionOffset = usePositionOffsetFlag == 1,
            PositionOffsetDistance = positionOffsetDistance,
            ReliefWidth = reliefWidth,
            ReliefDepth = reliefDepth
        };
        data.ParameterResolution.BendRadius = true;
        data.ParameterResolution.BendAngle = true;
        data.ParameterResolution.EdgeCount = true;
        data.ParameterResolution.OffsetDistance = true;
        data.ParameterResolution.PositionType = true;
        data.ParameterResolution.UsePositionOffset = true;
        data.ParameterResolution.PositionOffsetDistance = true;
        data.ParameterResolution.ReliefWidth = true;
        data.ParameterResolution.ReliefDepth = true;
        return data;
    }

    private static IFeatureData? ParseFreshSingleEdgeFlange(FeatureWindowContext ctx, MfcElement element)
    {
        if (!ctx.Selections.TryReadSingleEdgeCount(out int edgeCount, minOffset: ctx.Start, maxOffset: element.TagOffset) || edgeCount != 1)
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D4", out double angle, out int d4Name, element.TagOffset, ctx.End) ||
            !SheetMetalTemplateParser.PositiveFinite(angle) || angle > Math.PI)
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D1", out double radius, out int d1Name, element.TagOffset, d4Name, allowZero: true) ||
            radius < 0 || double.IsNaN(radius) || double.IsInfinity(radius))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D7", out double offsetDistance, out int d7Name, d4Name, ctx.End, allowZero: true) ||
            offsetDistance < 0 || double.IsNaN(offsetDistance) || double.IsInfinity(offsetDistance))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D6", out double positionOffsetDistance, out int d6Name, d4Name, d7Name) ||
            !SheetMetalTemplateParser.PositiveFinite(positionOffsetDistance))
            return null;

        byte[] raw = ctx.RawData;
        int defaultRadiusSlot = d1Name + 0x4A8;
        if (defaultRadiusSlot < 0 || defaultRadiusSlot >= d4Name) return null;
        byte defaultRadiusFlag = raw[defaultRadiusSlot];
        if (defaultRadiusFlag > 1) return null;

        int usePositionOffsetSlot = d6Name + 0x1E + 0x68;
        if (usePositionOffsetSlot < 0 || usePositionOffsetSlot + 4 > raw.Length) return null;
        uint usePositionOffsetFlag = BitConverter.ToUInt32(raw, usePositionOffsetSlot);
        if (usePositionOffsetFlag > 1) return null;

        if (!TryReadFreshReliefBlock(raw, d4Name, ctx.End,
                out double reliefRatio, out double reliefWidth, out double reliefDepth,
                out int positionType, out bool useReliefRatio, out bool useDefaultBendRelief))
            return null;

        var data = new EdgeFlangeFeatureModel
        {
            TypeId = FreshSingleEdgeSerializedTypeId,
            BendRadius = radius,
            BendAngle = angle,
            EdgeCount = edgeCount,
            OffsetDistance = offsetDistance > 0 ? offsetDistance : null,
            PositionType = positionType,
            UsePositionOffset = usePositionOffsetFlag == 1,
            PositionOffsetDistance = positionOffsetDistance,
            ReliefRatio = reliefRatio,
            ReliefWidth = reliefWidth,
            ReliefDepth = reliefDepth,
            UseDefaultBendRadius = defaultRadiusFlag == 1,
            UseReliefRatio = useReliefRatio,
            UseDefaultBendRelief = useDefaultBendRelief
        };
        data.ParameterResolution.BendRadius = true;
        data.ParameterResolution.BendAngle = true;
        data.ParameterResolution.EdgeCount = true;
        data.ParameterResolution.OffsetDistance = offsetDistance > 0;
        data.ParameterResolution.PositionType = true;
        data.ParameterResolution.UsePositionOffset = true;
        data.ParameterResolution.PositionOffsetDistance = true;
        data.ParameterResolution.ReliefRatio = true;
        data.ParameterResolution.ReliefWidth = true;
        data.ParameterResolution.ReliefDepth = true;
        data.ParameterResolution.UseDefaultBendRadius = true;
        data.ParameterResolution.UseReliefRatio = true;
        data.ParameterResolution.UseDefaultBendRelief = true;
        return data;
    }

    private static bool TryReadFreshReliefBlock(
        byte[] raw, int start, int end,
        out double reliefRatio, out double reliefWidth, out double reliefDepth,
        out int positionType, out bool useReliefRatio, out bool useDefaultBendRelief)
    {
        reliefRatio = 0;
        reliefWidth = 0;
        reliefDepth = 0;
        positionType = 0;
        useReliefRatio = false;
        useDefaultBendRelief = false;
        int count = 0;
        int first = Math.Max(0, start);
        int last = Math.Min(end, raw.Length) - 0x50;
        for (int p = first; p <= last; p++)
        {
            double ratio = BitConverter.ToDouble(raw, p);
            if (ratio != 0.5) continue;
            double width = BitConverter.ToDouble(raw, p + 8);
            double depth = BitConverter.ToDouble(raw, p + 16);
            if (!SheetMetalTemplateParser.PositiveFinite(width) ||
                !SheetMetalTemplateParser.PositiveFinite(depth) ||
                width > 0.1 || depth > 0.1) continue;
            uint ratioFlag = BitConverter.ToUInt32(raw, p + 0x18);
            if (ratioFlag > 1) continue;
            uint position = BitConverter.ToUInt32(raw, p + 0x20);
            if (position < 1 || position > 3) continue;
            if (BitConverter.ToUInt32(raw, p + 0x24) != 0 ||
                BitConverter.ToUInt32(raw, p + 0x28) != 0 ||
                BitConverter.ToUInt32(raw, p + 0x30) != 0 ||
                BitConverter.ToUInt32(raw, p + 0x38) != 0 ||
                BitConverter.ToUInt32(raw, p + 0x40) != 0) continue;
            uint defaultReliefFlag = BitConverter.ToUInt32(raw, p + 0x48);
            if (defaultReliefFlag > 1) continue;
            if (BitConverter.ToUInt32(raw, p + 0x4C) != 0x000080EFu) continue;
            if (BitConverter.ToUInt16(raw, p + 0x5E) != 0xFFFF) continue;

            if (count > 0) return false;
            reliefRatio = ratio;
            reliefWidth = width;
            reliefDepth = depth;
            positionType = checked((int)position);
            useReliefRatio = ratioFlag == 1;
            useDefaultBendRelief = defaultReliefFlag == 1;
            count++;
        }
        return count == 1;
    }

    private static bool TryReadReliefAndPositionBlock(
        byte[] raw, int start, int end,
        out double reliefWidth, out double reliefDepth, out int positionType)
    {
        reliefWidth = 0;
        reliefDepth = 0;
        positionType = 0;
        int count = 0;
        int first = Math.Max(0, start);
        int last = Math.Min(end, raw.Length) - 40;
        for (int p = first; p <= last; p++)
        {
            if (BitConverter.ToDouble(raw, p) != 0.5) continue;
            double width = BitConverter.ToDouble(raw, p + 8);
            double depth = BitConverter.ToDouble(raw, p + 16);
            if (!SheetMetalTemplateParser.PositiveFinite(width) ||
                !SheetMetalTemplateParser.PositiveFinite(depth))
                continue;
            if (BitConverter.ToUInt64(raw, p + 24) != 1UL) continue;
            uint position = BitConverter.ToUInt32(raw, p + 32);
            if (position < 1 || position > 3) continue;

            if (count > 0) return false;
            reliefWidth = width;
            reliefDepth = depth;
            positionType = checked((int)position);
            count++;
        }
        return count == 1;
    }

    public static IFeatureData? ParseMiterFlange(MfcElement element)
    {
        if (element == null || element.ClassName != MiterFlangeClassName || element.TagByte != 0x01)
            return null;
        return null;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FlatPatternAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured flat-pattern feature block from the controlled 0x5D specimen
/// (sm_base_flange_10mm.SLDPRT, ResolvedFeatures version 0x73).
/// <para>
/// The prefix runs from the stream start to the moFlatPattern_c class tag
/// (0x7380) and keeps the base flange, sheet-metal template and flat-pattern
/// template context. The suffix starts at that tag and runs to the stream end.
/// Prefix + suffix reconstructs the native stream byte-for-byte. The normalized
/// prefix hash zeroes the folder-count field at offset 4 before hashing, matching
/// the other task builders.
/// </para>
/// </summary>
internal static class FlatPatternAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x7380;
    internal const ushort SupportedStreamVersion = 0x73;
    internal const ushort MeasuredFullFolderCount = 22;
    internal const int MarkerOffset = 0x2281;
    internal const uint MeasuredMarkerValue = 22;
    internal const string MeasuredDisplayName = "平板型式6";

    /// <summary>Option-block anchor relative to the suffix start (moFaceRef_c.DataEnd).</summary>
    internal const int OptionAnchorSuffixOffset = 0xEA7;

    internal const string MeasuredNormalizedPrefixSha256 =
        "4B9BFDEAEA285DF315D03E8672A69AA33EA8719B2E55EAFB2F04BAFBEECB3E72";
    internal const string MeasuredSuffixSha256 =
        "77D7847BDD282A873DF46B10C0F102BE5FFACE41716CB14AA14911C303602890";

    private static readonly byte[] SuffixGzipBytes =
        SheetMetalAuthoringSegments.FlatPattern0;

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBytes, MeasuredSuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured flat-pattern suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FlatPatternFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured flat-pattern authoring for the controlled 0x5D specimen
/// (sm_base_flange_10mm.SLDPRT, version 0x73).
/// <para>
/// The stripped base keeps the base flange, sheet-metal template and
/// flat-pattern template context; the measured suffix starts at the
/// moFlatPattern_c tag. Only the same fixed face and default option combination
/// as the source is accepted. Custom names, altered options, unsupported stream
/// versions and non-matching bases are rejected before the stream is touched.
/// </para>
/// </summary>
internal static class FlatPatternFeatureBuilder
{
    internal const string DefaultFeatureName = FlatPatternAuthoringSegments.MeasuredDisplayName;

    internal static void ValidateDefinition(FlatPatternFeatureModel? description)
    {
        if (description == null) return;

        // Fixed persistent selection comes from the measured suffix.
        RequireNull(description.FixedFace, nameof(description.FixedFace));
        RequireNull(description.FixedFace2, nameof(description.FixedFace2));
        RequireNull(description.ExcludedFaces, nameof(description.ExcludedFaces));

        if (description.BreakCornerType.HasValue &&
            (description.BreakCornerType.Value < 0 || description.BreakCornerType.Value > 1))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring supports BreakCornerType 0 (fillet) or 1 (chamfer), " +
                $"but received {description.BreakCornerType.Value}. No stream changes have been made.");
        }
        ValidatePositiveOptional(description.BreakCornerRadius, nameof(description.BreakCornerRadius));

        if (description.CornerTrimReliefType.HasValue &&
            (description.CornerTrimReliefType.Value < 0 || description.CornerTrimReliefType.Value > 9))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring supports CornerTrimReliefType 0..9, " +
                $"but received {description.CornerTrimReliefType.Value}. No stream changes have been made.");
        }
        ValidatePositiveOptional(description.CornerTrimReliefDistance,
            nameof(description.CornerTrimReliefDistance));

        RequireNullOrEquals(description.UseRatioToThickness, false, nameof(description.UseRatioToThickness));
        RequireNull(description.CornerTrimRatioToThickness, nameof(description.CornerTrimRatioToThickness));
        RequireNull(description.ShowSlitInCornerRelief, nameof(description.ShowSlitInCornerRelief));
        RequireNull(description.AddCornerTrim, nameof(description.AddCornerTrim));
        RequireNull(description.BreakCorners, nameof(description.BreakCorners));
    }

    internal static byte[] Build(
        byte[] currentStream, FlatPatternFeatureModel description, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? DefaultFeatureName
            : featureName!;
        if (!string.Equals(resolvedName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; received '{resolvedName}'. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != FlatPatternAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring is only supported for ResolvedFeatures streams version " +
                $"0x{FlatPatternAuthoringSegments.SupportedStreamVersion:X2}, but the document uses " +
                $"0x{version:X2}. No stream changes have been made.");
        }

        if (currentStream.Length != FlatPatternAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires the stripped base prefix of the controlled specimen " +
                $"({FlatPatternAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream.Length} bytes. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) != FlatPatternAuthoringSegments.MeasuredFullFolderCount)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires the measured folder count " +
                $"{FlatPatternAuthoringSegments.MeasuredFullFolderCount}. No stream changes have been made.");
        }

        int marker = FlatPatternAuthoringSegments.MarkerOffset;
        if (marker < 0 || marker + 8 > currentStream.Length ||
            currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) !=
                FlatPatternAuthoringSegments.MeasuredMarkerValue)
        {
            throw new NotSupportedException(
                "Flat-pattern authoring requires the measured 0x67/marker block at " +
                $"0x{marker:X}. No stream changes have been made.");
        }

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string normalizedHash = Sha256Hex(normalized);
        if (!string.Equals(normalizedHash,
                FlatPatternAuthoringSegments.MeasuredNormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires the controlled stripped base: normalized prefix " +
                $"SHA-256 {normalizedHash.Substring(0, 12)} does not match the measured signature. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moFlatPattern_c") != 0)
        {
            throw new NotSupportedException(
                "Flat-pattern authoring requires a stripped base without an existing flat pattern. " +
                "No stream changes have been made.");
        }

        byte[] suffix = FlatPatternAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        BitConverter.GetBytes(FlatPatternAuthoringSegments.MeasuredFullFolderCount).CopyTo(newStream, 4);
        BitConverter.GetBytes(FlatPatternAuthoringSegments.MeasuredMarkerValue).CopyTo(newStream, marker + 4);

        int optionAnchor = currentStream.Length +
            FlatPatternAuthoringSegments.OptionAnchorSuffixOffset;
        PatchOptions(newStream, optionAnchor, description);
        return newStream;
    }

    private const int OptionFlags = 0x21;
    private const int OptionTrimType = 0x37;
    private const int OptionTrimDistance = 0x3B;
    private const int OptionBreakType = 0x43;
    private const int OptionBreakRadius = 0x47;
    private const int OptionSimplifyBends = 0x57;

    private static void PatchOptions(
        byte[] data, int anchor, FlatPatternFeatureModel description)
    {
        if (anchor < 0 || anchor + 0x64 > data.Length)
            throw new InvalidOperationException(
                $"Flat-pattern option block at 0x{anchor:X} is outside the stream.");

        if (data[anchor] != 0xFF || data[anchor + 1] != 0xFF ||
            BitConverter.ToUInt32(data, anchor + 0x16) != 0x4A38u ||
            BitConverter.ToUInt32(data, anchor + 0x1C) != 1u ||
            data[anchor + 0x2B] != 1 || data[anchor + 0x5B] != 1)
        {
            throw new NotSupportedException(
                "The measured flat-pattern option layout gates do not match. " +
                "No stream changes have been made.");
        }

        if (data[anchor + OptionFlags] != 1 ||
            BitConverter.ToUInt32(data, anchor + OptionTrimType) != 0u ||
            BitConverter.ToUInt32(data, anchor + OptionBreakType) != 0u ||
            data[anchor + OptionSimplifyBends] != 1 ||
            BitConverter.ToDouble(data, anchor + OptionTrimDistance) != 0.005 ||
            BitConverter.ToDouble(data, anchor + OptionBreakRadius) != 0.005)
        {
            throw new NotSupportedException(
                "The measured flat-pattern option defaults do not match the template. " +
                "No stream changes have been made.");
        }

        byte flags = (byte)(((description.MergeFace ?? true) ? 1 : 0) |
                            ((description.CornerTreatment ?? true) ? 0 : 2));
        data[anchor + OptionFlags] = flags;

        uint breakType = checked((uint)(description.BreakCornerType ?? 0));
        PatchUInt32(data, anchor + OptionBreakType, 0u, breakType, "BreakCornerType");
        PatchDouble(data, anchor + OptionBreakRadius, 0.005,
            description.BreakCornerRadius ?? 0.005, "BreakCornerRadius");

        uint trimType = checked((uint)(description.CornerTrimReliefType ?? 0));
        PatchUInt32(data, anchor + OptionTrimType, 0u, trimType, "CornerTrimReliefType");
        PatchDouble(data, anchor + OptionTrimDistance, 0.005,
            description.CornerTrimReliefDistance ?? 0.005, "CornerTrimReliefDistance");

        data[anchor + OptionSimplifyBends] =
            (byte)((description.SimplifyBends ?? true) ? 1 : 0);
    }

    private static void PatchUInt32(
        byte[] data, int offset, uint expected, uint value, string slotName)
    {
        uint current = BitConverter.ToUInt32(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Flat-pattern {slotName} slot at 0x{offset:X} contains {current}, " +
                $"expected {expected}. No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void PatchDouble(
        byte[] data, int offset, double expected, double value, string slotName)
    {
        double current = BitConverter.ToDouble(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Flat-pattern {slotName} slot at 0x{offset:X} contains {current:R}, " +
                $"expected {expected:R}. No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring does not yet support {property}={value.Value}. " +
                $"The measured template value is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring does not yet support {property}. No stream changes have been made.");
        }
    }

    private static void ValidatePositiveOptional(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0.0)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires {property} to be a positive finite number, " +
                $"but received {value.Value:R}. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FlatPatternInline066AuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed inline FlatPattern 0x66 template from sm_base_flange_after_fillet
/// (ResolvedFeatures 0x74, folder count 25, 3900-byte suffix from the class tag).
/// The suffix slots are patched for MergeFace/CornerTreatment, SimplifyBends,
/// BreakCornerType/Radius, CornerTrimReliefType and the shared distance/ratio
/// double, UseRatioToThickness and ShowSlitInCornerRelief.
/// </summary>
internal static class FlatPatternInline066AuthoringSegments
{
    internal const string Name = "FlatPatternInline066AfterFillet";
    internal const string ClassName = "moFlatPattern_c";
    internal const string DisplayName = "平板型式7";
    internal const int PrefixLength = 0x9B02;      // 39682
    internal const ushort StreamVersion = 0x74;
    internal const ushort FolderCount = 25;
    internal const int SuffixLength = 3900;
    internal const string NormalizedPrefixSha256 =
        "F615C7B30F845EE2D0261BBA283505F1EE3CCC5F2A57820C652F2FCB0C04C3D7";
    internal const string SuffixSha256 =
        "1E81FEF1B11531F191A633AA2DB96812593C54ACC3E623AFE7C34CC553017ABE";

    internal const int FlagsOffset = 0xDF7;
    internal const int TrimTypeOffset = 0xE0D;
    internal const int TrimOffset = 0xE11;
    internal const int BreakTypeOffset = 0xE19;
    internal const int BreakRadiusOffset = 0xE1D;
    internal const int UseRatioOffset = 0xE29;
    internal const int SimplifyOffset = 0xE2D;
    internal const int SlitOffset = 0xE31;

    private static readonly byte[] SuffixGzipBytes = SheetMetalAuthoringSegments.FlatPatternInline0;

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBytes, SuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (bytes.Length != SuffixLength)
            throw new InvalidOperationException("Measured 0x66 flat-pattern suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured 0x66 flat-pattern suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FlatPatternInline066FeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed-template authoring for the inline FlatPattern 0x66 layout
/// (sm_base_flange_after_fillet, ResolvedFeatures 0x74, folder count 25).
/// Only the proven slots are patchable; FixedFace/FixedFace2/ExcludedFaces are
/// rejected because the native API did not persist independent values, and
/// distance/ratio must be equal because the API serializes one shared double.
/// </summary>
internal static class FlatPatternInline066FeatureBuilder
{
    internal static byte[] Build(
        byte[] currentStream, FlatPatternFeatureModel definition, string? featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        ValidateDefinition(definition);

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? FlatPatternInline066AuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, FlatPatternInline066AuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring supports only the measured display name " +
                $"'{FlatPatternInline066AuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null ||
            currentStream.Length != FlatPatternInline066AuthoringSegments.PrefixLength)
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring requires the stripped base " +
                $"({FlatPatternInline066AuthoringSegments.PrefixLength} bytes, ending at the " +
                $"{FlatPatternInline066AuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != FlatPatternInline066AuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring supports only ResolvedFeatures version " +
                $"0x{FlatPatternInline066AuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != FlatPatternInline066AuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring requires folder count " +
                $"{FlatPatternInline066AuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, FlatPatternInline066AuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "0x66 flat-pattern authoring requires a stripped base without an existing flat " +
                "pattern. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, FlatPatternInline066AuthoringSegments.NormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "0x66 flat-pattern authoring requires the matching stripped base: normalized prefix " +
                $"SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{FlatPatternInline066AuthoringSegments.NormalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] suffix = FlatPatternInline066AuthoringSegments.GetSuffix();
        int flagsOffset = FlatPatternInline066AuthoringSegments.FlagsOffset;
        int trimTypeOffset = FlatPatternInline066AuthoringSegments.TrimTypeOffset;
        int trimOffset = FlatPatternInline066AuthoringSegments.TrimOffset;
        int breakTypeOffset = FlatPatternInline066AuthoringSegments.BreakTypeOffset;
        int breakRadiusOffset = FlatPatternInline066AuthoringSegments.BreakRadiusOffset;
        int useRatioOffset = FlatPatternInline066AuthoringSegments.UseRatioOffset;
        int simplifyOffset = FlatPatternInline066AuthoringSegments.SimplifyOffset;
        int slitOffset = FlatPatternInline066AuthoringSegments.SlitOffset;
        if (suffix[flagsOffset] != 1 ||
            BitConverter.ToUInt32(suffix, trimTypeOffset) != 0 ||
            BitConverter.ToDouble(suffix, trimOffset) != 0.005 ||
            BitConverter.ToUInt32(suffix, breakTypeOffset) != 0 ||
            BitConverter.ToDouble(suffix, breakRadiusOffset) != 0.005 ||
            suffix[useRatioOffset] != 0 || suffix[simplifyOffset] != 1 || suffix[slitOffset] != 1)
            throw new NotSupportedException(
                "The measured 0x66 flat-pattern option defaults do not match the template. " +
                "No stream changes have been made.");

        byte flags = (byte)(((definition.MergeFace ?? true) ? 1 : 0) |
                            ((definition.CornerTreatment ?? true) ? 0 : 2));
        suffix[flagsOffset] = flags;
        BitConverter.GetBytes(checked((uint)(definition.CornerTrimReliefType ?? 0)))
            .CopyTo(suffix, trimTypeOffset);
        double trim = definition.CornerTrimReliefDistance ??
                      definition.CornerTrimRatioToThickness ?? 0.005;
        BitConverter.GetBytes(trim).CopyTo(suffix, trimOffset);
        BitConverter.GetBytes(checked((uint)(definition.BreakCornerType ?? 0)))
            .CopyTo(suffix, breakTypeOffset);
        BitConverter.GetBytes(definition.BreakCornerRadius ?? 0.005)
            .CopyTo(suffix, breakRadiusOffset);
        suffix[useRatioOffset] = (byte)((definition.UseRatioToThickness ?? false) ? 1 : 0);
        suffix[simplifyOffset] = (byte)((definition.SimplifyBends ?? true) ? 1 : 0);
        suffix[slitOffset] = (byte)((definition.ShowSlitInCornerRelief ?? true) ? 1 : 0);

        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(FlatPatternInline066AuthoringSegments.FolderCount).CopyTo(newStream, 4);
        return newStream;
    }

    private static void ValidateDefinition(FlatPatternFeatureModel definition)
    {
        RequireNull(definition.FixedFace, nameof(definition.FixedFace));
        RequireNull(definition.FixedFace2, nameof(definition.FixedFace2));
        RequireNull(definition.ExcludedFaces, nameof(definition.ExcludedFaces));
        RequireNull(definition.AddCornerTrim, nameof(definition.AddCornerTrim));
        RequireNull(definition.BreakCorners, nameof(definition.BreakCorners));

        if (definition.BreakCornerType.HasValue &&
            (definition.BreakCornerType.Value < 0 || definition.BreakCornerType.Value > 1))
            throw Unsupported(nameof(definition.BreakCornerType), definition.BreakCornerType.Value);
        if (definition.CornerTrimReliefType.HasValue &&
            (definition.CornerTrimReliefType.Value < 0 || definition.CornerTrimReliefType.Value > 9))
            throw Unsupported(nameof(definition.CornerTrimReliefType),
                definition.CornerTrimReliefType.Value);
        ValidatePositive(definition.BreakCornerRadius, nameof(definition.BreakCornerRadius), 0.1);
        ValidatePositive(definition.CornerTrimReliefDistance,
            nameof(definition.CornerTrimReliefDistance), 1000);
        ValidatePositive(definition.CornerTrimRatioToThickness,
            nameof(definition.CornerTrimRatioToThickness), 1000);
        if (definition.CornerTrimReliefDistance.HasValue &&
            definition.CornerTrimRatioToThickness.HasValue &&
            definition.CornerTrimReliefDistance.Value !=
            definition.CornerTrimRatioToThickness.Value)
            throw new NotSupportedException(
                "0x66 flat-pattern serialization stores one shared distance/ratio double; " +
                "CornerTrimReliefDistance and CornerTrimRatioToThickness must be equal. " +
                "No stream changes have been made.");
    }

    private static void ValidatePositive(double? value, string property, double max)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            value.Value <= 0 || value.Value > max)
            throw Unsupported(property, value.Value);
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null) throw Unsupported(property, value);
    }

    private static NotSupportedException Unsupported(string property, object value) =>
        new($"0x66 flat-pattern authoring does not support {property}={value}. " +
            "No stream changes have been made.");

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/HemAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured hem subsystems for the two controlled combinations
/// (SheetMetalFormingOptions/sheetmetal_hem_open and sheetmetal_hem_closed,
/// ResolvedFeatures version 0x73).
/// <para>
/// The prefix is the base-flange stream before the <c>moHem_c</c> feature tag
/// (0x4D35). The suffix starts at the feature tag and contains the hem
/// folder/TypeId 0x67, the selection block, all hem dimensions, the hem edge
/// chain and the downstream flat-pattern objects. Prefix + matching suffix
/// reconstructs each native sample byte-for-byte. Each suffix is gzip/base64
/// encoded for source readability and hash-verified before use.
/// </para>
/// </summary>
internal static class HemAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x4D35;
    internal const ushort MeasuredFolderCount = 23;
    internal const ushort SupportedStreamVersion = 0x73;

    internal const string OpenPrefixSha256 =
        "D98033C40448265399D5034B0C7819B75A5672A22C6E0ECBF3EB8BADB7319C92";
    internal const string ClosedPrefixSha256 =
        "F0896F254AA6E702A8032D5A494D0EA8009DD4C85C8132920F45B629D93A6AC5";
    internal const string OpenSuffixSha256 =
        "70DBA046BBCDCCD1ECCE67700B68E0BA94C34BF2D80B21E338150292F3D38DD0";
    internal const string ClosedSuffixSha256 =
        "6E64CB72CD2478D3565127AF741824FDCF239A17E0AE4C189BE6F7AE7482782C";

    internal const string OpenDisplayName = "Hem_open";
    internal const string ClosedDisplayName = "Hem_closed";

    /// <summary>Measured native values shared by both templates.</summary>
    internal const double MeasuredLength = 0.005;
    internal const double MeasuredRadius = 0.001;
    internal const double MeasuredAngle = 3.1415926535898;
    internal const double MeasuredMiterGap = 0.0005;
    internal const double OpenGapDistance = 0.0005;
    internal const double ClosedGapDistance = 0.0001;

    private static readonly byte[] OpenSuffixGzipBase64 =
        SheetMetalAuthoringSegments.Hem0;

    private static readonly byte[] ClosedSuffixGzipBase64 =
        SheetMetalAuthoringSegments.Hem1;

    internal const double TenMillimetreLength = 0.010;
    internal const double OneMillimetreGap = 0.001;

    internal const string Length10PrefixSha256 =
        "DBC72C7B4DDDBD7E1D7D9694E32E6D5307D0FFB00A7CC96C5BECA3523F5A1E6C";
    internal const string Length10SuffixSha256 =
        "DB1178D3987FC29D2804FAA5D0FEE268B772E0B27AB80CA83E2C0E5D746E4825";
    internal const string Gap1PrefixSha256 =
        "1622D3D57DC712843F8AA2214A9C48B90A7573984600392C3DEBA4F4BC41C4E5";
    internal const string Gap1SuffixSha256 =
        "E21731278606243C69E295BA4C787FAD4D8E565886D3E498A5DCC84F2085DCAB";

    private static readonly byte[] Length10SuffixGzipBase64 =
        SheetMetalAuthoringSegments.Hem2;

    private static readonly byte[] Gap1SuffixGzipBase64 =
        SheetMetalAuthoringSegments.Hem3;
    internal const double OneMillimetreMiterGap = 0.001;

    internal const string ClosedLength10PrefixSha256 =
        "B3C597251ED20D7BFA3359BD26D29B8BE324583EC1DD5C907851959679994EBC";
    internal const string ClosedLength10SuffixSha256 =
        "BF20DD17A3D70CD6ADEE51CE2696D52F420EA9F77ED9176B83D55F3CC992A613";
    internal const string OpenMiterGap1PrefixSha256 =
        "3C5188BA2069C492270D045973AD7D9ACF24CCF0C4DAAE2CF42DB8CAC057F7A3";
    internal const string OpenMiterGap1SuffixSha256 =
        "5B99521818C7F7CEA8C6D69E8545039AAF0F9AD17948A88ED67E64C0FBF53FAE";

    private static readonly byte[] ClosedLength10SuffixGzipBase64 =
        SheetMetalAuthoringSegments.Hem4;

    private static readonly byte[] OpenMiterGap1SuffixGzipBase64 =
        SheetMetalAuthoringSegments.Hem5;
    private static readonly Lazy<byte[]> OpenSuffix =
        new(() => Decompress(OpenSuffixGzipBase64, OpenSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> ClosedSuffix =
        new(() => Decompress(ClosedSuffixGzipBase64, ClosedSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> Length10Suffix =
        new(() => Decompress(Length10SuffixGzipBase64, Length10SuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> Gap1Suffix =
        new(() => Decompress(Gap1SuffixGzipBase64, Gap1SuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> ClosedLength10Suffix =
        new(() => Decompress(ClosedLength10SuffixGzipBase64, ClosedLength10SuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> OpenMiterGap1Suffix =
        new(() => Decompress(OpenMiterGap1SuffixGzipBase64, OpenMiterGap1SuffixSha256), isThreadSafe: true);

    internal const int TemplateOpenBaseline = 0;
    internal const int TemplateClosedBaseline = 1;
    internal const int TemplateOpenLength10 = 2;
    internal const int TemplateOpenGap1 = 3;
    internal const int TemplateClosedLength10 = 4;
    internal const int TemplateOpenMiterGap1 = 5;

    /// <summary>
    /// Resolves the measured template. Only the combinations present in the
    /// controlled samples are accepted; null means the template default.
    /// </summary>
    internal static bool TryGetTemplate(
        int type, double? length, double? gap, double? miterGap, out int template)
    {
        double l = length ?? MeasuredLength;
        double g = gap ?? GetGapDistance(type);
        double m = miterGap ?? MeasuredMiterGap;
        if (type == 0 && l == MeasuredLength && g == OpenGapDistance && m == MeasuredMiterGap)
            template = TemplateOpenBaseline;
        else if (type == 1 && l == MeasuredLength && g == ClosedGapDistance && m == MeasuredMiterGap)
            template = TemplateClosedBaseline;
        else if (type == 0 && l == TenMillimetreLength && g == OpenGapDistance && m == MeasuredMiterGap)
            template = TemplateOpenLength10;
        else if (type == 0 && l == MeasuredLength && g == OneMillimetreGap && m == MeasuredMiterGap)
            template = TemplateOpenGap1;
        else if (type == 1 && l == TenMillimetreLength && g == ClosedGapDistance && m == MeasuredMiterGap)
            template = TemplateClosedLength10;
        else if (type == 0 && l == MeasuredLength && g == OpenGapDistance && m == OneMillimetreMiterGap)
            template = TemplateOpenMiterGap1;
        else
        {
            template = -1;
            return false;
        }
        return true;
    }

    internal static byte[] GetTemplateSuffix(int template)
    {
        return template switch
        {
            TemplateOpenBaseline => (byte[])OpenSuffix.Value.Clone(),
            TemplateClosedBaseline => (byte[])ClosedSuffix.Value.Clone(),
            TemplateOpenLength10 => (byte[])Length10Suffix.Value.Clone(),
            TemplateOpenGap1 => (byte[])Gap1Suffix.Value.Clone(),
            TemplateClosedLength10 => (byte[])ClosedLength10Suffix.Value.Clone(),
            TemplateOpenMiterGap1 => (byte[])OpenMiterGap1Suffix.Value.Clone(),
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };
    }

    internal static string GetTemplatePrefixSha256(int template)
    {
        return template switch
        {
            TemplateOpenBaseline => OpenPrefixSha256,
            TemplateClosedBaseline => ClosedPrefixSha256,
            TemplateOpenLength10 => Length10PrefixSha256,
            TemplateOpenGap1 => Gap1PrefixSha256,
            TemplateClosedLength10 => ClosedLength10PrefixSha256,
            TemplateOpenMiterGap1 => OpenMiterGap1PrefixSha256,
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };
    }

    internal static byte[] GetSuffix(int type)
    {
        return type switch
        {
            0 => (byte[])OpenSuffix.Value.Clone(),
            1 => (byte[])ClosedSuffix.Value.Clone(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    internal static string GetDisplayName(int type)
        => type == 0 ? OpenDisplayName : type == 1 ? ClosedDisplayName
            : throw new ArgumentOutOfRangeException(nameof(type));

    internal static string GetPrefixSha256(int type)
        => type == 0 ? OpenPrefixSha256 : type == 1 ? ClosedPrefixSha256
            : throw new ArgumentOutOfRangeException(nameof(type));

    internal static double GetGapDistance(int type)
        => type == 0 ? OpenGapDistance : type == 1 ? ClosedGapDistance
            : throw new ArgumentOutOfRangeException(nameof(type));

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured hem suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/HemFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed-combination hem authoring for the two controlled native specimens
/// (SheetMetalFormingOptions/sheetmetal_hem_open and sheetmetal_hem_closed).
/// <para>
/// The base prefix (before the moHem_c feature tag, 0x4D35) is hash-pinned per
/// template, and the matching measured suffix reconstructs the native stream
/// byte-for-byte. Only Type 0 (Open) and Type 1 (Closed) are accepted; teardrop,
/// rolled and double hems have no sample and are rejected. Length, gap, radius,
/// angle, miter gap, edge count and the three constant flags may be null or must
/// equal the measured template values; they are not patched, because no sample
/// varies them independently from Type.
/// </para>
/// </summary>
internal static class HemFeatureBuilder
{
    internal static void ValidateDefinition(HemFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.Type.HasValue || (def.Type.Value != 0 && def.Type.Value != 1))
        {
            throw new NotSupportedException(
                $"Hem authoring currently supports only Type=0 (Open) and Type=1 (Closed) " +
                $"from the controlled open/closed samples; received " +
                $"{def.Type?.ToString() ?? "null"}. Teardrop/rolled/double hems have no sample. " +
                "No stream changes have been made.");
        }

        int type = def.Type.Value;
        if (!HemAuthoringSegments.TryGetTemplate(
                type, def.Length, def.GapDistance, def.MiterGap, out _))
        {
            throw new NotSupportedException(
                $"Hem authoring supports only the measured combinations: " +
                $"Type=0 Length=0.005 Gap=0.0005 MiterGap=0.0005; " +
                $"Type=0 Length=0.010 Gap=0.0005 MiterGap=0.0005; " +
                $"Type=0 Length=0.005 Gap=0.001 MiterGap=0.0005; " +
                $"Type=0 Length=0.005 Gap=0.0005 MiterGap=0.001; " +
                $"Type=1 Length=0.005 Gap=0.0001 MiterGap=0.0005; and " +
                $"Type=1 Length=0.010 Gap=0.0001 MiterGap=0.0005. Received Type={type}, " +
                $"Length={def.Length?.ToString() ?? "default"}, " +
                $"GapDistance={def.GapDistance?.ToString() ?? "default"}, " +
                $"MiterGap={def.MiterGap?.ToString() ?? "default"}. No stream changes have been made.");
        }
        RequireNullOrClose(def.Angle, HemAuthoringSegments.MeasuredAngle, nameof(def.Angle));
        RequireNullOrClose(def.Radius, HemAuthoringSegments.MeasuredRadius, nameof(def.Radius));

        RequireNullOrEquals(def.BendPosition, 0, nameof(def.BendPosition));
        RequireNullOrEquals(def.ReverseDirection, false, nameof(def.ReverseDirection));
        RequireNullOrEquals(def.UseDefaultBendAllowance, true, nameof(def.UseDefaultBendAllowance));
        RequireNullOrEquals(def.EdgeCount, 1, nameof(def.EdgeCount));

        RequireNull(def.Edges, nameof(def.Edges));
        RequireNull(def.CustomBendAllowance, nameof(def.CustomBendAllowance));
    }

    internal static byte[] Build(
        byte[] currentStream, HemFeatureModel def, string? featureName)
    {
        ValidateDefinition(def);
        int type = def.Type!.Value;
        HemAuthoringSegments.TryGetTemplate(
            type, def.Length, def.GapDistance, def.MiterGap, out int template);
        string expectedName = HemAuthoringSegments.GetDisplayName(type);

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, expectedName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Hem authoring uses the measured display name '{expectedName}' for Type={type}; " +
                $"changing it to '{featureName}' would not match the measured block. " +
                "No stream changes have been made.");
        }

        if (currentStream == null || currentStream.Length != HemAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Hem authoring requires the stripped base prefix of the controlled hem specimen " +
                $"({HemAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != HemAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Hem authoring is only supported for ResolvedFeatures streams version " +
                $"0x{HemAuthoringSegments.SupportedStreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }

        string prefixHash = Sha256Hex(currentStream);
        string expectedPrefix = HemAuthoringSegments.GetTemplatePrefixSha256(template);
        if (!string.Equals(prefixHash, expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Hem authoring Type={type} Length={def.Length?.ToString() ?? "default"} " +
                $"GapDistance={def.GapDistance?.ToString() ?? "default"} " +
                $"MiterGap={def.MiterGap?.ToString() ?? "default"} requires the matching controlled " +
                $"stripped base specimen: prefix SHA-256 {prefixHash.Substring(0, 12)} does not match the " +
                $"measured signature {expectedPrefix.Substring(0, 12)}. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) != HemAuthoringSegments.MeasuredFolderCount)
        {
            throw new NotSupportedException(
                $"Hem authoring requires the measured folder count " +
                $"{HemAuthoringSegments.MeasuredFolderCount}. No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moHem_c") != 0)
        {
            throw new NotSupportedException(
                "Hem authoring requires a stripped base without an existing hem feature. " +
                "No stream changes have been made.");
        }

        byte[] suffix = HemAuthoringSegments.GetTemplateSuffix(template);
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        return newStream;
    }

    private static void RequireNullOrClose(double? value, double measured, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            Math.Abs(value.Value - measured) > 1e-12)
        {
            throw new NotSupportedException(
                $"Hem authoring does not yet support {property}={value.Value:R}. " +
                $"The measured template value is {measured:R}. No stream changes have been made.");
        }
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Hem authoring does not yet support {property}={value.Value}. " +
                $"The measured template value is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Hem authoring does not yet support {property}. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/LoftedBendAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured fixed LoftedBend block from the controlled 0x72 specimen
/// (LoftedBendOptions/lofted_bend_2lines.SLDPRT, version 0x74).
/// <para>
/// The prefix runs to the moLoftedBend_c class tag (0x6043) and keeps the base
/// flange and profile context. The suffix starts at that tag and runs to the
/// stream end. Prefix + suffix reconstructs the native stream byte-for-byte.
/// The normalized prefix hash zeroes the folder count at offset 4.
/// </para>
/// </summary>
internal static class LoftedBendAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x6043;
    internal const ushort SupportedStreamVersion = 0x74;
    internal const ushort MeasuredFullFolderCount = 26;
    internal const int MarkerOffset = 0x6033;
    internal const uint MeasuredMarkerValue = 19;
    internal const string MeasuredDisplayName = "LoftedBend";

    internal const double MeasuredThickness = 0.001;
    internal const int MeasuredNumberOfBendLines = 2;

    internal const string MeasuredNormalizedPrefixSha256 =
        "A94F196BD56D6F0014146C5F527C501600AE3EE6D66CD9B8DFC58ABFD5B597DD";
    internal const string MeasuredSuffixSha256 =
        "FB76CA2461DF51899797C161FD607703583E359AB293E2CA02B6437A2A1499D3";

    private static readonly byte[] SuffixGzipBytes =
        SheetMetalAuthoringSegments.LoftedBend0;

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBytes, MeasuredSuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured LoftedBend suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/LoftedBendFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured lofted-bend authoring for the controlled non-formed 0x72
/// specimen (LoftedBendOptions/lofted_bend_2lines.SLDPRT, version 0x74).
/// <para>
/// The stripped base keeps the sheet-metal body and the two line profiles; the
/// measured suffix starts at the moLoftedBend_c tag (0x6043). Only the measured
/// combination is accepted: Thickness 0.001 m, NumberOfBendLines 2 and the two
/// fixed line profiles. Every other API value may be null (unresolved) or equal
/// the measured create snapshot value; other values, persistent profile
/// references, custom names, unsupported stream versions and non-matching bases
/// are rejected before the stream is touched.
/// </para>
/// </summary>
internal static class LoftedBendFeatureBuilder
{
    internal const string DefaultFeatureName = LoftedBendAuthoringSegments.MeasuredDisplayName;

    private const double MeasuredMaximumDeviation = 0.0005;
    private const double MeasuredFacetValue = 0.001;
    private const int MeasuredProfileCount = 2;

    internal static void ValidateDefinition(LoftedBendFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        RequireNullOrClose(def.Thickness, LoftedBendAuthoringSegments.MeasuredThickness,
            nameof(def.Thickness));
        RequireNullOrEquals(def.NumberOfBendLines,
            LoftedBendAuthoringSegments.MeasuredNumberOfBendLines, nameof(def.NumberOfBendLines));

        // The two line profiles are fixed in the stripped prefix. A caller cannot
        // substitute other profiles, and the measured layout has exactly two.
        RequireNull(def.Profiles, nameof(def.Profiles));
        RequireNullOrEquals(def.ProfileCount, MeasuredProfileCount, nameof(def.ProfileCount));

        // Values below are constant in the measured create snapshot. They are not
        // patched (their serialized slots are unresolved), so only the measured
        // value can be accepted; any other value would silently not take effect.
        RequireNullOrEquals(def.Direction, false, nameof(def.Direction));
        RequireNullOrEquals(def.BendLineControlOption, 0, nameof(def.BendLineControlOption));
        RequireNullOrEquals(def.ReferToEndPoint, false, nameof(def.ReferToEndPoint));
        RequireNullOrEquals(def.FacetingOption, 0, nameof(def.FacetingOption));
        RequireNullOrEquals(def.FormedMethod, false, nameof(def.FormedMethod));
        RequireNullOrEquals(def.SymmetricThickness, false, nameof(def.SymmetricThickness));
        RequireNullOrClose(def.MaximumDeviation, MeasuredMaximumDeviation,
            nameof(def.MaximumDeviation));
        RequireNullOrClose(def.FacetValue, MeasuredFacetValue, nameof(def.FacetValue));
    }

    internal static byte[] Build(
        byte[] currentStream, LoftedBendFeatureModel def, string? featureName)
    {
        ValidateDefinition(def);

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? DefaultFeatureName
            : featureName!;
        if (!string.Equals(resolvedName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; received '{resolvedName}'. No stream changes have been made.");
        }

        if (currentStream == null ||
            currentStream.Length != LoftedBendAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring requires the stripped base prefix of the controlled specimen " +
                $"({LoftedBendAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != LoftedBendAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring is only supported for ResolvedFeatures streams version " +
                $"0x{LoftedBendAuthoringSegments.SupportedStreamVersion:X2}, but the document uses " +
                $"0x{version:X2}. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) !=
            LoftedBendAuthoringSegments.MeasuredFullFolderCount)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring requires the measured folder count " +
                $"{LoftedBendAuthoringSegments.MeasuredFullFolderCount}. No stream changes have been made.");
        }

        int marker = LoftedBendAuthoringSegments.MarkerOffset;
        if (marker < 0 || marker + 8 > currentStream.Length ||
            currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) !=
                LoftedBendAuthoringSegments.MeasuredMarkerValue)
        {
            throw new NotSupportedException(
                "Lofted-bend authoring requires the measured 0x67/marker block at " +
                $"0x{marker:X}. No stream changes have been made.");
        }

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string normalizedHash = Sha256Hex(normalized);
        if (!string.Equals(normalizedHash,
                LoftedBendAuthoringSegments.MeasuredNormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring requires the controlled stripped base: normalized prefix " +
                $"SHA-256 {normalizedHash.Substring(0, 12)} does not match the measured signature. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, LoftedBendFeatureModel.SerializedClassName) != 0)
        {
            throw new NotSupportedException(
                "Lofted-bend authoring requires a stripped base without an existing lofted bend. " +
                "No stream changes have been made.");
        }

        byte[] suffix = LoftedBendAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        BitConverter.GetBytes(LoftedBendAuthoringSegments.MeasuredFullFolderCount)
            .CopyTo(newStream, 4);
        BitConverter.GetBytes(LoftedBendAuthoringSegments.MeasuredMarkerValue)
            .CopyTo(newStream, marker + 4);
        return newStream;
    }

    private static void RequireNullOrClose(double? value, double measured, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            Math.Abs(value.Value - measured) > 1e-12)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring does not support {property}={value.Value:R}. " +
                $"The measured template value is {measured:R}. No stream changes have been made.");
        }
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring does not support {property}={value.Value}. " +
                $"The measured template value is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring does not support {property}. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalFormingParsers.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict parsers for Task 2.3 / 2.4 sheet metal forming features, powered by
/// <see cref="FeatureWindowContext"/> (<see cref="FeatureParameterBag"/> + <see cref="FeatureSelectionBag"/>).
/// </summary>
internal static class SheetMetalFormingParsers
{
    internal const string HemClassName = HemFeatureModel.SerializedClassName;
    internal const string JogClassName = JogFeatureModel.SerializedClassName;
    internal const string LoftedBendClassName = LoftedBendFeatureModel.SerializedClassName;
    internal const string NormalCutClassName = NormalCutFeatureModel.SerializedClassName;
    internal const string FlatPatternClassName = FlatPatternFeatureModel.SerializedClassName;
    internal const string FoldClassName = FoldFeatureModel.SerializedClassName;
    internal const string UnFoldClassName = UnFoldFeatureModel.SerializedClassName;

    internal const ushort HemStreamVersion = 0x73;
    private const int HemTypeOffsetFromGapValue = 0x6E;

    internal static string HemDisplayName(int type) => HemAuthoringSegments.GetDisplayName(type);

    internal static bool IsHemNode(MfcElement element)
    {
        if (element == null || element.ClassName != HemClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == HemFeatureModel.ObservedSerializedTypeId;
    }

    public static IFeatureData? ParseHem(MfcElement element)
    {
        if (!IsHemNode(element))
            return null;
        return null;
    }

    internal static IFeatureData? ParseHem(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null || !IsHemNode(element)) return null;
        windowEnd = Math.Min(doc.SourceData.Length, windowEnd);
        if (!FeatureWindowContext.TryCreate(doc, element.TagOffset, windowEnd, HemStreamVersion, out var ctx) || ctx == null)
            return null;

        if (!ctx.Selections.TryReadSingleEdgeCount(out int edgeCount) || edgeCount != 1)
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D4", out double angle, out int d4Name) ||
            !SheetMetalTemplateParser.PositiveFinite(angle) || angle > Math.PI + 1e-9)
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D7", out double length, out int d7Name) ||
            !SheetMetalTemplateParser.PositiveFinite(length))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D1", out double radius, out _, d4Name, d7Name) ||
            !SheetMetalTemplateParser.PositiveFinite(radius))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D8", out double gap, out int d8Name, d7Name, windowEnd) ||
            !SheetMetalTemplateParser.PositiveFinite(gap))
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D9", out double miterGap, out _, d7Name, windowEnd) ||
            !SheetMetalTemplateParser.PositiveFinite(miterGap))
            return null;

        byte[] raw = ctx.RawData;
        int typeSlot = d8Name + 0x1E + HemTypeOffsetFromGapValue;
        if (typeSlot < 0 || typeSlot + 2 > raw.Length) return null;
        ushort typeValue = BitConverter.ToUInt16(raw, typeSlot);
        if (typeValue > 1) return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D1", out double halfGap, out _, element.TagOffset, d4Name) ||
            !SheetMetalTemplateParser.PositiveFinite(halfGap) ||
            Math.Abs(halfGap * 2.0 - gap) > 1e-12)
            return null;

        var data = new HemFeatureModel
        {
            Type = typeValue,
            Length = length,
            GapDistance = gap,
            Angle = angle,
            Radius = radius,
            MiterGap = miterGap,
            EdgeCount = edgeCount
        };
        data.ParameterResolution.Type = true;
        data.ParameterResolution.Length = true;
        data.ParameterResolution.GapDistance = true;
        data.ParameterResolution.Angle = true;
        data.ParameterResolution.Radius = true;
        data.ParameterResolution.MiterGap = true;
        data.ParameterResolution.EdgeCount = true;
        return data;
    }

    public static IFeatureData? ParseJog(MfcElement element)
    {
        if (element == null || element.ClassName != JogClassName || element.TagByte != 0x01)
            return null;
        return null;
    }

    private static readonly string[] FeatureClassPrefixes =
    {
        "moBreakCornerFeat", "moFlatPattern", "moFold_", "moUnFold_", "moJog_",
        "moSMBaseFlange", "moMirrorPattern", "moLPattern", "moCirPattern",
        "moEdgeFlange", "moHem_", "moLoftedBend", "moExtru", "moRevolution",
        "moCut", "moChamfer", "Fillet_", "moShell", "moDeleteBody", "moDraft"
    };

    private static int FindNextFeatureTag(MfcDocument doc, int afterOffset, int windowEnd)
    {
        int best = windowEnd;
        foreach (var e in doc.AllElements)
        {
            if (e.TagOffset <= afterOffset || e.TagOffset >= best || e.Depth != 0) continue;
            string name = e.ClassName;
            if (string.IsNullOrEmpty(name)) continue;
            foreach (string prefix in FeatureClassPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    best = e.TagOffset;
                    break;
                }
            }
        }
        return best;
    }

    internal static bool IsJogNode(MfcElement element)
    {
        if (element == null || element.ClassName != JogClassName || element.TagByte != 0x01)
            return false;
        return ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId == 0x159;
    }

    internal static IFeatureData? ParseJog(MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null || !IsJogNode(element)) return null;
        windowEnd = Math.Min(windowEnd, doc.SourceData.Length);
        if (!FeatureWindowContext.TryCreate(doc, element.TagOffset, windowEnd, expectedStreamVersion: 0x1AA, out var ctx) || ctx == null)
            return null;

        int localEnd = FindNextFeatureTag(doc, element.TagOffset, windowEnd);
        int numericEnd = Math.Min(localEnd, element.TagOffset + 0x1000);

        if (!ctx.Parameters.TryGetFirst("D1", out double radius, out _, element.TagOffset, numericEnd) ||
            radius <= 0 || radius > 1.0)
            return null;

        if (!ctx.Parameters.TryGetFirst("D4", out double angle, out _, element.TagOffset, numericEnd) ||
            angle <= 0 || angle > Math.PI + 1e-9)
            return null;

        var data = new JogFeatureModel
        {
            TypeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId,
            BendRadius = radius,
            JogAngle = angle
        };
        data.ParameterResolution.BendRadius = true;
        data.ParameterResolution.JogAngle = true;

        if (ctx.Parameters.TryGetFirst("D3", out _, out int d3Name, element.TagOffset, localEnd) &&
            ctx.Parameters.TryGetFirst("D2", out double offset, out _, d3Name + 1, localEnd) &&
            offset > 0 && offset <= 1.0)
        {
            data.OffsetDistance = offset;
            data.ParameterResolution.OffsetDistance = true;
        }

        TryReadJogFlags(ctx.RawData, element.TagOffset, windowEnd, data);
        return data;
    }

    private static readonly byte[] JogDefaultFlagsAnchor =
    {
        0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00,
        0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00,
        0, 0, 0, 0, 0, 0, 0, 0
    };

    private static readonly byte[] JogOffsetFlagsAnchor =
        { 0x02, 0xFE, 0xB5, 0x4C, 0x94, 0x07, 0xA3, 0x3F };

    private static void TryReadJogFlags(
        byte[] raw, int tag, int windowEnd, JogFeatureModel data)
    {
        windowEnd = Math.Min(windowEnd, raw.Length);

        int a = FindPattern(raw, JogDefaultFlagsAnchor, tag, Math.Min(windowEnd, tag + 0x6000));
        if (a >= 0 && a + 0x55 < raw.Length)
        {
            byte allow = raw[a + 0x18];
            byte radius = raw[a + 0x1C];
            byte reverse = raw[a + 0x48];
            byte dimension = raw[a + 0x54];
            if (allow <= 1)
            {
                data.UseDefaultBendAllowance = allow != 0;
                data.ParameterResolution.UseDefaultBendAllowance = true;
            }
            if (radius <= 1)
            {
                data.UseDefaultBendRadius = radius != 0;
                data.ParameterResolution.UseDefaultBendRadius = true;
            }
            if (reverse <= 1)
            {
                data.ReverseDirection = reverse != 0;
                data.ParameterResolution.ReverseDirection = true;
            }
            if (dimension == 1 || dimension == 2)
            {
                data.DimensionPositionType = dimension == 1 ? 2 : 1;
                data.ParameterResolution.DimensionPositionType = true;
            }
        }

        int b = FindPattern(raw, JogOffsetFlagsAnchor,
            tag + 0x6000, Math.Min(windowEnd, tag + 0x7000));
        if (b >= 9 && b + 30 < raw.Length)
        {
            byte offsetType = raw[b - 9];
            byte fixProjected = raw[b + 26];
            byte reverseOffset = raw[b + 30];
            if (offsetType == 0 || offsetType == 3)
            {
                data.OffsetType = offsetType == 0 ? 1 : 2;
                data.ParameterResolution.OffsetType = true;
            }
            if (fixProjected <= 1)
            {
                data.FixProjectedLength = fixProjected != 0;
                data.ParameterResolution.FixProjectedLength = true;
            }
            if (reverseOffset <= 1)
            {
                data.ReverseOffset = reverseOffset != 0;
                data.ParameterResolution.ReverseOffset = true;
            }
        }

        int c = FindPattern(raw, new byte[] { 0x24, 0x82 },
            tag + 0x5A00, Math.Min(windowEnd, tag + 0x5C00));
        if (c >= 4 && raw[c - 4] <= 1)
        {
            data.JogPositionType = raw[c - 4] + 1;
            data.ParameterResolution.JogPositionType = true;
        }
    }

    private static int FindPattern(byte[] raw, byte[] pattern, int start, int end)
    {
        start = Math.Max(0, start);
        end = Math.Min(end, raw.Length);
        for (int p = start; p <= end - pattern.Length; p++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (raw[p + j] != pattern[j]) { match = false; break; }
            }
            if (match) return p;
        }
        return -1;
    }

    public static IFeatureData? ParseLoftedBend(MfcElement element)
    {
        if (!IsLoftedBendNode(element))
            return null;
        return null;
    }

    internal static bool IsLoftedBendNode(MfcElement element)
    {
        if (element == null || element.ClassName != LoftedBendClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == LoftedBendFeatureModel.ObservedSerializedTypeId;
    }

    internal static IFeatureData? ParseLoftedBend(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null || !IsLoftedBendNode(element)) return null;
        windowEnd = Math.Min(doc.SourceData.Length, windowEnd);
        if (!FeatureWindowContext.TryCreate(doc, element.TagOffset, windowEnd, expectedStreamVersion: 0x74, out var ctx) || ctx == null)
            return null;

        if (!ctx.Parameters.TryGetPositiveConsistent("D14", out double bendLines, out int d14Name) ||
            bendLines < 1.0 || bendLines > 1000.0 ||
            Math.Abs(bendLines - Math.Round(bendLines)) > 1e-9)
            return null;

        byte[] raw = ctx.RawData;
        int gate1 = d14Name - 0xBC5;
        int gate2 = d14Name - 0xBBD;
        int thicknessSlot = d14Name - 0xBB4;
        int companionSlot = d14Name - 0xBAC;
        if (gate1 < 0 || thicknessSlot < 0 || companionSlot + 8 > raw.Length) return null;
        if (BitConverter.ToDouble(raw, gate1) != 1.0 ||
            BitConverter.ToDouble(raw, gate2) != 1.0)
            return null;
        double thickness = BitConverter.ToDouble(raw, thicknessSlot);
        double companion = BitConverter.ToDouble(raw, companionSlot);
        if (!SheetMetalTemplateParser.PositiveFinite(thickness) ||
            !SheetMetalTemplateParser.PositiveFinite(companion))
            return null;

        var data = new LoftedBendFeatureModel
        {
            Thickness = thickness,
            NumberOfBendLines = checked((int)Math.Round(bendLines))
        };
        data.ParameterResolution.Thickness = true;
        data.ParameterResolution.NumberOfBendLines = true;
        return data;
    }

    public static IFeatureData? ParseNormalCut(MfcElement element)
    {
        if (element == null || element.ClassName != NormalCutClassName || element.TagByte != 0x01)
            return null;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (header.TypeId != 0x87) return null;
        return new NormalCutFeatureModel { TypeId = 0x87 };
    }

    internal static bool IsFlatPatternNode(MfcElement element)
    {
        if (element == null || element.ClassName != FlatPatternClassName || element.TagByte != 0x01)
            return false;
        uint typeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
        return typeId == FlatPatternFeatureModel.LegacySerializedTypeId ||
               typeId == FlatPatternFeatureModel.CurrentSerializedTypeId ||
               typeId == 0x72 || typeId == 0x1E || typeId == 0x164 ||
               typeId == 0x66;
    }

    public static IFeatureData? ParseFlatPattern(MfcElement element)
    {
        if (!IsFlatPatternNode(element))
            return null;
        return null;
    }

    internal static IFeatureData? ParseFlatPattern(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version == 0x155 || version == 0x1AA || version == 0xC7)
            return ParseTutorialFlatPattern(doc, element, windowEnd);
        if (version == 0x74)
        {
            uint inlineType = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
            if (inlineType == 0x66)
                return ParseInlineFlatPattern066(doc, element);
        }
        if (version != 0x73 && version != 0x74) return null;
        if (!IsFlatPatternNode(element)) return null;

        windowEnd = Math.Min(raw.Length, windowEnd);
        if (!FeatureWindowContext.TryCreate(doc, element.TagOffset, windowEnd, expectedStreamVersion: null, out var ctx) || ctx == null)
            return null;

        if (!ctx.Selections.TryReadSingleFaceEntityReference(out var fixedFace, out var faceRefElement) ||
            fixedFace == null || faceRefElement == null)
            return null;

        var data = new FlatPatternFeatureModel
        {
            TypeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId,
            FixedFace = fixedFace
        };
        data.ParameterResolution.FixedFace = true;

        if (!TryReadFlatPatternOptions(raw, faceRefElement.DataEnd, data))
            return null;
        return data;
    }

    internal static IFeatureData? ParseTutorialFlatPattern(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null || element.ClassName != FlatPatternClassName ||
            element.TagByte != 0x01)
            return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(raw, 0);
        uint expectedType = version switch
        {
            0x155 => 0x72,
            0x1AA => 0x1E,
            0xC7 => 0x164,
            _ => 0
        };
        if (expectedType == 0) return null;
        uint typeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
        if (typeId != expectedType) return null;

        var data = new FlatPatternFeatureModel { TypeId = typeId };
        if (!TryFindTutorialFlatOptionBlock(raw, out int anchor)) return data;
        if (anchor - 0x0E < 0 || anchor + 0x35 > raw.Length) return data;

        byte flags = raw[anchor - 0x0E];
        uint trimType = BitConverter.ToUInt32(raw, anchor + 0x08);
        double trim = BitConverter.ToDouble(raw, anchor + 0x0C);
        uint breakType = BitConverter.ToUInt32(raw, anchor + 0x14);
        double breakRadius = BitConverter.ToDouble(raw, anchor + 0x18);
        byte useRatio = raw[anchor + 0x24];
        byte simplify = raw[anchor + 0x28];
        byte slit = raw[anchor + 0x2C];
        if (trimType > 9 || breakType > 1 || useRatio > 1 ||
            simplify > 1 || slit > 1 || trim <= 0 || trim > 0.1 ||
            breakRadius <= 0 || breakRadius > 0.1)
            return data;

        if (flags <= 3)
        {
            data.MergeFace = (flags & 1) != 0;
            data.CornerTreatment = (flags & 2) == 0;
            data.ParameterResolution.MergeFace = true;
            data.ParameterResolution.CornerTreatment = true;
        }
        data.CornerTrimReliefType = checked((int)trimType);
        data.CornerTrimReliefDistance = trim;
        data.CornerTrimRatioToThickness = trim;
        data.BreakCornerType = checked((int)breakType);
        data.BreakCornerRadius = breakRadius;
        data.UseRatioToThickness = useRatio != 0;
        data.SimplifyBends = simplify != 0;
        data.ShowSlitInCornerRelief = slit != 0;

        data.ParameterResolution.CornerTrimReliefType = true;
        data.ParameterResolution.CornerTrimReliefDistance = true;
        data.ParameterResolution.CornerTrimRatioToThickness = true;
        data.ParameterResolution.BreakCornerType = true;
        data.ParameterResolution.BreakCornerRadius = true;
        data.ParameterResolution.UseRatioToThickness = true;
        data.ParameterResolution.SimplifyBends = true;
        data.ParameterResolution.ShowSlitInCornerRelief = true;
        return data;
    }

    private const int Inline066SuffixLength = 3900;
    private const int Inline066FlagsOffset = 0xDF7;
    private const int Inline066TrimTypeOffset = 0xE0D;
    private const int Inline066TrimOffset = 0xE11;
    private const int Inline066BreakTypeOffset = 0xE19;
    private const int Inline066BreakRadiusOffset = 0xE1D;
    private const int Inline066UseRatioOffset = 0xE29;
    private const int Inline066SimplifyOffset = 0xE2D;
    private const int Inline066SlitOffset = 0xE31;

    private static IFeatureData? ParseInlineFlatPattern066(MfcDocument doc, MfcElement element)
    {
        byte[] raw = doc.SourceData;
        if (element == null || element.ClassName != FlatPatternClassName ||
            element.TagByte != 0x01) return null;
        if (ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId != 0x66) return null;
        if (element.TagOffset < 0 ||
            raw.Length - element.TagOffset != Inline066SuffixLength) return null;

        int tag = element.TagOffset;
        byte flags = raw[tag + Inline066FlagsOffset];
        if (flags > 3) return null;
        uint trimType = BitConverter.ToUInt32(raw, tag + Inline066TrimTypeOffset);
        if (trimType > 9) return null;
        double trim = BitConverter.ToDouble(raw, tag + Inline066TrimOffset);
        if (double.IsNaN(trim) || double.IsInfinity(trim) || trim <= 0 || trim > 1000) return null;
        uint breakType = BitConverter.ToUInt32(raw, tag + Inline066BreakTypeOffset);
        if (breakType > 1) return null;
        double breakRadius = BitConverter.ToDouble(raw, tag + Inline066BreakRadiusOffset);
        if (double.IsNaN(breakRadius) || double.IsInfinity(breakRadius) ||
            breakRadius <= 0 || breakRadius > 0.1) return null;
        byte useRatio = raw[tag + Inline066UseRatioOffset];
        byte simplify = raw[tag + Inline066SimplifyOffset];
        byte slit = raw[tag + Inline066SlitOffset];
        if (useRatio > 1 || simplify > 1 || slit > 1) return null;

        var data = new FlatPatternFeatureModel
        {
            TypeId = 0x66,
            MergeFace = (flags & 1) != 0,
            CornerTreatment = (flags & 2) == 0,
            SimplifyBends = simplify != 0,
            BreakCornerType = checked((int)breakType),
            BreakCornerRadius = breakRadius,
            CornerTrimReliefType = checked((int)trimType),
            CornerTrimReliefDistance = trim,
            CornerTrimRatioToThickness = trim,
            UseRatioToThickness = useRatio != 0,
            ShowSlitInCornerRelief = slit != 0
        };
        data.ParameterResolution.MergeFace = true;
        data.ParameterResolution.CornerTreatment = true;
        data.ParameterResolution.SimplifyBends = true;
        data.ParameterResolution.BreakCornerType = true;
        data.ParameterResolution.BreakCornerRadius = true;
        data.ParameterResolution.CornerTrimReliefType = true;
        data.ParameterResolution.CornerTrimReliefDistance = true;
        data.ParameterResolution.CornerTrimRatioToThickness = true;
        data.ParameterResolution.UseRatioToThickness = true;
        data.ParameterResolution.ShowSlitInCornerRelief = true;
        return data;
    }

    private static bool TryFindTutorialFlatOptionBlock(byte[] raw, out int anchor)
    {
        anchor = -1;
        int count = 0;
        for (int p = 0x0E; p <= raw.Length - 0x36; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFF ||
                raw[p + 2] != 0xFF || raw[p + 3] != 0xFF)
                continue;
            bool zeros = true;
            for (int j = 4; j <= 7; j++)
                if (raw[p + j] != 0) { zeros = false; break; }
            if (!zeros) continue;
            if (BitConverter.ToUInt32(raw, p + 0x08) > 9) continue;
            double trim = BitConverter.ToDouble(raw, p + 0x0C);
            if (trim <= 0 || trim > 0.1) continue;
            if (BitConverter.ToUInt32(raw, p + 0x14) > 1) continue;
            double breakRadius = BitConverter.ToDouble(raw, p + 0x18);
            if (breakRadius <= 0 || breakRadius > 0.1) continue;
            if (raw[p + 0x24] > 1 || raw[p + 0x28] > 1 || raw[p + 0x2C] > 1) continue;
            if (raw[p + 0x32] != 0x2B || raw[p + 0x33] != 0x80 || raw[p + 0x34] != 0x02)
                continue;
            if (count == 0) anchor = p;
            count++;
            if (count > 1) { anchor = -1; return false; }
        }
        return count == 1;
    }

    private const int FlatOptionFlags = 0x21;
    private const int FlatOptionTrimType = 0x37;
    private const int FlatOptionTrimDistance = 0x3B;
    private const int FlatOptionBreakType = 0x43;
    private const int FlatOptionBreakRadius = 0x47;
    private const int FlatOptionSimplifyBends = 0x57;

    private static bool TryReadFlatPatternOptions(byte[] raw, int anchor, FlatPatternFeatureModel data)
    {
        if (anchor < 0 || anchor + 0x64 > raw.Length) return false;
        if (raw[anchor] != 0xFF || raw[anchor + 1] != 0xFF) return false;
        if (BitConverter.ToUInt32(raw, anchor + 0x16) != 0x4A38u) return false;
        if (BitConverter.ToUInt32(raw, anchor + 0x1C) != 1u) return false;
        if (raw[anchor + 0x2B] != 1 || raw[anchor + 0x5B] != 1) return false;

        byte flags = raw[anchor + FlatOptionFlags];
        if (flags > 3) return false;
        byte simplify = raw[anchor + FlatOptionSimplifyBends];
        if (simplify > 1) return false;

        uint breakType = BitConverter.ToUInt32(raw, anchor + FlatOptionBreakType);
        if (breakType > 1) return false;
        double breakRadius = BitConverter.ToDouble(raw, anchor + FlatOptionBreakRadius);
        if (!SheetMetalTemplateParser.PositiveFinite(breakRadius)) return false;

        uint trimType = BitConverter.ToUInt32(raw, anchor + FlatOptionTrimType);
        if (trimType > 9) return false;
        double trimDistance = BitConverter.ToDouble(raw, anchor + FlatOptionTrimDistance);
        if (!SheetMetalTemplateParser.PositiveFinite(trimDistance)) return false;

        data.MergeFace = (flags & 1) != 0;
        data.CornerTreatment = (flags & 2) == 0;
        data.SimplifyBends = simplify != 0;
        data.BreakCornerType = checked((int)breakType);
        data.BreakCornerRadius = breakRadius;
        data.CornerTrimReliefType = checked((int)trimType);
        data.CornerTrimReliefDistance = trimDistance;

        data.ParameterResolution.MergeFace = true;
        data.ParameterResolution.CornerTreatment = true;
        data.ParameterResolution.SimplifyBends = true;
        data.ParameterResolution.BreakCornerType = true;
        data.ParameterResolution.BreakCornerRadius = true;
        data.ParameterResolution.CornerTrimReliefType = true;
        data.ParameterResolution.CornerTrimReliefDistance = true;
        return true;
    }

    public static IFeatureData? ParseFold(MfcElement element)
    {
        if (element == null || element.ClassName != FoldClassName || element.TagByte != 0x01)
            return null;
        return null;
    }

    internal static IFeatureData? ParseFold(MfcDocument doc, MfcElement element, int windowEnd)
    {
        var data = ParseFolds(doc, element, windowEnd, isUnfold: false);
        if (data == null) return null;
        var result = new FoldFeatureModel
        {
            TypeId = data.Value.TypeId,
            BendsCount = data.Value.BendsCount
        };
        result.ParameterResolution.BendsCount = true;
        return result;
    }

    internal static IFeatureData? ParseUnFold(MfcDocument doc, MfcElement element, int windowEnd)
    {
        var data = ParseFolds(doc, element, windowEnd, isUnfold: true);
        if (data == null) return null;
        var result = new UnFoldFeatureModel
        {
            TypeId = data.Value.TypeId,
            BendsCount = data.Value.BendsCount
        };
        result.ParameterResolution.BendsCount = true;
        return result;
    }

    private static (uint TypeId, int BendsCount)? ParseFolds(
        MfcDocument doc, MfcElement element, int windowEnd, bool isUnfold)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x155 && version != 0x1AA && version != 0xC7) return null;
        string className = isUnfold ? UnFoldClassName : FoldClassName;
        if (element.ClassName != className || element.TagByte != 0x01) return null;

        uint typeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
        if (typeId == 0) return null;

        int bends = CountBendNames(raw, element.TagOffset);
        if (bends <= 0)
        {
            foreach (var e in doc.AllElements)
            {
                if (e.TagOffset >= element.TagOffset) continue;
                if (e.ClassName == "moOneBend_c" || e.ClassName == "moSM3dBend_c") bends++;
            }
        }
        if (bends <= 0 || bends > 1000) return null;
        return (typeId, bends);
    }

    private static int CountBendNames(byte[] raw, int beforeOffset)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        int end = Math.Min(beforeOffset, raw.Length);
        for (int p = 0; p <= end - 6; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF) continue;
            int chars = raw[p + 3];
            if (chars < 8 || chars > 40 || p + 4 + chars * 2 > end) continue;
            string text = System.Text.Encoding.Unicode.GetString(raw, p + 4, chars * 2);
            if (text.StartsWith("EdgeBend", StringComparison.Ordinal) ||
                text.StartsWith("SketchBend", StringComparison.Ordinal))
                names.Add(text);
        }
        return names.Count;
    }

    public static IFeatureData? ParseUnFold(MfcElement element)
    {
        if (element == null || element.ClassName != UnFoldClassName || element.TagByte != 0x01)
            return null;
        return null;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SMNormalCutAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed template for the first controlled cross-bend SMNormalCut sample
/// (ResolvedFeatures 0x75, FolderHeader TypeId 0x87). The suffix begins at the
/// moSMNormalCut_c tag and includes the following flat-pattern data.
/// </summary>
internal static class SMNormalCutAuthoringSegments
{
    internal const string Name = "SMNormalCutCrossBend";
    internal const string ClassName = "moSMNormalCut_c";
    internal const string DisplayName = "SMNormalCut";
    internal const int PrefixLength = 0xE81B;      // 59419
    internal const ushort StreamVersion = 0x75;
    internal const ushort FolderCount = 27;
    internal const int SuffixLength = 39509;
    internal const string NormalizedPrefixSha256 =
        "A1DD786FE4DB68D877D9A40BADB864FFBFAEDB5EDEA64ADB848A97BB7B819485";
    internal const string SuffixSha256 =
        "7F4A90224E8633351EE37C40ED75E45D1FD9729B44FB36810A8DCDB5512492F3";

    private static readonly byte[] SuffixGzipBytes = SheetMetalAuthoringSegments.NormalCut0;

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBytes, SuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (bytes.Length != SuffixLength)
            throw new InvalidOperationException("Measured SMNormalCut suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured SMNormalCut suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SMNormalCutFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed authoring for the controlled cross-bend SMNormalCut sample
/// (ResolvedFeatures 0x75, TypeId 0x87). The measured suffix from the
/// moSMNormalCut_c tag to the stream end is appended verbatim; no parameter is
/// patched because only one native sample exists.
/// </summary>
internal static class SMNormalCutFeatureBuilder
{
    internal static byte[] Build(byte[] currentStream, string? featureName)
    {
        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? SMNormalCutAuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, SMNormalCutAuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"SMNormalCut template authoring supports only the measured display name " +
                $"'{SMNormalCutAuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null ||
            currentStream.Length != SMNormalCutAuthoringSegments.PrefixLength)
            throw new NotSupportedException(
                $"SMNormalCut template authoring requires the stripped base " +
                $"({SMNormalCutAuthoringSegments.PrefixLength} bytes, ending at the " +
                $"{SMNormalCutAuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != SMNormalCutAuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"SMNormalCut template authoring supports only ResolvedFeatures version " +
                $"0x{SMNormalCutAuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != SMNormalCutAuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"SMNormalCut template authoring requires folder count " +
                $"{SMNormalCutAuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, SMNormalCutAuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "SMNormalCut template authoring requires a stripped base without an existing " +
                "SMNormalCut feature. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, SMNormalCutAuthoringSegments.NormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "SMNormalCut template authoring requires the matching stripped base: normalized " +
                $"prefix SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{SMNormalCutAuthoringSegments.NormalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] suffix = SMNormalCutAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(SMNormalCutAuthoringSegments.FolderCount).CopyTo(newStream, 4);
        return newStream;
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/TutorialBendTableAuthoringBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed-template authoring for the tutorial BaseFlange custom bend table.
/// The path is replaced in-place with a same-length file name (measured path is
/// 98 characters); the rest of the measured BaseFlange suffix is appended
/// verbatim. Native reopen reports the patched path from both
/// ISheetMetalFeatureData and IBaseFlangeFeatureData.
/// </summary>
internal static class TutorialBendTableAuthoringBuilder
{
    internal static byte[] Build(
        byte[] currentStream, string bendTableFile, string? featureName)
    {
        if (string.IsNullOrWhiteSpace(bendTableFile))
            throw new ArgumentException("A bend table path is required.", nameof(bendTableFile));
        if (bendTableFile.Length != TutorialBendTableAuthoringSegments.MeasuredPathLength)
            throw new NotSupportedException(
                $"Tutorial bend table authoring requires a same-length path " +
                $"({TutorialBendTableAuthoringSegments.MeasuredPathLength} characters), but received " +
                $"{bendTableFile.Length}. No stream changes have been made.");
        if (!bendTableFile.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) &&
            !bendTableFile.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !bendTableFile.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Tutorial bend table authoring requires a .xls/.xlsx/.csv path. " +
                "No stream changes have been made.");

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? TutorialBendTableAuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, TutorialBendTableAuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Tutorial bend table authoring supports only the measured display name " +
                $"'{TutorialBendTableAuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null ||
            currentStream.Length != TutorialBendTableAuthoringSegments.PrefixLength)
            throw new NotSupportedException(
                $"Tutorial bend table authoring requires the stripped base of Paper Airplane.SLDPRT " +
                $"({TutorialBendTableAuthoringSegments.PrefixLength} bytes, ending at the " +
                $"{TutorialBendTableAuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != TutorialBendTableAuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"Tutorial bend table authoring supports only ResolvedFeatures version " +
                $"0x{TutorialBendTableAuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != TutorialBendTableAuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"Tutorial bend table authoring requires folder count " +
                $"{TutorialBendTableAuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, TutorialBendTableAuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "Tutorial bend table authoring requires a stripped base without an existing base " +
                "flange. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, TutorialBendTableAuthoringSegments.NormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Tutorial bend table authoring requires the matching stripped base: normalized " +
                $"prefix SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{TutorialBendTableAuthoringSegments.NormalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] suffix = TutorialBendTableAuthoringSegments.GetSuffix();
        byte[] prefix = (byte[])currentStream.Clone();
        byte[] oldBytes = Encoding.Unicode.GetBytes(
            TutorialBendTableAuthoringSegments.MeasuredBendTablePath);
        byte[] newBytes = Encoding.Unicode.GetBytes(bendTableFile);
        int replaced = ReplacePath(prefix, oldBytes, newBytes) +
                       ReplacePath(suffix, oldBytes, newBytes);
        if (replaced == 0)
            throw new InvalidOperationException(
                "Measured tutorial bend table suffix does not contain the measured path.");

        byte[] newStream = new byte[prefix.Length + suffix.Length];
        Array.Copy(prefix, 0, newStream, 0, prefix.Length);
        Array.Copy(suffix, 0, newStream, prefix.Length, suffix.Length);
        BitConverter.GetBytes(TutorialBendTableAuthoringSegments.FolderCount).CopyTo(newStream, 4);
        return newStream;
    }

    private static int ReplacePath(byte[] data, byte[] oldBytes, byte[] newBytes)
    {
        int replaced = 0;
        for (int p = 0; p <= data.Length - oldBytes.Length; p++)
        {
            bool match = true;
            for (int j = 0; j < oldBytes.Length; j++)
                if (data[p + j] != oldBytes[j]) { match = false; break; }
            if (!match) continue;
            Array.Copy(newBytes, 0, data, p, newBytes.Length);
            replaced++;
            p += oldBytes.Length - 1;
        }
        return replaced;
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/TutorialBendTableAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed template for the Paper Airplane tutorial BaseFlange that carries the
/// measured custom bend table path. The stored path is replaced in-place by a
/// same-length file name so no serialized length changes; native reopen reports
/// the new SheetMetal/BaseFlange BendTableFile.
/// </summary>
internal static class TutorialBendTableAuthoringSegments
{
    internal const string Name = "TutorialBaseFlangeBendTable";
    internal const string ClassName = "moSMBaseFlange_c";
    internal const string DisplayName = "Base-Flange1";
    internal const int PrefixLength = 0x630F;      // 25359
    internal const ushort StreamVersion = 0x155;
    internal const ushort FolderCount = 38;
    internal const int SuffixLength = 177527;
    internal const string NormalizedPrefixSha256 =
        "7FF7312196A91274A8E24F03A437522DD67C55A2225866ACBB39A2F205619D7D";
    internal const string SuffixSha256 =
        "920F1529FFDC17C054F901B8AE21D59BE2DEF48E04192EF8272E16A0967FB627";
    internal const string MeasuredBendTablePath =
        @"C:\Program Files\SolidWorks2012\SolidWorks\lang\english\Sheetmetal Bend Tables\base bend table.xls";
    internal const int MeasuredPathLength = 98;

    private static readonly byte[] SuffixGzipBytes = SheetMetalAuthoringSegments.TutorialBendTable0;

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBytes, SuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (bytes.Length != SuffixLength)
            throw new InvalidOperationException("Measured tutorial bend table suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured tutorial bend table suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/TutorialFlatPatternAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured authoring templates for the tutorial FlatPattern layouts
/// (ResolvedFeatures versions 0x155, 0x1AA and 0xC7). Each template is the
/// exact source stream prefix ending at the moFlatPattern_c tag plus the
/// measured suffix from that tag to the stream end. The prefix and suffix
/// reconstruct the native tutorial ResolvedFeatures stream byte-for-byte.
/// </summary>
internal static class TutorialFlatPatternAuthoringSegments
{
    internal enum Template
    {
        PaperAirplane,
        ComplexPart,
        DriveSled
    }

    internal sealed class TemplateInfo
    {
        internal string Name { get; }
        internal string ClassName { get; }
        internal string DisplayName { get; }
        internal int PrefixLength { get; }
        internal ushort StreamVersion { get; }
        internal ushort FolderCount { get; }
        internal string NormalizedPrefixSha256 { get; }
        internal string SuffixSha256 { get; }

        internal TemplateInfo(string name, string className, string displayName,
            int prefixLength, ushort streamVersion, ushort folderCount,
            string normalizedPrefixSha256, string suffixSha256)
        {
            Name = name;
            ClassName = className;
            DisplayName = displayName;
            PrefixLength = prefixLength;
            StreamVersion = streamVersion;
            FolderCount = folderCount;
            NormalizedPrefixSha256 = normalizedPrefixSha256;
            SuffixSha256 = suffixSha256;
        }
    }

    private static readonly byte[] PaperSuffixGzipBytes = SheetMetalAuthoringSegments.TutorialFlatPattern0;
    private static readonly byte[] ComplexSuffixGzipBytes = SheetMetalAuthoringSegments.TutorialFlatPattern1;
    private static readonly byte[] DriveSuffixGzipBytes = SheetMetalAuthoringSegments.TutorialFlatPattern2;

    private static readonly Lazy<byte[]> PaperSuffix =
        new(() => Decompress(PaperSuffixGzipBytes,
            "14C54B6BB9D380923600871BCB663ECE2C65F4EE5661A31A623923F0FC0BF2FE"), isThreadSafe: true);
    private static readonly Lazy<byte[]> ComplexSuffix =
        new(() => Decompress(ComplexSuffixGzipBytes,
            "25B22AC1226F5429EE2145456B5FAFC758E7DC23C5DACA70362634AEB6DE21F3"), isThreadSafe: true);
    private static readonly Lazy<byte[]> DriveSuffix =
        new(() => Decompress(DriveSuffixGzipBytes,
            "17343CD839E070A6E73AB5281E7E3CC2DF5D2A84E5B0123C437A07E9D695EDA4"), isThreadSafe: true);

    internal static TemplateInfo GetInfo(Template template) => template switch
    {
        Template.PaperAirplane => new TemplateInfo(
            "FlatPatternPaperAirplane", "moFlatPattern_c", "Flat-Pattern2",
            0x2EE35, 0x155, 38,
            "02D59D33F0D5AF5139BC150B0BC85CF4314C7973840D0BF01610B6A71531AF6E",
            "14C54B6BB9D380923600871BCB663ECE2C65F4EE5661A31A623923F0FC0BF2FE"),
        Template.ComplexPart => new TemplateInfo(
            "FlatPatternComplexPart", "moFlatPattern_c", "Flat-Pattern1",
            0x7E9E1, 0x1AA, 50,
            "155A49D97577E924D7758A7D251375FD65E5033BAAC7291ADCA5457133ECEC6F",
            "25B22AC1226F5429EE2145456B5FAFC758E7DC23C5DACA70362634AEB6DE21F3"),
        Template.DriveSled => new TemplateInfo(
            "FlatPatternDriveSled", "moFlatPattern_c", "Flat-Pattern1",
            0xCD0F9, 0xC7, 76,
            "D06A01B875F10EAF968A2D6AD50A10D2988F92D2BF02434F5B89390F0DE8186D",
            "17343CD839E070A6E73AB5281E7E3CC2DF5D2A84E5B0123C437A07E9D695EDA4"),
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    internal static byte[] GetSuffix(Template template) => (byte[])(template switch
    {
        Template.PaperAirplane => PaperSuffix.Value,
        Template.ComplexPart => ComplexSuffix.Value,
        Template.DriveSled => DriveSuffix.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    }).Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured tutorial FlatPattern suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/TutorialSheetMetalAuthoringSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured authoring templates for the first real Jog/Fold/UnFold samples
/// found in the SolidWorks 2026 tutorial corpus (2026-10-31 scan).
/// <para>
/// Each template is the exact stripped base of the source document up to the
/// feature tag plus the measured suffix from that tag to the stream end. Prefix
/// and suffix reconstruct the native ResolvedFeatures stream byte-for-byte.
/// The tutorial generations are much newer than Task 2.3 (Jog: version 0x1AA,
/// folder count 50, Jog1; Paper Airplane Fold/UnFold: version 0x155, folder
/// count 38, Fold2/Unfold2). Drive sled (version 0xC7) is read-only for now.
/// </para>
/// </summary>
internal static class TutorialSheetMetalAuthoringSegments
{
    internal enum Template
    {
        JogComplexPart,
        UnFoldPaperAirplane,
        FoldPaperAirplane
    }

    internal sealed class TemplateInfo
    {
        internal string Name { get; }
        internal string ClassName { get; }
        internal string DisplayName { get; }
        internal int PrefixLength { get; }
        internal ushort StreamVersion { get; }
        internal ushort FolderCount { get; }
        internal string NormalizedPrefixSha256 { get; }
        internal string SuffixSha256 { get; }
        internal TemplateInfo(string name, string className, string displayName,
            int prefixLength, ushort streamVersion, ushort folderCount,
            string normalizedPrefixSha256, string suffixSha256)
        {
            Name = name;
            ClassName = className;
            DisplayName = displayName;
            PrefixLength = prefixLength;
            StreamVersion = streamVersion;
            FolderCount = folderCount;
            NormalizedPrefixSha256 = normalizedPrefixSha256;
            SuffixSha256 = suffixSha256;
        }
    }

    private static readonly byte[] JogSuffixGzipBytes = SheetMetalAuthoringSegments.TutorialSheetMetal0;
    private static readonly byte[] UnFoldPaperSuffixGzipBytes = SheetMetalAuthoringSegments.TutorialSheetMetal1;
    private static readonly byte[] FoldPaperSuffixGzipBytes = SheetMetalAuthoringSegments.TutorialSheetMetal2;

    private static readonly Lazy<byte[]> JogSuffix =
        new(() => Decompress(JogSuffixGzipBytes,
            "D0F37877650260C356848824174C82AFE5E5D2721A701A8EBD6F376DB8A9DE98"), isThreadSafe: true);
    private static readonly Lazy<byte[]> UnFoldPaperSuffix =
        new(() => Decompress(UnFoldPaperSuffixGzipBytes,
            "BA4C4588BFB0631BFC90339E18AE4F08647901BDFA8D3F790F097A11E06396A9"), isThreadSafe: true);
    private static readonly Lazy<byte[]> FoldPaperSuffix =
        new(() => Decompress(FoldPaperSuffixGzipBytes,
            "3D8D17639C09D0650F196D9747C12706E1D7849AD23E82F2D10C2A7C71DD5533"), isThreadSafe: true);

    internal static TemplateInfo GetInfo(Template template) => template switch
    {
        Template.JogComplexPart => new TemplateInfo(
            "JogComplexPart", "moJog_c", "Jog1",
            0x5B7D3, 0x1AA, 50,
            "B89225BB50C4BD4615AB264BBCCBC403F0590B4101F1248283DFC584045D2146",
            "D0F37877650260C356848824174C82AFE5E5D2721A701A8EBD6F376DB8A9DE98"),
        Template.UnFoldPaperAirplane => new TemplateInfo(
            "UnFoldPaperAirplane", "moUnFold_c", "Unfold2",
            0x197F6, 0x155, 38,
            "94502E1C3467FD86AF5B5BE748BC7CD251E2AF0E36183AE256EEDEFAEDD9F8E8",
            "BA4C4588BFB0631BFC90339E18AE4F08647901BDFA8D3F790F097A11E06396A9"),
        Template.FoldPaperAirplane => new TemplateInfo(
            "FoldPaperAirplane", "moFold_c", "Fold2",
            0x1F914, 0x155, 38,
            "CB94D046B6C9B3714EF926145E7B05CF6003032C8BCED3EEA34DBFC5C3BAD75D",
            "3D8D17639C09D0650F196D9747C12706E1D7849AD23E82F2D10C2A7C71DD5533"),
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    internal static byte[] GetSuffix(Template template) => (byte[])(template switch
    {
        Template.JogComplexPart => JogSuffix.Value,
        Template.UnFoldPaperAirplane => UnFoldPaperSuffix.Value,
        Template.FoldPaperAirplane => FoldPaperSuffix.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    }).Clone();

    private static byte[] Decompress(byte[] base64, string expectedSha256)
    {
        byte[] gzip = base64;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured tutorial feature suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/TutorialSheetMetalFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured authoring for the tutorial Jog/Fold/UnFold samples and the
/// newer FlatPattern layouts (2026-10-31 corpus scan). The stripped base must
/// end at the feature tag and match the template's length, stream version,
/// folder count and normalized prefix SHA-256; the measured suffix is appended
/// verbatim, so the authored ResolvedFeatures stream is byte-identical to the
/// native tutorial document. Custom names, another template's base and
/// non-stripped documents are rejected before the stream is modified.
/// </summary>
internal static class TutorialSheetMetalFeatureBuilder
{
    internal static TutorialSheetMetalAuthoringSegments.Template ToSegmentTemplate(
        TutorialSheetMetalFeatureTemplate template) => template switch
    {
        TutorialSheetMetalFeatureTemplate.JogComplexPart =>
            TutorialSheetMetalAuthoringSegments.Template.JogComplexPart,
        TutorialSheetMetalFeatureTemplate.UnFoldPaperAirplane =>
            TutorialSheetMetalAuthoringSegments.Template.UnFoldPaperAirplane,
        TutorialSheetMetalFeatureTemplate.FoldPaperAirplane =>
            TutorialSheetMetalAuthoringSegments.Template.FoldPaperAirplane,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    internal static byte[] Build(
        byte[] currentStream,
        TutorialSheetMetalFeatureTemplate template,
        string? featureName)
    {
        var segmentTemplate = ToSegmentTemplate(template);
        var info = TutorialSheetMetalAuthoringSegments.GetInfo(segmentTemplate);
        return BuildMeasured(currentStream, info.Name, info.ClassName, info.DisplayName,
            info.PrefixLength, info.StreamVersion, info.FolderCount,
            info.NormalizedPrefixSha256,
            TutorialSheetMetalAuthoringSegments.GetSuffix(segmentTemplate), featureName);
    }

    internal static byte[] BuildFlatPattern(
        byte[] currentStream,
        TutorialSheetMetalFeatureTemplate template,
        string? featureName)
    {
        var segmentTemplate = ToFlatPatternSegmentTemplate(template);
        var info = TutorialFlatPatternAuthoringSegments.GetInfo(segmentTemplate);
        return BuildMeasured(currentStream, info.Name, info.ClassName, info.DisplayName,
            info.PrefixLength, info.StreamVersion, info.FolderCount,
            info.NormalizedPrefixSha256,
            TutorialFlatPatternAuthoringSegments.GetSuffix(segmentTemplate), featureName);
    }

    internal static TutorialFlatPatternAuthoringSegments.Template ToFlatPatternSegmentTemplate(
        TutorialSheetMetalFeatureTemplate template) => template switch
    {
        TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane =>
            TutorialFlatPatternAuthoringSegments.Template.PaperAirplane,
        TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart =>
            TutorialFlatPatternAuthoringSegments.Template.ComplexPart,
        TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled =>
            TutorialFlatPatternAuthoringSegments.Template.DriveSled,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static byte[] BuildMeasured(
        byte[] currentStream,
        string templateName,
        string className,
        string displayName,
        int prefixLength,
        ushort streamVersion,
        ushort folderCount,
        string normalizedPrefixSha256,
        byte[] suffix,
        string? featureName)
    {
        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? displayName
            : featureName!;
        if (!string.Equals(resolvedName, displayName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' supports only the measured display " +
                $"name '{displayName}'; received '{resolvedName}'. No stream changes have been made.");

        if (currentStream == null || currentStream.Length != prefixLength)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires the stripped base of the " +
                $"matching tutorial document ({prefixLength} bytes, ending at the " +
                $"{className} tag), but received {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != streamVersion)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' is only supported for ResolvedFeatures " +
                $"version 0x{streamVersion:X}, but the document uses 0x{version:X}. " +
                "No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != folderCount)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires folder count {folderCount}. " +
                "No stream changes have been made.");

        if (CountOccurrences(currentStream, className) != 0)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires a stripped base without an " +
                $"existing {className} feature. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string normalizedHash = Sha256Hex(normalized);
        if (!string.Equals(normalizedHash, normalizedPrefixSha256, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires the matching stripped base: " +
                $"normalized prefix SHA-256 {normalizedHash.Substring(0, 12)} does not match the " +
                $"measured signature {normalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(folderCount).CopyTo(newStream, 4);
        return newStream;
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}


