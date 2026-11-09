using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Task 1.1 explicit-contour selection authoring on the measured SelectionAuthoring
/// dual-rectangle bases. The target document is the native sample stripped at its
/// moExtrusion_c tag, so the sketch and the complete chooser/trim suffix are retained.
/// </summary>
public class ExtrudeExplicitSelectionAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string OneContourSample = "select_one_of_two_contours";
    private const string TwoContourSample = "select_two_contours";
    private const string NativeFeatureName = "SelectionExtrude";

    private static readonly uint[] FirstContour = { 1, 4, 3, 2 };
    private static readonly uint[] SecondContour = { 5, 8, 7, 6 };

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "SelectionAuthoring", name + ".SLDPRT");

    private static TempTestFile CreateStrippedExplicitBase(string sampleName, out int chooserOffset, out int extrusionOffset)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sampleName));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
            var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
            chooserOffset = chooser.TagOffset;
            extrusionOffset = extrusion.TagOffset;

            byte[] stripped = new byte[extrusionOffset];
            Array.Copy(raw, 0, stripped, 0, extrusionOffset);
            ushort folderCount = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        return temp;
    }

    private static ExtrudeFeatureModel Definition(params uint[][] contours)
    {
        return new ExtrudeFeatureModel
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = contours
                .Select(ids => new SketchContourReference { EntityIndices = ids })
                .ToArray()
        };
    }

    public static System.Collections.Generic.IEnumerable<object[]> ContourCases()
    {
        yield return new object[] { OneContourSample, new uint[][] { FirstContour } };
        yield return new object[] { TwoContourSample, new uint[][] { FirstContour, SecondContour } };
    }

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
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

    [Theory]
    [MemberData(nameof(ContourCases))]
    public void ExplicitSelection_RoundTripsModeContoursAndDepth(string sampleName, uint[][] contours)
    {
        using var temp = CreateStrippedExplicitBase(sampleName, out _, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddExtrude(Definition(contours), featureName: "ExplicitExtrude");
            Assert.Equal(FeatureKind.Extrusion, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        Assert.Equal("ExplicitExtrude", feature.Name);
        var definition = Assert.IsType<ExtrudeFeatureModel>(feature.Model);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Equal(contours.Length, definition.SelectedContours.Count);
        for (int i = 0; i < contours.Length; i++)
            Assert.Equal(contours[i], definition.SelectedContours[i].EntityIndices.ToArray());

        byte[] stream = ReadStream(temp.FilePath);
        Assert.Equal(1, CountElements(stream, "moContourChooser_c"));
        Assert.Equal(1, CountElements(stream, "moExtrusion_c"));
        Assert.Equal((ushort)20, BitConverter.ToUInt16(stream, 4));
    }

    [Theory]
    [MemberData(nameof(ContourCases))]
    public void ExplicitSelection_WithNativeValues_ReproducesMeasuredFeatureBlock(
        string sampleName, uint[][] contours)
    {
        byte[] native = ReadStream(SamplePath(sampleName));
        var nativeDoc = MfcDocument.Parse(native);
        var nativeFeature = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] nativeFeatureBlock = MfcPrimitives.CopyRange(native, nativeFeature.TagOffset, native.Length);

        var magic = new byte[] { 0x41, 0x68, 0xDD, 0x01 };
        int magicOffset = FindBytes(nativeFeatureBlock, magic);
        Assert.True(magicOffset >= 0);
        byte[] expectedFeatureBlock = (byte[])nativeFeatureBlock.Clone();
        BitConverter.GetBytes(0xC28F06D1u).CopyTo(expectedFeatureBlock, magicOffset + 4);

        using var temp = CreateStrippedExplicitBase(sampleName, out int chooserOffset, out _);
        byte[] suffixSourcePath;
        byte[] expectedSelectionBlock;
        using (var baseFile = SwFile.Open(temp.FilePath, readOnly: true))
        {
            byte[] stripped = (byte[])baseFile.RawStreams[ResolvedStream].Clone();
            var strippedDoc = MfcDocument.Parse(stripped);
            var chooser = Assert.Single(strippedDoc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
            var suffix = SelectedSketchContourSuffixParser.Read(strippedDoc, chooser.TagOffset, stripped.Length);
            Assert.NotNull(suffix);
            suffixSourcePath = System.Text.Encoding.Unicode.GetBytes(suffix!.SourceDocumentPath!);
            expectedSelectionBlock = SketchSelectionWriter.BuildContourSelectionBlock(
                contours.Select(ids => (IReadOnlyList<uint>)ids).ToArray(), 1, suffix.SurfaceId, suffix,
                temp.FilePath);
        }

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition(contours), featureName: NativeFeatureName);
            part.Save();
        }

        byte[] authored = ReadStream(temp.FilePath);
        var authoredDoc = MfcDocument.Parse(authored);
        var authoredFeature = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] authoredFeatureBlock = MfcPrimitives.CopyRange(authored, authoredFeature.TagOffset, authored.Length);
        Assert.Equal(expectedFeatureBlock, authoredFeatureBlock);

        byte[] authoredWindow = MfcPrimitives.CopyRange(authored, chooserOffset, authoredFeature.TagOffset);
        Assert.Equal(expectedSelectionBlock, authoredWindow);
        Assert.True(FindBytes(authoredWindow, suffixSourcePath) < 0);
        byte[] targetUtf16 = System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(temp.FilePath));
        Assert.True(FindBytes(authoredWindow, targetUtf16) >= 0);
    }

    [Fact]
    public void ExplicitSelection_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedExplicitBase(TwoContourSample, out _, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition(FirstContour, SecondContour));
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
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Equal(2, definition.SelectedContours.Count);
        Assert.Equal(FirstContour, definition.SelectedContours[0].EntityIndices.ToArray());
        Assert.Equal(SecondContour, definition.SelectedContours[1].EntityIndices.ToArray());
    }

    [Fact]
    public void ExplicitSelection_RejectsWholeSketchBaseWithoutSuffix()
    {
        using var temp = TestFileProvider.CreateTempCopy(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT"));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
            byte[] stripped = new byte[extrusion.TagOffset];
            Array.Copy(raw, 0, stripped, 0, stripped.Length);
            ushort folderCount = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        AssertExplicitAddRejected(temp, Definition(FirstContour), "moContourChooser_c");
    }

    [Fact]
    public void ExplicitSelection_RejectsUnknownContourChains()
    {
        using var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _);
        AssertExplicitAddRejected(temp, Definition(new uint[] { 1, 2, 3, 4 }), "measured contour chain");
    }

    [Fact]
    public void ExplicitSelection_RejectsContourCountMismatchAndSurfaceMismatch()
    {
        using var one = CreateStrippedExplicitBase(OneContourSample, out _, out _);
        AssertExplicitAddRejected(one, Definition(FirstContour, SecondContour), "exposes 1 contour chains");

        using var two = CreateStrippedExplicitBase(TwoContourSample, out _, out _);
        AssertExplicitAddRejected(two, Definition(FirstContour), "requests 1");
    }

    [Fact]
    public void ExplicitSelection_RejectsRegionsCandidatesAndInvalidIds()
    {
        using var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _);
        AssertExplicitAddRejected(temp, new ExtrudeFeatureModel
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = FirstContour } },
            SelectedRegions = new[] { new SketchRegionReference { RegionIndex = 1 } }
        }, "SelectedRegions");

        AssertExplicitAddRejected(temp, new ExtrudeFeatureModel
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = FirstContour, IsCandidateOnly = true } }
        }, "Candidate-only");

        Assert.Throws<ArgumentException>(() => ExtrudeFeatureBuilder.ValidateDefinition(new ExtrudeFeatureModel
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = new uint[] { 1, 0, 3, 2 } } }
        }));
        Assert.Throws<ArgumentException>(() => ExtrudeFeatureBuilder.ValidateDefinition(new ExtrudeFeatureModel
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = new uint[] { 1, 4, 3, 1 } } }
        }));
    }

    [Fact]
    public void ExplicitSelection_RejectsUnsupportedOptionsAndDepth()
    {
        using var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _);
        AssertExplicitAddRejected(temp, new ExtrudeFeatureModel
        {
            Depth = 0.020,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = FirstContour } }
        }, "template depth");

        AssertExplicitAddRejected(temp, new ExtrudeFeatureModel
        {
            Depth = 0.010, IsCut = true,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = FirstContour } }
        }, "IsCut");

        AssertExplicitAddRejected(temp, new ExtrudeFeatureModel
        {
            Depth = 0.010, BothDirections = true, DepthDirection2 = 0.005,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = FirstContour } }
        }, "BothDirections");

        AssertExplicitAddRejected(temp, new ExtrudeFeatureModel
        {
            Depth = 0.010, ReverseDirection = true,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = FirstContour } }
        }, "ReverseDirection");
    }

    [Fact]
    public void ExplicitSelection_RejectsExistingExtrusionAndWrongVersion()
    {
        using (var full = TestFileProvider.CreateTempCopy(SamplePath(OneContourSample)))
        {
            using var sw = SwFile.Open(full.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(FirstContour)));
            Assert.Contains("extrusion", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x72).CopyTo(raw, 0));
            byte[] before = ReadStream(temp.FilePath);
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(FirstContour)));
                Assert.Contains("0x70/0x71", error.Message);
                part.Save();
            }
            Assert.Equal(before, ReadStream(temp.FilePath));
        }
    }

    [Fact]
    public void ExplicitSelection_RejectsMissingChainBrokenSuffixAndUncontrolledBase()
    {
        using (var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _))
        {
            RewriteStream(temp, raw =>
            {
                int chain = FindAscii(raw, "moSketchChain_c");
                Assert.True(chain > 0);
                raw[chain] = (byte)'n';
            });
            AssertExplicitAddRejected(temp, Definition(FirstContour), "moSketchChain_c");
        }

        using (var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _))
        {
            RewriteStream(temp, raw =>
            {
                int handle = FindAscii(raw, "moCStringHandle_c");
                Assert.True(handle > 0);
                int dataStart = handle + "moCStringHandle_c".Length;
                raw[dataStart] = 0x01;
            });
            AssertExplicitAddRejected(temp, Definition(FirstContour), "moCStringHandle_c");
        }

        using (var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _))
        {
            RewriteStream(temp, raw => raw[7000] ^= 0xFF);
            AssertExplicitAddRejected(temp, Definition(FirstContour), "prefix signature");
        }

        using (var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)20).CopyTo(raw, 4));
            AssertExplicitAddRejected(temp, Definition(FirstContour), "folder count");
        }
    }

    [Fact]
    public void ExplicitSelection_RejectsInvalidTargetDocumentPath()
    {
        using var temp = CreateStrippedExplicitBase(OneContourSample, out _, out _);
        byte[] stream = ReadStream(temp.FilePath);
        var doc = MfcDocument.Parse(stream);
        var folder = new FeatureFolder
        {
            ClassName = "moProfileFeature_c",
            DisplayName = "SelectionSeed",
            TagOffset = doc.AllElements.First(e => e.ClassName == "moProfileFeature_c").TagOffset
        };
        Assert.Throws<ArgumentException>(() => ExtrudeFeatureBuilder.BuildExtrusionStream(
            stream, Definition(FirstContour), folder, stream.Length, "ExplicitExtrude", "relative.SLDPRT"));
        Assert.Throws<ArgumentException>(() => ExtrudeFeatureBuilder.BuildExtrusionStream(
            stream, Definition(FirstContour), folder, stream.Length, "ExplicitExtrude", null));
    }

    private static void AssertExplicitAddRejected(
        TempTestFile temp, ExtrudeFeatureModel definition, string expectedMessage)
    {
        byte[] before = ReadStream(temp.FilePath);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(definition));
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }
        Assert.Equal(before, ReadStream(temp.FilePath));
    }

    private static void RewriteStream(TempTestFile temp, Action<byte[]> mutate)
    {
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        mutate(raw);
        sw.SetPendingStream(ResolvedStream, raw);
        sw.Save();
    }

    private static int CountElements(byte[] stream, string className)
    {
        var doc = MfcDocument.Parse(stream);
        return doc.AllElements.Count(e => e.ClassName == className);
    }

    private static int FindAscii(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        return FindBytes(data, pattern);
    }
}
