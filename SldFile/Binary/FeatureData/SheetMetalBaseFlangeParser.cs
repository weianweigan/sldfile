using System;
using System.Text;

namespace SldFile.Binary;

/// <summary>
/// Reads dimension parameters and option flags that belong to one
/// <c>moSMBaseFlange_c</c> feature. The base flange folder itself is a small
/// header; its dimensions are serialized as separate named-parameter records and
/// as a small option block in the feature's stream window.
/// </summary>
internal static class SheetMetalBaseFlangeParser
{
    /// <summary>
    /// Reads the feature-local fields. Evidence for each field is documented on
    /// the individual helpers below. Only values that pass the structural checks
    /// are written; conflicting or unknown layouts stay unresolved.
    /// </summary>
    internal static void Read(byte[] raw, int start, int end, SheetMetalBaseFlangeDefinitionData data)
    {
        if (raw == null || raw.Length < 2) return;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return;
        start = Math.Max(0, start);
        end = Math.Min(raw.Length, end);

        double? candidateDepth = null, candidateDepth2 = null;
        bool conflictingDepth = false, conflictingDepth2 = false;
        bool sawD2Record = false, sawBendDimension = false;
        for (int p = start; p <= end - 38; p++)
        {
            // Named parameter "D2"/"D3" followed by the observed scalar descriptor.
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != 'D' || raw[p + 5] != 0 || raw[p + 7] != 0)
                continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8)) continue;

            int valueOffset = p + 0x1E;
            if (valueOffset + 8 > end) continue;

            // D4 (bend angle), D8/D9 (effective relief dimensions) only exist for
            // flanges with a bend. Use them as a gate for bend-specific options.
            if (raw[p + 6] == '4' || raw[p + 6] == '8' || raw[p + 6] == '9')
                sawBendDimension = true;
            if (raw[p + 6] == '2')
                sawD2Record = true;

            double value = BitConverter.ToDouble(raw, valueOffset);
            if (!SheetMetalTemplateParser.PositiveFinite(value)) continue;

            if (raw[p + 6] == '2')
            {
                conflictingDepth |= candidateDepth.HasValue && candidateDepth.Value != value;
                candidateDepth = value;
            }
            else if (raw[p + 6] == '3')
            {
                // Direction 2 distance. Evidence: sm_l_dir2_d5_c.SLDPRT has the
                // extra D3 record = 0.005 while native D2EndConditionDistance is
                // 0.005 and offsetDirections is 2; sm_l_dir2_d5_a and the rest of
                // the D10 family have no such record and native D2 is 0.001.
                conflictingDepth2 |= candidateDepth2.HasValue && candidateDepth2.Value != value;
                candidateDepth2 = value;
            }
        }

        if (candidateDepth.HasValue && !conflictingDepth)
        {
            data.Depth = candidateDepth.Value;
            data.ParameterResolution.Depth = true;
        }
        else if (!sawD2Record && !conflictingDepth && TryReadClosedProfileDepth(raw, start, end, out double closedDepth))
        {
            data.Depth = closedDepth;
            data.ParameterResolution.Depth = true;
        }

        if (candidateDepth2.HasValue && !conflictingDepth2)
        {
            data.DepthDirection2 = candidateDepth2.Value;
            data.BothDirections = true;
            data.ParameterResolution.DepthDirection2 = true;
            data.ParameterResolution.BothDirections = true;
        }

        if (sawBendDimension)
            TryReadAutoReliefType(raw, start, data);
        TryReadGaugeTable(raw, start, end, data);
        TryReadReverseFlags(raw, start, end, data);
    }

    private const string GaugeTableFeatureClass = "moSMEnvTableFeat_c";

    /// <summary>
    /// Gauge-table layout from the 2026-10-31 native retry sample
    /// (retry_gauge_table): the base-flange window contains exactly one
    /// <c>moSMEnvTableFeat_c</c> whose data begins with 04 80 FF FE FF &lt;n&gt;
    /// followed by an n-character WChar table name. Baseline samples contain no
    /// such element (native UseGaugeTable=false). The table file path is a WChar
    /// record with a "Sheet Metal Gauge Tables" segment and an .xlsx/.xls/.csv/
    /// .btl extension; the API readback leaves GaugeTablePath empty while the
    /// stream keeps the full path. Missing element => false; malformed element or
    /// multiple/conflicting candidates => unresolved.
    /// </summary>
    private static void TryReadGaugeTable(
        byte[] raw, int start, int end, SheetMetalBaseFlangeDefinitionData data)
    {
        int gaugeTag = -1, count = 0;
        int last = raw.Length - (6 + GaugeTableFeatureClass.Length);
        for (int p = 0; p <= last; p++)
        {
            if (!IsMfcTagNamed(raw, p, GaugeTableFeatureClass)) continue;
            gaugeTag = p;
            count++;
        }

        if (count == 0)
        {
            data.UseGaugeTable = false;
            data.ParameterResolution.UseGaugeTable = true;
            return;
        }
        if (count != 1) return;

        // The gauge element lives under the global moSheetMetal_c block, outside
        // this feature's own window. Only bind it when the document has exactly
        // one base flange; multi-body ownership is not structurally proven.
        int baseFlangeCount = 0;
        int baseLast = raw.Length - (6 + "moSMBaseFlange_c".Length);
        for (int p = 0; p <= baseLast; p++)
            if (IsMfcTagNamed(raw, p, "moSMBaseFlange_c")) baseFlangeCount++;
        if (baseFlangeCount != 1) return;

        int dataStart = gaugeTag + 6 + GaugeTableFeatureClass.Length;
        if (dataStart + 6 > raw.Length) return;
        if (raw[dataStart] != 0x04 || raw[dataStart + 1] != 0x80 ||
            raw[dataStart + 2] != 0xFF || raw[dataStart + 3] != 0xFE ||
            raw[dataStart + 4] != 0xFF)
            return;
        int chars = raw[dataStart + 5];
        if (chars < 1 || chars > 128 || dataStart + 6 + chars * 2 > raw.Length) return;
        string name = Encoding.Unicode.GetString(raw, dataStart + 6, chars * 2);
        if (string.IsNullOrWhiteSpace(name)) return;

        data.UseGaugeTable = true;
        data.ParameterResolution.UseGaugeTable = true;
        data.ThicknessTableName = name;
        data.ParameterResolution.ThicknessTableName = true;

        if (TryFindGaugeTablePath(raw, out string path))
        {
            data.GaugeTablePath = path;
            data.ParameterResolution.GaugeTablePath = true;
        }
    }

    private static bool TryFindGaugeTablePath(byte[] raw, out string path)
    {
        path = string.Empty;
        string? candidate = null;
        int count = 0;
        for (int p = 0; p <= raw.Length - 6; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF) continue;
            int chars = raw[p + 3];
            if (chars < 8 || chars > 260 || p + 4 + chars * 2 > raw.Length) continue;
            string text = Encoding.Unicode.GetString(raw, p + 4, chars * 2);
            if (text.IndexOf("Sheet Metal Gauge Tables", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!EndsWithTableExtension(text)) continue;
            if (candidate != null && !string.Equals(candidate, text, StringComparison.Ordinal))
                return false;
            candidate = text;
            count++;
        }
        if (candidate == null) return false;
        path = candidate;
        return count >= 1;
    }

    private static bool EndsWithTableExtension(string text)
    {
        foreach (string extension in new[] { ".xlsx", ".xls", ".csv", ".btl" })
            if (text.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Closed flat profiles serialize the depth as an unnamed scalar immediately
    /// before the flat-pattern template tag. The exact framing observed in all
    /// controlled closed-profile samples is required; anything else is left
    /// unresolved instead of guessing.
    /// Evidence: sm_base_flange_10mm (0.010 at 0x4C90, tag 0x4CB8) vs
    /// sm_base_flange_20mm (0.020 at the same offset); thick2/rad3 keep 0.010;
    /// sm_base_flange_after_fillet (0x74, 0.010 at 0x7412, tag 0x743A). Native
    /// D1EndConditionDistance was re-read by Agent 5 as 0.010/0.020.
    /// </summary>
    private static bool TryReadClosedProfileDepth(byte[] raw, int start, int end, out double value)
    {
        value = 0;
        if (end < start + 0x30) return false;
        if (!IsMfcTagNamed(raw, end, "moTemplateFlatPattern_c")) return false;

        int valueOffset = end - 0x28;
        // The scalar belongs to the sheet-metal body feature that precedes the
        // flat-pattern template; without that tag the ownership is unproven.
        int compFeatureTag = FindLastMfcTag(raw, start, valueOffset, "moCompSheetmetalFeat_c");
        if (compFeatureTag < 0) return false;

        for (int i = valueOffset - 8; i < valueOffset; i++)
            if (raw[i] != 0) return false;
        for (int i = valueOffset + 8; i < end; i++)
            if (raw[i] != 0) return false;

        double candidate = BitConverter.ToDouble(raw, valueOffset);
        if (!SheetMetalTemplateParser.PositiveFinite(candidate)) return false;
        value = candidate;
        return true;
    }

    /// <summary>
    /// Auto relief type is a 4-byte internal code at <c>moSMBaseFlange_c tag - 0x28</c>,
    /// followed by four zero bytes. Observed codes and native autoReliefType:
    /// 0 -> 1 Rectangular (all baseline samples), 1 -> 2 Tear
    /// (sm_l_relief_tear), 2 -> 3 Obround (sm_l_relief_obround),
    /// 4 -> 4 None (sm_l_relief_none). Unknown codes stay unresolved.
    /// </summary>
    private static void TryReadAutoReliefType(byte[] raw, int start, SheetMetalBaseFlangeDefinitionData data)
    {
        if (start < 0x2C) return;
        if (!IsMfcTagNamed(raw, start, "moSMBaseFlange_c")) return;
        int valueOffset = start - 0x28;
        if (valueOffset + 8 > raw.Length) return;

        for (int i = valueOffset + 4; i < valueOffset + 8; i++)
            if (raw[i] != 0) return;

        uint code = BitConverter.ToUInt32(raw, valueOffset);
        uint apiValue = code switch
        {
            0 => 1, // Rectangular
            1 => 2, // Tear
            2 => 3, // Obround
            4 => 4, // None
            _ => 0
        };
        if (apiValue == 0) return;
        data.AutoReliefType = apiValue;
        data.ParameterResolution.AutoReliefType = true;
    }

    /// <summary>
    /// ReverseThickness and ReverseDirection are the boolean bytes at
    /// <c>moCompSheetmetalFeat_c tag + 0x8B</c> and <c>+ 0x8F</c>, each followed
    /// by three zero bytes. Evidence: sm_l_rev_thickness.SLDPRT sets only the
    /// first byte (native ReverseThickness=true) and sm_l_rev_direction.SLDPRT
    /// sets only the second (native ReverseDirection=true); both resaves agree.
    /// </summary>
    private static void TryReadReverseFlags(byte[] raw, int start, int end, SheetMetalBaseFlangeDefinitionData data)
    {
        int compTag = -1, compCount = 0;
        int last = Math.Min(end, raw.Length) - (6 + "moCompSheetmetalFeat_c".Length);
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (!IsMfcTagNamed(raw, p, "moCompSheetmetalFeat_c")) continue;
            compTag = p;
            compCount++;
        }
        if (compCount != 1) return;
        if (compTag + 0x93 > end) return;

        // Only the layout observed for flanges with a bend carries this compact
        // class indicator at +0x89. Closed flat profiles have a different tail
        // (the byte is zero and an unrelated flag sits at +0x91), so their
        // reverse state must stay unresolved instead of reading the wrong bytes.
        if (raw[compTag + 0x89] != 0xC4 && raw[compTag + 0x89] != 0xCB) return;
        if (raw[compTag + 0x8A] != 0) return;

        byte reverseThickness = raw[compTag + 0x8B];
        byte reverseDirection = raw[compTag + 0x8F];
        if (reverseThickness > 1 || reverseDirection > 1) return;
        for (int i = 0x8C; i <= 0x8E; i++) if (raw[compTag + i] != 0) return;
        for (int i = 0x90; i <= 0x92; i++) if (raw[compTag + i] != 0) return;

        data.ReverseThickness = reverseThickness != 0;
        data.ReverseDirection = reverseDirection != 0;
        data.ParameterResolution.ReverseThickness = true;
        data.ParameterResolution.ReverseDirection = true;
    }

    /// <summary>
    /// K-factor is the named D2 scalar in the global <c>moSheetMetal_c</c> window.
    /// Evidence: sm_l_k03_fixed/SLDPRT and sm_l_k04_fixed.SLDPRT change this
    /// scalar to 0.3/0.4 (value offset name+0x1E) while the base-flange template
    /// D2 stays 0.5; native flange KFactor and sheetMetalKFactor read 0.3/0.4.
    /// sm_l_relief_ratio03 keeps this scalar at 0.5 while D6 becomes 0.3, which
    /// separates KFactor from ReliefRatio.
    /// </summary>
    internal static bool TryReadKFactor(byte[] raw, int start, int end, out double value)
    {
        value = 0;
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return false;
        start = Math.Max(0, start);
        end = Math.Min(raw.Length, end);
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;

        double? candidate = null;
        bool conflict = false;
        for (int p = start; p <= end - 38; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != 'D' || raw[p + 5] != 0 ||
                raw[p + 6] != '2' || raw[p + 7] != 0)
                continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8)) continue;
            int valueOffset = p + 0x1E;
            if (valueOffset + 8 > end) continue;
            double v = BitConverter.ToDouble(raw, valueOffset);
            if (!SheetMetalTemplateParser.PositiveFinite(v)) continue;
            conflict |= candidate.HasValue && candidate.Value != v;
            candidate = v;
        }

        if (!candidate.HasValue || conflict) return false;
        value = candidate.Value;
        return true;
    }

    /// <summary>
    /// Custom bend-allowance type/value from the global <c>moSheetMetal_c</c>
    /// window. The type byte sits at D2 name - 0x2A2, framed by
    /// FF FE FF 00 01 before it and DF 80 after it. Native retry evidence
    /// (2026-10-31): code 1 -> API 2 KFactor (baseline, sm_l_k03_fixed and the
    /// gauge-table sample), code 3 -> API 4 Deduction (retry_allowance_deduction,
    /// persisted BendDeduction=0.001 in D3), code 2 -> API 3 Direct (persisted by
    /// the same route; see the direct retry sample). Code 0 / BendTable has no
    /// persisted sample and stays unresolved.
    /// </summary>
    internal static bool TryReadBendAllowance(
        byte[] raw, int start, int end, SheetMetalBaseFlangeDefinitionData data)
    {
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return false;
        start = Math.Max(0, start);
        end = Math.Min(end, raw.Length);
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;

        if (!TryReadUniqueNamedScalar(raw, "D2", start, end, out double d2, out int d2Name))
            return false;
        int typeOffset = d2Name - 0x2A2;
        if (typeOffset - 12 < 0 || typeOffset + 2 >= end) return false;
        // Framing observed in both the older 0x73 samples and the 2026-10-31
        // retry samples: an empty WChar (FF FE FF 00 01) followed by zeros (and,
        // in the older layout, one extra 01) before the type byte, then DF 80.
        bool framed = false;
        for (int q = typeOffset - 12; q <= typeOffset - 2; q++)
        {
            if (raw[q] != 0xFF || raw[q + 1] != 0xFE || raw[q + 2] != 0xFF ||
                raw[q + 3] != 0x00 || raw[q + 4] != 0x01)
                continue;
            bool padding = true;
            for (int k = q + 5; k < typeOffset; k++)
            {
                if (raw[k] != 0x00 && raw[k] != 0x01) { padding = false; break; }
            }
            if (padding) { framed = true; break; }
        }
        if (!framed) return false;

        uint apiType = raw[typeOffset] switch
        {
            1 => 2, // KFactor
            2 => 3, // Direct
            3 => 4, // Deduction
            _ => 0
        };
        if (apiType == 0) return false;

        data.BendAllowanceType = apiType;
        data.ParameterResolution.BendAllowanceType = true;

        if (apiType == 2) return true; // KFactor value is read by TryReadKFactor.
        if (!TryReadUniqueNamedScalar(raw, "D3", d2Name + 1, end, out double d3, out _))
            return true;
        if (d3 <= 0) return true;
        if (apiType == 3)
        {
            data.BendAllowance = d3;
            data.ParameterResolution.BendAllowance = true;
        }
        else
        {
            data.BendDeduction = d3;
            data.ParameterResolution.BendDeduction = true;
        }
        return true;
    }

    /// <summary>
    /// UseDefaultBendRelief from the global <c>moSheetMetal_c</c> element data.
    /// The byte at dataStart + 0x1B is a two-bit override mask (0x20 bend
    /// allowance default, 0x40 bend relief default). Native retry evidence:
    /// baseline/kfactor/deduction/gauge 0x60 or 0x40 -> true; the global
    /// AutoRelief override sample 0x20 -> false. Other bits stay unresolved.
    /// </summary>
    internal static bool TryReadUseDefaultBendRelief(
        byte[] raw, int start, SheetMetalBaseFlangeDefinitionData data)
    {
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return false;
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;
        int dataStart = start + 6 + "moSheetMetal_c".Length;
        if (dataStart + 0x20 > raw.Length) return false;
        if (raw[dataStart + 0x18] != 0 || raw[dataStart + 0x19] != 0 ||
            raw[dataStart + 0x1A] != 0 || raw[dataStart + 0x1C] != 0xFF)
            return false;
        byte flags = raw[dataStart + 0x1B];
        if ((flags & ~0x60) != 0) return false;

        data.UseDefaultBendRelief = (flags & 0x40) != 0;
        data.ParameterResolution.UseDefaultBendRelief = true;
        return true;
    }

    private static bool TryReadUniqueNamedScalar(
        byte[] raw, string name, int start, int end, out double value, out int nameOffset)
    {
        value = 0;
        nameOffset = -1;
        if (string.IsNullOrEmpty(name)) return false;
        int descriptorOffset = 4 + name.Length * 2;
        int valueOffset = descriptorOffset + 0x1E - 8;
        int last = Math.Min(end, raw.Length) - (valueOffset + 8);
        double? candidate = null;
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != name.Length)
                continue;
            bool match = true;
            for (int k = 0; k < name.Length; k++)
            {
                if (raw[p + 4 + 2 * k] != (byte)name[k] || raw[p + 5 + 2 * k] != 0)
                {
                    match = false;
                    break;
                }
            }
            if (!match) continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + descriptorOffset)) continue;
            double v = BitConverter.ToDouble(raw, p + valueOffset);
            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
            if (candidate.HasValue && candidate.Value != v) return false;
            candidate = v;
            if (nameOffset < 0) nameOffset = p;
        }
        if (!candidate.HasValue) return false;
        value = candidate.Value;
        return true;
    }

    /// <summary>
    /// Custom bend-table path for the tutorial generation (versions 0x155/0x1AA/
    /// 0xC7). Evidence: Paper Airplane and drive sled natively report a non-null
    /// SheetMetal/BaseFlange BendTableFile and their moSheetMetal_c window contains
    /// one WChar "…\Sheetmetal Bend Tables\base bend table.xls"; Complex natively
    /// reports null and its stream contains no such path. The path is unique in
    /// the window; conflicting candidates stay unresolved.
    /// </summary>
    internal static bool TryReadBendTableFile(
        byte[] raw, int start, int end, out string path)
    {
        path = string.Empty;
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x155 && version != 0x1AA && version != 0xC7) return false;
        start = Math.Max(0, start);
        end = Math.Min(end, raw.Length);
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;

        string? candidate = null;
        for (int p = start; p <= end - 6; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF) continue;
            int chars = raw[p + 3];
            if (chars < 8 || chars > 260 || p + 4 + chars * 2 > end) continue;
            string text = Encoding.Unicode.GetString(raw, p + 4, chars * 2);
            if (text.IndexOf("Bend Tables", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (!text.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) &&
                !text.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
                !text.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                continue;
            if (candidate != null && !string.Equals(candidate, text, StringComparison.Ordinal))
                return false;
            candidate = text;
        }
        if (candidate == null) return false;
        path = candidate;
        return true;
    }

    private static int FindLastMfcTag(byte[] raw, int start, int end, string name)
    {
        int found = -1;
        int last = Math.Min(end, raw.Length) - (6 + name.Length);
        for (int p = Math.Max(0, start); p <= last; p++)
            if (IsMfcTagNamed(raw, p, name)) found = p;
        return found;
    }

    private static bool IsMfcTagNamed(byte[] raw, int tagOffset, string name)
    {
        if (tagOffset < 0 || tagOffset + 6 + name.Length > raw.Length) return false;
        if (raw[tagOffset] != 0xFF || raw[tagOffset + 1] != 0xFF ||
            raw[tagOffset + 2] != 0x01 || raw[tagOffset + 3] != 0x00)
            return false;
        int length = raw[tagOffset + 4] | (raw[tagOffset + 5] << 8);
        if (length != name.Length) return false;
        for (int i = 0; i < name.Length; i++)
            if (raw[tagOffset + 6 + i] != (byte)name[i]) return false;
        return true;
    }
}
