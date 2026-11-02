using System;
using System.Collections.Generic;

namespace SldFile;

// Phase 4 surface feature definition shapes. No Phase 4 samples exist yet, so every
// value is nullable/unset by default and every Resolution flag is false. Values are
// authoring inputs, never parsed file values until a validated parser sets the flags.

/// <summary>Per-field evidence flags for <see cref="SurfaceExtrudeFeatureDefinitionData"/>.</summary>
public sealed class SurfaceExtrudeFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Depth { get; internal set; }
    public bool EndCondition { get; internal set; }
    public bool BothDirections { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool D1CapEnd { get; internal set; }
    public bool D2CapEnd { get; internal set; }
    public bool D1DraftOn { get; internal set; }
    public bool D2DraftOn { get; internal set; }
    public bool D1DraftAngle { get; internal set; }
    public bool D2DraftAngle { get; internal set; }
    public bool D1DraftOutward { get; internal set; }
    public bool D2DraftOutward { get; internal set; }
    public bool DeleteOriginalFace { get; internal set; }
    public bool KnitResult { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Surface extrude definition shape (<c>ISurfExtrudeFeatureData</c>, feature type
/// <c>ExtruRefSurface</c>). MFC class name/type code are not established by any sample.
/// </summary>
public sealed class SurfaceExtrudeFeatureDefinitionData : IFeatureData
{
    /// <summary>Serialized MFC class name; empty until a sample establishes it.</summary>
    public string ClassName => SerializedClassName ?? string.Empty;

    /// <summary>Serialized MFC class name; null until decoded from a sample.</summary>
    public string? SerializedClassName { get; set; }

    /// <summary>Folder-header type code; null until a sample establishes it.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    public SurfaceExtrudeFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Direction 1 depth in meters (from <c>GetDepth(true)</c>). Unset until decoded.</summary>
    public double? Depth { get; set; }

    /// <summary><c>swEndConditions_e</c> for direction 1 (from <c>GetEndCondition(true)</c>). Unset until decoded.</summary>
    public int? EndCondition { get; set; }

    public bool? BothDirections { get; set; }
    public bool? ReverseDirection { get; set; }
    public bool? D1CapEnd { get; set; }
    public bool? D2CapEnd { get; set; }
    public bool? D1DraftOn { get; set; }
    public bool? D2DraftOn { get; set; }
    public double? D1DraftAngle { get; set; }
    public double? D2DraftAngle { get; set; }
    public bool? D1DraftOutward { get; set; }
    public bool? D2DraftOutward { get; set; }
    public bool? DeleteOriginalFace { get; set; }
    public bool? KnitResult { get; set; }

    /// <summary>Presence-level surface-body folder reference; null when the folder layout is not validated.</summary>
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceRevolveFeatureDefinitionData"/>.</summary>
public sealed class SurfaceRevolveFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Angle { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool Type { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>Surface revolve definition shape (<c>ISurfRevolveFeatureData</c>, <c>RevolvRefSurf</c>).</summary>
public sealed class SurfaceRevolveFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceRevolveFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Revolution angle in radians (from <c>GetRevolutionAngle(true)</c>). Unset until decoded.</summary>
    public double? Angle { get; set; }

    public bool? ReverseDirection { get; set; }
    public int? Type { get; set; }

    /// <summary>Presence-level surface-body folder reference; null when the folder layout is not validated.</summary>
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceSweepFeatureDefinitionData"/>.</summary>
public sealed class SurfaceSweepFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool SelectionReferenceList { get; internal set; }
    public bool CircularProfile { get; internal set; }
    public bool PathReference { get; internal set; }

    /// <summary>Native <c>ISweepFeatureData.SweepType</c> measured on both controlled samples.</summary>
    public bool SweepType { get; internal set; }

    /// <summary>Native merge option measured on both controlled samples.</summary>
    public bool Merge { get; internal set; }

    /// <summary>Presence-level surface-body folder reference was decoded.</summary>
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Surface sweep definition shape (serialized class <c>moSweepRefSurface_c</c>), distinct
/// from solid <see cref="SweepFeatureDefinitionData"/>. The measured sample is a
/// circular-profile sweep with a single path selection and diameter 0.006 m.
/// </summary>
public sealed class SurfaceSweepFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceSweepFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Opaque ordered selection-reference ids from the folder header.</summary>
    public IReadOnlyList<ushort> SelectionReferenceIds { get; set; } = Array.Empty<ushort>();

    /// <summary>
    /// Decoded path sketch reference (single path selection in the circular-profile
    /// layout; header id is positional only).
    /// </summary>
    public SketchCurveReference? PathReference { get; set; }

    /// <summary>True when the circular-profile sweep layout was decoded.</summary>
    public bool CircularProfileLayout { get; set; }

    /// <summary>Circular profile diameter in meters, or null when unresolved.</summary>
    public double? CircularProfileDiameter { get; set; }

    /// <summary>
    /// Native <c>ISweepFeatureData.SweepType</c>. The controlled surface circular-profile
    /// samples read back 62 (solid circular-profile samples read back 17).
    /// </summary>
    public int? SweepType { get; set; }

    /// <summary>
    /// Native merge option. The controlled samples read back true; the stream slot was not
    /// isolated because no false variant exists.
    /// </summary>
    public bool? Merge { get; set; }

    /// <summary>Presence-level surface-body folder reference; null when the folder layout is not validated.</summary>
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }

    public int SelectionCount => SelectionReferenceIds.Count;
}

/// <summary>Per-field evidence flags for <see cref="SurfaceLoftFeatureDefinitionData"/>.</summary>
public sealed class SurfaceLoftFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool SelectionReferenceList { get; internal set; }
    public bool ProfileReferences { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Surface loft node (<c>moBlendRefSurface_c</c>). Native <c>BlendRefSurface</c> has no
/// FeatureData interface; this model exposes only the serialized class/type code, the
/// ordered profile-selection ids and the decoded profile sketch references.
/// </summary>
public sealed class SurfaceLoftFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceLoftFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<ushort> SelectionReferenceIds { get; set; } = Array.Empty<ushort>();

    /// <summary>Decoded profile sketch references in wrapper order (Profile1, Profile2, ...).</summary>
    public IReadOnlyList<SketchCurveReference> References { get; set; } = Array.Empty<SketchCurveReference>();

    /// <summary>Presence-level surface-body folder reference; null when the folder layout is not validated.</summary>
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }

    public int SelectionCount => SelectionReferenceIds.Count;
    public int ReferenceCount => References.Count;
}

/// <summary>Per-field evidence flags for <see cref="PlanarSurfaceFeatureDefinitionData"/>.</summary>
public sealed class PlanarSurfaceFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }

    /// <summary>API <c>BoundingEntities</c> (single sketch reference) was decoded.</summary>
    public bool BoundingEntities { get; internal set; }

    /// <summary>Raw boundary-segment candidates (moSegmEdgeIdRep_c) were structurally scanned.</summary>
    public bool BoundarySegments { get; internal set; }

    /// <summary>Presence-level surface-body folder reference was decoded.</summary>
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Planar surface definition shape (<c>ISurfacePlanarFeatureData</c>, feature type
/// <c>PlanarSurface</c>). The API exposes one bounding sketch; the stream additionally
/// contains four candidate boundary-segment records. Both are kept separate.
/// </summary>
public sealed class PlanarSurfaceFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public PlanarSurfaceFeatureParameterResolution Resolution { get; } = new();

    /// <summary>
    /// API <c>BoundingEntities</c>: one bounding sketch reference (native count = 1 in the
    /// controlled sample). Unset until decoded.
    /// </summary>
    public IReadOnlyList<EntityReference>? BoundingEntities { get; set; }

    /// <summary>
    /// Raw <c>moSegmEdgeIdRep_c</c> boundary-segment records (4 in the controlled sample).
    /// These are NOT the API BoundingEntities and remain candidate-only.
    /// </summary>
    public IReadOnlyList<EntityReference>? BoundarySegments { get; set; }

    /// <summary>Presence-level surface-body folder reference; null when the folder layout is not validated.</summary>
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }

    public int? BoundingEntitiesCount => BoundingEntities?.Count;
    public int? BoundarySegmentsCount => BoundarySegments?.Count;
}

/// <summary>Per-field evidence flags for <see cref="FillSurfaceFeatureDefinitionData"/>.</summary>
public sealed class FillSurfaceFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Merge { get; internal set; }
    public bool OptimizeSurface { get; internal set; }
    public bool ResolutionControl { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool ReverseSurface { get; internal set; }
    public bool TryToFormSolid { get; internal set; }
    public bool PatchBoundary { get; internal set; }
    public bool BoundaryCurvature { get; internal set; }
}

/// <summary>Fill surface definition shape (<c>IFillSurfaceFeatureData</c>, <c>FillRefSurface</c>).</summary>
public sealed class FillSurfaceFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public FillSurfaceFeatureParameterResolution Resolution { get; } = new();

    public bool? Merge { get; set; }
    public bool? OptimizeSurface { get; set; }

    /// <summary>
    /// Native resolution control (1/2). Measured slot: feature header + 0x307 with the
    /// surrounding 00 F0 BF ... 82 marker; only the res2 variant changes 0x41 to 0x49.
    /// </summary>
    public int? ResolutionControl { get; set; }
    public bool? ReverseDirection { get; set; }
    public bool? ReverseSurface { get; set; }
    public bool? TryToFormSolid { get; set; }

    /// <summary>Native patch-boundary count. No controlled variation; null until decoded.</summary>
    public int? PatchBoundaryCount { get; set; }

    /// <summary>Patch-boundary entities. No controlled variation; null until decoded.</summary>
    public IReadOnlyList<EntityReference>? PatchBoundary { get; set; }

    /// <summary>
    /// Per-boundary curvature controls (native curvatureControl: 0/1/2 for G0/G1/G2).
    /// The curvature1 variant did not change the native readback, so this stays null.
    /// </summary>
    public IReadOnlyList<int>? BoundaryCurvatureControls { get; set; }

    public int? PatchBoundaryEntityCount => PatchBoundary?.Count;
    public int? BoundaryCurvatureControlCount => BoundaryCurvatureControls?.Count;
}

