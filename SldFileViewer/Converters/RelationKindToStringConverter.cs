using System;
using System.Globalization;
using System.Windows.Data;
using SldFile.Sketches;

namespace SldFileViewer.Converters;

public sealed class RelationKindToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SketchRelationKind kind)
        {
            return kind switch
            {
                SketchRelationKind.Horizontal => "水平 (Horizontal)",
                SketchRelationKind.Vertical => "垂直 (Vertical)",
                SketchRelationKind.Coincident => "重合 (Coincident)",
                SketchRelationKind.Concentric => "同心 (Concentric)",
                SketchRelationKind.Tangent => "相切 (Tangent)",
                SketchRelationKind.Parallel => "平行 (Parallel)",
                SketchRelationKind.Perpendicular => "垂直正交 (Perpendicular)",
                SketchRelationKind.Distance => "距离尺寸 (Distance)",
                SketchRelationKind.Angle => "角度尺寸 (Angle)",
                SketchRelationKind.Radius => "半径 (Radius)",
                SketchRelationKind.Diameter => "直径 (Diameter)",
                SketchRelationKind.EqualLength => "等长 (Equal Length)",
                SketchRelationKind.Symmetric => "对称 (Symmetric)",
                SketchRelationKind.Midpoint => "中点 (Midpoint)",
                SketchRelationKind.Intersection => "交点 (Intersection)",
                SketchRelationKind.Fixed => "固定 (Fixed)",
                SketchRelationKind.SketchOffset => "等距实体 (Offset)",
                SketchRelationKind.Patterned => "阵列约束 (Patterned)",
                _ => $"约束 #{value}"
            };
        }
        if (value is int code)
        {
            return Enum.IsDefined(typeof(SketchRelationKind), code)
                ? Convert((SketchRelationKind)code, targetType, parameter, culture)
                : $"约束代码 ({code})";
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
