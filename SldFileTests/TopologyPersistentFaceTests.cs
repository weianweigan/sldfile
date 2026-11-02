using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Persistent face reference evidence (2026 samples).
///
/// moCompFace_c has two established layouts:
///   - 86-byte sheet-metal stub: 81-byte header + 38 4A 00 00 + 0x00, no faces;
///   - full multi-block layout (cut 572 B, fillet 926 B, after-fillet 3173 B):
///       per block: 81-byte header, 47-byte face reference at +81, 20-byte
///       surface mapping records from +128, then an undecoded trailer.
///     Header flag at +6: 0x40 for cut / sheet-metal, 0x00 otherwise.
///     Face reference: magic 38 4A 00 00 | u8 0 | u16 surface class | u32 1 |
///     u32 0 | u32 face count | u32 class word | u32 face id | 16-byte archive id |
///     u16 0 | u16 mapping-list class reference.
///
/// moFaceRef_c (38 B) shares the 16-byte archive id with the compFace face
/// reference and appears in blind features, so it is a generated-face reference,
/// not an UpToSurface selection.
/// moSurfaceIdRep_c (14 B) shares its class pair with the compFace surface class
/// and the first embedded mapping record.
/// </summary>
public class TopologyPersistentFaceTests
{
    [Fact]
    public void CompFace_CutLayout_DecodesSingleFaceBlock()
    {
        var data = ReadCompFace("extrude_cut_5mm");
        Assert.NotNull(data);
        Assert.Equal(CompFaceLayout.Full, data!.Layout);
        var block = Assert.Single(data.Blocks);
        Assert.Equal(0, block.BlockOffset);
        Assert.Equal(572, block.BlockLength);
        Assert.Equal((byte)0x40, block.HeaderFlag);
        Assert.Equal((ushort)0x80EA, block.FaceReference.SurfaceClass);
        Assert.Equal(6u, block.FaceReference.FaceCount);
        Assert.Equal(0x200u, block.FaceReference.ClassWord);
        Assert.Equal(0x26E6u, block.FaceReference.FaceId);
        Assert.Equal((ushort)0x80EC, block.FaceReference.MappingClassReference);
        Assert.Equal(new uint[] { 1, 4, 1, 2, 3 }, block.Mappings.Select(m => m.EntityIndex).ToArray());
        Assert.Equal(340, block.TrailerLength);
    }

    [Fact]
    public void CompFace_FilletLayout_DecodesThreeFaceBlocks()
    {
        var data = ReadCompFace("fillet_2mm");
        Assert.NotNull(data);
        Assert.Equal(CompFaceLayout.Full, data!.Layout);
        Assert.Equal(3, data.Blocks.Count);
        var first = data.Blocks[0];
        Assert.Equal((ushort)0x80E6, first.FaceReference.SurfaceClass);
        Assert.Equal(6u, first.FaceReference.FaceCount);
        Assert.Equal(0x300u, first.FaceReference.ClassWord);
        Assert.Equal(0x3A11u, first.FaceReference.FaceId);
        Assert.Equal((ushort)0x8122, first.FaceReference.MappingClassReference);
        Assert.Equal(new uint[] { 1, 1, 2, 3, 1 }, first.Mappings.Select(m => m.EntityIndex).ToArray());
        Assert.Equal(256, first.BlockLength);
        Assert.Equal(28, first.TrailerLength);

        foreach (var block in data.Blocks.Skip(1))
        {
            Assert.Equal((ushort)0x8125, block.FaceReference.SurfaceClass);
            Assert.Equal(4u, block.FaceReference.FaceCount);
            Assert.Equal(0x200u, block.FaceReference.ClassWord);
            Assert.Equal(0u, block.FaceReference.FaceId);
            Assert.Equal((ushort)0x80E8, block.FaceReference.MappingClassReference);
            Assert.Equal(new uint[] { 1, 2, 1, 3 }, block.Mappings.Select(m => m.EntityIndex).ToArray());
        }
        Assert.Equal(336, data.Blocks[1].BlockLength);
        Assert.Equal(334, data.Blocks[2].BlockLength);
    }

