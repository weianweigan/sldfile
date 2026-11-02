using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Strict reader for the measured thin-extrusion class (<c>moExtruThin_c</c>, FolderHeader TypeId 0x52,
/// ResolvedFeatures stream version 0x70). The thin options are not bits on moExtrusion_c: the class
/// itself carries the thin feature, and the wall type / cap flag live in a measured 28-byte block
/// immediately before <c>moDisplayRadialDim_c</c>. Every anchor is validated; unknown or conflicting
/// layouts return false and leave the definition unresolved instead of guessing.
/// </summary>
internal static class ExtrudeThinFeatureParser
{
    internal const string ClassName = "moExtruThin_c";
    internal const uint TypeId = 0x52;

    // Measured from ExtrudeOptions/extrude_thin_{one_dir,midplane,cap}_1mm plus the two-direction
    // sample, and unchanged in all four native resaves.
    //   displayRadialTag - 28: u32 CapEnds (0/1)
    //   displayRadialTag - 24: f64 wall thickness (equals D5)
    //   displayRadialTag - 16: seven zero bytes
    //   displayRadialTag -  9: 0xC0 marker
    //   displayRadialTag -  8: u8 ThinWallType (0 OneDirection, 2 MidPlane, 3 TwoDirection)
    //   displayRadialTag -  7: seven zero bytes
    // The direction depths and both wall thicknesses are complete named parameters:
    // D1 = depth, D2 = direction-2 depth (TwoDirection only), D5 = wall 1, D6 = wall 2.
    private const int DisplayBlockLength = 28;
    private const int CapOffset = 0;
    private const int ThicknessOffset = 4;
    private const int ZeroTailOffset = 12;
    private const int MarkerOffset = 19;
    private const int WallTypeOffset = 20;

    internal static bool TryParse(
        MfcDocument doc,
        FeatureFolder folder,
        ushort streamVersion,
        int start,
        int end,
        out ExtrudeFeatureDefinitionData? definition)
    {
        definition = null;
        if (streamVersion != 0x70 || folder == null ||
            folder.ClassName != ClassName || folder.Header.TypeId != TypeId) return false;

        byte[] raw = doc.SourceData;
        int limit = Math.Min(end, raw.Length);
        var nodes = doc.AllElements.Where(e => e.TagOffset >= start && e.TagOffset < limit).ToList();
        if (Count(nodes, "moEndSpec_c") != 1 ||
            Count(nodes, "moThinSpec_c") != 1 ||
            Count(nodes, "moDisplayRadialDim_c") != 1 ||
            Count(nodes, "ThreeDRadiusDim_c") != 1 ||
            Count(nodes, "moFavoriteHandle_c") != 1 ||
            Count(nodes, "moFromEndSpec_c") != 1 ||
            Count(nodes, "ParallelPlaneDistanceDim_c") != 1 ||
            Count(nodes, "moLengthParameter_c") != 1) return false;

        var radial = nodes.Single(e => e.ClassName == "moDisplayRadialDim_c");
        int block = radial.TagOffset - DisplayBlockLength;
        if (block < start || radial.TagOffset > limit) return false;
        if (!InRange(raw, block, DisplayBlockLength, limit)) return false;

        uint cap = BitConverter.ToUInt32(raw, block + CapOffset);
        if (cap > 1) return false;
        double blockThickness = BitConverter.ToDouble(raw, block + ThicknessOffset);
        if (!IsPositiveFinite(blockThickness)) return false;
        for (int i = block + ZeroTailOffset; i < block + MarkerOffset; i++)
            if (raw[i] != 0) return false;
        if (raw[block + MarkerOffset] != 0xC0) return false;
        byte wallType = raw[block + WallTypeOffset];
        // 1 (OppDirection) has no controlled sample; only the measured 0/2/3 values are accepted.
        if (wallType != 0 && wallType != 2 && wallType != 3) return false;
        for (int i = block + WallTypeOffset + 1; i < radial.TagOffset; i++)
            if (raw[i] != 0) return false;

        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D1");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D2");
        var d5 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D5");
        var d6 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D6");
        var d7 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D7");
        if (d1.Count != 1 || d5.Count != 1 || d6.Count != 1) return false;
        if (!IsPositiveFinite(d1[0].Value) || !IsPositiveFinite(d5[0].Value) || !IsPositiveFinite(d6[0].Value))
            return false;
        if (Math.Abs(blockThickness - d5[0].Value) > 1e-12) return false;

        // Task 1.1 AutoFilletCorner thin sample: D7 carries the 1 mm radius and the tail
        // u32 (stream length - 14) is the 0/1 AutoFilletCorners flag. The cap sample has
        // D7=0 and flag 0; both source and resave agree.
        bool autoFillet = false;
        double autoFilletRadius = 0;
        if (end == raw.Length)
        {
            if (raw.Length < 14) return false;
            uint flag = BitConverter.ToUInt32(raw, raw.Length - 14);
            if (flag > 1) return false;
            if (flag == 1)
            {
                if (d7.Count != 1 || !IsPositiveFinite(d7[0].Value)) return false;
                autoFillet = true;
                autoFilletRadius = d7[0].Value;
            }
            else if (d7.Count == 1 && Math.Abs(d7[0].Value) > 1e-12)
            {
                return false;
            }
        }
        else if (d7.Count != 0)
        {
            return false;
        }

        bool twoDirections = wallType == 3;
        double depthDirection2 = 0;
        if (twoDirections)
        {
            if (d2.Count != 1 || !IsPositiveFinite(d2[0].Value)) return false;
            depthDirection2 = d2[0].Value;
        }
        else if (d2.Count != 0)
        {
            return false;
        }

        var endSpec = nodes.Single(e => e.ClassName == "moEndSpec_c").RawData;
        if (endSpec == null || endSpec.Length < 24) return false;
        bool reverseDirection = false;
        if (twoDirections)
        {
            // The two-direction thin sample has a fully zeroed direction-1 EndSpec header.
            for (int i = 0; i < 24; i++)
                if (endSpec[i] != 0) return false;
        }
        else
        {
            // Same single-direction Blind header measured for solid Boss (u16@0=0, u32@2=1,
            // u32@6=0, u32@10 in {0,1}, u16@14=0).
            if (BitConverter.ToUInt16(endSpec, 0) != 0 ||
                BitConverter.ToUInt32(endSpec, 2) != 1 ||
                BitConverter.ToUInt32(endSpec, 6) != 0 ||
                BitConverter.ToUInt32(endSpec, 10) > 1 ||
                BitConverter.ToUInt16(endSpec, 14) != 0) return false;
            reverseDirection = BitConverter.ToUInt32(endSpec, 10) == 1;
        }

        definition = new ExtrudeFeatureDefinitionData
        {
            IsCut = false,
            IsThinFeature = true,
            ThinWallType = wallType,
            WallThickness = d5[0].Value,
            WallThicknessDirection2 = d6[0].Value,
            CapEnds = cap == 1,
            CapThickness = 0, // no non-zero cap-thickness sample; deliberately unresolved
            Depth = d1[0].Value,
            DepthDirection2 = depthDirection2,
            BothDirections = twoDirections,
            EndCondition = 0,
            EndConditionDirection2 = 0,
            ReverseDirection = reverseDirection,
            AutoFilletCorners = autoFillet,
            AutoFilletRadius = autoFilletRadius
        };
        string[] resolved =
        {
            nameof(ExtrudeFeatureDefinitionData.IsThinFeature),
            nameof(ExtrudeFeatureDefinitionData.ThinWallType),
            nameof(ExtrudeFeatureDefinitionData.WallThickness),
            nameof(ExtrudeFeatureDefinitionData.WallThicknessDirection2),
            nameof(ExtrudeFeatureDefinitionData.CapEnds),
            nameof(ExtrudeFeatureDefinitionData.Depth),
            nameof(ExtrudeFeatureDefinitionData.EndCondition)
        };
        foreach (string name in resolved)
            definition.MarkResolvedInternal(name);
        if (twoDirections)
        {
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.BothDirections));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DepthDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.EndConditionDirection2));
        }
        else
        {
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.ReverseDirection));
        }
        if (autoFillet)
        {
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.AutoFilletCorners));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.AutoFilletRadius));
        }
        return true;
    }

    private static int Count(List<MfcElement> nodes, string className) =>
        nodes.Count(e => e.ClassName == className);

    private static bool InRange(byte[] raw, int offset, int length, int limit) =>
        offset >= 0 && length >= 0 && offset <= limit && offset + length <= limit;

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
}
