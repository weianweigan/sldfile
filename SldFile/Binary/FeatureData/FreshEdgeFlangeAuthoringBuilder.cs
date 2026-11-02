using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed-template authoring for the fresh single-edge EdgeFlange layout
/// (ResolvedFeatures 0x74, TypeId 0x75). Only the fields that persisted in the
/// native save/reopen matrix are patchable: UseDefaultBendRadius/BendRadius,
/// ReliefWidth/ReliefDepth, UseDefaultBendRelief/UseReliefRatio,
/// UsePositionOffset/PositionOffsetDistance, OffsetDistance and the measured
/// BendAngle/EdgeCount/PositionType. PositionOffsetType=2 is rejected because
/// native readback reverts it to 1.
/// </summary>
internal static class FreshEdgeFlangeAuthoringBuilder
{
    internal static byte[] Build(
        byte[] currentStream, EdgeFlangeFeatureDefinitionData def, string? featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        ValidateDefinition(def);

        bool positionTemplate = def.UsePositionOffset == true;
        bool radiusTemplate = !positionTemplate &&
            (def.UseDefaultBendRadius == false ||
             (def.BendRadius.HasValue && Math.Abs(def.BendRadius.Value - 0.002) > 1e-12));
        if (positionTemplate &&
            (def.UseDefaultBendRadius == false ||
             (def.BendRadius.HasValue && Math.Abs(def.BendRadius.Value - 0.002) > 1e-12)))
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring has no measured template combining a non-default bend " +
                "radius with UsePositionOffset=true. No stream changes have been made.");
        int prefixLength = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionPrefixLength
            : radiusTemplate
                ? FreshEdgeFlangeAuthoringSegments.RadiusPrefixLength
                : FreshEdgeFlangeAuthoringSegments.PrefixLength;
        string normalizedPrefixSha = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionNormalizedPrefixSha256
            : radiusTemplate
                ? FreshEdgeFlangeAuthoringSegments.RadiusNormalizedPrefixSha256
                : FreshEdgeFlangeAuthoringSegments.NormalizedPrefixSha256;
        byte[] suffix = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.GetPositionSuffix()
            : radiusTemplate
                ? FreshEdgeFlangeAuthoringSegments.GetRadiusSuffix()
                : FreshEdgeFlangeAuthoringSegments.GetSuffix();
        string templateLabel = positionTemplate ? "position-offset"
            : radiusTemplate ? "radius" : "default";

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? FreshEdgeFlangeAuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, FreshEdgeFlangeAuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring supports only the measured display name " +
                $"'{FreshEdgeFlangeAuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null || currentStream.Length != prefixLength)
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring requires the stripped base of the " +
                $"{templateLabel} measured sample " +
                $"({prefixLength} bytes, ending at the " +
                $"{FreshEdgeFlangeAuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != FreshEdgeFlangeAuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring supports only ResolvedFeatures version " +
                $"0x{FreshEdgeFlangeAuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != FreshEdgeFlangeAuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"Fresh EdgeFlange authoring requires folder count " +
                $"{FreshEdgeFlangeAuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, FreshEdgeFlangeAuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring requires a stripped base without an existing edge flange. " +
                "No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, normalizedPrefixSha,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring requires the matching stripped base: normalized prefix " +
                $"SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{normalizedPrefixSha.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(FreshEdgeFlangeAuthoringSegments.FolderCount).CopyTo(newStream, 4);

        int Base(int offset) => currentStream.Length + offset;
        int radiusOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionRadiusOffset
            : FreshEdgeFlangeAuthoringSegments.RadiusOffset;
        int defaultRadiusOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUseDefaultBendRadiusOffset
            : FreshEdgeFlangeAuthoringSegments.UseDefaultBendRadiusOffset;
        int widthOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionReliefWidthOffset
            : FreshEdgeFlangeAuthoringSegments.ReliefWidthOffset;
        int depthOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionReliefDepthOffset
            : FreshEdgeFlangeAuthoringSegments.ReliefDepthOffset;
        int ratioOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionReliefRatioOffset
            : FreshEdgeFlangeAuthoringSegments.ReliefRatioOffset;
        int useRatioOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUseReliefRatioOffset
            : FreshEdgeFlangeAuthoringSegments.UseReliefRatioOffset;
        int usePositionOffsetOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUsePositionOffsetOffset
            : FreshEdgeFlangeAuthoringSegments.UsePositionOffsetOffset;
        int positionDistanceOffset = FreshEdgeFlangeAuthoringSegments.PositionOffsetDistanceOffset;
        int defaultReliefOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionUseDefaultBendReliefOffset
            : FreshEdgeFlangeAuthoringSegments.UseDefaultBendReliefOffset;
        int offsetDistanceOffset = positionTemplate
            ? FreshEdgeFlangeAuthoringSegments.PositionOffsetDistanceD7Offset
            : FreshEdgeFlangeAuthoringSegments.OffsetDistanceOffset;

