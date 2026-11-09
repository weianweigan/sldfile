using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
// using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Comprehensive verification suite for SolidWorks Sketch MFC CArchive serialization
/// based on the reverse metadata in reverse_workspace/tools/output/
/// (moProfileFeature_c, mo3DProfileFeature_c, sgPointHandle, sgLineHandle, moSketchRelMgr_c, moLengthParameter_c).
/// </summary>
public class SketchMfcSerializationTests
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
    public void SketchPointModel_Serialization_Roundtrips()
    {
        var original = new SketchPointModel
        {
            Id = 42,
            TableIndex = 3,
            X = 0.012345,
            Y = -0.054321,
            Z = 0.001
        };

        byte[] bytes = SerializeModel(original);
        var reloaded = DeserializeModel<SketchPointModel>(bytes);

        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(original.TableIndex, reloaded.TableIndex);
        Assert.Equal(original.X, reloaded.X, precision: 6);
        Assert.Equal(original.Y, reloaded.Y, precision: 6);
        Assert.Equal(original.Z, reloaded.Z, precision: 6);

        var pointDomain = reloaded;
        Assert.Equal(original.Id, pointDomain.Id);
        Assert.Equal(original.TableIndex, pointDomain.TableIndex);
        Assert.Equal(original.X, pointDomain.X, precision: 6);
    }

    [Fact]
    public void SketchSegmentModel_AllKinds_Serialization_Roundtrips()
    {
        // 1. Line
        var lineModel = new SketchSegmentModel
        {
            Id = 101,
            Kind = SketchSegmentKind.Line,
            IsConstruction = false,
            StartPointIndex = 0,
            EndPointIndex = 1
        };
        byte[] lineBytes = SerializeModel(lineModel);
        var reloadedLine = DeserializeModel<SketchSegmentModel>(lineBytes);
        Assert.Equal(101u, reloadedLine.Id);
        Assert.Equal(SketchSegmentKind.Line, reloadedLine.Kind);
        Assert.False(reloadedLine.IsConstruction);
        Assert.Equal(0, reloadedLine.StartPointIndex);
        Assert.Equal(1, reloadedLine.EndPointIndex);

        // 2. Centerline (Construction)
        var centerLineModel = new SketchSegmentModel
        {
            Id = 102,
            Kind = SketchSegmentKind.Line,
            IsConstruction = true,
            StartPointIndex = 1,
            EndPointIndex = 2
        };
        byte[] clBytes = SerializeModel(centerLineModel);
        var reloadedCl = DeserializeModel<SketchSegmentModel>(clBytes);
        Assert.True(reloadedCl.IsConstruction);

        // 3. Arc / Circle
        var arcModel = new SketchSegmentModel
        {
            Id = 103,
            Kind = SketchSegmentKind.Arc,
            IsConstruction = false,
            StartPointIndex = 2,
            EndPointIndex = 3,
            CenterPointIndex = 4,
            Radius = 0.025,
            IsCircle = false,
            Direction = 1
        };
        byte[] arcBytes = SerializeModel(arcModel);
        var reloadedArc = DeserializeModel<SketchSegmentModel>(arcBytes);
        Assert.Equal(SketchSegmentKind.Arc, reloadedArc.Kind);
        Assert.Equal(2, reloadedArc.StartPointIndex);
        Assert.Equal(3, reloadedArc.EndPointIndex);
        Assert.Equal(4, reloadedArc.CenterPointIndex);
        Assert.Equal(0.025, reloadedArc.Radius!.Value, precision: 6);
        Assert.False(reloadedArc.IsCircle!.Value);
        Assert.Equal(1, reloadedArc.Direction);

        // 4. Ellipse
        var ellipseModel = new SketchSegmentModel
        {
            Id = 104,
            Kind = SketchSegmentKind.Ellipse,
            CenterPointIndex = 0,
            MajorPointIndex = 1,
            MinorPointIndex = 2
        };
        byte[] ellipseBytes = SerializeModel(ellipseModel);
        var reloadedEllipse = DeserializeModel<SketchSegmentModel>(ellipseBytes);
        Assert.Equal(SketchSegmentKind.Ellipse, reloadedEllipse.Kind);
        Assert.Equal(0, reloadedEllipse.CenterPointIndex);
        Assert.Equal(1, reloadedEllipse.MajorPointIndex);
        Assert.Equal(2, reloadedEllipse.MinorPointIndex);

        // 5. Text
        var textModel = new SketchSegmentModel
        {
            Id = 105,
            Kind = SketchSegmentKind.Text,
            StartPointIndex = 0,
            EndPointIndex = 1,
            Text = "SolidWorks_MFC_Reversed"
        };
        byte[] textBytes = SerializeModel(textModel);
        var reloadedText = DeserializeModel<SketchSegmentModel>(textBytes);
        Assert.Equal(SketchSegmentKind.Text, reloadedText.Kind);
        Assert.Equal("SolidWorks_MFC_Reversed", reloadedText.Text);
    }

    [Fact]
    public void SketchRelationModel_AndReferences_Roundtrips()
    {
        var relModel = new SketchRelationModel
        {
            TypeCode = (int)SketchRelationKind.Parallel,
            Layout = "Common",
            References = new List<SketchEntityReferenceModel>
            {
                new SketchEntityReferenceModel { ClassReference = 0x80BD, Index = 0 },
                new SketchEntityReferenceModel { ClassReference = 0x80BD, Index = 1 }
            }
        };

        byte[] bytes = SerializeModel(relModel);
        var reloaded = DeserializeModel<SketchRelationModel>(bytes);

        Assert.Equal(SketchRelationKind.Parallel, reloaded.Kind);
        Assert.Equal("Common", reloaded.Layout);
        Assert.Equal(2, reloaded.References.Count);
        Assert.Equal(0x80BD, reloaded.References[0].ClassReference);
        Assert.Equal(0, reloaded.References[0].Index);
        Assert.Equal(0x80BD, reloaded.References[1].ClassReference);
        Assert.Equal(1, reloaded.References[1].Index);
    }

    [Fact]
    public void SketchDimensionModel_moLengthParameter_Roundtrips()
    {
        var dimModel = new SketchDimensionModel
        {
            Name = "D1@Sketch1",
            SystemValue = 0.05,
            DisplayValue = "50mm",
            ValueSource = "BinaryParameter",
            RelationIndex = 0
        };

        byte[] bytes = SerializeModel(dimModel);
        var reloaded = DeserializeModel<SketchDimensionModel>(bytes);

        Assert.Equal("D1@Sketch1", reloaded.Name);
        Assert.Equal(0.05, reloaded.SystemValue!.Value, precision: 6);
        Assert.Equal("50mm", reloaded.DisplayValue);
        Assert.Equal("BinaryParameter", reloaded.ValueSource);
        Assert.Equal(0, reloaded.RelationIndex);
    }

    [Fact]
    public void SketchFeatureModel_Full2DSketch_Roundtrip_AndGraphIntegrity()
    {
        // Construct a full 2D sketch representing a slot profile with dimensions & relations
        var original = new SketchFeatureModel
        {
            MfcClassName = SketchFeatureModel.ClassName2D,
            TypeId = 0x49u,
            Name = "SlotProfileSketch",
            Id = 100,
            Is3D = false,
            Points = new List<SketchPointModel>
            {
                new SketchPointModel { Id = 1, TableIndex = 0, X = 0.0, Y = 0.0, Z = 0.0 },
                new SketchPointModel { Id = 2, TableIndex = 1, X = 0.1, Y = 0.0, Z = 0.0 },
                new SketchPointModel { Id = 3, TableIndex = 2, X = 0.1, Y = 0.04, Z = 0.0 },
                new SketchPointModel { Id = 4, TableIndex = 3, X = 0.0, Y = 0.04, Z = 0.0 }
            },
            Segments = new List<SketchSegmentModel>
            {
                new SketchSegmentModel { Id = 11, Kind = SketchSegmentKind.Line, StartPointIndex = 0, EndPointIndex = 1 },
                new SketchSegmentModel { Id = 12, Kind = SketchSegmentKind.Line, StartPointIndex = 1, EndPointIndex = 2 },
                new SketchSegmentModel { Id = 13, Kind = SketchSegmentKind.Line, StartPointIndex = 2, EndPointIndex = 3 },
                new SketchSegmentModel { Id = 14, Kind = SketchSegmentKind.Line, StartPointIndex = 3, EndPointIndex = 0 }
            },
            Relations = new List<SketchRelationModel>
            {
                new SketchRelationModel
                {
                    TypeCode = (int)SketchRelationKind.Horizontal,
                    Layout = "Common",
                    References = new List<SketchEntityReferenceModel> { new SketchEntityReferenceModel { ClassReference = 0x80BD, Index = 0 } }
                },
                new SketchRelationModel
                {
                    TypeCode = (int)SketchRelationKind.Vertical,
                    Layout = "Common",
                    References = new List<SketchEntityReferenceModel> { new SketchEntityReferenceModel { ClassReference = 0x80BD, Index = 1 } }
                },
                new SketchRelationModel
                {
                    TypeCode = (int)SketchRelationKind.Distance,
                    Layout = "Dimension",
                    References = new List<SketchEntityReferenceModel>
                    {
                        new SketchEntityReferenceModel { ClassReference = 0x80AD, Index = 0 },
                        new SketchEntityReferenceModel { ClassReference = 0x80AD, Index = 1 }
                    }
                }
            },
            Dimensions = new List<SketchDimensionModel>
            {
                new SketchDimensionModel
                {
                    Name = "D1@SlotProfileSketch",
                    SystemValue = 0.100,
                    DisplayValue = "100.00mm",
                    ValueSource = "BinaryParameter",
                    RelationIndex = 2
                }
            }
        };

        // 1. Serialize via MfcArchive
        byte[] bytes = SerializeModel(original);
        Assert.NotEmpty(bytes);

        // 2. Deserialize via MfcArchive
        var reloaded = DeserializeModel<SketchFeatureModel>(bytes);

        Assert.Equal(original.Name, reloaded.Name);
        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(0x49u, reloaded.TypeId);
        Assert.False(reloaded.Is3D);
        Assert.Equal(4, reloaded.Points.Count);
        Assert.Equal(4, reloaded.Segments.Count);
        Assert.Equal(3, reloaded.Relations.Count);
        Assert.Single(reloaded.Dimensions);

        // 3. Verify domain model reconstruction & Graph Binding
        var feature = reloaded;
        Assert.Equal("SlotProfileSketch", feature.Name);
        Assert.Equal(100, feature.Id);
        Assert.Equal(4, feature.Points.Count);
        Assert.Equal(4, feature.Segments.Count);

        // Check segment points resolve to actual instances
        Assert.Same(feature.Points[0], feature.Segments[0].StartPoint);
        Assert.Same(feature.Points[1], feature.Segments[0].EndPoint);

        // Check relation resolution
        var horizRel = feature.Relations[0];
        Assert.Equal(SketchRelationKind.Horizontal, horizRel.Kind);
        var horizSegments = horizRel.GetSegments();
        Assert.Single(horizSegments);
        Assert.Same(feature.Segments[0], horizSegments[0]);

        var distRel = feature.Relations[2];
        Assert.Equal(SketchRelationKind.Distance, distRel.Kind);
        var distPoints = distRel.GetPoints();
        Assert.Equal(2, distPoints.Count);
        Assert.Same(feature.Points[0], distPoints[0]);
        Assert.Same(feature.Points[1], distPoints[1]);

        // Check dimension attachment
        var dim = Assert.Single(feature.Dimensions);
        Assert.Equal("D1@SlotProfileSketch", dim.Name);
        Assert.Same(distRel, dim.Relation);
        var attached = dim.GetAttachedPoints();
        Assert.Equal(2, attached.Count);
        Assert.Contains(feature.Points[0], attached);
        Assert.Contains(feature.Points[1], attached);
    }

    [Fact]
    public void SketchFeatureModel_3DSketch_Roundtrips()
    {
        var original = new SketchFeatureModel
        {
            MfcClassName = SketchFeatureModel.ClassName3D,
            Name = "3DSketch1",
            Id = 101,
            Is3D = true,
            Points = new List<SketchPointModel>
            {
                new SketchPointModel { Id = 1, TableIndex = 0, X = 0.01, Y = 0.02, Z = 0.03 },
                new SketchPointModel { Id = 2, TableIndex = 1, X = 0.04, Y = 0.05, Z = 0.06 }
            },
            Segments = new List<SketchSegmentModel>
            {
                new SketchSegmentModel { Id = 201, Kind = SketchSegmentKind.Line, StartPointIndex = 0, EndPointIndex = 1 }
            }
        };

        byte[] bytes = SerializeModel(original);
        var reloaded = DeserializeModel<SketchFeatureModel>(bytes);

        Assert.Equal("3DSketch1", reloaded.Name);
        Assert.True(reloaded.Is3D);
        Assert.Equal(0.03, reloaded.Points[0].Z, precision: 6);
        Assert.Equal(0.06, reloaded.Points[1].Z, precision: 6);

        var feature = reloaded;
        Assert.True(feature.Is3D);
    }

    [Fact]
    public void SketchFeatureModel_RealSpecimens_Parity_Parallel()
    {
        string path = TestFileProvider.Resolve("Sketches2026", "parallel.SLDPRT");
        using var file = SwFile.Open(path);
        var sketch = Assert.Single(file.GetSketches());

        var model = sketch;
        Assert.Equal(sketch.Name, model.Name);
        Assert.Equal(sketch.Points.Count, model.Points.Count);
        Assert.Equal(sketch.Segments.Count, model.Segments.Count);
        Assert.Equal(sketch.Relations.Count, model.Relations.Count);

        // Serialize and reload
        byte[] bytes = SerializeModel(model);
        var reloadedModel = DeserializeModel<SketchFeatureModel>(bytes);
        var roundtripFeature = reloadedModel;

        Assert.Equal(sketch.Name, roundtripFeature.Name);
        Assert.Equal(sketch.Points.Count, roundtripFeature.Points.Count);
        Assert.Equal(sketch.Segments.Count, roundtripFeature.Segments.Count);
        Assert.Equal(sketch.Relations.Count, roundtripFeature.Relations.Count);

        var originalRel = sketch.Relations[0];
        var reloadedRel = roundtripFeature.Relations[0];
        Assert.Equal(originalRel.Kind, reloadedRel.Kind);
        Assert.Equal(originalRel.References.Count, reloadedRel.References.Count);
    }

    [Fact]
    public void SketchFeatureModel_RealSpecimens_Parity_Concentric()
    {
        string path = TestFileProvider.Resolve("Sketches2026", "concentric.SLDPRT");
        using var file = SwFile.Open(path);
        var sketch = Assert.Single(file.GetSketches());

        var model = sketch;
        byte[] bytes = SerializeModel(model);
        var reloadedModel = DeserializeModel<SketchFeatureModel>(bytes);
        var roundtripFeature = reloadedModel;

        Assert.Equal(sketch.Name, roundtripFeature.Name);
        Assert.Equal(sketch.Segments.Count, roundtripFeature.Segments.Count);
        var rel = roundtripFeature.Relations.FirstOrDefault(r => r.Kind == SketchRelationKind.Concentric);
        Assert.NotNull(rel);
        Assert.Equal(2, rel.GetSegments().Count);
    }

    [Fact]
    public void SketchFeatureModel_RealSpecimens_Parity_Dimension10()
    {
        string path = TestFileProvider.Resolve("Sketches2026", "dimension10.SLDPRT");
        using var file = SwFile.Open(path);
        var sketch = Assert.Single(file.GetSketches());

        var model = sketch;
        byte[] bytes = SerializeModel(model);
        var reloadedModel = DeserializeModel<SketchFeatureModel>(bytes);
        var roundtripFeature = reloadedModel;

        Assert.Equal(sketch.Name, roundtripFeature.Name);
        Assert.Equal(sketch.Dimensions.Count, roundtripFeature.Dimensions.Count);
        if (sketch.Dimensions.Count > 0)
        {
            Assert.Equal(sketch.Dimensions[0].Name, roundtripFeature.Dimensions[0].Name);
            Assert.Equal(sketch.Dimensions[0].SystemValue, roundtripFeature.Dimensions[0].SystemValue);
        }
    }

    [Fact]
    public void MoParameterModels_LengthAndAngle_SerializationRoundtrip()
    {
        // 1. Length Parameter
        var lengthParam = new MoLengthParameterModel("D1@Sketch1", 0.075);
        byte[] lenBytes = SerializeModel(lengthParam);
        var lenReloaded = DeserializeModel<MoLengthParameterModel>(lenBytes);
        Assert.Equal("moLengthParameter_c", lenReloaded.ClassName);
        Assert.Equal("D1@Sketch1", lenReloaded.Name);
        Assert.Equal(0.075, lenReloaded.Value, precision: 6);

        // 2. Angle Parameter
        var angleParam = new MoAngleParameterModel("A1@Sketch1", Math.PI / 4.0);
        byte[] angBytes = SerializeModel(angleParam);
        var angReloaded = DeserializeModel<MoAngleParameterModel>(angBytes);
        Assert.Equal("moAngleParameter_c", angReloaded.ClassName);
        Assert.Equal("A1@Sketch1", angReloaded.Name);
        Assert.Equal(Math.PI / 4.0, angReloaded.Value, precision: 6);
    }

    [Fact]
    public void MoDisplayDimModels_Hierarchy_SerializationRoundtrip()
    {
        // 1. moDisplayDistanceDim_c (Linear)
        var distDim = new MoDisplayDistanceDimModel
        {
            TextX = 0.05, TextY = 0.02, TextZ = 0.0,
            DirectionX = 1.0, DirectionY = 0.0, DirectionZ = 0.0,
            NormalX = 0.0, NormalY = 1.0, NormalZ = 0.0,
            WitnessPoint1X = 0.0, WitnessPoint1Y = 0.0, WitnessPoint1Z = 0.0,
            WitnessPoint2X = 0.05, WitnessPoint2Y = 0.0, WitnessPoint2Z = 0.0,
            IsDriven = false
        };
        byte[] distBytes = SerializeModel(distDim);
        var distReloaded = DeserializeModel<MoDisplayDistanceDimModel>(distBytes);
        Assert.Equal(SketchDimensionType.Linear, distReloaded.DimensionType);
        Assert.Equal("moDisplayDistanceDim_c", distReloaded.ClassName);
        Assert.Equal(0.05, distReloaded.TextX!.Value, precision: 6);
        Assert.Equal(1.0, distReloaded.DirectionX!.Value, precision: 6);
        var (p1, p2) = distReloaded.GetWitnessPoints();
        Assert.NotNull(p1); Assert.NotNull(p2);
        Assert.Equal(0.05, p2!.Value.X, precision: 6);

        // 2. moDisplayRadialDim_c (Radius)
        var radDim = new MoDisplayRadialDimModel
        {
            CenterX = 0.01, CenterY = 0.02, CenterZ = 0.0,
            Radius = 0.015,
            TextX = 0.025, TextY = 0.02, TextZ = 0.0
        };
        Assert.Equal(SketchDimensionType.Radius, radDim.DimensionType);
        Assert.Equal("moDisplayRadialDim_c", radDim.ClassName);

        // 3. moDisplayDiameterDim_c (Diameter)
        var diaDim = new MoDisplayDiameterDimModel
        {
            CenterX = 0.0, CenterY = 0.0, CenterZ = 0.0,
            Diameter = 0.03
        };
        Assert.Equal(SketchDimensionType.Diameter, diaDim.DimensionType);
        Assert.Equal("moDisplayDiameterDim_c", diaDim.ClassName);
        Assert.Equal(0.015, diaDim.Radius!.Value, precision: 6);

        // 4. moDisplayAngularDim_c (Angle)
        var angDim = new MoDisplayAngularDimModel
        {
            VertexX = 0.0, VertexY = 0.0, VertexZ = 0.0,
            ArcAngle = 45.0
        };
        Assert.Equal(SketchDimensionType.Angle, angDim.DimensionType);
        Assert.Equal("moDisplayAngularDim_c", angDim.ClassName);

        // 5. moDisplayArcLengthDim_c (ArcLength)
        var arcDim = new MoDisplayArcLengthDimModel
        {
            ArcLength = 0.047
        };
        Assert.Equal(SketchDimensionType.ArcLength, arcDim.DimensionType);
        Assert.Equal("moDisplayArcLengthDim_c", arcDim.ClassName);

        // 6. moDisplayOrdDim_c (Ordinate)
        var ordDim = new MoDisplayOrdDimModel
        {
            BasePointX = 0.0, BasePointY = 0.0, BasePointZ = 0.0,
            OrdinateOffset = 0.025
        };
        Assert.Equal(SketchDimensionType.Ordinate, ordDim.DimensionType);
        Assert.Equal("moDisplayOrdDim_c", ordDim.ClassName);

        // 7. moDisplayChamferDim_c (Chamfer)
        var chfDim = new MoDisplayChamferDimModel
        {
            Distance1 = 0.005, Angle = 45.0
        };
        Assert.Equal(SketchDimensionType.Chamfer, chfDim.DimensionType);
        Assert.Equal("moDisplayChamferDim_c", chfDim.ClassName);
    }

    [Fact]
    public void SketchDimensionModel_UnifiedModel_LinksParameterAndDisplayDim()
    {
        var param = new MoLengthParameterModel("D2@Sketch1", 0.035);
        var dispDim = new MoDisplayDistanceDimModel
        {
            TextX = 0.02, TextY = 0.01, TextZ = 0.0,
            DirectionX = 0.0, DirectionY = 1.0, DirectionZ = 0.0,
            WitnessPoint1X = 0.0, WitnessPoint1Y = 0.0, WitnessPoint1Z = 0.0,
            WitnessPoint2X = 0.0, WitnessPoint2Y = 0.035, WitnessPoint2Z = 0.0
        };

        var unified = new SketchDimensionModel(param, dispDim);

        // Verification of property forwarding and delegation
        Assert.Same(param, unified.Parameter);
        Assert.Same(dispDim, unified.DisplayDimension);
        Assert.Equal("D2@Sketch1", unified.Name);
        Assert.Equal(0.035, unified.SystemValue!.Value, precision: 6);
        Assert.Equal(SketchDimensionType.Linear, unified.DimensionType);
        Assert.Equal("moDisplayDistanceDim_c", unified.DisplayDimClassName);
        Assert.True(unified.HasTextPosition);
        Assert.True(unified.HasDirection);

        var textPos = unified.GetTextPosition();
        Assert.NotNull(textPos);
        Assert.Equal(0.02, textPos!.Value.X, precision: 6);
        Assert.Equal(0.01, textPos!.Value.Y, precision: 6);

        // Verify copy constructor produces deep copies of parameter and display dimension
        var cloned = new SketchDimensionModel(unified);
        Assert.NotSame(unified.Parameter, cloned.Parameter);
        Assert.NotSame(unified.DisplayDimension, cloned.DisplayDimension);
        Assert.Equal(unified.Name, cloned.Name);
        Assert.Equal(unified.SystemValue, cloned.SystemValue);
        Assert.Equal(unified.DimensionType, cloned.DimensionType);
        Assert.Equal(unified.TextX, cloned.TextX);

        // Verify ToFeature conversion carries the typed objects
        var featureDim = unified.ToFeature();
        Assert.Same(unified.Parameter, featureDim.Parameter);
        Assert.Same(unified.DisplayDimension, featureDim.DisplayDimension);
        Assert.Equal(unified.Name, featureDim.Name);
        Assert.Equal(unified.SystemValue, featureDim.SystemValue);
    }
}
