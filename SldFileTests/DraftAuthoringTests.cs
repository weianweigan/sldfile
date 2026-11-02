using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured Draft authoring tests. The block is re-emitted verbatim on the exact
/// stripped base; the 3-degree angle and face references remain the measured values.
/// </summary>
public class DraftAuthoringTests
{
    [Theory]
    [InlineData("draft_side_3deg_neutral_bottom", "Primary")]
    [InlineData("draft_side_3deg_neutral_bottom_resave", "Resave")]
    public void DraftAuthoring_ReEmitsMeasuredBlock(string sample, string variant)
    {
        var template = Template(variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddDraft();
            byte[] authored = part.GetStreamBytes(streamPath)!;

            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            for (int i = 0; i < stripped.Length; i++)
            {
                if (i == 4 || i == 5) continue;
                Assert.Equal(stripped[i], authored[i]);
            }
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            AssertDefinition(feature);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "Draft3deg"));
        Assert.Equal(FeatureKind.Draft, reFeature.Kind);
        AssertDefinition(reFeature);
    }

    [Fact]
    public void AuthoredDraft_SurvivesResave()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("draft_side_3deg_neutral_bottom"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddDraft();
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        AssertDefinition(Assert.Single(reopened.Features.Where(f => f.Name == "Draft3deg")));
    }

    [Fact]
    public void DraftAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("draft_side_3deg_neutral_bottom"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddDraft(featureName: "MyDraft"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddDraft());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void DraftAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("draft_side_3deg_neutral_bottom"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddDraft());
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static DraftAuthoringTemplate Template(string variant) =>
        DraftAuthoringTemplates.All.Single(t =>
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/DraftOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, DraftAuthoringTemplate template)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        byte[] stripped = new byte[template.BaseLength];
        Array.Copy(full, 0, stripped, 0, stripped.Length);
        BitConverter.GetBytes((ushort)template.StrippedFolderCount).CopyTo(stripped, 4);
        part.SetPendingStream(streamPath, stripped);
        part.Save();
    }

    private static void AssertDefinition(Feature feature)
    {
        var definition = Assert.IsType<DraftFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moDraft_c", definition.ClassName);
        Assert.Equal(0x51u, definition.SerializedTypeId);
        Assert.Equal(3.0 * Math.PI / 180.0, definition.Angle!.Value, 12);
        Assert.IsType<EntityReference>(definition.NeutralPlane);
        Assert.Null(definition.Type);
        Assert.Null(definition.ReverseDirection);
        Assert.Null(definition.FacesToDraft);
        Assert.True(definition.Resolution.Angle);
        Assert.True(definition.Resolution.NeutralPlane);
        Assert.False(definition.Resolution.Type);
        Assert.False(definition.Resolution.FacesToDraft);
    }
}
