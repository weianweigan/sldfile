namespace SldFile.Assembly.Bom;

/// <summary>
/// Configuration options for BOM generation and component recursion.
/// </summary>
public sealed class BomOptions
{
    public string? ConfigurationName { get; set; }
    public IPathResolver? PathResolver { get; set; }
    public bool IncludeSuppressed { get; set; } = false;
    public bool IncludeHidden { get; set; } = false;
    public bool IncludeExcludedFromBom { get; set; } = false;
    public int MaxDepth { get; set; } = 32;
}
