using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured flat-pattern authoring for the controlled 0x5D specimen
/// (sm_base_flange_10mm.SLDPRT, version 0x73).
/// <para>
/// The stripped base keeps the base flange, sheet-metal template and
/// flat-pattern template context; the measured suffix starts at the
/// moFlatPattern_c tag. Only the same fixed face and default option combination
/// as the source is accepted. Custom names, altered options, unsupported stream
/// versions and non-matching bases are rejected before the stream is touched.
/// </para>
/// </summary>
internal static class FlatPatternFeatureBuilder
{
    internal const string DefaultFeatureName = FlatPatternAuthoringSegments.MeasuredDisplayName;

    internal static void ValidateDefinition(FlatPatternFeatureDefinitionData? description)
    {
        if (description == null) return;

        // Fixed persistent selection comes from the measured suffix.
        RequireNull(description.FixedFace, nameof(description.FixedFace));
        RequireNull(description.FixedFace2, nameof(description.FixedFace2));
        RequireNull(description.ExcludedFaces, nameof(description.ExcludedFaces));

        if (description.BreakCornerType.HasValue &&
            (description.BreakCornerType.Value < 0 || description.BreakCornerType.Value > 1))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring supports BreakCornerType 0 (fillet) or 1 (chamfer), " +
                $"but received {description.BreakCornerType.Value}. No stream changes have been made.");
        }
        ValidatePositiveOptional(description.BreakCornerRadius, nameof(description.BreakCornerRadius));

        if (description.CornerTrimReliefType.HasValue &&
            (description.CornerTrimReliefType.Value < 0 || description.CornerTrimReliefType.Value > 9))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring supports CornerTrimReliefType 0..9, " +
                $"but received {description.CornerTrimReliefType.Value}. No stream changes have been made.");
        }
        ValidatePositiveOptional(description.CornerTrimReliefDistance,
            nameof(description.CornerTrimReliefDistance));

        RequireNullOrEquals(description.UseRatioToThickness, false, nameof(description.UseRatioToThickness));
        RequireNull(description.CornerTrimRatioToThickness, nameof(description.CornerTrimRatioToThickness));
        RequireNull(description.ShowSlitInCornerRelief, nameof(description.ShowSlitInCornerRelief));
        RequireNull(description.AddCornerTrim, nameof(description.AddCornerTrim));
        RequireNull(description.BreakCorners, nameof(description.BreakCorners));
    }

    internal static byte[] Build(
        byte[] currentStream, FlatPatternFeatureDefinitionData description, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? DefaultFeatureName
            : featureName!;
        if (!string.Equals(resolvedName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; received '{resolvedName}'. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != FlatPatternAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring is only supported for ResolvedFeatures streams version " +
                $"0x{FlatPatternAuthoringSegments.SupportedStreamVersion:X2}, but the document uses " +
                $"0x{version:X2}. No stream changes have been made.");
        }

        if (currentStream.Length != FlatPatternAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires the stripped base prefix of the controlled specimen " +
                $"({FlatPatternAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream.Length} bytes. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) != FlatPatternAuthoringSegments.MeasuredFullFolderCount)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires the measured folder count " +
                $"{FlatPatternAuthoringSegments.MeasuredFullFolderCount}. No stream changes have been made.");
        }

        int marker = FlatPatternAuthoringSegments.MarkerOffset;
        if (marker < 0 || marker + 8 > currentStream.Length ||
            currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) !=
                FlatPatternAuthoringSegments.MeasuredMarkerValue)
        {
            throw new NotSupportedException(
                "Flat-pattern authoring requires the measured 0x67/marker block at " +
                $"0x{marker:X}. No stream changes have been made.");
        }

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string normalizedHash = Sha256Hex(normalized);
        if (!string.Equals(normalizedHash,
                FlatPatternAuthoringSegments.MeasuredNormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires the controlled stripped base: normalized prefix " +
                $"SHA-256 {normalizedHash.Substring(0, 12)} does not match the measured signature. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moFlatPattern_c") != 0)
        {
            throw new NotSupportedException(
                "Flat-pattern authoring requires a stripped base without an existing flat pattern. " +
                "No stream changes have been made.");
        }

        byte[] suffix = FlatPatternAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        BitConverter.GetBytes(FlatPatternAuthoringSegments.MeasuredFullFolderCount).CopyTo(newStream, 4);
        BitConverter.GetBytes(FlatPatternAuthoringSegments.MeasuredMarkerValue).CopyTo(newStream, marker + 4);

        int optionAnchor = currentStream.Length +
            FlatPatternAuthoringSegments.OptionAnchorSuffixOffset;
        PatchOptions(newStream, optionAnchor, description);
        return newStream;
    }

    private const int OptionFlags = 0x21;
    private const int OptionTrimType = 0x37;
    private const int OptionTrimDistance = 0x3B;
    private const int OptionBreakType = 0x43;
    private const int OptionBreakRadius = 0x47;
    private const int OptionSimplifyBends = 0x57;

    private static void PatchOptions(
        byte[] data, int anchor, FlatPatternFeatureDefinitionData description)
    {
        if (anchor < 0 || anchor + 0x64 > data.Length)
            throw new InvalidOperationException(
                $"Flat-pattern option block at 0x{anchor:X} is outside the stream.");

        if (data[anchor] != 0xFF || data[anchor + 1] != 0xFF ||
            BitConverter.ToUInt32(data, anchor + 0x16) != 0x4A38u ||
            BitConverter.ToUInt32(data, anchor + 0x1C) != 1u ||
            data[anchor + 0x2B] != 1 || data[anchor + 0x5B] != 1)
        {
            throw new NotSupportedException(
                "The measured flat-pattern option layout gates do not match. " +
                "No stream changes have been made.");
        }

        if (data[anchor + OptionFlags] != 1 ||
            BitConverter.ToUInt32(data, anchor + OptionTrimType) != 0u ||
            BitConverter.ToUInt32(data, anchor + OptionBreakType) != 0u ||
            data[anchor + OptionSimplifyBends] != 1 ||
            BitConverter.ToDouble(data, anchor + OptionTrimDistance) != 0.005 ||
            BitConverter.ToDouble(data, anchor + OptionBreakRadius) != 0.005)
        {
            throw new NotSupportedException(
                "The measured flat-pattern option defaults do not match the template. " +
                "No stream changes have been made.");
        }

        byte flags = (byte)(((description.MergeFace ?? true) ? 1 : 0) |
                            ((description.CornerTreatment ?? true) ? 0 : 2));
        data[anchor + OptionFlags] = flags;

        uint breakType = checked((uint)(description.BreakCornerType ?? 0));
        PatchUInt32(data, anchor + OptionBreakType, 0u, breakType, "BreakCornerType");
        PatchDouble(data, anchor + OptionBreakRadius, 0.005,
            description.BreakCornerRadius ?? 0.005, "BreakCornerRadius");

        uint trimType = checked((uint)(description.CornerTrimReliefType ?? 0));
        PatchUInt32(data, anchor + OptionTrimType, 0u, trimType, "CornerTrimReliefType");
        PatchDouble(data, anchor + OptionTrimDistance, 0.005,
            description.CornerTrimReliefDistance ?? 0.005, "CornerTrimReliefDistance");

        data[anchor + OptionSimplifyBends] =
            (byte)((description.SimplifyBends ?? true) ? 1 : 0);
    }

    private static void PatchUInt32(
        byte[] data, int offset, uint expected, uint value, string slotName)
    {
        uint current = BitConverter.ToUInt32(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Flat-pattern {slotName} slot at 0x{offset:X} contains {current}, " +
                $"expected {expected}. No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void PatchDouble(
        byte[] data, int offset, double expected, double value, string slotName)
    {
        double current = BitConverter.ToDouble(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Flat-pattern {slotName} slot at 0x{offset:X} contains {current:R}, " +
                $"expected {expected:R}. No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring does not yet support {property}={value.Value}. " +
                $"The measured template value is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring does not yet support {property}. No stream changes have been made.");
        }
    }

    private static void ValidatePositiveOptional(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0.0)
        {
            throw new NotSupportedException(
                $"Flat-pattern authoring requires {property} to be a positive finite number, " +
                $"but received {value.Value:R}. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
