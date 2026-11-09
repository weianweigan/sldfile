using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring tests for structural member features using strongly-typed MFC models.
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
        _ = variant;
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sample));
        StripInPlace(temp);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            var feature = selected
                ? part.AddStructuralMemberSelected(featureName)
                : part.AddStructuralMember(featureName);

            Assert.Equal(featureName, feature.Name);
            Assert.Equal(FeatureKind.StructuralMember, feature.Kind);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == featureName));
        Assert.Equal(FeatureKind.StructuralMember, reFeature.Kind);
        Assert.NotNull(reFeature.Model);
        Assert.Equal("moWeldMemberFeat_c", reFeature.Model.ClassName);
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsWrongVariantAndCustomName_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural2"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddStructuralMember());
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsNonStrippedBaseAndSecondMember_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        Assert.Throws<ArgumentNullException>(() => part.AddStructuralMember((StructuralMemberFeatureModel)null!));
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsTamperedBase_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddStructuralMemberSelected());
    }

    [Fact]
    public void StructuralMemberAuthoring_RejectsReadOnlyDocument()
    {
        using var temp = TestFileProvider.CreateTempCopy(Resolve("weldment_structural"));
        using var part = new SldPart(temp.FilePath, readOnly: true);
        Assert.Throws<InvalidOperationException>(() => part.AddStructuralMember());
        Assert.Throws<InvalidOperationException>(() => part.AddStructuralMemberSelected());
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/WeldmentOptions", name + ".SLDPRT");

    private static void StripInPlace(TempTestFile temp)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        var elem = MfcDocument.Parse(full).AllElements.FirstOrDefault(e => e.ClassName == "moWeldMemberFeat_c" && e.TagByte == 0x01);
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