        double radius = def.BendRadius ??
            (radiusTemplate ? 0.003 : (def.UseDefaultBendRadius == false ? 0.0 : 0.002));
        BitConverter.GetBytes(radius).CopyTo(newStream, Base(radiusOffset));
        newStream[Base(defaultRadiusOffset)] =
            (byte)((def.UseDefaultBendRadius ?? !radiusTemplate) ? 1 : 0);
        BitConverter.GetBytes(def.ReliefWidth ?? 0.0005)
            .CopyTo(newStream, Base(widthOffset));
        BitConverter.GetBytes(def.ReliefDepth ?? 0.0005)
            .CopyTo(newStream, Base(depthOffset));
        BitConverter.GetBytes(def.ReliefRatio ?? 0.5)
            .CopyTo(newStream, Base(ratioOffset));
        newStream[Base(useRatioOffset)] =
            (byte)((def.UseReliefRatio ?? true) ? 1 : 0);
        BitConverter.GetBytes(def.UsePositionOffset == true ? 1u : 0u)
            .CopyTo(newStream, Base(usePositionOffsetOffset));
        BitConverter.GetBytes(def.PositionOffsetDistance ?? 0.01)
            .CopyTo(newStream, Base(positionDistanceOffset));
        newStream[Base(defaultReliefOffset)] =
            (byte)((def.UseDefaultBendRelief ?? true) ? 1 : 0);
        if (def.OffsetDistance.HasValue)
            BitConverter.GetBytes(def.OffsetDistance.Value)
                .CopyTo(newStream, Base(offsetDistanceOffset));
        return newStream;
    }

    private static void ValidateDefinition(EdgeFlangeFeatureDefinitionData def)
    {
        if (def.BendAngle.HasValue && Math.Abs(def.BendAngle.Value - Math.PI / 2.0) > 1e-12)
            throw Unsupported(nameof(def.BendAngle), def.BendAngle.Value);
        RequireNullOrEquals(def.EdgeCount, 1, nameof(def.EdgeCount));
        RequireNullOrEquals(def.PositionType, 3, nameof(def.PositionType));
        RequireNullOrEquals(def.ReliefRatio, 0.5, nameof(def.ReliefRatio));
        if (def.PositionOffsetType == 2)
            throw new NotSupportedException(
                "Fresh EdgeFlange authoring cannot persist PositionOffsetType=2: native readback " +
                "reverts it to 1. No stream changes have been made.");
        RequireNullOrEquals(def.PositionOffsetType, 1, nameof(def.PositionOffsetType));

        RequireNull(def.Edge, nameof(def.Edge));
        RequireNull(def.Edges, nameof(def.Edges));
        RequireNull(def.OffsetReference, nameof(def.OffsetReference));
        RequireNull(def.PositionOffsetReference, nameof(def.PositionOffsetReference));
        RequireNull(def.AngleReference, nameof(def.AngleReference));
        RequireNull(def.CustomBendAllowance, nameof(def.CustomBendAllowance));
        RequireNull(def.OffsetType, nameof(def.OffsetType));
        RequireNull(def.OffsetDimType, nameof(def.OffsetDimType));
        RequireNull(def.ReverseOffset, nameof(def.ReverseOffset));
        RequireNull(def.UsePositionTrimSideBends, nameof(def.UsePositionTrimSideBends));
        RequireNull(def.ReversePositionOffset, nameof(def.ReversePositionOffset));
        RequireNull(def.AutoReliefType, nameof(def.AutoReliefType));
        RequireNull(def.ReliefTearType, nameof(def.ReliefTearType));
        RequireNull(def.UseDefaultBendAllowance, nameof(def.UseDefaultBendAllowance));
        RequireNull(def.GapDistance, nameof(def.GapDistance));
        RequireNull(def.LockAngle, nameof(def.LockAngle));
        RequireNull(def.PerpendicularToFace, nameof(def.PerpendicularToFace));
        RequireNull(def.NormalToFlangePlane, nameof(def.NormalToFlangePlane));
        RequireNull(def.PositionReferenceType, nameof(def.PositionReferenceType));

        ValidatePositiveOrZero(def.BendRadius, nameof(def.BendRadius));
        ValidatePositive(def.ReliefWidth, nameof(def.ReliefWidth));
        ValidatePositive(def.ReliefDepth, nameof(def.ReliefDepth));
        ValidatePositive(def.PositionOffsetDistance, nameof(def.PositionOffsetDistance));
        ValidatePositive(def.OffsetDistance, nameof(def.OffsetDistance));

        if (def.UseDefaultBendRadius == true && def.BendRadius.HasValue &&
            Math.Abs(def.BendRadius.Value - 0.002) > 1e-12)
            throw Unsupported(nameof(def.BendRadius), def.BendRadius.Value);
    }

    private static void ValidatePositiveOrZero(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            value.Value < 0 || value.Value > 0.1)
            throw Unsupported(property, value.Value);
    }

    private static void ValidatePositive(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            value.Value <= 0 || value.Value > 0.1)
            throw Unsupported(property, value.Value);
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null) throw Unsupported(property, value);
    }

    private static void RequireNullOrEquals(int? value, int expected, string property)
    {
        if (value.HasValue && value.Value != expected) throw Unsupported(property, value.Value);
    }

    private static void RequireNullOrEquals(double? value, double expected, string property)
    {
        if (value.HasValue && Math.Abs(value.Value - expected) > 1e-12)
            throw Unsupported(property, value.Value);
    }

    private static NotSupportedException Unsupported(string property, object value) =>
        new($"Fresh EdgeFlange authoring does not support {property}={value}. " +
            "No stream changes have been made.");

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
