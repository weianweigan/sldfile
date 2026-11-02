using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading tests for the Phase 3.1 native Sweep/Loft samples. The selection layer
/// (sketch references with roles, circular-profile diameter, thin-wall thickness)
/// is decoded by SweepLoftSelectionDecoder; remaining geometry/options stay unparsed.
/// </summary>
public class SweepLoftFeatureReadingTests
{
    [Theory]
    [InlineData("sweep_circle10_path40", 0x4Cu, 2, "C2,CD", "Profile,Path", "49,4A", false, false)]
    [InlineData("sweep_circle10_path60", 0x4Cu, 2, "C2,CD", "Profile,Path", "49,4A", false, false)]
    [InlineData("sweep_circular_profile12", 0x4Bu, 1, "BF", "Path", "49", true, false)]
    [InlineData("sweep_profile_rect", 0x4Cu, 2, "C2,DF", "Profile,Path", "", false, false)]
    [InlineData("sweep_guide1", 0x4Du, 3, "C5,DC,D1", "Profile,Path,Guide", "49,4B,4A", false, false)]
    [InlineData("sweep_thin_one_dir_t1", 0x4Cu, 2, "C2,CD", "Profile,Path", "", false, true)]
    public void SweepSample_ReadsSelectionHeaderAndReferences(
        string name, uint typeCode, int selectionCount, string ids, string roles,
        string exactSketchIds, bool circularProfile, bool thinFeature)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Features2026/SweepOptions", name + ".SLDPRT"), readOnly: true);
        var feature = FindFeature(file, name);
        var definition = Assert.IsType<SweepFeatureDefinitionData>(feature.Definition);

        Assert.Equal("moSweep_c", definition.ClassName);
        Assert.Equal(name, definition.DisplayName);
        Assert.Equal(typeCode, definition.SerializedTypeId);
        Assert.Equal(typeCode, definition.TypeId);
        Assert.Equal(selectionCount, definition.SelectionCount);
        Assert.Equal(ParseIds(ids), definition.SelectionReferenceIds.ToArray());

        AssertReferences(definition.References, definition.SelectionReferenceIds, roles, exactSketchIds);
        Assert.True(definition.IsHeaderSelectionIdMappingPositional);
        Assert.Equal(circularProfile, definition.CircularProfileLayout);
        Assert.Equal(thinFeature, definition.ThinFeatureLayout);

