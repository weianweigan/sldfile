using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Structural Member remaining-field evidence: native IDispatch ModifyDefinition
/// persists ApplyCornerTreatment/CornerTreatmentType/RotationAngle (decoded),
/// while ConfigurationName, PathSegments order and LocateProfilePoint replacement
/// produce no persisted file change in the tutorial families and stay null.
/// </summary>
public class StructuralMemberRemainingFieldsTests
{
    [Theory]
    [InlineData("tutorial_weldments_weldment_box2", 2)]
    [InlineData("tutorial_api_weldment_box3", 2)]
    [InlineData("tutorial_api_loop", 1)]
    [InlineData("tutorial_driveworksxpress_leg", 1)]
    [InlineData("tutorial_routing_pipes_framework_done", 1)]
    public void TutorialSamples_DecodeCornerFields_AndLeaveRemainingFieldsBlocked(
        string sample, int expectedCornerType)
    {
        using var file = SwFile.Open(SamplePath("Samples", sample + ".SLDPRT"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Model is StructuralMemberFeatureModel));
        var definition = Assert.IsType<StructuralMemberFeatureModel>(feature.Model);

        Assert.True(definition.ApplyCornerTreatment);
        Assert.Equal(expectedCornerType, definition.CornerTreatmentType);
        Assert.Equal(0.0, definition.RotationAngle!.Value, 12);
        Assert.True(definition.Resolution.ApplyCornerTreatment);
        Assert.True(definition.Resolution.CornerTreatmentType);
        Assert.True(definition.Resolution.RotationAngle);

        // Native probes set ConfigurationName non-empty / reversed PathSegments /
        // re-set LocateProfilePoint; reopen values did not change, so no strict
        // serialized slot is established.
        Assert.Null(definition.ConfigurationName);
        Assert.Null(definition.PathSegments);
        Assert.Null(definition.LocateProfilePoint);
        Assert.False(definition.Resolution.ConfigurationName);
        Assert.False(definition.Resolution.PathSegments);
        Assert.False(definition.Resolution.LocateProfilePoint);
    }

    [Theory]
    [InlineData("tutorial_weldments_weldment_box2_cornerType_1", true, 1, 0.0)]
    [InlineData("tutorial_weldments_weldment_box2_cornerType_2", true, 2, 0.0)]
    [InlineData("tutorial_weldments_weldment_box2_rotation_15deg", true, 2, 0.2617993877991494)]
    [InlineData("tutorial_weldments_weldment_box2_rotation_30deg", true, 2, 0.5235987755982988)]
    [InlineData("tutorial_weldments_weldment_box2_applyCorner_false", false, 0, 0.0)]
    public void NativeModifyVariants_DecodePersistedCornerAndRotation(
        string sample, bool applyCorner, int cornerType, double rotation)
    {
        using var file = SwFile.Open(SamplePath("StructuralModify", "box2", sample + ".SLDPRT"), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Model is StructuralMemberFeatureModel));
        var definition = Assert.IsType<StructuralMemberFeatureModel>(feature.Model);

        Assert.Equal(applyCorner, definition.ApplyCornerTreatment);
        Assert.Equal(cornerType, definition.CornerTreatmentType);
        Assert.Equal(rotation, definition.RotationAngle!.Value, 12);
        Assert.True(definition.Resolution.ApplyCornerTreatment);
        Assert.True(definition.Resolution.CornerTreatmentType);
        Assert.True(definition.Resolution.RotationAngle);

        Assert.Null(definition.ConfigurationName);
        Assert.Null(definition.PathSegments);
        Assert.Null(definition.LocateProfilePoint);
        Assert.False(definition.Resolution.ConfigurationName);
        Assert.False(definition.Resolution.PathSegments);
        Assert.False(definition.Resolution.LocateProfilePoint);
    }

    [Theory]
    [InlineData("tutorial_weldments_weldment_box2_configurationName")]
    [InlineData("tutorial_weldments_weldment_box2_pathSegments_reverse")]
    [InlineData("tutorial_weldments_weldment_box2_locatePoint_same")]
    public void RemainingFieldProbes_HaveNoPersistedStrictSlot(string sample)
    {
        using var file = SwFile.Open(SamplePath("StructuralModify", "box2", sample + ".SLDPRT"), readOnly: true);
        var definition = Assert.IsType<StructuralMemberFeatureModel>(file.Features
            .Single(f => f.Model is StructuralMemberFeatureModel).Model);

        Assert.Null(definition.ConfigurationName);
        Assert.Null(definition.PathSegments);
        Assert.Null(definition.LocateProfilePoint);
        Assert.False(definition.Resolution.ConfigurationName);
        Assert.False(definition.Resolution.PathSegments);
        Assert.False(definition.Resolution.LocateProfilePoint);
    }

    private static string SamplePath(params string[] parts)
    {
        return Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles", "Features2026",
            "LegacyTutorialSamples" }.Concat(parts).ToArray());
    }
}
