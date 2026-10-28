using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// DisplayLists stream data: display settings, line fonts, lights, materials.
/// Located at Contents/DisplayLists__ZLB (ZLB-compressed MFC stream).
/// </summary>
internal sealed class DisplayListsData
{
    /// <summary>12 f64 camera/viewport settings from the 96-byte prefix.</summary>
    public IReadOnlyList<double> Prefix { get; internal set; } = Array.Empty<double>();

    /// <summary>User name from uiUserModelEnv_c.</summary>
    public string UserName { get; internal set; } = string.Empty;

    /// <summary>Line font name from uiLineFontMgr_c.</summary>
    public string LineFontName { get; internal set; } = string.Empty;

    /// <summary>Line font configuration entries from uiLFConfig_c.</summary>
    public IReadOnlyList<LineFontConfigEntry> LineFontConfig { get; internal set; }
        = Array.Empty<LineFontConfigEntry>();

    /// <summary>Number of ambient light definitions.</summary>
    public int AmbientLightCount { get; internal set; }

    /// <summary>Number of directional light definitions.</summary>
    public int DirectionalLightCount { get; internal set; }

    /// <summary>WChar strings from moVisualProperties_c (material names, colors).</summary>
    public IReadOnlyList<string> VisualPropertiesStrings { get; internal set; } = Array.Empty<string>();

    /// <summary>Size of uoTempBodyTessData_c block in bytes.</summary>
    public int BodyTessDataSize { get; internal set; }

    /// <summary>Size of uoTempFaceTessData_c block in bytes.</summary>
    public int FaceTessDataSize { get; internal set; }

    /// <summary>Whether assembly-specific data (uoTempAssemblySHDData_c) is present.</summary>
    public bool HasAssemblyData { get; internal set; }

    /// <summary>Whether part tessellation data (uoTempPartTessData_c) is present.</summary>
    public bool HasPartTessData { get; internal set; }

    internal byte[]? RawData;
}

/// <summary>
/// A line font configuration entry: a style name paired with a line type.
/// </summary>
internal sealed class LineFontConfigEntry
{
    public string StyleName { get; set; } = string.Empty;
    public string LineType { get; set; } = string.Empty;
}
