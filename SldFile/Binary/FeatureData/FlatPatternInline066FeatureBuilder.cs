using System;
using System.Security.Cryptography;
using System.Text;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed-template authoring for the inline FlatPattern 0x66 layout
/// (sm_base_flange_after_fillet, ResolvedFeatures 0x74, folder count 25).
/// Only the proven slots are patchable; FixedFace/FixedFace2/ExcludedFaces are
/// rejected because the native API did not persist independent values, and
/// distance/ratio must be equal because the API serializes one shared double.
/// </summary>
internal static class FlatPatternInline066FeatureBuilder
{
    internal static byte[] Build(
        byte[] currentStream, FlatPatternFeatureDefinitionData definition, string? featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        ValidateDefinition(definition);

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? FlatPatternInline066AuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, FlatPatternInline066AuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring supports only the measured display name " +
                $"'{FlatPatternInline066AuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null ||
            currentStream.Length != FlatPatternInline066AuthoringSegments.PrefixLength)
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring requires the stripped base " +
                $"({FlatPatternInline066AuthoringSegments.PrefixLength} bytes, ending at the " +
                $"{FlatPatternInline066AuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != FlatPatternInline066AuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring supports only ResolvedFeatures version " +
                $"0x{FlatPatternInline066AuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != FlatPatternInline066AuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"0x66 flat-pattern authoring requires folder count " +
                $"{FlatPatternInline066AuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, FlatPatternInline066AuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "0x66 flat-pattern authoring requires a stripped base without an existing flat " +
                "pattern. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, FlatPatternInline066AuthoringSegments.NormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "0x66 flat-pattern authoring requires the matching stripped base: normalized prefix " +
                $"SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{FlatPatternInline066AuthoringSegments.NormalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] suffix = FlatPatternInline066AuthoringSegments.GetSuffix();
        int flagsOffset = FlatPatternInline066AuthoringSegments.FlagsOffset;
        int trimTypeOffset = FlatPatternInline066AuthoringSegments.TrimTypeOffset;
        int trimOffset = FlatPatternInline066AuthoringSegments.TrimOffset;
        int breakTypeOffset = FlatPatternInline066AuthoringSegments.BreakTypeOffset;
        int breakRadiusOffset = FlatPatternInline066AuthoringSegments.BreakRadiusOffset;
        int useRatioOffset = FlatPatternInline066AuthoringSegments.UseRatioOffset;
        int simplifyOffset = FlatPatternInline066AuthoringSegments.SimplifyOffset;
        int slitOffset = FlatPatternInline066AuthoringSegments.SlitOffset;
        if (suffix[flagsOffset] != 1 ||
            BitConverter.ToUInt32(suffix, trimTypeOffset) != 0 ||
            BitConverter.ToDouble(suffix, trimOffset) != 0.005 ||
            BitConverter.ToUInt32(suffix, breakTypeOffset) != 0 ||
            BitConverter.ToDouble(suffix, breakRadiusOffset) != 0.005 ||
            suffix[useRatioOffset] != 0 || suffix[simplifyOffset] != 1 || suffix[slitOffset] != 1)
            throw new NotSupportedException(
                "The measured 0x66 flat-pattern option defaults do not match the template. " +
                "No stream changes have been made.");

        byte flags = (byte)(((definition.MergeFace ?? true) ? 1 : 0) |
                            ((definition.CornerTreatment ?? true) ? 0 : 2));
        suffix[flagsOffset] = flags;
        BitConverter.GetBytes(checked((uint)(definition.CornerTrimReliefType ?? 0)))
            .CopyTo(suffix, trimTypeOffset);
        double trim = definition.CornerTrimReliefDistance ??
                      definition.CornerTrimRatioToThickness ?? 0.005;
        BitConverter.GetBytes(trim).CopyTo(suffix, trimOffset);
        BitConverter.GetBytes(checked((uint)(definition.BreakCornerType ?? 0)))
            .CopyTo(suffix, breakTypeOffset);
        BitConverter.GetBytes(definition.BreakCornerRadius ?? 0.005)
            .CopyTo(suffix, breakRadiusOffset);
        suffix[useRatioOffset] = (byte)((definition.UseRatioToThickness ?? false) ? 1 : 0);
        suffix[simplifyOffset] = (byte)((definition.SimplifyBends ?? true) ? 1 : 0);
        suffix[slitOffset] = (byte)((definition.ShowSlitInCornerRelief ?? true) ? 1 : 0);

        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(FlatPatternInline066AuthoringSegments.FolderCount).CopyTo(newStream, 4);
        return newStream;
    }

    private static void ValidateDefinition(FlatPatternFeatureDefinitionData definition)
    {
        RequireNull(definition.FixedFace, nameof(definition.FixedFace));
        RequireNull(definition.FixedFace2, nameof(definition.FixedFace2));
        RequireNull(definition.ExcludedFaces, nameof(definition.ExcludedFaces));
        RequireNull(definition.AddCornerTrim, nameof(definition.AddCornerTrim));
        RequireNull(definition.BreakCorners, nameof(definition.BreakCorners));

        if (definition.BreakCornerType.HasValue &&
            (definition.BreakCornerType.Value < 0 || definition.BreakCornerType.Value > 1))
            throw Unsupported(nameof(definition.BreakCornerType), definition.BreakCornerType.Value);
        if (definition.CornerTrimReliefType.HasValue &&
            (definition.CornerTrimReliefType.Value < 0 || definition.CornerTrimReliefType.Value > 9))
            throw Unsupported(nameof(definition.CornerTrimReliefType),
                definition.CornerTrimReliefType.Value);
        ValidatePositive(definition.BreakCornerRadius, nameof(definition.BreakCornerRadius), 0.1);
        ValidatePositive(definition.CornerTrimReliefDistance,
            nameof(definition.CornerTrimReliefDistance), 1000);
        ValidatePositive(definition.CornerTrimRatioToThickness,
            nameof(definition.CornerTrimRatioToThickness), 1000);
        if (definition.CornerTrimReliefDistance.HasValue &&
            definition.CornerTrimRatioToThickness.HasValue &&
            definition.CornerTrimReliefDistance.Value !=
            definition.CornerTrimRatioToThickness.Value)
            throw new NotSupportedException(
                "0x66 flat-pattern serialization stores one shared distance/ratio double; " +
                "CornerTrimReliefDistance and CornerTrimRatioToThickness must be equal. " +
                "No stream changes have been made.");
    }

    private static void ValidatePositive(double? value, string property, double max)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) ||
            value.Value <= 0 || value.Value > max)
            throw Unsupported(property, value.Value);
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null) throw Unsupported(property, value);
    }

    private static NotSupportedException Unsupported(string property, object value) =>
        new($"0x66 flat-pattern authoring does not support {property}={value}. " +
            "No stream changes have been made.");

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
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
