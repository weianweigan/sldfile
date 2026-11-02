using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured authoring tests for the 2026 surface sweep/loft samples. The complete
/// measured block is appended on the exact stripped base; no reference or option is rewritten.
/// </summary>
public class SurfaceSweepLoftAuthoringTests
{
    private const int MarkerDistance = 0x10;

    [Theory]
    [InlineData("surf_sweep_basic", "SurfaceSweepBasicPrimary", 0.006)]
    [InlineData("surf_sweep_basic_resave", "SurfaceSweepBasicResave", 0.006)]
    [InlineData("surf_sweep_path60", "SurfaceSweepPath60Primary", 0.006)]
    [InlineData("surf_sweep_path60_resave", "SurfaceSweepPath60Resave", 0.006)]
    [InlineData("surf_sweep_profile12", "SurfaceSweepProfile12Primary", 0.012)]
    [InlineData("surf_sweep_profile12_resave", "SurfaceSweepProfile12Resave", 0.012)]
    public void SurfaceSweepAuthoring_ReEmitsMeasuredBlockAndReReads(
        string sampleName, string templateField, double diameter)
    {
        var template = Template(templateField);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sampleName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddSurfaceSweep(pathSketchName: "Path1");
            byte[] authored = part.GetStreamBytes(streamPath)!;

            AssertMeasuredBlock(stripped, authored, template);
            AssertSweepDefinition(feature, template, diameter);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        Assert.Equal(FeatureKind.SurfaceSweep, reFeature.Kind);
        AssertSweepDefinition(reFeature, template, diameter);
    }

    [Theory]
    [InlineData("surf_loft_2sections_loft2", "SurfaceLoft2SectionsPrimary", "C5,E1", "Profile,Profile", 0x4D)]
    [InlineData("surf_loft_2sections_loft2_resave", "SurfaceLoft2SectionsResave", "C5,E1", "Profile,Profile", 0x4D)]
    [InlineData("surf_loft_3sections", "SurfaceLoft3SectionsPrimary", "CB,E7,FB", "Profile,Profile,Profile", 0x50)]
    [InlineData("surf_loft_3sections_resave", "SurfaceLoft3SectionsResave", "CB,E7,FB", "Profile,Profile,Profile", 0x50)]
    [InlineData("surf_loft_guide", "SurfaceLoftGuidePrimary", "C8,E4,EE", "Profile,Profile,Guide", 0x4E)]
    [InlineData("surf_loft_guide_resave", "SurfaceLoftGuideResave", "C8,E4,EE", "Profile,Profile,Guide", 0x4E)]
    public void SurfaceLoftAuthoring_ReEmitsMeasuredBlockAndReReads(
        string sampleName, string templateField, string ids, string roles, int typeId)
    {
        var template = Template(templateField);
        using var temp = TestFileProvider.CreateTempCopy(Resolve(sampleName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddSurfaceLoft(
                profileSketchNames: template.ProfileNames,
                guideSketchName: template.GuideName);
            byte[] authored = part.GetStreamBytes(streamPath)!;

            AssertMeasuredBlock(stripped, authored, template);
            AssertLoftDefinition(feature, template, ids, roles, typeId);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        Assert.Equal(FeatureKind.SurfaceLoft, reFeature.Kind);
        AssertLoftDefinition(reFeature, template, ids, roles, typeId);
    }

    [Fact]
    public void SurfaceAuthoring_RejectsCustomNameWrongSketchAndWrongVariant_WithoutChangingStream()
    {
        var sweep = Template("SurfaceSweepBasicPrimary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic")))
        {
            StripInPlace(temp, sweep);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceSweep(featureName: "MySweep"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceSweep(pathSketchName: "OtherPath"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceLoft());
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }

        var loft = Template("SurfaceLoft2SectionsPrimary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_loft_2sections_loft2")))
        {
            StripInPlace(temp, loft);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceLoft(featureName: "MyLoft"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceLoft(
                profileSketchNames: new[] { "Profile1" }));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceLoft(
                profileSketchNames: new[] { "Profile1", "Profile2" },
                guideSketchName: "OtherGuide"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));

