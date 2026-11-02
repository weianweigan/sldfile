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
/// Controlled chamfer reading/authoring coverage: distance-distance (D1/D3) and
/// angle-distance (D1 + D2 angle) on the measured single edge of the ChamferOptions base body.
/// </summary>
public class ChamferAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string ChamferClass = "Chamfer_c";
    private const uint ChamferHeaderTypeId = 0x52u;

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "ChamferOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedChamferBase(string sampleName)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sampleName));

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
        var chamfer = folders.FirstOrDefault(f => f.ClassName == ChamferClass);
        Assert.True(chamfer != null,
            "folders: " + string.Join(", ", folders.Select(f => f.ClassName + "|" + f.DisplayName)));
        int cutOffset = chamfer!.TagOffset;

        byte[] stripped = new byte[cutOffset];
        Array.Copy(raw, 0, stripped, 0, cutOffset);
        ushort folderCount = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
        Assert.Equal((ushort)0x0070, BitConverter.ToUInt16(stripped, 0));

        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static ChamferFeatureDefinitionData AuthorAndParse(
        ChamferFeatureDefinitionData definition, string sampleName,
        out byte[] stream, out string featureName)
    {
        using var temp = CreateStrippedChamferBase(sampleName);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddChamfer(definition);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var chamferElement = Assert.Single(doc.AllElements.Where(e => e.ClassName == ChamferClass));
        var parsed = FilletChamferFeatureParser.ParseChamfer(
            doc, ChamferClass, ChamferHeaderTypeId, chamferElement.TagOffset, stream.Length);
        Assert.NotNull(parsed);
        return parsed!;
    }

    private static byte[] SliceElement(MfcDocument doc, MfcElement element, int featureEnd)
    {
        var next = doc.AllElements
            .Where(e => e.TagOffset > element.TagOffset)
            .OrderBy(e => e.TagOffset)
            .FirstOrDefault();
        int end = next != null && next.TagOffset < featureEnd ? next.TagOffset : featureEnd;
        return MfcPrimitives.CopyRange(doc.SourceData, element.DataStart, end);
    }

    [Theory]
    [InlineData("chamfer_dist_dist_2_3mm", 2, 0.002, 0.003, 0.0, false)]
    [InlineData("chamfer_dist_dist_2_3mm_resave", 2, 0.002, 0.003, 0.0, false)]
    [InlineData("chamfer_dist_angle_2mm45", 1, 0.002, null, Math.PI / 4, false)]
    [InlineData("chamfer_dist_angle_2mm45_resave", 1, 0.002, null, Math.PI / 4, false)]
    public void NativeChamferSamples_ParseVerifiedFields(
        string sample, int expectedType, double distance0, double? distance1,
        double expectedAngle, bool equalDistance)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Chamfer));
        Assert.Equal(ChamferClass, feature.TypeName);
        Assert.Equal(0x52, feature.Id);

        var definition = Assert.IsType<ChamferFeatureDefinitionData>(feature.Definition);
        Assert.Equal(expectedType, definition.Type);
        Assert.Equal(distance0, definition.EdgeChamferDistanceSide0!.Value, 12);
        Assert.Equal(distance1, definition.EdgeChamferDistanceSide1);
        if (distance1.HasValue)
            Assert.Null(definition.EdgeChamferAngle);
        else
        {
            Assert.Null(definition.EdgeChamferDistanceSide1);
            Assert.Equal(expectedAngle, definition.EdgeChamferAngle!.Value, 12);
        }
        Assert.Equal(equalDistance, definition.EqualDistance);
        Assert.True(definition.IsResolved(nameof(ChamferFeatureDefinitionData.EdgeChamferDistanceSide0)));
        Assert.True(definition.IsResolved(nameof(ChamferFeatureDefinitionData.Type)));
        var edge = Assert.Single(definition.Edges!);
        Assert.Equal(SelectionEntityType.Edge, edge.EntityType);
        Assert.Equal(4u, edge.EntityId);
        Assert.Equal(0u, edge.ClassWord);
        Assert.Equal(PersistentEdgeReferenceParser.ExpectedArchiveGuid, edge.ArchiveGuid);
        Assert.Equal(4, edge.RecordCount);
        Assert.Null(definition.Faces);
        Assert.Null(definition.Vertex);
    }

    [Fact]
    public void ChamferAuthoring_DistanceDistance_Roundtrips()
    {
        var read = AuthorAndParse(new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = 0.0025,
            EdgeChamferDistanceSide1 = 0.004
        }, "chamfer_dist_dist_2_3mm", out _, out string featureName);

        Assert.Equal(2, read.Type);
        Assert.Equal(0.0025, read.EdgeChamferDistanceSide0!.Value, 12);
        Assert.Equal(0.004, read.EdgeChamferDistanceSide1!.Value, 12);
        Assert.False(read.EqualDistance);
        Assert.Null(read.EdgeChamferAngle);
        Assert.Equal("倒角1", featureName);
    }

    [Fact]
    public void ChamferAuthoring_AngleDistance_Roundtrips()
    {
        double angle = Math.PI / 6.0;
        var read = AuthorAndParse(new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = 0.003,
            EdgeChamferAngle = angle
        }, "chamfer_dist_angle_2mm45", out _, out string featureName);

        Assert.Equal(1, read.Type);
        Assert.Equal(0.003, read.EdgeChamferDistanceSide0!.Value, 12);
        Assert.Equal(angle, read.EdgeChamferAngle!.Value, 12);
        Assert.Null(read.EdgeChamferDistanceSide1);
        Assert.False(read.EqualDistance);
        Assert.Equal("倒角1", featureName);
    }

    [Fact]
    public void ChamferAuthoring_DistanceDistance_WritesMeasuredSlots()
    {
        AuthorAndParse(new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = 0.0025,
            EdgeChamferDistanceSide1 = 0.004
        }, "chamfer_dist_dist_2_3mm", out byte[] stream, out _);

        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == ChamferClass));
        var compEdge = Assert.Single(doc.AllElements.Where(e =>
            e.ClassName == "moCompEdge_c" && e.TagOffset >= feature.TagOffset));
        byte[] raw = SliceElement(doc, compEdge, stream.Length);
        Assert.Equal(ChamferSegments.ExpectedCompEdgeLengthDistanceDistance, raw.Length);

        Assert.Equal(0.0025,
            ExtrudeAdditionalOptionParser.ReadNamedParameter(raw, ChamferSegments.DistanceParameterOffset)!.Value, 12);
        Assert.Equal(0.004,
            ExtrudeAdditionalOptionParser.ReadNamedParameter(raw, ChamferSegments.SecondDistanceParameterOffset)!.Value, 12);
        foreach (int slot in ChamferSegments.DistancePositiveSlots)
            Assert.Equal(0.0025, BitConverter.ToDouble(raw, slot), 12);
        foreach (int slot in ChamferSegments.DistanceNegativeSlots)
            Assert.Equal(-0.0025, BitConverter.ToDouble(raw, slot), 12);
        foreach (int slot in ChamferSegments.SecondDistancePositiveSlots)
            Assert.Equal(0.004, BitConverter.ToDouble(raw, slot), 12);
        foreach (int slot in ChamferSegments.SecondDistanceNegativeSlots)
            Assert.Equal(-0.004, BitConverter.ToDouble(raw, slot), 12);
        Assert.DoesNotContain(doc.AllElements, e =>
            e.ClassName == "moAngleParameter_c" && e.TagOffset >= feature.TagOffset);
    }

    [Fact]
    public void ChamferAuthoring_AngleDistance_WritesMeasuredSlots()
    {
        double angle = Math.PI / 6.0;
        AuthorAndParse(new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = 0.003,
            EdgeChamferAngle = angle
        }, "chamfer_dist_angle_2mm45", out byte[] stream, out _);

        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == ChamferClass));
        var compEdge = Assert.Single(doc.AllElements.Where(e =>
            e.ClassName == "moCompEdge_c" && e.TagOffset >= feature.TagOffset));
        byte[] raw = SliceElement(doc, compEdge, stream.Length);
        Assert.Equal(ChamferSegments.ExpectedCompEdgeLengthAngleDistance, raw.Length);
        Assert.Equal(0.003,
            ExtrudeAdditionalOptionParser.ReadNamedParameter(raw, ChamferSegments.DistanceParameterOffset)!.Value, 12);
        foreach (int slot in ChamferSegments.DistancePositiveSlots)
            Assert.Equal(0.003, BitConverter.ToDouble(raw, slot), 12);
        foreach (int slot in ChamferSegments.DistanceNegativeSlots)
            Assert.Equal(-0.003, BitConverter.ToDouble(raw, slot), 12);

        var angleElement = Assert.Single(doc.AllElements.Where(e =>
            e.ClassName == "moAngleParameter_c" && e.TagOffset >= feature.TagOffset));
        byte[] angleRaw = SliceElement(doc, angleElement, stream.Length);
        Assert.Equal(ChamferSegments.ExpectedAngleParamLength, angleRaw.Length);
        Assert.Equal(angle,
            ExtrudeAdditionalOptionParser.ReadNamedParameter(angleRaw, ChamferSegments.AngleParameterOffset)!.Value, 12);
        foreach (int slot in ChamferSegments.AngleSlots)
            Assert.Equal(angle, BitConverter.ToDouble(angleRaw, slot), 12);
        Assert.Equal(0.003, BitConverter.ToDouble(angleRaw, ChamferSegments.AngleParameterDistanceCopySlot), 12);
    }

    [Theory]
    [InlineData("chamfer_dist_dist_2_3mm")]
    [InlineData("chamfer_dist_angle_2mm45")]
    public void ChamferAuthoring_PreservesMeasuredEdgeSelection(string sampleName)
    {
        ChamferFeatureDefinitionData definition = sampleName.Contains("dist_dist")
            ? new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .0025, EdgeChamferDistanceSide1 = .004 }
            : new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .003, EdgeChamferAngle = Math.PI / 5 };
        AuthorAndParse(definition, sampleName, out byte[] authored, out _);

        byte[] native;
        using (var file = SwFile.Open(SamplePath(sampleName), readOnly: true))
            native = (byte[])file.RawStreams[ResolvedStream].Clone();

        var nativeDoc = MfcDocument.Parse(native);
        var authoredDoc = MfcDocument.Parse(authored);
        var lengths = new Dictionary<string, int>
        {
            ["moSurfaceIdRep_c"] = 14,
            ["moFilletSurfIdRep_c"] = 20,
            ["moEdgeRef_c"] = 146
        };
        foreach (var pair in lengths)
        {
            var nativeElement = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == pair.Key));
            var authoredElement = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == pair.Key));
            Assert.Equal(
                MfcPrimitives.CopyRange(native, nativeElement.DataStart, nativeElement.DataStart + pair.Value),
                MfcPrimitives.CopyRange(authored, authoredElement.DataStart, authoredElement.DataStart + pair.Value));
        }

        // The shared edge-reference prefix of moCompEdge_c is preserved byte-for-byte.
        var nativeEdge = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moCompEdge_c"));
        var authoredEdge = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moCompEdge_c"));
        Assert.Equal(
            MfcPrimitives.CopyRange(native, nativeEdge.DataStart, nativeEdge.DataStart + ChamferSegments.DistanceParameterOffset),
            MfcPrimitives.CopyRange(authored, authoredEdge.DataStart, authoredEdge.DataStart + ChamferSegments.DistanceParameterOffset));
    }

    [Fact]
    public void ChamferAuthoring_CustomFeatureNameIsWrittenToHeader()
    {
        using var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm");
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddChamfer(new ChamferFeatureDefinitionData
            {
                EdgeChamferDistanceSide0 = .002,
                EdgeChamferDistanceSide1 = .004
            }, "Edge Chamfer");
            Assert.Equal("Edge Chamfer", feature.Name);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var chamfer = Assert.Single(doc.AllElements.Where(e => e.ClassName == ChamferClass));
        Assert.Equal("Edge Chamfer", chamfer.AsWChar());
    }

    [Fact]
    public void ChamferAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedChamferBase("chamfer_dist_angle_2mm45");
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddChamfer(new ChamferFeatureDefinitionData
            {
                EdgeChamferDistanceSide0 = .0025,
                EdgeChamferAngle = Math.PI / 5
            });
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
        var definition = Assert.IsType<ChamferFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Chamfer)).Definition);
        Assert.Equal(.0025, definition.EdgeChamferDistanceSide0!.Value, 12);
        Assert.Equal(Math.PI / 5, definition.EdgeChamferAngle!.Value, 12);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ChamferAuthoringRejectsInvalidDistance(double distance)
    {
        using var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm");
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        Assert.Throws<ArgumentOutOfRangeException>(() => part.AddChamfer(new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = distance,
            EdgeChamferDistanceSide1 = .003
        }));
    }

    [Fact]
    public void ChamferAuthoringRejectsInvalidAngle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferAngle = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferAngle = Math.PI / 2 }));
    }

    [Fact]
    public void ChamferAuthoringRejectsEqualDistances()
    {
        var error = Assert.Throws<NotSupportedException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .003, EdgeChamferDistanceSide1 = .003 }));
        Assert.Contains("equal", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChamferAuthoringRejectsAmbiguousOrMissingMode()
    {
        Assert.Throws<NotSupportedException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData
            {
                EdgeChamferDistanceSide0 = .002,
                EdgeChamferDistanceSide1 = .003,
                EdgeChamferAngle = Math.PI / 4
            }));
        Assert.Throws<NotSupportedException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002 }));
    }

    [Fact]
    public void ChamferAuthoringRejectsTypeAndEqualDistanceMismatches()
    {
        Assert.Throws<NotSupportedException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData
            { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, Type = 1 }));
        Assert.Throws<NotSupportedException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData
            { EdgeChamferDistanceSide0 = .002, EdgeChamferAngle = Math.PI / 4, Type = 2 }));
        Assert.Throws<NotSupportedException>(() => ChamferFeatureBuilder.ValidateDefinition(
            new ChamferFeatureDefinitionData
            { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, EqualDistance = true }));
    }

    public static IEnumerable<object[]> UnsupportedFields()
    {
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, KeepFeatures = true }, "KeepFeatures" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, TangentPropagation = true }, "TangentPropagation" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, PropagateFeatureToParts = true }, "PropagateFeatureToParts" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, LoopCount = 1 }, "LoopCount" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, VertexChamferDistanceSide0 = .001 }, "VertexChamferDistanceSide0" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, Edges = new[] { new FeatureReferenceData { EntityId = 1 } } }, "Edges" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, Faces = new[] { new FeatureReferenceData { EntityId = 2 } } }, "Faces" };
        yield return new object[] { new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003, Vertex = new FeatureReferenceData() }, "Vertex" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedFields))]
    public void ChamferAuthoringRejectsUnverifiedFieldsWithoutModifyingStream(ChamferFeatureDefinitionData definition, string property)
    {
        using var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm");
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(() => part.AddChamfer(definition));
        Assert.Contains(property, error.Message);

        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void ChamferAuthoringRejectsModeAgainstTheOtherControlledBaseline()
    {
        // Distance-distance definition on the angle-distance base (and vice versa).
        using (var temp = CreateStrippedChamferBase("chamfer_dist_angle_2mm45"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddChamfer(
                new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003 }));
            Assert.Contains("controlled ChamferOptions base body", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm"))
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddChamfer(
                new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferAngle = Math.PI / 5 }));
            Assert.Contains("controlled ChamferOptions base body", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void ChamferAuthoringRejectsExistingFeatureAndNon0x70Streams()
    {
        using (var temp = TestFileProvider.CreateTempCopy(SamplePath("chamfer_dist_dist_2_3mm")))
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddChamfer(
                    new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003 }));
                Assert.Contains("existing", error.Message);
            }
        }

        foreach (ushort version in new ushort[] { 0x6E, 0x71 })
        {
            using var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm");
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                BitConverter.GetBytes(version).CopyTo(raw, 0);
                sw.SetPendingStream(ResolvedStream, raw);
                sw.Save();
            }
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var part = Assert.IsType<SldPart>(sw);
                var error = Assert.Throws<NotSupportedException>(() => part.AddChamfer(
                    new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003 }));
                Assert.Contains($"0x{version:X2}", error.Message);
            }
        }
    }

    [Fact]
    public void ChamferAuthoringRejectsMissingChainAndMarker()
    {
        using (var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm"))
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
            AssertChamferAddRejected(temp, "moSketchChain_c");
        }

        using (var temp = CreateStrippedChamferBase("chamfer_dist_dist_2_3mm"))
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
            AssertChamferAddRejected(temp, "0x67");
        }
    }

    [Fact]
    public void ChamferParserRejectsUnknownLayoutAndDoesNotModifyStream()
    {
        AuthorAndParse(new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = .0025,
            EdgeChamferDistanceSide1 = .004
        }, "chamfer_dist_dist_2_3mm", out byte[] stream, out _);

        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == ChamferClass));
        var snapshot = (byte[])doc.SourceData.Clone();

        Assert.Null(FilletChamferFeatureParser.ParseChamfer(doc, "moChamfer_c", 0x4D, feature.TagOffset, stream.Length));
        Assert.Null(FilletChamferFeatureParser.ParseChamfer(doc, ChamferClass, 0x4D, feature.TagOffset, stream.Length));
        Assert.Null(FilletChamferFeatureParser.ParseChamfer(doc, ChamferClass, ChamferHeaderTypeId, stream.Length, stream.Length));

        var compEdge = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moCompEdge_c"));
        byte[] tampered = (byte[])stream.Clone();
        BitConverter.GetBytes(0.007).CopyTo(tampered, compEdge.DataStart + ChamferSegments.DistancePositiveSlots[1]);
        var tamperedDoc = MfcDocument.Parse(tampered);
        var tamperedFeature = Assert.Single(tamperedDoc.AllElements.Where(e => e.ClassName == ChamferClass));
        Assert.Null(FilletChamferFeatureParser.ParseChamfer(
            tamperedDoc, ChamferClass, ChamferHeaderTypeId, tamperedFeature.TagOffset, tampered.Length));

        Assert.Equal(snapshot, doc.SourceData);
    }

    private static void AssertChamferAddRejected(TempTestFile temp, string expectedMessage)
    {
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddChamfer(
                new ChamferFeatureDefinitionData { EdgeChamferDistanceSide0 = .002, EdgeChamferDistanceSide1 = .003 }));
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
