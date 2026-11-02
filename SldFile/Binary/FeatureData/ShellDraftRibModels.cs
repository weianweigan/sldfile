using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Per-field evidence flags for <see cref="ShellFeatureDefinitionData"/>.
/// All flags default to false: no Phase 3.2 sample has established a stream layout yet.
/// Setting an authoring input does not change these flags.
/// </summary>
public sealed class ShellFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample. Not established.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample. Not established.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Wall thickness was decoded from the feature stream. Not established.</summary>
    public bool Thickness { get; internal set; }

    /// <summary>Shell direction was decoded from the feature stream. Not established.</summary>
    public bool Direction { get; internal set; }

    /// <summary>Ordered removed-face references were decoded. Not established.</summary>
    public bool FacesRemoved { get; internal set; }

    /// <summary>Ordered multiple-thickness face references were decoded. Not established.</summary>
    public bool MultipleThicknessFaces { get; internal set; }

    /// <summary>Per-face multiple-thickness values were decoded. Not established.</summary>
    public bool MultipleThicknessValues { get; internal set; }
}

/// <summary>
/// Shell feature definition shape (<c>IShellFeatureData</c>).
/// <para>
/// No Shell sample exists yet, so every parameter is nullable and unset by default;
/// the values are authoring inputs, not file values. <see cref="TypeId"/> is 0 until
/// <see cref="SerializedTypeId"/> is populated from a validated sample.
/// </para>
/// </summary>
public sealed class ShellFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog candidate MFC class name; not sample-verified.</summary>
    public string ClassName => "moShell_c";

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved; see <see cref="Resolution"/>.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    /// <summary>Per-field evidence flags (all false until samples establish the layout).</summary>
    public ShellFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Default wall thickness in meters. Unset by default.</summary>
    public double? Thickness { get; set; }

    /// <summary>Shell direction (native enum value uncalibrated). Unset by default.</summary>
    public int? Direction { get; set; }

    /// <summary>Ordered faces to remove. Unset by default.</summary>
    public IReadOnlyList<EntityReference>? FacesRemoved { get; set; }

    /// <summary>Count derived from <see cref="FacesRemoved"/>; null when unset.</summary>
    public int? FacesRemovedCount => FacesRemoved?.Count;

    /// <summary>Ordered faces with non-default thickness. Unset by default.</summary>
    public IReadOnlyList<EntityReference>? MultipleThicknessFaces { get; set; }

    /// <summary>
    /// Thickness values in meters, index-aligned with <see cref="MultipleThicknessFaces"/>.
    /// Unset by default; index pairing has not been verified by samples.
    /// </summary>
    public IReadOnlyList<double>? MultipleThicknessValues { get; set; }
}

/// <summary>
/// Per-field evidence flags for <see cref="DraftFeatureDefinitionData"/>.
/// All flags default to false.
/// </summary>
public sealed class DraftFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample. Not established.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample. Not established.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Draft type (<c>swDraftType_e</c>) was decoded. Not established.</summary>
    public bool Type { get; internal set; }

    /// <summary>Draft angle was decoded. Not established.</summary>
    public bool Angle { get; internal set; }

    /// <summary>Direction reversal was decoded. Not established.</summary>
    public bool ReverseDirection { get; internal set; }

    /// <summary>Reduced-angle allowance was decoded. Not established.</summary>
    public bool AllowReducedAngle { get; internal set; }

    /// <summary>Neutral plane reference was decoded. Not established.</summary>
    public bool NeutralPlane { get; internal set; }

    /// <summary>Pull direction reference was decoded. Not established.</summary>
    public bool DirectionPull { get; internal set; }

    /// <summary>Ordered faces-to-draft references were decoded. Not established.</summary>
    public bool FacesToDraft { get; internal set; }

    /// <summary>Ordered parting-line references were decoded. Not established.</summary>
    public bool PartingLines { get; internal set; }

    /// <summary>Face propagation type was decoded. Not established.</summary>
    public bool FacePropagation { get; internal set; }

    /// <summary>Step draft type was decoded. Not established.</summary>
    public bool StepType { get; internal set; }
}

