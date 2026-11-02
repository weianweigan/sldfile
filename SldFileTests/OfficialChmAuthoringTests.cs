using System;
using System.Linq;
using SldFile;
using SldFile.Binary.FeatureData;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed measured authoring from the official CHM recipe artifacts. Each target is the
/// last feature of its official sample, so the exact stripped prefix is hash-pinned and
/// the measured block is appended verbatim without parameter synthesis.
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
        var template = OfficialChmAuthoringTemplates.All.Single(t =>
            t.Kind == kind && t.Variant == variant);
        string sampleName = SampleName(kind, variant);
        using var temp = TestFileProvider.CreateTempCopy(
            TestFileProvider.Resolve("Features2026/OfficialChmRecipes", sampleName + ".SLDPRT"));

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] raw = part.GetStreamBytes(streamPath)!;
            Assert.True(raw.Length > template.BaseLength);
            var stripped = new byte[template.BaseLength];
            Array.Copy(raw, stripped, stripped.Length);
            BitConverter.GetBytes(template.StrippedFolderCount).CopyTo(stripped, 4);
            part.SetPendingStream(streamPath, stripped);
            part.Save();
        }

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            var feature = Add(part, kind);
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Equal(template.BaseLength + template.Block.Length, authored.Length);
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
            Assert.Equal(template.Name, feature.Name);
            Assert.NotNull(feature.Definition);
            Assert.Equal(template.ClassName, feature.Definition!.ClassName);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reopenedFeature = Assert.Single(reopened.Features.Where(f =>
            f.Name == template.Name && f.Definition != null));
        Assert.Equal(template.ClassName, reopenedFeature.Definition!.ClassName);
        Assert.Equal(ExpectedKind(kind), reopenedFeature.Kind);
    }

    [Fact]
    public void OfficialChmFeature_RejectsCustomNameWithoutChangingStream()
    {
        var template = OfficialChmAuthoringTemplates.All.Single(t =>
            t.Kind == "Combine" && t.Variant == "Primary");
        using var temp = TestFileProvider.CreateTempCopy(
            TestFileProvider.Resolve("Features2026/OfficialChmRecipes", "official_combine_multi_inter.SLDPRT"));
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] raw = part.GetStreamBytes(streamPath)!;
            var stripped = new byte[template.BaseLength];
            Array.Copy(raw, stripped, stripped.Length);
            BitConverter.GetBytes(template.StrippedFolderCount).CopyTo(stripped, 4);
            part.SetPendingStream(streamPath, stripped);
            part.Save();
        }

        using var authoring = new SldPart(temp.FilePath, readOnly: false);
        string path = authoring.ResolvedFeaturesStreamPath!;
        byte[] before = (byte[])authoring.GetStreamBytes(path)!.Clone();
        Assert.Throws<NotSupportedException>(() => authoring.AddCombineBodies("CustomName"));
        Assert.Equal(before, authoring.GetStreamBytes(path));
    }

    private static Feature Add(SldPart part, string kind) => kind switch
    {
        "Rib" => part.AddRib(),
        "Combine" => part.AddCombineBodies(),
        "Split" => part.AddSplitBody(),
        "SurfaceTrim" => part.AddSurfaceTrim(),
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
