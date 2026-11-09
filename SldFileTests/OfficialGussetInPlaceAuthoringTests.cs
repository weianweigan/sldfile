using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Gusset feature authoring and parse tests. Verifies pure MFC object model serialization
/// and bi-directional fidelity without binary template stitching.
/// </summary>
public class OfficialGussetInPlaceAuthoringTests
{
    [Theory]
    [InlineData("official_gusset_weldment_box3")]
    [InlineData("official_gusset_weldment_box3_resave")]
    public void NativeGussetFiles_ParseCorrectlyAsGussetFeatureModel(string sourceName)
    {
        string source = TestFileProvider.Resolve(
            "Features2026/OfficialChmRecipes", sourceName + ".SLDPRT");

        using var part = new SldPart(source, readOnly: true);
        var gussetFeatures = part.Features.Where(f => f.Kind == FeatureKind.Gusset).ToList();
        Assert.NotEmpty(gussetFeatures);

        foreach (var feat in gussetFeatures)
        {
            Assert.Equal("moGusset_c", feat.TypeName);
            var model = Assert.IsType<GussetFeatureModel>(feat.Model);
            Assert.Equal("moGusset_c", model.ClassName);
        }
    }

    [Fact]
    public void AddGusset_ObjectOrientedModel_AddsFeatureAndRoundtrips()
    {
        string stripped = TestFileProvider.Resolve(
            "Features2026/OfficialGussetInPlace", "gusset_primary_stripped.SLDPRT");
        string temp = CopyToTemp(stripped);
        try
        {
            using (var part = new SldPart(temp, readOnly: false))
            {
                var model = new GussetFeatureModel(0.005)
                {
                    FeatureName = "TestGusset1"
                };
                var created = part.AddGusset(model);
                Assert.Equal("TestGusset1", created.Name);
                Assert.Equal(FeatureKind.Gusset, created.Kind);
                part.Save();
            }

            using (var part = new SldPart(temp, readOnly: true))
            {
                var created = Assert.Single(part.Features.Where(f => f.Name == "TestGusset1"));
                Assert.Equal(FeatureKind.Gusset, created.Kind);
                var model = Assert.IsType<GussetFeatureModel>(created.Model);
                Assert.Equal("moGusset_c", model.ClassName);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    [Fact]
    public void AddGusset_DefaultOverload_CreatesValidGusset()
    {
        string stripped = TestFileProvider.Resolve(
            "Features2026/OfficialGussetInPlace", "gusset_primary_stripped.SLDPRT");
        string temp = CopyToTemp(stripped);
        try
        {
            using (var part = new SldPart(temp, readOnly: false))
            {
                var created = part.AddGusset("MyGusset");
                Assert.Equal("MyGusset", created.Name);
                Assert.Equal(FeatureKind.Gusset, created.Kind);
                part.Save();
            }

            using (var part = new SldPart(temp, readOnly: true))
            {
                var created = Assert.Single(part.Features.Where(f => f.Name == "MyGusset"));
                Assert.Equal(FeatureKind.Gusset, created.Kind);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static string CopyToTemp(string source)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "sldfile_gusset_" + Guid.NewGuid().ToString("N") + ".SLDPRT");
        File.Copy(source, path, true);
        return path;
    }
}
