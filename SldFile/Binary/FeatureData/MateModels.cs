using System;
using System.Collections.Generic;
using SldFile.Binary;

namespace SldFile;

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
}

/// <summary>
/// Width mate constraint type (<c>swMateWidthOptions_e</c>): Centered (0),
/// Free (1), Dimension (2), Percent (3). Decoded for the measured
/// <c>moMateWidth</c> record; the dimensioned form is established by its D1
/// distance parameter.
/// </summary>
public enum AssemblyMateWidthConstraint
{
    Unknown = 0,
    Centered = 1,
    Free = 2,
    Dimension = 3,
    Percent = 4,
}

/// <summary>
/// RackPinion diameter option (<c>swRackPinionMateDistanceOptions_e</c>):
/// pinion pitch diameter (0) or rack travel per pinion revolution (1).
/// </summary>
public enum AssemblyMateRackPinionDiameterType
{
    Unknown = 0,
    PinionPitchDiameter = 1,
    RackTravelPerRevolution = 2,
}

/// <summary>
/// Mate alignment (<c>swMateAlign_e</c>). Decoded only from the controlled 2026
/// mate header: Aligned (0), AntiAligned (1) and Closest (2) all have measured
/// controls (Closest measured on the Perpendicular sample).
/// </summary>
public enum AssemblyMateAlignment
{
    Aligned = 0,
    AntiAligned = 1,
    Closest = 2,
}

/// <summary>
/// One entity reference of a controlled 2026 mate, decoded from the serialized
/// component-instance / feature selection / cylinder reference records.
/// RefPlane entities carry a full selection name; Tangent/Concentric planar or
/// cylindrical face entities carry only the component instance and kind.
/// </summary>
public sealed class AssemblyMateEntityReference
{
    internal AssemblyMateEntityReference(
        MateReferenceKind kind,
        string componentInstanceName,
        string featureName,
        string selectionName,
        uint? persistentId)
    {
        Kind = kind;
        ComponentInstanceName = componentInstanceName ?? string.Empty;
        FeatureName = featureName ?? string.Empty;
        SelectionName = selectionName ?? string.Empty;
        PersistentId = persistentId;
    }

    /// <summary>Reference kind established by the serialized wrapper/chain class names.</summary>
    public MateReferenceKind Kind { get; }

    /// <summary>Component instance selection id, e.g. <c>cube-1@assembly_mate</c>.</summary>
    public string ComponentInstanceName { get; }

    /// <summary>Referenced feature name, e.g. <c>前视基准面</c> (RefPlane); empty for face/cylinder references.</summary>
    public string FeatureName { get; }

    /// <summary>Full serialized selection name; empty when the file stores only a face/cylinder chain.</summary>
    public string SelectionName { get; }

    /// <summary>
    /// Persistent face id when the file carries one: the 2012 Coincident4 face
    /// records expose their class-specific ids (0x192D tagged, 0x263 via the
    /// F2 80 class reference). 2026 RefPlane/Tangent/Concentric entries carry no
    /// per-entity id and stay null. Component identity is empty for 2012 faces.
    /// </summary>
    public uint? PersistentId { get; }
}

/// <summary>
/// Per-field evidence flags for <see cref="AssemblyMateInfo"/>.
/// A false flag means the corresponding value is not decoded from the MatesList
/// stream and must not be treated as a file value.
/// </summary>
public sealed class AssemblyMateResolution
{
    /// <summary>Serialized MFC class name was read from the class tag.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Display name was read from the leading MFC WChar string.</summary>
    public bool DisplayName { get; internal set; }

    /// <summary>Reference wrapper class names were structurally scanned from direct children.</summary>
    public bool ReferenceStructure { get; internal set; }

    /// <summary>
    /// Native mate type (<c>IMate2.Type</c> / <c>swMateType_e</c>). True for the
    /// controlled 2026 RefPlane layouts and the measured moMateWidth (11) /
    /// moMateRackPinionDim_c (13) records; legacy and deferred layouts remain false.
    /// </summary>
    public bool MateType { get; internal set; }

    /// <summary>Mate alignment (Aligned / AntiAligned / Closest) from the controlled 2026 header.</summary>
    public bool MateAlignment { get; internal set; }

    /// <summary>Entity identities behind the wrappers. True when both RefPlane names are decoded.</summary>
    public bool EntityReferences { get; internal set; }

    /// <summary>Distance/Angle parameter. True only for a validated D1 record.</summary>
    public bool MateParameters { get; internal set; }

    /// <summary>Width constraint type and/or distance parameter decoded from a measured moMateWidth record.</summary>
    public bool WidthParameters { get; internal set; }

