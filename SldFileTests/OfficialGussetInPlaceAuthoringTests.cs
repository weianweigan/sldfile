using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// In-place Gusset authoring self-checks. The template was stripped from the native
/// official primary/resave sample by removing the class-tagged Gusset block while
/// preserving every following feature block and its order. AddGusset must rebuild
/// the measured stream byte-for-byte at the recorded insert offset.
/// </summary>
public class OfficialGussetInPlaceAuthoringTests
{
    [Theory]
    [InlineData("gusset_primary_stripped", "official_gusset_weldment_box3")]
    [InlineData("gusset_resave_stripped", "official_gusset_weldment_box3_resave")]
    public void AddGusset_ReconstructsMeasuredStream(string strippedName, string sourceName)
    {
        string stripped = TestFileProvider.Resolve(
            "Features2026/OfficialGussetInPlace", strippedName + ".SLDPRT");
        string source = TestFileProvider.Resolve(
            "Features2026/OfficialChmRecipes", sourceName + ".SLDPRT");
        byte[] expected = ReadStream(source);
        string temp = CopyToTemp(stripped);
        try
        {
            using (var part = new SldPart(temp, readOnly: false))
            {
                Assert.DoesNotContain(part.Features,
                    f => f.Name == "official_gusset_weldment_box3");
                var feature = part.AddGusset();
                Assert.Equal("official_gusset_weldment_box3", feature.Name);
                Assert.Equal(FeatureKind.Gusset, feature.Kind);
                part.Save();
            }

            using (var part = new SldPart(temp, readOnly: true))
            {
                Assert.Equal(expected, ReadStream(part));
                var feature = Assert.Single(part.Features.Where(
                    f => f.Name == "official_gusset_weldment_box3"));
                Assert.Equal(FeatureKind.Gusset, feature.Kind);
                var definition = Assert.IsType<GussetFeatureDefinitionData>(feature.Definition);
                Assert.Equal("moGusset_c", definition.ClassName);
            }
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void AddGusset_RejectsCustomNameWithoutChangingStream()
    {
        string stripped = TestFileProvider.Resolve(
            "Features2026/OfficialGussetInPlace", "gusset_primary_stripped.SLDPRT");
        string temp = CopyToTemp(stripped);
        try
        {
            using var part = new SldPart(temp, readOnly: false);
            byte[] before = ReadStream(part);
            Assert.Throws<NotSupportedException>(() => part.AddGusset("custom_gusset"));
            Assert.Equal(before, ReadStream(part));
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void AddGusset_RejectsUnrelatedBase()
    {
        string other = TestFileProvider.Resolve(
            "Features2026/OfficialChmRecipes", "official_rib_block20.SLDPRT");
        string temp = CopyToTemp(other);
        try
        {
            using var part = new SldPart(temp, readOnly: false);
            Assert.Throws<NotSupportedException>(() => part.AddGusset());
        }
        finally { File.Delete(temp); }
    }

    private static byte[] ReadStream(SwFile file)
    {
        string path = ((SldPart)file).ResolvedFeaturesStreamPath!;
        return file.GetStreamBytes(path)!;
    }

    private static byte[] ReadStream(string path)
    {
        using var part = new SldPart(path, readOnly: true);
        return ReadStream(part);
    }

    private static string CopyToTemp(string source)
    {
        string path = Path.Combine(Path.GetTempPath(),
            "sldfile_gusset_" + Guid.NewGuid().ToString("N") + ".SLDPRT");
        File.Copy(source, path, true);
        return path;
    }
}
