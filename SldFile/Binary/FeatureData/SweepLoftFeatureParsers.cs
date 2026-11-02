using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary.Topology;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Structural parsers for the Phase 3.1 feature nodes established by the
/// 2026 SweepOptions/LoftOptions samples.
/// <para>
/// The selection layer is decoded by <see cref="SweepLoftSelectionDecoder"/>
/// (Agent 3): folder-header type code and selection ids, sketch persistent
/// references with roles, circular-profile diameter and thin-wall thickness.
/// If the decoder rejects the layout, the parser returns null so the feature stays
/// <see cref="FeatureFolder.FeatureData"/> = null instead of exposing guessed values.
/// </para>
/// </summary>
internal static class SweepLoftFeatureParsers
{
    internal const string SweepClassName = "moSweep_c";

    // The serialized class observed in all four loft samples is moBlend_c.
    // moLoft_c is only a catalog name and has no validated payload parser.
    internal const string LoftClassName = "moBlend_c";

    public static IFeatureData? ParseSweep(MfcElement element)
    {
        if (element == null || element.ClassName != SweepClassName)
            return null;

        var selection = SweepLoftSelectionDecoder.Decode(element);
        if (selection == null || selection.Kind != SweepLoftSelectionKind.Sweep)
            return null;

        var options = ReadSweepOptions(element);

        return new SweepFeatureDefinitionData(
            element.AsWChar() ?? string.Empty,
            selection.SerializedTypeCode,
            selection.HeaderSelectionIds,
            selection.References,
            selection.CircularProfileLayout,
            selection.CircularProfileDiameter,
            selection.ThinFeatureLayout,
            selection.ThinWallThickness,
            selection.IsHeaderSelectionIdMappingPositional,
            options.Merge,
            options.TwistControlType,
            options.TwistAngleRad);
    }

    /// <summary>
    /// Measured solid-sweep option slots: merge at header +0x85B, twist control at
    /// +0x827 and twist angle (double) at +0x863. Invalid/unknown layouts leave all
    /// three options unresolved.
    /// </summary>
    private static (bool? Merge, int? TwistControlType, double? TwistAngleRad) ReadSweepOptions(
        MfcElement element)
    {
        byte[] data = element.SourceData;
        int end = element.DataEnd;
        if (end < 0 || end + 0x157 > data.Length)
            return (null, null, null);

        byte mergeByte = data[end + 0x147];
        if (mergeByte > 1)
            return (null, null, null);
        byte twistByte = data[end + 0x113];
        if (twistByte != 0 && twistByte != 1 && twistByte != 8)
            return (null, null, null);
        double angle = BitConverter.ToDouble(data, end + 0x14F);
        if (double.IsNaN(angle) || double.IsInfinity(angle) || angle < 0 || angle > Math.PI * 2 + 1e-9)
            return (null, null, null);
        return (mergeByte == 1, (int)twistByte, angle);
    }

    public static IFeatureData? ParseLoft(MfcElement element)
    {
        if (element == null || element.ClassName != LoftClassName)
            return null;

        var selection = SweepLoftSelectionDecoder.Decode(element);
        if (selection == null)
            selection = TryDecodeLoftCenterlineDirect(element);
        if (selection == null || selection.Kind != SweepLoftSelectionKind.Loft)
            return null;

        int? startTangency = null;
        int? endTangency = null;
        if (selection.SerializedTypeCode == 0x4D &&
            TryReadTangencyTypes(element, out int start, out int end))
        {
            startTangency = start;
            endTangency = end;
        }
        var centerline = ReadLoftCenterline(element);

        return new LoftFeatureDefinitionData(
            element.AsWChar() ?? string.Empty,
            selection.SerializedTypeCode,
            selection.HeaderSelectionIds,
            selection.References,
            selection.CircularProfileLayout,
            selection.CircularProfileDiameter,
            selection.ThinFeatureLayout,
            selection.ThinWallThickness,
            selection.IsHeaderSelectionIdMappingPositional,
            startTangency,
            endTangency,
            centerline);
    }

    /// <summary>
    /// Fallback for the centerline loft layout: the shared selection decoder sees the
    /// extra centerline record as a fourth reference and rejects the node. This direct
    /// decoder requires two profiles plus two secondary records with the same sketch id
    /// (guide + centerline), then keeps only the guide in References.
    /// </summary>
    private static SweepLoftSelectionData? TryDecodeLoftCenterlineDirect(MfcElement element)
    {
        byte[] data = element.SourceData;
        if (!TryReadLoftHeader(element, out uint typeCode, out var headerIds))
            return null;
        var tags = MfcPrimitives.ScanMfcTags(data);
        var primary = tags.FirstOrDefault(t =>
            t.ClassName == "moCompProfile_c" && t.TagOffset > element.TagOffset);
        var secondary = tags.FirstOrDefault(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > element.TagOffset);
        if (primary == null || secondary == null)
            return null;
        var profiles = ReadRefChain(data, primary.DataStart + 81, 188, 8);
        var secondaries = ReadRefChain(data, secondary.DataStart + 147, 190, 8);
        if (profiles.Count < 2 || secondaries.Count != 2 ||
            secondaries[0].Id != secondaries[1].Id ||
            headerIds.Count != profiles.Count + 1)
            return null;

        var refs = new List<SketchCurveReference>();
        for (int i = 0; i < profiles.Count; i++)
        {
            refs.Add(new SketchCurveReference
            {
                Role = SketchCurveRole.Profile,
                EntityId = profiles[i].Id,
                ArchiveSerial = profiles[i].Serial,
                HeaderSelectionId = headerIds[i],
                IsHeaderSelectionIdPositional = true
            });
        }
        refs.Add(new SketchCurveReference
        {
            Role = SketchCurveRole.Guide,
            EntityId = secondaries[0].Id,
            ArchiveSerial = secondaries[0].Serial,
            HeaderSelectionId = headerIds[profiles.Count],
            IsHeaderSelectionIdPositional = true
        });
        return new SweepLoftSelectionData
        {
            Kind = SweepLoftSelectionKind.Loft,
            ClassName = element.ClassName,
            SerializedTypeCode = typeCode,
            HeaderSelectionIds = headerIds,
            References = refs.AsReadOnly()
        };
    }

