using System;
using System.Security.Cryptography;
using System.Text;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured authoring for the tutorial Jog/Fold/UnFold samples and the
/// newer FlatPattern layouts (2026-10-31 corpus scan). The stripped base must
/// end at the feature tag and match the template's length, stream version,
/// folder count and normalized prefix SHA-256; the measured suffix is appended
/// verbatim, so the authored ResolvedFeatures stream is byte-identical to the
/// native tutorial document. Custom names, another template's base and
/// non-stripped documents are rejected before the stream is modified.
/// </summary>
internal static class TutorialSheetMetalFeatureBuilder
{
    internal static TutorialSheetMetalAuthoringSegments.Template ToSegmentTemplate(
        TutorialSheetMetalFeatureTemplate template) => template switch
    {
        TutorialSheetMetalFeatureTemplate.JogComplexPart =>
            TutorialSheetMetalAuthoringSegments.Template.JogComplexPart,
        TutorialSheetMetalFeatureTemplate.UnFoldPaperAirplane =>
            TutorialSheetMetalAuthoringSegments.Template.UnFoldPaperAirplane,
        TutorialSheetMetalFeatureTemplate.FoldPaperAirplane =>
            TutorialSheetMetalAuthoringSegments.Template.FoldPaperAirplane,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    internal static byte[] Build(
        byte[] currentStream,
        TutorialSheetMetalFeatureTemplate template,
        string? featureName)
    {
        var segmentTemplate = ToSegmentTemplate(template);
        var info = TutorialSheetMetalAuthoringSegments.GetInfo(segmentTemplate);
        return BuildMeasured(currentStream, info.Name, info.ClassName, info.DisplayName,
            info.PrefixLength, info.StreamVersion, info.FolderCount,
            info.NormalizedPrefixSha256,
            TutorialSheetMetalAuthoringSegments.GetSuffix(segmentTemplate), featureName);
    }

    internal static byte[] BuildFlatPattern(
        byte[] currentStream,
        TutorialSheetMetalFeatureTemplate template,
        string? featureName)
    {
        var segmentTemplate = ToFlatPatternSegmentTemplate(template);
        var info = TutorialFlatPatternAuthoringSegments.GetInfo(segmentTemplate);
        return BuildMeasured(currentStream, info.Name, info.ClassName, info.DisplayName,
            info.PrefixLength, info.StreamVersion, info.FolderCount,
            info.NormalizedPrefixSha256,
            TutorialFlatPatternAuthoringSegments.GetSuffix(segmentTemplate), featureName);
    }

    internal static TutorialFlatPatternAuthoringSegments.Template ToFlatPatternSegmentTemplate(
        TutorialSheetMetalFeatureTemplate template) => template switch
    {
        TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane =>
            TutorialFlatPatternAuthoringSegments.Template.PaperAirplane,
        TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart =>
            TutorialFlatPatternAuthoringSegments.Template.ComplexPart,
        TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled =>
            TutorialFlatPatternAuthoringSegments.Template.DriveSled,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static byte[] BuildMeasured(
        byte[] currentStream,
        string templateName,
        string className,
        string displayName,
        int prefixLength,
        ushort streamVersion,
        ushort folderCount,
        string normalizedPrefixSha256,
        byte[] suffix,
        string? featureName)
    {
        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? displayName
            : featureName!;
        if (!string.Equals(resolvedName, displayName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' supports only the measured display " +
                $"name '{displayName}'; received '{resolvedName}'. No stream changes have been made.");

        if (currentStream == null || currentStream.Length != prefixLength)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires the stripped base of the " +
                $"matching tutorial document ({prefixLength} bytes, ending at the " +
                $"{className} tag), but received {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != streamVersion)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' is only supported for ResolvedFeatures " +
                $"version 0x{streamVersion:X}, but the document uses 0x{version:X}. " +
                "No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != folderCount)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires folder count {folderCount}. " +
                "No stream changes have been made.");

        if (CountOccurrences(currentStream, className) != 0)
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires a stripped base without an " +
                $"existing {className} feature. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string normalizedHash = Sha256Hex(normalized);
        if (!string.Equals(normalizedHash, normalizedPrefixSha256, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"Tutorial feature template '{templateName}' requires the matching stripped base: " +
                $"normalized prefix SHA-256 {normalizedHash.Substring(0, 12)} does not match the " +
                $"measured signature {normalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(folderCount).CopyTo(newStream, 4);
        return newStream;
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
