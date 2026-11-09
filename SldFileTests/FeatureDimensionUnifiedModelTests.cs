using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public sealed class FeatureDimensionUnifiedModelTests
{
    private static string GetSamplePath(string fileName)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var candidate1 = Path.Combine(repoRoot, "models", fileName);
        if (File.Exists(candidate1)) return candidate1;
        var candidate2 = Path.Combine(repoRoot, "SldFileTests", "TestFiles", "Controlled", fileName);
        if (File.Exists(candidate2)) return candidate2;
        var candidate3 = Path.Combine(repoRoot, "SldFileTests", "TestFiles", fileName);
        if (File.Exists(candidate3)) return candidate3;
        var match = Directory.GetFiles(Path.Combine(repoRoot, "SldFileTests", "TestFiles"), fileName, SearchOption.AllDirectories).FirstOrDefault();
        if (match != null) return match;
        throw new FileNotFoundException($"Test sample not found: {fileName}");
    }

    [Fact]
    public void FeatureDimension_ConstructedFromParameterAndDisplayDim_ShouldPopulateTypedObjects()
    {
        var param = new MoLengthParameterModel("D1@Extrude1", 0.025);
        var disp = new MoDisplayDistanceDimModel
        {
            TextX = 0.05,
            TextY = 0.05,
            TextZ = 0.0,
            DirectionX = 1.0,
            DirectionY = 0.0,
            DirectionZ = 0.0
        };

        var featDim = new FeatureDimension(param, disp);

        Assert.Equal("D1@Extrude1", featDim.Name);
        Assert.Equal(0.025, featDim.NumericValue);
        Assert.Equal(0.025, featDim.SystemValue);
        Assert.Equal(SketchDimensionType.Linear, featDim.DimensionType);
        Assert.Same(param, featDim.Parameter);
        Assert.Same(disp, featDim.DisplayDimension);
        Assert.NotNull(featDim.Model);
        Assert.Equal("25.00mm", featDim.Value);

        // Roundtrip to DimensionModel and back
        var dimModel = featDim.ToDimensionModel();
        Assert.Same(param, dimModel.Parameter);
        Assert.Same(disp, dimModel.DisplayDimension);
        Assert.Equal(0.05, dimModel.TextX);
    }

    [Fact]
    public void FeatureDimension_AngleParameter_ShouldFormatDegreesAndSetAngularType()
    {
        var angParam = new MoAngleParameterModel("A1@Revolve1", Math.PI / 2.0); // 90 deg
        var angDisp = new MoDisplayAngularDimModel { ArcAngle = Math.PI / 2.0 };

        var featDim = new FeatureDimension(angParam, angDisp);

        Assert.Equal("A1@Revolve1", featDim.Name);
        Assert.Equal(Math.PI / 2.0, featDim.NumericValue!.Value, precision: 6);
        Assert.Equal(SketchDimensionType.Angle, featDim.DimensionType);
        Assert.Equal("90.00°", featDim.Value);
    }

    [Fact]
    public void FilletFeatureModel_ShouldExposeUnifiedDimensionModel()
    {
        var fillet = new FilletFeatureModel
        {
            FeatureName = "圆角1",
            Radius = 0.005 // 5mm
        };

        var dims = fillet.Dimensions;
        var dim = Assert.Single(dims);

        Assert.Equal("Radius@圆角1", dim.Name);
        Assert.Equal(0.005, dim.SystemValue);
        Assert.Equal(SketchDimensionType.Radius, dim.DimensionType);
        Assert.NotNull(dim.Parameter);
        Assert.IsType<MoLengthParameterModel>(dim.Parameter);
        Assert.NotNull(dim.DisplayDimension);
        Assert.IsType<MoDisplayRadialDimModel>(dim.DisplayDimension);

        var featDim = dim.ToFeature();
        Assert.Equal(0.005, featDim.NumericValue);
        Assert.Equal(SketchDimensionType.Radius, featDim.DimensionType);
    }

    [Fact]
    public void RevolveFeatureModel_ShouldExposeUnifiedDimensionModel()
    {
        var revolve = new RevolveFeatureModel
        {
            FeatureName = "旋转1",
            Angle = Math.PI, // 180 deg
            BothDirections = true,
            AngleDirection2 = Math.PI / 4.0 // 45 deg
        };

        var dims = revolve.Dimensions;
        Assert.Equal(2, dims.Count);

        var d1 = dims[0];
        Assert.Equal("Angle@旋转1", d1.Name);
        Assert.Equal(Math.PI, d1.SystemValue!.Value, precision: 6);
        Assert.Equal(SketchDimensionType.Angle, d1.DimensionType);
        Assert.IsType<MoAngleParameterModel>(d1.Parameter);

        var d2 = dims[1];
        Assert.Equal("Angle2@旋转1", d2.Name);
        Assert.Equal(Math.PI / 4.0, d2.SystemValue!.Value, precision: 6);
        Assert.Equal(SketchDimensionType.Angle, d2.DimensionType);
    }

    [Fact]
    public void ExtrudeFeatureModel_ShouldExposeUnifiedDimensionModel()
    {
        var extrude = new ExtrudeFeatureModel
        {
            FeatureName = "凸台-拉伸1",
            Depth = 0.020, // 20mm
            BothDirections = true,
            Direction2Depth = 0.010, // 10mm
            DraftAngle = 0.0349 // ~2 deg
        };

        var dims = extrude.Dimensions;
        Assert.Equal(3, dims.Count);

        Assert.Equal("Depth@凸台-拉伸1", dims[0].Name);
        Assert.Equal(0.020, dims[0].SystemValue);
        Assert.Equal(SketchDimensionType.Linear, dims[0].DimensionType);

        Assert.Equal("Depth2@凸台-拉伸1", dims[1].Name);
        Assert.Equal(0.010, dims[1].SystemValue);

        Assert.Equal("Draft@凸台-拉伸1", dims[2].Name);
        Assert.Equal(0.0349, dims[2].SystemValue!.Value, precision: 4);
        Assert.Equal(SketchDimensionType.Angle, dims[2].DimensionType);
    }

    [Fact]
    public void NativeFileFeatures_ShouldPopulateUnifiedFeatureDimensions()
    {
        var path = GetSamplePath("sm_base_flange_10mm.SLDPRT");
        using var file = SwFile.Open(path, readOnly: true);

        var smFeat = file.Features.FirstOrDefault(f => f.Name.Contains("基体-法兰") || f.Name.Contains("BaseFlange") || f.Kind == FeatureKind.Extrusion);
        Assert.NotNull(smFeat);

        var thicknessDim = smFeat!.Dimensions.FirstOrDefault(d => d.Name == "Thickness");
        Assert.NotNull(thicknessDim);
        Assert.NotNull(thicknessDim!.Parameter);
        Assert.IsType<MoLengthParameterModel>(thicknessDim.Parameter);
        Assert.Equal(0.001, thicknessDim.NumericValue!.Value, precision: 4);

        var radiusDim = smFeat.Dimensions.FirstOrDefault(d => d.Name == "BendRadius");
        Assert.NotNull(radiusDim);
        Assert.NotNull(radiusDim!.DisplayDimension);
        Assert.IsType<MoDisplayRadialDimModel>(radiusDim.DisplayDimension);
        Assert.Equal(SketchDimensionType.Radius, radiusDim.DimensionType);
        Assert.Equal(0.002, radiusDim.NumericValue!.Value, precision: 4);
    }
}
