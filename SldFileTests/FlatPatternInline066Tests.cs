using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// FlatPattern 0x66 inline layout (ResolvedFeatures 0x74, folder count 25) on
/// sm_base_flange_after_fillet.SLDPRT. The option slots were proven by native
/// single-variable probes: flags/MergeFace/CornerTreatment, trim type and shared
/// distance/ratio double, break type/radius, UseRatioToThickness, SimplifyBends
/// and ShowSlitInCornerRelief. FixedFace2 and ExcludedFaces did not persist
/// independently and stay unresolved.
/// </summary>
public class FlatPatternInline066Tests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath()
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "sm_base_flange_after_fillet.SLDPRT");
        if (File.Exists(direct)) return direct;
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026",
                "sm_base_flange_after_fillet.SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException("sm_base_flange_after_fillet.SLDPRT not found");
    }

    private static int FlatPatternTag(byte[] raw)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes("moFlatPattern_c");
        for (int i = 0; i <= raw.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (raw[i + j] != pattern[j]) { match = false; break; }
            if (match) return i - 6; // FF FF 01 00 + u16 length
        }
        throw new InvalidOperationException("moFlatPattern_c tag not found");
    }

    private static FlatPatternFeatureModel ParseWithMutation(Action<byte[], int> mutate)
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            mutate(raw, FlatPatternTag(raw));
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moFlatPattern_c");
        Assert.Equal(0x66u, folder.Header.TypeId);
        return Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);
    }

    [Fact]
    public void BaselineReadsNativeDefaults()
    {
        using var file = SwFile.Open(SamplePath(), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moFlatPattern_c");
        var data = Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);
        Assert.Equal(0x66u, data.TypeId);
        Assert.True(data.MergeFace ?? false);
        Assert.True(data.CornerTreatment ?? false);
        Assert.True(data.SimplifyBends ?? false);
        Assert.Equal(0, data.BreakCornerType ?? -1);
        Assert.Equal(0.005, data.BreakCornerRadius!.Value, 12);
        Assert.Equal(0, data.CornerTrimReliefType ?? -1);
        Assert.Equal(0.005, data.CornerTrimReliefDistance!.Value, 12);
        Assert.Equal(0.005, data.CornerTrimRatioToThickness!.Value, 12);
        Assert.False(data.UseRatioToThickness ?? true);
        Assert.True(data.ShowSlitInCornerRelief ?? false);
        Assert.Null(data.FixedFace);
        Assert.Null(data.FixedFace2);
        Assert.Null(data.ExcludedFaces);
        Assert.False(data.ParameterResolution.FixedFace);
        Assert.False(data.ParameterResolution.FixedFace2);
    }

    [Fact]
    public void OptionSlotsMatchNativeProbeVariants()
    {
        var data = ParseWithMutation((raw, tag) =>
        {
            raw[tag + 0xDF7] = 0x03;                    // MergeFace true, CornerTreatment false
            BitConverter.GetBytes(1u).CopyTo(raw, tag + 0xE0D);
            BitConverter.GetBytes(0.002).CopyTo(raw, tag + 0xE11);
            BitConverter.GetBytes(1u).CopyTo(raw, tag + 0xE19);
            BitConverter.GetBytes(0.002).CopyTo(raw, tag + 0xE1D);
            raw[tag + 0xE29] = 1;                       // UseRatioToThickness true
            raw[tag + 0xE2D] = 0;                       // SimplifyBends false
            raw[tag + 0xE31] = 0;                       // ShowSlit false
        });
        Assert.True(data.MergeFace ?? false);
        Assert.False(data.CornerTreatment ?? true);
        Assert.Equal(1, data.CornerTrimReliefType ?? -1);
        Assert.Equal(0.002, data.CornerTrimReliefDistance!.Value, 12);
        Assert.Equal(0.002, data.CornerTrimRatioToThickness!.Value, 12);
        Assert.Equal(1, data.BreakCornerType ?? -1);
        Assert.Equal(0.002, data.BreakCornerRadius!.Value, 12);
        Assert.True(data.UseRatioToThickness ?? false);
        Assert.False(data.SimplifyBends ?? true);
        Assert.False(data.ShowSlitInCornerRelief ?? true);
        Assert.True(data.ParameterResolution.CornerTrimReliefDistance);
        Assert.True(data.ParameterResolution.CornerTrimRatioToThickness);
        Assert.True(data.ParameterResolution.UseRatioToThickness);
        Assert.True(data.ParameterResolution.ShowSlitInCornerRelief);
    }

    [Fact]
    public void GateTamperingReturnsNull()
    {
        using var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            raw[FlatPatternTag(raw) + 0xDF7] = 0x04;
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moFlatPattern_c");
        Assert.Null(folder.FeatureData);
    }

    private static TempTestFile CreateStrippedBase()
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath());
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        byte[] stripped = new byte[0x9B02];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    [Fact]
    public void AuthoringDefaultsMatchNativeSource()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddFlatPatternInline066();
            part.Save();
            Assert.Equal("平板型式7", feature.Name);
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath(), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Fact]
    public void AuthoringPatchesProvenOptionSlots()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFlatPatternInline066(new FlatPatternFeatureModel
            {
                MergeFace = true,
                CornerTreatment = false,
                SimplifyBends = false,
                BreakCornerType = 1,
                BreakCornerRadius = 0.002,
                CornerTrimReliefType = 1,
                CornerTrimReliefDistance = 0.002,
                CornerTrimRatioToThickness = 0.002,
                UseRatioToThickness = true,
                ShowSlitInCornerRelief = false
            });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moFlatPattern_c");
        var data = Assert.IsType<FlatPatternFeatureModel>(folder.FeatureData);
        Assert.True(data.MergeFace ?? false);
        Assert.False(data.CornerTreatment ?? true);
        Assert.False(data.SimplifyBends ?? true);
        Assert.Equal(1, data.BreakCornerType ?? -1);
        Assert.Equal(0.002, data.BreakCornerRadius!.Value, 12);
        Assert.Equal(1, data.CornerTrimReliefType ?? -1);
        Assert.Equal(0.002, data.CornerTrimReliefDistance!.Value, 12);
        Assert.Equal(0.002, data.CornerTrimRatioToThickness!.Value, 12);
        Assert.True(data.UseRatioToThickness ?? false);
        Assert.False(data.ShowSlitInCornerRelief ?? true);
    }

    [Fact]
    public void AuthoringRejectsDistinctDistanceRatioWrongBaseAndTampering()
    {
        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddFlatPatternInline066(new FlatPatternFeatureModel
                {
                    CornerTrimReliefDistance = 0.003,
                    CornerTrimRatioToThickness = 2.0
                }));
            Assert.Contains("must be equal", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(SamplePath()))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddFlatPatternInline066());
            Assert.Contains("stripped base", error.Message);
        }

        using (var temp = CreateStrippedBase())
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
                    part.AddFlatPatternInline066());
                Assert.Contains("normalized prefix SHA-256", error.Message);
            }
        }
    }
}
