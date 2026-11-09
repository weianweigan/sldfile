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
/// Task 6.2 advanced mate samples (agent5-advanced-mates3-20261030):
/// LimitDistance (native type 5), LimitAngle (6), Gear (10, ratio 1/2),
/// Screw (17, revolution value 1) and UniversalJoint (19). Width/RackPinion/Path
/// samples are blocked and contain no mate records.
/// </summary>
public class MateAdvancedDecodingTests
{
    private const string FrontPlane = "前视基准面";
    private const string RightPlane = "右视基准面";

    [Theory]
    [InlineData("limit_distance_10")]
    [InlineData("limit_distance_10_resave")]
    public void LimitDistance_DecodesTypeDistanceAndPlaneEntities(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal("moMateLimitDistanceDim_c", mate.SerializedClassName);
        Assert.Equal(AssemblyMateType.LimitDistance, mate.MateType);
        Assert.Equal(5u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
        Assert.Equal(0.01, mate.Distance!.Value, 12);
        Assert.Null(mate.Angle);
        Assert.True(mate.Resolution.MateParameters);
        Assert.Equal(2, mate.Entities.Count);
        AssertEntity(mate.Entities[0], "cube-1@" + AssemblyInstance(name), FrontPlane);
        AssertEntity(mate.Entities[1], "cylinder-1@" + AssemblyInstance(name), FrontPlane);
    }

    [Theory]
    [InlineData("limit_angle_30")]
    [InlineData("limit_angle_30_resave")]
    public void LimitAngle_DecodesTypeAngleAndPlaneEntities(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal("moMateLimitPlanarAngleDim_c", mate.SerializedClassName);
        Assert.Equal(AssemblyMateType.LimitAngle, mate.MateType);
        Assert.Equal(6u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
        Assert.Equal(30.0 * Math.PI / 180.0, mate.Angle!.Value, 12);
        Assert.Null(mate.Distance);
        Assert.True(mate.Resolution.MateParameters);
        Assert.Equal(2, mate.Entities.Count);
        AssertEntity(mate.Entities[0], "cube-1@" + AssemblyInstance(name), FrontPlane);
        AssertEntity(mate.Entities[1], "cylinder-1@" + AssemblyInstance(name), RightPlane);
    }

    [Theory]
    [InlineData("gear_2to1")]
    [InlineData("gear_2to1_resave")]
    public void Gear_DecodesTypeRatioAndCylinderEntities(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal("moMateGearDim_c", mate.SerializedClassName);
        Assert.Equal(AssemblyMateType.Gear, mate.MateType);
        Assert.Equal(10u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Closest, mate.Alignment);
        Assert.Equal(1.0, mate.GearRatioNumerator!.Value, 12);
        Assert.Equal(2.0, mate.GearRatioDenominator!.Value, 12);
        Assert.True(mate.Resolution.MateParameters);
        Assert.Equal(2, mate.Entities.Count);
        AssertFaceEntity(mate.Entities[0], MateReferenceKind.Cylinder, "cylinder-3@" + AssemblyInstance(name));
        AssertFaceEntity(mate.Entities[1], MateReferenceKind.Cylinder, "cylinder-2@" + AssemblyInstance(name));
    }

    [Theory]
    [InlineData("screw_5mm")]
    [InlineData("screw_5mm_resave")]
    public void Screw_DecodesTypeRevolutionValueAndCylinderEntities(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal("moMateScrew_c", mate.SerializedClassName);
        Assert.Equal(AssemblyMateType.Screw, mate.MateType);
        Assert.Equal(17u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Aligned, mate.Alignment);
        Assert.Equal(1.0, mate.ScrewRevolutionsPerUnit!.Value, 12);
        Assert.True(mate.Resolution.MateParameters);
        Assert.Equal(2, mate.Entities.Count);
        AssertFaceEntity(mate.Entities[0], MateReferenceKind.Cylinder, "cylinder-1@" + AssemblyInstance(name));
        AssertFaceEntity(mate.Entities[1], MateReferenceKind.Cylinder, "cylinder-2@" + AssemblyInstance(name));
    }

    [Theory]
    [InlineData("universal_joint")]
    [InlineData("universal_joint_resave")]
    public void UniversalJoint_DecodesTypeAndClosestAlignment(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var mate = Assert.Single(Assert.IsType<SldAssembly>(file).Mates);

        Assert.Equal("moMateUniversalJoint_c", mate.SerializedClassName);
        Assert.Equal(AssemblyMateType.UniversalJoint, mate.MateType);
        Assert.Equal(19u, mate.MateTypeId);
        Assert.Equal(AssemblyMateAlignment.Closest, mate.Alignment);
        // No component instance-name strings are serialized for this specimen.
        Assert.Empty(mate.Entities);
        Assert.False(mate.Resolution.EntityReferences);
        Assert.False(mate.Resolution.MateParameters);
    }

    [Theory]
    [InlineData("assembly_mate")]
    [InlineData("assembly_mate_resave")]
    public void LegalDualMateSample_DecodesBothRecords(string name)
    {
        using var file = SwFile.Open(TestFileProvider.Resolve(
            "Mates2026/Dual", name + ".SLDASM"), readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);
        Assert.Equal(2, assembly.Mates.Count);

        var coincident = assembly.Mates[0];
        Assert.Equal("moMateCoincident", coincident.SerializedClassName);
        Assert.Equal(AssemblyMateType.Coincident, coincident.MateType);
        Assert.Equal(AssemblyMateAlignment.Aligned, coincident.Alignment);
        Assert.Equal(2, coincident.Entities.Count);
        AssertEntity(coincident.Entities[0], "cube-1@assembly_mate", FrontPlane);
        AssertEntity(coincident.Entities[1], "cylinder-1@assembly_mate", FrontPlane);

        var distance = assembly.Mates[1];
        Assert.Equal("moMateDistanceDim_c", distance.SerializedClassName);
        Assert.Equal(AssemblyMateType.Distance, distance.MateType);
        Assert.Equal(AssemblyMateAlignment.Aligned, distance.Alignment);
        Assert.Equal(0.01, distance.Distance!.Value, 12);
        Assert.Equal(2, distance.Entities.Count);
        AssertEntity(distance.Entities[0], "cube-1@assembly_mate", RightPlane);
        AssertEntity(distance.Entities[1], "cylinder-1@assembly_mate", RightPlane);
    }

    [Theory]
    [InlineData("width_mate")]
    [InlineData("rack_pinion")]
    [InlineData("path_mate")]
    public void BlockedAdvancedSamples_HaveNoMateRecords(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var assembly = Assert.IsType<SldAssembly>(file);
        Assert.Empty(assembly.Mates);
    }

    [Fact]
    public void TamperedUniversalJointAlignment_LeavesTypeUnresolved()
    {
        byte[] stream = ReadMatesStream("universal_joint");
        var doc = MfcDocument.Parse(stream);
        var mate = Assert.Single(doc.AllElements.Where(e => e.ClassName == "moMateUniversalJoint_c"));
        int p = mate.DataStart;
        while (p <= mate.DataStart + 16 && !MfcPrimitives.IsWCharPrefix(stream, p)) p++;
        int cursor = p;
        Assert.NotNull(MfcPrimitives.ReadWChar(stream, ref cursor));
        int headerStart = cursor + 2;
        stream[headerStart + 161] = 1; // non-Closest mode is not controlled

        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);
        var info = new AssemblyMateInfo(Assert.Single(model.Mates));
        Assert.Equal(AssemblyMateType.Unknown, info.MateType);
        Assert.Null(info.MateTypeId);
        Assert.Null(info.Alignment);
        Assert.False(info.Resolution.MateType);
    }

    [Fact]
    public void TamperedGearDenominator_LeavesOnlyRatioUnresolved()
    {
        byte[] stream = ReadMatesStream("gear_2to1");
        byte[] pattern = { 0x06, 0x80, 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x32, 0x00 };
        int d2 = FindBytes(stream, pattern);
        Assert.True(d2 > 0);
        BitConverter.GetBytes(0.0).CopyTo(stream, d2 + 32);

        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);
        var mate = new AssemblyMateInfo(Assert.Single(model.Mates));
        Assert.Equal(AssemblyMateType.Gear, mate.MateType);
        Assert.Equal(1.0, mate.GearRatioNumerator!.Value, 12);
        Assert.Null(mate.GearRatioDenominator);
    }

    [Fact]
    public void DecodingAdvancedMateStream_DoesNotModifyInput()
    {
        byte[] stream = ReadMatesStream("screw_5mm");
        byte[] before = (byte[])stream.Clone();
        var model = new MatesListModel();
        MfcArchive.DeserializeInto(stream, model);
        Assert.Single(model.Mates);
        Assert.Equal(before, stream);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string AssemblyInstance(string name) =>
        name.EndsWith("_resave", StringComparison.Ordinal)
            ? name.Substring(0, name.Length - "_resave".Length)
            : name;

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
        Assert.Null(entity.PersistentId);
    }

    private static byte[] ReadMatesStream(string name)
    {
        using var file = SwFile.Open(Sample(name), readOnly: true);
        var stream = file.RawStreams.First(k => k.Key.Contains("MatesList", StringComparison.Ordinal)).Value;
        return (byte[])stream.Clone();
    }

    private static int FindBytes(byte[] data, byte[] pattern)
    {
        for (int i = 0; i + pattern.Length <= data.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    private static string Sample(string name) =>
        TestFileProvider.Resolve("Mates2026/Advanced", name + ".SLDASM");
}
