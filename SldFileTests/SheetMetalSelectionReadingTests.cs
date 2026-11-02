using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Read-only Selection coverage for the base flange. The mapper reuses Agent 3's
/// structurally decoded moSketchChain_c references; byte-pattern candidates from
/// moFromSktEntSurfIdRep_c are never promoted to selected contours. Explicit
/// contour/region selection on a base flange has no controlled sample yet, so
/// these tests only assert what the whole-sketch samples prove.
/// </summary>
public class SheetMetalSelectionReadingTests
{
    private static string Sample(string folder, string fileName)
    {
        string Relative() => folder == "Features2026"
            ? Path.Combine("TestFiles", "Features2026", fileName)
            : Path.Combine("TestFiles", "Features2026", folder, fileName);

        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Relative());
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", Relative());
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample {folder}/{fileName} not found");
    }

    private static SheetMetalBaseFlangeDefinitionData ReadDefinition(SwFile file) =>
        Assert.IsType<SheetMetalBaseFlangeDefinitionData>(
            file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);

    [Theory]
    [InlineData("Features2026", "sm_base_flange_10mm.SLDPRT", 4)]
    [InlineData("Features2026", "sm_base_flange_after_fillet.SLDPRT", 4)]
    [InlineData("sm_l_profile", "sm_l_d10.SLDPRT", 2)]
    public void BaseFlangeBindsThePrecedingSketchChain(string folder, string fileName, int entityCount)
    {
        using var file = SwFile.Open(Sample(folder, fileName), readOnly: true);
        var definition = ReadDefinition(file);

        Assert.Equal("草图1", definition.SketchName);
        var contour = Assert.Single(definition.SelectedContours);
        Assert.False(contour.IsCandidateOnly);
        Assert.True(contour.IsValid);
        Assert.Equal(entityCount, contour.EntityIndices.Count);
        Assert.Equal(entityCount, contour.EntityIndices.Distinct().Count());

        Assert.NotNull(definition.BodyScope);
        Assert.True(definition.BodyScope.AutoSelect);
        Assert.Equal((ushort)0x0002, definition.BodyScope.Flags);
        Assert.Empty(definition.BodyScope.BodyIds);
    }

    [Fact]
    public void ClosedRectangleProfileExposesOneStructurallyDecodedRegion()
    {
        using var file = SwFile.Open(Sample("Features2026", "sm_base_flange_10mm.SLDPRT"), readOnly: true);
        var definition = ReadDefinition(file);

        var region = Assert.Single(definition.SelectedRegions);
        Assert.Equal(2u, region.RegionIndex);
        Assert.Equal(4u, region.LoopCount);
    }

    [Fact]
    public void OpenLProfileDoesNotInventRegions()
    {
        using var file = SwFile.Open(Sample("sm_l_profile", "sm_l_d10.SLDPRT"), readOnly: true);
        var definition = ReadDefinition(file);

        Assert.Empty(definition.SelectedRegions);
        var contour = Assert.Single(definition.SelectedContours);
        Assert.False(contour.IsCandidateOnly);
        Assert.Equal(new uint[] { 1, 2 }, contour.EntityIndices.OrderBy(i => i).ToArray());
    }

    [Fact]
    public void PrecedingFilletDoesNotStealTheSketchBinding()
    {
        using var file = SwFile.Open(Sample("Features2026", "sm_base_flange_after_fillet.SLDPRT"), readOnly: true);
        var definition = ReadDefinition(file);

        Assert.Equal("草图1", definition.SketchName);
        var contour = Assert.Single(definition.SelectedContours);
        Assert.False(contour.IsCandidateOnly);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, contour.EntityIndices.OrderBy(i => i).ToArray());
        Assert.Null(definition.DirectionReference);
    }
}
