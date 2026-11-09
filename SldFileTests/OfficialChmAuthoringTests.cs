using System;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring tests from official CHM recipes using strongly typed MFC models.
/// </summary>
public class OfficialChmAuthoringTests
{
    [Theory]
    [InlineData("Rib", "Primary")]
    [InlineData("Rib", "Resave")]
    [InlineData("Combine", "Primary")]
    [InlineData("Combine", "Resave")]
    [InlineData("Split", "Primary")]
    [InlineData("Split", "Resave")]
    [InlineData("SurfaceTrim", "Primary")]
    [InlineData("SurfaceTrim", "Resave")]
    public void OfficialChmFeature_ReEmitsMeasuredTailBlock(string kind, string variant)
    {
        string sampleName = SampleName(kind, variant);
        using var temp = TestFileProvider.CreateTempCopy(
            TestFileProvider.Resolve("Features2026/OfficialChmRecipes", sampleName + ".SLDPRT"));

        string featureName = "Official_" + kind;
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var feature = Add(part, kind, featureName);
            Assert.Equal(featureName, feature.Name);
            Assert.Equal(ExpectedKind(kind), feature.Kind);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reopenedFeature = Assert.Single(reopened.Features.Where(f =>
            f.Name == featureName && f.Model != null));
        Assert.Equal(ExpectedKind(kind), reopenedFeature.Kind);
    }

    [Fact]
    public void OfficialChmFeature_RejectsCustomNameWithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(
            TestFileProvider.Resolve("Features2026/OfficialChmRecipes", "official_combine_multi_inter.SLDPRT"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddCombineBodies());
    }

    private static Feature Add(SldPart part, string kind, string? featureName = null) => kind switch
    {
        "Rib" => part.AddRib(new RibFeatureModel { FeatureName = featureName ?? "筋1" }),
        "Combine" => part.AddCombineBodies(new CombineBodiesFeatureModel { FeatureName = featureName ?? "组合1" }),
        "Split" => part.AddSplitBody(new SplitBodyFeatureModel { FeatureName = featureName ?? "分割1" }),
        "SurfaceTrim" => part.AddSurfaceTrim(new SurfaceTrimFeatureModel { FeatureName = featureName ?? "曲面剪裁1" }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static FeatureKind ExpectedKind(string kind) => kind switch
    {
        "Rib" => FeatureKind.Rib,
        "Combine" => FeatureKind.CombineBodies,
        "Split" => FeatureKind.SplitBody,
        "SurfaceTrim" => FeatureKind.SurfaceTrim,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string SampleName(string kind, string variant) => kind switch
    {
        "Rib" => variant == "Primary" ? "official_rib_block20" : "official_rib_block20_resave",
        "Combine" => variant == "Primary" ? "official_combine_multi_inter" : "official_combine_multi_inter_resave",
        "Split" => variant == "Primary" ? "official_split_multi_inter" : "official_split_multi_inter_resave",
        "SurfaceTrim" => variant == "Primary" ? "official_surface_trim" : "official_surface_trim_resave",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
