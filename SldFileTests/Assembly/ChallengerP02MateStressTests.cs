using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Assembly;

/// <summary>
/// Empirical challenge and stress tests for Milestone P0.2.1 Assembly Mate Models.
/// Tests 50 mixed mates with 100% attribute fidelity, double roundtrips with byte-level
/// equality assertions, large scale collections (200 mates), and native resave fidelity.
/// </summary>
public class ChallengerP02MateStressTests
{
    // ── 1. 50 Mixed Mates 100% Attribute Fidelity & Double Roundtrip Byte Equality ──

    [Fact]
    public void MatesListModel_50MixedMates_FullAttributeFidelity_AndDoubleRoundtripByteEquality()
    {
        var originalList = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };

        for (int i = 0; i < 5; i++)
        {
            // 1. Standard Coincident
            var std = new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident)
            {
                DisplayName = $"Coincident_{i}",
                Alignment = (i % 2 == 0) ? AssemblyMateAlignment.Aligned : AssemblyMateAlignment.AntiAligned
            };
            std.AddPlaneReference($"PlaneA_{i}@part1-1");
            std.AddPlaneReference($"PlaneB_{i}@part2-1");
            originalList.Mates.Add(std);

            // 2. Width
            var widthConstraint = (i % 4) switch
            {
                0 => AssemblyMateWidthConstraint.Centered,
                1 => AssemblyMateWidthConstraint.Free,
                2 => AssemblyMateWidthConstraint.Dimension,
                _ => AssemblyMateWidthConstraint.Percent
            };
            var width = new WidthMateModel
            {
                DisplayName = $"Width_{i}",
                Alignment = AssemblyMateAlignment.Closest,
                ConstraintType = widthConstraint,
                DistanceFromEnd = (widthConstraint == AssemblyMateWidthConstraint.Dimension) ? 0.01 * (i + 1) : 0.0,
                PercentDistanceFromEnd = (widthConstraint == AssemblyMateWidthConstraint.Percent) ? 10.0 * (i + 1) : 0.0,
                FlipDimension = (i % 2 == 1)
            };
            width.AddPlaneReference($"Width1_{i}@part1-1");
            width.AddPlaneReference($"Width2_{i}@part1-1");
            width.AddPlaneReference($"Tab1_{i}@part2-1");
            width.AddPlaneReference($"Tab2_{i}@part2-1");
            originalList.Mates.Add(width);

            // 3. Gear
            var gear = new GearMateModel
            {
                DisplayName = $"Gear_{i}",
                Alignment = AssemblyMateAlignment.Aligned,
                GearRatioNumerator = 2.5 * (i + 1),
                GearRatioDenominator = 1.0 * (i + 1),
                Reverse = (i % 2 == 1),
                Pinion1Component = $"pinion1_{i}@gearbox-1",
                Pinion2Component = $"pinion2_{i}@gearbox-1"
            };
            originalList.Mates.Add(gear);

            // 4. RackPinion
            var rp = new RackPinionMateModel
            {
                DisplayName = $"Rack_{i}",
                Alignment = AssemblyMateAlignment.AntiAligned,
                DiameterType = (i % 2 == 0)
                    ? AssemblyMateRackPinionDiameterType.PinionPitchDiameter
                    : AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
                DiameterValue = (i % 2 == 0) ? 0.05 * (i + 1) : -0.02 * (i + 1),
                Reverse = (i % 2 == 0),
                RackComponent = $"rack_{i}@assembly",
                PinionComponent = $"pin_{i}@assembly"
            };
            originalList.Mates.Add(rp);

            // 5. ProfileCenter
            var pc = new ProfileCenterMateModel
            {
                DisplayName = $"ProfileCenter_{i}",
                Alignment = AssemblyMateAlignment.Aligned,
                OffsetDistance = 0.005 * (i + 1),
                FlipDimension = (i % 2 == 1),
                LockRotation = (i % 2 == 0),
                FirstProfileComponent = $"profile1_{i}@comp1-1",
                SecondProfileComponent = $"profile2_{i}@comp2-1"
            };
            pc.AddPlaneReference($"ProfileFace1_{i}@comp1-1");
            pc.AddPlaneReference($"ProfileFace2_{i}@comp2-1");
            originalList.Mates.Add(pc);