    /// <summary>RackPinion diameter type/value and/or reverse flag decoded from a measured moMateRackPinionDim_c record.</summary>
    public bool RackPinionParameters { get; internal set; }

    /// <summary>Path D1 value/free flag decoded from a measured moMatePath_c record.</summary>
    public bool PathParameters { get; internal set; }
}

/// <summary>
/// One reference wrapper attached to a serialized assembly mate.
/// </summary>
public sealed class AssemblyMateReference
{
    internal AssemblyMateReference(string wrapperClassName, IReadOnlyList<string> entityClassNames)
    {
        WrapperClassName = wrapperClassName ?? string.Empty;
        EntityClassNames = entityClassNames ?? Array.Empty<string>();
        Kind = ClassifyWrapper(WrapperClassName);
    }

    /// <summary>Wrapper MFC class name as serialized (e.g., <c>moPlaneRef_w</c>).</summary>
    public string WrapperClassName { get; }

    /// <summary>Reference kind classified from <see cref="WrapperClassName"/>.</summary>
    public MateReferenceKind Kind { get; }

    /// <summary>
    /// Direct MFC child class names of the wrapper (e.g., <c>moCompFace_c</c>).
    /// Raw structural information only; payloads are not decoded.
    /// </summary>
    public IReadOnlyList<string> EntityClassNames { get; }

    internal static MateReferenceKind ClassifyWrapper(string wrapperClassName)
    {
        return wrapperClassName switch
        {
            "moPlaneRef_w" => MateReferenceKind.Plane,
            "moPointRef_w" => MateReferenceKind.Point,
            _ => MateReferenceKind.Unknown,
        };
    }
}

