using System;
using SldFile.Mfc;

using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Static Parse methods for each known feature data class.
/// Each method reads from an <see cref="MfcElement"/> via <see cref="MfcReader"/> and returns
/// a typed data object, preserving unparsed regions for roundtrip fidelity.
/// </summary>
internal static class FeatureDataParsers
{
    // ═══════════════════════════════════════════════════════════════════
    // Folder classes with extra data beyond FolderHeader
    // ═══════════════════════════════════════════════════════════════════

    internal static LengthParameterData? ParseLengthParameter(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new LengthParameterData { RawData = element.RawData };

        r.ReadWChar();
        if (!r.CanRead(90)) return data;
        r.Skip(90);

        if (r.TryReadF64(out double paramValue))
            data.ParamValue = paramValue;
        if (r.TryReadU32(out uint flags))
            data.Flags = flags;
        if (r.TryReadU32(out uint handleCount))
            data.HandleCount = handleCount;
        if (r.HasMore)
            data.UnparsedTail = r.ReadRemainingBytes();

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Reference geometry
    // ═══════════════════════════════════════════════════════════════════

    internal static DefaultRefPlaneData? ParseDefaultRefPlane(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new DefaultRefPlaneData
        {
            RawData = element.RawData,
            UnparsedHead = r.ReadBytes(40)
        };

        if (r.TryReadF64(out double originX))
            data.OriginX = originX;
        r.Skip(24);
        if (r.TryReadF64(out double originY))
            data.OriginY = originY;

        r.ReadF64Array(data.XAxis);
        r.ReadF64Array(data.YAxis);
        r.ReadF64Array(data.ZAxis);
        r.ReadF64Array(data.Translation);
        r.ReadF64Array(data.TransformExtra);

        if (r.HasMore)
            data.UnparsedTail = r.ReadRemainingBytes();

        return data;
    }

    internal static CompRefPlaneData? ParseCompRefPlane(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new CompRefPlaneData { RawData = element.RawData };

        if (r.TryReadU16(out ushort refPlaneId))
            data.RefPlaneId = refPlaneId;
        if (r.TryReadU16(out ushort flags))
            data.Flags = flags;

        r.Skip(28);
        if (r.CanRead(16))
            data.Guid = r.ReadGuid();
        if (r.HasMore)
            data.UnparsedTail = r.ReadRemainingBytes();

        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Sketch & sketch entities
    // ═══════════════════════════════════════════════════════════════════

    internal static SketchData? ParseSketch(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new SketchData { RawData = element.RawData };

        if (r.TryReadU32(out uint entityCount)) data.EntityCount = entityCount;
        if (r.TryReadU32(out uint flags1)) data.Flags1 = flags1;
        if (r.TryReadU32(out uint flags2)) data.Flags2 = flags2;
        if (r.TryReadU32(out uint arrayHeader)) data.ArrayHeader = arrayHeader;
        if (r.HasMore) data.SketchBody = r.ReadRemainingBytes();

        return data;
    }

    internal static PointHandleData? ParsePointHandle(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new PointHandleData { RawData = element.RawData };

        if (r.TryReadU32(out uint refId)) data.RefId = refId;
        if (r.HasMore) data.HandleData = r.ReadRemainingBytes();

        return data;
    }

    internal static LineHandleData? ParseLineHandle(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new LineHandleData { RawData = element.RawData };

        if (r.TryReadU32(out uint index)) data.Index = index;
        if (r.HasMore) data.HandleData = r.ReadRemainingBytes();

        return data;
    }

    internal static ArcHandleData? ParseArcHandle(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new ArcHandleData { RawData = element.RawData };

        if (r.TryReadU32(out uint index)) data.Index = index;
        if (r.HasMore) data.HandleData = r.ReadRemainingBytes();

        return data;
    }

    internal static CircleDimData? ParseCircleDim(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static PntPntDistData? ParsePntPntDist(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static ExtEntData? ParseExtEnt(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static SketchExtRefData? ParseSketchExtRef(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static CompSketchEntHandleData? ParseCompSketchEntHandle(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new CompSketchEntHandleData { RawData = element.RawData };

        if (r.TryReadU32(out uint entityIndex)) data.EntityIndex = entityIndex;
        if (r.TryReadU32(out uint faceIndex)) data.FaceIndex = faceIndex;
        if (r.HasMore) data.TopologyRef = r.ReadRemainingBytes();

        return data;
    }

    internal static EntHandleData? ParseEntHandle(MfcElement element) =>
        new() { RawData = element.RawData };

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

        int flagOffset = -1;
        for (int i = offset; i + 8 <= len; i += 4)
        {
            if (BitConverter.ToUInt32(raw, i) == 0x00000004)
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
            offset = Math.Min(offset + 24, len);
        }

        if (offset < len)
            data.UnparsedTail = MfcPrimitives.CopyRange(raw, offset, len);

        TopologyParsers.ParseEnrichedSketchChain(data, raw);
        return data;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dimensions
    // ═══════════════════════════════════════════════════════════════════

    internal static DisplayDistanceDimData? ParseDisplayDistanceDim(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new DisplayDistanceDimData { RawData = element.RawData };

        if (r.CanRead(18))
            data.DimHeader = r.ReadBytes(18);
        else
            r.Skip(18);

        if (r.TryReadF64(out double dimValue))
            data.DimValue = dimValue;
        if (r.HasMore)
            data.TolData = r.ReadRemainingBytes();

        return data;
    }

    internal static DisplayRadialDimData? ParseDisplayRadialDim(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static SkDimHandleValG2Data? ParseSkDimHandleValG2(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static SkDimHandleRadialData? ParseSkDimHandleRadial(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static FeatureDimHandleData? ParseFeatureDimHandle(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static ParallelPlaneDistanceDimData? ParseParallelPlaneDistanceDim(MfcElement element)
    {
        byte[] raw = element.RawData;
        var data = new ParallelPlaneDistanceDimData { RawData = raw };
        if (raw.Length >= 0x45)
            data.DimValue = BitConverter.ToDouble(raw, 0x3D);
        return data;
    }

    internal static FavoriteHandleData? ParseFavoriteHandle(MfcElement element) =>
        new() { RawData = element.RawData };

    // ═══════════════════════════════════════════════════════════════════
    // Extrusion sub-objects
    // ═══════════════════════════════════════════════════════════════════

    internal static PerBodyChooserData? ParsePerBodyChooser(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new PerBodyChooserData { RawData = element.RawData };
        if (r.TryReadU16(out ushort chooserFlags))
            data.ChooserFlags = chooserFlags;
        return data;
    }

    internal static FaceRefData? ParseFaceRef(MfcElement element)
    {
        var data = new FaceRefData();
        TopologyParsers.ParseEnrichedFaceRef(data, element.RawData);
        return data;
    }

    internal static EndFaceSurfIdRepData? ParseEndFaceSurfIdRep(MfcElement element)
    {
        var data = new EndFaceSurfIdRepData();
        TopologyParsers.ParseEnrichedEndFaceSurfIdRep(data, element.RawData);
        return data;
    }

    internal static FrData? ParseFr(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static FromSktEntSurfIdRepData? ParseFromSktEntSurfIdRep(MfcElement element)
    {
        var data = new FromSktEntSurfIdRepData();
        TopologyParsers.ParseEnrichedFromSktEntSurfIdRep(data, element.RawData);
        return data;
    }

    internal static BBoxCenterData? ParseBBoxCenter(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new BBoxCenterData { RawData = element.RawData };

        if (r.TryReadF64(out double cx)) data.CenterX = cx;
        if (r.TryReadF64(out double cy)) data.CenterY = cy;
        if (r.TryReadF64(out double cz)) data.CenterZ = cz;
        if (r.HasMore) data.Extra = r.ReadRemainingBytes();

        return data;
    }

    internal static EndSpecData? ParseEndSpec(MfcElement element)
    {
        byte[] raw = element.RawData;
        var data = new EndSpecData
        {
            RawData = raw,
            UnparsedTail = raw
        };
        if (raw.Length >= 4) data.EndType = BitConverter.ToUInt32(raw, 0);
        if (raw.Length >= 0x14) data.EndCondition = BitConverter.ToUInt32(raw, 0x10);
        if (raw.Length >= 12) data.EndDepth = BitConverter.ToDouble(raw, 4);
        return data;
    }

    internal static FromEndSpecData? ParseFromEndSpec(MfcElement element) =>
        new() { RawData = element.RawData };

    // ═══════════════════════════════════════════════════════════════════
    // .ske-specific sketch classes
    // ═══════════════════════════════════════════════════════════════════

    internal static PntLineDistData? ParsePntLineDist(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new PntLineDistData { RawData = element.RawData };
        if (r.TryReadU32(out uint val)) data.Value = val;
        return data;
    }

    internal static LLDistanceData? ParseLLDistance(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static AngularDimData? ParseAngularDim(MfcElement element)
    {
        var r = element.CreateReader();
        var data = new AngularDimData { RawData = element.RawData };
        if (r.TryReadU32(out uint val)) data.Value = val;
        return data;
    }

    internal static NodeNameData? ParseNodeName(MfcElement element) =>
        new() { RawData = element.RawData, Name = ReadFirstWCharBeforeNestedTag(element.RawData) ?? string.Empty };

    internal static LengthParameterCData? ParseLengthParameterC(MfcElement element) =>
        new() { RawData = element.RawData, Name = ReadFirstWCharBeforeNestedTag(element.RawData) ?? string.Empty };

    internal static AngleParameterCData? ParseAngleParameterC(MfcElement element) =>
        new() { RawData = element.RawData, Name = ReadFirstWCharBeforeNestedTag(element.RawData) ?? string.Empty };

    private static string? ReadFirstWCharBeforeNestedTag(byte[] raw)
    {
        int searchEnd = raw.Length;
        for (int i = 0; i < raw.Length - 4; i++)
        {
            if (MfcPrimitives.IsMfcTag(raw, i))
            {
                searchEnd = i;
                break;
            }
        }

        var r = new MfcReader(raw, 0, searchEnd);
        return r.SeekNextWChar() ? r.ReadWChar() : null;
    }

    internal static DisplayAngularDimData? ParseDisplayAngularDim(MfcElement element) =>
        new() { RawData = element.RawData };

    internal static ObListData? ParseObList(MfcElement element) =>
        new() { RawData = element.RawData };
}
