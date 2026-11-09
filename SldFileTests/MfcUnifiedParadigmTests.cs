using System;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class MfcUnifiedParadigmTests
{
    [Fact]
    public void MfcArchiveContext_RegistersAndResolvesCompactClassReferences()
    {
        var ctx = new MfcArchiveContext();

        bool firstExtrude = ctx.RegisterOrGetClass("moExtrusion_c", out ushort extrudeTag, tagOffset: 16);
        bool secondExtrude = ctx.RegisterOrGetClass("moExtrusion_c", out ushort extrudeTagRepeat, tagOffset: 64);
        bool firstEndSpec = ctx.RegisterOrGetClass("moEndSpec_c", out ushort endSpecTag, tagOffset: 120, explicitTag: 0x8109);

        Assert.True(firstExtrude);
        Assert.False(secondExtrude);
        Assert.Equal(extrudeTag, extrudeTagRepeat);
        Assert.Equal((ushort)0x8001, extrudeTag);

        Assert.True(firstEndSpec);
        Assert.Equal((ushort)0x8109, endSpecTag);

        Assert.True(ctx.TryResolveClassReference(0x8001, out string resolved1));
        Assert.Equal("moExtrusion_c", resolved1);

        Assert.True(ctx.TryResolveClassReference(0x8109, out string resolved2));
        Assert.Equal("moEndSpec_c", resolved2);

        byte[] sampleBytes = { 0x09, 0x81 };
        Assert.True(MfcArchiveContext.IsCompactReferenceTag(sampleBytes, 0, out ushort parsedTag));
        Assert.Equal((ushort)0x8109, parsedTag);
    }

    [Fact]
    public void MfcStreamWriter_EmitsFullTagThenCompactRefTag()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteMfcClassTagOrRef("moLengthParameter_c", explicitRefTag: 0x810F);
        writer.WriteMfcClassTagOrRef("moLengthParameter_c", explicitRefTag: 0x810F);

        byte[] bytes = writer.ToArray();
        // First occurrence: FFFF + 0x0001 + len(19) + "moLengthParameter_c" = 2 + 2 + 2 + 19 = 25 bytes
        // Second occurrence: 0x810F = 2 bytes (0x0F, 0x81)
        Assert.Equal(27, bytes.Length);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFF, bytes[1]);
        Assert.Equal(0x0F, bytes[25]);
        Assert.Equal(0x81, bytes[26]);
    }

    [Fact]
    public void MfcParameterReader_ReadsValidatesAndPatchesNamedParameters()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteU16(0x8004);
        writer.WriteWChar("D1");
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteU16(0);
        writer.WriteF64(0.025);
        // Append one positive copy slot and one negative copy slot
        int posSlot = writer.Position;
        writer.WriteF64(0.025);
        int negSlot = writer.Position;
        writer.WriteF64(-0.025);

        byte[] raw = writer.ToArray();

        Assert.True(MfcParameterReader.HasParameterName(raw, 0, "D1"));
        Assert.False(MfcParameterReader.HasParameterName(raw, 0, "D2"));

        double? val = MfcParameterReader.ReadNamedDouble(raw, 0, "D1", requireDNumberFormat: true);
        Assert.Equal(0.025, val);

        var found = MfcParameterReader.FindNamedDoubles(raw, 0, raw.Length, "D1", requireDNumberFormat: true);
        Assert.Single(found);
        Assert.Equal(0.025, found[0].Value);

        Assert.True(MfcParameterReader.TryReadUniqueDoubleByWChar(
            raw, "D1", 0, raw.Length, out double uniqueVal, out int wcharOffset, requireSchemaPrefix: true));
        Assert.Equal(0.025, uniqueVal);
        Assert.Equal(2, wcharOffset);

        Assert.True(MfcParameterReader.VerifyDoubleSlots(raw, new[] { posSlot }, new[] { negSlot }, 0.025));

        MfcParameterReader.PatchDoubleSlots(raw, new[] { posSlot }, 0.050, new[] { negSlot });
        Assert.True(MfcParameterReader.VerifyDoubleSlots(raw, new[] { posSlot }, new[] { negSlot }, 0.050));
    }

    [Fact]
    public void FeatureSchema_DeclarativelyValidatesAndBindsFeatureWindow()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteU16(0x0070); // stream version
        int start = writer.Position;
        writer.WriteMfcTag("Fillet_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar("D1");
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteU16(0);
        writer.WriteF64(0.004);
        int end = writer.Position;

        var doc = MfcDocument.Parse(writer.ToArray());

        var schema = new FeatureSchema<FilletFeatureModel>("Fillet_c", 0x51u, streamVersion: 0x0070)
            .RequireSingleNode("Fillet_c")
            .RejectIfAnyNode("Chamfer_c")
            .BindUniqueDouble(
                "D1",
                nameof(FilletFeatureModel.DefaultRadius),
                (def, v) => def.DefaultRadius = v,
                minExclusive: 0.0,
                maxInclusive: 1.0);

        var parsed = schema.TryParse(doc, "Fillet_c", 0x51u, start, end);
        Assert.NotNull(parsed);
        Assert.Equal(0.004, parsed!.DefaultRadius);
        Assert.Contains(nameof(FilletFeatureModel.DefaultRadius), parsed.ResolvedProperties);

        // Wrong TypeId or stream range must cleanly return null
        Assert.Null(schema.TryParse(doc, "Fillet_c", 0x52u, start, end));
    }

    [Fact]
    public void AuthoringSegments_AllDecomposedSegmentsLoadedAndNotEmpty()
    {
        Assert.NotEmpty(ExtrudeAuthoringSegments.BossScopeBlock);
        Assert.NotEmpty(RevolveAuthoringSegments.HeaderSuffix);
        Assert.NotEmpty(ChamferAuthoringSegments.HeaderSuffixDistanceDistance);
        Assert.NotEmpty(FilletAuthoringSegments.HeaderSuffix);
        Assert.NotEmpty(PatternAuthoringSegments.LinearHeaderSuffix);
        Assert.NotEmpty(HoleWizardAuthoringSegments.CounterBore0);
        Assert.NotEmpty(SheetMetalAuthoringSegments.BaseFlange0);
        Assert.NotEmpty(SelectionAuthoringSegments.RegionLShapeHeaderRest);
    }

    [Fact]
    public void FeatureWindowContext_ParametersAndSelections_SupportPositiveConsistentAndEdgeRef()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteU16(0x00C6);
        int start = writer.Position;
        writer.WriteMfcTag("Bend_c");
        // Write a non-positive D1 copy (-1.0) that TryGetPositiveConsistent should ignore
        writer.WriteWChar("D1");
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteU16(0);
        writer.WriteF64(-1.0);
        // Write two identical positive D1 copies (0.015) that TryGetPositiveConsistent should coalesce
        writer.WriteWChar("D1");
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteU16(0);
        writer.WriteF64(0.015);
        writer.WriteWChar("D1");
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteU16(0);
        writer.WriteF64(0.015);
        int end = writer.Position;

        var doc = MfcDocument.Parse(writer.ToArray());
        var ctx = FeatureWindowContext.TryCreate(doc, "Bend_c", 0x56u, start, end);
        Assert.NotNull(ctx);

        Assert.True(ctx!.Parameters.TryGetPositiveConsistent("D1", out double d1Value, out int d1Offset));
        Assert.Equal(0.015, d1Value);
        Assert.True(d1Offset > start);
    }

    [Fact]
    public void MfcPrimitives_WCharFields_And_MateParameterHelpers_RoundTrip()
    {
        using var writer = new MfcStreamWriter();
        writer.WriteWChar("Front@cube-1@Assem1");
        writer.WriteU16(0x8006);
        writer.WriteWChar("D1");
        writer.WriteF64(2.0);
        writer.WriteU32(uint.MaxValue);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteU16(0);
        writer.WriteF64(0.020);
        writer.WriteF64(0.020); // aligned copy
        writer.WriteWChar("Right@cylinder-1@Assem1");

        byte[] raw = writer.ToArray();
        var atFields = MfcPrimitives.ScanWCharFields(raw, 0, raw.Length, predicate: s => s.IndexOf('@') >= 0);
        Assert.Equal(2, atFields.Count);
        Assert.Equal("Front@cube-1@Assem1", atFields[0].Value);
        Assert.Equal("Right@cylinder-1@Assem1", atFields[1].Value);

        double? mateD1 = MfcParameterReader.ReadUniqueMateNamedParameter(raw, 0, raw.Length, "D1");
        Assert.Equal(0.020, mateD1);

        byte[] rewritten = MfcPrimitives.RewriteWCharFields(
            raw,
            atFields,
            new[] { "Top@partA-1@Assem1", "Top@partB-1@Assem1" });

        var updatedFields = MfcPrimitives.ScanWCharFields(rewritten, 0, rewritten.Length, predicate: s => s.IndexOf('@') >= 0);
        Assert.Equal(2, updatedFields.Count);
        Assert.Equal("Top@partA-1@Assem1", updatedFields[0].Value);
        Assert.Equal("Top@partB-1@Assem1", updatedFields[1].Value);

        double? rewrittenD1 = MfcParameterReader.ReadUniqueMateNamedParameter(rewritten, 0, rewritten.Length, "D1");
        Assert.Equal(0.020, rewrittenD1);
    }
}
