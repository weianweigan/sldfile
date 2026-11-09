using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading checks for the controlled official parameter variants. Only slots that
/// changed consistently in primary and resave streams and matched the native API
/// values are promoted.
/// </summary>
public class OfficialParamVariantReadingTests
{
    [Theory]
    [InlineData("rib_base_single", 0.00254, false, false, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_base_single_resave", 0.00254, false, false, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_t005", 0.005, false, false, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_t005_resave", 0.005, false, false, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_reverse_thickness", 0.00254, false, true, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_reverse_thickness_resave", 0.00254, false, true, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_norm_to_sketch", 0.00254, false, false, 1, false, false, false, 0.0, 0)]
    [InlineData("rib_norm_to_sketch_resave", 0.00254, false, false, 1, false, false, false, 0.0, 0)]
    [InlineData("rib_two_sided", 0.00254, true, false, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_two_sided_resave", 0.00254, true, false, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_two_sided_reverse", 0.00254, true, true, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_two_sided_reverse_resave", 0.00254, true, true, 0, false, false, false, 0.0, 0)]
    [InlineData("rib_draft_on", 0.00254, false, false, 0, true, false, false, 0.017453292519943295, 0)]
    [InlineData("rib_draft_on_resave", 0.00254, false, false, 0, true, false, false, 0.017453292519943295, 0)]
    [InlineData("rib_draft_from_wall", 0.00254, false, false, 0, true, false, true, 0.03490658503988659, 0)]
    [InlineData("rib_draft_from_wall_resave", 0.00254, false, false, 0, true, false, true, 0.03490658503988659, 0)]
    [InlineData("rib_draft_off_outward", 0.00254, false, false, 0, false, true, false, 0.0, 0)]
    [InlineData("rib_draft_off_outward_resave", 0.00254, false, false, 0, false, true, false, 0.0, 0)]
    [InlineData("ribmod_base", 0.00254, true, false, 0, false, true, false, 0.0, 0)]
    [InlineData("ribmod_base_resave", 0.00254, true, false, 0, false, true, false, 0.0, 0)]
    [InlineData("ribmod_ref_sketch1", 0.00254, true, false, 0, false, true, false, 0.0, 1)]
    [InlineData("ribmod_ref_sketch1_resave", 0.00254, true, false, 0, false, true, false, 0.0, 1)]
    [InlineData("ribmod_ref_sketch2", 0.00254, true, false, 0, false, true, false, 0.0, 2)]
    [InlineData("ribmod_ref_sketch2_resave", 0.00254, true, false, 0, false, true, false, 0.0, 2)]
    public void RibMeasuredOptions(
        string sample, double thickness, bool twoSided, bool reverseThickness,
        int extrusionDirection, bool draft, bool draftOutward, bool draftFromWall,
        double draftAngle, int refSketchIndex)
    {
        var definition = Definition<RibFeatureModel>(sample);
        Assert.Equal(thickness, definition.Thickness!.Value, 12);
        Assert.Equal(twoSided, definition.IsTwoSided);
        Assert.Equal(reverseThickness, definition.ReverseThicknessDir);
        Assert.Equal(extrusionDirection, definition.ExtrusionDirection);
        Assert.Equal(draft, definition.EnableDraft);
        Assert.Equal(draftOutward, definition.DraftOutward);
        Assert.Equal(draftFromWall, definition.DraftFromWall);
        Assert.Equal(draftAngle, definition.DraftAngle!.Value, 12);
        Assert.Equal(refSketchIndex, definition.RefSketchIndex);
        Assert.True(definition.Resolution.IsTwoSided);
        Assert.True(definition.Resolution.ReverseThicknessDir);
        Assert.True(definition.Resolution.ExtrusionDirection);
        Assert.True(definition.Resolution.DraftAngle);
        Assert.True(definition.Resolution.RefSketchIndex);
        Assert.True(definition.Resolution.FlipSide);
        Assert.Null(definition.Type);
        Assert.False(definition.Resolution.Type);
    }

    [Theory]
    [InlineData("rib_norm_to_sketch", false)]
    [InlineData("rib_norm_to_sketch_resave", false)]
    [InlineData("rib_reverse_material_norm", true)]
    [InlineData("rib_reverse_material_norm_resave", true)]
    public void RibMeasuredFlipSide(string sample, bool flipSide)
    {
        var definition = Definition<RibFeatureModel>(sample);
        Assert.Equal(1, definition.ExtrusionDirection);
        Assert.Equal(flipSide, definition.FlipSide);
        Assert.True(definition.Resolution.FlipSide);
    }

    [Theory]
    [InlineData("ribmod_type0")]
    [InlineData("ribmod_type0_resave")]
    [InlineData("ribmod_type1")]
    [InlineData("ribmod_type1_resave")]
    public void RibType_HasNoStableScalarSlot(string sample)
    {
        var definition = Definition<RibFeatureModel>(sample);
        Assert.Null(definition.Type);
        Assert.False(definition.Resolution.Type);
        Assert.False(definition.FlipSide);
        Assert.True(definition.Resolution.FlipSide);
        Assert.Equal(0, definition.RefSketchIndex);
    }

    [Theory]
    [InlineData("gussetmoda_base", 1, 0.005, true)]
    [InlineData("gussetmoda_base_resave", 1, 0.005, true)]
    [InlineData("gussetmoda_thickness_002", 1, 0.002, true)]
    [InlineData("gussetmoda_thickness_002_resave", 1, 0.002, true)]
    [InlineData("gussetmoda_profile_polygon", 0, 0.005, true)]
    [InlineData("gussetmoda_profile_polygon_resave", 0, 0.005, true)]
    [InlineData("gussetmoda_use_length_false_angle", 1, 0.005, false)]
    [InlineData("gussetmoda_use_length_false_angle_resave", 1, 0.005, false)]
    public void GussetMeasuredProfileAndThickness(
        string sample, int profileType, double thickness, bool useAngle)
    {
        var definition = Definition<GussetFeatureModel>(sample);
        Assert.Equal(profileType, definition.ProfileType);
        Assert.Equal(thickness, definition.Thickness!.Value, 12);
        Assert.Equal(useAngle, definition.UseAngle);
        Assert.Null(definition.ProfileLocation);
        Assert.True(definition.Resolution.ProfileType);
        Assert.True(definition.Resolution.Thickness);
        Assert.True(definition.Resolution.UseAngle);
        Assert.False(definition.Resolution.ProfileLocation);
    }

    [Theory]
    [InlineData("split_base", true, 1)]
    [InlineData("split_base_resave", true, 1)]
    [InlineData("split_consume_false", false, 1)]
    [InlineData("split_consume_false_resave", false, 1)]
    [InlineData("split_trimtools2", true, 2)]
    [InlineData("split_trimtools2_resave", true, 2)]
    public void SplitMeasuredConsumeAndTrimToolCount(
        string sample, bool consume, int trimToolsCount)
    {
        var definition = Definition<SplitBodyFeatureModel>(sample);
        Assert.Equal(consume, definition.Consume);
        Assert.Equal(trimToolsCount, definition.TrimToolsCount);
        Assert.Null(definition.State);
        Assert.Null(definition.TrimTools);
        Assert.True(definition.Resolution.Consume);
        Assert.True(definition.Resolution.TrimToolsCount);
        Assert.False(definition.Resolution.State);
        Assert.False(definition.Resolution.TrimTools);
    }

    [Theory]
    [InlineData("surfacetrim_mutual_tools3", 1)]
    [InlineData("surfacetrim_mutual_tools3_resave", 1)]
    [InlineData("surfacetrim_standard_tool1", 0)]
    [InlineData("surfacetrim_standard_tool1_resave", 0)]
    public void SurfaceTrimMeasuredType(string sample, int trimType)
    {
        var definition = Definition<SurfaceTrimFeatureModel>(sample);
        Assert.Equal(trimType, definition.TrimType);
        Assert.True(definition.Resolution.TrimType);
        Assert.Null(definition.TrimTools);
        Assert.Null(definition.TrimToolsCount);
        Assert.Null(definition.PiecesToKeep);
        Assert.False(definition.Resolution.TrimTools);
        Assert.False(definition.Resolution.PiecesToKeep);
    }

    private static T Definition<T>(string sample) where T : class, IFeatureData
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Model is T));
        return Assert.IsType<T>(feature.Model);
    }

    private static string Sample(string name) =>
        TestFileProvider.Resolve("Features2026/OfficialParamVariants", name + ".SLDPRT");
}
