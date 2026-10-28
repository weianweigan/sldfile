namespace SldFile.Sketches;

/// <summary>Observed relation codes. Other numeric codes are retained without coercion.</summary>
public enum SketchRelationKind
{
    Distance = 1, Angle = 2, Radius = 3, Horizontal = 4, Vertical = 5, Tangent = 6,
    Parallel = 7, Perpendicular = 8, Coincident = 9, Concentric = 10, Symmetric = 11,
    Midpoint = 12, Intersection = 13, EqualLength = 14, Diameter = 15, Fixed = 17,
    SketchOffset = 47, Patterned = 57
}
