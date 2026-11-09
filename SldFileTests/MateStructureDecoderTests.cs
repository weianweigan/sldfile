using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Internal structural tests for serialized mate objects. Only offsets, chain
/// classes, face/sketch record fields and deferred 2026 texts are asserted; mate
/// type/alignment/parameters remain unresolved by design.
/// </summary>
public class MateStructureDecoderTests
{
    [Fact]
    public void Legacy2012_PointMate_DecodesSketchChainAndHeaderCandidates()
    {
        var structure = ReadMateStructure(TestFileProvider.Assembly, "moMateCoordinateM");

        Assert.Equal(MateStructureLayout.LegacyNested, structure.Layout);
        Assert.Equal("Coincident3", structure.DisplayName);
        Assert.Equal(0x94, structure.HeaderLength);
        Assert.Equal(0x00174000u, structure.GetHeaderField(0x04));
        Assert.Equal(0x01000000u, structure.GetHeaderField(0x88));
        Assert.Equal(2u, structure.GetHeaderField(0x90));

        var reference = Assert.Single(structure.References);
        Assert.Equal("moPointRef_w", reference.WrapperClassName);
        Assert.Equal(MateReferenceEntityKind.SketchEntity, reference.Kind);
        Assert.Contains("moCompSketchEntHandle_c", reference.ChainClassNames);
        Assert.Contains("moChainComponent_c", reference.ChainClassNames);
        Assert.Contains("moReferenceChain_c", reference.ChainClassNames);
        Assert.Contains("moExtFeatureList_c", reference.ChainClassNames);

        var sketch = Assert.IsType<SketchReferenceStructure>(reference.Sketch);
        Assert.Equal((uint?)2, sketch.ChainComponentCount);
        Assert.Equal((uint?)0, sketch.ReferenceChainCount);
        Assert.Equal((uint?)1, sketch.ExtFeatureCount);
        Assert.Equal((uint?)0x53, sketch.ExtFeatureTypeId);
        Assert.Equal((uint?)0x0AF1, sketch.ExtFeatureSerial);
    }

    [Fact]
    public void Legacy2012_PlaneMate_DecodesFaceRecordAndMappings()
    {
        var structure = ReadMateStructure(TestFileProvider.Assembly, "moMateCoincidentM");

        Assert.Equal(MateStructureLayout.LegacyNested, structure.Layout);
        Assert.Equal("Coincident4", structure.DisplayName);
        Assert.Equal(0x94, structure.HeaderLength);
        Assert.Equal(0x00184000u, structure.GetHeaderField(0x04));
        Assert.Equal(0x00000200u, structure.GetHeaderField(0x88));
        Assert.Equal(2u, structure.GetHeaderField(0x90));
        Assert.Empty(structure.DeferredReferenceTexts);

        var reference = Assert.Single(structure.References);
        Assert.Equal("moPlaneRef_w", reference.WrapperClassName);
        Assert.Equal(MateReferenceEntityKind.Face, reference.Kind);
        Assert.Contains("moCompFace_c", reference.ChainClassNames);
        Assert.Contains("moFaceRef_c", reference.ChainClassNames);
        Assert.Contains("moEndFaceSurfIdRep_c", reference.ChainClassNames);
        Assert.Contains("moFR_c", reference.ChainClassNames);
        Assert.Contains("moFromSktEntSurfIdRep_c", reference.ChainClassNames);
        Assert.Contains("默认", reference.WCharStrings);

        var face = Assert.IsType<FaceReferenceStructure>(reference.Face);
        Assert.Equal(1u, face.FaceCount);
        Assert.Equal(6u, face.TopologyType);
        Assert.Equal(0x00000200u, face.Field12);
        Assert.Equal(0x0000192Du, face.Field16);
        Assert.Equal(0x54B249AD2594C37Dul, face.Magic1);
        Assert.Equal(0x54B249AD2594C37Dul, face.Magic2);
        Assert.Equal((uint?)0x4B, face.FeatureTypeId);
        Assert.Equal((uint?)0x69DCB52C, face.Serial);
        Assert.Equal(new uint[] { 4, 3, 2, 1 }, face.MappingIndices.ToArray());
        Assert.All(face.MappingFeatureTypes, type => Assert.Equal(0x4Bu, type));
    }

