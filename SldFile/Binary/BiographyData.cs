using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Biography stream: file access path history.
/// Located at _MO_VERSION_5000/Biography.
/// </summary>
internal sealed class BiographyData
{
    /// <summary>Service pack version string (first WChar entry).</summary>
    public string ServicePackVersion { get; internal set; } = string.Empty;

    /// <summary>All file path entries recorded during editing sessions.</summary>
    public IReadOnlyList<BiographyPathEntry> PathEntries { get; internal set; } = Array.Empty<BiographyPathEntry>();

    internal byte[]? RawData;
    internal List<BiographyPathEntry> PathEntriesBuilder { get; set; } = new();

    internal void Freeze()
    {
        PathEntries = PathEntriesBuilder.AsReadOnly();
    }
}

/// <summary>
/// A single file path entry in the biography stream.
/// </summary>
internal sealed class BiographyPathEntry
{
    /// <summary>Delimiter value (usually 0x00000003).</summary>
    public uint Delimiter { get; set; }

    /// <summary>Raw timestamp/ID bytes before the path string.</summary>
    public byte[] TimestampData { get; set; } = Array.Empty<byte>();

    /// <summary>The file path string (WChar/UTF-16LE).</summary>
    public string PathString { get; set; } = string.Empty;
}
