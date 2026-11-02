using System;
using System.Collections.Generic;

namespace SldFile.Binary.Topology;

/// <summary>
/// A mapping entry linking a sketch entity (line, arc, etc.) to an extruded/cut lateral surface.
/// Serialized inside moFromSktEntSurfIdRep_c and moFromSktEnt3IntSurfIdRep_c.
/// </summary>
internal sealed class SktEntSurfMapping
{
    /// <summary>Index of the referenced sketch entity (1-based).</summary>
    public uint EntityIndex { get; set; }

    /// <summary>Feature type identifier (e.g. 0x4F for Extrude, 0x5E for SheetMetalBaseFlange).</summary>
    public uint FeatureTypeId { get; set; }

    /// <summary>Surface tag or topological ID assigned to this face.</summary>
    public uint SurfaceTag { get; set; }

    public override string ToString() =>
        $"SktEntSurfMapping(Entity={EntityIndex}, FeatureType=0x{FeatureTypeId:X2}, SurfaceTag=0x{SurfaceTag:X8})";
}

/// <summary>
/// Sketch region data inside a sketch node (moSketchRegion_c).
/// Represents a closed planar partition in multi-contour sketches.
/// </summary>
internal sealed class SketchRegionData : IFeatureData
{
    public string ClassName => "moSketchRegion_c";
    public uint TypeId => 0;

    /// <summary>Region index within the sketch (1-based).</summary>
    public ushort RegionIndex { get; set; }

    /// <summary>Number of boundary loops bounding this region.</summary>
    public ushort LoopCount { get; set; }

    /// <summary>First handle / tag prefix.</summary>
    public ushort Handle1 { get; set; }

    /// <summary>Second handle / tag suffix.</summary>
    public ushort Handle2 { get; set; }

    internal byte[] RawData = Array.Empty<byte>();

    public override string ToString() =>
        $"moSketchRegion_c(Index={RegionIndex}, Loops={LoopCount})";
}

/// <summary>
/// Component edge reference (moCompEdge_c). 59 bytes.
/// Points to an edge in a body/component.
/// </summary>
internal sealed class CompEdgeData : IFeatureData
{
    public string ClassName => "moCompEdge_c";
    public uint TypeId => 0;

    public ushort RefEdgeId { get; set; }
    public ushort Flags { get; set; }
    public uint BodyIndex { get; set; }
    public uint EdgeIndex { get; set; }

    internal byte[] RawData = Array.Empty<byte>();

    public override string ToString() =>
        $"moCompEdge_c(EdgeId=0x{RefEdgeId:X4}, EdgeIndex={EdgeIndex}, BodyIndex={BodyIndex})";
}

/// <summary>
/// Edge reference container (moEdgeRef_c).
/// Contains edge selection counts, types, and topological identifiers.
/// </summary>
internal sealed class EdgeRefData : IFeatureData
{
    public string ClassName => "moEdgeRef_c";
    public uint TypeId => 0;

    /// <summary>Number of edges in this reference.</summary>
    public uint EdgeCount { get; set; }

    /// <summary>Edge selection/topology type (typically 0x04).</summary>
    public uint EdgeType { get; set; }

    /// <summary>Referenced edge indices extracted from topology entries.</summary>
    public List<uint> EdgeIndices { get; set; } = new();

    internal byte[] RawData = Array.Empty<byte>();

    public override string ToString() =>
        $"moEdgeRef_c(Count={EdgeCount}, Type={EdgeType}, Indices=[{string.Join(", ", EdgeIndices)}])";
}

/// <summary>
/// Face reference plane data (moFaceRefPlnData_c, ~222 bytes).
/// Contains plane origin, normal vectors, and 3D coordinate transformation matrix.
/// </summary>
internal sealed class FaceRefPlnData : IFeatureData
{
    public string ClassName => "moFaceRefPlnData_c";
    public uint TypeId => 0;

    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }

    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; }

    public double[] DirectionVector => new[] { NormalX, NormalY, NormalZ };

    internal byte[] RawData = Array.Empty<byte>();

    public override string ToString() =>
        $"moFaceRefPlnData_c(Origin=[{OriginX:F3}, {OriginY:F3}, {OriginZ:F3}], Normal=[{NormalX:F3}, {NormalY:F3}, {NormalZ:F3}])";
}

/// <summary>
/// Component face reference (moCompFace_c, 59 bytes).
/// Represents an explicit selection of a part face.
/// </summary>
internal sealed class CompFaceData : IFeatureData
{
    public string ClassName => "moCompFace_c";
    public uint TypeId => 0;

    public ushort RefFaceId { get; set; }
    public ushort Flags { get; set; }
    public uint BodyIndex { get; set; }
    public uint FaceIndex { get; set; }

    internal byte[] RawData = Array.Empty<byte>();

    public override string ToString() =>
        $"moCompFace_c(FaceId=0x{RefFaceId:X4}, FaceIndex={FaceIndex}, BodyIndex={BodyIndex})";
}

/// <summary>
/// Component solid body reference (moCompSolidBody_c, 59 bytes).
/// Represents an explicit selection of a solid body for multi-body feature scope.
/// </summary>
internal sealed class CompSolidBodyData : IFeatureData
{
    public string ClassName => "moCompSolidBody_c";
    public uint TypeId => 0;

    public ushort RefBodyId { get; set; }
    public ushort Flags { get; set; }
    public uint BodyIndex { get; set; }

    internal byte[] RawData = Array.Empty<byte>();

    public override string ToString() =>
        $"moCompSolidBody_c(BodyId=0x{RefBodyId:X4}, BodyIndex={BodyIndex})";
}

/// <summary>
/// Surface ID representation (moSurfaceIdRep_c).
/// </summary>
internal sealed class SurfaceIdRepData : IFeatureData
{
    public string ClassName => "moSurfaceIdRep_c";
    public uint TypeId => 0;

    public uint SurfaceTag { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>
/// End face 3-intersection surface ID representation (moEndFace3IntSurfIdRep_c).
/// </summary>
internal sealed class EndFace3IntSurfIdRepData : IFeatureData
{
    public string ClassName => "moEndFace3IntSurfIdRep_c";
    public uint TypeId => 0;

    public uint FeatureTypeId { get; set; }
    public uint SurfaceTag { get; set; }
    public uint FaceIndex { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>
/// From-sketch-entity 3-intersection surface ID representation (moFromSktEnt3IntSurfIdRep_c).
/// </summary>
internal sealed class FromSktEnt3IntSurfIdRepData : IFeatureData
{
    public string ClassName => "moFromSktEnt3IntSurfIdRep_c";
    public uint TypeId => 0;

    public uint FeatureTypeId { get; set; }
    public List<SktEntSurfMapping> Mappings { get; set; } = new();

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>
/// Fillet surface ID representation (moFilletSurfIdRep_c).
/// </summary>
internal sealed class FilletSurfIdRepData : IFeatureData
{
    public string ClassName => "moFilletSurfIdRep_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}
