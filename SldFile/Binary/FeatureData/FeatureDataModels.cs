using System;
using System.Collections.Generic;
using SldFile;

namespace SldFile.Binary;

// ═══════════════════════════════════════════════════════════════════════════
// Folder classes with extra data beyond the 90-byte FolderHeader
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Length parameter data (D1, D2, ...) for features like Boss-Extrude.
/// Appears as a _ct folder class with extra fields after the FolderHeader.
/// </summary>
internal sealed class LengthParameterData : IFeatureData
{
    public string ClassName => "moLengthParameter_ct";
    public uint TypeId { get; internal set; }

    public string Name { get; set; } = string.Empty;
    public double ParamValue { get; set; }
    public double Value => ParamValue;
    public uint Flags { get; set; }
    public uint HandleCount { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] UnparsedTail = Array.Empty<byte>();
}

// ═══════════════════════════════════════════════════════════════════════════
// Reference geometry
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Default reference plane definition with a full 3D transform matrix.
/// Child of moRefPlane_ct / moRefPlane_cM.
/// </summary>
internal sealed class DefaultRefPlaneData : IFeatureData
{
    public string ClassName => "moDefaultRefPlnData_c";
    public uint TypeId => 0;

    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double[] XAxis { get; set; } = new double[3];
    public double[] YAxis { get; set; } = new double[3];
    public double[] ZAxis { get; set; } = new double[3];
    public double[] Translation { get; set; } = new double[3];
    public double[] TransformExtra { get; set; } = new double[2];

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] UnparsedHead = Array.Empty<byte>();
    internal byte[] UnparsedTail = Array.Empty<byte>();
}

/// <summary>
/// Component reference plane — references a plane by GUID.
/// Child of moOriginProfileFeature_ct/_cM, moProfileFeature_ct/_cM, moMateGroup_cM.
/// </summary>
internal sealed class CompRefPlaneData : IFeatureData
{
    public string ClassName => "moCompRefPlane_c";
    public uint TypeId => 0;

    public ushort RefPlaneId { get; set; }
    public ushort Flags { get; set; }
    public string Guid { get; set; } = string.Empty;

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] UnparsedTail = Array.Empty<byte>();
}

// ═══════════════════════════════════════════════════════════════════════════
// Sketch & sketch entities
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Sketch container holding entity count, flags, and sketch transform data.
/// </summary>
internal sealed class SketchData : IFeatureData
{
    public string ClassName => "sgSketch";
    public uint TypeId => 0;

    public uint EntityCount { get; set; }
    public uint Flags1 { get; set; }
    public uint Flags2 { get; set; }
    public uint ArrayHeader { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] SketchBody = Array.Empty<byte>();
}

/// <summary>Sketch point handle.</summary>
internal sealed class PointHandleData : IFeatureData
{
    public string ClassName => "sgPointHandle";
    public uint TypeId => 0;

    public uint RefId { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] HandleData = Array.Empty<byte>();
}

/// <summary>Sketch line handle.</summary>
internal sealed class LineHandleData : IFeatureData
{
    public string ClassName => "sgLineHandle";
    public uint TypeId => 0;

    public uint Index { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] HandleData = Array.Empty<byte>();
}

/// <summary>Sketch arc handle.</summary>
internal sealed class ArcHandleData : IFeatureData
{
    public string ClassName => "sgArcHandle";
    public uint TypeId => 0;

    public uint Index { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] HandleData = Array.Empty<byte>();
}

/// <summary>Sketch circle dimension.</summary>
internal sealed class CircleDimData : IFeatureData
{
    public string ClassName => "sgCircleDim";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Point-to-point distance constraint in a sketch.</summary>
internal sealed class PntPntDistData : IFeatureData
{
    public string ClassName => "sgPntPntDist";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>External sketch entity reference (empty container).</summary>
internal sealed class ExtEntData : IFeatureData
{
    public string ClassName => "sgExtEnt_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Sketch external reference wrapper (empty container).</summary>
internal sealed class SketchExtRefData : IFeatureData
{
    public string ClassName => "moSketchExtRef_w";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Component sketch entity handle with face and topology references.</summary>
internal sealed class CompSketchEntHandleData : IFeatureData
{
    public string ClassName => "moCompSketchEntHandle_c";
    public uint TypeId => 0;

    public uint EntityIndex { get; set; }
    public uint FaceIndex { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] TopologyRef = Array.Empty<byte>();
}

/// <summary>Generic sketch entity handle.</summary>
internal sealed class EntHandleData : IFeatureData
{
    public string ClassName => "sgEntHandle";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

// ═══════════════════════════════════════════════════════════════════════════
// Sketch chain
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Sketch chain — groups connected sketch entities.
/// </summary>
internal sealed class SketchChainData : IFeatureData
{
    public string ClassName => "moSketchChain_c";
    public uint TypeId => 0;

    public uint[] Indices { get; set; } = new uint[4];
    public uint Flag { get; set; }
    public uint Count { get; set; }
    public bool IsClosed { get; set; } = true;
    public uint SketchId { get; set; }
    public List<uint> EntityIndices { get; set; } = new();

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] UnparsedTail = Array.Empty<byte>();
}

// ═══════════════════════════════════════════════════════════════════════════
// Dimensions
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Display distance dimension (linear dimension shown in graphics).</summary>
internal sealed class DisplayDistanceDimData : IFeatureData
{
    public string ClassName => "moDisplayDistanceDim_c";
    public uint TypeId => 0;

