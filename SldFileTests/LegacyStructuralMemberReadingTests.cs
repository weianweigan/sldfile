using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Legacy tutorial <c>moWeldMemberFeat_c</c> reading. Only the profile path and
/// the contour-block group count are positionally proven; corner treatment,
/// rotation, configuration name, path segments and locate point stay unresolved.
/// </summary>
public class LegacyStructuralMemberReadingTests
{
    [Theory]
    [InlineData("tutorial_weldments_weldment_box2", "Square tube 30 X 30 X 2.6(1)", 0x36u, "30 x 30 x 2.6.sldlfp", 3, 2)]
    [InlineData("tutorial_api_weldment_box3", "Structural Member1", 0x33u, "30 x 30 x 2.6.sldlfp", 3, 2)]
    [InlineData("tutorial_api_loop", "Structural Member1", 0x17u, "120 x 80 x 8.sldlfp", 1, 1)]
    [InlineData("tutorial_driveworksxpress_leg", "Bottom Cross Member", 0x2Au, "80 x 80 x 5.sldlfp", 0, 1)]
    [InlineData("tutorial_routing_pipes_framework_done", "Structural Member1", 0x159u, "100 x 10.sldlfp", 0, 1)]
    public void LegacyStructuralMember_ParsesProfileGroupAndCornerTreatment(
        string sample, string featureName, uint typeId, string profileFile, int groups, int cornerType)
    {
        using var file = SwFile.Open(Sample(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f =>
            f.Name == featureName && f.Definition is StructuralMemberFeatureDefinitionData));

        Assert.Equal(FeatureKind.StructuralMember, feature.Kind);
        var definition = Assert.IsType<StructuralMemberFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moWeldMemberFeat_c", definition.ClassName);
        Assert.Equal(typeId, definition.SerializedTypeId);
        Assert.Equal(typeId, definition.TypeId);
        Assert.EndsWith(profileFile, definition.WeldmentProfilePath!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(groups, definition.GroupsCount);
        Assert.NotNull(definition.Groups);
        Assert.All(definition.Groups!, group =>
        {
            Assert.Equal(SelectionEntityType.None, group.EntityType);
            Assert.Equal(0u, group.EntityId);
            Assert.StartsWith("StructuralMemberGroup", group.Name, StringComparison.Ordinal);
        });

        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.WeldmentProfilePath);
        Assert.True(definition.Resolution.Groups);

        // Proven by native ModifyDefinition save/reopen probes across all five families.
        Assert.True(definition.ApplyCornerTreatment);
        Assert.Equal(cornerType, definition.CornerTreatmentType);
        Assert.Equal(0.0, definition.RotationAngle!.Value, 12);
        Assert.True(definition.Resolution.ApplyCornerTreatment);
        Assert.True(definition.Resolution.CornerTreatmentType);
        Assert.True(definition.Resolution.RotationAngle);

        // Native values exist but no strict serialized slot is proven yet.
        Assert.Null(definition.ConfigurationName);
        Assert.Null(definition.PathSegments);
        Assert.Null(definition.LocateProfilePoint);
        Assert.False(definition.Resolution.ConfigurationName);
        Assert.False(definition.Resolution.PathSegments);
    }

    [Fact]
    public void LegacyStructuralMember_InvalidCornerAndRotationStayUnresolved()
    {
        using var file = SwFile.Open(Sample("tutorial_weldments_weldment_box2"), readOnly: true);
        string streamPath = file.ResolvedFeaturesStreamPath!;
        byte[] raw = (byte[])file.RawStreams[streamPath].Clone();
        var original = MfcDocument.Parse(raw);
        var element = original.AllElements.Single(e =>
            e.TagByte == 0x01 && e.ClassName == "moWeldMemberFeat_c");

        // Measured box2 family slot offsets, proven by native ModifyDefinition probes.
        const int cornerOffset = 0x5DF1;
        const int rotationOffset = 0x5E03;
        Assert.Equal(2u, BitConverter.ToUInt32(raw, element.TagOffset + cornerOffset));
        Assert.Equal(0.0, BitConverter.ToDouble(raw, element.TagOffset + rotationOffset));

        BitConverter.GetBytes(3u).CopyTo(raw, element.TagOffset + cornerOffset);
        BitConverter.GetBytes(10.0).CopyTo(raw, element.TagOffset + rotationOffset);

        var tampered = MfcDocument.Parse(raw);
        var tamperedElement = tampered.AllElements.Single(e =>
            e.TagByte == 0x01 && e.ClassName == "moWeldMemberFeat_c");
        var definition = Assert.IsType<StructuralMemberFeatureDefinitionData>(
            LegacyStructuralMemberParser.Parse(
                tampered, tamperedElement.ClassName, 0x36u,
                tamperedElement.TagOffset, raw.Length));

        Assert.Null(definition.ApplyCornerTreatment);
        Assert.Null(definition.CornerTreatmentType);
        Assert.Null(definition.RotationAngle);
        Assert.False(definition.Resolution.ApplyCornerTreatment);
        Assert.False(definition.Resolution.CornerTreatmentType);
        Assert.False(definition.Resolution.RotationAngle);
        Assert.NotNull(definition.WeldmentProfilePath);
        Assert.Equal(3, definition.GroupsCount);
    }

    private static string Sample(string name) => Path.Combine(
        AppContext.BaseDirectory, "TestFiles", "Features2026",
        "LegacyTutorialSamples", "Samples", name + ".SLDPRT");
}
