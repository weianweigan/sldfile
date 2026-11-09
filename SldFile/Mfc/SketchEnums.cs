namespace SldFile.Mfc;

/// <summary>
/// Kind of sketch segment.
/// </summary>
public enum SketchSegmentKind
{
    Unknown = -1,
    Line = 0,
    Arc = 1,
    Ellipse = 2,
    Spline = 3,
    Text = 4,
    Circle = 1,
    Parabola = 5
}

/// <summary>
/// Geometric relation kind between sketch entities.
/// </summary>
public enum SketchRelationKind
{
    Distance = 1,
    Angle = 2,
    Radius = 3,
    Horizontal = 4,
    Vertical = 5,
    Tangent = 6,
    Parallel = 7,
    Perpendicular = 8,
    Coincident = 9,
    Concentric = 10,
    Symmetric = 11,
    Midpoint = 12,
    Intersection = 13,
    EqualLength = 14,
    Equal = 14,
    Diameter = 15,
    Fixed = 17,
    Fix = 17,
    Pierce = 18,
    Merge = 19,
    Coradial = 20,
    Collinear = 21,
    SketchOffset = 47,
    Patterned = 57,
    Unknown = 99
}

/// <summary>
/// Dimension types aligned with SolidWorks swDimensionType_e and MFC moDisplayDim classes.
/// </summary>
public enum SketchDimensionType
{
    Unknown = 0,
    Linear = 1,
    Distance = 1,
    Radial = 2,
    Radius = 2,
    Angle = 3,
    Diameter = 4,
    Ordinate = 5,
    ArcLength = 6,
    Chamfer = 7,
    Parallel = 8
}
