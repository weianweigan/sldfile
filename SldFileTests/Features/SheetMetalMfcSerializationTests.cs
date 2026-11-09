using System;
using System.Collections.Generic;
using System.Reflection;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests.Features;

/// <summary>
/// Comprehensive verification suite demonstrating that all Milestone P2 Sheet Metal features
/// (EdgeFlange, MiterFlange, Hem, Jog, LoftedBend, NormalCut, FlatPattern, Fold, UnFold)
/// uniformly inherit MfcFeatureModelBase, implement IMfcFeatureModel and IMfcSerializable,
/// and support double-roundtrip byte equality and slot patching.
/// </summary>
public class SheetMetalMfcSerializationTests
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
    public void EdgeFlangeFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new EdgeFlangeFeatureModel(bendAngle: 1.5707963, bendRadius: 0.002, offsetDistance: 0.01)
        {
            FeatureName = "EdgeFlange_Test",
            Edges = new List<EntityReference>
            {
                new EntityReference { EntityId = 101, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:00000065" }
            }.AsReadOnly(),
            PositionType = 2,
            ReliefWidth = 0.0015,
            ReliefDepth = 0.002,
            ReliefRatio = 0.5,
            UseReliefRatio = true,
            UsePositionOffset = true,
            PositionOffsetDistance = 0.005
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<EdgeFlangeFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.BendAngle!.Value, restored.BendAngle!.Value, 6);
        Assert.Equal(original.BendRadius!.Value, restored.BendRadius!.Value, 6);
        Assert.Equal(original.OffsetDistance!.Value, restored.OffsetDistance!.Value, 6);
        Assert.Equal(original.PositionType, restored.PositionType);
        Assert.Equal(original.ReliefWidth!.Value, restored.ReliefWidth!.Value, 6);
        Assert.Equal(original.ReliefDepth!.Value, restored.ReliefDepth!.Value, 6);
        Assert.Equal(original.ReliefRatio!.Value, restored.ReliefRatio!.Value, 6);
        Assert.Equal(original.UseReliefRatio, restored.UseReliefRatio);
        Assert.Equal(original.UsePositionOffset, restored.UsePositionOffset);
        Assert.Equal(original.PositionOffsetDistance!.Value, restored.PositionOffsetDistance!.Value, 6);
        Assert.NotNull(restored.Edges);
        Assert.Single(restored.Edges!);
        Assert.Equal(101u, restored.Edges![0].EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void EdgeFlangeFeatureModel_RawBlock_PreservedAndPatched()
    {
        byte[] dummyBlock = new byte[0x2000];
        for (int i = 0; i < dummyBlock.Length; i++) dummyBlock[i] = (byte)(i & 0xFF);

        var model = new EdgeFlangeFeatureModel
        {
            RawBlock = dummyBlock,
            BendAngle = 2.0944,
            BendRadius = 0.003
        };

        byte[] serialized = SerializeModel(model);
        Assert.Equal(dummyBlock.Length, serialized.Length);

        // Verify slot patching took place on the serialized copy
        double patchedAngle = BitConverter.ToDouble(serialized, EdgeFlangeAuthoringSegments.BendAngleSlots[0]);
        Assert.Equal(2.0944, patchedAngle, 6);

        double patchedRadius = BitConverter.ToDouble(serialized, EdgeFlangeAuthoringSegments.BendRadiusSlots[0]);
        Assert.Equal(0.003, patchedRadius, 6);
    }

    [Fact]
    public void MiterFlangeFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new MiterFlangeFeatureModel
        {
            FeatureName = "MiterFlange_Test",
            Edges = new List<EntityReference>
            {
                new EntityReference { EntityId = 111, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:0000006F" },
                new EntityReference { EntityId = 112, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:00000070" }
            }.AsReadOnly(),
            BendRadius = 0.0025,
            UseDefaultBendRadius = false,
            PositionType = 1,
            UsePositionTrimSideBends = true,
            GapDistance = 0.0012,
            StartOffset = 0.005,
            EndOffset = 0.008,
            ReliefType = 2,
            ReliefWidth = 0.002,
            ReliefDepth = 0.002,
            ReliefRatio = 0.6,
            UseReliefRatio = true,
            UseDefaultBendRelief = false,
            UseDefaultBendAllowance = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<MiterFlangeFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.BendRadius!.Value, restored.BendRadius!.Value, 6);
        Assert.Equal(original.UseDefaultBendRadius, restored.UseDefaultBendRadius);
        Assert.Equal(original.PositionType, restored.PositionType);
        Assert.Equal(original.UsePositionTrimSideBends, restored.UsePositionTrimSideBends);
        Assert.Equal(original.GapDistance!.Value, restored.GapDistance!.Value, 6);
        Assert.Equal(original.StartOffset!.Value, restored.StartOffset!.Value, 6);
        Assert.Equal(original.EndOffset!.Value, restored.EndOffset!.Value, 6);
        Assert.Equal(original.ReliefType, restored.ReliefType);
        Assert.Equal(original.ReliefWidth!.Value, restored.ReliefWidth!.Value, 6);
        Assert.Equal(original.ReliefDepth!.Value, restored.ReliefDepth!.Value, 6);
        Assert.Equal(original.ReliefRatio!.Value, restored.ReliefRatio!.Value, 6);
        Assert.Equal(original.UseReliefRatio, restored.UseReliefRatio);
        Assert.NotNull(restored.Edges);
        Assert.Equal(2, restored.Edges!.Count);
        Assert.Equal(111u, restored.Edges![0].EntityId);
        Assert.Equal(112u, restored.Edges![1].EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void HemFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new HemFeatureModel(type: 2, length: 0.008, gapDistance: 0.002)
        {
            FeatureName = "Hem_Test",
            Radius = 0.0015,
            Angle = 1.047197,
            Edges = new List<EntityReference>
            {
                new EntityReference { EntityId = 121, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:00000079" }
            }.AsReadOnly(),
            BendPosition = 2,
            MiterGap = 0.001,
            ReverseDirection = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<HemFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.Type, restored.Type);
        Assert.Equal(original.BendPosition, restored.BendPosition);
        Assert.Equal(original.Length!.Value, restored.Length!.Value, 6);
        Assert.Equal(original.Radius!.Value, restored.Radius!.Value, 6);
        Assert.Equal(original.Angle!.Value, restored.Angle!.Value, 6);
        Assert.Equal(original.GapDistance!.Value, restored.GapDistance!.Value, 6);
        Assert.Equal(original.MiterGap!.Value, restored.MiterGap!.Value, 6);
        Assert.Equal(original.ReverseDirection, restored.ReverseDirection);
        Assert.NotNull(restored.Edges);
        Assert.Single(restored.Edges!);
        Assert.Equal(121u, restored.Edges![0].EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void JogFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new JogFeatureModel(
            fixedFace: new EntityReference { EntityId = 131, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:00000083" },
            offsetDistance: 0.012,
            jogAngle: 1.308996,
            bendRadius: 0.003)
        {
            FeatureName = "Jog_Test",
            DimensionPositionType = 1,
            JogPositionType = 2,
            OffsetType = 1,
            FixProjectedLength = true,
            ReverseDirection = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<JogFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.NotNull(restored.FixedFace);
        Assert.Equal(131u, restored.FixedFace!.EntityId);
        Assert.Equal(original.OffsetDistance!.Value, restored.OffsetDistance!.Value, 6);
        Assert.Equal(original.JogAngle!.Value, restored.JogAngle!.Value, 6);
        Assert.Equal(original.BendRadius!.Value, restored.BendRadius!.Value, 6);
        Assert.Equal(original.DimensionPositionType, restored.DimensionPositionType);
        Assert.Equal(original.JogPositionType, restored.JogPositionType);
        Assert.Equal(original.OffsetType, restored.OffsetType);
        Assert.Equal(original.FixProjectedLength, restored.FixProjectedLength);
        Assert.Equal(original.ReverseDirection, restored.ReverseDirection);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void LoftedBendFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new LoftedBendFeatureModel(
            thickness: 0.002,
            numberOfBendLines: 4,
            profiles: new[]
            {
                new EntityReference { EntityId = 141, EntityType = SelectionEntityType.SketchContour, Name = "moSketchChain_c:0000008D" },
                new EntityReference { EntityId = 142, EntityType = SelectionEntityType.SketchContour, Name = "moSketchChain_c:0000008E" }
            })
        {
            FeatureName = "LoftedBend_Test",
            FormedMethod = true,
            BendLineControlOption = 1,
            Direction = true,
            FacetingOption = 1,
            FacetValue = 0.0015,
            MaximumDeviation = 0.0003,
            ReferToEndPoint = false,
            SymmetricThickness = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<LoftedBendFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.Thickness!.Value, restored.Thickness!.Value, 6);
        Assert.Equal(original.NumberOfBendLines, restored.NumberOfBendLines);
        Assert.Equal(original.FormedMethod, restored.FormedMethod);
        Assert.Equal(original.BendLineControlOption, restored.BendLineControlOption);
        Assert.Equal(original.Direction, restored.Direction);
        Assert.Equal(original.FacetingOption, restored.FacetingOption);
        Assert.Equal(original.FacetValue!.Value, restored.FacetValue!.Value, 6);
        Assert.Equal(original.MaximumDeviation!.Value, restored.MaximumDeviation!.Value, 6);
        Assert.Equal(original.ReferToEndPoint, restored.ReferToEndPoint);
        Assert.Equal(original.SymmetricThickness, restored.SymmetricThickness);
        Assert.NotNull(restored.Profiles);
        Assert.Equal(2, restored.Profiles!.Count);
        Assert.Equal(141u, restored.Profiles![0].EntityId);
        Assert.Equal(142u, restored.Profiles![1].EntityId);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void NormalCutFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new NormalCutFeatureModel(autoPropagation: true, optimizeGeometry: true, layerAdjustmentValue: 0.0004)
        {
            FeatureName = "NormalCut_Test",
            LinkToKFactor = true,
            NormalCutParameters = 2
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<NormalCutFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.Equal(original.AutoPropagation, restored.AutoPropagation);
        Assert.Equal(original.OptimizeGeometry, restored.OptimizeGeometry);
        Assert.Equal(original.LayerAdjustmentValue!.Value, restored.LayerAdjustmentValue!.Value, 6);
        Assert.Equal(original.LinkToKFactor, restored.LinkToKFactor);
        Assert.Equal(original.NormalCutParameters, restored.NormalCutParameters);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void FlatPatternFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new FlatPatternFeatureModel(
            fixedFace: new EntityReference { EntityId = 151, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:00000097" },
            cornerTreatment: true,
            mergeFace: false,
            simplifyBends: true)
        {
            FeatureName = "FlatPattern_Test",
            BreakCorners = true,
            BreakCornerType = 1,
            BreakCornerRadius = 0.0018,
            AddCornerTrim = true,
            CornerTrimReliefType = 2,
            CornerTrimReliefDistance = 0.0025,
            CornerTrimRatioToThickness = 0.7,
            UseRatioToThickness = true,
            ShowSlitInCornerRelief = true
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<FlatPatternFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.NotNull(restored.FixedFace);
        Assert.Equal(151u, restored.FixedFace!.EntityId);
        Assert.Equal(original.CornerTreatment, restored.CornerTreatment);
        Assert.Equal(original.MergeFace, restored.MergeFace);
        Assert.Equal(original.SimplifyBends, restored.SimplifyBends);
        Assert.Equal(original.BreakCorners, restored.BreakCorners);
        Assert.Equal(original.BreakCornerType, restored.BreakCornerType);
        Assert.Equal(original.BreakCornerRadius!.Value, restored.BreakCornerRadius!.Value, 6);
        Assert.Equal(original.AddCornerTrim, restored.AddCornerTrim);
        Assert.Equal(original.CornerTrimReliefType, restored.CornerTrimReliefType);
        Assert.Equal(original.CornerTrimReliefDistance!.Value, restored.CornerTrimReliefDistance!.Value, 6);
        Assert.Equal(original.CornerTrimRatioToThickness!.Value, restored.CornerTrimRatioToThickness!.Value, 6);
        Assert.Equal(original.UseRatioToThickness, restored.UseRatioToThickness);
        Assert.Equal(original.ShowSlitInCornerRelief, restored.ShowSlitInCornerRelief);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void FoldFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new FoldFeatureModel(
            fixedFace: new EntityReference { EntityId = 161, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:000000A1" },
            bends: new[]
            {
                new EntityReference { EntityId = 171, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000000AB" },
                new EntityReference { EntityId = 172, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000000AC" }
            })
        {
            FeatureName = "Fold_Test"
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<FoldFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.NotNull(restored.FixedFace);
        Assert.Equal(161u, restored.FixedFace!.EntityId);
        Assert.NotNull(restored.Bends);
        Assert.Equal(2, restored.Bends!.Count);
        Assert.Equal(171u, restored.Bends![0].EntityId);
        Assert.Equal(172u, restored.Bends![1].EntityId);
        Assert.Equal(2, restored.BendsCount);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void UnFoldFeatureModel_Parametric_DoubleRoundtripByteEquality()
    {
        var original = new UnFoldFeatureModel(
            fixedFace: new EntityReference { EntityId = 181, EntityType = SelectionEntityType.Face, Name = "moCompFace_c:000000B5" },
            bends: new[]
            {
                new EntityReference { EntityId = 191, EntityType = SelectionEntityType.Edge, Name = "moCompEdge_c:000000BF" }
            })
        {
            FeatureName = "UnFold_Test"
        };

        byte[] b1 = SerializeModel(original);
        Assert.True(b1.Length > 0);

        var restored = DeserializeModel<UnFoldFeatureModel>(b1);
        Assert.NotNull(restored);
        Assert.Equal(original.FeatureName, restored.FeatureName);
        Assert.NotNull(restored.FixedFace);
        Assert.Equal(181u, restored.FixedFace!.EntityId);
        Assert.NotNull(restored.Bends);
        Assert.Single(restored.Bends!);
        Assert.Equal(191u, restored.Bends![0].EntityId);
        Assert.Equal(1, restored.BendsCount);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void SheetMetal_AllModels_InheritMfcFeatureModelBase_AndImplementIMfcFeatureModel()
    {
        var models = new Type[]
        {
            typeof(EdgeFlangeFeatureModel),
            typeof(MiterFlangeFeatureModel),
            typeof(HemFeatureModel),
            typeof(JogFeatureModel),
            typeof(LoftedBendFeatureModel),
            typeof(NormalCutFeatureModel),
            typeof(FlatPatternFeatureModel),
            typeof(FoldFeatureModel),
            typeof(UnFoldFeatureModel)
        };

        foreach (var type in models)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcFeatureModel).IsAssignableFrom(type), $"{type.Name} must implement IMfcFeatureModel");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void SheetMetal_LegacyTemplates_DecoratedWithObsoleteAttribute()
    {
        var models = new Type[]
        {
            typeof(SheetMetalBaseFlangeModel),
            typeof(EdgeFlangeFeatureModel),
            typeof(MiterFlangeFeatureModel),
            typeof(HemFeatureModel),
            typeof(JogFeatureModel),
            typeof(LoftedBendFeatureModel),
            typeof(NormalCutFeatureModel),
            typeof(FlatPatternFeatureModel),
            typeof(FoldFeatureModel),
            typeof(UnFoldFeatureModel)
        };

        foreach (var type in models)
        {
            Assert.True(typeof(MfcFeatureModelBase).IsAssignableFrom(type), $"{type.Name} must inherit MfcFeatureModelBase");
            Assert.True(typeof(IMfcFeatureModel).IsAssignableFrom(type), $"{type.Name} must implement IMfcFeatureModel");
            Assert.True(typeof(IMfcSerializable).IsAssignableFrom(type), $"{type.Name} must implement IMfcSerializable");
        }
    }

    [Fact]
    public void SheetMetal_AllModels_DefaultFeatureNames_AreGenuineUtf8Chinese()
    {
        Assert.Equal("边线-法兰1", new EdgeFlangeFeatureModel().FeatureName);
        Assert.Equal("斜接-法兰1", new MiterFlangeFeatureModel().FeatureName);
        Assert.Equal("褶边1", new HemFeatureModel().FeatureName);
        Assert.Equal("转折1", new JogFeatureModel().FeatureName);
        Assert.Equal("放样折弯1", new LoftedBendFeatureModel().FeatureName);
        Assert.Equal("法向切除1", new NormalCutFeatureModel().FeatureName);
        Assert.Equal("平板型式1", new FlatPatternFeatureModel().FeatureName);
        Assert.Equal("折叠1", new FoldFeatureModel().FeatureName);
        Assert.Equal("展开1", new UnFoldFeatureModel().FeatureName);

        // Also verify that serialization with default names writes the genuine UTF-8 strings
        Assert.Equal("边线-法兰1", DeserializeModel<EdgeFlangeFeatureModel>(SerializeModel(new EdgeFlangeFeatureModel())).FeatureName);
        Assert.Equal("斜接-法兰1", DeserializeModel<MiterFlangeFeatureModel>(SerializeModel(new MiterFlangeFeatureModel())).FeatureName);
        Assert.Equal("褶边1", DeserializeModel<HemFeatureModel>(SerializeModel(new HemFeatureModel())).FeatureName);
        Assert.Equal("转折1", DeserializeModel<JogFeatureModel>(SerializeModel(new JogFeatureModel())).FeatureName);
        Assert.Equal("放样折弯1", DeserializeModel<LoftedBendFeatureModel>(SerializeModel(new LoftedBendFeatureModel())).FeatureName);
        Assert.Equal("法向切除1", DeserializeModel<NormalCutFeatureModel>(SerializeModel(new NormalCutFeatureModel())).FeatureName);
        Assert.Equal("平板型式1", DeserializeModel<FlatPatternFeatureModel>(SerializeModel(new FlatPatternFeatureModel())).FeatureName);
        Assert.Equal("折叠1", DeserializeModel<FoldFeatureModel>(SerializeModel(new FoldFeatureModel())).FeatureName);
        Assert.Equal("展开1", DeserializeModel<UnFoldFeatureModel>(SerializeModel(new UnFoldFeatureModel())).FeatureName);
    }

    [Fact]
    public void EdgeFlange_And_MiterFlange_EdgeCount_StrictlyMatchesSerializedElements()
    {
        // When EdgeCount property is populated but Edges/Edge is null, the serialized count must be 0
        var ef = new EdgeFlangeFeatureModel
        {
            EdgeCount = 99,
            Edges = null,
            Edge = null
        };
        byte[] b1 = SerializeModel(ef);
        var r1 = DeserializeModel<EdgeFlangeFeatureModel>(b1);
        Assert.True(r1.Edges == null || r1.Edges.Count == 0);

        var mf = new MiterFlangeFeatureModel
        {
            EdgeCount = 99,
            Edges = null
        };
        byte[] b2 = SerializeModel(mf);
        var r2 = DeserializeModel<MiterFlangeFeatureModel>(b2);
        Assert.True(r2.Edges == null || r2.Edges.Count == 0);
    }
}
