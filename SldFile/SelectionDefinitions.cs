using System;
using System.Collections.Generic;

namespace SldFile;

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

    /// <inheritdoc />
    public bool IsValid => EntityIndices.Count > 0;

    public override string ToString() =>
        $"Contour #{EntityId} (Chain {ChainId}, Entities: [{string.Join(", ", EntityIndices)}], Closed={IsClosed})";
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

    /// <inheritdoc />
    public bool IsValid => RegionIndex > 0 || EntityId > 0;

    public override string ToString() =>
        $"Region #{RegionIndex} (EntityId={EntityId}, Loops={LoopCount})";
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
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(ReferenceName) || Vector != null;

    public override string ToString() =>
        $"Direction ({EntityType}, EntityId={EntityId}, Name='{ReferenceName}'" +
        (Vector != null ? $", Vec=[{Vector[0]:F3}, {Vector[1]:F3}, {Vector[2]:F3}])" : ")");
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
    public uint FaceCount { get; set; } = 1;

    /// <inheritdoc />
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(SurfaceName) || FaceCount > 0;

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
