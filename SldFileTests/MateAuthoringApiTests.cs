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
/// Public single-mate authoring API on SldAssembly: append one Coincident or
/// Distance mate between two RefPlane selections to a clean 0xAB MatesList base.
/// </summary>
public class MateAuthoringApiTests
{
    private const string FrontPlane = "前视基准面"; // 前视基准面
    private const string TopPlane = "上视基准面"; // 上视基准面
    private const string AssemblyName = "assembly_nomate_base";
    private static readonly string Entity1 = FrontPlane + "@cube-1@" + AssemblyName;
    private static readonly string Entity2 = FrontPlane + "@cylinder-1@" + AssemblyName;

    [Fact]
    public void AddCoincidentMate_WritesOneMateAndReReads()
    {
        string path = CreateTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddCoincidentMate(Entity1, Entity2);
                assembly.Save();

                byte[] pending = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.Equal((ushort)0x00AB, BitConverter.ToUInt16(pending, 0));
                Assert.Equal((ushort)1, BitConverter.ToUInt16(pending, 4));
                Assert.True(pending.Length > 6);
                // Template placeholders are rewritten to the target names.
                Assert.False(ContainsWChar(pending, "cube-1@assembly_mate"));
                Assert.False(ContainsWChar(pending, FrontPlane + "@cube-1@assembly_mate"));
                Assert.False(ContainsWChar(pending, "cylinder-1@assembly_mate"));
                Assert.True(ContainsWChar(pending, "cube-1@" + AssemblyName));
                Assert.True(ContainsWChar(pending, FrontPlane + "@cube-1@" + AssemblyName));
                Assert.True(ContainsWChar(pending, "cylinder-1@" + AssemblyName));
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateCoincident", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Coincident, mate.MateType);
            Assert.Equal(0u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.MateAlignment);
            Assert.True(mate.Resolution.EntityReferences);

            Assert.Equal(2, mate.Entities.Count);
            AssertEntity(mate.Entities[0], "cube-1@" + AssemblyName);
            AssertEntity(mate.Entities[1], "cylinder-1@" + AssemblyName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddDistanceMate_WritesDistanceAndReReads()
    {
        string path = CreateTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddDistanceMate(Entity1, Entity2, 0.02);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateDistanceDim_c", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Distance, mate.MateType);
            Assert.Equal(5u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
            Assert.NotNull(mate.Distance);
            Assert.Equal(0.02, mate.Distance!.Value, 12);
            Assert.Equal(2, mate.Entities.Count);
            Assert.True(mate.Resolution.MateParameters);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddMate_OnAssemblyWithExistingMate_ThrowsNotSupportedAndKeepsStream()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            assembly.AddCoincidentMate(Entity1, Entity2);
            assembly.Save();

            byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
            Assert.Throws<NotSupportedException>(() => assembly.AddDistanceMate(Entity1, Entity2, 0.01));
            Assert.Equal(before, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
            Assert.Equal((ushort)1, BitConverter.ToUInt16(before, 4));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddMate_RejectsUnsupportedEntitySelections()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();

            Assert.Throws<ArgumentException>(() => assembly.AddCoincidentMate("", Entity2));
            Assert.Throws<ArgumentException>(() => assembly.AddCoincidentMate(Entity1, "  "));
            Assert.Throws<NotSupportedException>(() => assembly.AddCoincidentMate("front-plane", Entity2));
            Assert.Throws<NotSupportedException>(() => assembly.AddCoincidentMate(
                FrontPlane + "@cube-1@" + AssemblyName,
                TopPlane + "@cube-1@" + AssemblyName));
            Assert.Throws<NotSupportedException>(() => assembly.AddCoincidentMate(
                FrontPlane + "@cube-1@other_assembly",
                FrontPlane + "@cylinder-1@other_assembly"));
            Assert.Throws<NotSupportedException>(() => assembly.AddCoincidentMate(
                FrontPlane + "@ghost-1@" + AssemblyName,
                FrontPlane + "@cylinder-1@" + AssemblyName));

            Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddMate_RejectsInvalidDistance()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();

            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddDistanceMate(Entity1, Entity2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddDistanceMate(Entity1, Entity2, -0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddDistanceMate(Entity1, Entity2, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddDistanceMate(Entity1, Entity2, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddDistanceMate(Entity1, Entity2, 1001));

            Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddMate_ReadOnlyAssembly_ThrowsAndDoesNotModifyFile()
    {
        string path = CreateTempCopy();
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
            {
                Assert.Throws<InvalidOperationException>(() => assembly.AddCoincidentMate(Entity1, Entity2));
            }
            Assert.Equal(before, File.ReadAllBytes(path));

            using var reopened = new SldAssembly(path, readOnly: true);
            Assert.Empty(reopened.Mates);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddMate_OnLegacy2012Layout_ThrowsNotSupported()
    {
        string path = Path.Combine(Path.GetTempPath(), "sldfile-mate-authoring",
            "legacy_" + Guid.NewGuid().ToString("N") + ".SLDASM");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(TestFileProvider.Assembly, path, true);
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                Assert.Throws<NotSupportedException>(() =>
                    assembly.AddCoincidentMate(Entity1, Entity2));
            }
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void AssertEntity(AssemblyMateEntityReference entity, string component)
    {
        Assert.Equal(MateReferenceKind.Plane, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Equal(FrontPlane, entity.FeatureName);
        Assert.Equal(FrontPlane + "@" + component, entity.SelectionName);
    }

    private static bool ContainsWChar(byte[] data, string value)
    {
        byte[] chars = Encoding.Unicode.GetBytes(value);
        var pattern = new byte[4 + chars.Length];
        pattern[0] = 0xFF;
        pattern[1] = 0xFE;
        pattern[2] = 0xFF;
        pattern[3] = (byte)value.Length;
        Buffer.BlockCopy(chars, 0, pattern, 4, chars.Length);
        for (int i = 0; i + pattern.Length <= data.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    private static string CreateTempCopy()
    {
        string source = TestFileProvider.Resolve("Mates2026/NoMate", "assembly_nomate_base.SLDASM");
        string dir = Path.Combine(Path.GetTempPath(), "sldfile-mate-authoring");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, "assembly_nomate_base_" + Guid.NewGuid().ToString("N") + ".SLDASM");
        File.Copy(source, target, true);
        return target;
    }
}
