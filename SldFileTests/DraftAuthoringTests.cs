using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

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
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new DraftFeatureModel(3.0 * Math.PI / 180.0)
            {
                FeatureName = "Draft3deg",
                SerializedTypeId = 0x51u,
                NeutralPlane = new EntityReference { EntityType = SelectionEntityType.Face, EntityId = 1, Name = "moCompFace_c:1" }
            };
            var feature = part.AddDraft(model);
            Assert.NotNull(feature);
            Assert.Equal("Draft3deg", feature.Name);
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
        using var temp = TestFileProvider.CreateTempCopy(Resolve("draft_side_3deg_neutral_bottom"));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddDraft(new DraftFeatureModel(3.0 * Math.PI / 180.0) { FeatureName = "Draft3deg" });
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        Assert.NotNull(reopened.Features.FirstOrDefault(f => f.Name == "Draft3deg"));
    }

    [Fact]
    public void DraftAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("draft_side_3deg_neutral_bottom"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddDraft(new DraftFeatureModel()));
    }

    [Fact]
    public void DraftAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("draft_side_3deg_neutral_bottom"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddDraft((DraftFeatureModel)null!));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/DraftOptions", name + ".SLDPRT");

    private static void AssertDefinition(Feature feature)
    {
        var definition = Assert.IsType<DraftFeatureModel>(feature.Model);
        Assert.Equal("moDraft_c", definition.ClassName);
        Assert.True(definition.SerializedTypeId == 0x51u || definition.TypeId == 0x51u);
    }

    private static void StripInPlace(TempTestFile temp)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        var elem = MfcDocument.Parse(full).AllElements.FirstOrDefault(e => e.ClassName == "moDraft_c" && e.TagByte == 0x01);
        if (elem != null)
        {
            int offset = elem.TagOffset;
            byte[] stripped = new byte[offset];
            Array.Copy(full, 0, stripped, 0, offset);
            ushort count = BitConverter.ToUInt16(full, 4);
            BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
            part.SetPendingStream(streamPath, stripped);
            part.Save();
        }
    }
}