            // 6. Screw
            var screw = new ScrewMateModel
            {
                DisplayName = $"Screw_{i}",
                Alignment = AssemblyMateAlignment.Aligned,
                RevolutionVal = 0.002 * (i + 1),
                Reverse = (i % 2 == 1),
                ScrewEntity1 = $"thread1_{i}@bolt-1",
                ScrewEntity2 = $"thread2_{i}@nut-1"
            };
            originalList.Mates.Add(screw);

            // 7. Hinge
            var hinge = new HingeMateModel
            {
                DisplayName = $"Hinge_{i}",
                Alignment = AssemblyMateAlignment.Closest,
                CoaxialEntity1 = $"axis1_{i}@leaf1-1",
                CoaxialEntity2 = $"axis2_{i}@leaf2-1",
                CoincidentEntity1 = $"plane1_{i}@leaf1-1",
                CoincidentEntity2 = $"plane2_{i}@leaf2-1",
                AngleSelection = (i % 2 == 1),
                Angle = 0.5 * (i + 1),
                MinVal = 0.1 * (i + 1),
                MaxVal = 1.5 * (i + 1)
            };
            originalList.Mates.Add(hinge);

            // 8. UniversalJoint
            var uj = new UniversalJointMateModel
            {
                DisplayName = $"UJoint_{i}",
                Alignment = AssemblyMateAlignment.Aligned,
                Shaft1Entity = $"shaft1_{i}@driveshaft-1",
                Shaft2Entity = $"shaft2_{i}@axle-1"
            };
            originalList.Mates.Add(uj);

            // 9. CamFollower
            var cam = new CamFollowerMateModel(isTangent: (i % 2 == 0))
            {
                DisplayName = $"Cam_{i}",
                Alignment = AssemblyMateAlignment.Aligned,
                FollowerEntity = $"follower_{i}@follower-1"
            };
            for (int f = 0; f < 4; f++)
            {
                cam.CamProfileFaces.Add($"CamProfileFace_{i}_{f}@cam-1");
            }
            originalList.Mates.Add(cam);

