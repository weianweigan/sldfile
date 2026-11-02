using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured authoring for the nested EndCap / cosmetic WeldBead samples.
/// The controlled base is the exact stream prefix before the nested feature tag.
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
        var template = Template("moEndCap_c", featureName, variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddEndCap(chamfer);
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            var definition = Assert.IsType<EndCapFeatureDefinitionData>(feature.Definition);
            Assert.Equal(0.002, definition.Thickness!.Value, 12);
            Assert.Equal(chamfer, definition.UseChamferCorners);
            Assert.Equal(chamfer, definition.UseCornerTreatment);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f =>
            f.Definition is EndCapFeatureDefinitionData));
        Assert.Equal(FeatureKind.EndCap, reFeature.Kind);
        var reDefinition = Assert.IsType<EndCapFeatureDefinitionData>(reFeature.Definition);
        Assert.Equal(0.002, reDefinition.Thickness!.Value, 12);
        Assert.Equal(chamfer, reDefinition.UseChamferCorners);
    }

    [Theory]
    [InlineData("weldbead_edges", "Primary")]
    [InlineData("weldbead_edges_resave", "Resave")]
    public void WeldBead_AuthoringReproducesMeasuredBlockAndReReads(string sample, string variant)
    {
        var template = Template("moCosmeticWeldBead_c", "weldbead_edges", variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddWeldBead();
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            var definition = Assert.IsType<WeldBeadFeatureDefinitionData>(feature.Definition);
            Assert.Equal(0.003, definition.BeadSize!.Value, 12);
            Assert.Single(definition.EdgeReferences!);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f =>
            f.Definition is WeldBeadFeatureDefinitionData));
        Assert.Equal(FeatureKind.WeldBead, reFeature.Kind);
        var reDefinition = Assert.IsType<WeldBeadFeatureDefinitionData>(reFeature.Definition);
        Assert.Equal(0.003, reDefinition.BeadSize!.Value, 12);
    }

    [Fact]
    public void WeldmentChildAuthoring_RejectsWrongBaseCustomNameAndTamper_WithoutChangingStream()
    {
        var template = Template("moEndCap_c", "endcap_t2_structural", "Primary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("endcap_t2_structural_2")))
        {
            StripInPlace(temp, template);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            Assert.Throws<NotSupportedException>(() =>
                part.AddEndCap(false, featureName: "CustomEndCap"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            byte[] tampered = (byte[])stripped.Clone();
            tampered[tampered.Length / 2] ^= 0x01;
            part.SetPendingStream(streamPath, tampered);
            Assert.Throws<NotSupportedException>(() => part.AddEndCap());
            Assert.Equal(tampered, part.GetStreamBytes(streamPath));
        }

        using (var temp = TestFileProvider.CreateTempCopy(Resolve("endcap_t2_structural_2")))
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] before = (byte[])part.GetStreamBytes(streamPath)!.Clone();
            Assert.Throws<NotSupportedException>(() => part.AddEndCap());
            Assert.Equal(before, part.GetStreamBytes(streamPath));
        }
    }

    private static WeldmentChildAuthoringTemplate Template(
        string className, string name, string variant) =>
        WeldmentChildAuthoringTemplates.All.Single(t =>
            string.Equals(t.ClassName, className, StringComparison.Ordinal) &&
            string.Equals(t.Name, name, StringComparison.Ordinal) &&
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static void StripInPlace(TempTestFile temp, WeldmentChildAuthoringTemplate template)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        byte[] stripped = new byte[template.BaseLength];
        Array.Copy(full, 0, stripped, 0, stripped.Length);
        part.SetPendingStream(streamPath, stripped);
        part.Save();
    }

    private static string Resolve(string sample) =>
        TestFileProvider.Resolve("Features2026/WeldmentOptions", sample + ".SLDPRT");
}