            Assert.Throws<NotSupportedException>(() => part.AddSurfaceSweep());
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }
    }

    [Fact]
    public void SurfaceAuthoring_RejectsNonStrippedBaseAndSecondFeature_WithoutChangingStream()
    {
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic")))
        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] before = (byte[])part.GetStreamBytes(streamPath)!.Clone();
            Assert.Throws<NotSupportedException>(() => part.AddSurfaceSweep());
            Assert.Equal(before, part.GetStreamBytes(streamPath));
        }

        var template = Template("SurfaceSweepBasicPrimary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic")))
        {
            StripInPlace(temp, template);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            part.AddSurfaceSweep();
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Throws<NotSupportedException>(() => part.AddSurfaceSweep());
            Assert.Equal(authored, part.GetStreamBytes(streamPath));
        }
    }

    [Fact]
    public void SurfaceAuthoring_RejectsTamperedBaseAndReadOnly_WithoutChangingStream()
    {
        var template = Template("SurfaceSweepBasicPrimary");
        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic")))
        {
            StripInPlace(temp, template);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            stripped[stripped.Length / 2] ^= 0x01;
            part.SetPendingStream(streamPath, stripped);
            Assert.Throws<NotSupportedException>(() => part.AddSurfaceSweep());
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }

        using (var temp = TestFileProvider.CreateTempCopy(Resolve("surf_sweep_basic")))
        using (var part = new SldPart(temp.FilePath, readOnly: true))
        {
            Assert.Throws<InvalidOperationException>(() => part.AddSurfaceSweep());
            Assert.Throws<InvalidOperationException>(() => part.AddSurfaceLoft());
        }
    }

    private static void AssertSweepDefinition(
        Feature feature, SweepLoftAuthoringTemplate template, double diameter)
    {
        Assert.Equal(template.Name, feature.Name);
        var definition = Assert.IsType<SurfaceSweepFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moSweepRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Bu, definition.SerializedTypeId);
        Assert.True(definition.CircularProfileLayout);
        Assert.Equal(diameter, definition.CircularProfileDiameter!.Value, 12);
        Assert.Equal(62, definition.SweepType);
        Assert.True(definition.Merge);
        var path = Assert.IsType<SketchCurveReference>(definition.PathReference);
        Assert.Equal(SketchCurveRole.Path, path.Role);
        Assert.Equal(0x49u, path.EntityId);
        Assert.Equal(0xBF, path.HeaderSelectionId);
        Assert.Equal(new ushort[] { 0xBF }, definition.SelectionReferenceIds.ToArray());
        Assert.True(definition.Resolution.PathReference);
    }

    private static void AssertLoftDefinition(
        Feature feature, SweepLoftAuthoringTemplate template, string ids, string roles, int typeId)
    {
        Assert.Equal(template.Name, feature.Name);
        var definition = Assert.IsType<SurfaceLoftFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moBlendRefSurface_c", definition.ClassName);
        Assert.Equal((uint)typeId, definition.SerializedTypeId);
        Assert.Equal(
            ids.Split(',').Select(value => Convert.ToUInt16(value, 16)).ToArray(),
            definition.SelectionReferenceIds.ToArray());
        Assert.Equal(roles.Split(','), definition.References.Select(r => r.Role.ToString()).ToArray());
        Assert.Equal(definition.SelectionReferenceIds.Count, definition.ReferenceCount);
        Assert.True(definition.Resolution.ProfileReferences);
    }

    private static SweepLoftAuthoringTemplate Template(string fieldName) =>
        (SweepLoftAuthoringTemplate)typeof(SurfaceSweepLoftAuthoringTemplates)
            .GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

    private static void StripInPlace(TempTestFile temp, SweepLoftAuthoringTemplate template)
    {
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] full = part.GetStreamBytes(streamPath)!;
        byte[] stripped = new byte[template.BaseLength];
        Array.Copy(full, 0, stripped, 0, stripped.Length);
        BitConverter.GetBytes((ushort)template.StrippedFolderCount).CopyTo(stripped, 4);
        int marker = template.BaseLength - MarkerDistance;
        BitConverter.GetBytes(0u).CopyTo(stripped, marker + 4);
        part.SetPendingStream(streamPath, stripped);
        part.Save();
    }

    private static void AssertMeasuredBlock(
        byte[] stripped, byte[] authored, SweepLoftAuthoringTemplate template)
    {
        Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
        int markerValueOffset = stripped.Length - MarkerDistance + 4;
        for (int i = 0; i < stripped.Length; i++)
        {
            if (i == 4 || i == 5 || (i >= markerValueOffset && i < markerValueOffset + 4))
                continue;
            Assert.Equal(stripped[i], authored[i]);
        }

        Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
        Assert.Equal(template.MarkerValue, BitConverter.ToInt32(authored, markerValueOffset));
        Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");
}