    private static List<(uint Id, uint Serial)> ReadRefChain(
        byte[] data, int start, int stride, int max)
    {
        var records = new List<(uint Id, uint Serial)>();
        for (int i = 0; i < max; i++)
        {
            int pos = start + i * stride;
            if (pos < 0 || pos + 52 > data.Length ||
                BitConverter.ToUInt32(data, pos) != 0x4A38)
                break;
            uint id = BitConverter.ToUInt32(data, pos + 4);
            if (id == 0 ||
                BitConverter.ToUInt32(data, pos + 36) != 0xFFFFCFC7 ||
                BitConverter.ToUInt32(data, pos + 40) != 0xFFFFCFC7 ||
                BitConverter.ToUInt32(data, pos + 44) != 0xFFFFCFC7)
                break;
            records.Add((id, BitConverter.ToUInt32(data, pos + 8)));
        }
        return records;
    }

    private static bool TryReadLoftHeader(
        MfcElement element, out uint typeCode, out IReadOnlyList<ushort> headerIds)
    {
        typeCode = 0;
        headerIds = Array.Empty<ushort>();
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
        int header = cursor;
        if (header + 0x1C > data.Length ||
            data[header + 0x10] != 0xFF || data[header + 0x11] != 0xFE ||
            data[header + 0x12] != 0xFF || data[header + 0x13] != 0x00)
            return false;
        uint code = BitConverter.ToUInt32(data, header + 8);
        if (code < 0x4D || code > 0x50)
            return false;
        ushort count = BitConverter.ToUInt16(data, header + 0x1A);
        if (count == 0 || count > 32 || header + 0x1C + count * 2 > data.Length)
            return false;
        var ids = new ushort[count];
        for (int i = 0; i < count; i++)
            ids[i] = BitConverter.ToUInt16(data, header + 0x1C + i * 2);
        typeCode = code;
        headerIds = Array.AsReadOnly(ids);
        return true;
    }

    /// <summary>
    /// The centerline sample appends an extra 52-byte sketch reference after the
    /// moWireContact_c wrapper. Guide-only lofts have only the guide record, so a
    /// second valid positive-id record is required.
    /// </summary>
    private static SketchCurveReference? ReadLoftCenterline(MfcElement element)
    {
        byte[] data = element.SourceData;
        var wire = MfcPrimitives.ScanMfcTags(data)
            .FirstOrDefault(t => t.ClassName == "moWireContact_c" && t.TagOffset > element.TagOffset);
        if (wire == null)
            return null;
        int start = wire.DataStart;
        if (start < 0 || start + 52 > data.Length)
            return null;
        var records = new List<(int Offset, uint Id, uint Serial)>();
        for (int pos = start; pos + 52 <= data.Length; pos++)
        {
            if (BitConverter.ToUInt32(data, pos) != 0x4A38)
                continue;
            uint id = BitConverter.ToUInt32(data, pos + 4);
            if (id == 0)
                continue;
            if (BitConverter.ToUInt32(data, pos + 36) != 0xFFFFCFC7 ||
                BitConverter.ToUInt32(data, pos + 40) != 0xFFFFCFC7 ||
                BitConverter.ToUInt32(data, pos + 44) != 0xFFFFCFC7)
                continue;
            records.Add((pos, id, BitConverter.ToUInt32(data, pos + 8)));
        }
        if (records.Count < 2)
            return null;
        var second = records[1];
        return new SketchCurveReference
        {
            Role = SketchCurveRole.Centerline,
            EntityId = second.Id,
            ArchiveSerial = second.Serial,
            HeaderSelectionId = 0,
            IsHeaderSelectionIdPositional = false
        };
    }

    /// <summary>
    /// Measured TypeId 0x4D trailing option pair: 0x00000100 (tangency off) or
    /// 0x00000101 (normal-to-profile). The pair is anchored immediately after the
    /// feature element; the two values are start/end by field order. TypeId 0x4E/0x50
    /// layouts have different trailing structures and stay unresolved.
    /// </summary>
    private static bool TryReadTangencyTypes(MfcElement element, out int start, out int end)
    {
        start = 0;
        end = 0;
        int offset = element.DataEnd + 0x1A8;
        if (offset < 0 || offset + 8 > element.SourceData.Length)
            return false;
        uint first = BitConverter.ToUInt32(element.SourceData, offset);
        uint second = BitConverter.ToUInt32(element.SourceData, offset + 4);
        if ((first & 0xFFFFFF00) != 0x00000100 || (first & 0xFF) > 1 ||
            (second & 0xFFFFFF00) != 0x00000100 || (second & 0xFF) > 1)
            return false;
        start = (int)(first & 0xFF);
        end = (int)(second & 0xFF);
        return true;
    }
}
