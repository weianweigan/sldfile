using System;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Configuration Manager Header stream (101 bytes).
/// Located at Contents/CMgrHdr2. Shared by Part and Assembly documents.
/// </summary>
internal sealed class CmgrHdr2Data
{
    /// <summary>Config count hint from dmConfigMgrHeader_c.</summary>
    public ushort ConfigCount { get; internal set; }

    /// <summary>Tag count from dmConfigHeader_c.</summary>
    public ushort TagCount { get; internal set; }

    /// <summary>Magic marker (0x8BA49ED8), appears twice for verification.</summary>
    public uint MagicMarker { get; internal set; }

    /// <summary>Type flag: 0x8F = Part, 0x75 = Assembly.</summary>
    public uint TypeFlag { get; internal set; }

    /// <summary>File type code: 0x0044 = Part, 0x0004 = Assembly.</summary>
    public ushort FileTypeCode { get; internal set; }

    /// <summary>Last-modified Unix timestamp.</summary>
    public uint Timestamp { get; internal set; }

    internal byte[]? RawData;

    /// <summary>Whether this is a Part document (type flag 0x8F).</summary>
    public bool IsPart => TypeFlag == 0x8F;

    /// <summary>Whether this is an Assembly document (type flag 0x75).</summary>
    public bool IsAssembly => TypeFlag == 0x75;

    /// <summary>Known magic constant.</summary>
    public const uint ExpectedMagic = 0x8BA49ED8;

    internal void Freeze() { }
}