/// <summary>
/// Structural information for one assembly mate serialized in
/// <c>Contents/Config-0-MatesList</c>.
/// <para>
/// Evidence established offline (SolidWorks 2012, 2026 and the controlled 2026
/// mate samples): class labels, display names and reference wrappers. For the
/// controlled 188-byte 2026 mate record, the mate type (Coincident, Concentric,
/// Perpendicular, Parallel, Tangent, Distance, Angle), alignment
/// (Aligned/AntiAligned/Closest) and the Distance/Angle D1 parameter are decoded.
/// Coincident/Distance/Perpendicular/Parallel/Angle also expose their two RefPlane
/// entities; Tangent/Concentric face/cylinder topology remains unresolved.
/// The measured moMateWidth record exposes DimensionConstraint/Distance/Flip and
/// four planar faces; moMateRackPinionDim_c exposes diameter option/value, reverse
/// and the rack edge + pinion circle.
/// Legacy/deferred layouts leave all of these unresolved; see <see cref="Resolution"/>.
/// </para>
/// <para>
/// Class names are serialized implementation classes, not <c>swMateType_e</c> values.
/// In the older files a <c>moMateCoordinate*</c> element is displayed as
/// "Coincident3", so only the explicitly controlled class mapping is exposed.
/// </para>
/// </summary>
public sealed class AssemblyMateInfo
{
    internal AssemblyMateInfo(MateEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        SerializedClassName = entry.ClassName;
        DisplayName = entry.DisplayName;

        var references = new List<AssemblyMateReference>(entry.References.Count);
        foreach (var reference in entry.References)
        {
            references.Add(new AssemblyMateReference(reference.WrapperClassName, reference.EntityClassNames));
        }
        References = references.AsReadOnly();

        var structure = entry.Structure;
        uint? mateTypeId = structure?.MateTypeId;
        MateTypeId = mateTypeId;
        // Limit mates share native type ids 5/6 with Distance/Angle; the serialized
        // class name carries the limit semantics.
        MateType = structure?.ClassName switch
        {
            "moMateLimitDistanceDim_c" => AssemblyMateType.LimitDistance,
            "moMateLimitPlanarAngleDim_c" => AssemblyMateType.LimitAngle,
            "moMateGearDim_c" => AssemblyMateType.Gear,
            "moMateScrew_c" => AssemblyMateType.Screw,
            "moMateUniversalJoint_c" => AssemblyMateType.UniversalJoint,
            "moMateWidth" => AssemblyMateType.Width,
            "moMateRackPinionDim_c" => AssemblyMateType.RackPinion,
            "moMatePath_c" => AssemblyMateType.Path,
            "moMateHinge" => AssemblyMateType.Hinge,
            "moMateCamTangent" => AssemblyMateType.Cam,
            "moMateCamCoincident" => AssemblyMateType.Cam,
            "moMatePlanarAngleDim_c" when structure?.Layout == MateStructureLayout.MeasuredMechanical2026
                => AssemblyMateType.PlanarAngle,
            "moMatePlanarAngleDim_c" => AssemblyMateType.Angle,
            "moMateInPlace" => AssemblyMateType.InPlace,
            _ => mateTypeId switch
            {
                0u => AssemblyMateType.Coincident,
                1u => AssemblyMateType.Concentric,
                2u => AssemblyMateType.Perpendicular,
                3u => AssemblyMateType.Parallel,
                4u => AssemblyMateType.Tangent,
                5u => AssemblyMateType.Distance,
                6u => AssemblyMateType.Angle,
                9u => AssemblyMateType.Cam,
                10u => AssemblyMateType.Gear,
                11u => AssemblyMateType.Width,
                13u => AssemblyMateType.RackPinion,
                15u => AssemblyMateType.Path,
                17u => AssemblyMateType.Screw,
                19u => AssemblyMateType.UniversalJoint,
                20u => AssemblyMateType.Coordinate,
                22u => AssemblyMateType.Hinge,
                _ => AssemblyMateType.Unknown,
            }
        };
        Alignment = structure?.AlignmentId is uint alignment && alignment <= 2
            ? (AssemblyMateAlignment?)(AssemblyMateAlignment)(int)alignment
            : null;
        Distance = structure?.Distance;
        Angle = structure?.Angle;
        GearRatioNumerator = structure?.GearRatioNumerator;
        GearRatioDenominator = structure?.GearRatioDenominator;
        ScrewRevolutionsPerUnit = structure?.ScrewRevolutionsPerUnit;
        WidthConstraintType = structure?.WidthConstraintTypeId switch
        {
            0u => AssemblyMateWidthConstraint.Centered,
            1u => AssemblyMateWidthConstraint.Free,
            2u => AssemblyMateWidthConstraint.Dimension,
            3u => AssemblyMateWidthConstraint.Percent,
            _ => null
        };
        WidthDistanceFromEnd = structure?.WidthDistanceFromEnd;
        WidthFlipDimension = structure?.WidthFlipDimension;
        RackPinionDiameterType = structure?.RackPinionDiameterTypeId switch
        {
            0u => AssemblyMateRackPinionDiameterType.PinionPitchDiameter,
            1u => AssemblyMateRackPinionDiameterType.RackTravelPerRevolution,
            _ => null
        };
        RackPinionDiameterValue = structure?.RackPinionDiameterValue;
        RackPinionReverse = structure?.RackPinionReverse;
        PathValue = structure?.PathValue;
        PathIsFree = structure?.PathIsFree;

        var entities = new List<AssemblyMateEntityReference>();
        if (structure != null)
        {
            foreach (var entity in structure.EntityReferences)
            {
                MateReferenceKind kind = entity.Kind switch
                {
                    MateReferenceEntityKind.Cylinder => MateReferenceKind.Cylinder,
                    MateReferenceEntityKind.Plane => MateReferenceKind.Plane,
                    MateReferenceEntityKind.Edge => MateReferenceKind.Edge,
                    MateReferenceEntityKind.Circle => MateReferenceKind.Circle,
                    MateReferenceEntityKind.Path => MateReferenceKind.Path,
                    MateReferenceEntityKind.SketchEntity => MateReferenceKind.Point,
                    _ => MateReferenceKind.Unknown
                };
                entities.Add(new AssemblyMateEntityReference(
                    kind,
                    entity.ComponentInstanceName,
                    entity.FeatureName,
                    entity.SelectionName,
                    entity.PersistentId));
            }
        }
        Entities = entities.AsReadOnly();

        Resolution = new AssemblyMateResolution
        {
            ClassName = !string.IsNullOrEmpty(entry.ClassName),
            DisplayName = !string.IsNullOrEmpty(entry.DisplayName),
            ReferenceStructure = References.Count > 0,
            MateType = MateTypeId.HasValue,
            MateAlignment = Alignment.HasValue,
            EntityReferences = Entities.Count > 0,
            MateParameters = Distance.HasValue || Angle.HasValue ||
                             GearRatioNumerator.HasValue || GearRatioDenominator.HasValue ||
                             ScrewRevolutionsPerUnit.HasValue ||
                             WidthConstraintType.HasValue || WidthDistanceFromEnd.HasValue ||
                             RackPinionDiameterType.HasValue || RackPinionDiameterValue.HasValue ||
                             RackPinionReverse.HasValue || PathValue.HasValue,
            WidthParameters = WidthConstraintType.HasValue || WidthDistanceFromEnd.HasValue,
            RackPinionParameters = RackPinionDiameterType.HasValue || RackPinionDiameterValue.HasValue ||
                                   RackPinionReverse.HasValue,
            PathParameters = PathValue.HasValue || PathIsFree.HasValue,
        };
    }

