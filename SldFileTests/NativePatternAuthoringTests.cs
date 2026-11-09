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
/// Measured native linear/circular pattern variants (BossSeed base lineage). Authoring uses the
/// matching native stripped base so the other OLE streams already contain the correct instance
/// geometry; the feature block must be byte-identical to that native sample.
/// </summary>
public class NativePatternAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "PatternOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, out byte[] nativeBlock)
    {
        string className = sample.StartsWith("lpattern") ? "moLPattern_c" : "moCirPattern_c";
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = sw.ResolvedFeatures.RawData!;
        var folders = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> current)
        {
            foreach (var folder in current)
            {
                folders.Add(folder);
                Walk(folder.Children);
            }
        }
        Walk(sw.ResolvedFeatures.Folders);
        var pattern = folders.First(f => f.ClassName == className);
        nativeBlock = raw.AsSpan(pattern.TagOffset).ToArray();
        byte[] stripped = raw.AsSpan(0, pattern.TagOffset).ToArray();
        ushort count = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static LinearPatternFeatureModel ReadLinear(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        return Assert.IsType<LinearPatternFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Pattern)).Model);
    }

    private static CircularPatternFeatureModel ReadCircular(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        return Assert.IsType<CircularPatternFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Pattern)).Model);
    }

    private static void AssertBlockEquals(string sample, Action<SldPart> add)
    {
        using var temp = CreateStrippedBase(sample, out byte[] nativeBlock);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            add(Assert.IsType<SldPart>(sw));

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        byte[] stream = reopened.RawStreams[ResolvedStream];
        var doc = MfcDocument.Parse((byte[])stream.Clone());
        var pattern = Assert.Single(doc.AllElements.Where(e =>
            e.ClassName == "moLPattern_c" || e.ClassName == "moCirPattern_c"));
        Assert.Equal(nativeBlock, stream.AsSpan(pattern.TagOffset).ToArray());
        Assert.Equal(23, BitConverter.ToUInt16(stream, 4));
    }

    [Theory]
    [InlineData("lpattern_native_2dir_2x3_10_15", 2, 3, 0.010, 0.015)]
    [InlineData("lpattern_native_2dir_2x3_10_15_resave", 2, 3, 0.010, 0.015)]
    [InlineData("lpattern_1dir_3x15", 3, 1, 0.015, 0.015)]
    [InlineData("lpattern_1dir_3x15_resave", 3, 1, 0.015, 0.015)]
    [InlineData("lpattern_2dir_2x3_20_25", 2, 3, 0.020, 0.025)]
    [InlineData("lpattern_2dir_2x3_20_25_resave", 2, 3, 0.020, 0.025)]
    [InlineData("lpattern_geometry_on", 2, 3, 0.010, 0.015)]
    [InlineData("lpattern_geometry_on_resave", 2, 3, 0.010, 0.015)]
    [InlineData("lpattern_geometry_off", 2, 3, 0.010, 0.015)]
    [InlineData("lpattern_geometry_off_resave", 2, 3, 0.010, 0.015)]
    public void NativeLinear_ReadingVariants(
        string sample, int d1Count, int d2Count, double d1Spacing, double d2Spacing)
    {
        var d = ReadLinear(sample);
        Assert.Equal(d1Count, d.D1TotalInstances);
        Assert.Equal(d2Count, d.D2TotalInstances);
        Assert.Equal(d1Spacing, d.D1Spacing!.Value, 12);
        Assert.Equal(d2Spacing, d.D2Spacing!.Value, 12);
        Assert.Equal(1, d.D1AxisType);
        Assert.Null(d.GeometryPattern);
        Assert.False(d.IsResolved(nameof(d.GeometryPattern)));
    }

    [Theory]
    [InlineData("cirpattern_native_4x90", 4, Math.PI / 2, true)]
    [InlineData("cirpattern_native_4x90_resave", 4, Math.PI / 2, true)]
    [InlineData("cirpattern_6x60", 6, Math.PI / 3, true)]
    [InlineData("cirpattern_6x60_resave", 6, Math.PI / 3, true)]
    [InlineData("cirpattern_equal_on", 4, Math.PI / 2, true)]
    [InlineData("cirpattern_equal_on_resave", 4, Math.PI / 2, true)]
    [InlineData("cirpattern_equal_off", 4, Math.PI / 2, false)]
    [InlineData("cirpattern_equal_off_resave", 4, Math.PI / 2, false)]
    public void NativeCircular_ReadingVariants(
        string sample, int count, double spacing, bool equal)
    {
        var d = ReadCircular(sample);
        Assert.Equal(count, d.TotalInstances);
        Assert.Equal(spacing, d.Spacing!.Value, 12);
        Assert.Equal(equal, d.EqualSpacing);
        Assert.Equal(1, d.AxisType);
        Assert.Null(d.GeometryPattern);
        Assert.False(d.IsResolved(nameof(d.GeometryPattern)));
    }

    [Fact]
    public void NativeCircularParser_RejectsTamperedFields()
    {
        byte[] original;
        using (var file = SwFile.Open(SamplePath("cirpattern_6x60"), readOnly: true))
            original = (byte[])file.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "moCirPattern_c");
        Assert.NotNull(PatternFeatureParser.ParseCircularPattern(
            doc, "moCirPattern_c", 0x63, feature.TagOffset, original.Length));

        void Tamper(Action<byte[], MfcDocument> mutate)
        {
            byte[] copy = (byte[])original.Clone();
            var copyDoc = MfcDocument.Parse(copy);
            mutate(copy, copyDoc);
            var tampered = MfcDocument.Parse(copy);
            var f = tampered.AllElements.First(e => e.ClassName == "moCirPattern_c");
            Assert.Null(PatternFeatureParser.ParseCircularPattern(
                tampered, "moCirPattern_c", 0x63, f.TagOffset, copy.Length));
        }

        Tamper((b, d) =>
        {
            var integer = d.AllElements.First(e => e.ClassName == "moIntegerParameter_c");
            BitConverter.GetBytes(3u).CopyTo(b, integer.DataStart + 0x20);
        });
        Tamper((b, d) =>
        {
            var edge = d.AllElements.First(e => e.ClassName == "moCompEdge_c");
            int pos = FindBytes(b, edge.DataStart, Math.Min(b.Length, edge.DataStart + 0x600),
                BitConverter.GetBytes(Math.PI / 3.0));
            Assert.True(pos > 0);
            BitConverter.GetBytes(0.5).CopyTo(b, pos);
        });
        Tamper((b, d) =>
        {
            var angle = d.AllElements.First(e => e.ClassName == "moAngleParameter_c");
            BitConverter.GetBytes(0.4).CopyTo(b, angle.DataStart + 0x20);
        });
        Tamper((b, d) =>
        {
            var edge = d.AllElements.First(e => e.ClassName == "moCompEdge_c");
            int d6 = FindBytes(b, edge.DataStart, Math.Min(b.Length, edge.DataStart + 0xC00),
                new byte[] { (byte)'D', 0, (byte)'6', 0 });
            Assert.True(d6 > 0);
            b[d6] = (byte)'X';
        });
    }

    private static int FindBytes(byte[] data, int start, int end, byte[] pattern)
    {
        for (int i = start; i + pattern.Length <= end; i++)
        {
            bool ok = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }

    [Theory]
    [InlineData("lpattern_native_2dir_2x3_10_15")]
    [InlineData("lpattern_1dir_3x15")]
    [InlineData("lpattern_2dir_2x3_20_25")]
    [InlineData("lpattern_geometry_on")]
    [InlineData("lpattern_geometry_off")]
    public void NativeLinear_AuthoringReproducesNativeBlock(string sample)
    {
        var read = ReadLinear(sample);
        AssertBlockEquals(sample, part =>
        {
            var feature = part.AddLinearPattern(new LinearPatternFeatureModel
            {
                D1TotalInstances = read.D1TotalInstances,
                D1Spacing = read.D1Spacing,
                D2TotalInstances = read.D2TotalInstances,
                D2Spacing = read.D2Spacing
            });
            Assert.Equal("LinearPattern", feature.Name);
            part.Save();
        });
    }

    [Theory]
    [InlineData("cirpattern_native_4x90")]
    [InlineData("cirpattern_6x60")]
    [InlineData("cirpattern_equal_on")]
    [InlineData("cirpattern_equal_off")]
    public void NativeCircular_AuthoringReproducesNativeBlock(string sample)
    {
        var read = ReadCircular(sample);
        AssertBlockEquals(sample, part =>
        {
            var feature = part.AddCircularPattern(new CircularPatternFeatureModel
            {
                TotalInstances = read.TotalInstances,
                Spacing = read.Spacing,
                EqualSpacing = read.EqualSpacing
            });
            Assert.Equal("CircularPattern", feature.Name);
            part.Save();
        });
    }

    [Theory]
    [InlineData("lpattern_1dir_3x15")]
    [InlineData("lpattern_2dir_2x3_20_25")]
    [InlineData("cirpattern_6x60")]
    [InlineData("cirpattern_equal_off")]
    public void NativePattern_ReadbackMatchesNativeValues(string sample)
    {
        if (sample.StartsWith("lpattern"))
        {
            var d = ReadLinear(sample);
            Assert.True(d.IsResolved(nameof(LinearPatternFeatureModel.D1TotalInstances)));
            Assert.True(d.IsResolved(nameof(LinearPatternFeatureModel.D2TotalInstances)));
            Assert.Null(d.GeometryPattern);
        }
        else
        {
            var d = ReadCircular(sample);
            Assert.True(d.IsResolved(nameof(CircularPatternFeatureModel.TotalInstances)));
            Assert.True(d.IsResolved(nameof(CircularPatternFeatureModel.EqualSpacing)));
            Assert.Null(d.GeometryPattern);
        }
    }

    [Fact]
    public void NativePattern_RejectsMismatchedValuesAndCustomNames()
    {
        using (var temp = CreateStrippedBase("lpattern_1dir_3x15", out _))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            byte[] before = (byte[])part.RawStreams[ResolvedStream].Clone();
            Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(
                new LinearPatternFeatureModel
                {
                    D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015
                }));
            Assert.Equal(before, part.RawStreams[ResolvedStream]);
            Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(
                new LinearPatternFeatureModel
                {
                    D1TotalInstances = 3, D1Spacing = .015, D2TotalInstances = 1, D2Spacing = .015
                }, "Custom"));
        }

        using (var temp = CreateStrippedBase("cirpattern_equal_off", out _))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            byte[] before = (byte[])part.RawStreams[ResolvedStream].Clone();
            Assert.Throws<NotSupportedException>(() => part.AddCircularPattern(
                new CircularPatternFeatureModel
                {
                    TotalInstances = 4, Spacing = Math.PI / 2, EqualSpacing = true
                }));
            Assert.Equal(before, part.RawStreams[ResolvedStream]);
            Assert.Throws<NotSupportedException>(() => part.AddCircularPattern(
                new CircularPatternFeatureModel
                {
                    TotalInstances = 4, Spacing = Math.PI / 2, EqualSpacing = false
                }, "Custom"));
        }
    }
}
