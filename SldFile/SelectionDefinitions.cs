using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>Observed sketch selection strategy; does not imply resolved persistent contour handles.</summary>
public enum SketchSelectionMode
{
    /// <summary>The observed stream header, feature class or sketch association is not established.</summary>
    Unknown = 0,
    /// <summary>The supported feature uses its whole sketch without an explicit contour chooser.</summary>
    WholeSketch = 1,
    /// <summary>An explicit chooser selects contours or regions; individual handles may remain unresolved.</summary>
    ExplicitSelection = 2
}

/// <summary>
/// Entity type classification for SolidWorks persistent topology and selection references.
/// </summary>
public enum SelectionEntityType
{
    /// <summary>Unspecified or unknown entity type.</summary>
    None = 0,

    /// <summary>Topological face or surface reference.</summary>
    Face = 1,

    /// <summary>Topological edge reference.</summary>
    Edge = 2,

    /// <summary>Topological vertex reference.</summary>
    Vertex = 3,

    /// <summary>Reference plane or planar datum.</summary>
    Plane = 4,

    /// <summary>Entire sketch reference.</summary>
    Sketch = 5,

    /// <summary>Closed contour loop within a sketch.</summary>
    SketchContour = 6,

    /// <summary>Planar closed region within a sketch.</summary>
    SketchRegion = 7,

    /// <summary>Solid or surface body reference in multi-body parts.</summary>
    Body = 8
}

/// <summary>
/// Common contract for all SolidWorks selection and persistent topology references.
/// </summary>
public interface ISelectionReference
{
    /// <summary>Category or kind of reference (e.g. "Contour", "Region", "Direction", "EndConditionEntity", "Entity").</summary>
    string ReferenceType { get; }

    /// <summary>Persistent or topological identifier within the feature stream.</summary>
    uint EntityId { get; }

    /// <summary>Whether this selection reference holds a valid target.</summary>
    bool IsValid { get; }
}

/// <summary>
/// Represents a selected closed sketch contour loop (e.g. from moSketchChain_c and moFromSktEntSurfIdRep_c).
/// </summary>
public sealed class SketchContourReference : ISelectionReference
{
    /// <inheritdoc />
    public string ReferenceType => "Contour";

    /// <summary>Topological ID or index of this contour loop.</summary>
    public uint EntityId { get; set; }

    /// <summary>Sketch chain ID or feature index.</summary>
    public uint ChainId { get; set; }

    /// <summary>Indices of sketch entities (lines, arcs, splines) forming this closed loop.</summary>
    public IReadOnlyList<uint> EntityIndices { get; set; } = Array.Empty<uint>();

    /// <summary>Whether this contour forms a closed loop.</summary>
    public bool IsClosed { get; set; } = true;

    /// <summary>
    /// True when <see cref="EntityIndices"/> was produced by a byte-pattern scan
    /// (candidate data) instead of a structurally decoded chain record. Candidate
    /// contours are never written to a feature's selected-contour collection.
    /// </summary>
    public bool IsCandidateOnly { get; set; }

    /// <inheritdoc />
    public bool IsValid => EntityIndices.Count > 0;

    public override string ToString() =>
        $"Contour #{EntityId} (Chain {ChainId}, Entities: [{string.Join(", ", EntityIndices)}], Closed={IsClosed}" +
        (IsCandidateOnly ? ", Candidate=True)" : ")");
}

/// <summary>
/// Represents a selected planar sketch region (e.g. from moSketchRegion_c).
/// </summary>
public sealed class SketchRegionReference : ISelectionReference
{
    /// <inheritdoc />
    public string ReferenceType => "Region";

    /// <summary>Topological ID or handle of this region.</summary>
    public uint EntityId { get; set; }

    /// <summary>Index of the region within the sketch.</summary>
    public uint RegionIndex { get; set; }

    /// <summary>Number of loops bounding this region.</summary>
    public uint LoopCount { get; set; }

