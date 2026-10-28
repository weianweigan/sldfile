using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Typed data model for the Header2 MFC stream.
/// Contains document metadata: users, revision logs, and external file references.
/// </summary>
internal sealed class Header2Data
{
    /// <summary>User names from su_CStringArray (first entry is the creator).</summary>
    public IReadOnlyList<string> Users { get; internal set; } = Array.Empty<string>();

    /// <summary>Child count hint from suObList.</summary>
    public int ObjectCount { get; internal set; }

    /// <summary>Document creation timestamp from moStamp_c (also the timestamp of the first log record).</summary>
    public uint BaseTimestamp { get; internal set; }

    /// <summary>Revision log records from moLogs_c.</summary>
    public IReadOnlyList<LogEntry> LogEntries { get; internal set; } = Array.Empty<LogEntry>();

    /// <summary>External file references from moExtObject_c blocks.</summary>
    public IReadOnlyList<ExternalReference> ExternalReferences { get; internal set; } = Array.Empty<ExternalReference>();

    /// <summary>Original raw bytes for roundtrip fidelity.</summary>
    internal byte[]? RawData;

    /// <summary>Scanned MFC tag tree for offset-aware patching during write.</summary>
    internal List<MfcTag> Tags = new List<MfcTag>();

    internal List<string> UsersBuilder = new List<string>();
    internal List<LogEntry> LogEntriesBuilder = new List<LogEntry>();
    internal List<ExternalReference> ExternalReferencesBuilder = new List<ExternalReference>();

    internal void Freeze()
    {
        Users = UsersBuilder.AsReadOnly();
        LogEntries = LogEntriesBuilder.AsReadOnly();
        ExternalReferences = ExternalReferencesBuilder.AsReadOnly();
    }
}

internal enum LogEventType
{
    Created,
    Modified,
    Deleted
}

/// <summary>
/// A single revision history entry parsed from moLogs_c.
///
/// Record structure (action-first, from hex analysis):
///   Action (WChar) — "Created" / "Modified" / "Deleted"
///   [Branch A]: record_id (u32) + feature_name (WChar) + composite (u32: tag|flags&lt;&lt;16)
///   Handle (u64)
///   [Non-terminal]: Timestamp (u32)
///   [Branch B, optional]: Value (WChar)
///
/// Terminal detection: composite field == base_timestamp → no trailing Timestamp.
/// </summary>
internal sealed class LogEntry
{
    public LogEventType EventType { get; set; }
    public string Action { get; set; } = string.Empty;

    /// <summary>Feature record ID (Branch A only).</summary>
    public uint? RecordId { get; set; }

    /// <summary>Feature display name (Branch A only).</summary>
    public string? FeatureName { get; set; }

    /// <summary>Feature type tag, normally 32776 / 0x8008 (Branch A only).</summary>
    public ushort? Tag { get; set; }

    /// <summary>Feature state flags (Branch A only).</summary>
    public ushort? Flags { get; set; }

    /// <summary>Composite u32 of tag and flags: (tag) | (flags &lt;&lt; 16).</summary>
    public uint CompositeField { get; set; }

    /// <summary>Index into <see cref="Header2Data.Users"/>, identifying the user who performed this action.</summary>
    public uint Handle { get; set; }

    /// <summary>Resolved user name from <see cref="Handle"/> lookup into <see cref="Header2Data.Users"/>.</summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Unix epoch timestamp. Null for terminal records (the last entry),
    /// which use <see cref="Header2Data.BaseTimestamp"/> instead.
    /// </summary>
    public uint? Timestamp { get; set; }

    /// <summary>Whether this is the terminal (last) record that uses BaseTimestamp.</summary>
    public bool IsTerminal { get; set; }

    /// <summary>Optional value WChar for Branch B records.</summary>
    public string? Value { get; set; }
}

/// <summary>
/// An external file reference parsed from moExtObject_c.
/// References a part or sub-assembly file.
/// </summary>
internal sealed class ExternalReference
{
    /// <summary>Full file path from the nested moCStringHandle_c.</summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>Short display name (the file name portion).</summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// Variable-length proprietary metadata between the name separator and the
    /// next structural boundary (MFC tag or BBox). Content is currently unknown.
    /// Preserved for roundtrip fidelity.
    /// </summary>
    public byte[] ProprietaryMeta { get; set; } = Array.Empty<byte>();
}
