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
/// Controlled authoring coverage for the measured PatternOptions layouts:
/// linear two-direction (D1/D2 counts and spacings) and circular equal-spacing patterns.
/// </summary>
public class PatternAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "PatternOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedPatternBase(string sampleName)
    {
        string className = sampleName.StartsWith("lpattern") ? "moLPattern_c" : "moCirPattern_c";
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sampleName));

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var raw = sw.ResolvedFeatures.RawData!;

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
        var pattern = folders.FirstOrDefault(f => f.ClassName == className);
        Assert.True(pattern != null, "pattern folder not found");
        int cutOffset = pattern!.TagOffset;

        byte[] stripped = new byte[cutOffset];
        Array.Copy(raw, 0, stripped, 0, cutOffset);
        ushort folderCount = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);

        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static byte[] SliceElement(MfcDocument doc, MfcElement element, int expectedLength)
    {
        int start = element.DataStart;
        Assert.True(start >= 0 && start + expectedLength <= doc.SourceData.Length);
        return MfcPrimitives.CopyRange(doc.SourceData, start, start + expectedLength);
    }

    private static LinearPatternFeatureModel AuthorAndParseLinear(
        LinearPatternFeatureModel definition, out byte[] stream, out string featureName)
    {
        using var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15");
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddLinearPattern(definition);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moLPattern_c"));
        var parsed = PatternFeatureParser.ParseLinearPattern(
            doc, "moLPattern_c", 0x5D, element.TagOffset, stream.Length);
        Assert.NotNull(parsed);
        return parsed!;
    }

    private static CircularPatternFeatureModel AuthorAndParseCircular(
        CircularPatternFeatureModel definition, out byte[] stream, out string featureName)
    {
        using var temp = CreateStrippedPatternBase("cirpattern_4x90");
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddCircularPattern(definition);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCirPattern_c"));
        var parsed = PatternFeatureParser.ParseCircularPattern(
            doc, "moCirPattern_c", 0x63, element.TagOffset, stream.Length);
        Assert.NotNull(parsed);
        return parsed!;
    }

    [Fact]
    public void LinearPatternAuthoring_RoundtripsCountsAndSpacings()
    {
        var read = AuthorAndParseLinear(new LinearPatternFeatureModel
        {
            D1TotalInstances = 2,
            D1Spacing = 0.010,
            D2TotalInstances = 3,
            D2Spacing = 0.015
        }, out _, out string featureName);

        Assert.Equal(2, read.D1TotalInstances);
        Assert.Equal(0.010, read.D1Spacing!.Value, 12);
        Assert.Equal(3, read.D2TotalInstances);
        Assert.Equal(0.015, read.D2Spacing!.Value, 12);
        Assert.Equal(1, read.D1AxisType);
        Assert.Equal(1, read.D2AxisType);
        Assert.True(read.IsDirection2Specified());
        Assert.True(read.IsResolved(nameof(LinearPatternFeatureModel.D1Spacing)));
        Assert.Null(read.GeometryPattern);
        Assert.Null(read.PatternFeatureArray);
        Assert.Equal("线性阵列1", featureName);
    }

    [Fact]
    public void CircularPatternAuthoring_RoundtripsCountAndSpacing()
    {
        var read = AuthorAndParseCircular(new CircularPatternFeatureModel
        {
            TotalInstances = 4,
            Spacing = Math.PI / 2.0
        }, out _, out string featureName);

        Assert.Equal(4, read.TotalInstances);
        Assert.Equal(Math.PI / 2.0, read.Spacing!.Value, 12);
        Assert.True(read.EqualSpacing);
        Assert.Equal(1, read.AxisType);
        Assert.True(read.IsResolved(nameof(CircularPatternFeatureModel.Spacing)));
        Assert.Null(read.ReverseDirection);
        Assert.Null(read.PatternFeatureArray);
        Assert.Equal("圆周阵列1", featureName);
    }

    [Fact]
    public void LinearPatternAuthoring_WritesMeasuredSlots()
    {
        AuthorAndParseLinear(new LinearPatternFeatureModel
        {
            D1TotalInstances = 2,
            D1Spacing = 0.010,
            D2TotalInstances = 3,
            D2Spacing = 0.015
        }, out byte[] stream, out _);

        var doc = MfcDocument.Parse(stream);
        var integerParam = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moIntegerParameter_c"));
        byte[] raw = SliceElement(doc, integerParam, PatternSegments.LinearIntegerParameterLength);
        Assert.Equal(2u, BitConverter.ToUInt32(raw, PatternSegments.LinearD1CountValueSlot));
        Assert.Equal(3u, BitConverter.ToUInt32(raw, PatternSegments.LinearD2CountValueSlot));
        Assert.Equal(0.010, BitConverter.ToDouble(raw, PatternSegments.LinearD1SpacingValueSlot), 12);
        Assert.Equal(0.015, BitConverter.ToDouble(raw, PatternSegments.LinearD2SpacingValueSlot), 12);
        foreach (int slot in PatternSegments.LinearD1SpacingCopySlots)
            Assert.Equal(0.010, BitConverter.ToDouble(raw, slot), 12);
        foreach (int slot in PatternSegments.LinearD2SpacingCopySlots)
            Assert.Equal(0.015, BitConverter.ToDouble(raw, slot), 12);

        var displayDim = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moDisplayDim_c"));
        byte[] displayRaw = SliceElement(doc, displayDim, 665);
        Assert.Equal(0.010, BitConverter.ToDouble(displayRaw, PatternSegments.LinearDisplayDimSpacingSlot), 12);
    }

    [Fact]
    public void CircularPatternAuthoring_WritesMeasuredSlots()
    {
        double spacing = Math.PI / 2.0;
        AuthorAndParseCircular(new CircularPatternFeatureModel
        {
            TotalInstances = 4,
            Spacing = spacing
        }, out byte[] stream, out _);

        var doc = MfcDocument.Parse(stream);
        var compEdge = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompEdge_c"));
        byte[] compRaw = SliceElement(doc, compEdge, PatternSegments.CircularCompEdgeLength);
        Assert.Equal(spacing, BitConverter.ToDouble(compRaw, PatternSegments.CircularSpacingValueSlot), 12);
        foreach (int slot in PatternSegments.CircularSpacingCopySlots)
            Assert.Equal(spacing, BitConverter.ToDouble(compRaw, slot), 12);

        var angleParam = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moAngleParameter_c"));
        byte[] angleRaw = SliceElement(doc, angleParam, PatternSegments.CircularAngleParameterLength);
        Assert.Equal(spacing / 3.0, BitConverter.ToDouble(angleRaw, PatternSegments.CircularAngleValueSlot), 12);

        var integerParam = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moIntegerParameter_c"));
        byte[] intRaw = SliceElement(doc, integerParam, PatternSegments.CircularIntegerParameterLength);
        Assert.Equal(4u, BitConverter.ToUInt32(intRaw, PatternSegments.CircularCountValueSlot));
    }

    [Theory]
    [InlineData("lpattern_2dir_2x3_10_15", "moLPattern_c", "LinearPattern")]
    [InlineData("cirpattern_4x90", "moCirPattern_c", "CircularPattern")]
    public void PatternAuthoring_WithNativeValues_MatchesNativeFeatureBlock(
        string sampleName, string className, string nativeName)
    {
        byte[] native;
        using (var file = SwFile.Open(SamplePath(sampleName), readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();
        var nativeDoc = MfcDocument.Parse(native);
        var nativeElement = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == className));

        // Use the scalar values parsed back from the native sample so the authored doubles carry
        // exactly the native bit patterns.
        LinearPatternFeatureModel linear = null!;
        CircularPatternFeatureModel circular = null!;
        if (className == "moLPattern_c")
        {
            linear = PatternFeatureParser.ParseLinearPattern(
                nativeDoc, className, 0x5D, nativeElement.TagOffset, native.Length)!;
            Assert.NotNull(linear);
        }
        else
        {
            circular = PatternFeatureParser.ParseCircularPattern(
                nativeDoc, className, 0x63, nativeElement.TagOffset, native.Length)!;
            Assert.NotNull(circular);
        }

        byte[] authored;
        using (var temp = CreateStrippedPatternBase(sampleName))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                if (className == "moLPattern_c")
                {
                    part.AddLinearPattern(new LinearPatternFeatureModel
                    {
                        D1TotalInstances = linear.D1TotalInstances,
                        D1Spacing = linear.D1Spacing,
                        D2TotalInstances = linear.D2TotalInstances,
                        D2Spacing = linear.D2Spacing
                    }, nativeName);
                }
                else
                {
                    part.AddCircularPattern(new CircularPatternFeatureModel
                    {
                        TotalInstances = circular.TotalInstances,
                        Spacing = circular.Spacing
                    }, nativeName);
                }
                part.Save();
            }
            using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
            authored = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        }

        var authoredDoc = MfcDocument.Parse(authored);
        var authoredElement = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == className));
        Assert.Equal(
            MfcPrimitives.CopyRange(native, nativeElement.TagOffset, native.Length),
            MfcPrimitives.CopyRange(authored, authoredElement.TagOffset, authored.Length));
    }

    [Fact]
    public void LinearPatternAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15");
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddLinearPattern(new LinearPatternFeatureModel
            {
                D1TotalInstances = 2,
                D1Spacing = 0.010,
                D2TotalInstances = 3,
                D2Spacing = 0.015
            });
            part.Save();
        }

        byte[] before;
        using (var first = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])first.RawStreams[ResolvedStream].Clone();
            first.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        var definition = Assert.IsType<LinearPatternFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Pattern)).Model);
        Assert.Equal(2, definition.D1TotalInstances);
        Assert.Equal(0.010, definition.D1Spacing!.Value, 12);
        Assert.Equal(3, definition.D2TotalInstances);
        Assert.Equal(0.015, definition.D2Spacing!.Value, 12);
    }

    [Theory]
    [InlineData(1.0, 0.01, 2.0, 0.01)]
    [InlineData(2.0, 0.0, 3.0, 0.01)]
    [InlineData(2.0, -0.01, 3.0, 0.01)]
    [InlineData(2.0, 0.01, 0.0, 0.01)]
    [InlineData(2.0, 0.01, 3.0, double.NaN)]
    public void LinearPatternAuthoringRejectsInvalidCountsOrSpacings(
        double count1, double spacing1, double count2, double spacing2)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PatternFeatureBuilder.ValidateLinearDefinition(
            new LinearPatternFeatureModel
            {
                D1TotalInstances = (int)count1,
                D1Spacing = spacing1,
                D2TotalInstances = (int)count2,
                D2Spacing = spacing2
            }));
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(2.0, 0.0)]
    [InlineData(2.0, -1.0)]
    [InlineData(2.0, 2.0 * Math.PI + 0.1)]
    [InlineData(2.0, double.NaN)]
    public void CircularPatternAuthoringRejectsInvalidCountOrSpacing(double count, double spacing)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PatternFeatureBuilder.ValidateCircularDefinition(
            new CircularPatternFeatureModel
            {
                TotalInstances = (int)count,
                Spacing = spacing
            }));
    }

    [Fact]
    public void PatternAuthoringRejectsNonMeasuredGeometryParameters()
    {
        // The legacy fixed templates remain restricted to their native values; non-measured
        // combinations are accepted only on a matching measured native variant base.
        using (var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(new LinearPatternFeatureModel
            {
                D1TotalInstances = 3,
                D1Spacing = 0.012,
                D2TotalInstances = 4,
                D2Spacing = 0.018
            }));
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedPatternBase("cirpattern_4x90"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            Assert.Throws<NotSupportedException>(() => part.AddCircularPattern(new CircularPatternFeatureModel
            {
                TotalInstances = 6,
                Spacing = Math.PI / 3.0
            }));
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void PatternAuthoringRejectsUnverifiedFields()
    {
        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateLinearDefinition(
            new LinearPatternFeatureModel
            {
                D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015,
                GeometryPattern = true
            }));
        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateLinearDefinition(
            new LinearPatternFeatureModel
            {
                D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015,
                VarySketch = false
            }));
        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateLinearDefinition(
            new LinearPatternFeatureModel
            {
                D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015,
                D1Axis = new FeatureReferenceData { EntityId = 1 }
            }));
        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateLinearDefinition(
            new LinearPatternFeatureModel
            {
                D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015,
                SkippedItemArray = new uint[] { 1 }
            }));

        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateCircularDefinition(
            new CircularPatternFeatureModel
            {
                TotalInstances = 4, Spacing = Math.PI / 2, ReverseDirection = true
            }));
        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateCircularDefinition(
            new CircularPatternFeatureModel
            {
                TotalInstances = 4, Spacing = Math.PI / 2, Symmetric = false
            }));
        Assert.Throws<NotSupportedException>(() => PatternFeatureBuilder.ValidateCircularDefinition(
            new CircularPatternFeatureModel
            {
                TotalInstances = 4, Spacing = Math.PI / 2, Axis = new FeatureReferenceData { EntityId = 1 }
            }));
    }

    [Fact]
    public void PatternAuthoringRejectsWrongBaselineExistingFeatureAndStreamVersion()
    {
        // Linear definition on the circular base (and vice versa).
        using (var temp = CreateStrippedPatternBase("cirpattern_4x90"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(
                new LinearPatternFeatureModel
                {
                    D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015
                }));
            Assert.Contains("controlled PatternOptions base body", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddCircularPattern(
                new CircularPatternFeatureModel { TotalInstances = 4, Spacing = Math.PI / 2 }));
            Assert.Contains("controlled PatternOptions base body", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        // Existing pattern feature.
        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("lpattern_2dir_2x3_10_15")))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(
                new LinearPatternFeatureModel
                {
                    D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015
                }));
            Assert.Contains("existing", error.Message);
        }

        // Non-0x72 stream.
        using (var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15"))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                BitConverter.GetBytes((ushort)0x70).CopyTo(raw, 0);
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            using var sw2 = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw2);
            var error = Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(
                new LinearPatternFeatureModel
                {
                    D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015
                }));
            Assert.Contains("0x72", error.Message);
        }
    }

    [Fact]
    public void PatternAuthoringRejectsMissingChainAndMarker()
    {
        using (var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15"))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                int chainTag = FindAscii(raw, "moSketchChain_c");
                Assert.True(chainTag > 0);
                raw[chainTag] = (byte)'n';
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            AssertPatternAddRejected(temp, "moSketchChain_c");
        }

        using (var temp = CreateStrippedPatternBase("lpattern_2dir_2x3_10_15"))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                int corrupted = 0;
                for (int i = 0; i <= raw.Length - 4; i++)
                {
                    if (raw[i] == 0x67 && raw[i + 1] == 0 && raw[i + 2] == 0 && raw[i + 3] == 0)
                    {
                        raw[i] = 0x68;
                        corrupted++;
                    }
                }
                Assert.True(corrupted > 0);
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            AssertPatternAddRejected(temp, "0x67");
        }
    }

    private static void AssertPatternAddRejected(TempTestFile temp, string expectedMessage)
    {
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddLinearPattern(
                new LinearPatternFeatureModel
                {
                    D1TotalInstances = 2, D1Spacing = .01, D2TotalInstances = 3, D2Spacing = .015
                }));
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    private static int FindAscii(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
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
}
