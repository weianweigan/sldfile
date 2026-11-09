using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed measured FlatPattern authoring from the controlled 0x5D specimen
/// (sm_base_flange_10mm.SLDPRT, ResolvedFeatures version 0x73).
/// </summary>
public class FlatPatternAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0x7380;

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Features2026/{fileName} not found");
    }

    private static TempTestFile CreateStrippedBase(ushort? versionOverride = null)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath("sm_base_flange_10mm.SLDPRT"));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        byte[] stripped = new byte[PrefixLength];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        if (versionOverride.HasValue)
            BitConverter.GetBytes(versionOverride.Value).CopyTo(stripped, 0);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static FlatPatternFeatureModel AuthorAndReopen(out string featureName)
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddFlatPattern();
            featureName = feature.Name;
            Assert.Equal(FeatureKind.FlatPattern, feature.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders, f => f.ClassName == "moFlatPattern_c");
        return Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);
    }

    private static FlatPatternFeatureModel AuthorOptionsAndReopen(
        bool merge, bool simplify, bool corner, int breakType, double breakRadius,
        int trimType, double trimDistance, out string featureName)
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddFlatPattern(
                mergeFace: merge, simplifyBends: simplify, cornerTreatment: corner,
                breakCornerType: breakType, breakCornerRadius: breakRadius,
                cornerTrimReliefType: trimType, cornerTrimReliefDistance: trimDistance);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        return Assert.IsType<FlatPatternFeatureModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moFlatPattern_c").FeatureData);
    }

    [Theory]
    [InlineData(false, true, true, 0, 0.005, 0, 0.005)]
    [InlineData(true, false, true, 0, 0.005, 0, 0.005)]
    [InlineData(true, true, false, 0, 0.005, 0, 0.005)]
    [InlineData(true, true, true, 1, 0.002, 0, 0.005)]
    [InlineData(true, true, true, 0, 0.005, 1, 0.002)]
    [InlineData(true, true, true, 0, 0.005, 0, 2.0)]
    [InlineData(false, false, false, 1, 0.002, 1, 0.002)]
    public void ParameterizedOptionsRoundtrip(
        bool merge, bool simplify, bool corner, int breakType, double breakRadius,
        int trimType, double trimDistance)
    {
        var parsed = AuthorOptionsAndReopen(merge, simplify, corner, breakType,
            breakRadius, trimType, trimDistance, out string featureName);
        Assert.Equal("平板型式6", featureName);
        Assert.Equal(merge, parsed.MergeFace ?? !merge);
        Assert.Equal(simplify, parsed.SimplifyBends ?? !simplify);
        Assert.Equal(corner, parsed.CornerTreatment ?? !corner);
        Assert.Equal(breakType, parsed.BreakCornerType ?? -1);
        Assert.Equal(breakRadius, parsed.BreakCornerRadius!.Value, 12);
        Assert.Equal(trimType, parsed.CornerTrimReliefType ?? -1);
        Assert.Equal(trimDistance, parsed.CornerTrimReliefDistance!.Value, 12);
    }

    [Theory]
    [InlineData(false, true, true, 0, 0.005, 0, 0.005, "flat_merge_face_false.SLDPRT")]
    [InlineData(true, false, true, 0, 0.005, 0, 0.005, "flat_simplify_bends_false.SLDPRT")]
    [InlineData(true, true, false, 0, 0.005, 0, 0.005, "flat_corner_treatment_false.SLDPRT")]
    [InlineData(true, true, true, 1, 0.002, 0, 0.005, "flat_break_corner_chamfer.SLDPRT")]
    [InlineData(true, true, true, 0, 0.005, 1, 0.002, "flat_corner_trim_square.SLDPRT")]
    [InlineData(true, true, true, 0, 0.005, 0, 2.0, "flat_ratio_false_2.SLDPRT")]
    public void AuthoredOptionBlockMatchesNativeVariant(
        bool merge, bool simplify, bool corner, int breakType, double breakRadius,
        int trimType, double trimDistance, string nativeFile)
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFlatPattern(
                mergeFace: merge, simplifyBends: simplify, cornerTreatment: corner,
                breakCornerType: breakType, breakCornerRadius: breakRadius,
                cornerTrimReliefType: trimType, cornerTrimReliefDistance: trimDistance);
            part.Save();
        }

        byte[] authored = FlatOptionBlock(temp.FilePath);
        byte[] native = FlatOptionBlock(SamplePath("FlatPatternOptions/" + nativeFile));
        // The full block also contains the embedded source-file path length at
        // +0x1B, which differs between the base specimen and the variant file.
        Assert.Equal(native[0x21], authored[0x21]);
        Assert.Equal(native.AsSpan(0x37, 4).ToArray(), authored.AsSpan(0x37, 4).ToArray());
        Assert.Equal(native.AsSpan(0x3B, 8).ToArray(), authored.AsSpan(0x3B, 8).ToArray());
        Assert.Equal(native.AsSpan(0x43, 4).ToArray(), authored.AsSpan(0x43, 4).ToArray());
        Assert.Equal(native.AsSpan(0x47, 8).ToArray(), authored.AsSpan(0x47, 8).ToArray());
        Assert.Equal(native[0x57], authored[0x57]);
    }

    private static byte[] FlatOptionBlock(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        byte[] raw = file.RawStreams[ResolvedStream];
        var doc = MfcDocument.Parse(raw);
        var faceRef = Assert.Single(doc.AllElements, e => e.ClassName == "moFaceRef_c");
        int anchor = faceRef.DataEnd;
        return raw.AsSpan(anchor, 0x60).ToArray();
    }

    [Fact]
    public void AuthoringReconstructsMeasuredFlatPattern()
    {
        var parsed = AuthorAndReopen(out string featureName);
        Assert.Equal("平板型式6", featureName);

        Assert.NotNull(parsed.FixedFace);
        Assert.Equal(27136u, parsed.FixedFace!.EntityId);
        Assert.True(parsed.MergeFace ?? false);
        Assert.True(parsed.SimplifyBends ?? false);
        Assert.True(parsed.CornerTreatment ?? false);
        Assert.Equal(0, parsed.BreakCornerType ?? -1);
        Assert.Equal(0.005, parsed.BreakCornerRadius!.Value, 12);
        Assert.Equal(0, parsed.CornerTrimReliefType ?? -1);
        Assert.Equal(0.005, parsed.CornerTrimReliefDistance!.Value, 12);
        Assert.True(parsed.ParameterResolution.FixedFace);
        Assert.True(parsed.ParameterResolution.MergeFace);
        Assert.True(parsed.ParameterResolution.SimplifyBends);
        Assert.True(parsed.ParameterResolution.CornerTreatment);
    }

    [Fact]
    public void AuthoredStreamMatchesNativeBytes()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFlatPattern();
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath("sm_base_flange_10mm.SLDPRT"), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Fact]
    public void StrippedBaseReadsAsIntermediateWithoutFlatPattern()
    {
        using var temp = CreateStrippedBase();
        using var sw = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Empty(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moFlatPattern_c"));
        Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
        Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moTemplateFlatPattern_c"));
        Assert.Equal((ushort)0x73, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 0));
        Assert.Equal((ushort)22, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 4));
    }

    [Fact]
    public void ResavePreservesAuthoredFlatPattern()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFlatPattern();
            part.Save();
        }

        byte[] before;
        using (var second = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])second.RawStreams[ResolvedStream].Clone();
            second.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<FlatPatternFeatureModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moFlatPattern_c").FeatureData);
        Assert.True(definition.MergeFace ?? false);
    }

    public static IEnumerable<object[]> UnsupportedDefinitions()
    {
        FlatPatternFeatureModel Base() => new();

        var breakType = Base(); breakType.BreakCornerType = 2;
        yield return new object[] { breakType, "BreakCornerType" };
        var breakRadius = Base(); breakRadius.BreakCornerRadius = 0.0;
        yield return new object[] { breakRadius, "BreakCornerRadius" };
        var trimType = Base(); trimType.CornerTrimReliefType = 10;
        yield return new object[] { trimType, "CornerTrimReliefType" };
        var trimDistance = Base(); trimDistance.CornerTrimReliefDistance = 0.0;
        yield return new object[] { trimDistance, "CornerTrimReliefDistance" };
        var ratio = Base(); ratio.CornerTrimRatioToThickness = 2.0;
        yield return new object[] { ratio, "CornerTrimRatioToThickness" };
        var useRatio = Base(); useRatio.UseRatioToThickness = true;
        yield return new object[] { useRatio, "UseRatioToThickness" };
        var slit = Base(); slit.ShowSlitInCornerRelief = false;
        yield return new object[] { slit, "ShowSlitInCornerRelief" };
        var addTrim = Base(); addTrim.AddCornerTrim = true;
        yield return new object[] { addTrim, "AddCornerTrim" };
        var breakCorners = Base(); breakCorners.BreakCorners = true;
        yield return new object[] { breakCorners, "BreakCorners" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedOptionsAreRejectedWithoutModifyingStream(
        FlatPatternFeatureModel definition, string property)
    {
        using var temp = CreateStrippedBase();
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddFlatPattern(definition));
            Assert.Contains(property, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void WrongBaseVersionNameAndReadOnlyAreRejected()
    {
        using (var temp = CreateStrippedBase(versionOverride: 0x74))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddFlatPattern());
            Assert.Contains("0x73", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("sm_base_flange_10mm.SLDPRT")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddFlatPattern());
            Assert.Contains("stripped", error.Message);
        }

        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddFlatPattern(featureName: "FlatPattern2"));
            Assert.Contains("display name", error.Message);
        }

        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<InvalidOperationException>(() => part.AddFlatPattern());
        }
    }

    [Fact]
    public void TamperedMarkerOrPrefixIsRejected()
    {
        using (var temp = CreateStrippedBase())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x2281] ^= 0xFF;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddFlatPattern());
                Assert.Contains("0x67/marker", error.Message);
            }
        }

        using (var temp = CreateStrippedBase())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x100] ^= 0xFF;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddFlatPattern());
                Assert.Contains("SHA-256", error.Message);
            }
        }
    }
}
