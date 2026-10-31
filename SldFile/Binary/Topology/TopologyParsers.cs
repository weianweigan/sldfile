using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary.Topology;

/// <summary>
/// Binary parsers for SolidWorks persistent topology and selection elements.
/// </summary>
internal static class TopologyParsers
{
    public static SketchRegionData ParseSketchRegion(MfcElement element)
    {
        var data = new SketchRegionData { RawData = element.RawData };
        byte[] raw = element.RawData;
        if (raw.Length >= 8)
        {
            data.Handle1 = BitConverter.ToUInt16(raw, 0);
            data.LoopCount = BitConverter.ToUInt16(raw, 2);
            data.Handle2 = BitConverter.ToUInt16(raw, 4);
            data.RegionIndex = BitConverter.ToUInt16(raw, 6);
        }
        return data;
    }

    public static CompEdgeData ParseCompEdge(MfcElement element)
    {
        var data = new CompEdgeData { RawData = element.RawData };
        byte[] raw = element.RawData;
        if (raw.Length >= 4)
        {
            data.RefEdgeId = BitConverter.ToUInt16(raw, 0);
            data.Flags = BitConverter.ToUInt16(raw, 2);
        }
        if (raw.Length >= 12)
        {
            data.BodyIndex = BitConverter.ToUInt32(raw, 4);
            data.EdgeIndex = BitConverter.ToUInt32(raw, 8);
        }
        return data;
    }

    public static EdgeRefData ParseEdgeRef(MfcElement element)
    {
        var data = new EdgeRefData { RawData = element.RawData };
        byte[] raw = element.RawData;
        if (raw.Length >= 12)
        {
            data.EdgeCount = BitConverter.ToUInt32(raw, 0);
            data.EdgeType = BitConverter.ToUInt32(raw, 8);
        }

        // Scan for edge index entries: pattern [Type: 4B|4E|4F|5E 00 00 00] [Tag: u32] [EdgeIndex: u32]
        for (int i = 12; i <= raw.Length - 12; i++)
        {
            uint typeCandidate = BitConverter.ToUInt32(raw, i);
            if (typeCandidate is 0x4B or 0x4C or 0x4D or 0x4E or 0x4F or 0x5E)
            {
                uint edgeIdx = BitConverter.ToUInt32(raw, i + 8);
                if (edgeIdx > 0 && edgeIdx < 5000 && !data.EdgeIndices.Contains(edgeIdx))
                {
                    data.EdgeIndices.Add(edgeIdx);
                }
            }
        }

        return data;
    }

    public static FaceRefPlnData ParseFaceRefPln(MfcElement element)
    {
        var data = new FaceRefPlnData { RawData = element.RawData };
        byte[] raw = element.RawData;

        // In moFaceRefPlnData_c, origin is typically around offset 0x10 and normal around offset 0x28-0x30
        if (raw.Length >= 0x40)
        {
            data.OriginX = BitConverter.ToDouble(raw, 0x10);
            data.OriginY = BitConverter.ToDouble(raw, 0x18);
            data.OriginZ = BitConverter.ToDouble(raw, 0x20);

            data.NormalX = BitConverter.ToDouble(raw, 0x28);
            data.NormalY = BitConverter.ToDouble(raw, 0x30);
            data.NormalZ = BitConverter.ToDouble(raw, 0x38);
        }
        return data;
    }

    public static CompFaceData ParseCompFace(MfcElement element)
    {
        var data = new CompFaceData { RawData = element.RawData };
        byte[] raw = element.RawData;
        if (raw.Length >= 4)
        {
            data.RefFaceId = BitConverter.ToUInt16(raw, 0);
            data.Flags = BitConverter.ToUInt16(raw, 2);
        }
        if (raw.Length >= 12)
        {
            data.BodyIndex = BitConverter.ToUInt32(raw, 4);
            data.FaceIndex = BitConverter.ToUInt32(raw, 8);
        }
        return data;
    }

    public static CompSolidBodyData ParseCompSolidBody(MfcElement element)
    {
        var data = new CompSolidBodyData { RawData = element.RawData };
        byte[] raw = element.RawData;
        if (raw.Length >= 4)
        {
            data.RefBodyId = BitConverter.ToUInt16(raw, 0);
            data.Flags = BitConverter.ToUInt16(raw, 2);
        }
        if (raw.Length >= 12)
        {
            data.BodyIndex = BitConverter.ToUInt32(raw, 8);
        }
        return data;
    }

    public static SurfaceIdRepData ParseSurfaceIdRep(MfcElement element)
    {
        var data = new SurfaceIdRepData { RawData = element.RawData };
        if (element.RawData.Length >= 4)
        {
            data.SurfaceTag = BitConverter.ToUInt32(element.RawData, 0);
        }
        return data;
    }

    public static EndFace3IntSurfIdRepData ParseEndFace3IntSurfIdRep(MfcElement element)
    {
        var data = new EndFace3IntSurfIdRepData { RawData = element.RawData };
        byte[] raw = element.RawData;
        for (int i = 0; i <= raw.Length - 12; i++)
        {
            uint typeCandidate = BitConverter.ToUInt32(raw, i);
            if (typeCandidate is 0x4B or 0x4C or 0x4E or 0x4F or 0x56 or 0x5E)
            {
                data.FeatureTypeId = typeCandidate;
                data.SurfaceTag = BitConverter.ToUInt32(raw, i + 4);
                if (i + 12 <= raw.Length)
                {
                    data.FaceIndex = BitConverter.ToUInt32(raw, i + 8);
                }
                break;
            }
        }
        if (data.FaceIndex == 0) data.FaceIndex = 1;
        return data;
    }

