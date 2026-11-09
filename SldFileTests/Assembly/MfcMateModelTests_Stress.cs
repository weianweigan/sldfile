using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Assembly;

/// <summary>
/// Adversarial and empirical stress tests for Milestone P0 MFC Mate Models.
/// Tests extreme values, unicode/odd-length names, empty entity lists,
/// repeated registrations, and memory roundtrip fidelity.
/// </summary>
public class MfcMateModelTests_Stress
{
    // ── 1. Extreme Values Stress Tests ────────────────────────────────────

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(-5.0, -2.5)]
    [InlineData(1e12, 1e-6)]
    [InlineData(double.MaxValue / 2, 1.0)]
    [InlineData(1.0, double.MaxValue / 2)]
    public void GearMate_ExtremeRatios_RoundtripsFaithfully(double numerator, double denominator)
    {
        var original = new GearMateModel
        {
            DisplayName = "ExtremeGear",
            Alignment = AssemblyMateAlignment.Closest,
            GearRatioNumerator = numerator,
            GearRatioDenominator = denominator,
            Reverse = true,
            Pinion1Component = "pin1@asm",
            Pinion2Component = "pin2@asm"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new GearMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ExtremeGear", restored.DisplayName);
        Assert.Equal(numerator, restored.GearRatioNumerator);
        Assert.Equal(denominator, restored.GearRatioDenominator);
        Assert.True(restored.Reverse);
    }

    [Theory]
    [InlineData(AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 0.0)]
    [InlineData(AssemblyMateRackPinionDiameterType.PinionPitchDiameter, -0.05)]
    [InlineData(AssemblyMateRackPinionDiameterType.PinionPitchDiameter, 1e9)]
    [InlineData(AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, 0.0)]
    [InlineData(AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, -0.05)]
    [InlineData(AssemblyMateRackPinionDiameterType.RackTravelPerRevolution, 1e9)]
    public void RackPinionMate_ExtremeDiameterValues_RoundtripsFaithfully(
        AssemblyMateRackPinionDiameterType diameterType, double diameterValue)
    {
        var original = new RackPinionMateModel
        {
            DisplayName = "ExtremeRackPinion",
            Alignment = AssemblyMateAlignment.Aligned,
            DiameterType = diameterType,
            DiameterValue = diameterValue,
            Reverse = true,
            RackComponent = "rack@asm",
            PinionComponent = "pinion@asm"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new RackPinionMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ExtremeRackPinion", restored.DisplayName);
        Assert.Equal(diameterType, restored.DiameterType);
        Assert.Equal(diameterValue, restored.DiameterValue, precision: 6);
        Assert.True(restored.Reverse);
    }

    [Fact]
    public void RackPinionMate_ReusedInstanceWithDifferentDiameterType_DeserializesCorrectly()
    {
        var original = new RackPinionMateModel
        {
            DisplayName = "PinionPitchMode",
            DiameterType = AssemblyMateRackPinionDiameterType.PinionPitchDiameter,
            DiameterValue = 0.05
        };

        byte[] bytes = MfcArchive.Serialize(original);

        // Deserializing into an instance that had RackTravelPerRevolution pre-set
        var restored = new RackPinionMateModel
        {
            DiameterType = AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
            DiameterValue = 999.0
        };
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal(AssemblyMateRackPinionDiameterType.PinionPitchDiameter, restored.DiameterType);
        Assert.Equal(0.05, restored.DiameterValue, precision: 6);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.005)]
    [InlineData(1e9)]
    public void ScrewMate_ExtremeRevolutionValues_RoundtripsFaithfully(double revVal)
    {
        var original = new ScrewMateModel
        {
            DisplayName = "ExtremeScrew",
            Alignment = AssemblyMateAlignment.Aligned,
            RevolutionType = 1,
            RevolutionVal = revVal,
            Reverse = false,
            ScrewEntity1 = "s1@asm",
            ScrewEntity2 = "s2@asm"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new ScrewMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ExtremeScrew", restored.DisplayName);
        Assert.Equal(revVal, restored.RevolutionVal, precision: 6);
        Assert.False(restored.Reverse);
    }

    [Theory]
    [InlineData(-0.05, true)]
    [InlineData(-0.05, false)]
    [InlineData(0.0, true)]
    [InlineData(0.0, false)]
    [InlineData(1e6, true)]
    public void ProfileCenterMate_NegativeAndZeroOffsets_RoundtripsFaithfully(double offset, bool flip)
    {
        var original = new ProfileCenterMateModel
        {
            DisplayName = "ExtremeProfileCenter",
            Alignment = AssemblyMateAlignment.AntiAligned,
            OffsetDistance = offset,
            FlipDimension = flip,
            LockRotation = true,
            FirstProfileComponent = "c1@asm",
            SecondProfileComponent = "c2@asm"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new ProfileCenterMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ExtremeProfileCenter", restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.AntiAligned, restored.Alignment);
        Assert.Equal(offset, restored.OffsetDistance, precision: 6);
        if (offset != 0.0)
        {
            Assert.Equal(flip, restored.FlipDimension);
        }
        Assert.True(restored.LockRotation);
    }

    [Theory]
    [InlineData(true, -Math.PI * 4, -1e6, 1e6)]
    [InlineData(true, 1000.0, 0.0, 2000.0)]
    [InlineData(false, 0.0, 0.0, 0.0)]
    public void HingeMate_ExtremeAnglesAndLimits_RoundtripsFaithfully(
        bool angleSelection, double angle, double min, double max)
    {
        var original = new HingeMateModel
        {
            DisplayName = "ExtremeHinge",
            Alignment = AssemblyMateAlignment.Aligned,
            CoaxialEntity1 = "ax1@asm",
            CoaxialEntity2 = "ax2@asm",
            CoincidentEntity1 = "pln1@asm",
            CoincidentEntity2 = "pln2@asm",
            AngleSelection = angleSelection,
            Angle = angle,
            MinVal = min,
            MaxVal = max
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new HingeMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ExtremeHinge", restored.DisplayName);
        Assert.Equal(angleSelection, restored.AngleSelection);
        if (angleSelection)
        {
            Assert.Equal(angle, restored.Angle, precision: 6);
            Assert.Equal(min, restored.MinVal, precision: 6);
            Assert.Equal(max, restored.MaxVal, precision: 6);
        }
    }

    [Theory]
    [InlineData(AssemblyMateWidthConstraint.Centered, 0.0, 0.0)]
    [InlineData(AssemblyMateWidthConstraint.Free, 0.0, 0.0)]
    [InlineData(AssemblyMateWidthConstraint.Dimension, -0.05, 0.0)]
    [InlineData(AssemblyMateWidthConstraint.Dimension, 1e6, 0.0)]
    [InlineData(AssemblyMateWidthConstraint.Percent, 0.0, -25.0)]
    [InlineData(AssemblyMateWidthConstraint.Percent, 0.0, 150.0)]
    public void WidthMate_AllConstraintsAndExtremeValues_RoundtripsFaithfully(
        AssemblyMateWidthConstraint constraint, double distance, double percent)
    {
        var original = new WidthMateModel
        {
            DisplayName = "ExtremeWidth",
            Alignment = AssemblyMateAlignment.Aligned,
            ConstraintType = constraint,
            DistanceFromEnd = distance,
            PercentDistanceFromEnd = percent,
            FlipDimension = true
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal("ExtremeWidth", restored.DisplayName);
        Assert.Equal(constraint, restored.ConstraintType);
        if (constraint == AssemblyMateWidthConstraint.Dimension)
        {
            Assert.Equal(distance, restored.DistanceFromEnd, precision: 6);
            Assert.True(restored.FlipDimension);
        }
        else if (constraint == AssemblyMateWidthConstraint.Percent)
        {
            Assert.Equal(percent, restored.PercentDistanceFromEnd, precision: 6);
            Assert.True(restored.FlipDimension);
        }
    }

    // ── 2. Odd-Length Display Names and Unicode Stress Tests ──────────────

    [Theory]
    [InlineData("")]                          // 0 chars (empty)
    [InlineData("A")]                         // 1 char (odd)
    [InlineData("AB")]                        // 2 chars (even)
    [InlineData("XYZ")]                       // 3 chars (odd)
    [InlineData("Gear")]                      // 4 chars (even)
    [InlineData("Mate5")]                     // 5 chars (odd)
    [InlineData("Width01")]                   // 7 chars (odd)
    [InlineData("VeryLongMateDisplayNameThatExceedsNormalLengthByFar1234567890")] // 61 chars (odd)
    public void AllMateModels_OddAndEvenNameLengths_RoundtripFaithfully(string displayName)
    {
        // 1. Gear
        var gear = new GearMateModel { DisplayName = displayName };
        var restoredGear = new GearMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(gear), restoredGear);
        Assert.Equal(displayName, restoredGear.DisplayName);

        // 2. Width
        var width = new WidthMateModel { DisplayName = displayName };
        var restoredWidth = new WidthMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(width), restoredWidth);
        Assert.Equal(displayName, restoredWidth.DisplayName);

        // 3. Path
        var path = new PathMateModel { DisplayName = displayName };
        var restoredPath = new PathMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(path), restoredPath);
        Assert.Equal(displayName, restoredPath.DisplayName);

        // 4. ProfileCenter
        var pc = new ProfileCenterMateModel { DisplayName = displayName };
        var restoredPc = new ProfileCenterMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(pc), restoredPc);
        Assert.Equal(displayName, restoredPc.DisplayName);

        // 5. RackPinion
        var rp = new RackPinionMateModel { DisplayName = displayName };
        var restoredRp = new RackPinionMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(rp), restoredRp);
        Assert.Equal(displayName, restoredRp.DisplayName);

        // 6. CamFollower
        var cam = new CamFollowerMateModel { DisplayName = displayName };
        var restoredCam = new CamFollowerMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(cam), restoredCam);
        Assert.Equal(displayName, restoredCam.DisplayName);

        // 7. Hinge
        var hinge = new HingeMateModel { DisplayName = displayName };
        var restoredHinge = new HingeMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(hinge), restoredHinge);
        Assert.Equal(displayName, restoredHinge.DisplayName);

        // 8. Screw
        var screw = new ScrewMateModel { DisplayName = displayName };
        var restoredScrew = new ScrewMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(screw), restoredScrew);
        Assert.Equal(displayName, restoredScrew.DisplayName);

        // 9. UniversalJoint
        var uj = new UniversalJointMateModel { DisplayName = displayName };
        var restoredUj = new UniversalJointMateModel();
        MfcArchive.DeserializeInto(MfcArchive.Serialize(uj), restoredUj);
        Assert.Equal(displayName, restoredUj.DisplayName);

        // 10. Standard
        var std = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident) { DisplayName = displayName };
        var restoredStd = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident);
        MfcArchive.DeserializeInto(MfcArchive.Serialize(std), restoredStd);
        Assert.Equal(displayName, restoredStd.DisplayName);
    }

    [Theory]
    [InlineData("配合1")]                       // Chinese (3 chars, odd)
    [InlineData("齿轮配合_测试_№1")]             // Chinese with symbols
    [InlineData("メイト_平面一致_123")]           // Japanese
    [InlineData("메이트_테스트")]                 // Korean
    [InlineData("Сопряжение_Тест")]             // Russian Cyrillic
    [InlineData("Mâte_Élégant_Français")]       // French accented
    [InlineData("Fläche_Überlappung")]           // German umlauts
    [InlineData("⚙️_Gear_Mate_🚀")]              // Emojis & surrogate pairs
    [InlineData("Mate <with> 'quotes' & \"special\" / \\ chars \t")] // Special symbols
    public void AllMateModels_UnicodeAndSpecialNames_RoundtripFaithfully(string displayName)
    {
        var original = new WidthMateModel
        {
            DisplayName = displayName,
            Alignment = AssemblyMateAlignment.Closest,
            ConstraintType = AssemblyMateWidthConstraint.Dimension,
            DistanceFromEnd = 0.01
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal(displayName, restored.DisplayName);
        Assert.Equal(AssemblyMateAlignment.Closest, restored.Alignment);
        Assert.Equal(0.01, restored.DistanceFromEnd, precision: 6);
    }

    // ── 3. Empty Entity Lists and Boundary Topologies ─────────────────────

    [Fact]
    public void EmptyEntityLists_AllMateTypes_NeverCrashAndRoundtrip()
    {
        // 1. Width with 0 entities
        var width = new WidthMateModel();
        var bytesW = MfcArchive.Serialize(width);
        var resW = new WidthMateModel();
        MfcArchive.DeserializeInto(bytesW, resW);
        Assert.NotNull(resW);

        // 2. Path with empty strings
        var path = new PathMateModel();
        var bytesP = MfcArchive.Serialize(path);
        var resP = new PathMateModel();
        MfcArchive.DeserializeInto(bytesP, resP);
        Assert.Equal(string.Empty, resP.FollowerEntity);
        Assert.Equal(string.Empty, resP.PathEntity);

        // 3. ProfileCenter with empty strings
        var pc = new ProfileCenterMateModel();
        var bytesPc = MfcArchive.Serialize(pc);
        var resPc = new ProfileCenterMateModel();
        MfcArchive.DeserializeInto(bytesPc, resPc);
        Assert.Equal(string.Empty, resPc.FirstProfileComponent);

        // 4. Gear with empty strings
        var gear = new GearMateModel();
        var bytesG = MfcArchive.Serialize(gear);
        var resG = new GearMateModel();
        MfcArchive.DeserializeInto(bytesG, resG);
        Assert.Equal(string.Empty, resG.Pinion1Component);

        // 5. RackPinion with empty strings
        var rp = new RackPinionMateModel();
        var bytesRp = MfcArchive.Serialize(rp);
        var resRp = new RackPinionMateModel();
        MfcArchive.DeserializeInto(bytesRp, resRp);
        Assert.Equal(string.Empty, resRp.RackComponent);

        // 6. CamFollower with 0 cam profile faces
        var cam = new CamFollowerMateModel { FollowerEntity = string.Empty };
        var bytesCam = MfcArchive.Serialize(cam);
        var resCam = new CamFollowerMateModel();
        MfcArchive.DeserializeInto(bytesCam, resCam);
        Assert.Empty(resCam.CamProfileFaces);

        // 7. Hinge with empty strings
        var hinge = new HingeMateModel();
        var bytesH = MfcArchive.Serialize(hinge);
        var resH = new HingeMateModel();
        MfcArchive.DeserializeInto(bytesH, resH);
        Assert.Equal(string.Empty, resH.CoaxialEntity1);

        // 8. Screw with empty strings
        var screw = new ScrewMateModel();
        var bytesS = MfcArchive.Serialize(screw);
        var resS = new ScrewMateModel();
        MfcArchive.DeserializeInto(bytesS, resS);
        Assert.Equal(string.Empty, resS.ScrewEntity1);

        // 9. UniversalJoint with empty strings
        var uj = new UniversalJointMateModel();
        var bytesU = MfcArchive.Serialize(uj);
        var resU = new UniversalJointMateModel();
        MfcArchive.DeserializeInto(bytesU, resU);
        Assert.Equal(string.Empty, resU.Shaft1Entity);

        // 10. Standard with 0 entities
        var std = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident);
        var bytesStd = MfcArchive.Serialize(std);
        var resStd = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident);
        MfcArchive.DeserializeInto(bytesStd, resStd);
        Assert.Empty(resStd.PlaneReferences);
    }

    [Fact]
    public void CamFollower_LargeNumberOfProfileFaces_RoundtripsFaithfully()
    {
        var cam = new CamFollowerMateModel(isTangent: true)
        {
            DisplayName = "MultiFaceCam",
            FollowerEntity = "follower@assembly"
        };
        for (int i = 0; i < 64; i++)
        {
            cam.CamProfileFaces.Add($"CamFace_{i}@cam-1");
        }

        byte[] bytes = MfcArchive.Serialize(cam);
        var restored = new CamFollowerMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        Assert.Equal(64, restored.CamProfileFaces.Count);
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal($"CamFace_{i}@cam-1", restored.CamProfileFaces[i]);
        }
    }

    // ── 4. Multiple Repeated Registrations and MatesList Scaling ──────────

    [Fact]
    public void MfcMateRegistry_RepeatedRegistrations_RemainsStable()
    {
        // Re-registering the same class multiple times should not corrupt the factory map
        for (int i = 0; i < 10; i++)
        {
            MfcMateRegistry.Register("moMateGearDim_c", () => new GearMateModel());
            MfcMateRegistry.Register("moMateWidth", () => new WidthMateModel());
        }

        var gear = MfcMateRegistry.Create("moMateGearDim_c");
        Assert.IsType<GearMateModel>(gear);

        var width = MfcMateRegistry.Create("moMateWidth");
        Assert.IsType<WidthMateModel>(width);
    }

    [Fact]
    public void MatesListModel_EmptyMatesList_RoundtripsCleanly()
    {
        var emptyList = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };

        byte[] streamData = MfcArchive.Serialize(emptyList);
        Assert.NotNull(streamData);

        var restored = new MatesListModel();
        MfcArchive.DeserializeInto(streamData, restored);

        Assert.Equal(MatesListModel.DefaultCleanVersion, restored.StreamVersion);
        Assert.Empty(restored.Mates);
    }

    [Fact]
    public void MatesListModel_50MixedMates_PreservesOrderAndAttributes()
    {
        var list = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };

        for (int i = 0; i < 5; i++)
        {
            list.Mates.Add(new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident) { DisplayName = $"Coincident_{i}" });
            list.Mates.Add(new WidthMateModel { DisplayName = $"Width_{i}", DistanceFromEnd = i * 0.01 });
            list.Mates.Add(new GearMateModel { DisplayName = $"Gear_{i}", GearRatioNumerator = i + 1 });
            list.Mates.Add(new RackPinionMateModel { DisplayName = $"Rack_{i}", DiameterValue = i * 0.02 });
            list.Mates.Add(new ProfileCenterMateModel { DisplayName = $"ProfileCenter_{i}", OffsetDistance = i * 0.005 });
            list.Mates.Add(new ScrewMateModel { DisplayName = $"Screw_{i}", RevolutionVal = (i + 1) * 0.001 });
            list.Mates.Add(new HingeMateModel { DisplayName = $"Hinge_{i}", Angle = i * 0.1 });
            list.Mates.Add(new UniversalJointMateModel { DisplayName = $"UJoint_{i}" });
            list.Mates.Add(new CamFollowerMateModel { DisplayName = $"Cam_{i}" });
            list.Mates.Add(new PathMateModel { DisplayName = $"Path_{i}", PathValue = i * 0.03 });
        }

        Assert.Equal(50, list.Mates.Count);

        byte[] streamData = MfcArchive.Serialize(list);
        var restored = new MatesListModel();
        MfcArchive.DeserializeInto(streamData, restored);

        Assert.Equal(50, restored.Mates.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(list.Mates[i].DisplayName, restored.Mates[i].DisplayName);
            Assert.Equal(list.Mates[i].MateType, restored.Mates[i].MateType);
        }
    }

    // ── 5. Two-Tier Definition Roundtrip Fidelity ──────────────────────────

    [Fact]
    public void TwoTier_AllMateDefinitions_RoundtripLosslessly()
    {
        // 1. Width
        var origWidth = new WidthMateModel
        {
            DisplayName = "WidthDef",
            Alignment = AssemblyMateAlignment.AntiAligned,
            ConstraintType = AssemblyMateWidthConstraint.Dimension,
            DistanceFromEnd = 0.025,
            FlipDimension = true
        };
        origWidth.AddPlaneReference("W1@partA");
        origWidth.AddPlaneReference("W2@partA");
        origWidth.AddPlaneReference("T1@partB");
        origWidth.AddPlaneReference("T2@partB");

        var widthDef = origWidth.ToDefinition();
        var newWidth = MfcMateRegistry.CreateForDefinition(widthDef);
        Assert.Equal(origWidth.DisplayName, newWidth.DisplayName);
        Assert.Equal(origWidth.Alignment, newWidth.Alignment);
        Assert.Equal(4, newWidth.EntityReferences.Count);

        // 2. ProfileCenter
        var origPc = new ProfileCenterMateModel
        {
            DisplayName = "ProfileCenterDef",
            Alignment = AssemblyMateAlignment.Aligned,
            OffsetDistance = 0.03,
            FlipDimension = true,
            LockRotation = true
        };
        origPc.AddPlaneReference("Face1@p1");
        origPc.AddPlaneReference("Face2@p2");

        var pcDef = origPc.ToDefinition();
        var newPc = (ProfileCenterMateModel)MfcMateRegistry.CreateForDefinition(pcDef);
        Assert.Equal(origPc.DisplayName, newPc.DisplayName);
        Assert.Equal(origPc.OffsetDistance, newPc.OffsetDistance);
        Assert.True(newPc.FlipDimension);
        Assert.True(newPc.LockRotation);

        // 3. Gear
        var origGear = new GearMateModel
        {
            DisplayName = "GearDef",
            GearRatioNumerator = 4.0,
            GearRatioDenominator = 2.0,
            Reverse = true,
            Pinion1Component = "pin1@asm",
            Pinion2Component = "pin2@asm"
        };
        var gearDef = origGear.ToDefinition();
        var newGear = (GearMateModel)MfcMateRegistry.CreateForDefinition(gearDef);
        Assert.Equal(origGear.GearRatioNumerator, newGear.GearRatioNumerator);
        Assert.Equal(origGear.GearRatioDenominator, newGear.GearRatioDenominator);
        Assert.True(newGear.Reverse);

        // 4. RackPinion
        var origRp = new RackPinionMateModel
        {
            DisplayName = "RackDef",
            DiameterType = AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
            DiameterValue = 0.12,
            Reverse = true,
            RackComponent = "rack@asm",
            PinionComponent = "pin@asm"
        };
        var rpDef = origRp.ToDefinition();
        var newRp = (RackPinionMateModel)MfcMateRegistry.CreateForDefinition(rpDef);
        Assert.Equal(origRp.DiameterType, newRp.DiameterType);
        Assert.Equal(origRp.DiameterValue, newRp.DiameterValue);
        Assert.True(newRp.Reverse);

        // 5. Hinge
        var origHinge = new HingeMateModel
        {
            DisplayName = "HingeDef",
            CoaxialEntity1 = "c1@asm",
            CoaxialEntity2 = "c2@asm",
            CoincidentEntity1 = "p1@asm",
            CoincidentEntity2 = "p2@asm",
            AngleSelection = true,
            Angle = 1.23,
            MinVal = 0.1,
            MaxVal = 2.0
        };
        var hingeDef = origHinge.ToDefinition();
        var newHinge = (HingeMateModel)MfcMateRegistry.CreateForDefinition(hingeDef);
        Assert.Equal(origHinge.Angle, newHinge.Angle);
        Assert.Equal(origHinge.MinVal, newHinge.MinVal);
        Assert.Equal(origHinge.MaxVal, newHinge.MaxVal);

        // 6. Screw
        var origScrew = new ScrewMateModel
        {
            DisplayName = "ScrewDef",
            RevolutionVal = 0.008,
            Reverse = true,
            ScrewEntity1 = "sc1@asm",
            ScrewEntity2 = "sc2@asm"
        };
        var screwDef = origScrew.ToDefinition();
        var newScrew = (ScrewMateModel)MfcMateRegistry.CreateForDefinition(screwDef);
        Assert.Equal(origScrew.RevolutionVal, newScrew.RevolutionVal);
        Assert.True(newScrew.Reverse);
    }

    // ── 6. Repeated Mates of the Same Type & Specimen Roundtrip ───────────

    [Fact]
    public void MatesListModel_TwoGearMates_PreservesBoth()
    {
        var list = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };
        list.Mates.Add(new GearMateModel { DisplayName = "Gear1", GearRatioNumerator = 2.0 });
        list.Mates.Add(new GearMateModel { DisplayName = "Gear2", GearRatioNumerator = 3.0 });

        byte[] streamData = MfcArchive.Serialize(list);
        var restored = new MatesListModel();
        MfcArchive.DeserializeInto(streamData, restored);

        Assert.Equal(2, restored.Mates.Count);
        Assert.Equal("Gear1", restored.Mates[0].DisplayName);
        Assert.Equal("Gear2", restored.Mates[1].DisplayName);
    }

    [Fact]
    public void MatesListModel_TwoCoincidentMates_PreservesBoth()
    {
        var list = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };
        var m1 = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident) { DisplayName = "Coincident1" };
        m1.AddPlaneReference("F1@p1");
        m1.AddPlaneReference("F2@p2");
        list.Mates.Add(m1);

        var m2 = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident) { DisplayName = "Coincident2" };
        m2.AddPlaneReference("F3@p1");
        m2.AddPlaneReference("F4@p2");
        list.Mates.Add(m2);

        byte[] streamData = MfcArchive.Serialize(list);
        var restored = new MatesListModel();
        MfcArchive.DeserializeInto(streamData, restored);

        Assert.Equal(2, restored.Mates.Count);
        Assert.Equal("Coincident1", restored.Mates[0].DisplayName);
        Assert.Equal("Coincident2", restored.Mates[1].DisplayName);
    }

    [Fact]
    public void NativeAssembly_MatesList_RoundtripsWithoutDataCorruption()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly2026, readOnly: true);
        var originalMates = file.MatesList.Mates;
        Assert.NotEmpty(originalMates);
        int originalCount = originalMates.Count;

        // Serialize the loaded MatesList
        byte[] serialized = MfcArchive.Serialize(file.MatesList);
        Assert.NotNull(serialized);

        // Deserialize into a fresh MatesListModel
        var restored = new MatesListModel();
        MfcArchive.DeserializeInto(serialized, restored);

        Assert.Equal(originalCount, restored.Mates.Count);
        for (int i = 0; i < originalCount; i++)
        {
            Assert.Equal(originalMates[i].DisplayName, restored.Mates[i].DisplayName);
            Assert.Equal(originalMates[i].ClassName, restored.Mates[i].ClassName);
        }
    }
}
