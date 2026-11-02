using System;

namespace SldFile.Binary;

/// <summary>
/// Reads the observed revision-73/74 template dimensions within one template's
/// ownership window. Compact class references make global class-name searches
/// unsuitable when another feature has already declared the dimension classes.
/// </summary>
internal static class SheetMetalTemplateParser
{
    internal static void Read(byte[] raw, int start, int end, SheetMetalBaseFlangeDefinitionData data)
    {
        if (raw.Length < 2) return;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return;
        start = Math.Max(0, start);
        end = Math.Min(raw.Length, end);
        double? candidateRadius = null, candidateThickness = null, candidateReliefRatio = null;
        bool conflictingRadius = false, conflictingThickness = false, conflictingReliefRatio = false;
        for (int p = start; p <= end - 38; p++)
        {
            // Named D1/D3 length parameters followed by the observed scalar
            // descriptor. Validate the descriptor instead of matching UTF-16
            // names alone, which may also occur in display strings.
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != 'D' || raw[p + 5] != 0 || raw[p + 7] != 0)
                continue;
            if (!IsScalarDescriptor(raw, p + 8)) continue;
            if (raw[p + 6] == '1')
            {
                var radius = BitConverter.ToDouble(raw, p + 30);
                // The radial display dimension stores the same radius twice.
                // This also rejects unrelated named parameters in the window.
                if (PositiveFinite(radius) && p + 30 + 0xAE + 8 <= end &&
                    BitConverter.ToDouble(raw, p + 30 + 0xAE) == radius)
                {
                    conflictingRadius |= candidateRadius.HasValue && candidateRadius.Value != radius;
                    candidateRadius = radius;
                }
            }
            else if (raw[p + 6] == '3')
            {
                // Revision-73/74 base-flange template's thickness field. Its
                // count/reserved header is stable across T=1/2 mm and a model
                // with preceding extrusion+fillet using compact class refs.
                int thicknessOffset = p + 0x3CC;
                if (thicknessOffset + 8 > end || BitConverter.ToUInt32(raw, thicknessOffset - 12) != 2)
                    continue;
                bool reservedZero = true;
                for (int k = thicknessOffset - 8; k < thicknessOffset; k++)
                    reservedZero &= raw[k] == 0;
                var thickness = BitConverter.ToDouble(raw, thicknessOffset);
                if (reservedZero && PositiveFinite(thickness))
                {
                    conflictingThickness |= candidateThickness.HasValue && candidateThickness.Value != thickness;
                    candidateThickness = thickness;
                }
            }
            else if (raw[p + 6] == '6')
            {
                // Relief ratio. Evidence: sm_l_relief_ratio03.SLDPRT changes
                // this scalar from 0.5 to 0.3 together with the global D6 copy,
                // while K-factor changes leave D6 untouched.
                var ratio = BitConverter.ToDouble(raw, p + 0x1E);
                if (FiniteNonNegative(ratio))
                {
                    conflictingReliefRatio |= candidateReliefRatio.HasValue &&
                        candidateReliefRatio.Value != ratio;
                    candidateReliefRatio = ratio;
                }
            }
        }
        if (candidateRadius.HasValue && !conflictingRadius)
        {
            data.BendRadius = candidateRadius.Value;
            data.ParameterResolution.BendRadius = true;
        }
        if (candidateThickness.HasValue && !conflictingThickness)
        {
            data.Thickness = candidateThickness.Value;
            data.ParameterResolution.Thickness = true;
        }
        if (candidateReliefRatio.HasValue && !conflictingReliefRatio)
        {
            data.ReliefRatio = candidateReliefRatio.Value;
            data.ParameterResolution.ReliefRatio = true;
        }
    }

    internal static bool PositiveFinite(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);

    internal static bool FiniteNonNegative(double value) => value >= 0 && !double.IsInfinity(value) && !double.IsNaN(value);

    internal static bool IsScalarDescriptor(byte[] raw, int p)
    {
        // 8-byte 2.0, FF FF FF FF, zero DWORD, empty MFC WChar,
        // followed by two reserved zero bytes (22 bytes in total).
        for (int i = 0; i < 7; i++) if (raw[p + i] != 0) return false;
        if (raw[p + 7] != 0x40) return false;
        for (int i = 8; i < 12; i++) if (raw[p + i] != 0xFF) return false;
        for (int i = 12; i < 16; i++) if (raw[p + i] != 0) return false;
        return raw[p + 16] == 0xFF && raw[p + 17] == 0xFE && raw[p + 18] == 0xFF &&
            raw[p + 19] == 0 && raw[p + 20] == 0 && raw[p + 21] == 0;
    }
}
