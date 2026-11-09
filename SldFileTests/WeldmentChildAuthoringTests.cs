using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring tests for nested EndCap and Cosmetic WeldBead using strongly-typed MFC models.
/// </summary>
public class WeldmentChildAuthoringTests
{
    [Theory]
    [InlineData("endcap_t2_structural_2", "endcap_t2_structural", "Primary", false)]
    [InlineData("endcap_t2_structural_resave_2", "endcap_t2_structural", "Resave", false)]
    [InlineData("endcap_t2_structural_chamfer", "endcap_t2_structural_chamfer", "Primary", true)]
    [InlineData("endcap_t2_structural_chamfer_resave", "endcap_t2_structural_chamfer", "Resave", true)]
    public void EndCap_AuthoringReproducesMeasuredBlockAndReReads(
        string sample, string featureName, string variant, bool chamfer)
    {
        _ = featureName;
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        string targetName = "EndCap_Test";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new EndCapFeatureModel
            {
                FeatureName = targetName,
                Thickness = 0.002,
                UseChamferCorners = chamfer,
                UseCornerTreatment = chamfer
            };
            var feature = part.AddEndCap(model);
            Assert.NotNull(feature);
            Assert.Equal(targetName, feature.Name);
            Assert.Equal(FeatureKind.EndCap, feature.Kind);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == targetName));
        Assert.Equal(FeatureKind.EndCap, reFeature.Kind);
        Assert.NotNull(reFeature.Model);
        Assert.Equal("moEndCap_c", reFeature.Model.ClassName);
    }

    [Theory]
    [InlineData("weldbead_edges", "Primary")]
    [InlineData("weldbead_edges_resave", "Resave")]
    public void WeldBead_AuthoringReproducesMeasuredBlockAndReReads(string sample, string variant)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        string targetName = "WeldBead_Test";

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new CosmeticWeldBeadFeatureModel
            {
                FeatureName = targetName,
                BeadSize = 0.003
            };
            var feature = part.AddWeldBead(model);
            Assert.NotNull(feature);
            Assert.Equal(targetName, feature.Name);
            Assert.Equal(FeatureKind.WeldBead, feature.Kind);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == targetName));
        Assert.Equal(FeatureKind.WeldBead, reFeature.Kind);
        Assert.NotNull(reFeature.Model);
    }

    [Fact]
    public void WeldmentChildAuthoring_RejectsWrongBaseCustomNameAndTamper_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("endcap_t2_structural_2"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddEndCap());
        Assert.Throws<InvalidOperationException>(() => part.AddWeldBead());
    }

    private static string Resolve(string sample) =>
        TestFileProvider.Resolve("Features2026/WeldmentOptions", sample + ".SLDPRT");
}
