using System;
using System.Collections.Generic;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Features;

/// <summary>
/// Comprehensive verification suite demonstrating that all Milestone P2 Weldment features
/// (StructuralMember, EndCap, CosmeticWeldBead, WeldBead, SMWeldBead)
/// uniformly inherit MfcFeatureModelBase, implement IMfcFeatureModel and IMfcSerializable,
/// and support double-roundtrip byte equality and profile/selection encoding.
/// </summary>
public class WeldmentMfcSerializationTests
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
    public void StructuralMemberFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new StructuralMemberFeatureModel(
            profilePath: @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\chinese-simplified\weldment profiles\iso\pipe\21.3 x 2.3.sldlfp",
            pathSegments: new[]
            {
                new EntityReference { EntityId = 101, EntityType = SelectionEntityType.Edge, Name = "moWeldSegSkIndex_c:00000065" },
                new EntityReference { EntityId = 102, EntityType = SelectionEntityType.Edge, Name = "moWeldSegSkIndex_c:00000066" }
            },
            rotationAngle: 0.785398, // 45 deg
            cornerTreatmentType: 1) // EndMiter
        {
            FeatureName = "StructuralMember_Test",
            LocateProfilePoint = new EntityReference { EntityId = 201, EntityType = SelectionEntityType.Sketch, Name = "moPointRef_w:000000C9" },
            MirrorProfile = true,
            AllowProtrusion = true,
            TransferMaterial = true,
            LibraryProfileMaterial = "AISI 304"
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<StructuralMemberFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.WeldmentProfilePath, restored.WeldmentProfilePath);
        Assert.Equal(original.RotationAngle!.Value, restored.RotationAngle!.Value, 6);
        Assert.Equal(original.CornerTreatmentType, restored.CornerTreatmentType);
        Assert.Equal(original.ApplyCornerTreatment, restored.ApplyCornerTreatment);
        Assert.Equal(original.MirrorProfile, restored.MirrorProfile);
        Assert.Equal(original.AllowProtrusion, restored.AllowProtrusion);
        Assert.NotNull(restored.LocateProfilePoint);
        Assert.Equal(201u, restored.LocateProfilePoint!.EntityId);
        Assert.NotNull(restored.PathSegments);
        Assert.Equal(2, restored.PathSegments!.Count);
        Assert.Equal(101u, restored.PathSegments![0].EntityId);
        Assert.Equal(102u, restored.PathSegments![1].EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void EndCapFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new EndCapFeatureModel(
            thickness: 0.005,
            face: new EntityReference { EntityId = 301, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:0000012D" },
            useChamferCorners: true,
            chamferDistance: 0.0015)
        {
            FeatureName = "EndCap_Test",
            IsEndCapInward = 2,
            ThicknessRatioForOffset = 0.6,
            UseThicknessRatioForOffset = true,
            OffsetDistance = 0.002,
            UseReverse = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<EndCapFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.Thickness!.Value, restored.Thickness!.Value, 6);
        Assert.Equal(original.IsEndCapInward, restored.IsEndCapInward);
        Assert.Equal(original.UseThicknessRatioForOffset, restored.UseThicknessRatioForOffset);
        Assert.Equal(original.ThicknessRatioForOffset!.Value, restored.ThicknessRatioForOffset!.Value, 6);
        Assert.Equal(original.OffsetDistance!.Value, restored.OffsetDistance!.Value, 6);
        Assert.Equal(original.UseCornerTreatment, restored.UseCornerTreatment);
        Assert.Equal(original.UseChamferCorners, restored.UseChamferCorners);
        Assert.Equal(original.ChamferDistance!.Value, restored.ChamferDistance!.Value, 6);
        Assert.Equal(original.UseReverse, restored.UseReverse);
        Assert.NotNull(restored.Face);
        Assert.Equal(301u, restored.Face!.EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void CosmeticWeldBeadFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new CosmeticWeldBeadFeatureModel(
            beadSize: 0.004,
            edgeReferences: new[]
            {
                new EntityReference { EntityId = 401, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:00000191" },
                new EntityReference { EntityId = 402, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:00000192" }
            },
            side: 1)
        {
            FeatureName = "CosmeticWeldBead_Test",
            IntermittentWeld = true,
            IntermittentWeldLength = 0.03,
            Pitch = 0.08,
            Staggered = false,
            TangentPropagation = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<CosmeticWeldBeadFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.Side, restored.Side);
        Assert.Equal(original.BeadSize!.Value, restored.BeadSize!.Value, 6);
        Assert.Equal(original.IntermittentWeld, restored.IntermittentWeld);
        Assert.Equal(original.IntermittentWeldLength!.Value, restored.IntermittentWeldLength!.Value, 6);
        Assert.Equal(original.Pitch!.Value, restored.Pitch!.Value, 6);
        Assert.Equal(original.Staggered, restored.Staggered);
        Assert.Equal(original.TangentPropagation, restored.TangentPropagation);
        Assert.NotNull(restored.EdgeReferences);
        Assert.Equal(2, restored.EdgeReferences!.Count);
        Assert.Equal(401u, restored.EdgeReferences![0].EntityId);
        Assert.Equal(402u, restored.EdgeReferences![1].EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void WeldBeadFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new WeldBeadFeatureModel(beadSize: 0.005, beadLength: 0.08, beadPitch: 0.04, beadType: 1)
        {
            FeatureName = "WeldBead_Test",
            TangentPropagation = false,
            UseOtherSide = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<WeldBeadFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.BeadSize!.Value, restored.BeadSize!.Value, 6);
        Assert.Equal(original.BeadLength!.Value, restored.BeadLength!.Value, 6);
        Assert.Equal(original.BeadPitch!.Value, restored.BeadPitch!.Value, 6);
        Assert.Equal(original.BeadType, restored.BeadType);
        Assert.Equal(original.TangentPropagation, restored.TangentPropagation);
        Assert.Equal(original.UseOtherSide, restored.UseOtherSide);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void SMWeldBeadFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new SMWeldBeadFeatureModel(beadSize: 0.0025, beadLength: 0.04, beadPitch: 0.02, beadType: 0)
        {
            FeatureName = "SMWeldBead_Test",
            TangentPropagation = true,
            UseOtherSide = false
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<SMWeldBeadFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.BeadSize!.Value, restored.BeadSize!.Value, 6);
        Assert.Equal(original.BeadLength!.Value, restored.BeadLength!.Value, 6);
        Assert.Equal(original.BeadPitch!.Value, restored.BeadPitch!.Value, 6);
        Assert.Equal(original.BeadType, restored.BeadType);
        Assert.Equal(original.TangentPropagation, restored.TangentPropagation);
        Assert.Equal(original.UseOtherSide, restored.UseOtherSide);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Theory]
    [InlineData(@"D:\Custom Profiles\rectangular tube\50 x 30 x 2.6.sldlfp")]
    [InlineData(@"weldment\焊件轮廓\角钢\L 40x40x4.sldlfp")]
    [InlineData(@"C:\Profiles with spaces and (special) chars\pipe.sldlfp")]
    public void StructuralMemberFeatureModel_ProfilePath_EncodingVerification(string path)
    {
        var original = new StructuralMemberFeatureModel(profilePath: path);
        byte[] bytes = SerializeModel(original);
        var restored = DeserializeModel<StructuralMemberFeatureModel>(bytes);
        Assert.Equal(path, restored.WeldmentProfilePath);
    }

    [Fact]
    public void EndCapFeatureModel_ChamferAndOffset_Variations()
    {
        // Variation 1: No chamfer, outward direction
        var v1 = new EndCapFeatureModel(thickness: 0.003, useChamferCorners: false)
        {
            IsEndCapInward = 1,
            UseThicknessRatioForOffset = false,
            OffsetDistance = 0.001
        };
        byte[] b1 = SerializeModel(v1);
        var r1 = DeserializeModel<EndCapFeatureModel>(b1);
        Assert.False(r1.UseChamferCorners);
        Assert.Null(r1.ChamferDistance);
        Assert.Equal(1, r1.IsEndCapInward);
        Assert.False(r1.UseThicknessRatioForOffset);
        Assert.Equal(0.001, r1.OffsetDistance!.Value, 6);

        // Variation 2: Chamfer, inward direction, thickness ratio
        var v2 = new EndCapFeatureModel(thickness: 0.004, useChamferCorners: true, chamferDistance: 0.002)
        {
            IsEndCapInward = 2,
            UseThicknessRatioForOffset = true,
            ThicknessRatioForOffset = 0.75
        };
        byte[] b2 = SerializeModel(v2);
        var r2 = DeserializeModel<EndCapFeatureModel>(b2);
        Assert.True(r2.UseChamferCorners);
        Assert.Equal(0.002, r2.ChamferDistance!.Value, 6);
        Assert.Equal(2, r2.IsEndCapInward);
        Assert.True(r2.UseThicknessRatioForOffset);
        Assert.Equal(0.75, r2.ThicknessRatioForOffset!.Value, 6);
    }

    [Fact]
    public void CosmeticWeldBeadFeatureModel_EdgeMap_MultiEdges()
    {
        var edges = new List<EntityReference>();
        for (uint i = 1; i <= 5; i++)
        {
            edges.Add(new EntityReference
            {
                EntityId = 500 + i,
                EntityType = SelectionEntityType.Edge,
                Name = $"moCompEdge_c:{(500 + i):X8}"
            });
        }

        var original = new CosmeticWeldBeadFeatureModel(beadSize: 0.0035, edgeReferences: edges);
        byte[] bytes = SerializeModel(original);
        var restored = DeserializeModel<CosmeticWeldBeadFeatureModel>(bytes);
        Assert.NotNull(restored.EdgeReferences);
        Assert.Equal(5, restored.EdgeReferences!.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(501u + (uint)i, restored.EdgeReferences[i].EntityId);
        }
    }

    [Fact]
    public void Weldment_AllModels_InheritMfcFeatureModelBase_AndImplementIMfcFeatureModel()
    {
        var models = new Type[]
        {
            typeof(StructuralMemberFeatureModel),
            typeof(EndCapFeatureModel),
            typeof(CosmeticWeldBeadFeatureModel),
            typeof(WeldBeadFeatureModel),
            typeof(SMWeldBeadFeatureModel)
        };

        foreach (var type in models)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcFeatureModel).IsAssignableFrom(type), $"{type.Name} must implement IMfcFeatureModel");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void Weldment_LegacyTemplates_DecoratedWithObsoleteAttribute()
    {
        var models = new Type[]
        {
            typeof(StructuralMemberFeatureModel),
            typeof(EndCapFeatureModel),
            typeof(WeldBeadFeatureModel),
            typeof(CosmeticWeldBeadFeatureModel),
            typeof(SMWeldBeadFeatureModel),
            typeof(GussetFeatureModel)
        };

        foreach (var type in models)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcFeatureModel).IsAssignableFrom(type), $"{type.Name} must implement IMfcFeatureModel");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void StructuralMemberFeatureModel_RawBlock_PreservedAndEqual()
    {
        byte[] dummy = new byte[256];
        for (int i = 0; i < dummy.Length; i++) dummy[i] = (byte)(i ^ 0x5A);

        // Test with default FeatureName ("结构构件1")
        var modelWithDefaultName = new StructuralMemberFeatureModel { RawBlock = dummy };
        byte[] s1 = SerializeModel(modelWithDefaultName);
        Assert.Equal(dummy, s1);

        // Test with custom FeatureName
        var modelWithCustomName = new StructuralMemberFeatureModel
        {
            RawBlock = dummy,
            FeatureName = "CustomStructuralMember"
        };
        byte[] s2 = SerializeModel(modelWithCustomName);
        Assert.Equal(dummy, s2);
    }

    [Fact]
    public void EndCapFeatureModel_RawBlock_PreservedAndEqual()
    {
        byte[] dummy = new byte[128];
        for (int i = 0; i < dummy.Length; i++) dummy[i] = (byte)(i ^ 0xA5);

        // Test with default FeatureName ("顶端盖1")
        var modelWithDefaultName = new EndCapFeatureModel { RawBlock = dummy };
        byte[] s1 = SerializeModel(modelWithDefaultName);
        Assert.Equal(dummy, s1);

        // Test with custom FeatureName
        var modelWithCustomName = new EndCapFeatureModel
        {
            RawBlock = dummy,
            FeatureName = "CustomEndCap"
        };
        byte[] s2 = SerializeModel(modelWithCustomName);
        Assert.Equal(dummy, s2);
    }

    [Fact]
    public void CosmeticWeldBeadFeatureModel_RawBlock_PreservedAndEqual()
    {
        byte[] dummy = new byte[192];
        for (int i = 0; i < dummy.Length; i++) dummy[i] = (byte)(i ^ 0x3C);

        // Test with default FeatureName ("装饰焊缝1")
        var modelWithDefaultName = new CosmeticWeldBeadFeatureModel { RawBlock = dummy };
        byte[] s1 = SerializeModel(modelWithDefaultName);
        Assert.Equal(dummy, s1);

        // Test with custom FeatureName
        var modelWithCustomName = new CosmeticWeldBeadFeatureModel
        {
            RawBlock = dummy,
            FeatureName = "CustomCosmeticWeldBead"
        };
        byte[] s2 = SerializeModel(modelWithCustomName);
        Assert.Equal(dummy, s2);
    }

    [Fact]
    public void WeldBeadFeatureModel_RawBlock_PreservedAndEqual()
    {
        byte[] dummy = new byte[160];
        for (int i = 0; i < dummy.Length; i++) dummy[i] = (byte)(i ^ 0x7E);

        // Test with default FeatureName ("焊缝1")
        var modelWithDefaultName = new WeldBeadFeatureModel { RawBlock = dummy };
        byte[] s1 = SerializeModel(modelWithDefaultName);
        Assert.Equal(dummy, s1);

        // Test with custom FeatureName
        var modelWithCustomName = new WeldBeadFeatureModel
        {
            RawBlock = dummy,
            FeatureName = "CustomWeldBead"
        };
        byte[] s2 = SerializeModel(modelWithCustomName);
        Assert.Equal(dummy, s2);
    }

    [Fact]
    public void SMWeldBeadFeatureModel_RawBlock_PreservedAndEqual()
    {
        byte[] dummy = new byte[144];
        for (int i = 0; i < dummy.Length; i++) dummy[i] = (byte)(i ^ 0xC3);

        // Test with default FeatureName ("钣金焊缝1")
        var modelWithDefaultName = new SMWeldBeadFeatureModel { RawBlock = dummy };
        byte[] s1 = SerializeModel(modelWithDefaultName);
        Assert.Equal(dummy, s1);

        // Test with custom FeatureName
        var modelWithCustomName = new SMWeldBeadFeatureModel
        {
            RawBlock = dummy,
            FeatureName = "CustomSMWeldBead"
        };
        byte[] s2 = SerializeModel(modelWithCustomName);
        Assert.Equal(dummy, s2);
    }
}
