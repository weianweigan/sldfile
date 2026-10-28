using System;
using System.Collections.Generic;
using System.Numerics;

namespace SldFile;

/// <summary>
/// Represents a component instance within a SolidWorks assembly tree.
/// </summary>
public sealed class AssemblyComponentNode
{
    public string ComponentId { get; internal set; } = string.Empty;
    public string Name { get; internal set; } = string.Empty;
    public string InstanceName { get; internal set; } = string.Empty;
    public int ReferenceNumber { get; internal set; }
    public string FilePath { get; internal set; } = string.Empty;
    public string ConfigurationName { get; internal set; } = string.Empty;
    public Matrix4x4 Transform { get; internal set; } = Matrix4x4.Identity;
    public bool IsSuppressed { get; internal set; }
    public bool IsHidden { get; internal set; }
    public bool IsVirtual { get; internal set; }
    public bool IsFlexible { get; internal set; }
    public bool ExcludeFromBom { get; internal set; }

    public IReadOnlyList<AssemblyComponentNode> Children { get; internal set; } = Array.Empty<AssemblyComponentNode>();
}
