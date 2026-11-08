using System;
using System.Collections.Generic;

namespace SldFile.Drawing.Vector;

/// <summary>
/// Encapsulates the 2D CAD vector geometry, bounding box, paper position, and scale matrix
/// for an individual drawing view (e.g. Front View, Top View, Section View, Detail View) on a drawing sheet.
/// </summary>
public sealed class DrawingViewGeometry
{
    /// <summary>Name of the drawing view (e.g. "工程图视图1", "局部视图 A").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Center X coordinate of the view on the drawing sheet in millimeters (origin lower-left).</summary>
    public double CenterXMm { get; set; }

    /// <summary>Center Y coordinate of the view on the drawing sheet in millimeters (origin lower-left).</summary>
    public double CenterYMm { get; set; }

    /// <summary>Width of the view bounding box on the sheet in millimeters.</summary>
    public double WidthMm { get; set; }

    /// <summary>Height of the view bounding box on the sheet in millimeters.</summary>
    public double HeightMm { get; set; }

    /// <summary>View scale numerator (e.g. 1.0 for 1:50).</summary>
    public double ScaleNumerator { get; set; } = 1.0;

    /// <summary>View scale denominator (e.g. 50.0 for 1:50, 2.0 for 1:2).</summary>
    public double ScaleDenominator { get; set; } = 1.0;

    /// <summary>Calculated effective scale factor (Numerator / Denominator).</summary>
    public double Scale => (ScaleDenominator > 1e-6) ? (ScaleNumerator / ScaleDenominator) : 1.0;

    /// <summary>Left X boundary of the view on the sheet (mm).</summary>
    public double LeftXMm => CenterXMm - WidthMm / 2.0;

    /// <summary>Right X boundary of the view on the sheet (mm).</summary>
    public double RightXMm => CenterXMm + WidthMm / 2.0;

    /// <summary>Bottom Y boundary of the view on the sheet (mm).</summary>
    public double BottomYMm => CenterYMm - HeightMm / 2.0;

    /// <summary>Top Y boundary of the view on the sheet (mm).</summary>
    public double TopYMm => CenterYMm + HeightMm / 2.0;

    /// <summary>
    /// All 2D CAD vector line segments belonging to this view, mapped to physical sheet coordinates (mm).
    /// </summary>
    public IReadOnlyList<DrawingVectorLine> Lines { get; set; } = Array.Empty<DrawingVectorLine>();

    public DrawingViewGeometry()
    {
    }

    public DrawingViewGeometry(
        string name,
        double centerXMm,
        double centerYMm,
        double widthMm,
        double heightMm,
        double scaleNumerator = 1.0,
        double scaleDenominator = 1.0)
    {
        Name = name;
        CenterXMm = centerXMm;
        CenterYMm = centerYMm;
        WidthMm = widthMm;
        HeightMm = heightMm;
        ScaleNumerator = scaleNumerator > 0 ? scaleNumerator : 1.0;
        ScaleDenominator = scaleDenominator > 0 ? scaleDenominator : 1.0;
    }

    public override string ToString() =>
        $"View '{Name}': Center=({CenterXMm:F1}, {CenterYMm:F1})mm, Size={WidthMm:F1}x{HeightMm:F1}mm, Scale={ScaleNumerator}:{ScaleDenominator}, Lines={Lines?.Count ?? 0}";
}
