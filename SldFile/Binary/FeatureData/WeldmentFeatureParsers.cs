using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Parsers for Phase 5 weldment/structure features.
/// <para>
/// Structural member (<c>moWeldMemberFeat_c</c>), EndCap (<c>moEndCap_c</c>) and
/// cosmetic weld bead (<c>moCosmeticWeldBead_c</c>) have measured 2026 layouts.
/// Gusset continues to reject until a persisted sample establishes its layout.
/// </para>
/// </summary>
internal static class WeldmentFeatureParsers
{
    internal const string StructuralMemberClassName = "moWeldMemberFeat_c";
    internal const uint StructuralMemberTypeId = 0x4D;
    internal const string EndCapClassName = "moEndCap_c";
    internal const uint EndCapTypeId = 0x51;
    internal const string CosmeticWeldBeadClassName = "moCosmeticWeldBead_c";
    internal const uint CosmeticWeldBeadTypeId = 0x50;
    private const ushort StreamVersion = 0x70;
    private const ushort EndCapStreamVersion71 = 0x71;
    private const ushort EndCapStreamVersion72 = 0x72;
    private const ushort WeldBeadStreamVersion = 0x71;

    public static IFeatureData? ParseStructuralMember(MfcElement? element)
    {
        if (!TryReadHeader(element, StructuralMemberClassName, StructuralMemberTypeId, out uint typeId))
            return null;

        byte[] data = element!.SourceData;
        int tag = element.TagOffset;
        var tags = MfcPrimitives.ScanMfcTags(data);

        // The measured 2026 payload stores the weldment profile path as a WChar after
        // the feature's reference wrappers. The controlled samples contain exactly one
        // .sldlfp string, so a multi-member stream is rejected rather than guessed.
        var profilePaths = ScanProfilePaths(data, tag);
        if (profilePaths.Count != 1)
            return null;
        string profilePath = profilePaths[0];
        if (!profilePath.EndsWith(".sldlfp", StringComparison.OrdinalIgnoreCase))
            return null;

        // moWeldSegSkIndices_c: u16 count followed by one or more moWeldSegSkIndex_c
        // children whose first u32 is the sketch-segment entity id.
        var indices = tags.FirstOrDefault(t =>
            t.ClassName == "moWeldSegSkIndices_c" && t.TagOffset > tag);
        if (indices == null)
            return null;
        if (indices.DataStart + 2 > data.Length)
            return null;
        ushort segmentCount = BitConverter.ToUInt16(data, indices.DataStart);
        if (segmentCount == 0 || segmentCount > 256)
            return null;
        var indexElements = tags
            .Where(t => t.ClassName == "moWeldSegSkIndex_c" && t.TagOffset > indices.TagOffset)
            .Take(segmentCount)
            .ToList();
        if (indexElements.Count != segmentCount)
            return null;
        var segments = new EntityReference[segmentCount];
        for (int i = 0; i < segmentCount; i++)
        {
            int start = indexElements[i].DataStart;
            if (start < 0 || start + 4 > data.Length)
                return null;
            uint entityId = BitConverter.ToUInt32(data, start);
            if (entityId == 0)
                return null;
            segments[i] = new EntityReference
            {
                EntityType = SelectionEntityType.Edge,
                EntityId = entityId,
                Name = $"moWeldSegSkIndex_c:{entityId:X8}"
            };
        }

        EntityReference? locatePoint = null;
        var pointPlane = tags.FirstOrDefault(t =>
            t.ClassName == "moPerLinePtRefPlnData_c" && t.TagOffset > tag);
        var sketchHandle = pointPlane == null ? null : tags.FirstOrDefault(t =>
            t.ClassName == "moCompSketchEntHandle_c" && t.TagOffset > pointPlane.TagOffset);
        // Measured layout: 0x802C / count 2 wrapper, then the sketch entity index at
        // data + 0x11. The handle's first u32 is the wrapper magic, not the entity id.
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

        var definition = new StructuralMemberFeatureDefinitionData
        {
            SerializedClassName = StructuralMemberClassName,
            SerializedTypeId = typeId,
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

    public static IFeatureData? ParseEndCap(MfcElement? element)
    {
        if (element == null || element.ClassName != EndCapClassName || element.TagByte != 0x01)
            return null;
        byte[] data = element.SourceData;
        ushort version = BitConverter.ToUInt16(data, 0);
        if (version != EndCapStreamVersion71 && version != EndCapStreamVersion72)
            return null;

        if (!TryReadWeldmentHeader(element, out uint typeId, out int header))
            return null;
        if (typeId != EndCapTypeId || header + 0xC0 > data.Length ||
            data[header + 0x10] != 0xFF || data[header + 0x11] != 0xFE ||
            data[header + 0x12] != 0xFF || data[header + 0x13] != 0x00)
            return null;

        // Thickness is stored in the measured ParallelPlaneDistanceDim_c D1 value.
        var tags = MfcPrimitives.ScanMfcTags(data);
        var dim = tags.FirstOrDefault(t =>
            t.ClassName == "ParallelPlaneDistanceDim_c" && t.TagOffset > element.TagOffset);
        if (dim == null || dim.DataStart + 0x26 + 8 > data.Length)
            return null;
        double thickness = BitConverter.ToDouble(data, dim.DataStart + 0x26);
        if (double.IsNaN(thickness) || double.IsInfinity(thickness) ||
            thickness <= 0 || thickness > 0.1)
            return null;

        // The two measured EndCap samples differ only by the chamfer/corner flags.
        // These bytes are stable across primary/resave for the controlled layout.
        byte chamferCorners = data[header + 0xA7];
        byte cornerTreatment = data[header + 0xBD];
        if (chamferCorners > 1 || cornerTreatment > 1)
            return null;

        var definition = new EndCapFeatureDefinitionData
        {
            SerializedClassName = EndCapClassName,
            SerializedTypeId = typeId,
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
            double? chamfer = TryReadChamferDistance(data, tags, element.TagOffset);
            if (chamfer.HasValue)
            {
                definition.ChamferDistance = chamfer;
                definition.Resolution.ChamferDistance = true;
            }
        }
        return definition;
    }

    /// <summary>
    /// The chamfer variant carries five copies of the same 0.001 m double after its
    /// moFeatureDimHandle_c node. The offsets are stable for the controlled main/resave
    /// pair; all copies must agree before the primary copy is exposed.
    /// </summary>
    private static double? TryReadChamferDistance(
        byte[] data, List<MfcTag> tags, int featureTagOffset)
    {
        var handle = tags.FirstOrDefault(t =>
            t.ClassName == "moFeatureDimHandle_c" && t.TagOffset > featureTagOffset);
        if (handle == null)
            return null;
        int[] offsets = { 0xD7F, 0xF05, 0xF1D, 0xFAF, 0xFC7 };
        double? first = null;
        foreach (int relative in offsets)
        {
            int pos = handle.TagOffset + relative;
            if (pos < 0 || pos + 8 > data.Length)
                return null;
            double value = BitConverter.ToDouble(data, pos);
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value <= 0 || value > 0.01)
                return null;
            if (!first.HasValue)
                first = value;
            else if (Math.Abs(first.Value - value) > 1e-12)
                return null;
        }
        return first;
    }

    /// <summary>
    /// Cosmetic weld bead: the actual feature is the nested <c>moCosmeticWeldBead_c</c>
    /// node. Bead size follows its moCosmeticWeldBeadData_c child; the wire-edge map
    /// child carries the selected edge reference. Side has no controlled variation and
    /// stays unresolved.
    /// </summary>
    public static IFeatureData? ParseCosmeticWeldBead(MfcElement? element)
    {
        if (element == null || element.ClassName != CosmeticWeldBeadClassName || element.TagByte != 0x01)
            return null;
        byte[] data = element.SourceData;
        if (BitConverter.ToUInt16(data, 0) != WeldBeadStreamVersion)
            return null;
        if (!TryReadWeldmentHeader(element, out uint typeId, out int header))
            return null;
        if (typeId != CosmeticWeldBeadTypeId || header + 0x30 > data.Length)
            return null;

        var tags = MfcPrimitives.ScanMfcTags(data);
        var beadData = tags.FirstOrDefault(t =>
            t.ClassName == "moCosmeticWeldBeadData_c" && t.TagOffset > element.TagOffset);
        if (beadData == null || beadData.DataStart + 0x4E + 8 > data.Length)
            return null;
        double beadSize = BitConverter.ToDouble(data, beadData.DataStart + 0x4E);
        if (double.IsNaN(beadSize) || double.IsInfinity(beadSize) ||
            beadSize <= 0 || beadSize > 0.1)
            return null;

        var map = tags.FirstOrDefault(t =>
            t.ClassName == "moWeldWireEdgeToFaceMap_c" && t.TagOffset > element.TagOffset);
        var compEdge = map == null ? null : tags.FirstOrDefault(t =>
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

        var definition = new WeldBeadFeatureDefinitionData
        {
            SerializedClassName = CosmeticWeldBeadClassName,
            SerializedTypeId = typeId,
            BeadSize = beadSize,
            EdgeReferences = edges.Count > 0 ? edges.AsReadOnly() : null
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.BeadSize = true;
        definition.Resolution.EdgeReferences = edges.Count > 0;
        return definition;
    }

    public static IFeatureData? ParseGusset(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseWeldBead(MfcElement? element) => Reject(element);

    private static bool TryReadWeldmentHeader(
        MfcElement element, out uint typeId, out int header)
    {
        typeId = 0;
        header = -1;
        byte[] data = element.SourceData;
        int limit = Math.Min(data.Length, element.DataStart + 16);
        int offset = element.DataStart;
        while (offset <= limit - 3 && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset > limit - 3)
            return false;
        int cursor = offset;
        if (MfcPrimitives.ReadWChar(data, ref cursor, data.Length) == null)
            return false;
        header = cursor;
        if (header < 0 || header + 0xC0 > data.Length)
            return false;
        typeId = BitConverter.ToUInt32(data, header + 8);
        return true;
    }

    private static bool TryReadHeader(
        MfcElement? element, string className, uint expectedTypeId, out uint typeId)
    {
        typeId = 0;
        if (element == null || element.ClassName != className || element.TagByte != 0x01)
            return false;
        byte[] data = element.SourceData;
        if (data.Length < 16 || BitConverter.ToUInt16(data, 0) != StreamVersion)
            return false;

        int limit = Math.Min(data.Length, element.DataStart + 16);
        int offset = element.DataStart;
        while (offset <= limit - 3 && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset > limit - 3)
            return false;
        int cursor = offset;
        if (MfcPrimitives.ReadWChar(data, ref cursor, data.Length) == null)
            return false;

        foreach (int header in new[] { cursor, cursor + 2 })
        {
            if (header + 12 > data.Length)
                continue;
            if (data[header + 0x10] != 0xFF || data[header + 0x11] != 0xFE ||
                data[header + 0x12] != 0xFF || data[header + 0x13] != 0x00)
                continue;
            uint code = BitConverter.ToUInt32(data, header + 8);
            if (code != expectedTypeId)
                continue;
            typeId = code;
            return true;
        }
        return false;
    }

    private static List<string> ScanProfilePaths(byte[] data, int start)
    {
        var paths = new List<string>();
        for (int i = Math.Max(0, start); i + 4 <= data.Length; i++)
        {
            if (!MfcPrimitives.IsWCharPrefix(data, i))
                continue;
            int cursor = i;
            string? value = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
            if (string.IsNullOrEmpty(value) || value!.Length > 512)
                continue;
            if (value.IndexOf(".sldlfp", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (!paths.Contains(value, StringComparer.OrdinalIgnoreCase))
                paths.Add(value);
        }
        return paths;
    }

    private static IFeatureData? Reject(MfcElement? element)
    {
        _ = element;
        return null;
    }
}
