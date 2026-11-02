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
/// Controlled 2026 RefPlane mate samples:
///   run7 = Coincident (native IMate2.Type 0, swMateAlign_e 0),
///   run8 = Distance 10 mm (native Type 5, Alignment 0, distance 0.01 m).
/// Both use moPlaneRef_w with two moAbsoluteRefPlnData_c plane blocks carrying
/// component instance / feature@component@assembly name pairs.
/// Alignment has no varying control and must remain unresolved.
/// </summary>
public class MateControlledDecodingTests
{
    private const string FrontPlane = "前视基准面"; // 前视基准面

    [Fact]
    public void CoincidentRefPlaneMate_DecodesTypeAndTwoEntities()
    {
        using var file = SwFile.Open(Sample("run7"), readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);
        var mate = Assert.Single(assembly.Mates);

        Assert.Equal("moMateCoincident", mate.SerializedClassName);
        Assert.Equal("重合1", mate.DisplayName); // 重合1
        Assert.Equal(AssemblyMateType.Coincident, mate.MateType);
        Assert.Equal(0u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
        Assert.Null(mate.Distance);

        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.MateAlignment);
        Assert.True(mate.Resolution.EntityReferences);
        Assert.False(mate.Resolution.MateParameters);

        Assert.Equal(2, mate.Entities.Count);
        AssertEntity(mate.Entities[0], "cube-1@assembly_mate", FrontPlane);
        AssertEntity(mate.Entities[1], "cylinder-1@assembly_mate", FrontPlane);

        var wrapper = Assert.Single(mate.References);
        Assert.Equal("moPlaneRef_w", wrapper.WrapperClassName);
    }

    [Fact]
    public void DistanceRefPlaneMate_DecodesTypeValueAndTwoEntities()
    {
        using var file = SwFile.Open(Sample("run8"), readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);
        var mate = Assert.Single(assembly.Mates);

        Assert.Equal("moMateDistanceDim_c", mate.SerializedClassName);
        Assert.Equal("距离1", mate.DisplayName); // 距离1
        Assert.Equal(AssemblyMateType.Distance, mate.MateType);
        Assert.Equal(5u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
        Assert.NotNull(mate.Distance);
        Assert.Equal(0.01, mate.Distance!.Value, 12);

        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.MateAlignment);
        Assert.True(mate.Resolution.EntityReferences);
        Assert.True(mate.Resolution.MateParameters);

        Assert.Equal(2, mate.Entities.Count);
        AssertEntity(mate.Entities[0], "cylinder-1@assembly_mate", FrontPlane);
        AssertEntity(mate.Entities[1], "cube-1@assembly_mate", FrontPlane);
    }

