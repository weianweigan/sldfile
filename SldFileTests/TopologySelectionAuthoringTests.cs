using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// SelectionAuthoring native specimens (Agent 5, 2026-10-30):
///   select_one_of_two_contours  v=0x70, suObArray 1, chain [1,4,3,2]
///   select_two_contours         v=0x71, suObArray 2, chains [5,8,7,6] then
///                               [1,4,3,2] (native API contours order), records
///                               separated by 00 00 F3 80
///   select_region               v=0x70, L-shape, 6 boundary edges {3,4,5,6,7,8}
///                               (prefix 0x80EE/0x80F0, variant 04 03)
///   select_region_overlap       v=0x70, 4 boundary edges {3,4,5,6}
///                               (variant 06 03)
/// Evidence offsets and grammar are documented in
/// SelectedSketchContourParser / SelectedSketchRegionParser. Native verification
/// of writer output is delegated to Agent 5; these tests only assert the decoder
/// and the self round-trip.
/// </summary>
public class TopologySelectionAuthoringTests
{
    [Theory]
    [InlineData("select_one_of_two_contours", "1,4,3,2", "")]
    [InlineData("select_two_contours", "5,8,7,6|1,4,3,2", "")]
    [InlineData("select_region", "", "3,4,5,6,7,8")]
    [InlineData("select_region_overlap", "", "3,4,5,6")]
    public void NativeSamples_DecodeSelectedContoursOrRegions(
        string name, string contours, string regionEdges)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);

        string[] expectedContours = contours.Length == 0 ? Array.Empty<string>() : contours.Split('|');
        Assert.Equal(expectedContours.Length, definition.SelectedContours.Count);
        for (int i = 0; i < expectedContours.Length; i++)
        {
            Assert.Equal(ParseIds(expectedContours[i]), definition.SelectedContours[i].EntityIndices);
            Assert.True(definition.SelectedContours[i].IsClosed);
            Assert.False(definition.SelectedContours[i].IsCandidateOnly);
        }

        if (regionEdges.Length == 0)
        {
            Assert.Empty(definition.SelectedRegions);
        }
        else
        {
            Assert.Empty(definition.SelectedContours);
            var region = Assert.Single(definition.SelectedRegions);
            Assert.Equal(ParseIds(regionEdges), region.EdgeIds);
            Assert.Equal(0u, region.EntityId);
            Assert.Equal(0u, region.RegionIndex);
            Assert.True(region.IsValid);
        }
    }

    [Theory]
    [InlineData("select_one_of_two_contours", "1,4,3,2", "")]
    [InlineData("select_two_contours", "5,8,7,6|1,4,3,2", "")]
    [InlineData("select_region", "", "3,4,5,6,7,8")]
    [InlineData("select_region_overlap", "", "3,4,5,6")]
    public void Resave_KeepsSelectionDecodingStable(string name, string contours, string regionEdges)
    {
        var main = Decode(name);
        var resave = Decode(name + "_resave");
        Assert.Equal((int)SketchSelectionMode.ExplicitSelection, main.Mode);
        Assert.Equal(main.Mode, resave.Mode);
        Assert.Equal(main.Contours, resave.Contours);
        Assert.Equal(main.Regions, resave.Regions);
        Assert.Equal(contours, main.Contours);
        Assert.Equal(regionEdges, main.Regions);
    }

    [Fact]
    public void MultiContour_CarriesSuObArrayCountTwoAndTwoChainRecords()
    {
        using var file = SwFile.Open(SamplePath("select_two_contours"), readOnly: true);
        Assert.Equal((ushort)0x71, file.ResolvedFeatures.StreamVersion);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var array = Assert.Single(doc.AllElements.Where(e => e.ClassName == "suObArray"));
        var chain = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSketchChain_c"));
        Assert.Equal(chooser.DataStart + 18, array.TagOffset);
        Assert.Equal(array.DataStart + 2, chain.TagOffset);
        Assert.Equal((ushort)2, BitConverter.ToUInt16(stream, array.DataStart));
        int chainIndex = doc.AllElements.ToList().IndexOf(chain);
        int chainEnd = doc.AllElements[chainIndex + 1].TagOffset;
        Assert.Equal(164, chainEnd - chain.DataStart); // 76 + 4 + 76 + 8

        var contours = SelectedSketchContourParser.Read(doc, 0, stream.Length);
        Assert.Equal(2, contours.Count);
        Assert.Equal(new uint[] { 5, 8, 7, 6 }, contours[0].EntityIndices);
        Assert.Equal(new uint[] { 1, 4, 3, 2 }, contours[1].EntityIndices);
    }

    [Fact]
    public void MultiContourWriter_RoundTripsThroughDecoder()
    {
        uint[][] contours = { new uint[] { 5, 8, 7, 6 }, new uint[] { 1, 4, 3, 2 } };
        byte[] block = SketchSelectionWriter.BuildContourSelectionBlock(contours);
        // Tags 80 bytes + chain payload (76 + separator 4 + 76 + terminator 8).
        Assert.Equal(244, block.Length);
        byte[] stream = PrependVersion(block);
        var doc = MfcDocument.Parse(stream);
        var decoded = SelectedSketchContourParser.Read(doc, 0, stream.Length);
        Assert.Equal(2, decoded.Count);
        Assert.Equal(contours[0], decoded[0].EntityIndices);
        Assert.Equal(contours[1], decoded[1].EntityIndices);

        var envelope = SketchSelectionParser.Read(doc, 0, stream.Length);
        Assert.Equal(SketchSelectionEnvelopeKind.Contour, envelope.Kind);
        Assert.Equal(2, envelope.Contours.Count);
    }

    [Fact]
    public void MultiContour_TamperedSeparator_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("select_two_contours"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chain = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSketchChain_c"));
        int separator = chain.DataStart + 76;
        Assert.Equal(0xF3, stream[separator + 2]);
        stream[separator + 2] = 0x00;
        var mutated = MfcDocument.Parse(stream);
        Assert.Empty(SelectedSketchContourParser.Read(mutated, 0, stream.Length));
        var envelope = SketchSelectionParser.Read(mutated, 0, stream.Length);
        Assert.Equal(SketchSelectionEnvelopeKind.UnresolvedChooser, envelope.Kind);
    }

    [Fact]
    public void MultiContour_SuObArrayCountMismatch_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("select_two_contours"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var array = Assert.Single(doc.AllElements.Where(e => e.ClassName == "suObArray"));
        BitConverter.GetBytes((ushort)3).CopyTo(stream, array.DataStart);
        var mutated = MfcDocument.Parse(stream);
        Assert.Empty(SelectedSketchContourParser.Read(mutated, 0, stream.Length));
    }

    [Fact]
    public void RegionVariants_TamperedVariantByte_IsRejected()
    {
        foreach (var name in new[] { "select_region", "select_region_overlap" })
        {
            using var file = SwFile.Open(SamplePath(name), readOnly: true);
            byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
            var doc = MfcDocument.Parse(stream);
            var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
            Assert.True(stream[chooser.DataStart + 20] is 0x04 or 0x06);
            stream[chooser.DataStart + 20] = 0x07;
            var mutated = MfcDocument.Parse(stream);
            Assert.Empty(SelectedSketchRegionParser.Read(mutated, 0, stream.Length));
        }
    }

    [Fact]
    public void RegionVariants_TamperedEdgeUnion_IsRejected()
    {
        // Force a duplicate edge so the distinct boundary set no longer matches a
        // prefix edge count.
        using var file = SwFile.Open(SamplePath("select_region"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        Assert.Single(SelectedSketchRegionParser.TryRead(doc, chooser));
        // L-shape: prefix 46 + start unit 87; first edge unit edge field at +81.
        int edgeField = chooser.DataStart + SelectedSketchRegionParser.PrefixLength + 87 + 81;
        BitConverter.GetBytes(0u).CopyTo(stream, edgeField);
        Assert.Empty(SelectedSketchRegionParser.TryRead(MfcDocument.Parse(stream), chooser));
    }

    [Fact]
    public void RegionWriters_RoundTripAllEstablishedVariants()
    {
        foreach (var edges in new[] { new uint[] { 3, 4, 5, 6 }, new uint[] { 3, 4, 5, 6, 7, 8 } })
        {
            byte[] block = SketchSelectionWriter.BuildRegionSelectionBlock(edges);
            byte[] stream = PrependVersion(block);
            var doc = MfcDocument.Parse(stream);
            var region = Assert.Single(SelectedSketchRegionParser.Read(doc, 0, stream.Length));
            Assert.Equal(edges, region.EdgeIds);
        }
    }

    [Theory]
    [InlineData("select_one_of_two_contours", "1,4,3,2", 0xBF, "6,7", "8,5,2,3,4,1")]
    [InlineData("select_two_contours", "5,8,7,6|1,4,3,2", 0xC0, "6,7", "8,5,2,3,4,1")]
    public void ContourWriter_WithCapturedSuffix_ReproducesNativeWindowBytes(
        string name, string contours, int surfaceId, string leftTrim, string rightTrim)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var suffix = SelectedSketchContourSuffixParser.Read(doc, chooser.TagOffset, extrusion.TagOffset);
        Assert.NotNull(suffix);
        Assert.Equal((ushort)surfaceId, suffix!.SurfaceId);
        Assert.Equal(ParseIds(leftTrim), suffix.LeftTrimEdges);
        Assert.Equal(ParseIds(rightTrim), suffix.RightTrimEdges);
        Assert.True(suffix.IsValid);

        uint[][] contourIds = contours.Split('|').Select(ParseIds).ToArray();
        Assert.NotNull(suffix.SourceDocumentPath);
        byte[] block = SketchSelectionWriter.BuildContourSelectionBlock(
            contourIds, suffix: suffix, targetDocumentPath: suffix.SourceDocumentPath);
        byte[] nativeWindow = stream[chooser.TagOffset..extrusion.TagOffset];
        Assert.Equal(nativeWindow, block);
    }

    [Fact]
    public void ContourWriter_CrossFileTarget_RewritesCStringHandlePath()
    {
        using var file = SwFile.Open(SamplePath("select_one_of_two_contours"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var suffix = SelectedSketchContourSuffixParser.Read(doc, chooser.TagOffset, extrusion.TagOffset);
        Assert.NotNull(suffix);
        string sourcePath = suffix!.SourceDocumentPath!;
        string targetPath = Path.Combine(Path.GetTempPath(), "agent3-target-document.SLDPRT");
        byte[] originalHandle = (byte[])suffix.CStringHandlePayload.Clone();

        byte[] block = SketchSelectionWriter.BuildContourSelectionBlock(
            new uint[] { 1, 4, 3, 2 }, suffix: suffix, targetDocumentPath: targetPath);

        // The written block must contain the target path and never the source path.
        byte[] targetUtf16 = System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(targetPath));
        byte[] sourceUtf16 = System.Text.Encoding.Unicode.GetBytes(sourcePath);
        Assert.True(Subsequence(block, targetUtf16).Index >= 0);
        Assert.True(Subsequence(block, sourceUtf16).Index < 0);

        // The captured suffix is not mutated; the rewritten payload keeps the tail.
        Assert.Equal(originalHandle, suffix.CStringHandlePayload);
        Assert.True(CStringHandlePathPolicy.TryRead(originalHandle, out _, out byte[] originalTail));
        byte[] rewritten = suffix.PrepareCStringHandlePayload(targetPath);
        Assert.True(CStringHandlePathPolicy.TryRead(rewritten, out string rewrittenPath, out byte[] rewrittenTail));
        Assert.Equal(Path.GetFullPath(targetPath), rewrittenPath);
        Assert.Equal(originalTail, rewrittenTail);
    }

    [Fact]
    public void ContourWriter_SameDocumentTarget_KeepsCStringHandleVerbatim()
    {
        using var file = SwFile.Open(SamplePath("select_two_contours"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var suffix = SelectedSketchContourSuffixParser.Read(doc, chooser.TagOffset, extrusion.TagOffset);
        Assert.NotNull(suffix);
        byte[] same = suffix!.PrepareCStringHandlePayload(suffix.SourceDocumentPath);
        Assert.Equal(suffix.CStringHandlePayload, same);
    }

    [Fact]
    public void ContourWriter_MissingRelativeOrUnreadableTargetPath_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("select_one_of_two_contours"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var suffix = SelectedSketchContourSuffixParser.Read(doc, chooser.TagOffset, extrusion.TagOffset);
        Assert.NotNull(suffix);

        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildContourSelectionBlock(
            new uint[] { 1, 4, 3, 2 }, suffix: suffix));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildContourSelectionBlock(
            new uint[] { 1, 4, 3, 2 }, suffix: suffix, targetDocumentPath: "relative.SLDPRT"));

        var broken = new SketchContourSuffixData
        {
            SurfaceId = suffix!.SurfaceId,
            FrPayload = suffix.FrPayload,
            ExtObjectPayload = suffix.ExtObjectPayload,
            CStringHandlePayload = new byte[] { 0x00, 0x01, 0x02, 0x03 },
            LeftTrimPayload = suffix.LeftTrimPayload,
            RightTrimPayload = suffix.RightTrimPayload
        };
        Assert.Throws<InvalidOperationException>(() => SketchSelectionWriter.BuildContourSelectionBlock(
            new uint[] { 1, 4, 3, 2 }, suffix: broken,
            targetDocumentPath: Path.Combine(Path.GetTempPath(), "agent3-target.SLDPRT")));
    }

    [Fact]
    public void RegionTemplate_BuildContext_ReproducesNativeContextBytesForSameDocument()
    {
        using var file = SwFile.Open(SamplePath("select_region_overlap"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var template = RegionChooserTemplate.Capture(doc, 0, extrusion.TagOffset);
        Assert.NotNull(template);
        Assert.NotNull(template!.SourceDocumentPath);
        Assert.NotEmpty(template.ContextTags);
        Assert.Contains(template.ContextTags, t => t.ClassName == "moCStringHandle_c");

        byte[] context = SketchSelectionWriter.BuildRegionSelectionContext(
            template.NativeEdgeIds, template, template.SourceDocumentPath);
        int contextStart = template.ContextTags[0].TagOffset;
        int chooserEnd = extrusion.TagOffset;
        Assert.Equal(stream[contextStart..chooserEnd], context);
    }

    [Fact]
    public void RegionTemplate_CrossFileContext_RewritesAndChooserOnlyRejects()
    {
        using var file = SwFile.Open(SamplePath("select_region_overlap"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var template = RegionChooserTemplate.Capture(doc, 0, extrusion.TagOffset);
        Assert.NotNull(template);
        string targetPath = Path.Combine(Path.GetTempPath(), "agent3-region-target.SLDPRT");

        byte[] context = SketchSelectionWriter.BuildRegionSelectionContext(
            template!.NativeEdgeIds, template, targetPath);
        byte[] targetUtf16 = System.Text.Encoding.Unicode.GetBytes(Path.GetFullPath(targetPath));
        byte[] sourceUtf16 = System.Text.Encoding.Unicode.GetBytes(template.SourceDocumentPath!);
        Assert.True(Subsequence(context, targetUtf16).Index >= 0);
        Assert.True(Subsequence(context, sourceUtf16).Index < 0);

        // Chooser-only block carries no path and is returned for the same document;
        // a foreign target is rejected to force the context rewrite path.
        Assert.NotNull(SketchSelectionWriter.BuildRegionSelectionBlock(
            template.NativeEdgeIds, template, template.SourceDocumentPath));
        Assert.Throws<InvalidOperationException>(() => SketchSelectionWriter.BuildRegionSelectionBlock(
            template.NativeEdgeIds, template, targetPath));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildRegionSelectionBlock(
            template.NativeEdgeIds, template, null));
    }

    [Fact]
    public void RegionTemplate_UnreadableCStringHandlePath_RejectsContext()
    {
        using var file = SwFile.Open(SamplePath("select_region_overlap"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var handle = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCStringHandle_c"));
        stream[handle.DataStart] = 0x00; // destroy the FF FE FF WChar prefix
        var mutated = MfcDocument.Parse(stream);
        var extrusion = Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var template = RegionChooserTemplate.Capture(mutated, 0, extrusion.TagOffset);
        Assert.NotNull(template);
        Assert.Null(template!.SourceDocumentPath);
        Assert.Throws<InvalidOperationException>(() => SketchSelectionWriter.BuildRegionSelectionContext(
            template.NativeEdgeIds, template, Path.Combine(Path.GetTempPath(), "agent3-region-target.SLDPRT")));
    }

    private static int ChooserPayloadEnd(MfcDocument doc, MfcElement chooser, int fallback)
    {
        // The chooser payload ends at the next class tag (an anonymous child can
        // follow some choosers before the feature tag).
        var elements = doc.AllElements;
        for (int i = 0; i < elements.Count; i++)
        {
            if (!ReferenceEquals(elements[i], chooser)) continue;
            return i + 1 < elements.Count ? elements[i + 1].TagOffset : fallback;
        }
        return fallback;
    }

    private static (int Index, int Length) Subsequence(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return (i, needle.Length);
        }
        return (-1, 0);
    }

    [Fact]
    public void ContourWriter_EnvelopeOnly_IsShorterThanNativeSuffixWindow()
    {
        // The 164-byte single-contour envelope lacks the native trim/reference
        // suffix; native rebuild of such a block fails (Agent 5 evidence), so the
        // writer exposes the suffix parameter for full emission.
        byte[] envelope = SketchSelectionWriter.BuildContourSelectionBlock(new uint[] { 1, 4, 3, 2 });
        Assert.Equal(164, envelope.Length);

        using var file = SwFile.Open(SamplePath("select_one_of_two_contours"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        int nativeWindowLength = extrusion.TagOffset - chooser.TagOffset;
        Assert.True(nativeWindowLength > envelope.Length);
    }

    [Theory]
    [InlineData("select_region", "3,4,5,6,7,8")]
    [InlineData("select_region_resave", "3,4,5,6,7,8")]
    [InlineData("select_region_overlap", "3,4,5,6")]
    [InlineData("select_region_overlap_resave", "3,4,5,6")]
    public void RegionChooserTemplate_RebuildsNativePayloadByteIdentically(string name, string edges)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] nativePayload = stream[chooser.DataStart..ChooserPayloadEnd(doc, chooser, extrusion.TagOffset)];
        var template = RegionChooserTemplate.Capture(nativePayload);
        Assert.NotNull(template);
        Assert.Equal(ParseIds(edges), template!.NativeEdgeIds);
        Assert.Equal(ParseIds(edges).Length, template.GroupCount);

        byte[] rebuiltPayload = template.BuildPayload(ParseIds(edges));
        Assert.Equal(nativePayload, rebuiltPayload);

        byte[] block = SketchSelectionWriter.BuildRegionSelectionBlock(ParseIds(edges), template);
        var rebuiltDoc = MfcDocument.Parse(PrependVersion(block));
        var region = Assert.Single(SelectedSketchRegionParser.Read(rebuiltDoc, 0, block.Length + 2));
        Assert.Equal(ParseIds(edges), region.EdgeIds);
    }

    [Fact]
    public void RegionChooserTemplate_LoopOrderReEmissionPreservesTailAndGroupRule()
    {
        using var file = SwFile.Open(SamplePath("select_region_overlap"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] nativePayload = stream[chooser.DataStart..ChooserPayloadEnd(doc, chooser, extrusion.TagOffset)];
        var template = RegionChooserTemplate.Capture(nativePayload);
        Assert.NotNull(template);
        Assert.True(template!.OrderingRuleVerified);
        Assert.Equal(new uint[] { 3, 4, 5, 6 }, template.NativeLoopOrder);

        // Same sketch, different boundary set of the same size, with its loop
        // traversal order. Only the selection group edge fields change; every other
        // byte (inter-group headers and the sketch-wide tail) is preserved.
        uint[] differentLoop = { 1, 2, 7, 8 };
        byte[] payload = template.BuildPayloadFromLoopOrder(differentLoop);
        Assert.Equal(nativePayload.Length, payload.Length);
        int differingBytes = 0;
        int firstDifference = 0;
        for (int i = 0; i < payload.Length; i++)
        {
            if (payload[i] != nativePayload[i])
            {
                if (differingBytes == 0) firstDifference = i;
                differingBytes++;
            }
        }
        Assert.True(differingBytes > 0);
        Assert.True(firstDifference < 46 + 4 * (87 + 3 * 89 + 56)); // inside the selection groups
        Assert.Equal(differentLoop.OrderBy(x => x).ToArray(), Assert.Single(
            SelectedSketchRegionParser.TryReadPayload(payload)).EdgeIds);

        // The set-only API rejects a different set instead of guessing an order.
        Assert.Throws<ArgumentException>(() => template.BuildPayload(differentLoop));
    }

    [Theory]
    [InlineData("select_region", new uint[] { 4, 3, 6, 7, 8, 5 })]
    [InlineData("select_region_resave", new uint[] { 4, 3, 6, 7, 8, 5 })]
    [InlineData("select_region_overlap", new uint[] { 3, 4, 5, 6 })]
    [InlineData("select_region_overlap_resave", new uint[] { 3, 4, 5, 6 })]
    [InlineData("SelectionModes/region", new uint[] { 6, 7, 8, 5 })]
    public void RegionChooserTemplate_NativeLoopOrderRuleReproducesNativePayload(
        string relativePath, uint[] expectedLoopOrder)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        byte[] nativePayload = stream[chooser.DataStart..ChooserPayloadEnd(doc, chooser, extrusion.TagOffset)];
        var template = RegionChooserTemplate.Capture(nativePayload);
        Assert.NotNull(template);
        Assert.True(template!.OrderingRuleVerified);
        Assert.Equal(expectedLoopOrder, template.NativeLoopOrder);
        Assert.Equal(nativePayload, template.BuildPayloadFromLoopOrder(expectedLoopOrder));

        // Group g = [ e[N-g], e[N-2-g], e[N-1-g] ] (mod N) is reproduced exactly.
        int n = expectedLoopOrder.Length;
        Assert.Equal(n, template.NativeGroupEdges.Count);
        for (int g = 0; g < n; g++)
        {
            var expected = new[]
            {
                expectedLoopOrder[((n - g) % n + n) % n],
                expectedLoopOrder[((n - 2 - g) % n + n) % n],
                expectedLoopOrder[((n - 1 - g) % n + n) % n]
            };
            Assert.Equal(expected, template.NativeGroupEdges[g]);
        }
    }

    [Fact]
    public void RegionChooserTemplate_CapturesFromDocumentAndRendersNativePayload()
    {
        using var file = SwFile.Open(SamplePath("select_region"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var template = RegionChooserTemplate.Capture(doc, chooser.TagOffset, extrusion.TagOffset);
        Assert.NotNull(template);
        Assert.Equal(6, template!.GroupCount);
        byte[] nativePayload = stream[chooser.DataStart..ChooserPayloadEnd(doc, chooser, extrusion.TagOffset)];
        Assert.Equal(nativePayload, template.BuildPayload(template.NativeEdgeIds));
    }

    private static (int Mode, string Contours, string Regions) Decode(string name)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        string contours = string.Join("|", definition.SelectedContours.Select(c => string.Join(",", c.EntityIndices)));
        string regions = string.Join("|", definition.SelectedRegions.Select(r => string.Join(",", r.EdgeIds)));
        return ((int)definition.SketchSelectionMode, contours, regions);
    }

    private static uint[] ParseIds(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => Convert.ToUInt32(v.Trim(), 10)).ToArray();

    private static byte[] PrependVersion(byte[] block)
    {
        var stream = new byte[block.Length + 2];
        stream[0] = 0x70;
        Array.Copy(block, 0, stream, 2, block.Length);
        return stream;
    }

    private static string SamplePath(string name)
    {
        string relative = name.Contains('/') ? name : "SelectionAuthoring/" + name;
        relative = relative.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", relative + ".SLDPRT");
        if (File.Exists(path)) return path;
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", relative + ".SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample Features2026/{relative}.SLDPRT not found");
    }
}