    public double DimValue { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] DimHeader = Array.Empty<byte>();   // 18 bytes
    internal byte[] TolData = Array.Empty<byte>();
}

/// <summary>Display radial dimension.</summary>
internal sealed class DisplayRadialDimData : IFeatureData
{
    public string ClassName => "moDisplayRadialDim_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Sketch dimension handle value (G2 generation).</summary>
internal sealed class SkDimHandleValG2Data : IFeatureData
{
    public string ClassName => "moSkDimHandleValG2_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Sketch radial dimension handle.</summary>
internal sealed class SkDimHandleRadialData : IFeatureData
{
    public string ClassName => "moSkDimHandleRadial_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Feature dimension handle.</summary>
internal sealed class FeatureDimHandleData : IFeatureData
{
    public string ClassName => "moFeatureDimHandle_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Parallel plane distance dimension (large, ~631 bytes with witness lines).</summary>
internal sealed class ParallelPlaneDistanceDimData : IFeatureData
{
    public string ClassName => "ParallelPlaneDistanceDim_c";
    public uint TypeId => 0;

    public double DimValue { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Favorite handle — small 6-byte attachment used by dimensions and features.</summary>
internal sealed class FavoriteHandleData : IFeatureData
{
    public string ClassName => "moFavoriteHandle_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

// ═══════════════════════════════════════════════════════════════════════════
// Extrusion sub-objects
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Per-body chooser data (2 bytes).</summary>
internal sealed class PerBodyChooserData : IFeatureData
{
    public string ClassName => "moPerBodyChooserData_c";
    public uint TypeId => 0;

    public ushort ChooserFlags { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Face reference with indexed face list.</summary>
internal sealed class FaceRefData : IFeatureData
{
    public string ClassName => "moFaceRef_c";
    public uint TypeId => 0;

    public uint FaceCount { get; set; }
    public uint TopologyType { get; set; }
    public ulong BodyMagic { get; set; }
    public List<uint> FaceIds { get; set; } = new();

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] TopologyData = Array.Empty<byte>();
}

/// <summary>End face surface ID representation (small container, 2 bytes).</summary>
internal sealed class EndFaceSurfIdRepData : IFeatureData
{
    public string ClassName => "moEndFaceSurfIdRep_c";
    public uint TypeId => 0;

    public string SurfaceName { get; set; } = string.Empty;
    public uint SurfaceId { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Face reference container with topology path (~66 bytes).</summary>
internal sealed class FrData : IFeatureData
{
    public string ClassName => "moFR_c";
    public uint TypeId => 0;

    public string PathName { get; set; } = string.Empty;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>From-sketch-entity surface ID representation.</summary>
internal sealed class FromSktEntSurfIdRepData : IFeatureData
{
    public string ClassName => "moFromSktEntSurfIdRep_c";
    public uint TypeId => 0;

    public uint FeatureTypeId { get; set; }
    public List<SldFile.Binary.Topology.SktEntSurfMapping> Mappings { get; set; } = new();

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>
/// Bounding box center data. Contains a 3D center point and additional data.
/// Child of moExtrusion_ct.
/// </summary>
internal sealed class BBoxCenterData : IFeatureData
{
    public string ClassName => "moBBoxCenterData_c";
    public uint TypeId => 0;

    public double CenterX { get; set; }
    public double CenterY { get; set; }
    public double CenterZ { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] Extra = Array.Empty<byte>();
}

/// <summary>
/// End specification for an extrusion feature.
/// Defines how the extrusion terminates (blind, through-all, up-to-surface, etc.).
/// </summary>
internal sealed class EndSpecData : IFeatureData
{
    public string ClassName => "moEndSpec_c";
    public uint TypeId => 0;

    public uint EndType { get; set; }
    public uint EndCondition { get; set; }
    public double EndDepth { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
    internal byte[] UnparsedTail = Array.Empty<byte>();
}

/// <summary>
/// From-end specification — defines the start condition of an extrusion
/// (typically an offset from the sketch plane).
/// </summary>
internal sealed class FromEndSpecData : IFeatureData
{
    public string ClassName => "moFromEndSpec_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

// ═══════════════════════════════════════════════════════════════════════════
// .ske-specific sketch classes
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Point-to-line distance dimension in a sketch.</summary>
internal sealed class PntLineDistData : IFeatureData
{
    public string ClassName => "sgPntLineDist";
    public uint TypeId => 0;

    public uint Value { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Line-to-line distance dimension in a sketch.</summary>
internal sealed class LLDistanceData : IFeatureData
{
    public string ClassName => "sgLLDist";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Angular dimension in a sketch.</summary>
internal sealed class AngularDimData : IFeatureData
{
    public string ClassName => "sgAnglDim";
    public uint TypeId => 0;

    public uint Value { get; set; }

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Node name — WChar display label for a dimension (e.g. "D1", "Hole Ø").</summary>
internal sealed class NodeNameData : IFeatureData
{
    public string ClassName => "moNodeName_c";
    public uint TypeId => 0;

    public string Name { get; set; } = string.Empty;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Length parameter (_c variant, used in .ske files). WChar name.</summary>
internal sealed class LengthParameterCData : IFeatureData
{
    public string ClassName => "moLengthParameter_c";
    public uint TypeId => 0;

    public string Name { get; set; } = string.Empty;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Angle parameter (_c variant, used in .ske files). WChar name.</summary>
internal sealed class AngleParameterCData : IFeatureData
{
    public string ClassName => "moAngleParameter_c";
    public uint TypeId => 0;

    public string Name { get; set; } = string.Empty;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>Display angular dimension.</summary>
internal sealed class DisplayAngularDimData : IFeatureData
{
    public string ClassName => "moDisplayAngularDim_c";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}

/// <summary>MFC CObList terminator (2 bytes).</summary>
internal sealed class ObListData : IFeatureData
{
    public string ClassName => "suObList";
    public uint TypeId => 0;

    internal byte[] RawData = Array.Empty<byte>();
}
