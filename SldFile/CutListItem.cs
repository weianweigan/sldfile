using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Represents one item in a weldment cut list (e.g. structural tube, plate).
/// </summary>
public sealed class CutListItem
{
    /// <summary>Feature name as stored in the file (e.g. "UNIDAL AA 7019<1>").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Number of identical bodies in the weldment.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Custom properties attached to this cut-list item.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}
