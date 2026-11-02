using System;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured 2026 surface-extrude layout
/// (moExtruRefSurface_c, stream 0x70). Only the depth (two identical slots after
/// ParallelPlaneDistanceDim_c) and the folder-header type code are decoded.
/// EndCondition/BothDirections/draft/cap/delete-original-face/knit-result have no
/// controlled variation and stay unset. Selection/direction references are not decoded.
/// </summary>
internal static class SurfaceExtrudeFeatureParser
{
    internal const string ClassName = "moExtruRefSurface_c";
    internal const uint ExpectedTypeId = 0x4F;
    internal const ushort ExpectedStreamVersion = 0x70;

    internal const int DepthSlot1FromPlaneDim = 0x5D;
    internal const int DepthSlot2FromPlaneDim = 0xA5;

    public static SurfaceExtrudeFeatureDefinitionData? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != ClassName || element.TagByte != 0x01)
            return null;

        byte[] data = element.SourceData;
        int tag = element.TagOffset;
        if (tag < 0 || tag + 16 > data.Length)
            return null;
        if (BitConverter.ToUInt16(data, 0) != ExpectedStreamVersion)
            return null;

        if (!TryReadHeaderTypeId(data, element.DataStart, out uint typeId) || typeId != ExpectedTypeId)
            return null;

        var planeDim = MfcPrimitives.ScanMfcTags(data)
            .FirstOrDefault(t => t.ClassName == "ParallelPlaneDistanceDim_c" && t.TagOffset > tag);
        if (planeDim == null)
            return null;

        int slot1 = planeDim.TagOffset + DepthSlot1FromPlaneDim;
        int slot2 = planeDim.TagOffset + DepthSlot2FromPlaneDim;
        if (slot1 + 8 > data.Length || slot2 + 8 > data.Length)
            return null;

        double depth = BitConverter.ToDouble(data, slot1);
        double second = BitConverter.ToDouble(data, slot2);
        if (double.IsNaN(depth) || double.IsInfinity(depth) || depth <= 0 || depth > 1.0)
            return null;
        if (Math.Abs(depth - second) > 1e-15)
            return null;

        var definition = new SurfaceExtrudeFeatureDefinitionData
        {
            SerializedClassName = ClassName,
            SerializedTypeId = typeId,
            Depth = depth,
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(element.SourceData)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Depth = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        // EndCondition / BothDirections / ReverseDirection / draft / cap / delete-original-face /
        // knit-result / direction references have no controlled variation.
        return definition;
    }

    private static bool TryReadHeaderTypeId(byte[] data, int dataStart, out uint typeId)
    {
        typeId = 0;
        int limit = Math.Min(data.Length, dataStart + 16);
        int offset = dataStart;
        while (offset <= limit - 3 && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset > limit - 3 || !MfcPrimitives.IsWCharPrefix(data, offset))
            return false;

        int cursor = offset;
        if (MfcPrimitives.ReadWChar(data, ref cursor, data.Length) == null)
            return false;
        int header = cursor;
        if (header + 12 > data.Length)
            return false;
        typeId = BitConverter.ToUInt32(data, header + 8);
        return true;
    }
}
