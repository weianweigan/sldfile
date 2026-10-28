using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Config-0 stream: per-configuration document data.
/// Located at Contents/Config-0. Contains a Header2-equivalent MFC front-end
/// followed by raw body data.
/// </summary>
internal sealed class Config0Data
{
    /// <summary>User names from the Header2-like front-end.</summary>
    public IReadOnlyList<string> Users { get; internal set; } = Array.Empty<string>();

    /// <summary>Log entries from the Header2-like front-end.</summary>
    public IReadOnlyList<string> LogEntries { get; internal set; } = Array.Empty<string>();

    /// <summary>Raw body data (unparsed portion after the MFC front-end).</summary>
    public byte[] BodyData { get; internal set; } = Array.Empty<byte>();

    internal byte[]? RawData;
    internal List<string> UsersBuilder { get; set; } = new();
    internal List<string> LogEntriesBuilder { get; set; } = new();

    internal void Freeze()
    {
        Users = UsersBuilder.AsReadOnly();
        LogEntries = LogEntriesBuilder.AsReadOnly();
    }
}
