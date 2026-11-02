using System;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured single-variable slot map for the ISO M6 counterbore and countersink authoring
/// blocks. The offsets below are relative to the unique Chinese named-parameter value slot
/// (the 04 80 WChar parameter record); native same-document ModifyDefinition experiments
/// changed exactly these copies while all other bytes stayed stable:
/// - counterbore Ø6: two copies (named value and +0x1383);
/// - counterbore Ø11: two copies (+0x1B8D);
/// - counterbore depth 6: nine copies (-0x429A, -0x4156, 0, +0xDC9, +0x1937, +0x1ECC,
///   +0x1EE4, +0x1F6E, +0x1F86);
/// - countersink Ø11: two copies (+0x1DB2);
/// - countersink 90°: two copies (+0x21C0).
/// Each slot is validated against the measured template value before patching.
/// </summary>
internal static class HoleWizardParameterSlots
{
    internal static readonly int[] CounterBoreThruDiameterDeltas = { 0, 0x1383 };
    internal static readonly int[] CounterBoreDiameterDeltas = { 0, 0x1B8D };
    internal static readonly int[] CounterBoreDepthDeltas =
        { -0x429A, -0x4156, 0, 0xDC9, 0x1937, 0x1ECC, 0x1EE4, 0x1F6E, 0x1F86 };
    internal static readonly int[] CounterSinkDiameterDeltas = { 0, 0x1DB2 };
    internal static readonly int[] CounterSinkAngleDeltas = { 0, 0x21C0 };

    internal static bool TryFindNamedDouble(
        byte[] payload, string name, out int valueOffset, out double value)
    {
        valueOffset = -1;
        value = 0;
        if (payload == null) return false;
        int found = -1;
        double foundValue = 0;
        int count = 0;
        for (int i = 0; i + 6 <= payload.Length; i++)
        {
            if (payload[i] != 0x04 || payload[i + 1] != 0x80 ||
                !Mfc.MfcPrimitives.IsWCharPrefix(payload, i + 2)) continue;
            int position = i + 2;
            string? candidate = Mfc.MfcPrimitives.ReadWChar(payload, ref position);
            if (!string.Equals(candidate, name, StringComparison.Ordinal)) continue;
            if (position + 30 > payload.Length ||
                BitConverter.ToDouble(payload, position) != 2.0 ||
                BitConverter.ToUInt32(payload, position + 8) != uint.MaxValue ||
                BitConverter.ToUInt32(payload, position + 12) != 0 ||
                !Mfc.MfcPrimitives.IsWCharPrefix(payload, position + 16) ||
                payload[position + 19] != 0 ||
                BitConverter.ToUInt16(payload, position + 20) != 0)
                return false;
            found = position + 22;
            foundValue = BitConverter.ToDouble(payload, found);
            count++;
        }
        if (count != 1) return false;
        valueOffset = found;
        value = foundValue;
        return true;
    }

    internal static void PatchCounterBore(
        byte[] block, double thruDiameter, double counterBoreDiameter, double counterBoreDepth)
    {
        RequirePositiveFinite(thruDiameter, nameof(thruDiameter));
        RequirePositiveFinite(counterBoreDiameter, nameof(counterBoreDiameter));
        RequirePositiveFinite(counterBoreDepth, nameof(counterBoreDepth));
        if (!TryFindNamedDouble(block, HoleWizardSegments.ThruHoleDiameterName,
                out int thruOffset, out double thruMeasured) ||
            Math.Abs(thruMeasured - 0.006) > 1e-12)
            throw new NotSupportedException(
                "Counterbore parameterization requires the measured Ø6 named-parameter layout. " +
                "No stream changes have been made.");
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterBoreDiameterName,
                out int boreOffset, out double boreMeasured) ||
            Math.Abs(boreMeasured - 0.011) > 1e-12)
            throw new NotSupportedException(
                "Counterbore parameterization requires the measured Ø11 named-parameter layout. " +
                "No stream changes have been made.");
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterBoreDepthName,
                out int depthOffset, out double depthMeasured) ||
            Math.Abs(depthMeasured - 0.006) > 1e-12)
            throw new NotSupportedException(
                "Counterbore parameterization requires the measured 6 mm depth named-parameter layout. " +
                "No stream changes have been made.");
        PatchDeltas(block, thruOffset, CounterBoreThruDiameterDeltas, 0.006, thruDiameter);
        PatchDeltas(block, boreOffset, CounterBoreDiameterDeltas, 0.011, counterBoreDiameter);
        PatchDeltas(block, depthOffset, CounterBoreDepthDeltas, 0.006, counterBoreDepth);
    }

    internal static void PatchCounterSink(
        byte[] block, bool m6, double counterSinkDiameter, double counterSinkAngle)
    {
        RequirePositiveFinite(counterSinkDiameter, nameof(counterSinkDiameter));
        RequirePositiveFinite(counterSinkAngle, nameof(counterSinkAngle));
        double measuredDiameter = m6 ? 0.011 : 0.015;
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterSinkDiameterName,
                out int diameterOffset, out double diameterMeasured) ||
            Math.Abs(diameterMeasured - measuredDiameter) > 1e-12)
            throw new NotSupportedException(
                "Countersink parameterization requires the measured diameter named-parameter layout. " +
                "No stream changes have been made.");
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterSinkAngleName,
                out int angleOffset, out double angleMeasured) ||
            Math.Abs(angleMeasured - Math.PI / 2.0) > 1e-12)
            throw new NotSupportedException(
                "Countersink parameterization requires the measured 90-degree named-parameter layout. " +
                "No stream changes have been made.");
        PatchDeltas(block, diameterOffset, CounterSinkDiameterDeltas, measuredDiameter, counterSinkDiameter);
        PatchDeltas(block, angleOffset, CounterSinkAngleDeltas, Math.PI / 2.0, counterSinkAngle);
    }

    private static void PatchDeltas(
        byte[] block, int baseOffset, int[] deltas, double measuredValue, double newValue)
    {
        if (Math.Abs(measuredValue - newValue) <= 1e-15) return;
        foreach (int delta in deltas)
        {
            int offset = baseOffset + delta;
            if (offset < 0 || offset + 8 > block.Length)
                throw new NotSupportedException(
                    "Counterbore/countersink parameter slot lies outside the measured block. " +
                    "No stream changes have been made.");
            if (Math.Abs(BitConverter.ToDouble(block, offset) - measuredValue) > 1e-12)
                throw new NotSupportedException(
                    "Counterbore/countersink parameter slot does not carry the measured template value. " +
                    "No stream changes have been made.");
            BitConverter.GetBytes(newValue).CopyTo(block, offset);
        }
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(name, $"{name} must be a positive finite number.");
    }
}