    [Theory]
    [InlineData("sm_base_flange_10mm")]
    [InlineData("sm_base_flange_20mm")]
    [InlineData("sm_base_flange_thick2")]
    [InlineData("sm_base_flange_rad3")]
    public void CompFace_SheetMetalStubLayout_HasNoFaceBlocks(string name)
    {
        var data = ReadCompFace(name);
        Assert.NotNull(data);
        Assert.Equal(CompFaceLayout.Stub, data!.Layout);
        Assert.Empty(data.Blocks);
    }

    [Fact]
    public void CompFace_AfterFilletLayout_DecodesMultipleBlocks()
    {
        var data = ReadCompFace("sm_base_flange_after_fillet");
        Assert.NotNull(data);
        Assert.Equal(CompFaceLayout.Full, data!.Layout);
        Assert.Equal(3, data.Blocks.Count);
        Assert.Equal((ushort)0x80F2, data.Blocks[0].FaceReference.SurfaceClass);
        Assert.Equal(0x300u, data.Blocks[0].FaceReference.ClassWord);
        Assert.Equal(0x7CDu, data.Blocks[0].FaceReference.FaceId);
        Assert.Equal((ushort)0x8130, data.Blocks[1].FaceReference.SurfaceClass);
        Assert.Equal(0x200u, data.Blocks[1].FaceReference.ClassWord);
    }

    [Theory]
    [InlineData("extrude_10mm", 6u, 0x300u, 0u)]
    [InlineData("extrude_cut_5mm", 6u, 0x300u, 0u)]
    [InlineData("fillet_2mm", 6u, 0x300u, 0u)]
    [InlineData("sweep_circle10_path40", 5u, 0x300u, 0u)]
    [InlineData("revolve_360", 5u, 0x300u, 0x3036u)]
    [InlineData("sm_base_flange_10mm", 6u, 0x200u, 0x42CDu)]
    public void FaceRef_LayoutIsConsistentAndGenerated(
        string relativePath, uint topologyType, uint classWord, uint classSpecificId)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFaceRef_c"));
        var faceRef = PersistentFaceReferenceParser.ReadFaceRef(doc, element);
        Assert.NotNull(faceRef);
        Assert.True(faceRef!.IsGeneratedFaceReference);
        Assert.Equal(1u, faceRef.FaceCount);
        Assert.Equal(topologyType, faceRef.TopologyType);
        Assert.Equal(classWord, faceRef.ClassWord);
        Assert.Equal(classSpecificId, faceRef.ClassSpecificId);
        Assert.Equal(0x54B249AD2594C37DUL, faceRef.ArchiveIdLow);
        Assert.Equal(0x54B249AD2594C37DUL, faceRef.ArchiveIdHigh);

