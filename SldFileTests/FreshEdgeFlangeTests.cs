using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Fresh single-edge EdgeFlange layout (ResolvedFeatures 0x74, TypeId 0x75)
/// recovered from the 2026-10-31 native edgepersist matrix. All samples were
/// created by InsertSheetMetalEdgeFlange2 on a simple L-shaped base flange and
/// saved/reopened natively; each variant changes one or two fields.
/// </summary>
public class FreshEdgeFlangeTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const int PrefixLength = 0x804B;

    private static string SamplePath(string fileName)
    {
        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalFormingOptions", "EdgeFlangeFresh", fileName);
        if (File.Exists(direct)) return direct;
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026",
                "SheetMetalFormingOptions", "EdgeFlangeFresh", fileName);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"EdgeFlangeFresh/{fileName} not found");
    }

    private static TempTestFile CreateStrippedBase()
        => CreateStrippedBase("edge_flange_fresh_baseline.SLDPRT", PrefixLength);

    private static TempTestFile CreateStrippedBase(string fileName, int prefixLength)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(fileName));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        byte[] stripped = new byte[prefixLength];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    [Fact]
    public void BaselineReadsNativeDefaults()
    {
        using var file = SwFile.Open(SamplePath("edge_flange_fresh_baseline.SLDPRT"), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moEdgeFlange_c");
        Assert.Equal(0x75u, folder.Header.TypeId);
        var def = Assert.IsType<EdgeFlangeFeatureDefinitionData>(folder.FeatureData);
        Assert.Equal(0x75u, def.TypeId);
        Assert.Equal(1, def.EdgeCount ?? -1);
        Assert.Equal(0.002, def.BendRadius!.Value, 12);
        Assert.Equal(Math.PI / 2.0, def.BendAngle!.Value, 12);
        Assert.Null(def.OffsetDistance);
        Assert.False(def.ParameterResolution.OffsetDistance);
        Assert.Equal(3, def.PositionType ?? -1);
        Assert.True(def.UseDefaultBendRadius ?? false);
        Assert.True(def.UseDefaultBendRelief ?? false);
        Assert.True(def.UseReliefRatio ?? false);
        Assert.Equal(0.5, def.ReliefRatio!.Value, 12);
        Assert.Equal(0.0005, def.ReliefWidth!.Value, 12);
        Assert.Equal(0.0005, def.ReliefDepth!.Value, 12);
        Assert.False(def.UsePositionOffset ?? true);
        Assert.Equal(0.01, def.PositionOffsetDistance!.Value, 12);
        Assert.Null(def.PositionOffsetType);
        Assert.False(def.ParameterResolution.PositionOffsetType);
        Assert.True(def.ParameterResolution.UseDefaultBendRadius);
        Assert.True(def.ParameterResolution.UseDefaultBendRelief);
        Assert.True(def.ParameterResolution.UseReliefRatio);
        Assert.True(def.ParameterResolution.UsePositionOffset);
        Assert.True(def.ParameterResolution.ReliefWidth);
        Assert.True(def.ParameterResolution.ReliefDepth);
    }

    public static System.Collections.Generic.IEnumerable<object[]> Variants()
    {
        yield return new object[] { "edge_flange_fresh_radius_003.SLDPRT", false, 0.003,
            true, true, 0.0005, 0.0005, false, 0.01 };
        yield return new object[] { "edge_flange_fresh_relief_both.SLDPRT", true, 0.002,
            false, false, 0.0015, 0.0025, false, 0.01 };
        yield return new object[] { "edge_flange_fresh_relief_default_only.SLDPRT", true, 0.002,
            false, true, 0.0005, 0.0005, false, 0.01 };
        yield return new object[] { "edge_flange_fresh_relief_ratio_only.SLDPRT", true, 0.002,
            true, true, 0.0005, 0.0005, false, 0.01 };
        yield return new object[] { "edge_flange_fresh_position_offset_2.SLDPRT", true, 0.002,
            true, true, 0.0005, 0.0005, true, 0.02 };
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void VariantsReadPersistedFields(string fileName, bool defaultRadius, double radius,
        bool defaultRelief, bool useRatio, double width, double depth, bool usePositionOffset,
        double positionOffsetDistance)
    {
        using var file = SwFile.Open(SamplePath(fileName), readOnly: true);
        var folder = Assert.Single(file.ResolvedFeatures.Folders,
            f => f.ClassName == "moEdgeFlange_c");
        var def = Assert.IsType<EdgeFlangeFeatureDefinitionData>(folder.FeatureData);
        Assert.Equal(defaultRadius, def.UseDefaultBendRadius ?? !defaultRadius);
        Assert.Equal(radius, def.BendRadius!.Value, 12);
        Assert.Equal(defaultRelief, def.UseDefaultBendRelief ?? !defaultRelief);
        Assert.Equal(useRatio, def.UseReliefRatio ?? !useRatio);
        Assert.Equal(width, def.ReliefWidth!.Value, 12);
        Assert.Equal(depth, def.ReliefDepth!.Value, 12);
        Assert.Equal(usePositionOffset, def.UsePositionOffset ?? !usePositionOffset);
        Assert.Equal(positionOffsetDistance, def.PositionOffsetDistance!.Value, 12);
        // PositionOffsetType=2 was requested natively but read back as 1 and is
        // not resolvable from this layout.
        Assert.Null(def.PositionOffsetType);
        Assert.False(def.ParameterResolution.PositionOffsetType);
    }

    [Fact]
    public void AuthoringDefaultsMatchNativeSource()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData());
            part.Save();
            Assert.Equal("边线-法兰2", feature.Name);
        }

        byte[] authored;
        using (var reopened = SwFile.Open(temp.FilePath, readOnly: true))
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        using var original = SwFile.Open(SamplePath("edge_flange_fresh_baseline.SLDPRT"), readOnly: true);
        Assert.Equal((byte[])original.RawStreams[ResolvedStream].Clone(), authored);
    }

    [Fact]
    public void AuthoringRoundTripsPersistedFields()
    {
        using var temp = CreateStrippedBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData
            {
                ReliefWidth = 0.0015,
                ReliefDepth = 0.0025,
                UseDefaultBendRelief = false,
                UseReliefRatio = false
            });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moEdgeFlange_c");
        var def = Assert.IsType<EdgeFlangeFeatureDefinitionData>(folder.FeatureData);
        Assert.True(def.UseDefaultBendRadius ?? false);
        Assert.Equal(0.002, def.BendRadius!.Value, 12);
        Assert.False(def.UseDefaultBendRelief ?? true);
        Assert.False(def.UseReliefRatio ?? true);
        Assert.Equal(0.0015, def.ReliefWidth!.Value, 12);
        Assert.Equal(0.0025, def.ReliefDepth!.Value, 12);
        Assert.False(def.UsePositionOffset ?? true);
        Assert.Equal(0.01, def.PositionOffsetDistance!.Value, 12);
    }

    [Fact]
    public void AuthoringRadiusTemplateRoundTrips()
    {
        using var temp = CreateStrippedBase("edge_flange_fresh_radius_003.SLDPRT", 0x815B);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData
            {
                UseDefaultBendRadius = false,
                BendRadius = 0.003,
                ReliefWidth = 0.0015,
                ReliefDepth = 0.0025,
                UseDefaultBendRelief = false,
                UseReliefRatio = false
            });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moEdgeFlange_c");
        var def = Assert.IsType<EdgeFlangeFeatureDefinitionData>(folder.FeatureData);
        Assert.False(def.UseDefaultBendRadius ?? true);
        Assert.Equal(0.003, def.BendRadius!.Value, 12);
        Assert.False(def.UseDefaultBendRelief ?? true);
        Assert.False(def.UseReliefRatio ?? true);
        Assert.Equal(0.0015, def.ReliefWidth!.Value, 12);
        Assert.Equal(0.0025, def.ReliefDepth!.Value, 12);
    }

    [Fact]
    public void AuthoringPositionOffsetTemplateRoundTrips()
    {
        using var temp = CreateStrippedBase("edge_flange_fresh_position_offset_2.SLDPRT", 0x8177);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData
            {
                ReliefWidth = 0.0015,
                ReliefDepth = 0.0025,
                UseDefaultBendRelief = false,
                UseReliefRatio = false,
                UsePositionOffset = true,
                PositionOffsetDistance = 0.02
            });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var folder = Assert.Single(reopened.ResolvedFeatures.Folders,
            f => f.ClassName == "moEdgeFlange_c");
        var def = Assert.IsType<EdgeFlangeFeatureDefinitionData>(folder.FeatureData);
        Assert.True(def.UseDefaultBendRadius ?? false);
        Assert.Equal(0.002, def.BendRadius!.Value, 12);
        Assert.False(def.UseDefaultBendRelief ?? true);
        Assert.False(def.UseReliefRatio ?? true);
        Assert.Equal(0.0015, def.ReliefWidth!.Value, 12);
        Assert.Equal(0.0025, def.ReliefDepth!.Value, 12);
        Assert.True(def.UsePositionOffset ?? false);
        Assert.Equal(0.02, def.PositionOffsetDistance!.Value, 12);
    }

    [Fact]
    public void AuthoringRejectsPositionOffsetType2WrongBaseAndTampering()
    {
        using (var temp = CreateStrippedBase())
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData
                {
                    PositionOffsetType = 2
                }));
            Assert.Contains("PositionOffsetType=2", error.Message);
        }

        using (var temp = TestFileProvider.CreateTempCopy(
                   SamplePath("edge_flange_fresh_baseline.SLDPRT")))
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() =>
                part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData()));
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
                    part.AddFreshEdgeFlange(new EdgeFlangeFeatureDefinitionData()));
                Assert.Contains("normalized prefix SHA-256", error.Message);
            }
        }
    }
}
