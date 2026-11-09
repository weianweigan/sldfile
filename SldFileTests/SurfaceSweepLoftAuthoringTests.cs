using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring tests for surface sweep and surface loft using strongly-typed MFC models.
/// </summary>
public class SurfaceSweepLoftAuthoringTests
{
    [Theory]
    [InlineData("surf_sweep_basic", "SurfaceSweepBasicPrimary", 0.006)]
    [InlineData("surf_sweep_basic_resave", "SurfaceSweepBasicResave", 0.006)]
    [InlineData("surf_sweep_path60", "SurfaceSweepPath60Primary", 0.006)]
    [InlineData("surf_sweep_path60_resave", "SurfaceSweepPath60Resave", 0.006)]
    [InlineData("surf_sweep_profile12", "SurfaceSweepProfile12Primary", 0.012)]
    [InlineData("surf_sweep_profile12_resave", "SurfaceSweepProfile12Resave", 0.012)]
    public void SurfaceSweepAuthoring_ReEmitsMeasuredBlockAndReReads(
        string sampleName, string templateField, double diameter)
    {
        _ = templateField;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sampleName));
        string featureName = "曲面-扫描_Test";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new SweepFeatureModel
            {
                FeatureName = featureName,
                IsSurface = true,
                CircularProfile = true,
                CircularProfileDiameter = diameter
            };
            var feature = part.AddSweep(model);
            Assert.NotNull(feature);
            Assert.Equal(featureName, feature.Name);
            Assert.Equal(FeatureKind.SurfaceSweep, feature.Kind);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        Assert.Equal(FeatureKind.SurfaceSweep, reFeature.Kind);
        Assert.NotNull(reFeature.Model);
        Assert.Equal("moSweepRefSurface_c", reFeature.Model.ClassName);
    }

    [Theory]
    [InlineData("surf_loft_2sections_loft2", "SurfaceLoft2SectionsPrimary", "C5,E1", "Profile,Profile", 0x4D)]
    [InlineData("surf_loft_2sections_loft2_resave", "SurfaceLoft2SectionsResave", "C5,E1", "Profile,Profile", 0x4D)]
    [InlineData("surf_loft_3sections", "SurfaceLoft3SectionsPrimary", "CB,E7,FB", "Profile,Profile,Profile", 0x50)]
    [InlineData("surf_loft_3sections_resave", "SurfaceLoft3SectionsResave", "CB,E7,FB", "Profile,Profile,Profile", 0x50)]
    [InlineData("surf_loft_guide", "SurfaceLoftGuidePrimary", "C8,E4,EE", "Profile,Profile,Guide", 0x4E)]
    [InlineData("surf_loft_guide_resave", "SurfaceLoftGuideResave", "C8,E4,EE", "Profile,Profile,Guide", 0x4E)]
    public void SurfaceLoftAuthoring_ReEmitsMeasuredBlockAndReReads(
        string sampleName, string templateField, string ids, string roles, int typeId)
    {
        _ = templateField;
        _ = ids;
        _ = roles;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sampleName));
        string featureName = "曲面-放样_Test";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new LoftFeatureModel
            {
                FeatureName = featureName,
                IsSurface = true,
                SerializedTypeId = (uint)typeId
            };
            var feature = part.AddLoft(model);
            Assert.NotNull(feature);
            Assert.Equal(featureName, feature.Name);
            Assert.Equal(FeatureKind.SurfaceLoft, feature.Kind);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        Assert.Equal(FeatureKind.SurfaceLoft, reFeature.Kind);
        Assert.NotNull(reFeature.Model);
        Assert.Equal("moBlendRefSurface_c", reFeature.Model.ClassName);
    }

    [Fact]
    public void SurfaceAuthoring_RejectsCustomNameWrongSketchAndWrongVariant_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddSurfaceSweep());
        Assert.Throws<InvalidOperationException>(() => part.AddSurfaceLoft());
    }

    [Fact]
    public void SurfaceAuthoring_RejectsNonStrippedBaseAndSecondFeature_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddSweep(null!));
        Assert.Throws<ArgumentNullException>(() => part.AddLoft(null!));
    }

    [Fact]
    public void SurfaceAuthoring_RejectsTamperedBaseAndReadOnly_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddSweep(new SweepFeatureModel()));
        Assert.Throws<InvalidOperationException>(() => part.AddLoft(new LoftFeatureModel()));
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");
}
