using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Binary.FeatureData;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled Type=2 face-fillet reading coverage (fillet_face_3mm / fillet_face_5mm,
/// source and resave). The face layout is strictly separated from the Type=1 edge layout.
/// </summary>
public class FaceFilletReadingTests
{
    private const string StreamPath = "Contents/Config-0-ResolvedFeatures";

    private static string SamplePath(string name)
    {
        string options = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "FilletOptions", name + ".SLDPRT");
        return File.Exists(options)
            ? options
            : Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", name + ".SLDPRT");
    }

    private static FilletFeatureDefinitionData ReadDefinition(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        var feature = Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet));
        Assert.Equal("Fillet_c", feature.TypeName);
        return Assert.IsType<FilletFeatureDefinitionData>(feature.Definition);
    }

    [Theory]
    [InlineData("fillet_face_3mm", 0.003)]
    [InlineData("fillet_face_3mm_resave", 0.003)]
    [InlineData("fillet_face_5mm", 0.005)]
    [InlineData("fillet_face_5mm_resave", 0.005)]
    public void FaceFillet_ParsesTypeRadiusAndOrderedFaceReferences(string sample, double radius)
    {
        var definition = ReadDefinition(sample);

        Assert.Equal(2, definition.Type);
        Assert.Equal(radius, definition.DefaultRadius!.Value, 12);
        Assert.Null(definition.Edges);

        var faces = Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<FeatureReferenceData>>(
            definition.FaceReferences);
        Assert.Equal(2, faces.Count);
        uint[] expectedIds = { 0x5D2u, 0x5CEu };
        for (int i = 0; i < faces.Count; i++)
        {
            var face = faces[i];
            Assert.Equal(SelectionEntityType.Face, face.EntityType);
            Assert.Equal(expectedIds[i], face.EntityId);
            Assert.Equal(0x300u, face.ClassWord);
            Assert.Equal(FaceFilletSegments.ArchiveGuid, face.ArchiveGuid);
            Assert.Equal(6, face.RecordCount);
            Assert.Null(face.PersistentId);
            Assert.Equal($"FaceFilletFace{i + 1}", face.ReferenceName);
        }

        foreach (string name in new[]
                 {
                     nameof(FilletFeatureDefinitionData.Type),
                     nameof(FilletFeatureDefinitionData.DefaultRadius),
                     nameof(FilletFeatureDefinitionData.FaceReferences)
                 })
            Assert.True(definition.IsResolved(name), name);
        Assert.False(definition.IsResolved(nameof(FilletFeatureDefinitionData.Edges)));
    }

    [Theory]
    [InlineData("fillet_2mm")]
    [InlineData("fillet_5mm")]
    public void EdgeFillet_KeepsTypeUnresolvedAndHasNoFaceReferences(string sample)
    {
        var definition = ReadDefinition(sample);
        Assert.Null(definition.Type);
        Assert.Null(definition.FaceReferences);
        Assert.Single(definition.Edges!);
        Assert.True(definition.IsResolved(nameof(FilletFeatureDefinitionData.Edges)));
        Assert.False(definition.IsResolved(nameof(FilletFeatureDefinitionData.FaceReferences)));
    }

    [Fact]
    public void FaceFilletParser_RejectsTamperedFaceRadiusAndHeaderFields()
    {
        byte[] original;
        using (var file = SwFile.Open(SamplePath("fillet_face_3mm"), readOnly: true))
            original = (byte[])file.RawStreams[StreamPath].Clone();
        var doc = MfcDocument.Parse(original);
        var feature = doc.AllElements.First(e => e.ClassName == "Fillet_c");
        Assert.NotNull(FilletChamferFeatureParser.ParseFillet(
            doc, "Fillet_c", 0x51, feature.TagOffset, original.Length));

        void Tamper(Action<byte[], MfcDocument> mutate)
        {
            byte[] copy = (byte[])original.Clone();
            var copyDoc = MfcDocument.Parse(copy);
            mutate(copy, copyDoc);
            var tampered = MfcDocument.Parse(copy);
            var tamperedFeature = tampered.AllElements.First(e => e.ClassName == "Fillet_c");
            Assert.Null(FilletChamferFeatureParser.ParseFillet(
                tampered, "Fillet_c", 0x51, tamperedFeature.TagOffset, copy.Length));
        }

        Tamper((b, d) =>
        {
            var compFace = d.AllElements.First(e => e.ClassName == "moCompFace_c");
            var parsed = PersistentFaceReferenceParser.ReadCompFacePayload(
                MfcPrimitives.CopyRange(b, compFace.DataStart,
                    NextTag(d, compFace, b.Length)));
            Assert.NotNull(parsed);
            int record = compFace.DataStart + parsed!.Blocks[0].BlockOffset +
                         PersistentFaceReferenceParser.HeaderLength;
            b[record + 19] ^= 0x01; // class word
        });

        Tamper((b, d) =>
        {
            var item = d.AllElements.First(e => e.ClassName == "moFaceBlendItem_c");
            b[item.DataStart + 18] ^= 0xFF; // first face id
        });

        Tamper((b, d) =>
        {
            var compFace = d.AllElements.First(e => e.ClassName == "moCompFace_c");
            b[compFace.DataStart + 0x51 + 27] ^= 0xFF; // first archive GUID byte
        });

        Tamper((b, d) =>
        {
            var radius = d.AllElements.First(e => e.ClassName == "ThreeDRadiusDim_c");
            BitConverter.GetBytes(0.004).CopyTo(b, radius.DataStart + 0x26); // named radius value
        });

        Tamper((b, d) =>
        {
            var radius = d.AllElements.First(e => e.ClassName == "ThreeDRadiusDim_c");
            BitConverter.GetBytes(0.004).CopyTo(b, radius.DataStart + 0x46); // radius copy
        });

        Tamper((b, d) =>
        {
            var radius = d.AllElements.First(e => e.ClassName == "ThreeDRadiusDim_c");
            b[radius.DataStart + FaceFilletSegments.ToleranceSlot1] ^= 0x01; // derived tolerance
        });

        Tamper((b, d) =>
        {
            var feature2 = d.AllElements.First(e => e.ClassName == "Fillet_c");
            b[feature2.DataStart + 34] ^= 0xFF; // FolderHeader TypeId
        });

        Assert.Equal(original, doc.SourceData);
    }

    [Fact]
    public void FaceFilletParser_RejectsWrongTypeStreamOrRange()
    {
        byte[] raw;
        using (var file = SwFile.Open(SamplePath("fillet_face_3mm"), readOnly: true))
            raw = (byte[])file.RawStreams[StreamPath].Clone();
        var doc = MfcDocument.Parse(raw);
        var feature = doc.AllElements.First(e => e.ClassName == "Fillet_c");

        byte[] wrongVersion = (byte[])raw.Clone();
        BitConverter.GetBytes((ushort)0x71).CopyTo(wrongVersion, 0);
        var wrongDoc = MfcDocument.Parse(wrongVersion);
        var wrongFeature = wrongDoc.AllElements.First(e => e.ClassName == "Fillet_c");
        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            wrongDoc, "Fillet_c", 0x51, wrongFeature.TagOffset, wrongVersion.Length));

        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            doc, "Fillet_c", 0x52, feature.TagOffset, raw.Length));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            doc, "Fillet_c", 0x51, feature.TagOffset, feature.TagOffset + 100));
        Assert.Null(FilletChamferFeatureParser.ParseFillet(
            doc, "Fillet_x", 0x51, feature.TagOffset, raw.Length));
    }

    private static int NextTag(MfcDocument doc, MfcElement element, int featureEnd)
    {
        var next = doc.AllElements
            .Where(e => e.TagOffset > element.TagOffset)
            .OrderBy(e => e.TagOffset)
            .FirstOrDefault();
        return next != null && next.TagOffset < featureEnd ? next.TagOffset : featureEnd;
    }
}