    [Fact]
    public void PointMate2026_DecodesComponentInstanceAndHeaderCandidates()
    {
        var structure = ReadMateStructure(TestFileProvider.Assembly2026, "moMateCoordinate");

        // The point/sketch mate has no deferred facet text of its own; the
        // following plane mate's data is excluded by the next-inline-mate bound.
        Assert.Equal(MateStructureLayout.LegacyNested, structure.Layout);
        Assert.Equal("Coincident3", structure.DisplayName);
        Assert.Equal(0xAE, structure.HeaderLength);
        Assert.Equal(0x00174000u, structure.GetHeaderField(0x04));
        Assert.Equal(0x01000000u, structure.GetHeaderField(0xA0));

        var reference = Assert.Single(structure.References);
        Assert.Equal("moPointRef_w", reference.WrapperClassName);
        Assert.Equal(MateReferenceEntityKind.SketchEntity, reference.Kind);
        Assert.Contains("moCompSketchEntHandle_c", reference.ChainClassNames);
        Assert.Contains("cube-1@assembly", reference.WCharStrings);
        Assert.Contains("原点@cube-1@assembly", reference.WCharStrings);

        var sketch = Assert.IsType<SketchReferenceStructure>(reference.Sketch);
        Assert.Equal((uint?)2, sketch.ChainComponentCount);
        Assert.Contains("cube-1@assembly", sketch.ComponentInstanceNames);
        Assert.Contains(5u, sketch.SketchReferenceIds);
    }

    [Fact]
    public void Deferred2026_PlaneMate_DecodesDeferredTextsAndSharedFaceRecord()
    {
        var structure = ReadMateStructure(TestFileProvider.Assembly2026, "moMateCoincident");

        Assert.Equal(MateStructureLayout.DeferredReference2026, structure.Layout);
        Assert.Equal("Coincident4", structure.DisplayName);
        Assert.Equal(0xAE, structure.HeaderLength);
        Assert.Equal(0x00184000u, structure.GetHeaderField(0x04));
        Assert.Equal(0x00000200u, structure.GetHeaderField(0xA0));

        Assert.Contains("moEndFaceSurfIdRep_c,75,0,0, \n", structure.DeferredReferenceTexts);
        Assert.Contains("moEndFaceSurfIdRep_c,73,1,0, \n", structure.DeferredReferenceTexts);
        Assert.Contains("cube-1@assembly", structure.WCharStrings);
        Assert.Contains("cylinder-1@assembly", structure.WCharStrings);
        Assert.Contains(structure.WCharStrings, s => s.EndsWith("cube.SLDPRT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(structure.WCharStrings, s => s.EndsWith("cylinder.SLDPRT", StringComparison.OrdinalIgnoreCase));

        var reference = Assert.Single(structure.References);
        Assert.Equal("moPlaneRef_w", reference.WrapperClassName);
        Assert.Equal(MateReferenceEntityKind.Face, reference.Kind);
        Assert.Contains("moFacetedData_c", reference.ChainClassNames);

        var face = Assert.IsType<FaceReferenceStructure>(reference.Face);
        Assert.Equal(1u, face.FaceCount);
        Assert.Equal(6u, face.TopologyType);
        Assert.Equal(0x00000300u, face.Field12);
        Assert.Equal(0x000044E4u, face.Field16);
        Assert.Equal(0x54B249AD2594C37Dul, face.Magic1);
        Assert.Equal(0x54B249AD2594C37Dul, face.Magic2);
        Assert.Equal((uint?)0x4B, face.FeatureTypeId);
        Assert.Equal((uint?)0x69DCB52C, face.Serial);
        Assert.Equal(new uint[] { 4, 3, 2, 1 }, face.MappingIndices.ToArray());
    }

    [Fact]
    public void UnknownOrTruncatedMateLayout_LeavesStructureNull()
    {
        byte[] stream = BuildMatesListStream(
            MfcTag("moMateDistance", 0x4D, 0x80),
            WChar("Distance1"),
            new byte[64]);

        byte[] before = (byte[])stream.Clone();
        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);

        var entry = Assert.Single(model.Mates);
        Assert.Equal("moMateDistanceM", entry.ClassName);
        Assert.Null(entry.Structure);
        Assert.Empty(entry.References);
        Assert.Equal(before, stream);

        // Public mate model still does not claim type/alignment/entities/parameters.
        var info = new AssemblyMateInfo(entry);
        Assert.False(info.Resolution.MateType);
        Assert.False(info.Resolution.MateAlignment);
        Assert.False(info.Resolution.EntityReferences);
        Assert.False(info.Resolution.MateParameters);
    }

    [Fact]
    public void Decode_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);

