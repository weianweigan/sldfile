using System.Text;
using System.Xml.Linq;
using SldFile;
using SldFile.Sketches;

namespace SldFileTests;

public class SketchBinaryReaderTests
{
    private const string Stream = "Contents/Config-0-ResolvedFeatures";
    private static SwFile Open(string name = "line10") => SwFile.Open(TestFileProvider.Resolve("Sketches2026", name + ".SLDPRT"));

    [Fact]
    public void ConfigurationStreamsAreIndependentAndMissingConfigurationsDoNotFallBack()
    {
        using var file = Open(); using var second = Open("line3d");
        file.RawStreams["Contents/Config-7-ResolvedFeatures"] = (byte[])second.RawStreams[Stream].Clone();
        var flat = Assert.Single(file.GetSketches()); var spatial = Assert.Single(file.GetSketches(7));
        Assert.False(flat.Is3D); Assert.True(spatial.Is3D); Assert.Equal(7, spatial.ConfigurationIndex);
        Assert.Equal(0, Assert.Single(flat.Segments).EndPoint!.Z);
        Assert.Equal(.023, Assert.Single(spatial.Segments).EndPoint!.Z, 12);
        Assert.Empty(file.GetSketches(6));
    }

    [Fact]
    public void InvalidPointDoesNotShiftSubsequentPointReferences()
    {
        using var file = Open(); var valid = Assert.Single(file.GetSketches());
        var bytes = (byte[])file.RawStreams[Stream].Clone();
        BitConverter.GetBytes(double.NaN).CopyTo(bytes, valid.Points[0].SourceOffset + 4 + 66);
        var sketch = Assert.Single(SketchBinaryReader.Read(bytes, file.RawStreams["swXmlContents/KeyWords"], Stream, 0));
        var segment = Assert.Single(sketch.Segments);
        Assert.Null(segment.StartPoint); Assert.Equal(2u, segment.EndPoint!.Id);
        Assert.Equal(.01, segment.EndPoint.X, 12); Assert.Equal(1, segment.EndPoint.TableIndex);
        Assert.Contains(sketch.Diagnostics, s => s.StartsWith("NonFinitePoint"));
        Assert.Contains(sketch.Diagnostics, s => s.StartsWith("UnresolvedPointReference"));
    }

    [Fact]
    public void MissingMiddlePointMarkerInvalidatesTableInsteadOfReindexingRemainingPoints()
    {
        using var file = Open("rectangle"); var original = Assert.Single(file.GetSketches());
        var bytes = (byte[])file.RawStreams[Stream].Clone();
        bytes[original.Points[1].SourceOffset + 4 + 5] ^= 1;
        var sketch = Assert.Single(SketchBinaryReader.Read(bytes, file.RawStreams["swXmlContents/KeyWords"], Stream, 0));
        Assert.Empty(sketch.Points);
        Assert.NotEmpty(sketch.Segments);
        Assert.All(sketch.Segments, s => { Assert.Null(s.StartPoint); Assert.Null(s.EndPoint); Assert.Null(s.CenterPoint); });
        Assert.Contains(sketch.Diagnostics, s => s.StartsWith("PointTableCountMismatch"));
        Assert.Equal(bytes.Skip(sketch.SourceOffset), sketch.GetRawData());
    }

    [Fact]
    public void OutOfRangeReferenceStaysUnresolvedInsteadOfClampingToAValidPoint()
    {
        using var file = Open(); var valid = Assert.Single(file.GetSketches());
        var bytes = (byte[])file.RawStreams[Stream].Clone();
        BitConverter.GetBytes((ushort)60000).CopyTo(bytes, valid.Segments[0].SourceOffset + 4 + 66);
        var sketch = Assert.Single(SketchBinaryReader.Read(bytes, file.RawStreams["swXmlContents/KeyWords"], Stream, 0));
        var segment = Assert.Single(sketch.Segments);
        Assert.Null(segment.EndPoint);
        Assert.Contains(sketch.Diagnostics, s => s.StartsWith("UnresolvedPointReference"));
    }

