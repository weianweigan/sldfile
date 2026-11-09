using System;
using System.Collections.Generic;
using System.Text;

namespace SldFile.Mfc;

/// <summary>
/// Locates the authoritative sheet-metal thickness parameter in the
/// <c>Contents/Config-0</c> stream (not the ResolvedFeatures display/dimension
/// copies). Native evidence: the named MFC length parameter "厚度"/"Thickness"
/// changes 0.001 -> 0.002 with native T=1/2 samples
/// (sm_l_profile d10/t2, sm_base_flange_10mm/thick2), while the ResolvedFeatures
/// D3 fields are display copies. Authored T=1.5/2 files left this field at 0.001,
/// so SolidWorks rebuilt with 0.001 even though ResolvedFeatures was patched.
/// </summary>
internal static class SheetMetalConfigThicknessPatcher
{
    private static readonly string[] ParameterNames = { "厚度", "Thickness" };

    internal static bool TryReadThickness(byte[] configStream, out double thickness)
    {
        thickness = 0;
        if (!TryFindSingle(configStream, out _, out double value)) return false;
        thickness = value;
        return true;
    }

    /// <summary>
    /// Returns a patched copy of the Config stream. The existing value must equal
    /// the measured 0.001 thickness; otherwise the target is not the controlled
    /// specimen and the operation is rejected before any stream is modified.
    /// </summary>
    internal static byte[] Patch(byte[] configStream, double thickness)
    {
        if (configStream == null)
            throw new ArgumentNullException(nameof(configStream));

        if (!TryFindSingle(configStream, out int valueOffset, out double current))
        {
            throw new NotSupportedException(
                "Cannot author a sheet metal base flange: the authoritative Config-0 thickness " +
                "parameter ('厚度'/'Thickness') was not found exactly once. No stream changes have been made.");
        }

        if (current != SheetMetalBaseFlangeSegments.MeasuredThickness)
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange: the Config-0 thickness parameter contains " +
                $"{current:R}, expected the measured {SheetMetalBaseFlangeSegments.MeasuredThickness:R}. " +
                "The target is not the controlled specimen. No stream changes have been made.");
        }

        byte[] patched = (byte[])configStream.Clone();
        BitConverter.GetBytes(thickness).CopyTo(patched, valueOffset);
        return patched;
    }

    private static bool TryFindSingle(byte[] raw, out int valueOffset, out double value)
    {
        valueOffset = -1;
        value = 0;
        if (raw == null) return false;

        var candidates = new List<(int Offset, double Value)>();
        foreach (string name in ParameterNames)
        {
            byte[] pattern = BuildNamePattern(name);
            for (int p = 0; p <= raw.Length - Math.Max(pattern.Length, 0x1E + 8); p++)
            {
                bool match = true;
                for (int i = 0; i < pattern.Length; i++)
                {
                    if (raw[p + i] != pattern[i]) { match = false; break; }
                }
                if (!match) continue;
                if (!IsThicknessDescriptor(raw, p + 8)) continue;

                double v = BitConverter.ToDouble(raw, p + 0x1E);
                if (!SheetMetalTemplateParser.PositiveFinite(v)) continue;
                candidates.Add((p + 0x1E, v));
            }
        }

        if (candidates.Count != 1) return false;
        valueOffset = candidates[0].Offset;
        value = candidates[0].Value;
        return true;
    }

    private static byte[] BuildNamePattern(string name)
    {
        var bytes = new List<byte> { 0xFF, 0xFE, 0xFF, (byte)name.Length };
        bytes.AddRange(Encoding.Unicode.GetBytes(name));
        return bytes.ToArray();
    }

    /// <summary>
    /// Observed Config-0 length-parameter descriptor: 8-byte 2.0, FFFFFFFF,
    /// flags 01 00 00 00, empty WChar, two reserved zero bytes. This differs from
    /// the ResolvedFeatures descriptor (zero flags), so the shared helper is not used.
    /// </summary>
    private static bool IsThicknessDescriptor(byte[] raw, int p)
    {
        if (p + 22 > raw.Length) return false;
        for (int i = 0; i < 7; i++) if (raw[p + i] != 0) return false;
        if (raw[p + 7] != 0x40) return false;
        for (int i = 8; i < 12; i++) if (raw[p + i] != 0xFF) return false;
        if (raw[p + 12] != 0x01 || raw[p + 13] != 0 || raw[p + 14] != 0 || raw[p + 15] != 0)
            return false;
        return raw[p + 16] == 0xFF && raw[p + 17] == 0xFE && raw[p + 18] == 0xFF &&
            raw[p + 19] == 0 && raw[p + 20] == 0 && raw[p + 21] == 0;
    }
}
