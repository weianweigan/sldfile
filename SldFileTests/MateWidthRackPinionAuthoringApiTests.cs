using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Measured-template authoring APIs:
/// Width patches the existing dimensioned moMateWidth (DistanceFromEnd + FlipDimension);
/// RackPinion replaces the clean three-mate rack-and-pinion MatesList with the
/// natively verified four-mate template and patches diameter type/value/reverse.
/// </summary>
public class MateWidthRackPinionAuthoringApiTests
{
    private const string Rack = "rack-1@rack and pinion";
    private const string Pinion = "spurgear-1@rack and pinion";

    [Fact]
    public void SetWidthMateDimension_UpdatesDistanceAndFlip()
    {
        string path = CreateWidthTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                byte[] before = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                assembly.SetWidthMateDimension(0.02, flipDimension: true);
                byte[] pending = assembly.GetStreamBytes("Contents/Config-0-MatesList")!;
                Assert.NotEqual(before, pending);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates.Where(m => m.MateType == AssemblyMateType.Width));
            Assert.Equal(AssemblyMateWidthConstraint.Dimension, mate.WidthConstraintType);
            Assert.Equal(0.02, mate.WidthDistanceFromEnd!.Value, 12);
            Assert.True(mate.WidthFlipDimension);
            Assert.True(mate.Resolution.WidthParameters);
            Assert.Equal(4, mate.Entities.Count);
            Assert.All(mate.Entities.Take(2), e =>
            {
                Assert.Equal(MateReferenceKind.Plane, e.Kind);
                Assert.Equal("pinforyoke-1@universal joint", e.ComponentInstanceName);
            });
            Assert.All(mate.Entities.Skip(2), e =>
            {
                Assert.Equal(MateReferenceKind.Plane, e.Kind);
                Assert.Equal("yoke_female-1@universal joint", e.ComponentInstanceName);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SetWidthMateDimension_CanSetMeasuredDefaults()
    {
        string path = CreateTempCopy(TestFilesPath("Width", "dim20", "universal joint.SLDASM"), "width_api");
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.SetWidthMateDimension(0.01, flipDimension: false);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            var mate = Assert.Single(reopened.Mates.Where(m => m.MateType == AssemblyMateType.Width));
            Assert.Equal(0.01, mate.WidthDistanceFromEnd!.Value, 12);
            Assert.False(mate.WidthFlipDimension);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SetWidthMateDimension_RejectsInvalidInputsAndKeepsStream()
    {
        string path = CreateWidthTempCopy();
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();

            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.SetWidthMateDimension(0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.SetWidthMateDimension(-0.01, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.SetWidthMateDimension(double.NaN, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.SetWidthMateDimension(double.PositiveInfinity, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.SetWidthMateDimension(0.03, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => assembly.SetWidthMateDimension(1001, false));
            Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SetWidthMateDimension_RejectsUnsupportedBase()
    {
        string path = CreateTempCopy(TestFilesPath("Width", "centered", "universal joint.SLDASM"), "wide_centered");
        try
        {
            using var assembly = new SldAssembly(path, readOnly: false);
            byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
            Assert.Throws<NotSupportedException>(() => assembly.SetWidthMateDimension(0.02, true));
            Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SetWidthMateDimension_ReadOnly_ThrowsAndDoesNotModify()
    {
        string path = CreateWidthTempCopy();
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
                Assert.Throws<InvalidOperationException>(() => assembly.SetWidthMateDimension(0.02, true));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.02, false, AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.02, false)]
    [InlineData(AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, 0.02, true, AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, 0.02, true)]
    public void AddRackPinionMate_WritesMeasuredTemplateAndReReads(
        AssemblyMateRackPinionDiameterType inputType,
        double inputValue,
        bool inputReverse,
        AssemblyMateRackPinionDiameterType expectedType,
        double expectedValue,
        bool expectedReverse)
    {
        string path = CreateRackTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddRackPinionMate(Rack, Pinion, inputType, inputValue, inputReverse);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: true);
            Assert.Equal(4, reopened.Mates.Count);
            var mate = Assert.Single(reopened.Mates.Where(m => m.MateType == AssemblyMateType.RackPinion));
            Assert.Equal("moMateRackPinionDim_c", mate.SerializedClassName);
            Assert.Equal(expectedType, mate.RackPinionDiameterType);
            Assert.Equal(expectedValue, mate.RackPinionDiameterValue!.Value, 10);
            Assert.Equal(expectedReverse, mate.RackPinionReverse);
            Assert.Equal(2, mate.Entities.Count);
            Assert.Equal(MateReferenceKind.Edge, mate.Entities[0].Kind);
            Assert.Equal(Rack, mate.Entities[0].ComponentInstanceName);
            Assert.Equal(MateReferenceKind.Circle, mate.Entities[1].Kind);
            Assert.Equal(Pinion, mate.Entities[1].ComponentInstanceName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddRackPinionMate_RejectsExistingMateAndKeepsStream()
    {
        string path = CreateRackTempCopy();
        try
        {
            using (var assembly = new SldAssembly(path, readOnly: false))
            {
                assembly.AddRackPinionMate(Rack, Pinion,
                    AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, true);
                assembly.Save();
            }

            using var reopened = new SldAssembly(path, readOnly: false);
            byte[] before = (byte[])reopened.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
            Assert.Throws<NotSupportedException>(() => reopened.AddRackPinionMate(
                Rack, Pinion, AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.02, false));
            Assert.Equal(before, reopened.GetStreamBytes("Contents/Config-0-MatesList"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AddRackPinionMate_RejectsWrongInputsAndBase()
    {
        string clean = CreateRackTempCopy();
        string noMate = CreateTempCopy(
            TestFilesPath("NoMate", "assembly_nomate_base.SLDASM"),
            "nomate_rack");
        try
        {
            using (var assembly = new SldAssembly(clean, readOnly: false))
            {
                byte[] original = (byte[])assembly.GetStreamBytes("Contents/Config-0-MatesList")!.Clone();
                Assert.Throws<NotSupportedException>(() => assembly.AddRackPinionMate(
                    "rack-2@rack and pinion", Pinion,
                    AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, false));
                Assert.Throws<NotSupportedException>(() => assembly.AddRackPinionMate(
                    Rack, "ghost-1@rack and pinion",
                    AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, false));
                Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddRackPinionMate(
                    Rack, Pinion, AssemblyMateRackPinionDiameterType.Unknown, 0.01, false));
                Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddRackPinionMate(
                    Rack, Pinion, AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0, false));
                Assert.Throws<ArgumentOutOfRangeException>(() => assembly.AddRackPinionMate(
                    Rack, Pinion, AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 1001, false));
                Assert.Equal(original, assembly.GetStreamBytes("Contents/Config-0-MatesList"));
            }

            using (var assembly = new SldAssembly(noMate, readOnly: false))
            {
                Assert.Throws<NotSupportedException>(() => assembly.AddRackPinionMate(
                    Rack, Pinion,
                    AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, false));
            }
        }
        finally
        {
            File.Delete(clean);
            File.Delete(noMate);
        }
    }

    [Fact]
    public void AddRackPinionMate_ReadOnly_ThrowsAndDoesNotModify()
    {
        string path = CreateRackTempCopy();
        try
        {
            byte[] before = File.ReadAllBytes(path);
            using (var assembly = new SldAssembly(path, readOnly: true))
                Assert.Throws<InvalidOperationException>(() => assembly.AddRackPinionMate(
                    Rack, Pinion, AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, true));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateWidthTempCopy()
    {
        return CreateTempCopy(TestFilesPath("Width", "dim10", "universal joint.SLDASM"), "width_api");
    }

    private static string CreateRackTempCopy()
    {
        return CreateTempCopy(TestFilesPath("RackPinion", "rack and pinion_clean.sldasm"), "rack_api");
    }

    private static string CreateTempCopy(string source, string prefix)
    {
        Assert.True(File.Exists(source), "Missing measured sample: " + source);
        string dir = Path.GetDirectoryName(source)!;
        string target = Path.Combine(dir, prefix + "_" + Guid.NewGuid().ToString("N") + ".sldasm");
        File.Copy(source, target, true);
        return target;
    }

    private static string TestFilesPath(params string[] parts)
    {
        return Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles", "Mates2026" }
            .Concat(parts).ToArray());
    }
}