    [Fact]
    public void PointTableKeepsHiddenCirclePointAndResolvesCenterByIndexNotId()
    {
        using var file = Open("circle10"); var sketch = Assert.Single(file.GetSketches());
        Assert.Equal(new uint[] { 2, 1 }, sketch.Points.Select(p => p.Id));
        var circle = Assert.Single(sketch.Segments);
        Assert.Same(sketch.Points[0], circle.CenterPoint); Assert.Same(sketch.Points[1], circle.StartPoint);
        Assert.True(circle.IsCircle); Assert.Equal(.01, circle.Radius!.Value, 12);
    }

    [Fact]
    public void PointSignaturesInOpaqueTailDoNotExtendTheCountedPointTable()
    {
        using var file = Open(); var valid = Assert.Single(file.GetSketches()); var source = file.RawStreams[Stream];
        var record = source.Skip(valid.Points[0].SourceOffset).Take(90).ToArray();
        var altered = source.Concat(record).ToArray();
        var actual = Assert.Single(SketchBinaryReader.Read(altered, file.RawStreams["swXmlContents/KeyWords"], Stream, 0));
        Assert.Equal(2, actual.Points.Count); Assert.Single(actual.Segments);
        Assert.Equal(source.Length + record.Length - actual.SourceOffset, actual.GetRawData().Length);
    }

    [Fact]
    public void UnknownSegmentBodyIsNotTypedByNearbyHandleClassNames()
    {
        using var file = Open("circle10"); var valid = Assert.Single(file.GetSketches()); var bytes = (byte[])file.RawStreams[Stream].Clone();
        BitConverter.GetBytes((uint)0).CopyTo(bytes, valid.Segments[0].SourceOffset + 4 + 86);
        var actual = Assert.Single(SketchBinaryReader.Read(bytes, file.RawStreams["swXmlContents/KeyWords"], Stream, 0));
        Assert.Equal(SketchSegmentKind.Unknown, Assert.Single(actual.Segments).Kind);
        Assert.Contains(actual.Diagnostics, d => d.StartsWith("UnknownSegmentLayout"));
    }

    [Fact]
    public void DerivedRadiusDoesNotPublishInfinity()
    {
        using var file = Open("circle10"); var valid = Assert.Single(file.GetSketches()); var bytes = (byte[])file.RawStreams[Stream].Clone();
        BitConverter.GetBytes(1e308).CopyTo(bytes, valid.Points[0].SourceOffset + 4 + 66);
        BitConverter.GetBytes(-1e308).CopyTo(bytes, valid.Points[1].SourceOffset + 4 + 66);
        var actual = Assert.Single(SketchBinaryReader.Read(bytes, file.RawStreams["swXmlContents/KeyWords"], Stream, 0));
        Assert.Null(Assert.Single(actual.Segments).Radius);
        Assert.Contains(actual.Diagnostics, d => d.StartsWith("NonFiniteDerivedRadius"));
    }

    [Fact]
    public void ReusedHandleWordsRemainArchiveLocalAndUnresolved()
    {
        using var lines = Open("parallel"); using var arcs = Open("concentric");
        var lineRelation = Assert.Single(Assert.Single(lines.GetSketches()).Relations);
        var arcRelation = Assert.Single(Assert.Single(arcs.GetSketches()).Relations);
        Assert.Equal(lineRelation.References[0].ClassReference, arcRelation.References[0].ClassReference);
        Assert.Equal(SketchRelationKind.Parallel, lineRelation.Kind);
        Assert.Equal(SketchRelationKind.Concentric, arcRelation.Kind);
        Assert.Equal(2, lineRelation.References.Count); Assert.Equal(2, arcRelation.References.Count);
    }

    [Fact]
    public void HiddenOffsetParameterIsSeparateFromDisplayDimensions()
    {
        using var file = Open("offset"); var sketch = Assert.Single(file.GetSketches());
        Assert.Empty(sketch.Dimensions); var parameter = Assert.Single(sketch.Parameters);
        Assert.Equal("D1", parameter.Name); Assert.Equal(.003, parameter.SystemValue!.Value, 12);
        Assert.Equal("BinaryParameter", parameter.ValueSource);
    }

