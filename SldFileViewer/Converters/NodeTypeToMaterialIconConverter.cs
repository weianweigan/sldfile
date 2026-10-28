using System;
using System.Globalization;
using System.Windows.Data;
using MahApps.Metro.IconPacks;
using SldFileViewer.Models;

namespace SldFileViewer.Converters;

public sealed class NodeTypeToMaterialIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TreeNodeKind kind)
            return PackIconMaterialKind.CubeOutline;

        return kind switch
        {
            TreeNodeKind.DocumentRoot => PackIconMaterialKind.Cube,
            TreeNodeKind.CustomProperties => PackIconMaterialKind.CardBulletedOutline,
            TreeNodeKind.Equations => PackIconMaterialKind.FunctionVariant,
            TreeNodeKind.GeometryPreview => PackIconMaterialKind.CubeScan,
            TreeNodeKind.Bom => PackIconMaterialKind.TableLarge,
            TreeNodeKind.FeaturePlane => PackIconMaterialKind.CheckboxMultipleBlankOutline,
            TreeNodeKind.FeatureOrigin => PackIconMaterialKind.CrosshairsGps,
            TreeNodeKind.FeatureSketch => PackIconMaterialKind.DrawPen,
            TreeNodeKind.FeatureExtrude => PackIconMaterialKind.CubeSend,
            TreeNodeKind.FeatureFolder => PackIconMaterialKind.FolderOutline,
            TreeNodeKind.AssemblyComponent => PackIconMaterialKind.ToyBrickOutline,
            TreeNodeKind.AssemblyMate => PackIconMaterialKind.LinkVariant,
            TreeNodeKind.DrawingSheet => PackIconMaterialKind.FileDocumentOutline,
            _ => PackIconMaterialKind.CogOutline
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
