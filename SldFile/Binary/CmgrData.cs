using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Configuration Manager stream (CMgr).
/// Located at Contents/CMgr. Part and Assembly have different structures.
/// </summary>
internal sealed class CmgrData
{
    /// <summary>Display name for the configuration (e.g., "默认").</summary>
    public string DisplayName { get; internal set; } = string.Empty;

    /// <summary>Visual properties: colors, material.</summary>
    public CmgrVisualProperties? VisualProperties { get; internal set; }

    /// <summary>Cross-hatch pattern name (Part only).</summary>
    public string? HatchPattern { get; internal set; }

    /// <summary>External file references.</summary>
    public IReadOnlyList<CmgrExternalRef> FilePaths { get; internal set; } = Array.Empty<CmgrExternalRef>();

    /// <summary>Component names (Assembly only).</summary>
    public IReadOnlyList<string> ComponentNames { get; internal set; } = Array.Empty<string>();

    internal byte[]? RawData;
    internal List<CmgrExternalRef> FilePathsBuilder { get; set; } = new();
    internal List<string> ComponentNamesBuilder { get; set; } = new();

    internal void Freeze()
    {
        FilePaths = FilePathsBuilder.AsReadOnly();
        ComponentNames = ComponentNamesBuilder.AsReadOnly();
    }
}

/// <summary>
/// Visual properties for a configuration.
/// </summary>
internal sealed class CmgrVisualProperties
{
    public uint ColorRgb { get; set; }
    public float Transparency { get; set; }
    public float Ambient { get; set; }
    public float Diffuse { get; set; }
    public float Specular { get; set; }
    public float Shininess { get; set; }
    public float Emission { get; set; }
    public float Blend { get; set; }
}

/// <summary>
/// A file path reference in the configuration manager.
/// </summary>
internal sealed class CmgrExternalRef
{
    public string FullPath { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
}
