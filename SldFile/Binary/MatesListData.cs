using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// MatesList stream: assembly mate constraint definitions.
/// Located at Contents/Config-0-MatesList (Assembly only).
/// </summary>
internal sealed class MatesListData
{
    /// <summary>Stream version (0x0004).</summary>
    public ushort StreamVersion { get; internal set; }

    /// <summary>Mate constraint entries.</summary>
    public IReadOnlyList<MateEntry> Mates { get; internal set; } = Array.Empty<MateEntry>();

    internal byte[]? RawData;
    internal List<MateEntry> MatesBuilder { get; set; } = new();

    public const ushort ExpectedVersion = 0x0004;

    internal void Freeze()
    {
        Mates = MatesBuilder.AsReadOnly();
    }
}

/// <summary>
/// A single mate constraint entry.
/// </summary>
internal sealed class MateEntry
{
    /// <summary>MFC class name (e.g., moMateCoincidentM, moMateCoordinateM).</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Display name (e.g., "Coincident3").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Mate type identifier.</summary>
    public uint TypeId { get; set; }

    /// <summary>Mate flags.</summary>
    public ushort MateFlags { get; set; }

    /// <summary>Gap value (for coordinate mates).</summary>
    public double GapValue { get; set; }

    internal byte[]? RawData;
}
