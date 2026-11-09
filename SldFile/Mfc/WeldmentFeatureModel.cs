using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

// Phase 5 weldment/structure feature definition shapes.

/// <summary>Per-field evidence flags for <see cref="StructuralMemberFeatureModel"/>.</summary>
public sealed class StructuralMemberFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool AllowProtrusion { get; internal set; }
    public bool ApplyCornerTreatment { get; internal set; }
    public bool ConfigurationName { get; internal set; }
    public bool ConnectedSegmentsOption { get; internal set; }
    public bool ConnectionTypes { get; internal set; }
    public bool CornerTreatmentType { get; internal set; }
    public bool Groups { get; internal set; }
    public bool LibraryProfileMaterial { get; internal set; }
    public bool LocateProfilePoint { get; internal set; }
    public bool PathSegments { get; internal set; }
    public bool RotationAngle { get; internal set; }
    public bool TransferMaterial { get; internal set; }
    public bool WeldmentProfilePath { get; internal set; }
    public bool MirrorProfile { get; internal set; }
}

/// <summary>
/// Structural-member definition shape (<c>IStructuralMemberFeatureData</c>,
/// feature types <c>WeldMemberFeat</c>/<c>WeldmentFeature</c>).
/// Implements IMfcFeatureModel and IMfcSerializable with bi-directional CArchive serialization.
/// </summary>
public sealed class StructuralMemberFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moWeldMemberFeat_c";
    public const uint HeaderTypeId = 0x4Du; // 77 in FolderHeader
    public const uint SwFeatureId = 72u;    // swFeatureNameID_e.swFmWeldMemberFeat

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "结构构件1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }
    public StructuralMemberFeatureParameterResolution Resolution { get; } = new();

    public bool? AllowProtrusion { get; set; }
    public bool? ApplyCornerTreatment { get; set; }
    public string? ConfigurationName { get; set; }
    public int? ConnectedSegmentsOption { get; set; }

    /// <summary>Per-connection native type index (native indexed property). Unset by default.</summary>
    public IReadOnlyList<int>? ConnectionTypes { get; set; }

    public int? CornerTreatmentType { get; set; }

    /// <summary>Opaque structural-member group handles (native <c>StructuralMemberGroup</c> array). Contents not modeled.</summary>
    public IReadOnlyList<EntityReference>? Groups { get; set; }

    public string? LibraryProfileMaterial { get; set; }
    public EntityReference? LocateProfilePoint { get; set; }
    public IReadOnlyList<EntityReference>? PathSegments { get; set; }
    public double? RotationAngle { get; set; }
    public bool? MirrorProfile { get; set; }
    public bool? TransferMaterial { get; set; }
    public string? WeldmentProfilePath { get; set; }

    public int? GroupsCount => Groups?.Count;
    public int? PathSegmentsCount => PathSegments?.Count;
    public int? ConnectionTypesCount => ConnectionTypes?.Count;

    public byte[]? RawBlock { get; set; }

    public StructuralMemberFeatureModel()
    {
    }

    public StructuralMemberFeatureModel(
        string profilePath,
        IEnumerable<EntityReference>? pathSegments = null,
        double rotationAngle = 0.0,
        int cornerTreatmentType = 0)
    {
        FeatureName = "StructuralMember";
        SerializedTypeId = HeaderTypeId;
        WeldmentProfilePath = profilePath;
        if (pathSegments != null)
            PathSegments = pathSegments.ToList().AsReadOnly();
        RotationAngle = rotationAngle;
        CornerTreatmentType = cornerTreatmentType;
        ApplyCornerTreatment = cornerTreatmentType > 0;
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.WeldmentProfilePath = true;
        Resolution.RotationAngle = true;
        Resolution.CornerTreatmentType = true;
        Resolution.ApplyCornerTreatment = true;
        MarkResolved(nameof(WeldmentProfilePath));
        MarkResolved(nameof(RotationAngle));
        MarkResolved(nameof(CornerTreatmentType));
        MarkResolved(nameof(ApplyCornerTreatment));
        if (PathSegments != null && PathSegments.Count > 0)
        {
            Resolution.PathSegments = true;
            MarkResolved(nameof(PathSegments));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "StructuralMember";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Locate Profile Point (moPerLinePtRefPlnData_c)
            bool hasLocate = LocateProfilePoint != null;
            ar.Boolean(ref hasLocate);
            if (hasLocate)
            {
                ushort ptTag = 0; ar.ClassTagOrRef("moPerLinePtRefPlnData_c", ref ptTag);
                ushort ptRefTag = 0; ar.ClassTagOrRef("moPointRef_w", ref ptRefTag);
                ushort skTag = 0; ar.ClassTagOrRef("moCompSketchEntHandle_c", ref skTag);
                ushort skRef = 0x802C; ar.U16(ref skRef);
                ushort skSchema = 0x0002; ar.U16(ref skSchema);
                uint skFlags = 0x00400000; ar.U32(ref skFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint ptId = LocateProfilePoint!.EntityId; ar.U32(ref ptId);
            }

            // 5. Parameters (RotationAngle, CornerTreatment, Mirror, Protrusion)
            double rot = RotationAngle ?? 0.0; ar.F64(ref rot);
            int corner = CornerTreatmentType ?? 0; ar.I32(ref corner);
            bool mirror = MirrorProfile ?? false; ar.Boolean(ref mirror);
            bool protrude = AllowProtrusion ?? false; ar.Boolean(ref protrude);

            // 6. Profile Path WChar (.sldlfp)
            string? path = WeldmentProfilePath ?? string.Empty;
            ar.WChar(ref path);

            // 7. Path Segments (moWeldSegSkIndices_c)
            var segments = PathSegments ?? Array.Empty<EntityReference>();
            ushort segIndicesTag = 0; ar.ClassTagOrRef("moWeldSegSkIndices_c", ref segIndicesTag);
            ushort segCount = (ushort)segments.Count; ar.U16(ref segCount);
            foreach (var seg in segments)
            {
                ushort segIndexTag = 0; ar.ClassTagOrRef("moWeldSegSkIndex_c", ref segIndexTag);
                uint entId = seg.EntityId; ar.U32(ref entId);
                uint one = 1; ar.U32(ref one);
                uint zero2 = 0; ar.U32(ref zero2);
                string? empty1 = string.Empty; ar.WChar(ref empty1);
                string? empty2 = string.Empty; ar.WChar(ref empty2);
                uint zero3 = 0; ar.U32(ref zero3);
                string? empty3 = string.Empty; ar.WChar(ref empty3);
                uint one2 = 1; ar.U32(ref one2);
                uint zero4 = 0; ar.U32(ref zero4);
                uint zero5 = 0; ar.U32(ref zero5);
                string? mat = LibraryProfileMaterial ?? "材质 <未指定>"; ar.WChar(ref mat);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Deserialize Locate Profile Point
                bool hasLocate = false; ar.Boolean(ref hasLocate);
                if (hasLocate && ar.Remaining >= 6)
                {
                    ushort ptTag = 0; ar.ReadClassTag(out string ptCls, out ptTag, out _);
                    if (ptCls == "moPerLinePtRefPlnData_c")
                    {
                        ar.ReadClassTag(out _, out _, out _);
                        ar.ReadClassTag(out _, out _, out _);
                        ushort skRef = 0; ar.U16(ref skRef);
                        ushort skSchema = 0; ar.U16(ref skSchema);
                        uint skFlags = 0; ar.U32(ref skFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint ptId = 0; ar.U32(ref ptId);
                        LocateProfilePoint = new EntityReference { EntityId = ptId, EntityType = SelectionEntityType.Sketch, Name = $"moCompSketchEntHandle_c:{ptId:X8}" };
                        Resolution.LocateProfilePoint = true;
                        MarkResolved(nameof(LocateProfilePoint));
                    }
                }

                // Deserialize Parameters
                if (ar.Remaining >= 8) { double rot = 0; ar.F64(ref rot); RotationAngle = rot; Resolution.RotationAngle = true; MarkResolved(nameof(RotationAngle)); }
                if (ar.Remaining >= 4) { int corner = 0; ar.I32(ref corner); CornerTreatmentType = corner; ApplyCornerTreatment = corner > 0; Resolution.CornerTreatmentType = true; Resolution.ApplyCornerTreatment = true; MarkResolved(nameof(CornerTreatmentType)); MarkResolved(nameof(ApplyCornerTreatment)); }
                if (ar.Remaining >= 1) { bool mirror = false; ar.Boolean(ref mirror); MirrorProfile = mirror; Resolution.MirrorProfile = true; MarkResolved(nameof(MirrorProfile)); }
                if (ar.Remaining >= 1) { bool protrude = false; ar.Boolean(ref protrude); AllowProtrusion = protrude; Resolution.AllowProtrusion = true; MarkResolved(nameof(AllowProtrusion)); }

                // Deserialize Profile Path
                if (ar.Remaining >= 2)
                {
                    string? path = null; ar.WChar(ref path); WeldmentProfilePath = path;
                    Resolution.WeldmentProfilePath = !string.IsNullOrEmpty(path);
                    if (Resolution.WeldmentProfilePath) MarkResolved(nameof(WeldmentProfilePath));
                }

                // Deserialize Path Segments
                if (ar.Remaining >= 2)
                {
                    ushort segIndicesTag = 0;
                    ar.ReadClassTag(out string segIndicesCls, out segIndicesTag, out _);
                    if (segIndicesCls == "moWeldSegSkIndices_c")
                    {
                        ushort count = 0; ar.U16(ref count);
                        var segList = new List<EntityReference>();
                        for (int i = 0; i < count; i++)
                        {
                            if (ar.Remaining < 6) break;
                            ar.ReadClassTag(out _, out _, out _);
                            uint entId = 0; ar.U32(ref entId);
                            uint one = 0; ar.U32(ref one);
                            uint zero2 = 0; ar.U32(ref zero2);
                            string? e1 = null; ar.WChar(ref e1);
                            string? e2 = null; ar.WChar(ref e2);
                            uint zero3 = 0; ar.U32(ref zero3);
                            string? e3 = null; ar.WChar(ref e3);
                            uint one2 = 0; ar.U32(ref one2);
                            uint zero4 = 0; ar.U32(ref zero4);
                            uint zero5 = 0; ar.U32(ref zero5);
                            string? mat = null; ar.WChar(ref mat); LibraryProfileMaterial = mat;
                            segList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Edge, Name = $"moWeldSegSkIndex_c:{entId:X8}" });
                        }
                        PathSegments = segList.AsReadOnly();
                        Resolution.PathSegments = segList.Count > 0;
                        if (Resolution.PathSegments) MarkResolved(nameof(PathSegments));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        var profilePaths = MfcPrimitives.ScanWCharFields(
            data, 0, data.Length, maxStringLength: 512,
            predicate: static s => s.EndsWith(".sldlfp", StringComparison.OrdinalIgnoreCase));
        if (profilePaths.Count > 0)
        {
            WeldmentProfilePath = profilePaths[0].Value;
            Resolution.WeldmentProfilePath = true;
            MarkResolved(nameof(WeldmentProfilePath));
        }

        int segIndicesOffset = MfcPrimitives.FindAscii(data, "moWeldSegSkIndices_c");
        if (segIndicesOffset > 0 && segIndicesOffset + "moWeldSegSkIndices_c".Length + 2 <= data.Length)
        {
            ushort count = BitConverter.ToUInt16(data, segIndicesOffset + "moWeldSegSkIndices_c".Length);
            int segIndexOffset = MfcPrimitives.FindAscii(data, "moWeldSegSkIndex_c");
            if (segIndexOffset > 0 && count > 0 && count < 256)
            {
                int start = segIndexOffset + "moWeldSegSkIndex_c".Length;
                if (start + 4 <= data.Length)
                {
                    uint entId = BitConverter.ToUInt32(data, start);
                    PathSegments = new[] { new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Edge, Name = $"moWeldSegSkIndex_c:{entId:X8}" } };
                    Resolution.PathSegments = true;
                    MarkResolved(nameof(PathSegments));
                }
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    public const string LegacyClassName = "moWeldMemberFeat_c";

    private static readonly Dictionary<(ushort StreamVersion, uint TypeId), (int CornerOffset, int RotationOffset)> LegacyFamilies =
        new()
        {
            [(0x02A6, 0x36)] = (-0xF8, -0xE6),
            [(0x029D, 0x33)] = (-0xFC, -0xEA),
            [(0x0076, 0x17)] = (-0xF8, -0xE6),
            [(0x008F, 0x2A)] = (-0x1AA, -0xAA),
            [(0x0074, 0x159)] = (-0x1AA, -0xAA)
        };

    internal static StructuralMemberFeatureModel? ParseLegacy(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreateWithFolderHeader(
            doc, className, typeId, start, doc?.SourceData.Length ?? end, out var header, requiredClassName: LegacyClassName);
        if (ctx == null || !LegacyFamilies.TryGetValue((ctx.StreamVersion, header.TypeId), out var family))
            return null;

        byte[] data = ctx.RawData;
        var paths = FindProfilePaths(data, start, 2);
        if (paths.Count == 0)
            return null;

        var definition = new StructuralMemberFeatureModel
        {
            SerializedClassName = LegacyClassName,
            SerializedTypeId = header.TypeId,
            WeldmentProfilePath = paths[0].Value
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.WeldmentProfilePath = true;

        int? secondPathOffset = paths.Count >= 2 ? paths[1].PrefixOffset : (int?)null;
        var contours = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moWeldmentContours_c" && t.TagOffset > start);

        int? groupCount = null;
        if (contours == null)
        {
            groupCount = 0;
        }
        else if (secondPathOffset.HasValue && contours.TagOffset < secondPathOffset.Value)
        {
            int dataStart = contours.DataStart;
            if (dataStart >= 0 && dataStart + 4 <= data.Length)
            {
                uint count = BitConverter.ToUInt32(data, dataStart);
                if (count <= 64)
                    groupCount = (int)count;
            }
        }
        else if (secondPathOffset.HasValue)
        {
            groupCount = 0;
        }

        if (groupCount.HasValue)
        {
            var groups = new EntityReference[groupCount.Value];
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = new EntityReference
                {
                    EntityType = SelectionEntityType.None,
                    EntityId = 0,
                    Name = $"StructuralMemberGroup{i + 1}"
                };
            }
            definition.Groups = Array.AsReadOnly(groups);
            definition.Resolution.Groups = true;
        }

        int suffix = paths[0].SuffixOffset;
        int cornerOffset = suffix + family.CornerOffset;
        if (cornerOffset >= 0 && cornerOffset + 4 <= data.Length)
        {
            uint cornerValue = BitConverter.ToUInt32(data, cornerOffset);
            if (cornerValue <= 2)
            {
                definition.ApplyCornerTreatment = cornerValue != 0;
                definition.CornerTreatmentType = (int)cornerValue;
                definition.Resolution.ApplyCornerTreatment = true;
                definition.Resolution.CornerTreatmentType = true;
            }
        }

        if (ctx.Parameters.TryReadVerifiedDoubleSlots(
                suffix,
                new[] { family.RotationOffset },
                out double rotation,
                minExclusive: -Math.PI * 2 - 1e-9,
                maxInclusive: Math.PI * 2 + 1e-9))
        {
            definition.RotationAngle = rotation;
            definition.Resolution.RotationAngle = true;
        }

        return definition;
    }

    private static List<(int PrefixOffset, int SuffixOffset, string Value)> FindProfilePaths(
        byte[] data, int start, int max)
    {
        var paths = new List<(int PrefixOffset, int SuffixOffset, string Value)>();
        foreach (var field in MfcPrimitives.ScanWCharFields(
                     data,
                     start,
                     data.Length,
                     maxStringLength: 512,
                     predicate: static s => s.EndsWith(".sldlfp", StringComparison.OrdinalIgnoreCase)))
        {
            if (paths.Any(p => string.Equals(p.Value, field.Value, StringComparison.OrdinalIgnoreCase)))
                continue;
            int stringStart = field.EndOffset - field.Value.Length * 2;
            int suffixOffset = stringStart + (field.Value.Length - 6) * 2;
            if (stringStart < 0 || suffixOffset < 0 || suffixOffset + 6 * 2 > data.Length)
                continue;
            paths.Add((field.Offset, suffixOffset, field.Value));
            if (paths.Count >= max)
                break;
        }
        return paths;
    }

    internal static IFeatureData? Parse(MfcElement? element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x70,
            requireEmptyWCharAt10: true,
            allowTwoByteHeaderPadding: true);
        if (ctx != null)
        {
            byte[] data = ctx.RawData;
            var profilePaths = MfcPrimitives.ScanWCharFields(
                    data,
                    ctx.Start,
                    data.Length,
                    maxStringLength: 512,
                    predicate: static s => s.IndexOf(".sldlfp", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(static f => f.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (profilePaths.Count == 1 && profilePaths[0].EndsWith(".sldlfp", StringComparison.OrdinalIgnoreCase))
            {
                string profilePath = profilePaths[0];

                var indices = ctx.Nodes.FirstOrDefault(t =>
                    t.ClassName == "moWeldSegSkIndices_c" && t.TagOffset > ctx.Start);
                if (indices != null && indices.DataStart + 2 <= data.Length)
                {
                    ushort segmentCount = BitConverter.ToUInt16(data, indices.DataStart);
                    if (segmentCount > 0 && segmentCount <= 256)
                    {
                        var indexElements = ctx.Nodes
                            .Where(t => t.ClassName == "moWeldSegSkIndex_c" && t.TagOffset > indices.TagOffset)
                            .Take(segmentCount)
                            .ToList();
                        if (indexElements.Count == segmentCount)
                        {
                            var segments = new EntityReference[segmentCount];
                            bool segsValid = true;
                            for (int i = 0; i < segmentCount; i++)
                            {
                                int start = indexElements[i].DataStart;
                                if (start < 0 || start + 4 > data.Length)
                                {
                                    segsValid = false;
                                    break;
                                }
                                uint entityId = BitConverter.ToUInt32(data, start);
                                if (entityId == 0)
                                {
                                    segsValid = false;
                                    break;
                                }
                                segments[i] = new EntityReference
                                {
                                    EntityType = SelectionEntityType.Edge,
                                    EntityId = entityId,
                                    Name = $"moWeldSegSkIndex_c:{entityId:X8}"
                                };
                            }

                            if (segsValid)
                            {
                                EntityReference? locatePoint = null;
                                var pointPlane = ctx.Nodes.FirstOrDefault(t =>
                                    t.ClassName == "moPerLinePtRefPlnData_c" && t.TagOffset > ctx.Start);
                                var sketchHandle = pointPlane == null ? null : ctx.Nodes.FirstOrDefault(t =>
                                    t.ClassName == "moCompSketchEntHandle_c" && t.TagOffset > pointPlane.TagOffset);
                                if (sketchHandle != null && sketchHandle.DataStart >= 0 &&
                                    sketchHandle.DataStart + 0x15 <= data.Length &&
                                    BitConverter.ToUInt16(data, sketchHandle.DataStart) == 0x802C &&
                                    BitConverter.ToUInt16(data, sketchHandle.DataStart + 2) == 2)
                                {
                                    uint pointId = BitConverter.ToUInt32(data, sketchHandle.DataStart + 0x11);
                                    if (pointId != 0)
                                    {
                                        locatePoint = new EntityReference
                                        {
                                            EntityType = SelectionEntityType.Sketch,
                                            EntityId = pointId,
                                            Name = $"moCompSketchEntHandle_c:{pointId:X8}"
                                        };
                                    }
                                }

                                var definition = new StructuralMemberFeatureModel
                                {
                                    SerializedClassName = MfcClassName,
                                    SerializedTypeId = ctx.TypeId,
                                    WeldmentProfilePath = profilePath,
                                    PathSegments = Array.AsReadOnly(segments),
                                    LocateProfilePoint = locatePoint
                                };
                                definition.Resolution.ClassName = true;
                                definition.Resolution.SerializedTypeId = true;
                                definition.Resolution.WeldmentProfilePath = true;
                                definition.Resolution.PathSegments = true;
                                definition.Resolution.LocateProfilePoint = locatePoint != null;
                                return definition;
                            }
                        }
                    }
                }
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            int length = block.Length - offset;
            byte[] rawSlice = new byte[length];
            Buffer.BlockCopy(block, offset, rawSlice, 0, length);
            var ar = MfcArchive.CreateReader(rawSlice);
            var model = new StructuralMemberFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.ClassName && model.Version == 2.0 && (model.SerializedTypeId == HeaderTypeId || model.TypeId == HeaderTypeId))
                return model;
        }
        catch { }

        return null;
    }
}

/// <summary>Per-field evidence flags for <see cref="EndCapFeatureModel"/>.</summary>
public sealed class EndCapFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool ChamferDistance { get; internal set; }
    public bool DepthDistance { get; internal set; }
    public bool Face { get; internal set; }
    public bool IsEndCapInward { get; internal set; }
    public bool OffsetDistance { get; internal set; }
    public bool Thickness { get; internal set; }
    public bool ThicknessRatioForOffset { get; internal set; }
    public bool UseChamferCorners { get; internal set; }
    public bool UseCornerTreatment { get; internal set; }
    public bool UseReverse { get; internal set; }
    public bool UseThicknessRatioForOffset { get; internal set; }
}

/// <summary>
/// End-cap definition shape (<c>IEndCapFeatureData</c>, <c>EndCap</c>).
/// Implements IMfcFeatureModel and IMfcSerializable with bi-directional CArchive serialization.
/// </summary>
public sealed class EndCapFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moEndCap_c";
    public const uint HeaderTypeId = 0x51u; // 81 in FolderHeader
    public const uint SwFeatureId = 81u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "顶端盖1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }
    public EndCapFeatureParameterResolution Resolution { get; } = new();

    public double? ChamferDistance { get; set; }
    public double? DepthDistance { get; set; }
    public EntityReference? Face { get; set; }

    /// <summary><c>swEndCapThicknessDirection_e</c>: 1 Outward, 2 Inward, 3 SpecifyInset. Unset by default.</summary>
    public int? IsEndCapInward { get; set; }

    public double? OffsetDistance { get; set; }
    public double? Thickness { get; set; }
    public double? ThicknessRatioForOffset { get; set; }
    public bool? UseChamferCorners { get; set; }
    public bool? UseCornerTreatment { get; set; }
    public bool? UseReverse { get; set; }
    public bool? UseThicknessRatioForOffset { get; set; }

    public byte[]? RawBlock { get; set; }

    public EndCapFeatureModel()
    {
    }

    public EndCapFeatureModel(
        double thickness,
        EntityReference? face = null,
        bool useChamferCorners = false,
        double chamferDistance = 0.0)
    {
        FeatureName = "EndCap";
        SerializedTypeId = HeaderTypeId;
        Thickness = thickness;
        Face = face;
        UseChamferCorners = useChamferCorners;
        UseCornerTreatment = useChamferCorners;
        ChamferDistance = useChamferCorners ? chamferDistance : null;
        ThicknessRatioForOffset = 0.5;
        UseThicknessRatioForOffset = true;
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.Thickness = true;
        Resolution.UseChamferCorners = true;
        Resolution.UseCornerTreatment = true;
        Resolution.ChamferDistance = useChamferCorners;
        MarkResolved(nameof(Thickness));
        MarkResolved(nameof(UseChamferCorners));
        MarkResolved(nameof(UseCornerTreatment));
        if (useChamferCorners) MarkResolved(nameof(ChamferDistance));
        if (face != null)
        {
            Resolution.Face = true;
            MarkResolved(nameof(Face));
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "EndCap";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Face Selection (moCompFace_c)
            bool hasFace = Face != null;
            ar.Boolean(ref hasFace);
            if (hasFace)
            {
                ushort compFaceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort faceRef = 0x802C; ar.U16(ref faceRef);
                ushort faceSchema = 0x0002; ar.U16(ref faceSchema);
                uint faceFlags = 0x00400000; ar.U32(ref faceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = Face!.EntityId; ar.U32(ref entId);
            }

            // 5. Parameters & Flags
            double thk = Thickness ?? 0.002; ar.F64(ref thk);
            int dir = IsEndCapInward ?? 1; ar.I32(ref dir);
            bool useRatio = UseThicknessRatioForOffset ?? true; ar.Boolean(ref useRatio);
            double ratio = ThicknessRatioForOffset ?? 0.5; ar.F64(ref ratio);
            double offsetDist = OffsetDistance ?? 0.0; ar.F64(ref offsetDist);
            bool cornerTreatment = UseCornerTreatment ?? (UseChamferCorners == true); ar.Boolean(ref cornerTreatment);
            bool chamfer = UseChamferCorners ?? false; ar.Boolean(ref chamfer);
            double chamferDist = ChamferDistance ?? 0.001; ar.F64(ref chamferDist);
            bool reverse = UseReverse ?? false; ar.Boolean(ref reverse);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Face reference
                bool hasFace = false; ar.Boolean(ref hasFace);
                if (hasFace && ar.Remaining >= 6)
                {
                    ushort faceTag = 0;
                    ar.ReadClassTag(out string faceCls, out faceTag, out _);
                    if (faceCls == "moCompFace_c")
                    {
                        ushort faceRef = 0; ar.U16(ref faceRef);
                        ushort faceSchema = 0; ar.U16(ref faceSchema);
                        uint faceFlags = 0; ar.U32(ref faceFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        Face = new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId:X8}" };
                        Resolution.Face = true;
                        MarkResolved(nameof(Face));
                    }
                }

                // Parameters
                if (ar.Remaining >= 8) { double thk = 0; ar.F64(ref thk); Thickness = thk; Resolution.Thickness = true; MarkResolved(nameof(Thickness)); }
                if (ar.Remaining >= 4) { int dir = 0; ar.I32(ref dir); IsEndCapInward = dir; Resolution.IsEndCapInward = true; MarkResolved(nameof(IsEndCapInward)); }
                if (ar.Remaining >= 1) { bool useRatio = false; ar.Boolean(ref useRatio); UseThicknessRatioForOffset = useRatio; Resolution.UseThicknessRatioForOffset = true; MarkResolved(nameof(UseThicknessRatioForOffset)); }
                if (ar.Remaining >= 8) { double ratio = 0; ar.F64(ref ratio); ThicknessRatioForOffset = ratio; Resolution.ThicknessRatioForOffset = true; MarkResolved(nameof(ThicknessRatioForOffset)); }
                if (ar.Remaining >= 8) { double offsetDist = 0; ar.F64(ref offsetDist); OffsetDistance = offsetDist; Resolution.OffsetDistance = true; MarkResolved(nameof(OffsetDistance)); }
                if (ar.Remaining >= 1) { bool cornerTreatment = false; ar.Boolean(ref cornerTreatment); UseCornerTreatment = cornerTreatment; Resolution.UseCornerTreatment = true; MarkResolved(nameof(UseCornerTreatment)); }
                bool chamfer = false;
                if (ar.Remaining >= 1) { ar.Boolean(ref chamfer); UseChamferCorners = chamfer; Resolution.UseChamferCorners = true; MarkResolved(nameof(UseChamferCorners)); }
                if (ar.Remaining >= 8)
                {
                    double chamferDist = 0;
                    ar.F64(ref chamferDist);
                    if (chamfer)
                    {
                        ChamferDistance = chamferDist;
                        Resolution.ChamferDistance = true;
                        MarkResolved(nameof(ChamferDistance));
                    }
                    else
                    {
                        ChamferDistance = null;
                    }
                }
                if (ar.Remaining >= 1) { bool reverse = false; ar.Boolean(ref reverse); UseReverse = reverse; Resolution.UseReverse = true; MarkResolved(nameof(UseReverse)); }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        int dimOffset = MfcPrimitives.FindAscii(data, "ParallelPlaneDistanceDim_c");
        if (dimOffset > 0 && dimOffset + 0x48 <= data.Length)
        {
            double thk = BitConverter.ToDouble(data, dimOffset + 0x40);
            if (thk > 0 && thk < 0.1 && !double.IsNaN(thk))
            {
                Thickness = thk;
                Resolution.Thickness = true;
                MarkResolved(nameof(Thickness));
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    private static readonly int[] EndCapChamferRelativeOffsets = { 0xD7F, 0xF05, 0xF1D, 0xFAF, 0xFC7 };

    internal static EndCapFeatureModel? Parse(MfcElement? element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x71,
            altStreamVersion: 0x72,
            requireEmptyWCharAt10: true);
        if (ctx != null && ctx.HeaderStart + 0xC0 <= ctx.RawData.Length)
        {
            byte[] data = ctx.RawData;
            double thickness = 0;
            var dim = ctx.Nodes.FirstOrDefault(t =>
                t.ClassName == "ParallelPlaneDistanceDim_c" && t.TagOffset > ctx.Start);
            if (dim != null &&
                ctx.Parameters.TryReadVerifiedDoubleSlots(
                    dim.DataStart,
                    new[] { 0x26 },
                    out thickness,
                    minExclusive: 0.0,
                    maxInclusive: 0.1))
            {
                byte chamferCorners = data[ctx.HeaderStart + 0xA7];
                byte cornerTreatment = data[ctx.HeaderStart + 0xBD];
                if (chamferCorners <= 1 && cornerTreatment <= 1)
                {
                    var definition = new EndCapFeatureModel
                    {
                        SerializedClassName = MfcClassName,
                        SerializedTypeId = ctx.TypeId,
                        Thickness = thickness,
                        UseChamferCorners = chamferCorners == 1,
                        UseCornerTreatment = cornerTreatment == 1
                    };
                    definition.Resolution.ClassName = true;
                    definition.Resolution.SerializedTypeId = true;
                    definition.Resolution.Thickness = true;
                    definition.Resolution.UseChamferCorners = true;
                    definition.Resolution.UseCornerTreatment = true;

                    if (chamferCorners == 1)
                    {
                        double chamfer = 0;
                        var handle = ctx.Nodes.FirstOrDefault(t =>
                            t.ClassName == "moFeatureDimHandle_c" && t.TagOffset > ctx.Start);
                        if (handle != null &&
                            ctx.Parameters.TryReadVerifiedDoubleSlots(
                                handle.TagOffset,
                                EndCapChamferRelativeOffsets,
                                out chamfer,
                                minExclusive: 0.0,
                                maxInclusive: 0.01))
                        {
                            definition.ChamferDistance = chamfer;
                            definition.Resolution.ChamferDistance = true;
                        }
                    }
                    return definition;
                }
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
            byte[] rawSlice = new byte[length];
            Buffer.BlockCopy(block, offset, rawSlice, 0, length);
            var ar = MfcArchive.CreateReader(rawSlice);
            var model = new EndCapFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.ClassName && model.Thickness.HasValue && model.Thickness.Value >= 0.0001 && model.Thickness.Value <= 0.1 && !double.IsNaN(model.Thickness.Value))
            {
                model.Resolution.ClassName = true;
                model.Resolution.SerializedTypeId = true;
                model.Resolution.Thickness = true;
                return model;
            }
        }
        catch { }

        return null;
    }
}

/// <summary>Per-field evidence flags for <see cref="GussetFeatureModel"/>.</summary>
public sealed class GussetFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool FlipOffsetDirection { get; internal set; }
    public bool FlipProfileDistanceParameters { get; internal set; }
    public bool OffsetUsed { get; internal set; }
    public bool ProfileAngle { get; internal set; }
    public bool ProfileDistance1 { get; internal set; }
    public bool ProfileDistance2 { get; internal set; }
    public bool ProfileDistance3 { get; internal set; }
    public bool ProfileDistance4 { get; internal set; }
    public bool ProfileLocation { get; internal set; }
    public bool ProfileOffsetDistance { get; internal set; }
    public bool ProfileType { get; internal set; }
    public bool Thickness { get; internal set; }
    public bool ThicknessType { get; internal set; }
    public bool UseAngle { get; internal set; }
    public bool SupportingFace1 { get; internal set; }
    public bool SupportingFace2 { get; internal set; }
}

/// <summary>Gusset definition shape (<c>IGussetFeatureData</c>, <c>Gusset</c>).</summary>
public sealed class GussetFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string ClassNameConstant = "moGusset_c";
    public const string MfcClassName = "moGusset_c";
    public const uint HeaderTypeId = 0x51u;
    public const uint SwFeatureId = 81u;

    private static readonly byte[] GussetD1Anchor =
        { 0x04, 0x80, 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x31, 0x00 };

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "角撑板1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }
    public GussetFeatureParameterResolution Resolution { get; } = new();

    public bool? FlipOffsetDirection { get; set; }
    public bool? FlipProfileDistanceParameters { get; set; }
    public bool? OffsetUsed { get; set; }
    public double? ProfileAngle { get; set; }
    public double? ProfileDistance1 { get; set; }
    public double? ProfileDistance2 { get; set; }
    public double? ProfileDistance3 { get; set; }
    public double? ProfileDistance4 { get; set; }

    /// <summary><c>swGussetProfileLocationType_e</c>: 0 Start, 1 Center, 2 End. Unset by default.</summary>
    public int? ProfileLocation { get; set; }

    public double? ProfileOffsetDistance { get; set; }

    /// <summary><c>swGussetProfileType_e</c>: 0 Polygon, 1 Triangle. Unset by default.</summary>
    public int? ProfileType { get; set; }

    public double? Thickness { get; set; }

    /// <summary><c>swGussetThicknessType_e</c>: 0 Inner, 1 BothSides, 2 Outer. Unset by default.</summary>
    public int? ThicknessType { get; set; }

    public bool? UseAngle { get; set; }

    /// <summary>First supporting face (native <c>GetSupportingFaces</c>). Unset by default.</summary>
    public EntityReference? SupportingFace1 { get; set; }

    /// <summary>Second supporting face (native <c>GetSupportingFaces</c>). Unset by default.</summary>
    public EntityReference? SupportingFace2 { get; set; }

    public byte[]? RawBlock { get; set; }

    public GussetFeatureModel()
    {
    }

    public GussetFeatureModel(double thickness)
    {
        FeatureName = "Gusset";
        SerializedTypeId = HeaderTypeId;
        Thickness = thickness;
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.Thickness = true;
        MarkResolved(nameof(Thickness));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            ushort classTag = 0;
            ar.ClassTagOrRef(ClassNameConstant, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            string? name = FeatureName ?? "角撑板1";
            ar.WChar(ref name);
            FeatureName = name;

            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            double th = Thickness ?? 0.005; ar.F64(ref th);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == ClassNameConstant)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 8) { double th = 0; ar.F64(ref th); Thickness = th; Resolution.Thickness = true; MarkResolved(nameof(Thickness)); }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
            }
        }
    }

    internal static IFeatureData? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreateWithFolderHeader(
            doc, className, typeId, start, end, out var header, requiredClassName: ClassNameConstant);
        if (ctx == null)
            return null;

        var data = new GussetFeatureModel
        {
            SerializedClassName = className,
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        if (TryReadGussetParameters(ctx, out double? thickness, out int? profileType, out bool? useAngle))
        {
            if (thickness.HasValue)
            {
                data.Thickness = thickness;
                data.Resolution.Thickness = true;
            }
            if (profileType.HasValue)
            {
                data.ProfileType = profileType;
                data.Resolution.ProfileType = true;
            }
            if (useAngle.HasValue)
            {
                data.UseAngle = useAngle;
                data.Resolution.UseAngle = true;
            }
        }
        return data;
    }

    internal static GussetFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != ClassNameConstant) return null;

        var model = new GussetFeatureModel
        {
            SerializedClassName = element.ClassName,
            SerializedTypeId = HeaderTypeId
        };

        if (element.RawData != null)
        {
            model.RawBlock = element.RawData;
            using var ar = MfcArchive.CreateReader(element.RawData);
            model.Serialize(ar);
        }

        return model;
    }

    private static bool TryReadGussetParameters(
        FeatureWindowContext ctx,
        out double? thickness,
        out int? profileType,
        out bool? useAngle)
    {
        thickness = null;
        profileType = null;
        useAngle = null;
        if (ctx.StreamVersion != 0x029D && ctx.StreamVersion != 0x02A0)
            return false;

        byte[] raw = ctx.RawData;
        int limit = ctx.End;
        int start = ctx.Start;
        if (start + GussetD1Anchor.Length > limit)
            return false;

        int anchor = -1;
        for (int i = start; i + GussetD1Anchor.Length <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < GussetD1Anchor.Length; j++)
            {
                if (raw[i + j] != GussetD1Anchor[j]) { match = false; break; }
            }
            if (match) { anchor = i; break; }
        }
        if (anchor < 0) return false;

        for (int i = anchor + GussetD1Anchor.Length; i + 14 <= limit; i++)
        {
            if (raw[i] != 0xFF || raw[i + 1] != 0xFE || raw[i + 2] != 0xFF ||
                raw[i + 3] != 0x00 || raw[i + 4] != 0x00 || raw[i + 5] != 0x00)
                continue;
            double value = BitConverter.ToDouble(raw, i + 6);
            if (!double.IsNaN(value) && !double.IsInfinity(value) &&
                value > 0 && value <= 0.1)
            {
                thickness = value;
                break;
            }
        }

        int flag = anchor + 0x2046;
        if (flag + 5 < limit &&
            raw[flag + 1] == 0 && raw[flag + 2] == 0 &&
            raw[flag + 3] == 0 && raw[flag + 4] != 0 && raw[flag + 5] == 0x84)
        {
            switch (raw[flag])
            {
                case 0x00:
                    profileType = 1;
                    useAngle = true;
                    break;
                case 0x01:
                    profileType = 0;
                    useAngle = true;
                    break;
                case 0x02:
                    profileType = 1;
                    useAngle = false;
                    break;
            }
        }

        return thickness.HasValue || profileType.HasValue || useAngle.HasValue;
    }
}

/// <summary>Per-field evidence flags for <see cref="WeldBeadFeatureModel"/>.</summary>
public sealed class WeldBeadFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool BeadLengths { get; internal set; }
    public bool BeadPitches { get; internal set; }
    public bool BeadSizes { get; internal set; }
    public bool BeadTypes { get; internal set; }
    public bool TangentPropagation { get; internal set; }
    public bool UseOtherSide { get; internal set; }
    public bool BeadSize { get; internal set; }
    public bool EdgeReferences { get; internal set; }
    public bool Side { get; internal set; }
}

