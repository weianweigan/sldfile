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
            TreeNodeKind.FeatureCut => "Brush.Accent.Red",
            TreeNodeKind.FeatureRevolve => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureSweep => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureLoft => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureFillet => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureChamfer => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureHole => "Brush.Accent.Blue",
            TreeNodeKind.FeaturePattern => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureMirror => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureRib => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureShell => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureDraft => "Brush.Accent.Yellow",
            TreeNodeKind.FeatureThread => "Brush.Accent.Orange",
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