/// <summary>Per-field evidence flags for <see cref="SurfaceKnitFeatureDefinitionData"/>.</summary>
public sealed class SurfaceKnitFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Entities { get; internal set; }
    public bool KnitTolerance { get; internal set; }
    public bool MaxValueForGapRange { get; internal set; }
    public bool MinValueForGapRange { get; internal set; }
    public bool SeedFace { get; internal set; }
    public bool UseGapFilters { get; internal set; }
    public bool UseMergeEntities { get; internal set; }
    public bool UseTryToFormSolid { get; internal set; }
    public bool StitchReferences { get; internal set; }
}

/// <summary>Surface knit definition shape (<c>ISurfaceKnitFeatureData</c>, <c>SewRefSurface</c>).</summary>
public sealed class SurfaceKnitFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceKnitFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<EntityReference>? Entities { get; set; }
    public double? KnitTolerance { get; set; }
    public double? MaxValueForGapRange { get; set; }
    public double? MinValueForGapRange { get; set; }
    public EntityReference? SeedFace { get; set; }
    public bool? UseGapFilters { get; set; }
    public bool? UseMergeEntities { get; set; }
    public bool? UseTryToFormSolid { get; set; }

    /// <summary>
    /// Decoded <c>moCompSurfaceBody_c</c> stitch/body references in wrapper order.
    /// Presence/opaque id only; no geometric reconstruction.
    /// </summary>
    public IReadOnlyList<EntityReference>? StitchReferences { get; set; }

    public int? EntitiesCount => Entities?.Count;
    public int? StitchReferenceCount => StitchReferences?.Count;
}

