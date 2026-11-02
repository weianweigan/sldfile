using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Strict parsers for Task 2.2 edge and miter flanges.
/// <para>
/// EdgeFlange is now sample-confirmed: class <c>moEdgeFlange_c</c>, FolderHeader
/// TypeId <b>0x67</b> (catalog/TODO candidate 0x63 was not observed), stream
/// version 0x74. The window-aware parser resolves BendAngle (D4), BendRadius
/// (D1), EdgeCount, OffsetDistance (D7), PositionType and ReliefWidth/Depth
/// (measured anonymous relief block). Every other field remains unresolved and
/// the parser returns null for unknown or tampered layouts. Miter flange still
/// has no sample: class <c>moMiterFlange_c</c> remains a candidate and its TypeId
/// is not encoded.
/// </para>
/// </summary>
internal static class FlangeFeatureParsers
{
    internal const string EdgeFlangeClassName = EdgeFlangeFeatureDefinitionData.SerializedClassName;
    internal const string MiterFlangeClassName = MiterFlangeFeatureDefinitionData.SerializedClassName;

    /// <summary>
    /// FolderHeader TypeId of the fresh single-edge layout produced by
    /// InsertSheetMetalEdgeFlange2 (ResolvedFeatures 0x74, one base flange plus
    /// one edge flange). Measured on the 2026-10-31 edgepersist samples.
    /// </summary>
    internal const uint FreshSingleEdgeSerializedTypeId = 0x75;

    /// <summary>
    /// True when the element is the observed edge-flange node: class
    /// <c>moEdgeFlange_c</c>, tag byte 0x01, and FolderHeader TypeId 0x67.
    /// The TypeId was measured on samples4/edge_flange_90_len10.SLDPRT
    /// (version 0x74); the catalog/TODO candidate 0x63 was not observed.
    /// Payload fields remain unresolved, so the parser still returns null.
    /// </summary>
    internal static bool IsEdgeFlangeNode(MfcElement element)
    {
        if (element == null || element.ClassName != EdgeFlangeClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == EdgeFlangeFeatureDefinitionData.ObservedSerializedTypeId ||
               header.TypeId == FreshSingleEdgeSerializedTypeId;
    }

    /// <summary>
    /// Window-less parser entry used by the registry. It validates the observed
    /// class/TypeId but has no selection/dimension window here, so it returns
    /// null; the mapper calls the window-aware overload below for actual reading.
    /// </summary>
    public static IFeatureData? ParseEdgeFlange(MfcElement element)
    {
        if (!IsEdgeFlangeNode(element))
            return null;

        return null;
    }

    /// <summary>
    /// Strict parser for the single-edge edge-flange layout (stream version
    /// 0x74), validated against the six controlled FlangeOptions samples.
    /// <para>
    /// Resolved from native evidence: BendAngle (named D4), BendRadius (the first
    /// named D1 after the feature tag and before the first angular display
    /// dimension), EdgeCount (the first u32 of the single moEdgeRef_c),
    /// OffsetDistance (named D7), PositionType and ReliefWidth/ReliefDepth (the
    /// unique anonymous relief block), PositionOffsetDistance (named D6; the two
    /// position-offset samples are 0.005/0.010) and UsePositionOffset (the u32 at
    /// D6 value + 0x68; 0 in the original series, 1 in both position-offset
    /// samples). The remaining field PositionOffsetType is constant 1 in every
    /// native snapshot and has no disambiguating sample, so it stays unresolved.
    /// </para>
    /// <para>
    /// D8/D9 (display copies of the relief depth, not the authoritative width),
    /// ReliefRatio, the relief/default flags, PositionOffsetType and the
    /// persistent edge references remain unresolved. Any
    /// missing/duplicate/tampered layout returns null.
    /// </para>
    /// </summary>
    internal static IFeatureData? ParseEdgeFlange(
        MfcDocument doc, MfcElement element, int windowStart, int windowEnd)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2 || BitConverter.ToUInt16(raw, 0) != 0x74) return null;
        if (!IsEdgeFlangeNode(element)) return null;

        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (header.TypeId == FreshSingleEdgeSerializedTypeId)
            return ParseFreshSingleEdgeFlange(doc, element, windowStart, windowEnd);
        if (header.TypeId != EdgeFlangeFeatureDefinitionData.ObservedSerializedTypeId) return null;

