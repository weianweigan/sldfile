using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured 2026 Shell layout (moShell_c, stream version 0x70).
/// Only fields established by shell_t2/shell_t3 are decoded: thickness (six identical
/// double slots anchored after ThreeDRadiusDim_c), one removed-face record
/// (moCompFace_c) and the folder-header type code. Direction and multiple-thickness
/// faces are not decoded (no variation/samples) and stay unset.
/// </summary>
internal static class ShellFeatureParser
{
    internal const string ClassName = "moShell_c";
    internal const uint ExpectedTypeId = 0x51;
    internal const ushort ExpectedStreamVersion = 0x70;

    // Absolute offsets from the moShell_c tag to the six measured thickness slots.
    internal static readonly int[] ThicknessSlotOffsets = { 0x51C, 0x554, 0x6A2, 0x6BA, 0x74C, 0x764 };
    internal const int ThreeDRadiusRelativeOffset = 0x4DF;

    public static ShellFeatureDefinitionData? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != ClassName || element.TagByte != 0x01)
            return null;

        byte[] data = element.SourceData;
        int tag = element.TagOffset;
        if (tag < 0 || tag + 0x10 > data.Length)
            return null;

        if (BitConverter.ToUInt16(data, 0) != ExpectedStreamVersion)
            return null;

        if (!TryReadHeaderTypeId(data, element.DataStart, out uint typeId) || typeId != ExpectedTypeId)
            return null;

        var radiusDim = MfcPrimitives.ScanMfcTags(data)
            .FirstOrDefault(t => t.ClassName == "ThreeDRadiusDim_c" && t.TagOffset > tag);
        if (radiusDim == null || radiusDim.TagOffset - tag != ThreeDRadiusRelativeOffset)
            return null;

        if (!TryReadThickness(data, tag, out double thickness))
            return null;

        var faceChildren = element.Children.Where(c => c.ClassName == "moCompFace_c").ToList();
        if (faceChildren.Count != 1)
            return null;

        var face = ParseRemovedFace(faceChildren[0]);
        if (face == null)
            return null;

        var definition = new ShellFeatureDefinitionData
        {
            SerializedTypeId = typeId,
            Thickness = thickness,
            FacesRemoved = new[] { face }
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Thickness = true;
        definition.Resolution.FacesRemoved = true;
        // Direction / MultipleThickness* have no controlled evidence.
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
        // The folder header starts immediately after the WChar characters; the two
        // zero bytes at its start belong to the timestamp double, not a terminator.
        int header = cursor;
        if (header + 12 > data.Length)
            return false;
        typeId = BitConverter.ToUInt32(data, header + 8);
        return true;
    }

    private static bool TryReadThickness(byte[] data, int tag, out double thickness)
    {
        thickness = 0;
        double first = 0;
        for (int i = 0; i < ThicknessSlotOffsets.Length; i++)
        {
            int offset = tag + ThicknessSlotOffsets[i];
            if (offset + 8 > data.Length)
                return false;
            double value = BitConverter.ToDouble(data, offset);
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > 0.1)
                return false;
            if (i == 0) first = value;
            else if (Math.Abs(value - first) > 1e-15)
                return false;
        }
        thickness = first;
        return true;
    }

    private static EntityReference? ParseRemovedFace(MfcElement faceElement)
    {
        byte[] raw = faceElement.RawData;
        if (raw.Length < 20)
            return null;
        if (BitConverter.ToUInt16(raw, 0) != 0x802C || BitConverter.ToUInt16(raw, 2) != 2)
            return null;

        uint faceId = BitConverter.ToUInt32(raw, 12);
        if (faceId == 0)
            return null;
        uint componentId = BitConverter.ToUInt32(raw, 8);
        return new EntityReference
        {
            EntityType = SelectionEntityType.Face,
            EntityId = faceId,
            Name = $"moCompFace_c:{componentId:X8}:{faceId:X8}"
        };
    }
}
