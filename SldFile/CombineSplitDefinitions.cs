namespace SldFile;

/// <summary>
/// Per-field evidence flags for CombineBodiesFeatureModel.
/// </summary>
public sealed class CombineBodiesFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Combine operation type was decoded.</summary>
    public bool OperationType { get; internal set; }

    /// <summary>Main body reference was decoded.</summary>
    public bool MainBody { get; internal set; }

    /// <summary>Ordered bodies-to-combine references were decoded.</summary>
    public bool BodiesToCombine { get; internal set; }
}

/// <summary>
/// Per-field evidence flags for SplitBodyFeatureModel.
/// </summary>
public sealed class SplitBodyFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Consume flag was decoded.</summary>
    public bool Consume { get; internal set; }

    /// <summary>Split-body state was decoded.</summary>
    public bool State { get; internal set; }

    /// <summary>Ordered trim-tool references were decoded.</summary>
    public bool TrimTools { get; internal set; }

    /// <summary>Trim-tool count was decoded without individual references.</summary>
    public bool TrimToolsCount { get; internal set; }

    /// <summary>Template path was decoded.</summary>
    public bool TemplatePath { get; internal set; }

    /// <summary>Override-default-template flag was decoded.</summary>
    public bool OverrideDefaultTemplateSettings { get; internal set; }
}

/// <summary>
/// Per-field evidence flags for DeleteBodyFeatureModel.
/// </summary>
public sealed class DeleteBodyFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Ordered body references were decoded.</summary>
    public bool Bodies { get; internal set; }

    /// <summary>Keep-bodies flag was decoded.</summary>
    public bool KeepBodies { get; internal set; }
}