/// <summary>
/// Weldment bead definition shape (<c>IWeldmentBeadFeatureData</c>, <c>WeldBeadFeat</c>).
/// Implements IMfcFeatureModel and IMfcSerializable.
/// </summary>
public class WeldBeadFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moWeldBeadFeat_c";
    public const uint HeaderTypeId = 0x53u; // 83 in FolderHeader
    public const uint SwFeatureId = 83u;

    public virtual string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "焊缝1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public virtual uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }
    public WeldBeadFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Per-side bead length in meters (native indexed property). Unset by default.</summary>
    public IReadOnlyList<double>? BeadLengths { get; set; }

    /// <summary>Per-side bead pitch in meters (native indexed property). Unset by default.</summary>
    public IReadOnlyList<double>? BeadPitches { get; set; }

    /// <summary>Per-side bead size in meters (native indexed property). Unset by default.</summary>
    public IReadOnlyList<double>? BeadSizes { get; set; }

    /// <summary>Per-side <c>swWeldBeadType_e</c> (native indexed property). Unset by default.</summary>
    public IReadOnlyList<int>? BeadTypes { get; set; }

    public bool? TangentPropagation { get; set; }
    public bool? UseOtherSide { get; set; }

    /// <summary>Single bead size in meters (also decoded for moCosmeticWeldBead_c).</summary>
    public double? BeadSize { get; set; }
    public double? BeadLength { get; set; }
    public double? BeadPitch { get; set; }
    public int? BeadType { get; set; }

    /// <summary>
    /// Cosmetic weld bead side. The native sample reads 1, but no controlled side
    /// variant exists; the stable integer candidate remains unpromoted/null.
    /// </summary>
    public int? Side { get; set; }

    /// <summary>Cosmetic weld bead side/edge references in wrapper order.</summary>
    public IReadOnlyList<EntityReference>? EdgeReferences { get; set; }

    public int? EdgeReferenceCount => EdgeReferences?.Count;

    public byte[]? RawBlock { get; set; }

    public WeldBeadFeatureModel()
    {
    }

    public WeldBeadFeatureModel(double beadSize, double beadLength = 0.0, double beadPitch = 0.0, int beadType = 0)
    {
        FeatureName = "WeldBead";
        SerializedTypeId = HeaderTypeId;
        BeadSize = beadSize;
        BeadLength = beadLength;
        BeadPitch = beadPitch;
        BeadType = beadType;
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.BeadSize = true;
        MarkResolved(nameof(BeadSize));
    }

    public virtual void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            ushort classTag = 0;
            string cls = !string.IsNullOrEmpty(SerializedClassName) ? SerializedClassName! : MfcClassName;
            ar.ClassTagOrRef(cls, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            string? name = FeatureName ?? "WeldBead";
            ar.WChar(ref name);
            FeatureName = name;

            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            double size = BeadSize ?? 0.003; ar.F64(ref size);
            double len = BeadLength ?? 0.0; ar.F64(ref len);
            double pitch = BeadPitch ?? 0.0; ar.F64(ref pitch);
            int bType = BeadType ?? 0; ar.I32(ref bType);
            bool tangent = TangentPropagation ?? true; ar.Boolean(ref tangent);
            bool otherSide = UseOtherSide ?? false; ar.Boolean(ref otherSide);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName || cls == ClassName || cls == "moWeldBead_c")
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 8) { double size = 0; ar.F64(ref size); BeadSize = size; Resolution.BeadSize = true; MarkResolved(nameof(BeadSize)); }
                if (ar.Remaining >= 8) { double len = 0; ar.F64(ref len); BeadLength = len; }
                if (ar.Remaining >= 8) { double pitch = 0; ar.F64(ref pitch); BeadPitch = pitch; }
                if (ar.Remaining >= 4) { int bt = 0; ar.I32(ref bt); BeadType = bt; }
                if (ar.Remaining >= 1) { bool tan = false; ar.Boolean(ref tan); TangentPropagation = tan; Resolution.TangentPropagation = true; MarkResolved(nameof(TangentPropagation)); }
                if (ar.Remaining >= 1) { bool oth = false; ar.Boolean(ref oth); UseOtherSide = oth; Resolution.UseOtherSide = true; MarkResolved(nameof(UseOtherSide)); }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static WeldBeadFeatureModel? Parse(MfcElement? element) =>
        CosmeticWeldBeadFeatureModel.Parse(element);
}

