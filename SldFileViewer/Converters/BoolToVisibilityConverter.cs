using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SldFileViewer.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        bool invert = Invert || (parameter is string paramStr && string.Equals(paramStr, "Invert", StringComparison.OrdinalIgnoreCase));
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = value is Visibility v && v == Visibility.Visible;
        bool invert = Invert || (parameter is string paramStr && string.Equals(paramStr, "Invert", StringComparison.OrdinalIgnoreCase));
        return invert ? !isVisible : isVisible;
    }
}