        // Blind extrusion: the FaceRef is present but is not a terminating target.
        if (relativePath == "extrude_10mm")
        {
            var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
                Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
            Assert.Equal(0u, definition.EndCondition);
            Assert.Null(definition.EndConditionEntity);
            var target = TopologyDecoder.BuildEndConditionEntity(new FaceRefData { FaceCount = faceRef.FaceCount }, null, null);
            Assert.False(target!.IsValid);
        }
    }

    [Fact]
    public void CompFaceAndFaceRefShareArchiveIdAndSurfaceIdRepClassPair()
    {
        var compFace = ReadCompFace("fillet_2mm");
        Assert.NotNull(compFace);
        var block = compFace!.Blocks[0];
        var faceRef = ReadFaceRef("fillet_2mm");
        Assert.Equal(faceRef!.ArchiveIdLow, block.FaceReference.ArchiveIdLow);
        Assert.Equal(faceRef.ArchiveIdHigh, block.FaceReference.ArchiveIdHigh);

        using var file = SwFile.Open(SamplePath("fillet_2mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var surfaceElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSurfaceIdRep_c"));
        var surfaceBytes = stream[surfaceElement.DataStart..NextTagOffset(doc, surfaceElement)];
        var surface = PersistentFaceReferenceParser.ReadSurfaceIdRepPayload(surfaceBytes);
        Assert.NotNull(surface);
        Assert.Equal((ushort)0x80EA, surface!.ClassA);
        Assert.Equal((ushort)0x00ED, surface.ClassB);
        Assert.Equal(0x51u, surface.FeatureClass);
        // The moSurfaceIdRep record equals the first embedded surface mapping
        // record of the compFace block (class pair, feature class and serial).
        var mapping = block.Mappings[0];
        Assert.Equal(mapping.ClassA, surface.ClassA);
        Assert.Equal(mapping.ClassB, surface.ClassB);
        Assert.Equal(mapping.FeatureClass, surface.FeatureClass);
        Assert.Equal(mapping.ArchiveSerial, surface.ArchiveSerial);
    }

    [Fact]
    public void EndFace3IntSurfIdRep_IsOneMappingRecordPlusSixZeros()
    {
        using var file = SwFile.Open(SamplePath("extrude_cut_5mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moEndFace3IntSurfIdRep_c"));
        byte[] raw = stream[element.DataStart..NextTagOffset(doc, element)];
        Assert.Equal(26, raw.Length);
        Assert.True(FromSktEntSurfIdRepParser.TryReadMapping(raw, 0, out var entry));
        Assert.Equal((ushort)0x80EE, entry.ClassA);
        Assert.Equal((ushort)0x00F1, entry.ClassB);
        Assert.Equal(0x56u, entry.FeatureClass);
        Assert.Equal(1u, entry.EntityIndex);
        for (int i = 20; i < 26; i++) Assert.Equal(0, raw[i]);
    }

    [Fact]
    public void CompFaceLayouts_TamperedOrTruncated_AreRejected()
    {
        using var file = SwFile.Open(SamplePath("fillet_2mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompFace_c"));
        int end = NextTagOffset(doc, element);
        byte[] raw = stream[element.DataStart..end];
        Assert.NotNull(PersistentFaceReferenceParser.ReadCompFacePayload(raw));

        byte[] badFlag = (byte[])raw.Clone();
        badFlag[6] = 0x10;
        Assert.Null(PersistentFaceReferenceParser.ReadCompFacePayload(badFlag));

        byte[] badMagic = (byte[])raw.Clone();
        BitConverter.GetBytes(0xDEADu).CopyTo(badMagic, 81);
        Assert.Null(PersistentFaceReferenceParser.ReadCompFacePayload(badMagic));

        byte[] badClassWord = (byte[])raw.Clone();
        BitConverter.GetBytes(0x123u).CopyTo(badClassWord, 81 + 19);
        Assert.Null(PersistentFaceReferenceParser.ReadCompFacePayload(badClassWord));

        Assert.Null(PersistentFaceReferenceParser.ReadCompFacePayload(raw.Take(100).ToArray()));
        Assert.Equal(raw, stream[element.DataStart..end]);
    }

    [Fact]
    public void StubLayout_TamperedIsRejected()
    {
        using var file = SwFile.Open(SamplePath("sm_base_flange_10mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompFace_c"));
        byte[] raw = stream[element.DataStart..NextTagOffset(doc, element)];
        Assert.Equal(86, raw.Length);
        Assert.Equal(CompFaceLayout.Stub, PersistentFaceReferenceParser.ReadCompFacePayload(raw)!.Layout);

        byte[] badMagic = (byte[])raw.Clone();
        BitConverter.GetBytes(0xDEADu).CopyTo(badMagic, 81);
        Assert.Null(PersistentFaceReferenceParser.ReadCompFacePayload(badMagic));

        byte[] badTail = (byte[])raw.Clone();
        badTail[85] = 1;
        Assert.Null(PersistentFaceReferenceParser.ReadCompFacePayload(badTail));
    }

    private static CompFaceReferenceData? ReadCompFace(string relativePath)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompFace_c"));
        return PersistentFaceReferenceParser.ReadCompFace(doc, element);
    }

    private static GeneratedFaceReferenceData? ReadFaceRef(string relativePath)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFaceRef_c"));
        return PersistentFaceReferenceParser.ReadFaceRef(doc, element);
    }

    private static int NextTagOffset(MfcDocument doc, MfcElement element)
    {
        var elements = doc.AllElements.ToList();
        int index = elements.IndexOf(element);
        return index >= 0 && index + 1 < elements.Count ? elements[index + 1].TagOffset : doc.SourceData.Length;
    }

    private static string SamplePath(string relativePath)
    {
        if (relativePath == "sweep_circle10_path40") relativePath = "SweepOptions/" + relativePath;
        string baseName = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var direct = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", baseName + ".SLDPRT");
        if (File.Exists(direct)) return direct;
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", baseName + ".SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample Features2026/{relativePath}.SLDPRT not found");
    }
}
