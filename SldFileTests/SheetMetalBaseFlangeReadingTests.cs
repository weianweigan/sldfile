using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Evidence-backed reading tests for SheetMetalBaseFlangeModel.
/// Only thickness, bend radius and depth have controlled stream evidence. The
/// remaining manufacturing options must report unresolved through
/// ParameterResolution instead of exposing a model default as a parsed value.
/// </summary>
public class SheetMetalBaseFlangeReadingTests
{
    private static string Sample(string folder, string fileName)
    {
        string Relative() => folder == "Features2026"
            ? Path.Combine("TestFiles", "Features2026", fileName)
            : Path.Combine("TestFiles", "Features2026", folder, fileName);

        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Relative());
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", Relative());
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample {folder}/{fileName} not found");
    }

    private static SheetMetalBaseFlangeModel ReadDefinition(SwFile file)
    {
        var folder = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        return Assert.IsType<SheetMetalBaseFlangeModel>(folder.FeatureData);
    }

    [Theory]
    // Open L profile: depth is serialized (D2 named scalar). Native
    // IBaseFlangeFeatureData.D1EndConditionDistance is 10 mm / 20 mm and the
    // union volume doubles across the pair.
    [InlineData("sm_l_profile", "sm_l_d10.SLDPRT", 0.001, 0.002, 0.010, true)]
    [InlineData("sm_l_profile", "sm_l_d20.SLDPRT", 0.001, 0.002, 0.020, true)]
    [InlineData("sm_l_profile", "sm_l_d10_resave.SLDPRT", 0.001, 0.002, 0.010, true)]
    [InlineData("sm_l_profile", "sm_l_t2.SLDPRT", 0.002, 0.002, 0.010, true)]
    [InlineData("sm_l_profile", "sm_l_r3.SLDPRT", 0.001, 0.003, 0.010, true)]
    // Closed flat rectangle profiles store depth without a named D2 record: an
    // unnamed scalar 0x28 bytes before the moTemplateFlatPattern_c tag. The
    // 10/20 pair changes only this field; T/R variants keep 0.010.
    [InlineData("Features2026", "sm_base_flange_10mm.SLDPRT", 0.001, 0.002, 0.010, true)]
    [InlineData("Features2026", "sm_base_flange_20mm.SLDPRT", 0.001, 0.002, 0.020, true)]
    [InlineData("Features2026", "sm_base_flange_thick2.SLDPRT", 0.002, 0.002, 0.010, true)]
    [InlineData("Features2026", "sm_base_flange_rad3.SLDPRT", 0.001, 0.003, 0.010, true)]
    [InlineData("Features2026", "sm_base_flange_after_fillet.SLDPRT", 0.002, 0.003, 0.010, true)]
    // Agent 5 option matrix (SheetMetalOptions) shares the same dimension layout.
    [InlineData("SheetMetalOptions", "sm_l_d10.SLDPRT", 0.001, 0.002, 0.010, true)]
    [InlineData("SheetMetalOptions", "sm_l_k03_fixed.SLDPRT", 0.001, 0.002, 0.010, true)]
    [InlineData("SheetMetalOptions", "sm_l_use_global_radius.SLDPRT", 0.001, 0.004, 0.010, true)]
    [InlineData("SheetMetalOptions", "sm_l_relief_tear.SLDPRT", 0.001, 0.002, 0.010, true)]
    [InlineData("SheetMetalOptions", "sm_l_dir2_d5_c.SLDPRT", 0.001, 0.002, 0.010, true)]
    // Two base flanges serialize two D2 depth records in one folder window;
    // ownership is ambiguous without decoding the compact class reference, so
    // Depth must stay unresolved rather than taking either value.
    [InlineData("SheetMetalOptions", "sm_two_base_flanges.SLDPRT", 0.002, 0.003, 0.0, false)]
    public void ReadsOnlyEvidencedDimensions(
        string folder,
        string fileName,
        double thickness,
        double bendRadius,
        double depth,
        bool depthResolved)
    {
        using var file = SwFile.Open(Sample(folder, fileName), readOnly: true);
        var definition = ReadDefinition(file);

        Assert.Equal(thickness, definition.Thickness, precision: 9);
        Assert.Equal(bendRadius, definition.BendRadius, precision: 9);
        Assert.Equal(depth, definition.Depth, precision: 9);

        Assert.True(definition.ParameterResolution.Thickness);
        Assert.True(definition.ParameterResolution.BendRadius);
        Assert.Equal(depthResolved, definition.ParameterResolution.Depth);
    }

    [Theory]
    [InlineData("sm_l_profile", "sm_l_d10.SLDPRT", true)]
    [InlineData("Features2026", "sm_base_flange_10mm.SLDPRT", false)]
    [InlineData("Features2026", "sm_base_flange_after_fillet.SLDPRT", false)]
    public void ResolutionFlagsSeparateDecodedValuesFromDefaults(
        string folder, string fileName, bool reverseFlagsResolved)
    {
        using var file = SwFile.Open(Sample(folder, fileName), readOnly: true);
        var definition = ReadDefinition(file);
        var r = definition.ParameterResolution;

        // Decoded from the baseline layout.
        Assert.True(r.Thickness);
        Assert.True(r.BendRadius);
        Assert.True(r.Depth);
        Assert.True(r.KFactor);
        Assert.True(r.ReliefRatio);
        // Auto relief and reverse flags sit in bend-specific structures; closed
        // flat profiles without a controlled option sample stay unresolved.
        Assert.Equal(reverseFlagsResolved, r.AutoReliefType);
        Assert.Equal(reverseFlagsResolved, r.ReverseThickness);
        Assert.Equal(reverseFlagsResolved, r.ReverseDirection);
        Assert.False(r.DepthDirection2);
        Assert.False(r.BothDirections);

        // Resolved by the 2026-10-31 retry evidence: the persisted allowance
        // type is KFactor (2) in every default sample, the global relief flag is
        // true, and the absence of moSMEnvTableFeat_c means UseGaugeTable=false.
        Assert.True(r.BendAllowanceType);
        Assert.Equal(2u, definition.BendAllowanceType);
        Assert.True(r.UseDefaultBendRelief);
        Assert.True(definition.UseDefaultBendRelief);
        Assert.True(r.UseGaugeTable);
        Assert.False(definition.UseGaugeTable);
        // Still no controlled evidence.
        Assert.False(r.UseDefaultBendRadius);
        Assert.False(r.ReliefWidth);
        Assert.False(r.ReliefDepth);
    }

    /// <summary>
    /// Proves the exact byte location used for depth: the named D2 scalar inside
    /// the base flange window [moSMBaseFlange_c tag .. next tree folder).
    /// Both stream-0x73 samples share the same layout; only the IEEE-754 value
    /// at D2 + 0x1E changes between 10 mm and 20 mm.
    /// </summary>
    [Theory]
    [InlineData("sm_l_d10.SLDPRT", 0.010)]
    [InlineData("sm_l_d20.SLDPRT", 0.020)]
    public void DepthIsTheD2ScalarInsideTheBaseFlangeWindow(string fileName, double expectedDepth)
    {
        using var file = SwFile.Open(Sample("sm_l_profile", fileName), readOnly: true);
        var raw = file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var baseFlange = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        var flatPatternTemplate = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moTemplateFlatPattern_c");
        Assert.True(baseFlange.TagOffset < flatPatternTemplate.TagOffset);

        int nameOffset = FindNamedD2(raw, baseFlange.TagOffset, flatPatternTemplate.TagOffset);
        Assert.Equal(0x6BD1, nameOffset);
        Assert.Equal(0x6BEF, nameOffset + 0x1E);
        Assert.Equal(expectedDepth, BitConverter.ToDouble(raw, nameOffset + 0x1E));

        var definition = ReadDefinition(file);
        Assert.True(definition.ParameterResolution.Depth);
        Assert.Equal(expectedDepth, definition.Depth);
    }

    /// <summary>
    /// Closed flat profiles have no named D2 record; the depth is the unnamed
    /// scalar at moTemplateFlatPattern tag - 0x28, framed by zero bytes on both
    /// sides. The offset is identical across the 10/20 depth pair and the
    /// thickness/radius variants; the 0x74 after-fillet stream uses the same
    /// relative layout.
    /// </summary>
    [Theory]
    [InlineData("sm_base_flange_10mm.SLDPRT", 0x4C90, 0.010)]
    [InlineData("sm_base_flange_20mm.SLDPRT", 0x4C90, 0.020)]
    [InlineData("sm_base_flange_thick2.SLDPRT", 0x4C90, 0.010)]
    [InlineData("sm_base_flange_rad3.SLDPRT", 0x4C90, 0.010)]
    [InlineData("sm_base_flange_after_fillet.SLDPRT", 0x7412, 0.010)]
    public void ClosedFlatProfileDepthIsTheScalarBeforeTheFlatPatternTemplate(
        string fileName, int expectedValueOffset, double expectedDepth)
    {
        using var file = SwFile.Open(Sample("Features2026", fileName), readOnly: true);
        var raw = file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var baseFlange = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c");
        var flatPatternTemplate = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moTemplateFlatPattern_c");

        Assert.Equal(-1, FindNamedD2(raw, baseFlange.TagOffset, flatPatternTemplate.TagOffset));
        Assert.Equal(expectedValueOffset, flatPatternTemplate.TagOffset - 0x28);
        Assert.Equal(expectedDepth, BitConverter.ToDouble(raw, expectedValueOffset));

        var definition = ReadDefinition(file);
        Assert.Equal(expectedDepth, definition.Depth);
        Assert.True(definition.ParameterResolution.Depth);
    }

    [Fact]
    public void ConflictingDepthCandidatesAreNotResolvedByLastMatch()
    {
        var first = BuildD2Record(0.010);
        var second = BuildD2Record(0.020);
        var raw = new byte[2 + first.Length + second.Length];
        raw[0] = 0x73;
        first.CopyTo(raw, 2);
        second.CopyTo(raw, 2 + first.Length);

        var definition = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, raw.Length, definition);

        Assert.Equal(0.0, definition.Depth);
        Assert.False(definition.ParameterResolution.Depth);
    }

    [Fact]
    public void SingleDepthCandidateIsResolved()
    {
        var record = BuildD2Record(0.0125);
        var raw = new byte[2 + record.Length];
        raw[0] = 0x73;
        record.CopyTo(raw, 2);

        var definition = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, raw.Length, definition);

        Assert.Equal(0.0125, definition.Depth);
        Assert.True(definition.ParameterResolution.Depth);
    }

    [Theory]
    [InlineData(0x70)]
    [InlineData(0x75)]
    public void UnknownStreamVersionDoesNotInventDepth(ushort version)
    {
        var record = BuildD2Record(0.010);
        var raw = new byte[2 + record.Length];
        BitConverter.GetBytes(version).CopyTo(raw, 0);
        record.CopyTo(raw, 2);

        var definition = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, raw.Length, definition);

        Assert.Equal(0.0, definition.Depth);
        Assert.False(definition.ParameterResolution.Depth);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.005)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonPositiveOrNonFiniteDepthIsRejected(double value)
    {
        var record = BuildD2Record(value);
        var raw = new byte[2 + record.Length];
        raw[0] = 0x73;
        record.CopyTo(raw, 2);

        var definition = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, raw.Length, definition);

        Assert.Equal(0.0, definition.Depth);
        Assert.False(definition.ParameterResolution.Depth);
    }

    [Theory]
    [InlineData("sm_l_d10.SLDPRT", 0.001)]
    [InlineData("sm_l_t2.SLDPRT", 0.002)]
    public void TemplateRadiusAndThicknessUseTheVerifiedOffsets(string fileName, double expectedThickness)
    {
        using var file = SwFile.Open(Sample("sm_l_profile", fileName), readOnly: true);
        var raw = file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var template = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moTemplateSheetMetal_c");
        var baseFlange = file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c");

        int d1 = FindNamedParameter(raw, "D1", template.TagOffset, baseFlange.TagOffset);
        Assert.Equal(0x217A, d1);
        Assert.Equal(0.002, BitConverter.ToDouble(raw, d1 + 0x1E));
        // Radial display dimension repeats the radius 0xAE bytes later.
        Assert.Equal(0.002, BitConverter.ToDouble(raw, d1 + 0x1E + 0xAE));

        int d3 = FindNamedParameter(raw, "D3", template.TagOffset, baseFlange.TagOffset);
        Assert.Equal(0x2E54, d3);
        Assert.Equal(0.0, BitConverter.ToDouble(raw, d3 + 0x1E));
        // Thickness is associated with D3 but serialized 0x3CC bytes later.
        Assert.Equal(expectedThickness, BitConverter.ToDouble(raw, d3 + 0x3CC));
    }

    [Fact]
    public void ClosedProfileFallbackRequiresTheObservedZeroFrameAndFlatPatternTag()
    {
        // Valid frame: eight zero bytes before the scalar, 32 zero bytes until the
        // moTemplateFlatPattern_c tag; no named D2 record.
        const string tagName = "moTemplateFlatPattern_c";
        const string compTagName = "moCompSheetmetalFeat_c";
        int tagLength = 6 + tagName.Length;
        int tagOffset = 0x100;
        var raw = new byte[tagOffset + tagLength + 8];
        raw[0] = 0x73;
        WriteMfcTag(raw, 0x80, compTagName);
        int valueOffset = tagOffset - 0x28;
        BitConverter.GetBytes(0.015).CopyTo(raw, valueOffset);
        WriteMfcTag(raw, tagOffset, tagName);

        var definition = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, tagOffset, definition);
        Assert.Equal(0.015, definition.Depth);
        Assert.True(definition.ParameterResolution.Depth);

        // Break the 32-byte zero frame: the fallback must not guess a value.
        raw[valueOffset + 12] = 0x7F;
        var rejected = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, tagOffset, rejected);
        Assert.Equal(0.0, rejected.Depth);
        Assert.False(rejected.ParameterResolution.Depth);

        // Without the flat-pattern template tag the layout is unknown.
        raw[valueOffset + 12] = 0;
        raw[tagOffset + 6] = (byte)'X';
        var wrongTag = new SheetMetalBaseFlangeModel();
        SheetMetalBaseFlangeParser.Read(raw, 2, tagOffset, wrongTag);
        Assert.Equal(0.0, wrongTag.Depth);
        Assert.False(wrongTag.ParameterResolution.Depth);
    }

    private static int FindNamedD2(byte[] raw, int start, int end)
    {
        return FindNamedParameter(raw, "D2", start, end);
    }

    private static int FindNamedParameter(byte[] raw, string name, int start, int end)
    {
        if (name.Length != 2) throw new ArgumentException("Only two-character names are supported.", nameof(name));
        end = Math.Min(end, raw.Length);
        for (int p = Math.Max(0, start); p <= end - 38; p++)
        {
            if (raw[p] == 0xFF && raw[p + 1] == 0xFE && raw[p + 2] == 0xFF &&
                raw[p + 3] == 2 && raw[p + 4] == (byte)name[0] && raw[p + 5] == 0 &&
                raw[p + 6] == (byte)name[1] && raw[p + 7] == 0 &&
                SheetMetalTemplateParser.IsScalarDescriptor(raw, p + 8))
                return p;
        }
        return -1;
    }

    private static void WriteMfcTag(byte[] raw, int offset, string name)
    {
        raw[offset] = 0xFF; raw[offset + 1] = 0xFF; raw[offset + 2] = 0x01; raw[offset + 3] = 0x00;
        raw[offset + 4] = (byte)name.Length; raw[offset + 5] = 0;
        for (int i = 0; i < name.Length; i++) raw[offset + 6 + i] = (byte)name[i];
    }

    private static byte[] BuildD2Record(double value)
    {
        var bytes = new byte[0x1E + 8];
        bytes[0] = 0xFF; bytes[1] = 0xFE; bytes[2] = 0xFF; bytes[3] = 2;
        bytes[4] = (byte)'D'; bytes[5] = 0; bytes[6] = (byte)'2'; bytes[7] = 0;
        // 22-byte scalar descriptor: 8-byte 2.0, FFFFFFFF, zero DWORD, empty WChar, two reserved bytes.
        bytes[8 + 7] = 0x40;
        for (int i = 16; i < 20; i++) bytes[i] = 0xFF;
        bytes[24] = 0xFF; bytes[25] = 0xFE; bytes[26] = 0xFF;
        BitConverter.GetBytes(value).CopyTo(bytes, 0x1E);
        return bytes;
    }
}