    [Fact]
    public void LegacyOriginalAssembly_DecodesTypeAndFaceIdsButNotAlignment()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);

        // Two inline mate tags (Coincident3/Coincident4) plus two class-reference
        // records (Coincident5/6, EA 80 4D 80). Class, display, wrapper, native type
        // and Coincident4's two face ids decode; alignment, parameters and the
        // per-entity component identity remain unresolved.
        Assert.Equal(4, assembly.Mates.Count);
        Assert.Equal(
            new[] { "Coincident3", "Coincident4", "Coincident5", "Coincident6" },
            assembly.Mates.Select(m => m.DisplayName).ToArray());
        foreach (var mate in assembly.Mates)
        {
            Assert.True(mate.Resolution.MateType);
            Assert.Null(mate.Alignment);
            Assert.Null(mate.Distance);
            Assert.Null(mate.Angle);
            Assert.False(mate.Resolution.MateAlignment);
            Assert.False(mate.Resolution.MateParameters);
        }

        var coordinate = Assert.Single(assembly.Mates.Where(m => m.DisplayName == "Coincident3"));
        Assert.Equal(AssemblyMateType.Coordinate, coordinate.MateType);
        Assert.Equal(20u, coordinate.MateTypeId);
        Assert.Empty(coordinate.Entities);
        Assert.False(coordinate.Resolution.EntityReferences);

        var faceMate = Assert.Single(assembly.Mates.Where(m => m.DisplayName == "Coincident4"));
        Assert.Equal(AssemblyMateType.Coincident, faceMate.MateType);
        Assert.Equal(0u, faceMate.MateTypeId);
        Assert.Equal(2, faceMate.Entities.Count);
        Assert.Equal(
            new uint?[] { 0x192Du, 0x263u },
            faceMate.Entities.Select(e => e.PersistentId).ToArray());
        Assert.True(faceMate.Resolution.EntityReferences);

        var classRefMates = assembly.Mates
            .Where(m => m.DisplayName == "Coincident5" || m.DisplayName == "Coincident6")
            .ToArray();
        Assert.All(classRefMates, m =>
        {
            Assert.Equal(AssemblyMateType.Coincident, m.MateType);
            Assert.Equal(0u, m.MateTypeId);
            Assert.Empty(m.Entities);
            Assert.False(m.Resolution.EntityReferences);
        });
    }

    [Fact]
    public void DeferredFace2026Layout_DoesNotClaimPlaneMateFields()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly2026, readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);

        Assert.Equal(4, assembly.Mates.Count);
        // Inline Coincident3 (point) and Coincident4 (deferred face) stay unresolved.
        var inlineMates = assembly.Mates
            .Where(m => m.DisplayName == "Coincident3" || m.DisplayName == "Coincident4")
            .ToArray();
        Assert.Equal(2, inlineMates.Length);
        foreach (var mate in inlineMates)
        {
            Assert.Equal(AssemblyMateType.Unknown, mate.MateType);
            Assert.Null(mate.MateTypeId);
            Assert.Null(mate.Alignment);
            Assert.Null(mate.Distance);
            Assert.Empty(mate.Entities);
            Assert.False(mate.Resolution.MateType);
            Assert.False(mate.Resolution.MateAlignment);
            Assert.False(mate.Resolution.EntityReferences);
            Assert.False(mate.Resolution.MateParameters);
        }

        // Class-reference Coincident5/6 have two RefPlane pairs: type and entities
        // decode. Coincident6 carries the controlled header signature; Coincident5
        // uses a deferred header variant and must not claim alignment.
        var classRefMates = assembly.Mates
            .Where(m => m.DisplayName == "Coincident5" || m.DisplayName == "Coincident6")
            .ToArray();
        Assert.Equal(2, classRefMates.Length);
        foreach (var mate in classRefMates)
        {
            Assert.Equal("moMateCoincident", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Coincident, mate.MateType);
            Assert.Equal(0u, mate.MateTypeId);
            Assert.Equal(2, mate.Entities.Count);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.False(mate.Resolution.MateParameters);
        }
        Assert.Null(Assert.Single(classRefMates.Where(m => m.DisplayName == "Coincident5")).Alignment);
        Assert.False(Assert.Single(classRefMates.Where(m => m.DisplayName == "Coincident5")).Resolution.MateAlignment);
        Assert.Equal(
            AssemblyMateAlignment.Aligned,
            Assert.Single(classRefMates.Where(m => m.DisplayName == "Coincident6")).Alignment);
        Assert.True(Assert.Single(classRefMates.Where(m => m.DisplayName == "Coincident6")).Resolution.MateAlignment);
    }

    [Fact]
    public void TamperedPlaneSelectionName_LeavesEntitiesUnresolvedButKeepsHeaderFields()
    {
        byte[] stream = ReadMatesStream("run7");
        int mateTag = FindMateTagOffset(stream, "moMateCoincident");
        int planeName = FindWChar(stream, FrontPlane + "@cube-1@assembly_mate", mateTag);
        Assert.True(planeName > mateTag);

        // Break the first '@' of the selection name so the feature@component suffix
        // no longer matches the component instance name.
        int at = planeName + 4 + Encoding.Unicode.GetBytes(FrontPlane).Length;
        stream[at] = (byte)'#';
        stream[at + 1] = 0;

        var model = new MatesListData();
        MatesListSchemaMapper.Map(stream, model);
        var entry = Assert.Single(model.Mates);
        Assert.NotNull(entry.Structure);

        var info = new AssemblyMateInfo(entry);
        // The controlled header is independent of the entity names.
        Assert.Equal(AssemblyMateType.Coincident, info.MateType);
        Assert.Equal(0u, info.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, info.Alignment);
        Assert.True(info.Resolution.MateType);
        Assert.True(info.Resolution.MateAlignment);
        // The broken selection pair prevents any entity claim.
        Assert.Empty(info.Entities);
        Assert.False(info.Resolution.EntityReferences);
        Assert.Null(info.Distance);
        Assert.False(info.Resolution.MateParameters);
    }

    [Fact]
    public void TamperedDistanceParameter_LeavesDistanceUnresolved()
    {
        byte[] stream = ReadMatesStream("run8");
        int d1 = FindD1Record(stream);
        Assert.True(d1 > 0);

        // Corrupt the fixed 2.0 metadata word of the D1 record (offset +10).
        BitConverter.GetBytes(3.0).CopyTo(stream, d1 + 10);

        var model = new MatesListData();
        MatesListSchemaMapper.Map(stream, model);
        var entry = Assert.Single(model.Mates);
        var info = new AssemblyMateInfo(entry);

        Assert.Equal(AssemblyMateType.Distance, info.MateType);
        Assert.Equal(5u, info.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, info.Alignment);
        Assert.Null(info.Distance);
        Assert.False(info.Resolution.MateParameters);
        Assert.True(info.Resolution.MateType);
        Assert.True(info.Resolution.MateAlignment);
        Assert.True(info.Resolution.EntityReferences);
        Assert.Equal(2, info.Entities.Count);
    }

    [Fact]
    public void DecodingControlledMateStream_DoesNotModifyInput()
    {
        byte[] stream = ReadMatesStream("run7");
        byte[] before = (byte[])stream.Clone();

        var doc = MfcDocument.Parse(stream);
        var mate = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moMateCoincident"));
        Assert.NotNull(MateStructureDecoder.Decode(mate));

        var model = new MatesListData();
        MatesListSchemaMapper.Map(stream, model);

        Assert.Equal(before, stream);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void AssertEntity(AssemblyMateEntityReference entity, string component, string feature)
    {
        Assert.Equal(MateReferenceKind.Plane, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Equal(feature, entity.FeatureName);
        Assert.Equal(feature + "@" + component, entity.SelectionName);
    }

    private static string Sample(string run) =>
        TestFileProvider.Resolve(Path.Combine("Mates2026", "Controlled", run), "assembly_mate.SLDASM");

    private static byte[] ReadMatesStream(string run)
    {
        using var file = SwFile.Open(Sample(run), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.Contains("MatesList", StringComparison.Ordinal)).Value;
        return (byte[])stream.Clone();
    }

    private static int FindMateTagOffset(byte[] stream, string className)
    {
        byte[] name = Encoding.ASCII.GetBytes(className);
        for (int i = 0; i + 6 + name.Length <= stream.Length; i++)
        {
            if (stream[i] != 0xFF || stream[i + 1] != 0xFF || stream[i + 2] != 0x01 || stream[i + 3] != 0x00)
                continue;
            if (BitConverter.ToUInt16(stream, i + 4) != name.Length) continue;
            bool match = true;
            for (int j = 0; j < name.Length; j++)
            {
                if (stream[i + 6 + j] != name[j]) { match = false; break; }
            }
            if (match) return i;
        }
        throw new InvalidOperationException($"MFC tag '{className}' not found");
    }

    private static int FindWChar(byte[] stream, string value, int start)
    {
        byte[] chars = Encoding.Unicode.GetBytes(value);
        var pattern = new byte[4 + chars.Length];
        pattern[0] = 0xFF;
        pattern[1] = 0xFE;
        pattern[2] = 0xFF;
        pattern[3] = (byte)value.Length;
        Buffer.BlockCopy(chars, 0, pattern, 4, chars.Length);
        for (int i = Math.Max(0, start); i + pattern.Length <= stream.Length; i++)
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
