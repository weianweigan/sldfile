using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// First controlled cross-bend SMNormalCut feature (ResolvedFeatures 0x75,
/// FolderHeader TypeId 0x87). Created natively from a base flange + edge flange
/// and a non-normal through-all rectangle cut.
/// </summary>
public class SMNormalCutFeatureTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0xE81B;

    private static string SamplePath()
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalFormingOptions", "SMNormalCutCrossBend",
            "sm_normal_cut_cross_bend.SLDPRT");
        if (File.Exists(direct)) return direct;
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026",
                "SheetMetalFormingOptions", "SMNormalCutCrossBend",
                "sm_normal_cut_cross_bend.SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException("SMNormalCutCrossBend sample not found");
    }

    [Fact]
    public void ReadsCrossBendNormalCutTypeId()
    {
        using var file = SwFile.Open(SamplePath(), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moSMNormalCut_c");
        Assert.Equal(0x87u, folder.Header.TypeId);
        var data = Assert.IsType<NormalCutFeatureDefinitionData>(folder.FeatureData);
        Assert.Equal(0x87u, data.TypeId);
        // Only one native sample exists; no field slot is claimed.
        Assert.Null(data.AutoPropagation);
        Assert.Null(data.OptimizeGeometry);
        Assert.Null(data.NormalCutParameters);
        Assert.False(data.ParameterResolution.AutoPropagation);
        Assert.False(data.ParameterResolution.OptimizeGeometry);
        Assert.False(data.ParameterResolution.NormalCutParameters);
    }

    [Fact]
    public void FixedTemplateAuthoringMatchesNativeSource()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            byte[] stripped = new byte[PrefixLength];
            Array.Copy(raw, 0, stripped, 0, stripped.Length);
            sw.SetPendingStream(ResolvedStream, stripped);
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddSMNormalCutFromTemplate();
            part.Save();
            Assert.Equal("SMNormalCut", feature.Name);
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Fact]
    public void TemplateRejectsWrongBaseNameAndTampering()
    {
        using (var temp = TestFileProvider.CreateTempCopy(SamplePath()))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddSMNormalCutFromTemplate());
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath()))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                byte[] stripped = new byte[PrefixLength];
                Array.Copy(raw, 0, stripped, 0, stripped.Length);
                stripped[0x100] ^= 0x01;
                sw.SetPendingStream(ResolvedStream, stripped);
                sw.Save();
            }
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() =>
                    part.AddSMNormalCutFromTemplate());
                Assert.Contains("normalized prefix SHA-256", error.Message);
            }
        }
    }
}
