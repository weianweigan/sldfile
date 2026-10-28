using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Lightweight display data stream (Config-0-LWDATA).
/// Located at Contents/Config-0-LWDATA.
/// </summary>
internal sealed class LwData
{
    /// <summary>Type flag (0x8F = Part, 0x75 = Assembly).</summary>
    public uint TypeFlag { get; internal set; }

    /// <summary>Raw body transform data (f64 values).</summary>
    public byte[] BodyTransformData { get; internal set; } = Array.Empty<byte>();

    /// <summary>Hatch pattern name (Part only).</summary>
    public string? HatchPattern { get; internal set; }

    /// <summary>Lightweight plane node data entries.</summary>
    public IReadOnlyList<LwPlaneNodeData> PlaneNodes { get; internal set; } = Array.Empty<LwPlaneNodeData>();

    internal byte[]? RawData;
    internal List<LwPlaneNodeData> PlaneNodesBuilder { get; set; } = new();

    /// <summary>Known type flags.</summary>
    public const uint TypeFlagPart = 0x8F;
    public const uint TypeFlagAssembly = 0x75;

    internal void Freeze()
    {
        PlaneNodes = PlaneNodesBuilder.AsReadOnly();
    }
}

/// <summary>
/// A lightweight plane reference node.
/// </summary>
internal sealed class LwPlaneNodeData
{
    public string PlaneClassName { get; set; } = string.Empty;
    public string DataClassName { get; set; } = string.Empty;
    public string FeatureIdentifier { get; set; } = string.Empty;
    public byte[] TransformMatrix { get; set; } = Array.Empty<byte>();
}
