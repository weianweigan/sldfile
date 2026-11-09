using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Task 2.4 flat-pattern reading against the native sheet-metal samples. The
/// existing samples prove the serialized class, the two FolderHeader TypeIds and
/// the fixed-face selection; all other IFlatPatternFeatureData values are
/// constant defaults without native snapshots and stay unresolved.
/// </summary>
public class FlatPatternFeatureTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string relative)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026",
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Features2026/{relative} not found");
    }

    public static IEnumerable<object[]> NativeSamples()
    {
        yield return new object[] { "sm_base_flange_10mm.SLDPRT", 0x5D };
        yield return new object[] { "sm_base_flange_20mm.SLDPRT", 0x5D };
        yield return new object[] { "sm_base_flange_thick2.SLDPRT", 0x5D };
        yield return new object[] { "sm_base_flange_rad3.SLDPRT", 0x5D };
        yield return new object[] { "SheetMetalOptions/sm_l_d10.SLDPRT", 0x5E };
        yield return new object[] { "SheetMetalOptions/sm_l_k03_fixed.SLDPRT", 0x5E };
        yield return new object[] { "SheetMetalOptions/sm_two_base_flanges.SLDPRT", 0x5E };
    }

    [Theory]
    [MemberData(nameof(NativeSamples))]
    public void NativeFlatPatternReadsObservedHeaderAndFixedFace(string relative, uint typeId)
    {
        using var file = SwFile.Open(SamplePath(relative), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders, f => f.ClassName == "moFlatPattern_c");
        Assert.Equal(typeId, folder.Header.TypeId);
        var definition = Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);

        Assert.Equal(typeId, definition.TypeId);
        Assert.NotNull(definition.FixedFace);
        Assert.Equal(27136u, definition.FixedFace!.EntityId);
        Assert.Equal(SelectionEntityType.Face, definition.FixedFace.EntityType);
        Assert.False(string.IsNullOrEmpty(definition.FixedFace.Name));
        Assert.True(definition.ParameterResolution.FixedFace);

        // Defaults proven by the variants2 samples.
        Assert.True(definition.MergeFace ?? false);
        Assert.True(definition.SimplifyBends ?? false);
        Assert.True(definition.CornerTreatment ?? false);
        Assert.Equal(0, definition.BreakCornerType ?? -1);
        Assert.Equal(0.005, definition.BreakCornerRadius!.Value, 12);
        Assert.Equal(0, definition.CornerTrimReliefType ?? -1);
        Assert.Equal(0.005, definition.CornerTrimReliefDistance!.Value, 12);

        // Still unresolved: no varying sample.
        Assert.Null(definition.FixedFace2);
        Assert.Null(definition.ExcludedFaces);
        Assert.Null(definition.CornerTrimRatioToThickness);
        Assert.Null(definition.UseRatioToThickness);
        Assert.Null(definition.ShowSlitInCornerRelief);
        Assert.Null(definition.AddCornerTrim);
        Assert.Null(definition.BreakCorners);

        var resolved = new HashSet<string>
        {
            "FixedFace", "MergeFace", "SimplifyBends", "CornerTreatment",
            "BreakCornerType", "BreakCornerRadius",
            "CornerTrimReliefType", "CornerTrimReliefDistance"
        };
        foreach (var property in typeof(FlatPatternParameterResolution).GetProperties())
        {
            Assert.Equal(resolved.Contains(property.Name),
                (bool)property.GetValue(definition.ParameterResolution)!);
        }
    }

    public static IEnumerable<object[]> FlatPatternVariants()
    {
        // merge, simplify, corner, breakType, breakRadius, trimType, trimDistance
        yield return new object[] { "flat_merge_face_true.SLDPRT", true, true, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_merge_face_true_resave.SLDPRT", true, true, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_merge_face_false.SLDPRT", false, true, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_merge_face_false_resave.SLDPRT", false, true, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_simplify_bends_true.SLDPRT", true, true, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_simplify_bends_false.SLDPRT", true, false, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_simplify_bends_false_resave.SLDPRT", true, false, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_corner_treatment_true.SLDPRT", true, true, true, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_corner_treatment_false.SLDPRT", true, true, false, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_corner_treatment_false_resave.SLDPRT", true, true, false, 0, 0.005, 0, 0.005 };
        yield return new object[] { "flat_break_corner_chamfer.SLDPRT", true, true, true, 1, 0.002, 0, 0.005 };
        yield return new object[] { "flat_break_corner_chamfer_resave.SLDPRT", true, true, true, 1, 0.002, 0, 0.005 };
        yield return new object[] { "flat_corner_trim_square.SLDPRT", true, true, true, 0, 0.005, 1, 0.002 };
        yield return new object[] { "flat_corner_trim_square_resave.SLDPRT", true, true, true, 0, 0.005, 1, 0.002 };
        yield return new object[] { "flat_ratio_false_2.SLDPRT", true, true, true, 0, 0.005, 0, 2.0 };
        yield return new object[] { "flat_ratio_false_2_resave.SLDPRT", true, true, true, 0, 0.005, 0, 2.0 };
    }

    [Theory]
    [MemberData(nameof(FlatPatternVariants))]
    public void FlatPatternOptionVariantsMatchNativeSnapshots(
        string fileName, bool merge, bool simplify, bool corner,
        int breakType, double breakRadius, int trimType, double trimDistance)
    {
        using var file = SwFile.Open(SamplePath("FlatPatternOptions/" + fileName), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders, f => f.ClassName == "moFlatPattern_c");
        Assert.Equal(0x5Du, folder.Header.TypeId);
        var definition = Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);

        Assert.Equal(merge, definition.MergeFace ?? !merge);
        Assert.Equal(simplify, definition.SimplifyBends ?? !simplify);
        Assert.Equal(corner, definition.CornerTreatment ?? !corner);
        Assert.Equal(breakType, definition.BreakCornerType ?? -1);
        Assert.Equal(breakRadius, definition.BreakCornerRadius!.Value, 12);
        Assert.Equal(trimType, definition.CornerTrimReliefType ?? -1);
        Assert.Equal(trimDistance, definition.CornerTrimReliefDistance!.Value, 12);

        Assert.True(definition.ParameterResolution.MergeFace);
        Assert.True(definition.ParameterResolution.SimplifyBends);
        Assert.True(definition.ParameterResolution.CornerTreatment);
        Assert.True(definition.ParameterResolution.BreakCornerType);
        Assert.True(definition.ParameterResolution.BreakCornerRadius);
        Assert.True(definition.ParameterResolution.CornerTrimReliefType);
        Assert.True(definition.ParameterResolution.CornerTrimReliefDistance);

        // Both distance and ratio are 2.0 in the ratio sample; only one double
        // changes, so the ratio itself and UseRatioToThickness stay unresolved.
        Assert.Null(definition.CornerTrimRatioToThickness);
        Assert.Null(definition.UseRatioToThickness);
        Assert.Null(definition.ShowSlitInCornerRelief);
        Assert.False(definition.ParameterResolution.CornerTrimRatioToThickness);
        Assert.False(definition.ParameterResolution.UseRatioToThickness);
        Assert.False(definition.ParameterResolution.ShowSlitInCornerRelief);
    }

    [Fact]
    public void FlatPatternFolderMapsToFlatPatternKindButTemplateDoesNot()
    {
        using var file = SwFile.Open(SamplePath("sm_base_flange_10mm.SLDPRT"), readOnly: true);
        var part = Assert.IsType<SldPart>(file);
        Assert.Contains(part.Features, f => f.Kind == FeatureKind.FlatPattern);

        var template = Assert.Single(part.ResolvedFeatures.Folders,
            f => f.ClassName == "moTemplateFlatPattern_c");
        Assert.False(template.FeatureData is FlatPatternFeatureModel);
    }

    [Fact]
    public void MappingDoesNotModifyFlatPatternStreams()
    {
        foreach (var relative in new[]
        {
            "sm_base_flange_10mm.SLDPRT",
            "SheetMetalOptions/sm_l_d10.SLDPRT",
            "SheetMetalOptions/sm_two_base_flanges.SLDPRT"
        })
        {
            using var file = SwFile.Open(SamplePath(relative), readOnly: true);
            byte[] raw = (byte[])file.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            ResolvedFeaturesModel.Map(doc, new ResolvedFeaturesModel());
            Assert.Equal(raw, doc.SourceData);
        }
    }

    [Fact]
    public void UnknownFlatPatternLayoutsAreRejected()
    {
        // after_fillet uses the now-decoded inline 0x66 layout; the strict
        // family tuple is covered by FlatPatternInline066Tests. Synthetic
        // unknown layouts below must still return null.
        using (var afterFillet = SwFile.Open(SamplePath("sm_base_flange_after_fillet.SLDPRT"), readOnly: true))
        {
            var folder = Assert.Single(afterFillet.ResolvedFeatures.Folders,
                f => f.ClassName == "moFlatPattern_c");
            Assert.Equal(0x66u, folder.Header.TypeId);
            var data = Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);
            Assert.Equal(0x66u, data.TypeId);
        }

        // TypeId 0x5D but no moFaceRef_c/moCompFace_c selection.
        var missingFaces = BuildSyntheticFlatPatternStream(0x5D);
        var doc = MfcDocument.Parse(missingFaces);
        var element = Assert.Single(doc.AllElements, e => e.ClassName == "moFlatPattern_c");
        Assert.True(SheetMetalFormingParsers.IsFlatPatternNode(element));
        Assert.Null(SheetMetalFormingParsers.ParseFlatPattern(doc, element, missingFaces.Length));

        // Catalog candidate 0x62 is not an observed flat-pattern TypeId.
        var wrongType = BuildSyntheticFlatPatternStream(0x62);
        var wrongDoc = MfcDocument.Parse(wrongType);
        var wrongElement = Assert.Single(wrongDoc.AllElements, e => e.ClassName == "moFlatPattern_c");
        Assert.False(SheetMetalFormingParsers.IsFlatPatternNode(wrongElement));
        Assert.Null(SheetMetalFormingParsers.ParseFlatPattern(wrongDoc, wrongElement, wrongType.Length));

        // Wrong stream version is rejected before any field is exposed.
        var badVersion = BuildSyntheticFlatPatternStream(0x5D);
        BitConverter.GetBytes((ushort)0x72).CopyTo(badVersion, 0);
        var badDoc = MfcDocument.Parse(badVersion);
        var badElement = Assert.Single(badDoc.AllElements, e => e.ClassName == "moFlatPattern_c");
        Assert.Null(SheetMetalFormingParsers.ParseFlatPattern(badDoc, badElement, badVersion.Length));
    }

    [Fact]
    public void FlatPatternPayloadsHaveNoUnmappedFieldVariation()
    {
        // 0x5D payloads (four base-flange samples) differ only in a 4-byte session
        // id at 0xBF..0xC2; the unresolved flag/corner fields are byte-identical.
        var a = FlatPayload("sm_base_flange_10mm.SLDPRT");
        var b = FlatPayload("sm_base_flange_20mm.SLDPRT");
        Assert.Equal(a.Length, b.Length);
        foreach (int i in Differences(a, b))
            Assert.InRange(i, 0xBF, 0xC2);

        // 0x5E payloads additionally carry a geometry-derived double at
        // 0x12D..0x134 (it changes with the base-flange bend geometry, not with an
        // IFlatPatternFeatureData option). No other bytes vary.
        var c = FlatPayload("SheetMetalOptions/sm_l_d10.SLDPRT");
        var d = FlatPayload("SheetMetalOptions/sm_l_k03_fixed.SLDPRT");
        Assert.Equal(c.Length, d.Length);
        foreach (int i in Differences(c, d))
            Assert.True((i >= 0xBF && i <= 0xC2) || (i >= 0x12D && i <= 0x134), $"unexpected diff at 0x{i:X}");

        // The option block lives outside the moFlatPattern_c payload, anchored at
        // the end of moFaceRef_c; it resolves the defaults without changing the
        // class payload bytes compared above.
        using var file = SwFile.Open(SamplePath("SheetMetalOptions/sm_l_d10.SLDPRT"), readOnly: true);
        var definition = Assert.IsType<FlatPatternFeatureModel>(
            file.ResolvedFeatures.Folders.Single(f => f.ClassName == "moFlatPattern_c").FeatureData);
        Assert.True(definition.MergeFace ?? false);
        Assert.True(definition.SimplifyBends ?? false);
        Assert.True(definition.CornerTreatment ?? false);
        Assert.Equal(0, definition.BreakCornerType ?? -1);
        Assert.Equal(0.005, definition.BreakCornerRadius!.Value, 12);
        Assert.Equal(0, definition.CornerTrimReliefType ?? -1);
        Assert.Equal(0.005, definition.CornerTrimReliefDistance!.Value, 12);
        Assert.Null(definition.CornerTrimRatioToThickness);
        Assert.Null(definition.UseRatioToThickness);
        Assert.Null(definition.ExcludedFaces);
    }

    private static byte[] FlatPayload(string relative)
    {
        using var file = SwFile.Open(SamplePath(relative), readOnly: true);
        byte[] raw = file.RawStreams[ResolvedStream];
        var doc = MfcDocument.Parse(raw);
        var element = Assert.Single(doc.AllElements, e => e.ClassName == "moFlatPattern_c");
        return element.RawData;
    }

    private static IEnumerable<int> Differences(byte[] a, byte[] b)
    {
        for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
            if (a[i] != b[i]) yield return i;
    }

    private static byte[] BuildSyntheticFlatPatternStream(uint typeId)
    {
        var bytes = new List<byte> { 0x73, 0x00 };
        const string className = "moFlatPattern_c";
        var classBytes = Encoding.ASCII.GetBytes(className);
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00 });
        bytes.Add((byte)(classBytes.Length & 0xFF));
        bytes.Add((byte)(classBytes.Length >> 8));
        bytes.AddRange(classBytes);

        const string displayName = "FlatPattern1";
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, (byte)displayName.Length });
        bytes.AddRange(Encoding.Unicode.GetBytes(displayName));

        var header = new byte[90];
        BitConverter.GetBytes(typeId).CopyTo(header, 8); // f64 timestamp at 0, u32 TypeId at 8
        bytes.AddRange(header);
        return bytes.ToArray();
    }
}
