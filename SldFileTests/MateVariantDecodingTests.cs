using System;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Controlled mate-variant samples (agent5-mates-variants4-20261030):
/// distance aligned/anti-aligned/closest/10/20, parallel, perpendicular,
/// angle30, tangent and concentric. The 188-byte 2026 mate record encodes:
///   header+0xA1 alignment: 0x01 Aligned(0), 0x02 AntiAligned(1), 0x00 Closest(2);
///   class name: moMateCoincident 0, moMateConcentric 1, moMatePerpendicular 2,
///   moMateParallel 3, moMateTangent 4, moMateDistanceDim_c 5,
///   moMatePlanarAngleDim_c 6;
///   Distance D1 in moLengthParameter_c (meters), Angle D1 in
///   moAngleParameter_c (radians).
/// </summary>
public class MateVariantDecodingTests
{
    private const string FrontPlane = "前视基准面"; // 前视基准面
    private const string RightPlane = "右视基准面"; // 右视基准面

    [Theory]
    [InlineData("distance_aligned_10", 5u, AssemblyMateType.Distance, AssemblyMateAlignment.Aligned, 2)]
    [InlineData("distance_antialigned_10", 5u, AssemblyMateType.Distance, AssemblyMateAlignment.AntiAligned, 2)]
    [InlineData("distance_closest_10", 5u, AssemblyMateType.Distance, AssemblyMateAlignment.Aligned, 2)]
    [InlineData("distance_aligned_20", 5u, AssemblyMateType.Distance, AssemblyMateAlignment.Aligned, 2)]
    [InlineData("parallel", 3u, AssemblyMateType.Parallel, AssemblyMateAlignment.Aligned, 2)]
    [InlineData("perpendicular", 2u, AssemblyMateType.Perpendicular, AssemblyMateAlignment.Closest, 2)]
    [InlineData("angle30", 6u, AssemblyMateType.Angle, AssemblyMateAlignment.Aligned, 2)]
    [InlineData("tangent", 4u, AssemblyMateType.Tangent, AssemblyMateAlignment.Aligned, 2)]
    [InlineData("concentric", 1u, AssemblyMateType.Concentric, AssemblyMateAlignment.Aligned, 2)]
    public void VariantSamples_DecodeMateTypeAndAlignment(
        string name, uint typeId, AssemblyMateType type, AssemblyMateAlignment alignment, int entityCount)
    {
        using var file = SwFile.Open(VariantSample(name), readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);
        var mate = Assert.Single(assembly.Mates);

        Assert.Equal(typeId, mate.MateTypeId);
        Assert.Equal(type, mate.MateType);
        Assert.Equal(alignment, mate.Alignment);
        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.MateAlignment);
        Assert.Equal(entityCount, mate.Entities.Count);
        Assert.Equal(entityCount > 0, mate.Resolution.EntityReferences);
    }

    [Theory]
    [InlineData("distance_aligned_10", 0.01)]
    [InlineData("distance_antialigned_10", 0.01)]
    [InlineData("distance_closest_10", 0.01)]
    [InlineData("distance_aligned_20", 0.02)]
    public void DistanceVariants_DecodeDistanceInMeters(string name, double expected)
    {
        using var file = SwFile.Open(VariantSample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal(AssemblyMateType.Distance, mate.MateType);
        Assert.NotNull(mate.Distance);
        Assert.Equal(expected, mate.Distance!.Value, 12);
        Assert.Null(mate.Angle);
        Assert.True(mate.Resolution.MateParameters);
    }

    [Fact]
    public void Angle30_DecodesThirtyDegreesInRadians()
    {
        using var file = SwFile.Open(VariantSample("angle30"), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal(AssemblyMateType.Angle, mate.MateType);
        Assert.Equal(6u, mate.MateTypeId);
        Assert.NotNull(mate.Angle);
        Assert.Equal(30.0 * Math.PI / 180.0, mate.Angle!.Value, 12);
        Assert.Null(mate.Distance);
        Assert.True(mate.Resolution.MateParameters);
    }

    [Theory]
    [InlineData("distance_aligned_10", FrontPlane)]
    [InlineData("distance_antialigned_10", FrontPlane)]
    [InlineData("distance_closest_10", FrontPlane)]
    [InlineData("distance_aligned_20", FrontPlane)]
    [InlineData("parallel", FrontPlane)]
    [InlineData("perpendicular", RightPlane)]
    [InlineData("angle30", RightPlane)]
    public void PlaneTypeVariants_DecodeTwoRefPlaneEntities(string name, string secondFeature)
    {
        using var file = SwFile.Open(VariantSample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal(2, mate.Entities.Count);
        AssertEntity(mate.Entities[0], "cube-1@" + name, FrontPlane);
        AssertEntity(mate.Entities[1], "cylinder-1@" + name, secondFeature);
    }

    [Fact]
    public void TangentAndConcentric_DecodeComponentFaceEntitiesWithoutIds()
    {
        // Tangent: cube planar face (moPlaneRef_w) + cylinder face (moCylinderRef_w).
        using (var file = SwFile.Open(VariantSample("tangent"), readOnly: true))
        {
            var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);
            Assert.Equal(2, mate.Entities.Count);
            AssertFaceEntity(mate.Entities[0], MateReferenceKind.Plane, "cube-1@tangent");
            AssertFaceEntity(mate.Entities[1], MateReferenceKind.Cylinder, "cylinder-1@tangent");
            Assert.True(mate.Resolution.EntityReferences);
            Assert.Null(mate.Distance);
            Assert.Null(mate.Angle);
            Assert.False(mate.Resolution.MateParameters);
        }

        // Concentric: cylinder-1 and cylinder-2 faces, both cylindrical.
        using (var file = SwFile.Open(VariantSample("concentric"), readOnly: true))
        {
            var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);
            Assert.Equal(2, mate.Entities.Count);
            AssertFaceEntity(mate.Entities[0], MateReferenceKind.Cylinder, "cylinder-1@concentric");
            AssertFaceEntity(mate.Entities[1], MateReferenceKind.Cylinder, "cylinder-2@concentric");
            Assert.True(mate.Resolution.EntityReferences);
            Assert.Null(mate.Distance);
            Assert.Null(mate.Angle);
            Assert.False(mate.Resolution.MateParameters);
        }
    }

    [Fact]
    public void TamperedTangentComponentName_LeavesEntitiesUnresolved()
    {
        byte[] stream = ReadStream("tangent");
        int component = FindWChar(stream, "cylinder-1@tangent");
        Assert.True(component > 0);
        // Break the '@' so only one component instance name remains decodable.
        int at = component + 4 + Encoding.Unicode.GetBytes("cylinder-1").Length;
        stream[at] = (byte)'#';
        stream[at + 1] = 0;

        var info = Decode(stream);
        Assert.Equal(AssemblyMateType.Tangent, info.MateType);
        Assert.Equal(AssemblyMateAlignment.Aligned, info.Alignment);
        Assert.Empty(info.Entities);
        Assert.False(info.Resolution.EntityReferences);
    }

    [Fact]
    public void AlignedAndAntiAlignedHeaders_DifferAtMeasuredAlignmentBytes()
    {
        var aligned = ReadMateHeader("distance_aligned_10");
        var anti = ReadMateHeader("distance_antialigned_10");

        // Alignment mode at header+0xA1 and the anti-only flip byte at header-1.
        Assert.Equal((byte)0x01, aligned.Stream[aligned.HeaderStart + 161]);
        Assert.Equal((byte)0x00, aligned.Stream[aligned.HeaderStart - 1]);
        Assert.Equal((byte)0x02, anti.Stream[anti.HeaderStart + 161]);
        Assert.Equal((byte)0x01, anti.Stream[anti.HeaderStart - 1]);

        // The surrounding fixed signature is identical; only the mode byte changes.
        for (int i = 150; i < 174; i++)
        {
            if (i == 161) continue;
            Assert.Equal(aligned.Stream[aligned.HeaderStart + i], anti.Stream[anti.HeaderStart + i]);
        }
    }

    [Fact]
    public void TamperedAlignmentByte_RejectsControlledHeader()
    {
        var (stream, headerStart, _) = ReadMateHeader("distance_aligned_10");
        stream[headerStart + 161] = 3;

        var info = Decode(stream);
        Assert.Equal(AssemblyMateType.Unknown, info.MateType);
        Assert.Null(info.MateTypeId);
        Assert.Null(info.Alignment);
        Assert.Empty(info.Entities);
        Assert.False(info.Resolution.MateType);
        Assert.False(info.Resolution.MateAlignment);
        Assert.False(info.Resolution.EntityReferences);
    }

    [Fact]
    public void InconsistentAlignmentFlipByte_RejectsControlledHeader()
    {
        var (stream, headerStart, _) = ReadMateHeader("distance_aligned_10");
        // Claim AntiAligned (mode 2) while leaving the anti-only flip byte at 0.
        stream[headerStart + 161] = 2;

        var info = Decode(stream);
        Assert.Equal(AssemblyMateType.Unknown, info.MateType);
        Assert.Null(info.Alignment);
        Assert.Empty(info.Entities);
        Assert.False(info.Resolution.MateType);
        Assert.False(info.Resolution.MateAlignment);
    }

    [Fact]
    public void TamperedDistanceParameter_LeavesOnlyDistanceUnresolved()
    {
        var (stream, _, _) = ReadMateHeader("distance_aligned_20");
        int d1 = FindD1Record(stream);
        Assert.True(d1 > 0);
        BitConverter.GetBytes(3.0).CopyTo(stream, d1 + 10); // fixed 2.0 metadata

        var info = Decode(stream);
        Assert.Equal(AssemblyMateType.Distance, info.MateType);
        Assert.Equal(AssemblyMateAlignment.Aligned, info.Alignment);
        Assert.Null(info.Distance);
        Assert.False(info.Resolution.MateParameters);
        Assert.True(info.Resolution.MateType);
        Assert.True(info.Resolution.MateAlignment);
        Assert.True(info.Resolution.EntityReferences);
        Assert.Equal(2, info.Entities.Count);
    }

    [Fact]
    public void DecodingVariantStream_DoesNotModifyInput()
    {
        byte[] stream = ReadStream("distance_antialigned_10");
        byte[] before = (byte[])stream.Clone();
        var info = Decode(stream);
        Assert.Equal(AssemblyMateAlignment.AntiAligned, info.Alignment);
        Assert.Equal(before, stream);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static AssemblyMateInfo Decode(byte[] stream)
    {
        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);
        return new AssemblyMateInfo(Assert.Single(model.Mates));
    }

    private static void AssertEntity(AssemblyMateEntityReference entity, string component, string feature)
    {
        Assert.Equal(MateReferenceKind.Plane, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Equal(feature, entity.FeatureName);
        Assert.Equal(feature + "@" + component, entity.SelectionName);
    }

    private static void AssertFaceEntity(AssemblyMateEntityReference entity, MateReferenceKind kind, string component)
    {
        Assert.Equal(kind, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Empty(entity.FeatureName);
        Assert.Empty(entity.SelectionName);
        Assert.Null(entity.PersistentId);
    }

    private static int FindWChar(byte[] stream, string value)
    {
        byte[] chars = Encoding.Unicode.GetBytes(value);
        var pattern = new byte[4 + chars.Length];
        pattern[0] = 0xFF;
        pattern[1] = 0xFE;
        pattern[2] = 0xFF;
        pattern[3] = (byte)value.Length;
        Buffer.BlockCopy(chars, 0, pattern, 4, chars.Length);
        for (int i = 0; i + pattern.Length <= stream.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (stream[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private static string VariantSample(string name) =>
        TestFileProvider.Resolve("Mates2026/Variants", name + ".SLDASM");

    private static byte[] ReadStream(string name)
    {
        using var file = SwFile.Open(VariantSample(name), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.Contains("MatesList", StringComparison.Ordinal)).Value;
        return (byte[])stream.Clone();
    }

    private static (byte[] Stream, int HeaderStart, int MateTag) ReadMateHeader(string name)
    {
        byte[] stream = ReadStream(name);
        var doc = MfcDocument.Parse(stream);
        var mate = doc.AllElements.Single(e =>
            e.ClassName.StartsWith("moMate", StringComparison.Ordinal) &&
            !e.ClassName.StartsWith("moMaterial", StringComparison.Ordinal));
        int p = mate.DataStart;
        while (p <= Math.Min(stream.Length, mate.DataStart + 16) - 3 && !MfcPrimitives.IsWCharPrefix(stream, p))
            p++;
        Assert.True(p <= mate.DataStart + 16);
        int cursor = p;
        Assert.NotNull(MfcPrimitives.ReadWChar(stream, ref cursor, stream.Length));
        return (stream, cursor + 2, mate.TagOffset);
    }

    private static int FindD1Record(byte[] stream)
    {
        byte[] pattern = { 0x06, 0x80, 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x31, 0x00 };
        for (int i = 0; i + pattern.Length <= stream.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (stream[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
