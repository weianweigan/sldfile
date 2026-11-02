using System.Collections.Generic;

namespace SldFile;

// Phase 5 weldment/structure feature definition shapes. No Phase 5 samples exist yet,
// so every value is nullable/unset by default and every Resolution flag is false.
// Catalog class-name candidates are documented in agent4-phase45-catalog.md; none is
// treated as a parsed file value until a sample establishes it.

/// <summary>Per-field evidence flags for <see cref="StructuralMemberFeatureDefinitionData"/>.</summary>
public sealed class StructuralMemberFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool AllowProtrusion { get; internal set; }
    public bool ApplyCornerTreatment { get; internal set; }
    public bool ConfigurationName { get; internal set; }
    public bool ConnectedSegmentsOption { get; internal set; }
    public bool ConnectionTypes { get; internal set; }
    public bool CornerTreatmentType { get; internal set; }
    public bool Groups { get; internal set; }
    public bool LibraryProfileMaterial { get; internal set; }
    public bool LocateProfilePoint { get; internal set; }
    public bool PathSegments { get; internal set; }
    public bool RotationAngle { get; internal set; }
    public bool TransferMaterial { get; internal set; }
    public bool WeldmentProfilePath { get; internal set; }
}

/// <summary>
/// Structural-member definition shape (<c>IStructuralMemberFeatureData</c>,
/// feature types <c>WeldMemberFeat</c>/<c>WeldmentFeature</c>).
/// </summary>
public sealed class StructuralMemberFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public StructuralMemberFeatureParameterResolution Resolution { get; } = new();

    public bool? AllowProtrusion { get; set; }
    public bool? ApplyCornerTreatment { get; set; }
    public string? ConfigurationName { get; set; }
    public int? ConnectedSegmentsOption { get; set; }

    /// <summary>Per-connection native type index (native indexed property). Unset by default.</summary>
    public IReadOnlyList<int>? ConnectionTypes { get; set; }

    public int? CornerTreatmentType { get; set; }

    /// <summary>Opaque structural-member group handles (native <c>StructuralMemberGroup</c> array). Contents not modeled.</summary>
    public IReadOnlyList<EntityReference>? Groups { get; set; }

    public string? LibraryProfileMaterial { get; set; }
    public EntityReference? LocateProfilePoint { get; set; }
    public IReadOnlyList<EntityReference>? PathSegments { get; set; }
    public double? RotationAngle { get; set; }
    public bool? TransferMaterial { get; set; }
    public string? WeldmentProfilePath { get; set; }

    public int? GroupsCount => Groups?.Count;
    public int? PathSegmentsCount => PathSegments?.Count;
    public int? ConnectionTypesCount => ConnectionTypes?.Count;
}

/// <summary>Per-field evidence flags for <see cref="EndCapFeatureDefinitionData"/>.</summary>
public sealed class EndCapFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool ChamferDistance { get; internal set; }
    public bool DepthDistance { get; internal set; }
    public bool Face { get; internal set; }
    public bool IsEndCapInward { get; internal set; }
    public bool OffsetDistance { get; internal set; }
    public bool Thickness { get; internal set; }
    public bool ThicknessRatioForOffset { get; internal set; }
    public bool UseChamferCorners { get; internal set; }
    public bool UseCornerTreatment { get; internal set; }
    public bool UseReverse { get; internal set; }
    public bool UseThicknessRatioForOffset { get; internal set; }
}

/// <summary>End-cap definition shape (<c>IEndCapFeatureData</c>, <c>EndCap</c>).</summary>
public sealed class EndCapFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public EndCapFeatureParameterResolution Resolution { get; } = new();

    public double? ChamferDistance { get; set; }
    public double? DepthDistance { get; set; }
    public EntityReference? Face { get; set; }

    /// <summary><c>swEndCapThicknessDirection_e</c>: 1 Outward, 2 Inward, 3 SpecifyInset. Unset by default.</summary>
    public int? IsEndCapInward { get; set; }

    public double? OffsetDistance { get; set; }
    public double? Thickness { get; set; }
    public double? ThicknessRatioForOffset { get; set; }
    public bool? UseChamferCorners { get; set; }
    public bool? UseCornerTreatment { get; set; }
    public bool? UseReverse { get; set; }
    public bool? UseThicknessRatioForOffset { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="GussetFeatureDefinitionData"/>.</summary>
public sealed class GussetFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool FlipOffsetDirection { get; internal set; }
    public bool FlipProfileDistanceParameters { get; internal set; }
    public bool OffsetUsed { get; internal set; }
    public bool ProfileAngle { get; internal set; }
    public bool ProfileDistance1 { get; internal set; }
    public bool ProfileDistance2 { get; internal set; }
    public bool ProfileDistance3 { get; internal set; }
    public bool ProfileDistance4 { get; internal set; }
    public bool ProfileLocation { get; internal set; }
    public bool ProfileOffsetDistance { get; internal set; }
    public bool ProfileType { get; internal set; }
    public bool Thickness { get; internal set; }
    public bool ThicknessType { get; internal set; }
    public bool UseAngle { get; internal set; }
    public bool SupportingFace1 { get; internal set; }
    public bool SupportingFace2 { get; internal set; }
}

