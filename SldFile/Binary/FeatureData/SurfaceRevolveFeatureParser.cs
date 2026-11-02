using System;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured 2026 surface-revolve layout
/// (moRevolvRefSurf_c, stream 0x70). Only the revolution angle (AngleDim_c ->
/// moAngleParameter_c "D1", three identical copies) and the type code are decoded.
/// Type/ReverseDirection have no controlled variant and stay unset; axis/profile
/// selections are not decoded.
/// </summary>
internal static class SurfaceRevolveFeatureParser
{
    internal const string ClassName = "moRevolvRefSurf_c";
    internal const uint ExpectedTypeId = 0x4E;
    internal const ushort ExpectedStreamVersion = 0x70;

    internal const int AngleDimRelativeOffset = 0x80E;
    internal const int AngleParameterRelativeOffset = 0x822;
    internal const int AngleValueFromParameter = 0x20;
    internal const int AngleCopy1FromParameter = 0x222;
    internal const int AngleCopy2FromParameter = 0x23A;

    public static SurfaceRevolveFeatureDefinitionData? Parse(MfcElement element)
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

        var tags = MfcPrimitives.ScanMfcTags(data);
        var angleDim = tags.FirstOrDefault(t => t.ClassName == "AngleDim_c" && t.TagOffset > tag);
        if (angleDim == null)
            return null;
        var angleParam = tags.FirstOrDefault(t =>
            t.ClassName == "moAngleParameter_c" && t.TagOffset > angleDim.TagOffset);
        if (angleParam == null)
            return null;

        int baseOffset = angleParam.DataStart;
        int s1 = baseOffset + AngleValueFromParameter;
        int s2 = baseOffset + AngleCopy1FromParameter;
        int s3 = baseOffset + AngleCopy2FromParameter;
        if (s3 + 8 > data.Length)
            return null;

        double angle = BitConverter.ToDouble(data, s1);
        double copy1 = BitConverter.ToDouble(data, s2);
        double copy2 = BitConverter.ToDouble(data, s3);
        if (double.IsNaN(angle) || double.IsInfinity(angle) || angle <= 0 || angle > 2 * Math.PI)
            return null;
        if (Math.Abs(angle - copy1) > 1e-15 || Math.Abs(angle - copy2) > 1e-15)
            return null;

        var definition = new SurfaceRevolveFeatureDefinitionData
        {
            SerializedClassName = ClassName,
            SerializedTypeId = typeId,
            Angle = angle,
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(element.SourceData)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Angle = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        // Type / ReverseDirection have no controlled variation.
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
