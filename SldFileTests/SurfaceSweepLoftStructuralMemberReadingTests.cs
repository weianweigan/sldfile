using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading tests for the measured 2026 surface sweep, surface loft and structural
/// member layouts. Surface nodes are distinct from their solid counterparts.
/// </summary>
public class SurfaceSweepLoftStructuralMemberReadingTests
{
    [Theory]
    [InlineData("surf_sweep_basic", 0.006)]
    [InlineData("surf_sweep_basic_resave", 0.006)]
    [InlineData("surf_sweep_path60", 0.006)]
    [InlineData("surf_sweep_path60_resave", 0.006)]
    [InlineData("surf_sweep_profile12", 0.012)]
    [InlineData("surf_sweep_profile12_resave", 0.012)]
    public void SurfaceSweep_DecodesCircularProfileAndSelection(string name, double diameter)
    {
        using var file = SwFile.Open(ResolveSurface(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is SurfaceSweepFeatureDefinitionData));
        Assert.Equal(FeatureKind.SurfaceSweep, feature.Kind);

        var definition = Assert.IsType<SurfaceSweepFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moSweepRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Bu, definition.SerializedTypeId);
        Assert.True(definition.CircularProfileLayout);
        Assert.Equal(diameter, definition.CircularProfileDiameter!.Value, 12);
        Assert.Equal(1, definition.SelectionCount);
        Assert.Equal(0xBF, Assert.Single(definition.SelectionReferenceIds));
        var path = Assert.IsType<SketchCurveReference>(definition.PathReference);
        Assert.Equal(SketchCurveRole.Path, path.Role);
        Assert.Equal(0x49u, path.EntityId);
        Assert.Equal(0xBF, path.HeaderSelectionId);
        Assert.True(path.IsHeaderSelectionIdPositional);
        Assert.Equal(62, definition.SweepType);
        Assert.True(definition.Merge);
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.SelectionReferenceList);
        Assert.True(definition.Resolution.CircularProfile);
        Assert.True(definition.Resolution.PathReference);
        Assert.True(definition.Resolution.SweepType);
        Assert.True(definition.Resolution.Merge);
        Assert.Empty(file.Features.Where(f => f.Definition is SweepFeatureDefinitionData));
    }

    [Theory]
    [InlineData("surf_loft_2sections")]
    [InlineData("surf_loft_2sections_resave")]
    [InlineData("surf_loft_2sections_loft2")]
    [InlineData("surf_loft_2sections_loft2_resave")]
    public void SurfaceLoft_DecodesTwoProfileSelections(string name)
    {
        using var file = SwFile.Open(ResolveSurface(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is SurfaceLoftFeatureDefinitionData));
        Assert.Equal(FeatureKind.SurfaceLoft, feature.Kind);

        var definition = Assert.IsType<SurfaceLoftFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moBlendRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Du, definition.SerializedTypeId);
        Assert.Equal(2, definition.SelectionCount);
        Assert.Equal(new ushort[] { 0xC5, 0xE1 }, definition.SelectionReferenceIds);
        Assert.Equal(2, definition.ReferenceCount);
        Assert.Equal(new uint[] { 0x49, 0x4C },
            definition.References.Select(r => r.EntityId).ToArray());
        Assert.All(definition.References, r => Assert.Equal(SketchCurveRole.Profile, r.Role));
        Assert.Equal(new ushort[] { 0xC5, 0xE1 },
            definition.References.Select(r => r.HeaderSelectionId).ToArray());
        Assert.All(definition.References, r => Assert.True(r.IsHeaderSelectionIdPositional));
        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.SelectionReferenceList);
        Assert.True(definition.Resolution.ProfileReferences);
        Assert.Empty(file.Features.Where(f => f.Definition is LoftFeatureDefinitionData));
    }

    [Theory]
    [InlineData("surf_loft_3sections", 0x50u, "CB,E7,FB", "Profile,Profile,Profile")]
    [InlineData("surf_loft_3sections_resave", 0x50u, "CB,E7,FB", "Profile,Profile,Profile")]
    [InlineData("surf_loft_guide", 0x4Eu, "C8,E4,EE", "Profile,Profile,Guide")]
    [InlineData("surf_loft_guide_resave", 0x4Eu, "C8,E4,EE", "Profile,Profile,Guide")]
    public void SurfaceLoftVariants_DecodeProfileAndGuideSelections(
        string name, uint typeId, string ids, string roles)
    {
        using var file = SwFile.Open(ResolveSurface(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Definition is SurfaceLoftFeatureDefinitionData));
        var definition = Assert.IsType<SurfaceLoftFeatureDefinitionData>(feature.Definition);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.Equal(
            ids.Split(',').Select(value => Convert.ToUInt16(value, 16)).ToArray(),
            definition.SelectionReferenceIds.ToArray());
        Assert.Equal(roles.Split(','), definition.References.Select(r => r.Role.ToString()).ToArray());
        Assert.True(definition.Resolution.ProfileReferences);
    }

    [Theory]
    [InlineData("weldment_structural", "StructuralMember")]
    [InlineData("weldment_structural_resave", "StructuralMember")]
    [InlineData("weldment_structural2", "StructuralMemberSelected")]
    [InlineData("weldment_structural2_resave", "StructuralMemberSelected")]
    public void StructuralMember_DecodesProfilePathAndPathSegment(string name, string featureName)
    {
        using var file = SwFile.Open(ResolveWeldment(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == featureName));
        Assert.Equal(FeatureKind.StructuralMember, feature.Kind);

        var definition = Assert.IsType<StructuralMemberFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moWeldMemberFeat_c", definition.ClassName);
        Assert.Equal(0x4Du, definition.SerializedTypeId);
        Assert.Equal(
            @"C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\weldment profiles\ansi\al tube square.sldlfp",
            definition.WeldmentProfilePath);

        var segment = Assert.Single(definition.PathSegments!);
        Assert.Equal(SelectionEntityType.Edge, segment.EntityType);
        Assert.Equal(0x49u, segment.EntityId);
        Assert.Equal(1, definition.PathSegmentsCount);

        var point = Assert.IsType<EntityReference>(definition.LocateProfilePoint);
        Assert.Equal(SelectionEntityType.Sketch, point.EntityType);
        Assert.Equal(0x2711u, point.EntityId);

        // No controlled variation exists for these fields, so they stay unset.
        Assert.Null(definition.Groups);
        Assert.Null(definition.ConfigurationName);
        Assert.Null(definition.RotationAngle);
        Assert.Null(definition.ApplyCornerTreatment);
        Assert.Null(definition.CornerTreatmentType);
        Assert.False(definition.Resolution.Groups);
        Assert.False(definition.Resolution.ConfigurationName);
        Assert.False(definition.Resolution.RotationAngle);
        Assert.False(definition.Resolution.ApplyCornerTreatment);

        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.WeldmentProfilePath);
        Assert.True(definition.Resolution.PathSegments);
        Assert.True(definition.Resolution.LocateProfilePoint);
    }

    [Theory]
    [InlineData("moSweepRefSurface_c", (ushort)0x71)]
    [InlineData("moBlendRefSurface_c", (ushort)0x71)]
    [InlineData("moWeldMemberFeat_c", (ushort)0x71)]
    [InlineData("moWeldMemberFeat_c", (ushort)0x70)]
    public void Parsers_RejectUnknownOrIncompleteLayouts(string className, ushort version)
    {
        byte[] stream = BuildStream(className, version);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);
        var folder = Assert.Single(model.Folders);
        Assert.Equal(className, folder.ClassName);
        Assert.Null(folder.FeatureData);
    }

    [Fact]
    public void Parsers_DoNotModifyInputStream()
    {
        AssertStreamUnchanged("surf_sweep_basic", "moSweepRefSurface_c",
            e => SurfaceSweepLoftFeatureParsers.ParseSweep(e));
        AssertStreamUnchanged("surf_loft_2sections", "moBlendRefSurface_c",
            e => SurfaceSweepLoftFeatureParsers.ParseLoft(e));
        AssertStreamUnchanged("weldment_structural", "moWeldMemberFeat_c",
            e => WeldmentFeatureParsers.ParseStructuralMember(e));
    }

    private static void AssertStreamUnchanged(
        string name, string className, Func<MfcElement, object?> parse)
    {
        using var file = SwFile.Open(className == "moWeldMemberFeat_c"
            ? ResolveWeldment(name) : ResolveSurface(name), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == className);
        Assert.NotNull(parse(element));
        Assert.Equal(before, stream);
    }

    private static string ResolveSurface(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static string ResolveWeldment(string name) =>
        TestFileProvider.Resolve("Features2026/WeldmentOptions", name + ".SLDPRT");

    private static byte[] BuildStream(string className, ushort version)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { (byte)(version & 0xFF), (byte)(version >> 8), 0x00, 0x00, 0x14, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x07 });
        bytes.AddRange(Encoding.Unicode.GetBytes("Feature"));
        bytes.AddRange(new byte[64]);
        return bytes.ToArray();
    }
}
