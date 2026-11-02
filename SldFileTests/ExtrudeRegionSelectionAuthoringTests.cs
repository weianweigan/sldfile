using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using SldFile.Sketches;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled region-selection extrusion authoring on the measured SelectionAuthoring
/// L-shape (6 edges) and overlap-rectangle (4 edges) bases. The target document is the
/// native sample stripped at its moExtrusion_c tag, retaining the sketch, the complete
/// region context and the region chooser.
/// </summary>
public class ExtrudeRegionSelectionAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string LShapeSample = "select_region";
    private const string OverlapSample = "select_region_overlap";
    private const string NativeFeatureName = "SelectionExtrude";

    private static readonly uint[] LShapeEdges = { 3, 4, 5, 6, 7, 8 };
    private static readonly uint[] OverlapEdges = { 3, 4, 5, 6 };

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "SelectionAuthoring", name + ".SLDPRT");

    public static System.Collections.Generic.IEnumerable<object[]> RegionCases()
    {
        yield return new object[] { LShapeSample, LShapeEdges };
        yield return new object[] { OverlapSample, OverlapEdges };
    }

    private static TempTestFile CreateStrippedRegionBase(
        string sampleName, out int contextStart, out int extrusionOffset)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sampleName));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
            var sketch = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
            var template = RegionChooserTemplate.Capture(doc, sketch.TagOffset, extrusion.TagOffset);
            Assert.NotNull(template);
            Assert.NotEmpty(template!.ContextTags);
            contextStart = template.ContextTags[0].TagOffset;
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

    private static ExtrudeFeatureDefinitionData Definition(uint[] edges, uint loopCount = 0)
    {
        return new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedRegions = new[]
            {
                new SketchRegionReference { EdgeIds = edges, LoopCount = loopCount }
            }
        };
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
    [MemberData(nameof(RegionCases))]
    public void RegionSelection_RoundTripsModeRegionAndDepth(string sampleName, uint[] edges)
    {
        using var temp = CreateStrippedRegionBase(sampleName, out _, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddExtrude(Definition(edges), featureName: "ExplicitRegion");
            Assert.Equal(FeatureKind.Extrusion, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        Assert.Equal("ExplicitRegion", feature.Name);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Equal(0.010, definition.Depth, 12);
        Assert.Empty(definition.SelectedContours);
        var region = Assert.Single(definition.SelectedRegions);
        Assert.Equal(edges.OrderBy(x => x), region.EdgeIds.OrderBy(x => x));

        byte[] stream = ReadStream(temp.FilePath);
        Assert.Equal(1, CountElements(stream, "moContourChooser_c"));
        Assert.Equal(1, CountElements(stream, "moExtrusion_c"));
        Assert.Equal((ushort)20, BitConverter.ToUInt16(stream, 4));
    }

    [Theory]
    [MemberData(nameof(RegionCases))]
    public void RegionSelection_WithNativeValues_ReproducesMeasuredContextAndFeatureBlock(
        string sampleName, uint[] edges)
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

        using var temp = CreateStrippedRegionBase(sampleName, out int contextStart, out _);
        RegionChooserTemplate template;
        SketchFeature sketch;
        byte[] nativeWindow;
        using (var baseFile = SwFile.Open(temp.FilePath, readOnly: true))
        {
            var part = Assert.IsType<SldPart>(baseFile);
            byte[] stripped = (byte[])part.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(stripped);
            var sketchFolder = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
            template = RegionChooserTemplate.Capture(doc, sketchFolder.TagOffset, stripped.Length)!;
            Assert.NotNull(template);
            sketch = Assert.Single(part.Sketches.Where(s => s.Name == "SelectionSeed"));
            nativeWindow = MfcPrimitives.CopyRange(stripped, contextStart, stripped.Length);
        }
        // Same-document re-emission must reproduce the native context+chooser window byte-for-byte.
        byte[] sameDocumentContext = SketchSelectionWriter.BuildRegionSelectionContextFromSketch(
            edges, template, sketch, template.SourceDocumentPath);
        Assert.Equal(nativeWindow, sameDocumentContext);

        byte[] expectedContext = SketchSelectionWriter.BuildRegionSelectionContextFromSketch(
            edges, template, sketch, temp.FilePath);
        byte[] sourcePath = System.Text.Encoding.Unicode.GetBytes(template.SourceDocumentPath!);

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition(edges), featureName: NativeFeatureName);
            part.Save();
        }

        byte[] authored = ReadStream(temp.FilePath);
        var authoredDoc = MfcDocument.Parse(authored);
        var authoredFeature = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] authoredFeatureBlock = MfcPrimitives.CopyRange(authored, authoredFeature.TagOffset, authored.Length);
        Assert.Equal(expectedFeatureBlock, authoredFeatureBlock);

        byte[] authoredContext = MfcPrimitives.CopyRange(authored, contextStart, authoredFeature.TagOffset);
        Assert.Equal(expectedContext, authoredContext);
        Assert.True(FindBytes(authoredContext, sourcePath) < 0);
        byte[] targetPath = System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(temp.FilePath));
        Assert.True(FindBytes(authoredContext, targetPath) >= 0);
    }

    [Fact]
    public void RegionSelection_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedRegionBase(LShapeSample, out _, out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddExtrude(Definition(LShapeEdges));
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
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Equal(LShapeEdges, Assert.Single(definition.SelectedRegions).EdgeIds.OrderBy(x => x));
    }

    [Fact]
    public void RegionSelection_RejectsContourBaseAndWrongEdgeSets()
    {
        using (var contourBase = CreateStrippedContourBase())
            AssertRegionAddRejected(contourBase, Definition(LShapeEdges), "region context");

        using var temp = CreateStrippedRegionBase(LShapeSample, out _, out _);
        AssertRegionAddRejected(temp, Definition(new uint[] { 1, 2, 3, 4, 5, 6 }), "measured boundary");
        AssertRegionAddRejected(temp, Definition(new uint[] { 3, 4, 5, 6, 7 }), "4-edge and 6-edge");
        AssertRegionAddRejected(temp, Definition(new uint[] { 3, 3, 4, 5, 6, 7 }), "non-zero and distinct");
    }

    [Fact]
    public void RegionSelection_RejectsMultipleRegionsHolesAndMixedSelections()
    {
        using var temp = CreateStrippedRegionBase(LShapeSample, out _, out _);
        AssertRegionAddRejected(temp, new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedRegions = new[]
            {
                new SketchRegionReference { EdgeIds = LShapeEdges },
                new SketchRegionReference { EdgeIds = OverlapEdges }
            }
        }, "exactly one");

        AssertRegionAddRejected(temp, Definition(LShapeEdges, loopCount: 2), "holes");

        AssertRegionAddRejected(temp, new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedContours = new[] { new SketchContourReference { EntityIndices = new uint[] { 1, 4, 3, 2 } } },
            SelectedRegions = new[] { new SketchRegionReference { EdgeIds = LShapeEdges } }
        }, "not both");
    }

    [Fact]
    public void RegionSelection_RejectsUnsupportedOptionsDepthAndNonControlledBase()
    {
        using var temp = CreateStrippedRegionBase(LShapeSample, out _, out _);
        AssertRegionAddRejected(temp, new ExtrudeFeatureDefinitionData
        {
            Depth = 0.020,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedRegions = new[] { new SketchRegionReference { EdgeIds = LShapeEdges } }
        }, "template depth");

        AssertRegionAddRejected(temp, new ExtrudeFeatureDefinitionData
        {
            Depth = 0.010, BothDirections = true, DepthDirection2 = 0.005,
            SketchSelectionMode = SketchSelectionMode.ExplicitSelection,
            SelectedRegions = new[] { new SketchRegionReference { EdgeIds = LShapeEdges } }
        }, "BothDirections");

        using (var altered = CreateStrippedRegionBase(LShapeSample, out _, out _))
        {
            RewriteStream(altered, raw => raw[7000] ^= 0xFF);
            AssertRegionAddRejected(altered, Definition(LShapeEdges), "base signature");
        }

        using (var altered = CreateStrippedRegionBase(LShapeSample, out _, out _))
        {
            RewriteStream(altered, raw => BitConverter.GetBytes((ushort)20).CopyTo(raw, 4));
            AssertRegionAddRejected(altered, Definition(LShapeEdges), "folder count");
        }

        using (var resave = CreateStrippedRegionBase("select_region_resave", out _, out _))
            AssertRegionAddRejected(resave, Definition(LShapeEdges), "base signature");
    }

    [Fact]
    public void RegionSelection_RejectsExistingExtrusionWrongVersionAndBrokenContext()
    {
        using (var full = TestFileProvider.CreateTempCopy(SamplePath(LShapeSample)))
        {
            using var sw = SwFile.Open(full.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddExtrude(Definition(LShapeEdges)));
            Assert.Contains("extrusion", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedRegionBase(LShapeSample, out _, out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x72).CopyTo(raw, 0));
            AssertRegionAddRejected(temp, Definition(LShapeEdges), "0x70/0x71");
        }

        using (var broken = CreateStrippedRegionBase(LShapeSample, out _, out _))
        {
            RewriteStream(broken, raw =>
            {
                int handle = FindAscii(raw, "moCStringHandle_c");
                Assert.True(handle > 0);
                raw[handle + "moCStringHandle_c".Length] = 0x01;
            });
            AssertRegionAddRejected(broken, Definition(LShapeEdges), "moCStringHandle_c");
        }
    }

    [Fact]
    public void RegionSelection_RejectsMissingSketchGeometryAndInvalidTargetPath()
    {
        using var temp = CreateStrippedRegionBase(LShapeSample, out _, out _);
        byte[] stream = ReadStream(temp.FilePath);
        var doc = MfcDocument.Parse(stream);
        var sketchFolder = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
        var folder = new FeatureFolder
        {
            ClassName = "moProfileFeature_c",
            DisplayName = "SelectionSeed",
            TagOffset = sketchFolder.TagOffset
        };
        var error = Assert.Throws<NotSupportedException>(() => ExtrudeFeatureBuilder.BuildExtrusionStream(
            stream, Definition(LShapeEdges), folder, stream.Length, "ExplicitRegion",
            temp.FilePath, targetSketch: null));
        Assert.Contains("geometry", error.Message);

        Assert.Throws<ArgumentException>(() => ExtrudeFeatureBuilder.BuildExtrusionStream(
            stream, Definition(LShapeEdges), folder, stream.Length, "ExplicitRegion",
            "relative.SLDPRT", targetSketch: null));
        Assert.Throws<ArgumentException>(() => ExtrudeFeatureBuilder.BuildExtrusionStream(
            stream, Definition(LShapeEdges), folder, stream.Length, "ExplicitRegion",
            null, targetSketch: null));
    }

    private static TempTestFile CreateStrippedContourBase()
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath("select_one_of_two_contours"));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(raw);
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] stripped = new byte[extrusion.TagOffset];
        Array.Copy(raw, 0, stripped, 0, stripped.Length);
        ushort folderCount = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static void AssertRegionAddRejected(
        TempTestFile temp, ExtrudeFeatureDefinitionData definition, string expectedMessage)
    {
        byte[] before = ReadStream(temp.FilePath);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.ThrowsAny<Exception>(() => part.AddExtrude(definition));
            Assert.True(error is NotSupportedException || error is ArgumentException,
                "Unexpected exception type: " + error.GetType().Name);
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
        return FindBytes(data, System.Text.Encoding.ASCII.GetBytes(text));
    }
}
