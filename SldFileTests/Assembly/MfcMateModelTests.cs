using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Assembly;

/// <summary>
/// Unit tests for MFC assembly mates modeling (Milestone P0).
/// Verifies two-tier architecture, symmetric CArchive serialization,
/// roundtrip fidelity, property equality, and public API surface purity.
/// </summary>
public class MfcMateModelTests
{
    // ── 1. Advanced Mates Roundtrip Tests ─────────────────────────────────

    [Fact]
    public void WidthMateModel_Centered_RoundtripsFaithfully()
    {
        var original = new WidthMateModel
        {
            DisplayName = "WidthCentered1",
            Alignment = AssemblyMateAlignment.Aligned,
            ConstraintType = AssemblyMateWidthConstraint.Centered
        };
        original.AddPlaneReference("WidthFace1@cube-1@assembly");
        original.AddPlaneReference("WidthFace2@cube-1@assembly");
        original.AddPlaneReference("TabFace1@cylinder-1@assembly");
        original.AddPlaneReference("TabFace2@cylinder-1@assembly");

        byte[] bytes = MfcArchive.Serialize(original);
        Assert.NotEmpty(bytes);

        var restored = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("WidthCentered1", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Aligned, restored.Alignment);
        Assert.Equal(AssemblyMateWidthConstraint.Centered, restored.ConstraintType);
        Assert.Equal(4, restored.EntityReferences.Count);
        Assert.Equal("WidthFace1@cube-1@assembly", restored.EntityReferences[0].SelectionName);
        Assert.Equal("WidthFace2@cube-1@assembly", restored.EntityReferences[1].SelectionName);
        Assert.Equal("TabFace1@cylinder-1@assembly", restored.EntityReferences[2].SelectionName);
        Assert.Equal("TabFace2@cylinder-1@assembly", restored.EntityReferences[3].SelectionName);

        // Definition mapping
        var def = restored.ToDefinition();
        Assert.IsType<WidthMateDefinitionData>(def);
        var widthDef = (WidthMateDefinitionData)def;
        Assert.Equal("WidthCentered1", widthDef.DisplayName);
        Assert.Equal(AssemblyMateWidthConstraint.Centered, widthDef.ConstraintType);
        Assert.Equal(4, widthDef.EntitiesToMate.Count);

        var fromDef = new WidthMateModel();
        fromDef.FromDefinition(widthDef);
        Assert.Equal(restored.DisplayName, fromDef.DisplayName);
        Assert.Equal(restored.ConstraintType, fromDef.ConstraintType);
    }

    [Fact]
    public void WidthMateModel_DimensionWithFlip_RoundtripsFaithfully()
    {
        var original = new WidthMateModel
        {
            DisplayName = "WidthDimFlip1",
            Alignment = AssemblyMateAlignment.AntiAligned,
            ConstraintType = AssemblyMateWidthConstraint.Dimension,
            DistanceFromEnd = 0.025,
            FlipDimension = true
        };
        original.AddPlaneReference("W1@compA");
        original.AddPlaneReference("W2@compA");
        original.AddPlaneReference("T1@compB");
        original.AddPlaneReference("T2@compB");

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("WidthDimFlip1", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.AntiAligned, restored.Alignment);
        Assert.Equal(AssemblyMateWidthConstraint.Dimension, restored.ConstraintType);
        Assert.Equal(0.025, restored.DistanceFromEnd, precision: 6);
        Assert.True(restored.FlipDimension);
        Assert.Equal(4, restored.EntityReferences.Count);
    }

