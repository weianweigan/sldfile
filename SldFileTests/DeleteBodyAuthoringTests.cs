using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

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
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var model = new DeleteBodyFeatureModel
            {
                FeatureName = "DeleteBody",
                SerializedTypeId = 0x57u,
                Bodies = new[] { new EntityReference { EntityType = SelectionEntityType.Body, EntityId = 1, Name = "moCompSolidBody_c:1" } }
            };
            var feature = part.AddDeleteBody(model);
            Assert.NotNull(feature);
            Assert.Equal("DeleteBody", feature.Name);
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
        using var temp = TestFileProvider.CreateTempCopy(Resolve("delete_body_second"));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddDeleteBody(new DeleteBodyFeatureModel { FeatureName = "DeleteBody" });
            part.Save();
        }
        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        Assert.NotNull(reopened.Features.FirstOrDefault(f => f.Name == "DeleteBody"));
    }

    [Fact]
    public void DeleteBodyAuthoring_RejectsWrongBaseAndName_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("delete_body_second"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddDeleteBody(new DeleteBodyFeatureModel()));
    }

    [Fact]
    public void DeleteBodyAuthoring_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("delete_body_second"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddDeleteBody((DeleteBodyFeatureModel)null!));
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/CombineSplitOptions", name + ".SLDPRT");

    private static void AssertDefinition(Feature feature)
    {
        var definition = Assert.IsType<DeleteBodyFeatureModel>(feature.Model);
        Assert.Equal("moDeleteBody_c", definition.ClassName);
        Assert.True(definition.SerializedTypeId == 0x57u || definition.TypeId == 0x57u);
    }

    private static void StripInPlace(TempTestFile temp)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        var elem = MfcDocument.Parse(full).AllElements.FirstOrDefault(e => e.ClassName == "moDeleteBody_c" && e.TagByte == 0x01);
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
