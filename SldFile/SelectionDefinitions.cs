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
/// Represents a selected closed sketch contour loop.
/// </summary>
public sealed class SketchContourReference : ISelectionReference
{
    public string ReferenceType => "Contour";
    public uint EntityId { get; set; }
    public uint ChainId { get; set; }
    public IReadOnlyList<uint> EntityIndices { get; set; } = Array.Empty<uint>();
    public bool IsClosed { get; set; } = true;
    public bool IsCandidateOnly { get; set; }
    public bool IsValid => EntityIndices.Count > 0;

    public override string ToString() =>
        $"Contour #{EntityId} (Chain {ChainId}, Entities: [{string.Join(", ", EntityIndices)}], Closed={IsClosed}" +
        (IsCandidateOnly ? ", Candidate=True)" : ")");
}

/// <summary>
/// Represents a selected planar sketch region.
/// </summary>
public sealed class SketchRegionReference : ISelectionReference
{
    public string ReferenceType => "Region";
    public uint EntityId { get; set; }
    public uint RegionIndex { get; set; }
    public uint LoopCount { get; set; }
    public IReadOnlyList<uint> EdgeIds { get; set; } = Array.Empty<uint>();
    public bool IsValid => RegionIndex > 0 || EntityId > 0 || EdgeIds.Count > 0;

    public override string ToString() =>
        $"Region #{RegionIndex} (EntityId={EntityId}, Loops={LoopCount}" +
        (EdgeIds.Count > 0 ? $", Edges=[{string.Join(", ", EdgeIds)}])" : ")");
}

public enum SketchCurveRole
{
    Unknown = 0,
    Profile = 1,
    Path = 2,
    Guide = 3,
    Centerline = 4
}

public class SketchCurveReference : ISelectionReference
{
    public string ReferenceType => "SketchCurve";
    public SketchCurveRole Role { get; set; }
    public uint EntityId { get; set; }
    public uint ArchiveSerial { get; set; }
    public ushort HeaderSelectionId { get; set; }
    public bool IsHeaderSelectionIdPositional { get; set; }
    public bool IsValid => EntityId > 0;

    public override string ToString() =>
        $"SketchCurve ({Role}, SketchId={EntityId}, Serial=0x{ArchiveSerial:X8}, HeaderId=0x{HeaderSelectionId:X4}" +
        (IsHeaderSelectionIdPositional ? ", Positional=True)" : ")");
}

public class DirectionReference : ISelectionReference
{
    public string ReferenceType => "Direction";
    public uint EntityId { get; set; }
    public SelectionEntityType EntityType { get; set; } = SelectionEntityType.Edge;
    public string ReferenceName { get; set; } = string.Empty;
    public double[]? Vector { get; set; }
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

public class EntityReference : ISelectionReference
{
    public string ReferenceType => "Entity";
    public uint EntityId { get; set; }
    public SelectionEntityType EntityType { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(Name);

    public override string ToString() =>
        $"EntityReference ({EntityType}, Id={EntityId}, Name='{Name}')";
}

public class EndConditionEntityReference : ISelectionReference
{
    public string ReferenceType => "EndConditionEntity";
    public uint EntityId { get; set; }
    public SelectionEntityType EntityType { get; set; } = SelectionEntityType.Face;
    public string SurfaceName { get; set; } = string.Empty;
    public uint FaceCount { get; set; }
    public bool IsValid => EntityId > 0 || !string.IsNullOrEmpty(SurfaceName);

    public override string ToString() =>
        $"EndConditionEntity ({EntityType}, Id={EntityId}, Surface='{SurfaceName}', Faces={FaceCount})";
}

public class BodyScopeData
{
    public bool AutoSelect { get; set; } = true;
    public ushort Flags { get; set; } = 0x0002;
    public IReadOnlyList<uint> BodyIds { get; set; } = Array.Empty<uint>();
    public IReadOnlyList<EntityReference> Bodies { get; set; } = Array.Empty<EntityReference>();

    public override string ToString() =>
        $"BodyScope (AutoSelect={AutoSelect}, Flags=0x{Flags:X4}, BodiesCount={BodyIds.Count})";
}

public sealed class FeatureReferenceData
{
    public SelectionEntityType? EntityType { get; set; }
    public uint? EntityId { get; set; }
    public uint? PersistentId { get; set; }
    public string? ReferenceName { get; set; }
    public uint? ClassWord { get; set; }
    public string? ArchiveGuid { get; set; }
    public int? RecordCount { get; set; }
}
