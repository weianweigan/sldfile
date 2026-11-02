using System;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Binary;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Task 6.2 advanced mate authoring APIs. Each API targets the matching clean
/// advanced-mate base (MatesList emptied to the 0xAB 6-byte header; the sample's
/// ModelHeader/ContentMgr template with the mate display name is retained).
/// </summary>
public class MateAdvancedAuthoringApiTests
{
    private const string FrontPlane = "前视基准面";
    private const string RightPlane = "右视基准面";

    [Fact]
    public void AddGearMate_WritesRatioAndReReads()
    {
        string path = CreateEmptySampleBase("gear_2to1");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddGearMate("cylinder-3@gear_2to1", "cylinder-2@gear_2to1", 2.0, 3.0);
                assembly.Save();
                byte[] stream = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.Equal((ushort)0x00BF, BitConverter.ToUInt16(stream, 0));
                Assert.Equal((ushort)1, BitConverter.ToUInt16(stream, 4));
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateGearDim_c", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Gear, mate.MateType);
            Assert.Equal(10u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Closest, mate.Alignment);
            Assert.Equal(2.0, mate.GearRatioNumerator!.Value, 12);
            Assert.Equal(3.0, mate.GearRatioDenominator!.Value, 12);
            Assert.True(mate.Resolution.MateParameters);
            Assert.Equal(2, mate.Entities.Count);
            AssertFaceEntity(mate.Entities[0], "cylinder-3@gear_2to1");
            AssertFaceEntity(mate.Entities[1], "cylinder-2@gear_2to1");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddScrewMate_WritesRevolutionValueAndReReads()
    {
        string path = CreateEmptySampleBase("screw_5mm");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddScrewMate("cylinder-1@screw_5mm", "cylinder-2@screw_5mm", 2.0);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateScrew_c", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.Screw, mate.MateType);
            Assert.Equal(17u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
            Assert.Equal(2.0, mate.ScrewRevolutionsPerUnit!.Value, 12);
            Assert.Equal(2, mate.Entities.Count);
            AssertFaceEntity(mate.Entities[0], "cylinder-1@screw_5mm");
            AssertFaceEntity(mate.Entities[1], "cylinder-2@screw_5mm");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddUniversalJointMate_WritesMeasuredRecordAndReReads()
    {
        string path = CreateEmptySampleBase("universal_joint");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddUniversalJointMate("cylinder-3@universal_joint", "cylinder-1@universal_joint");
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateUniversalJoint_c", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.UniversalJoint, mate.MateType);
            Assert.Equal(19u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Closest, mate.Alignment);
            Assert.Empty(mate.Entities);
            Assert.False(mate.Resolution.MateParameters);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddLimitDistanceMate_WritesDistanceAndReReads()
    {
        string path = CreateEmptySampleBase("limit_distance_10");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddLimitDistanceMate(
                    FrontPlane + "@cube-1@limit_distance_10",
                    FrontPlane + "@cylinder-1@limit_distance_10",
                    0.02);
                assembly.Save();
                byte[] stream = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.Equal((ushort)0x00AB, BitConverter.ToUInt16(stream, 0));
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateLimitDistanceDim_c", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.LimitDistance, mate.MateType);
            Assert.Equal(5u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
            Assert.Equal(0.02, mate.Distance!.Value, 12);
            Assert.Equal(2, mate.Entities.Count);
            AssertPlaneEntity(mate.Entities[0], "cube-1@limit_distance_10", FrontPlane);
            AssertPlaneEntity(mate.Entities[1], "cylinder-1@limit_distance_10", FrontPlane);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AddLimitAngleMate_WritesAngleAndReReads()
    {
        string path = CreateEmptySampleBase("limit_angle_30");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddLimitAngleMate(
                    FrontPlane + "@cube-1@limit_angle_30",
                    RightPlane + "@cylinder-1@limit_angle_30",
                    60.0 * Math.PI / 180.0);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates);
            Assert.Equal("moMateLimitPlanarAngleDim_c", mate.SerializedClassName);
            Assert.Equal(AssemblyMateType.LimitAngle, mate.MateType);
            Assert.Equal(6u, mate.MateTypeId);
            Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
            Assert.Equal(60.0 * Math.PI / 180.0, mate.Angle!.Value, 12);
            Assert.Equal(2, mate.Entities.Count);
            AssertPlaneEntity(mate.Entities[0], "cube-1@limit_angle_30", FrontPlane);
            AssertPlaneEntity(mate.Entities[1], "cylinder-1@limit_angle_30", RightPlane);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AdvancedAddMate_OnBaseWithoutMatchingModelHeaderTemplate_ThrowsNotSupported()
    {
        // The plain no-mate base has no Gear ModelHeader mate node.
        string path = CreateTempCopy(TestFileProvider.Resolve(
            "Mates2026/NoMate", "assembly_nomate_base.SLDASM"));
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddGearMate("cube-1@assembly_nomate_base", "cylinder-1@assembly_nomate_base", 1, 2));
            Assert.Equal(before, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AdvancedAddMate_OnAssemblyWithExistingMate_ThrowsNotSupported()
    {
        string path = CreateTempCopy(Sample("gear_2to1"));
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
            Assert.Throws<NotSupportedException>(() =>
                assembly.AddGearMate("cylinder-3@gear_2to1", "cylinder-2@gear_2to1", 1, 2));
            Assert.Equal(before, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AdvancedAddMate_RejectsInvalidParametersAndEntities()
    {
        string gear = CreateEmptySampleBase("gear_2to1");
        string screw = CreateEmptySampleBase("screw_5mm");
        string limitDistance = CreateEmptySampleBase("limit_distance_10");
        string limitAngle = CreateEmptySampleBase("limit_angle_30");
        try
        {
            using (var assembly = new SldAssembly(gear, readOnly: false))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    assembly.AddGearMate("cylinder-3@gear_2to1", "cylinder-2@gear_2to1", 0, 2));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    assembly.AddGearMate("cylinder-3@gear_2to1", "cylinder-2@gear_2to1", 1, double.NaN));
                Assert.Throws<NotSupportedException>(() =>
                    assembly.AddGearMate("cylinder-3@gear_2to1", "cylinder-3@gear_2to1", 1, 2));
                Assert.Throws<NotSupportedException>(() =>
                    assembly.AddGearMate("ghost-1@gear_2to1", "cylinder-2@gear_2to1", 1, 2));
                Assert.Throws<NotSupportedException>(() =>
                    assembly.AddGearMate("cylinder-3", "cylinder-2@gear_2to1", 1, 2));
            }
            using (var assembly = new SldAssembly(screw, readOnly: false))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    assembly.AddScrewMate("cylinder-1@screw_5mm", "cylinder-2@screw_5mm", -1));
            }
            using (var assembly = new SldAssembly(limitDistance, readOnly: false))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    assembly.AddLimitDistanceMate(
                        FrontPlane + "@cube-1@limit_distance_10",
                        FrontPlane + "@cylinder-1@limit_distance_10", 0));
            }
            using (var assembly = new SldAssembly(limitAngle, readOnly: false))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    assembly.AddLimitAngleMate(
                        FrontPlane + "@cube-1@limit_angle_30",
                        RightPlane + "@cylinder-1@limit_angle_30", 2 * Math.PI));
            }
        }
        finally
        {
            File.Delete(gear);
            File.Delete(screw);
            File.Delete(limitDistance);
            File.Delete(limitAngle);
        }
    }

