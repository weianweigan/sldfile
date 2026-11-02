using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Read-only legacy tutorial sweep fallback. The modern 0x70 selection decoder
/// rejects these older layouts, so only the option slots that were proven by
/// native ModifyDefinition save/reopen probes are promoted.
/// <para>
/// Families are keyed by (ResolvedFeatures stream version, FolderHeader TypeId).
/// The option block is anchored at the first <c>moWireContact_c</c> after the sweep
/// class tag; its delta from the class tag and the merge/path/angle offsets were
/// verified on the original tutorial stream and on modified copies.
/// </para>
/// <para>
/// TwistControlType is promoted only when the measured
/// <c>moTwistDataForBiDirectionalSweep_c</c> marker is present (=8). Values 0/1 are
/// not guessed from absence. PathAlignmentType maps only internal 0 -> 0 and
/// internal 1 -> 10 (the two native values proven by probes); other values stay null.
/// </para>
/// </summary>
internal static class LegacySweepLoftFeatureParsers
{
    internal const string SweepClassName = "moSweep_c";

    private sealed class SweepFamily
    {
        public ushort StreamVersion { get; set; }
        public uint TypeId { get; set; }
        public int WireContactDelta { get; set; }
        public int MergeOffset { get; set; }
        public int PathAlignmentOffset { get; set; }
        public int TwistAngleOffset { get; set; }
    }

    private static readonly SweepFamily[] SweepFamilies =
    {
        // motor Sweep1: stream 0x0CFF, TypeId 0x123, native Merge=false/PathAlignment=10.
        new() { StreamVersion = 0x0CFF, TypeId = 0x123, WireContactDelta = 0x68F, MergeOffset = 0xCC2, PathAlignmentOffset = 0xCC7, TwistAngleOffset = 0xCCB },
        // dimxpert/mbd spring Sweep1: 0x0073/0x2E, native Merge=true/PathAlignment=0.
        new() { StreamVersion = 0x0073, TypeId = 0x2E, WireContactDelta = 0x6B8, MergeOffset = 0x161, PathAlignmentOffset = 0x17E, TwistAngleOffset = 0x169 },
        // tutorial/api/maingrip Sweep1: 0x007B/0xCF, native Merge=true/PathAlignment=0/Twist=1.
        new() { StreamVersion = 0x007B, TypeId = 0xCF, WireContactDelta = 0xABF, MergeOffset = 0x147, PathAlignmentOffset = 0x162, TwistAngleOffset = 0x157 },
        // routing tube Sweep-Thin1: 0x0069/0x159, native Merge=false/PathAlignment=0.
        new() { StreamVersion = 0x0069, TypeId = 0x159, WireContactDelta = 0x8AA, MergeOffset = 0x147, PathAlignmentOffset = 0x164, TwistAngleOffset = 0x14F }
    };

    public static IFeatureData? ParseSweep(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null || className != SweepClassName || start < 0)
            return null;

        var element = doc.AllElements.FirstOrDefault(e =>
            e.TagByte == 0x01 && e.TagOffset == start && e.ClassName == SweepClassName);
        if (element == null)
            return null;

        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (!header.IsValid || header.TypeId == 0)
            return null;
        if (typeId != 0 && header.TypeId != typeId)
            return null;

        byte[] data = doc.SourceData;
        ushort streamVersion = data.Length >= 2 ? BitConverter.ToUInt16(data, 0) : (ushort)0;
        var family = SweepFamilies.FirstOrDefault(f =>
            f.StreamVersion == streamVersion && f.TypeId == header.TypeId);
        if (family == null)
            return null;

        var tags = MfcPrimitives.ScanMfcTags(data);
        var wire = tags.FirstOrDefault(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > start);
        if (wire == null || wire.TagOffset - start != family.WireContactDelta)
            return null;

        int anchor = wire.TagOffset;
        bool? merge = null;
        int? pathAlignment = null;
        double? twistAngle = null;
        int? twistControlType = null;

        int mergeOffset = anchor + family.MergeOffset;
        if (mergeOffset >= 0 && mergeOffset < data.Length && data[mergeOffset] <= 1)
            merge = data[mergeOffset] != 0;

        int pathOffset = anchor + family.PathAlignmentOffset;
        if (pathOffset >= 0 && pathOffset < data.Length)
        {
            pathAlignment = data[pathOffset] switch
            {
                0 => 0,
                1 => 10,
                _ => (int?)null
            };
        }

        int angleOffset = anchor + family.TwistAngleOffset;
        if (angleOffset >= 0 && angleOffset + 8 <= data.Length)
        {
            double angle = BitConverter.ToDouble(data, angleOffset);
            if (!double.IsNaN(angle) && !double.IsInfinity(angle) &&
                Math.Abs(angle) <= Math.PI * 2 + 1e-9)
                twistAngle = angle;
        }

        // Measured marker for constant twist; absence is not interpreted as 0/1.
        // Bound the search to this feature's measured option block so a later sweep
        // with constant twist cannot leak into this instance.
        long markerLimit = (long)anchor + 0x400;
        if (tags.Any(t => t.ClassName == "moTwistDataForBiDirectionalSweep_c" &&
                          t.TagOffset > start && t.TagOffset < markerLimit))
            twistControlType = 8;

        return new SweepFeatureDefinitionData(
            element.AsWChar() ?? string.Empty,
            header.TypeId,
            Array.Empty<ushort>(),
            Array.Empty<SketchCurveReference>(),
            false,
            null,
            false,
            null,
            false,
            merge,
            twistControlType,
            twistAngle,
            pathAlignment);
    }
}

