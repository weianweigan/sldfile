using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Mfc;

/// <summary>
/// Unified reader, scanner, and slot validator for MFC 4.2 named parameter records
/// (<c>moLengthParameter_c</c>, <c>moAngleParameter_c</c>, <c>moIntegerParameter_c</c>, and embedded
/// <c>0x8004 + WChar</c> dimension parameter blocks).
/// Eliminates duplicate parameter scanning logic across Extrude, Revolve, Fillet, Chamfer,
/// Pattern, HoleWizard, SheetMetal, and Mate parsers.
/// </summary>
internal static class MfcParameterReader
{
    public const ushort ParameterSchemaMarker = 0x8004;
    public const ushort MateParameterSchemaMarker = 0x8006;
    private const double DescriptorHeaderVersion = 2.0;

    /// <summary>
    /// Reads a named double parameter starting at the <c>0x8004</c> schema prefix at <paramref name="offset"/>.
    /// Layout: <c>u16(0x8004) | WChar(name) | f64(2.0) | u32(0xFFFFFFFF) | u32(0) | WChar("") | u16(0) | f64(value)</c>.
    /// </summary>
    public static double? ReadNamedDouble(
        byte[] raw,
        int offset,
        string? expectedName = null,
        bool requireDNumberFormat = false)
    {
        if (raw == null || offset < 0 || offset + 6 > raw.Length)
            return null;
        if (BitConverter.ToUInt16(raw, offset) != ParameterSchemaMarker ||
            !MfcPrimitives.IsWCharPrefix(raw, offset + 2))
            return null;

        int position = offset + 2;
        string? name = MfcPrimitives.ReadWChar(raw, ref position);
        if (string.IsNullOrEmpty(name))
            return null;

        if (expectedName != null && !string.Equals(name, expectedName, StringComparison.Ordinal))
            return null;

        if (requireDNumberFormat && (name![0] != 'D' || name.Length < 2 || !name.Skip(1).All(char.IsDigit)))
            return null;

        return ReadDoubleDescriptorAfterName(raw, position);
    }

    /// <summary>
    /// Reads the 22-byte double parameter descriptor + 8-byte IEEE 754 value immediately following a WChar parameter name.
    /// </summary>
    public static double? ReadDoubleDescriptorAfterName(byte[] raw, int positionAfterName)
    {
        if (raw == null || positionAfterName < 0 || positionAfterName + 30 > raw.Length)
            return null;

        if (BitConverter.ToDouble(raw, positionAfterName) != DescriptorHeaderVersion ||
            BitConverter.ToUInt32(raw, positionAfterName + 8) != uint.MaxValue ||
            BitConverter.ToUInt32(raw, positionAfterName + 12) != 0 ||
            !MfcPrimitives.IsWCharPrefix(raw, positionAfterName + 16) ||
            raw[positionAfterName + 19] != 0 ||
            BitConverter.ToUInt16(raw, positionAfterName + 20) != 0)
        {
            return null;
        }

        double value = BitConverter.ToDouble(raw, positionAfterName + 22);
        return double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
    }

    /// <summary>
    /// Reads a named integer parameter (<c>moIntegerParameter_c</c>) starting at the <c>0x8004</c> schema prefix.
    /// Layout: <c>u16(0x8004) | WChar(name) | f64(2.0) | u32(0xFFFFFFFF) | u32(0) | WChar("") | u16(0) | u32(value)</c>.
    /// </summary>
    public static uint? ReadNamedUInt32(byte[] raw, int offset, string? expectedName = null)
    {
        if (raw == null || offset < 0 || offset + 6 > raw.Length)
            return null;
        if (BitConverter.ToUInt16(raw, offset) != ParameterSchemaMarker ||
            !MfcPrimitives.IsWCharPrefix(raw, offset + 2))
            return null;

        int position = offset + 2;
        string? name = MfcPrimitives.ReadWChar(raw, ref position);
        if (string.IsNullOrEmpty(name))
            return null;
        if (expectedName != null && !string.Equals(name, expectedName, StringComparison.Ordinal))
            return null;

        if (position + 26 > raw.Length ||
            BitConverter.ToDouble(raw, position) != DescriptorHeaderVersion ||
            BitConverter.ToUInt32(raw, position + 8) != uint.MaxValue ||
            BitConverter.ToUInt32(raw, position + 12) != 0 ||
            !MfcPrimitives.IsWCharPrefix(raw, position + 16) ||
            raw[position + 19] != 0 ||
            BitConverter.ToUInt16(raw, position + 20) != 0)
        {
            return null;
        }

        return BitConverter.ToUInt32(raw, position + 22);
    }

