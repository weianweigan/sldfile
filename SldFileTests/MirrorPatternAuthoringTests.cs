using System;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled mirror-pattern authoring on the measured PatternOptions/mirror_plane base.
/// </summary>
public class MirrorPatternAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string NativeName = "Mirror";

    private static string SamplePath(string name) =>
        System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "Features2026", "PatternOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedMirrorBase(out int mirrorTagOffset)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath("mirror_plane"));
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var doc = MfcDocument.Parse(raw);
            var mirror = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moMirrorPattern_c"));
            mirrorTagOffset = mirror.TagOffset;
            byte[] stripped = new byte[mirrorTagOffset];
            Array.Copy(raw, 0, stripped, 0, stripped.Length);
            ushort folderCount = BitConverter.ToUInt16(stripped, 4);
            BitConverter.GetBytes((ushort)(folderCount - 1)).CopyTo(stripped, 4);
            sw.SetPendingStream(ResolvedStream, stripped);
            sw.Save();
        }
        return temp;
    }

    private static MirrorPatternFeatureDefinitionData Definition(bool presenceOnly = true)
    {
        if (!presenceOnly) return new MirrorPatternFeatureDefinitionData();
        return new MirrorPatternFeatureDefinitionData
        {
            MirrorPlaneType = 1,
            Plane = new FeatureReferenceData { EntityType = SelectionEntityType.Plane },
            PatternFeatureArray = new[]
            {
                new FeatureReferenceData { EntityType = SelectionEntityType.None }
            }
        };
    }

    private static byte[] ReadStream(string path)
    {
        using var file = SwFile.Open(path, readOnly: true);
        return (byte[])file.RawStreams[ResolvedStream].Clone();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MirrorPatternAuthoring_RoundTripsNativeLayout(bool presenceOnly)
    {
        using var temp = CreateStrippedMirrorBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var created = part.AddMirrorPattern(Definition(presenceOnly), featureName: "Mirror");
            Assert.Equal(FeatureKind.Mirror, created.Kind);
            part.Save();
        }

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var feature = Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Mirror));
        Assert.Equal("moMirrorPattern_c", feature.TypeName);
        Assert.Equal(0x57, feature.Id);
        Assert.Equal("Mirror", feature.Name);
        var definition = Assert.IsType<MirrorPatternFeatureDefinitionData>(feature.Definition);
        Assert.Equal(1, definition.MirrorPlaneType);
        Assert.Equal(SelectionEntityType.Plane, definition.Plane!.EntityType);
        Assert.Equal(1, definition.GetPatternFeatureCount());
        Assert.Equal((ushort)23, BitConverter.ToUInt16(ReadStream(temp.FilePath), 4));
    }

    [Fact]
    public void MirrorPatternAuthoring_WithNativeValuesReproducesNativeFeatureBlock()
    {
        byte[] native = ReadStream(SamplePath("mirror_plane"));
        var nativeDoc = MfcDocument.Parse(native);
        var nativeFeature = Assert.Single(nativeDoc.AllElements.Where(e => e.ClassName == "moMirrorPattern_c"));
        byte[] nativeBlock = MfcPrimitives.CopyRange(native, nativeFeature.TagOffset, native.Length);

        using var temp = CreateStrippedMirrorBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddMirrorPattern(Definition(), featureName: NativeName);
            part.Save();
        }

        byte[] authored = ReadStream(temp.FilePath);
        var authoredDoc = MfcDocument.Parse(authored);
        var authoredFeature = Assert.Single(authoredDoc.AllElements.Where(e => e.ClassName == "moMirrorPattern_c"));
        byte[] authoredBlock = MfcPrimitives.CopyRange(authored, authoredFeature.TagOffset, authored.Length);
        Assert.Equal(nativeBlock, authoredBlock);
    }

    [Fact]
    public void MirrorPatternAuthoring_ResavePreservesDefinitionAndBytes()
    {
        using var temp = CreateStrippedMirrorBase(out _);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            part.AddMirrorPattern(Definition());
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
        var definition = Assert.IsType<MirrorPatternFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Mirror)).Definition);
        Assert.Equal(1, definition.MirrorPlaneType);
        Assert.Equal(1, definition.GetPatternFeatureCount());
    }

    [Fact]
    public void MirrorPatternAuthoring_RejectsWrongBaseExistingMirrorAndVersion()
    {
        using (var temp = CreateStrippedMirrorBase(out _))
        {
            RewriteStream(temp, raw => raw[7000] ^= 0xFF);
            AssertMirrorAddRejected(temp, Definition(), "base signature");
        }

        using (var full = TestFileProvider.CreateTempCopy(SamplePath("mirror_plane")))
        {
            using var sw = SwFile.Open(full.FilePath, readOnly: false);
            var part = Assert.IsType<SldPart>(sw);
            var original = (byte[])part.RawStreams[ResolvedStream].Clone();
            var error = Assert.Throws<NotSupportedException>(() => part.AddMirrorPattern(Definition()));
            Assert.Contains("existing mirror", error.Message);
            Assert.Equal(original, part.RawStreams[ResolvedStream]);
        }

        using (var temp = CreateStrippedMirrorBase(out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)0x71).CopyTo(raw, 0));
            AssertMirrorAddRejected(temp, Definition(), "0x72");
        }

        using (var temp = CreateStrippedMirrorBase(out _))
        {
            RewriteStream(temp, raw => BitConverter.GetBytes((ushort)23).CopyTo(raw, 4));
            AssertMirrorAddRejected(temp, Definition(), "folder count");
        }
    }

    [Fact]
    public void MirrorPatternAuthoring_RejectsUnverifiedFields()
    {
        using var temp = CreateStrippedMirrorBase(out _);

        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData { GeometryPattern = true }, "GeometryPattern");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData { MirrorPlaneType = 2 }, "MirrorPlaneType");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData { FeatureScope = 1 }, "feature scope");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData
        {
            Plane = new FeatureReferenceData { EntityType = SelectionEntityType.Plane, EntityId = 42 }
        }, "Plane");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData
        {
            PatternFeatureArray = new[]
            {
                new FeatureReferenceData { EntityType = SelectionEntityType.None },
                new FeatureReferenceData { EntityType = SelectionEntityType.None }
            }
        }, "source reference");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData
        {
            MirrorFaceArray = new[] { new FeatureReferenceData { EntityType = SelectionEntityType.Face } }
        }, "mirror faces");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData
        {
            FeatureScopeBodies = new[] { new FeatureReferenceData { EntityType = SelectionEntityType.Body } }
        }, "feature-scope bodies");
        AssertMirrorAddRejected(temp, new MirrorPatternFeatureDefinitionData
        {
            PropagateVisualProperty = true
        }, "PropagateVisualProperty");
    }

    private static void AssertMirrorAddRejected(
        TempTestFile temp, MirrorPatternFeatureDefinitionData definition, string expectedMessage)
    {
        byte[] before = ReadStream(temp.FilePath);
        using (var sw = SwFile.Open(temp.FilePath, readOnly: false))
        {
            var part = Assert.IsType<SldPart>(sw);
            var error = Assert.Throws<NotSupportedException>(() => part.AddMirrorPattern(definition));
            Assert.Contains(expectedMessage, error.Message);
            part.Save();
        }
        Assert.Equal(before, ReadStream(temp.FilePath));
    }

    private static void RewriteStream(TempTestFile temp, Action<byte[]> mutate)
    {
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = (byte[])sw.RawStreams[ResolvedStream].Clone();
        mutate(raw);
        sw.SetPendingStream(ResolvedStream, raw);
        sw.Save();
    }
}
