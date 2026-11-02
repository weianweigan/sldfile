using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Authoring coverage for the verified single-direction Blind solid revolution:
/// round-trip angle/reverse/axis layout, resave, and fail-fast rejection paths.
/// </summary>
public class RevolveAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static TempTestFile CreateStrippedRevolveSketchPart(string sampleName = "revolve_180")
    {
        string sample = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "RevolveOptions", sampleName + ".SLDPRT");
        var temp = TestFileProvider.CreateTempCopy(sample);

        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var raw = sw.ResolvedFeatures.RawData!;

        var folders = new System.Collections.Generic.List<FeatureFolder>();
        void Walk(System.Collections.Generic.IEnumerable<FeatureFolder> current)
        {
            foreach (var folder in current)
            {
                folders.Add(folder);
                Walk(folder.Children);
            }
        }
        Walk(sw.ResolvedFeatures.Folders);
        var revolve = folders.First(f => f.ClassName == "moRevolution_c");
        int cutOffset = revolve.TagOffset;

        byte[] sketchOnly = new byte[cutOffset];
        Array.Copy(raw, 0, sketchOnly, 0, cutOffset);
        ushort folderCount = BitConverter.ToUInt16(sketchOnly, 4);
        BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(sketchOnly, 4);

        bool markerFound = false;
        for (int i = cutOffset - 4; i >= Math.Max(0, cutOffset - 256); i--)
        {
            if (sketchOnly[i] == 0x67 && sketchOnly[i + 1] == 0 && sketchOnly[i + 2] == 0 && sketchOnly[i + 3] == 0)
            {
                BitConverter.GetBytes(0u).CopyTo(sketchOnly, i + 4);
                markerFound = true;
                break;
            }
        }
        Assert.True(markerFound, "the native revolve sample must contain the 0x67 sketch marker");

        sw.SetPendingStream(ResolvedStream, sketchOnly);
        sw.Save();
        return temp;
    }

    private static RevolveFeatureDefinitionData AuthorAndReopen(
        RevolveFeatureDefinitionData definition, out byte[] stream, out string featureName,
        string sampleName = "revolve_180")
    {
        using var temp = CreateStrippedRevolveSketchPart(sampleName);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var feature = part.AddRevolve(definition);
            featureName = feature.Name;
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var revolve = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Revolve));
        Assert.Equal(featureName, revolve.Name);
        return Assert.IsType<RevolveFeatureDefinitionData>(revolve.Definition);
    }

    private static string BaseFor(double angle, bool reverse)
    {
        if (Math.Abs(angle - 2.0 * Math.PI) <= 1e-12) return "revolve_360";
        return reverse ? "revolve_180_reverse" : "revolve_180";
    }

    [Theory]
    [InlineData(Math.PI / 2.0, false)]
    [InlineData(Math.PI, false)]
    [InlineData(0.75 * Math.PI, false)]
    [InlineData(Math.PI, true)]
    [InlineData(0.75 * Math.PI, true)]
    [InlineData(2.0 * Math.PI, false)]
    public void RevolveAuthoring_RoundtripsAngleAndDirection(double angle, bool reverse)
    {
        var read = AuthorAndReopen(new RevolveFeatureDefinitionData
        {
            Angle = angle,
            ReverseDirection = reverse
        }, out _, out _, BaseFor(angle, reverse));

        Assert.Equal(angle, read.Angle, 12);
        Assert.Equal(reverse, read.ReverseDirection);
        Assert.Equal(0, read.Type);
        Assert.False(read.BothDirections);
        Assert.Equal(0, read.GetRevolutionAngle(false));
        Assert.Equal(0x4Eu, read.TypeId);
    }

    [Theory]
    [InlineData(Math.PI / 2.0, false)]
    [InlineData(Math.PI, true)]
    [InlineData(2.0 * Math.PI, false)]
    public void RevolveAuthoring_EmitsMeasuredEndSpecAngleAndAxisLayout(double angle, bool reverse)
    {
        AuthorAndReopen(new RevolveFeatureDefinitionData
        {
            Angle = angle,
            ReverseDirection = reverse
        }, out byte[] stream, out _, BaseFor(angle, reverse));

        var doc = MfcDocument.Parse(stream);
        var feature = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moRevolution_c"));
        var nodes = doc.AllElements.Where(e => e.TagOffset >= feature.TagOffset).ToList();

        var endSpec = Assert.Single(nodes.Where(e => e.ClassName == "moRevEndSpec_c"));
        Assert.Equal(1u, BitConverter.ToUInt32(stream, endSpec.DataStart));
        for (int i = endSpec.DataStart + 4; i < endSpec.DataStart + 28; i++)
            Assert.Equal(0, stream[i]);

        var axis = Assert.Single(nodes.Where(e => e.ClassName == "moLineRef_w"));
        Assert.Equal(reverse ? 1u : 0u, BitConverter.ToUInt32(stream, axis.TagOffset - 19));
        Assert.Equal(0x8064, BitConverter.ToUInt16(stream, axis.TagOffset - 12));

        var parameter = Assert.Single(nodes.Where(e => e.ClassName == "moAngleParameter_c"));
        var rawParameter = stream.Skip(parameter.DataStart).ToArray();
        Assert.Equal("D1", MfcPrimitives.PeekWChar(rawParameter, 2));
        Assert.Equal(angle, ExtrudeAdditionalOptionParser.ReadNamedParameter(rawParameter, 0)!.Value, 12);

        bool full = Math.Abs(angle - 2.0 * Math.PI) <= 1e-12;
        Assert.Equal(!full, nodes.Any(e => e.ClassName == "moEndFaceSurfIdRep_c"));
        var fromSkt = nodes.Single(e => e.ClassName == "moFromSktEntSurfIdRep_c");
        if (full)
        {
            // Full revolutions carry an empty sketch-surface mapping.
            Assert.Equal(0, stream[fromSkt.DataStart]);
            Assert.Equal(0, stream[fromSkt.DataStart + 1]);
        }
        else
        {
            Assert.Equal(0xE8, stream[fromSkt.DataStart + 2]);
            Assert.Equal(0x80, stream[fromSkt.DataStart + 3]);
        }
    }

    [Fact]
    public void RevolveAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedRevolveSketchPart("revolve_180_reverse");
        double angle = 0.6 * Math.PI;
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddRevolve(new RevolveFeatureDefinitionData { Angle = angle, ReverseDirection = true });
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
        var definition = Assert.IsType<RevolveFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Revolve)).Definition);
        Assert.Equal(angle, definition.Angle, 12);
        Assert.True(definition.ReverseDirection);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(2.0 * Math.PI + 0.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RevolveAuthoringRejectsInvalidAngles(double angle)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RevolveFeatureBuilder.ValidateDefinition(new RevolveFeatureDefinitionData { Angle = angle }));
    }

    public static System.Collections.Generic.IEnumerable<object[]> UnsupportedDefinitions()
    {
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, IsCut = true }, "360-degree" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, IsThinFeature = true, ThinWallType = 2, Thickness1 = .001 }, "ThinWallType" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, BothDirections = true, Type = 2 }, "AngleDirection2" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, Type = 2 }, "Type" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, ThinWallType = 2 }, "ThinWallType" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, Thickness1 = .001 }, "Thickness1" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, Thickness2 = .001 }, "Thickness2" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, Merge = false }, "Merge" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = .01, AngleDirection2 = .5 }, "AngleDirection2" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = 1.5 * Math.PI }, "Angle" };
        yield return new object[] { new RevolveFeatureDefinitionData { Angle = 2.0 * Math.PI, ReverseDirection = true }, "ReverseDirection" };
    }

    [Theory]
    [MemberData(nameof(UnsupportedDefinitions))]
    public void UnsupportedRevolveCombinationsAreRejectedWithoutModifyingStream(RevolveFeatureDefinitionData definition, string property)
    {
        using var temp = CreateStrippedRevolveSketchPart();
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(() => part.AddRevolve(definition));
        Assert.Contains(property, error.Message);

        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void RevolveAuthoringRejectsSketchesOutsideTheControlledSpecimen()
    {
        using var temp = TestFileProvider.CreateTempCopy(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "extrude_10mm.SLDPRT"));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        var original = (byte[])part.RawStreams[ResolvedStream].Clone();

        var error = Assert.Throws<NotSupportedException>(
            () => part.AddRevolve(new RevolveFeatureDefinitionData { Angle = Math.PI }));
        Assert.Contains("controlled rectangle", error.Message);

        part.Save();
        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(original, reopened.RawStreams[ResolvedStream]);
    }

    [Theory]
    [InlineData(0x6E)]
    [InlineData(0x71)]
    public void RevolveAuthoringRejectsNon0x70Streams(ushort version)
    {
        using var temp = CreateStrippedRevolveSketchPart();
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            BitConverter.GetBytes(version).CopyTo(raw, 0);
            sw.SetPendingStream(ResolvedStream, raw);
            sw.Save();
        }

        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(
                () => part.AddRevolve(new RevolveFeatureDefinitionData { Angle = Math.PI }));
            Assert.Contains($"0x{version:X2}", error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    [Fact]
    public void RevolveAuthoringRejectsMissingSketchChainAndMarker()
    {
        // Missing chain: corrupt the class-name tag.
        using (var temp = CreateStrippedRevolveSketchPart())
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
            AssertAddRevolveRejected(temp, "moSketchChain_c");
        }

        // Missing 0x67 marker.
        using (var temp = CreateStrippedRevolveSketchPart())
        {
            using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
            {
                var raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
                bool corrupted = false;
                for (int i = raw.Length - 4; i >= Math.Max(0, raw.Length - 256); i--)
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
            AssertAddRevolveRejected(temp, "0x67");
        }
    }

    [Theory]
    [InlineData("revolve_180", Math.PI, false)]
    [InlineData("revolve_180_reverse", Math.PI, true)]
    [InlineData("revolve_360", 2.0 * Math.PI, false)]
    public void RevolveAuthoring_ReproducesNativeFeatureBlockExceptAllocatedFeatureId(
        string baseName, double angle, bool reverse)
    {
        byte[] native = ReadSampleStream(baseName);
        var nativeDoc = MfcDocument.Parse(native);
        var nativeFeature = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moRevolution_c"));
        byte[] nativeRegion = MfcPrimitives.CopyRange(native, nativeFeature.TagOffset, native.Length);
        byte[] expected = PatchRevolveFeatureId(nativeRegion, 0x6DE654A5u);

        AuthorAndReopen(new RevolveFeatureDefinitionData
        {
            Angle = angle,
            ReverseDirection = reverse
        }, out byte[] stream, out string featureName, baseName);
        Assert.Equal("旋转1", featureName);

        var authoredDoc = MfcDocument.Parse(stream);
        var authoredFeature = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moRevolution_c"));
        byte[] authoredRegion = MfcPrimitives.CopyRange(stream, authoredFeature.TagOffset, stream.Length);
        Assert.Equal(expected, authoredRegion);

        // The sketch/feature surface serials must come from the target base, not a
        // fixed allocation. Reverse uses distinct sketch/feature serials.
        var serials = ReadAuthoredSurfaceSerials(stream, authoredFeature.TagOffset);
        Assert.Equal(baseName == "revolve_180_reverse" ? 0x6AE40CB0u : baseName == "revolve_360" ? 0x6AE40CA3u : 0x6AE40CADu, serials.Sketch);
        Assert.Equal(baseName == "revolve_180_reverse" ? 0x6AE40CB1u : baseName == "revolve_360" ? 0x6AE40CA3u : 0x6AE40CADu, serials.Feature);
    }

    private static (uint Sketch, uint Feature) ReadAuthoredSurfaceSerials(byte[] stream, int featureTag)
    {
        var doc = MfcDocument.Parse(stream);
        var nodes = doc.AllElements.Where(e => e.TagOffset >= featureTag).ToList();
        var cstring = nodes.Single(e => e.ClassName == "moCStringHandle_c");
        var compSketch = nodes.Single(e => e.ClassName == "moCompSketchEntHandle_c");
        uint featureSerial = BitConverter.ToUInt32(stream, cstring.DataStart + 81);
        uint sketchSerial = BitConverter.ToUInt32(stream, compSketch.DataStart + 89);
        Assert.Equal(BitConverter.ToUInt32(stream, cstring.DataStart + 19), sketchSerial - 1);
        return (sketchSerial, featureSerial);
    }

    private static byte[] PatchRevolveFeatureId(byte[] featureRegion, uint featureId)
    {
        byte[] copy = (byte[])featureRegion.Clone();
        int magic = FindBytes(copy, new byte[] { 0x1D, 0x68, 0xDD, 0x01 });
        Assert.True(magic >= 0);
        BitConverter.GetBytes(featureId).CopyTo(copy, magic + 4);
        return copy;
    }

    private static byte[] ReadSampleStream(string sampleName)
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "RevolveOptions", sampleName + ".SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Fact]
    public void RevolveAuthoringRejectsMismatchedLayoutBases()
    {
        using (var temp = CreateStrippedRevolveSketchPart("revolve_180"))
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = 2.0 * Math.PI }, "revolve_360");

        using (var temp = CreateStrippedRevolveSketchPart("revolve_180"))
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData
            {
                Angle = Math.PI,
                ReverseDirection = true
            }, "revolve_180_reverse");

        using (var temp = CreateStrippedRevolveSketchPart("revolve_360"))
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = Math.PI }, "revolve_180");

        using (var temp = CreateStrippedRevolveSketchPart("revolve_90"))
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = Math.PI }, "base signature");

        using (var temp = CreateStrippedRevolveSketchPart("revolve_360_resave"))
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = 2.0 * Math.PI }, "base signature");

        using (var temp = CreateStrippedRevolveSketchPart("revolve_180"))
        {
            RewriteStream(temp, raw =>
            {
                int comp = FindAscii(raw, "moCompFeature_c");
                Assert.True(comp > 0);
                raw[comp] = (byte)'n';
            });
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = Math.PI }, "base signature");
        }
    }

    [Fact]
    public void RevolveAuthoringRejectsWrongFolderCountAndSketchChildCount()
    {
        using (var temp = CreateStrippedRevolveSketchPart("revolve_180"))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)20).CopyTo(raw, 4));
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = Math.PI }, "folder count");
        }

        using (var temp = CreateStrippedRevolveSketchPart("revolve_180"))
        {
            RewriteStream(temp, raw =>
            {
                int marker = FindMarker(raw);
                BitConverter.GetBytes(1u).CopyTo(raw, marker + 4);
            });
            AssertRevolveAddRejected(temp, new RevolveFeatureDefinitionData { Angle = Math.PI }, "child count");
        }
    }

    private static void RewriteStream(TempTestFile temp, Action<byte[]> mutate)
    {
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        mutate(raw);
        sw.SetPendingStream(ResolvedStream, raw);
        sw.Save();
    }

    private static int FindMarker(byte[] stream)
    {
        for (int i = stream.Length - 4; i >= Math.Max(0, stream.Length - 256); i--)
        {
            if (stream[i] == 0x67 && stream[i + 1] == 0 && stream[i + 2] == 0 && stream[i + 3] == 0)
                return i;
        }
        throw new InvalidOperationException("0x67 marker not found");
    }

    private static int FindBytes(byte[] data, byte[] pattern)
    {
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

    private static void AssertRevolveAddRejected(
        TempTestFile temp, RevolveFeatureDefinitionData definition, string expectedMessage)
    {
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddRevolve(definition));
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        Assert.Equal(before, reopened.RawStreams[ResolvedStream]);
    }

    private static void AssertAddRevolveRejected(TempTestFile temp, string expectedMessage)
    {
        byte[] before;
        using (var read = SwFile.Open(temp.FilePath, readOnly: true))
            before = (byte[])read.RawStreams[ResolvedStream].Clone();

        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(
                () => part.AddRevolve(new RevolveFeatureDefinitionData { Angle = Math.PI }));
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
