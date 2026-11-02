using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Measured tutorial mechanical/advanced mates: Hinge, CamTangent/CamCoincident,
/// PlanarAngle, LimitAngle, LimitDistance and the assembly-specific InPlace
/// record. Values are exposed only where the measured main/resave shape supports
/// them; upper/lower limits and Hinge angle stay unresolved.
/// </summary>
public class MateTutorialMechanicalTests
{
    [Theory]
    [InlineData("Hinge/chain_link1.SLDASM")]
    [InlineData("Hinge/chain_link1_resave.SLDASM")]
    public void Hinge_FourMates_Decode(string relativePath)
    {
        using var assembly = new SldAssembly(SamplePath(relativePath), readOnly: true);
        var hinges = assembly.Mates.Where(m => m.MateType == AssemblyMateType.Hinge).ToArray();
        Assert.Equal(4, hinges.Length);
        foreach (var mate in hinges)
        {
            Assert.Equal("moMateHinge", mate.SerializedClassName);
            Assert.Equal(22u, mate.MateTypeId);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.False(mate.Resolution.MateParameters);
            Assert.Equal(4, mate.Entities.Count);
            Assert.Contains(mate.Entities, e => e.ComponentInstanceName.Contains("pin"));
            Assert.Contains(mate.Entities, e => e.ComponentInstanceName.Contains("clip"));
        }
    }

    [Theory]
    [InlineData("Cam/cam-follower.SLDASM")]
    [InlineData("Cam/cam-follower_resave.SLDASM")]
    public void CamTangentAndCoincident_Decode(string relativePath)
    {
        using var assembly = new SldAssembly(SamplePath(relativePath), readOnly: true);
        var cams = assembly.Mates.Where(m => m.MateType == AssemblyMateType.Cam).ToArray();
        Assert.Equal(2, cams.Length);
        Assert.Contains(cams, m => m.SerializedClassName == "moMateCamTangent");
        Assert.Contains(cams, m => m.SerializedClassName == "moMateCamCoincident");
        Assert.All(cams, mate =>
        {
            Assert.Equal(9u, mate.MateTypeId);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.False(mate.Resolution.MateParameters);
            Assert.Equal(2, mate.Entities.Count);
            Assert.All(mate.Entities, e => Assert.False(string.IsNullOrEmpty(e.ComponentInstanceName)));
        });
    }

    [Theory]
    [InlineData("Planar/key_pad_1.SLDASM")]
    [InlineData("Planar/key_pad_1_resave.SLDASM")]
    public void PlanarAngle_TwoMates_DecodeAnglesAndEntities(string path)
    {
        using var assembly = new SldAssembly(SamplePath(path), readOnly: true);
        var angles = assembly.Mates.Where(m => m.MateType == AssemblyMateType.PlanarAngle).ToArray();
        Assert.Equal(2, angles.Length);
        Assert.Equal(new[] { 3.4033920413889502, 6.02138591938045 },
            angles.Select(a => a.Angle!.Value).OrderBy(v => v).ToArray(), new DoubleComparer(10));
        Assert.All(angles, mate =>
        {
            Assert.Equal("moMatePlanarAngleDim_c", mate.SerializedClassName);
            Assert.Equal(6u, mate.MateTypeId);
            Assert.True(mate.Resolution.MateType);
            Assert.True(mate.Resolution.MateParameters);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.InRange(mate.Entities.Count, 1, 2);
        });
    }

