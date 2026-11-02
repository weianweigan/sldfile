using System;
using SldFile.Binary;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Serializes the measured Chamfer_c block (FolderHeader TypeId 0x52) for the controlled
/// distance-distance and angle-distance layouts. D1/D3 distances and the D2 angle are written
/// into the measured named-parameter slots and display copies; the selection/header metadata
/// remains the measured payload for the controlled single target edge.
/// </summary>
internal static class MfcChamferSerializer
{
    public static byte[] SerializeChamfer(
        ChamferFeatureDefinitionData def, string featureName, bool angleDistance)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (!def.EdgeChamferDistanceSide0.HasValue)
            throw new ArgumentException("Chamfer distance must be set.", nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "倒角1";

        double distance0 = def.EdgeChamferDistanceSide0.Value;
        using var writer = new MfcStreamWriter(6144);

        // 1. Chamfer_c header: structural prefix + mode-specific measured suffix.
        writer.WriteMfcTag(ChamferSegments.ClassName);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(ChamferSegments.HeaderTypeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(angleDistance
            ? ChamferSegments.HeaderSuffixAngleDistance
            : ChamferSegments.HeaderSuffixDistanceDistance);

        // 2. Measured selection blocks for the controlled single edge.
        WriteTagged(writer, "moSurfaceIdRep_c", angleDistance
            ? ChamferSegments.SurfaceIdRepDataAngleDistance
            : ChamferSegments.SurfaceIdRepDataDistanceDistance);
        WriteTagged(writer, "moFilletSurfIdRep_c", angleDistance
            ? ChamferSegments.FilletSurfIdRepDataAngleDistance
            : ChamferSegments.FilletSurfIdRepDataDistanceDistance);
        WriteTagged(writer, "moEdgeRef_c", angleDistance
            ? ChamferSegments.EdgeRefDataAngleDistance
            : ChamferSegments.EdgeRefDataDistanceDistance);

        // 3. moCompEdge_c: shared edge prefix + D1 (and D3 for distance-distance) slots.
        var compEdge = Clone(angleDistance
            ? ChamferSegments.CompEdgeDataAngleDistance
            : ChamferSegments.CompEdgeDataDistanceDistance);
        foreach (int slot in ChamferSegments.DistancePositiveSlots)
            WriteF64At(compEdge, slot, distance0);
        foreach (int slot in ChamferSegments.DistanceNegativeSlots)
            WriteF64At(compEdge, slot, -distance0);

        if (!angleDistance)
        {
            double distance1 = def.EdgeChamferDistanceSide1!.Value;
            foreach (int slot in ChamferSegments.SecondDistancePositiveSlots)
                WriteF64At(compEdge, slot, distance1);
            foreach (int slot in ChamferSegments.SecondDistanceNegativeSlots)
                WriteF64At(compEdge, slot, -distance1);
        }
        writer.WriteMfcTag("moCompEdge_c");
        writer.WriteBytes(compEdge);

        // 4. Angle-distance adds the measured angular display stack and D2 parameter.
        if (angleDistance)
        {
            double angle = def.EdgeChamferAngle!.Value;
            var angleParam = Clone(ChamferSegments.AngleParamData);
            foreach (int slot in ChamferSegments.AngleSlots)
                WriteF64At(angleParam, slot, angle);
            WriteF64At(angleParam, ChamferSegments.AngleParameterDistanceCopySlot, distance0);

            WriteTagged(writer, "moDisplayAngularDim_c", ChamferSegments.DisplayAngularDimData);
            WriteTagged(writer, "AngleDim_c", ChamferSegments.AngleDimData);
            WriteTagged(writer, "moAngleParameter_c", angleParam);
        }

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
            throw new InvalidOperationException($"Chamfer segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }
}
