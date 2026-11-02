using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Selection envelope evidence (Contents/Config-0-ResolvedFeatures offsets refer
/// to SldFileTests/TestFiles/Features2026/SelectionModes/*.SLDPRT and the
/// selection_dual_rect_v2/v3 controlled experiments):
///
/// Object boundaries / fields:
///   moProfileFeature_c      tag @6055, DataStart 6079, next named tag 6370 and
///                           291 bytes in whole/whole_resave/contour/region and
///                           the v3 contour/region samples; byte-identical after
///                           the name. It carries no selected-contour handle.
///   moSketchRegion_c        tag @10445, DataStart 10467. Its leading u16 differs
///                           by save state (whole 0x8065/0x80C3, explicit
///                           0x8064/0x80C2, seed 0x8062/0x80BD), but u16@6
///                           (RegionIndex) is 2 in every sample and the block
///                           length varies (144/165/322/336). It is the sketch's
///                           loop table, not an explicit-selection discriminator.
///   moContourChooser_c      contour: tag @10789 in v2/v3 contour, 18-byte
///                           envelope; region: tag @12890 (v2/v3 region), 1710+
///                           byte unit layout (see SelectedSketchRegionParser).
///   suObArray               contour only, tag @10831 = chooser.DataStart + 18,
///                           payload u16 = selected contour count (1 observed).
///   moSketchChain_c         contour only (explicit), tag @10848 =
///                           suObArray.DataStart + 2; u16 entityCount, then
///                           entityCount * u32 chain ids, then u32 closed flag
///                           == 1 at 2 + 4*count (offset 18 for count 4).
///                           Whole-sketch samples also contain a chain block with
///                           two records, but chain presence alone is not an
///                           explicit selection.
///
/// Differential evidence (only the selected ordinal changes):
///   selection_dual_rect_v3 contour  chain ids [1,4,3,2]; contour_second
///   chain ids [5,8,7,6]; the 18-byte chooser payload is byte-identical.
///   selection_dual_rect_v3 region   edge set {5,6,7,8}; region_second
///   edge set {1,2,3,4}; both use the same prefix and 4x4 unit layout.
/// </summary>
public class SelectedSketchContourTests
{
    [Theory]
    [InlineData("contour", 1e-6, new uint[] { 1, 4, 3, 2 })]
    [InlineData("contour_second", 2e-6, new uint[] { 5, 8, 7, 6 })]
    public void ExplicitContour_OrderMatchesNativeSegmentIdentifiers(string name, double volume, uint[] expected)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionContours");
        string path = Path.Combine(root, name + ".SLDPRT");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "native-manifest.json")));
        var evidence = manifest.RootElement.GetProperty("observations").EnumerateArray()
            .Single(e => e.GetProperty("observation").GetProperty("file").GetString() == name + ".SLDPRT");
        Assert.Equal(evidence.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        var native = evidence.GetProperty("observation");
        Assert.True(native.GetProperty("rebuilt").GetBoolean());
        Assert.Equal(0, native.GetProperty("error").GetInt32());
        Assert.False(native.GetProperty("warning").GetBoolean());
        Assert.Equal(volume, native.GetProperty("volume").GetDouble(), 12);
        var selected = native.GetProperty("selectedDetails")[0];
        Assert.Equal("SketchContour", selected.GetProperty("type").GetString());
        Assert.True(selected.GetProperty("closed").GetBoolean());
        uint[] nativeIds = selected.GetProperty("segmentIds").EnumerateArray().Select(id => id[1].GetUInt32()).ToArray();
        Assert.Equal(expected, nativeIds);

        using var file = SwFile.Open(path, readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        var contour = Assert.Single(definition.SelectedContours);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Equal(nativeIds, contour.EntityIndices);
        Assert.True(contour.IsClosed);
        Assert.False(contour.IsCandidateOnly);
        Assert.Equal(0u, contour.EntityId); // Persistent contour/chain identifiers remain unresolved.
        Assert.Equal(0u, contour.ChainId);
        Assert.Equal(4, contour.EntityIndices.Count); // The seed has eight sketch segments, not all selected.
        Assert.Empty(definition.SelectedRegions);
    }

    [Fact]
    public void ExplicitRegion_DecodesBoundaryChainEntitySet()
    {
        // SelectionModes/region.SLDPRT is the v2 specimen whose selected region is
        // the 20x10 mm rectangle (native volume 2e-6, centre 0.03/0.005).
        // The region chooser unit records carry the same chain entity id space as
        // moSketchChain_c; the boundary set for that rectangle is {5,6,7,8}
        // (compare contour_second chain [5,8,7,6] = same rectangle).
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "region.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, definition.SketchSelectionMode);
        Assert.Empty(definition.SelectedContours);
        var region = Assert.Single(definition.SelectedRegions);
        Assert.Equal(new uint[] { 5, 6, 7, 8 }, region.EdgeIds);
        Assert.Equal(0u, region.EntityId);   // persistent region handle not stored
        Assert.Equal(0u, region.RegionIndex); // region ordinal not stored
        Assert.True(region.IsValid);
        Assert.Contains("Edges=[5, 6, 7, 8]", region.ToString());
    }

    [Fact]
    public void UnsupportedChooserCount_DoesNotReturnAvailableSketchChainAsSelection()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionContours", "contour.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(raw);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        var sketch = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        Assert.Single(SelectedSketchContourParser.Read(doc, sketch.TagOffset, extrusion.TagOffset));
        BitConverter.GetBytes(2u).CopyTo(raw, chooser.DataStart + 2);
        Assert.Empty(SelectedSketchContourParser.Read(MfcDocument.Parse(raw), sketch.TagOffset, extrusion.TagOffset));
    }

    [Fact]
    public void UnsupportedSuObArrayCount_IsUnresolvedInsteadOfGuessed()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionContours", "contour.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(raw);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        Assert.Equal(1, BitConverter.ToUInt16(raw, chooser.DataStart + 18 + TagLength(raw, chooser.DataStart + 18)));
        // Simulate a simultaneous multi-contour selection: count = 2 with only one
        // chain record present. The parser must refuse (no multi-record specimen).
        int arrayDataStart = chooser.DataStart + 18 + TagLength(raw, chooser.DataStart + 18);
        BitConverter.GetBytes((ushort)2).CopyTo(raw, arrayDataStart);
        var sketch = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        Assert.Empty(SelectedSketchContourParser.Read(MfcDocument.Parse(raw), sketch.TagOffset, extrusion.TagOffset));
    }

    private static int TagLength(byte[] raw, int tagOffset)
    {
        // FF FF tag_byte 00 | u16 nameLen | ASCII name (test streams use no _ct suffix)
        int nameLen = BitConverter.ToUInt16(raw, tagOffset + 4);
        return 4 + 2 + nameLen;
    }

    [Fact]
    public void RegionChooser_TamperedUnitEdgeId_IsRejected()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "region.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(raw);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        Assert.Single(SelectedSketchRegionParser.TryRead(doc, chooser));
        // First edge unit starts at +46 (prefix) + 87 (start unit); edge field at +81.
        int edgeField = chooser.DataStart + SelectedSketchRegionParser.PrefixLength + 87 + 81;
        BitConverter.GetBytes(0u).CopyTo(raw, edgeField);
        Assert.Empty(SelectedSketchRegionParser.TryRead(MfcDocument.Parse(raw), chooser));
    }

    [Fact]
    public void RegionChooser_TamperedArchiveSerial_IsRejected()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "region.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(raw);
        var chooser = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        Assert.Single(SelectedSketchRegionParser.TryRead(doc, chooser));
        // break the serial + 1 relation inside the first unit
        int serialPlusOne = chooser.DataStart + SelectedSketchRegionParser.PrefixLength + 77;
        BitConverter.GetBytes(0xDEADBEEFu).CopyTo(raw, serialPlusOne);
        Assert.Empty(SelectedSketchRegionParser.TryRead(MfcDocument.Parse(raw), chooser));
    }

    [Fact]
    public void Writer_ContourSelectionBlock_RoundTripsThroughDecoder()
    {
        uint[] ids = { 1, 4, 3, 2 };
        byte[] block = SketchSelectionWriter.BuildContourSelectionBlock(ids);
        byte[] stream = PrependVersion(block);
        var doc = MfcDocument.Parse(stream);
        var decoded = SelectedSketchContourParser.Read(doc, 0, stream.Length);
        var contour = Assert.Single(decoded);
        Assert.Equal(ids, contour.EntityIndices);
        Assert.True(contour.IsClosed);
        Assert.False(contour.IsCandidateOnly);

        var envelope = SketchSelectionParser.Read(doc, 0, stream.Length);
        Assert.Equal(SketchSelectionEnvelopeKind.Contour, envelope.Kind);
        Assert.Equal(ids, Assert.Single(envelope.Contours).EntityIndices);
        Assert.Empty(envelope.Regions);
    }

    [Fact]
    public void Writer_RegionSelectionBlock_RoundTripsThroughDecoder()
    {
        uint[] edges = { 5, 6, 7, 8 };
        byte[] block = SketchSelectionWriter.BuildRegionSelectionBlock(edges);
        byte[] stream = PrependVersion(block);
        var doc = MfcDocument.Parse(stream);
        var decoded = SelectedSketchRegionParser.Read(doc, 0, stream.Length);
        var region = Assert.Single(decoded);
        Assert.Equal(edges, region.EdgeIds);
        Assert.Equal(0u, region.EntityId);
        Assert.Equal(0u, region.RegionIndex);

        var envelope = SketchSelectionParser.Read(doc, 0, stream.Length);
        Assert.Equal(SketchSelectionEnvelopeKind.Region, envelope.Kind);
        Assert.Empty(envelope.Contours);
        Assert.Equal(edges, Assert.Single(envelope.Regions).EdgeIds);

        // Six-edge (L-shape) layout and unsorted input: the decoder reports the
        // boundary as a sorted set because intra-group ordering is not established.
        byte[] sixEdge = PrependVersion(SketchSelectionWriter.BuildRegionSelectionBlock(new uint[] { 8, 7, 6, 5, 4, 3 }));
        Assert.Equal(new uint[] { 3, 4, 5, 6, 7, 8 },
            Assert.Single(SelectedSketchRegionParser.Read(MfcDocument.Parse(sixEdge), 0, sixEdge.Length)).EdgeIds);
        byte[] unsorted = PrependVersion(SketchSelectionWriter.BuildRegionSelectionBlock(new uint[] { 8, 5, 7, 6 }));
        Assert.Equal(edges,
            Assert.Single(SelectedSketchRegionParser.Read(MfcDocument.Parse(unsorted), 0, unsorted.Length)).EdgeIds);
    }

    [Fact]
    public void Writer_RejectsInvalidSelectionInputs()
    {
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildContourSelectionBlock(Array.Empty<uint>()));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildContourSelectionBlock(new uint[] { 1, 1 }));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildContourSelectionBlock(new uint[] { 0 }));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildRegionSelectionBlock(Array.Empty<uint>()));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildRegionSelectionBlock(new uint[] { 5 }));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildRegionSelectionBlock(new uint[] { 5, 5 }));
        Assert.Throws<ArgumentException>(() => SketchSelectionWriter.BuildRegionSelectionBlock(new uint[] { 0, 1 }));
    }

    [Fact]
    public void SelectionEnvelope_NoChooser_IsWholeSketch()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "whole.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var sketch = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var envelope = SketchSelectionParser.Read(doc, sketch.TagOffset, extrusion.TagOffset);
        Assert.Equal(SketchSelectionEnvelopeKind.None, envelope.Kind);
        Assert.Empty(envelope.Contours);
        Assert.Empty(envelope.Regions);
    }

    [Fact]
    public void RegionChooser_TruncatedPayload_IsRejectedWithoutThrowing()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "SelectionModes", "region.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        byte[] raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var chooser = Assert.Single(MfcDocument.Parse(raw).AllElements.Where(e => e.ClassName == "moContourChooser_c"));
        int[] cuts =
        {
            chooser.DataStart,
            chooser.DataStart + 10,
            chooser.DataStart + SelectedSketchRegionParser.PrefixLength,
            chooser.DataStart + SelectedSketchRegionParser.PrefixLength + 87,
            chooser.DataStart + SelectedSketchRegionParser.PrefixLength + 87 + 89,
            chooser.DataStart + 1629
        };
        foreach (int cut in cuts)
        {
            byte[] truncated = raw.Take(cut).ToArray();
            var doc = MfcDocument.Parse(truncated);
            var ex = Record.Exception(() => SelectedSketchRegionParser.Read(doc, 0, truncated.Length));
            Assert.Null(ex);
            Assert.Empty(SelectedSketchRegionParser.Read(doc, 0, truncated.Length));
        }
    }

    private static byte[] PrependVersion(byte[] block)
    {
        // Real ResolvedFeatures streams begin with the 0x70 explicit-selection
        // version word; the contour parser gates on it.
        var stream = new byte[block.Length + 2];
        stream[0] = 0x70;
        stream[1] = 0x00;
        Array.Copy(block, 0, stream, 2, block.Length);
        return stream;
    }
}
