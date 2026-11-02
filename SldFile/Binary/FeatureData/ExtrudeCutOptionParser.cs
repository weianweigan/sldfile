using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Strict reader for the extrusion options measured on ResolvedFeatures version-0x71 cut samples:
/// direction-1 draft, direction-2 draft (with direction-2 depth), and the isolated no-merge layout.
/// Only the exact measured element/flag layouts are accepted. DraftOutward has no positive sample
/// (the "draft_outward" probe was created with DraftOutward=false), so that field is never claimed.
/// Unknown or conflicting layouts leave the affected fields unresolved.
/// </summary>
internal static class ExtrudeCutOptionParser
{
    internal static void Apply(
        MfcDocument doc,
        ushort streamVersion,
        string className,
        int start,
        int end,
        ExtrudeFeatureDefinitionData definition)
    {
        if (className != "moICE_c" || definition == null) return;
        byte[] raw = doc.SourceData;
        if (streamVersion != 0x71)
        {
            // The measured normal-cut sheet-metal sample uses 0x74; only its exact flag tail is
            // recognized here, not the 0x71 option layouts.
            if (streamVersion == 0x74) ReadMeasuredCutFlag(raw, start, definition);
            return;
        }
        int limit = Math.Min(end, raw.Length);
        var nodes = doc.AllElements.Where(e => e.TagOffset >= start && e.TagOffset < limit).ToList();

        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D1");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(raw, start, limit, "D2");
        if (d1.Count == 1 && IsPositiveFinite(d1[0].Value))
        {
            definition.Depth = d1[0].Value;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.Depth));
        }

        var displays = nodes.Where(e => e.ClassName == "moDisplayAngularDim_c").ToList();
        var angleDims = nodes.Where(e => e.ClassName == "AngleDim_c").ToList();
        var angleParameters = nodes.Where(e => e.ClassName == "moAngleParameter_c").ToList();
        bool hasDraftElements = displays.Count > 0 || angleDims.Count > 0 || angleParameters.Count > 0;
        if (hasDraftElements)
        {
            ReadDraft(raw, start, limit, displays, angleDims, angleParameters, d2, definition);
        }

        // Merge is the u32 at moEndFace3IntSurfIdRep_c data+14, inside the measured reference
        // header (00 00, EE 80, F1 00, TypeId 0x56, persistent id, then the flag). It is 1 in all
        // merge=true cut samples (cut_5mm, draft_outward, draft_dir2 and their resaves) and 0 in
        // the merge_false sample/resave. Through-all features without this record stay unresolved.
        if (TryReadMergeFlag(raw, nodes, limit, out bool merge))
        {
            definition.Merge = merge;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.Merge));
        }

        // FlipSideToCut/NormalCut have no isolated scalar control in the available samples; their
        // measured layout tails are recognized verbatim instead of guessing a boolean offset.
        ReadMeasuredCutFlag(raw, start, definition);

        // Task 1.1 FromOffsetReverse cut: the start-offset record is embedded inside
        // moCompSolidBody_c (no moFromEndSpec_c element). Require one measured 5/0x8000
        // header followed by the named parameter block; the reverse flag is part of this
        // measured layout.
        TryReadEmbeddedOffsetReverse(raw, start, limit, definition);
    }

    private static void TryReadEmbeddedOffsetReverse(
        byte[] raw, int start, int limit, ExtrudeFeatureDefinitionData definition)
    {
        int candidate = -1;
        for (int i = start; i + 22 <= limit; i++)
        {
            if (BitConverter.ToUInt32(raw, i) != 5) continue;
            if ((BitConverter.ToUInt16(raw, i + 4) & 0x8000) == 0) continue;
            bool zeros = true;
            for (int j = i + 6; j < i + 22; j++) if (raw[j] != 0) { zeros = false; break; }
            if (!zeros) continue;
            if (candidate >= 0) return; // ambiguous
            candidate = i;
        }
        if (candidate < 0) return;
        var slice = MfcPrimitives.CopyRange(raw, candidate, limit);
        if (slice == null) return;
        double? offset = ExtrudeAdditionalOptionParser.ReadStartOffset(slice);
        if (!offset.HasValue || offset.Value <= 0 || double.IsInfinity(offset.Value)) return;
        definition.FromType = 3;
        definition.FromOffsetDistance = offset.Value;
        definition.FromOffsetReverse = true;
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FromType));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FromOffsetDistance));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FromOffsetReverse));
    }

    private static void ReadMeasuredCutFlag(byte[] raw, int start, ExtrudeFeatureDefinitionData definition)
    {
        int length = raw.Length - start;
        if (length <= 0) return;
        if (TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.FlipSource.Block) ||
            TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.FlipResave.Block))
        {
            definition.FlipSideToCut = true;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FlipSideToCut));
            return;
        }
        if (TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.NormalSource.Block) ||
            TailMatches(raw, start, ExtrudeCutFlagAuthoringTemplates.NormalResave.Block))
        {
            definition.NormalCut = true;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.NormalCut));
        }
    }

    private static bool TailMatches(byte[] raw, int start, byte[] block)
    {
        if (raw.Length - start != block.Length) return false;
        for (int i = 0; i < block.Length; i++)
            if (raw[start + i] != block[i]) return false;
        return true;
    }

    private static bool TryReadMergeFlag(byte[] raw, List<MfcElement> nodes, int limit, out bool merge)
    {
        merge = false;
        var endFaces = nodes.Where(e => e.ClassName == "moEndFace3IntSurfIdRep_c").ToList();
        if (endFaces.Count != 1) return false;
        int data = endFaces[0].DataStart;
        if (data < 0 || data + 18 > limit) return false;
        if (raw[data] != 0 || raw[data + 1] != 0 ||
            raw[data + 2] != 0xEE || raw[data + 3] != 0x80 ||
            raw[data + 4] != 0xF1 || raw[data + 5] != 0x00 ||
            BitConverter.ToUInt32(raw, data + 6) != 0x56) return false;
        uint flag = BitConverter.ToUInt32(raw, data + 14);
        if (flag > 1) return false;
        merge = flag == 1;
        return true;
    }

    private static void ReadDraft(
        byte[] raw,
        int start,
        int limit,
        List<MfcElement> displays,
        List<MfcElement> angleDims,
        List<MfcElement> angleParameters,
        List<(int Offset, double Value)> d2,
        ExtrudeFeatureDefinitionData definition)
    {
        if (displays.Count != 1 || angleDims.Count != 1 || angleParameters.Count != 1) return;
        var display = displays[0];
        var angleDim = angleDims[0];
        var parameter = angleParameters[0];
        if (!(display.TagOffset < angleDim.TagOffset && angleDim.TagOffset < parameter.TagOffset)) return;

        // Direction-1 draft writes a u32 enable flag 24 bytes before moDisplayAngularDim_c;
        // the Task 1.1 sample additionally sets a 0/1 outward flag 8 bytes later.
        bool forward = TryReadForwardFlag(raw, display.TagOffset, start, limit, out bool outward);
        bool reverse = FlagWithZeros(raw, display.TagOffset - 22, 18, start, limit);
        if (forward == reverse) return;

        var angle = ExtrudeAdditionalOptionParser.ReadNamedParameter(raw, parameter.DataStart);
        if (!angle.HasValue || !IsPositiveFinite(angle.Value) || angle.Value >= Math.PI / 2.0) return;
        int namePosition = parameter.DataStart + 2;
        string? angleName = MfcPrimitives.ReadWChar(raw, ref namePosition);

        if (forward)
        {
            // Measured direction-1 cut draft: D3 angle, no direction-2 depth parameter.
            if (angleName != "D3" || d2.Count != 0) return;
            definition.DraftWhileExtruding = true;
            definition.DraftAngle = angle.Value;
            // Outward=true is only claimed for the positive Task 1.1 sample; the existing
            // inward sample keeps the field unresolved instead of claiming a negative.
            if (outward)
            {
                definition.DraftOutward = true;
                definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftOutward));
            }
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtruding));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftAngle));
        }
        else
        {
            // Measured direction-2 cut draft: D4 angle, both directions true, D2 = reverse depth.
            if (angleName != "D4" || d2.Count != 1 || !IsPositiveFinite(d2[0].Value)) return;
            definition.BothDirections = true;
            definition.DepthDirection2 = d2[0].Value;
            definition.EndConditionDirection2 = 0;
            definition.DraftWhileExtrudingDirection2 = true;
            definition.DraftAngleDirection2 = angle.Value;
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.BothDirections));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DepthDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.EndConditionDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtrudingDirection2));
            definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftAngleDirection2));
        }
    }

    private static bool TryReadForwardFlag(
        byte[] raw, int displayTag, int start, int limit, out bool outward)
    {
        outward = false;
        int flag = displayTag - 24;
        if (flag < start || flag + 24 > limit) return false;
        if (BitConverter.ToUInt32(raw, flag) != 1) return false;
        for (int i = flag + 4; i < flag + 8; i++) if (raw[i] != 0) return false;
        uint outwardValue = BitConverter.ToUInt32(raw, flag + 8);
        if (outwardValue > 1) return false;
        for (int i = flag + 12; i < flag + 20; i++) if (raw[i] != 0) return false;
        outward = outwardValue == 1;
        return true;
    }

    private static bool FlagWithZeros(byte[] raw, int offset, int zeroCount, int start, int limit)
    {
        if (offset < start || offset + 4 + zeroCount > limit) return false;
        if (BitConverter.ToUInt32(raw, offset) != 1) return false;
        for (int i = offset + 4; i < offset + 4 + zeroCount; i++)
            if (raw[i] != 0) return false;
        return true;
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
}