        windowStart = Math.Max(0, windowStart);
        windowEnd = Math.Min(raw.Length, windowEnd);
        if (windowStart >= element.TagOffset || element.TagOffset >= windowEnd) return null;

        var elements = doc.AllElements
            .Where(e => e.TagOffset >= windowStart && e.TagOffset < windowEnd)
            .ToList();

        // Single controlled selection block: exactly one moEdgeRef_c / moCompEdge_c
        // between the previous top-level folder and the feature tag.
        var edgeRefs = elements.Where(e =>
            e.ClassName == "moEdgeRef_c" && e.TagOffset < element.TagOffset).ToList();
        var compEdges = elements.Where(e =>
            e.ClassName == "moCompEdge_c" && e.TagOffset < element.TagOffset).ToList();
        if (edgeRefs.Count != 1 || compEdges.Count != 1) return null;

        var edgeRef = edgeRefs[0];
        if (edgeRef.DataEnd - edgeRef.DataStart < 4) return null;
        uint edgeCount = BitConverter.ToUInt32(raw, edgeRef.DataStart);
        // Only the observed single-edge layout is established.
        if (edgeCount != 1) return null;

        // BendRadius: first named D1 after the feature tag and before the first
        // angular display dimension. This excludes the template/base-flange D1s
        // before the feature and the global/other D1 copies later in the block.
        var firstAngular = elements
            .Where(e => e.ClassName == "moDisplayAngularDim_c" && e.TagOffset > element.TagOffset)
            .OrderBy(e => e.TagOffset)
            .FirstOrDefault();
        if (firstAngular == null) return null;

