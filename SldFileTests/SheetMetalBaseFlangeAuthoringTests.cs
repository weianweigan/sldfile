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
/// Authoring coverage for the first writable sheet-metal base-flange combination:
/// the measured open-L specimen (SheetMetalOptions/sm_l_d10, stream version 0x73),
/// a stripped sketch-only baseline, and thickness/radius/depth slot replacement.
/// </summary>
public class SheetMetalBaseFlangeAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string ConfigStream = "Contents/Config-0";

    private static string Sample(string folder, string fileName)
    {
        string relative = folder == "Features2026"
            ? Path.Combine("TestFiles", "Features2026", fileName)
            : Path.Combine("TestFiles", "Features2026", folder, fileName);

        var direct = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);
        if (File.Exists(direct)) return direct;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", relative);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"{folder}/{fileName} not found");
    }

    private static double ReadConfigThickness(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        Assert.True(SheetMetalConfigThicknessPatcher.TryReadThickness(
            file.RawStreams[ConfigStream], out double thickness));
        return thickness;
    }

    private static void AssertMeasuredSketchMarkers(byte[] raw)
    {
        byte[] expected = SheetMetalBaseFlangeSegments.SketchMarkerBytes;
        foreach (int offset in SheetMetalBaseFlangeSegments.SketchMarkerOffsets)
        {
            Assert.True(offset + expected.Length <= raw.Length);
            Assert.Equal(expected, raw.Skip(offset).Take(expected.Length).ToArray());
        }
    }

    private static string ControlledSamplePath()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "SheetMetalOptions", "sm_l_d10.SLDPRT");
        if (File.Exists(path)) return path;

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", "SheetMetalOptions", "sm_l_d10.SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException("SheetMetalOptions/sm_l_d10.SLDPRT not found");
    }

    private static List<FeatureFolder> Flatten(IReadOnlyList<FeatureFolder> folders)
    {
        var result = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> current)
        {
            foreach (var folder in current)
            {
                result.Add(folder);
                Walk(folder.Children);
            }
        }
        Walk(folders);
        return result;
    }

    private static TempTestFile CreateStrippedSketchPart(ushort? versionOverride = null)
        => CreateStrippedSketchPart(ControlledSamplePath(), versionOverride);

    private static TempTestFile CreateStrippedSketchPart(string samplePath, ushort? versionOverride = null)
    {
        var temp = TestFileProvider.CreateTempCopy(samplePath);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        var folders = Flatten(sw.ResolvedFeatures.Folders);
        var template = folders.First(f => f.ClassName == "moTemplateSheetMetal_c");
        int cut = template.TagOffset;

        byte[] stripped = new byte[cut];
        Array.Copy(raw, 0, stripped, 0, cut);
        if (versionOverride.HasValue)
            BitConverter.GetBytes(versionOverride.Value).CopyTo(stripped, 0);

        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static SheetMetalBaseFlangeModel AuthorAndReopen(
        SheetMetalBaseFlangeModel definition,
        out string featureName)
    {
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddSheetMetalBaseFlange(definition);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var featureFolder = Assert.Single(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
        return Assert.IsType<SheetMetalBaseFlangeModel>(featureFolder.FeatureData);
    }

    [Fact]
    public void StrippedBaselineOfflineValidation()
    {
        using var temp = CreateStrippedSketchPart();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var raw = reopened.RawStreams[ResolvedStream];

        Assert.Equal((ushort)0x73, BitConverter.ToUInt16(raw, 0));
        Assert.Empty(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moSMBaseFlange_c"));
        Assert.Empty(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moTemplateSheetMetal_c"));
        Assert.Single(reopened.ResolvedFeatures.Folders.Where(f => f.ClassName == "moProfileFeature_c"));

        var chain = MfcDocument.Parse(raw).AllElements.Single(e => e.ClassName == "moSketchChain_c");
        Assert.Equal(2, BitConverter.ToUInt16(raw, chain.DataStart));
        Assert.Equal(2u, BitConverter.ToUInt32(raw, chain.DataStart + 2));
        Assert.Equal(1u, BitConverter.ToUInt32(raw, chain.DataStart + 6));

        var line = MfcDocument.Parse(raw).AllElements.Single(e => e.ClassName == "sgLineHandle");
        using var sha = System.Security.Cryptography.SHA256.Create();
        string hash = BitConverter.ToString(
            sha.ComputeHash(raw, line.DataStart, line.DataEnd - line.DataStart)).Replace("-", "").ToLowerInvariant();
        Assert.Equal("b7d1b3a1104cc86b1cea310793cf777002db0517281d135a02de079b0ea87c23", hash);

        // The stripped baseline keeps the native header count and markers so that
        // appending the measured suffix restores the exact native topology.
        Assert.Equal((ushort)22, BitConverter.ToUInt16(raw, 4));
        AssertMeasuredSketchMarkers(raw);
    }

    [Theory]
    [InlineData(0.0015, 0.0025, 0.012)]
    [InlineData(0.0020, 0.0030, 0.020)]
    [InlineData(0.0010, 0.0020, 0.010)]
    public void AuthoringRoundtripsThicknessRadiusDepth(
        double thickness, double bendRadius, double depth)
    {
        var definition = new SheetMetalBaseFlangeModel
        {
            Thickness = thickness,
            BendRadius = bendRadius,
            Depth = depth,
            SketchName = "草图1"
        };

        var parsed = AuthorAndReopen(definition, out string featureName);

        Assert.Equal(SheetMetalBaseFlangeBuilder.DefaultFeatureName, featureName);
        Assert.Equal(thickness, parsed.Thickness, 12);
        Assert.Equal(bendRadius, parsed.BendRadius, 12);
        Assert.Equal(depth, parsed.Depth, 12);
        Assert.True(parsed.ParameterResolution.Thickness);
        Assert.True(parsed.ParameterResolution.BendRadius);
        Assert.True(parsed.ParameterResolution.Depth);
        Assert.Equal(0.5, parsed.KFactor, 12);
        Assert.Equal(0.5, parsed.ReliefRatio, 12);
        Assert.Equal(1u, parsed.AutoReliefType);
        Assert.False(parsed.ReverseThickness);
        Assert.False(parsed.ReverseDirection);
    }

    [Theory]
    [InlineData("sm_l_profile", "sm_l_d10.SLDPRT", 0.001)]
    [InlineData("sm_l_profile", "sm_l_t2.SLDPRT", 0.002)]
    [InlineData("Features2026", "sm_base_flange_10mm.SLDPRT", 0.001)]
    [InlineData("Features2026", "sm_base_flange_thick2.SLDPRT", 0.002)]
    [InlineData("SheetMetalOptions", "sm_l_d10.SLDPRT", 0.001)]
    public void NativeThicknessVariantsUseTheConfig0LengthParameter(
        string folder, string fileName, double expectedThickness)
    {
        string path = Sample(folder, fileName);
        using var file = SwFile.Open(path, readOnly: true);
        Assert.True(SheetMetalConfigThicknessPatcher.TryReadThickness(
            file.RawStreams[ConfigStream], out double thickness));
        Assert.Equal(expectedThickness, thickness, 12);
    }

    [Fact]
    public void AuthoringPatchesConfig0AuthoritativeThickness()
    {
        const double thickness = 0.0017, radius = 0.002, depth = 0.010;
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalBaseFlange(new SheetMetalBaseFlangeModel
            {
                Thickness = thickness,
                BendRadius = radius,
                Depth = depth,
                SketchName = "草图1"
            });
            part.Save();
        }

        Assert.Equal(thickness, ReadConfigThickness(temp.FilePath), 12);
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var parsed = Assert.IsType<SheetMetalBaseFlangeModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        Assert.Equal(thickness, parsed.Thickness, 12);
    }

    [Fact]
    public void AuthoredStreamContainsOneCompleteMeasuredSubsystem()
    {
        double thickness = 0.0015, radius = 0.0025, depth = 0.012;
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalBaseFlange(new SheetMetalBaseFlangeModel
            {
                Thickness = thickness,
                BendRadius = radius,
                Depth = depth,
                SketchName = "草图1"
            });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var raw = reopened.RawStreams[ResolvedStream];
        Assert.True(SheetMetalConfigThicknessPatcher.TryReadThickness(
            reopened.RawStreams[ConfigStream], out double configThickness));
        Assert.Equal(thickness, configThickness, 12);
        var doc = MfcDocument.Parse(raw);
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSMBaseFlange_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moTemplateSheetMetal_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompSheetmetalFeat_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moTemplateFlatPattern_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSheetMetal_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFlatPattern_c"));
        Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSketchChain_c"));

        // Header count is restored to the measured value; the 0x67 markers are preserved.
        Assert.Equal((ushort)22, BitConverter.ToUInt16(raw, 4));
        AssertMeasuredSketchMarkers(raw);

        // Every proven slot carries the new value.
        int suffixStart = raw.Length - SheetMetalBaseFlangeSegments.GetSuffix().Length;
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixTemplateRadiusSlots)
            Assert.Equal(radius, BitConverter.ToDouble(raw, suffixStart + slot), 12);
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixTemplateThicknessSlots)
            Assert.Equal(thickness, BitConverter.ToDouble(raw, suffixStart + slot), 12);
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixRadiusSlots)
            Assert.Equal(radius, BitConverter.ToDouble(raw, suffixStart + slot), 12);
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixDepthSlots)
            Assert.Equal(depth, BitConverter.ToDouble(raw, suffixStart + slot), 12);

        // Unproven newer-layout depth copies are preserved as lazy caches.
        foreach (int slot in SheetMetalBaseFlangeSegments.UnprovenLazyDepthCaches)
            Assert.Equal(0.010, BitConverter.ToDouble(raw, suffixStart + slot), 12);
    }

    [Fact]
    public void ResavePreservesAuthoredStreamAndDefinition()
    {
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddSheetMetalBaseFlange(new SheetMetalBaseFlangeModel
            {
                Thickness = 0.0017,
                BendRadius = 0.0022,
                Depth = 0.014,
                SketchName = "草图1"
            });
            part.Save();
        }

        byte[] before, beforeConfig;
        using (var second = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])second.RawStreams[ResolvedStream].Clone();
            beforeConfig = (byte[])second.RawStreams[ConfigStream].Clone();
            second.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        Assert.Equal(beforeConfig, reopened.RawStreams[ConfigStream]);
        var definition = Assert.IsType<SheetMetalBaseFlangeModel>(
            reopened.ResolvedFeatures.Folders.Single(f => f.ClassName == "moSMBaseFlange_c").FeatureData);
        Assert.Equal(0.0017, definition.Thickness, 12);
        Assert.Equal(0.0022, definition.BendRadius, 12);
        Assert.Equal(0.014, definition.Depth, 12);
    }

    public static IEnumerable<object[]> UnsupportedDefinitions()
    {
        SheetMetalBaseFlangeModel Base() => new()
        {
            Thickness = 0.0015,
            BendRadius = 0.0025,
            Depth = 0.012,
            SketchName = "草图1"
        };

        var reverseThickness = Base(); reverseThickness.ReverseThickness = true;
        yield return new object[] { reverseThickness, "ReverseThickness" };
        var reverseDirection = Base(); reverseDirection.ReverseDirection = true;
        yield return new object[] { reverseDirection, "ReverseDirection" };
        var both = Base(); both.BothDirections = true;
        yield return new object[] { both, "BothDirections" };
        var depth2 = Base(); depth2.DepthDirection2 = 0.005;
        yield return new object[] { depth2, "DepthDirection2" };
        var k = Base(); k.KFactor = 0.3;
        yield return new object[] { k, "KFactor" };
        var relief = Base(); relief.AutoReliefType = 2;
        yield return new object[] { relief, "AutoReliefType" };
        var ratio = Base(); ratio.ReliefRatio = 0.3;
        yield return new object[] { ratio, "ReliefRatio" };
        var width = Base(); width.ReliefWidth = 0.001;
        yield return new object[] { width, "ReliefWidth" };
        var depthRelief = Base(); depthRelief.ReliefDepth = 0.001;
        yield return new object[] { depthRelief, "ReliefDepth" };
        var defaultRelief = Base(); defaultRelief.UseDefaultBendRelief = false;
        yield return new object[] { defaultRelief, "UseDefaultBendRelief" };
        var gauge = Base(); gauge.UseGaugeTable = true;
        yield return new object[] { gauge, "UseGaugeTable" };
        var defaultRadius = Base(); defaultRadius.UseDefaultBendRadius = false;
        yield return new object[] { defaultRadius, "UseDefaultBendRadius" };
        var allowance = Base(); allowance.BendAllowanceType = 1;
        yield return new object[] { allowance, "BendAllowanceType" };
        var contours = Base(); contours.SelectedContours = new[] { new SketchContourReference() };
        yield return new object[] { contours, "SelectedContours" };
        var direction = Base(); direction.DirectionReference = new DirectionReference();
        yield return new object[] { direction, "DirectionReference" };
        var scope = Base(); scope.BodyScope = new BodyScopeData { BodyIds = new uint[] { 42 } };
        yield return new object[] { scope, "BodyScope" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedOptionsAreRejectedWithoutModifyingStream(
        SheetMetalBaseFlangeModel definition, string property)
    {
        using var temp = CreateStrippedSketchPart();
        byte[] before, beforeConfig;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
        {
            before = (byte[])read.RawStreams[ResolvedStream].Clone();
            beforeConfig = (byte[])read.RawStreams[ConfigStream].Clone();
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(definition));
            Assert.Contains(property, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        Assert.Equal(beforeConfig, reopened.RawStreams[ConfigStream]);
    }

    [Fact]
    public void MissingConfigThicknessParameterIsRejectedWithoutModifyingStream()
    {
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] config = (byte[])sw.RawStreams[ConfigStream].Clone();
            byte[] pattern = { 0xFF, 0xFE, 0xFF, 0x02, 0x9A, 0x53, 0xA6, 0x5E }; // "厚度"
            int offset = FindBytes(config, pattern);
            Assert.True(offset >= 0);
            config[offset + 7] = 0x5F; // corrupt the name; parameter can no longer be found
            sw.SetPendingStream(ConfigStream, config);
            sw.Save();
        }

        byte[] before, beforeConfig;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
        {
            before = (byte[])read.RawStreams[ResolvedStream].Clone();
            beforeConfig = (byte[])read.RawStreams[ConfigStream].Clone();
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }));
            Assert.Contains("Config-0", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        Assert.Equal(beforeConfig, reopened.RawStreams[ConfigStream]);
    }

    private static int FindBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    [Fact]
    public void NonControlledSketchIsRejectedWithoutModifyingStream()
    {
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            int chain = FindClassTag(raw, "moSketchChain_c");
            Assert.True(chain > 0);
            raw[chain + 6] = (byte)'9'; // corrupt the class tag
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }

        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }));
            Assert.Contains("moSketchChain_c", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Theory]
    [InlineData(0x70)]
    [InlineData(0x74)]
    [InlineData(0x75)]
    public void Non0x73StreamVersionIsRejectedWithoutModifyingStream(ushort version)
    {
        using var temp = CreateStrippedSketchPart(version);
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }));
            Assert.Contains("0x73", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void MissingSketchMarkerIsRejectedWithoutModifyingStream()
    {
        using var temp = CreateStrippedSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            raw[SheetMetalBaseFlangeSegments.SketchMarkerOffsets[0]] = 0x68;
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }

        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }));
            Assert.Contains("0x67", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void ExistingBaseFlangeIsRejectedWithoutModifyingStream()
    {
        using var temp = TestFileProvider.CreateTempCopy(ControlledSamplePath());
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }));
            Assert.Contains("still contains moSMBaseFlange_c", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void ClosedRectangleSketchIsRejectedWithoutModifyingStream()
    {
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "TestFiles", "Features2026", "sm_base_flange_10mm.SLDPRT");
        using var temp = CreateStrippedSketchPart(sample);
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }));
            Assert.Contains("entity ids [2,1]", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void CustomFeatureNameIsRejectedWithoutModifyingStream()
    {
        using var temp = CreateStrippedSketchPart();
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddSheetMetalBaseFlange(
                new SheetMetalBaseFlangeModel
                {
                    Thickness = 0.0015,
                    BendRadius = 0.0025,
                    Depth = 0.012,
                    SketchName = "草图1"
                }, featureName: "基体-法兰2"));
            Assert.Contains("display name", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    private static int FindClassTag(byte[] data, string className)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(className);
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }
}
