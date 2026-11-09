using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Legacy tutorial sweep family fallback. Merge, PathAlignmentType and TwistAngle
/// are promoted only from native-validated WireContact-anchored slots. TwistControlType
/// 0/1 is not guessed; only the measured constant-twist marker (=8) is promoted.
/// </summary>
public class LegacySweepLoftFeatureReadingTests
{
    [Theory]
    [InlineData("learn_carvingknife_motor", "Sweep1", 0x123u, false, 10, 0.0)]
    [InlineData("tutorial_dimxpert_spring", "Sweep1", 0x2Eu, true, 0, 0.0)]
    [InlineData("tutorial_api_maingrip", "Sweep1", 0xCFu, true, 0, 0.0)]
    [InlineData("tutorial_routing_pipes_tube", "Sweep-Thin1", 0x159u, false, 0, 0.0)]
    public void LegacySweep_ParsesMergePathAlignmentAndTwistAngle(
        string sample, string featureName, uint typeId, bool merge,
        int pathAlignmentType, double twistAngle)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == featureName && f.Model is SweepFeatureModel));
        Assert.Equal(FeatureKind.Sweep, feature.Kind);

        var definition = Assert.IsType<SweepFeatureModel>(feature.Model);
        Assert.Equal("moSweep_c", definition.ClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.Equal(merge, definition.Merge);
        Assert.Equal(pathAlignmentType, definition.PathAlignmentType);
        Assert.Equal(twistAngle, definition.TwistAngleRad!.Value, 12);
        Assert.True(definition.Resolution.Merge);
        Assert.True(definition.Resolution.PathAlignmentType);
        Assert.True(definition.Resolution.TwistAngle);

        // No dedicated 0/1 twist slot is proven for legacy streams.
        Assert.Null(definition.TwistControlType);
        Assert.False(definition.Resolution.TwistControlType);
        Assert.Empty(definition.References);
        Assert.Empty(definition.SelectionReferenceIds);
        Assert.False(definition.Resolution.SelectionReferenceList);
    }

    [Fact]
    public void LegacySweep_TwistMarkerSetsOnlyConstantTwist()
    {
        using var file = SwFile.Open(Sample("tutorial_dimxpert_spring_twist8_probe"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == "Sweep1" && f.Model is SweepFeatureModel));
        var definition = Assert.IsType<SweepFeatureModel>(feature.Model);
        Assert.Equal(8, definition.TwistControlType);
        Assert.True(definition.Resolution.TwistControlType);
        Assert.True(definition.Merge);
        Assert.Equal(0, definition.PathAlignmentType);
    }

    [Fact]
    public void LegacySweep_InvalidSlotsStayUnresolved()
    {
        using var file = SwFile.Open(Sample("learn_carvingknife_motor"), readOnly: true);
        string streamPath = file.ResolvedFeaturesStreamPath!;
        byte[] raw = (byte[])file.RawStreams[streamPath].Clone();
        var parsed = SldFile.Mfc.MfcDocument.Parse(raw);
        var sweep = parsed.AllElements.Single(e =>
            e.TagByte == 0x01 && e.ClassName == "moSweep_c");
        var wire = SldFile.Mfc.MfcPrimitives.ScanMfcTags(raw).First(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > sweep.TagOffset);
        Assert.Equal(0x68F, wire.TagOffset - sweep.TagOffset);

        raw[wire.TagOffset + 0xCC2] = 0x02; // invalid merge byte
        raw[wire.TagOffset + 0xCC7] = 0x02; // unknown path-alignment code
        BitConverter.GetBytes(10.0).CopyTo(raw, wire.TagOffset + 0xCCB);

        var tampered = SldFile.Mfc.MfcDocument.Parse(raw);
        var tamperedElement = tampered.AllElements.Single(e =>
            e.TagByte == 0x01 && e.ClassName == "moSweep_c");
        var definition = Assert.IsType<SweepFeatureModel>(
            SweepFeatureModel.ParseLegacySweep(
                tampered, tamperedElement.ClassName, 0x123u,
                tamperedElement.TagOffset, raw.Length));

        Assert.Null(definition.Merge);
        Assert.Null(definition.PathAlignmentType);
        Assert.Null(definition.TwistAngleRad);
        Assert.False(definition.Resolution.Merge);
        Assert.False(definition.Resolution.PathAlignmentType);
        Assert.False(definition.Resolution.TwistAngle);
    }

    private static string Sample(string name) => Path.Combine(
        AppContext.BaseDirectory, "TestFiles", "Features2026",
        "LegacyTutorialSamples", "Samples", name + ".SLDPRT");
}
