using System;
using System.Linq;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured authoring for the official CHM recipe artifacts. Each template is the
/// exact tail feature block from a native official example (Rib/Combine/Split/SurfaceTrim);
/// only the exact stripped base (length, stream version, folder count and normalized SHA)
/// is accepted. The block is appended verbatim and only the header folder count is patched.
/// No parameter is re-synthesized: unsupported options/names are rejected before any write.
/// </summary>
internal static class OfficialChmFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, string className, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (string.IsNullOrEmpty(className))
            throw new ArgumentException("A feature class name is required.", nameof(className));

        var template = SelectTemplate(currentStream, className);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Official CHM measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, className) > 0)
        {
            throw new NotSupportedException(
                $"The document already contains a {className} feature. No stream changes have been made.");
        }

        var newStream = new byte[currentStream.Length + template.Block.Length];
        if (template.InsertOffset >= 0)
        {
            if (template.InsertOffset > currentStream.Length)
                throw new NotSupportedException(
                    $"The measured insert offset 0x{template.InsertOffset:X} is beyond the current stream. No stream changes have been made.");
            Array.Copy(currentStream, 0, newStream, 0, template.InsertOffset);
            Array.Copy(template.Block, 0, newStream, template.InsertOffset, template.Block.Length);
            Array.Copy(currentStream, template.InsertOffset, newStream,
                template.InsertOffset + template.Block.Length, currentStream.Length - template.InsertOffset);
        }
        else
        {
            Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
            Array.Copy(template.Block, 0, newStream, currentStream.Length, template.Block.Length);
        }
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        return (newStream, template.Name);
    }

    private static OfficialChmAuthoringTemplate SelectTemplate(byte[] currentStream, string className)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        string? normalizedSha = null;
        foreach (var template in OfficialChmAuthoringTemplates.All.Concat(OfficialGussetInPlaceTemplates.All))
        {
            if (!string.Equals(template.ClassName, className, StringComparison.Ordinal))
                continue;
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (folderCount != template.StrippedFolderCount)
                continue;

            if (normalizedSha == null)
            {
                byte[] normalized = (byte[])currentStream.Clone();
                BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
                using var hash = SHA256.Create();
                normalizedSha = BitConverter.ToString(hash.ComputeHash(normalized))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(normalizedSha, template.BaseSha256, StringComparison.Ordinal))
                return template;
        }

        throw new NotSupportedException(
            $"The document is not one of the official CHM stripped bases for {className} " +
            $"(stream length {currentStream.Length}, version 0x{version:X}, folder count {folderCount}). " +
            "No stream changes have been made.");
    }
}
