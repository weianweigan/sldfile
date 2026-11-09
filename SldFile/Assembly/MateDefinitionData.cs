using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Base class for public assembly mate definitions. Pure domain object with standard SI units and zero MFC dependencies.
/// </summary>
public abstract class AssemblyMateDefinitionData
{
    public string DisplayName { get; set; } = string.Empty;
    public abstract AssemblyMateType MateType { get; }
    public AssemblyMateAlignment Alignment { get; set; } = AssemblyMateAlignment.Aligned;
    public List<string> EntitiesToMate { get; } = new();
}

/// <summary>
/// Public definition data for Width Mate (moMateWidth, TypeId 100).
/// </summary>
public sealed class WidthMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Width;
    public AssemblyMateWidthConstraint ConstraintType { get; set; } = AssemblyMateWidthConstraint.Centered;
    public double DistanceFromEnd { get; set; }
    public double PercentDistanceFromEnd { get; set; }
    public bool FlipDimension { get; set; }
    public List<string> WidthEntities { get; } = new(2);
    public List<string> TabEntities { get; } = new(2);
}

/// <summary>
/// Public definition data for Path Mate (moMatePath_c, TypeId 15).
/// </summary>
public sealed class PathMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Path;
    public double PathValue { get; set; }
    public bool IsFreeAlongPath => PathValue == 0;
    public string ComponentPointEntity { get; set; } = string.Empty;
    public string PathCurveEntity { get; set; } = string.Empty;
}

/// <summary>
/// Public definition data for Gear Mate (moMateGearDim_c, TypeId 96).
/// </summary>
public sealed class GearMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Gear;
    public double GearRatioNumerator { get; set; } = 1.0;
    public double GearRatioDenominator { get; set; } = 1.0;
    public bool Reverse { get; set; }

    public string Pinion1Entity
    {
        get => EntitiesToMate.Count > 0 ? EntitiesToMate[0] : string.Empty;
        set { EnsureEntities(1); EntitiesToMate[0] = value; }
    }

    public string Pinion2Entity
    {
        get => EntitiesToMate.Count > 1 ? EntitiesToMate[1] : string.Empty;
        set { EnsureEntities(2); EntitiesToMate[1] = value; }
    }

    private void EnsureEntities(int count)
    {
        while (EntitiesToMate.Count < count) EntitiesToMate.Add(string.Empty);
    }
}

/// <summary>
/// Public definition data for Rack & Pinion Mate (moMateRackPinionDim_c, TypeId 95).
/// </summary>
public sealed class RackPinionMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.RackPinion;
    public AssemblyMateRackPinionDiameterType DiameterType { get; set; } = AssemblyMateRackPinionDiameterType.PinionPitchDiameter;
    public double DiameterValue { get; set; }
    public bool Reverse { get; set; }

    public string RackEntity
    {
        get => EntitiesToMate.Count > 0 ? EntitiesToMate[0] : string.Empty;
        set { EnsureEntities(1); EntitiesToMate[0] = value; }
    }

    public string PinionEntity
    {
        get => EntitiesToMate.Count > 1 ? EntitiesToMate[1] : string.Empty;
        set { EnsureEntities(2); EntitiesToMate[1] = value; }
    }

    private void EnsureEntities(int count)
    {
        while (EntitiesToMate.Count < count) EntitiesToMate.Add(string.Empty);
    }
}

/// <summary>
/// Public definition data for Cam Follower Mate (moMateCamTangent / moMateCamCoincident, TypeId 91).
/// </summary>
public sealed class CamFollowerMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Cam;
    public bool IsTangent { get; set; } = true;
    public string FollowerEntity { get; set; } = string.Empty;
    public List<string> CamProfileFaces { get; } = new();
}

/// <summary>
/// Public definition data for Profile Center Mate (moMateProfileCenter, TypeId 101).
/// </summary>
public sealed class ProfileCenterMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.ProfileCenter;
    public double OffsetDistance { get; set; }
    public bool FlipDimension { get; set; }
    public bool LockRotation { get; set; }
    public string FirstProfileEntity { get; set; } = string.Empty;
    public string SecondProfileEntity { get; set; } = string.Empty;
}

/// <summary>
/// Public definition data for Hinge Mate (moMateHinge, TypeId 94).
/// </summary>
public sealed class HingeMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Hinge;
    public string CoaxialEntity1 { get; set; } = string.Empty;
    public string CoaxialEntity2 { get; set; } = string.Empty;
    public string CoincidentEntity1 { get; set; } = string.Empty;
    public string CoincidentEntity2 { get; set; } = string.Empty;
    public bool AngleSelection { get; set; }
    public double Angle { get; set; }
    public double MinVal { get; set; }
    public double MaxVal { get; set; }
}

/// <summary>
/// Public definition data for Screw Mate (moMateScrew_c, TypeId 97).
/// </summary>
public sealed class ScrewMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Screw;
    public int RevolutionType { get; set; }
    public double RevolutionVal { get; set; } = 0.005;
    public bool Reverse { get; set; }
    public string ScrewEntity1 { get; set; } = string.Empty;
    public string ScrewEntity2 { get; set; } = string.Empty;
}

/// <summary>
/// Public definition data for Universal Joint Mate (moMateUniversalJoint_c, TypeId 98).
/// </summary>
public sealed class UniversalJointMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.UniversalJoint;
    public string Shaft1Entity { get; set; } = string.Empty;
    public string Shaft2Entity { get; set; } = string.Empty;
    public bool DefineJointPoint { get; set; }
    public string JointPointEntity { get; set; } = string.Empty;
}

/// <summary>
/// Public definition data for standard geometric mates (Coincident, Concentric, Distance, Parallel, etc.).
/// </summary>
public sealed class StandardMateDefinitionData : AssemblyMateDefinitionData
{
    private readonly AssemblyMateType _mateType;
    public override AssemblyMateType MateType => _mateType;
    public double? Distance { get; set; }
    public double? Angle { get; set; }

    public StandardMateDefinitionData(AssemblyMateType mateType)
    {
        _mateType = mateType;
    }
}