    /// <summary>
    /// Sketch chain entity identifiers forming the selected region boundary.
    /// Decoded from the region chooser (<c>moContourChooser_c</c>) unit records
    /// and reported in ascending order (the chooser's intra-group ordering is not
    /// established). The persistent region handle/index itself is not stored
    /// there and remains 0.
    /// </summary>
    public IReadOnlyList<uint> EdgeIds { get; set; } = Array.Empty<uint>();

    /// <inheritdoc />
    public bool IsValid => RegionIndex > 0 || EntityId > 0 || EdgeIds.Count > 0;

    public override string ToString() =>
        $"Region #{RegionIndex} (EntityId={EntityId}, Loops={LoopCount}" +
        (EdgeIds.Count > 0 ? $", Edges=[{string.Join(", ", EdgeIds)}])" : ")");
}

/// <summary>
/// Role of a sketch reference inside a sweep or loft feature. Established by the
/// selection wrapper containers: <c>moCompProfile_c</c> holds profiles (or the
/// path of a circular-profile sweep), <c>moWireContact_c</c> holds the sweep path
/// followed by guides, or the loft guides.
/// </summary>
public enum SketchCurveRole
{
    /// <summary>Role could not be established.</summary>
    Unknown = 0,

    /// <summary>Profile/section sketch (loft sections, sweep profile).</summary>
    Profile = 1,

    /// <summary>Sweep path.</summary>
    Path = 2,

    /// <summary>Guide curve.</summary>
    Guide = 3,

    /// <summary>Centerline (not present in the 2026 controlled samples).</summary>
    Centerline = 4
}

/// <summary>
/// Persistent sketch reference decoded from a sweep/loft selection wrapper
/// (<c>moCompProfile_c</c> / <c>moWireContact_c</c>). The 52-byte record starts
/// with <c>38 4A 00 00</c> (19000) followed by the sketch persistent id.
/// </summary>
public sealed class SketchCurveReference : ISelectionReference
{
    /// <inheritdoc />
    public string ReferenceType => "SketchCurve";

    /// <summary>Role of the sketch in the feature.</summary>
    public SketchCurveRole Role { get; set; }

    /// <summary>Sketch persistent id stored at record offset +4.</summary>
    public uint EntityId { get; set; }

    /// <summary>Archive serial stored at record offset +8 (per-feature creation serial).</summary>
    public uint ArchiveSerial { get; set; }

    /// <summary>
    /// Opaque selection id from the folder-header list, assigned by list position
    /// (wrapper order) when <see cref="IsHeaderSelectionIdPositional"/> is true.
    /// </summary>
    public ushort HeaderSelectionId { get; set; }

    /// <summary>
    /// True when <see cref="HeaderSelectionId"/> was correlated by position only;
    /// no direct byte-level link between the header ids and the sketch ids exists
    /// in the observed samples.
    /// </summary>
    public bool IsHeaderSelectionIdPositional { get; set; }

    /// <inheritdoc />
    public bool IsValid => EntityId > 0;

    public override string ToString() =>
        $"SketchCurve ({Role}, SketchId={EntityId}, Serial=0x{ArchiveSerial:X8}, HeaderId=0x{HeaderSelectionId:X4}" +
        (IsHeaderSelectionIdPositional ? ", Positional=True)" : ")");
}

/// <summary>
/// Direction vector or reference entity for extrusion, sweep, pattern, or revolution (e.g. moEdgeRef_c, moCompRefPlane_c, moFaceRefPlnData_c).
/// </summary>
public sealed class DirectionReference : ISelectionReference
{
    /// <inheritdoc />
    public string ReferenceType => "Direction";

    /// <summary>Target entity topological ID.</summary>
    public uint EntityId { get; set; }

    /// <summary>Entity type (Edge, Plane, Face, etc.).</summary>
    public SelectionEntityType EntityType { get; set; } = SelectionEntityType.Edge;

