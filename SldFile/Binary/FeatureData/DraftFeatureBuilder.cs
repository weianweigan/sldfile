using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured Draft authoring from the controlled 3-degree neutral-bottom sample.
/// The target must be the exact stripped base (stream 0x70, folder count 20, normalized
/// SHA). The measured block is appended verbatim; angle, faces and all options remain
/// the measured values. A different angle (no 6-degree sample) is not supported.
/// </summary>
internal static class DraftFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(byte[] currentStream, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        var template = SelectTemplate(currentStream);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Draft measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, DraftFeatureParser.ClassName) > 0)
        {
            throw new NotSupportedException(
                "The document already contains a moDraft_c feature. No stream changes have been made.");
        }

        var newStream = new byte[currentStream.Length + template.Block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.Block.Length);
        return (newStream, template.Name);
    }

    private static DraftAuthoringTemplate SelectTemplate(byte[] currentStream)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in DraftAuthoringTemplates.All)
        {
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
                continue;

            byte[] normalized = (byte[])currentStream.Clone();
            BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
            string sha;
            using (var hash = SHA256.Create())
                sha = BitConverter.ToString(hash.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
            if (string.Equals(sha, template.BaseSha256, StringComparison.Ordinal))
                return template;
        }

        throw new NotSupportedException(
            $"The document is not the controlled stripped Draft base " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}
