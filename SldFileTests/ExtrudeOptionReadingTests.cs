using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

public class ExtrudeOptionReadingTests
{
    [Theory]
    [InlineData("extrude_10mm", false)]
    [InlineData("extrude_10mm_resave", false)]
    [InlineData("extrude_reverse", true)]
    [InlineData("extrude_draft3deg", false)]
    [InlineData("extrude_draft6deg", false)]
    [InlineData("extrude_offset2mm", false)]
    [InlineData("extrude_offset4mm", false)]
    [InlineData("extrude_two_direction", false)]
    public void NativeControlledSamples_ReadReverseDirection(string sample, bool expected)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
        Assert.Equal(expected, definition.ReverseDirection);
        Assert.Equal(.01, definition.Depth, 12);
    }

    [Theory]
    [InlineData("extrude_10mm", 0, 0)]
    [InlineData("extrude_10mm_resave", 0, 0)]
    [InlineData("extrude_reverse", 0, 0)]
    [InlineData("extrude_draft3deg", 3, 0)]
    [InlineData("extrude_draft6deg", 6, 0)]
    [InlineData("extrude_offset2mm", 0, .002)]
    [InlineData("extrude_offset4mm", 0, .004)]
    [InlineData("extrude_two_direction", 0, 0)]
    public void NativeControlledSamples_ReadDraftAndOffsetWithoutConfusingDepth(string sample, double draftDegrees, double offset)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
        Assert.Equal(draftDegrees > 0, definition.DraftWhileExtruding);
        Assert.Equal(draftDegrees * Math.PI / 180, definition.DraftAngle, 12);
        Assert.Equal(offset > 0 ? 3u : 0u, definition.FromType);
        Assert.Equal(offset, definition.FromOffsetDistance, 12);
        Assert.Equal(.01, definition.Depth, 12);
    }
    [Theory]
    [InlineData("extrude_two_direction", .005)]
    [InlineData("extrude_two_direction_3mm", .003)]
    [InlineData("extrude_two_direction_3mm_resave", .003)]
    [InlineData("extrude_two_direction_7mm", .007)]
    public void NativeTwoDirectionDepthMatchesIndependentApiSnapshot(string sample, double secondDepth)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", sample + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Extrusion));
        var definition = Assert.IsType<ExtrudeFeatureDefinitionData>(feature.Definition);
        Assert.True(definition.BothDirections);
        Assert.Equal(.01, definition.GetDepth(true), 12);
        Assert.Equal(secondDepth, definition.GetDepth(false), 12);
        Assert.Equal(0u, definition.GetEndCondition(false));
    }

    [Fact]
    public void UnknownSecondDirectionReferenceDoesNotInventDepth()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", "extrude_two_direction_3mm.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = SldFile.Mfc.MfcDocument.Parse(stream);
        var favorite = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moFavoriteHandle_c"));
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        stream[favorite.DataStart + 436] = 12;
        var definition = new ExtrudeFeatureDefinitionData();
        ExtrudeAdditionalOptionParser.Apply(SldFile.Mfc.MfcDocument.Parse(stream), feature.ClassName, feature.TagOffset, stream.Length, definition);
        Assert.False(definition.BothDirections);
        Assert.Equal(0, definition.DepthDirection2);
    }
    [Fact]
    public void OffsetRejectsTruncatedOrAmbiguousParameters()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", "extrude_offset2mm.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var stream = file.RawStreams["Contents/Config-0-ResolvedFeatures"];
        var document = SldFile.Mfc.MfcDocument.Parse(stream);
        var from = Assert.Single(document.AllElements.Where(e => e.ClassName == "moFromEndSpec_c"));
        var raw = stream.Skip(from.DataStart).ToArray();
        Assert.Equal(.002, ExtrudeAdditionalOptionParser.ReadStartOffset(raw));
        // Cutting in the named-parameter header/value must not synthesize zero or throw.
        Assert.Null(ExtrudeAdditionalOptionParser.ReadStartOffset(raw.Take(713).ToArray()));
        Assert.Null(ExtrudeAdditionalOptionParser.ReadStartOffset(raw.Take(720).ToArray()));
        var duplicated = raw.Concat(raw).ToArray();
        Assert.Null(ExtrudeAdditionalOptionParser.ReadStartOffset(duplicated));
        var unknown = (byte[])raw.Clone();
        unknown[0] = 6;
        Assert.Null(ExtrudeAdditionalOptionParser.ReadStartOffset(unknown));
    }

    [Fact]
    public void DraftRequiresOwnedEnableFlagAndCompleteParameter()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "ExtrudeOptions", "extrude_draft3deg.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        var stream = (byte[])file.RawStreams["Contents/Config-0-ResolvedFeatures"].Clone();
        var doc = SldFile.Mfc.MfcDocument.Parse(stream);
        var display = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moDisplayAngularDim_c"));
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moExtrusion_c"));
        stream[display.TagOffset - 24] = 0;
        var definition = new ExtrudeFeatureDefinitionData();
        ExtrudeAdditionalOptionParser.Apply(SldFile.Mfc.MfcDocument.Parse(stream), feature.ClassName, feature.TagOffset, stream.Length, definition);
        Assert.False(definition.DraftWhileExtruding);
        Assert.Equal(0, definition.DraftAngle);
        Assert.Null(ExtrudeAdditionalOptionParser.ReadNamedParameter(new byte[] { 4, 128, 255, 254, 255, 255 }, 0));
    }
    [Fact]
    public void UnrecognizedLayoutsDoNotInventDirection()
    {
        var raw = new byte[24];
        raw[2] = 1;
        raw[10] = 1;
        Assert.True(ExtrudeOptionParser.ReadReverseDirection(0x70, "moExtrusion_c", raw));
        Assert.Null(ExtrudeOptionParser.ReadReverseDirection(0x71, "moExtrusion_c", raw));
        Assert.Null(ExtrudeOptionParser.ReadReverseDirection(0x70, "moICE_c", raw));
        Assert.Null(ExtrudeOptionParser.ReadReverseDirection(0x70, "moExtrusion_c", new byte[23]));
        raw[10] = 2;
        Assert.Null(ExtrudeOptionParser.ReadReverseDirection(0x70, "moExtrusion_c", raw));
    }
}