/// <summary>Gusset definition shape (<c>IGussetFeatureData</c>, <c>Gusset</c>).</summary>
public sealed class GussetFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public GussetFeatureParameterResolution Resolution { get; } = new();

    public bool? FlipOffsetDirection { get; set; }
    public bool? FlipProfileDistanceParameters { get; set; }
    public bool? OffsetUsed { get; set; }
    public double? ProfileAngle { get; set; }
    public double? ProfileDistance1 { get; set; }
    public double? ProfileDistance2 { get; set; }
    public double? ProfileDistance3 { get; set; }
    public double? ProfileDistance4 { get; set; }

    /// <summary><c>swGussetProfileLocationType_e</c>: 0 Start, 1 Center, 2 End. Unset by default.</summary>
    public int? ProfileLocation { get; set; }

    public double? ProfileOffsetDistance { get; set; }

    /// <summary><c>swGussetProfileType_e</c>: 0 Polygon, 1 Triangle. Unset by default.</summary>
    public int? ProfileType { get; set; }

    public double? Thickness { get; set; }

    /// <summary><c>swGussetThicknessType_e</c>: 0 Inner, 1 BothSides, 2 Outer. Unset by default.</summary>
    public int? ThicknessType { get; set; }

    public bool? UseAngle { get; set; }

    /// <summary>First supporting face (native <c>GetSupportingFaces</c>). Unset by default.</summary>
    public EntityReference? SupportingFace1 { get; set; }

    /// <summary>Second supporting face (native <c>GetSupportingFaces</c>). Unset by default.</summary>
    public EntityReference? SupportingFace2 { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="WeldBeadFeatureDefinitionData"/>.</summary>
public sealed class WeldBeadFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool BeadLengths { get; internal set; }
    public bool BeadPitches { get; internal set; }
    public bool BeadSizes { get; internal set; }
    public bool BeadTypes { get; internal set; }
    public bool TangentPropagation { get; internal set; }
    public bool UseOtherSide { get; internal set; }
    public bool BeadSize { get; internal set; }
    public bool EdgeReferences { get; internal set; }
    public bool Side { get; internal set; }
}

/// <summary>
/// Weldment bead definition shape (<c>IWeldmentBeadFeatureData</c>, <c>WeldBeadFeat</c>).
/// Cosmetic weld beads use a separate interface (<c>ICosmeticWeldBeadFeatureData</c>)
/// and are not modeled in this round.
/// </summary>
public sealed class WeldBeadFeatureDefinitionData : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public WeldBeadFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Per-side bead length in meters (native indexed property). Unset by default.</summary>
    public IReadOnlyList<double>? BeadLengths { get; set; }

    /// <summary>Per-side bead pitch in meters (native indexed property). Unset by default.</summary>
    public IReadOnlyList<double>? BeadPitches { get; set; }

    /// <summary>Per-side bead size in meters (native indexed property). Unset by default.</summary>
    public IReadOnlyList<double>? BeadSizes { get; set; }

    /// <summary>Per-side <c>swWeldBeadType_e</c> (native indexed property). Unset by default.</summary>
    public IReadOnlyList<int>? BeadTypes { get; set; }

    public bool? TangentPropagation { get; set; }
    public bool? UseOtherSide { get; set; }

    /// <summary>Cosmetic weld bead size in meters; decoded for moCosmeticWeldBead_c.</summary>
    public double? BeadSize { get; set; }

    /// <summary>
    /// Cosmetic weld bead side. The native sample reads 1, but no controlled side
    /// variant exists; the stable integer candidate remains unpromoted/null.
    /// </summary>
    public int? Side { get; set; }

    /// <summary>Cosmetic weld bead side/edge references in wrapper order.</summary>
    public IReadOnlyList<EntityReference>? EdgeReferences { get; set; }

    public int? EdgeReferenceCount => EdgeReferences?.Count;
}
