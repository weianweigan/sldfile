using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading tests for the Agent 5 sheet-metal option matrix
/// (TestFiles/Features2026/SheetMetalOptions). Each field is tied to a
/// single-variable sample pair and to the exact byte structure used by the
/// parser. The 2026-10-31 native retry added persisted Direct/Deduction
/// allowance types, gauge-table true, and UseDefaultBendRelief=false; explicit
/// relief width/depth, useReliefRatio and bend-table persistence still revert
/// natively and remain unresolved.
/// </summary>
public class SheetMetalOptionReadingTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";

    private static string Sample(string name)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalOptions", name);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", "SheetMetalOptions", name);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"SheetMetalOptions/{name} not found");
    }

    private static (SheetMetalBaseFlangeDefinitionData Definition, byte[] Raw, IReadOnlyList<FeatureFolder> Folders) Load(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var folders = file.ResolvedFeatures.Folders;
        var definition = Assert.IsType<SheetMetalBaseFlangeDefinitionData>(
            folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        return (definition, (byte[])file.RawStreams[StreamPath].Clone(), folders);
    }

    private static int NextFolderOffset(IReadOnlyList<FeatureFolder> folders, int tagOffset, int fallback) =>
        folders.Select(f => f.TagOffset).Where(t => t > tagOffset).DefaultIfEmpty(fallback).Min();

    private static int FindNamedParameter(byte[] raw, string name, int start, int end)
    {
        for (int p = Math.Max(0, start); p <= Math.Min(end, raw.Length) - 38; p++)
        {
            if (raw[p] == 0xFF && raw[p + 1] == 0xFE && raw[p + 2] == 0xFF &&
                raw[p + 3] == 2 && raw[p + 4] == (byte)name[0] && raw[p + 5] == 0 &&
                raw[p + 6] == (byte)name[1] && raw[p + 7] == 0 &&
                SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8))
                return p;
        }
        return -1;
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", false, false)]
    [InlineData("sm_l_rev_thickness.SLDPRT", true, false)]
    [InlineData("sm_l_rev_direction.SLDPRT", false, true)]
    [InlineData("sm_l_rev_thickness_resave.SLDPRT", true, false)]
    [InlineData("sm_l_rev_direction_resave.SLDPRT", false, true)]
    public void ReadsReverseFlagsFromCompSheetmetalFeature(
        string name, bool reverseThickness, bool reverseDirection)
    {
        var (definition, raw, folders) = Load(name);
        Assert.True(definition.ParameterResolution.ReverseThickness);
        Assert.True(definition.ParameterResolution.ReverseDirection);
        Assert.Equal(reverseThickness, definition.ReverseThickness);
        Assert.Equal(reverseDirection, definition.ReverseDirection);

        var comp = MfcDocument.Parse(raw).AllElements
            .Single(e => e.ClassName == "moCompSheetmetalFeat_c");
        Assert.Equal(reverseThickness ? (byte)1 : (byte)0, raw[comp.TagOffset + 0x8B]);
        Assert.Equal(reverseDirection ? (byte)1 : (byte)0, raw[comp.TagOffset + 0x8F]);
        for (int i = 0x8C; i <= 0x8E; i++) Assert.Equal((byte)0, raw[comp.TagOffset + i]);
        for (int i = 0x90; i <= 0x92; i++) Assert.Equal((byte)0, raw[comp.TagOffset + i]);
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", false, 0.0)]
    [InlineData("sm_l_dir2_d5_a.SLDPRT", false, 0.0)]
    [InlineData("sm_l_dir2_d5_c.SLDPRT", true, 0.005)]
    [InlineData("sm_l_dir2_d5_c_resave.SLDPRT", true, 0.005)]
    public void ReadsDirection2DistanceAndBothDirections(string name, bool both, double depth2)
    {
        var (definition, raw, folders) = Load(name);
        Assert.Equal(0.010, definition.Depth, precision: 9);
        Assert.Equal(both, definition.BothDirections);
        Assert.Equal(both, definition.ParameterResolution.BothDirections);
        Assert.Equal(both, definition.ParameterResolution.DepthDirection2);
        Assert.Equal(depth2, definition.DepthDirection2, precision: 9);

        var flange = folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        int end = NextFolderOffset(folders, flange.TagOffset, raw.Length);
        int d3 = FindNamedParameter(raw, "D3", flange.TagOffset, end);
        if (both)
        {
            Assert.True(d3 >= 0);
            Assert.Equal(depth2, BitConverter.ToDouble(raw, d3 + 0x1E));
        }
        else
        {
            Assert.Equal(-1, d3);
        }
    }

    [Fact]
    public void Direction2D3ParameterOffsetIsEvidenced()
    {
        var (_, raw, _) = Load("sm_l_dir2_d5_c.SLDPRT");
        Assert.Equal(0x72FA, FindNamedParameter(raw, "D3", 0x3EF1, 0x776A));
        Assert.Equal(0.005, BitConverter.ToDouble(raw, 0x72FA + 0x1E));
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", 0.5)]
    [InlineData("sm_l_k03_fixed.SLDPRT", 0.3)]
    [InlineData("sm_l_k04_fixed.SLDPRT", 0.4)]
    [InlineData("sm_l_k03_fixed_resave.SLDPRT", 0.3)]
    [InlineData("sm_l_k04_fixed_resave.SLDPRT", 0.4)]
    [InlineData("sm_l_relief_ratio03.SLDPRT", 0.5)]
    public void ReadsKFactorFromGlobalSheetMetalBlock(string name, double expected)
    {
        var (definition, raw, folders) = Load(name);
        Assert.True(definition.ParameterResolution.KFactor);
        Assert.Equal(expected, definition.KFactor, precision: 9);

        var global = folders.Single(f => f.ClassName == "moSheetMetal_c");
        int end = NextFolderOffset(folders, global.TagOffset, raw.Length);
        int d2 = FindNamedParameter(raw, "D2", global.TagOffset, end);
        Assert.True(d2 >= 0);
        Assert.Equal(expected, BitConverter.ToDouble(raw, d2 + 0x1E));

        // The base-flange template keeps its own D2 at 0.5; the effective K is
        // the global block above. This separation is proven by k03/k04.
        var template = folders.Single(f => f.ClassName == "moTemplateSheetMetal_c");
        var flange = folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        int templateD2 = FindNamedParameter(raw, "D2", template.TagOffset, flange.TagOffset);
        Assert.True(templateD2 >= 0);
        Assert.Equal(0.5, BitConverter.ToDouble(raw, templateD2 + 0x1E));
    }

    /// <summary>
    /// K-factor evidence is the global moSheetMetal_c named D2 scalar, not the
    /// native getter. A targeted diff of the global window [0x7D36, 0x97BF)
    /// between sm_l_d10 and k03/k04 shows exactly one scalar change at window
    /// offset 0xAE9 (absolute 0x881F): 0.5 -> 0.3/0.4. The base-flange template
    /// D2 stays 0.5. Agent 5 route D (global SetOverrideDefaultParameter2(
    /// BendAllowance=1,true) + BendAllowanceType=2 + KFactor + ModifyDefinition,
    /// then base flange OverrideDefaultSheetMetalParameters/OverrideKFactor)
    /// corroborates the values across save/reopen and the volume change.
    /// </summary>
    [Fact]
    public void KFactorGlobalD2OffsetIsEvidenced()
    {
        var (_, d10, _) = Load("sm_l_d10.SLDPRT");
        var (_, k03, _) = Load("sm_l_k03_fixed.SLDPRT");
        var (_, k04, _) = Load("sm_l_k04_fixed.SLDPRT");

        Assert.Equal(0x8801, FindNamedParameter(k03, "D2", 0x7D36, 0x97BF));
        Assert.Equal(0xAE9, 0x881F - 0x7D36);
        Assert.Equal(0.5, BitConverter.ToDouble(d10, 0x881F));
        Assert.Equal(0.3, BitConverter.ToDouble(k03, 0x881F));
        Assert.Equal(0.4, BitConverter.ToDouble(k04, 0x881F));
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", 0.5, true)]
    [InlineData("sm_l_relief_ratio03.SLDPRT", 0.3, true)]
    [InlineData("sm_l_relief_ratio03_resave.SLDPRT", 0.3, true)]
    [InlineData("sm_l_relief_tear.SLDPRT", 0.0, false)]
    public void ReadsReliefRatioFromTemplateD6(string name, double expected, bool resolved)
    {
        var (definition, raw, folders) = Load(name);
        Assert.Equal(resolved, definition.ParameterResolution.ReliefRatio);
        if (resolved)
        {
            Assert.Equal(expected, definition.ReliefRatio, precision: 9);
        }
        else
        {
            // D6 is absent for tear relief; the model default 0.5 must not be
            // reported as a parsed value.
            Assert.Equal(0.5, definition.ReliefRatio);
        }

        var template = folders.Single(f => f.ClassName == "moTemplateSheetMetal_c");
        var flange = folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        int d6 = FindNamedParameter(raw, "D6", template.TagOffset, flange.TagOffset);
        if (resolved)
        {
            Assert.True(d6 >= 0);
            Assert.Equal(expected, BitConverter.ToDouble(raw, d6 + 0x1E));
        }
        else
        {
            Assert.Equal(-1, d6);
        }
    }

    [Fact]
    public void ReliefRatioTemplateD6OffsetIsEvidenced()
    {
        var (_, raw, _) = Load("sm_l_relief_ratio03.SLDPRT");
        Assert.Equal(0x3B8B, FindNamedParameter(raw, "D6", 0x1D61, 0x3EF1));
        Assert.Equal(0.3, BitConverter.ToDouble(raw, 0x3B8B + 0x1E));
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", 1u, 0u)]
    [InlineData("sm_l_relief_tear.SLDPRT", 2u, 1u)]
    [InlineData("sm_l_relief_obround.SLDPRT", 3u, 2u)]
    [InlineData("sm_l_relief_none.SLDPRT", 4u, 4u)]
    [InlineData("sm_l_relief_tear_resave.SLDPRT", 2u, 1u)]
    public void ReadsAutoReliefTypeFromPreFlangeCode(string name, uint expectedApi, uint expectedCode)
    {
        var (definition, raw, folders) = Load(name);
        Assert.True(definition.ParameterResolution.AutoReliefType);
        Assert.Equal(expectedApi, definition.AutoReliefType);

        var flange = folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        Assert.Equal(expectedCode, BitConverter.ToUInt32(raw, flange.TagOffset - 0x28));
        for (int i = flange.TagOffset - 0x24; i < flange.TagOffset - 0x20; i++)
            Assert.Equal((byte)0, raw[i]);
    }

    /// <summary>
    /// Both radius samples have native OverrideDefaultSheetMetalParameters and
    /// OverrideRadius = false after save/reopen. They share the same non-semantic
    /// edit marker at base flange tag - 0x19 (offset 0x3ED8 = 1, baseline = 0),
    /// and their flange/global radius copies are identical within each file
    /// (0.002 / 0.004). No byte distinguishes "use default" from "override", so
    /// only BendRadius is resolved.
    /// </summary>
    [Theory]
    [InlineData("sm_l_use_global_radius.SLDPRT", 0.004)]
    [InlineData("sm_l_override_radius.SLDPRT", 0.002)]
    public void RadiusOnlySamplesResolveBendRadiusButNotUseDefaultFlag(string name, double radius)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.BendRadius);
        Assert.Equal(radius, definition.BendRadius, precision: 9);
        Assert.False(definition.ParameterResolution.UseDefaultBendRadius);
    }

    /// <summary>
    /// allowance_bend/deduction accept type 3/4 in memory but save back as
    /// type 2 / K 0.5. The type byte still records the persisted KFactor type (2),
    /// so it is resolved, while Direct/Deduction values stay null.
    /// </summary>
    [Theory]
    [InlineData("sm_l_allowance_bend.SLDPRT")]
    [InlineData("sm_l_allowance_deduction.SLDPRT")]
    public void RevertedAllowanceSamplesResolvePersistedKFactorType(string name)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.BendAllowanceType);
        Assert.Equal(2u, definition.BendAllowanceType);
        Assert.Null(definition.BendAllowance);
        Assert.Null(definition.BendDeduction);
        // Unrelated fields remain resolved.
        Assert.True(definition.ParameterResolution.KFactor);
        Assert.True(definition.ParameterResolution.AutoReliefType);
    }

    /// <summary>
    /// 2026-10-31 native retry: the global BendAllowance override + base Initialize
    /// route persists Deduction type 4 and BendDeduction=0.001. The type byte at
    /// D2 name - 0x2A2 is 3 (API type - 1) and D3 carries 0.001.
    /// </summary>
    [Theory]
    [InlineData("sm_l_allowance_deduction_persisted.SLDPRT")]
    [InlineData("sm_l_allowance_deduction_persisted_resave.SLDPRT")]
    public void PersistedDeductionResolvesTypeAndValue(string name)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.BendAllowanceType);
        Assert.Equal(4u, definition.BendAllowanceType);
        Assert.True(definition.ParameterResolution.BendDeduction);
        Assert.Equal(0.001, definition.BendDeduction!.Value, precision: 12);
        Assert.False(definition.ParameterResolution.BendAllowance);
        Assert.Null(definition.BendAllowance);
        Assert.Equal(0.5, definition.KFactor, precision: 12);
    }

    /// <summary>
    /// 2026-10-31 native retry: the same global override + base Initialize route
    /// persists Direct type 3 and BendAllowance=0.001. The type byte is 2 and D3
    /// carries 0.001, confirming the internal code -> API type mapping
    /// (1 -> 2 KFactor, 2 -> 3 Direct, 3 -> 4 Deduction).
    /// </summary>
    [Theory]
    [InlineData("sm_l_allowance_direct_persisted.SLDPRT")]
    [InlineData("sm_l_allowance_direct_persisted_resave.SLDPRT")]
    public void PersistedDirectAllowanceResolvesTypeAndValue(string name)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.BendAllowanceType);
        Assert.Equal(3u, definition.BendAllowanceType);
        Assert.True(definition.ParameterResolution.BendAllowance);
        Assert.Equal(0.001, definition.BendAllowance!.Value, precision: 12);
        Assert.False(definition.ParameterResolution.BendDeduction);
        Assert.Null(definition.BendDeduction);
        Assert.Equal(0.5, definition.KFactor, precision: 12);
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", 0.0005)]
    [InlineData("sm_l_relief_ratio03.SLDPRT", 0.0003)]
    [InlineData("sm_l_relief_w15_d25.SLDPRT", 0.0)]
    [InlineData("sm_l_relief_tear.SLDPRT", 0.0)]
    public void ReliefWidthDepthCandidatesRemainAmbiguous(string name, double expectedEffective)
    {
        var (definition, raw, folders) = Load(name);
        var flange = folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        int end = NextFolderOffset(folders, flange.TagOffset, raw.Length);
        int d8 = FindNamedParameter(raw, "D8", flange.TagOffset, end);
        int d9 = FindNamedParameter(raw, "D9", flange.TagOffset, end);
        Assert.True(d8 >= 0);
        Assert.True(d9 >= 0);
        Assert.Equal(expectedEffective, BitConverter.ToDouble(raw, d8 + 0x1E));
        Assert.Equal(expectedEffective, BitConverter.ToDouble(raw, d9 + 0x1E));

        // sm_l_relief_w15_d25 contains no 0.0015/0.0025 double anywhere; its D8/D9
        // are zero while native ReliefWidth/ReliefDepth reverted to 0.0005. D8/D9
        // are ratio-derived in the other samples and zero for tear/obround/none,
        // so neither width-vs-depth role nor semantics are proven.
        Assert.False(definition.ParameterResolution.ReliefWidth);
        Assert.False(definition.ParameterResolution.ReliefDepth);
    }

    [Fact]
    public void ExplicitReliefWidthAndDepthAreNotResolvedAfterNativeRevert()
    {
        var (definition, _, _) = Load("sm_l_relief_w15_d25.SLDPRT");
        Assert.False(definition.ParameterResolution.ReliefWidth);
        Assert.False(definition.ParameterResolution.ReliefDepth);
        Assert.Equal(0.0, definition.ReliefWidth);
        Assert.Equal(0.0, definition.ReliefDepth);
        Assert.True(definition.ParameterResolution.AutoReliefType);
    }

    /// <summary>
    /// Both create-time samples share the default-relief flag 0x40 in the global
    /// moSheetMetal_c element data; their native UseDefaultBendRelief getter is
    /// true for both. The create-false -> read-true contradiction remains a
    /// creation-time non-persistence, but the serialized flag resolves true.
    /// </summary>
    [Theory]
    [InlineData("sm_l_relief_default_true.SLDPRT")]
    [InlineData("sm_l_relief_default_false.SLDPRT")]
    public void UseDefaultBendReliefResolvesTrueForCreateFlagSamples(string name)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.UseDefaultBendRelief);
        Assert.True(definition.UseDefaultBendRelief);
    }

    /// <summary>
    /// 2026-10-31 native retry: the global AutoRelief override route persists
    /// UseDefaultBendRelief=false (moSheetMetal_c flags byte 0x20). Relief width,
    /// depth and ratio still revert to 0/0/true and stay unresolved; the
    /// pre-feature relief code still describes the default, so AutoReliefType is
    /// deliberately left unresolved for this layout.
    /// </summary>
    [Theory]
    [InlineData("sm_l_relief_global_override.SLDPRT")]
    [InlineData("sm_l_relief_global_override_resave.SLDPRT")]
    public void GlobalAutoReliefOverrideResolvesUseDefaultBendReliefFalse(string name)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.UseDefaultBendRelief);
        Assert.False(definition.UseDefaultBendRelief);
        Assert.False(definition.ParameterResolution.AutoReliefType);
        Assert.False(definition.ParameterResolution.ReliefWidth);
        Assert.False(definition.ParameterResolution.ReliefDepth);
        Assert.Equal(0.0, definition.ReliefWidth);
        Assert.Equal(0.0, definition.ReliefDepth);
    }

    /// <summary>
    /// Only a useGaugeTable=false baseline was previously available. The
    /// 2026-10-31 retry adds a persisted gauge-table sample; absence of the
    /// moSMEnvTableFeat_c element now resolves UseGaugeTable=false.
    /// </summary>
    [Fact]
    public void GaugeTableBaselineResolvesFalse()
    {
        var (definition, _, _) = Load("sm_l_gauge_baseline.SLDPRT");
        Assert.True(definition.ParameterResolution.UseGaugeTable);
        Assert.False(definition.UseGaugeTable);
        Assert.Null(definition.ThicknessTableName);
        Assert.Null(definition.GaugeTablePath);
    }

    /// <summary>
    /// 2026-10-31 native retry gauge sample: moSMEnvTableFeat_c is present, the
    /// stored table name is the 3-character WChar "规格表", and the full gauge
    /// table path is retained in a WChar record even though the API
    /// GaugeTablePath getter returns an empty string. KFactor=-1 in D2 is not a
    /// valid K-factor and stays unresolved.
    /// </summary>
    [Theory]
    [InlineData("sm_l_gauge_table.SLDPRT")]
    [InlineData("sm_l_gauge_table_resave.SLDPRT")]
    public void GaugeTableSampleResolvesTrueNameAndPath(string name)
    {
        var (definition, _, _) = Load(name);
        Assert.True(definition.ParameterResolution.UseGaugeTable);
        Assert.True(definition.UseGaugeTable);
        Assert.True(definition.ParameterResolution.ThicknessTableName);
        Assert.Equal("规格表", definition.ThicknessTableName);
        Assert.True(definition.ParameterResolution.GaugeTablePath);
        Assert.Contains("k-factor mm sample.xlsx", definition.GaugeTablePath!,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(definition.ParameterResolution.BendAllowanceType);
        Assert.Equal(2u, definition.BendAllowanceType);
        Assert.False(definition.ParameterResolution.KFactor);
        Assert.Equal(0.5, definition.KFactor, precision: 12);
    }

    [Fact]
    public void TwoBaseFlangesLeavesAmbiguousDepthUnresolved()
    {
        var (definition, _, _) = Load("sm_two_base_flanges.SLDPRT");
        Assert.True(definition.ParameterResolution.Thickness);
        Assert.True(definition.ParameterResolution.BendRadius);
        Assert.Equal(0.002, definition.Thickness, precision: 9);
        Assert.Equal(0.003, definition.BendRadius, precision: 9);

        // Two D2 records (0.010 and 0.008) share one folder window; ownership is
        // not proven without decoding compact class references.
        Assert.False(definition.ParameterResolution.Depth);
        Assert.Equal(0.0, definition.Depth);

        Assert.True(definition.ParameterResolution.KFactor);
        Assert.Equal(0.5, definition.KFactor, precision: 9);
        Assert.True(definition.ParameterResolution.AutoReliefType);
        Assert.Equal(1u, definition.AutoReliefType);
        Assert.True(definition.ParameterResolution.ReverseThickness);
        Assert.True(definition.ParameterResolution.ReverseDirection);
        Assert.False(definition.ReverseThickness);
        Assert.False(definition.ReverseDirection);
    }
}
