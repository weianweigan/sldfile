using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parsers for the measured 2026 surface operations: knit, extend, offset and fill.
/// Only the class/type code plus structurally identified reference wrappers are decoded;
/// numeric/option fields have no controlled variation in the available samples and stay unset.
/// </summary>
internal static class SurfaceOperationFeatureParsers
{
    internal const string KnitClassName = "moSewRefSurface_c";
    internal const string ExtendClassName = "moExtendRefSurface_c";
    internal const string OffsetClassName = "moOffsetRefSurface_c";
    internal const string FillClassName = "moFillRefSurface_c";

    internal const uint KnitTypeId = 0x4E;
    internal const uint ExtendTypeId = 0x54;
    internal const uint OffsetTypeId = 0x51;
    internal const uint FillTypeId = 0x4A;

    public static SurfaceKnitFeatureDefinitionData? ParseKnit(MfcElement element)
    {
        if (!TryReadHeader(element, KnitClassName, KnitTypeId, 0x72, out uint typeId))
            return null;
        var body = ParseComponentEntities(element, "moCompSurfaceBody_c", SelectionEntityType.Body);
        if (body == null)
            return null;
        var definition = new SurfaceKnitFeatureDefinitionData
        {
            SerializedClassName = KnitClassName,
            SerializedTypeId = typeId,
            Entities = body,
            StitchReferences = body
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Entities = true;
        definition.Resolution.StitchReferences = true;
        return definition;
    }

    public static SurfaceExtendFeatureDefinitionData? ParseExtend(MfcElement element)
    {
        if (!TryReadHeader(element, ExtendClassName, ExtendTypeId, 0x70, out uint typeId))
            return null;
        var edge = ParseComponentEntity(element, "moCompEdge_c", SelectionEntityType.Edge);
        if (edge == null)
            return null;
        var definition = new SurfaceExtendFeatureDefinitionData
        {
            SerializedClassName = ExtendClassName,
            SerializedTypeId = typeId,
            Items = edge
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Items = true;
        return definition;
    }

    public static SurfaceOffsetFeatureDefinitionData? ParseOffset(MfcElement element)
    {
        if (!TryReadHeader(element, OffsetClassName, OffsetTypeId, 0x71, out uint typeId))
            return null;
        var body = ParseComponentEntities(element, "moCompSurfaceBody_c", SelectionEntityType.Body);
        if (body == null)
            return null;
        var definition = new SurfaceOffsetFeatureDefinitionData
        {
            SerializedClassName = OffsetClassName,
            SerializedTypeId = typeId,
            Entities = body,
            BodyReferences = body
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Entities = true;
        definition.Resolution.BodyReferences = true;
        return definition;
    }

    public static FillSurfaceFeatureDefinitionData? ParseFill(MfcElement element)
    {
        if (!TryReadHeader(element, FillClassName, FillTypeId, 0x70, out uint typeId))
            return null;
        byte[] data = element.SourceData;
        int tag = element.TagOffset;
        var tags = MfcPrimitives.ScanMfcTags(data);
        if (!tags.Any(t => t.ClassName == "moRadiateEdgeIdRep_c" && t.TagOffset > tag))
            return null;
        if (!tags.Any(t => t.ClassName == "moCurveContactData_c" && t.TagOffset > tag))
            return null;

        var definition = new FillSurfaceFeatureDefinitionData
        {
            SerializedClassName = FillClassName,
            SerializedTypeId = typeId
        };
        int? resolutionControl = null;
        if (TryReadHeaderTypeId(data, element.DataStart, out _, out int headerStart) &&
            headerStart >= 0 && headerStart + 0x309 <= data.Length &&
            data[headerStart + 0x300] == 0x00 &&
            data[headerStart + 0x301] == 0xF0 &&
            data[headerStart + 0x302] == 0xBF &&
            data[headerStart + 0x308] == 0x82)
        {
            byte raw = data[headerStart + 0x307];
            if (raw == 0x41) resolutionControl = 1;
            else if (raw == 0x49) resolutionControl = 2;
        }
        definition.ResolutionControl = resolutionControl;
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.ResolutionControl = resolutionControl.HasValue;
        return definition;
    }

    private static bool TryReadHeader(
        MfcElement? element, string className, uint expectedTypeId, ushort expectedVersion, out uint typeId)
    {
        typeId = 0;
        if (element == null || element.ClassName != className || element.TagByte != 0x01)
            return false;
        byte[] data = element.SourceData;
        if (data.Length < 16 || BitConverter.ToUInt16(data, 0) != expectedVersion)
            return false;
        return TryReadHeaderTypeId(data, element.DataStart, out typeId, out _) && typeId == expectedTypeId;
    }

    private static bool TryReadHeaderTypeId(byte[] data, int dataStart, out uint typeId, out int headerStart)
    {
        typeId = 0;
        headerStart = -1;
        int limit = Math.Min(data.Length, dataStart + 16);
        int offset = dataStart;
        while (offset <= limit - 3 && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset > limit - 3 || !MfcPrimitives.IsWCharPrefix(data, offset))
            return false;
        int cursor = offset;
        if (MfcPrimitives.ReadWChar(data, ref cursor, data.Length) == null)
            return false;
        int header = cursor;
        if (header + 12 > data.Length)
            return false;
        headerStart = header;
        typeId = BitConverter.ToUInt32(data, header + 8);
        return true;
    }

    private static IReadOnlyList<EntityReference>? ParseComponentEntities(
        MfcElement element, string childClass, SelectionEntityType entityType)
    {
        byte[] data = element.SourceData;
        var children = MfcPrimitives.ScanMfcTags(data)
            .Where(t => t.ClassName == childClass && t.TagOffset > element.TagOffset)
            .ToList();
        if (children.Count == 0)
            return null;
        var references = new List<EntityReference>(children.Count);
        foreach (var child in children)
        {
            int start = child.DataStart;
            if (start < 0 || start + 20 > data.Length)
                return null;
            if (BitConverter.ToUInt16(data, start) != 0x802C ||
                BitConverter.ToUInt16(data, start + 2) != 2)
                return null;
            uint entityId = BitConverter.ToUInt32(data, start + 12);
            if (entityId == 0)
                return null;
            uint componentId = BitConverter.ToUInt32(data, start + 8);
            references.Add(new EntityReference
            {
                EntityType = entityType,
                EntityId = entityId,
                Name = $"{childClass}:{componentId:X8}:{entityId:X8}"
            });
        }
        return references.AsReadOnly();
    }

    private static IReadOnlyList<EntityReference>? ParseComponentEntity(
        MfcElement element, string childClass, SelectionEntityType entityType)
    {
        byte[] data = element.SourceData;
        int tag = element.TagOffset;
        var child = MfcPrimitives.ScanMfcTags(data)
            .FirstOrDefault(t => t.ClassName == childClass && t.TagOffset > tag);
        if (child == null)
            return null;

        int start = child.DataStart;
        if (start < 0 || start + 20 > data.Length)
            return null;
        if (BitConverter.ToUInt16(data, start) != 0x802C || BitConverter.ToUInt16(data, start + 2) != 2)
            return null;
        uint entityId = BitConverter.ToUInt32(data, start + 12);
        if (entityId == 0)
            return null;
        uint componentId = BitConverter.ToUInt32(data, start + 8);
        return new[]
        {
            new EntityReference
            {
                EntityType = entityType,
                EntityId = entityId,
                Name = $"{childClass}:{componentId:X8}:{entityId:X8}"
            }
        };
    }
}
