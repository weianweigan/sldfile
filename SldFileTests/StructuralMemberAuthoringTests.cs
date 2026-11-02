using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured structural-member authoring tests. The measured block is re-emitted
/// verbatim; only the folder count and feature presence change.
/// </summary>
public class StructuralMemberAuthoringTests
{
    [Theory]
    [InlineData("weldment_structural", "PlainPrimary", false, "StructuralMember")]
    [InlineData("weldment_structural_resave", "PlainResave", false, "StructuralMember")]
    [InlineData("weldment_structural2", "SelectedPrimary", true, "StructuralMemberSelected")]
    [InlineData("weldment_structural2_resave", "SelectedResave", true, "StructuralMemberSelected")]
    public void StructuralMemberAuthoring_ReEmitsMeasuredBlockAndReReads(
        string sample, string variant, bool selected, string featureName)
    {
        var template = FindTemplate(variant);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = selected
                ? part.AddStructuralMemberSelected()
                : part.AddStructuralMember();
            byte[] authored = part.GetStreamBytes(streamPath)!;

            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            Assert.Equal(stripped.Take(4), authored.Take(4));
            Assert.Equal(stripped.Skip(6), authored.Skip(6).Take(stripped.Length - 6));
            Assert.Equal(template.Block, authored.Skip(stripped.Length));
            Assert.Equal((ushort)(template.StrippedFolderCount + 1),
                BitConverter.ToUInt16(authored, 4));

            Assert.Equal(featureName, feature.Name);
            Assert.Equal(FeatureKind.StructuralMember, feature.Kind);
            AssertStructuralMember(Assert.IsType<StructuralMemberFeatureDefinitionData>(feature.Definition));
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        Assert.Equal(FeatureKind.StructuralMember, reFeature.Kind);
        AssertStructuralMember(Assert.IsType<StructuralMemberFeatureDefinitionData>(reFeature.Definition));
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsWrongVariantAndCustomName_WithoutChangingStream()
    {
        var template = FindTemplate("SelectedPrimary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural2"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddStructuralMember());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<NotSupportedException>(() =>
            part.AddStructuralMemberSelected(featureName: "MyMember"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<NotSupportedException>(() =>
            part.AddStructuralMember(featureName: "StructuralMember"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsNonStrippedBaseAndSecondMember_WithoutChangingStream()
    {
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural")))
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] full = part.GetStreamBytes(streamPath)!;
            byte[] before = (byte[])full.Clone();
            Assert.Throws<NotSupportedException>(() => part.AddStructuralMember());
            Assert.Equal(before, part.GetStreamBytes(streamPath));
        }

        var template = FindTemplate("PlainPrimary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural")))
        {
            StripInPlace(temp, template);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            part.AddStructuralMember();
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Throws<NotSupportedException>(() => part.AddStructuralMember());
            Assert.Equal(authored, part.GetStreamBytes(streamPath));
        }
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsTamperedBase_WithoutChangingStream()
    {
        var template = FindTemplate("PlainPrimary");
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;
        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);

        Assert.Throws<NotSupportedException>(() => part.AddStructuralMember());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsReadOnlyDocument()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddStructuralMember());
        Assert.Throws<InvalidOperationException>(() => part.AddStructuralMemberSelected());
    }

    private static void AssertStructuralMember(StructuralMemberFeatureDefinitionData definition)
    {
        Assert.Equal("moWeldMemberFeat_c", definition.ClassName);
        Assert.Equal(0x4Du, definition.SerializedTypeId);
        Assert.Equal(
            @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\weldment profiles\ansi\al tube square.sldlfp",
            definition.WeldmentProfilePath);
        var segment = Assert.Single(definition.PathSegments!);
        Assert.Equal(0x49u, segment.EntityId);
        Assert.Equal(0x2711u, definition.LocateProfilePoint!.EntityId);
        Assert.True(definition.Resolution.WeldmentProfilePath);
        Assert.True(definition.Resolution.PathSegments);
        Assert.True(definition.Resolution.LocateProfilePoint);
    }

    private static StructuralMemberAuthoringTemplate FindTemplate(string variant) =>
        StructuralMemberAuthoringTemplates.All.Single(t => t.Variant == variant);

    private static void StripInPlace(TempTestFile temp, StructuralMemberAuthoringTemplate template)
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

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/WeldmentOptions", name + ".SLDPRT");
}
