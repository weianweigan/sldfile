using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed-combination hem authoring for the two controlled native specimens
/// (SheetMetalFormingOptions/sheetmetal_hem_open and sheetmetal_hem_closed).
/// <para>
/// The base prefix (before the moHem_c feature tag, 0x4D35) is hash-pinned per
/// template, and the matching measured suffix reconstructs the native stream
/// byte-for-byte. Only Type 0 (Open) and Type 1 (Closed) are accepted; teardrop,
/// rolled and double hems have no sample and are rejected. Length, gap, radius,
/// angle, miter gap, edge count and the three constant flags may be null or must
/// equal the measured template values; they are not patched, because no sample
/// varies them independently from Type.
/// </para>
/// </summary>
internal static class HemFeatureBuilder
{
    internal static void ValidateDefinition(HemFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.Type.HasValue || (def.Type.Value != 0 && def.Type.Value != 1))
        {
            throw new NotSupportedException(
                $"Hem authoring currently supports only Type=0 (Open) and Type=1 (Closed) " +
                $"from the controlled open/closed samples; received " +
                $"{def.Type?.ToString() ?? "null"}. Teardrop/rolled/double hems have no sample. " +
                "No stream changes have been made.");
        }

        int type = def.Type.Value;
        if (!HemAuthoringSegments.TryGetTemplate(
                type, def.Length, def.GapDistance, def.MiterGap, out _))
        {
            throw new NotSupportedException(
                $"Hem authoring supports only the measured combinations: " +
                $"Type=0 Length=0.005 Gap=0.0005 MiterGap=0.0005; " +
                $"Type=0 Length=0.010 Gap=0.0005 MiterGap=0.0005; " +
                $"Type=0 Length=0.005 Gap=0.001 MiterGap=0.0005; " +
                $"Type=0 Length=0.005 Gap=0.0005 MiterGap=0.001; " +
                $"Type=1 Length=0.005 Gap=0.0001 MiterGap=0.0005; and " +
                $"Type=1 Length=0.010 Gap=0.0001 MiterGap=0.0005. Received Type={type}, " +
                $"Length={def.Length?.ToString() ?? "default"}, " +
                $"GapDistance={def.GapDistance?.ToString() ?? "default"}, " +
                $"MiterGap={def.MiterGap?.ToString() ?? "default"}. No stream changes have been made.");
        }
        RequireNullOrClose(def.Angle, HemAuthoringSegments.MeasuredAngle, nameof(def.Angle));
        RequireNullOrClose(def.Radius, HemAuthoringSegments.MeasuredRadius, nameof(def.Radius));

        RequireNullOrEquals(def.BendPosition, 0, nameof(def.BendPosition));
        RequireNullOrEquals(def.ReverseDirection, false, nameof(def.ReverseDirection));
        RequireNullOrEquals(def.UseDefaultBendAllowance, true, nameof(def.UseDefaultBendAllowance));
        RequireNullOrEquals(def.EdgeCount, 1, nameof(def.EdgeCount));

        RequireNull(def.Edges, nameof(def.Edges));
        RequireNull(def.CustomBendAllowance, nameof(def.CustomBendAllowance));
    }

    internal static byte[] Build(
        byte[] currentStream, HemFeatureDefinitionData def, string? featureName)
    {
        ValidateDefinition(def);
        int type = def.Type!.Value;
        HemAuthoringSegments.TryGetTemplate(
            type, def.Length, def.GapDistance, def.MiterGap, out int template);
        string expectedName = HemAuthoringSegments.GetDisplayName(type);

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, expectedName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Hem authoring uses the measured display name '{expectedName}' for Type={type}; " +
                $"changing it to '{featureName}' would not match the measured block. " +
                "No stream changes have been made.");
        }

        if (currentStream == null || currentStream.Length != HemAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Hem authoring requires the stripped base prefix of the controlled hem specimen " +
                $"({HemAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != HemAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Hem authoring is only supported for ResolvedFeatures streams version " +
                $"0x{HemAuthoringSegments.SupportedStreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }

        string prefixHash = Sha256Hex(currentStream);
        string expectedPrefix = HemAuthoringSegments.GetTemplatePrefixSha256(template);
        if (!string.Equals(prefixHash, expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Hem authoring Type={type} Length={def.Length?.ToString() ?? "default"} " +
                $"GapDistance={def.GapDistance?.ToString() ?? "default"} " +
                $"MiterGap={def.MiterGap?.ToString() ?? "default"} requires the matching controlled " +
                $"stripped base specimen: prefix SHA-256 {prefixHash.Substring(0, 12)} does not match the " +
                $"measured signature {expectedPrefix.Substring(0, 12)}. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) != HemAuthoringSegments.MeasuredFolderCount)
        {
            throw new NotSupportedException(
                $"Hem authoring requires the measured folder count " +
                $"{HemAuthoringSegments.MeasuredFolderCount}. No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moHem_c") != 0)
        {
            throw new NotSupportedException(
                "Hem authoring requires a stripped base without an existing hem feature. " +
                "No stream changes have been made.");
        }

        byte[] suffix = HemAuthoringSegments.GetTemplateSuffix(template);
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        return newStream;
    }

    private static void RequireNullOrClose(double? value, double measured, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            Math.Abs(value.Value - measured) > 1e-12)
        {
            throw new NotSupportedException(
                $"Hem authoring does not yet support {property}={value.Value:R}. " +
                $"The measured template value is {measured:R}. No stream changes have been made.");
        }
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Hem authoring does not yet support {property}={value.Value}. " +
                $"The measured template value is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Hem authoring does not yet support {property}. No stream changes have been made.");
        }
    }

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
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
