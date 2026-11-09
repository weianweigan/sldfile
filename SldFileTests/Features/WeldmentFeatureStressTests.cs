using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Features;

/// <summary>
/// Empirical adversarial stress test harness executed by Challenger P2.2.
/// Evaluates all 5 Weldment feature models:
///   1. StructuralMemberFeatureModel (moWeldMemberFeat_c, TypeId 0x4D)
///   2. EndCapFeatureModel (moEndCap_c, TypeId 0x51)
///   3. WeldBeadFeatureModel (moWeldBeadFeat_c, TypeId 0x53)
///   4. CosmeticWeldBeadFeatureModel (moCosmeticWeldBead_c, TypeId 0x50)
///   5. SMWeldBeadFeatureModel (moSMWeldBead_c, TypeId 0x52)
/// 
/// Stress dimensions:
/// - Bit-exact double roundtrip serialization (Serialize -> MemoryStream -> Deserialize -> MemoryStream -> compare byte arrays and properties)
/// - Triple roundtrip convergence
/// - Extreme boundary inputs (long paths, unicode, extreme angles, zero/negative thickness, large collections)
/// - Property mutation stability
/// - RawBlock fallback & passthrough behavior
/// - MfcFeatureModelBase compliance
/// </summary>
public class WeldmentFeatureStressTests
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

    private static (T Reloaded, byte[] B1, byte[] B2) ExecuteDoubleRoundtrip<T>(T original, int docVersion = 0) where T : IMfcSerializable, new()
    {
        byte[] b1 = SerializeModel(original, docVersion);
        var reloaded = DeserializeModel<T>(b1, docVersion);
        byte[] b2 = SerializeModel(reloaded, docVersion);
        return (reloaded, b1, b2);
    }

    // =========================================================================
    // 1. StructuralMemberFeatureModel Empirical Stress Tests
    // =========================================================================

    [Fact]
    public void StructuralMember_FullyPopulated_TripleRoundtripBitExact()
    {
        var original = new StructuralMemberFeatureModel(
            profilePath: @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\chinese-simplified\weldment profiles\iso\pipe\48.3 x 3.2.sldlfp",
            pathSegments: new[]
            {
                new EntityReference { EntityId = 1001, EntityType = SelectionEntityType.Edge, Name = "moWeldSegSkIndex_c:000003E9" },
                new EntityReference { EntityId = 1002, EntityType = SelectionEntityType.Edge, Name = "moWeldSegSkIndex_c:000003EA" },
                new EntityReference { EntityId = 1003, EntityType = SelectionEntityType.Edge, Name = "moWeldSegSkIndex_c:000003EB" }
            },
            rotationAngle: 1.5707963267948966, // 90 deg
            cornerTreatmentType: 2) // EndButt2
        {
            FeatureName = "焊件结构构件_压力测试_01",
            LocateProfilePoint = new EntityReference { EntityId = 5555, EntityType = SelectionEntityType.Sketch, Name = "moPointRef_w:000015B3" },
            MirrorProfile = true,
            AllowProtrusion = true,
            TransferMaterial = true,
            LibraryProfileMaterial = "不锈钢 316L (UNS S31603)"
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(original.FeatureName, m2.FeatureName);
        Assert.Equal(original.WeldmentProfilePath, m2.WeldmentProfilePath);
        Assert.Equal(original.RotationAngle!.Value, m2.RotationAngle!.Value, 8);
        Assert.Equal(original.CornerTreatmentType, m2.CornerTreatmentType);
        Assert.Equal(original.ApplyCornerTreatment, m2.ApplyCornerTreatment);
        Assert.Equal(original.MirrorProfile, m2.MirrorProfile);
        Assert.Equal(original.AllowProtrusion, m2.AllowProtrusion);
        Assert.Equal(original.LibraryProfileMaterial, m2.LibraryProfileMaterial);
        Assert.NotNull(m2.LocateProfilePoint);
        Assert.Equal(5555u, m2.LocateProfilePoint!.EntityId);
        Assert.NotNull(m2.PathSegments);
        Assert.Equal(3, m2.PathSegments!.Count);
        Assert.Equal(1001u, m2.PathSegments[0].EntityId);
        Assert.Equal(1002u, m2.PathSegments[1].EntityId);
        Assert.Equal(1003u, m2.PathSegments[2].EntityId);

        // Triple roundtrip
        byte[] b3 = SerializeModel(DeserializeModel<StructuralMemberFeatureModel>(b2));
        Assert.Equal(b2, b3);
    }

    [Fact]
    public void StructuralMember_DefaultMinimal_DoubleRoundtripBitExact()
    {
        var original = new StructuralMemberFeatureModel();
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(original.FeatureName, m2.FeatureName);
        Assert.Equal(StructuralMemberFeatureModel.MfcClassName, m2.ClassName);
        Assert.Equal(StructuralMemberFeatureModel.HeaderTypeId, m2.TypeId);
    }

    [Fact]
    public void StructuralMember_EmptyPathSegments_DoubleRoundtrip()
    {
        var original = new StructuralMemberFeatureModel(
            profilePath: @"profiles\box.sldlfp",
            pathSegments: Array.Empty<EntityReference>(),
            rotationAngle: 0.0,
            cornerTreatmentType: 0);

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.NotNull(m2.PathSegments);
        Assert.Empty(m2.PathSegments);
    }

    [Fact]
    public void StructuralMember_LargeNumberOfPathSegments_DoubleRoundtrip()
    {
        // Stress test with 100 path segments
        const int segmentCount = 100;
        var segments = new List<EntityReference>(segmentCount);
        for (uint i = 1; i <= segmentCount; i++)
        {
            segments.Add(new EntityReference
            {
                EntityId = 10000 + i,
                EntityType = SelectionEntityType.Edge,
                Name = $"moWeldSegSkIndex_c:{(10000 + i):X8}"
            });
        }

        var original = new StructuralMemberFeatureModel(
            profilePath: @"profiles\large_truss.sldlfp",
            pathSegments: segments,
            rotationAngle: 0.5,
            cornerTreatmentType: 1)
        {
            LibraryProfileMaterial = "Structural Carbon Steel Q235B"
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.NotNull(m2.PathSegments);
        Assert.Equal(segmentCount, m2.PathSegments.Count);
        for (int i = 0; i < segmentCount; i++)
        {
            Assert.Equal(10001u + (uint)i, m2.PathSegments[i].EntityId);
        }
    }

    [Theory]
    [InlineData(@"")]
    [InlineData(@"\\STORAGE-SERVER\cad\profiles\weldments\iso\angle\200x200x20.sldlfp")]
    [InlineData(@"D:\Проекты\Сварные_детали_2026\Профиль_Швеллер_100.sldlfp")]
    [InlineData(@"D:\测试路径\具有特别长的文件夹名称和非常深的文件层级结构\为了验证路径字符串边界不会发生截断或者越界溢出\测试文件.sldlfp")]
    [InlineData(@"C:\Profiles with (symbols) [brackets] {braces} and spaces & symbols !@#$%\test.sldlfp")]
    public void StructuralMember_ExtremePaths_DoubleRoundtrip(string path)
    {
        var original = new StructuralMemberFeatureModel(profilePath: path);
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(path, m2.WeldmentProfilePath);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(Math.PI)]
    [InlineData(-Math.PI)]
    [InlineData(2 * Math.PI)]
    [InlineData(-2 * Math.PI)]
    [InlineData(1000000.0)]
    [InlineData(-1000000.0)]
    [InlineData(1e-12)]
    public void StructuralMember_ExtremeAngles_DoubleRoundtrip(double angle)
    {
        var original = new StructuralMemberFeatureModel(
            profilePath: @"profiles\pipe.sldlfp",
            rotationAngle: angle);

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(angle, m2.RotationAngle!.Value, 8);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    [InlineData(int.MaxValue)]
    public void StructuralMember_CornerTreatmentVariations_DoubleRoundtrip(int cornerType)
    {
        var original = new StructuralMemberFeatureModel(
            profilePath: @"profiles\beam.sldlfp",
            cornerTreatmentType: cornerType);

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(cornerType, m2.CornerTreatmentType);
    }

    [Fact]
    public void StructuralMember_EntityBoundaryIds_DoubleRoundtrip()
    {
        var original = new StructuralMemberFeatureModel(
            profilePath: @"profiles\test.sldlfp",
            pathSegments: new[]
            {
                new EntityReference { EntityId = 0, EntityType = SelectionEntityType.Edge },
                new EntityReference { EntityId = uint.MaxValue, EntityType = SelectionEntityType.Edge }
            })
        {
            LocateProfilePoint = new EntityReference { EntityId = uint.MaxValue, EntityType = SelectionEntityType.Sketch }
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(0u, m2.PathSegments![0].EntityId);
        Assert.Equal(uint.MaxValue, m2.PathSegments[1].EntityId);
        Assert.Equal(uint.MaxValue, m2.LocateProfilePoint!.EntityId);
    }

    // =========================================================================
    // 2. EndCapFeatureModel Empirical Stress Tests
    // =========================================================================

    [Fact]
    public void EndCap_FullyPopulatedWithChamfer_TripleRoundtripBitExact()
    {
        var original = new EndCapFeatureModel(
            thickness: 0.008,
            face: new EntityReference { EntityId = 8888, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:000022B8" },
            useChamferCorners: true,
            chamferDistance: 0.0025)
        {
            FeatureName = "顶端盖_倒角_压力测试",
            IsEndCapInward = 2, // Inward
            ThicknessRatioForOffset = 0.75,
            UseThicknessRatioForOffset = true,
            OffsetDistance = 0.0012,
            UseReverse = true
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(original.FeatureName, m2.FeatureName);
        Assert.Equal(original.Thickness!.Value, m2.Thickness!.Value, 8);
        Assert.Equal(original.IsEndCapInward, m2.IsEndCapInward);
        Assert.Equal(original.ThicknessRatioForOffset!.Value, m2.ThicknessRatioForOffset!.Value, 8);
        Assert.Equal(original.UseThicknessRatioForOffset, m2.UseThicknessRatioForOffset);
        Assert.Equal(original.OffsetDistance!.Value, m2.OffsetDistance!.Value, 8);
        Assert.Equal(original.UseCornerTreatment, m2.UseCornerTreatment);
        Assert.Equal(original.UseChamferCorners, m2.UseChamferCorners);
        Assert.Equal(original.ChamferDistance!.Value, m2.ChamferDistance!.Value, 8);
        Assert.Equal(original.UseReverse, m2.UseReverse);
        Assert.NotNull(m2.Face);
        Assert.Equal(8888u, m2.Face!.EntityId);

        // Triple roundtrip
        byte[] b3 = SerializeModel(DeserializeModel<EndCapFeatureModel>(b2));
        Assert.Equal(b2, b3);
    }

    [Fact]
    public void EndCap_NoChamfer_NullChamferPreserved_DoubleRoundtrip()
    {
        var original = new EndCapFeatureModel(
            thickness: 0.004,
            face: new EntityReference { EntityId = 1234, EntityType = SelectionEntityType.Face },
            useChamferCorners: false)
        {
            IsEndCapInward = 1, // Outward
            UseThicknessRatioForOffset = false,
            OffsetDistance = 0.003
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.False(m2.UseChamferCorners);
        Assert.Null(m2.ChamferDistance);
        Assert.Equal(0.004, m2.Thickness!.Value, 8);
        Assert.Equal(0.003, m2.OffsetDistance!.Value, 8);
    }

    [Fact]
    public void EndCap_NullFace_DoubleRoundtrip()
    {
        var original = new EndCapFeatureModel(thickness: 0.002, face: null, useChamferCorners: false);
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Null(m2.Face);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.005)]
    [InlineData(1e-9)]
    [InlineData(5.0)]
    [InlineData(100.0)]
    public void EndCap_ExtremeThicknessValues_DoubleRoundtrip(double thickness)
    {
        var original = new EndCapFeatureModel(thickness: thickness);
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(thickness, m2.Thickness!.Value, 8);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0001)]
    [InlineData(0.05)]
    [InlineData(-0.002)]
    public void EndCap_ExtremeChamferDistances_DoubleRoundtrip(double chamferDist)
    {
        var original = new EndCapFeatureModel(thickness: 0.005, useChamferCorners: true, chamferDistance: chamferDist);
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.True(m2.UseChamferCorners);
        Assert.Equal(chamferDist, m2.ChamferDistance!.Value, 8);
    }

    [Theory]
    [InlineData(1)]  // Outward
    [InlineData(2)]  // Inward
    [InlineData(3)]  // SpecifyInset
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public void EndCap_DirectionVariations_DoubleRoundtrip(int direction)
    {
        var original = new EndCapFeatureModel(thickness: 0.003) { IsEndCapInward = direction };
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(direction, m2.IsEndCapInward);
    }

    // =========================================================================
    // 3. WeldBeadFeatureModel Empirical Stress Tests
    // =========================================================================

    [Fact]
    public void WeldBead_FullyPopulated_TripleRoundtripBitExact()
    {
        var original = new WeldBeadFeatureModel(
            beadSize: 0.006,
            beadLength: 0.12,
            beadPitch: 0.05,
            beadType: 2)
        {
            FeatureName = "主焊缝_全参数测试",
            TangentPropagation = false,
            UseOtherSide = true
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(original.FeatureName, m2.FeatureName);
        Assert.Equal(original.BeadSize!.Value, m2.BeadSize!.Value, 8);
        Assert.Equal(original.BeadLength!.Value, m2.BeadLength!.Value, 8);
        Assert.Equal(original.BeadPitch!.Value, m2.BeadPitch!.Value, 8);
        Assert.Equal(original.BeadType, m2.BeadType);
        Assert.Equal(original.TangentPropagation, m2.TangentPropagation);
        Assert.Equal(original.UseOtherSide, m2.UseOtherSide);

        // Triple roundtrip
        byte[] b3 = SerializeModel(DeserializeModel<WeldBeadFeatureModel>(b2));
        Assert.Equal(b2, b3);
    }

    [Fact]
    public void WeldBead_ClassNameFallback_DoubleRoundtrip()
    {
        // When SerializedClassName is null, it should default to MfcClassName
        var original = new WeldBeadFeatureModel(beadSize: 0.004)
        {
            SerializedClassName = null
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(WeldBeadFeatureModel.MfcClassName, m2.SerializedClassName);
        Assert.Equal(WeldBeadFeatureModel.MfcClassName, m2.ClassName);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0, 0)]
    [InlineData(1e-6, 1e-6, 1e-6, 1)]
    [InlineData(0.5, 2.0, 1.0, -1)]
    [InlineData(-0.005, -0.01, -0.02, 99)]
    public void WeldBead_ExtremeParameters_DoubleRoundtrip(double size, double length, double pitch, int type)
    {
        var original = new WeldBeadFeatureModel(beadSize: size, beadLength: length, beadPitch: pitch, beadType: type)
        {
            TangentPropagation = true,
            UseOtherSide = false
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(size, m2.BeadSize!.Value, 8);
        Assert.Equal(length, m2.BeadLength!.Value, 8);
        Assert.Equal(pitch, m2.BeadPitch!.Value, 8);
        Assert.Equal(type, m2.BeadType);
    }

    // =========================================================================
    // 4. CosmeticWeldBeadFeatureModel Empirical Stress Tests
    // =========================================================================

    [Fact]
    public void CosmeticWeldBead_FullyPopulatedIntermittent_TripleRoundtripBitExact()
    {
        var edges = new[]
        {
            new EntityReference { EntityId = 701, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000002BD" },
            new EntityReference { EntityId = 702, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000002BE" },
            new EntityReference { EntityId = 703, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000002BF" }
        };

        var original = new CosmeticWeldBeadFeatureModel(
            beadSize: 0.0045,
            edgeReferences: edges,
            side: 2)
        {
            FeatureName = "装饰角焊缝_断续焊_测试",
            IntermittentWeld = true,
            IntermittentWeldLength = 0.025,
            Pitch = 0.060,
            Staggered = true,
            TangentPropagation = true
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(original.FeatureName, m2.FeatureName);
        Assert.Equal(original.Side, m2.Side);
        Assert.Equal(original.BeadSize!.Value, m2.BeadSize!.Value, 8);
        Assert.Equal(original.IntermittentWeld, m2.IntermittentWeld);
        Assert.Equal(original.IntermittentWeldLength!.Value, m2.IntermittentWeldLength!.Value, 8);
        Assert.Equal(original.Pitch!.Value, m2.Pitch!.Value, 8);
        Assert.Equal(original.Staggered, m2.Staggered);
        Assert.Equal(original.TangentPropagation, m2.TangentPropagation);
        Assert.NotNull(m2.EdgeReferences);
        Assert.Equal(3, m2.EdgeReferences!.Count);
        Assert.Equal(701u, m2.EdgeReferences[0].EntityId);
        Assert.Equal(702u, m2.EdgeReferences[1].EntityId);
        Assert.Equal(703u, m2.EdgeReferences[2].EntityId);

        // Triple roundtrip
        byte[] b3 = SerializeModel(DeserializeModel<CosmeticWeldBeadFeatureModel>(b2));
        Assert.Equal(b2, b3);
    }

    [Fact]
    public void CosmeticWeldBead_LargeEdgeMap_DoubleRoundtrip()
    {
        // Stress test with 120 edges in the edge map
        const int edgeCount = 120;
        var edges = new List<EntityReference>(edgeCount);
        for (uint i = 1; i <= edgeCount; i++)
        {
            edges.Add(new EntityReference
            {
                EntityId = 50000 + i,
                EntityType = SelectionEntityType.Edge,
                Name = $"moCompEdge_c:{(50000 + i):X8}"
            });
        }

        var original = new CosmeticWeldBeadFeatureModel(beadSize: 0.003, edgeReferences: edges, side: 1);
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.NotNull(m2.EdgeReferences);
        Assert.Equal(edgeCount, m2.EdgeReferences.Count);
        for (int i = 0; i < edgeCount; i++)
        {
            Assert.Equal(50001u + (uint)i, m2.EdgeReferences[i].EntityId);
        }
    }

    [Fact]
    public void CosmeticWeldBead_EmptyEdgeReferences_DoubleRoundtrip()
    {
        var original = new CosmeticWeldBeadFeatureModel(beadSize: 0.003, edgeReferences: Array.Empty<EntityReference>());
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.NotNull(m2.EdgeReferences);
        Assert.Empty(m2.EdgeReferences);
    }

    // =========================================================================
    // 5. SMWeldBeadFeatureModel Empirical Stress Tests
    // =========================================================================

    [Fact]
    public void SMWeldBead_FullyPopulated_TripleRoundtripBitExact()
    {
        var original = new SMWeldBeadFeatureModel(
            beadSize: 0.002,
            beadLength: 0.035,
            beadPitch: 0.015,
            beadType: 1)
        {
            FeatureName = "钣金焊缝_压力测试",
            TangentPropagation = true,
            UseOtherSide = false
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(original.FeatureName, m2.FeatureName);
        Assert.Equal(original.BeadSize!.Value, m2.BeadSize!.Value, 8);
        Assert.Equal(original.BeadLength!.Value, m2.BeadLength!.Value, 8);
        Assert.Equal(original.BeadPitch!.Value, m2.BeadPitch!.Value, 8);
        Assert.Equal(original.BeadType, m2.BeadType);
        Assert.Equal(original.TangentPropagation, m2.TangentPropagation);
        Assert.Equal(original.UseOtherSide, m2.UseOtherSide);

        // Triple roundtrip
        byte[] b3 = SerializeModel(DeserializeModel<SMWeldBeadFeatureModel>(b2));
        Assert.Equal(b2, b3);
    }

    [Fact]
    public void SMWeldBead_DefaultValues_DoubleRoundtripBitExact()
    {
        var original = new SMWeldBeadFeatureModel();
        var (m2, b1, b2) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(SMWeldBeadFeatureModel.MfcClassName, m2.ClassName);
        Assert.Equal(SMWeldBeadFeatureModel.HeaderTypeId, m2.TypeId);
    }

    // =========================================================================
    // 6. Property Mutation & Reserialization Stability
    // =========================================================================

    [Fact]
    public void StructuralMember_MutationReserialization_ProducesStableStream()
    {
        var model = new StructuralMemberFeatureModel(
            profilePath: @"profiles\initial.sldlfp",
            rotationAngle: 0.0,
            cornerTreatmentType: 0);

        byte[] b1 = SerializeModel(model);
        var reloaded = DeserializeModel<StructuralMemberFeatureModel>(b1);

        // Mutate properties
        reloaded.RotationAngle = 0.7853981633974483; // 45 deg
        reloaded.CornerTreatmentType = 1;             // EndMiter
        reloaded.MirrorProfile = true;
        reloaded.WeldmentProfilePath = @"profiles\modified.sldlfp";
        reloaded.FeatureName = "StructuralMember_Mutated";

        byte[] b2 = SerializeModel(reloaded);
        Assert.NotEqual(b1, b2); // Stream must change on parameter mutation

        // Re-deserialize and verify mutated values survive
        var reloaded2 = DeserializeModel<StructuralMemberFeatureModel>(b2);
        Assert.Equal("StructuralMember_Mutated", reloaded2.FeatureName);
        Assert.Equal(@"profiles\modified.sldlfp", reloaded2.WeldmentProfilePath);
        Assert.Equal(0.7853981633974483, reloaded2.RotationAngle!.Value, 8);
        Assert.Equal(1, reloaded2.CornerTreatmentType);
        Assert.True(reloaded2.MirrorProfile);

        // Bit-exact stability after mutation
        byte[] b3 = SerializeModel(reloaded2);
        Assert.Equal(b2, b3);
    }

    [Fact]
    public void EndCap_MutationReserialization_ChamferToggleStability()
    {
        // Start without chamfer
        var model = new EndCapFeatureModel(thickness: 0.003, useChamferCorners: false);
        byte[] b1 = SerializeModel(model);
        var r1 = DeserializeModel<EndCapFeatureModel>(b1);
        Assert.False(r1.UseChamferCorners);
        Assert.Null(r1.ChamferDistance);

        // Mutate: enable chamfer
        r1.UseChamferCorners = true;
        r1.ChamferDistance = 0.0015;
        r1.UseCornerTreatment = true;
        r1.Thickness = 0.006;

        byte[] b2 = SerializeModel(r1);
        var r2 = DeserializeModel<EndCapFeatureModel>(b2);
        Assert.True(r2.UseChamferCorners);
        Assert.Equal(0.0015, r2.ChamferDistance!.Value, 8);
        Assert.Equal(0.006, r2.Thickness!.Value, 8);

        // Mutate back: disable chamfer
        r2.UseChamferCorners = false;
        r2.ChamferDistance = null;
        r2.UseCornerTreatment = false;

        byte[] b3 = SerializeModel(r2);
        var r3 = DeserializeModel<EndCapFeatureModel>(b3);
        Assert.False(r3.UseChamferCorners);
        Assert.Null(r3.ChamferDistance);

        byte[] b4 = SerializeModel(r3);
        Assert.Equal(b3, b4);
    }

    // =========================================================================
    // 7. RawBlock Fallback & Passthrough Stress Tests
    // =========================================================================

    [Fact]
    public void StructuralMember_RawBlockPassthrough_WhenFeatureNameIsNull()
    {
        byte[] mockRawBlock = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22, 0x33, 0x44 };
        var model = new StructuralMemberFeatureModel
        {
            RawBlock = mockRawBlock,
            FeatureName = null // Triggers Mode B passthrough
        };

        byte[] output = SerializeModel(model);
        Assert.Equal(mockRawBlock, output);
    }

    [Fact]
    public void EndCap_RawBlockPassthrough_WhenFeatureNameIsNull()
    {
        byte[] mockRawBlock = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var model = new EndCapFeatureModel
        {
            RawBlock = mockRawBlock,
            FeatureName = null
        };

        byte[] output = SerializeModel(model);
        Assert.Equal(mockRawBlock, output);
    }

    [Fact]
    public void CosmeticWeldBead_RawBlockPassthrough_WhenFeatureNameIsNull()
    {
        byte[] mockRawBlock = new byte[] { 0x55, 0x66, 0x77, 0x88, 0x99 };
        var model = new CosmeticWeldBeadFeatureModel
        {
            RawBlock = mockRawBlock,
            FeatureName = null
        };

        byte[] output = SerializeModel(model);
        Assert.Equal(mockRawBlock, output);
    }

    [Fact]
    public void StructuralMember_UnknownClassFallback_CapturesRawBlock()
    {
        // Synthesize an archive stream with an unknown class name
        byte[] fakeStream;
        using (var writer = MfcArchive.CreateWriter())
        {
            ushort tag = 0;
            writer.ClassTagOrRef("moUnknownWeldFeat_c", ref tag);
            writer.WriteBytes(new byte[] { 0x12, 0x34, 0x56, 0x78 });
            fakeStream = writer.ToArray();
        }

        // Deserialize into StructuralMemberFeatureModel
        using var reader = MfcArchive.CreateReader(fakeStream);
        var model = new StructuralMemberFeatureModel();
        model.Serialize(reader);

        // Verify fallback: RawBlock is captured
        Assert.NotNull(model.RawBlock);
        Assert.Equal(fakeStream, model.RawBlock);
    }

    // =========================================================================
    // 8. Legacy Templates & Builders MfcFeatureModelBase Check
    // =========================================================================

    [Fact]
    public void Weldment_AllModels_InheritMfcFeatureModelBase()
    {
        var models = new[]
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

    // =========================================================================
    // 9. Polymorphic Serialization Pipeline
    // =========================================================================

    [Fact]
    public void Weldment_HeterogeneousPolymorphicCollection_DoubleRoundtrip()
    {
        var models = new (IMfcFeatureModel Model, string ExpectedWireClass)[]
        {
            (new StructuralMemberFeatureModel(profilePath: @"profiles\pipe1.sldlfp") { FeatureName = "StrMem_1" }, StructuralMemberFeatureModel.MfcClassName),
            (new EndCapFeatureModel(thickness: 0.005, useChamferCorners: true, chamferDistance: 0.001) { FeatureName = "EndCap_1" }, EndCapFeatureModel.MfcClassName),
            (new WeldBeadFeatureModel(beadSize: 0.004) { FeatureName = "WeldBead_1" }, WeldBeadFeatureModel.MfcClassName),
            (new CosmeticWeldBeadFeatureModel(beadSize: 0.003, side: 1) { FeatureName = "CosmWeld_1" }, CosmeticWeldBeadFeatureModel.MfcClassName),
            (new SMWeldBeadFeatureModel(beadSize: 0.002) { FeatureName = "SMWeld_1" }, SMWeldBeadFeatureModel.MfcClassName)
        };

        foreach (var (model, expectedWireClass) in models)
        {
            byte[] b1 = SerializeModel(model);
            Assert.True(b1.Length > 0);

            // Re-read via generic archive and verify wire class tag fidelity
            using var reader = MfcArchive.CreateReader(b1);
            reader.ReadClassTag(out string cls, out _, out _);
            Assert.Equal(expectedWireClass, cls);
        }
    }

    // =========================================================================
    // 10. Topological Entity Reference Preservation Tests
    // =========================================================================

    [Fact]
    public void StructuralMember_LocateProfilePoint_EntityDetails_DoubleRoundtrip()
    {
        var model = new StructuralMemberFeatureModel(profilePath: @"profiles\test.sldlfp")
        {
            LocateProfilePoint = new EntityReference
            {
                EntityId = 0x12345678,
                EntityType = SelectionEntityType.Sketch
            }
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(model);
        Assert.Equal(b1, b2);
        Assert.NotNull(m2.LocateProfilePoint);
        Assert.Equal(0x12345678u, m2.LocateProfilePoint!.EntityId);
        Assert.Equal(SelectionEntityType.Sketch, m2.LocateProfilePoint.EntityType);
        Assert.Equal("moCompSketchEntHandle_c:12345678", m2.LocateProfilePoint.Name);
    }

    [Fact]
    public void EndCap_FaceReference_EntityDetails_DoubleRoundtrip()
    {
        var model = new EndCapFeatureModel(thickness: 0.005)
        {
            Face = new EntityReference
            {
                EntityId = 0xAABBCCDD,
                EntityType = SelectionEntityType.Face
            }
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(model);
        Assert.Equal(b1, b2);
        Assert.NotNull(m2.Face);
        Assert.Equal(0xAABBCCDDu, m2.Face!.EntityId);
        Assert.Equal(SelectionEntityType.Face, m2.Face.EntityType);
        Assert.Equal("moCompFace_c:AABBCCDD", m2.Face.Name);
    }

    [Fact]
    public void CosmeticWeldBead_EdgeReference_EntityDetails_DoubleRoundtrip()
    {
        var model = new CosmeticWeldBeadFeatureModel(beadSize: 0.003)
        {
            EdgeReferences = new[]
            {
                new EntityReference { EntityId = 0x11223344, EntityType = SelectionEntityType.Edge }
            }
        };

        var (m2, b1, b2) = ExecuteDoubleRoundtrip(model);
        Assert.Equal(b1, b2);
        Assert.NotNull(m2.EdgeReferences);
        Assert.Single(m2.EdgeReferences!);
        Assert.Equal(0x11223344u, m2.EdgeReferences![0].EntityId);
        Assert.Equal(SelectionEntityType.Edge, m2.EdgeReferences[0].EntityType);
        Assert.Equal("moCompEdge_c:11223344", m2.EdgeReferences[0].Name);
    }

    // =========================================================================
    // 11. Empirical Documentation of Wire Format Modeling Scope
    // =========================================================================

    [Fact]
    public void CosmeticWeldBead_FromToParameters_EmpiricallyNotSerializedInWireFormat()
    {
        // Empirically documents that From-To parameters (moCWeldBeadFromToParams_c)
        // are currently unmodeled in the wire format (deserialized to null).
        var model = new CosmeticWeldBeadFeatureModel(beadSize: 0.003)
        {
            FromToLength = true,
            FromToStartPoint = 0.010,
            FromToWeldLength = 0.050,
            FromToReverse = true
        };

        byte[] bytes = SerializeModel(model);
        var restored = DeserializeModel<CosmeticWeldBeadFeatureModel>(bytes);

        Assert.Null(restored.FromToLength);
        Assert.Null(restored.FromToStartPoint);
        Assert.Null(restored.FromToWeldLength);
        Assert.Null(restored.FromToReverse);
    }

    [Fact]
    public void StructuralMember_UnmodeledFields_EmpiricallyNotSerializedInWireFormat()
    {
        // Empirically documents unmodeled structural member fields
        var model = new StructuralMemberFeatureModel(profilePath: @"profiles\test.sldlfp")
        {
            ConnectedSegmentsOption = 1,
            ConnectionTypes = new[] { 1, 2, 3 },
            Groups = new[] { new EntityReference { EntityId = 1, Name = "Group1" } },
            ConfigurationName = "DefaultConfig",
            TransferMaterial = true
        };

        byte[] bytes = SerializeModel(model);
        var restored = DeserializeModel<StructuralMemberFeatureModel>(bytes);

        Assert.Null(restored.ConnectedSegmentsOption);
        Assert.Null(restored.ConnectionTypes);
        Assert.Null(restored.Groups);
        Assert.Null(restored.ConfigurationName);
        Assert.Null(restored.TransferMaterial);
    }

    [Fact]
    public void EndCap_DepthDistance_EmpiricallyNotSerializedInWireFormat()
    {
        var model = new EndCapFeatureModel(thickness: 0.003)
        {
            DepthDistance = 0.012
        };

        byte[] bytes = SerializeModel(model);
        var restored = DeserializeModel<EndCapFeatureModel>(bytes);
        Assert.Null(restored.DepthDistance);
    }

    [Fact]
    public void WeldBead_IndexedListProperties_EmpiricallyNotSerializedInWireFormat()
    {
        var model = new WeldBeadFeatureModel(beadSize: 0.004)
        {
            BeadLengths = new[] { 0.01, 0.02 },
            BeadPitches = new[] { 0.03, 0.04 },
            BeadSizes = new[] { 0.004, 0.004 },
            BeadTypes = new[] { 1, 1 }
        };

        byte[] bytes = SerializeModel(model);
        var restored = DeserializeModel<WeldBeadFeatureModel>(bytes);

        Assert.Null(restored.BeadLengths);
        Assert.Null(restored.BeadPitches);
        Assert.Null(restored.BeadSizes);
        Assert.Null(restored.BeadTypes);
    }
}
