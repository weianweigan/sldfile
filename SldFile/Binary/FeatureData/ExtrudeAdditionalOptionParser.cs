using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class ExtrudeAdditionalOptionParser
{
    internal static void Apply(MfcDocument doc, string className, int start, int end, ExtrudeFeatureDefinitionData definition)
    {
        if (className != "moExtrusion_c" || doc.SourceData.Length < 2 ||
            System.BitConverter.ToUInt16(doc.SourceData, 0) != 0x70) return;
        var nodes = doc.AllElements.Where(e => e.TagOffset >= start && e.TagOffset < end).ToList();
        var specs = nodes.Where(e => e.ClassName == "moEndSpec_c").ToList();
        if (specs.Count != 1) return;
        var spec = specs[0].RawData;
        ReadTwoDirections(nodes, end, definition);
        // The single-direction prefix was controlled separately from the two-direction sample.
        if (spec.Length < 24 || System.BitConverter.ToUInt16(spec, 0) != 0 ||
            System.BitConverter.ToUInt32(spec, 2) != 1 ||
            System.BitConverter.ToUInt32(spec, 6) != 0 ||
            System.BitConverter.ToUInt32(spec, 10) > 1 ||
            System.BitConverter.ToUInt16(spec, 14) != 0) return;
        ReadDraft(nodes, end, definition);
        var from = nodes.Where(e => e.ClassName == "moFromEndSpec_c").ToList();
        if (from.Count == 1)
        {
            var offset = ReadStartOffset(MfcPrimitives.CopyRange(doc.SourceData, from[0].DataStart, end));
            if (offset.HasValue)
            {
                definition.FromType = 3;
                definition.FromOffsetDistance = offset.Value;
                definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FromType));
                definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FromOffsetDistance));
            }
        }
    }

    private static void ReadTwoDirections(List<MfcElement> nodes, int end, ExtrudeFeatureDefinitionData definition)
    {
        var spec = nodes.Single(e => e.ClassName == "moEndSpec_c").RawData;
        if (spec.Length < 24 || BitConverter.ToUInt16(spec, 0) != 0 ||
            BitConverter.ToUInt32(spec, 2) != 0 || BitConverter.ToUInt32(spec, 6) != 0 ||
            BitConverter.ToUInt32(spec, 10) > 1 || BitConverter.ToUInt16(spec, 14) != 0 ||
            BitConverter.ToUInt32(spec, 16) != 0) return;
        var favorites = nodes.Where(e => e.ClassName == "moFavoriteHandle_c").ToList();
        var from = nodes.Where(e => e.ClassName == "moFromEndSpec_c").ToList();
        if (favorites.Count != 1 || from.Count != 1) return;
        var source = favorites[0].SourceData;
        int secondSpec = favorites[0].DataStart + 436;
        int bound = from[0].TagOffset;
        // Version-0x70 indexed layout measured in 3/5/7mm plus a resave control.
        // These reference codes are deliberately not generalized to other archive layouts.
        if (secondSpec + 18 > bound || bound > end || BitConverter.ToUInt16(source, secondSpec) != 0x810B) return;
        for (int i = secondSpec + 2; i < secondSpec + 18; i++) if (source[i] != 0) return;
        int parameter = favorites[0].DataStart + 1111;
        if (parameter + 10 > bound || parameter - 8 < secondSpec) return;
        if (BitConverter.ToUInt16(source, parameter - 8) != 0x810F ||
            BitConverter.ToUInt32(source, parameter - 6) != 2 ||
            BitConverter.ToUInt16(source, parameter - 2) != 0x8111) return;
        var raw = MfcPrimitives.CopyRange(source, parameter, bound);
        int namePosition = 2;
        if (raw.Length < 10 || MfcPrimitives.ReadWChar(raw, ref namePosition) != "D2") return;
        var depth = ReadNamedParameter(raw, 0);
        if (!depth.HasValue || depth <= 0) return;
        definition.BothDirections = true;
        definition.DepthDirection2 = depth.Value;
        definition.EndConditionDirection2 = 0;
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.BothDirections));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DepthDirection2));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.EndConditionDirection2));
    }
    private static void ReadDraft(List<MfcElement> nodes, int end, ExtrudeFeatureDefinitionData definition)
    {
        var angles = nodes.Where(e => e.ClassName == "moAngleParameter_c").ToList();
        if (angles.Count != 1) return;
        int index = nodes.IndexOf(angles[0]);
        if (index < 3 || nodes[index - 3].ClassName != "moFavoriteHandle_c" || nodes[index - 1].ClassName != "AngleDim_c" ||
            nodes[index - 2].ClassName != "moDisplayAngularDim_c") return;
        var display = nodes[index - 2];
        var source = display.SourceData;
        int flag = display.TagOffset - 24;
        if (flag < nodes[index - 3].DataStart || System.BitConverter.ToUInt32(source, flag) != 1) return;
        for (int i = flag + 4; i < display.TagOffset; i++)
            if (source[i] != 0) return;
        // The generic scanner can truncate at FFFF in parameter metadata; bound by the next named object.
        int bound = index + 1 < nodes.Count ? nodes[index + 1].TagOffset : end;
        var raw = MfcPrimitives.CopyRange(source, angles[0].DataStart, bound);
        var value = ReadNamedParameter(raw, 0);
        if (!value.HasValue || value.Value <= 0 || value.Value >= System.Math.PI / 2) return;
        definition.DraftWhileExtruding = true;
        definition.DraftAngle = value.Value; // radians, matching native GetDraftAngle.
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftWhileExtruding));
        definition.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.DraftAngle));
    }

    internal static double? ReadStartOffset(byte[] raw)
    {
        // Offset-start's internal discriminator is 5 (API FromType=3), followed
        // by an indexed EndSpec reference. Only the evidenced unflipped header is accepted.
        if (raw.Length < 24 || System.BitConverter.ToUInt32(raw, 0) != 5 ||
            (System.BitConverter.ToUInt16(raw, 4) & 0x8000) == 0) return null;
        for (int i = 6; i < 22; i++) if (raw[i] != 0) return null;
        double? result = null;
        int count = 0;
        for (int i = 22; i + 6 < raw.Length; i++)
        {
            if (raw[i] != 4 || raw[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(raw, i + 2)) continue;
            var value = ReadNamedParameter(raw, i);
            if (!value.HasValue) continue;
            count++;
            result = value;
        }
        return count == 1 && result >= 0 && !double.IsInfinity(result.Value) ? result : null;
    }

    internal static double? ReadNamedParameter(byte[] raw, int offset)
    {
        if (offset < 0 || offset + 6 > raw.Length || System.BitConverter.ToUInt16(raw, offset) != 0x8004 ||
            !MfcPrimitives.IsWCharPrefix(raw, offset + 2)) return null;
        int position = offset + 2;
        string? name = MfcPrimitives.ReadWChar(raw, ref position);
        if (string.IsNullOrEmpty(name) || name![0] != 'D' || name.Length < 2 || !name.Skip(1).All(char.IsDigit)) return null;
        if (position + 30 > raw.Length || System.BitConverter.ToDouble(raw, position) != 2.0 ||
            System.BitConverter.ToUInt32(raw, position + 8) != uint.MaxValue ||
            System.BitConverter.ToUInt32(raw, position + 12) != 0 ||
            !MfcPrimitives.IsWCharPrefix(raw, position + 16) || raw[position + 19] != 0 ||
            System.BitConverter.ToUInt16(raw, position + 20) != 0) return null;
        double value = System.BitConverter.ToDouble(raw, position + 22);
        return double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
    }

    /// <summary>
    /// Scans the feature range for complete named parameters whose exact WChar name matches.
    /// The full schema/name/metadata header is re-validated through <see cref="ReadNamedParameter"/>,
    /// so display-only fragments, truncated records and duplicated layouts are not promoted.
    /// </summary>
    internal static List<(int Offset, double Value)> FindNamedParameters(byte[] raw, int start, int end, string name)
    {
        var results = new List<(int Offset, double Value)>();
        int limit = Math.Min(end, raw.Length);
        for (int i = Math.Max(0, start); i + 4 <= limit; i++)
        {
            if (raw[i] != 0x04 || raw[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(raw, i + 2)) continue;
            int position = i + 2;
            if (!string.Equals(MfcPrimitives.ReadWChar(raw, ref position), name, StringComparison.Ordinal)) continue;
            var value = ReadNamedParameter(raw, i);
            if (value.HasValue) results.Add((i, value.Value));
        }
        return results;
    }
}