            // 10. Path
            var path = new PathMateModel
            {
                DisplayName = $"Path_{i}",
                Alignment = AssemblyMateAlignment.Aligned,
                FollowerEntity = $"roller_{i}@cart-1",
                PathEntity = $"rail_{i}@track-1",
                PathValue = 0.15 * (i + 1)
            };
            originalList.Mates.Add(path);
        }

        Assert.Equal(50, originalList.Mates.Count);

        // First Serialization: MatesList -> Bytes1
        byte[] bytes1 = MfcArchive.Serialize(originalList);
        Assert.NotNull(bytes1);
        Assert.NotEmpty(bytes1);

        // First Deserialization: Bytes1 -> RestoredList1
        var restored1 = new MatesListModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(50, restored1.Mates.Count);

        // Verify 100% attribute fidelity across all 50 mates
        AssertMatesAttributeEquality(originalList.Mates, restored1.Mates);

        // Second Serialization: RestoredList1 -> Bytes2 (Double Roundtrip)
        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.NotNull(bytes2);

        // Byte-level equality between first serialize and second serialize
        Assert.Equal(bytes1.Length, bytes2.Length);
        Assert.True(bytes1.SequenceEqual(bytes2), "Double roundtrip bytes must be byte-for-byte identical.");

        // Second Deserialization: Bytes2 -> RestoredList2
        var restored2 = new MatesListModel();
        MfcArchive.DeserializeInto(bytes2, restored2);

        Assert.Equal(50, restored2.Mates.Count);
        AssertMatesAttributeEquality(restored1.Mates, restored2.Mates);
    }

    // ── 2. Double Roundtrips with Byte-Level Equality on Concrete Mate Models ──

    [Fact]
    public void StandardMateModel_DoubleRoundtrip_ByteAndAttributeEquality()
    {
        var original = new StandardMateModel("moMateDistanceDim_c", AssemblyMateType.Distance)
        {
            DisplayName = "DistanceDoubleRoundtrip",
            Alignment = AssemblyMateAlignment.AntiAligned,
            Distance = 0.045
        };
        original.AddPlaneReference("TopFace@part1-1");
        original.AddPlaneReference("BottomFace@part2-1");

        byte[] bytes1 = MfcArchive.Serialize(original);
        var restored1 = new StandardMateModel("moMateDistanceDim_c", AssemblyMateType.Distance);
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(original.DisplayName, restored1.DisplayName);
        Assert.Equal(original.Alignment, restored1.Alignment);
        Assert.Equal(original.Distance, restored1.Distance);
        Assert.Equal(2, restored1.PlaneReferences.Count);

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "StandardMateModel byte-level equality failed on double roundtrip.");

        var restored2 = new StandardMateModel("moMateDistanceDim_c", AssemblyMateType.Distance);
        MfcArchive.DeserializeInto(bytes2, restored2);

        Assert.Equal(restored1.DisplayName, restored2.DisplayName);
        Assert.Equal(restored1.Alignment, restored2.Alignment);
        Assert.Equal(restored1.Distance, restored2.Distance);
        Assert.Equal(restored1.PlaneReferences.Count, restored2.PlaneReferences.Count);
    }

    [Theory]
    [InlineData(AssemblyMateWidthConstraint.Centered, 0.0, 0.0, false)]
    [InlineData(AssemblyMateWidthConstraint.Free, 0.0, 0.0, false)]
    [InlineData(AssemblyMateWidthConstraint.Dimension, 0.025, 0.0, true)]
    [InlineData(AssemblyMateWidthConstraint.Percent, 0.0, 42.5, true)]
    public void WidthMateModel_AllConstraints_DoubleRoundtrip_ByteAndAttributeEquality(
        AssemblyMateWidthConstraint constraint, double distance, double percent, bool flip)
    {
        var original = new WidthMateModel
        {
            DisplayName = $"Width_{constraint}",
            Alignment = AssemblyMateAlignment.Aligned,
            ConstraintType = constraint,
            DistanceFromEnd = distance,
            PercentDistanceFromEnd = percent,
            FlipDimension = flip
        };
        original.AddPlaneReference("W1@partA-1");
        original.AddPlaneReference("W2@partA-1");
        original.AddPlaneReference("T1@partB-1");
        original.AddPlaneReference("T2@partB-1");

        byte[] bytes1 = MfcArchive.Serialize(original);
        var restored1 = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(original.DisplayName, restored1.DisplayName);
        Assert.Equal(original.ConstraintType, restored1.ConstraintType);
        Assert.Equal(4, restored1.EntityReferences.Count);

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), $"WidthMateModel ({constraint}) byte-level equality failed on double roundtrip.");

        var restored2 = new WidthMateModel();
        MfcArchive.DeserializeInto(bytes2, restored2);
        Assert.Equal(restored1.ConstraintType, restored2.ConstraintType);
        Assert.Equal(restored1.DistanceFromEnd, restored2.DistanceFromEnd);
        Assert.Equal(restored1.PercentDistanceFromEnd, restored2.PercentDistanceFromEnd);
        Assert.Equal(restored1.FlipDimension, restored2.FlipDimension);
    }

    [Fact]
    public void GearMateModel_DoubleRoundtrip_ByteAndAttributeEquality()
    {
        var original = new GearMateModel
        {
            DisplayName = "PrecisionGear",
            Alignment = AssemblyMateAlignment.AntiAligned,
            GearRatioNumerator = 4.25,
            GearRatioDenominator = 1.75,
            Reverse = true,
            Pinion1Component = "gearA@housing-1",
            Pinion2Component = "gearB@housing-1"
        };

        byte[] bytes1 = MfcArchive.Serialize(original);
        var restored1 = new GearMateModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(original.DisplayName, restored1.DisplayName);
        Assert.Equal(original.GearRatioNumerator, restored1.GearRatioNumerator);
        Assert.Equal(original.GearRatioDenominator, restored1.GearRatioDenominator);
        Assert.True(restored1.Reverse);

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "GearMateModel byte-level equality failed on double roundtrip.");

        var restored2 = new GearMateModel();
        MfcArchive.DeserializeInto(bytes2, restored2);
        Assert.Equal(restored1.GearRatioNumerator, restored2.GearRatioNumerator);
        Assert.Equal(restored1.GearRatioDenominator, restored2.GearRatioDenominator);
        Assert.Equal(restored1.Reverse, restored2.Reverse);
        Assert.Equal(restored1.Pinion1Component, restored2.Pinion1Component);
        Assert.Equal(restored1.Pinion2Component, restored2.Pinion2Component);
    }

    [Fact]
    public void RackPinionMateModel_DoubleRoundtrip_ByteAndAttributeEquality()
    {
        var original = new RackPinionMateModel
        {
            DisplayName = "PrecisionRackPinion",
            Alignment = AssemblyMateAlignment.Aligned,
            DiameterType = AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
            DiameterValue = -0.031415,
            Reverse = true,
            RackComponent = "rack_bar@base-1",
            PinionComponent = "pinion_wheel@motor-1"
        };

        byte[] bytes1 = MfcArchive.Serialize(original);
        var restored1 = new RackPinionMateModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(original.DisplayName, restored1.DisplayName);
        Assert.Equal(original.DiameterType, restored1.DiameterType);
        Assert.Equal(original.DiameterValue, restored1.DiameterValue, precision: 6);
        Assert.True(restored1.Reverse);

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "RackPinionMateModel byte-level equality failed on double roundtrip.");

        var restored2 = new RackPinionMateModel();
        MfcArchive.DeserializeInto(bytes2, restored2);
        Assert.Equal(restored1.DiameterType, restored2.DiameterType);
        Assert.Equal(restored1.DiameterValue, restored2.DiameterValue, precision: 6);
        Assert.Equal(restored1.Reverse, restored2.Reverse);
    }

    [Fact]
    public void ProfileCenterMateModel_DoubleRoundtrip_ByteAndAttributeEquality()
    {
        var original = new ProfileCenterMateModel
        {
            DisplayName = "PrecisionProfileCenter",
            Alignment = AssemblyMateAlignment.AntiAligned,
            OffsetDistance = 0.0125,
            FlipDimension = true,
            LockRotation = true,
            FirstProfileComponent = "flangeA@assy",
            SecondProfileComponent = "flangeB@assy"
        };
        original.AddPlaneReference("Face1@flangeA");
        original.AddPlaneReference("Face2@flangeB");

        byte[] bytes1 = MfcArchive.Serialize(original);
        var restored1 = new ProfileCenterMateModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(original.DisplayName, restored1.DisplayName);
        Assert.Equal(original.OffsetDistance, restored1.OffsetDistance, precision: 6);
        Assert.True(restored1.FlipDimension);
        Assert.True(restored1.LockRotation);

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "ProfileCenterMateModel byte-level equality failed on double roundtrip.");

        var restored2 = new ProfileCenterMateModel();
        MfcArchive.DeserializeInto(bytes2, restored2);
        Assert.Equal(restored1.OffsetDistance, restored2.OffsetDistance, precision: 6);
        Assert.Equal(restored1.FlipDimension, restored2.FlipDimension);
        Assert.Equal(restored1.LockRotation, restored2.LockRotation);
    }

    // ── 3. CamFollower Large Profile Faces (64 and 128) ────────────────────

    [Fact]
    public void CamFollower_64ProfileFaces_DoubleRoundtrip_ByteAndAttributeEquality()
    {
        var cam = new CamFollowerMateModel(isTangent: true)
        {
            DisplayName = "Cam64Faces",
            Alignment = AssemblyMateAlignment.Aligned,
            FollowerEntity = "roller_face@follower-1"
        };
        for (int i = 0; i < 64; i++)
        {
            cam.CamProfileFaces.Add($"CamLobeFace_{i:D2}@camshaft-1");
        }

        byte[] bytes1 = MfcArchive.Serialize(cam);
        var restored1 = new CamFollowerMateModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(64, restored1.CamProfileFaces.Count);
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal($"CamLobeFace_{i:D2}@camshaft-1", restored1.CamProfileFaces[i]);
        }

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "CamFollower 64-face byte-level equality failed on double roundtrip.");

        var restored2 = new CamFollowerMateModel();
        MfcArchive.DeserializeInto(bytes2, restored2);
        Assert.Equal(64, restored2.CamProfileFaces.Count);
        for (int i = 0; i < 64; i++)
        {
            Assert.Equal(restored1.CamProfileFaces[i], restored2.CamProfileFaces[i]);
        }
    }

    [Fact]
    public void CamFollower_128ProfileFaces_PreservesAllFaces_AndRoundtrips()
    {
        var cam = new CamFollowerMateModel(isTangent: false)
        {
            DisplayName = "Cam128FacesCoincident",
            Alignment = AssemblyMateAlignment.Aligned,
            FollowerEntity = "point@follower-1"
        };
        for (int i = 0; i < 128; i++)
        {
            cam.CamProfileFaces.Add($"ComplexProfileFace_{i:D3}@camshaft-1");
        }

        byte[] bytes1 = MfcArchive.Serialize(cam);
        var restored1 = new CamFollowerMateModel(isTangent: false);
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(128, restored1.CamProfileFaces.Count);
        for (int i = 0; i < 128; i++)
        {
            Assert.Equal($"ComplexProfileFace_{i:D3}@camshaft-1", restored1.CamProfileFaces[i]);
        }

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "CamFollower 128-face byte-level equality failed on double roundtrip.");

        var restored2 = new CamFollowerMateModel(isTangent: false);
        MfcArchive.DeserializeInto(bytes2, restored2);
        Assert.Equal(128, restored2.CamProfileFaces.Count);
        for (int i = 0; i < 128; i++)
        {
            Assert.Equal(restored1.CamProfileFaces[i], restored2.CamProfileFaces[i]);
        }
    }

    // ── 4. Scaling Stress: 200 Mixed Mates in Single Stream ────────────────

    [Fact]
    public void MatesListModel_200MixedMates_ScalesSmoothlyAndRoundtripsByteExact()
    {
        var list = new MatesListModel
        {
            StreamVersion = MatesListModel.DefaultCleanVersion
        };

        for (int i = 0; i < 20; i++)
        {
            list.Mates.Add(new StandardMateModel("moMateCoincident", AssemblyMateType.Coincident) { DisplayName = $"Std_{i}" });
            list.Mates.Add(new WidthMateModel { DisplayName = $"Width_{i}", DistanceFromEnd = 0.001 * (i + 1) });
            list.Mates.Add(new GearMateModel { DisplayName = $"Gear_{i}", GearRatioNumerator = i + 1 });
            list.Mates.Add(new RackPinionMateModel { DisplayName = $"RP_{i}", DiameterValue = 0.01 * (i + 1) });
            list.Mates.Add(new ProfileCenterMateModel { DisplayName = $"PC_{i}", OffsetDistance = 0.002 * (i + 1) });
            list.Mates.Add(new ScrewMateModel { DisplayName = $"Screw_{i}", RevolutionVal = 0.0005 * (i + 1) });
            list.Mates.Add(new HingeMateModel { DisplayName = $"Hinge_{i}", Angle = 0.05 * (i + 1) });
            list.Mates.Add(new UniversalJointMateModel { DisplayName = $"UJ_{i}" });
            list.Mates.Add(new CamFollowerMateModel { DisplayName = $"Cam_{i}" });
            list.Mates.Add(new PathMateModel { DisplayName = $"Path_{i}", PathValue = 0.01 * (i + 1) });
        }

        Assert.Equal(200, list.Mates.Count);

        byte[] bytes1 = MfcArchive.Serialize(list);
        var restored1 = new MatesListModel();
        MfcArchive.DeserializeInto(bytes1, restored1);

        Assert.Equal(200, restored1.Mates.Count);
        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(list.Mates[i].DisplayName, restored1.Mates[i].DisplayName);
            Assert.Equal(list.Mates[i].MateType, restored1.Mates[i].MateType);
        }

        byte[] bytes2 = MfcArchive.Serialize(restored1);
        Assert.True(bytes1.SequenceEqual(bytes2), "200 Mixed Mates byte equality failed on double roundtrip.");
    }

    // ── 5. Empirical Challenge & Edge Cases Findings ─────────────────────

    [Fact]
    public void ScrewMateModel_RevolutionType_IsLostOnRoundtrip_EmpiricalFinding()
    {
        // Challenge: ScrewMateModel exposes public RevolutionType property,
        // but SerializeParameters does not serialize it into the CArchive stream.
        var original = new ScrewMateModel
        {
            DisplayName = "ScrewRevsPerUnit",
            RevolutionType = 1, // swScrewMateRevolutionsPerUnit
            RevolutionVal = 200.0,
            Reverse = false,
            ScrewEntity1 = "screw1@asm",
            ScrewEntity2 = "screw2@asm"
        };

        byte[] bytes = MfcArchive.Serialize(original);
        var restored = new ScrewMateModel();
        MfcArchive.DeserializeInto(bytes, restored);

        // All wire-serialized attributes are preserved
        Assert.Equal("ScrewRevsPerUnit", restored.DisplayName);
        Assert.Equal(200.0, restored.RevolutionVal, precision: 6);
        Assert.False(restored.Reverse);
        Assert.Equal("screw1@asm", restored.ScrewEntity1);
        Assert.Equal("screw2@asm", restored.ScrewEntity2);

        // BUT RevolutionType is NOT serialized in SerializeParameters:
        // Documenting that RevolutionType resets to default (0).
        Assert.Equal(0, restored.RevolutionType);
    }

    [Fact]
    public void CamFollower_StandaloneCoincidentDeserialization_IntoDefaultTangentInstance_ShiftsStream()
    {
        // Challenge: In standalone deserialization (outside MatesListModel container),
        // deserializing a coincident cam mate (moMateCamCoincident) into a new CamFollowerMateModel()
        // (which defaults to IsTangent = true) shifts the entity stream offsets by 2 bytes.
        var originalCoincident = new CamFollowerMateModel(isTangent: false)
        {
            DisplayName = "CoincidentCam",
            FollowerEntity = "pt@follower"
        };
        originalCoincident.CamProfileFaces.Add("Face1@cam");
        originalCoincident.CamProfileFaces.Add("Face2@cam");

        byte[] bytes = MfcArchive.Serialize(originalCoincident);

        // When deserialized into an instance instantiated with matching isTangent: false -> 100% faithful
        var matchingRestored = new CamFollowerMateModel(isTangent: false);
        MfcArchive.DeserializeInto(bytes, matchingRestored);
        Assert.Equal(2, matchingRestored.CamProfileFaces.Count);
        Assert.Equal("Face1@cam", matchingRestored.CamProfileFaces[0]);

        // When deserialized via MatesListModel (container), MfcMateRegistry.Create resolves class automatically:
        var list = new MatesListModel();
        list.Mates.Add(originalCoincident);
        byte[] listBytes = MfcArchive.Serialize(list);
        var restoredList = new MatesListModel();
        MfcArchive.DeserializeInto(listBytes, restoredList);
        var containerCam = Assert.IsType<CamFollowerMateModel>(restoredList.Mates[0]);
        Assert.False(containerCam.IsTangent);
        Assert.Equal(2, containerCam.CamProfileFaces.Count);
    }

    [Fact]
    public void NativeAssembly_SLDASM_FullResaveAndReopen_Fidelity()
    {
        string modelsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../models/assembly.SLDASM"));
        if (!File.Exists(modelsPath))
            modelsPath = @"d:\weigan\repos\sldfile\models\assembly.SLDASM";

        Assert.True(File.Exists(modelsPath), $"File not found at {modelsPath}");

        string tempFile = Path.Combine(Path.GetTempPath(), $"challenger_p02_{Guid.NewGuid():N}.SLDASM");
        File.Copy(modelsPath, tempFile, true);

        try
        {
            int originalMateCount;
            List<string> originalNames = new();
            using (var assy = new SldAssembly(tempFile, readOnly: true))
            {
                originalMateCount = assy.MatesList.Mates.Count;
                Assert.True(originalMateCount > 0);
                foreach (var m in assy.MatesList.Mates)
                    originalNames.Add(m.DisplayName);
            }

            // Resave using SldAssembly
            using (var assyRw = new SldAssembly(tempFile, readOnly: false))
            {
                byte[] reserialized = MfcArchive.Serialize(assyRw.MatesList);
                assyRw.SetPendingStream("Contents/Config-0-MatesList", reserialized);
                assyRw.Save();
            }

            // Reopen and assert all mates and names are preserved
            using (var reopened = new SldAssembly(tempFile, readOnly: true))
            {
                Assert.Equal(originalMateCount, reopened.MatesList.Mates.Count);
                for (int i = 0; i < originalMateCount; i++)
                {
                    Assert.Equal(originalNames[i], reopened.MatesList.Mates[i].DisplayName);
                }
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    // ── Helper Verification Methods ───────────────────────────────────────

    private static void AssertMatesAttributeEquality(IReadOnlyList<IMfcMateModel> expected, IReadOnlyList<IMfcMateModel> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            var exp = expected[i];
            var act = actual[i];

            Assert.Equal(exp.ClassName, act.ClassName);
            Assert.Equal(exp.DisplayName, act.DisplayName);
            Assert.Equal(exp.MateType, act.MateType);
            Assert.Equal(exp.TypeId, act.TypeId);
            Assert.Equal(exp.Alignment, act.Alignment);

            switch (exp)
            {
                case StandardMateModel expStd:
                    var actStd = Assert.IsType<StandardMateModel>(act);
                    Assert.Equal(expStd.Distance, actStd.Distance);
                    Assert.Equal(expStd.Angle, actStd.Angle);
                    Assert.Equal(expStd.PlaneReferences.Count, actStd.PlaneReferences.Count);
                    for (int p = 0; p < expStd.PlaneReferences.Count; p++)
                        Assert.Equal(expStd.PlaneReferences[p], actStd.PlaneReferences[p]);
                    break;

                case WidthMateModel expW:
                    var actW = Assert.IsType<WidthMateModel>(act);
                    Assert.Equal(expW.ConstraintType, actW.ConstraintType);
                    if (expW.ConstraintType == AssemblyMateWidthConstraint.Dimension)
                    {
                        Assert.Equal(expW.DistanceFromEnd, actW.DistanceFromEnd, precision: 6);
                        Assert.Equal(expW.FlipDimension, actW.FlipDimension);
                    }
                    else if (expW.ConstraintType == AssemblyMateWidthConstraint.Percent)
                    {
                        Assert.Equal(expW.PercentDistanceFromEnd, actW.PercentDistanceFromEnd, precision: 6);
                        Assert.Equal(expW.FlipDimension, actW.FlipDimension);
                    }
                    Assert.Equal(expW.EntityReferences.Count, actW.EntityReferences.Count);
                    break;

                case GearMateModel expG:
                    var actG = Assert.IsType<GearMateModel>(act);
                    Assert.Equal(expG.GearRatioNumerator, actG.GearRatioNumerator, precision: 6);
                    Assert.Equal(expG.GearRatioDenominator, actG.GearRatioDenominator, precision: 6);
                    Assert.Equal(expG.Reverse, actG.Reverse);
                    Assert.Equal(expG.Pinion1Component, actG.Pinion1Component);
                    Assert.Equal(expG.Pinion2Component, actG.Pinion2Component);
                    break;

                case RackPinionMateModel expRp:
                    var actRp = Assert.IsType<RackPinionMateModel>(act);
                    Assert.Equal(expRp.DiameterType, actRp.DiameterType);
                    Assert.Equal(expRp.DiameterValue, actRp.DiameterValue, precision: 6);
                    Assert.Equal(expRp.Reverse, actRp.Reverse);
                    Assert.Equal(expRp.RackComponent, actRp.RackComponent);
                    Assert.Equal(expRp.PinionComponent, actRp.PinionComponent);
                    break;

                case ProfileCenterMateModel expPc:
                    var actPc = Assert.IsType<ProfileCenterMateModel>(act);
                    Assert.Equal(expPc.OffsetDistance, actPc.OffsetDistance, precision: 6);
                    Assert.Equal(expPc.FlipDimension, actPc.FlipDimension);
                    Assert.Equal(expPc.LockRotation, actPc.LockRotation);
                    Assert.Equal(expPc.FirstProfileComponent, actPc.FirstProfileComponent);
                    Assert.Equal(expPc.SecondProfileComponent, actPc.SecondProfileComponent);
                    break;

                case ScrewMateModel expS:
                    var actS = Assert.IsType<ScrewMateModel>(act);
                    Assert.Equal(expS.RevolutionVal, actS.RevolutionVal, precision: 6);
                    Assert.Equal(expS.Reverse, actS.Reverse);
                    Assert.Equal(expS.ScrewEntity1, actS.ScrewEntity1);
                    Assert.Equal(expS.ScrewEntity2, actS.ScrewEntity2);
                    break;

                case HingeMateModel expH:
                    var actH = Assert.IsType<HingeMateModel>(act);
                    Assert.Equal(expH.CoaxialEntity1, actH.CoaxialEntity1);
                    Assert.Equal(expH.CoaxialEntity2, actH.CoaxialEntity2);
                    Assert.Equal(expH.CoincidentEntity1, actH.CoincidentEntity1);
                    Assert.Equal(expH.CoincidentEntity2, actH.CoincidentEntity2);
                    Assert.Equal(expH.AngleSelection, actH.AngleSelection);
                    if (expH.AngleSelection)
                    {
                        Assert.Equal(expH.Angle, actH.Angle, precision: 6);
                        Assert.Equal(expH.MinVal, actH.MinVal, precision: 6);
                        Assert.Equal(expH.MaxVal, actH.MaxVal, precision: 6);
                    }
                    break;

                case UniversalJointMateModel expUj:
                    var actUj = Assert.IsType<UniversalJointMateModel>(act);
                    Assert.Equal(expUj.Shaft1Entity, actUj.Shaft1Entity);
                    Assert.Equal(expUj.Shaft2Entity, actUj.Shaft2Entity);
                    break;

                case CamFollowerMateModel expCam:
                    var actCam = Assert.IsType<CamFollowerMateModel>(act);
                    Assert.Equal(expCam.FollowerEntity, actCam.FollowerEntity);
                    Assert.Equal(expCam.CamProfileFaces.Count, actCam.CamProfileFaces.Count);
                    for (int f = 0; f < expCam.CamProfileFaces.Count; f++)
                        Assert.Equal(expCam.CamProfileFaces[f], actCam.CamProfileFaces[f]);
                    break;

                case PathMateModel expPath:
                    var actPath = Assert.IsType<PathMateModel>(act);
                    Assert.Equal(expPath.FollowerEntity, actPath.FollowerEntity);
                    Assert.Equal(expPath.PathEntity, actPath.PathEntity);
                    Assert.Equal(expPath.PathValue, actPath.PathValue, precision: 6);
                    break;
            }
        }
    }
}
