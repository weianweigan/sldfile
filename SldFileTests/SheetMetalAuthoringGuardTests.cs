using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Guard tests for the deliberately restricted SheetMetalBaseFlange authoring
/// entry point. No combination is enabled yet because the template/dimension
/// subsystem has no controlled synthesis sample; every call must reject before
/// modifying the stream instead of emitting a partially understood template.
/// </summary>
public class SheetMetalAuthoringGuardTests
{
    private static string SamplePath()
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "sm_l_profile", "sm_l_d10.SLDPRT");
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", "sm_l_profile", "sm_l_d10.SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException("sm_l_profile/sm_l_d10.SLDPRT not found");
    }

    private static SheetMetalBaseFlangeModel ValidDefinition() => new()
    {
        Thickness = 0.0015,
        BendRadius = 0.002,
        Depth = 0.012,
        SketchName = "草图1"
    };

    public static System.Collections.Generic.IEnumerable<object[]> UnsupportedAuthoringDefinitions()
    {
        SheetMetalBaseFlangeModel Base() => ValidDefinition();

        var reverseThickness = Base(); reverseThickness.ReverseThickness = true;
        yield return new object[] { reverseThickness, "ReverseThickness" };
        var bothDirections = Base(); bothDirections.BothDirections = true;
        yield return new object[] { bothDirections, "BothDirections" };
        var depth2 = Base(); depth2.DepthDirection2 = 0.005;
        yield return new object[] { depth2, "DepthDirection2" };
        var reverseDirection = Base(); reverseDirection.ReverseDirection = true;
        yield return new object[] { reverseDirection, "ReverseDirection" };
        var defaultRadius = Base(); defaultRadius.UseDefaultBendRadius = false;
        yield return new object[] { defaultRadius, "UseDefaultBendRadius" };
        var kFactor = Base(); kFactor.KFactor = 0.3;
        yield return new object[] { kFactor, "KFactor" };
        var allowance = Base(); allowance.BendAllowanceType = 1;
        yield return new object[] { allowance, "BendAllowanceType" };
        var reliefType = Base(); reliefType.AutoReliefType = 2;
        yield return new object[] { reliefType, "AutoReliefType" };
        var reliefRatio = Base(); reliefRatio.ReliefRatio = 0.3;
        yield return new object[] { reliefRatio, "ReliefRatio" };
        var reliefWidth = Base(); reliefWidth.ReliefWidth = 0.001;
        yield return new object[] { reliefWidth, "ReliefWidth" };
        var reliefDepth = Base(); reliefDepth.ReliefDepth = 0.001;
        yield return new object[] { reliefDepth, "ReliefDepth" };
        var defaultRelief = Base(); defaultRelief.UseDefaultBendRelief = false;
        yield return new object[] { defaultRelief, "UseDefaultBendRelief" };
        var gauge = Base(); gauge.UseGaugeTable = true;
        yield return new object[] { gauge, "UseGaugeTable" };
        var bendAllowance = Base(); bendAllowance.BendAllowance = 0.001;
        yield return new object[] { bendAllowance, "BendAllowance" };
        var bendDeduction = Base(); bendDeduction.BendDeduction = 0.001;
        yield return new object[] { bendDeduction, "BendDeduction" };
        var gaugePath = Base(); gaugePath.GaugeTablePath = "table.xlsx";
        yield return new object[] { gaugePath, "GaugeTablePath" };
        var gaugeName = Base(); gaugeName.ThicknessTableName = "规格表";
        yield return new object[] { gaugeName, "ThicknessTableName" };
        var bendTable = Base(); bendTable.BendTableFile = "table.xls";
        yield return new object[] { bendTable, "BendTableFile" };
        var contours = Base(); contours.SelectedContours = new[] { new SketchContourReference() };
        yield return new object[] { contours, "SelectedContours" };
        var regions = Base(); regions.SelectedRegions = new[] { new SketchRegionReference() };
        yield return new object[] { regions, "SelectedRegions" };
        var direction = Base(); direction.DirectionReference = new DirectionReference();
        yield return new object[] { direction, "DirectionReference" };
        var scope = Base(); scope.BodyScope = new BodyScopeData { BodyIds = new uint[] { 42 } };
        yield return new object[] { scope, "BodyScope" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedAuthoringDefinitions))]
    public void UnsupportedOptionsAreRejectedWithoutModifyingTheStream(
        SheetMetalBaseFlangeModel definition, string property)
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        const string streamPath = "Contents/Config-0-ResolvedFeatures";
        byte[] streamBefore;
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(file);
            streamBefore = (byte[])part.RawStreams[streamPath].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(definition));
            Assert.Contains(property, error.Message);
            Assert.Equal(streamBefore, part.RawStreams[streamPath]);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(streamBefore, reopened.RawStreams[streamPath]);
    }

    [Fact]
    public void AddSheetMetalBaseFlange_OnReadOnlyDocument_ThrowsInvalidOperationException()
    {
        using var document = Assert.IsType<SldPart>(SwFile.Open(SamplePath(), readOnly: true));
        Assert.Throws<InvalidOperationException>(() => document.AddSheetMetalBaseFlange(ValidDefinition()));
    }

    [Fact]
    public void AddSheetMetalBaseFlange_WithNullDefinition_ThrowsArgumentNullException()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        Assert.Throws<ArgumentNullException>(() => document.AddSheetMetalBaseFlange(null!));
    }

    [Theory]
    [InlineData(0.0, 0.002, 0.010)]
    [InlineData(-0.001, 0.002, 0.010)]
    [InlineData(double.NaN, 0.002, 0.010)]
    [InlineData(double.PositiveInfinity, 0.002, 0.010)]
    public void AddSheetMetalBaseFlange_WithInvalidThickness_ThrowsArgumentOutOfRange(
        double thickness, double radius, double depth)
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var definition = new SheetMetalBaseFlangeModel { Thickness = thickness, BendRadius = radius, Depth = depth };
        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddSheetMetalBaseFlange(definition));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.002)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddSheetMetalBaseFlange_WithInvalidRadius_ThrowsArgumentOutOfRange(double radius)
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var definition = ValidDefinition();
        definition.BendRadius = radius;
        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddSheetMetalBaseFlange(definition));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.010)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddSheetMetalBaseFlange_WithInvalidDepth_ThrowsArgumentOutOfRange(double depth)
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var definition = ValidDefinition();
        definition.Depth = depth;
        Assert.Throws<ArgumentOutOfRangeException>(() => document.AddSheetMetalBaseFlange(definition));
    }

    [Fact]
    public void AddSheetMetalBaseFlange_WithUnsupportedOption_ThrowsNotSupported()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var definition = ValidDefinition();
        definition.KFactor = 0.3;
        var ex = Assert.Throws<NotSupportedException>(() => document.AddSheetMetalBaseFlange(definition));
        Assert.Contains("KFactor", ex.Message);
        Assert.Contains("No stream changes", ex.Message);
    }

    [Fact]
    public void AddSheetMetalBaseFlange_WithMissingSketch_ThrowsArgumentException()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var definition = ValidDefinition();
        definition.SketchName = "不存在的草图";
        Assert.Throws<ArgumentException>(() => document.AddSheetMetalBaseFlange(definition, "不存在的草图"));
    }

    [Fact]
    public void DecodedDefaultOptionsReachTheStrippedSpecimenGate()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var definition = ValidDefinition();
        definition.AutoReliefType = 1; // evidenced Rectangular default

        var ex = Assert.Throws<NotSupportedException>(() => document.AddSheetMetalBaseFlange(definition));
        Assert.Contains("still contains moSMBaseFlange_c", ex.Message);
        Assert.Contains("No stream changes", ex.Message);
    }

    [Fact]
    public void AddSheetMetalBaseFlange_WithCustomFeatureName_RejectsWithoutMutatingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var document = Assert.IsType<SldPart>(SwFile.Open(temp.FilePath, readOnly: false));
        var streamPath = "Contents/Config-0-ResolvedFeatures";
        var before = document.RawStreams[streamPath].ToArray();
        var featureCountBefore = document.Features.Count;

        var ex = Assert.Throws<NotSupportedException>(() =>
            document.AddSheetMetalBaseFlange(ValidDefinition(), "草图1", "基体-法兰2"));

        Assert.Contains("display name", ex.Message);
        Assert.Contains("No stream changes", ex.Message);
        Assert.Equal(before, document.RawStreams[streamPath]);
        Assert.Equal(featureCountBefore, document.Features.Count);
    }
}
