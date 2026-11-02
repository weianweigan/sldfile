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
/// moFromSktEntSurfIdRep_c and parent-sketch evidence (2026 samples).
///
/// Mapping record (20 bytes): u16 0 | u16 classA | u16 classB |
/// u32 feature class (0x4F extrusion, 0x56 cut, 0x5E sheet-metal flange) |
/// u32 serial | u32 entity index | u16 surface class tag.
/// After the leading list: 8 zero bytes, then persist-reference magic
/// 38 4A 00 00 + u32 id; the id is 0 in every specimen (NULL parent reference),
/// so the parent sketch id is NOT stored in this object.
///
/// Parent sketch id comes from moProfileFeature_c payload offset
/// displayNameEnd + 8 (0x49 in every specimen), cross-checked against the first
/// persist-reference id of the root moCompFeature_c list.
/// </summary>
public class TopologyFromSktMappingTests
{
    [Theory]
    [InlineData("extrude_10mm", 0x4Fu, "4,3,2,1", "1,2,3,4")]
    [InlineData("sm_base_flange_10mm", 0x5Eu, "4,3,2,1", "1,2,3,4")]
    [InlineData("SelectionModes/contour", 0x4Fu, "2,3,4,1", "1,4,3,2")]
    [InlineData("SelectionContours/contour_second", 0x4Fu, "6,7,8,5", "5,8,7,6")]
    [InlineData("SelectionAuthoring/select_two_contours", 0x4Fu, "6,7,8,5", "5,8,7,6")]
    public void FromSkt_PrimaryMappingIsReversedChainEntityList(
        string relativePath, uint featureClass, string expectedMappings, string expectedChain)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"));
        var decoded = FromSktEntSurfIdRepParser.Read(doc, element);
        Assert.NotNull(decoded);
        Assert.Equal(featureClass, decoded!.FeatureClass);
        Assert.Equal(ParseIds(expectedMappings), decoded.PrimaryMappings.Select(m => m.EntityIndex).ToArray());
        Assert.True(decoded.ParentReferenceIsNull);
        Assert.Equal(0u, decoded.ParentReferenceId);

        var chainIds = ReadFirstChainIds(stream, doc);
        Assert.Equal(ParseIds(expectedChain), chainIds);
        Assert.True(decoded.MatchesReversedChain(chainIds));
        Assert.True(decoded.MatchesEntitySet(chainIds));

        // Decoding must not modify the stream.
        Assert.Equal(stream, (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
    }

    [Theory]
    [InlineData("SelectionAuthoring/select_region", "3,4,5,6,7,8")]
    [InlineData("SelectionAuthoring/select_region_overlap", "3,4,5,6")]
    public void FromSkt_RegionPrimaryMappingMatchesRegionBoundarySet(string relativePath, string expectedEdges)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"));
        var decoded = FromSktEntSurfIdRepParser.Read(doc, element);
        Assert.NotNull(decoded);
        Assert.True(decoded!.ParentReferenceIsNull);
        Assert.True(decoded.MatchesEntitySet(ParseIds(expectedEdges)));

        var extrusion = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        var region = Assert.Single(SelectedSketchRegionParser.Read(doc, 0, extrusion.TagOffset));
        Assert.Equal(ParseIds(expectedEdges), region.EdgeIds);
        Assert.True(decoded.MatchesEntitySet(region.EdgeIds));
    }

    [Fact]
    public void FromSkt_WholeAndExplicitContourHaveIdenticalPrimaryMappings()
    {
        var whole = ReadFromSkt("SelectionModes/whole");
        var contour = ReadFromSkt("SelectionModes/contour");
        Assert.NotNull(whole);
        Assert.NotNull(contour);
        Assert.Equal(
            whole!.PrimaryMappings.Select(m => m.EntityIndex).ToArray(),
            contour!.PrimaryMappings.Select(m => m.EntityIndex).ToArray());
        Assert.True(whole.ParentReferenceIsNull);
        Assert.True(contour.ParentReferenceIsNull);
        // Presence/layout of the object does not distinguish whole vs explicit.
        Assert.Equal(whole.PrimaryMappings.Count, contour.PrimaryMappings.Count);
    }

