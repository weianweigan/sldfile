using System;
using SldFile.Binary;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Serializes the measured constant-radius fillet block (<c>Fillet_c</c>, FolderHeader TypeId
/// 0x51) from the controlled fillet_2mm sample. The radius named parameter inside
/// <c>ThreeDRadiusDim_c</c> and its measured display copies are written structurally; the
/// selection/display blocks remain the measured metadata for the controlled single edge.
/// </summary>
internal static class MfcFilletSerializer
{
    public static byte[] SerializeFillet(FilletFeatureDefinitionData def, string featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (!def.DefaultRadius.HasValue)
            throw new ArgumentException("Fillet DefaultRadius must be set.", nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "圆角1";

        double radius = def.DefaultRadius.Value;
        using var writer = new MfcStreamWriter(4096);

        // 1. Fillet_c folder header: structural prefix + measured suffix.
        writer.WriteMfcTag(FilletSegments.ClassName);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(FilletSegments.HeaderTypeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(FilletSegments.HeaderSuffix);

        // 2. Measured selection/reference blocks for the controlled single edge.
        WriteTagged(writer, "moSurfaceIdRep_c", FilletSegments.SurfaceIdRepData);
        WriteTagged(writer, "moFilletSurfIdRep_c", FilletSegments.FilletSurfIdRepData);
        WriteTagged(writer, "moEdgeRef_c", FilletSegments.EdgeRefData);
        WriteTagged(writer, "moCompEdge_c", FilletSegments.CompEdgeData);
        writer.WriteMfcTag("edgeRadiusObject_c");
        WriteTagged(writer, "moDisplayRadialDim_c", FilletSegments.DisplayRadialDimData);

        // 3. Radius dimension: named parameter value plus the measured display copies.
        var radiusDim = Clone(FilletSegments.ThreeDRadiusDimData);
        foreach (int slot in FilletSegments.ThreeDRadiusPositiveSlots)
            WriteF64At(radiusDim, slot, radius);
        WriteF64At(radiusDim, FilletSegments.ThreeDRadiusNegativeSlot, -radius);
        writer.WriteMfcTag("ThreeDRadiusDim_c");
        writer.WriteBytes(radiusDim);

        var compFace = Clone(FilletSegments.CompFaceData);
        WriteF64At(compFace, FilletSegments.CompFaceRadiusSlot, radius);
        writer.WriteMfcTag("moCompFace_c");
        writer.WriteBytes(compFace);

        return writer.ToArray();
    }

    private static void WriteTagged(MfcStreamWriter writer, string className, byte[] data)
    {
        writer.WriteMfcTag(className);
        writer.WriteBytes(data);
    }

    private static byte[] Clone(byte[] source) => (byte[])source.Clone();

    private static void WriteF64At(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException($"Fillet segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }
}
