using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading checks for the official CHM recipe artifacts. Measured Rib options,
/// Gusset profile/thickness/use-angle, Split consume/tool-count and SurfaceTrim
/// type are resolved; fields without a passable controlled variation stay null.
/// </summary>
public class OfficialChmReadingTests
{
    [Fact]
    public void RibOfficialSample_ReadsMeasuredThickness()
    {
        using var file = SwFile.Open(Sample("official_rib_block20"), readOnly: true);
        var definition = Assert.IsType<RibFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Definition is RibFeatureDefinitionData)).Definition);
        Assert.Equal("moRib_c", definition.ClassName);
        Assert.Equal(0x29u, definition.SerializedTypeId);
        Assert.Equal(0.00254, definition.Thickness!.Value, 12);
        Assert.True(definition.Resolution.Thickness);
        Assert.True(definition.IsTwoSided);
        Assert.False(definition.ReverseThicknessDir);
        Assert.Equal(0, definition.ExtrusionDirection!.Value);
        Assert.False(definition.EnableDraft);
        Assert.True(definition.DraftOutward);
        Assert.False(definition.DraftFromWall);
        Assert.False(definition.FlipSide);
        Assert.Equal(0, definition.RefSketchIndex);
        Assert.True(definition.Resolution.FlipSide);
        Assert.True(definition.Resolution.RefSketchIndex);
        Assert.Equal(0, definition.DraftAngle!.Value);
        Assert.True(definition.Resolution.IsTwoSided);
        Assert.True(definition.Resolution.DraftOutward);
    }

    [Fact]
    public void CombineOfficialSample_ClassAndTypeOnly()
    {
        using var file = SwFile.Open(Sample("official_combine_multi_inter"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is CombineBodiesFeatureDefinitionData));
        Assert.Equal(FeatureKind.CombineBodies, feature.Kind);
        var definition = Assert.IsType<CombineBodiesFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moCombineBodies_c", definition.ClassName);
        Assert.Equal(0x5Au, definition.SerializedTypeId);
        Assert.Null(definition.OperationType);
        Assert.Null(definition.BodiesToCombine);
        Assert.False(definition.Resolution.OperationType);
    }

    [Fact]
    public void SplitOfficialSample_ClassAndTypeOnly()
    {
        using var file = SwFile.Open(Sample("official_split_multi_inter"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is SplitBodyFeatureDefinitionData));
        Assert.Equal(FeatureKind.SplitBody, feature.Kind);
        var definition = Assert.IsType<SplitBodyFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moSplit_c", definition.ClassName);
        Assert.Equal(0x5Au, definition.SerializedTypeId);
        Assert.Null(definition.State);
        Assert.True(definition.Consume);
        Assert.Null(definition.TrimTools);
        Assert.Equal(1, definition.TrimToolsCount);
        Assert.True(definition.Resolution.Consume);
        Assert.True(definition.Resolution.TrimToolsCount);
        Assert.False(definition.Resolution.TrimTools);
        Assert.False(definition.Resolution.State);
    }

    [Fact]
    public void GussetOfficialSample_ClassAndTypeOnly()
    {
        using var file = SwFile.Open(Sample("official_gusset_weldment_box3"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == "official_gusset_weldment_box3" &&
            f.Definition is GussetFeatureDefinitionData));
        Assert.Equal(FeatureKind.Gusset, feature.Kind);
        var definition = Assert.IsType<GussetFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moGusset_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(1, definition.ProfileType);
        Assert.Equal(0.005, definition.Thickness!.Value, 12);
        Assert.True(definition.UseAngle);
        Assert.Null(definition.ProfileLocation);
        Assert.True(definition.Resolution.ProfileType);
        Assert.True(definition.Resolution.Thickness);
        Assert.True(definition.Resolution.UseAngle);
        Assert.False(definition.Resolution.ProfileLocation);
    }

    [Fact]
    public void SurfaceTrimOfficialSample_ClassAndTypeOnly()
    {
        using var file = SwFile.Open(Sample("official_surface_trim"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is SurfaceTrimFeatureDefinitionData));
        Assert.Equal(FeatureKind.SurfaceTrim, feature.Kind);
        var definition = Assert.IsType<SurfaceTrimFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moTrimRefSurface_c", definition.ClassName);
        Assert.Equal(0x2Au, definition.SerializedTypeId);
        Assert.Equal(1, definition.TrimType);
        Assert.Null(definition.TrimTools);
        Assert.Null(definition.TrimToolsCount);
        Assert.Null(definition.PiecesToKeep);
        Assert.True(definition.Resolution.TrimType);
        Assert.False(definition.Resolution.TrimTools);
        Assert.False(definition.Resolution.PiecesToKeep);
    }

    private static string Sample(string name) =>
        TestFileProvider.Resolve("Features2026/OfficialChmRecipes", name + ".SLDPRT");
}
