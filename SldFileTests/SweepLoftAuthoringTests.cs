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
/// Authoring tests for sweep and loft combinations using strongly-typed MFC models.
/// </summary>
public class SweepLoftAuthoringTests
{
    [Theory]
    [InlineData("sweep_circle10_path40", "SweepProfilePathPrimary")]
    [InlineData("sweep_circle10_path40_resave", "SweepProfilePathResave")]
    public void SweepAuthoring_ReEmitsMeasuredBlockVerbatim(string sampleName, string templateProperty)
    {
        _ = templateProperty;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath(sampleName));
        string featureName = "扫描_Test";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new SweepFeatureModel { FeatureName = featureName };
            var feature = part.AddSweep(model);
            Assert.NotNull(feature);
            Assert.Equal(featureName, feature.Name);
            Assert.Equal(FeatureKind.Sweep, feature.Kind);
            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
            Assert.Equal(FeatureKind.Sweep, feature.Kind);
            var definition = Assert.IsType<SweepFeatureModel>(feature.Model);
            Assert.Equal(featureName, definition.DisplayName);
            Assert.Equal("moSweep_c", definition.ClassName);
        }
    }

    [Theory]
    [InlineData("loft_circle10_20", "LoftTwoProfilesPrimary")]
    [InlineData("loft_circle10_20_resave", "LoftTwoProfilesResave")]
    public void LoftAuthoring_ReEmitsMeasuredBlockVerbatim(string sampleName, string templateProperty)
    {
        _ = templateProperty;
        using var temp = TestFileProvider.CreateTempCopy(LoftPath(sampleName));
        string featureName = "放样_Test";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new LoftFeatureModel { FeatureName = featureName };
            var feature = part.AddLoft(model);
            Assert.NotNull(feature);
            Assert.Equal(featureName, feature.Name);
            Assert.Equal(FeatureKind.Loft, feature.Kind);
            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
            Assert.Equal(FeatureKind.Loft, feature.Kind);
            var definition = Assert.IsType<LoftFeatureModel>(feature.Model);
            Assert.Equal(featureName, definition.DisplayName);
            Assert.Equal("moBlend_c", definition.ClassName);
        }
    }

    [Theory]
    [InlineData("sweep_circle10_path40", "Primary", false, 0, 0.0)]
    [InlineData("sweep_circle10_path40_resave", "Resave", false, 0, 0.0)]
    [InlineData("sweep_circle10_path40", "Primary", true, 8, Math.PI / 2)]
    [InlineData("sweep_circle10_path40_resave", "Resave", true, 8, Math.PI / 2)]
    [InlineData("sweep_circle10_path40", "Primary", true, 1, Math.PI / 2)]
    [InlineData("sweep_circle10_path40_resave", "Resave", true, 1, Math.PI / 2)]
    [InlineData("sweep_circle10_path40", "Primary", true, 1, 0.0)]
    public void SweepOptions_AuthoringPatchesMergeAndTwistAndReReads(
        string sampleName, string variant, bool merge, int twistControlType, double twistAngle)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath(sampleName));
        string featureName = "扫描_Twist";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new SweepFeatureModel
            {
                FeatureName = featureName,
                Merge = merge,
                TwistControlType = twistControlType,
                TwistAngleRad = twistAngle
            };
            var feature = part.AddSweep(model);
            Assert.NotNull(feature);
            var definition = Assert.IsType<SweepFeatureModel>(feature.Model);
            Assert.Equal(merge, definition.Merge);
            Assert.Equal(twistControlType, definition.TwistControlType);
            Assert.Equal(twistAngle, definition.TwistAngleRad!.Value, 12);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        var reDefinition = Assert.IsType<SweepFeatureModel>(reFeature.Model);
        Assert.Equal(merge, reDefinition.Merge);
        Assert.Equal(twistControlType, reDefinition.TwistControlType);
        Assert.Equal(twistAngle, reDefinition.TwistAngleRad!.Value, 12);
    }

    [Fact]
    public void SweepOptions_RejectsInvalidValues_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddSweep(new SweepFeatureModel()));
    }

    [Theory]
    [InlineData("loft_centerline", "Primary")]
    [InlineData("loft_centerline_resave", "Resave")]
    public void LoftCenterline_AuthoringAppendsMeasuredBlockAndReReads(
        string sampleName, string variant)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(LoftPath(sampleName));
        string featureName = "放样_Centerline";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new LoftFeatureModel
            {
                FeatureName = featureName,
                Centerline = new SketchCurveReference { EntityId = 0x49u, Role = SketchCurveRole.Guide }
            };
            var feature = part.AddLoft(model);
            Assert.NotNull(feature);
            var definition = Assert.IsType<LoftFeatureModel>(feature.Model);
            Assert.Equal(0x49u, definition.Centerline!.EntityId);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        var reDefinition = Assert.IsType<LoftFeatureModel>(reFeature.Model);
        Assert.NotNull(reDefinition);
    }

    [Fact]
    public void LoftCenterline_RejectsWrongBaseWithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(LoftPath("loft_circle10_20"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddLoft(centerline: true));
    }

    [Fact]
    public void AuthoredSweep_SurvivesResave()
    {
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        string featureName = "扫描_Resave";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddSweep(new SweepFeatureModel { FeatureName = featureName });
            part.Save();
        }

        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        Assert.Equal(FeatureKind.Sweep, feature.Kind);
    }

    [Fact]
    public void AddSweep_RejectsModifiedBase_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddSweep());
    }

    [Fact]
    public void AddSweep_RejectsCustomNameAndSketchNames_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddSweep((SweepFeatureModel)null!));
    }

    [Fact]
    public void AddLoft_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(LoftPath("loft_circle10_20"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddLoft((LoftFeatureModel)null!));
    }

    [Theory]
    [InlineData("sweep_circular_profile12", "Primary", "Path", "BF", true, 0.012, false, 0.0)]
    [InlineData("sweep_circular_profile12", "Resave", "Path", "BF", true, 0.012, false, 0.0)]
    [InlineData("sweep_thin_one_dir_t1", "Primary", "Profile,Path", "C2,CD", false, 0.0, true, 0.001)]
    [InlineData("sweep_thin_one_dir_t1", "Resave", "Profile,Path", "C2,CD", false, 0.0, true, 0.001)]
    [InlineData("sweep_guide1", "Primary", "Profile,Path,Guide", "C5,DC,D1", false, 0.0, false, 0.0)]
    [InlineData("sweep_guide1", "Resave", "Profile,Path,Guide", "C5,DC,D1", false, 0.0, false, 0.0)]
    [InlineData("sweep_profile_rect", "Primary", "Profile,Path", "C2,DF", false, 0.0, false, 0.0)]
    [InlineData("sweep_profile_rect", "Resave", "Profile,Path", "C2,DF", false, 0.0, false, 0.0)]
    public void AdditionalSweepCombinations_AuthoringAndReRead(
        string sampleName, string variant, string roles, string ids,
        bool circularProfile, double diameter, bool thinFeature, double thickness)
    {
        _ = roles;
        _ = ids;
        string fileName = variant == "Resave" ? sampleName + "_resave" : sampleName;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath(fileName));
        string featureName = "Sweep_" + sampleName;

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new SweepFeatureModel
            {
                FeatureName = featureName,
                CircularProfile = circularProfile,
                CircularProfileDiameter = diameter,
                IsThinFeature = thinFeature,
                WallThickness = thickness
            };
            part.AddSweep(model);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        var definition = Assert.IsType<SweepFeatureModel>(feature.Model);
        Assert.Equal(circularProfile, definition.CircularProfileLayout);
        Assert.Equal(thinFeature, definition.ThinFeatureLayout);
        if (circularProfile) Assert.Equal(diameter, definition.CircularProfileDiameter!.Value, 12);
        if (thinFeature) Assert.Equal(thickness, definition.ThinWallThickness!.Value, 12);
    }

    [Theory]
    [InlineData("loft_three_sections", "Primary", "Profile,Profile,Profile", "CB,E7,FB")]
    [InlineData("loft_three_sections", "Resave", "Profile,Profile,Profile", "CB,E7,FB")]
    [InlineData("loft_guide1", "Primary", "Profile,Profile,Guide", "D7,F2,C8")]
    [InlineData("loft_guide1", "Resave", "Profile,Profile,Guide", "D7,F2,C8")]
    [InlineData("loft_start_end_tangency", "Primary", "Profile,Profile", "C5,E1")]
    [InlineData("loft_start_end_tangency", "Resave", "Profile,Profile", "C5,E1")]
    public void AdditionalLoftCombinations_AuthoringAndReRead(
        string sampleName, string variant, string roles, string ids)
    {
        _ = variant;
        _ = roles;
        _ = ids;
        string fileName = variant == "Resave" ? sampleName + "_resave" : sampleName;
        using var temp = TestFileProvider.CreateTempCopy(LoftPath(fileName));
        string featureName = "Loft_" + sampleName;

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new LoftFeatureModel { FeatureName = featureName };
            part.AddLoft(model);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        Assert.IsType<LoftFeatureModel>(feature.Model);
    }

    [Fact]
    public void AddSweep_RejectsMismatchedMeasuredSketchLayout()
    {
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circular_profile12"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddSweep(profileSketchName: "Profile1"));
    }

    private static string SweepPath(string name) =>
        TestFileProvider.Resolve("Features2026/SweepOptions", name + ".SLDPRT");

    private static string LoftPath(string name) =>
        TestFileProvider.Resolve("Features2026/LoftOptions", name + ".SLDPRT");
}
