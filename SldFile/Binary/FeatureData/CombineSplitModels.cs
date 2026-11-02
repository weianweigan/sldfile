using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Per-field evidence flags for <see cref="CombineBodiesFeatureDefinitionData"/>.
/// All flags default to false: no Phase 3.3 sample has established a stream layout yet.
/// </summary>
public sealed class CombineBodiesFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample. Not established.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample. Not established.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Combine operation type was decoded. Not established.</summary>
    public bool OperationType { get; internal set; }

    /// <summary>Main body reference was decoded. Not established.</summary>
    public bool MainBody { get; internal set; }

    /// <summary>Ordered bodies-to-combine references were decoded. Not established.</summary>
    public bool BodiesToCombine { get; internal set; }
}

/// <summary>
/// Combine-bodies feature definition shape (<c>ICombineBodiesFeatureData</c>).
/// No Combine sample exists yet; all values are nullable authoring inputs.
/// </summary>
public sealed class CombineBodiesFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog candidate MFC class name; not sample-verified.</summary>
    public string ClassName => "moCombineBodies_c";

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    /// <summary>Per-field evidence flags (all false until samples establish the layout).</summary>
    public CombineBodiesFeatureParameterResolution Resolution { get; } = new();

    /// <summary><c>swCombineBodiesOperationType_e</c>: 0 Add, 1 Subtract, 2 Common. Unset by default.</summary>
    public int? OperationType { get; set; }

    /// <summary>Main body kept by a subtract operation. Unset by default.</summary>
    public EntityReference? MainBody { get; set; }

    /// <summary>Ordered tool bodies. Unset by default.</summary>
    public IReadOnlyList<EntityReference>? BodiesToCombine { get; set; }

    /// <summary>Count derived from <see cref="BodiesToCombine"/>; null when unset.</summary>
    public int? BodiesToCombineCount => BodiesToCombine?.Count;
}

/// <summary>
/// Per-field evidence flags for <see cref="SplitBodyFeatureDefinitionData"/>.
/// All flags default to false.
/// </summary>
public sealed class SplitBodyFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample. Not established.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample. Not established.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Consume flag was decoded. Not established.</summary>
    public bool Consume { get; internal set; }

    /// <summary>Split-body state was decoded. Not established.</summary>
    public bool State { get; internal set; }

    /// <summary>Ordered trim-tool references were decoded. Not established.</summary>
    public bool TrimTools { get; internal set; }

    /// <summary>Trim-tool count was decoded without individual references. Not established.</summary>
    public bool TrimToolsCount { get; internal set; }

    /// <summary>Template path was decoded. Not established.</summary>
    public bool TemplatePath { get; internal set; }

    /// <summary>Override-default-template flag was decoded. Not established.</summary>
    public bool OverrideDefaultTemplateSettings { get; internal set; }
}

/// <summary>
/// Split-body feature definition shape (<c>ISplitBodyFeatureData</c>).
/// No Split sample exists yet; all values are nullable authoring inputs.
/// Split output bodies (native <c>GetSplitBodies</c>) are outputs, not inputs,
/// and are intentionally not exposed here.
/// </summary>
public sealed class SplitBodyFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog candidate MFC class name; not sample-verified.</summary>
    public string ClassName => SerializedClassName ?? "moSplitBody_c";

    /// <summary>
    /// Serialized MFC class name read from a tutorial sample (<c>moSplit_c</c>).
    /// Null keeps the catalog candidate.
    /// </summary>
    public string? SerializedClassName { get; set; }

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    /// <summary>Per-field evidence flags (all false until samples establish the layout).</summary>
    public SplitBodyFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Consume the split body. Unset by default.</summary>
    public bool? Consume { get; set; }

    /// <summary><c>swSplitBodyType_e</c>: 0 Show, 1 Hide, 2 Consume. Unset by default.</summary>
    public int? State { get; set; }

    /// <summary>Ordered trim tools (surface, plane or body). Unset by default.</summary>
    public IReadOnlyList<EntityReference>? TrimTools { get; set; }

    /// <summary>
    /// Count decoded from the class payload when individual trim-tool references were not resolved.
    /// Null when unset.
    /// </summary>
    public int? DecodedTrimToolsCount { get; set; }

    /// <summary>Count derived from <see cref="TrimTools"/> or the decoded scalar count; null when unset.</summary>
    public int? TrimToolsCount => TrimTools?.Count ?? DecodedTrimToolsCount;

    /// <summary>Template path used when saving split bodies. Unset by default.</summary>
    public string? TemplatePath { get; set; }

    /// <summary>Override default template settings. Unset by default.</summary>
    public bool? OverrideDefaultTemplateSettings { get; set; }
}

/// <summary>
/// Per-field evidence flags for <see cref="DeleteBodyFeatureDefinitionData"/>.
/// All flags default to false.
/// </summary>
public sealed class DeleteBodyFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample. Not established.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample. Not established.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Ordered body references were decoded. Not established.</summary>
    public bool Bodies { get; internal set; }

    /// <summary>Keep-bodies flag was decoded. Not established.</summary>
    public bool KeepBodies { get; internal set; }
}

/// <summary>
/// Delete-body feature definition shape (<c>IDeleteBodyFeatureData</c>).
/// No DeleteBody sample exists yet; all values are nullable authoring inputs.
/// </summary>
public sealed class DeleteBodyFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog candidate MFC class name; not sample-verified.</summary>
    public string ClassName => "moDeleteBody_c";

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when <see cref="SerializedTypeId"/> is unresolved.</summary>
    public uint TypeId => SerializedTypeId ?? 0;

    /// <summary>Per-field evidence flags (all false until samples establish the layout).</summary>
    public DeleteBodyFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Ordered body references. Unset by default.</summary>
    public IReadOnlyList<EntityReference>? Bodies { get; set; }

    /// <summary>Count derived from <see cref="Bodies"/>; null when unset.</summary>
    public int? BodiesCount => Bodies?.Count;

    /// <summary>true keeps the selected bodies (deletes the others); false deletes the selected bodies. Unset by default.</summary>
    public bool? KeepBodies { get; set; }
}
