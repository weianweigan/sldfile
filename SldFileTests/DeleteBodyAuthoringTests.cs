using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured DeleteBody authoring tests. The block is re-emitted verbatim on the
/// exact stripped base; the body choice remains the measured default-selection result.
/// </summary>
public class DeleteBodyAuthoringTests
{
    [Theory]
    [InlineData("delete_body_second", "Primary")]
    [InlineData("delete_body_second_resave", "Resave")]
    public void DeleteBodyAuthoring_ReEmitsMeasuredBlock(string sample, string variant)
    {
        var template = Template(variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddDeleteBody();
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
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == "DeleteBody"));
        Assert.Equal(FeatureKind.DeleteBody, reFeature.Kind);
        AssertDefinition(reFeature);
    }

    [Fact]
    public void AuthoredDeleteBody_SurvivesResave()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("delete_body_second"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddDeleteBody();
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        AssertDefinition(Assert.Single(reopened.Features.Where(f => f.Name == "DeleteBody")));
    }

    [Fact]
    public void DeleteBodyAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        var template = Template("Primary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("delete_body_second"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddDeleteBody(featureName: "MyDeleteBody"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);
        Assert.Throws<NotSupportedException>(() => part.AddDeleteBody());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void DeleteBodyAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("delete_body_second"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddDeleteBody());
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static DeleteBodyAuthoringTemplate Template(string variant) =>
        DeleteBodyAuthoringTemplates.All.Single(t =>
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/CombineSplitOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp, DeleteBodyAuthoringTemplate template)
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
        var definition = Assert.IsType<DeleteBodyFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moDeleteBody_c", definition.ClassName);
        Assert.Equal(0x57u, definition.SerializedTypeId);
        Assert.Equal(1, definition.BodiesCount);
        Assert.Equal(SelectionEntityType.Body, definition.Bodies![0].EntityType);
        Assert.True(definition.Bodies[0].EntityId > 0);
        Assert.Null(definition.KeepBodies);
        Assert.True(definition.Resolution.Bodies);
        Assert.False(definition.Resolution.KeepBodies);
    }
}
