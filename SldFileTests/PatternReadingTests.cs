using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled reading coverage for Task 1.5 linear/circular patterns. Controlled authoring is
/// covered separately in <see cref="PatternAuthoringTests"/>; direction/source-feature persistent
/// references and flag variants remain measured-only.
/// </summary>
public class PatternReadingTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "PatternOptions", name + ".SLDPRT");

    [Theory]
    [InlineData("lpattern_2dir_2x3_10_15")]
    [InlineData("lpattern_2dir_2x3_10_15_resave")]
    public void NativeLinearPattern_ParsesCountsSpacingsAndDirectionReferences(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Pattern));
        Assert.Equal("moLPattern_c", feature.TypeName);
        Assert.Equal(0x5D, feature.Id);

        var definition = Assert.IsType<LinearPatternFeatureModel>(feature.Model);
        Assert.Equal(2, definition.D1TotalInstances);
        Assert.Equal(0.010, definition.D1Spacing!.Value, 12);
        Assert.Equal(3, definition.D2TotalInstances);
        Assert.Equal(0.015, definition.D2Spacing!.Value, 12);
        Assert.True(definition.IsDirection2Specified());
        Assert.Equal(1, definition.D1AxisType);
        Assert.Equal(1, definition.D2AxisType);
        Assert.NotNull(definition.D1Axis);
        Assert.Equal(SelectionEntityType.Edge, definition.D1Axis!.EntityType);
        Assert.NotNull(definition.D2Axis);
        Assert.Equal(SelectionEntityType.Edge, definition.D2Axis!.EntityType);

        Assert.True(definition.IsResolved(nameof(LinearPatternFeatureModel.D1Spacing)));
        Assert.True(definition.IsResolved(nameof(LinearPatternFeatureModel.D2Spacing)));
        // Not yet decoded: geometry flags and source-feature selections.
        Assert.Null(definition.GeometryPattern);
        Assert.Null(definition.VarySketch);
        Assert.Null(definition.PatternFeatureArray);
        Assert.Null(definition.GetPatternFeatureCount());
        Assert.Null(definition.D1Axis!.EntityId);
    }

    [Theory]
    [InlineData("cirpattern_4x90")]
    [InlineData("cirpattern_4x90_resave")]
    public void NativeCircularPattern_ParsesCountSpacingAndEqualSpacing(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Pattern));
        Assert.Equal("moCirPattern_c", feature.TypeName);
        Assert.Equal(0x63, feature.Id);

        var definition = Assert.IsType<CircularPatternFeatureModel>(feature.Model);
        Assert.Equal(4, definition.TotalInstances);
        Assert.Equal(Math.PI / 2.0, definition.Spacing!.Value, 12);
        Assert.True(definition.EqualSpacing);
        Assert.Equal(1, definition.AxisType);
        Assert.NotNull(definition.Axis);
        Assert.Equal(SelectionEntityType.Edge, definition.Axis!.EntityType);

        Assert.True(definition.IsResolved(nameof(CircularPatternFeatureModel.Spacing)));
        Assert.True(definition.IsResolved(nameof(CircularPatternFeatureModel.EqualSpacing)));
        // Not yet decoded: direction/geometry flags and source-feature selections.
        Assert.Null(definition.ReverseDirection);
        Assert.Null(definition.GeometryPattern);
        Assert.Null(definition.Symmetric);
        Assert.Null(definition.Direction2);
        Assert.Null(definition.PatternFeatureArray);
        Assert.Null(definition.GetPatternFeatureCount());
        Assert.Null(definition.Axis!.EntityId);
    }

    [Fact]
    public void LinearPatternParserRejectsUnknownLayoutAndDoesNotModifyStream()
    {
        byte[] native;
        using (var file = SwFile.Open(SamplePath("lpattern_2dir_2x3_10_15"), readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();

        var doc = MfcDocument.Parse(native);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moLPattern_c"));
        var snapshot = (byte[])doc.SourceData.Clone();

        Assert.Null(PatternFeatureParser.ParseLinearPattern(doc, "moLPattern_x", 0x5D, feature.TagOffset, native.Length));
        Assert.Null(PatternFeatureParser.ParseLinearPattern(doc, "moLPattern_c", 0x54, feature.TagOffset, native.Length));
        Assert.Null(PatternFeatureParser.ParseLinearPattern(doc, "moLPattern_c", 0x5D, native.Length, native.Length));

        // Tamper the D1 instance count below the valid minimum; the strict parser must reject it.
        byte[] tampered = (byte[])native.Clone();
        var integerParam = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moIntegerParameter_c"));
        BitConverter.GetBytes(1u).CopyTo(tampered,
            integerParam.DataStart + PatternSegments.LinearD1CountParameterOffset + 32);
        var tamperedDoc = MfcDocument.Parse(tampered);
        var tamperedFeature = Assert.Single(tamperedDoc.AllElements.Where(e => e.ClassName == "moLPattern_c"));
        Assert.Null(PatternFeatureParser.ParseLinearPattern(
            tamperedDoc, "moLPattern_c", 0x5D, tamperedFeature.TagOffset, tampered.Length));

        Assert.Equal(snapshot, doc.SourceData);
    }

    [Fact]
    public void CircularPatternParserRejectsUnknownLayoutAndDoesNotModifyStream()
    {
        byte[] native;
        using (var file = SwFile.Open(SamplePath("cirpattern_4x90"), readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();

        var doc = MfcDocument.Parse(native);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCirPattern_c"));
        var snapshot = (byte[])doc.SourceData.Clone();

        Assert.Null(PatternFeatureParser.ParseCircularPattern(doc, "moCirPattern_x", 0x63, feature.TagOffset, native.Length));
        Assert.Null(PatternFeatureParser.ParseCircularPattern(doc, "moCirPattern_c", 0x53, feature.TagOffset, native.Length));
        Assert.Null(PatternFeatureParser.ParseCircularPattern(doc, "moCirPattern_c", 0x63, native.Length, native.Length));

        // Break the equal-spacing relationship (per-instance angle no longer spacing/(count-1)).
        byte[] tampered = (byte[])native.Clone();
        var angleParam = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moAngleParameter_c"));
        BitConverter.GetBytes(Math.PI / 4.0).CopyTo(tampered,
            angleParam.DataStart + PatternSegments.CircularAngleParameterOffset + 32);
        var tamperedDoc = MfcDocument.Parse(tampered);
        var tamperedFeature = Assert.Single(tamperedDoc.AllElements.Where(e => e.ClassName == "moCirPattern_c"));
        Assert.Null(PatternFeatureParser.ParseCircularPattern(
            tamperedDoc, "moCirPattern_c", 0x63, tamperedFeature.TagOffset, tampered.Length));

        Assert.Equal(snapshot, doc.SourceData);
    }

    [Theory]
    [InlineData(0x6E)]
    [InlineData(0x71)]
    public void PatternParsersRejectUnmeasuredStreamVersions(ushort version)
    {
        byte[] native;
        using (var file = SwFile.Open(SamplePath("lpattern_2dir_2x3_10_15"), readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();
        BitConverter.GetBytes(version).CopyTo(native, 0);
        var doc = MfcDocument.Parse(native);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moLPattern_c"));
        Assert.Null(PatternFeatureParser.ParseLinearPattern(
            doc, "moLPattern_c", 0x5D, feature.TagOffset, native.Length));
    }

    [Fact]
    public void MirrorPatternParserRejectsNonMirrorStreams()
    {
        byte[] native;
        using (var file = SwFile.Open(SamplePath("cirpattern_4x90"), readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(native);
        Assert.Null(PatternFeatureParser.ParseMirrorPattern(doc, "moMirrorPattern_c", 0x57, 0, native.Length));
    }
}