/// <summary>
/// Strongly-typed MFC model for moCosmeticWeldBead_c (Cosmetic Weld Bead feature).
/// Inherits from WeldBeadFeatureModel for 100% backward compatibility with existing tests.
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with ICosmeticWeldBeadFeatureData.
/// </summary>
public sealed class CosmeticWeldBeadFeatureModel : WeldBeadFeatureModel
{
    new public const string MfcClassName = "moCosmeticWeldBead_c";
    new public const uint HeaderTypeId = 0x50u; // 80 in FolderHeader
    new public const uint SwFeatureId = 80u;

    public override string ClassName => SerializedClassName ?? MfcClassName;
    public override uint TypeId
    {
        get => SerializedTypeId ?? HeaderTypeId;
        set => SerializedTypeId = value;
    }

    public bool? IntermittentWeld { get; set; }
    public double? IntermittentWeldLength { get; set; }
    public double? Pitch { get; set; }
    public bool? Staggered { get; set; }

    public bool? FromToLength { get; set; }
    public double? FromToStartPoint { get; set; }
    public double? FromToWeldLength { get; set; }
    public bool? FromToReverse { get; set; }

    public CosmeticWeldBeadFeatureModel()
    {
        FeatureName = "装饰焊缝1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
    }

    public CosmeticWeldBeadFeatureModel(
        double beadSize,
        IEnumerable<EntityReference>? edgeReferences = null,
        int side = 1)
    {
        FeatureName = "WeldBead";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        BeadSize = beadSize;
        if (edgeReferences != null)
            EdgeReferences = edgeReferences.ToList().AsReadOnly();
        Side = side;
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.BeadSize = true;
        MarkResolved(nameof(BeadSize));
        if (EdgeReferences != null && EdgeReferences.Count > 0)
        {
            Resolution.EdgeReferences = true;
            MarkResolved(nameof(EdgeReferences));
        }
    }

