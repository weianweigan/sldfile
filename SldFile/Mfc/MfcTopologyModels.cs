using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// A mapping entry linking a sketch entity (line, arc, etc.) to an extruded/cut lateral surface.
/// Serialized inside moFromSktEntSurfIdRep_c and moFromSktEnt3IntSurfIdRep_c.
/// </summary>
public sealed class SktEntSurfMapping
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
/// Sketch region model inside a sketch node (moSketchRegion_c).
/// Represents a closed planar partition in multi-contour sketches.
/// </summary>
public sealed class MfcSketchRegionModel : IMfcSerializable
{
    public const string MfcClassName = "moSketchRegion_c";

    /// <summary>Region index within the sketch (1-based).</summary>
    public ushort RegionIndex { get; set; }

    /// <summary>Number of boundary loops bounding this region.</summary>
    public ushort LoopCount { get; set; }

    /// <summary>First handle / tag prefix.</summary>
    public ushort Handle1 { get; set; }

    /// <summary>Second handle / tag suffix.</summary>
    public ushort Handle2 { get; set; }

    public byte[]? RawData { get; set; }

    public MfcSketchRegionModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort h1 = Handle1;
            ar.U16(ref h1);

            ushort lc = LoopCount;
            ar.U16(ref lc);

            ushort h2 = Handle2;
            ar.U16(ref h2);

            ushort ri = RegionIndex;
            ar.U16(ref ri);
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
            if (RawData != null && RawData.Length >= 8)
            {
                Handle1 = BitConverter.ToUInt16(RawData, 0);
                LoopCount = BitConverter.ToUInt16(RawData, 2);
                Handle2 = BitConverter.ToUInt16(RawData, 4);
                RegionIndex = BitConverter.ToUInt16(RawData, 6);
            }
        }
    }

    public override string ToString() =>
        $"moSketchRegion_c(Index={RegionIndex}, Loops={LoopCount})";
}

/// <summary>
/// Component edge reference (moCompEdge_c).
/// Points to an edge in a body/component.
/// </summary>
public sealed class MfcCompEdgeModel : IMfcSerializable
{
    public const string MfcClassName = "moCompEdge_c";

    public ushort RefEdgeId { get; set; }
    public ushort Flags { get; set; }
    public uint BodyIndex { get; set; }
    public uint EdgeIndex { get; set; }

    public byte[]? RawData { get; set; }

    public MfcCompEdgeModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort reid = RefEdgeId;
            ar.U16(ref reid);

            ushort flg = Flags;
            ar.U16(ref flg);

            uint bi = BodyIndex;
            ar.U32(ref bi);

            uint ei = EdgeIndex;
            ar.U32(ref ei);
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
            if (RawData != null && RawData.Length >= 4)
            {
                RefEdgeId = BitConverter.ToUInt16(RawData, 0);
                Flags = BitConverter.ToUInt16(RawData, 2);
            }
            if (RawData != null && RawData.Length >= 12)
            {
                BodyIndex = BitConverter.ToUInt32(RawData, 4);
                EdgeIndex = BitConverter.ToUInt32(RawData, 8);
            }
        }
    }

    public override string ToString() =>
        $"moCompEdge_c(EdgeId=0x{RefEdgeId:X4}, EdgeIndex={EdgeIndex}, BodyIndex={BodyIndex})";
}

/// <summary>
/// Edge reference container (moEdgeRef_c).
/// Contains edge selection counts, types, and topological identifiers.
/// </summary>
public sealed class MfcEdgeRefModel : IMfcSerializable
{
    public const string MfcClassName = "moEdgeRef_c";

    public uint EdgeCount { get; set; }
    public uint EdgeType { get; set; }
    public List<uint> CandidateEdgeIndices { get; set; } = new();
    public List<uint> EdgeIndices { get; set; } = new();

    public byte[]? RawData { get; set; }

    public MfcEdgeRefModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            uint ec = EdgeCount;
            ar.U32(ref ec);

