using System;
using System.Linq;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured authoring for the surface operation samples: knit, extend, offset and
/// fill. The target must be the exact stripped base (matching stream version, folder count
/// and normalized SHA). Blocks are appended verbatim; only Fill ResolutionControl is
/// parameterized in its measured header slot; all other options remain measured values.
/// </summary>
internal static class SurfaceOperationFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, string className, string? featureName)
        => Build(currentStream, className, featureName, fillResolutionControl: null);

    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, string className, string? featureName, int? fillResolutionControl)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (string.IsNullOrEmpty(className))
            throw new ArgumentException("A feature class name is required.", nameof(className));
        if (fillResolutionControl.HasValue &&
            fillResolutionControl.Value != 1 && fillResolutionControl.Value != 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fillResolutionControl), "Fill ResolutionControl must be 1 or 2.");
        }

        var template = SelectTemplate(currentStream, className);
        if (fillResolutionControl.HasValue && template.Kind != "FillSurface")
        {
            throw new NotSupportedException(
                "Fill ResolutionControl authoring is only supported for fill-surface templates. " +
                "No stream changes have been made.");
        }
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Surface operation authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, className) > 0)
        {
            throw new NotSupportedException(
                $"The document already contains a {className} feature. No stream changes have been made.");
        }

        var newStream = new byte[currentStream.Length + template.Block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.Block.Length);
        if (fillResolutionControl.HasValue)
        {
            PatchFillResolutionControl(newStream, currentStream.Length, fillResolutionControl.Value);
        }
        return (newStream, template.Name);
    }

    /// <summary>
    /// Patches the measured fill option byte at feature header +0x307. The surrounding
    /// pattern 00 F0 BF ... 82 is validated; only 0x41 (ResolutionControl=1) and
    /// 0x49 (ResolutionControl=2) are accepted.
    /// </summary>
    private static void PatchFillResolutionControl(byte[] stream, int blockStart, int resolutionControl)
    {
        const string className = "moFillRefSurface_c";
        if (blockStart < 0 || blockStart + 6 + className.Length > stream.Length ||
            stream[blockStart] != 0xFF || stream[blockStart + 1] != 0xFF ||
            stream[blockStart + 2] != 0x01 || stream[blockStart + 3] != 0x00 ||
            System.Text.Encoding.ASCII.GetString(stream, blockStart + 6, className.Length) != className)
        {
            throw new NotSupportedException(
                "The fill template block does not start with the measured class tag. No stream changes have been made.");
        }

        int dataStart = blockStart + 6 + className.Length;
        int limit = Math.Min(stream.Length, dataStart + 16);
        int offset = dataStart;
        while (offset <= limit - 3 && !SldFile.Mfc.MfcPrimitives.IsWCharPrefix(stream, offset))
            offset++;
        if (offset > limit - 3)
        {
            throw new NotSupportedException(
                "The fill template block has no display name. No stream changes have been made.");
        }
        int cursor = offset;
        if (SldFile.Mfc.MfcPrimitives.ReadWChar(stream, ref cursor, stream.Length) == null)
        {
            throw new NotSupportedException(
                "The fill template display name is malformed. No stream changes have been made.");
        }
        int header = cursor;
        if (header + 0x309 > stream.Length ||
            BitConverter.ToUInt32(stream, header + 0x08) != 0x4A ||
            stream[header + 0x10] != 0xFF || stream[header + 0x11] != 0xFE ||
            stream[header + 0x12] != 0xFF || stream[header + 0x13] != 0x00 ||
            stream[header + 0x300] != 0x00 || stream[header + 0x301] != 0xF0 ||
            stream[header + 0x302] != 0xBF || stream[header + 0x308] != 0x82 ||
            (stream[header + 0x307] != 0x41 && stream[header + 0x307] != 0x49))
        {
            throw new NotSupportedException(
                "The fill template is missing the measured ResolutionControl slot/marker. No stream changes have been made.");
        }
        stream[header + 0x307] = resolutionControl == 1 ? (byte)0x41 : (byte)0x49;
    }

    private static SurfaceOperationAuthoringTemplate SelectTemplate(byte[] currentStream, string className)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in SurfaceOperationAuthoringTemplates.All
            .Concat(FillSurfaceAuthoringTemplates.All))
        {
            if (!string.Equals(template.ClassName, className, StringComparison.Ordinal))
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
            $"The document is not one of the controlled stripped surface-operation bases for {className} " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}
