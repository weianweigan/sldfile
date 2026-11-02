using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured 2026 planar-surface layout (moPlanarSurface_c,
/// stream 0x70). Only the class/type code and the ordered bounding sketch segments
/// (moSegmEdgeIdRep_c, 20-byte records with feature type 0x4A) are decoded.
/// </summary>
internal static class SurfacePlanarFeatureParser
{
    internal const string ClassName = "moPlanarSurface_c";
    internal const uint ExpectedTypeId = 0x4A;
    internal const ushort ExpectedStreamVersion = 0x70;
    internal const uint BoundaryFeatureType = 0x4A;
    internal const int BoundaryRecordStride = 20;
    internal const int MaxBoundaryRecords = 64;

    public static PlanarSurfaceFeatureDefinitionData? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != ClassName || element.TagByte != 0x01)
            return null;

        byte[] data = element.SourceData;
        int tag = element.TagOffset;
        if (tag < 0 || tag + 16 > data.Length)
            return null;
        if (BitConverter.ToUInt16(data, 0) != ExpectedStreamVersion)
            return null;

        if (!TryReadHeaderTypeId(data, element.DataStart, out uint typeId) || typeId != ExpectedTypeId)
            return null;

        var boundaryTags = MfcPrimitives.ScanMfcTags(data);
        var segmentTag = boundaryTags.FirstOrDefault(t =>
            t.ClassName == "moSegmEdgeIdRep_c" && t.TagOffset > tag);
        if (segmentTag == null)
            return null;

        var boundarySegments = ParseBoundaries(data, segmentTag.DataStart);
        if (boundarySegments == null || boundarySegments.Count == 0)
            return null;

        var sketchTag = boundaryTags.LastOrDefault(t =>
            t.ClassName == "moProfileFeature_c" && t.TagOffset < tag);
        if (sketchTag == null)
            return null;
        int nameOffset = sketchTag.DataStart;
        int nameLimit = Math.Min(data.Length, sketchTag.DataStart + 16);
        while (nameOffset <= nameLimit - 3 && !MfcPrimitives.IsWCharPrefix(data, nameOffset))
            nameOffset++;
        if (nameOffset > nameLimit - 3 || !MfcPrimitives.IsWCharPrefix(data, nameOffset))
            return null;
        int cursor = nameOffset;
        string? sketchName = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
        if (string.IsNullOrEmpty(sketchName))
            return null;

        var definition = new PlanarSurfaceFeatureDefinitionData
        {
            SerializedClassName = ClassName,
            SerializedTypeId = typeId,
            BoundingEntities = new[]
            {
                new EntityReference
                {
                    EntityType = SelectionEntityType.Sketch,
                    Name = sketchName!
                }
            },
            BoundarySegments = boundarySegments
        };
        definition.SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(element.SourceData);
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.BoundingEntities = true;
        definition.Resolution.BoundarySegments = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }

    private static List<EntityReference>? ParseBoundaries(byte[] data, int start)
    {
        var result = new List<EntityReference>();
        int pos = start;
        for (int i = 0; i < MaxBoundaryRecords && pos + BoundaryRecordStride <= data.Length; i++)
        {
            uint featureType = BitConverter.ToUInt32(data, pos + 6);
            uint index = BitConverter.ToUInt32(data, pos + 14);
            if (featureType != BoundaryFeatureType || index == 0)
                break;
            result.Add(new EntityReference
            {
                EntityType = SelectionEntityType.Edge,
                EntityId = index,
                Name = $"BoundarySegment:{index}"
            });
            pos += BoundaryRecordStride;
        }
        return result;
    }

    private static bool TryReadHeaderTypeId(byte[] data, int dataStart, out uint typeId)
    {
        typeId = 0;
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
        typeId = BitConverter.ToUInt32(data, header + 8);
        return true;
    }
}
