using System;

namespace SldFile.Drawing.Vector;

/// <summary>
/// Represents a 2D CAD dimension arrowhead or datum indicator arrow extracted or mapped from drawing geometry.
/// </summary>
public sealed class DrawingVectorArrow
{
    /// <summary>Arrowhead tip X position in millimeters (CAD coordinate, origin at sheet lower-left).</summary>
    public double TipX { get; set; }

    /// <summary>Arrowhead tip Y position in millimeters (CAD coordinate, origin at sheet lower-left).</summary>
    public double TipY { get; set; }

    /// <summary>Normalized pointing direction vector X component.</summary>
    public double DirX { get; set; }

    /// <summary>Normalized pointing direction vector Y component.</summary>
    public double DirY { get; set; }

    /// <summary>Arrowhead length in millimeters along direction vector (standard ~3.5mm).</summary>
    public double LengthMm { get; set; } = 3.5;

    /// <summary>Arrowhead half-width in millimeters perpendicular to direction vector (standard ~0.4 - 0.5mm).</summary>
    public double HalfWidthMm { get; set; } = 0.45;

    public DrawingVectorArrow(double tipX, double tipY, double dirX, double dirY, double lengthMm = 3.5, double halfWidthMm = 0.45)
    {
        TipX = tipX;
        TipY = tipY;
        DirX = dirX;
        DirY = dirY;
        LengthMm = lengthMm;
        HalfWidthMm = halfWidthMm;
    }
}