            uint et = EdgeType;
            ar.U32(ref et);
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
            if (RawData != null && RawData.Length >= 12)
            {
                EdgeCount = BitConverter.ToUInt32(RawData, 0);
                EdgeType = BitConverter.ToUInt32(RawData, 8);

                for (int i = 12; i <= RawData.Length - 12; i++)
                {
                    uint typeCandidate = BitConverter.ToUInt32(RawData, i);
                    if (typeCandidate is 0x4B or 0x4C or 0x4D or 0x4E or 0x4F or 0x5E)
                    {
                        uint edgeIdx = BitConverter.ToUInt32(RawData, i + 8);
                        if (edgeIdx > 0 && edgeIdx < 5000 && !CandidateEdgeIndices.Contains(edgeIdx))
                        {
                            CandidateEdgeIndices.Add(edgeIdx);
                        }
                    }
                }
            }
        }
    }

    public override string ToString() =>
        $"moEdgeRef_c(Count={EdgeCount}, Type={EdgeType}, CandidateIndices=[{string.Join(", ", CandidateEdgeIndices)}])";
}

/// <summary>
/// Face reference plane data (moFaceRefPlnData_c).
/// Contains plane origin, normal vectors, and coordinate transformation data.
/// </summary>
public sealed class MfcFaceRefPlnModel : IMfcSerializable
{
    public const string MfcClassName = "moFaceRefPlnData_c";

    public double OriginX { get; set; }
    public double OriginY { get; set; }
    public double OriginZ { get; set; }

    public double NormalX { get; set; }
    public double NormalY { get; set; }
    public double NormalZ { get; set; }

    public double[] DirectionVector => new[] { NormalX, NormalY, NormalZ };

    public byte[]? RawData { get; set; }

    public MfcFaceRefPlnModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            double ox = OriginX; ar.F64(ref ox);
            double oy = OriginY; ar.F64(ref oy);
            double oz = OriginZ; ar.F64(ref oz);
            double nx = NormalX; ar.F64(ref nx);
            double ny = NormalY; ar.F64(ref ny);
            double nz = NormalZ; ar.F64(ref nz);
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
            if (RawData != null && RawData.Length >= 0x40)
            {
                OriginX = BitConverter.ToDouble(RawData, 0x10);
                OriginY = BitConverter.ToDouble(RawData, 0x18);
                OriginZ = BitConverter.ToDouble(RawData, 0x20);

                NormalX = BitConverter.ToDouble(RawData, 0x28);
                NormalY = BitConverter.ToDouble(RawData, 0x30);
                NormalZ = BitConverter.ToDouble(RawData, 0x38);
            }
        }
    }

    public override string ToString() =>
        $"moFaceRefPlnData_c(Origin=[{OriginX:F3}, {OriginY:F3}, {OriginZ:F3}], Normal=[{NormalX:F3}, {NormalY:F3}, {NormalZ:F3}])";
}

/// <summary>
/// Component face reference (moCompFace_c).
/// Represents an explicit selection of a part face.
/// </summary>
public sealed class MfcCompFaceModel : IMfcSerializable
{
    public const string MfcClassName = "moCompFace_c";

    public ushort RefFaceId { get; set; }
    public ushort Flags { get; set; }
    public uint BodyIndex { get; set; }
    public uint FaceIndex { get; set; }

    public byte[]? RawData { get; set; }

    public MfcCompFaceModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort rfid = RefFaceId; ar.U16(ref rfid);
            ushort flg = Flags; ar.U16(ref flg);
            uint bi = BodyIndex; ar.U32(ref bi);
            uint fi = FaceIndex; ar.U32(ref fi);
        }
        else
        {
            if (ar.Remaining >= 2) { ushort rfid = 0; ar.U16(ref rfid); RefFaceId = rfid; }
            if (ar.Remaining >= 2) { ushort flg = 0; ar.U16(ref flg); Flags = flg; }
            if (ar.Remaining >= 4) { uint bi = 0; ar.U32(ref bi); BodyIndex = bi; }
            if (ar.Remaining >= 4) { uint fi = 0; ar.U32(ref fi); FaceIndex = fi; }
        }
    }

    public override string ToString() =>
        $"moCompFace_c(FaceId=0x{RefFaceId:X4}, FaceIndex={FaceIndex}, BodyIndex={BodyIndex})";
}

/// <summary>
/// Component solid body reference (moCompSolidBody_c).
/// Represents an explicit selection of a solid body for multi-body feature scope.
/// </summary>
public sealed class MfcCompSolidBodyModel : IMfcSerializable
{
    public const string MfcClassName = "moCompSolidBody_c";

    public ushort RefBodyId { get; set; }
    public ushort Flags { get; set; }
    public uint BodyIndex { get; set; }

    public byte[]? RawData { get; set; }

