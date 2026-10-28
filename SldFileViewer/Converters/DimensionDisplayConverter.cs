using System;
using System.Globalization;
using System.Windows.Data;

namespace SldFileViewer.Converters;

public sealed class DimensionDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            // Convert meters to mm
            double mm = d * 1000.0;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} mm", mm);
        }
        if (value is float f)
        {
            double mm = f * 1000.0;
            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} mm", mm);
        }
        return value?.ToString() ?? "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
