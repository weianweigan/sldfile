using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Public dual-mate authoring API on SldAssembly: append the natively verified
/// Coincident front/front + Distance right/right pair to a clean 0xAB MatesList
/// base. The written MatesList is byte-identical to the native dual_v1 artifact
/// for the canonical cube-1/cylinder-1 selections.
/// </summary>
public class MateGroupAuthoringApiTests
{
    private const string FrontPlane = "前视基准面";
    private const string RightPlane = "右视基准面";
    private const string TopPlane = "上视基准面";
    private const string AssemblyName = "assembly_nomate_base";
    private const string VerifiedMatesListSha256 =
        "41e21c9d9232261f4548ec45a37c30a9bb5ad1db8e26fb2f6c3079e20802d3f7";

    private static readonly string Coincident1 = FrontPlane + "@cube-1@" + AssemblyName;
    private static readonly string Coincident2 = FrontPlane + "@cylinder-1@" + AssemblyName;
    private static readonly string Distance1 = RightPlane + "@cube-1@" + AssemblyName;
    private static readonly string Distance2 = RightPlane + "@cylinder-1@" + AssemblyName;

    [Fact]
    public void AddCoincidentDistanceMates_WritesTwoMatesAndReReads()
    {
        string path = CreateTempCopy();
        try
        {
            byte[] modelHeader;
            byte[] header2;
            byte[] contentManager;
            byte[] exchange;
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                modelHeader = assembly.GetStreamBytes("Contents/Config-0-ModelHeader")!;
                header2 = assembly.GetStreamBytes("Header2")!;
                contentManager = assembly.GetStreamBytes("SwDocContentMgr/SwDocContentMgrInfo")!;
                exchange = assembly.GetStreamBytes("Contents/3DExperienceExchange2")!;

                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2);

                byte[] pending = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.Equal((ushort)0x00AB, BitConverter.ToUInt16(pending, 0));
                Assert.Equal((ushort)0, BitConverter.ToUInt16(pending, 2));
                Assert.Equal((ushort)2, BitConverter.ToUInt16(pending, 4));
                Assert.Equal(7763, pending.Length);
                // Lock the natively verified MatesList-only layout.
                Assert.Equal(VerifiedMatesListSha256,
                    Convert.ToHexString(SHA256.HashData(pending)).ToLowerInvariant());

                // The template placeholders are rewritten to the target assembly.
                Assert.False(ContainsWChar(pending, "cube-1@assembly_mate"));
                Assert.False(ContainsWChar(pending, "cylinder-1@assembly_mate"));
                Assert.True(ContainsWChar(pending, Coincident1));
                Assert.True(ContainsWChar(pending, Coincident2));
                Assert.True(ContainsWChar(pending, Distance1));
                Assert.True(ContainsWChar(pending, Distance2));

                // MatesList-only authoring: the tree streams are untouched.
                Assert.Equal(modelHeader, assembly.GetStreamBytes("Contents/Config-0-ModelHeader"));
                Assert.Equal(header2, assembly.GetStreamBytes("Header2"));
                Assert.Equal(contentManager, assembly.GetStreamBytes("SwDocContentMgr/SwDocContentMgrInfo"));
                Assert.Equal(exchange, assembly.GetStreamBytes("Contents/3DExperienceExchange2"));

                assembly.Save();

                Assert.Equal(modelHeader, assembly.GetStreamBytes("Contents/Config-0-ModelHeader"));
                Assert.Equal(header2, assembly.GetStreamBytes("Header2"));
                Assert.Equal(contentManager, assembly.GetStreamBytes("SwDocContentMgr/SwDocContentMgrInfo"));
                Assert.Equal(exchange, assembly.GetStreamBytes("Contents/3DExperienceExchange2"));
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            Assert.Equal(2, reopened.Mates.Count);

            var coincident = reopened.Mates[0];
            Assert.Equal("moMateCoincident", coincident.SerializedClassName);
            Assert.Equal(AssemblyMateType.Coincident, coincident.MateType);
            Assert.Equal(0u, coincident.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, coincident.Alignment);
            Assert.True(coincident.Resolution.MateType);
            Assert.True(coincident.Resolution.MateAlignment);
            Assert.True(coincident.Resolution.EntityReferences);
            Assert.Equal(2, coincident.Entities.Count);
            AssertEntity(coincident.Entities[0], FrontPlane, "cube-1@" + AssemblyName);
            AssertEntity(coincident.Entities[1], FrontPlane, "cylinder-1@" + AssemblyName);
            Assert.Null(coincident.Distance);

            var distance = reopened.Mates[1];
            Assert.Equal("moMateDistanceDim_c", distance.SerializedClassName);
            Assert.Equal(AssemblyMateType.Distance, distance.MateType);
            Assert.Equal(5u, distance.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, distance.Alignment);
            Assert.True(distance.Resolution.MateType);
            Assert.True(distance.Resolution.MateAlignment);
            Assert.True(distance.Resolution.EntityReferences);
            Assert.True(distance.Resolution.MateParameters);
            Assert.NotNull(distance.Distance);
            Assert.Equal(0.01, distance.Distance!.Value, 12);
            Assert.Equal(2, distance.Entities.Count);
            AssertEntity(distance.Entities[0], RightPlane, "cube-1@" + AssemblyName);
            AssertEntity(distance.Entities[1], RightPlane, "cylinder-1@" + AssemblyName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_PatchesExplicitDistance()
    {
        string path = CreateTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, 0.02);
                byte[] pending = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.Equal((ushort)2, BitConverter.ToUInt16(pending, 4));
                Assert.NotEqual(VerifiedMatesListSha256,
                    Convert.ToHexString(SHA256.HashData(pending)).ToLowerInvariant());
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            Assert.Equal(2, reopened.Mates.Count);
            Assert.Equal(0.02, reopened.Mates[1].Distance!.Value, 12);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_ReversedComponentOrderKeepsNativeSlots()
    {
        string path = CreateTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddCoincidentDistanceMates(Coincident2, Coincident1, Distance2, Distance1);
                byte[] pending = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.Equal((ushort)2, BitConverter.ToUInt16(pending, 4));
                Assert.Equal(VerifiedMatesListSha256,
                    Convert.ToHexString(SHA256.HashData(pending)).ToLowerInvariant());
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            Assert.Equal(2, reopened.Mates.Count);
            AssertEntity(reopened.Mates[0].Entities[0], FrontPlane, "cube-1@" + AssemblyName);
            AssertEntity(reopened.Mates[0].Entities[1], FrontPlane, "cylinder-1@" + AssemblyName);
            AssertEntity(reopened.Mates[1].Entities[0], RightPlane, "cube-1@" + AssemblyName);
            AssertEntity(reopened.Mates[1].Entities[1], RightPlane, "cylinder-1@" + AssemblyName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_RejectsWrongCombinationsAndKeepsStream()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();

            // Both pairs must be the verified front/right layout on cube-1/cylinder-1.
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Coincident1, Coincident2));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Distance1, Distance2, Distance1, Distance2));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(
                    Coincident1, Coincident2, TopPlane + "@cube-1@" + AssemblyName, Distance2));

            // Both mates must use one two-component pair, with no repeated component.
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident1, Distance1, Distance2));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance1));

            // Unknown components / assemblies and malformed selection names are rejected.
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(
                    Coincident1, Coincident2, Distance1,
                    RightPlane + "@ghost-1@" + AssemblyName));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(
                    Coincident1, Coincident2,
                    RightPlane + "@cube-1@other_assembly",
                    RightPlane + "@cylinder-1@other_assembly"));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(
                    Coincident1, Coincident2,
                    RightPlane + "@cube-2@" + AssemblyName,
                    RightPlane + "@cylinder-1@" + AssemblyName));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(
                    FrontPlane + "@cube-1@assembly_mate",
                    FrontPlane + "@cylinder-1@assembly_mate",
                    Distance1, Distance2));
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates("front-plane", Coincident2, Distance1, Distance2));
            Assert.Throws<ArgumentException>(() =>
                assembly.AddCoincidentDistanceMates("", Coincident2, Distance1, Distance2));
            Assert.Throws<ArgumentException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, "  "));

            Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_OnAssemblyWithExistingMate_RejectsAndKeepsStream()
    {
        string path = CreateTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddCoincidentMate(Coincident1, Coincident2);
                assembly.Save();

                byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                Assert.Throws<NotSupportedException>(() => assembly.AddCoincidentDistanceMates(
                    Coincident1, Coincident2, Distance1, Distance2));
                Assert.Equal(before, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
                Assert.Equal((ushort)1, BitConverter.ToUInt16(before, 4));
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            Assert.Single(reopened.Mates);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_CalledTwice_RejectsSecondGroup()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2);
            byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2));
            Assert.Equal(before, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_ReadOnlyAssembly_ThrowsAndDoesNotModifyFile()
    {
        string path = CreateTempCopy();
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
            {
                Assert.Throws<InvalidOperationException>(() => assembly.AddCoincidentDistanceMates(
                    Coincident1, Coincident2, Distance1, Distance2));
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
    public void AddCoincidentDistanceMates_RejectsInvalidDistance()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, -0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, double.NegativeInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2, 1001));

            Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_RejectsTamperedBaseAndKeepsStream()
    {
        string path = CreateTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            string matesPath = "Contents/Config-0-MatesList";
            byte[] original = (byte[])assembly.GetStreamBytes(matesPath)!.Clone();

            // A ModelHeader without the assembly document path/name is not a usable base.
            assembly.SetPendingStream("Contents/Config-0-ModelHeader",
                new byte[] { 0xFF, 0xFE, 0xFF, 0x01, 0x41, 0x00 });
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2));
            Assert.Equal(original, assembly.GetStreamBytes(matesPath));

            // An unknown MatesList version is rejected before any stream change.
            assembly.SetPendingStream(matesPath, new byte[] { 0xAC, 0x00, 0x00, 0x00, 0x00, 0x00 });
            byte[] tampered = (byte[])assembly.GetStreamBytes(matesPath)!.Clone();
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2));
            Assert.Equal(tampered, assembly.GetStreamBytes(matesPath));

            // A second configuration MatesList is not supported.
            assembly.SetPendingStream(matesPath, original);
            assembly.RawStreams["Contents/Config-1-MatesList"] =
                new byte[] { 0xAB, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2));
            Assert.Equal(original, assembly.GetStreamBytes(matesPath));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddCoincidentDistanceMates_OnLegacy2012Layout_ThrowsNotSupported()
    {
        string path = Path.Combine(Path.GetTempPath(), "sldfile-mate-authoring",
            "legacy_group_" + Guid.NewGuid().ToString("N") + ".SLDASM");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(TestFileProvider.Assembly, path, true);
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                Assert.Throws<NotSupportedException>(() =>
                    assembly.AddCoincidentDistanceMates(Coincident1, Coincident2, Distance1, Distance2));
            }
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void AssertEntity(
        AssemblyMateEntityReference entity,
        string feature,
        string component)
    {
        Assert.Equal(MateReferenceKind.Plane, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Equal(feature, entity.FeatureName);
        Assert.Equal(feature + "@" + component, entity.SelectionName);
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
