using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Comprehensive verification suite demonstrating that all major feature families
/// (Extrude, Revolve, Fillet, Chamfer, Pattern, SheetMetal, and CmgrHdr2)
/// uniformly implement and roundtrip via the IMfcSerializable contract.
/// </summary>
public class AllFeaturesMfcSerializationTests
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
    public void ExtrudeFeatureModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new ExtrudeFeatureModel
        {
            FeatureName = "拉伸凸台_测试",
            Depth = 0.045, // 45mm
            EndCondition = 0,
            IsCut = false,
            Merge = true,
            BothDirections = true,
            Direction2Depth = 0.015,
            DraftAngle = 0.087266, // 5 deg
            DraftOutward = true,
            SketchName = "Sketch_Extrude1"
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<ExtrudeFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Depth, reloaded.Depth, precision: 6);
        Assert.Equal(original.EndCondition, reloaded.EndCondition);
        Assert.Equal(original.IsCut, reloaded.IsCut);
        Assert.Equal(original.Merge, reloaded.Merge);
        Assert.Equal(original.BothDirections, reloaded.BothDirections);
        Assert.Equal(original.Direction2Depth, reloaded.Direction2Depth, precision: 6);
        Assert.Equal(original.DraftAngle, reloaded.DraftAngle, precision: 6);
        Assert.Equal(original.DraftOutward, reloaded.DraftOutward);
        Assert.Equal(original.SketchName, reloaded.SketchName);
    }

    [Fact]
    public void ExtrudeCutFeatureModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new ExtrudeFeatureModel
        {
            FeatureName = "切除拉伸_测试",
            Depth = 0.0125, // 12.5mm
            EndCondition = 1, // Through All
            IsCut = true,
            Merge = false,
            BothDirections = false,
            SketchName = "Sketch_Cut1"
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<ExtrudeFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Depth, reloaded.Depth, precision: 6);
        Assert.Equal(original.EndCondition, reloaded.EndCondition);
        Assert.True(reloaded.IsCut);
        Assert.Equal(original.SketchName, reloaded.SketchName);
    }

    [Fact]
    public void ExtrudeFeatureModel_WithSelectionAndTopologySubEntities_RoundtripsAccurately()
    {
        var original = new ExtrudeFeatureModel
        {
            FeatureName = "拉伸_复杂拓扑参考",
            Depth = 0.05,
            EndCondition = 4, // Up to Surface
            IsCut = false,
            SketchName = "Sketch_Complex",
            ContourSelection = new MfcContourSelection
            {
                SelectionMode = SketchSelectionMode.ExplicitSelection,
                Contours = new()
                {
                    new MfcContourItem
                    {
                        EntityId = 101,
                        ChainId = 1,
                        IsClosed = true,
                        EntityIndices = new() { 1, 2, 3, 4 }
                    }
                },
                Regions = new()
                {
                    new MfcRegionItem
                    {
                        EntityId = 201,
                        RegionIndex = 0,
                        LoopCount = 1,
                        EdgeIds = new() { 10, 11, 12 }
                    }
                }
            },
            EndConditionEntity = new MfcEndConditionEntity
            {
                EntityType = SelectionEntityType.Face,
                EntityId = 0x5A12u,
                SurfaceName = "Face_TopTarget",
                FaceCount = 1
            },
            BodyScope = new MfcBodyScope
            {
                AutoSelect = false,
                Flags = 0x0001,
                BodyIds = new uint[] { 1, 3 },
                Bodies = new EntityReference[]
                {
                    new MfcEntityReferenceItem { EntityId = 1, EntityType = SelectionEntityType.Body, Name = "MainBody" },
                    new MfcEntityReferenceItem { EntityId = 3, EntityType = SelectionEntityType.Body, Name = "ToolBody" }
                }
            },
            DirectionReference = new MfcDirectionReference
            {
                EntityType = SelectionEntityType.Edge,
                EntityId = 0x1234u,
                ReferenceName = "DirectionEdge1",
                Vector = new[] { 0.0, 0.707106, 0.707106 }
            }
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<ExtrudeFeatureModel>(serialized);

        // Core fields
        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Depth, reloaded.Depth, precision: 6);
        Assert.Equal(4u, reloaded.EndCondition);

        // Contour selection
        Assert.NotNull(reloaded.ContourSelection);
        Assert.Equal(SketchSelectionMode.ExplicitSelection, reloaded.ContourSelection.SelectionMode);
        Assert.Single(reloaded.ContourSelection.Contours);
        Assert.Equal(101u, reloaded.ContourSelection.Contours[0].EntityId);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, reloaded.ContourSelection.Contours[0].EntityIndices);
        Assert.Single(reloaded.ContourSelection.Regions);
        Assert.Equal(201u, reloaded.ContourSelection.Regions[0].EntityId);
        Assert.Equal(new uint[] { 10, 11, 12 }, reloaded.ContourSelection.Regions[0].EdgeIds);

        // End condition entity
        Assert.NotNull(reloaded.EndConditionEntity);
        Assert.Equal(SelectionEntityType.Face, reloaded.EndConditionEntity.EntityType);
        Assert.Equal(0x5A12u, reloaded.EndConditionEntity.EntityId);
        Assert.Equal("Face_TopTarget", reloaded.EndConditionEntity.SurfaceName);

        // Body scope
        Assert.NotNull(reloaded.BodyScope);
        Assert.False(reloaded.BodyScope.AutoSelect);
        Assert.Equal(new uint[] { 1, 3 }, reloaded.BodyScope.BodyIds);
        Assert.Equal(2, reloaded.BodyScope.Bodies.Count);
        Assert.Equal("MainBody", reloaded.BodyScope.Bodies[0].Name);

        // Direction reference
        Assert.NotNull(reloaded.DirectionReference);
        Assert.Equal("DirectionEdge1", reloaded.DirectionReference.ReferenceName);
        Assert.NotNull(reloaded.DirectionReference.Vector);
        Assert.Equal(0.707106, reloaded.DirectionReference.Vector[1], precision: 6);
    }



    [Fact]
    public void RevolveFeatureModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new RevolveFeatureModel
        {
            FeatureName = "旋转凸台_测试",
            Angle = Math.PI * 1.5, // 270 deg
            FeatureId = 42
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<RevolveFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Angle, reloaded.Angle, precision: 6);
        Assert.Equal(original.TypeId, reloaded.TypeId);
    }

    [Fact]
    public void FilletFeatureModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new FilletFeatureModel
        {
            FeatureName = "圆角_测试",
            Radius = 0.0035 // 3.5mm
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<FilletFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Radius, reloaded.Radius, precision: 6);
        Assert.Equal(original.TypeId, reloaded.TypeId);
    }

    [Fact]
    public void ChamferFeatureModel_AngleDistance_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new ChamferFeatureModel(angleDistance: true)
        {
            FeatureName = "倒角_角度距离",
            Distance0 = 0.004, // 4mm
            Angle = Math.PI / 6.0 // 30 deg
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<ChamferFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Distance0!.Value, reloaded.Distance0!.Value, precision: 6);
        Assert.True(reloaded.IsAngleDistance);
        Assert.NotNull(reloaded.Angle);
        Assert.Equal(original.Angle.Value, reloaded.Angle.Value, precision: 6);
    }

    [Fact]
    public void ChamferFeatureModel_DistanceDistance_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new ChamferFeatureModel(angleDistance: false)
        {
            FeatureName = "倒角_距离距离",
            Distance0 = 0.003, // 3mm
            Distance1 = 0.005  // 5mm
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<ChamferFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Distance0!.Value, reloaded.Distance0!.Value, precision: 6);
        Assert.False(reloaded.IsAngleDistance);
        Assert.NotNull(reloaded.Distance1);
        Assert.Equal(original.Distance1.Value, reloaded.Distance1.Value, precision: 6);
    }

    [Fact]
    public void PatternFeatureModel_Linear_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new LinearPatternFeatureModel
        {
            FeatureName = "线性阵列_测试",
            D1TotalInstances = 5,
            D1Spacing = 0.025, // 25mm
            D2TotalInstances = 3,
            D2Spacing = 0.015  // 15mm
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<LinearPatternFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(5, reloaded.D1TotalInstances);
        Assert.Equal(0.025, reloaded.D1Spacing!.Value, precision: 6);
        Assert.Equal(3, reloaded.D2TotalInstances);
        Assert.Equal(0.015, reloaded.D2Spacing!.Value, precision: 6);
    }

    [Fact]
    public void PatternFeatureModel_Circular_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new CircularPatternFeatureModel
        {
            FeatureName = "圆周阵列_测试",
            TotalInstances = 8,
            Spacing = Math.PI * 2.0, // 360 deg
            EqualSpacing = true
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<CircularPatternFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(8, reloaded.TotalInstances);
        Assert.Equal(Math.PI * 2.0, reloaded.Spacing!.Value, precision: 6);
        Assert.True(reloaded.EqualSpacing);
    }

    [Fact]
    public void SheetMetalBaseFlangeModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new SheetMetalBaseFlangeModel
        {
            FeatureName = "基体-法兰_测试",
            Thickness = 0.0015,  // 1.5mm
            BendRadius = 0.0025, // 2.5mm
            KFactor = 0.45,
            SketchName = "Sketch_Sheet1"
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<SheetMetalBaseFlangeModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.Thickness, reloaded.Thickness, precision: 6);
        Assert.Equal(original.BendRadius, reloaded.BendRadius, precision: 6);
        Assert.Equal(original.KFactor, reloaded.KFactor, precision: 6);
        Assert.Equal(original.SketchName, reloaded.SketchName);
    }

    [Fact]
    public void CmgrHdr2Model_WithVersionGating_RoundtripsAccurately()
    {
        var original = new CmgrHdr2Model();
        original.Configurations.Add(new CmgrConfigEntry
        {
            ConfigId = 0,
            Name = "默认",
            ParentConfigId = null
        });
        original.Configurations.Add(new CmgrConfigEntry
        {
            ConfigId = 1,
            Name = "派生配置1",
            ParentConfigId = 0
        });

        // Test with version >= 2037 (enabling parent-config ID serialization)
        byte[] serialized = SerializeModel(original, docVersion: MfcVersionGates.CfgVerParent);
        var reloaded = DeserializeModel<CmgrHdr2Model>(serialized, docVersion: MfcVersionGates.CfgVerParent);

        Assert.Equal(2, reloaded.Configurations.Count);
        Assert.Equal("默认", reloaded.Configurations[0].Name);
        Assert.Null(reloaded.Configurations[0].ParentConfigId);

        Assert.Equal("派生配置1", reloaded.Configurations[1].Name);
        Assert.Equal(0u, reloaded.Configurations[1].ParentConfigId);
    }



    [Fact]
    public void MirrorFeatureModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new MirrorFeatureModel
        {
            FeatureName = "镜像_实体阵列",
            MirrorPlaneName = "Top Plane",
            FeatureId = 0x2A3B4C5Du,
            PatternBodyOnly = true,
            GeometryPattern = false
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<MirrorFeatureModel>(serialized);

        Assert.Equal(original.FeatureName, reloaded.FeatureName);
        Assert.Equal(original.MirrorPlaneName, reloaded.MirrorPlaneName);
        Assert.Equal(original.FeatureId, reloaded.FeatureId);
        Assert.True(reloaded.PatternBodyOnly);
        Assert.False(reloaded.GeometryPattern);
    }



    [Fact]
    public void SketchFeatureModel_IMfcSerializable_RoundtripsAccurately()
    {
        var original = new SketchFeatureModel
        {
            Name = "草图1_测试",
            Id = 10,
            Is3D = false,
            Points = new List<SketchPointModel>
            {
                new SketchPointModel { Id = 1, TableIndex = 0, X = 0.0, Y = 0.0, Z = 0.0 },
                new SketchPointModel { Id = 2, TableIndex = 1, X = 0.05, Y = 0.0, Z = 0.0 },
                new SketchPointModel { Id = 3, TableIndex = 2, X = 0.05, Y = 0.05, Z = 0.0 }
            },
            Segments = new List<SketchSegmentModel>
            {
                new SketchSegmentModel { Id = 101, Kind = SketchSegmentKind.Line, StartPointIndex = 0, EndPointIndex = 1 },
                new SketchSegmentModel { Id = 102, Kind = SketchSegmentKind.Line, StartPointIndex = 1, EndPointIndex = 2 },
                new SketchSegmentModel { Id = 103, Kind = SketchSegmentKind.Arc, StartPointIndex = 2, EndPointIndex = 0, CenterPointIndex = 0, Radius = 0.05 }
            }
        };

        byte[] serialized = SerializeModel(original);
        var reloaded = DeserializeModel<SketchFeatureModel>(serialized);

        Assert.Equal(original.Name, reloaded.Name);
        Assert.Equal(10, reloaded.Id);
        Assert.False(reloaded.Is3D);
        Assert.Equal(3, reloaded.Points.Count);
        Assert.Equal(0.05, reloaded.Points[1].X, precision: 6);
        Assert.Equal(3, reloaded.Segments.Count);
        Assert.Equal(SketchSegmentKind.Arc, reloaded.Segments[2].Kind);
        Assert.Equal(0.05, reloaded.Segments[2].Radius!.Value, precision: 6);

        var sketch = reloaded;
        Assert.Equal("草图1_测试", sketch.Name);
        Assert.Equal(3, sketch.Points.Count);
        Assert.Equal(3, sketch.Segments.Count);
        Assert.NotNull(sketch.Segments[0].StartPoint);
    }

}
