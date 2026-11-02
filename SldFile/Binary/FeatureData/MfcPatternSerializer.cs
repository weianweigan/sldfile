using System;
using SldFile.Binary;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Serializes the controlled PatternOptions blocks for linear (moLPattern_c / 0x5D) and
/// circular (moCirPattern_c / 0x63) patterns. Only the measured instance-count, spacing and
/// per-instance-angle slots and their display copies are written; the direction/axis/source
/// selection metadata and header suffix remain the measured payloads.
/// </summary>
internal static class MfcPatternSerializer
{
    public static byte[] SerializeLinearPattern(
        LinearPatternFeatureDefinitionData def, string featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "线性阵列1";

        int count1 = def.D1TotalInstances!.Value;
        double spacing1 = def.D1Spacing!.Value;
        int count2 = def.D2TotalInstances!.Value;
        double spacing2 = def.D2Spacing!.Value;

        using var writer = new MfcStreamWriter(12288);
        WriteHeader(writer, PatternSegments.LinearPatternClassName,
            PatternSegments.LinearPatternTypeId, featureName, PatternSegments.LinearHeaderSuffix);

        var displayDim = Clone(PatternSegments.LinearDisplayDimData);
        WriteF64At(displayDim, PatternSegments.LinearDisplayDimSpacingSlot, spacing1);
        WriteTagged(writer, "moDisplayDim_c", displayDim);
        WriteTagged(writer, "moNumberDim_c", PatternSegments.LinearNumberDimData);

        var integerParam = Clone(PatternSegments.LinearIntegerParameterData);
        WriteU32At(integerParam, PatternSegments.LinearD1CountValueSlot, (uint)count1);
        WriteF64At(integerParam, PatternSegments.LinearD1SpacingValueSlot, spacing1);
        foreach (int slot in PatternSegments.LinearD1SpacingCopySlots)
            WriteF64At(integerParam, slot, spacing1);
        WriteU32At(integerParam, PatternSegments.LinearD2CountValueSlot, (uint)count2);
        WriteF64At(integerParam, PatternSegments.LinearD2SpacingValueSlot, spacing2);
        foreach (int slot in PatternSegments.LinearD2SpacingCopySlots)
            WriteF64At(integerParam, slot, spacing2);
        WriteTagged(writer, "moIntegerParameter_c", integerParam);

        writer.WriteMfcTag("moLineRef_w");
        WriteTagged(writer, "moCompEdge_c", PatternSegments.LinearCompEdgeData);
        return writer.ToArray();
    }

    public static byte[] SerializeCircularPattern(
        CircularPatternFeatureDefinitionData def, string featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "圆周阵列1";

        int count = def.TotalInstances!.Value;
        double spacing = def.Spacing!.Value;
        double perInstanceAngle = spacing / (count - 1);

        using var writer = new MfcStreamWriter(10240);
        WriteHeader(writer, PatternSegments.CircularPatternClassName,
            PatternSegments.CircularPatternTypeId, featureName, PatternSegments.CircularHeaderSuffix);

        WriteTagged(writer, "moDisplayDim_c", PatternSegments.CircularDisplayDimData);
        WriteTagged(writer, "moNumberDim_c", PatternSegments.CircularNumberDimData);

        var integerParam = Clone(PatternSegments.CircularIntegerParameterData);
        WriteU32At(integerParam, PatternSegments.CircularCountValueSlot, (uint)count);
        WriteTagged(writer, "moIntegerParameter_c", integerParam);

        WriteTagged(writer, "moDisplayAngularDim_c", PatternSegments.CircularDisplayAngularDimData);
        WriteTagged(writer, "AngleDim_c", PatternSegments.CircularAngleDimData);

        var angleParam = Clone(PatternSegments.CircularAngleParameterData);
        WriteF64At(angleParam, PatternSegments.CircularAngleValueSlot, perInstanceAngle);
        WriteTagged(writer, "moAngleParameter_c", angleParam);

        writer.WriteMfcTag("moLineRef_w");
        var compEdge = Clone(PatternSegments.CircularCompEdgeData);
        WriteF64At(compEdge, PatternSegments.CircularSpacingValueSlot, spacing);
        foreach (int slot in PatternSegments.CircularSpacingCopySlots)
            WriteF64At(compEdge, slot, spacing);
        WriteTagged(writer, "moCompEdge_c", compEdge);
        return writer.ToArray();
    }

    private static void WriteHeader(
        MfcStreamWriter writer, string className, uint typeId, string featureName, byte[] suffix)
    {
        writer.WriteMfcTag(className);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(typeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(suffix);
    }

    private static void WriteTagged(MfcStreamWriter writer, string className, byte[] data)
    {
        writer.WriteMfcTag(className);
        writer.WriteBytes(data);
    }

    private static byte[] Clone(byte[] source) => (byte[])source.Clone();

    private static void WriteU32At(byte[] block, int offset, uint value)
    {
        if (offset < 0 || offset + 4 > block.Length)
            throw new InvalidOperationException($"Pattern segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }

    private static void WriteF64At(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException($"Pattern segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }
}
