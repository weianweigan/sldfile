using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured Shell authoring: one removed face, direction 0, arbitrary thickness
/// patched into the six validated thickness slots of the t2/t3 measured blocks.
/// The target must be the exact stripped base of one ShellOptions sample (stream
/// version 0x70, stripped folder count 20, 0x67 marker at 0x2347 with value 1, and the
/// same normalized SHA-256). Any mismatch, an existing moShell_c, multiple-thickness
/// input or a custom feature name is rejected before the stream changes.
/// </summary>
internal static class ShellFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream, double thickness, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (double.IsNaN(thickness) || double.IsInfinity(thickness) || thickness <= 0 || thickness > 0.1)
            throw new ArgumentOutOfRangeException(nameof(thickness),
                $"Shell thickness must be a positive finite value <= 0.1 m, but received {thickness}.");

        var template = SelectTemplate(currentStream);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Shell measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, ShellFeatureParser.ClassName) > 0)
        {
            throw new NotSupportedException(
                "The document already contains a moShell_c feature. No stream changes have been made.");
        }

        if (BitConverter.ToUInt32(currentStream, template.MarkerOffset + 4) != template.MarkerValue)
        {
            throw new NotSupportedException(
                "The stripped Shell base is missing the measured 0x67 marker value. No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        foreach (int slot in ShellFeatureParser.ThicknessSlotOffsets)
        {
            if (slot + 8 > block.Length)
                throw new NotSupportedException("Shell template block is truncated. No stream changes have been made.");
            double measured = BitConverter.ToDouble(block, slot);
            if (Math.Abs(measured - template.MeasuredThickness) > 1e-15)
                throw new NotSupportedException(
                    $"Shell template thickness slot 0x{slot:X} changed; refusing to patch an unknown layout. " +
                    "No stream changes have been made.");
            BitConverter.GetBytes(thickness).CopyTo(block, slot);
        }

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return (newStream, template.Name);
    }

    private static ShellAuthoringTemplate SelectTemplate(byte[] currentStream)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in ShellAuthoringTemplates.All)
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
                currentStream[template.MarkerOffset + 3] != 0)
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
            $"The document is not one of the controlled stripped Shell bases " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}
