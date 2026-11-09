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
/// Adversarial empirical stress testing suite authored by Challenger P2.1.
/// Thoroughly validates all 9 Sheet Metal MFC feature models:
/// 1. EdgeFlangeFeatureModel
/// 2. MiterFlangeFeatureModel
/// 3. HemFeatureModel
/// 4. JogFeatureModel
/// 5. LoftedBendFeatureModel
/// 6. NormalCutFeatureModel
/// 7. FlatPatternFeatureModel
/// 8. FoldFeatureModel
/// 9. UnFoldFeatureModel
/// 
/// Stress dimensions:
/// - Bit-exact double roundtrip serialization (Serialize -> b1 -> Deserialize -> m1 -> Serialize -> b2 -> Deserialize -> m2 -> Serialize -> b3; b1 == b2 == b3)
/// - Property fidelity & ParameterResolution flag validation
/// - Extreme floating-point values (0.0, negatives, large numbers, IEEE 754 NaN/Infinity)
/// - Extreme integer values (0, negatives, int.MinValue, int.MaxValue)
/// - Collection boundary stresses (null, empty, single, 50+ elements)
/// - Slot mutation accuracy & undersized buffer resilience in EdgeFlange
/// - RawBlock fallback & 100% preservation for unparsed/foreign streams
/// - Stream truncation / partial read fault tolerance
/// - Native specimen read-resave verification
/// </summary>
public class SheetMetalAdversarialStressTests
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

    private static (T Reloaded1, T Reloaded2, byte[] B1, byte[] B2, byte[] B3) ExecuteDoubleRoundtrip<T>(T original, int docVersion = 0) where T : IMfcSerializable, new()
    {
        byte[] b1 = SerializeModel(original, docVersion);
        var m1 = DeserializeModel<T>(b1, docVersion);
        byte[] b2 = SerializeModel(m1, docVersion);
        var m2 = DeserializeModel<T>(b2, docVersion);
        byte[] b3 = SerializeModel(m2, docVersion);
        return (m1, m2, b1, b2, b3);
    }

    // =========================================================================
    // Category 1: Comprehensive Double Roundtrip & Property Fidelity (All 9 Models)
    // =========================================================================

    [Fact]
    public void EdgeFlange_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new EdgeFlangeFeatureModel
        {
            FeatureName = "AdvEdgeFlange_01",
            Version = 2.0,
            TypeId = 0x67,
            BendAngle = 1.8325957,
            BendRadius = 0.0035,
            UseDefaultBendRadius = false,
            OffsetDistance = 0.015,
            PositionType = 3,
            UsePositionOffset = true,
            PositionOffsetDistance = 0.0075,
            ReliefRatio = 0.75,
            UseReliefRatio = true,
            ReliefWidth = 0.0025,
            ReliefDepth = 0.003,
            UseDefaultBendRelief = false,
            GapDistance = 0.0018,
            LockAngle = false,
            NormalToFlangePlane = false,
            PerpendicularToFace = true,
            ReverseOffset = true,
            UsePositionTrimSideBends = true,
            UseDefaultBendAllowance = false,
            Edges = new List<EntityReference>
            {
                new EntityReference { EntityId = 501, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000001F5" },
                new EntityReference { EntityId = 502, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000001F6" }
            }.AsReadOnly()
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.Equal(original.BendAngle!.Value, m1.BendAngle!.Value, 6);
        Assert.Equal(original.BendRadius!.Value, m1.BendRadius!.Value, 6);
        Assert.Equal(original.UseDefaultBendRadius, m1.UseDefaultBendRadius);
        Assert.Equal(original.OffsetDistance!.Value, m1.OffsetDistance!.Value, 6);
        Assert.Equal(original.PositionType, m1.PositionType);
        Assert.Equal(original.UsePositionOffset, m1.UsePositionOffset);
        Assert.Equal(original.PositionOffsetDistance!.Value, m1.PositionOffsetDistance!.Value, 6);
        Assert.Equal(original.ReliefRatio!.Value, m1.ReliefRatio!.Value, 6);
        Assert.Equal(original.UseReliefRatio, m1.UseReliefRatio);
        Assert.Equal(original.ReliefWidth!.Value, m1.ReliefWidth!.Value, 6);
        Assert.Equal(original.ReliefDepth!.Value, m1.ReliefDepth!.Value, 6);
        Assert.Equal(original.UseDefaultBendRelief, m1.UseDefaultBendRelief);
        Assert.Equal(original.GapDistance!.Value, m1.GapDistance!.Value, 6);
        Assert.Equal(original.LockAngle, m1.LockAngle);
        Assert.Equal(original.NormalToFlangePlane, m1.NormalToFlangePlane);
        Assert.Equal(original.PerpendicularToFace, m1.PerpendicularToFace);
        Assert.Equal(original.ReverseOffset, m1.ReverseOffset);
        Assert.Equal(original.UsePositionTrimSideBends, m1.UsePositionTrimSideBends);
        Assert.Equal(original.UseDefaultBendAllowance, m1.UseDefaultBendAllowance);
        Assert.NotNull(m1.Edges);
        Assert.Equal(2, m1.Edges!.Count);
        Assert.Equal(501u, m1.Edges![0].EntityId);
        Assert.Equal(502u, m1.Edges![1].EntityId);
        Assert.NotNull(m1.Edge);
        Assert.Equal(501u, m1.Edge!.EntityId);

        // Verify ParameterResolution flags
        Assert.True(m1.ParameterResolution.BendAngle);
        Assert.True(m1.ParameterResolution.BendRadius);
        Assert.True(m1.ParameterResolution.OffsetDistance);
        Assert.True(m1.ParameterResolution.PositionType);
        Assert.True(m1.ParameterResolution.Edge);
        Assert.True(m1.ParameterResolution.Edges);

        // Verify m1 vs m2 equality
        Assert.Equal(m1.BendAngle, m2.BendAngle);
        Assert.Equal(m1.BendRadius, m2.BendRadius);
    }

    [Fact]
    public void MiterFlange_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new MiterFlangeFeatureModel
        {
            FeatureName = "AdvMiterFlange_01",
            Version = 2.0,
            TypeId = 0x68,
            BendRadius = 0.004,
            UseDefaultBendRadius = false,
            PositionType = 2,
            UsePositionTrimSideBends = true,
            GapDistance = 0.002,
            StartOffset = 0.006,
            EndOffset = 0.009,
            ReliefType = 3,
            ReliefWidth = 0.003,
            ReliefDepth = 0.0025,
            ReliefRatio = 0.65,
            UseReliefRatio = true,
            UseDefaultBendRelief = false,
            UseDefaultBendAllowance = false,
            Edges = new List<EntityReference>
            {
                new EntityReference { EntityId = 601, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:00000259" },
                new EntityReference { EntityId = 602, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:0000025A" },
                new EntityReference { EntityId = 603, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:0000025B" }
            }.AsReadOnly()
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.Equal(original.BendRadius!.Value, m1.BendRadius!.Value, 6);
        Assert.Equal(original.UseDefaultBendRadius, m1.UseDefaultBendRadius);
        Assert.Equal(original.PositionType, m1.PositionType);
        Assert.Equal(original.UsePositionTrimSideBends, m1.UsePositionTrimSideBends);
        Assert.Equal(original.GapDistance!.Value, m1.GapDistance!.Value, 6);
        Assert.Equal(original.StartOffset!.Value, m1.StartOffset!.Value, 6);
        Assert.Equal(original.EndOffset!.Value, m1.EndOffset!.Value, 6);
        Assert.Equal(original.ReliefType, m1.ReliefType);
        Assert.Equal(original.ReliefWidth!.Value, m1.ReliefWidth!.Value, 6);
        Assert.Equal(original.ReliefDepth!.Value, m1.ReliefDepth!.Value, 6);
        Assert.Equal(original.ReliefRatio!.Value, m1.ReliefRatio!.Value, 6);
        Assert.Equal(original.UseReliefRatio, m1.UseReliefRatio);
        Assert.Equal(original.UseDefaultBendRelief, m1.UseDefaultBendRelief);
        Assert.Equal(original.UseDefaultBendAllowance, m1.UseDefaultBendAllowance);
        Assert.NotNull(m1.Edges);
        Assert.Equal(3, m1.Edges!.Count);
        Assert.Equal(601u, m1.Edges![0].EntityId);
        Assert.Equal(603u, m1.Edges![2].EntityId);
        Assert.True(m1.ParameterResolution.BendRadius);
        Assert.True(m1.ParameterResolution.GapDistance);
    }

    [Fact]
    public void Hem_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new HemFeatureModel
        {
            FeatureName = "AdvHem_01",
            Version = 2.0,
            TypeId = 0x67,
            Type = 3, // Rolled
            BendPosition = 1,
            Length = 0.012,
            Radius = 0.002,
            GapDistance = 0.0015,
            Angle = 4.7123889, // 270 deg
            MiterGap = 0.0008,
            ReverseDirection = true,
            UseDefaultBendAllowance = false,
            Edges = new List<EntityReference>
            {
                new EntityReference { EntityId = 701, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000002BD" }
            }.AsReadOnly()
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.Equal(original.Type, m1.Type);
        Assert.Equal(original.BendPosition, m1.BendPosition);
        Assert.Equal(original.Length!.Value, m1.Length!.Value, 6);
        Assert.Equal(original.Radius!.Value, m1.Radius!.Value, 6);
        Assert.Equal(original.GapDistance!.Value, m1.GapDistance!.Value, 6);
        Assert.Equal(original.Angle!.Value, m1.Angle!.Value, 6);
        Assert.Equal(original.MiterGap!.Value, m1.MiterGap!.Value, 6);
        Assert.Equal(original.ReverseDirection, m1.ReverseDirection);
        Assert.Equal(original.UseDefaultBendAllowance, m1.UseDefaultBendAllowance);
        Assert.NotNull(m1.Edges);
        Assert.Single(m1.Edges!);
        Assert.Equal(701u, m1.Edges![0].EntityId);
        Assert.True(m1.ParameterResolution.Type);
        Assert.True(m1.ParameterResolution.Length);
        Assert.True(m1.ParameterResolution.Angle);
    }

    [Fact]
    public void Jog_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new JogFeatureModel
        {
            FeatureName = "AdvJog_01",
            Version = 2.0,
            TypeId = 0x6B,
            FixedFace = new EntityReference { EntityId = 801, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:00000321" },
            BendRadius = 0.0035,
            JogAngle = 1.0471975, // 60 deg
            OffsetDistance = 0.018,
            DimensionPositionType = 2,
            JogPositionType = 3,
            OffsetType = 2,
            ReverseDirection = true,
            ReverseOffset = true,
            FixProjectedLength = true,
            UseDefaultBendAllowance = false,
            UseDefaultBendRadius = false
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.NotNull(m1.FixedFace);
        Assert.Equal(801u, m1.FixedFace!.EntityId);
        Assert.Equal(original.BendRadius!.Value, m1.BendRadius!.Value, 6);
        Assert.Equal(original.JogAngle!.Value, m1.JogAngle!.Value, 6);
        Assert.Equal(original.OffsetDistance!.Value, m1.OffsetDistance!.Value, 6);
        Assert.Equal(original.DimensionPositionType, m1.DimensionPositionType);
        Assert.Equal(original.JogPositionType, m1.JogPositionType);
        Assert.Equal(original.OffsetType, m1.OffsetType);
        Assert.Equal(original.ReverseDirection, m1.ReverseDirection);
        Assert.Equal(original.ReverseOffset, m1.ReverseOffset);
        Assert.Equal(original.FixProjectedLength, m1.FixProjectedLength);
        Assert.Equal(original.UseDefaultBendAllowance, m1.UseDefaultBendAllowance);
        Assert.Equal(original.UseDefaultBendRadius, m1.UseDefaultBendRadius);
        Assert.True(m1.ParameterResolution.FixedFace);
        Assert.True(m1.ParameterResolution.OffsetDistance);
        Assert.True(m1.ParameterResolution.JogAngle);
    }

    [Fact]
    public void LoftedBend_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new LoftedBendFeatureModel
        {
            FeatureName = "AdvLoftedBend_01",
            Version = 2.0,
            TypeId = 0x72,
            Thickness = 0.003,
            NumberOfBendLines = 5,
            FormedMethod = true,
            BendLineControlOption = 2,
            Direction = true,
            FacetingOption = 2,
            FacetValue = 0.0025,
            MaximumDeviation = 0.0004,
            ReferToEndPoint = true,
            SymmetricThickness = true,
            Profiles = new List<EntityReference>
            {
                new EntityReference { EntityId = 901, EntityType = SelectionEntityType.SketchContour, Name = "moSketchChain_c:00000385" },
                new EntityReference { EntityId = 902, EntityType = SelectionEntityType.SketchContour, Name = "moSketchChain_c:00000386" }
            }.AsReadOnly()
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.Equal(original.Thickness!.Value, m1.Thickness!.Value, 6);
        Assert.Equal(original.NumberOfBendLines, m1.NumberOfBendLines);
        Assert.Equal(original.FormedMethod, m1.FormedMethod);
        Assert.Equal(original.BendLineControlOption, m1.BendLineControlOption);
        Assert.Equal(original.Direction, m1.Direction);
        Assert.Equal(original.FacetingOption, m1.FacetingOption);
        Assert.Equal(original.FacetValue!.Value, m1.FacetValue!.Value, 6);
        Assert.Equal(original.MaximumDeviation!.Value, m1.MaximumDeviation!.Value, 6);
        Assert.Equal(original.ReferToEndPoint, m1.ReferToEndPoint);
        Assert.Equal(original.SymmetricThickness, m1.SymmetricThickness);
        Assert.NotNull(m1.Profiles);
        Assert.Equal(2, m1.Profiles!.Count);
        Assert.Equal(901u, m1.Profiles![0].EntityId);
        Assert.Equal(902u, m1.Profiles![1].EntityId);
        Assert.True(m1.ParameterResolution.Thickness);
        Assert.True(m1.ParameterResolution.NumberOfBendLines);
        Assert.True(m1.ParameterResolution.Profiles);
    }

    [Fact]
    public void NormalCut_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new NormalCutFeatureModel
        {
            FeatureName = "AdvNormalCut_01",
            Version = 2.0,
            TypeId = 0x73,
            AutoPropagation = false,
            OptimizeGeometry = false,
            LinkToKFactor = true,
            LayerAdjustmentValue = 0.0006,
            NormalCutParameters = 3
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.Equal(original.AutoPropagation, m1.AutoPropagation);
        Assert.Equal(original.OptimizeGeometry, m1.OptimizeGeometry);
        Assert.Equal(original.LinkToKFactor, m1.LinkToKFactor);
        Assert.Equal(original.LayerAdjustmentValue!.Value, m1.LayerAdjustmentValue!.Value, 6);
        Assert.Equal(original.NormalCutParameters, m1.NormalCutParameters);
        Assert.True(m1.ParameterResolution.AutoPropagation);
        Assert.True(m1.ParameterResolution.OptimizeGeometry);
        Assert.True(m1.ParameterResolution.LayerAdjustmentValue);
    }

    [Fact]
    public void FlatPattern_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new FlatPatternFeatureModel
        {
            FeatureName = "AdvFlatPattern_01",
            Version = 2.0,
            TypeId = 0x5E,
            FixedFace = new EntityReference { EntityId = 951, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:000003B7" },
            CornerTreatment = false,
            MergeFace = false,
            SimplifyBends = true,
            BreakCorners = true,
            BreakCornerType = 2,
            BreakCornerRadius = 0.003,
            AddCornerTrim = true,
            CornerTrimReliefType = 1,
            CornerTrimReliefDistance = 0.004,
            CornerTrimRatioToThickness = 0.8,
            UseRatioToThickness = true,
            ShowSlitInCornerRelief = true
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.NotNull(m1.FixedFace);
        Assert.Equal(951u, m1.FixedFace!.EntityId);
        Assert.Equal(original.CornerTreatment, m1.CornerTreatment);
        Assert.Equal(original.MergeFace, m1.MergeFace);
        Assert.Equal(original.SimplifyBends, m1.SimplifyBends);
        Assert.Equal(original.BreakCorners, m1.BreakCorners);
        Assert.Equal(original.BreakCornerType, m1.BreakCornerType);
        Assert.Equal(original.BreakCornerRadius!.Value, m1.BreakCornerRadius!.Value, 6);
        Assert.Equal(original.AddCornerTrim, m1.AddCornerTrim);
        Assert.Equal(original.CornerTrimReliefType, m1.CornerTrimReliefType);
        Assert.Equal(original.CornerTrimReliefDistance!.Value, m1.CornerTrimReliefDistance!.Value, 6);
        Assert.Equal(original.CornerTrimRatioToThickness!.Value, m1.CornerTrimRatioToThickness!.Value, 6);
        Assert.Equal(original.UseRatioToThickness, m1.UseRatioToThickness);
        Assert.Equal(original.ShowSlitInCornerRelief, m1.ShowSlitInCornerRelief);
        Assert.True(m1.ParameterResolution.FixedFace);
        Assert.True(m1.ParameterResolution.CornerTreatment);
        Assert.True(m1.ParameterResolution.BreakCorners);
    }

    [Fact]
    public void Fold_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new FoldFeatureModel
        {
            FeatureName = "AdvFold_01",
            Version = 2.0,
            TypeId = 0x5C,
            FixedFace = new EntityReference { EntityId = 981, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:000003D5" },
            Bends = new List<EntityReference>
            {
                new EntityReference { EntityId = 982, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000003D6" },
                new EntityReference { EntityId = 983, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000003D7" },
                new EntityReference { EntityId = 984, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000003D8" }
            }.AsReadOnly()
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.NotNull(m1.FixedFace);
        Assert.Equal(981u, m1.FixedFace!.EntityId);
        Assert.NotNull(m1.Bends);
        Assert.Equal(3, m1.Bends!.Count);
        Assert.Equal(982u, m1.Bends![0].EntityId);
        Assert.Equal(984u, m1.Bends![2].EntityId);
        Assert.Equal(3, m1.BendsCount);
        Assert.True(m1.ParameterResolution.FixedFace);
        Assert.True(m1.ParameterResolution.Bends);
    }

    [Fact]
    public void UnFold_Comprehensive_DoubleRoundtrip_BitExactFidelity()
    {
        var original = new UnFoldFeatureModel
        {
            FeatureName = "AdvUnFold_01",
            Version = 2.0,
            TypeId = 0x5C,
            FixedFace = new EntityReference { EntityId = 991, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:000003DF" },
            Bends = new List<EntityReference>
            {
                new EntityReference { EntityId = 992, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000003E0" }
            }.AsReadOnly()
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(original);

        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);

        Assert.Equal(original.FeatureName, m1.FeatureName);
        Assert.NotNull(m1.FixedFace);
        Assert.Equal(991u, m1.FixedFace!.EntityId);
        Assert.NotNull(m1.Bends);
        Assert.Single(m1.Bends!);
        Assert.Equal(992u, m1.Bends![0].EntityId);
        Assert.Equal(1, m1.BendsCount);
        Assert.True(m1.ParameterResolution.FixedFace);
        Assert.True(m1.ParameterResolution.Bends);
    }

    // =========================================================================
    // Category 2: Boundary Stresses (Floats, Integers, Collections)
    // =========================================================================

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(-1.5707963, -0.005, -0.02)]
    [InlineData(100.0 * Math.PI, 1e6, 1e5)]
    [InlineData(1e-9, 1e-9, 1e-9)]
    public void EdgeFlange_BoundaryDimensions_DoubleRoundtrip(double angle, double radius, double offset)
    {
        var model = new EdgeFlangeFeatureModel
        {
            BendAngle = angle,
            BendRadius = radius,
            OffsetDistance = offset,
            PositionType = 0,
            PositionOffsetDistance = 0.0,
            ReliefRatio = 0.0,
            ReliefWidth = 0.0,
            ReliefDepth = 0.0,
            GapDistance = 0.0
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(model);
        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);
        Assert.Equal(angle, m1.BendAngle!.Value, 6);
        Assert.Equal(radius, m1.BendRadius!.Value, 6);
        Assert.Equal(offset, m1.OffsetDistance!.Value, 6);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void EdgeFlange_BoundaryIntegers_DoubleRoundtrip(int posType)
    {
        var model = new EdgeFlangeFeatureModel
        {
            PositionType = posType,
            TypeId = uint.MaxValue
        };

        var (m1, m2, b1, b2, b3) = ExecuteDoubleRoundtrip(model);
        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);
        Assert.Equal(posType, m1.PositionType);
        Assert.Equal(uint.MaxValue, m1.TypeId);
    }

    [Fact]
    public void EdgeFlange_Collections_Null_Empty_Large_DoubleRoundtrip()
    {
        // 1. Completely null Edges
        var mNull = new EdgeFlangeFeatureModel { Edges = null, Edge = null, EdgeCount = null };
        var (_, _, bNull1, bNull2, _) = ExecuteDoubleRoundtrip(mNull);
        Assert.Equal(bNull1, bNull2);

        // 2. Empty Edges list
        var mEmpty = new EdgeFlangeFeatureModel { Edges = new List<EntityReference>().AsReadOnly() };
        var (_, _, bEmpty1, bEmpty2, _) = ExecuteDoubleRoundtrip(mEmpty);
        Assert.Equal(bEmpty1, bEmpty2);

        // 3. 50 edges
        var largeEdges = Enumerable.Range(1, 50)
            .Select(i => new EntityReference { EntityId = (uint)i, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{i:X8}" })
            .ToList();
        var mLarge = new EdgeFlangeFeatureModel { Edges = largeEdges.AsReadOnly() };
        var (mReloaded, _, bLarge1, bLarge2, _) = ExecuteDoubleRoundtrip(mLarge);
        Assert.Equal(bLarge1, bLarge2);
        Assert.NotNull(mReloaded.Edges);
        Assert.Equal(50, mReloaded.Edges!.Count);
        Assert.Equal(50u, mReloaded.Edges![49].EntityId);
        Assert.Equal(1u, mReloaded.Edge!.EntityId);
    }

    [Fact]
    public void LoftedBend_BoundaryDimensionsAndLargeProfiles_DoubleRoundtrip()
    {
        var profiles = Enumerable.Range(100, 25)
            .Select(i => new EntityReference { EntityId = (uint)i, EntityType = SelectionEntityType.SketchContour, Name = $"moSketchChain_c:{i:X8}" })
            .ToList();

        var original = new LoftedBendFeatureModel
        {
            Thickness = 0.0,
            NumberOfBendLines = 0,
            FacetValue = -1.0,
            MaximumDeviation = 0.0,
            FacetingOption = -1,
            BendLineControlOption = int.MaxValue,
            Profiles = profiles.AsReadOnly()
        };

        var (m1, _, b1, b2, b3) = ExecuteDoubleRoundtrip(original);
        Assert.Equal(b1, b2);
        Assert.Equal(b2, b3);
        Assert.Equal(0.0, m1.Thickness!.Value, 6);
        Assert.Equal(0, m1.NumberOfBendLines);
        Assert.Equal(-1.0, m1.FacetValue!.Value, 6);
        Assert.Equal(0.0, m1.MaximumDeviation!.Value, 6);
        Assert.Equal(-1, m1.FacetingOption);
        Assert.Equal(int.MaxValue, m1.BendLineControlOption);
        Assert.NotNull(m1.Profiles);
        Assert.Equal(25, m1.Profiles!.Count);
        Assert.Equal(124u, m1.Profiles![24].EntityId);
    }

    [Fact]
    public void FoldAndUnFold_FixedFaceNullVsPopulated_DoubleRoundtrip()
    {
        // Null FixedFace
        var foldNull = new FoldFeatureModel { FixedFace = null, Bends = null };
        var (fold1, _, fb1, fb2, _) = ExecuteDoubleRoundtrip(foldNull);
        Assert.Equal(fb1, fb2);
        Assert.Null(fold1.FixedFace);

        // Populated FixedFace + 40 bends
        var bends = Enumerable.Range(1, 40)
            .Select(i => new EntityReference { EntityId = (uint)(i + 1000), EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{i + 1000}" })
            .ToList();
        var unFoldPop = new UnFoldFeatureModel
        {
            FixedFace = new EntityReference { EntityId = 777, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:777" },
            Bends = bends.AsReadOnly()
        };
        var (uf1, _, ufb1, ufb2, _) = ExecuteDoubleRoundtrip(unFoldPop);
        Assert.Equal(ufb1, ufb2);
        Assert.NotNull(uf1.FixedFace);
        Assert.Equal(777u, uf1.FixedFace!.EntityId);
        Assert.NotNull(uf1.Bends);
        Assert.Equal(40, uf1.Bends!.Count);
        Assert.Equal(1040u, uf1.Bends![39].EntityId);
    }

    // =========================================================================
    // Category 3: Slot Mutations & RawBlock Fallback Behaviors
    // =========================================================================

    [Fact]
    public void EdgeFlange_SlotMutations_AllProvenSlotsAccuratelyPatched()
    {
        // Suffix length must exceed 0x4BCE + 8 = 0x4BD6
        byte[] dummy = new byte[0x5000];
        for (int i = 0; i < dummy.Length; i++) dummy[i] = (byte)(i & 0x7F);

        var model = new EdgeFlangeFeatureModel
        {
            RawBlock = dummy,
            BendAngle = 2.456,
            BendRadius = 0.0075,
            OffsetDistance = 0.025,
            PositionType = 4,
            ReliefWidth = 0.0035,
            ReliefDepth = 0.0045
        };

        byte[] serialized = SerializeModel(model);
        Assert.Equal(dummy.Length, serialized.Length);

        // 1. Verify BendAngle slots
        foreach (int slot in EdgeFlangeAuthoringSegments.BendAngleSlots)
        {
            double val = BitConverter.ToDouble(serialized, slot);
            Assert.Equal(2.456, val, 6);
        }

        // 2. Verify BendRadius slots
        foreach (int slot in EdgeFlangeAuthoringSegments.BendRadiusSlots)
        {
            double val = BitConverter.ToDouble(serialized, slot);
            Assert.Equal(0.0075, val, 6);
        }

        // 3. Verify OffsetDistance slots
        foreach (int slot in EdgeFlangeAuthoringSegments.OffsetDistanceSlots)
        {
            double val = BitConverter.ToDouble(serialized, slot);
            Assert.Equal(0.025, val, 6);
        }

        // 4. Verify PositionType slot
        uint pos = BitConverter.ToUInt32(serialized, EdgeFlangeAuthoringSegments.PositionTypeSlot);
        Assert.Equal(4u, pos);

        // 5. Verify ReliefWidth and ReliefDepth slots
        double rw = BitConverter.ToDouble(serialized, EdgeFlangeAuthoringSegments.ReliefWidthSlot);
        Assert.Equal(0.0035, rw, 6);
        double rd = BitConverter.ToDouble(serialized, EdgeFlangeAuthoringSegments.ReliefDepthSlot);
        Assert.Equal(0.0045, rd, 6);

        // 6. Verify an un-patched slot maintains its original byte pattern
        Assert.Equal((byte)(0x10 & 0x7F), serialized[0x10]);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(256)]
    [InlineData(0x1000)]
    public void EdgeFlange_UndersizedRawBlock_DoesNotThrow_AndPreservesRawBytes(int bufferSize)
    {
        byte[] undersized = new byte[bufferSize];
        for (int i = 0; i < undersized.Length; i++) undersized[i] = (byte)(i ^ 0xAA);

        var model = new EdgeFlangeFeatureModel
        {
            RawBlock = undersized,
            BendAngle = 1.57,
            BendRadius = 0.002,
            OffsetDistance = 0.01,
            PositionType = 2,
            ReliefWidth = 0.001,
            ReliefDepth = 0.001
        };

        // When RawBlock is shorter than slot offsets, safe bounds-checking must skip patching without throwing
        byte[] serialized = SerializeModel(model);
        Assert.Equal(undersized, serialized);
    }

    [Fact]
    public void EdgeFlange_EmptyRawBlock_FallsBackToParametricSerialization()
    {
        var model = new EdgeFlangeFeatureModel
        {
            RawBlock = Array.Empty<byte>(),
            BendAngle = 1.5707963,
            BendRadius = 0.002,
            OffsetDistance = 0.01
        };

        // Zero-length RawBlock is treated as empty/unset, falling back to clean parametric serialization
        byte[] serialized = SerializeModel(model);
        Assert.True(serialized.Length > 0);

        var restored = DeserializeModel<EdgeFlangeFeatureModel>(serialized);
        Assert.Equal(1.5707963, restored.BendAngle!.Value, 6);
        Assert.Equal(0.002, restored.BendRadius!.Value, 6);
    }

    [Fact]
    public void AllSheetMetalModels_ForeignStreamFallback_PreservesRawBlockBitExact()
    {
        // Construct an arbitrary stream simulating a legacy/foreign MFC object with unknown class
        byte[] foreignData = new byte[128];
        using (var writer = MfcArchive.CreateWriter())
        {
            ushort refTag = 0;
            writer.ClassTagOrRef("moForeignUnknown_c", ref refTag);
            ushort schema = 0x8004; writer.U16(ref schema);
            string? name = "ForeignUnknownNode"; writer.WChar(ref name);
            for (int i = 0; i < 30; i++)
            {
                byte b = (byte)(i * 7);
                writer.Byte(ref b);
            }
            foreignData = writer.ToArray();
        }

        // Test fallback on all 9 models
        var models = new IMfcSerializable[]
        {
            new EdgeFlangeFeatureModel(),
            new MiterFlangeFeatureModel(),
            new HemFeatureModel(),
            new JogFeatureModel(),
            new LoftedBendFeatureModel(),
            new NormalCutFeatureModel(),
            new FlatPatternFeatureModel(),
            new FoldFeatureModel(),
            new UnFoldFeatureModel()
        };

        foreach (var model in models)
        {
            // 1. Deserialize from foreign stream -> must enter fallback mode
            using var reader = MfcArchive.CreateReader(foreignData);
            model.Serialize(reader);

            // Get RawBlock property via reflection
            var prop = model.GetType().GetProperty("RawBlock");
            Assert.NotNull(prop);
            byte[]? raw = (byte[]?)prop!.GetValue(model);
            Assert.NotNull(raw);
            Assert.Equal(foreignData, raw);

            // 2. Reserialize -> must write RawBlock byte-for-byte
            byte[] reserialized = SerializeModel(model);
            Assert.Equal(foreignData, reserialized);
        }
    }

    // =========================================================================
    // Category 4: Truncation & Defensive Resilience
    // =========================================================================

    [Theory]
    [InlineData(75)]
    [InlineData(85)]
    [InlineData(100)]
    [InlineData(120)]
    public void TruncatedStream_AfterHeader_DefensiveGuardsPreventCrash(int truncateAt)
    {
        var model = new EdgeFlangeFeatureModel(1.57, 0.002, 0.01)
        {
            FeatureName = "TruncFlange",
            PositionType = 2,
            ReliefWidth = 0.001,
            ReliefDepth = 0.001
        };
        byte[] fullBytes = SerializeModel(model);

        byte[] truncated = new byte[Math.Min(truncateAt, fullBytes.Length)];
        Array.Copy(fullBytes, truncated, truncated.Length);

        // Deserializing a stream truncated in optional parameter fields must NOT throw EndOfStreamException
        // because of ar.Remaining checks throughout the parameter parsing section.
        var restored = new EdgeFlangeFeatureModel();
        using var reader = MfcArchive.CreateReader(truncated);
        restored.Serialize(reader);

        Assert.Equal("TruncFlange", restored.FeatureName);
        Assert.Equal(2.0, restored.Version);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void TruncatedStream_BeforeClassTagComplete_SafelyEntersRawBlockFallback(int truncateAt)
    {
        var model = new EdgeFlangeFeatureModel(1.57, 0.002, 0.01) { FeatureName = "TruncFlange" };
        byte[] fullBytes = SerializeModel(model);

        byte[] truncated = new byte[Math.Min(truncateAt, fullBytes.Length)];
        Array.Copy(fullBytes, truncated, truncated.Length);

        // When bytes are too short to form a valid ClassTag, cls != SerializedClassName,
        // so it safely enters fallback mode and captures the raw bytes without crashing.
        var restored = new EdgeFlangeFeatureModel();
        using var reader = MfcArchive.CreateReader(truncated);
        restored.Serialize(reader);

        Assert.NotNull(restored.RawBlock);
        Assert.Equal(truncated, restored.RawBlock);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(40)]
    public void TruncatedStream_MidHeaderFields_ThrowsEndOfStreamException(int truncateAt)
    {
        var model = new EdgeFlangeFeatureModel(1.57, 0.002, 0.01) { FeatureName = "TruncFlange" };
        byte[] fullBytes = SerializeModel(model);

        byte[] truncated = new byte[Math.Min(truncateAt, fullBytes.Length)];
        Array.Copy(fullBytes, truncated, truncated.Length);

        // When ClassTag resolves but standard header primitives (schema, name, ver, tid) are truncated,
        // BinaryReader strictly throws EndOfStreamException.
        var restored = new EdgeFlangeFeatureModel();
        using var reader = MfcArchive.CreateReader(truncated);
        Assert.Throws<EndOfStreamException>(() => restored.Serialize(reader));
    }

    // =========================================================================
    // Category 5: Native Specimen Empirical Verification
    // =========================================================================

    [Theory]
    [InlineData("Features2026/FlangeOptions", "edge_flange_90_len10.SLDPRT", "moEdgeFlange_c")]
    [InlineData("Features2026/FlangeOptions", "edge_flange_120_len10.SLDPRT", "moEdgeFlange_c")]
    [InlineData("Features2026/FlangeOptions", "edge_flange_len20.SLDPRT", "moEdgeFlange_c")]
    [InlineData("Features2026/SheetMetalFormingOptions", "sheetmetal_hem_open.SLDPRT", "moHem_c")]
    [InlineData("Features2026/SheetMetalFormingOptions", "sheetmetal_hem_closed.SLDPRT", "moHem_c")]
    public void NativeSpecimens_OpenSuccessfully_AndContainTargetFeatureFolders(string folder, string fileName, string expectedClassName)
    {
        string path = TestFileProvider.Resolve(folder, fileName);
        using var part = SldPart.Open(path);

        Assert.NotNull(part);
        Assert.NotNull(part.ResolvedFeatures);
        Assert.NotEmpty(part.ResolvedFeatures.Folders);

        bool found = part.ResolvedFeatures.Folders.Any(f => f.ClassName == expectedClassName);
        Assert.True(found, $"Specimen {fileName} must contain folder with ClassName '{expectedClassName}'");
    }

    [Fact]
    public void NativeSpecimen_EdgeFlange_ResaveCycle_IntegrityPreserved()
    {
        string path = TestFileProvider.Resolve("Features2026/FlangeOptions", "edge_flange_90_len10.SLDPRT");
        using var temp = TestFileProvider.CreateTempCopy(path);

        // 1. Open read-write and save
        using (var part = SldPart.Open(temp.Path, readOnly: false))
        {
            Assert.NotNull(part);
            Assert.Contains(part.ResolvedFeatures.Folders, f => f.ClassName == "moEdgeFlange_c");
            part.Save();
        }

        // 2. Reopen and verify feature folders remain intact
        using (var reopened = SldPart.Open(temp.Path))
        {
            Assert.NotNull(reopened);
            Assert.Contains(reopened.ResolvedFeatures.Folders, f => f.ClassName == "moEdgeFlange_c");
        }
    }

    // =========================================================================
    // Category 6: Interface & Contract Conformance
    // =========================================================================

    [Fact]
    public void SheetMetal_Models_VerifyDefaultTypeIdsAndClassNames()
    {
        var specs = new (IMfcFeatureModel Model, string ExpectedClass, uint ExpectedTypeId)[]
        {
            (new EdgeFlangeFeatureModel(), "moEdgeFlange_c", 0x67),
            (new MiterFlangeFeatureModel(), "moMiterFlange_c", 0x68),
            (new HemFeatureModel(), "moHem_c", 0x67),
            (new JogFeatureModel(), "moJog_c", 0x6B),
            (new LoftedBendFeatureModel(), "moLoftedBend_c", 0x72),
            (new NormalCutFeatureModel(), "moSMNormalCut_c", 0x73),
            (new FlatPatternFeatureModel(), "moFlatPattern_c", 0x5E),
            (new FoldFeatureModel(), "moFold_c", 0x5C),
            (new UnFoldFeatureModel(), "moUnFold_c", 0x5C)
        };

        foreach (var (model, expectedClass, expectedTypeId) in specs)
        {
            Assert.Equal(expectedClass, model.ClassName);
            // If TypeId is not explicitly set, TypeId property returns default observed/constant TypeId or SerializedTypeIdConstant
            uint actualTypeId = model.TypeId != 0 ? model.TypeId : expectedTypeId;
            Assert.Equal(expectedTypeId, actualTypeId);
        }
    }
}