    [Fact]
    public void KeywordDimensionsPreserveDuplicatesAndUseKnownDocumentUnitsOnly()
    {
        using var file = Open("chamfer"); var bytes = file.RawStreams[Stream]; var xml = file.RawStreams["swXmlContents/KeyWords"];
        var sketch = Assert.Single(file.GetSketches());
        Assert.Equal(2, sketch.Dimensions.Count);
        Assert.All(sketch.Dimensions, d => { Assert.Equal("D3", d.Name); Assert.Equal(.003, d.SystemValue!.Value, 12); Assert.Equal("KeywordSummary", d.ValueSource); });
        var inches = Assert.Single(SketchBinaryReader.Read(bytes, xml, Stream, 0, LengthUnit.Inch));
        Assert.All(inches.Dimensions, d => Assert.Equal(.0762, d.SystemValue!.Value, 12));
        var unknown = Assert.Single(SketchBinaryReader.Read(bytes, xml, Stream, 0));
        Assert.All(unknown.Dimensions, d => { Assert.Null(d.SystemValue); Assert.Equal("3", d.DisplayValue); });
    }

    [Fact]
    public void FeatureIdentityAndGeometrySurviveLongUnicodeRenameAndRelocation()
    {
        using var file = Open(); var old = Assert.Single(file.GetSketches());
        var source = file.RawStreams[Stream]; int start = old.SourceOffset + 2;
        int end = start; Assert.True(SketchBinaryReader.TryWide(source, ref end, source.Length, out _));
        string name = new string('长', 300) + "_草图🙂";
        var nameBytes = Wide(name); var rewritten = source.Take(start).Concat(nameBytes).Concat(source.Skip(end)).ToArray();
        var xmlBytes = file.RawStreams["swXmlContents/KeyWords"]; int xmlStart = Array.IndexOf(xmlBytes, (byte)'<');
        var xml = XDocument.Parse(Encoding.UTF8.GetString(xmlBytes, xmlStart, xmlBytes.Length - xmlStart));
        xml.Descendants("Sketch").Single(s => (string?)s.Attribute("id") == "73").SetAttributeValue("Name", name);
        var actual = Assert.Single(SketchBinaryReader.Read(rewritten, Encoding.UTF8.GetBytes(xml.ToString()), Stream, 0));
        Assert.Equal(name, actual.Name); Assert.Equal(old.Id, actual.Id);
        var line = Assert.Single(actual.Segments); Assert.Equal(.01, line.EndPoint!.X, 12);
        Assert.True(line.SourceOffset > old.Segments[0].SourceOffset);
    }

    [Fact]
    public void OpaquePayloadIsPreservedAndNotClaimedAsComplete()
    {
        using var file = Open("spline"); var sketch = Assert.Single(file.GetSketches());
        Assert.True(sketch.IsPartial);
        Assert.Equal(file.RawStreams[Stream].Skip(sketch.SourceOffset), sketch.GetRawData());
        Assert.Contains(sketch.Diagnostics, d => d.StartsWith("SplineParametersNotDecoded"));
    }

