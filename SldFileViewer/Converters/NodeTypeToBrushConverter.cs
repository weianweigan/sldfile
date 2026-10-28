using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SldFileViewer.Models;

namespace SldFileViewer.Converters;

public sealed class NodeTypeToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TreeNodeKind kind)
            return Application.Current?.TryFindResource("Brush.Accent.Blue") as Brush ?? Brushes.DodgerBlue;

        string resourceKey = kind switch
        {
            TreeNodeKind.DocumentRoot => "Brush.Accent.Blue",
            TreeNodeKind.CustomProperties => "Brush.Accent.Green",
            TreeNodeKind.Equations => "Brush.Accent.Orange",
            TreeNodeKind.GeometryPreview => "Brush.Accent.Blue",
            TreeNodeKind.Bom => "Brush.Accent.Green",
            TreeNodeKind.FeaturePlane => "Brush.Accent.Blue",
            TreeNodeKind.FeatureOrigin => "Brush.Accent.Red",
            TreeNodeKind.FeatureSketch => "Brush.Accent.Blue",
            TreeNodeKind.FeatureExtrude => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureFolder => "Brush.Accent.Yellow",
            TreeNodeKind.AssemblyComponent => "Brush.Accent.Green",
            TreeNodeKind.AssemblyMate => "Brush.Accent.Orange",
            TreeNodeKind.DrawingSheet => "Brush.Accent.Blue",
            _ => "Brush.Text.Secondary"
        };

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.SlateGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
