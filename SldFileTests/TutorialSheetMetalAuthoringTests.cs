using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fixed tutorial-template authoring for the first real Jog/Fold/UnFold samples
/// (2026-10-31 corpus scan). The authored ResolvedFeatures stream must be
/// byte-identical to the native tutorial document.
/// </summary>
public class TutorialSheetMetalAuthoringTests
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

    private static TempTestFile CreateStrippedBase(
        string fileName, int prefixLength, ushort? versionOverride = null)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(fileName));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        string streamPath = sw.RawStreams.Keys.FirstOrDefault(k =>
                                k.EndsWith("ResolvedFeatures", StringComparison.Ordinal))
                            ?? ResolvedStream;
        byte[] raw = (byte[])sw.RawStreams[streamPath].Clone();
        byte[] stripped = new byte[prefixLength];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        if (versionOverride.HasValue)
            BitConverter.GetBytes(versionOverride.Value).CopyTo(stripped, 0);
        sw.SetPendingStream(streamPath, stripped);
        sw.Save();
        return temp;
    }

    public static IEnumerable<object[]> Templates()
    {
        yield return new object[] { TutorialSheetMetalFeatureTemplate.JogComplexPart,
            "Complex Sheet Metal Part.SLDPRT", 0x5B7D3 };
        yield return new object[] { TutorialSheetMetalFeatureTemplate.UnFoldPaperAirplane,
            "Paper Airplane.SLDPRT", 0x197F6 };
        yield return new object[] { TutorialSheetMetalFeatureTemplate.FoldPaperAirplane,
            "Paper Airplane.SLDPRT", 0x1F914 };
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void AuthoredStreamMatchesNativeSource(
        TutorialSheetMetalFeatureTemplate template, string fileName, int prefixLength)
    {
        using var temp = CreateStrippedBase(fileName, prefixLength);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddSheetMetalTutorialFeature(template);
            part.Save();
            Assert.Equal(template switch
            {
                TutorialSheetMetalFeatureTemplate.JogComplexPart => "Jog1",
                TutorialSheetMetalFeatureTemplate.UnFoldPaperAirplane => "Unfold2",
                _ => "Fold2"
            }, feature.Name);
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStreamPath(reopened)].Clone();
        using var original = SwFile.Open(SamplePath(fileName), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStreamPath(original)].Clone(), authored);
    }

    [Fact]
    public void AuthoredJogReadsMeasuredParameters()
    {
        using var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x5B7D3);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.JogComplexPart);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moJog_c");
        var definition = Assert.IsType<JogFeatureDefinitionData>(folder.FeatureData);
        Assert.Equal(0x159u, definition.TypeId);
        Assert.Equal(0.00254, definition.BendRadius!.Value, precision: 12);
        Assert.Equal(1.5707963267949, definition.JogAngle!.Value, precision: 12);
        Assert.Equal(0.0127, definition.OffsetDistance!.Value, precision: 12);
    }

    [Fact]
    public void AuthoredFoldAndUnfoldReadBendsCount()
    {
        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x197F6))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.UnFoldPaperAirplane);
                part.Save();
            }

            using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
            var definition = Assert.IsType<UnFoldFeatureDefinitionData>(
                reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moUnFold_c").FeatureData);
            Assert.Equal(3, definition.BendsCount ?? -1);
            Assert.Equal(0x144u, definition.TypeId);
        }

        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x1F914))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.FoldPaperAirplane);
                part.Save();
            }

            using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
            var definition = Assert.IsType<FoldFeatureDefinitionData>(
                reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moFold_c").FeatureData);
            Assert.Equal(3, definition.BendsCount ?? -1);
            Assert.Equal(0x153u, definition.TypeId);
        }
    }

    private static string ResolvedStreamPath(SwFile file) =>
        file.RawStreams.Keys.First(k => k.EndsWith("ResolvedFeatures", StringComparison.Ordinal));

    public static IEnumerable<object[]> FlatPatternTemplates()
    {
        yield return new object[] { TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane,
            "Paper Airplane.SLDPRT", 0x2EE35, "Flat-Pattern2", 0x72u, true };
        yield return new object[] { TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart,
            "Complex Sheet Metal Part.SLDPRT", 0x7E9E1, "Flat-Pattern1", 0x1Eu, true };
        yield return new object[] { TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled,
            "drive sled.sldprt", 0xCD0F9, "Flat-Pattern1", 0x164u, false };
    }

    [Theory]
    [MemberData(nameof(FlatPatternTemplates))]
    public void AuthoredFlatPatternStreamMatchesNativeSource(
        TutorialSheetMetalFeatureTemplate template, string fileName, int prefixLength,
        string displayName, uint typeId, bool flagsResolved)
    {
        using var temp = CreateStrippedBase(fileName, prefixLength);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddSheetMetalTutorialFeature(template);
            part.Save();
            Assert.Equal(displayName, feature.Name);
            Assert.Equal(FeatureKind.FlatPattern, feature.Kind);
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
        {
            authored = (byte[])reopened.RawStreams[ResolvedStreamPath(reopened)].Clone();
            var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
                f => f.ClassName == "moFlatPattern_c");
            var definition = Assert.IsType<FlatPatternFeatureDefinitionData>(folder.FeatureData);
            Assert.Equal(typeId, definition.TypeId);
            Assert.True(definition.SimplifyBends ?? false);
            Assert.Equal(0, definition.BreakCornerType ?? -1);
            Assert.Equal(0.005, definition.BreakCornerRadius!.Value, 12);
            Assert.Equal(0, definition.CornerTrimReliefType ?? -1);
            Assert.Equal(0.005, definition.CornerTrimReliefDistance!.Value, 12);
            Assert.Equal(0.005, definition.CornerTrimRatioToThickness!.Value, 12);
            Assert.False(definition.UseRatioToThickness ?? true);
            Assert.True(definition.ShowSlitInCornerRelief ?? false);
            if (flagsResolved)
            {
                Assert.True(definition.MergeFace ?? false);
                Assert.True(definition.CornerTreatment ?? false);
            }
            else
            {
                Assert.Null(definition.MergeFace);
                Assert.Null(definition.CornerTreatment);
            }
        }
        using var original = SwFile.Open(SamplePath(fileName), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStreamPath(original)].Clone(), authored);
    }

    [Theory]
    [MemberData(nameof(FlatPatternTemplates))]
    public void ResavePreservesAuthoredFlatPatternFeature(
        TutorialSheetMetalFeatureTemplate template, string fileName, int prefixLength,
        string displayName, uint typeId, bool flagsResolved)
    {
        using var temp = CreateStrippedBase(fileName, prefixLength);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalTutorialFeature(template);
            part.Save();
        }

        byte[] before;
        using (var second = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])second.RawStreams[ResolvedStreamPath(second)].Clone();
            second.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStreamPath(reopened)]);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moFlatPattern_c");
        Assert.Equal(typeId, Assert.IsType<FlatPatternFeatureDefinitionData>(folder.FeatureData).TypeId);
        var reopenedPart = Assert.IsType<SldPart>(reopened);
        Assert.Contains(reopenedPart.Features,
            f => f.Kind == FeatureKind.FlatPattern && f.Name == displayName);
        _ = flagsResolved;
    }

    [Fact]
    public void FlatPatternWrongBaseNameAndTamperingAreRejected()
    {
        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x197F6))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(
                    TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane));
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x2EE35))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(
                    TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane, "Flat-Pattern1"));
            Assert.Contains("display name", error.Message);
        }

        using (var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x7E9E1,
                   versionOverride: 0x73))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(
                    TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart));
            Assert.Contains("0x1AA", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("drive sled.sldprt")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(
                    TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled));
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x2EE35))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                raw[0x100] ^= 0x01;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() =>
                    part.AddSheetMetalTutorialFeature(
                        TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane));
                Assert.Contains("normalized prefix SHA-256", error.Message);
            }
        }
    }

    [Fact]
    public void StrippedBaseReadsAsIntermediate()
    {
        using (var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x5B7D3))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Empty(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moJog_c"));
            Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
            Assert.Equal((ushort)0x1AA, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 0));
            Assert.Equal((ushort)50, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 4));
        }

        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x197F6))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.Empty(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moUnFold_c"));
            Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
            Assert.Equal((ushort)0x155, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 0));
            Assert.Equal((ushort)38, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 4));
        }
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void ResavePreservesAuthoredTutorialFeature(
        TutorialSheetMetalFeatureTemplate template, string fileName, int prefixLength)
    {
        using var temp = CreateStrippedBase(fileName, prefixLength);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalTutorialFeature(template);
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
    }

    [Fact]
    public void WrongBaseVersionNameAndTamperingAreRejected()
    {
        // Jog template against the UnFold Paper base (different length).
        using (var temp = CreateStrippedBase("Paper Airplane.SLDPRT", 0x197F6))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.JogComplexPart));
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x5B7D3))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(
                    TutorialSheetMetalFeatureTemplate.JogComplexPart, "Jog2"));
            Assert.Contains("display name", error.Message);
        }

        using (var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x5B7D3,
                   versionOverride: 0x73))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.JogComplexPart));
            Assert.Contains("0x1AA", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("Complex Sheet Metal Part.SLDPRT")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.JogComplexPart));
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x5B7D3))
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
                var error = Assert.Throws<NotSupportedException>(() =>
                    part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.JogComplexPart));
                Assert.Contains("SHA-256", error.Message);
            }
        }

        using (var temp = CreateStrippedBase("Complex Sheet Metal Part.SLDPRT", 0x5B7D3))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<InvalidOperationException>(() =>
                part.AddSheetMetalTutorialFeature(TutorialSheetMetalFeatureTemplate.JogComplexPart));
        }
    }
}
