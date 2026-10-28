using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Typed data model for the ResolvedFeatures MFC stream.
/// Contains the FeatureManager design tree: folders, reference geometry,
/// sketches, features, dimensions, and mates.
/// </summary>
internal sealed class ResolvedFeaturesData
{
    /// <summary>
    /// Stream version prefix. 0x000F = Part (.SLDPRT), 0x000B = Assembly (.SLDASM).
    /// </summary>
    public ushort StreamVersion { get; internal set; }

    /// <summary>Human-readable file type classification.</summary>
    public string FileType { get; internal set; } = "Unknown";

    /// <summary>Root-level folder/feature nodes forming the design tree.</summary>
    public IReadOnlyList<FeatureFolder> Folders { get; internal set; } = Array.Empty<FeatureFolder>();

    /// <summary>Original raw bytes for roundtrip fidelity.</summary>
    internal byte[]? RawData;

    /// <summary>Scanned MFC tag tree for offset-aware patching during write.</summary>
    internal List<MfcTag> Tags = new List<MfcTag>();

    /// <summary>Mutable builder list populated during parsing.</summary>
    internal List<FeatureFolder> FoldersBuilder = new List<FeatureFolder>();

    /// <summary>Called by the reader after parsing to freeze builder lists.</summary>
    internal void Freeze()
    {
        Folders = FoldersBuilder.AsReadOnly();
    }
}

/// <summary>
/// A single node in the FeatureManager design tree.
/// Corresponds to an MFC class with _ct (Part) or _cM (Assembly) suffix.
/// </summary>
internal sealed class FeatureFolder
{
    /// <summary>MFC class name (e.g. "moProfileFeature_ct", "moRefPlane_cM").</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Display name as shown in the SolidWorks FeatureManager (Chinese/English).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Offset in the raw stream where the MFC tag for this folder begins.</summary>
    public int TagOffset { get; set; }

    /// <summary>Offset in the raw stream where the folder data begins.</summary>
    public int DataStart { get; set; }

    /// <summary>Parsed 90-byte folder header.</summary>
    public FolderHeader Header { get; set; } = new FolderHeader();

    /// <summary>
    /// Feature-specific data for this folder, parsed from bytes beyond the FolderHeader.
    /// Populated for classes like moLengthParameter_ct that carry extra fields.
    /// Null for most container folders (moSensorFolder_ct, moSolidBodyFolder_ct, etc.).
    /// </summary>
    public IFeatureData? FeatureData { get; set; }

    /// <summary>Child folders/features nested within this folder.</summary>
    public IReadOnlyList<FeatureFolder> Children { get; set; } = Array.Empty<FeatureFolder>();

    /// <summary>
    /// Non-folder child elements with typed feature data
    /// (e.g., moEndSpec_c under moExtrusion_ct, moDefaultRefPlnData_c under moRefPlane_ct).
    /// </summary>
    public IReadOnlyList<IFeatureData> ChildFeatureData { get; set; } = Array.Empty<IFeatureData>();

    internal List<FeatureFolder> ChildrenBuilder = new List<FeatureFolder>();
    internal List<IFeatureData> ChildFeatureDataBuilder = new List<IFeatureData>();

    internal void Freeze()
    {
        Children = ChildrenBuilder.AsReadOnly();
        ChildFeatureData = ChildFeatureDataBuilder.AsReadOnly();
    }
}

/// <summary>
/// The 90-byte folder_header structure shared by all _ct / _cM folder and feature classes.
/// Immediately follows the display_name WChar string within the tag's data region.
/// </summary>
internal sealed class FolderHeader
{
    /// <summary>OLE Automation date timestamp (2.0 = default, -2.0 for reference planes).</summary>
    public double Timestamp { get; set; }

    /// <summary>Folder/feature type identifier.</summary>
    public uint TypeId { get; set; }

    /// <summary>Size indicator (varies by class).</summary>
    public uint SizeIndicator { get; set; }

    /// <summary>Known constant: 0x00000065 (101 = ASCII 'e').</summary>
    public uint MarkerE { get; set; }

    /// <summary>Varies, likely a serialized block size marker.</summary>
    public double ExtraValue { get; set; }

    /// <summary>0x00000002 = has children.</summary>
    public uint ChildFlag { get; set; }

    /// <summary>0xBA83AF6301CB5678 (Part) or 0xB92E01B401CB5678 (Assembly).</summary>
    public ulong Magic { get; set; }

    /// <summary>Sub-type identifier, typically 0x02.</summary>
    public uint SubType { get; set; }

    /// <summary>Version flag, typically 0x01.</summary>
    public uint VersionFlag { get; set; }

    /// <summary>0 or 1 — whether this folder has associated data.</summary>
    public uint HasData { get; set; }

    /// <summary>Whether the parsed header is valid (magic constant matched).</summary>
    public bool IsValid { get; internal set; }

    // ── Roundtrip preservation for padding / reserved regions ──

    internal byte[] Reserved1 = new byte[32];
    internal byte[] ZeroPad1 = new byte[8];
    internal byte[] Pattern = new byte[4];
    internal byte[] Pad2 = new byte[12];
    internal byte[] Pad3 = new byte[8];

    // ── Known constants ──

    public const ulong PartMagic = 0xBA83AF6301CB5678;
    public const ulong AssemblyMagic = 0xB92E01B401CB5678;
    public const uint KnownMarkerE = 0x00000065;
    public const uint KnownChildFlag = 0x00000002;
    public const uint KnownMarkerFfff = 0x0000FFFF;
}
