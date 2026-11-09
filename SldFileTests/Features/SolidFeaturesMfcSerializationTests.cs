using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Features;

/// <summary>
/// Comprehensive verification suite demonstrating that all Milestone P1 solid features
/// (MfcSketchCurveReference, Sweep, Loft, Shell, Draft, CombineBodies, SplitBody, DeleteBody, Rib)
/// uniformly implement and roundtrip via the IMfcSerializable contract with exact attribute retention
/// and double-roundtrip byte equality.
/// </summary>
public class SolidFeaturesMfcSerializationTests
{
    private static byte[] SerializeModel(IMfcSerializable model, int docVersion = 0)
    {
        var ctx = new MfcArchiveContext { DocumentVersion = docVersion };
        using var writer = MfcArchive.CreateWriter(context: ctx);
        model.Serialize(writer);
        return writer.ToArray();
    }

    private static T DeserializeModel<T>(byte[] data, int docVersion = 0) where T : IMfcSerializable, new()
    {
        var ctx = new MfcArchiveContext { DocumentVersion = docVersion };
        using var reader = MfcArchive.CreateReader(data, context: ctx);
        var model = new T();
        model.Serialize(reader);
        return model;
    }

    [Fact]
    public void MfcSketchCurveReference_52ByteWireFormat_RoundtripsAccurately()
    {
        var original = new MfcSketchCurveReference
        {
            EntityId = 42,
            ArchiveSerial = 3,
            Flags = 0x0100,
            RoleTag = 0x4A38,
            Opaque28 = 0xFFFFFFFF
        };

        byte[] b1 = SerializeModel(original);
        Assert.Equal(52, b1.Length);

        var restored = DeserializeModel<MfcSketchCurveReference>(b1);
        Assert.NotNull(restored);
        Assert.Equal(42u, restored.EntityId);
        Assert.Equal(3u, restored.ArchiveSerial);
        Assert.Equal(0x0100u, restored.Flags);
        Assert.Equal(0x4A38u, restored.RoleTag);
        Assert.Equal(0xFFFFFFFFu, restored.Opaque28);

        // Double roundtrip byte equality
        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void MfcSketchCurveReference_NullPlaceholder_RoundtripsCorrectly()
    {
        using var writer = MfcArchive.CreateWriter();
        MfcSketchCurveReference.WriteNullPlaceholder(writer);
        byte[] bytes = writer.ToArray();
        Assert.Equal(52, bytes.Length);

        using var reader = MfcArchive.CreateReader(bytes);
        MfcSketchCurveReference.ReadNullPlaceholder(reader);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void SweepFeatureModel_StandardProfileAndPath_DoubleRoundtripByteEquality()
    {
        var original = new SweepFeatureModel
        {
            FeatureName = "SweepBoss_Test",
            IsCut = false,
            ProfileReference = new MfcSketchCurveReference(10, SketchCurveRole.Profile, 0),
            PathReference = new MfcSketchCurveReference(11, SketchCurveRole.Path, 1),
            Merge = true,
            PathAlignmentType = 0,
            TwistControlType = 1,
            TwistAngleRad = 0.785398 // 45 deg
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var reloaded = DeserializeModel<SweepFeatureModel>(b1);
        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.False(reloaded.IsCut);
        Assert.True(reloaded.Merge);
        Assert.Equal(1, reloaded.TwistControlType);
        Assert.NotNull(reloaded.TwistAngleRad);
        Assert.Equal(original.TwistAngleRad.Value, reloaded.TwistAngleRad.Value, precision: 5);

        // Double roundtrip: re-serialize deserialized model -> bytes must match
        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void SweepFeatureModel_CircularProfileAndThinWall_DoubleRoundtripByteEquality()
    {
        var original = new SweepFeatureModel
        {
            FeatureName = "CircularSweep_Thin",
            CircularProfile = true,
            CircularProfileDiameter = 0.025, // 25mm
            IsThinFeature = true,
            WallThickness = 0.002, // 2mm
            WallThicknessDirection2 = 0.001,
            ThinWallType = 1,
            PathReference = new MfcSketchCurveReference(20, SketchCurveRole.Path, 0)
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<SweepFeatureModel>(b1);

        Assert.True(reloaded.CircularProfile);
        Assert.Equal(0.025, reloaded.CircularProfileDiameter!.Value, precision: 5);
        Assert.True(reloaded.IsThinFeature);
        Assert.Equal(0.002, reloaded.WallThickness!.Value, precision: 5);
        Assert.Equal(0.001, reloaded.WallThicknessDirection2, precision: 5);
        Assert.Equal(1u, reloaded.ThinWallType);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void LoftFeatureModel_MultiProfilesAndCenterline_DoubleRoundtripByteEquality()
    {
        var original = new LoftFeatureModel
        {
            FeatureName = "LoftBoss_Test",
            IsCut = false,
            Centerline = new SketchCurveReference { EntityId = 5, Role = SketchCurveRole.Centerline },
            StartTangencyType = 1,
            EndTangencyType = 2
        };
        original.ProfileReferences.Add(new MfcSketchCurveReference(1, SketchCurveRole.Profile, 0));
        original.ProfileReferences.Add(new MfcSketchCurveReference(2, SketchCurveRole.Profile, 1));
        original.ProfileReferences.Add(new MfcSketchCurveReference(3, SketchCurveRole.Profile, 2));

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<LoftFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.False(reloaded.IsCut);
        Assert.Equal(3, reloaded.ProfileReferences.Count);
        Assert.NotNull(reloaded.Centerline);
        Assert.Equal(5u, reloaded.Centerline.EntityId);
        Assert.Equal(1, reloaded.StartTangencyType);
        Assert.Equal(2, reloaded.EndTangencyType);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void ShellFeatureModel_SynchronizedThicknessSlots_DoubleRoundtripByteEquality()
    {
        var original = new ShellFeatureModel
        {
            FeatureName = "Shell_Test",
            Thickness = 0.0035, // 3.5mm
            Direction = 1, // Outward
            FacesRemoved = new[]
            {
                new EntityReference { EntityId = 101, EntityType = SelectionEntityType.Face },
                new EntityReference { EntityId = 102, EntityType = SelectionEntityType.Face }
            }
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<ShellFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(0.0035, reloaded.Thickness!.Value, precision: 5);
        Assert.Equal(1, reloaded.Direction);
        Assert.Equal(2, reloaded.FacesRemovedCount);
        Assert.Equal(101u, reloaded.FacesRemoved![0].EntityId);
        Assert.Equal(102u, reloaded.FacesRemoved![1].EntityId);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void DraftFeatureModel_AngleParameterAndDuplicateSlot_DoubleRoundtripByteEquality()
    {
        double angleRad = 5.0 * Math.PI / 180.0; // 5 degrees
        var original = new DraftFeatureModel
        {
            FeatureName = "Draft_Test",
            Angle = angleRad,
            Type = 0, // Neutral plane
            ReverseDirection = true,
            NeutralPlane = new EntityReference { EntityId = 55, EntityType = SelectionEntityType.Face },
            FacesToDraft = new[]
            {
                new EntityReference { EntityId = 60, EntityType = SelectionEntityType.Face }
            }
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<DraftFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(angleRad, reloaded.Angle!.Value, precision: 5);
        Assert.True(reloaded.ReverseDirection);
        Assert.NotNull(reloaded.NeutralPlane);
        Assert.Equal(55u, reloaded.NeutralPlane.EntityId);
        Assert.Single(reloaded.FacesToDraft!);
        Assert.Equal(60u, reloaded.FacesToDraft![0].EntityId);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void CombineBodiesFeatureModel_AddSubtractCommon_DoubleRoundtripByteEquality()
    {
        var original = new CombineBodiesFeatureModel
        {
            FeatureName = "Combine_Subtract",
            OperationType = 1, // Subtract
            MainBody = new EntityReference { EntityId = 200, EntityType = SelectionEntityType.Body },
            BodiesToCombine = new[]
            {
                new EntityReference { EntityId = 201, EntityType = SelectionEntityType.Body },
                new EntityReference { EntityId = 202, EntityType = SelectionEntityType.Body }
            }
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<CombineBodiesFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(1, reloaded.OperationType);
        Assert.Equal(2, reloaded.BodiesToCombineCount);
        Assert.Equal(201u, reloaded.BodiesToCombine![0].EntityId);
        Assert.Equal(202u, reloaded.BodiesToCombine![1].EntityId);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void SplitBodyFeatureModel_ConsumeAndTrimTools_DoubleRoundtripByteEquality()
    {
        var original = new SplitBodyFeatureModel
        {
            FeatureName = "Split_Consume",
            Consume = false,
            State = 0,
            TrimTools = new[]
            {
                new EntityReference { EntityId = 301, EntityType = SelectionEntityType.Face },
                new EntityReference { EntityId = 302, EntityType = SelectionEntityType.Face }
            },
            TemplatePath = "C:\\Template\\Part.prtdot",
            OverrideDefaultTemplateSettings = true
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<SplitBodyFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.False(reloaded.Consume);
        Assert.Equal(0, reloaded.State);
        Assert.Equal(2, reloaded.TrimToolsCount);
        Assert.Equal(301u, reloaded.TrimTools![0].EntityId);
        Assert.Equal(302u, reloaded.TrimTools![1].EntityId);
        Assert.Equal("C:\\Template\\Part.prtdot", reloaded.TemplatePath);
        Assert.True(reloaded.OverrideDefaultTemplateSettings);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void DeleteBodyFeatureModel_KeepAndBodiesList_DoubleRoundtripByteEquality()
    {
        var original = new DeleteBodyFeatureModel
        {
            FeatureName = "DeleteBody_Multi",
            KeepBodies = true,
            Bodies = new[]
            {
                new EntityReference { EntityId = 401, EntityType = SelectionEntityType.Body },
                new EntityReference { EntityId = 402, EntityType = SelectionEntityType.Body }
            }
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<DeleteBodyFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.True(reloaded.KeepBodies);
        Assert.Equal(2, reloaded.BodiesCount);
        Assert.Equal(401u, reloaded.Bodies![0].EntityId);
        Assert.Equal(402u, reloaded.Bodies![1].EntityId);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void RibFeatureModel_ParametersAndBitfieldOptions_DoubleRoundtripByteEquality()
    {
        var original = new RibFeatureModel
        {
            FeatureName = "Rib_Drafted",
            Thickness = 0.004, // 4mm
            DraftAngle = 0.05236, // 3 deg
            EnableDraft = true,
            DraftOutward = true,
            DraftFromWall = false,
            IsTwoSided = true,
            ExtrusionDirection = 1,
            FlipSide = true,
            RefSketchIndex = 2
        };

        byte[] b1 = SerializeModel(original);
        var reloaded = DeserializeModel<RibFeatureModel>(b1);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(0.004, reloaded.Thickness!.Value, precision: 5);
        Assert.Equal(0.05236, reloaded.DraftAngle!.Value, precision: 5);
        Assert.True(reloaded.EnableDraft);
        Assert.True(reloaded.DraftOutward);
        Assert.False(reloaded.DraftFromWall);
        Assert.True(reloaded.IsTwoSided);
        Assert.Equal(1, reloaded.ExtrusionDirection);
        Assert.True(reloaded.FlipSide);
        Assert.Equal(2, reloaded.RefSketchIndex);

        byte[] b2 = SerializeModel(reloaded);
        Assert.Equal(b1, b2);
    }
}