        if (!TryReadUniqueNamed(raw, "D1", element.TagOffset, firstAngular.TagOffset, out double radius))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(radius)) return null;

        // BendAngle: unique named D4 in the angular dimension block.
        if (!TryReadUniqueNamed(raw, "D4", firstAngular.TagOffset, windowEnd,
                out double angle, out int d4NameOffset))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(angle) || angle > Math.PI) return null;

        // OffsetDistance: named D7. The length-20 sample is the single-variable
        // proof: D7 changes 0.010 -> 0.020 while the neighbouring D6 stays 0.010.
        if (!TryReadUniqueNamed(raw, "D7", firstAngular.TagOffset, windowEnd,
                out double offsetDistance, out int d7NameOffset))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(offsetDistance)) return null;

        // D6 is PositionOffsetDistance. The position-offset samples prove it:
        // edge_flange_position_offset = 0.005 and position_offset10 = 0.010,
        // while every other edge-flange value is unchanged.
        if (!TryReadUniqueNamed(raw, "D6", firstAngular.TagOffset, windowEnd,
                out double positionOffsetDistance, out int d6Name))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(positionOffsetDistance)) return null;

        // UsePositionOffset: u32 immediately after the D6 value block. All six
        // original variants/resaves store 0 (native false); the two position-offset
        // samples + resaves store 1 (native true). The absolute D6 offset shifts on
        // resave, so the relative +0x68 from the value is used.
        int usePositionOffsetSlot = d6Name + 0x1E + 0x68;
        if (usePositionOffsetSlot < 0 || usePositionOffsetSlot + 4 > raw.Length) return null;
        uint usePositionOffsetFlag = BitConverter.ToUInt32(raw, usePositionOffsetSlot);
        if (usePositionOffsetFlag > 1) return null;

        // The authoritative relief/position record is an untagged anonymous block
        // between the D4 and D7 named-parameter records. It was found uniquely in
        // every controlled variant and resave.
        if (!TryReadReliefAndPositionBlock(raw, d4NameOffset, d7NameOffset,
                out double reliefWidth, out double reliefDepth, out int positionType))
            return null;

        var data = new EdgeFlangeFeatureDefinitionData
        {
            BendRadius = radius,
            BendAngle = angle,
            EdgeCount = checked((int)edgeCount),
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

    /// <summary>
    /// Strict parser for the fresh single-edge layout (FolderHeader TypeId 0x75)
    /// produced by InsertSheetMetalEdgeFlange2 on a simple L-shaped base flange.
    /// Evidence: fresh native EdgeFlange with single-variable ModifyDefinition
    /// variants for UseDefaultBendRadius/BendRadius, relief width/depth,
    /// UseReliefRatio/UseDefaultBendRelief and UsePositionOffset/distance, each
    /// saved and reopened. Slots:
    /// BendRadius at the D1 named value; UseDefaultBendRadius at D1 name +0x4A8;
    /// the anonymous relief record at D4 name +0x30DD with relay ratio 0.5,
    /// width +0x08, depth +0x10, UseReliefRatio +0x18 and UseDefaultBendRelief
    /// +0x48, PositionType +0x20; UsePositionOffset at D6 name +0x86; D6 is
    /// PositionOffsetDistance, D4 BendAngle and D7 OffsetDistance.
    /// PositionOffsetType=2 does not persist (native readback reverts to 1), so
    /// it stays unresolved.
    /// </summary>
    private static IFeatureData? ParseFreshSingleEdgeFlange(
        MfcDocument doc, MfcElement element, int windowStart, int windowEnd)
    {
        byte[] raw = doc.SourceData;
        windowStart = Math.Max(0, windowStart);
        windowEnd = Math.Min(raw.Length, windowEnd);
        if (windowStart >= element.TagOffset || element.TagOffset >= windowEnd) return null;

        var elements = doc.AllElements
            .Where(e => e.TagOffset >= windowStart && e.TagOffset < windowEnd)
            .ToList();
        var edgeRefs = elements.Where(e =>
            e.ClassName == "moEdgeRef_c" && e.TagOffset < element.TagOffset).ToList();
        var compEdges = elements.Where(e =>
            e.ClassName == "moCompEdge_c" && e.TagOffset < element.TagOffset).ToList();
        if (edgeRefs.Count != 1 || compEdges.Count != 1) return null;
        if (edgeRefs[0].DataEnd - edgeRefs[0].DataStart < 4) return null;
        uint edgeCount = BitConverter.ToUInt32(raw, edgeRefs[0].DataStart);
        if (edgeCount != 1) return null;

        if (!TryReadUniqueNamed(raw, "D4", element.TagOffset, windowEnd,
                out double angle, out int d4Name)) return null;
        if (!SheetMetalTemplateParser.PositiveFinite(angle) || angle > Math.PI) return null;

        if (!TryReadUniqueNamedAllowZero(raw, "D1", element.TagOffset, d4Name,
                out double radius, out int d1Name)) return null;
        if (radius < 0 || double.IsNaN(radius) || double.IsInfinity(radius)) return null;

        if (!TryReadUniqueNamedAllowZero(raw, "D7", d4Name, windowEnd,
                out double offsetDistance, out int d7Name)) return null;
        if (offsetDistance < 0 || double.IsNaN(offsetDistance) || double.IsInfinity(offsetDistance)) return null;

        if (!TryReadUniqueNamed(raw, "D6", d4Name, d7Name,
                out double positionOffsetDistance, out int d6Name)) return null;
        if (!SheetMetalTemplateParser.PositiveFinite(positionOffsetDistance)) return null;

        int defaultRadiusSlot = d1Name + 0x4A8;
        if (defaultRadiusSlot < 0 || defaultRadiusSlot >= d4Name) return null;
        byte defaultRadiusFlag = raw[defaultRadiusSlot];
        if (defaultRadiusFlag > 1) return null;

        int usePositionOffsetSlot = d6Name + 0x1E + 0x68;
        if (usePositionOffsetSlot < 0 || usePositionOffsetSlot + 4 > raw.Length) return null;
        uint usePositionOffsetFlag = BitConverter.ToUInt32(raw, usePositionOffsetSlot);
        if (usePositionOffsetFlag > 1) return null;

        if (!TryReadFreshReliefBlock(raw, d4Name, windowEnd,
                out double reliefRatio, out double reliefWidth, out double reliefDepth,
                out int positionType, out bool useReliefRatio, out bool useDefaultBendRelief))
            return null;

        var data = new EdgeFlangeFeatureDefinitionData
        {
            TypeId = FreshSingleEdgeSerializedTypeId,
            BendRadius = radius,
            BendAngle = angle,
            EdgeCount = checked((int)edgeCount),
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

    /// <summary>
    /// Unique anonymous relief record for the 0x75 fresh layout:
    /// [double ratio=0.5][double width][double depth]
    /// [u32 UseReliefRatio][u32 PositionType][...][u32 UseDefaultBendRelief at 0x48].
    /// </summary>
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

    /// <summary>
    /// Locates the unique anonymous edge-flange record
    /// <c>[double reliefRatio=0.5][double reliefWidth][double reliefDepth]
    /// [u64 flag=1][u32 positionType]</c> between the D4 and D7 name offsets.
    /// <para>
    /// The 0.5 ratio and the u64 flag are layout gates, not resolved values: no
    /// controlled sample varies either one. ReliefWidth/Depth and PositionType
    /// are backed by the relief_custom and pos_outer/pos_bend_outside samples.
    /// A second matching record is ambiguous and rejects the layout.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// As TryReadUniqueNamed but accepts a zero scalar (the fresh single-edge
    /// creation stores D7=0 for the default flange length; a later
    /// ModifyDefinition persists the numeric length).
    /// </summary>
    private static bool TryReadUniqueNamedAllowZero(
        byte[] raw, string name, int start, int end, out double value, out int nameOffset)
    {
        value = 0;
        nameOffset = -1;
        if (name.Length != 2) return false;
        int count = 0;
        double candidate = 0;
        int last = Math.Min(end, raw.Length) - 38;
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != (byte)name[0] || raw[p + 5] != 0 ||
                raw[p + 6] != (byte)name[1] || raw[p + 7] != 0)
                continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8)) continue;
            double v = BitConverter.ToDouble(raw, p + 0x1E);
            if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) continue;
            if (count > 0 && v != candidate) return false;
            if (count == 0) nameOffset = p;
            candidate = v;
            count++;
        }
        if (count != 1) return false;
        value = candidate;
        return true;
    }

    /// <summary>
    /// Reads exactly one named two-character scalar parameter in [start, end).
    /// Duplicate or conflicting records return false; the layout is then unknown.
    /// </summary>
    private static bool TryReadUniqueNamed(
        byte[] raw, string name, int start, int end, out double value)
    {
        return TryReadUniqueNamed(raw, name, start, end, out value, out _);
    }

    /// <summary>
    /// As <see cref="TryReadUniqueNamed(byte[], string, int, int, out double)"/>,
    /// also returning the offset of the (first) named record.
    /// </summary>
    private static bool TryReadUniqueNamed(
        byte[] raw, string name, int start, int end, out double value, out int nameOffset)
    {
        value = 0;
        nameOffset = -1;
        if (name.Length != 2) return false;
        int count = 0;
        double candidate = 0;
        int last = Math.Min(end, raw.Length) - 38;
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != (byte)name[0] || raw[p + 5] != 0 ||
                raw[p + 6] != (byte)name[1] || raw[p + 7] != 0)
                continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8)) continue;
            double v = BitConverter.ToDouble(raw, p + 0x1E);
            if (!SheetMetalTemplateParser.PositiveFinite(v)) continue;
            if (count > 0 && v != candidate) return false;
            if (count == 0) nameOffset = p;
            candidate = v;
            count++;
        }
        if (count == 0) return false;
        value = candidate;
        return true;
    }

    /// <summary>
    /// Parses one <c>moMiterFlange_c</c> payload. Returns null because no native
    /// miter-flange sample has established the serialized layout.
    /// </summary>
    public static IFeatureData? ParseMiterFlange(MfcElement element)
    {
        if (element == null || element.ClassName != MiterFlangeClassName || element.TagByte != 0x01)
            return null;

        return null;
    }
}
