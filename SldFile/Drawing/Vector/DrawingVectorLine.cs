using System;

namespace SldFile.Drawing.Vector;

/// <summary>
/// Type and semantic role of a 2D CAD vector line in a drawing sheet.
/// </summary>
public enum DrawingLineKind
{
    /// <summary>Outer border of the drawing sheet (fine line).</summary>
    SheetBorderOuter,

    /// <summary>Inner border and title block frame (thick line, typically 0.5 - 0.7mm).</summary>
    SheetBorderInner,

    /// <summary>Grid lines inside title block and tolerance tables (fine line, 0.25 - 0.35mm).</summary>
    TableGrid,

    /// <summary>Visible model outline geometry in drawing views (thick continuous line).</summary>
    SolidOutline,

    /// <summary>Internal model features, witness lines, hatch lines or leader lines (thin continuous line).</summary>
    ThinLine,

    /// <summary>Centerlines and pitch circles (dash-dot chain line, ISO/GB standard).</summary>
    Centerline,

    /// <summary>Hidden model edges (dashed line).</summary>
    HiddenLine,

    /// <summary>Dimension lines and extension lines.</summary>
    DimensionLine
}

/// <summary>
/// Represents a 2D CAD vector line segment extracted from SolidWorks drawing streams
/// (e.g. VBLists, DisplayLists, Definition) or generated for standard sheet frames.
/// </summary>
public sealed class DrawingVectorLine
{
    /// <summary>Start X in millimeters (sheet CAD coordinate system, origin lower-left).</summary>
    public double X1 { get; }

    /// <summary>Start Y in millimeters (sheet CAD coordinate system, origin lower-left).</summary>
    public double Y1 { get; }

    /// <summary>End X in millimeters (sheet CAD coordinate system, origin lower-left).</summary>
    public double X2 { get; }

    /// <summary>End Y in millimeters (sheet CAD coordinate system, origin lower-left).</summary>
    public double Y2 { get; }

    /// <summary>Type and rendering role of this vector line.</summary>
    public DrawingLineKind Kind { get; }

    /// <summary>Nominal line weight in millimeters (e.g. 0.5mm for bold outline, 0.25mm for thin).</summary>
    public double WeightMm { get; }

    /// <summary>Physical length in millimeters.</summary>
    public double LengthMm => Math.Sqrt(Math.Pow(X2 - X1, 2) + Math.Pow(Y2 - Y1, 2));

    /// <summary>True if segment is horizontal within 1e-3 mm.</summary>
    public bool IsHorizontal => Math.Abs(Y2 - Y1) < 1e-3;

    /// <summary>True if segment is vertical within 1e-3 mm.</summary>
    public bool IsVertical => Math.Abs(X2 - X1) < 1e-3;

    /// <summary>Calculated pixel X1 on reference preview viewport.</summary>
    public double PixelX1 { get; internal set; }

    /// <summary>Calculated pixel Y1 on reference preview viewport (origin top-left).</summary>
    public double PixelY1 { get; internal set; }

    /// <summary>Calculated pixel X2 on reference preview viewport.</summary>
    public double PixelX2 { get; internal set; }

    /// <summary>Calculated pixel Y2 on reference preview viewport (origin top-left).</summary>
    public double PixelY2 { get; internal set; }

    public DrawingVectorLine(
        double x1Mm,
        double y1Mm,
        double x2Mm,
        double y2Mm,
        DrawingLineKind kind = DrawingLineKind.SolidOutline,
        double weightMm = 0.35)
    {
        X1 = x1Mm;
        Y1 = y1Mm;
        X2 = x2Mm;
        Y2 = y2Mm;
        Kind = kind;
        WeightMm = weightMm > 0.05 ? weightMm : 0.35;
    }

    public override string ToString() =>
        $"[{Kind} W={WeightMm:F2}mm] ({X1:F2}, {Y1:F2}) -> ({X2:F2}, {Y2:F2}) (Len={LengthMm:F2}mm)";
}
