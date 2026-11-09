using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// UpToSurface cut specimen (ExtrudeOptions/extrude_upto_surface_modify.SLDPRT
/// and its resave; native: EndCondition=4, end entity type Face, depth 0,
/// AutoSelect=false, FeatureScope=true, FeatureScopeBodiesCount=1).
///
/// Structural evidence:
///   moICE_c header: selection count 1, id 0x011F;
///   moSingleFaceRef_w (473 B, inside the moICE_c folder): u16 0x8151 +
///     81-byte component header (flag 0x40) + 47-byte face reference at +83
///     (magic, surface class 0x80EA, face count 6, class word 0x200, face id
///     0x7CB, archive GUID) + 20-byte mapping records from +130 (first record
///     entity index 0);
///   moCompSolidBody_c (327 B, inside the moICE_c folder): header + refs at
///     +81/+243 + owner class word 0xFFFFFFFF at +263 (explicit scope body) +
///     body id 2479 (0x09AF) at +305 and the 16-byte archive GUID at +309;
///   moFromEndSpec_c (451 B) sits in the preceding BaseBox folder range and its
///     +260 word is 4 in blind cuts too, so it is not an end-condition source;
///   moFaceRef_c / moCompFace_c describe generated faces (the cut's generated
///     face group has a different face id) and are never used as the target.
/// </summary>
public class TopologyUpToSurfaceSelectionTests
{
    [Theory]
    [InlineData("ExtrudeOptions/extrude_upto_surface_modify", 0x7CBu)]
    [InlineData("ExtrudeOptions/extrude_upto_surface_modify_resave", 0x0AFAu)]
    public void UpToSurfaceCut_DecodesEndFaceAndBodyScope(string name, uint faceId)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        var definitions = file.Features.Select(f => f.Model).OfType<ExtrudeFeatureModel>().ToArray();
        var baseBox = definitions.Single(d => !d.IsCut);
        var cut = definitions.Single(d => d.IsCut);

        Assert.Equal(0u, baseBox.EndCondition);
        Assert.Null(baseBox.EndConditionEntity);
        Assert.True(baseBox.BodyScope!.AutoSelect);
        Assert.Empty(baseBox.BodyScope.BodyIds);

        Assert.Equal(4u, cut.EndCondition);
        Assert.True(cut.IsResolved(nameof(ExtrudeFeatureModel.EndCondition)));
        Assert.Equal(0d, cut.Depth);
        var target = Assert.IsType<EndConditionEntityReference>(cut.EndConditionEntity);
        Assert.Equal(SelectionEntityType.Face, target.EntityType);
        Assert.Equal(faceId, target.EntityId);
        Assert.Equal(1u, target.FaceCount);
        Assert.True(target.IsValid);
        Assert.Contains($"Face_0x{faceId:X}", target.SurfaceName);
        Assert.True(cut.IsResolved(nameof(ExtrudeFeatureModel.EndConditionEntity)));

