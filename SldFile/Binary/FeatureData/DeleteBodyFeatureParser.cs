using System;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured 2026 DeleteBody layout (moDeleteBody_c, stream 0x71).
/// The sample is the API default-body-selection result (first body deleted, second kept);
/// only the body count/reference and type code are structurally decoded. KeepBodies is
/// not isolated by a controlled variant and remains unset.
/// </summary>
internal static class DeleteBodyFeatureParser
{
    internal const string ClassName = "moDeleteBody_c";
    internal const uint ExpectedTypeId = 0x57;
    internal const ushort ExpectedStreamVersion = 0x71;

    public static DeleteBodyFeatureDefinitionData? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != ClassName || element.TagByte != 0x01)
            return null;

        byte[] data = element.SourceData;
        if (BitConverter.ToUInt16(data, 0) != ExpectedStreamVersion)
            return null;

        if (!TryReadHeaderTypeId(data, element.DataStart, out uint typeId) || typeId != ExpectedTypeId)
            return null;

        var dataChildren = element.Children.Where(c => c.ClassName == "moDeleteBodyData_c").ToList();
        if (dataChildren.Count != 1)
            return null;
        var bodyData = dataChildren[0];
        if (bodyData.RawData.Length < 2 || BitConverter.ToUInt16(bodyData.RawData, 0) != 1)
            return null;

        var bodyChildren = bodyData.Children.Where(c => c.ClassName == "moCompSolidBody_c").ToList();
        if (bodyChildren.Count != 1)
            return null;
        var body = ParseBodyReference(bodyChildren[0]);
        if (body == null)
            return null;

        var definition = new DeleteBodyFeatureDefinitionData
        {
            SerializedTypeId = typeId,
            Bodies = new[] { body }
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Bodies = true;
        // KeepBodies has no controlled false/true variant; do not claim it.
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

    private static EntityReference? ParseBodyReference(MfcElement element)
    {
        byte[] raw = element.RawData;
        if (raw.Length < 20)
            return null;
        if (BitConverter.ToUInt16(raw, 0) != 0x802C || BitConverter.ToUInt16(raw, 2) != 2)
            return null;

        uint bodyId = BitConverter.ToUInt32(raw, 12);
        if (bodyId == 0)
            return null;
        uint componentId = BitConverter.ToUInt32(raw, 8);
        return new EntityReference
        {
            EntityType = SelectionEntityType.Body,
            EntityId = bodyId,
            Name = $"moCompSolidBody_c:{componentId:X8}:{bodyId:X8}"
        };
    }
}
