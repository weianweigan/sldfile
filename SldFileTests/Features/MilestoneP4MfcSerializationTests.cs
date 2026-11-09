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
/// Comprehensive verification suite demonstrating that all Milestone P4 objects
/// (DrBalloonMfcModel, DrGtolMfcModel, DrDatumMfcModel, DrBomTableMfcModel, DrawingDocumentMfcModel,
/// CmgrConfigurationModel, CmgrModel, MoMaterialModel, MoDensityParameterModel)
/// uniformly implement and roundtrip via the IMfcSerializable contract with exact attribute retention
/// and double-roundtrip byte equality.
/// </summary>
public class MilestoneP4MfcSerializationTests
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

    // ── Drawing Annotations (moBalloon_c, moGtol_c, moDatum_c, moBomTemplate_c) ──

    [Fact]
    public void DrBalloonMfcModel_RoundtripByteEquality()
    {
        var original = new DrBalloonMfcModel("5", 0.125, 0.250, "Hexagon")
        {
            Size = 0.012,
            CustomText = "PART-B",
            AttachedEntityId = 1001
        };
        original.LeaderPoints.Add(new DrPoint2D(0.120, 0.240));
        original.LeaderPoints.Add(new DrPoint2D(0.110, 0.230));

        byte[] b1 = SerializeModel(original);
        var restored = DeserializeModel<DrBalloonMfcModel>(b1);

        Assert.Equal("5", restored.ItemNumber);
        Assert.Equal("Hexagon", restored.BalloonShape);
        Assert.Equal("PART-B", restored.CustomText);
        Assert.Equal(0.125, restored.PositionX, 4);
        Assert.Equal(0.250, restored.PositionY, 4);
        Assert.Equal(0.012, restored.Size, 4);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void DrGtolMfcModel_RoundtripByteEquality()
    {
        var original = new DrGtolMfcModel("Perpendicularity", 0.02, "A", 0.05, 0.08)
        {
            MaterialCondition = "MMC",
            SecondaryDatum = "B",
            TertiaryDatum = "C",
            FrameCount = 2
        };

        byte[] b1 = SerializeModel(original);
        var restored = DeserializeModel<DrGtolMfcModel>(b1);

        Assert.Equal("Perpendicularity", restored.Symbol);
        Assert.Equal(0.02, restored.ToleranceValue, 4);
        Assert.Equal("MMC", restored.MaterialCondition);
        Assert.Equal("A", restored.PrimaryDatum);
        Assert.Equal("B", restored.SecondaryDatum);
        Assert.Equal("C", restored.TertiaryDatum);
        Assert.Equal(0.05, restored.PositionX, 4);
        Assert.Equal(0.08, restored.PositionY, 4);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void DrDatumMfcModel_RoundtripByteEquality()
    {
        var original = new DrDatumMfcModel("DATUM B", 0.300, 0.150, isFilled: true);
        original.LeaderPoints.Add(new DrPoint2D(0.300, 0.140));

        byte[] b1 = SerializeModel(original);
        var restored = DeserializeModel<DrDatumMfcModel>(b1);

        Assert.Equal("DATUM B", restored.DatumLabel);
        Assert.Equal(0.300, restored.PositionX, 4);
        Assert.Equal(0.150, restored.PositionY, 4);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void DrBomTableMfcModel_RoundtripByteEquality()
    {
        var original = new DrBomTableMfcModel("MainBOM", 0.01, 0.02);
        original.Columns.Add(new DrBomColumnModel { Title = "ITEM NO.", PropertyBinding = "ItemNo", Width = 0.015 });
        original.Columns.Add(new DrBomColumnModel { Title = "PART NUMBER", PropertyBinding = "PartNumber", Width = 0.040 });
        original.Columns.Add(new DrBomColumnModel { Title = "QTY.", PropertyBinding = "Quantity", Width = 0.015 });

        original.Rows.Add(new DrBomRowModel { ItemNumber = "1", PartNumber = "BRACKET-01", Description = "Steel Bracket", Quantity = 2 });
        original.Rows.Add(new DrBomRowModel { ItemNumber = "2", PartNumber = "BOLT-M6", Description = "Hex Bolt", Quantity = 8 });

        byte[] b1 = SerializeModel(original);
        var restored = DeserializeModel<DrBomTableMfcModel>(b1);

        Assert.Equal("MainBOM", restored.TableName);
        Assert.Equal(0.01, restored.AnchorX, 4);
        Assert.Equal(0.02, restored.AnchorY, 4);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void DrawingDocumentMfcModel_AggregatesAllAnnotations()
    {
        var doc = new DrawingDocumentMfcModel();
        doc.Balloons.Add(new DrBalloonMfcModel("1", 0.1, 0.1));
        doc.Gtols.Add(new DrGtolMfcModel("Position", 0.05, "A"));
        doc.Datums.Add(new DrDatumMfcModel("A", 0.2, 0.2));
        doc.BomTables.Add(new DrBomTableMfcModel("TopBOM", 0.0, 0.0));

        byte[] b1 = SerializeModel(doc);
        var restored = DeserializeModel<DrawingDocumentMfcModel>(b1);

        Assert.NotNull(restored.Balloons);
        Assert.NotNull(restored.Gtols);
        Assert.NotNull(restored.Datums);
        Assert.NotNull(restored.BomTables);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    // ── CMgr Configuration Tree (moConfiguration_c) ──

    [Fact]
    public void CmgrConfigurationModel_DerivedAndSuppression_Roundtrip()
    {
        var config = new CmgrConfigurationModel("HeavyDuty", id: 2, parentId: 1)
        {
            Description = "Heavy duty variant with thickened flanges",
            Comment = "Engineering release rev C",
            IsActive = true
        };
        config.FeatureSuppressionStates["LighteningHole1"] = true;
        config.FeatureSuppressionStates["RibReinforcement"] = false;

        config.DimensionOverrides["D1@Boss-Extrude1"] = 0.025;
        config.DimensionOverrides["Thickness@Sheet-Metal"] = 0.003;

        Assert.True(config.IsDerived);

        byte[] b1 = SerializeModel(config);
        var restored = DeserializeModel<CmgrConfigurationModel>(b1);

        Assert.Equal("HeavyDuty", restored.Name);
        Assert.Equal(2u, restored.ConfigId);
        Assert.Equal(1u, restored.ParentConfigId);
        Assert.True(restored.IsDerived);
        Assert.Equal("Heavy duty variant with thickened flanges", restored.Description);
        Assert.Equal("Engineering release rev C", restored.Comment);
        Assert.True(restored.IsActive);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void CmgrModel_WithConfigurations_Roundtrip()
    {
        var cmgr = new CmgrModel
        {
            DisplayName = "Part Configuration Manager",
            VisualProperties = new CmgrVisualProperties
            {
                ColorRgb = 0x00FF8000,
                Transparency = 0.1f,
                Ambient = 0.3f,
                Diffuse = 0.7f,
                Specular = 0.5f,
                Shininess = 0.6f
            }
        };

        cmgr.Configurations.Add(new CmgrConfigurationModel("Default", id: 1));
        cmgr.Configurations.Add(new CmgrConfigurationModel("SubConfig", id: 2, parentId: 1));

        byte[] b1 = SerializeModel(cmgr);
        var restored = DeserializeModel<CmgrModel>(b1);

        Assert.Equal("Part Configuration Manager", restored.DisplayName);
        Assert.NotNull(restored.VisualProperties);
        Assert.Equal(0x00FF8000u, restored.VisualProperties!.ColorRgb);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    // ── Config-0 Material & Physical Density (moMaterial_c, moDensityParameter_c) ──

    [Fact]
    public void MoMaterialModel_RoundtripByteEquality()
    {
        var original = new MoMaterialModel("AISI 316 Stainless Steel", "SOLIDWORKS Materials", "Steel");

        byte[] b1 = SerializeModel(original);
        var restored = DeserializeModel<MoMaterialModel>(b1);

        Assert.Equal("AISI 316 Stainless Steel", restored.MaterialName);
        Assert.Equal("SOLIDWORKS Materials", restored.DatabaseName);
        Assert.Equal("Steel", restored.Category);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void MoDensityParameterModel_RoundtripByteEquality()
    {
        var original = new MoDensityParameterModel(7900.0, 1.93e11, 0.27)
        {
            YieldStrength = 2.9e8,
            TensileStrength = 5.8e8
        };

        byte[] b1 = SerializeModel(original);
        var restored = DeserializeModel<MoDensityParameterModel>(b1);

        Assert.Equal(7900.0, restored.Density, 2);
        Assert.Equal(1.93e11, restored.ElasticModulus, 1e9);
        Assert.Equal(0.27, restored.PoissonRatio, 4);
        Assert.Equal(2.9e8, restored.YieldStrength, 1e6);
        Assert.Equal(5.8e8, restored.TensileStrength, 1e6);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void Config0Model_WithMaterialAndDensity_Roundtrip()
    {
        var config0 = new Config0Model();
        config0.Users.Add("Engineer_Alice");
        config0.Users.Add("Engineer_Bob");
        config0.LogEntries.Add("Created from part template");
        config0.LogEntries.Add("Applied AISI 304 material");

        config0.Material = new MoMaterialModel("AISI 304", "SOLIDWORKS Materials", "Steel");
        config0.DensityParameter = new MoDensityParameterModel(8000.0, 1.9e11, 0.29);
        config0.Parameters["厚度"] = 0.002;
        config0.Parameters["D1@Boss-Extrude1"] = 0.015;

        byte[] b1 = SerializeModel(config0);
        var restored = DeserializeModel<Config0Model>(b1);

        Assert.Contains("Engineer_Alice", restored.Users);
        Assert.Contains("Engineer_Bob", restored.Users);
        Assert.NotNull(restored.Material);
        Assert.Equal("AISI 304", restored.Material!.MaterialName);
        Assert.NotNull(restored.DensityParameter);
        Assert.Equal(8000.0, restored.DensityParameter!.Density, 2);

        byte[] b2 = SerializeModel(restored);
        Assert.Equal(b1, b2);
    }
}
