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
/// Mate records serialized with MFC class references instead of inline class tags:
///   2026: C1 80 06 80 ... -> moMateCoincident (run6 Coincident5/6/重合1,
///         TestFiles/Solidworks2026 Coincident5/6);
///   2012: EA 80 4D 80 ... -> moMateCoincidentM (TestFiles/Solidworks2012/assembly.SLDASM 5/6).
/// </summary>
public class MateClassRefTests
{
    [Fact]
    public void Run6_ClassReferenceCoincidentMates_AreRecovered()
    {
        using var file = SwFile.Open(Run6Sample(), readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);

        Assert.Equal(6, assembly.Mates.Count);
        Assert.Equal(
            new[] { "Coincident3", "Coincident4", "Coincident5", "Coincident6", "重合1", "距离1" },
            assembly.Mates.Select(m => m.DisplayName).ToArray());

        var classRefMates = assembly.Mates
            .Where(m => m.DisplayName == "Coincident5" || m.DisplayName == "Coincident6" ||
                        m.DisplayName == "重合1")
            .ToArray();
        Assert.Equal(3, classRefMates.Length);
        foreach (var mate in classRefMates)
        {
            Assert.Equal("moMateCoincident", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Coincident, mate.MateType);
            Assert.Equal(0u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
            Assert.Equal(2, mate.Entities.Count);
            Assert.Equal("cube-1@assembly_mate", mate.Entities[0].ComponentInstanceName);
            Assert.Equal("cylinder-1@assembly_mate", mate.Entities[1].ComponentInstanceName);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.MateAlignment);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.False(mate.Resolution.MateParameters);
        }

        // The inline deferred-face Coincident4 and the old-layout distance mate are
        // outside the controlled signature/layout and stay unresolved.
        var coincident4 = Assert.Single(assembly.Mates.Where(m => m.DisplayName == "Coincident4"));
        Assert.Equal(AssemblyMateType.Unknown, coincident4.MateType);
        var distance = Assert.Single(assembly.Mates.Where(m => m.DisplayName == "距离1"));
        Assert.Equal(AssemblyMateType.Unknown, distance.MateType);
    }

    [Fact]
    public void TamperedClassRefCode_HidesOnlyThatRecord()
    {
        byte[] stream = ReadMatesStream(TestFileProvider.Assembly2026);
        int classRef = FindBytes(stream, new byte[] { 0xC1, 0x80, 0x06, 0x80, 0xFF, 0xFE, 0xFF });
        Assert.True(classRef > 0);
        stream[classRef] = 0xC2; // unknown class reference

        var model = new MatesListData();
        MatesListSchemaMapper.Map(stream, model);

        Assert.Equal(3, model.Mates.Count);
        Assert.DoesNotContain(model.Mates, m => m.DisplayName == "Coincident5");
        Assert.Contains(model.Mates, m => m.DisplayName == "Coincident6");
    }

    [Fact]
    public void Legacy2012_ClassReferenceMates_AreRecoveredWithoutTypeClaims()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);

        var recovered = assembly.Mates
            .Where(m => m.DisplayName == "Coincident5" || m.DisplayName == "Coincident6")
            .ToArray();
        Assert.Equal(2, recovered.Length);
        foreach (var mate in recovered)
        {
            Assert.Equal("moMateCoincidentM", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Coincident, mate.MateType);
            Assert.Equal(0u, mate.MateTypeId);
            Assert.Null(mate.Alignment);
            Assert.Empty(mate.Entities);
            Assert.True(mate.Resolution.ClassName);
            Assert.True(mate.Resolution.DisplayName);
            Assert.True(mate.Resolution.ReferenceStructure);
            Assert.True(mate.Resolution.MateType);
            Assert.False(mate.Resolution.MateAlignment);
            Assert.False(mate.Resolution.EntityReferences);
        }
    }

    [Fact]
    public void AppendedClassRefDistanceRecord_ParsesWithSingleInlinePlaneTag()
    {
        // v4 pattern: first mate uses inline class tags (run7 Coincident), the
        // second mate reuses the shared plane/chain classes through stable MFC
        // class references (run6's native Distance record has no inline
        // moPlaneRef_w tag). Only the mate class (moMateDistanceDim_c) is inline.
        byte[] run7 = ReadMatesStream(Run7Sample());
        byte[] run6 = ReadMatesStream(Run6Sample());

        byte[] header = new byte[6];
        Array.Copy(run7, header, 6);
        BitConverter.GetBytes((ushort)2).CopyTo(header, 4);

        byte[] first = run7.Skip(6).ToArray();
        byte[] distanceName = Encoding.ASCII.GetBytes("moMateDistanceDim_c");
        int namePos = FindBytes(run6, distanceName);
        Assert.True(namePos >= 6);
        int recordStart = namePos - 6; // MFC tag: FFFF 0100 len(2) + name
        byte[] second = run6.Skip(recordStart).ToArray();

        byte[] v4 = header.Concat(first).Concat(second).ToArray();
        Assert.Equal(2, BitConverter.ToUInt16(v4, 4));
        // The shared wrapper class is defined exactly once (first record).
        Assert.Equal(1, CountOccurrences(v4, Encoding.ASCII.GetBytes("moPlaneRef_w")));

        var model = new MatesListData();
        MatesListSchemaMapper.Map(v4, model);

        Assert.Equal(2, model.Mates.Count);
        Assert.Equal("moMateCoincident", model.Mates[0].ClassName);
        Assert.Equal("moMateDistanceDim_c", model.Mates[1].ClassName);

        var coincident = new AssemblyMateInfo(model.Mates[0]);
        Assert.Equal(AssemblyMateType.Coincident, coincident.MateType);
        // The window is bounded at the next inline mate tag, so the first record's
        // selection strings are not contaminated by the second record.
        Assert.Equal(2, coincident.Entities.Count);

        var distance = new AssemblyMateInfo(model.Mates[1]);
        Assert.Equal("moMateDistanceDim_c", distance.SerializedClassName);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static int CountOccurrences(byte[] data, byte[] pattern)
    {
        int count = 0;
        for (int i = 0; i + pattern.Length <= data.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }

    private static string Run7Sample() =>
        TestFileProvider.Resolve("Mates2026/Controlled/run7", "assembly_mate.SLDASM");

    private static string Run6Sample() =>
        TestFileProvider.Resolve("Mates2026/Controlled/run6", "assembly_mate.SLDASM");

    private static byte[] ReadMatesStream(string assemblyPath)
    {
        using var file = SwFile.Open(assemblyPath, readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.Contains("MatesList", StringComparison.Ordinal)).Value;
        return (byte[])stream.Clone();
    }

    private static int FindBytes(byte[] stream, byte[] pattern)
    {
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
