using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class FeatureFolderMfcSerializationTests
{
    private static byte[] SerializeFolder(FeatureFolder folder)
    {
        using var writer = MfcArchive.CreateWriter();
        folder.Serialize(writer);
        return writer.ToArray();
    }

    private static FeatureFolder DeserializeFolder(byte[] data)
    {
        using var reader = MfcArchive.CreateReader(data);
        var folder = new FeatureFolder();
        folder.Serialize(reader);
        return folder;
    }

    [Fact]
    public void FeatureFolder_IMfcSerializable_RoundtripsSuccessfully()
    {
        var original = new FeatureFolder
        {
            ClassName = "moExtrusion_c",
            DisplayName = "Boss-Extrude1",
            Header = new FolderHeader
            {
                TypeId = 0x4F,
                Timestamp = 1234567.89,
                MarkerE = 0x65,
                SizeIndicator = 100,
                Magic = FolderHeader.PartMagic
            },
            FeatureData = new ExtrudeFeatureModel
            {
                FeatureName = "Boss-Extrude1",
                Depth = 0.05,
                BothDirections = false
            }
        };

        var child = new FeatureFolder
        {
            ClassName = "moProfileFeature_c",
            DisplayName = "Sketch1",
            Header = new FolderHeader
            {
                TypeId = 0x20,
                Timestamp = 1234560.00,
                Magic = FolderHeader.PartMagic
            }
        };
        original.ChildrenBuilder.Add(child);
        original.Freeze();

        byte[] bytes = SerializeFolder(original);
        Assert.NotEmpty(bytes);

        var restored = DeserializeFolder(bytes);
        Assert.Equal(original.DisplayName, restored.DisplayName);
        Assert.Equal(original.Header.TypeId, restored.Header.TypeId);
        Assert.Equal(original.Header.Timestamp, restored.Header.Timestamp);
        Assert.Equal(original.Header.Magic, restored.Header.Magic);
        Assert.Single(restored.Children);
        Assert.Equal("Sketch1", restored.Children[0].DisplayName);
        Assert.Equal(0x20u, restored.Children[0].Header.TypeId);
    }

    [Fact]
    public void Feature_Bidirectional_Conversion_With_FeatureFolder()
    {
        var folder = new FeatureFolder
        {
            ClassName = "moExtrusion_c",
            DisplayName = "凸台-拉伸1",
            Header = new FolderHeader
            {
                TypeId = 0x4F,
                Timestamp = 999.0
            },
            FeatureData = new ExtrudeFeatureModel
            {
                FeatureName = "凸台-拉伸1",
                Depth = 0.03
            }
        };

        // Internal MFC FeatureFolder -> Public Domain Feature
        Feature feature = folder.ToFeature();
        Assert.NotNull(feature);
        Assert.Equal("凸台-拉伸1", feature.Name);
        Assert.Equal(FeatureKind.Extrusion, feature.Kind);
        Assert.Equal(0x4F, feature.Id);
        Assert.NotNull(feature.Model);
        Assert.Same(feature.Model, feature.FeatureData);

        var extData = Assert.IsAssignableFrom<ExtrudeFeatureModel>(feature.FeatureData);
        Assert.Equal(0.03, extData.Depth);

        // Public Domain Feature -> Internal MFC FeatureFolder
        FeatureFolder convertedFolder = feature.ToFolder();
        Assert.NotNull(convertedFolder);
        Assert.Equal(feature.Name, convertedFolder.DisplayName);
        Assert.Equal(feature.TypeName, convertedFolder.ClassName);
        Assert.Equal((uint)feature.Id!.Value, convertedFolder.Header.TypeId);
        Assert.Same(feature.FeatureData, convertedFolder.FeatureData);
    }

    [Fact]
    public void Feature_Model_IsTypedAsIFeatureData()
    {
        IFeatureData extModel = new ExtrudeFeatureModel
        {
            FeatureName = "Extrude1",
            Depth = 0.025
        };

        var feat = new Feature(
            name: "Extrude1",
            kind: FeatureKind.Extrusion,
            typeName: "moExtrusion_c",
            id: 0x4F,
            model: extModel
        );

        Assert.NotNull(feat.Model);
        Assert.Same(extModel, feat.Model);
        Assert.Same(extModel, feat.FeatureData);
        Assert.Equal("moExtrusion_c", feat.Model.ClassName);
    }
}