/// <summary>
/// Draft feature definition shape (<c>IDraftFeatureData2</c>).
/// No Draft sample exists yet; all values are nullable authoring inputs.
/// </summary>
public sealed class DraftFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog candidate MFC class name; not sample-verified.</summary>
    public string ClassName => "moDraft_c";

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    /// <summary>Per-field evidence flags (all false until samples establish the layout).</summary>
    public DraftFeatureParameterResolution Resolution { get; } = new();

    /// <summary><c>swDraftType_e</c>: 0 NeutralPlane, 1 PartingLine, 3 Step. Unset by default.</summary>
    public int? Type { get; set; }

    /// <summary>Draft angle in radians. Unset by default.</summary>
    public double? Angle { get; set; }

    /// <summary>Reverse draft direction. Unset by default.</summary>
    public bool? ReverseDirection { get; set; }

    /// <summary>Allow reduced angle. Unset by default.</summary>
    public bool? AllowReducedAngle { get; set; }

    /// <summary>Neutral plane reference. Unset by default.</summary>
    public EntityReference? NeutralPlane { get; set; }

    /// <summary>Pull-direction reference (edge/plane/axis). Unset by default.</summary>
    public DirectionReference? DirectionPull { get; set; }

    /// <summary>Ordered faces to draft. Unset by default.</summary>
    public IReadOnlyList<EntityReference>? FacesToDraft { get; set; }

    /// <summary>Ordered parting-line edges. Unset by default.</summary>
    public IReadOnlyList<EntityReference>? PartingLines { get; set; }

    /// <summary><c>swDraftFacePropagationType_e</c>: 0 None, 1 Tangent, 2 AllLoops, 3 InnerLoops, 4 OuterLoops. Unset by default.</summary>
    public int? FacePropagation { get; set; }

    /// <summary><c>swDraftStepType_e</c>: 3 TaperedStep, 6 Perpendicular. Unset by default.</summary>
    public int? StepType { get; set; }
}

/// <summary>
/// Per-field evidence flags for <see cref="RibFeatureDefinitionData"/>.
/// All flags default to false.
/// </summary>
public sealed class RibFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample. Not established.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample. Not established.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Rib thickness was decoded. Not established.</summary>
    public bool Thickness { get; internal set; }

    /// <summary>Rib type (<c>swRibType_e</c>) was decoded. Not established.</summary>
    public bool Type { get; internal set; }

    /// <summary>Extrusion direction (<c>swRibExtrusionDirection_e</c>) was decoded. Not established.</summary>
    public bool ExtrusionDirection { get; internal set; }

    /// <summary>Two-sided flag was decoded. Not established.</summary>
    public bool IsTwoSided { get; internal set; }

    /// <summary>Reverse thickness direction was decoded. Not established.</summary>
    public bool ReverseThicknessDir { get; internal set; }

    /// <summary>Flip side flag was decoded. Not established.</summary>
    public bool FlipSide { get; internal set; }

    /// <summary>Draft enable flag was decoded. Not established.</summary>
    public bool EnableDraft { get; internal set; }

    /// <summary>Draft angle was decoded. Not established.</summary>
    public bool DraftAngle { get; internal set; }

    /// <summary>Draft outward flag was decoded. Not established.</summary>
    public bool DraftOutward { get; internal set; }

    /// <summary>Draft-from-wall flag was decoded. Not established.</summary>
    public bool DraftFromWall { get; internal set; }

    /// <summary>Target body reference was decoded. Not established.</summary>
    public bool Body { get; internal set; }

    /// <summary>Reference sketch index was decoded. Not established.</summary>
    public bool RefSketchIndex { get; internal set; }
}

/// <summary>
/// Rib feature definition shape (<c>IRibFeatureData2</c>).
/// No Rib sample exists yet; all values are nullable authoring inputs.
/// </summary>
public sealed class RibFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog candidate MFC class name; not sample-verified.</summary>
    public string ClassName => "moRib_c";

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    /// <summary>Per-field evidence flags (all false until samples establish the layout).</summary>
    public RibFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Rib thickness in meters. Unset by default.</summary>
    public double? Thickness { get; set; }

    /// <summary><c>swRibType_e</c>: 0 Linear, 1 Natural. Unset by default.</summary>
    public int? Type { get; set; }

    /// <summary><c>swRibExtrusionDirection_e</c>: 0 ParallelToSketch, 1 NormalToSketch. Unset by default.</summary>
    public int? ExtrusionDirection { get; set; }

    /// <summary>Two-sided rib. Unset by default.</summary>
    public bool? IsTwoSided { get; set; }

    /// <summary>Reverse thickness direction. Unset by default.</summary>
    public bool? ReverseThicknessDir { get; set; }

    /// <summary>Flip side. Unset by default.</summary>
    public bool? FlipSide { get; set; }

    /// <summary>Enable draft. Unset by default.</summary>
    public bool? EnableDraft { get; set; }

    /// <summary>Draft angle in radians. Unset by default.</summary>
    public double? DraftAngle { get; set; }

    /// <summary>Draft outward. Unset by default.</summary>
    public bool? DraftOutward { get; set; }

    /// <summary>Draft from wall. Unset by default.</summary>
    public bool? DraftFromWall { get; set; }

    /// <summary>Target body reference. Unset by default.</summary>
    public EntityReference? Body { get; set; }

    /// <summary>Reference sketch index (native). Unset by default.</summary>
    public int? RefSketchIndex { get; set; }
}