    /// <summary>Name or identifier of the referenced plane or axis, if named.</summary>
    public string ReferenceName { get; set; } = string.Empty;

    /// <summary>Explicit 3D direction vector [X, Y, Z] if resolved from reference geometry.</summary>
    public double[]? Vector { get; set; }

    /// <inheritdoc />
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(ReferenceName) || HasDirectionVector;

    private bool HasDirectionVector => Vector != null && Vector.Length == 3 &&
        !double.IsNaN(Vector[0]) && !double.IsInfinity(Vector[0]) &&
        !double.IsNaN(Vector[1]) && !double.IsInfinity(Vector[1]) &&
        !double.IsNaN(Vector[2]) && !double.IsInfinity(Vector[2]) &&
        (Vector[0] != 0 || Vector[1] != 0 || Vector[2] != 0);

    public override string ToString() =>
        $"Direction ({EntityType}, EntityId={EntityId}, Name='{ReferenceName}'" +
        (HasDirectionVector ? $", Vec=[{Vector![0]:F3}, {Vector[1]:F3}, {Vector[2]:F3}])" : ")");
}

/// <summary>
/// Generic entity reference used across SolidWorks feature selections (e.g. moCompEdge_c, moCompFace_c, moCompSolidBody_c).
/// </summary>
public sealed class EntityReference : ISelectionReference
{
    /// <inheritdoc />
    public string ReferenceType => "Entity";

    /// <summary>Persistent entity handle or index.</summary>
    public uint EntityId { get; set; }

    /// <summary>Entity type (Edge, Face, Body, Vertex, Plane).</summary>
    public SelectionEntityType EntityType { get; set; }

    /// <summary>Optional display or internal name of the referenced entity.</summary>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(Name);

    public override string ToString() =>
        $"EntityReference ({EntityType}, Id={EntityId}, Name='{Name}')";
}

/// <summary>
/// End condition terminating target entity (e.g. Up to Surface, Up to Vertex, Up to Body from moFaceRef_c, moEndFaceSurfIdRep_c, moCompFace_c).
/// </summary>
public sealed class EndConditionEntityReference : ISelectionReference
{
    /// <inheritdoc />
    public string ReferenceType => "EndConditionEntity";

    /// <summary>Target entity topological ID.</summary>
    public uint EntityId { get; set; }

    /// <summary>Type of the terminating entity (Face, Vertex, Body, Plane).</summary>
    public SelectionEntityType EntityType { get; set; } = SelectionEntityType.Face;

    /// <summary>Topological surface name or path.</summary>
    public string SurfaceName { get; set; } = string.Empty;

    /// <summary>Number of referenced faces in this terminating reference.</summary>
    public uint FaceCount { get; set; }

    /// <inheritdoc />
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(SurfaceName);

    public override string ToString() =>
        $"EndConditionEntity ({EntityType}, Id={EntityId}, Surface='{SurfaceName}', Faces={FaceCount})";
}

/// <summary>
/// Multi-body feature scope specification (e.g. moPerBodyChooserData_c and moCompSolidBody_c).
/// </summary>
public sealed class BodyScopeData
{
    /// <summary>Whether to automatically select all intersecting bodies (AutoSelect = true).</summary>
    public bool AutoSelect { get; set; } = true;

    /// <summary>Raw chooser flags from moPerBodyChooserData_c (0x0002 = AutoSelect).</summary>
    public ushort Flags { get; set; } = 0x0002;

    /// <summary>Target body IDs when AutoSelect is false (selected feature scope bodies).</summary>
    public IReadOnlyList<uint> BodyIds { get; set; } = Array.Empty<uint>();

    /// <summary>Explicit entity references to selected bodies.</summary>
    public IReadOnlyList<EntityReference> Bodies { get; set; } = Array.Empty<EntityReference>();

    public override string ToString() =>
        $"BodyScope (AutoSelect={AutoSelect}, Flags=0x{Flags:X4}, BodiesCount={BodyIds.Count})";
}
