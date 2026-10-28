using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SldFileViewer.Models;

namespace SldFileViewer.Converters;

public sealed class NodeTypeToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TreeNodeKind kind) return null;

        string key = kind switch
        {
            TreeNodeKind.DocumentRoot => "Geo.Part",
            TreeNodeKind.CustomProperties => "Geo.CustomProperties",
            TreeNodeKind.Equations => "Geo.Equations",
            TreeNodeKind.GeometryPreview => "Geo.Preview3D",
            TreeNodeKind.Bom => "Geo.BOM",
            TreeNodeKind.FeaturePlane => "Geo.Plane",
            TreeNodeKind.FeatureOrigin => "Geo.Origin",
            TreeNodeKind.FeatureSketch => "Geo.Sketch",
            TreeNodeKind.FeatureExtrude => "Geo.Extrude",
            TreeNodeKind.FeatureFolder => "Geo.Folder",
            TreeNodeKind.AssemblyComponent => "Geo.Component",
            TreeNodeKind.AssemblyMate => "Geo.Mate",
            TreeNodeKind.DrawingSheet => "Geo.Sheet",
            _ => "Geo.Part"
        };

        return Application.Current.TryFindResource(key) as Geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
