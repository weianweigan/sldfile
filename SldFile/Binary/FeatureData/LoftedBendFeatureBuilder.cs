using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured lofted-bend authoring for the controlled non-formed 0x72
/// specimen (LoftedBendOptions/lofted_bend_2lines.SLDPRT, version 0x74).
/// <para>
/// The stripped base keeps the sheet-metal body and the two line profiles; the
/// measured suffix starts at the moLoftedBend_c tag (0x6043). Only the measured
/// combination is accepted: Thickness 0.001 m, NumberOfBendLines 2 and the two
/// fixed line profiles. Every other API value may be null (unresolved) or equal
/// the measured create snapshot value; other values, persistent profile
/// references, custom names, unsupported stream versions and non-matching bases
/// are rejected before the stream is touched.
/// </para>
/// </summary>
internal static class LoftedBendFeatureBuilder
{
    internal const string DefaultFeatureName = LoftedBendAuthoringSegments.MeasuredDisplayName;

    private const double MeasuredMaximumDeviation = 0.0005;
    private const double MeasuredFacetValue = 0.001;
    private const int MeasuredProfileCount = 2;

    internal static void ValidateDefinition(LoftedBendFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        RequireNullOrClose(def.Thickness, LoftedBendAuthoringSegments.MeasuredThickness,
            nameof(def.Thickness));
        RequireNullOrEquals(def.NumberOfBendLines,
            LoftedBendAuthoringSegments.MeasuredNumberOfBendLines, nameof(def.NumberOfBendLines));

        // The two line profiles are fixed in the stripped prefix. A caller cannot
        // substitute other profiles, and the measured layout has exactly two.
        RequireNull(def.Profiles, nameof(def.Profiles));
        RequireNullOrEquals(def.ProfileCount, MeasuredProfileCount, nameof(def.ProfileCount));

        // Values below are constant in the measured create snapshot. They are not
        // patched (their serialized slots are unresolved), so only the measured
        // value can be accepted; any other value would silently not take effect.
        RequireNullOrEquals(def.Direction, false, nameof(def.Direction));
        RequireNullOrEquals(def.BendLineControlOption, 0, nameof(def.BendLineControlOption));
        RequireNullOrEquals(def.ReferToEndPoint, false, nameof(def.ReferToEndPoint));
        RequireNullOrEquals(def.FacetingOption, 0, nameof(def.FacetingOption));
        RequireNullOrEquals(def.FormedMethod, false, nameof(def.FormedMethod));
        RequireNullOrEquals(def.SymmetricThickness, false, nameof(def.SymmetricThickness));
        RequireNullOrClose(def.MaximumDeviation, MeasuredMaximumDeviation,
            nameof(def.MaximumDeviation));
        RequireNullOrClose(def.FacetValue, MeasuredFacetValue, nameof(def.FacetValue));
    }

    internal static byte[] Build(
        byte[] currentStream, LoftedBendFeatureDefinitionData def, string? featureName)
    {
        ValidateDefinition(def);

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? DefaultFeatureName
            : featureName!;
        if (!string.Equals(resolvedName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; received '{resolvedName}'. No stream changes have been made.");
        }

        if (currentStream == null ||
            currentStream.Length != LoftedBendAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring requires the stripped base prefix of the controlled specimen " +
                $"({LoftedBendAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != LoftedBendAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring is only supported for ResolvedFeatures streams version " +
                $"0x{LoftedBendAuthoringSegments.SupportedStreamVersion:X2}, but the document uses " +
                $"0x{version:X2}. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) !=
            LoftedBendAuthoringSegments.MeasuredFullFolderCount)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring requires the measured folder count " +
                $"{LoftedBendAuthoringSegments.MeasuredFullFolderCount}. No stream changes have been made.");
        }

        int marker = LoftedBendAuthoringSegments.MarkerOffset;
        if (marker < 0 || marker + 8 > currentStream.Length ||
            currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) !=
                LoftedBendAuthoringSegments.MeasuredMarkerValue)
        {
            throw new NotSupportedException(
                "Lofted-bend authoring requires the measured 0x67/marker block at " +
                $"0x{marker:X}. No stream changes have been made.");
        }

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string normalizedHash = Sha256Hex(normalized);
        if (!string.Equals(normalizedHash,
                LoftedBendAuthoringSegments.MeasuredNormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring requires the controlled stripped base: normalized prefix " +
                $"SHA-256 {normalizedHash.Substring(0, 12)} does not match the measured signature. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, LoftedBendFeatureDefinitionData.SerializedClassName) != 0)
        {
            throw new NotSupportedException(
                "Lofted-bend authoring requires a stripped base without an existing lofted bend. " +
                "No stream changes have been made.");
        }

        byte[] suffix = LoftedBendAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        BitConverter.GetBytes(LoftedBendAuthoringSegments.MeasuredFullFolderCount)
            .CopyTo(newStream, 4);
        BitConverter.GetBytes(LoftedBendAuthoringSegments.MeasuredMarkerValue)
            .CopyTo(newStream, marker + 4);
        return newStream;
    }

    private static void RequireNullOrClose(double? value, double measured, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            Math.Abs(value.Value - measured) > 1e-12)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring does not support {property}={value.Value:R}. " +
                $"The measured template value is {measured:R}. No stream changes have been made.");
        }
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring does not support {property}={value.Value}. " +
                $"The measured template value is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Lofted-bend authoring does not support {property}. No stream changes have been made.");
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
