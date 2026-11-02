using System;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured 2026 Draft layout (moDraft_c, stream 0x70).
/// Only the 3-degree angle (AngleDim_c -> moAngleParameter_c "D1" + a second copy)
/// and the neutral-plane face reference (moDirectionSpec_c -> moPlaneRef_w ->
/// moCompFace_c) are decoded. Type, alignment/reverse, face propagation, parting lines,
/// step type and the drafted-face list have no controlled variation and stay unset.
/// </summary>
internal static class DraftFeatureParser
{
    internal const string ClassName = "moDraft_c";
    internal const uint ExpectedTypeId = 0x51;
    internal const ushort ExpectedStreamVersion = 0x70;

    internal const int AngleDimRelativeOffset = 0x3EC;
    internal const int AngleParameterRelativeOffset = 0x400;
    internal const int AngleValueRelativeOffset = 0x438;
    internal const int SecondAngleRelativeOffset = 0x652;
    internal const int DirectionSpecRelativeOffset = 0x83B;
    internal const int PlaneRefRelativeOffset = 0x858;
    internal const int CompFaceRelativeOffset = 0x86A;

    public static DraftFeatureDefinitionData? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != ClassName || element.TagByte != 0x01)
            return null;

        byte[] data = element.SourceData;
        int tag = element.TagOffset;
        if (tag < 0 || tag + SecondAngleRelativeOffset + 8 > data.Length)
            return null;

        if (BitConverter.ToUInt16(data, 0) != ExpectedStreamVersion)
            return null;

        if (!TryReadHeaderTypeId(data, element.DataStart, out uint typeId) || typeId != ExpectedTypeId)
            return null;

        var tags = MfcPrimitives.ScanMfcTags(data);
        var angleDim = tags.FirstOrDefault(t => t.ClassName == "AngleDim_c" && t.TagOffset > tag);
        var angleParam = tags.FirstOrDefault(t => t.ClassName == "moAngleParameter_c" && t.TagOffset > tag);
        var directionSpec = tags.FirstOrDefault(t => t.ClassName == "moDirectionSpec_c" && t.TagOffset > tag);
        var planeRef = tags.FirstOrDefault(t => t.ClassName == "moPlaneRef_w" && t.TagOffset > tag);
        var compFace = tags.FirstOrDefault(t => t.ClassName == "moCompFace_c" && t.TagOffset > tag);

        if (angleDim == null || angleDim.TagOffset - tag != AngleDimRelativeOffset) return null;
        if (angleParam == null || angleParam.TagOffset - tag != AngleParameterRelativeOffset) return null;
        if (directionSpec == null || directionSpec.TagOffset - tag != DirectionSpecRelativeOffset) return null;
        if (planeRef == null || planeRef.TagOffset - tag != PlaneRefRelativeOffset) return null;
        if (compFace == null || compFace.TagOffset - tag != CompFaceRelativeOffset) return null;

        double angle = BitConverter.ToDouble(data, tag + AngleValueRelativeOffset);
        double secondAngle = BitConverter.ToDouble(data, tag + SecondAngleRelativeOffset);
        if (double.IsNaN(angle) || double.IsInfinity(angle) || angle <= 0 || angle >= Math.PI / 2)
            return null;
        if (Math.Abs(angle - secondAngle) > 1e-15)
            return null;

        var neutralPlane = ParseFaceReference(data, compFace);
        if (neutralPlane == null)
            return null;

        var definition = new DraftFeatureDefinitionData
        {
            SerializedTypeId = typeId,
            Angle = angle,
            NeutralPlane = neutralPlane
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Angle = true;
        definition.Resolution.NeutralPlane = true;
        // Type / Reverse / AllowReduced / FacesToDraft / PartingLines / FacePropagation /
        // StepType have no controlled variation in the available samples.
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

    private static EntityReference? ParseFaceReference(byte[] data, MfcTag element)
    {
        int start = element.DataStart;
        if (start < 0 || start + 20 > data.Length)
            return null;
        if (BitConverter.ToUInt16(data, start) != 0x802C || BitConverter.ToUInt16(data, start + 2) != 2)
            return null;
        uint faceId = BitConverter.ToUInt32(data, start + 12);
        if (faceId == 0)
            return null;
        uint componentId = BitConverter.ToUInt32(data, start + 8);
        return new EntityReference
        {
            EntityType = SelectionEntityType.Face,
            EntityId = faceId,
            Name = $"moCompFace_c:{componentId:X8}:{faceId:X8}"
        };
    }
}