    [Fact]
    public void AdvancedAddMate_ReadOnly_ThrowsAndDoesNotModifyFile()
    {
        string path = CreateEmptySampleBase("limit_distance_10");
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
            {
                Assert.Throws<InvalidOperationException>(() =>
                    assembly.AddLimitDistanceMate(
                        FrontPlane + "@cube-1@limit_distance_10",
                        FrontPlane + "@cylinder-1@limit_distance_10", 0.01));
            }
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally { File.Delete(path); }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void AssertFaceEntity(AssemblyMateEntityReference entity, string component)
    {
        Assert.Equal(MateReferenceKind.Cylinder, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Empty(entity.FeatureName);
    }

    private static void AssertPlaneEntity(AssemblyMateEntityReference entity, string component, string feature)
    {
        Assert.Equal(MateReferenceKind.Plane, entity.Kind);
        Assert.Equal(component, entity.ComponentInstanceName);
        Assert.Equal(feature, entity.FeatureName);
        Assert.Equal(feature + "@" + component, entity.SelectionName);
    }

    /// <summary>
    /// Copies the advanced specimen and replaces its MatesList with the clean
    /// 0xAB 6-byte header, keeping the specimen's ModelHeader/ContentMgr templates
    /// (including the mate display-name node).
    /// </summary>
    private static string CreateEmptySampleBase(string sampleName)
    {
        string path = CreateTempCopy(Sample(sampleName));
        using (var assembly = new SldAssembly(path, readOnly: false))
        {
            assembly.SetPendingStream("Contents/Config-0-MatesList",
                new byte[] { 0xAB, 0x00, 0x00, 0x00, 0x00, 0x00 });
            assembly.Save();
        }
        return path;
    }

    private static string CreateTempCopy(string source)
    {
        string dir = Path.Combine(Path.GetTempPath(), "sldfile-mate-authoring-advanced");
        Directory.CreateDirectory(dir);
        string target = Path.Combine(dir, Path.GetFileNameWithoutExtension(source) + "_" +
            Guid.NewGuid().ToString("N") + ".SLDASM");
        File.Copy(source, target, true);
        return target;
    }

    private static string Sample(string name) =>
        TestFileProvider.Resolve("Mates2026/Advanced", name + ".SLDASM");
}
