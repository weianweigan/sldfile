using SldFile.Mfc;
using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Reading tests for the SolidWorks 2026 tutorial/design-library samples found by
/// the 2026-10-31 corpus scan. These are much newer stream generations than the
/// controlled Task 2.3 samples (0x155 / 0x1AA) and carry the first real
/// Jog/Fold/UnFold features.
/// </summary>
public class TutorialSheetMetalFeatureTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "TutorialSheetMetal", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles",
                "Features2026", "TutorialSheetMetal", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"TutorialSheetMetal/{fileName} not found");
    }

    [Fact]
    public void PaperAirplaneFoldAndUnfoldReadBendsCount()
    {
        using var file = SwFile.Open(SamplePath("Paper Airplane.SLDPRT"), readOnly: true);
        var unfoldFolder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moUnFold_c");
        var foldFolder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moFold_c");
        Assert.Equal(0x144u, unfoldFolder.Header.TypeId);
        Assert.Equal(0x153u, foldFolder.Header.TypeId);

        var unfold = Assert.IsType<UnFoldFeatureModel>(unfoldFolder.FeatureData);
        Assert.Equal(0x144u, unfold.TypeId);
        Assert.Equal(3, unfold.BendsCount ?? -1);
        Assert.True(unfold.ParameterResolution.BendsCount);
        Assert.Null(unfold.FixedFace);
        Assert.Null(unfold.Bends);
        Assert.False(unfold.ParameterResolution.FixedFace);
        Assert.False(unfold.ParameterResolution.Bends);

        var fold = Assert.IsType<FoldFeatureModel>(foldFolder.FeatureData);
        Assert.Equal(0x153u, fold.TypeId);
        Assert.Equal(3, fold.BendsCount ?? -1);
        Assert.True(fold.ParameterResolution.BendsCount);
        Assert.Null(fold.FixedFace);
        Assert.Null(fold.Bends);
        Assert.False(fold.ParameterResolution.FixedFace);
        Assert.False(fold.ParameterResolution.Bends);

        var part = Assert.IsType<SldPart>(file);
        Assert.Single(part.Features, f => f.Name == "Unfold2" && f.Kind == FeatureKind.UnFold);
        Assert.Single(part.Features, f => f.Name == "Fold2" && f.Kind == FeatureKind.Fold);
    }

    [Fact]
    public void ComplexSheetMetalPartJogReadsRadiusAngleAndOffset()
    {
        using var file = SwFile.Open(SamplePath("Complex Sheet Metal Part.SLDPRT"), readOnly: true);
        Assert.Equal((ushort)0x1AA,
            BitConverter.ToUInt16(file.RawStreams[ResolvedStream], 0));

        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moJog_c");
        Assert.Equal(0x159u, folder.Header.TypeId);
        var jog = Assert.IsType<JogFeatureModel>(folder.FeatureData);
        Assert.Equal(0x159u, jog.TypeId);
        Assert.True(jog.ParameterResolution.BendRadius);
        Assert.True(jog.ParameterResolution.JogAngle);
        Assert.True(jog.ParameterResolution.OffsetDistance);
        Assert.Equal(0.00254, jog.BendRadius!.Value, precision: 12);
        Assert.Equal(1.5707963267949, jog.JogAngle!.Value, precision: 12);
        Assert.Equal(0.0127, jog.OffsetDistance!.Value, precision: 12);

        // Flags resolved from the anchored tutorial layout (native readback and
        // 11 single-variable native variants).
        Assert.True(jog.UseDefaultBendRadius ?? false);
        Assert.True(jog.UseDefaultBendAllowance ?? false);
        Assert.True(jog.ReverseDirection ?? false);
        Assert.False(jog.ReverseOffset ?? true);
        Assert.True(jog.FixProjectedLength ?? false);
        Assert.Equal(1, jog.OffsetType ?? -1);
        Assert.Equal(2, jog.JogPositionType ?? -1);
        Assert.Equal(2, jog.DimensionPositionType ?? -1);
        Assert.True(jog.ParameterResolution.UseDefaultBendRadius);
        Assert.True(jog.ParameterResolution.UseDefaultBendAllowance);
        Assert.True(jog.ParameterResolution.ReverseDirection);
        Assert.True(jog.ParameterResolution.ReverseOffset);
        Assert.True(jog.ParameterResolution.FixProjectedLength);
        Assert.True(jog.ParameterResolution.OffsetType);
        Assert.True(jog.ParameterResolution.JogPositionType);
        Assert.True(jog.ParameterResolution.DimensionPositionType);
        // No provable serialized face reference for the tutorial Jog layout.
        Assert.Null(jog.FixedFace);
        Assert.False(jog.ParameterResolution.FixedFace);

        var part = Assert.IsType<SldPart>(file);
        Assert.Single(part.Features, f => f.Name == "Jog1" && f.Kind == FeatureKind.Jog);
    }

    [Fact]
    public void JogFlagAnchorsAreRequiredAndTamperingLeavesFieldsUnresolved()
    {
        using var file = SwFile.Open(SamplePath("Complex Sheet Metal Part.SLDPRT"), readOnly: true);
        byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
        const int defaultAnchor = 0x6100C;
        const int offsetAnchor = 0x62084;
        const int jogPositionAnchor = 0x612EC;

        Assert.Equal(0x01, raw[defaultAnchor + 0x18]);
        Assert.Equal(0x02, raw[offsetAnchor]);
        Assert.Equal(0x24, raw[jogPositionAnchor]);

        // Invalid boolean at the default/allowance anchor.
        byte[] badAllow = (byte[])raw.Clone();
        badAllow[defaultAnchor + 0x18] = 0x02;
        var doc = SldFile.Mfc.MfcDocument.Parse(badAllow);
        var element = Assert.Single(doc.AllElements, e => e.ClassName == "moJog_c");
        var jog = Assert.IsType<JogFeatureModel>(
            SheetMetalFormingParsers.ParseJog(doc, element, badAllow.Length));
        Assert.False(jog.ParameterResolution.UseDefaultBendAllowance);
        Assert.Null(jog.UseDefaultBendAllowance);
        Assert.True(jog.ParameterResolution.UseDefaultBendRadius);
        Assert.True(jog.ParameterResolution.BendRadius);

        // Broken offset-block anchor.
        byte[] badOffset = (byte[])raw.Clone();
        badOffset[offsetAnchor] = 0x03;
        doc = SldFile.Mfc.MfcDocument.Parse(badOffset);
        element = Assert.Single(doc.AllElements, e => e.ClassName == "moJog_c");
        jog = Assert.IsType<JogFeatureModel>(
            SheetMetalFormingParsers.ParseJog(doc, element, badOffset.Length));
        Assert.False(jog.ParameterResolution.OffsetType);
        Assert.False(jog.ParameterResolution.FixProjectedLength);
        Assert.False(jog.ParameterResolution.ReverseOffset);
        Assert.Null(jog.OffsetType);

        // Broken jog-position anchor.
        byte[] badPosition = (byte[])raw.Clone();
        badPosition[jogPositionAnchor + 1] = 0x83;
        doc = SldFile.Mfc.MfcDocument.Parse(badPosition);
        element = Assert.Single(doc.AllElements, e => e.ClassName == "moJog_c");
        jog = Assert.IsType<JogFeatureModel>(
            SheetMetalFormingParsers.ParseJog(doc, element, badPosition.Length));
        Assert.False(jog.ParameterResolution.JogPositionType);
        Assert.Null(jog.JogPositionType);
        Assert.True(jog.ParameterResolution.JogAngle);
    }

    [Fact]
    public void DriveSledFoldAndUnfoldReadBendsCount()
    {
        using var file = SwFile.Open(SamplePath("drive sled.sldprt"), readOnly: true);
        var unfoldFolder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moUnFold_c");
        var foldFolder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moFold_c");
        Assert.Equal(0x209u, unfoldFolder.Header.TypeId);
        Assert.Equal(0x213u, foldFolder.Header.TypeId);

        var unfold = Assert.IsType<UnFoldFeatureModel>(unfoldFolder.FeatureData);
        Assert.Equal(4, unfold.BendsCount ?? -1);
        var fold = Assert.IsType<FoldFeatureModel>(foldFolder.FeatureData);
        Assert.Equal(4, fold.BendsCount ?? -1);
    }

    [Fact]
    public void TutorialBaseFlangeBendTableFileIsResolvedFromGlobalWindow()
    {
        // Paper Airplane and drive sled natively report a custom bend table;
        // Complex reports null and its stream has no such path.
        using (var paper = SwFile.Open(SamplePath("Paper Airplane.SLDPRT"), readOnly: true))
        {
            var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
                paper.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
            Assert.True(definition.ParameterResolution.BendTableFile);
            Assert.Contains("base bend table.xls", definition.BendTableFile!,
                StringComparison.OrdinalIgnoreCase);
        }

        using (var sled = SwFile.Open(SamplePath("drive sled.sldprt"), readOnly: true))
        {
            var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
                sled.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
            Assert.True(definition.ParameterResolution.BendTableFile);
            Assert.Contains("base bend table.xls", definition.BendTableFile!,
                StringComparison.OrdinalIgnoreCase);
        }

        using (var complex = SwFile.Open(SamplePath("Complex Sheet Metal Part.SLDPRT"), readOnly: true))
        {
            var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
                complex.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
            Assert.False(definition.ParameterResolution.BendTableFile);
            Assert.Null(definition.BendTableFile);
        }
    }

    [Fact]
    public void TutorialBendTableFileSurvivesResave()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath("Paper Airplane.SLDPRT"));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            sw.Save();

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        Assert.True(definition.ParameterResolution.BendTableFile);
        Assert.Contains("base bend table.xls", definition.BendTableFile!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TutorialBendTablePathTamperingLeavesFieldUnresolved()
    {
        using var paper = SwFile.Open(SamplePath("Paper Airplane.SLDPRT"), readOnly: true);
        byte[] raw = (byte[])paper.RawStreams[ResolvedStream].Clone();
        // First "bend table" WChar occurrence in the moSheetMetal window.
        byte[] needle = System.Text.Encoding.Unicode.GetBytes("Bend Tables");
        int hit = -1;
        for (int p = 0; p <= raw.Length - needle.Length; p++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
                if (raw[p + j] != needle[j]) { match = false; break; }
            if (match) { hit = p; break; }
        }
        Assert.True(hit > 0);
        raw[hit] ^= 0x01; // "Bend Tables" -> invalid marker
        var doc = SldFile.Mfc.MfcDocument.Parse(raw);
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(doc, model);
        var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
            model.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        Assert.False(definition.ParameterResolution.BendTableFile);
        Assert.Null(definition.BendTableFile);
    }

    public static IEnumerable<object[]> FlatPatternSamples()
    {
        yield return new object[] { "Paper Airplane.SLDPRT", 0x72u, true };
        yield return new object[] { "Complex Sheet Metal Part.SLDPRT", 0x1Eu, true };
        // The drive-sled 0xC7 layout stores the option block elsewhere than the
        // flag byte; MergeFace/CornerTreatment are intentionally unresolved.
        yield return new object[] { "drive sled.sldprt", 0x164u, false };
    }

    [Theory]
    [MemberData(nameof(FlatPatternSamples))]
    public void TutorialFlatPatternReadsNativeDefaults(string fileName, uint typeId,
        bool flagsResolved)
    {
        using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moFlatPattern_c");
        Assert.Equal(typeId, folder.Header.TypeId);
        var definition = Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);
        Assert.Equal(typeId, definition.TypeId);

        if (flagsResolved)
        {
            Assert.True(definition.MergeFace ?? false);
            Assert.True(definition.CornerTreatment ?? false);
            Assert.True(definition.ParameterResolution.MergeFace);
            Assert.True(definition.ParameterResolution.CornerTreatment);
        }
        else
        {
            Assert.Null(definition.MergeFace);
            Assert.Null(definition.CornerTreatment);
            Assert.False(definition.ParameterResolution.MergeFace);
            Assert.False(definition.ParameterResolution.CornerTreatment);
        }
        Assert.True(definition.SimplifyBends ?? false);
        Assert.Equal(0, definition.BreakCornerType ?? -1);
        Assert.Equal(0.005, definition.BreakCornerRadius!.Value, 12);
        Assert.Equal(0, definition.CornerTrimReliefType ?? -1);
        Assert.Equal(0.005, definition.CornerTrimReliefDistance!.Value, 12);
        Assert.Equal(0.005, definition.CornerTrimRatioToThickness!.Value, 12);
        Assert.False(definition.UseRatioToThickness ?? true);
        Assert.True(definition.ShowSlitInCornerRelief ?? false);

        Assert.True(definition.ParameterResolution.SimplifyBends);
        Assert.True(definition.ParameterResolution.BreakCornerType);
        Assert.True(definition.ParameterResolution.BreakCornerRadius);
        Assert.True(definition.ParameterResolution.CornerTrimReliefType);
        Assert.True(definition.ParameterResolution.CornerTrimReliefDistance);
        Assert.True(definition.ParameterResolution.CornerTrimRatioToThickness);
        Assert.True(definition.ParameterResolution.UseRatioToThickness);
        Assert.True(definition.ParameterResolution.ShowSlitInCornerRelief);
        Assert.Null(definition.FixedFace);
        Assert.Null(definition.FixedFace2);
        Assert.False(definition.ParameterResolution.FixedFace);
    }

    [Fact]
    public void TutorialFlatPatternOptionSlotsAndTamper()
    {
        using var file = SwFile.Open(SamplePath("Paper Airplane.SLDPRT"), readOnly: true);
        byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
        int anchor = FindTutorialFlatAnchor(raw);
        Assert.True(anchor > 0);

        byte[] mutated = (byte[])raw.Clone();
        mutated[anchor - 0x0E] = 0;             // MergeFace false, CornerTreatment true
        BitConverter.GetBytes(1u).CopyTo(mutated, anchor + 0x08);
        BitConverter.GetBytes(0.002).CopyTo(mutated, anchor + 0x0C);
        BitConverter.GetBytes(1u).CopyTo(mutated, anchor + 0x14);
        BitConverter.GetBytes(0.003).CopyTo(mutated, anchor + 0x18);
        mutated[anchor + 0x24] = 1;             // UseRatioToThickness true
        mutated[anchor + 0x28] = 0;             // SimplifyBends false
        mutated[anchor + 0x2C] = 0;             // ShowSlit false
        var doc = SldFile.Mfc.MfcDocument.Parse(mutated);
        var element = Assert.Single(doc.AllElements, e => e.ClassName == "moFlatPattern_c");
        var parsed = Assert.IsType<FlatPatternFeatureModel>(
            SheetMetalFormingParsers.ParseFlatPattern(doc, element, mutated.Length));
        Assert.False(parsed.MergeFace ?? true);
        Assert.True(parsed.CornerTreatment ?? false);
        Assert.Equal(1, parsed.CornerTrimReliefType ?? -1);
        Assert.Equal(0.002, parsed.CornerTrimReliefDistance!.Value, 12);
        Assert.Equal(0.002, parsed.CornerTrimRatioToThickness!.Value, 12);
        Assert.Equal(1, parsed.BreakCornerType ?? -1);
        Assert.Equal(0.003, parsed.BreakCornerRadius!.Value, 12);
        Assert.True(parsed.UseRatioToThickness ?? false);
        Assert.False(parsed.SimplifyBends ?? true);
        Assert.False(parsed.ShowSlitInCornerRelief ?? true);

        // Break the trailing signature: fields stay unresolved, TypeId remains.
        byte[] tampered = (byte[])raw.Clone();
        tampered[anchor + 0x32] = 0;
        doc = SldFile.Mfc.MfcDocument.Parse(tampered);
        element = Assert.Single(doc.AllElements, e => e.ClassName == "moFlatPattern_c");
        parsed = Assert.IsType<FlatPatternFeatureModel>(
            SheetMetalFormingParsers.ParseFlatPattern(doc, element, tampered.Length));
        Assert.Equal(0x72u, parsed.TypeId);
        Assert.False(parsed.ParameterResolution.MergeFace);
        Assert.False(parsed.ParameterResolution.BreakCornerRadius);
        Assert.Null(parsed.MergeFace);
    }

    private static int FindTutorialFlatAnchor(byte[] raw)
    {
        for (int p = 0x0E; p <= raw.Length - 0x36; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFF ||
                raw[p + 2] != 0xFF || raw[p + 3] != 0xFF)
                continue;
            bool zeros = true;
            for (int j = 4; j <= 7; j++)
                if (raw[p + j] != 0) { zeros = false; break; }
            if (!zeros) continue;
            if (BitConverter.ToUInt32(raw, p + 0x08) > 9) continue;
            double trim = BitConverter.ToDouble(raw, p + 0x0C);
            if (trim <= 0 || trim > 0.1) continue;
            if (BitConverter.ToUInt32(raw, p + 0x14) > 1) continue;
            double radius = BitConverter.ToDouble(raw, p + 0x18);
            if (radius <= 0 || radius > 0.1) continue;
            if (raw[p + 0x24] > 1 || raw[p + 0x28] > 1 || raw[p + 0x2C] > 1) continue;
            if (raw[p + 0x32] != 0x2B || raw[p + 0x33] != 0x80 || raw[p + 0x34] != 0x02)
                continue;
            return p;
        }
        return -1;
    }

    [Fact]
    public void UnknownLayoutsReturnNull()
    {
        using var file = SwFile.Open(SamplePath("Complex Sheet Metal Part.SLDPRT"), readOnly: true);
        byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
        byte[] wrongVersion = (byte[])raw.Clone();
        BitConverter.GetBytes((ushort)0x73).CopyTo(wrongVersion, 0);
        var doc = SldFile.Mfc.MfcDocument.Parse(wrongVersion);
        var element = Assert.Single(doc.AllElements, e => e.ClassName == "moJog_c");
        Assert.Null(SheetMetalFormingParsers.ParseJog(doc, element, wrongVersion.Length));
    }
}
