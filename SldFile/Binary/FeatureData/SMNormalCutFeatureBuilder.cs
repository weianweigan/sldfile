using System;
using System.Security.Cryptography;
using System.Text;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed authoring for the controlled cross-bend SMNormalCut sample
/// (ResolvedFeatures 0x75, TypeId 0x87). The measured suffix from the
/// moSMNormalCut_c tag to the stream end is appended verbatim; no parameter is
/// patched because only one native sample exists.
/// </summary>
internal static class SMNormalCutFeatureBuilder
{
    internal static byte[] Build(byte[] currentStream, string? featureName)
    {
        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? SMNormalCutAuthoringSegments.DisplayName
            : featureName!;
        if (!string.Equals(resolvedName, SMNormalCutAuthoringSegments.DisplayName,
                StringComparison.Ordinal))
            throw new NotSupportedException(
                $"SMNormalCut template authoring supports only the measured display name " +
                $"'{SMNormalCutAuthoringSegments.DisplayName}'; received '{resolvedName}'. " +
                "No stream changes have been made.");

        if (currentStream == null ||
            currentStream.Length != SMNormalCutAuthoringSegments.PrefixLength)
            throw new NotSupportedException(
                $"SMNormalCut template authoring requires the stripped base " +
                $"({SMNormalCutAuthoringSegments.PrefixLength} bytes, ending at the " +
                $"{SMNormalCutAuthoringSegments.ClassName} tag), but received " +
                $"{currentStream?.Length ?? 0} bytes. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 0) != SMNormalCutAuthoringSegments.StreamVersion)
            throw new NotSupportedException(
                $"SMNormalCut template authoring supports only ResolvedFeatures version " +
                $"0x{SMNormalCutAuthoringSegments.StreamVersion:X}, but the document uses " +
                $"0x{BitConverter.ToUInt16(currentStream, 0):X}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) != SMNormalCutAuthoringSegments.FolderCount)
            throw new NotSupportedException(
                $"SMNormalCut template authoring requires folder count " +
                $"{SMNormalCutAuthoringSegments.FolderCount}. No stream changes have been made.");

        if (CountOccurrences(currentStream, SMNormalCutAuthoringSegments.ClassName) != 0)
            throw new NotSupportedException(
                "SMNormalCut template authoring requires a stripped base without an existing " +
                "SMNormalCut feature. No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        if (!string.Equals(hash, SMNormalCutAuthoringSegments.NormalizedPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "SMNormalCut template authoring requires the matching stripped base: normalized " +
                $"prefix SHA-256 {hash.Substring(0, 12)} does not match the measured signature " +
                $"{SMNormalCutAuthoringSegments.NormalizedPrefixSha256.Substring(0, 12)}. " +
                "No stream changes have been made.");

        byte[] suffix = SMNormalCutAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        BitConverter.GetBytes(SMNormalCutAuthoringSegments.FolderCount).CopyTo(newStream, 4);
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
                if (data[i + j] != pattern[j]) { match = false; break; }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
