using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary.FeatureData;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading tests for legacy tutorial samples that contain the previously blocked
/// Rib / Gusset / Combine / Split / SurfaceTrim classes. Stream versions in these
/// samples range from 0x006D to 0x0334, so FolderHeader TypeIds are recorded
/// exactly as read rather than normalized to a catalog constant.
/// </summary>
public class LegacyTutorialFeatureReadingTests
{
    [Theory]
    [InlineData("tutorial_advdrawings_red_button", "bad rib", 0x48u, 0.035)]
    [InlineData("tutorial_sustainability_drive_sled", "Rib1", 0x3Au, 0.0015)]
    [InlineData("tutorial_advdrawings_shaft_gear", "boss rib", 0x6Fu, 0.002)]
    [InlineData("tutorial_advdrawings_switch_plate", "rocket tail rib a", 0x171u, 0.00122)]
    [InlineData("learn_Mouse_MainHousing", "Screw Boss Rib1", 0x4C0u, 0.000762)]
    [InlineData("learn_Mouse_BottomHousing", "Main Rib- Rib5", 0x2A2u, 0.000762)]
    public void Rib_ParsesThicknessFromMeasuredAnchor(
        string sample, string featureName, uint typeId, double thickness)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == featureName && f.Definition is RibFeatureDefinitionData));

        Assert.Equal(FeatureKind.Rib, feature.Kind);
        var definition = Assert.IsType<RibFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moRib_c", definition.ClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.Equal(typeId, definition.TypeId);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.Equal(thickness, definition.Thickness!.Value, 12);
        Assert.True(definition.Resolution.Thickness);
        // No option byte is promoted without a controlled variation.
        Assert.Null(definition.Type);
        Assert.Null(definition.ExtrusionDirection);
        Assert.Null(definition.IsTwoSided);
        Assert.False(definition.Resolution.Type);
        Assert.False(definition.Resolution.ExtrusionDirection);
    }

    [Theory]
    [InlineData("tutorial_weldments_weldment_box2", 0x61u, false, null, null)]
    [InlineData("tutorial_api_weldment_box3", 0x51u, true, 1, 0.005)]
    public void Gusset_ParsesClassAndHeaderType(
        string sample, uint typeId, bool measured, int? profileType, double? thickness)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var definition = Assert.IsType<GussetFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Definition is GussetFeatureDefinitionData)).Definition);
        var feature = Assert.Single(file.Features.Where(f => f.Definition == definition));

        Assert.Equal(FeatureKind.Gusset, feature.Kind);
        Assert.Equal("Gusset1", feature.Name);
        Assert.Equal("moGusset_c", definition.ClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.Equal(typeId, definition.TypeId);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.Equal(profileType, definition.ProfileType);
        Assert.Equal(thickness, definition.Thickness);
        Assert.Equal(measured, definition.Resolution.ProfileType);
        Assert.Equal(measured, definition.Resolution.Thickness);
        if (measured)
        {
            Assert.True(definition.UseAngle);
            Assert.True(definition.Resolution.UseAngle);
            Assert.Null(definition.ProfileLocation);
        }
    }

    [Theory]
    [InlineData("learn_Mouse_MainHousing", "Merge Shells", 0x41Bu)]
    [InlineData("pf_sldBenchmarking_carrier", "Combine1", 0x5C5u)]
    [InlineData("tutorial_molded_mousemultibody", "Combine1", 0x1ABu)]
    public void CombineBodies_ParsesClassAndHeaderType(
        string sample, string featureName, uint typeId)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == featureName && f.Definition is CombineBodiesFeatureDefinitionData));

        Assert.Equal(FeatureKind.CombineBodies, feature.Kind);
        var definition = Assert.IsType<CombineBodiesFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moCombineBodies_c", definition.ClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        // Operation/body selection has no established legacy layout yet.
        Assert.Null(definition.OperationType);
        Assert.Null(definition.MainBody);
        Assert.Null(definition.BodiesToCombine);
        Assert.False(definition.Resolution.OperationType);
    }

    [Theory]
    [InlineData("learn_KnifeHandle", "Split1", 0x186u)]
    [InlineData("learn_Mouse_MainHousing", "Split for Shell", 0x417u)]
    [InlineData("tutorial_molded_mousemultibody", "Split1", 0x207u)]
    public void Split_ParsesSerializedClassAndHeaderType(
        string sample, string featureName, uint typeId)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == featureName && f.Definition is SplitBodyFeatureDefinitionData));

        Assert.Equal(FeatureKind.SplitBody, feature.Kind);
        var definition = Assert.IsType<SplitBodyFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moSplit_c", definition.ClassName);
        Assert.Equal("moSplit_c", definition.SerializedClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.Null(definition.State);
        Assert.Null(definition.Consume);
        Assert.Null(definition.TrimTools);
        Assert.False(definition.Resolution.TrimTools);
    }

    [Theory]
    [InlineData("tutorial_api_tjoint", "Surface-Trim1", 0x2Eu)]
    [InlineData("tutorial_molded_mousesplit", "Surface-Trim3", 0x50u)]
    [InlineData("tutorial_molded_mousemultibody", "Surface-Trim1", 0x1CEu)]
    [InlineData("learn_KnifeHandle", "Surface-Trim1", 0x10Bu)]
    public void SurfaceTrim_ParsesClassAndHeaderType(
        string sample, string featureName, uint typeId)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == featureName && f.Definition is SurfaceTrimFeatureDefinitionData));

        Assert.Equal(FeatureKind.SurfaceTrim, feature.Kind);
        var definition = Assert.IsType<SurfaceTrimFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moTrimRefSurface_c", definition.ClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.Null(definition.TrimType);
        Assert.Null(definition.TrimTools);
        Assert.False(definition.Resolution.TrimType);
        Assert.False(definition.Resolution.TrimTools);
    }

    [Fact]
    public void RibThicknessAnchor_RequiresExactMarkerAndValueRange()
    {
        byte[] valid = BuildRibAnchor(0.03);
        Assert.Equal(0.03, LegacyTutorialFeatureParsers.FindRibThickness(valid, 0, valid.Length));

        byte[] brokenMarker = BuildRibAnchor(0.03);
        brokenMarker[AnchorOffset + 8] = 0x00;
        Assert.Null(LegacyTutorialFeatureParsers.FindRibThickness(
            brokenMarker, 0, brokenMarker.Length));

        byte[] outOfRange = BuildRibAnchor(0.25);
        Assert.Null(LegacyTutorialFeatureParsers.FindRibThickness(
            outOfRange, 0, outOfRange.Length));

        byte[] single = BuildRibAnchor(0.03);
        byte[] duplicate = new byte[single.Length + 0x40];
        single.CopyTo(duplicate, 0);
        single.CopyTo(duplicate, 0x40);
        // A later anchor may belong to a following class-reference Rib feature;
        // the first anchor is the recognized feature's thickness.
        Assert.Equal(0.03, LegacyTutorialFeatureParsers.FindRibThickness(
            duplicate, 0, duplicate.Length));
    }

    private const int AnchorOffset = 16;

    private static byte[] BuildRibAnchor(double thickness)
    {
        var bytes = new byte[AnchorOffset + 0x16 + sizeof(double) + 16];
        BitConverter.GetBytes(2.0).CopyTo(bytes, AnchorOffset);
        new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }
            .CopyTo(bytes, AnchorOffset + 8);
        new byte[] { 0xFF, 0xFE, 0xFF, 0x00, 0x00, 0x00 }
            .CopyTo(bytes, AnchorOffset + 16);
        BitConverter.GetBytes(thickness).CopyTo(bytes, AnchorOffset + 0x16);
        return bytes;
    }

    private static string Sample(string name) => Path.Combine(
        AppContext.BaseDirectory, "TestFiles", "Features2026",
        "LegacyTutorialSamples", "Samples", name + ".SLDPRT");
}
