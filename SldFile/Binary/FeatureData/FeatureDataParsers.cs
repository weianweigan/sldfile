using System;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Static Parse methods for each known feature data class.
/// Each method reads from an <see cref="MfcElement"/>'s RawData and returns
/// a typed data object, preserving unparsed regions for roundtrip fidelity.
/// </summary>
internal static class FeatureDataParsers
{
    // ═══════════════════════════════════════════════════════════════════
    // Folder classes with extra data beyond FolderHeader
    // ═══════════════════════════════════════════════════════════════════

    internal static LengthParameterData? ParseLengthParameter(MfcElement element)
    {
        var data = new LengthParameterData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int len = raw.Length;
        int offset = 0;

        // Skip display_name WChar + 90-byte folder_header
        MfcPrimitives.ReadWChar(raw, ref offset);
        if (offset + 90 > len) return data;
        offset += 90;

        if (offset + 8 <= len)
            data.ParamValue = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        if (offset + 4 <= len)
            data.Flags = BitConverter.ToUInt32(raw, offset);
        offset = Math.Min(offset + 4, len);

        if (offset + 4 <= len)
            data.HandleCount = BitConverter.ToUInt32(raw, offset);
        offset = Math.Min(offset + 4, len);

        if (offset < len)
            data.UnparsedTail = MfcPrimitives.CopyRange(raw, offset, len);

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reference geometry
    // ═══════════════════════════════════════════════════════════════════

    internal static DefaultRefPlaneData? ParseDefaultRefPlane(MfcElement element)
    {
        var data = new DefaultRefPlaneData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int len = raw.Length;
        int offset = 0;

        // Skip padding: 40 bytes
        data.UnparsedHead = MfcPrimitives.CopyRange(raw, offset, Math.Min(offset + 40, len));
        offset = Math.Min(offset + 40, len);

        if (offset + 8 <= len)
            data.OriginX = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        // Skip padding2: 24 bytes
        offset = Math.Min(offset + 24, len);

        if (offset + 8 <= len)
            data.OriginY = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        // Read axis vectors, translation, and extra — each block is optional
        ReadF64Array(raw, ref offset, data.XAxis);
        ReadF64Array(raw, ref offset, data.YAxis);
        ReadF64Array(raw, ref offset, data.ZAxis);
        ReadF64Array(raw, ref offset, data.Translation);
        ReadF64Array(raw, ref offset, data.TransformExtra);
        offset = Math.Min(offset, len);

        if (offset < len)
            data.UnparsedTail = MfcPrimitives.CopyRange(raw, offset, len);

        return data;
    }

    private static void ReadF64Array(byte[] raw, ref int offset, double[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (offset + 8 <= raw.Length)
            {
                arr[i] = BitConverter.ToDouble(raw, offset);
                offset += 8;
            }
            else
            {
                break;
            }
        }
    }

    internal static CompRefPlaneData? ParseCompRefPlane(MfcElement element)
    {
        var data = new CompRefPlaneData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int len = raw.Length;
        int offset = 0;

        if (offset + 2 <= len)
            data.RefPlaneId = BitConverter.ToUInt16(raw, offset);
        offset = Math.Min(offset + 2, len);

        if (offset + 2 <= len)
            data.Flags = BitConverter.ToUInt16(raw, offset);
        offset = Math.Min(offset + 2, len);

        // Skip 28 zero bytes
        offset = Math.Min(offset + 28, len);

        if (offset + 16 <= len)
        {
            int saved = offset;
            data.Guid = MfcPrimitives.ReadGuid(raw, ref offset);
            offset = saved + 16;
        }

        if (offset < len)
            data.UnparsedTail = MfcPrimitives.CopyRange(raw, offset, len);

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sketch & sketch entities
    // ═══════════════════════════════════════════════════════════════════

    internal static SketchData? ParseSketch(MfcElement element)
    {
        var data = new SketchData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.EntityCount = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset + 4 <= raw.Length)
            data.Flags1 = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset + 4 <= raw.Length)
            data.Flags2 = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset + 4 <= raw.Length)
            data.ArrayHeader = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset < raw.Length)
            data.SketchBody = MfcPrimitives.CopyRange(raw, offset, raw.Length);

        return data;
    }

    internal static PointHandleData? ParsePointHandle(MfcElement element)
    {
        var data = new PointHandleData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.RefId = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset < raw.Length)
            data.HandleData = MfcPrimitives.CopyRange(raw, offset, raw.Length);

        return data;
    }

    internal static LineHandleData? ParseLineHandle(MfcElement element)
    {
        var data = new LineHandleData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.Index = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset < raw.Length)
            data.HandleData = MfcPrimitives.CopyRange(raw, offset, raw.Length);

        return data;
    }

    internal static ArcHandleData? ParseArcHandle(MfcElement element)
    {
        var data = new ArcHandleData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.Index = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset < raw.Length)
            data.HandleData = MfcPrimitives.CopyRange(raw, offset, raw.Length);

        return data;
    }

    internal static CircleDimData? ParseCircleDim(MfcElement element)
    {
        return new CircleDimData { RawData = element.RawData };
    }

    internal static PntPntDistData? ParsePntPntDist(MfcElement element)
    {
        return new PntPntDistData { RawData = element.RawData };
    }

    internal static ExtEntData? ParseExtEnt(MfcElement element)
    {
        return new ExtEntData { RawData = element.RawData };
    }

    internal static SketchExtRefData? ParseSketchExtRef(MfcElement element)
    {
        return new SketchExtRefData { RawData = element.RawData };
    }

    internal static CompSketchEntHandleData? ParseCompSketchEntHandle(MfcElement element)
    {
        var data = new CompSketchEntHandleData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.EntityIndex = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset + 4 <= raw.Length)
            data.FaceIndex = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset < raw.Length)
            data.TopologyRef = MfcPrimitives.CopyRange(raw, offset, raw.Length);

        return data;
    }

    internal static EntHandleData? ParseEntHandle(MfcElement element)
    {
        return new EntHandleData { RawData = element.RawData };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sketch chain
    // ═══════════════════════════════════════════════════════════════════

    internal static SketchChainData? ParseSketchChain(MfcElement element)
    {
        var data = new SketchChainData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int len = raw.Length;
        int offset = 0;

        for (int i = 0; i < 4 && offset + 4 <= len; i++)
        {
            data.Indices[i] = BitConverter.ToUInt32(raw, offset);
            offset += 4;
        }

        // Search for the flag marker (0x00000004) to locate flag + count
        int flagOffset = -1;
        for (int i = offset; i + 8 <= len; i += 4)
        {
            uint candidate = BitConverter.ToUInt32(raw, i);
            if (candidate == 0x00000004)
            {
                flagOffset = i;
                break;
            }
        }

        if (flagOffset > 0)
        {
            data.Flag = 0x00000004;
            if (flagOffset + 8 <= len)
            {
                data.Count = BitConverter.ToUInt32(raw, flagOffset + 4);
                offset = flagOffset + 8;
            }
            else
            {
                offset = flagOffset + 4;
            }
        }
        else
        {
            // No flag found — just advance past reasonable padding
            offset = Math.Min(offset + 24, len);
        }

        if (offset < len)
            data.UnparsedTail = MfcPrimitives.CopyRange(raw, offset, len);

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dimensions
    // ═══════════════════════════════════════════════════════════════════

    internal static DisplayDistanceDimData? ParseDisplayDistanceDim(MfcElement element)
    {
        var data = new DisplayDistanceDimData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int len = raw.Length;
        int offset = 0;

        // 18-byte dimension header
        if (offset + 18 <= len)
            data.DimHeader = MfcPrimitives.CopyRange(raw, offset, offset + 18);
        offset = Math.Min(offset + 18, len);

        // f64 dimension value
        if (offset + 8 <= len)
            data.DimValue = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        // Tolerance data
        if (offset < len)
            data.TolData = MfcPrimitives.CopyRange(raw, offset, len);

        return data;
    }

    internal static DisplayRadialDimData? ParseDisplayRadialDim(MfcElement element)
    {
        return new DisplayRadialDimData { RawData = element.RawData };
    }

    internal static SkDimHandleValG2Data? ParseSkDimHandleValG2(MfcElement element)
    {
        return new SkDimHandleValG2Data { RawData = element.RawData };
    }

    internal static SkDimHandleRadialData? ParseSkDimHandleRadial(MfcElement element)
    {
        return new SkDimHandleRadialData { RawData = element.RawData };
    }

    internal static FeatureDimHandleData? ParseFeatureDimHandle(MfcElement element)
    {
        return new FeatureDimHandleData { RawData = element.RawData };
    }

    internal static ParallelPlaneDistanceDimData? ParseParallelPlaneDistanceDim(MfcElement element)
    {
        var data = new ParallelPlaneDistanceDimData { RawData = element.RawData };
        byte[] raw = element.RawData;
        if (raw.Length >= 0x45)
        {
            data.DimValue = BitConverter.ToDouble(raw, 0x3D);
        }
        return data;
    }

    internal static FavoriteHandleData? ParseFavoriteHandle(MfcElement element)
    {
        return new FavoriteHandleData { RawData = element.RawData };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Extrusion sub-objects
    // ═══════════════════════════════════════════════════════════════════

    internal static PerBodyChooserData? ParsePerBodyChooser(MfcElement element)
    {
        var data = new PerBodyChooserData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 2 <= raw.Length)
            data.ChooserFlags = BitConverter.ToUInt16(raw, offset);

        return data;
    }

    internal static FaceRefData? ParseFaceRef(MfcElement element)
    {
        var data = new FaceRefData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.FaceCount = BitConverter.ToUInt32(raw, offset);
        offset += 4;

        if (offset < raw.Length)
            data.TopologyData = MfcPrimitives.CopyRange(raw, offset, raw.Length);

        return data;
    }

    internal static EndFaceSurfIdRepData? ParseEndFaceSurfIdRep(MfcElement element)
    {
        return new EndFaceSurfIdRepData { RawData = element.RawData };
    }

    internal static FrData? ParseFr(MfcElement element)
    {
        return new FrData { RawData = element.RawData };
    }

    internal static FromSktEntSurfIdRepData? ParseFromSktEntSurfIdRep(MfcElement element)
    {
        return new FromSktEntSurfIdRepData { RawData = element.RawData };
    }

    internal static BBoxCenterData? ParseBBoxCenter(MfcElement element)
    {
        var data = new BBoxCenterData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int len = raw.Length;
        int offset = 0;

        if (offset + 8 <= len)
            data.CenterX = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        if (offset + 8 <= len)
            data.CenterY = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        if (offset + 8 <= len)
            data.CenterZ = BitConverter.ToDouble(raw, offset);
        offset = Math.Min(offset + 8, len);

        if (offset < len)
            data.Extra = MfcPrimitives.CopyRange(raw, offset, len);

        return data;
    }

    internal static EndSpecData? ParseEndSpec(MfcElement element)
    {
        var data = new EndSpecData();
        byte[] raw = element.RawData;
        data.RawData = raw;

        if (raw.Length >= 4)
        {
            data.EndType = BitConverter.ToUInt32(raw, 0);
        }

        if (raw.Length >= 0x14)
        {
            data.EndCondition = BitConverter.ToUInt32(raw, 0x10);
        }

        if (raw.Length >= 12)
        {
            data.EndDepth = BitConverter.ToDouble(raw, 4);
        }

        data.UnparsedTail = raw;
        return data;
    }

    internal static FromEndSpecData? ParseFromEndSpec(MfcElement element)
    {
        return new FromEndSpecData { RawData = element.RawData };
    }

    // ═══════════════════════════════════════════════════════════════════
    // .ske-specific sketch classes
    // ═══════════════════════════════════════════════════════════════════

    internal static PntLineDistData? ParsePntLineDist(MfcElement element)
    {
        var data = new PntLineDistData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.Value = BitConverter.ToUInt32(raw, offset);

        return data;
    }

    internal static LLDistanceData? ParseLLDistance(MfcElement element)
    {
        return new LLDistanceData { RawData = element.RawData };
    }

    internal static AngularDimData? ParseAngularDim(MfcElement element)
    {
        var data = new AngularDimData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        if (offset + 4 <= raw.Length)
            data.Value = BitConverter.ToUInt32(raw, offset);

        return data;
    }

    internal static NodeNameData? ParseNodeName(MfcElement element)
    {
        var data = new NodeNameData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        // Limit search to before the first nested MFC tag
        int searchEnd = FindFirstNestedTag(raw);

        // Scan forward to the WChar prefix within this element's own data
        while (offset + 3 <= searchEnd && !MfcPrimitives.IsWCharPrefix(raw, offset))
            offset++;

        var name = MfcPrimitives.ReadWChar(raw, ref offset);
        if (name != null)
            data.Name = name;

        return data;
    }

    internal static LengthParameterCData? ParseLengthParameterC(MfcElement element)
    {
        var data = new LengthParameterCData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        // Limit search to before the first nested MFC tag
        int searchEnd = FindFirstNestedTag(raw);

        // Skip header u32/u16 values before the WChar name
        while (offset + 3 <= searchEnd && !MfcPrimitives.IsWCharPrefix(raw, offset))
            offset++;

        var name = MfcPrimitives.ReadWChar(raw, ref offset);
        if (name != null)
            data.Name = name;

        return data;
    }

    internal static AngleParameterCData? ParseAngleParameterC(MfcElement element)
    {
        var data = new AngleParameterCData();
        byte[] raw = element.RawData;
        data.RawData = raw;
        int offset = 0;

        // Limit search to before the first nested MFC tag
        int searchEnd = FindFirstNestedTag(raw);

        // Skip header u32/u16 values before the WChar name
        while (offset + 3 <= searchEnd && !MfcPrimitives.IsWCharPrefix(raw, offset))
            offset++;

        var name = MfcPrimitives.ReadWChar(raw, ref offset);
        if (name != null)
            data.Name = name;

        return data;
    }

    /// <summary>
    /// Find the offset of the first nested MFC tag within RawData.
    /// Returns raw.Length if no nested tag is found.
    /// </summary>
    private static int FindFirstNestedTag(byte[] raw)
    {
        for (int i = 0; i < raw.Length - 4; i++)
        {
            if (MfcPrimitives.IsMfcTag(raw, i))
                return i;
        }
        return raw.Length;
    }

    internal static DisplayAngularDimData? ParseDisplayAngularDim(MfcElement element)
    {
        return new DisplayAngularDimData { RawData = element.RawData };
    }

    internal static ObListData? ParseObList(MfcElement element)
    {
        return new ObListData { RawData = element.RawData };
    }
}
