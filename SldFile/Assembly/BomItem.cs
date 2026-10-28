using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Represents an aggregated item entry in a Bill of Materials (BOM).
/// </summary>
public sealed class BomItem
{
    public int ItemNumber { get; internal set; }
    public string PartNumber { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public string ConfigurationName { get; internal set; } = string.Empty;
    public int Quantity { get; internal set; }
    public string FilePath { get; internal set; } = string.Empty;
    public bool IsVirtual { get; internal set; }
    public IReadOnlyList<string> InstanceNames { get; internal set; } = Array.Empty<string>();
}