    [Theory]
    [InlineData("extrude_10mm", "草图1")]
    [InlineData("SelectionModes/whole", "SelectionSeed")]
    [InlineData("SelectionModes/contour", "SelectionSeed")]
    [InlineData("SelectionModes/region", "SelectionSeed")]
    [InlineData("SelectionAuthoring/select_two_contours", "SelectionSeed")]
    [InlineData("sm_base_flange_10mm", "草图1")]
    public void ProfileFeature_DecodesSketchIdAndMatchesComponentFeatureReference(
        string relativePath, string expectedName)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var profile = Assert.Single(doc.AllElements.Where(e => e.ClassName.StartsWith("moProfileFeature_")));
        var decoded = ParentSketchReferenceParser.TryReadProfileFeature(doc, profile);
        Assert.NotNull(decoded);
        Assert.Equal(expectedName, decoded!.DisplayName);
        Assert.Equal(0x49u, decoded.SketchId);
        Assert.True(decoded.MatchesComponentFeatureReference);
    }

    [Fact]
    public void ProfileFeature_TamperedSketchId_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("extrude_10mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var profile = Assert.Single(doc.AllElements.Where(e => e.ClassName.StartsWith("moProfileFeature_")));
        int sketchIdOffset = ProfileSketchIdOffset(stream, profile);
        Assert.Equal(0x49u, BitConverter.ToUInt32(stream, sketchIdOffset));
        BitConverter.GetBytes(0u).CopyTo(stream, sketchIdOffset);
        var mutated = MfcDocument.Parse(stream);
        var mutatedProfile = Assert.Single(mutated.AllElements.Where(e => e.ClassName.StartsWith("moProfileFeature_")));
        Assert.Null(ParentSketchReferenceParser.TryReadProfileFeature(mutated, mutatedProfile));
    }

    [Fact]
    public void FromSktTamperedLayouts_AreRejected()
    {
        using var file = SwFile.Open(SamplePath("extrude_10mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"));

        byte[] badClass = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0x1234).CopyTo(badClass, element.DataStart + 2);
        var doc1 = MfcDocument.Parse(badClass);
        Assert.Null(FromSktEntSurfIdRepParser.Read(doc1,
            Assert.Single(doc1.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"))));

        byte[] badFeatureClass = (byte[])stream.Clone();
        BitConverter.GetBytes(0x99u).CopyTo(badFeatureClass, element.DataStart + 6);
        var doc2 = MfcDocument.Parse(badFeatureClass);
        Assert.Null(FromSktEntSurfIdRepParser.Read(doc2,
            Assert.Single(doc2.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"))));

        byte[] badParentRef = (byte[])stream.Clone();
        BitConverter.GetBytes(0xDEADu).CopyTo(badParentRef, element.DataStart + 80 + 8);
        var doc3 = MfcDocument.Parse(badParentRef);
        Assert.Null(FromSktEntSurfIdRepParser.Read(doc3,
            Assert.Single(doc3.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"))));
    }

    [Theory]
    [InlineData("extrude_10mm", "草图1")]
    [InlineData("SelectionAuthoring/select_region", "SelectionSeed")]
    [InlineData("sm_base_flange_10mm", "草图1")]
    public void ExtrudeAndFlangeDefinition_SketchNameUsesStructuralParentResolution(
        string relativePath, string expectedName)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        var feature = file.Features.FirstOrDefault(f =>
            f.Definition is ExtrudeFeatureDefinitionData || f.Definition is SheetMetalBaseFlangeDefinitionData);
        Assert.NotNull(feature);
        string? sketchName = feature!.Definition switch
        {
            ExtrudeFeatureDefinitionData ext => ext.SketchName,
            SheetMetalBaseFlangeDefinitionData sm => sm.SketchName,
            _ => null
        };
        Assert.Equal(expectedName, sketchName);
    }

    [Fact]
    public void FromSkt_SecondaryListCarriesSketchWideMappings()
    {
        var contour = ReadFromSkt("SelectionContours/contour");
        Assert.NotNull(contour);
        Assert.Equal(0x8114, contour!.TailClassReference);
        Assert.NotEmpty(contour.TailMappings);
        Assert.Contains(1u, contour.TailMappings.Select(m => m.EntityIndex));

        var region = ReadFromSkt("SelectionAuthoring/select_region");
        Assert.NotNull(region);
        Assert.Equal(0x81BD, region!.TailClassReference);
        Assert.NotEmpty(region.TailMappings);
        // The first secondary record of a region payload carries the 0x86A0 marker.
        Assert.Contains(region.TailMappings, m => m.ClassTag == 0x86A0);
    }

    [Fact]
    public void FaceRef_GeneratedFaceReferenceIsNotAnUpToSurfaceSelection()
    {
        // moFaceRef_c is 38 bytes:
        //   +0 u32 FaceCount (1) | +4 u32 0 | +8 u32 type (5/6) |
        //   +12 u32 class word (0x300 / 0x200) | +16 u32 face/class id |
        //   +20 16-byte archive id | +36 u16 0.
        // It occurs in blind extrusions, sweeps, revolves and fillets, i.e. even
        // when no terminating face was selected, so it is a generated-face
        // reference and must not be promoted to an UpToSurface target.
        using var file = SwFile.Open(SamplePath("extrude_10mm"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var elements = doc.AllElements.ToList();
        var element = Assert.Single(elements.Where(e => e.ClassName == "moFaceRef_c"));
        int index = elements.IndexOf(element);
        int end = index + 1 < elements.Count ? elements[index + 1].TagOffset : stream.Length;
        byte[] raw = stream[element.DataStart..end];
        Assert.Equal(38, raw.Length);
        var faceRef = new FaceRefData();
        TopologyParsers.ParseEnrichedFaceRef(faceRef, raw);
        Assert.Equal(1u, faceRef.FaceCount);
        Assert.Equal(6u, faceRef.TopologyType);
        Assert.Empty(faceRef.FaceIds);
        Assert.Equal(raw.Length - 4, faceRef.TopologyData.Length);

        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Definition);
        Assert.Equal(0u, definition.EndCondition); // Blind: no terminating entity.
        Assert.Null(definition.EndConditionEntity);

        var endTarget = TopologyDecoder.BuildEndConditionEntity(faceRef, null, null);
        Assert.NotNull(endTarget);
        Assert.False(endTarget!.IsValid); // FaceCount alone is not a selected face.
    }

    [Theory]
    [InlineData("extrude_10mm")]
    [InlineData("SelectionModes/whole")]
    public void Resave_KeepsFromSktAndParentSketchStable(string relativePath)
    {
        var main = ReadPair(relativePath);
        var resave = ReadPair(relativePath + "_resave");
        Assert.Equal(main.Mappings, resave.Mappings);
        Assert.Equal(main.FeatureClass, resave.FeatureClass);
        Assert.Equal(main.ParentReferenceId, resave.ParentReferenceId);
        Assert.Equal(main.TailClassReference, resave.TailClassReference);
        Assert.Equal(main.SketchId, resave.SketchId);
        Assert.Equal(main.DisplayName, resave.DisplayName);
    }

    private static (string Mappings, uint FeatureClass, uint ParentReferenceId, ushort TailClassReference,
        uint SketchId, string DisplayName) ReadPair(string relativePath)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var fromSkt = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"));
        var decoded = FromSktEntSurfIdRepParser.Read(doc, fromSkt);
        Assert.NotNull(decoded);
        var profile = Assert.Single(doc.AllElements.Where(e => e.ClassName.StartsWith("moProfileFeature_")));
        var parent = ParentSketchReferenceParser.TryReadProfileFeature(doc, profile);
        Assert.NotNull(parent);
        return (
            string.Join(",", decoded!.PrimaryMappings.Select(m => m.EntityIndex)),
            decoded.FeatureClass,
            decoded.ParentReferenceId,
            decoded.TailClassReference,
            parent!.SketchId,
            parent.DisplayName);
    }

    private static FromSktSelectionData? ReadFromSkt(string relativePath)
    {
        using var file = SwFile.Open(SamplePath(relativePath), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = MfcDocument.Parse(stream);
        var element = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromSktEntSurfIdRep_c"));
        return FromSktEntSurfIdRepParser.Read(doc, element);
    }

    private static uint[] ReadFirstChainIds(byte[] stream, MfcDocument doc)
    {
        var chain = doc.AllElements.FirstOrDefault(e => e.ClassName == "moSketchChain_c");
        if (chain == null) return Array.Empty<uint>();
        int count = BitConverter.ToUInt16(stream, chain.DataStart);
        var ids = new uint[count];
        for (int i = 0; i < count; i++) ids[i] = BitConverter.ToUInt32(stream, chain.DataStart + 2 + i * 4);
        return ids;
    }

    private static int ProfileSketchIdOffset(byte[] stream, MfcElement profile)
    {
        int offset = profile.DataStart;
        while (offset < profile.DataStart + 16 && !MfcPrimitives.IsWCharPrefix(stream, offset)) offset++;
        int cursor = offset;
        MfcPrimitives.ReadWChar(stream, ref cursor);
        return cursor + 8;
    }

    private static uint[] ParseIds(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => Convert.ToUInt32(v.Trim(), 10)).ToArray();

    private static string SamplePath(string relativePath)
    {
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
