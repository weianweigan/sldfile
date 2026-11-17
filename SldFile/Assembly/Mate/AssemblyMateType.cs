namespace SldFile.Assembly.Mate;

/// <summary>
/// Semantically decoded mate type. The controlled 2026 mate samples map the
/// serialized classes to <c>swMateType_e</c>: Coincident (0), Concentric (1),
/// Perpendicular (2), Parallel (3), Tangent (4), Distance (5), Angle (6),
/// Gear (10), Width (11), RackPinion (13), LimitDistance/LimitAngle (5/6 with
/// limit semantics), Screw (17), UniversalJoint (19). The 2012 classes map to
/// Coordinate (20, moMateCoordinateM) and Coincident (0, moMateCoincidentM).
/// Every other serialized class stays <see cref="Unknown"/>.
/// </summary>
public enum AssemblyMateType
{
    Unknown = 0,
    Coincident = 1,
    Concentric = 2,
    Perpendicular = 3,
    Parallel = 4,
    Tangent = 5,
    Distance = 6,
    Angle = 7,
    Coordinate = 8,
    Gear = 9,
    LimitDistance = 10,
    LimitAngle = 11,
    Screw = 12,
    UniversalJoint = 13,
    Width = 14,
    RackPinion = 15,
    Path = 16,
    Hinge = 17,
    Cam = 18,
    PlanarAngle = 19,
    InPlace = 20,
    ProfileCenter = 21,
}
