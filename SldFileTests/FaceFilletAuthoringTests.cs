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
/// Fixed measured Type=2 face-fillet authoring: exact 3 mm / 5 mm two-face blocks on the
/// controlled source and resave stripped bases, with fail-fast base/name/face/radius guards.
/// </summary>
public class FaceFilletAuthoringTests
{
    private const string ResolvedStream = "Contents/Config-0-ResolvedFeatures";
    private const string FilletClass = "Fillet_c";

    private static string SamplePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "TestFiles", "Features2026", "FilletOptions", name + ".SLDPRT");

    private static TempTestFile CreateStrippedBase(string sample, out byte[] nativeBlock)
    {
        var temp = TestFileProvider.CreateTempCopy(SamplePath(sample));
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] raw = sw.ResolvedFeatures.RawData!;
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
        int cut = fillet.TagOffset;
        nativeBlock = raw.AsSpan(cut).ToArray();
        byte[] stripped = raw.AsSpan(0, cut).ToArray();
        ushort count = BitConverter.ToUInt16(stripped, 4);
        BitConverter.GetBytes((ushort)(count - 1)).CopyTo(stripped, 4);
        Assert.Equal((ushort)0x70, BitConverter.ToUInt16(stripped, 0));
        sw.SetPendingStream(ResolvedStream, stripped);
        sw.Save();
        return temp;
    }

    private static void Author(
        TempTestFile temp, FilletFeatureDefinitionData definition, string? name = null)
    {
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        var part = Assert.IsType<SldPart>(sw);
        part.AddFillet(definition, name);
        part.Save();
    }

    private static FilletFeatureDefinitionData ReadFaceDefinition(string sample)
    {
        using var file = SwFile.Open(SamplePath(sample), readOnly: true);
        return Assert.IsType<FilletFeatureDefinitionData>(
            Assert.Single(file.Features.Where(f => f.Kind == FeatureKind.Fillet)).Definition);
    }

    [Theory]
    [InlineData("fillet_face_3mm", 0.003)]
    [InlineData("fillet_face_3mm_resave", 0.003)]
    [InlineData("fillet_face_5mm", 0.005)]
    [InlineData("fillet_face_5mm_resave", 0.005)]
    public void FaceFilletAuthoring_ReproducesNativeBlockAndRoundTrips(string sample, double radius)
    {
        using var temp = CreateStrippedBase(sample, out byte[] nativeBlock);
        Author(temp, new FilletFeatureDefinitionData
        {
            Type = 2,
            DefaultRadius = radius
        });

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        byte[] stream = (byte[])reopened.RawStreams[ResolvedStream].Clone();
        var doc = MfcDocument.Parse(stream);
        var fillet = Assert.Single(doc.AllElements.Where(e => e.ClassName == FilletClass));
        byte[] authoredBlock = stream.AsSpan(fillet.TagOffset).ToArray();
        Assert.Equal(nativeBlock, authoredBlock);

        var definition = Assert.IsType<FilletFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Fillet)).Definition);
        Assert.Equal(2, definition.Type);
        Assert.Equal(radius, definition.DefaultRadius!.Value, 12);
        Assert.Equal(2, definition.FaceReferences!.Count);
        Assert.Equal(0x5D2u, definition.FaceReferences[0].EntityId);
        Assert.Equal(0x5CEu, definition.FaceReferences[1].EntityId);
        Assert.Equal("FaceFillet",
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Fillet)).Name);
    }

    [Theory]
    [InlineData("fillet_face_3mm")]
    [InlineData("fillet_face_5mm")]
    public void FaceFilletAuthoring_AcceptsParsedFacePairInOrder(string sample)
    {
        var parsed = ReadFaceDefinition(sample);
        double radius = parsed.DefaultRadius!.Value;
        using var temp = CreateStrippedBase(sample, out _);
        Author(temp, parsed);

        using var reopened = SwFile.Open(temp.FilePath, readOnly: true);
        var read = Assert.IsType<FilletFeatureDefinitionData>(
            Assert.Single(reopened.Features.Where(f => f.Kind == FeatureKind.Fillet)).Definition);
        Assert.Equal(radius, read.DefaultRadius!.Value, 12);
        Assert.Equal(new uint?[] { 0x5D2u, 0x5CEu },
            read.FaceReferences!.Select(f => f.EntityId).ToArray());
    }

    [Fact]
    public void FaceFilletAuthoring_RejectsCustomNames()
    {
        using var temp = CreateStrippedBase("fillet_face_3mm", out _);
        var ex = Assert.Throws<NotSupportedException>(() => Author(
            temp, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.003 }, "MyFaceFillet"));
        Assert.Contains("FaceFillet", ex.Message);
    }

    [Fact]
    public void FaceFilletAuthoring_RejectsWrongRadiusFacesTypeAndBase()
    {
        using var temp3 = CreateStrippedBase("fillet_face_3mm", out _);
        Reject(temp3, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.004 });

        using var temp5 = CreateStrippedBase("fillet_face_5mm", out _);
        Reject(temp5, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.003 });
        Reject(temp5, new FilletFeatureDefinitionData { Type = 1, DefaultRadius = 0.005 });
        Reject(temp5, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.005,
            FaceReferences = new[] { Face(0x5CE), Face(0x5D2) } });
        Reject(temp5, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.005,
            FaceReferences = new[] { Face(0x5D2), Face(0x5D2) } });
        Reject(temp5, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.005,
            FaceReferences = new[] { Face(0x5D2, classWord: 0x200), Face(0x5CE) } });
        Reject(temp5, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.005,
            FaceReferences = new[] { Face(0x5D2), Face(0x5CE), Face(0x5D2) } });
        Reject(temp5, new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.005,
            Edges = new[] { new FeatureReferenceData() } });

        static void Reject(TempTestFile temp, FilletFeatureDefinitionData definition)
        {
            using var sw = SwFile.Open(temp.FilePath, readOnly: false);
            byte[] before = (byte[])sw.RawStreams[ResolvedStream].Clone();
            var part = Assert.IsType<SldPart>(sw);
            Assert.ThrowsAny<Exception>(() => part.AddFillet(definition));
            Assert.Equal(before, sw.RawStreams[ResolvedStream]);
        }
    }

    [Fact]
    public void FaceFilletAuthoring_RejectsWrongFolderMarkerAndBaseSignature()
    {
        using var temp = CreateStrippedBase("fillet_face_3mm", out _);
        using var sw = SwFile.Open(temp.FilePath, readOnly: false);
        byte[] before = (byte[])sw.RawStreams[ResolvedStream].Clone();

        byte[] wrongFolder = (byte[])before.Clone();
        BitConverter.GetBytes((ushort)19).CopyTo(wrongFolder, 4);
        sw.SetPendingStream(ResolvedStream, wrongFolder);
        Assert.Throws<NotSupportedException>(() =>
            ((SldPart)sw).AddFillet(new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.003 }));

        byte[] wrongMarker = (byte[])before.Clone();
        int marker = FindMarker(wrongMarker);
        Assert.True(marker > 0);
        BitConverter.GetBytes(0u).CopyTo(wrongMarker, marker + 4);
        sw.SetPendingStream(ResolvedStream, wrongMarker);
        Assert.Throws<NotSupportedException>(() =>
            ((SldPart)sw).AddFillet(new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.003 }));

        byte[] wrongBase = (byte[])before.Clone();
        wrongBase[wrongBase.Length - 1] ^= 0xFF;
        sw.SetPendingStream(ResolvedStream, wrongBase);
        Assert.Throws<NotSupportedException>(() =>
            ((SldPart)sw).AddFillet(new FilletFeatureDefinitionData { Type = 2, DefaultRadius = 0.003 }));
    }

    private static FeatureReferenceData Face(
        uint id, uint classWord = 0x300, string guid = FaceFilletSegments.ArchiveGuid) =>
        new FeatureReferenceData
        {
            EntityType = SelectionEntityType.Face,
            EntityId = id,
            ClassWord = classWord,
            ArchiveGuid = guid,
            RecordCount = 6
        };

    private static int FindMarker(byte[] stream)
    {
        for (int i = 0; i <= stream.Length - 8; i++)
            if (stream[i] == 0x67 && stream[i + 1] == 0 && stream[i + 2] == 0 &&
                stream[i + 3] == 0 && BitConverter.ToUInt32(stream, i + 4) == 1)
                return i;
        return -1;
    }
}