/// <summary>Per-field evidence flags for <see cref="SurfaceTrimFeatureDefinitionData"/>.</summary>
public sealed class SurfaceTrimFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool PiecesToKeep { get; internal set; }
    public bool TrimTools { get; internal set; }
    public bool TrimType { get; internal set; }
}

/// <summary>Surface trim definition shape (<c>ISurfaceTrimFeatureData</c>, <c>TrimRefSurface</c>).</summary>
public sealed class SurfaceTrimFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceTrimFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<EntityReference>? PiecesToKeep { get; set; }
    public IReadOnlyList<EntityReference>? TrimTools { get; set; }

    /// <summary><c>swSurfaceTrimType_e</c>: 0 TrimTool, 1 MutualTrim (from <c>GetType()</c>). Unset by default.</summary>
    public int? TrimType { get; set; }

    public int? PiecesToKeepCount => PiecesToKeep?.Count;
    public int? TrimToolsCount => TrimTools?.Count;
}

/// <summary>Per-field evidence flags for <see cref="SurfaceExtendFeatureDefinitionData"/>.</summary>
public sealed class SurfaceExtendFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Distance { get; internal set; }
    public bool EndCondition { get; internal set; }
    public bool Face { get; internal set; }
    public bool Vertex { get; internal set; }
    public bool Items { get; internal set; }
    public bool PropagatingEdges { get; internal set; }
    public bool Type { get; internal set; }
}

