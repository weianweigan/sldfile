using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Document type for the Definition stream.
/// </summary>
internal enum DefinitionFileType
{
    Unknown = 0,
    Part = 0x51,
    Assembly = 0x68,
    Drawing = 0x50
}

/// <summary>
/// Definition stream: document-wide settings and metadata.
/// Located at Contents/Definition. Part/Asm (~5KB) and Drawing (~145KB) have different structures.
/// </summary>
internal sealed class DefinitionData
{
    /// <summary>Document type identified by header signature.</summary>
    public DefinitionFileType FileType { get; internal set; }

    /// <summary>Line style name (Part/Asm only, e.g., "CONTINUOUS").</summary>
    public string? LineStyle { get; internal set; }

    /// <summary>Line font name (Part/Asm only, e.g., "Detail").</summary>
    public string? LineFont { get; internal set; }

    /// <summary>Line font config name (Part/Asm only, e.g., "CONTINUOUS").</summary>
    public string? LineFontConfig { get; internal set; }

    /// <summary>User name from uiUserModelEnv_c (Part/Asm only).</summary>
    public string? UserName { get; internal set; }

    /// <summary>Unit system class names observed in the stream (Drawing only).</summary>
    public IReadOnlyList<string> UnitSystemClasses { get; internal set; } = Array.Empty<string>();

    internal byte[]? RawData;
    internal List<string> UnitSystemClassesBuilder { get; set; } = new();

    internal void Freeze()
    {
        UnitSystemClasses = UnitSystemClassesBuilder.AsReadOnly();
    }
}
