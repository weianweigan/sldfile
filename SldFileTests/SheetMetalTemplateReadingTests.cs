using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

public class SheetMetalTemplateReadingTests
{
    [Theory]
    [InlineData("sm_base_flange_10mm.SLDPRT", 0.001, 0.002)]
    [InlineData("sm_base_flange_thick2.SLDPRT", 0.002, 0.002)]
    [InlineData("sm_base_flange_rad3.SLDPRT", 0.001, 0.003)]
    [InlineData("sm_base_flange_after_fillet.SLDPRT", 0.002, 0.003)]
    public void ReadsDimensionsFromOwningTemplateDespiteEarlierFillet(string name, double thickness, double radius)
    {
        using var part = SwFile.Open(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", name), true);
        var definition = Assert.IsType<SheetMetalBaseFlangeDefinitionData>(
            Assert.Single(part.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c")).FeatureData);
        Assert.Equal(thickness, definition.Thickness);
        Assert.Equal(radius, definition.BendRadius);
        Assert.True(definition.ParameterResolution.Thickness);
        Assert.True(definition.ParameterResolution.BendRadius);
    }

    [Fact]
    public void UnknownOrTruncatedTemplateDoesNotInventManufacturingDimensions()
    {
        var definition = new SheetMetalBaseFlangeDefinitionData();
        SheetMetalTemplateParser.Read(new byte[] { 0x73, 0, 0, 0 }, 0, 4, definition);
        Assert.Equal(0, definition.Thickness);
        Assert.Equal(0, definition.BendRadius);
        Assert.False(definition.ParameterResolution.Thickness);
        Assert.False(definition.ParameterResolution.BendRadius);
    }

    [Fact]
    public void ConflictingTemplateCandidatesAreNotResolvedByLastMatch()
    {
        byte[] ReadWindow(string name)
        {
            using var part = SwFile.Open(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", name), true);
            var template = Assert.Single(part.ResolvedFeatures.Folders.Where(f => f.ClassName == "moTemplateSheetMetal_c"));
            var flange = Assert.Single(part.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
            return part.RawStreams["Contents/Config-0-ResolvedFeatures"]
                .AsSpan(template.TagOffset, flange.TagOffset - template.TagOffset).ToArray();
        }
        var first = ReadWindow("sm_base_flange_10mm.SLDPRT");
        var second = ReadWindow("sm_base_flange_rad3.SLDPRT");
        var ambiguous = new byte[2 + first.Length + second.Length];
        ambiguous[0] = 0x73;
        first.CopyTo(ambiguous, 2);
        second.CopyTo(ambiguous, 2 + first.Length);
        var definition = new SheetMetalBaseFlangeDefinitionData();
        SheetMetalTemplateParser.Read(ambiguous, 2, ambiguous.Length, definition);
        Assert.Equal(0, definition.BendRadius);
        Assert.Equal(0.001, definition.Thickness);
    }
}