    /// <summary>
    /// Checks whether a parameter at <paramref name="offset"/> has the specified WChar name.
    /// </summary>
    public static bool HasParameterName(byte[] raw, int offset, string expectedName)
    {
        if (raw == null || offset < 0 || offset + 6 > raw.Length ||
            BitConverter.ToUInt16(raw, offset) != ParameterSchemaMarker ||
            !MfcPrimitives.IsWCharPrefix(raw, offset + 2))
            return false;

        int position = offset + 2;
        return string.Equals(MfcPrimitives.ReadWChar(raw, ref position), expectedName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Scans the range <c>[start, end)</c> for all valid <c>0x8004</c>-prefixed named double parameters
    /// matching <paramref name="name"/>.
    /// </summary>
    public static List<(int Offset, double Value)> FindNamedDoubles(
        byte[] raw,
        int start,
        int end,
        string name,
        bool requireDNumberFormat = false)
    {
        var results = new List<(int Offset, double Value)>();
        if (raw == null || string.IsNullOrEmpty(name)) return results;

        int limit = Math.Min(end, raw.Length);
        for (int i = Math.Max(0, start); i + 4 <= limit; i++)
        {
            if (raw[i] != 0x04 || raw[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(raw, i + 2))
                continue;

            var value = ReadNamedDouble(raw, i, expectedName: name, requireDNumberFormat: requireDNumberFormat);
            if (value.HasValue)
                results.Add((i, value.Value));
        }

        return results;
    }

    /// <summary>
    /// Scans <c>[start, end)</c> for all valid <c>0x8004</c>-prefixed named double parameters (regardless of name).
    /// </summary>
    public static List<(string Name, int Offset, double Value)> ScanAllNamedDoubles(byte[] raw, int start, int end)
    {
        var results = new List<(string Name, int Offset, double Value)>();
        if (raw == null) return results;

        int limit = Math.Min(end, raw.Length);
        for (int i = Math.Max(0, start); i + 6 <= limit; i++)
        {
            if (raw[i] != 0x04 || raw[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(raw, i + 2))
                continue;

            int pos = i + 2;
            string? name = MfcPrimitives.ReadWChar(raw, ref pos);
            if (string.IsNullOrEmpty(name))
                continue;

            var value = ReadDoubleDescriptorAfterName(raw, pos);
            if (value.HasValue)
                results.Add((name!, i, value.Value));
        }

        return results;
    }

    /// <summary>
    /// Reads the single <c>0x8004</c>-prefixed named double parameter matching <paramref name="expectedName"/>
    /// inside <paramref name="payload"/>. Returns <c>null</c> if zero or multiple occurrences exist, or if any
    /// occurrence with <paramref name="expectedName"/> has a malformed descriptor.
    /// </summary>
    public static double? ReadUniqueNamedDoubleInSlice(byte[] payload, string expectedName)
    {
        if (payload == null || string.IsNullOrEmpty(expectedName))
            return null;

        double? result = null;
        int count = 0;
        for (int i = 0; i + 6 <= payload.Length; i++)
        {
            if (payload[i] != 0x04 || payload[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(payload, i + 2))
                continue;

            int position = i + 2;
            string? name = MfcPrimitives.ReadWChar(payload, ref position);
            if (!string.Equals(name, expectedName, StringComparison.Ordinal))
                continue;

            double? value = ReadDoubleDescriptorAfterName(payload, position);
            if (!value.HasValue)
                return null;

            result = value;
            count++;
        }

        return count == 1 ? result : null;
    }

    /// <summary>
    /// Scans <c>[start, end)</c> for a unique named double parameter by its WChar prefix (with or without
    /// a preceding <c>0x8004</c> schema word). Returns <c>true</c> only if a single valid occurrence exists
    /// (or if all valid occurrences have the exact same value when <paramref name="allowIdenticalDuplicates"/> is true).
    /// </summary>
    public static bool TryReadUniqueDoubleByWChar(
        byte[] raw,
        string name,
        int start,
        int end,
        out double value,
        out int wcharOffset,
        bool requireSchemaPrefix = false,
        bool allowIdenticalDuplicates = false,
        bool onlyPositive = false,
        bool allowZero = true)
    {
        value = 0;
        wcharOffset = -1;
        if (raw == null || string.IsNullOrEmpty(name)) return false;

        int count = 0;
        double candidate = 0;
        int descriptorOffset = 4 + name.Length * 2;
        int valueOffset = descriptorOffset + 0x16;
        int last = Math.Min(end, raw.Length) - (valueOffset + 8);

        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF || raw[p + 3] != name.Length)
                continue;

            if (requireSchemaPrefix && (p < 2 || raw[p - 2] != 0x04 || raw[p - 1] != 0x80))
                continue;

            int cursor = p;
            if (!string.Equals(MfcPrimitives.ReadWChar(raw, ref cursor), name, StringComparison.Ordinal))
                continue;

            var parsed = ReadDoubleDescriptorAfterName(raw, p + descriptorOffset);
            if (!parsed.HasValue)
                continue;

            double v = parsed.Value;
            if (onlyPositive && (allowZero ? v < 0.0 : v <= 0.0))
                continue;

            if (count > 0)
            {
                if (allowIdenticalDuplicates && v == candidate)
                    continue;
                return false;
            }

            candidate = v;
            wcharOffset = p;
            count++;
        }

        if (count != 1) return false;
        value = candidate;
        return true;
    }

    /// <summary>
    /// Scans <c>[start, end)</c> for the first valid named double parameter matching <paramref name="name"/>
    /// by its WChar prefix.
    /// </summary>
    public static bool TryReadFirstDoubleByWChar(
        byte[] raw,
        string name,
        int start,
        int end,
        out double value,
        out int wcharOffset)
    {
        value = 0;
        wcharOffset = -1;
        if (raw == null || string.IsNullOrEmpty(name)) return false;

        int descriptorOffset = 4 + name.Length * 2;
        int valueOffset = descriptorOffset + 0x16;
        int last = Math.Min(end, raw.Length) - (valueOffset + 8);

        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF || raw[p + 3] != name.Length)
                continue;

            int cursor = p;
            if (!string.Equals(MfcPrimitives.ReadWChar(raw, ref cursor), name, StringComparison.Ordinal))
                continue;

            var parsed = ReadDoubleDescriptorAfterName(raw, p + descriptorOffset);
            if (!parsed.HasValue)
                continue;

            value = parsed.Value;
            wcharOffset = p;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Verifies that all specified byte offsets in <paramref name="raw"/> contain IEEE 754 doubles
    /// matching <paramref name="expectedValue"/> within <paramref name="tolerance"/>.
    /// </summary>
    public static bool VerifyDoubleSlots(
        byte[] raw,
        int[] offsets,
        double expectedValue,
        double tolerance = 1e-12)
    {
        if (raw == null || offsets == null) return false;
        foreach (int slot in offsets)
        {
            if (slot < 0 || slot + 8 > raw.Length)
                return false;
            double copy = BitConverter.ToDouble(raw, slot);
            if (Math.Abs(copy - expectedValue) > tolerance)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Verifies both positive and negative double slots in <paramref name="raw"/> against <paramref name="expectedValue"/>.
    /// </summary>
    public static bool VerifyDoubleSlots(
        byte[] raw,
        int[]? positiveOffsets,
        int[]? negativeOffsets,
        double expectedValue,
        double tolerance = 1e-12)
    {
        if (positiveOffsets != null && !VerifyDoubleSlots(raw, positiveOffsets, expectedValue, tolerance))
            return false;
        if (negativeOffsets != null && !VerifyDoubleSlots(raw, negativeOffsets, -expectedValue, tolerance))
            return false;
        return true;
    }

    /// <summary>
    /// Writes <paramref name="value"/> (or <c>-value</c> for negative slots) into all specified byte offsets.
    /// </summary>
    public static void PatchDoubleSlots(
        byte[] block,
        int[] positiveOffsets,
        double value,
        int[]? negativeOffsets = null)
    {
        if (block == null) throw new ArgumentNullException(nameof(block));
        byte[] posBytes = BitConverter.GetBytes(value);
        if (positiveOffsets != null)
        {
            foreach (int slot in positiveOffsets)
                posBytes.CopyTo(block, slot);
        }

        if (negativeOffsets != null && negativeOffsets.Length > 0)
        {
            byte[] negBytes = BitConverter.GetBytes(-value);
            foreach (int slot in negativeOffsets)
                negBytes.CopyTo(block, slot);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Mate Parameter Helpers (0x8006 schema records)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads a strict 2-character named parameter record (<c>D1</c>, <c>D2</c>, etc.) at <paramref name="pos"/>
    /// bounded by <paramref name="end"/> using the standard 22-byte double parameter descriptor.
    /// </summary>
    public static bool TryReadMateNamedRecord(
        byte[] data,
        int pos,
        int end,
        string name,
        out double value,
        bool allowZero = false,
        double maxValue = 1000.0)
    {
        value = 0;
        if (data == null || string.IsNullOrEmpty(name)) return false;
        int limit = Math.Min(end, data.Length);
        int expectedCursor = pos + 6 + name.Length * 2;
        if (pos < 0 || pos + 32 + name.Length * 2 > limit) return false;
        int cursor = pos + 2;
        if (MfcPrimitives.ReadWChar(data, ref cursor, limit) != name || cursor != expectedCursor)
            return false;

        double? parsed = ReadDoubleDescriptorAfterName(data, cursor);
        if (!parsed.HasValue) return false;
        double v = parsed.Value;
        if ((allowZero ? v < 0.0 : v <= 0.0) || v > maxValue)
            return false;
        value = v;
        return true;
    }

    /// <summary>
    /// Scans <c>[start, end)</c> for a unique <c>0x8006</c>-prefixed named parameter record matching <paramref name="name"/>.
    /// </summary>
    public static double? ReadUniqueMateNamedParameter(
        byte[] data,
        int start,
        int end,
        string name,
        double maxValue = 1000.0)
    {
        if (data == null || string.IsNullOrEmpty(name)) return null;
        int limit = Math.Min(end, data.Length);
        double? result = null;
        int count = 0;
        for (int i = Math.Max(0, start); i + 6 <= limit; i++)
        {
            if (data[i] != 0x06 || data[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(data, i + 2))
                continue;
            int cursor = i + 2;
            string? found = MfcPrimitives.ReadWChar(data, ref cursor, limit);
            if (!string.Equals(found, name, StringComparison.Ordinal)) continue;
            if (!TryReadMateNamedRecord(data, i, limit, name, out double value, allowZero: false, maxValue: maxValue))
                return null;
            if (count > 0) return null;
            result = value;
            count++;
        }
        return count == 1 ? result : null;
    }

    /// <summary>
    /// Patches all 4-byte-aligned copies of <paramref name="oldValue"/> inside <c>[copyRangeStart, copyRangeEnd)</c>
    /// to <paramref name="newValue"/>, and unconditionally writes <paramref name="newValue"/> at <paramref name="authoritativeValueOffset"/>.
    /// </summary>
    public static void PatchParameterValueAndCopies(
        byte[] record,
        int copyRangeStart,
        int copyRangeEnd,
        int authoritativeValueOffset,
        double oldValue,
        double newValue)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        byte[] newBytes = BitConverter.GetBytes(newValue);
        int limit = Math.Min(copyRangeEnd, record.Length);
        for (int i = Math.Max(0, copyRangeStart); i + 8 <= limit; i += 4)
        {
            if (BitConverter.ToDouble(record, i) == oldValue)
                newBytes.CopyTo(record, i);
        }
        newBytes.CopyTo(record, authoritativeValueOffset);
    }
}
