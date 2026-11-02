using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Measured Width (moMateWidth) and RackPinion (moMateRackPinionDim_c) decoding
/// on the natively authored main/resave artifacts. Width exposes its Dimension
/// constraint and D1 distance plus four planar face references; RackPinion
/// exposes its diameter option/value, reverse flag and rack/pinion entities.
/// </summary>
public class MateWidthRackPinionDecodingTests
{
    private const string WidthAssembly = "universal joint";
    private const string RackAssembly = "rack and pinion";

    [Theory]
    [InlineData("Width/dim10/universal joint.SLDASM", 0.01, false)]
    [InlineData("Width/dim10/universal joint_resave.SLDASM", 0.01, false)]
    [InlineData("Width/dim20_flip/universal joint.SLDASM", 0.02, true)]
    [InlineData("Width/dim20_flip/universal joint_resave.SLDASM", 0.02, true)]
    public void Width_DimensionMate_DecodesMainAndResave(string relativePath, double expectedDistance, bool expectedFlip)
    {
        string path = TestFilesPath(relativePath.Split('/'));
        using var assembly = new SldAssembly(path, readOnly: true);

        var mate = Assert.Single(assembly.Mates.Where(m => m.MateType == AssemblyMateType.Width));
        Assert.Equal("moMateWidth", mate.SerializedClassName);
        Assert.Equal(11u, mate.MateTypeId);
        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.EntityReferences);
        Assert.True(mate.Resolution.MateParameters);
        Assert.True(mate.Resolution.WidthParameters);

        Assert.Equal(AssemblyMateWidthConstraint.Dimension, mate.WidthConstraintType);
        Assert.NotNull(mate.WidthDistanceFromEnd);
        Assert.Equal(expectedDistance, mate.WidthDistanceFromEnd!.Value, 12);
        Assert.Equal(expectedFlip, mate.WidthFlipDimension);
        // Width uses a distinct header tail; alignment is not claimed.
        Assert.Null(mate.Alignment);
        Assert.False(mate.Resolution.MateAlignment);

        Assert.Equal(4, mate.Entities.Count);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(MateReferenceKind.Plane, mate.Entities[i].Kind);
            Assert.Equal("pinforyoke-1@" + WidthAssembly, mate.Entities[i].ComponentInstanceName);
        }
        for (int i = 2; i < 4; i++)
        {
            Assert.Equal(MateReferenceKind.Plane, mate.Entities[i].Kind);
            Assert.Equal("yoke_female-1@" + WidthAssembly, mate.Entities[i].ComponentInstanceName);
        }
    }

    [Theory]
    [InlineData("RackPinion/pitchdia10_aligned/rack and pinion.SLDASM",
        AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, true)]
    [InlineData("RackPinion/pitchdia10_aligned/rack and pinion_resave.SLDASM",
        AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.01, true)]
    [InlineData("RackPinion/pitchdia20_reverse/rack and pinion.SLDASM",
        AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.02, false)]
    [InlineData("RackPinion/pitchdia20_reverse/rack and pinion_resave.SLDASM",
        AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.02, false)]
    [InlineData("RackPinion/travel20/rack and pinion.SLDASM",
        AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, 0.02, true)]
    [InlineData("RackPinion/travel20/rack and pinion_resave.SLDASM",
        AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, 0.02, true)]
    public void RackPinion_DecodesMainAndResave(
        string relativePath,
        AssemblyMateRackPinionDiameterType expectedType,
        double expectedDiameter,
        bool expectedReverse)
    {
        string path = TestFilesPath(relativePath.Split('/'));
        using var assembly = new SldAssembly(path, readOnly: true);

        var mate = Assert.Single(assembly.Mates.Where(m => m.MateType == AssemblyMateType.RackPinion));
        Assert.Equal("moMateRackPinionDim_c", mate.SerializedClassName);
        Assert.Equal(13u, mate.MateTypeId);
        Assert.True(mate.Resolution.MateType);
        Assert.True(mate.Resolution.EntityReferences);
        Assert.True(mate.Resolution.MateParameters);
        Assert.True(mate.Resolution.RackPinionParameters);

        Assert.Equal(expectedType, mate.RackPinionDiameterType);
        Assert.NotNull(mate.RackPinionDiameterValue);
        Assert.Equal(expectedDiameter, mate.RackPinionDiameterValue!.Value, 10);
        Assert.Equal(expectedReverse, mate.RackPinionReverse);
        Assert.Null(mate.Alignment);
        Assert.False(mate.Resolution.MateAlignment);

        Assert.Equal(2, mate.Entities.Count);
        Assert.Equal(MateReferenceKind.Edge, mate.Entities[0].Kind);
        Assert.Equal("rack-1@" + RackAssembly, mate.Entities[0].ComponentInstanceName);
        Assert.Equal(MateReferenceKind.Circle, mate.Entities[1].Kind);
        Assert.Equal("spurgear-1@" + RackAssembly, mate.Entities[1].ComponentInstanceName);
    }

    private static string TestFilesPath(params string[] parts)
    {
        return Path.Combine(new[] { AppContext.BaseDirectory, "TestFiles", "Mates2026" }
            .Concat(parts).ToArray());
    }
}
