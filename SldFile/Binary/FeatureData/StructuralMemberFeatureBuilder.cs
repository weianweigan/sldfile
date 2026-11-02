using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured authoring for the 2026 structural-member samples. The target must be
/// the exact stripped base of one of the controlled samples (matching stream version,
/// folder count and normalized SHA). The measured block is appended verbatim; profile
/// path, segment selection, corner treatment and rotation are the measured values.
/// </summary>
internal static class StructuralMemberFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, string? featureName, bool selected)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        var template = SelectTemplate(currentStream, selected);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Structural-member authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, StructuralMemberClassName) > 0)
        {
            throw new NotSupportedException(
                "The document already contains a structural member. No stream changes have been made.");
        }

        var newStream = new byte[currentStream.Length + template.Block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.Block.Length);
        return (newStream, template.Name);
    }

    internal const string StructuralMemberClassName = "moWeldMemberFeat_c";

    private static StructuralMemberAuthoringTemplate SelectTemplate(byte[] currentStream, bool selected)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in StructuralMemberAuthoringTemplates.All)
        {
            bool isSelected = template.Variant.StartsWith("Selected", StringComparison.Ordinal);
            if (isSelected != selected)
                continue;
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
            $"The document is not one of the controlled stripped structural-member bases " +
            $"({(selected ? "selected" : "plain")} variant, stream length {currentStream.Length}, " +
            $"version 0x{version:X2}). No stream changes have been made.");
    }
}
