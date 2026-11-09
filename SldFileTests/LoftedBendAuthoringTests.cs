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
/// Fixed measured LoftedBend authoring from the controlled 0x72 specimen
/// (LoftedBendOptions/lofted_bend_2lines.SLDPRT, ResolvedFeatures version 0x74).
/// </summary>
public class LoftedBendAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0x6043;

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "LoftedBendOptions", fileName);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles",
                "Features2026", "LoftedBendOptions", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"LoftedBendOptions/{fileName} not found");
    }

    private static TempTestFile CreateStrippedBase(ushort? versionOverride = null)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath("lofted_bend_2lines.SLDPRT"));
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

    private static LoftedBendFeatureModel DefineMeasured()
        => new()
        {
            Thickness = 0.001,
            NumberOfBendLines = 2,
            ProfileCount = 2,
            Direction = false,
            BendLineControlOption = 0,
            MaximumDeviation = 0.0005,
            ReferToEndPoint = false,
            FacetingOption = 0,
            FacetValue = 0.001,
            FormedMethod = false,
            SymmetricThickness = false
        };

    [Fact]
    public void AuthoringReconstructsNativeLoftedBend()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddLoftedBend(DefineMeasured());
            Assert.Equal("LoftedBend", feature.Name);
            Assert.Equal(FeatureKind.LoftedBend, feature.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moLoftedBend_c");
        Assert.Equal(0x72u, folder.Header.TypeId);
        var parsed = Assert.IsType<LoftedBendFeatureModel>(folder.FeatureData);
        Assert.Equal(0.001, parsed.Thickness!.Value, 12);
        Assert.Equal(2, parsed.NumberOfBendLines ?? -1);
        Assert.True(parsed.ParameterResolution.Thickness);
        Assert.True(parsed.ParameterResolution.NumberOfBendLines);
        Assert.Null(parsed.Profiles);
    }

    [Fact]
    public void AuthoredStreamMatchesNativeBytes()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddLoftedBend();
            part.Save();
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath("lofted_bend_2lines.SLDPRT"), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Fact]
    public void StrippedBaseReadsAsIntermediateWithoutLoftedBend()
    {
        using var temp = CreateStrippedBase();
        using var sw = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Empty(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moLoftedBend_c"));
        Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
        Assert.Single(sw.ResolvedFeatures.Folders.Where(f => f.ClassName == "moProfileFeature_c"));
        Assert.Equal((ushort)0x74, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 0));
        Assert.Equal((ushort)26, BitConverter.ToUInt16(sw.RawStreams[ResolvedStream], 4));
    }

    [Fact]
    public void ResavePreservesAuthoredLoftedBend()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddLoftedBend();
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
        var definition = Assert.IsType<LoftedBendFeatureModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moLoftedBend_c").FeatureData);
        Assert.Equal(0.001, definition.Thickness!.Value, 12);
        Assert.Equal(2, definition.NumberOfBendLines ?? -1);
    }

    [Fact]
    public void ResaveSampleMatchesNativeResaveShape()
    {
        // The native resave moved the D14 record but kept the same values and
        // TypeId; the authored stream must stay parseable after a second save.
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddLoftedBend();
            part.Save();
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal((ushort)0x74,
            BitConverter.ToUInt16(reopened.RawStreams[ResolvedStream], 0));
        Assert.Equal(0x72u, Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moLoftedBend_c").Header.TypeId);
    }

    public static IEnumerable<object[]> UnsupportedDefinitions()
    {
        LoftedBendFeatureModel Base() => new();

        var thickness = Base(); thickness.Thickness = 0.002;
        yield return new object[] { thickness, "Thickness" };
        var lines = Base(); lines.NumberOfBendLines = 3;
        yield return new object[] { lines, "NumberOfBendLines" };
        var zeroLines = Base(); zeroLines.NumberOfBendLines = 0;
        yield return new object[] { zeroLines, "NumberOfBendLines" };
        var profiles = Base();
        profiles.Profiles = new[] { new EntityReference { EntityId = 1 } };
        yield return new object[] { profiles, "Profiles" };
        var profileCount = Base(); profileCount.ProfileCount = 3;
        yield return new object[] { profileCount, "ProfileCount" };
        var direction = Base(); direction.Direction = true;
        yield return new object[] { direction, "Direction" };
        var control = Base(); control.BendLineControlOption = 1;
        yield return new object[] { control, "BendLineControlOption" };
        var deviation = Base(); deviation.MaximumDeviation = 0.001;
        yield return new object[] { deviation, "MaximumDeviation" };
        var endPoint = Base(); endPoint.ReferToEndPoint = true;
        yield return new object[] { endPoint, "ReferToEndPoint" };
        var faceting = Base(); faceting.FacetingOption = 1;
        yield return new object[] { faceting, "FacetingOption" };
        var facetValue = Base(); facetValue.FacetValue = 0.002;
        yield return new object[] { facetValue, "FacetValue" };
        var formed = Base(); formed.FormedMethod = true;
        yield return new object[] { formed, "FormedMethod" };
        var symmetric = Base(); symmetric.SymmetricThickness = true;
        yield return new object[] { symmetric, "SymmetricThickness" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedDefinitionsAreRejectedWithoutModifyingStream(
        LoftedBendFeatureModel definition, string property)
    {
        using var temp = CreateStrippedBase();
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddLoftedBend(definition));
            Assert.Contains(property, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void WrongBaseVersionNameReadOnlyAndRepeatAreRejected()
    {
        using (var temp = CreateStrippedBase(versionOverride: 0x73))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddLoftedBend());
            Assert.Contains("0x74", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("lofted_bend_2lines.SLDPRT")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddLoftedBend());
            Assert.Contains("stripped", error.Message);
        }

        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddLoftedBend(featureName: "LoftedBend2"));
            Assert.Contains("display name", error.Message);
        }

        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var part = Assert.IsType<SldPart>(sw);
            Assert.Throws<InvalidOperationException>(() => part.AddLoftedBend());
        }

        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddLoftedBend();
            var error = Assert.Throws<NotSupportedException>(() => part.AddLoftedBend());
            Assert.Contains("stripped", error.Message);
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
                raw[0x6033] ^= 0xFF;
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }

            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddLoftedBend());
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
                var error = Assert.Throws<NotSupportedException>(() => part.AddLoftedBend());
                Assert.Contains("SHA-256", error.Message);
            }
        }
    }
}
