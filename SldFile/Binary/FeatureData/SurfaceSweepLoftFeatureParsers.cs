using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parsers for the measured 2026 surface sweep and surface loft nodes.
/// Surface sweep (moSweepRefSurface_c) is a circular-profile sweep with a D2 diameter
/// parameter; surface loft (moBlendRefSurface_c) has no FeatureData and exposes only its
/// ordered profile-selection ids.
/// </summary>
internal static class SurfaceSweepLoftFeatureParsers
{
    internal const string SweepClassName = "moSweepRefSurface_c";
    internal const string LoftClassName = "moBlendRefSurface_c";
    internal const uint SweepTypeId = 0x4B;
    internal const uint LoftTypeId = 0x4D;
    internal const ushort StreamVersion = 0x70;

    public static SurfaceSweepFeatureDefinitionData? ParseSweep(MfcElement element)
    {
        if (!TryReadHeader(element, SweepClassName, SweepTypeId, SweepTypeId, out uint typeId, out var ids))
            return null;

        var tags = MfcPrimitives.ScanMfcTags(element.SourceData);
        int tag = element.TagOffset;
        if (ids.Count != 1)
            return null;
        var primary = tags.FirstOrDefault(t =>
            t.ClassName == "moCompProfile_c" && t.TagOffset > tag);
        if (primary == null)
            return null;
        int record = primary.DataStart + 81;
        if (record < 0 || record + 52 > element.SourceData.Length ||
            BitConverter.ToUInt32(element.SourceData, record) != 0x4A38)
            return null;
        if (BitConverter.ToUInt32(element.SourceData, record + 36) != 0xFFFFCFC7 ||
            BitConverter.ToUInt32(element.SourceData, record + 40) != 0xFFFFCFC7 ||
            BitConverter.ToUInt32(element.SourceData, record + 44) != 0xFFFFCFC7)
            return null;
        uint sketchId = BitConverter.ToUInt32(element.SourceData, record + 4);
        if (sketchId == 0)
            return null;
        uint serial = BitConverter.ToUInt32(element.SourceData, record + 8);
        var path = new SketchCurveReference
        {
            Role = SketchCurveRole.Path,
            EntityId = sketchId,
            ArchiveSerial = serial,
            HeaderSelectionId = ids[0],
            IsHeaderSelectionIdPositional = true
        };

        var diameterDim = tags.FirstOrDefault(t => t.ClassName == "ThreeDdiameterDim_c" && t.TagOffset > tag);
        if (diameterDim == null)
            return null;
        var lengthParam = tags.FirstOrDefault(t =>
            t.ClassName == "moLengthParameter_c" && t.TagOffset > diameterDim.TagOffset);
        if (lengthParam == null)
            return null;

        int valueOffset = lengthParam.DataStart + 0x20;
        if (valueOffset + 8 > element.SourceData.Length)
            return null;
        double diameter = BitConverter.ToDouble(element.SourceData, valueOffset);
        if (double.IsNaN(diameter) || double.IsInfinity(diameter) || diameter <= 0 || diameter > 1.0)
            return null;
        if (!tags.Any(t => t.ClassName == "moWireContact_c" && t.TagOffset > tag))
            return null;

        var definition = new SurfaceSweepFeatureDefinitionData
        {
            SerializedClassName = SweepClassName,
            SerializedTypeId = typeId,
            SelectionReferenceIds = ids,
            PathReference = path,
            CircularProfileLayout = true,
            CircularProfileDiameter = diameter,
            SweepType = 62,
            Merge = true,
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(element.SourceData)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.SelectionReferenceList = ids.Count > 0;
        definition.Resolution.CircularProfile = true;
        definition.Resolution.PathReference = true;
        definition.Resolution.SweepType = true;
        definition.Resolution.Merge = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }

    public static SurfaceLoftFeatureDefinitionData? ParseLoft(MfcElement element)
    {
        if (!TryReadHeader(element, LoftClassName, LoftTypeId, 0x50, out uint typeId, out var ids))
            return null;
        if (ids.Count < 2)
            return null;

        byte[] data = element.SourceData;
        var tags = MfcPrimitives.ScanMfcTags(data);
        var primary = tags.FirstOrDefault(t =>
            t.ClassName == "moCompProfile_c" && t.TagOffset > element.TagOffset);
        if (primary == null)
            return null;
        int primaryStart = primary.DataStart + 81;
        var records = new List<(uint Id, uint Serial)>();
        while (records.Count < ids.Count)
        {
            int record = primaryStart + records.Count * 188;
            if (!TryReadSketchRef(data, record, out uint id, out uint serial))
                break;
            records.Add((id, serial));
        }
        if (records.Count < 2)
            return null;

        var secondary = tags.FirstOrDefault(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > element.TagOffset);
        int secondaryCount = 0;
        if (secondary != null)
        {
            int secondaryStart = secondary.DataStart + 147;
            while (records.Count + secondaryCount < ids.Count)
            {
                int record = secondaryStart + secondaryCount * 190;
                if (!TryReadSketchRef(data, record, out uint id, out uint serial))
                    break;
                records.Add((id, serial));
                secondaryCount++;
            }
        }
        if (records.Count != ids.Count)
            return null;

        var references = new SketchCurveReference[ids.Count];
        int profileCount = ids.Count - secondaryCount;
        for (int i = 0; i < ids.Count; i++)
        {
            references[i] = new SketchCurveReference
            {
                Role = i < profileCount ? SketchCurveRole.Profile : SketchCurveRole.Guide,
                EntityId = records[i].Id,
                ArchiveSerial = records[i].Serial,
                HeaderSelectionId = ids[i],
                IsHeaderSelectionIdPositional = true
            };
        }

        var definition = new SurfaceLoftFeatureDefinitionData
        {
            SerializedClassName = LoftClassName,
            SerializedTypeId = typeId,
            SelectionReferenceIds = ids,
            References = Array.AsReadOnly(references),
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(element.SourceData)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.SelectionReferenceList = true;
        definition.Resolution.ProfileReferences = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }

    private static bool TryReadSketchRef(byte[] data, int record, out uint id, out uint serial)
    {
        id = 0;
        serial = 0;
        if (record < 0 || record + 52 > data.Length ||
            BitConverter.ToUInt32(data, record) != 0x4A38)
            return false;
        id = BitConverter.ToUInt32(data, record + 4);
        if (id == 0)
            return false;
        if (BitConverter.ToUInt32(data, record + 36) != 0xFFFFCFC7 ||
            BitConverter.ToUInt32(data, record + 40) != 0xFFFFCFC7 ||
            BitConverter.ToUInt32(data, record + 44) != 0xFFFFCFC7)
            return false;
        serial = BitConverter.ToUInt32(data, record + 8);
        return true;
    }

    private static bool TryReadHeader(
        MfcElement? element,
        string className,
        uint minTypeId,
        uint maxTypeId,
        out uint typeId,
        out IReadOnlyList<ushort> selectionIds)
    {
        typeId = 0;
        selectionIds = Array.Empty<ushort>();
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
            if (header + 0x1C > data.Length)
                continue;
            if (data[header + 0x10] != 0xFF || data[header + 0x11] != 0xFE ||
                data[header + 0x12] != 0xFF || data[header + 0x13] != 0x00)
                continue;
            uint code = BitConverter.ToUInt32(data, header + 8);
            if (code < minTypeId || code > maxTypeId)
                continue;
            ushort count = BitConverter.ToUInt16(data, header + 0x1A);
            if (count == 0 || count > 32 || header + 0x1C + count * 2 > data.Length)
                continue;
            var ids = new ushort[count];
            for (int i = 0; i < count; i++)
                ids[i] = BitConverter.ToUInt16(data, header + 0x1C + i * 2);
            typeId = code;
            selectionIds = Array.AsReadOnly(ids);
            return true;
        }
        return false;
    }
}
