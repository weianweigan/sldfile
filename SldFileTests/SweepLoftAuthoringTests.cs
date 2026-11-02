using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFileTests;

/// <summary>
/// Fixed measured-combination authoring tests for the 2026 sweep/loft samples.
/// The measured feature block must be re-emitted byte-for-byte on the exact stripped
/// base; selection references and geometry are never rewritten.
/// </summary>
public class SweepLoftAuthoringTests
{
    private const int MarkerDistance = 0x10;

    [Theory]
    [InlineData("sweep_circle10_path40", "SweepProfilePathPrimary")]
    [InlineData("sweep_circle10_path40_resave", "SweepProfilePathResave")]
    public void SweepAuthoring_ReEmitsMeasuredBlockVerbatim(string sampleName, string templateProperty)
    {
        var template = Template(templateProperty);
        using var temp = TestFileProvider.CreateTempCopy(SweepPath(sampleName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddSweep();
            byte[] authored = part.GetStreamBytes(streamPath)!;

            AssertMeasuredBlock(stripped, authored, template);
            AssertSweepDefinition(feature, template);
            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
            Assert.Equal(FeatureKind.Sweep, feature.Kind);
            AssertSweepDefinition(feature, template);
            var definition = Assert.IsType<SweepFeatureDefinitionData>(feature.Definition);
            Assert.Equal(new ushort[] { 0xC2, 0xCD }, definition.SelectionReferenceIds.ToArray());
            Assert.Equal(SketchCurveRole.Profile, definition.References[0].Role);
            Assert.Equal(SketchCurveRole.Path, definition.References[1].Role);
        }
    }

    [Theory]
    [InlineData("loft_circle10_20", "LoftTwoProfilesPrimary")]
    [InlineData("loft_circle10_20_resave", "LoftTwoProfilesResave")]
    public void LoftAuthoring_ReEmitsMeasuredBlockVerbatim(string sampleName, string templateProperty)
    {
        var template = Template(templateProperty);
        using var temp = TestFileProvider.CreateTempCopy(LoftPath(sampleName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddLoft();
            byte[] authored = part.GetStreamBytes(streamPath)!;

            AssertMeasuredBlock(stripped, authored, template);
            AssertLoftDefinition(feature, template);
            part.Save();
        }

        using (var reopened = new SldPart(temp.FilePath, readOnly: true))
        {
            var feature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
            Assert.Equal(FeatureKind.Loft, feature.Kind);
            AssertLoftDefinition(feature, template);
            var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);
            Assert.Equal(new ushort[] { 0xC5, 0xE1 }, definition.SelectionReferenceIds.ToArray());
            Assert.All(definition.References, reference => Assert.Equal(SketchCurveRole.Profile, reference.Role));
        }
    }

    [Theory]
    [InlineData("sweep_circle10_path40", "Primary", false, 0, 0.0)]
    [InlineData("sweep_circle10_path40_resave", "Resave", false, 0, 0.0)]
    [InlineData("sweep_circle10_path40", "Primary", true, 8, Math.PI / 2)]
    [InlineData("sweep_circle10_path40_resave", "Resave", true, 8, Math.PI / 2)]
    [InlineData("sweep_circle10_path40", "Primary", true, 1, Math.PI / 2)]
    [InlineData("sweep_circle10_path40_resave", "Resave", true, 1, Math.PI / 2)]
    [InlineData("sweep_circle10_path40", "Primary", true, 1, 0.0)]
    public void SweepOptions_AuthoringPatchesMergeAndTwistAndReReads(
        string sampleName, string variant, bool merge, int twistControlType, double twistAngle)
    {
        var template = Template("SweepProfilePath" + variant);
        using var temp = TestFileProvider.CreateTempCopy(SweepPath(sampleName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddSweep(
                merge: merge,
                twistControlType: twistControlType,
                twistAngleRad: twistAngle);
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
            var definition = Assert.IsType<SweepFeatureDefinitionData>(feature.Definition);
            Assert.Equal(merge, definition.Merge);
            Assert.Equal(twistControlType, definition.TwistControlType);
            Assert.Equal(twistAngle, definition.TwistAngleRad!.Value, 12);
            Assert.True(definition.Resolution.Merge);
            Assert.True(definition.Resolution.TwistControlType);
            Assert.True(definition.Resolution.TwistAngle);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        var reDefinition = Assert.IsType<SweepFeatureDefinitionData>(reFeature.Definition);
        Assert.Equal(merge, reDefinition.Merge);
        Assert.Equal(twistControlType, reDefinition.TwistControlType);
        Assert.Equal(twistAngle, reDefinition.TwistAngleRad!.Value, 12);
    }

    [Fact]
    public void SweepOptions_RejectsInvalidValues_WithoutChangingStream()
    {
        var template = Template("SweepProfilePathPrimary");
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        StripInPlace(temp, template);
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddSweep(twistControlType: 3));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddSweep(twistAngleRad: -1));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        Assert.Throws<ArgumentException>(() => part.AddSweep(twistControlType: 0, twistAngleRad: 1));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Theory]
    [InlineData("loft_centerline", "Primary")]
    [InlineData("loft_centerline_resave", "Resave")]
    public void LoftCenterline_AuthoringAppendsMeasuredBlockAndReReads(
        string sampleName, string variant)
    {
        var template = CenterlineTemplate(variant);
        using var temp = TestFileProvider.CreateTempCopy(LoftPath(sampleName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            var feature = part.AddLoft(
                profileSketchNames: template.ProfileNames,
                guideSketchName: template.GuideName,
                centerline: true);
            byte[] authored = part.GetStreamBytes(streamPath)!;
            Assert.Equal(stripped.Length + template.Block.Length, authored.Length);
            Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
            Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
            var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);
            Assert.Equal(0x49u, definition.Centerline!.EntityId);
            Assert.True(definition.Resolution.Centerline);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var reFeature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        var reDefinition = Assert.IsType<LoftFeatureDefinitionData>(reFeature.Definition);
        Assert.Equal(0x49u, reDefinition.Centerline!.EntityId);
        Assert.True(reDefinition.Resolution.Centerline);
    }

    [Fact]
    public void LoftCenterline_RejectsWrongBaseWithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(LoftPath("loft_circle10_20"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] before = (byte[])part.GetStreamBytes(streamPath)!.Clone();
        Assert.Throws<NotSupportedException>(() => part.AddLoft(centerline: true));
        Assert.Equal(before, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void AuthoredSweep_SurvivesResave()
    {
        var template = SweepLoftAuthoringTemplates.SweepProfilePathPrimary;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            part.AddSweep();
            part.Save();
        }

        using (var resave = new SldPart(temp.FilePath, readOnly: false))
        {
            resave.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        AssertSweepDefinition(feature, template);
    }

    [Fact]
    public void AddSweep_RejectsModifiedBase_WithoutChangingStream()
    {
        var template = SweepLoftAuthoringTemplates.SweepProfilePathPrimary;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;
        stripped[stripped.Length / 2] ^= 0x01;
        part.SetPendingStream(streamPath, stripped);

        Assert.Throws<NotSupportedException>(() => part.AddSweep());
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void AddSweep_RejectsCustomNameAndSketchNames_WithoutChangingStream()
    {
        var template = SweepLoftAuthoringTemplates.SweepProfilePathPrimary;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circle10_path40"));
        StripInPlace(temp, template);

        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] stripped = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddSweep(featureName: "MySweep"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));

        Assert.Throws<NotSupportedException>(() => part.AddSweep(profileSketchName: "OtherProfile"));
        Assert.Equal(stripped, part.GetStreamBytes(streamPath));
    }

    [Fact]
    public void AddLoft_RejectsNonStrippedSample_WithoutChangingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(LoftPath("loft_circle10_20"));
        using var part = new SldPart(temp.FilePath, readOnly: false);
        string streamPath = part.ResolvedFeaturesStreamPath!;
        byte[] original = part.GetStreamBytes(streamPath)!;

        Assert.Throws<NotSupportedException>(() => part.AddLoft());
        Assert.Equal(original, part.GetStreamBytes(streamPath));
    }

    [Theory]
    [InlineData("sweep_circular_profile12", "Primary", "Path", "BF", true, 0.012, false, 0.0)]
    [InlineData("sweep_circular_profile12", "Resave", "Path", "BF", true, 0.012, false, 0.0)]
    [InlineData("sweep_thin_one_dir_t1", "Primary", "Profile,Path", "C2,CD", false, 0.0, true, 0.001)]
    [InlineData("sweep_thin_one_dir_t1", "Resave", "Profile,Path", "C2,CD", false, 0.0, true, 0.001)]
    [InlineData("sweep_guide1", "Primary", "Profile,Path,Guide", "C5,DC,D1", false, 0.0, false, 0.0)]
    [InlineData("sweep_guide1", "Resave", "Profile,Path,Guide", "C5,DC,D1", false, 0.0, false, 0.0)]
    [InlineData("sweep_profile_rect", "Primary", "Profile,Path", "C2,DF", false, 0.0, false, 0.0)]
    [InlineData("sweep_profile_rect", "Resave", "Profile,Path", "C2,DF", false, 0.0, false, 0.0)]
    public void AdditionalSweepCombinations_AuthoringAndReRead(
        string sampleName, string variant, string roles, string ids,
        bool circularProfile, double diameter, bool thinFeature, double thickness)
    {
        var template = FindTemplate(sampleName, variant);
        string fileName = variant == "Resave" ? sampleName + "_resave" : sampleName;
        using var temp = TestFileProvider.CreateTempCopy(SweepPath(fileName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            part.AddSweep();
            AssertMeasuredBlock(stripped, part.GetStreamBytes(streamPath)!, template);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        var definition = Assert.IsType<SweepFeatureDefinitionData>(feature.Definition);
        Assert.Equal(ParseIds(ids), definition.SelectionReferenceIds.ToArray());
        Assert.Equal(roles.Split(','), definition.References.Select(r => r.Role.ToString()).ToArray());
        Assert.Equal(circularProfile, definition.CircularProfileLayout);
        Assert.Equal(thinFeature, definition.ThinFeatureLayout);
        if (circularProfile) Assert.Equal(diameter, definition.CircularProfileDiameter!.Value, 12);
        else Assert.Null(definition.CircularProfileDiameter);
        if (thinFeature) Assert.Equal(thickness, definition.ThinWallThickness!.Value, 12);
        else Assert.Null(definition.ThinWallThickness);
        Assert.Equal(roles.Contains("Guide"), definition.Resolution.GuideCurveReferences);
    }

    [Theory]
    [InlineData("loft_three_sections", "Primary", "Profile,Profile,Profile", "CB,E7,FB")]
    [InlineData("loft_three_sections", "Resave", "Profile,Profile,Profile", "CB,E7,FB")]
    [InlineData("loft_guide1", "Primary", "Profile,Profile,Guide", "D7,F2,C8")]
    [InlineData("loft_guide1", "Resave", "Profile,Profile,Guide", "D7,F2,C8")]
    [InlineData("loft_start_end_tangency", "Primary", "Profile,Profile", "C5,E1")]
    [InlineData("loft_start_end_tangency", "Resave", "Profile,Profile", "C5,E1")]
    public void AdditionalLoftCombinations_AuthoringAndReRead(
        string sampleName, string variant, string roles, string ids)
    {
        var template = FindTemplate(sampleName, variant);
        string fileName = variant == "Resave" ? sampleName + "_resave" : sampleName;
        using var temp = TestFileProvider.CreateTempCopy(LoftPath(fileName));
        StripInPlace(temp, template);

        using (var part = new SldPart(temp.FilePath, readOnly: false))
        {
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            part.AddLoft();
            AssertMeasuredBlock(stripped, part.GetStreamBytes(streamPath)!, template);
            part.Save();
        }

        using var reopened = new SldPart(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Name == template.Name));
        var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);
        Assert.Equal(ParseIds(ids), definition.SelectionReferenceIds.ToArray());
        Assert.Equal(roles.Split(','), definition.References.Select(r => r.Role.ToString()).ToArray());
        Assert.Equal(roles.Contains("Guide"), definition.Resolution.GuideCurves);
    }

    [Fact]
    public void AddSweep_RejectsMismatchedMeasuredSketchLayout()
    {
        var circular = FindTemplate("sweep_circular_profile12", "Primary");
        using (var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_circular_profile12")))
        {
            StripInPlace(temp, circular);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            Assert.Throws<NotSupportedException>(() => part.AddSweep(profileSketchName: "Profile1"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }

        var guided = FindTemplate("sweep_guide1", "Primary");
        using (var temp = TestFileProvider.CreateTempCopy(SweepPath("sweep_guide1")))
        {
            StripInPlace(temp, guided);
            using var part = new SldPart(temp.FilePath, readOnly: false);
            string streamPath = part.ResolvedFeaturesStreamPath!;
            byte[] stripped = part.GetStreamBytes(streamPath)!;
            Assert.Throws<NotSupportedException>(() => part.AddSweep(guideSketchName: "OtherGuide"));
            Assert.Equal(stripped, part.GetStreamBytes(streamPath));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static SweepLoftAuthoringTemplate Template(string propertyName) =>
        (SweepLoftAuthoringTemplate)typeof(SweepLoftAuthoringTemplates)
            .GetField(propertyName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;

    private static SweepLoftAuthoringTemplate FindTemplate(string name, string variant) =>
        SweepLoftAuthoringTemplates.All.Single(t =>
            string.Equals(t.Name, name, StringComparison.Ordinal) &&
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static SweepLoftAuthoringTemplate CenterlineTemplate(string variant) =>
        LoftCenterlineAuthoringTemplates.All.Single(t =>
            string.Equals(t.Variant, variant, StringComparison.Ordinal));

    private static ushort[] ParseIds(string ids) =>
        ids.Split(',').Select(value => Convert.ToUInt16(value, 16)).ToArray();

    private static string SweepPath(string name) =>
        TestFileProvider.Resolve("Features2026/SweepOptions", name + ".SLDPRT");

    private static string LoftPath(string name) =>
        TestFileProvider.Resolve("Features2026/LoftOptions", name + ".SLDPRT");

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
        for (int i = 0; i < stripped.Length; i++)
        {
            if (i == 4 || i == 5 || i == stripped.Length - MarkerDistance + 4 ||
                i == stripped.Length - MarkerDistance + 5 ||
                i == stripped.Length - MarkerDistance + 6 ||
                i == stripped.Length - MarkerDistance + 7)
                continue;
            Assert.Equal(stripped[i], authored[i]);
        }

        Assert.Equal(template.StrippedFolderCount + 1, BitConverter.ToUInt16(authored, 4));
        Assert.Equal(template.MarkerValue, BitConverter.ToInt32(authored, stripped.Length - MarkerDistance + 4));
        Assert.Equal(template.Block, authored.Skip(stripped.Length).ToArray());
    }

    private static void AssertSweepDefinition(Feature feature, SweepLoftAuthoringTemplate template)
    {
        Assert.Equal(template.Name, feature.Name);
        var definition = Assert.IsType<SweepFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moSweep_c", definition.ClassName);
        Assert.Equal(template.Name, definition.DisplayName);
        Assert.Equal(2, definition.References.Count);
        Assert.False(definition.CircularProfileLayout);
        Assert.False(definition.ThinFeatureLayout);
        Assert.Null(definition.CircularProfileDiameter);
        Assert.Null(definition.ThinWallThickness);
    }

    private static void AssertLoftDefinition(Feature feature, SweepLoftAuthoringTemplate template)
    {
        Assert.Equal(template.Name, feature.Name);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moBlend_c", definition.ClassName);
        Assert.Equal(template.Name, definition.DisplayName);
        Assert.Equal(2, definition.References.Count);
        Assert.False(definition.CircularProfileLayout);
        Assert.False(definition.ThinFeatureLayout);
        Assert.Null(definition.CircularProfileDiameter);
        Assert.Null(definition.ThinWallThickness);
    }
}
