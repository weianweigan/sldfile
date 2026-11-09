namespace SldFile;

/// <summary>
/// Base interface for typed SolidWorks feature data and MFC elements.
/// </summary>
public interface IFeatureData
{
    /// <summary>MFC class name (e.g. "moExtrusion_c", "moBaseFlange_c").</summary>
    string ClassName { get; }

    /// <summary>Folder/feature type identifier from the FolderHeader.</summary>
    uint TypeId { get; }
}