/// <summary>Surface extend definition shape (<c>ISurfaceExtendFeatureData</c>, <c>ExtendRefSurface</c>).</summary>
public sealed class SurfaceExtendFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceExtendFeatureParameterResolution Resolution { get; } = new();

    public double? Distance { get; set; }

    /// <summary><c>swSurfaceExtendEndCond_e</c>: 0 Distance, 1 UpToPoint, 2 UpToSurface. Unset by default.</summary>
    public int? EndCondition { get; set; }

    public EntityReference? Face { get; set; }
    public EntityReference? Vertex { get; set; }
    public IReadOnlyList<EntityReference>? Items { get; set; }
    public IReadOnlyList<EntityReference>? PropagatingEdges { get; set; }
    public int? Type { get; set; }

    public int? ItemsCount => Items?.Count;
    public int? PropagatingEdgesCount => PropagatingEdges?.Count;
}

/// <summary>Per-field evidence flags for <see cref="SurfaceOffsetFeatureDefinitionData"/>.</summary>
public sealed class SurfaceOffsetFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Distance { get; internal set; }
    public bool Entities { get; internal set; }
    public bool Faces { get; internal set; }
    public bool Flip { get; internal set; }
    public bool BodyReferences { get; internal set; }
}

/// <summary>Surface offset definition shape (<c>ISurfaceOffsetFeatureData</c>, <c>OffsetRefSuface</c>).</summary>
public sealed class SurfaceOffsetFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceOffsetFeatureParameterResolution Resolution { get; } = new();

    public double? Distance { get; set; }
    public IReadOnlyList<EntityReference>? Entities { get; set; }
    public IReadOnlyList<EntityReference>? Faces { get; set; }
    public bool? Flip { get; set; }

    /// <summary>
    /// Decoded <c>moCompSurfaceBody_c</c> body references in wrapper order.
    /// Presence/opaque id only; no geometric reconstruction.
    /// </summary>
    public IReadOnlyList<EntityReference>? BodyReferences { get; set; }

    public int? EntitiesCount => Entities?.Count;
    public int? FacesCount => Faces?.Count;
    public int? BodyReferenceCount => BodyReferences?.Count;
}