    public MfcCompSolidBodyModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort rbid = RefBodyId; ar.U16(ref rbid);
            ushort flg = Flags; ar.U16(ref flg);
            uint bi = BodyIndex; ar.U32(ref bi);
        }
        else
        {
            if (ar.Remaining >= 2) { ushort rbid = 0; ar.U16(ref rbid); RefBodyId = rbid; }
            if (ar.Remaining >= 2) { ushort flg = 0; ar.U16(ref flg); Flags = flg; }
            if (ar.Remaining >= 4) { uint bi = 0; ar.U32(ref bi); BodyIndex = bi; }
        }
    }

    public override string ToString() =>
        $"moCompSolidBody_c(BodyId=0x{RefBodyId:X4}, BodyIndex={BodyIndex})";
}

/// <summary>
/// Surface ID representation (moSurfaceIdRep_c).
/// </summary>
public sealed class MfcSurfaceIdRepModel : IMfcSerializable
{
    public const string MfcClassName = "moSurfaceIdRep_c";

    public uint SurfaceTag { get; set; }
    public byte[]? RawData { get; set; }

    public MfcSurfaceIdRepModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            uint tag = SurfaceTag;
            ar.U32(ref tag);
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
            if (RawData != null && RawData.Length >= 4)
            {
                SurfaceTag = BitConverter.ToUInt32(RawData, 0);
            }
        }
    }
}

/// <summary>
/// End face 3-intersection surface ID representation (moEndFace3IntSurfIdRep_c).
/// </summary>
public sealed class MfcEndFace3IntSurfIdRepModel : IMfcSerializable
{
    public const string MfcClassName = "moEndFace3IntSurfIdRep_c";

    public uint FeatureTypeId { get; set; }
    public uint SurfaceTag { get; set; }
    public uint FaceIndex { get; set; }
    public byte[]? RawData { get; set; }

    public MfcEndFace3IntSurfIdRepModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            uint ft = FeatureTypeId; ar.U32(ref ft);
            uint st = SurfaceTag; ar.U32(ref st);
            uint fi = FaceIndex; ar.U32(ref fi);
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
        }
    }
}

/// <summary>
/// From-sketch-entity 3-intersection surface ID representation (moFromSktEnt3IntSurfIdRep_c).
/// </summary>
public sealed class MfcFromSktEnt3IntSurfIdRepModel : IMfcSerializable
{
    public const string MfcClassName = "moFromSktEnt3IntSurfIdRep_c";

    public uint FeatureTypeId { get; set; }
    public List<SktEntSurfMapping> Mappings { get; set; } = new();
    public byte[]? RawData { get; set; }

    public MfcFromSktEnt3IntSurfIdRepModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            uint ft = FeatureTypeId; ar.U32(ref ft);
            uint count = (uint)Mappings.Count; ar.Count(ref count);
            for (int i = 0; i < Mappings.Count; i++)
            {
                uint ent = Mappings[i].EntityIndex; ar.U32(ref ent);
                uint ftype = Mappings[i].FeatureTypeId; ar.U32(ref ftype);
                uint stag = Mappings[i].SurfaceTag; ar.U32(ref stag);
            }
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
            if (RawData != null && RawData.Length >= 12)
            {
                Mappings.Clear();
                for (int i = 0; i <= RawData.Length - 12; i += 4)
                {
                    uint entityCandidate = BitConverter.ToUInt32(RawData, i);
                    uint typeCandidate = BitConverter.ToUInt32(RawData, i + 4);
                    uint tagCandidate = BitConverter.ToUInt32(RawData, i + 8);

                    if (entityCandidate > 0 && entityCandidate < 5000 &&
                        (typeCandidate is 0x4F or 0x5E or 0x4B or 0x4C or 0x4D or 0x4E) &&
                        tagCandidate > 0)
                    {
                        Mappings.Add(new SktEntSurfMapping
                        {
                            EntityIndex = entityCandidate,
                            FeatureTypeId = typeCandidate,
                            SurfaceTag = tagCandidate
                        });
                        i += 8;
                    }
                }
                if (Mappings.Count > 0)
                {
                    FeatureTypeId = Mappings[0].FeatureTypeId;
                }
            }
        }
    }
}

/// <summary>
/// Fillet surface ID representation (moFilletSurfIdRep_c).
/// </summary>
public sealed class MfcFilletSurfIdRepModel : IMfcSerializable
{
    public const string MfcClassName = "moFilletSurfIdRep_c";

    public byte[]? RawData { get; set; }

    public MfcFilletSurfIdRepModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
            }
        }
        else
        {
            RawData = ar.ReadRemainingBytes();
        }
    }
}
