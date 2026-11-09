using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Sweep/Loft selection-layer decoding evidence (2026 controlled samples in
/// SldFileTests/TestFiles/Features2026/{SweepOptions,LoftOptions}).
///
/// Folder header (after the display-name WChar): +0x00 f64 timestamp,
/// +0x08 u32 opaque type code, +0x10 empty WChar, +0x14 six zero bytes,
/// +0x1A u16 selection count, +0x1C count x u16 opaque selection ids.
///
/// 52-byte sketch reference record (inside moCompProfile_c / moWireContact_c):
///   38 4A 00 00 | u32 sketch id | u32 serial | 01/00 | 0 | opaque | 0 |
///   0/FFFFFFFF | 0 | FFFFCFC7 x3 | 0.
/// moCompProfile_c: first record +81, object stride 188. Sweep = one primary
/// record; loft = one record per section (2/3). Circular sweep: the single
/// primary record is the path (native Profile=null).
/// moWireContact_c: first record +147, object stride 190. Sweep object 0 = path,
/// later objects = guides; loft objects = guides.
/// Header ids are opaque: their count equals the number of records and no
/// byte-level link to sketch ids exists, so they are correlated by position.
/// The Agent 5 creation harness selects profile (mark 1) -> path (mark 4) ->
/// guide (mark 2) and loft profiles -> guide, which is exactly the wrapper order;
/// header lists are not numerically sorted (sweep_guide1: C5, DC, D1), consistent
/// with insertion order.
///
/// Thin sweep: moSideFace3IntSurfIdRep_c replaces moSweepSideSurfIdRep_c, and
/// moWireContact_c[+285]==1 / f64@+286==0.001; the same 0.001 is at
/// moLengthParameter_c[+32] after ParallelPlaneDistanceDim_c (so the thickness is
/// present in ResolvedFeatures, not only Config-0).
/// Circular sweep: ThreeDdiameterDim_c + moLengthParameter_c[+32] = 0.012.
/// </summary>
public class TopologySweepLoftSelectionTests
{
    [Theory]
    [InlineData("sweep_circle10_path40", 0x4Cu, new ushort[] { 0xC2, 0xCD }, new uint[] { 0x49 }, new uint[] { 0x4A }, false, false)]
    [InlineData("sweep_circle10_path60", 0x4Cu, new ushort[] { 0xC2, 0xCD }, new uint[] { 0x49 }, new uint[] { 0x4A }, false, false)]
    [InlineData("sweep_circular_profile12", 0x4Bu, new ushort[] { 0xBF }, new uint[] { 0x49 }, new uint[0], true, false)]
    [InlineData("sweep_guide1", 0x4Du, new ushort[] { 0xC5, 0xDC, 0xD1 }, new uint[] { 0x49 }, new uint[] { 0x4B, 0x4A }, false, false)]
    [InlineData("sweep_profile_rect", 0x4Cu, new ushort[] { 0xC2, 0xDF }, new uint[] { 0x49 }, new uint[] { 0x4A }, false, false)]
    [InlineData("sweep_thin_one_dir_t1", 0x4Cu, new ushort[] { 0xC2, 0xCD }, new uint[] { 0x49 }, new uint[] { 0x4A }, false, true)]
    public void Sweep_DecodesRolesAndSketchPersistentIds(
        string name, uint typeCode, ushort[] headerIds, uint[] profiles, uint[] secondary,
        bool circular, bool thin)
    {
        using var file = SwFile.Open(SamplePath("SweepOptions", name), readOnly: true);
        var doc = MfcDocument.Parse(file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSweep_c"));
        var decoded = SweepLoftSelectionDecoder.Decode(doc, feature);
        Assert.NotNull(decoded);
        Assert.Equal(SweepLoftSelectionKind.Sweep, decoded!.Kind);
        Assert.Equal(typeCode, decoded.SerializedTypeCode);
        Assert.Equal(headerIds, decoded.HeaderSelectionIds);
        Assert.Equal(circular, decoded.CircularProfileLayout);
        Assert.Equal(thin, decoded.ThinFeatureLayout);

        if (circular)
        {
            var reference = Assert.Single(decoded.References);
            Assert.Equal(SketchCurveRole.Path, reference.Role);
            Assert.Equal(0x49u, reference.EntityId);
            Assert.Equal(0.012, decoded.CircularProfileDiameter!.Value, 12);
        }
        else
        {
            var decodedProfiles = decoded.References.Where(r => r.Role == SketchCurveRole.Profile)
                .Select(r => r.EntityId).ToArray();
            Assert.Equal(profiles, decodedProfiles);
            var path = Assert.Single(decoded.References.Where(r => r.Role == SketchCurveRole.Path));
            Assert.Equal(secondary[0], path.EntityId);
            var guides = decoded.References.Where(r => r.Role == SketchCurveRole.Guide).Select(r => r.EntityId).ToArray();
            Assert.Equal(secondary.Skip(1).ToArray(), guides);
        }

        if (thin)
            Assert.Equal(0.001, decoded.ThinWallThickness!.Value, 12);
        else
            Assert.Null(decoded.ThinWallThickness);

        // The opaque header ids are correlated to references by position only.
        Assert.Equal(headerIds.Length, decoded.References.Count);
        for (int i = 0; i < headerIds.Length; i++)
        {
            Assert.Equal(headerIds[i], decoded.References[i].HeaderSelectionId);
            Assert.True(decoded.References[i].IsHeaderSelectionIdPositional);
        }
    }

    [Theory]
    [InlineData("loft_circle10_20", 0x4Du, new ushort[] { 0xC5, 0xE1 }, new uint[] { 0x49, 0x4C }, new uint[0])]
    [InlineData("loft_three_sections", 0x50u, new ushort[] { 0xCB, 0xE7, 0xFB }, new uint[] { 0x49, 0x4C, 0x4F }, new uint[0])]
    [InlineData("loft_guide1", 0x4Eu, new ushort[] { 0xD7, 0xF2, 0xC8 }, new uint[] { 0x4A, 0x4D }, new uint[] { 0x49 })]
    [InlineData("loft_start_end_tangency", 0x4Du, new ushort[] { 0xC5, 0xE1 }, new uint[] { 0x49, 0x4C }, new uint[0])]
    public void Loft_DecodesSectionOrderAndGuideReferences(
        string name, uint typeCode, ushort[] headerIds, uint[] profiles, uint[] guides)
    {
        using var file = SwFile.Open(SamplePath("LoftOptions", name), readOnly: true);
        var doc = MfcDocument.Parse(file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moBlend_c"));
        var decoded = SweepLoftSelectionDecoder.Decode(doc, feature);
        Assert.NotNull(decoded);
        Assert.Equal(SweepLoftSelectionKind.Loft, decoded!.Kind);
        Assert.Equal(typeCode, decoded.SerializedTypeCode);
        Assert.Equal(headerIds, decoded.HeaderSelectionIds);
        Assert.False(decoded.CircularProfileLayout);
        Assert.False(decoded.ThinFeatureLayout);
        Assert.Equal(profiles, decoded.References.Where(r => r.Role == SketchCurveRole.Profile).Select(r => r.EntityId).ToArray());
        Assert.Equal(guides, decoded.References.Where(r => r.Role == SketchCurveRole.Guide).Select(r => r.EntityId).ToArray());
        Assert.Empty(decoded.References.Where(r => r.Role == SketchCurveRole.Path));
    }

    [Theory]
    [InlineData("SweepOptions", "sweep_circle10_path40", "moSweep_c")]
    [InlineData("SweepOptions", "sweep_circular_profile12", "moSweep_c")]
    [InlineData("SweepOptions", "sweep_guide1", "moSweep_c")]
    [InlineData("SweepOptions", "sweep_thin_one_dir_t1", "moSweep_c")]
    [InlineData("LoftOptions", "loft_circle10_20", "moBlend_c")]
    [InlineData("LoftOptions", "loft_three_sections", "moBlend_c")]
    [InlineData("LoftOptions", "loft_guide1", "moBlend_c")]
    [InlineData("LoftOptions", "loft_start_end_tangency", "moBlend_c")]
    public void Resave_KeepsDecodedSelectionLayerStable(string folder, string name, string className)
    {
        using var main = SwFile.Open(SamplePath(folder, name), readOnly: true);
        using var resave = SwFile.Open(SamplePath(folder, name + "_resave"), readOnly: true);
        var a = Decode(main.RawStreams["Contents/Config-0-ResolvedFeatures"], className);
        var b = Decode(resave.RawStreams["Contents/Config-0-ResolvedFeatures"], className);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a!.Kind, b!.Kind);
        Assert.Equal(a.SerializedTypeCode, b.SerializedTypeCode);
        Assert.Equal(a.HeaderSelectionIds, b.HeaderSelectionIds);
        Assert.Equal(a.CircularProfileLayout, b.CircularProfileLayout);
        Assert.Equal(a.ThinFeatureLayout, b.ThinFeatureLayout);
        Assert.Equal(a.CircularProfileDiameter, b.CircularProfileDiameter);
        Assert.Equal(a.ThinWallThickness, b.ThinWallThickness);
        Assert.Equal(a.References.Count, b.References.Count);
        for (int i = 0; i < a.References.Count; i++)
        {
            Assert.Equal(a.References[i].Role, b.References[i].Role);
            Assert.Equal(a.References[i].EntityId, b.References[i].EntityId);
            Assert.Equal(a.References[i].ArchiveSerial, b.References[i].ArchiveSerial);
            Assert.Equal(a.References[i].HeaderSelectionId, b.References[i].HeaderSelectionId);
        }
    }

    [Fact]
    public void ElementOnlyOverload_MatchesDocumentOverload()
    {
        using var file = SwFile.Open(SamplePath("SweepOptions", "sweep_guide1"), readOnly: true);
        var doc = MfcDocument.Parse(file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSweep_c"));
        var withDoc = SweepLoftSelectionDecoder.Decode(doc, feature);
        var withElement = SweepLoftSelectionDecoder.Decode(feature);
        Assert.NotNull(withDoc);
        Assert.NotNull(withElement);
        Assert.Equal(withDoc!.HeaderSelectionIds, withElement!.HeaderSelectionIds);
        Assert.Equal(
            withDoc.References.Select(r => (r.Role, r.EntityId)).ToArray(),
            withElement.References.Select(r => (r.Role, r.EntityId)).ToArray());
    }

    [Fact]
    public void TamperedReferenceRecord_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("SweepOptions", "sweep_circle10_path40"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSweep_c"));
        var wrapper = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompProfile_c"));
        int record = wrapper.DataStart + 81;
        Assert.Equal(0x4A38u, BitConverter.ToUInt32(stream, record));
        Assert.NotNull(SweepLoftSelectionDecoder.Decode(doc, feature));

        BitConverter.GetBytes(0xDEAD0000u).CopyTo(stream, record);
        var mutated = MfcDocument.Parse(stream);
        Assert.Null(SweepLoftSelectionDecoder.Decode(mutated,
            Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moSweep_c"))));
    }

    [Fact]
    public void TamperedReferenceSentinel_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("SweepOptions", "sweep_circle10_path40"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSweep_c"));
        var wrapper = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompProfile_c"));
        int sentinel = wrapper.DataStart + 81 + 36;
        BitConverter.GetBytes(0u).CopyTo(stream, sentinel);
        var mutated = MfcDocument.Parse(stream);
        Assert.Null(SweepLoftSelectionDecoder.Decode(mutated,
            Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moSweep_c"))));
    }

    [Fact]
    public void TamperedSecondaryReferenceRecord_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("SweepOptions", "sweep_circle10_path40"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSweep_c"));
        var wrapper = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moWireContact_c"));
        int record = wrapper.DataStart + 147;
        Assert.Equal(0x4A38u, BitConverter.ToUInt32(stream, record));
        // Break the magic so the long wrapper has no decodable reference; the
        // layout is then unresolved rather than silently dropping the path.
        BitConverter.GetBytes(0xDEAD0000u).CopyTo(stream, record);
        var mutated = MfcDocument.Parse(stream);
        Assert.Null(SweepLoftSelectionDecoder.Decode(mutated,
            Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moSweep_c"))));
    }

    [Fact]
    public void HeaderSelectionCountMismatch_IsRejected()
    {
        using var file = SwFile.Open(SamplePath("SweepOptions", "sweep_guide1"), readOnly: true);
        byte[] stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moSweep_c"));
        // Locate the selection count (u16 at header + 0x1A) from the display name end.
        int nameOffset = feature.DataStart;
        while (!MfcPrimitives.IsWCharPrefix(stream, nameOffset)) nameOffset++;
        int cursor = nameOffset;
        MfcPrimitives.ReadWChar(stream, ref cursor);
        int countOffset = cursor + 0x1A;
        Assert.Equal((ushort)3, BitConverter.ToUInt16(stream, countOffset));
        BitConverter.GetBytes((ushort)2).CopyTo(stream, countOffset);
        var mutated = MfcDocument.Parse(stream);
        Assert.Null(SweepLoftSelectionDecoder.Decode(mutated,
            Assert.Single(mutated.AllElements.Where(e => e.ClassName == "moSweep_c"))));
    }

    [Fact]
    public void TruncatedFeatureWindow_DoesNotThrow()
    {
        using var file = SwFile.Open(SamplePath("LoftOptions", "loft_three_sections"), readOnly: true);
        byte[] raw = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = MfcDocument.Parse(raw);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moBlend_c"));
        int[] cuts = { feature.DataStart, feature.DataStart + 60, feature.DataStart + 0xC0, feature.DataStart + 0x120 };
        foreach (int cut in cuts)
        {
            byte[] truncated = raw.Take(cut).ToArray();
            var mutated = MfcDocument.Parse(truncated);
            var elements = mutated.AllElements.Where(e => e.ClassName == "moBlend_c").ToArray();
            if (elements.Length == 0) continue;
            var exception = Record.Exception(() => SweepLoftSelectionDecoder.Decode(mutated, elements[0]));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void UnknownClassOrNullInput_ReturnsNull()
    {
        Assert.Null(SweepLoftSelectionDecoder.Decode((MfcElement)null!));
        Assert.Null(SweepLoftSelectionDecoder.Decode((MfcDocument)null!, null!));
        using var file = SwFile.Open(SamplePath("SweepOptions", "sweep_circle10_path40"), readOnly: true);
        var doc = MfcDocument.Parse(file.RawStreams["Contents/Config-0-ResolvedFeatures"]);
        var other = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moProfileFeature_c"));
        Assert.Null(SweepLoftSelectionDecoder.Decode(doc, other));
    }

    private static SweepLoftSelectionData? Decode(byte[] stream, string className)
    {
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == className));
        return SweepLoftSelectionDecoder.Decode(doc, feature);
    }

    private static string SamplePath(string folder, string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", folder, name + ".SLDPRT");
        if (File.Exists(path)) return path;
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "SldFileTests", "TestFiles", "Features2026", folder, name + ".SLDPRT");
            if (File.Exists(candidate)) return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        throw new FileNotFoundException($"Sample {folder}/{name}.SLDPRT not found");
    }
}
