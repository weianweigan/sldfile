using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring coverage for the first controlled fillet slice: constant radius on the measured
/// single edge of the fillet_2mm base body. Covers radius round-trip, measured slots, edge
/// selection preservation, resave, and fail-fast rejection paths.
/// </summary>
public class FilletAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string FilletClass = "Fillet_c";
    private const uint FilletHeaderTypeId = 0x51u;

    private static TempTestFile CreateStrippedFilletBase()
    {
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "fillet_2mm.SLDPRT");
        var temp = TestFileProvider.CreateTempCopy(sample);

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var raw = sw.ResolvedFeatures.RawData!;

        var folders = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> current)
        {
            foreach (var folder in current)
            {
                folders.Add(folder);
                Walk(folder.Children);
            }
        }
        Walk(sw.ResolvedFeatures.Folders);
        var fillet = folders.First(f => f.ClassName == FilletClass);
        int cutOffset = fillet.TagOffset;

        byte[] stripped = new byte[cutOffset];
        Array.Copy(raw, 0, stripped, 0, cutOffset);
        ushort folderCount = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
        Assert.Equal((ushort)0x0070, BitConverter.ToUInt16(stripped, 0));

        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static FilletFeatureDefinitionData AuthorAndParse(
        double radius, out byte[] stream, out string featureName)
    {
        using var temp = CreateStrippedFilletBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = radius });
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var filletElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == FilletClass));
        var definition = FilletChamferFeatureParser.ParseFillet(
            doc, FilletClass, FilletHeaderTypeId, filletElement.TagOffset, stream.Length);
        Assert.NotNull(definition);
        return definition!;
    }

    [Theory]
    [InlineData(0.002)]
    [InlineData(0.003)]
    [InlineData(0.005)]
    public void FilletAuthoring_RoundtripsRadius(double radius)
    {
        var read = AuthorAndParse(radius, out _, out string featureName);

        Assert.Equal(radius, read.DefaultRadius!.Value, 12);
        Assert.True(read.IsResolved(nameof(FilletFeatureDefinitionData.DefaultRadius)));
        Assert.Null(read.Type);
        var edge = Assert.Single(read.Edges!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.Equal(4u, edge.EntityId);
        Assert.Equal(0x200u, edge.ClassWord);
        Assert.Equal(PersistentEdgeReferenceParser.ExpectedArchiveGuid, edge.ArchiveGuid);
        Assert.Equal(4, edge.RecordCount);
        Assert.Null(read.IsMultipleRadius);
        Assert.Equal("圆角1", featureName);
    }

    [Theory]
    [InlineData(0.002)]
    [InlineData(0.004)]
    public void FilletAuthoring_WritesMeasuredRadiusSlots(double radius)
    {
        AuthorAndParse(radius, out byte[] stream, out _);
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == FilletClass));
        var radiusDim = Assert.Single(doc.AllElements.Where(e =>
            e.ClassName == "ThreeDRadiusDim_c" && e.TagOffset >= feature.TagOffset));
        byte[] raw = MfcPrimitives.CopyRange(
            stream, radiusDim.DataStart, radiusDim.DataStart + FilletSegments.ThreeDRadiusExpectedLength);

        Assert.Equal(radius,
            ExtrudeAdditionalOptionParser.ReadNamedParameter(raw, FilletSegments.ThreeDRadiusParameterOffset)!.Value, 12);
        foreach (int slot in FilletSegments.ThreeDRadiusPositiveSlots)
            Assert.Equal(radius, BitConverter.ToDouble(raw, slot), 12);
        Assert.Equal(-radius, BitConverter.ToDouble(raw, FilletSegments.ThreeDRadiusNegativeSlot), 12);

        var compFace = Assert.Single(doc.AllElements.Where(e =>
            e.ClassName == "moCompFace_c" && e.TagOffset >= feature.TagOffset));
        byte[] compFaceRaw = MfcPrimitives.CopyRange(
            stream, compFace.DataStart, compFace.DataStart + FilletSegments.CompFaceExpectedLength);
        Assert.Equal(radius, BitConverter.ToDouble(compFaceRaw, FilletSegments.CompFaceRadiusSlot), 12);
    }

    [Fact]
    public void FilletAuthoring_PreservesMeasuredEdgeSelection()
    {
        AuthorAndParse(0.003, out byte[] authored, out _);

        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "fillet_2mm.SLDPRT");
        byte[] native;
        using (var file = SwFile.Open(sample, readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();

        foreach (string className in new[] { "moEdgeRef_c", "moCompEdge_c" })
        {
            var nativeDoc = MfcDocument.Parse(native);
            var authoredDoc = MfcDocument.Parse(authored);
            byte[] expected = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == className)).RawData;
            byte[] actual = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == className)).RawData;
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void FilletAuthoring_CustomFeatureNameIsWrittenToHeader()
    {
        using var temp = CreateStrippedFilletBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = 0.0025 }, "Profile Fillet");
            Assert.Equal("Profile Fillet", feature.Name);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var fillet = Assert.Single(doc.AllElements.Where(e => e.ClassName == FilletClass));
        Assert.Equal("Profile Fillet", fillet.AsWChar());
    }

    [Fact]
    public void FilletAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedFilletBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = 0.0045 });
            part.Save();
        }

        byte[] before;
        using (var first = SwFile.Open(temp.FilePath, readOnly: false))
        {
            before = (byte[])first.RawStreams[ResolvedStream].Clone();
            first.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
        var doc = MfcDocument.Parse((byte[])reopened.RawStreams[ResolvedStream].Clone());
        var fillet = Assert.Single(doc.AllElements.Where(e => e.ClassName == FilletClass));
        var definition = FilletChamferFeatureParser.ParseFillet(
            doc, FilletClass, FilletHeaderTypeId, fillet.TagOffset, doc.SourceData.Length);
        Assert.NotNull(definition);
        Assert.Equal(0.0045, definition!.DefaultRadius!.Value, 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.002)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void FilletAuthoringRejectsInvalidRadius(double radius)
    {
        using var temp = CreateStrippedFilletBase();
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = radius }));
    }

    [Fact]
    public void FilletAuthoringRejectsNullRadius()
    {
        using var temp = CreateStrippedFilletBase();
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => part.AddFillet(new FilletFeatureDefinitionData()));
    }

    public static IEnumerable<object[]> UnsupportedFields()
    {
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, Type = 1 }, "Type" };
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, IsMultipleRadius = true }, "IsMultipleRadius" };
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, RoundCorners = true }, "RoundCorners" };
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, CurvatureContinuous = true }, "CurvatureContinuous" };
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, DefaultDistance = .001 }, "DefaultDistance" };
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, Edges = new[] { new FeatureReferenceData { EntityId = 1 } } }, "Edges" };
        yield return new object[] { new FilletFeatureDefinitionData { DefaultRadius = .002, HelpPoint = new FeatureReferenceData() }, "HelpPoint" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedFields))]
    public void FilletAuthoringRejectsUnverifiedFieldsWithoutModifyingStream(FilletFeatureDefinitionData definition, string property)
    {
        using var temp = CreateStrippedFilletBase();
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(() => part.AddFillet(definition));
        Assert.Contains(property, error.Message);

        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void FilletAuthoringRejectsPerItemRadii()
    {
        var definition = new FilletFeatureDefinitionData { DefaultRadius = .002 };
        definition.SetRadius(1, .003);
        Assert.Throws<NotSupportedException>(() => FilletFeatureBuilder.ValidateDefinition(definition));
    }

    [Fact]
    public void FilletAuthoringRejectsNonControlledBaseBody()
    {
        using var temp = TestFileProvider.CreateTempCopy(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT"));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(
            () => part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = .002 }));
        Assert.Contains("controlled fillet_2mm base body", error.Message);

        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void FilletAuthoringRejectsExistingFilletFeature()
    {
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "fillet_2mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(sample);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(
            () => part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = .002 }));
        Assert.Contains("existing", error.Message);
        Assert.Equal(original, part.RawStreams[ResolvedStream]);
    }

    [Theory]
    [InlineData(0x6E)]
    [InlineData(0x71)]
    public void FilletAuthoringRejectsNon0x70Streams(ushort version)
    {
        using var temp = CreateStrippedFilletBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            BitConverter.GetBytes(version).CopyTo(raw, 0);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }

        using var sw2 = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw2);
        var error = Assert.Throws<NotSupportedException>(
            () => part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = .002 }));
        Assert.Contains($"0x{version:X2}", error.Message);
    }

    [Fact]
    public void FilletAuthoringRejectsMissingSketchChainAndMarker()
    {
        // Missing chain.
        using (var temp = CreateStrippedFilletBase())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                int chainTag = FindAscii(raw, "moSketchChain_c");
                Assert.True(chainTag > 0);
                raw[chainTag] = (byte)'n';
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            AssertFilletAddRejected(temp, "moSketchChain_c");
        }

        // Missing 0x67 marker (the sketch marker, not the fillet).
        using (var temp = CreateStrippedFilletBase())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                bool corrupted = false;
                for (int i = raw.Length - 4; i >= 0; i--)
                {
                    if (raw[i] == 0x67 && raw[i + 1] == 0 && raw[i + 2] == 0 && raw[i + 3] == 0)
                    {
                        raw[i] = 0x68;
                        corrupted = true;
                        break;
                    }
                }
                Assert.True(corrupted);
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            AssertFilletAddRejected(temp, "0x67");
        }
    }

    [Fact]
    public void FilletParserRejectsUnknownLayoutAndDoesNotModifyStream()
    {
        AuthorAndParse(0.003, out byte[] stream, out _);
        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == FilletClass));
        var snapshot = (byte[])doc.SourceData.Clone();

        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, "moFillet_c", 0x4C, feature.TagOffset, stream.Length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, FilletClass, 0x4C, feature.TagOffset, stream.Length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(doc, FilletClass, FilletHeaderTypeId, stream.Length, stream.Length));

        // Tamper a radius copy: the parser must reject the inconsistent layout.
        var radiusDim = Assert.Single(doc.AllElements.Where(e => e.ClassName == "ThreeDRadiusDim_c"));
        byte[] tampered = (byte[])stream.Clone();
        BitConverter.GetBytes(0.009).CopyTo(tampered, radiusDim.DataStart + FilletSegments.ThreeDRadiusPositiveSlots[1]);
        var tamperedDoc = MfcDocument.Parse(tampered);
        var tamperedFeature = Assert.Single(tamperedDoc.AllElements.Where(e => e.ClassName == FilletClass));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            tamperedDoc, FilletClass, FilletHeaderTypeId, tamperedFeature.TagOffset, tampered.Length));

        Assert.Equal(snapshot, doc.SourceData);
    }

    [Fact]
    public void NativeFilletSample_IsClassifiedAndParsed()
    {
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "fillet_2mm.SLDPRT");
        using var file = SwFile.Open(sample, readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet));

        Assert.Equal("圆角1", feature.Name);
        Assert.Equal("Fillet_c", feature.TypeName);
        Assert.Equal(0x51, feature.Id);
        var definition = Assert.IsType<FilletFeatureDefinitionData>(feature.Definition);
        Assert.Equal(0.002, definition.DefaultRadius!.Value, 12);
        Assert.True(definition.IsResolved(nameof(FilletFeatureDefinitionData.DefaultRadius)));
        Assert.Null(definition.Type);
        var edge = Assert.Single(definition.Edges!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.Equal(4u, edge.EntityId);
        Assert.Equal(0x200u, edge.ClassWord);
        Assert.Equal(0x38D9u, edge.PersistentId);
        Assert.Equal(4, edge.RecordCount);
    }

    [Fact]
    public void AuthoredFillet_IsRecognizedByFeatureList()
    {
        using var temp = CreateStrippedFilletBase();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = 0.003 });
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Fillet));
        Assert.Equal("圆角1", feature.Name);
        var definition = Assert.IsType<FilletFeatureDefinitionData>(feature.Definition);
        Assert.Equal(0.003, definition.DefaultRadius!.Value, 12);
        Assert.True(definition.IsResolved(nameof(FilletFeatureDefinitionData.DefaultRadius)));
    }

    [Fact]
    public void UnknownFilletLayoutLeavesDefinitionUnresolvedButKeepsClassification()
    {
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "fillet_2mm.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(sample);

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            var dim = Assert.Single(doc.AllElements.Where(e => e.ClassName == "ThreeDRadiusDim_c"));
            // Break one measured radius display copy: the strict parser must not invent a value.
            BitConverter.GetBytes(0.009).CopyTo(raw, dim.DataStart + FilletSegments.ThreeDRadiusPositiveSlots[1]);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Fillet));
        Assert.Null(feature.Definition);
    }

    private static void AssertFilletAddRejected(TempTestFile temp, string expectedMessage)
    {
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(
                () => part.AddFillet(new FilletFeatureDefinitionData { DefaultRadius = .002 }));
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    private static int FindAscii(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        for (int i = 0; i <= data.Length - pattern.Length; i++)
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
}