        var stream = file.RawStreams.First(k => k.Key.Contains("MatesList")).Value;
        byte[] before = (byte[])stream.Clone();
        var document = MfcDocument.Parse(stream);
        var mate = document.AllElements.First(e => e.ClassName.StartsWith("moMate", StringComparison.Ordinal) &&
                                                   !e.ClassName.StartsWith("moMaterial", StringComparison.Ordinal));

        var structure = MateStructureDecoder.Decode(mate);
        Assert.NotNull(structure);
        Assert.Equal(before, stream);
    }

    [Fact]
    public void Legacy2012_PlaneMate_DecodesTaggedAndClassReferenceFaces()
    {
        var structure = ReadMateStructure(TestFileProvider.Assembly, "moMateCoincidentM");

        Assert.Equal(0u, structure.MateTypeId);
        Assert.Equal(2, structure.EntityReferences.Count);
        Assert.Equal(
            new uint?[] { 0x192Du, 0x263u },
            structure.EntityReferences.Select(e => e.PersistentId).ToArray());
        Assert.All(structure.EntityReferences, e =>
        {
            Assert.Equal(MateReferenceEntityKind.Plane, e.Kind);
            Assert.Empty(e.ComponentInstanceName);
        });
    }

    [Fact]
    public void Legacy2012_TamperedClassReferenceFaceCode_DropsThatFace()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        byte[] stream = (byte[])file.RawStreams.First(k => k.Key.Contains("MatesList")).Value.Clone();
        int classRef = FindBytes(stream, new byte[] { 0xF2, 0x80 });
        Assert.True(classRef > 0);
        stream[classRef] = 0xF3; // unknown class reference

        var doc = MfcDocument.Parse(stream);
        var mate = doc.AllElements.First(e => e.ClassName == "moMateCoincidentM");
        var structure = Assert.IsType<MateStructureData>(MateStructureDecoder.Decode(mate));

        // Only the tagged faceRef remains; the untagged class-reference face is gone.
        Assert.Single(structure.EntityReferences);
        Assert.Equal(0x192Du, structure.EntityReferences[0].PersistentId);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int FindBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i + pattern.Length <= data.Length; i++)
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

    private static MateStructureData ReadMateStructure(string path, string className)
    {
        using var file = SwFile.Open(path, readOnly: true);
        // Repeated coincident mates are also recovered from class-reference records;
        // these decoder tests target the inline (first in stream order) record.
        var entry = file.MatesList.Mates.First(m => m.ClassName == className);
        return Assert.IsType<MateStructureData>(entry.Structure);
    }

    private static byte[] BuildMatesListStream(params byte[][] parts)
    {
        var bytes = new List<byte> { 0x04, 0x00 };
        foreach (byte[] part in parts)
            bytes.AddRange(part);
        bytes.AddRange(new byte[48]);
        return bytes.ToArray();
    }

    private static byte[] MfcTag(string className, params byte[] suffix)
    {
        byte[] ascii = Encoding.ASCII.GetBytes(className);
        var bytes = new List<byte> { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) };
        bytes.AddRange(ascii);
        bytes.AddRange(suffix);
        return bytes.ToArray();
    }

    private static byte[] WChar(string value)
    {
        var bytes = new List<byte> { 0xFF, 0xFE, 0xFF, (byte)value.Length };
        bytes.AddRange(Encoding.Unicode.GetBytes(value));
        bytes.AddRange(new byte[] { 0x00, 0x00 });
        return bytes.ToArray();
    }
}