    [Theory]
    [InlineData("Pedal/drum_pedal.SLDASM")]
    [InlineData("Pedal/drum_pedal_resave.SLDASM")]
    public void LimitAngle_Tutorial_DecodesZeroAngleAndEntities(string path)
    {
        using var assembly = new SldAssembly(SamplePath(path), readOnly: true);
        var mate = Assert.Single(assembly.Mates.Where(m => m.MateType == AssemblyMateType.LimitAngle));
        Assert.Equal("moMateLimitPlanarAngleDim_c", mate.SerializedClassName);
        Assert.Equal(6u, mate.MateTypeId);
        Assert.Equal(0.0, mate.Angle);
        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.MateParameters);
        Assert.True(mate.Resolution.EntityReferences);
        Assert.Equal(2, mate.Entities.Count);
    }

    [Theory]
    [InlineData("Advanced/limit_distance_10.SLDASM")]
    [InlineData("Advanced/limit_distance_10_resave.SLDASM")]
    public void LimitDistance_NativeSamples_DecodeDistance(string path)
    {
        using var assembly = new SldAssembly(SamplePath(path), readOnly: true);
        var mate = Assert.Single(assembly.Mates.Where(m => m.MateType == AssemblyMateType.LimitDistance));
        Assert.Equal(5u, mate.MateTypeId);
        Assert.Equal(0.01, mate.Distance!.Value, 12);
        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.MateParameters);
        Assert.Equal(2, mate.Entities.Count);
    }

    [Theory]
    [InlineData("Lifter/lifterassy.SLDASM")]
    [InlineData("Lifter/lifterassy_resave.SLDASM")]
    public void InPlace_IsRecordedButNotAMate2(string path)
    {
        using var assembly = new SldAssembly(SamplePath(path), readOnly: true);
        var inplaces = assembly.Mates.Where(m => m.MateType == AssemblyMateType.InPlace).ToArray();
        Assert.NotEmpty(inplaces);
        Assert.All(inplaces, mate =>
        {
            Assert.Equal("moMateInPlace", mate.SerializedClassName);
            Assert.Null(mate.MateTypeId);
            Assert.False(mate.Resolution.MateType);
            Assert.Empty(mate.Entities);
        });
    }

    [Fact]
    public void HingeProbeArtifact_RemainsClassAndEntitiesOnly()
    {
        using var assembly = new SldAssembly(SamplePath("Probes/hinge_probe.SLDASM"), readOnly: true);
        var hinges = assembly.Mates.Where(m => m.MateType == AssemblyMateType.Hinge).ToArray();
        Assert.Equal(4, hinges.Length);
        Assert.All(hinges, mate =>
        {
            Assert.Equal(22u, mate.MateTypeId);
            Assert.True(mate.Resolution.EntityReferences);
            Assert.False(mate.Resolution.MateParameters);
            Assert.Equal(4, mate.Entities.Count);
        });
    }

    [Fact]
    public void LimitDistanceProbe_StillDecodesD1Only()
    {
        using var assembly = new SldAssembly(SamplePath("Probes/limit_distance_max_only.SLDASM"), readOnly: true);
        var mate = Assert.Single(assembly.Mates.Where(m => m.MateType == AssemblyMateType.LimitDistance));
        Assert.Equal(0.01, mate.Distance!.Value, 12);
        Assert.Equal(2, mate.Entities.Count);
    }

    [Fact]
    public void LimitAngleProbe_StillDecodesD1Only()
    {
        using var assembly = new SldAssembly(SamplePath("Probes/limit_angle_max_only.SLDASM"), readOnly: true);
        var mate = Assert.Single(assembly.Mates.Where(m => m.MateType == AssemblyMateType.LimitAngle));
        Assert.Equal(0.5235987755982988, mate.Angle!.Value, 12);
        Assert.Equal(2, mate.Entities.Count);
    }

    private sealed class DoubleComparer : System.Collections.Generic.IEqualityComparer<double>
    {
        private readonly int _precision;
        public DoubleComparer(int precision) => _precision = precision;
        public bool Equals(double x, double y) => Math.Round(x, _precision) == Math.Round(y, _precision);
        public int GetHashCode(double obj) => Math.Round(obj, _precision).GetHashCode();
    }

    private static string SamplePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) return relativePath;
        return TestFilesPath(relativePath.Split('/'));
    }

    private static string TestFilesPath(params string[] parts)
    {
        return Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles", "Mates2026" }
            .Concat(parts).ToArray());
    }
}
