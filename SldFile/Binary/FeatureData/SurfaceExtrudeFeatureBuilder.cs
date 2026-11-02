using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured surface-extrude authoring from the controlled surf_extrude_10mm sample.
/// The target must be the exact stripped base (stream 0x70, folder count 19, 0x67 marker
/// at the measured position with value 0, normalized SHA). Only the depth is parameterized,
/// patched into the two validated slots. All other fields remain measured defaults.
/// </summary>
internal static class SurfaceExtrudeFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, double depth, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (double.IsNaN(depth) || double.IsInfinity(depth) || depth <= 0 || depth > 1.0)
            throw new ArgumentOutOfRangeException(nameof(depth),
                $"Surface extrude depth must be a positive finite value <= 1.0 m, but received {depth}.");

        var template = SelectTemplate(currentStream);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Surface extrude measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, SurfaceExtrudeFeatureParser.ClassName) > 0)
        {
            throw new NotSupportedException(
                "The document already contains a moExtruRefSurface_c feature. No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        foreach (int slot in new[] { template.DepthSlot1, template.DepthSlot2 })
        {
            if (slot + 8 > block.Length)
                throw new NotSupportedException("Surface extrude template block is truncated. No stream changes have been made.");
            double measured = BitConverter.ToDouble(block, slot);
            if (Math.Abs(measured - template.MeasuredDepth) > 1e-15)
                throw new NotSupportedException(
                    $"Surface extrude depth slot 0x{slot:X} changed; refusing to patch an unknown layout. " +
                    "No stream changes have been made.");
            BitConverter.GetBytes(depth).CopyTo(block, slot);
        }

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, template.MarkerOffset + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return (newStream, template.Name);
    }

    private static SurfaceExtrudeAuthoringTemplate SelectTemplate(byte[] currentStream)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in SurfaceExtrudeAuthoringTemplates.All)
        {
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
                continue;
            if (template.MarkerOffset + 8 > currentStream.Length)
                continue;
            if (currentStream[template.MarkerOffset] != 0x67 ||
                currentStream[template.MarkerOffset + 1] != 0 ||
                currentStream[template.MarkerOffset + 2] != 0 ||
                currentStream[template.MarkerOffset + 3] != 0 ||
                BitConverter.ToUInt32(currentStream, template.MarkerOffset + 4) != 0)
                continue;

            byte[] normalized = (byte[])currentStream.Clone();
            BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
            BitConverter.GetBytes(0u).CopyTo(normalized, template.MarkerOffset + 4);
            string sha;
            using (var hash = SHA256.Create())
                sha = BitConverter.ToString(hash.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
            if (string.Equals(sha, template.BaseSha256, StringComparison.Ordinal))
                return template;
        }

        throw new NotSupportedException(
            $"The document is not the controlled stripped surface-extrude base " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}
