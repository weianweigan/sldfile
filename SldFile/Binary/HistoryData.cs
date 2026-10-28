using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// History stream: version history with date-code stamps.
/// Located at _MO_VERSION_5000/History.
/// </summary>
internal sealed class HistoryData
{
    /// <summary>Number of version entries.</summary>
    public uint VersionCount { get; internal set; }

    /// <summary>Header timestamp (FILETIME low dword).</summary>
    public uint HeaderTimestamp { get; internal set; }

    /// <summary>Post-WChar value (0x0D48=3400 for Part/Asm, 0x1388=5000 for Drawing).</summary>
    public uint PostWcharValue { get; internal set; }

    /// <summary>Version history entries.</summary>
    public IReadOnlyList<HistoryVersionEntry> Versions { get; internal set; } = Array.Empty<HistoryVersionEntry>();

    internal byte[]? RawData;
    internal List<HistoryVersionEntry> VersionsBuilder { get; set; } = new();

    internal void Freeze()
    {
        Versions = VersionsBuilder.AsReadOnly();
    }
}

/// <summary>
/// A single version history entry.
/// </summary>
internal sealed class HistoryVersionEntry
{
    /// <summary>Field count (2 for Part/Asm, 1 for Drawing).</summary>
    public uint FieldCount { get; set; }

    /// <summary>Date code values (1-2 entries).</summary>
    public uint[] DateCodes { get; set; } = Array.Empty<uint>();

    /// <summary>Type tag.</summary>
    public ushort Tag { get; set; }

    /// <summary>State flags.</summary>
    public ushort Flags { get; set; }

    /// <summary>Post-tag value.</summary>
    public uint PostTagValue { get; set; }

    /// <summary>Counter (incrementing per entry).</summary>
    public uint Counter { get; set; }

    /// <summary>Whether this is a tagged MFC entry (first record only).</summary>
    public bool IsTagged { get; set; }
}
