using System;
using System.Security.Cryptography;
using System.Text;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed-template authoring for the tutorial BaseFlange custom bend table.
/// The path is replaced in-place with a same-length file name (measured path is
/// 98 characters); the rest of the measured BaseFlange suffix is appended
/// verbatim. Native reopen reports the patched path from both
/// ISheetMetalFeatureData and IBaseFlangeFeatureData.
/// </summary>
internal static class TutorialBendTableAuthoringBuilder
{
    internal static byte[] Build(
        byte[] currentStream, string bendTableFile, string? featureName)
    {
        if (string.IsNullOrWhiteSpace(bendTableFile))
            throw new ArgumentException("A bend table path is required.", nameof(bendTableFile));
        if (bendTableFile.Length != TutorialBendTableAuthoringSegments.MeasuredPathLength)
            throw new NotSupportedException(
                $"Tutorial bend table authoring requires a same-length path " +
                $"({TutorialBendTableAuthoringSegments.MeasuredPathLength} characters), but received " +
                $"{bendTableFile.Length}. No stream changes have been made.");
        if (!bendTableFile.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) &&
            !bendTableFile.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !bendTableFile.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Tutorial bend table authoring requires a .xls/.xlsx/.csv path. " +
                "No stream changes have been made.");

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? TutorialBendTableAuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, TutorialBendTableAuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Tutorial bend table authoring supports only the measured display name " +
                $"'{TutorialBendTableAuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null ||
            currentStream.Length != TutorialBendTableAuthoringSegments.PrefixLength)
            throw new NotSupportedException(
                $"Tutorial bend table authoring requires the stripped base of Paper Airplane.SLDPRT " +
                $"({TutorialBendTableAuthoringSegments.PrefixLength} bytes, ending at the " +
                $"{TutorialBendTableAuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != TutorialBendTableAuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"Tutorial bend table authoring supports only ResolvedFeatures version " +
                $"0x{TutorialBendTableAuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != TutorialBendTableAuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"Tutorial bend table authoring requires folder count " +
                $"{TutorialBendTableAuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, TutorialBendTableAuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "Tutorial bend table authoring requires a stripped base without an existing base " +
                "flange. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, TutorialBendTableAuthoringSegments.NormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Tutorial bend table authoring requires the matching stripped base: normalized " +
                $"prefix SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{TutorialBendTableAuthoringSegments.NormalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] suffix = TutorialBendTableAuthoringSegments.GetSuffix();
        byte[] prefix = (byte[])currentStream.Clone();
        byte[] oldBytes = Encoding.Unicode.GetBytes(
            TutorialBendTableAuthoringSegments.MeasuredBendTablePath);
        byte[] newBytes = Encoding.Unicode.GetBytes(bendTableFile);
        int replaced = ReplacePath(prefix, oldBytes, newBytes) +
                       ReplacePath(suffix, oldBytes, newBytes);
        if (replaced == 0)
            throw new InvalidOperationException(
                "Measured tutorial bend table suffix does not contain the measured path.");

        byte[] newStream = new byte[prefix.Length + suffix.Length];
        Array.Copy(prefix, 0, newStream, 0, prefix.Length);
        Array.Copy(suffix, 0, newStream, prefix.Length, suffix.Length);
        BitConverter.GetBytes(TutorialBendTableAuthoringSegments.FolderCount).CopyTo(newStream, 4);
        return newStream;
    }

    private static int ReplacePath(byte[] data, byte[] oldBytes, byte[] newBytes)
    {
        int replaced = 0;
        for (int p = 0; p <= data.Length - oldBytes.Length; p++)
        {
            bool match = true;
            for (int j = 0; j < oldBytes.Length; j++)
                if (data[p + j] != oldBytes[j]) { match = false; break; }
            if (!match) continue;
            Array.Copy(newBytes, 0, data, p, newBytes.Length);
            replaced++;
            p += oldBytes.Length - 1;
        }
        return replaced;
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
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