    public static FromSktEnt3IntSurfIdRepData ParseFromSktEnt3IntSurfIdRep(MfcElement element)
    {
        var data = new FromSktEnt3IntSurfIdRepData { RawData = element.RawData };
        ExtractSktEntMappings(element.RawData, data.Mappings);
        if (data.Mappings.Count > 0)
        {
            data.FeatureTypeId = data.Mappings[0].FeatureTypeId;
        }
        return data;
    }

    public static FilletSurfIdRepData ParseFilletSurfIdRep(MfcElement element)
    {
        return new FilletSurfIdRepData { RawData = element.RawData };
    }

    public static void ParseEnrichedFromSktEntSurfIdRep(FromSktEntSurfIdRepData data, byte[] raw)
    {
        data.RawData = raw;
        ExtractSktEntMappings(raw, data.Mappings);
        if (data.Mappings.Count > 0)
        {
            data.FeatureTypeId = data.Mappings[0].FeatureTypeId;
        }
    }

    public static void ParseEnrichedFaceRef(FaceRefData data, byte[] raw)
    {
        data.RawData = raw;
        if (raw.Length >= 4)
        {
            data.FaceCount = BitConverter.ToUInt32(raw, 0);
        }
        if (raw.Length >= 12)
        {
            data.TopologyType = BitConverter.ToUInt32(raw, 8);
        }
        if (raw.Length >= 28)
        {
            data.BodyMagic = BitConverter.ToUInt64(raw, 20);
        }

        // Scan for face indices in topology blocks
        for (int i = 12; i <= raw.Length - 4; i += 2)
        {
            ushort candidate = BitConverter.ToUInt16(raw, i);
            if (candidate > 0 && candidate < 500 && (candidate & 0x8000) == 0)
            {
                if (!data.FaceIds.Contains(candidate))
                    data.FaceIds.Add(candidate);
            }
        }
    }

    public static void ParseEnrichedEndFaceSurfIdRep(EndFaceSurfIdRepData data, byte[] raw)
    {
        data.RawData = raw;

        // Scan for WChar name prefix: FF FE FF [len:u8]
        int offset = 0;
        while (offset + 4 <= raw.Length)
        {
            if (MfcPrimitives.IsWCharPrefix(raw, offset))
            {
                var name = MfcPrimitives.ReadWChar(raw, ref offset);
                if (!string.IsNullOrEmpty(name))
                {
                    data.SurfaceName = name!;
                    break;
                }
            }
            else
            {
                offset++;
            }
        }
    }

    public static void ParseEnrichedSketchChain(SketchChainData data, byte[] raw)
    {
        data.RawData = raw;
        if (raw == null || raw.Length < 2) return;

        ushort entityCount = BitConverter.ToUInt16(raw, 0);
        data.Count = entityCount;

        int offset = 2;
        int maxEntities = Math.Max(0, (raw.Length - offset) / 4);
        int countToRead = Math.Min((int)entityCount, maxEntities);

        data.Indices = new uint[Math.Max(4, (int)entityCount)];
        data.EntityIndices.Clear();

        for (int i = 0; i < countToRead; i++)
        {
            uint entId = BitConverter.ToUInt32(raw, offset);
            data.Indices[i] = entId;
            if (entId > 0 && !data.EntityIndices.Contains(entId))
                data.EntityIndices.Add(entId);
            offset += 4;
        }

        if (offset + 4 <= raw.Length)
        {
            uint closedFlag = BitConverter.ToUInt32(raw, offset);
            data.IsClosed = (closedFlag == 1);
            offset += 4;
        }

        // 2 bytes flags/padding, then 4 bytes sketchId
        if (offset + 6 <= raw.Length)
        {
            data.SketchId = BitConverter.ToUInt32(raw, offset + 2);
            offset += 6;
        }
    }

    private static void ExtractSktEntMappings(byte[] raw, List<SktEntSurfMapping> mappings)
    {
        if (raw == null || raw.Length < 16) return;

        // Scan for repeating pattern: [TypeId: 4 bytes (0x4B, 0x4C, 0x4E, 0x4F, 0x5E)] [Tag: 4 bytes] [EntityId: 4 bytes]
        for (int i = 0; i <= raw.Length - 12; i++)
        {
            uint typeCandidate = BitConverter.ToUInt32(raw, i);
            if (typeCandidate is 0x4B or 0x4C or 0x4D or 0x4E or 0x4F or 0x56 or 0x5E)
            {
                uint surfaceTag = BitConverter.ToUInt32(raw, i + 4);
                uint entId = BitConverter.ToUInt32(raw, i + 8);
                if (entId > 0 && entId < 5000)
                {
                    bool exists = false;
                    for (int m = 0; m < mappings.Count; m++)
                    {
                        if (mappings[m].EntityIndex == entId && mappings[m].SurfaceTag == surfaceTag)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        mappings.Add(new SktEntSurfMapping
                        {
                            FeatureTypeId = typeCandidate,
                            SurfaceTag = surfaceTag,
                            EntityIndex = entId
                        });
                    }
                }
            }
        }
    }
}
