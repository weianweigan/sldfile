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

    /// <summary>
    /// Direct reference-wrapper children observed in the serialized mate object
    /// (e.g., moPlaneRef_w / moPointRef_w). Only structurally scanned class names;
    /// the referenced topology is not decoded.
    /// </summary>
    public IReadOnlyList<MateReferenceEntry> References { get; internal set; } = Array.Empty<MateReferenceEntry>();

    /// <summary>
    /// Structural decode of the mate object (header candidates, reference chains,
    /// deferred 2026 texts). Mate type/alignment/parameters remain unresolved.
    /// Null when the layout is not recognized.
    /// </summary>
    public MateStructureData? Structure { get; internal set; }

    internal byte[]? RawData;
}

/// <summary>
/// One entity-reference wrapper attached to a serialized mate.
/// Observed layouts (SolidWorks 2012 and 2026 assemblies):
/// moPlaneRef_w -> moCompFace_c, moPointRef_w -> moCompSketchEntHandle_c.
/// </summary>
internal sealed class MateReferenceEntry
{
    /// <summary>Wrapper MFC class name (e.g., moPlaneRef_w).</summary>
    public string WrapperClassName { get; set; } = string.Empty;

    /// <summary>Direct MFC child class names of the wrapper (raw, undecoded).</summary>
    public IReadOnlyList<string> EntityClassNames { get; set; } = Array.Empty<string>();
}