    [Fact]
    public void WidthMateModel_Percent_RoundtripsFaithfully()
    {
        var original = new WidthMateModel
        {
            DisplayName = "WidthPercent1",
            Alignment = AssemblyMateAlignment.Closest,
            ConstraintType = AssemblyMateWidthConstraint.Percent,
            PercentDistanceFromEnd = 45.0,
            FlipDimension = false
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("WidthPercent1", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Closest, restored.Alignment);
        Assert.Equal(AssemblyMateWidthConstraint.Percent, restored.ConstraintType);
        Assert.Equal(45.0, restored.PercentDistanceFromEnd, precision: 3);
        Assert.False(restored.FlipDimension);
    }

    [Fact]
    public void PathMateModel_FreeAndFixed_RoundtripsFaithfully()
    {
        var original = new PathMateModel
        {
            DisplayName = "PathMate1",
            Alignment = AssemblyMateAlignment.Aligned,
            PathValue = 0.075
        };
        original.SetEntities("FollowerPt@component-1", "PathCurve@rail-1");

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new PathMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("PathMate1", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Aligned, restored.Alignment);
        Assert.False(restored.PathIsFree);
        Assert.Equal(0.075, restored.PathValue, precision: 6);
        Assert.Equal("FollowerPt@component-1", restored.FollowerEntity);
        Assert.Equal("PathCurve@rail-1", restored.PathEntity);

        // Definition mapping
        var def = (PathMateDefinitionData)restored.ToDefinition();
        Assert.False(def.IsFreeAlongPath);
        Assert.Equal(0.075, def.PathValue, precision: 6);
        Assert.Equal("FollowerPt@component-1", def.ComponentPointEntity);
        Assert.Equal("PathCurve@rail-1", def.PathCurveEntity);
    }

    [Fact]
    public void ProfileCenterMateModel_OffsetAndLockRotation_RoundtripsFaithfully()
    {
        var original = new ProfileCenterMateModel
        {
            DisplayName = "ProfileCenter1",
            Alignment = AssemblyMateAlignment.Aligned,
            OffsetDistance = 0.0125,
            FlipDimension = true,
            LockRotation = true
        };
        original.AddPlaneReference("Face1@plate-1@assembly");
        original.AddPlaneReference("Face2@bracket-1@assembly");

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new ProfileCenterMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ProfileCenter1", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Aligned, restored.Alignment);
        Assert.Equal(0.0125, restored.OffsetDistance, precision: 6);
        Assert.True(restored.FlipDimension);
        Assert.True(restored.LockRotation);
        Assert.Equal(2, restored.EntityReferences.Count);
        Assert.Equal("Face1@plate-1@assembly", restored.EntityReferences[0].SelectionName);
        Assert.Equal("Face2@bracket-1@assembly", restored.EntityReferences[1].SelectionName);

        // Definition mapping
        var def = (ProfileCenterMateDefinitionData)restored.ToDefinition();
        Assert.Equal(0.0125, def.OffsetDistance, precision: 6);
        Assert.True(def.FlipDimension);
        Assert.True(def.LockRotation);
    }

    // ── 2. Mechanical Mates Roundtrip Tests ───────────────────────────────

    [Fact]
    public void GearMateModel_RatioAndReverse_RoundtripsFaithfully()
    {
        var original = new GearMateModel
        {
            DisplayName = "GearMate1",
            Alignment = AssemblyMateAlignment.Closest,
            GearRatioNumerator = 3.5,
            GearRatioDenominator = 1.0,
            Reverse = true,
            Pinion1Component = "pinion-1@gearbox",
            Pinion2Component = "gear-1@gearbox"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new GearMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("GearMate1", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Closest, restored.Alignment);
        Assert.Equal(3.5, restored.GearRatioNumerator, precision: 6);
        Assert.Equal(1.0, restored.GearRatioDenominator, precision: 6);
        Assert.True(restored.Reverse);
        Assert.Equal("pinion-1@gearbox", restored.Pinion1Component);
        Assert.Equal("gear-1@gearbox", restored.Pinion2Component);

        // Definition mapping
        var def = (GearMateDefinitionData)restored.ToDefinition();
        Assert.Equal(3.5, def.GearRatioNumerator);
        Assert.Equal(1.0, def.GearRatioDenominator);
        Assert.True(def.Reverse);
        Assert.Equal("pinion-1@gearbox", def.Pinion1Entity);
        Assert.Equal("gear-1@gearbox", def.Pinion2Entity);
    }

    [Fact]
    public void RackPinionMateModel_DiameterOptions_RoundtripsFaithfully()
    {
        // 1. PinionPitchDiameter mode
        var original1 = new RackPinionMateModel
        {
            DisplayName = "RackPinion1",
            Alignment = AssemblyMateAlignment.Aligned,
            DiameterType = AssemblyMateRackPinionDiameterType.PinionPitchDiameter,
            DiameterValue = 0.05,
            Reverse = false
        };
        original1.SetEntities("rack-1@assembly", "spurgear-1@assembly");

        byte[] bytes1 = MfcArchive.Serialize(original1);
        var restored1 = new RackPinionMateModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal("RackPinion1", restored1.DisplayName);
        Assert.Equal(AssemblyMateRackPinionDiameterType.PinionPitchDiameter, restored1.DiameterType);
        Assert.Equal(0.05, restored1.DiameterValue, precision: 6);
        Assert.False(restored1.Reverse);
        Assert.Equal("rack-1@assembly", restored1.RackComponent);
        Assert.Equal("spurgear-1@assembly", restored1.PinionComponent);

        // 2. RackTravelPerRevolution mode
        var original2 = new RackPinionMateModel
        {
            DisplayName = "RackPinion2",
            Alignment = AssemblyMateAlignment.Aligned,
            DiameterType = AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
            DiameterValue = 0.15707963267948966,
            Reverse = true
        };
        original2.SetEntities("rack-2@assembly", "spurgear-2@assembly");

        byte[] bytes2 = MfcArchive.Serialize(original2);
        var restored2 = new RackPinionMateModel();
        MfcArchive.DeserializeInto(bytes2, restored2);

        Assert.Equal(AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, restored2.DiameterType);
        Assert.Equal(0.15707963267948966, restored2.DiameterValue, precision: 6);
        Assert.True(restored2.Reverse);
    }

    [Fact]
    public void CamFollowerMateModel_TangentAndCoincident_RoundtripsFaithfully()
    {
        var tangentCam = new CamFollowerMateModel(isTangent: true)
        {
            DisplayName = "CamTangent1",
            Alignment = AssemblyMateAlignment.Closest,
            FollowerEntity = "follower-1@assembly"
        };
        tangentCam.CamProfileFaces.Add("CamFace1@cam-1");
        tangentCam.CamProfileFaces.Add("CamFace2@cam-1");

        byte[] bytes = MfcArchive.Serialize(tangentCam);
        var restored = new CamFollowerMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("CamTangent1", restored.DisplayName);
        Assert.True(restored.IsTangent);
        Assert.Equal("follower-1@assembly", restored.FollowerEntity);
        Assert.Equal(2, restored.CamProfileFaces.Count);
        Assert.Equal("CamFace1@cam-1", restored.CamProfileFaces[0]);
        Assert.Equal("CamFace2@cam-1", restored.CamProfileFaces[1]);

        // Definition mapping
        var def = (CamFollowerMateDefinitionData)restored.ToDefinition();
        Assert.True(def.IsTangent);
        Assert.Equal("follower-1@assembly", def.FollowerEntity);
        Assert.Equal(2, def.CamProfileFaces.Count);
    }

    [Fact]
    public void HingeMateModel_LimitsAndPairs_RoundtripsFaithfully()
    {
        var original = new HingeMateModel
        {
            DisplayName = "Hinge1",
            Alignment = AssemblyMateAlignment.Aligned,
            CoaxialEntity1 = "axis1@linkA",
            CoaxialEntity2 = "axis2@linkB",
            CoincidentEntity1 = "face1@linkA",
            CoincidentEntity2 = "face2@linkB",
            AngleSelection = true,
            Angle = 0.5235987755982988, // 30 deg
            MinVal = 0.0,
            MaxVal = 1.5707963267948966 // 90 deg
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new HingeMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("Hinge1", restored.DisplayName);
        Assert.Equal("axis1@linkA", restored.CoaxialEntity1);
        Assert.Equal("axis2@linkB", restored.CoaxialEntity2);
        Assert.Equal("face1@linkA", restored.CoincidentEntity1);
        Assert.Equal("face2@linkB", restored.CoincidentEntity2);
        Assert.True(restored.AngleSelection);
        Assert.Equal(0.5235987755982988, restored.Angle, precision: 6);
        Assert.Equal(0.0, restored.MinVal, precision: 6);
        Assert.Equal(1.5707963267948966, restored.MaxVal, precision: 6);
    }

    [Fact]
    public void ScrewMateModel_RevolutionsAndReverse_RoundtripsFaithfully()
    {
        var original = new ScrewMateModel
        {
            DisplayName = "Screw1",
            Alignment = AssemblyMateAlignment.Aligned,
            RevolutionType = 0,
            RevolutionVal = 0.004,
            Reverse = true,
            ScrewEntity1 = "screw-1@assembly",
            ScrewEntity2 = "nut-1@assembly"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new ScrewMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("Screw1", restored.DisplayName);
        Assert.Equal(0.004, restored.RevolutionVal, precision: 6);
        Assert.True(restored.Reverse);
        Assert.Equal("screw-1@assembly", restored.ScrewEntity1);
        Assert.Equal("nut-1@assembly", restored.ScrewEntity2);
    }

    [Fact]
    public void UniversalJointMateModel_Shafts_RoundtripsFaithfully()
    {
        var original = new UniversalJointMateModel
        {
            DisplayName = "UJoint1",
            Alignment = AssemblyMateAlignment.Closest,
            Shaft1Entity = "input_shaft-1@assembly",
            Shaft2Entity = "output_shaft-1@assembly"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new UniversalJointMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("UJoint1", restored.DisplayName);
        Assert.Equal("input_shaft-1@assembly", restored.Shaft1Entity);
        Assert.Equal("output_shaft-1@assembly", restored.Shaft2Entity);
    }

    // ── 3. Standard & Generic Mates Roundtrip Tests ───────────────────────

    [Theory]
    [InlineData("moMateCoincident", AssemblyMateType.Coincident, 0.0)]
    [InlineData("moMateConcentric", AssemblyMateType.Concentric, 0.0)]
    [InlineData("moMateDistanceDim_c", AssemblyMateType.Distance, 0.035)]
    [InlineData("moMateParallel", AssemblyMateType.Parallel, 0.0)]
    [InlineData("moMateTangent", AssemblyMateType.Tangent, 0.0)]
    [InlineData("moMatePerpendicular", AssemblyMateType.Perpendicular, 0.0)]
    [InlineData("moMatePlanarAngleDim_c", AssemblyMateType.PlanarAngle, 0.785)]
    [InlineData("moMateLimitDistanceDim_c", AssemblyMateType.LimitDistance, 0.05)]
    [InlineData("moMateLimitPlanarAngleDim_c", AssemblyMateType.LimitAngle, 1.0)]
    public void StandardMateModel_AllTypes_RoundtripFaithfully(
        string className, AssemblyMateType mateType, double paramValue)
    {
        var original = new StandardMateModel(className, mateType)
        {
            DisplayName = "StandardMate_" + mateType,
            Alignment = AssemblyMateAlignment.Aligned,
            Distance = paramValue
        };
        original.AddPlaneReference("Entity1@compA");
        original.AddPlaneReference("Entity2@compB");

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new StandardMateModel(className, mateType);
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal(className, restored.ClassName);
        Assert.Equal(mateType, restored.MateType);
        Assert.Equal("StandardMate_" + mateType, restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Aligned, restored.Alignment);
        Assert.Equal(paramValue, restored.Distance ?? 0.0, precision: 6);
        Assert.Equal(2, restored.EntityReferences.Count);
        Assert.Equal("Entity1@compA", restored.EntityReferences[0].SelectionName);
        Assert.Equal("Entity2@compB", restored.EntityReferences[1].SelectionName);
    }

    [Fact]
    public void GenericMateModel_LosslessPreservation_RoundtripsFaithfully()
    {
        var original = new GenericMateModel("moMateCustomVendor")
        {
            DisplayName = "CustomVendorMate1",
            Alignment = AssemblyMateAlignment.Closest,
            RawRecordBytes = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF }
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new GenericMateModel("moMateCustomVendor");
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("moMateCustomVendor", restored.ClassName);
        Assert.Equal("CustomVendorMate1", restored.DisplayName);
        Assert.Equal(original.RawRecordBytes, restored.RawRecordBytes);
    }

    // ── 4. MatesListModel Multi-Mate Symmetric Serialization ──────────────

    [Fact]
    public void MatesListModel_MultipleMates_SerializesAndDeserializesSymmetrically()
    {
        var list = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };

        // 1. Coincident mate
        var coincident = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident)
        {
            DisplayName = "Coincident1",
            Alignment = AssemblyMateAlignment.Aligned
        };
        coincident.AddPlaneReference("Front@part1");
        coincident.AddPlaneReference("Front@part2");
        list.Mates.Add(coincident);

        // 2. Width mate
        var width = new WidthMateModel
        {
            DisplayName = "Width1",
            Alignment = AssemblyMateAlignment.Aligned,
            ConstraintType = AssemblyMateWidthConstraint.Centered
        };
        width.AddPlaneReference("W1@part1");
        width.AddPlaneReference("W2@part1");
        width.AddPlaneReference("T1@part2");
        width.AddPlaneReference("T2@part2");
        list.Mates.Add(width);

        // 3. Gear mate
        var gear = new GearMateModel
        {
            DisplayName = "Gear1",
            Alignment = AssemblyMateAlignment.Closest,
            GearRatioNumerator = 2.0,
            GearRatioDenominator = 1.0,
            Pinion1Component = "pinion@gearbox",
            Pinion2Component = "gear@gearbox"
        };
        list.Mates.Add(gear);

        // 4. RackPinion mate
        var rack = new RackPinionMateModel
        {
            DisplayName = "RackPinion1",
            Alignment = AssemblyMateAlignment.Aligned,
            DiameterType = AssemblyMateRackPinionDiameterType.PinionPitchDiameter,
            DiameterValue = 0.04
        };
        rack.SetEntities("rack@assembly", "pinion@assembly");
        list.Mates.Add(rack);

        // Serialize the entire container symmetrically
        byte[] streamData = MfcArchive.Serialize(list);
        Assert.True(streamData.Length > 6);
        Assert.Equal(MatesListModel.DefaultCleanVersion, BitConverter.ToUInt16(streamData, 0));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(streamData, 2));
        Assert.Equal((ushort)4, BitConverter.ToUInt16(streamData, 4));

        // Deserialize back into a fresh instance
        var reloaded = new MatesListModel();
        MfcArchive.DeserializeInto(streamData, reloaded);

        Assert.Equal(MatesListModel.DefaultCleanVersion, reloaded.StreamVersion);
        Assert.Equal(4, reloaded.Mates.Count);

        Assert.IsType<StandardMateModel>(reloaded.Mates[0]);
        Assert.Equal("Coincident1", reloaded.Mates[0].DisplayName);
        Assert.Equal(AssemblyMateType.Coincident, reloaded.Mates[0].MateType);

        Assert.IsType<WidthMateModel>(reloaded.Mates[1]);
        Assert.Equal("Width1", reloaded.Mates[1].DisplayName);
        Assert.Equal(AssemblyMateType.Width, reloaded.Mates[1].MateType);

        Assert.IsType<GearMateModel>(reloaded.Mates[2]);
        Assert.Equal("Gear1", reloaded.Mates[2].DisplayName);
        Assert.Equal(AssemblyMateType.Gear, reloaded.Mates[2].MateType);

        Assert.IsType<RackPinionMateModel>(reloaded.Mates[3]);
        Assert.Equal("RackPinion1", reloaded.Mates[3].DisplayName);
        Assert.Equal(AssemblyMateType.RackPinion, reloaded.Mates[3].MateType);
    }

    // ── 5. MfcMateRegistry Factory Verification ───────────────────────────

    [Theory]
    [InlineData("moMateWidth", typeof(WidthMateModel))]
    [InlineData("moMatePath_c", typeof(PathMateModel))]
    [InlineData("moMateProfileCenter", typeof(ProfileCenterMateModel))]
    [InlineData("moMateGearDim_c", typeof(GearMateModel))]
    [InlineData("moMateRackPinionDim_c", typeof(RackPinionMateModel))]
    [InlineData("moMateCamTangent", typeof(CamFollowerMateModel))]
    [InlineData("moMateCamCoincident", typeof(CamFollowerMateModel))]
    [InlineData("moMateHinge", typeof(HingeMateModel))]
    [InlineData("moMateScrew_c", typeof(ScrewMateModel))]
    [InlineData("moMateUniversalJoint_c", typeof(UniversalJointMateModel))]
    [InlineData("moMateCoincident", typeof(StandardMateModel))]
    [InlineData("moMateDistanceDim_c", typeof(StandardMateModel))]
    [InlineData("moMateUnknownVendorFoo", typeof(GenericMateModel))]
    public void MfcMateRegistry_CreatesExpectedConcreteTypes(string className, Type expectedType)
    {
        var mate = MfcMateRegistry.Create(className);
        Assert.IsType(expectedType, mate);
        Assert.Equal(className, mate.ClassName);
    }

    [Fact]
    public void MfcMateRegistry_CreateForDefinition_InstantiatesAndPopulates()
    {
        var def = new WidthMateDefinitionData
        {
            DisplayName = "WidthDef1",
            Alignment = AssemblyMateAlignment.AntiAligned,
            ConstraintType = AssemblyMateWidthConstraint.Dimension,
            DistanceFromEnd = 0.015,
            FlipDimension = true
        };
        def.EntitiesToMate.Add("FaceA@part1");
        def.EntitiesToMate.Add("FaceB@part1");
        def.EntitiesToMate.Add("FaceC@part2");
        def.EntitiesToMate.Add("FaceD@part2");

        var model = MfcMateRegistry.CreateForDefinition(def);
        Assert.IsType<WidthMateModel>(model);
        var width = (WidthMateModel)model;
        Assert.Equal("WidthDef1", width.DisplayName);
        Assert.Equal(AssemblyMateAlignment.AntiAligned, width.Alignment);
        Assert.Equal(AssemblyMateWidthConstraint.Dimension, width.ConstraintType);
        Assert.Equal(0.015, width.DistanceFromEnd, precision: 6);
        Assert.True(width.FlipDimension);
        Assert.Equal(4, width.EntityReferences.Count);
    }

    // ── 6. Two-Tier Architecture and Public API Purity ───────────────────

    [Fact]
    public void PublicSurface_ExposesOnlyDefinitionDataAndNoInternalMfcModels()
    {
        var sldAssemblyType = typeof(SldAssembly);
        Assert.True(sldAssemblyType.IsPublic);

        var assembly = typeof(SldFile.AssemblyMateDefinitionData).Assembly;
        var publicTypes = assembly.GetExportedTypes();

        // 1. All MateDefinitionData types must be public and in namespace SldFile
        Assert.Contains(publicTypes, t => t.Name == nameof(AssemblyMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(WidthMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(PathMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(GearMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(RackPinionMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(CamFollowerMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(ProfileCenterMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(HingeMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(ScrewMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(UniversalJointMateDefinitionData));
        Assert.Contains(publicTypes, t => t.Name == nameof(StandardMateDefinitionData));

        // 2. All *MateModel and IMfcMateModel classes must be internal (NOT exported)
        Assert.DoesNotContain(publicTypes, t => t.Name == "IMfcMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "MfcMateModelBase");
        Assert.DoesNotContain(publicTypes, t => t.Name == "MfcMateRegistry");
        Assert.DoesNotContain(publicTypes, t => t.Name == "WidthMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "PathMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "ProfileCenterMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "GearMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "RackPinionMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "CamFollowerMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "HingeMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "ScrewMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "UniversalJointMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "StandardMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "GenericMateModel");
        Assert.DoesNotContain(publicTypes, t => t.Name == "MatesListModel");
    }
}
