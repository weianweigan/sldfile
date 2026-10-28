using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// User Units Table stream data: unit system configuration.
/// Located at Contents/User Units Table__ZLB (ZLB-compressed MFC stream).
/// </summary>
internal sealed class UserUnitsTableData
{
    /// <summary>Number of unit types (typically 16), from moUnitsTable_c.</summary>
    public int UnitCount { get; internal set; }

    /// <summary>Parsed unit type entries.</summary>
    public IReadOnlyList<UserUnitTypeEntry> UnitTypes { get; internal set; }
        = Array.Empty<UserUnitTypeEntry>();

    internal byte[]? RawData;
}

/// <summary>
/// A single unit type entry (e.g., moLengthUserUnits_c).
/// </summary>
internal sealed class UserUnitTypeEntry
{
    /// <summary>MFC class name of this unit type.</summary>
    public string ClassName { get; internal set; } = string.Empty;

    /// <summary>Human-readable unit type name (derived from class name).</summary>
    public string UnitTypeName { get; internal set; } = string.Empty;

    /// <summary>Number of sub-unit entries.</summary>
    public int SubUnitCount { get; internal set; }

    /// <summary>Sub-unit definitions (index + conversion factor).</summary>
    public IReadOnlyList<UserSubUnit> SubUnits { get; internal set; }
        = Array.Empty<UserSubUnit>();

    /// <summary>Tail field value at end of data block.</summary>
    public uint TailField { get; internal set; }

    /// <summary>Raw data block for round-tripping.</summary>
    public byte[] RawBlock { get; internal set; } = Array.Empty<byte>();
}

/// <summary>
/// A single sub-unit within a unit type (e.g., meters within length).
/// </summary>
internal sealed class UserSubUnit
{
    public uint Index { get; set; }
    public uint Flags { get; set; }
    public double ConversionFactor { get; set; }
}
