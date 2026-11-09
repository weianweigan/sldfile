using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Represents an aggregated item entry in a Bill of Materials (BOM).
/// </summary>
public sealed class BomItem
{
    public int ItemNumber { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConfigurationName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string FilePath { get; set; } = string.Empty;
    public string? ResolvedFilePath { get; set; }
    public bool IsVirtual { get; set; }
    public IReadOnlyList<string> InstanceNames { get; set; } = Array.Empty<string>();

    /// <summary>Hierarchical child BOM items when this item represents a sub-assembly.</summary>
    public IReadOnlyList<BomItem> Children { get; set; } = Array.Empty<BomItem>();

    /// <summary>Custom metadata properties attached to this component or configuration.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