        Assert.False(cut.AutoSelect);
        Assert.True(cut.FeatureScope);
        Assert.False(cut.BodyScope!.AutoSelect);
        Assert.Equal(new uint[] { 2479u }, cut.BodyScope.BodyIds.ToArray());
        var body = Assert.Single(cut.BodyScope.Bodies);
        Assert.Equal(2479u, body.EntityId);
        Assert.Equal(SelectionEntityType.Body, body.EntityType);
        Assert.True(cut.IsResolved(nameof(ExtrudeFeatureModel.BodyScope)));
        Assert.True(cut.IsResolved(nameof(ExtrudeFeatureModel.AutoSelect)));
        Assert.True(cut.IsResolved(nameof(ExtrudeFeatureModel.FeatureScope)));
    }

    [Theory]
    [InlineData("ExtrudeOptions/extrude_upto_surface_modify", 0x7CBu)]
    [InlineData("ExtrudeOptions/extrude_upto_surface_modify_resave", 0x0AFAu)]
    public void UpToSurfaceObjects_DecodeStructuralFields(string name, uint faceId)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);

        var singleFace = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSingleFaceRef_w"));
        var face = PersistentFaceReferenceParser.ReadSingleFaceRef(doc, singleFace);
        Assert.NotNull(face);
        Assert.Equal((byte)0x40, face!.HeaderFlag);
        Assert.Equal((ushort)0x80EA, face.FaceReference.SurfaceClass);
        Assert.Equal(6u, face.FaceReference.FaceCount);
        Assert.Equal(0x200u, face.FaceReference.ClassWord);
        Assert.Equal(faceId, face.FaceReference.FaceId);
        Assert.Equal((ushort)0x80EC, face.FaceReference.MappingClassReference);
        Assert.Equal(5, face.Mappings.Count);
        Assert.Equal(0u, face.Mappings[0].EntityIndex);
        Assert.Equal(new uint[] { 0, 4, 3, 2, 1 }, face.Mappings.Select(m => m.EntityIndex).ToArray());
        Assert.Equal(0x54B249AD2594C37DUL, face.FaceReference.ArchiveIdLow);

        var compBody = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        var body = CompSolidBodyReferenceParser.Read(doc, compBody);
        Assert.NotNull(body);
        Assert.Equal(2479u, body!.BodyId);
        Assert.Equal(0xFFFFFFFFu, body.BodyClassWord);
        Assert.True(body.IsExplicitBodySelection);
        Assert.Equal(0x54B249AD2594C37DUL, body.ArchiveIdLow);

        // The cut's generated-face block is a different face group (face id 0x775):
        // it must not be promoted to the terminating entity.
        var compFaceElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompFace_c"));
        var compFace = PersistentFaceReferenceParser.ReadCompFace(doc, compFaceElement);
        Assert.NotNull(compFace);
        Assert.Equal(CompFaceLayout.Full, compFace!.Layout);
        Assert.Equal(0x775u, compFace.Blocks[0].FaceReference.FaceId);
        Assert.NotEqual(face.FaceReference.FaceId, compFace.Blocks[0].FaceReference.FaceId);

        // Decoding must not modify the stream.
        Assert.Equal(stream, (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
    }

    [Fact]
    public void UpToSurface_TamperedSingleFaceRef_LeavesEndFaceUnresolved()
    {
        using var file = SwFile.Open(SamplePath("ExtrudeOptions/extrude_upto_surface_modify"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var singleFace = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSingleFaceRef_w"));
        BitConverter.GetBytes(0xDEADu).CopyTo(stream, singleFace.DataStart + 83);
        var mutated = MfcDocument.Parse(stream);
        var mutatedFace = Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moSingleFaceRef_w"));
        Assert.Null(PersistentFaceReferenceParser.ReadSingleFaceRef(mutated, mutatedFace));

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(mutated, model);
        var cut = Assert.Single(model.Folders.Where(f => f.ClassName == "moICE_c")).FeatureData;
        var definition = Assert.IsType<ExtrudeFeatureModel>(cut);
        // The malformed selection object yields no structural claim at all.
        Assert.NotEqual(4u, definition.EndCondition);
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.EndCondition)));
        Assert.Null(definition.EndConditionEntity);
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.EndConditionEntity)));
    }

    [Fact]
    public void UpToSurface_PrecedingFromEndSpec_TamperingDoesNotAffectEndFace()
    {
        using var file = SwFile.Open(SamplePath("ExtrudeOptions/extrude_upto_surface_modify"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);

        // moFromEndSpec_c is serialized inside the preceding BaseBox folder range,
        // before moICE_c, and is not the cut's end-condition object.
        var fromEndSpec = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromEndSpec_c"));
        int baseStart = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c")).TagOffset;
        int cutStart = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moICE_c")).TagOffset;
        Assert.InRange(fromEndSpec.TagOffset, baseStart + 1, cutStart - 1);

        // Its +260 word is not an end condition: it is also 4 for the blind
        // extrude_cut_5mm specimen (see BlindCut_CompSolidBodyIsNotExplicitScope).
        Assert.Equal(0x0000BF80u, BitConverter.ToUInt32(stream, fromEndSpec.DataStart + 256));
        Assert.Equal(4u, BitConverter.ToUInt32(stream, fromEndSpec.DataStart + 260));

        BitConverter.GetBytes(0u).CopyTo(stream, fromEndSpec.DataStart + 256);
        BitConverter.GetBytes(0u).CopyTo(stream, fromEndSpec.DataStart + 260);
        var mutated = MfcDocument.Parse(stream);
        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(mutated, model);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(model.Folders.Where(f => f.ClassName == "moICE_c")).FeatureData);

        // The cut's terminating face is decoded from moSingleFaceRef_w only.
        Assert.Equal(4u, definition.EndCondition);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.EndCondition)));
        var target = Assert.IsType<EndConditionEntityReference>(definition.EndConditionEntity);
        Assert.Equal(SelectionEntityType.Face, target.EntityType);
        Assert.Equal(0x7CBu, target.EntityId);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.EndConditionEntity)));
    }

    [Fact]
    public void UpToSurface_TamperedBodyId_FallsBackToDefaultScope()
    {
        using var file = SwFile.Open(SamplePath("ExtrudeOptions/extrude_upto_surface_modify"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var compBody = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        BitConverter.GetBytes(0u).CopyTo(stream, compBody.DataStart + 305);
        var mutated = MfcDocument.Parse(stream);
        var mutatedBody = Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        Assert.Null(CompSolidBodyReferenceParser.Read(mutated, mutatedBody));

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(mutated, model);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(model.Folders.Where(f => f.ClassName == "moICE_c")).FeatureData);
        Assert.Equal(4u, definition.EndCondition);
        Assert.NotNull(definition.EndConditionEntity);
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.BodyScope)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.FeatureScope)));
    }

    [Fact]
    public void UpToSurface_TamperedBodyClassWord_LeavesScopeUnresolved()
    {
        using var file = SwFile.Open(SamplePath("ExtrudeOptions/extrude_upto_surface_modify"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var compBody = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        // 0x4F is the auto-resolved body class word measured on blind/through-all cuts.
        BitConverter.GetBytes(0x4Fu).CopyTo(stream, compBody.DataStart + 263);
        var mutated = MfcDocument.Parse(stream);
        var mutatedBody = Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        var body = CompSolidBodyReferenceParser.Read(mutated, mutatedBody);
        Assert.NotNull(body);
        Assert.Equal(0x4Fu, body!.BodyClassWord);
        Assert.False(body.IsExplicitBodySelection);

        var model = new ResolvedFeaturesModel();
        ResolvedFeaturesModel.Map(mutated, model);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(model.Folders.Where(f => f.ClassName == "moICE_c")).FeatureData);
        // The terminating-face decode is independent of the body class word.
        Assert.Equal(4u, definition.EndCondition);
        Assert.NotNull(definition.EndConditionEntity);
        Assert.True(definition.IsResolved(nameof(ExtrudeFeatureModel.EndConditionEntity)));
        // An auto-resolved body reference must not claim explicit scope/AutoSelect values.
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.BodyScope)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.AutoSelect)));
        Assert.False(definition.IsResolved(nameof(ExtrudeFeatureModel.FeatureScope)));
    }

    [Fact]
    public void BlindCut_CompSolidBodyIsNotExplicitScope()
    {
        using var file = SwFile.Open(SamplePath("extrude_cut_5mm"), readOnly: true);
        var cut = Assert.Single(file.Features
            .Select(f => f.Model)
            .OfType<ExtrudeFeatureModel>()
            .Where(d => d.IsCut));

        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = MfcDocument.Parse(stream);

        // The blind cut carries the same 327-byte body record but with the creating
        // feature class (0x4F) and no terminating-face object.
        var compBody = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        var body = CompSolidBodyReferenceParser.Read(doc, compBody);
        Assert.NotNull(body);
        Assert.Equal(0x4Fu, body!.BodyClassWord);
        Assert.False(body.IsExplicitBodySelection);
        Assert.DoesNotContain(doc.AllElements, e => e.ClassName == "moSingleFaceRef_w");

        // Its preceding moFromEndSpec_c has the same +256 signature and +260 word as
        // the UpToSurface specimen, proving why that word is not an end condition.
        var fromEndSpec = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFromEndSpec_c"));
        Assert.Equal(0x0000BF80u, BitConverter.ToUInt32(stream, fromEndSpec.DataStart + 256));
        Assert.Equal(4u, BitConverter.ToUInt32(stream, fromEndSpec.DataStart + 260));

        Assert.NotEqual(4u, cut.EndCondition);
        Assert.Null(cut.EndConditionEntity);
        Assert.True(cut.BodyScope!.AutoSelect);
        Assert.False(cut.IsResolved(nameof(ExtrudeFeatureModel.BodyScope)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(80)]
    [InlineData(129)]
    [InlineData(200)]
    public void StructuralFaceParsers_TruncatedPayloads_ReturnNull(int length)
    {
        using var file = SwFile.Open(SamplePath("ExtrudeOptions/extrude_upto_surface_modify"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var singleFace = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSingleFaceRef_w"));
        var compBody = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompSolidBody_c"));
        byte[] faceRaw = stream[singleFace.DataStart..NextTagOffset(doc, singleFace)];
        byte[] bodyRaw = stream[compBody.DataStart..NextTagOffset(doc, compBody)];
        Assert.Null(PersistentFaceReferenceParser.ReadSingleFaceRefPayload(faceRaw.Take(length).ToArray()));
        Assert.Null(CompSolidBodyReferenceParser.ReadPayload(bodyRaw.Take(length).ToArray()));
    }

    [Theory]
    [InlineData("extrude_10mm")]
    [InlineData("extrude_midplane")]
    public void BlindAndMidPlaneSamples_DoNotClaimTerminatingEntity(string name)
    {
        using var file = SwFile.Open(SamplePath(name), readOnly: true);
        var definition = Assert.IsType<ExtrudeFeatureModel>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion)).Model);
        if (name == "extrude_midplane") Assert.Equal(6u, definition.EndCondition);
        else Assert.Equal(0u, definition.EndCondition);
        Assert.Null(definition.EndConditionEntity);

        // A generated moFaceRef_c exists but is not promoted to an end target.
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var doc = MfcDocument.Parse(stream);
        Assert.Contains(doc.AllElements, e => e.ClassName == "moFaceRef_c");
        Assert.DoesNotContain(doc.AllElements, e => e.ClassName == "moSingleFaceRef_w");
    }

    private static int NextTagOffset(MfcDocument doc, MfcElement element)
    {
        var elements = doc.AllElements;
        for (int i = 0; i < elements.Count; i++)
        {
            if (!ReferenceEquals(elements[i], element)) continue;
            return i + 1 < elements.Count ? elements[i + 1].TagOffset : doc.SourceData.Length;
        }
        return element.DataEnd;
    }

    private static string SamplePath(string name)
    {
        var relative = name.Replace('/', Path.DirectorySeparatorChar) + ".SLDPRT";
        var direct = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", relative);
        if (File.Exists(direct)) return direct;
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", relative);
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample Features2026/{relative} not found");
    }
}
