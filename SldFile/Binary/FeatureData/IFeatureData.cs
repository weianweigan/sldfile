namespace SldFile.Binary;

/// <summary>
/// Typed data payload for a specific MFC feature class in the ResolvedFeatures stream.
/// Each concrete implementation carries parsed fields and raw bytes for roundtrip fidelity.
/// </summary>
internal interface IFeatureData
{
    /// <summary>MFC class name (e.g. "moEndSpec_c", "moExtrusion_ct").
    /// Always available — used as the registry dispatch key.</summary>
    string ClassName { get; }

    /// <summary>Folder/feature type identifier from the FolderHeader.
    /// Only meaningful for _ct/_cM folder classes; 0 for non-folder classes.</summary>
    uint TypeId { get; }
}