    [Fact]
    public void MissingOrMalformedXmlStillReadsExplicitBinaryProfileWithoutThrowing()
    {
        using var file = Open();
        foreach (var keywords in new byte[]?[] { null, Array.Empty<byte>(), Encoding.UTF8.GetBytes("<broken"), Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///does-not-exist'>]><x>&x;</x>") })
        {
            var sketch = Assert.Single(SketchBinaryReader.Read(file.RawStreams[Stream], keywords, Stream, 0));
            Assert.Equal("草图1", sketch.Name); Assert.Equal(.01, Assert.Single(sketch.Segments).EndPoint!.X, 12);
            Assert.True(sketch.IsPartial);
        }
    }

    [Fact]
    public void TruncationAtEveryByteOfTheLastRecordDoesNotThrow()
    {
        using var file = Open(); var original = file.RawStreams[Stream]; var sketch = Assert.Single(file.GetSketches());
        int offset = sketch.Segments[0].SourceOffset;
        for (int length = offset; length < offset + 100; length++)
        {
            var truncated = original.Take(length).ToArray();
            var result = SketchBinaryReader.Read(truncated, file.RawStreams["swXmlContents/KeyWords"], Stream, 0);
            Assert.Single(result); Assert.True(result[0].IsPartial);
        }
    }

    [Fact]
    public void EmptyAndUnrecognizedStreamsHaveNoFabricatedSketches()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[500], Enumerable.Repeat((byte)255, 500).ToArray() })
            Assert.Empty(SketchBinaryReader.Read(bytes, null, Stream, 0));
        Assert.Throws<ArgumentNullException>(() => SketchBinaryReader.Read(null!, null, Stream, 0));
    }

    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    public void LegacyCompoundDocumentsExposeBinarySketchIdentityAsPartial(string fixture)
    {
        string path = fixture == nameof(TestFileProvider.CubePart) ? TestFileProvider.CubePart : TestFileProvider.CylinderPart;
        using var file = SwFile.Open(path);
        var sketch = Assert.Single(file.GetSketches());
        Assert.Equal(fixture == nameof(TestFileProvider.CubePart) ? "草图1" : "Sketch1", sketch.Name);
        Assert.True(sketch.IsPartial);
    }

    [Fact]
    public void MixedOrOtherConfigurationKeywordSummariesAreNotAssumedToBeLengths()
    {
        using var file = Open("chamfer"); var bytes = file.RawStreams[Stream]; var xmlBytes = file.RawStreams["swXmlContents/KeyWords"];
        var other = Assert.Single(SketchBinaryReader.Read(bytes, xmlBytes, "Contents/Config-9-ResolvedFeatures", 9, LengthUnit.Millimeter));
        Assert.Empty(other.Dimensions);
        Assert.Contains(other.Diagnostics, d => d.StartsWith("ConfigurationDimensionSummaryUnavailable"));
        int start = Array.IndexOf(xmlBytes, (byte)'<'); var xml = XDocument.Parse(Encoding.UTF8.GetString(xmlBytes, start, xmlBytes.Length - start));
        xml.Descendants("Sketch").Single(s => (string?)s.Attribute("id") == "73").Add(new XElement("Dimension", new XAttribute("Name", "A1"), "90"));
        var mixed = Assert.Single(SketchBinaryReader.Read(bytes, Encoding.UTF8.GetBytes(xml.ToString()), Stream, 0, LengthUnit.Millimeter));
        Assert.Equal(3, mixed.Dimensions.Count); Assert.All(mixed.Dimensions, d => Assert.Null(d.SystemValue));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(254)] [InlineData(255)] [InlineData(300)] [InlineData(65535)]
    public void ArchiveWideStringSupportsLengthEncodingsWithoutUnboundedAllocation(int count)
    {
        string expected = new string('中', count); var bytes = Wide(expected); int cursor = 0;
        Assert.True(SketchBinaryReader.TryWide(bytes, ref cursor, bytes.Length, out var actual));
        Assert.Equal(expected, actual); Assert.Equal(bytes.Length, cursor);
        if (bytes.Length > 4) { cursor = 0; Assert.False(SketchBinaryReader.TryWide(bytes, ref cursor, bytes.Length - 1, out _)); Assert.Equal(0, cursor); }
    }

    [Fact]
    public void MalformedWideLengthsDoNotAdvanceOrOverflow()
    {
        var bytes = new byte[] { 255, 254, 255, 255, 255, 255, 255, 255, 255, 255 };
        int cursor = 0;
        Assert.False(SketchBinaryReader.TryWide(bytes, ref cursor, bytes.Length, out _)); Assert.Equal(0, cursor);
        Assert.False(SketchBinaryReader.TryWide(bytes, ref cursor, bytes.Length + 1, out _));
        cursor = -1; Assert.False(SketchBinaryReader.TryWide(bytes, ref cursor, bytes.Length, out _)); Assert.Equal(-1, cursor);
    }

    private static byte[] Wide(string value)
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer, Encoding.Unicode, true);
        writer.Write(new byte[] { 255, 254, 255 });
        if (value.Length < 255) writer.Write((byte)value.Length);
        else { writer.Write((byte)255); if (value.Length < 65535) writer.Write((ushort)value.Length); else { writer.Write(ushort.MaxValue); writer.Write((uint)value.Length); } }
        writer.Write(Encoding.Unicode.GetBytes(value)); writer.Flush(); return buffer.ToArray();
    }
}