    public override void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "WeldBead";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. moCosmeticWeldBeadData_c sub-object
            ushort beadDataTag = 0; ar.ClassTagOrRef("moCosmeticWeldBeadData_c", ref beadDataTag);
            int side = Side ?? 1; ar.I32(ref side);
            double size = BeadSize ?? 0.003; ar.F64(ref size);
            bool intermittent = IntermittentWeld ?? false; ar.Boolean(ref intermittent);
            double intLen = IntermittentWeldLength ?? 0.0; ar.F64(ref intLen);
            double pitch = Pitch ?? 0.0; ar.F64(ref pitch);
            bool staggered = Staggered ?? false; ar.Boolean(ref staggered);
            bool tangent = TangentPropagation ?? true; ar.Boolean(ref tangent);

            // 5. moWeldWireEdgeToFaceMap_c & moCompEdge_c edge selections
            var edges = EdgeReferences ?? Array.Empty<EntityReference>();
            ushort edgeMapTag = 0; ar.ClassTagOrRef("moWeldWireEdgeToFaceMap_c", ref edgeMapTag);
            ushort edgeCount = (ushort)edges.Count; ar.U16(ref edgeCount);
            foreach (var edge in edges)
            {
                ushort compEdgeTag = 0; ar.ClassTagOrRef("moCompEdge_c", ref compEdgeTag);
                ushort edgeRef = 0x802C; ar.U16(ref edgeRef);
                ushort edgeSchema = 0x0002; ar.U16(ref edgeSchema);
                uint edgeFlags = 0x00000000; ar.U32(ref edgeFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = edge.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // moCosmeticWeldBeadData_c
                if (ar.Remaining >= 2)
                {
                    ushort beadDataTag = 0;
                    ar.ReadClassTag(out string beadDataCls, out beadDataTag, out _);
                    if (beadDataCls == "moCosmeticWeldBeadData_c")
                    {
                        if (ar.Remaining >= 4) { int side = 0; ar.I32(ref side); Side = side; Resolution.Side = true; MarkResolved(nameof(Side)); }
                        if (ar.Remaining >= 8) { double size = 0; ar.F64(ref size); BeadSize = size; Resolution.BeadSize = true; MarkResolved(nameof(BeadSize)); }
                        if (ar.Remaining >= 1) { bool intermittent = false; ar.Boolean(ref intermittent); IntermittentWeld = intermittent; }
                        if (ar.Remaining >= 8) { double intLen = 0; ar.F64(ref intLen); IntermittentWeldLength = intLen; }
                        if (ar.Remaining >= 8) { double pitch = 0; ar.F64(ref pitch); Pitch = pitch; }
                        if (ar.Remaining >= 1) { bool staggered = false; ar.Boolean(ref staggered); Staggered = staggered; }
                        if (ar.Remaining >= 1) { bool tangent = false; ar.Boolean(ref tangent); TangentPropagation = tangent; Resolution.TangentPropagation = true; MarkResolved(nameof(TangentPropagation)); }
                    }
                }

                // moWeldWireEdgeToFaceMap_c
                if (ar.Remaining >= 2)
                {
                    ushort edgeMapTag = 0;
                    ar.ReadClassTag(out string edgeMapCls, out edgeMapTag, out _);
                    if (edgeMapCls == "moWeldWireEdgeToFaceMap_c")
                    {
                        ushort edgeCount = 0; ar.U16(ref edgeCount);
                        var edgeList = new List<EntityReference>();
                        for (int i = 0; i < edgeCount; i++)
                        {
                            if (ar.Remaining < 6) break;
                            ar.ReadClassTag(out _, out _, out _);
                            ushort edgeRef = 0; ar.U16(ref edgeRef);
                            ushort edgeSchema = 0; ar.U16(ref edgeSchema);
                            uint edgeFlags = 0; ar.U32(ref edgeFlags);
                            uint compId = 0; ar.U32(ref compId);
                            uint entId = 0; ar.U32(ref entId);
                            edgeList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{entId:X8}" });
                        }
                        EdgeReferences = edgeList.AsReadOnly();
                        Resolution.EdgeReferences = edgeList.Count > 0;
                        if (Resolution.EdgeReferences) MarkResolved(nameof(EdgeReferences));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        int beadDataOffset = MfcPrimitives.FindAscii(data, "moCosmeticWeldBeadData_c");
        if (beadDataOffset > 0 && beadDataOffset + 0x56 <= data.Length)
        {
            double size = BitConverter.ToDouble(data, beadDataOffset + 0x4E);
            if (size > 0 && size < 0.1 && !double.IsNaN(size))
            {
                BeadSize = size;
                Resolution.BeadSize = true;
                MarkResolved(nameof(BeadSize));
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    new internal static CosmeticWeldBeadFeatureModel? Parse(MfcElement? element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
            byte[] rawSlice = new byte[length];
            Buffer.BlockCopy(block, offset, rawSlice, 0, length);
            var ar = MfcArchive.CreateReader(rawSlice);
            var model = new CosmeticWeldBeadFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.BeadSize)
                return model;
        }
        catch { }

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x71);
        if (ctx == null || ctx.HeaderStart + 0xC0 > ctx.RawData.Length)
            return null;

        double beadSize = 0;
        var beadData = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moCosmeticWeldBeadData_c" && t.TagOffset > ctx.Start);
        if (beadData == null ||
            !ctx.Parameters.TryReadVerifiedDoubleSlots(
                beadData.DataStart,
                new[] { 0x4E },
                out beadSize,
                minExclusive: 0.0,
                maxInclusive: 0.1))
        {
            return null;
        }

        byte[] data = ctx.RawData;
        var map = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moWeldWireEdgeToFaceMap_c" && t.TagOffset > ctx.Start);
        var compEdge = map == null ? null : ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moCompEdge_c" && t.TagOffset > map.TagOffset);
        var edges = new List<EntityReference>();
        if (compEdge != null && compEdge.DataStart + 12 <= data.Length)
        {
            uint edgeId = BitConverter.ToUInt32(data, compEdge.DataStart + 8);
            if (edgeId != 0)
            {
                edges.Add(new EntityReference
                {
                    EntityType = SelectionEntityType.Edge,
                    EntityId = edgeId,
                    Name = $"moCompEdge_c:{edgeId:X8}"
                });
            }
        }

        var definition = new CosmeticWeldBeadFeatureModel
        {
            SerializedClassName = MfcClassName,
            SerializedTypeId = ctx.TypeId,
            BeadSize = beadSize,
            EdgeReferences = edges.Count > 0 ? edges.AsReadOnly() : null
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.BeadSize = true;
        definition.Resolution.EdgeReferences = edges.Count > 0;
        return definition;
    }
}

/// <summary>
/// Strongly-typed MFC model for moSMWeldBead_c (Sheet Metal Weld Bead).
/// Implements IMfcFeatureModel and IMfcSerializable. Aligned with IWeldmentBeadFeatureData.
/// </summary>
public sealed class SMWeldBeadFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moSMWeldBead_c";
    public const uint HeaderTypeId = 0x52u; // 82 in FolderHeader
    public const uint SwFeatureId = 82u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "钣金焊缝1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? HeaderTypeId;
        set => SerializedTypeId = value;
    }
    public WeldBeadFeatureParameterResolution Resolution { get; } = new();

    public double? BeadSize { get; set; }
    public double? BeadLength { get; set; }
    public double? BeadPitch { get; set; }
    public int? BeadType { get; set; }
    public bool? TangentPropagation { get; set; }
    public bool? UseOtherSide { get; set; }

    public byte[]? RawBlock { get; set; }

    public SMWeldBeadFeatureModel()
    {
    }

    public SMWeldBeadFeatureModel(double beadSize, double beadLength = 0.0, double beadPitch = 0.0, int beadType = 0)
    {
        FeatureName = "SMWeldBead";
        SerializedTypeId = HeaderTypeId;
        BeadSize = beadSize;
        BeadLength = beadLength;
        BeadPitch = beadPitch;
        BeadType = beadType;
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.BeadSize = true;
        MarkResolved(nameof(BeadSize));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            string? name = FeatureName ?? "SMWeldBead";
            ar.WChar(ref name);
            FeatureName = name;

            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            double size = BeadSize ?? 0.003; ar.F64(ref size);
            double len = BeadLength ?? 0.0; ar.F64(ref len);
            double pitch = BeadPitch ?? 0.0; ar.F64(ref pitch);
            int bType = BeadType ?? 0; ar.I32(ref bType);
            bool tangent = TangentPropagation ?? true; ar.Boolean(ref tangent);
            bool otherSide = UseOtherSide ?? false; ar.Boolean(ref otherSide);
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                ushort schema = 0; ar.U16(ref schema);
                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                if (ar.Remaining >= 8) { double size = 0; ar.F64(ref size); BeadSize = size; Resolution.BeadSize = true; MarkResolved(nameof(BeadSize)); }
                if (ar.Remaining >= 8) { double len = 0; ar.F64(ref len); BeadLength = len; }
                if (ar.Remaining >= 8) { double pitch = 0; ar.F64(ref pitch); BeadPitch = pitch; }
                if (ar.Remaining >= 4) { int bt = 0; ar.I32(ref bt); BeadType = bt; }
                if (ar.Remaining >= 1) { bool tan = false; ar.Boolean(ref tan); TangentPropagation = tan; Resolution.TangentPropagation = true; MarkResolved(nameof(TangentPropagation)); }
                if (ar.Remaining >= 1) { bool oth = false; ar.Boolean(ref oth); UseOtherSide = oth; Resolution.UseOtherSide = true; MarkResolved(nameof(UseOtherSide)); }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
            }
        }
    }
}

