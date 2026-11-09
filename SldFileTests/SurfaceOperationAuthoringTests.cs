using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Authoring tests for knit/extend/offset/fill using strongly-typed MFC surface models.
/// </summary>
public class SurfaceOperationAuthoringTests
{
    [Theory]
    [InlineData("surf_knit_two", "Primary", "moSewRefSurface_c")]
    [InlineData("surf_knit_two_resave", "Resave", "moSewRefSurface_c")]
    [InlineData("surf_extend_5mm", "Primary", "moExtendRefSurface_c")]
    [InlineData("surf_extend_5mm_resave", "Resave", "moExtendRefSurface_c")]
    [InlineData("surf_offset_5mm", "Primary", "moOffsetRefSurface_c")]
    [InlineData("surf_offset_5mm_resave", "Resave", "moOffsetRefSurface_c")]
    [InlineData("surf_fill_region", "Primary", "moFillRefSurface_c")]
    [InlineData("surf_fill_region_resave", "Resave", "moFillRefSurface_c")]
    public void SurfaceOperationAuthoring_ReEmitsMeasuredBlock(string sample, string variant, string className)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));

        Feature feature;
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            feature = AddMeasured(part, className);
            Assert.NotNull(feature);
            AssertDefinition(feature, className);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == feature.Name));
        AssertDefinition(reFeature, className);
    }

    [Fact]
    public void SurfaceOperationAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_knit_two"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddSurfaceKnit(new SurfaceKnitFeatureModel()));
    }

    [Fact]
    public void SurfaceOperationAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddSurfaceFill((SurfaceFillFeatureModel)null!));
    }

    [Theory]
    [InlineData("surf_fill_region", "Primary", 1)]
    [InlineData("surf_fill_region", "Primary", 2)]
    [InlineData("surf_fill_region_resave", "Resave", 1)]
    [InlineData("surf_fill_region_resave", "Resave", 2)]
    [InlineData("surf_fill_res2", "Res2Primary", 1)]
    [InlineData("surf_fill_res2", "Res2Primary", 2)]
    [InlineData("surf_fill_res2_resave", "Res2Resave", 1)]
    [InlineData("surf_fill_res2_resave", "Res2Resave", 2)]
    public void FillAuthoring_ParameterizesResolutionControl(
        string sample, string variant, int resolutionControl)
    {
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new SurfaceFillFeatureModel
            {
                FeatureName = "FillSurface",
                ResolutionControl = resolutionControl
            };
            var feature = part.AddSurfaceFill(model);
            Assert.NotNull(feature);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "FillSurface"));
        Assert.Equal("moFillRefSurface_c", reFeature.Model!.ClassName);
    }

    [Fact]
    public void FillAuthoring_RejectsInvalidControlWrongBaseAndCustomName_WithoutChangingStream()
    {
        using var readOnlyTemp = TestFileProvider.CreateTempCopy(Resolve("surf_fill_region"));
        using var readOnly = new SldPart(readOnlyTemp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => readOnly.AddSurfaceFill(new SurfaceFillFeatureModel()));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static Feature AddMeasured(SldPart part, string className) => className switch
    {
        "moSewRefSurface_c" => part.AddSurfaceKnit(new SurfaceKnitFeatureModel()),
        "moExtendRefSurface_c" => part.AddSurfaceExtend(new SurfaceExtendFeatureModel()),
        "moOffsetRefSurface_c" => part.AddSurfaceOffset(new SurfaceOffsetFeatureModel()),
        "moFillRefSurface_c" => part.AddSurfaceFill(new SurfaceFillFeatureModel()),
        _ => throw new InvalidOperationException(className)
    };

    private static void AssertDefinition(Feature feature, string className)
    {
        Assert.Equal(className, ((IFeatureData)feature.Model!).ClassName);
    }
}
