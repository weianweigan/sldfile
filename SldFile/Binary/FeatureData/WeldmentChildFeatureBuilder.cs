using System;
using System.Linq;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured authoring for the nested EndCap and cosmetic WeldBead nodes. The
/// controlled bases are the exact sample prefixes before the nested feature tag; the
/// measured block from that tag to stream end is appended verbatim. Folder count and
/// marker bytes are inherited from the base because these nodes are nested features.
/// </summary>
internal static class WeldmentChildFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, string className, string? featureName, bool? chamfer = null)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        var template = SelectTemplate(currentStream, className, featureName, chamfer);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"EndCap/WeldBead authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, className) > 0)
        {
            throw new NotSupportedException(
                $"The document already contains a {className} feature. No stream changes have been made.");
        }

        var newStream = new byte[currentStream.Length + template.Block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.Block.Length);
        return (newStream, template.Name);
    }

    private static WeldmentChildAuthoringTemplate SelectTemplate(
        byte[] currentStream, string className, string? featureName, bool? chamfer)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in WeldmentChildAuthoringTemplates.All)
        {
            if (!string.Equals(template.ClassName, className, StringComparison.Ordinal))
                continue;
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (!string.IsNullOrWhiteSpace(featureName) &&
                !string.Equals(template.Name, featureName, StringComparison.Ordinal))
                continue;
            if (chamfer.HasValue && className == "moEndCap_c")
            {
                bool isChamfer = template.Name.EndsWith("_chamfer", StringComparison.Ordinal);
                if (chamfer.Value != isChamfer)
                    continue;
            }
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(currentStream))
                .Replace("-", string.Empty).ToLowerInvariant();
            if (string.Equals(hash, template.BaseSha256, StringComparison.Ordinal))
                return template;
        }
        throw new NotSupportedException(
            $"The document is not one of the controlled stripped EndCap/WeldBead bases for {className} " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}
