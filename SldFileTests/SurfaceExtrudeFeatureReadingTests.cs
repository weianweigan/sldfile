using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;

namespace SldFileTests;

/// <summary>
/// Reading tests for the measured 2026 surface-extrude sample. Only depth and type code
/// are decoded; end condition/draft/cap/knit-result and selections stay unset.
/// </summary>
public class SurfaceExtrudeFeatureReadingTests
{
    [Theory]
    [InlineData("surf_extrude_10mm")]
    [InlineData("surf_extrude_10mm_resave")]
    public void SurfaceExtrudeSample_DecodesDepth(string name)
    {
        using var file = SwFile.Open(Resolve(name), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Name == "SurfaceExtrude"));
        Assert.Equal(FeatureKind.SurfaceExtrude, feature.Kind);
        var definition = Assert.IsType<SurfaceExtrudeFeatureDefinitionData>(feature.Definition);

        Assert.Equal("moExtruRefSurface_c", definition.ClassName);
        Assert.Equal(0x4Fu, definition.SerializedTypeId);
        Assert.Equal(0x4Fu, definition.TypeId);
        Assert.Equal(0.010, definition.Depth!.Value, 12);

        Assert.Null(definition.EndCondition);
        Assert.Null(definition.BothDirections);
        Assert.Null(definition.ReverseDirection);
        Assert.Null(definition.D1DraftOn);
        Assert.Null(definition.D1DraftAngle);
        Assert.Null(definition.D1DraftOutward);
        Assert.Null(definition.D1CapEnd);
        Assert.Null(definition.D2CapEnd);
        Assert.Null(definition.DeleteOriginalFace);
        Assert.Null(definition.KnitResult);

        Assert.True(definition.Resolution.ClassName);
        Assert.True(definition.Resolution.SerializedTypeId);
        Assert.True(definition.Resolution.Depth);
        Assert.False(definition.Resolution.EndCondition);
        Assert.False(definition.Resolution.BothDirections);
        Assert.False(definition.Resolution.D1CapEnd);
        Assert.False(definition.Resolution.KnitResult);
    }

    [Fact]
    public void SurfaceExtrudeParser_RejectsUnknownLayout()
    {
        byte[] stream = BuildSurfaceExtrudeStream(new byte[64]);
        var model = new ResolvedFeaturesData();
        ResolvedFeaturesSchemaMapper.Map(MfcDocument.Parse(stream), model);

        var folder = Assert.Single(model.Folders);
        Assert.Equal("moExtruRefSurface_c", folder.ClassName);
        Assert.Null(folder.FeatureData);

        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moExtruRefSurface_c");
        Assert.False(FeatureDataRegistry.TryParse(element, out var data));
        Assert.Null(data);
    }

    [Fact]
    public void SurfaceExtrudeParser_DoesNotModifyInputStream()
    {
        using var file = SwFile.Open(Resolve("surf_extrude_10mm"), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.EndsWith("-ResolvedFeatures")).Value;
        byte[] before = (byte[])stream.Clone();
        var element = MfcDocument.Parse(stream).AllElements.First(e => e.ClassName == "moExtruRefSurface_c");

        Assert.NotNull(SurfaceExtrudeFeatureParser.Parse(element));
        Assert.Equal(before, stream);
    }

    private static string Resolve(string name) =>
        TestFileProvider.Resolve("Features2026/SurfaceOptions", name + ".SLDPRT");

    private static byte[] BuildSurfaceExtrudeStream(byte[] payload)
    {
        byte[] ascii = Encoding.ASCII.GetBytes("moExtruRefSurface_c");
        var bytes = new List<byte> { 0x70, 0x00, 0x00, 0x00, 0x14, 0x00 };
        bytes.AddRange(new byte[] { 0xFF, 0xFF, 0x01, 0x00, (byte)ascii.Length, (byte)(ascii.Length >> 8) });
        bytes.AddRange(ascii);
        bytes.AddRange(new byte[] { 0xFF, 0xFE, 0xFF, 0x0E });
        bytes.AddRange(Encoding.Unicode.GetBytes("SurfaceExtrude"));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }
}
