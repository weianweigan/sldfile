namespace SldFile.Assembly.Mate;

/// <summary>
/// Kind of persistent entity reference wrapper attached to a serialized assembly mate.
/// Classified only from the observed MFC wrapper class name; the referenced topology
/// itself is not decoded.
/// </summary>
public enum MateReferenceKind
{
    /// <summary>Wrapper class is not one of the layouts established by samples.</summary>
    Unknown = 0,

    /// <summary>Observed <c>moPlaneRef_w</c> wrapper (sample payload child: <c>moCompFace_c</c>).</summary>
    Plane = 1,

    /// <summary>Observed <c>moPointRef_w</c> wrapper (sample payload child: <c>moCompSketchEntHandle_c</c>).</summary>
    Point = 2,

    /// <summary>Observed <c>moCylinderRef_w</c> wrapper / <c>moRefCylinder_c</c> chain (cylindrical face).</summary>
    Cylinder = 3,

    /// <summary>Measured RackPinion rack reference: a linear edge (<c>moCompRefLine_c</c>).</summary>
    Edge = 4,

    /// <summary>Measured RackPinion pinion reference: a circular edge/face (<c>moCylinderRef_w</c>, native Circle).</summary>
    Circle = 5,

    /// <summary>Measured Path mate path reference (<c>moGeneralCurveRef_w</c> / component sketch chain).</summary>
    Path = 6,
}