        Assert.True(definition.Resolution.DisplayName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.SelectionReferenceList);
        Assert.True(definition.Resolution.ProfileAndPathReferences);
        Assert.Equal(roles.Contains("Guide"), definition.Resolution.GuideCurveReferences);
        Assert.Equal(thinFeature, definition.Resolution.ThinFeature);
        Assert.Equal(circularProfile, definition.Resolution.CircularProfile);
        Assert.False(definition.Resolution.MergeAndScope);
        Assert.False(definition.Resolution.SweepParameters);
    }

    [Theory]
    [InlineData("loft_circle10_20", 0x4Du, 2, "C5,E1", "Profile,Profile", "49,4C")]
    [InlineData("loft_three_sections", 0x50u, 3, "CB,E7,FB", "Profile,Profile,Profile", "49,4C,4F")]
    [InlineData("loft_guide1", 0x4Eu, 3, "D7,F2,C8", "Profile,Profile,Guide", "4A,4D,49")]
    [InlineData("loft_start_end_tangency", 0x4Du, 2, "C5,E1", "Profile,Profile", "49,4C")]
    public void LoftSample_ReadsSelectionHeaderAndReferences(
        string name, uint typeCode, int selectionCount, string ids, string roles, string exactSketchIds)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Features2026/LoftOptions", name + ".SLDPRT"), readOnly: true);
        var feature = FindFeature(file, name);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);

        // The serialized class observed in samples is moBlend_c, not moLoft_c.
        Assert.Equal("moBlend_c", definition.ClassName);
        Assert.Equal(name, definition.DisplayName);
        Assert.Equal(typeCode, definition.SerializedTypeId);
        Assert.Equal(typeCode, definition.TypeId);
        Assert.Equal(selectionCount, definition.SelectionCount);
        Assert.Equal(ParseIds(ids), definition.SelectionReferenceIds.ToArray());

        AssertReferences(definition.References, definition.SelectionReferenceIds, roles, exactSketchIds);
        Assert.True(definition.IsHeaderSelectionIdMappingPositional);
        Assert.False(definition.CircularProfileLayout);
        Assert.False(definition.ThinFeatureLayout);
        Assert.Null(definition.CircularProfileDiameter);
        Assert.Null(definition.ThinWallThickness);

        Assert.True(definition.Resolution.DisplayName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.SelectionReferenceList);
        Assert.True(definition.Resolution.Profiles);
        Assert.Equal(roles.Contains("Guide"), definition.Resolution.GuideCurves);
        Assert.False(definition.Resolution.Centerline);
        Assert.False(definition.Resolution.StartEndConstraints);
        Assert.False(definition.Resolution.ThinFeature);
        Assert.False(definition.Resolution.CircularProfile);
        Assert.False(definition.Resolution.MergeAndScope);
        Assert.False(definition.Resolution.LoftParameters);
    }

    [Theory]
    [InlineData("loft_circle10_20", "loft_circle10_20", 0, 0)]
    [InlineData("loft_circle10_20_resave", "loft_circle10_20", 0, 0)]
    [InlineData("loft_start_end_tangency", "loft_start_end_tangency", 1, 1)]
    [InlineData("loft_start_end_tangency_resave", "loft_start_end_tangency", 1, 1)]
    public void LoftTangencyTypes_DecodeFromSingleVariableVariant(
        string fileName, string featureName, int start, int end)
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/LoftOptions", fileName + ".SLDPRT"), readOnly: true);
        var feature = FindFeature(file, featureName);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);
        Assert.Equal(start, definition.StartTangencyType);
        Assert.Equal(end, definition.EndTangencyType);
        Assert.True(definition.Resolution.StartTangencyType);
        Assert.True(definition.Resolution.EndTangencyType);
    }

    [Theory]
    [InlineData("loft_three_sections")]
    [InlineData("loft_guide1")]
    public void LoftTangencyTypes_StayUnresolvedForOtherLayouts(string name)
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/LoftOptions", name + ".SLDPRT"), readOnly: true);
        var feature = FindFeature(file, name);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(feature.Definition);
        Assert.Null(definition.StartTangencyType);
        Assert.Null(definition.EndTangencyType);
        Assert.False(definition.Resolution.StartTangencyType);
        Assert.False(definition.Resolution.EndTangencyType);
    }

    [Theory]
    [InlineData("modern_sweep_rect_baseline", "modern_sweep_rect_baseline", true, 0, 0.0)]
    [InlineData("modern_sweep_merge_false", "modern_sweep_merge_false", false, 0, 0.0)]
    [InlineData("modern_sweep_merge_false_resave", "modern_sweep_merge_false", false, 0, 0.0)]
    [InlineData("modern_sweep_twist1_90", "modern_sweep_twist1_90", true, 1, Math.PI / 2)]
    [InlineData("modern_sweep_twist1_90_resave", "modern_sweep_twist1_90", true, 1, Math.PI / 2)]
    [InlineData("modern_sweep_twist8_90", "modern_sweep_twist8_90", true, 8, Math.PI / 2)]
    [InlineData("modern_sweep_twist8_90_resave", "modern_sweep_twist8_90", true, 8, Math.PI / 2)]
    public void ModernSweepOptionSamples_DecodeMergeAndTwist(
        string fileName, string featureName, bool merge, int twistControlType, double twistAngle)
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/SweepOptions", fileName + ".SLDPRT"), readOnly: true);
        var definition = Assert.IsType<SweepFeatureDefinitionData>(FindFeature(file, featureName).Definition);
        Assert.Equal(merge, definition.Merge);
        Assert.Equal(twistControlType, definition.TwistControlType);
        Assert.Equal(twistAngle, definition.TwistAngleRad!.Value, 12);
        Assert.True(definition.Resolution.Merge);
        Assert.True(definition.Resolution.TwistControlType);
        Assert.True(definition.Resolution.TwistAngle);
        // PathAlignmentType is still blocked; no guessed public field/flag.
        Assert.False(definition.Resolution.SweepParameters);
    }

    [Theory]
    [InlineData("modern_loft_3sections_baseline", "modern_loft_3sections_baseline", 3)]
    [InlineData("modern_loft_3sections_baseline_resave", "modern_loft_3sections_baseline", 3)]
    [InlineData("modern_loft_pick_points3", "modern_loft_pick_points3", 3)]
    [InlineData("modern_loft_pick_points3_resave", "modern_loft_pick_points3", 3)]
    public void ModernLoftOptionSamples_ParseProfilesWithoutGuessingCloseOrPickPoints(
        string fileName, string featureName, int profileCount)
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/SweepOptions", fileName + ".SLDPRT"), readOnly: true);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(FindFeature(file, featureName).Definition);
        Assert.Equal(profileCount, definition.SelectionCount);
        Assert.All(definition.References, r => Assert.Equal(SketchCurveRole.Profile, r.Role));
        Assert.Null(definition.StartTangencyType);
        Assert.Null(definition.EndTangencyType);
        Assert.Null(definition.Centerline);
        Assert.False(definition.Resolution.StartTangencyType);
        Assert.False(definition.Resolution.EndTangencyType);
        Assert.False(definition.Resolution.Centerline);
        // Close/custom PickPoints are not modeled or promoted.
        Assert.False(definition.Resolution.LoftParameters);
    }

    [Theory]
    [InlineData("sweep_merge_false", "sweep_merge_false", false, 0, 0.0)]
    [InlineData("sweep_merge_false_resave", "sweep_merge_false", false, 0, 0.0)]
    [InlineData("sweep_twist90", "sweep_twist90", true, 8, Math.PI / 2)]
    [InlineData("sweep_twist90_resave", "sweep_twist90", true, 8, Math.PI / 2)]
    [InlineData("sweep_circle10_path40", "sweep_circle10_path40", true, 0, 0.0)]
    public void SweepOptions_DecodeMergeTwistControlAndAngle(
        string fileName, string featureName, bool merge, int twistControlType, double twistAngle)
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/SweepOptions", fileName + ".SLDPRT"), readOnly: true);
        var definition = Assert.IsType<SweepFeatureDefinitionData>(FindFeature(file, featureName).Definition);
        Assert.Equal(merge, definition.Merge);
        Assert.Equal(twistControlType, definition.TwistControlType);
        Assert.Equal(twistAngle, definition.TwistAngleRad!.Value, 12);
        Assert.True(definition.Resolution.Merge);
        Assert.True(definition.Resolution.TwistControlType);
        Assert.True(definition.Resolution.TwistAngle);
    }

    [Theory]
    [InlineData("loft_centerline", "loft_centerline")]
    [InlineData("loft_centerline_resave", "loft_centerline")]
    public void LoftCenterline_DecodesSecondReference(string fileName, string featureName)
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/LoftOptions", fileName + ".SLDPRT"), readOnly: true);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(FindFeature(file, featureName).Definition);
        var centerline = Assert.IsType<SketchCurveReference>(definition.Centerline);
        Assert.Equal(SketchCurveRole.Centerline, centerline.Role);
        Assert.Equal(0x49u, centerline.EntityId);
        Assert.True(definition.Resolution.Centerline);
    }

    [Fact]
    public void LoftGuideWithoutCenterline_KeepsCenterlineUnresolved()
    {
        using var file = SwFile.Open(
            TestFileProvider.Resolve("Features2026/LoftOptions", "loft_guide1.SLDPRT"), readOnly: true);
        var definition = Assert.IsType<LoftFeatureDefinitionData>(FindFeature(file, "loft_guide1").Definition);
        Assert.Null(definition.Centerline);
        Assert.False(definition.Resolution.Centerline);
    }

    [Fact]
    public void SweepCircularProfile_DecodesDiameter()
    {
        var definition = ReadSweep("sweep_circular_profile12", "sweep_circular_profile12");
        Assert.True(definition.CircularProfileLayout);
        Assert.Equal(0.012, definition.CircularProfileDiameter!.Value, 12);
        Assert.False(definition.ThinFeatureLayout);
        Assert.Null(definition.ThinWallThickness);
        Assert.Equal(SketchCurveRole.Path, Assert.Single(definition.References).Role);
    }

    [Fact]
    public void SweepThinFeature_DecodesWallThickness()
    {
        var definition = ReadSweep("sweep_thin_one_dir_t1", "sweep_thin_one_dir_t1");
        Assert.True(definition.ThinFeatureLayout);
        Assert.Equal(0.001, definition.ThinWallThickness!.Value, 12);
        Assert.False(definition.CircularProfileLayout);
        Assert.Null(definition.CircularProfileDiameter);
        Assert.Equal(new[] { SketchCurveRole.Profile, SketchCurveRole.Path },
            definition.References.Select(r => r.Role).ToArray());
    }

    [Theory]
    [InlineData("sweep_circle10_path40")]
    [InlineData("sweep_circle10_path60")]
    [InlineData("sweep_circular_profile12")]
    [InlineData("sweep_profile_rect")]
    [InlineData("sweep_guide1")]
    [InlineData("sweep_thin_one_dir_t1")]
    public void SweepResave_KeepsSelectionHeaderStable(string name)
    {
        var primary = ReadSweep(name, name);
        var resave = ReadSweep(name + "_resave", name);

        Assert.Equal(primary.ClassName, resave.ClassName);
        Assert.Equal(primary.DisplayName, resave.DisplayName);
        Assert.Equal(primary.SerializedTypeId, resave.SerializedTypeId);
        Assert.Equal(primary.SelectionReferenceIds.ToArray(), resave.SelectionReferenceIds.ToArray());
        Assert.Equal(ProjectReferences(primary.References), ProjectReferences(resave.References));
        Assert.Equal(primary.IsHeaderSelectionIdMappingPositional, resave.IsHeaderSelectionIdMappingPositional);
        Assert.Equal(primary.CircularProfileLayout, resave.CircularProfileLayout);
        Assert.Equal(primary.CircularProfileDiameter, resave.CircularProfileDiameter);
        Assert.Equal(primary.ThinFeatureLayout, resave.ThinFeatureLayout);
        Assert.Equal(primary.ThinWallThickness, resave.ThinWallThickness);
    }

    [Theory]
    [InlineData("loft_circle10_20")]
    [InlineData("loft_three_sections")]
    [InlineData("loft_guide1")]
    [InlineData("loft_start_end_tangency")]
    public void LoftResave_KeepsSelectionHeaderStable(string name)
    {
        var primary = ReadLoft(name, name);
        var resave = ReadLoft(name + "_resave", name);

        Assert.Equal(primary.ClassName, resave.ClassName);
        Assert.Equal(primary.DisplayName, resave.DisplayName);
        Assert.Equal(primary.SerializedTypeId, resave.SerializedTypeId);
        Assert.Equal(primary.SelectionReferenceIds.ToArray(), resave.SelectionReferenceIds.ToArray());
        Assert.Equal(ProjectReferences(primary.References), ProjectReferences(resave.References));
        Assert.Equal(primary.IsHeaderSelectionIdMappingPositional, resave.IsHeaderSelectionIdMappingPositional);
        Assert.Equal(primary.CircularProfileLayout, resave.CircularProfileLayout);
        Assert.Equal(primary.CircularProfileDiameter, resave.CircularProfileDiameter);
        Assert.Equal(primary.ThinFeatureLayout, resave.ThinFeatureLayout);
        Assert.Equal(primary.ThinWallThickness, resave.ThinWallThickness);
    }

    [Theory]
    [InlineData("moSweep_c")]
    [InlineData("moBlend_c")]
    public void TruncatedHeader_IsRecognizedButNotParsed(string className)
    {
        byte[] stream = BuildResolvedFeaturesStream(className, "BadFeature", headerBytes: new byte[10]);

        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal(className, folder.ClassName);
        Assert.Null(folder.FeatureData);
    }

    [Theory]
    [InlineData("moSweep_c")]
    [InlineData("moBlend_c")]
    public void UnrecognizedHeaderLayout_IsNotParsed(string className)
    {
        // Full-length but structurally different header: no empty-WChar marker,
        // non-zero reserved bytes, and an out-of-range type code.
        var header = new byte[64];
        header[8] = 0x10; // type code below the observed 0x4B..0x50 range

        byte[] stream = BuildResolvedFeaturesStream(className, "BadFeature", header);

        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal(className, folder.ClassName);
        Assert.Null(folder.FeatureData);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static SweepFeatureDefinitionData ReadSweep(string fileName, string featureName)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Features2026/SweepOptions", fileName + ".SLDPRT"), readOnly: true);
        return Assert.IsType<SweepFeatureDefinitionData>(FindFeature(file, featureName).Definition);
    }

    private static LoftFeatureDefinitionData ReadLoft(string fileName, string featureName)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve("Features2026/LoftOptions", fileName + ".SLDPRT"), readOnly: true);
        return Assert.IsType<LoftFeatureDefinitionData>(FindFeature(file, featureName).Definition);
    }

    private static Feature FindFeature(SwFile file, string name)
    {
        var feature = Flatten(file.Features).FirstOrDefault(f => f.Name == name);
        Assert.NotNull(feature);
        return feature!;
    }

    private static IEnumerable<Feature> Flatten(IEnumerable<Feature> features)
    {
        foreach (var feature in features)
        {
            yield return feature;
            foreach (var child in Flatten(feature.Children))
                yield return child;
        }
    }

    private static ushort[] ParseIds(string ids) =>
        ids.Split(',').Select(value => Convert.ToUInt16(value, 16)).ToArray();

    private static void AssertReferences(
        IReadOnlyList<SketchCurveReference> references,
        IReadOnlyList<ushort> headerIds,
        string roles,
        string exactSketchIds)
    {
        string[] roleNames = roles.Split(',');
        Assert.Equal(roleNames.Length, references.Count);
        Assert.Equal(headerIds.Count, references.Count);
        ushort[] exactIds = string.IsNullOrEmpty(exactSketchIds) ? Array.Empty<ushort>() : ParseIds(exactSketchIds);

        for (int i = 0; i < references.Count; i++)
        {
            var reference = references[i];
            Assert.Equal(roleNames[i], reference.Role.ToString());
            Assert.True(reference.IsValid);
            Assert.True(reference.IsHeaderSelectionIdPositional);
            Assert.Equal(headerIds[i], reference.HeaderSelectionId);
            Assert.True(reference.EntityId > 0);
            if (exactIds.Length > 0)
                Assert.Equal(exactIds[i], reference.EntityId);
        }
    }

    private static (SketchCurveRole Role, uint EntityId, uint ArchiveSerial, ushort HeaderSelectionId, bool Positional)[]
        ProjectReferences(IReadOnlyList<SketchCurveReference> references) =>
        references.Select(r => (r.Role, r.EntityId, r.ArchiveSerial, r.HeaderSelectionId, r.IsHeaderSelectionIdPositional)).ToArray();

    private static byte[] BuildResolvedFeaturesStream(string className, string displayName, byte[] headerBytes)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0x0F, 0x00 }; // Part stream version
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);

        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, (byte)displayName.Length });
        bytes.AddRange(Encoding.Unicode.GetBytes(displayName));

        bytes.AddRange(headerBytes);
        bytes.AddRange(new byte[32]);
        return bytes.ToArray();
    }
}