    /// <summary>MFC class name serialized in the mate tag (e.g., <c>moMateCoincidentM</c>).</summary>
    public string SerializedClassName { get; }

    /// <summary>FeatureManager display name (e.g., "Coincident4").</summary>
    public string DisplayName { get; }

    /// <summary>
    /// Decoded mate type for the controlled 2026 record and the 2012
    /// moMateCoordinateM/moMateCoincidentM classes; everything else stays
    /// <see cref="AssemblyMateType.Unknown"/>.
    /// </summary>
    public AssemblyMateType MateType { get; }

    /// <summary>
    /// Raw native <c>swMateType_e</c> value: 0 Coincident, 1 Concentric,
    /// 2 Perpendicular, 3 Parallel, 4 Tangent, 5 Distance/LimitDistance,
    /// 6 Angle/LimitAngle, 10 Gear, 11 Width, 13 RackPinion, 17 Screw,
    /// 19 UniversalJoint, 20 Coordinate; <c>null</c> when unresolved.
    /// </summary>
    public uint? MateTypeId { get; }

    /// <summary>Decoded mate alignment; <c>null</c> when the controlled header is not recognized.</summary>
    public AssemblyMateAlignment? Alignment { get; }

    /// <summary>Distance in meters for a validated Distance mate; otherwise <c>null</c>.</summary>
    public double? Distance { get; }

    /// <summary>Angle in radians for a validated Angle/LimitAngle mate; otherwise <c>null</c>.</summary>
    public double? Angle { get; }

    /// <summary>Gear ratio numerator (D1 of moMateGearDim_c); otherwise <c>null</c>.</summary>
    public double? GearRatioNumerator { get; }

    /// <summary>Gear ratio denominator (D2 of moMateGearDim_c); otherwise <c>null</c>.</summary>
    public double? GearRatioDenominator { get; }

    /// <summary>Screw revolution value (D1 of moMateScrew_c); otherwise <c>null</c>.</summary>
    public double? ScrewRevolutionsPerUnit { get; }

    /// <summary>
    /// Width mate constraint type from the measured <c>moMateWidth</c> record.
    /// The dimensioned form is established when the D1 distance parameter is
    /// present; Centered/Free/Percent remain null unless separately evidenced.
    /// </summary>
    public AssemblyMateWidthConstraint? WidthConstraintType { get; }

    /// <summary>Width distance-from-end (D1 of moMateWidth's moLengthParameter_c, meters); otherwise <c>null</c>.</summary>
    public double? WidthDistanceFromEnd { get; }

    /// <summary>
    /// Width FlipDimension flag from the measured dimension metadata sign byte
    /// (0xBF=false, 0x3F=true); null when the width mate is not dimensioned.
    /// </summary>
    public bool? WidthFlipDimension { get; }

    /// <summary>RackPinion diameter option from the measured <c>moMateRackPinionDim_c</c> record.</summary>
    public AssemblyMateRackPinionDiameterType? RackPinionDiameterType { get; }

    /// <summary>
    /// RackPinion diameter value in meters, derived from the D1 dimension:
    /// pitch diameter = D1; rack travel per revolution = D1 * pi.
    /// </summary>
    public double? RackPinionDiameterValue { get; }

    /// <summary>RackPinion reverse flag from the measured record; otherwise <c>null</c>.</summary>
    public bool? RackPinionReverse { get; }

    /// <summary>
    /// Path mate D1 value from the measured tutorial record. The measured
    /// tutorial path mates serialize 0 (free along the path); positive values
    /// would represent the distance/percentage state but have no measured control.
    /// </summary>
    public double? PathValue { get; }

    /// <summary>True when the measured Path D1 value is zero (free along path).</summary>
    public bool? PathIsFree { get; }

    /// <summary>
    /// Entity references in serialization/native order: two RefPlane selections for
    /// the controlled 2026 plane layouts; component+kind face references for
    /// Tangent/Concentric; four planar faces (two width, two tab) for the measured
    /// Width record; one rack edge and one pinion circle for RackPinion; and the
    /// 2012 Coincident4 face-reference ids. Empty when the layout is unresolved.
    /// </summary>
    public IReadOnlyList<AssemblyMateEntityReference> Entities { get; }

    /// <summary>Reference wrappers found as direct children of the mate object.</summary>
    public IReadOnlyList<AssemblyMateReference> References { get; }

    /// <summary>Per-field evidence flags. Fields that are not decoded remain false.</summary>
    public AssemblyMateResolution Resolution { get; }
}
