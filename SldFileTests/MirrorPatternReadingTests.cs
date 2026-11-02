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
/// Controlled reading coverage for the SW2026 mirror pattern
/// (moMirrorPattern_c / measured FolderHeader TypeId 0x57, stream 0x72).
/// </summary>
public class MirrorPatternReadingTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "PatternOptions", name + ".SLDPRT");

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Theory]
    [InlineData("mirror_plane")]
    [InlineData("mirror_plane_resave")]
    public void NativeMirrorPattern_ParsesPlaneAndSourceFeature(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Mirror));
        Assert.Equal("moMirrorPattern_c", feature.TypeName);
        Assert.Equal(0x57, feature.Id);
        Assert.Equal("Mirror", feature.Name);

        var definition = Assert.IsType<MirrorPatternFeatureDefinitionData>(feature.Definition);
        Assert.Equal("moMirrorPattern_c", definition.ClassName);
        Assert.Equal(0x57u, definition.TypeId);
        Assert.Equal(1, definition.MirrorPlaneType);
        Assert.NotNull(definition.Plane);
        Assert.Equal(SelectionEntityType.Plane, definition.Plane!.EntityType);
        Assert.Null(definition.Plane.EntityId);
        Assert.NotNull(definition.PatternFeatureArray);
        Assert.Single(definition.PatternFeatureArray!);
        Assert.Equal(1, definition.GetPatternFeatureCount());
        Assert.Equal(1, definition.GetMirrorPlaneType());
        Assert.True(definition.IsResolved(nameof(MirrorPatternFeatureDefinitionData.MirrorPlaneType)));
        Assert.True(definition.IsResolved(nameof(MirrorPatternFeatureDefinitionData.Plane)));
        Assert.True(definition.IsResolved(nameof(MirrorPatternFeatureDefinitionData.PatternFeatureArray)));

        // Flags without a controlled variant stay unresolved.
        Assert.Null(definition.GeometryPattern);
        Assert.Null(definition.FeatureScope);
        Assert.Null(definition.MirrorFaceArray);
        Assert.Null(definition.PropagateVisualProperty);
        Assert.Null(definition.FeatureScopeBodies);
    }

    [Fact]
    public void MirrorPattern_MainAndResaveProduceTheSameDefinition()
    {
        MirrorPatternFeatureDefinitionData Read(string name)
        {
            using var file = SwFile.Open(SamplePath(name), readOnly: true);
            return Assert.IsType<MirrorPatternFeatureDefinitionData>(
                Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Mirror)).Definition);
        }

        var main = Read("mirror_plane");
        var resave = Read("mirror_plane_resave");
        Assert.Equal(main.MirrorPlaneType, resave.MirrorPlaneType);
        Assert.Equal(main.PatternFeatureArray!.Count, resave.PatternFeatureArray!.Count);
        Assert.Equal(main.Plane!.EntityType, resave.Plane!.EntityType);
        Assert.Equal(main.IsResolved(nameof(MirrorPatternFeatureDefinitionData.MirrorPlaneType)),
            resave.IsResolved(nameof(MirrorPatternFeatureDefinitionData.MirrorPlaneType)));
    }

    [Fact]
    public void MirrorPatternParser_RejectsTamperedLayoutsAndWrongGuards()
    {
        byte[] original = ReadStream(SamplePath("mirror_plane"));
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "moMirrorPattern_c");
        Assert.NotNull(PatternFeatureParser.ParseMirrorPattern(
            doc, "moMirrorPattern_c", 0x57, feature.TagOffset, original.Length));

        void Tamper(int offset)
        {
            byte[] copy = (byte[])original.Clone();
            copy[offset] ^= 0xFF;
            var tampered = MfcDocument.Parse(copy);
            var tamperedFeature = tampered.AllElements.First(e => e.ClassName == "moMirrorPattern_c");
            Assert.Null(PatternFeatureParser.ParseMirrorPattern(
                tampered, "moMirrorPattern_c", 0x57, tamperedFeature.TagOffset, copy.Length));
        }

        // Source-feature persistent-reference serial at payload +372.
        Tamper(feature.DataStart + 372);
        // Right-Plane reference block at payload +514.
        Tamper(feature.DataStart + 514);
        // Feature-id magic at payload +143.
        Tamper(feature.DataStart + 143);
        // Header TypeId at payload +30.
        Tamper(feature.DataStart + 30);
        // Arbitrary measured rest byte (payload +638 = rest +600).
        Tamper(feature.DataStart + 638);

        byte[] wrongVersion = (byte[])original.Clone();
        BitConverter.GetBytes((ushort)0x70).CopyTo(wrongVersion, 0);
        var wrongDoc = MfcDocument.Parse(wrongVersion);
        var wrongFeature = wrongDoc.AllElements.First(e => e.ClassName == "moMirrorPattern_c");
        Assert.Null(PatternFeatureParser.ParseMirrorPattern(
            wrongDoc, "moMirrorPattern_c", 0x57, wrongFeature.TagOffset, wrongVersion.Length));

        Assert.Null(PatternFeatureParser.ParseMirrorPattern(
            doc, "moMirrorPattern_c", 0x55, feature.TagOffset, original.Length));
        Assert.Null(PatternFeatureParser.ParseMirrorPattern(
            doc, "moLPattern_c", 0x57, feature.TagOffset, original.Length));
        Assert.Null(PatternFeatureParser.ParseMirrorPattern(
            doc, "moMirrorPattern_c", 0x57, feature.TagOffset, feature.TagOffset + 100));

        // Parsing must not mutate the source buffer.
        Assert.Equal(original, doc.SourceData);
    }

    [Fact]
    public void MirrorPatternMappingDoesNotModifyTheStream()
    {
        string path = SamplePath("mirror_plane");
        byte[] before = ReadStream(path);
        using (var file = SwFile.Open(path, readOnly: true))
        {
            var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Mirror));
            Assert.IsType<MirrorPatternFeatureDefinitionData>(feature.Definition);
            Assert.Equal(before, file.RawStreams[ResolvedStream]);
        }
        Assert.Equal(before, ReadStream(path));
    }
}
