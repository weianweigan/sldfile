using System;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled template authoring for the measured 0x71 cut layouts:
/// - direction-2 draft cut on the extrude_draft_dir2_3deg base (D4 angle patched);
/// - Merge=false multi-body ICE feature on the extrude_merge_false base (measured geometry fixed).
/// Complete native blocks are appended to their exact stripped bases; other layouts are rejected.
/// </summary>
internal static class ExtrudeCutTemplateBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static bool IsDraftDirection2Template(ExtrudeFeatureDefinitionData definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.Merge &&
        (definition.BothDirections || definition.DraftWhileExtrudingDirection2 ||
         definition.DraftAngleDirection2 != 0 || definition.DraftOutwardDirection2);

    internal static bool IsMergeFalseTemplate(ExtrudeFeatureDefinitionData definition) =>
        definition.IsCut && !definition.IsThinFeature && !definition.Merge;

    internal static string GetTemplateName(ExtrudeFeatureDefinitionData definition) =>
        Select(definition).Name;

    private static ExtrudeCutAuthoringTemplate Select(ExtrudeFeatureDefinitionData definition)
    {
        if (IsMergeFalseTemplate(definition))
            return ExtrudeCutAuthoringTemplates.MergeFalse;
        if (IsDraftDirection2Template(definition))
            return ExtrudeCutAuthoringTemplates.DraftDirection2;
        throw new NotSupportedException("This cut layout has no template authoring block.");
    }

    internal static byte[] Build(
        byte[] currentStream, ExtrudeFeatureDefinitionData definition, string featureName)
    {
        var template = Select(definition);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cut template authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream == null || currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Cut template authoring requires the measured stripped base of length " +
                $"{template.BaseLength} bytes; the document stream is {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Cut template authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Cut template authoring requires the stripped folder count {template.StrippedFolderCount}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Cut template authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cut template authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        if (IsDraftDirection2Template(definition))
            PatchDraftDirection2(block, definition.DraftAngleDirection2);

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static void PatchDraftDirection2(byte[] block, double angle)
    {
        var d4 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D4");
        if (d4.Count != 1)
            throw new InvalidOperationException("Measured direction-2 draft template is missing D4.");
        foreach (int off in new[] { 0x20, 0x23A })
        {
            if (Math.Abs(BitConverter.ToDouble(block, d4[0].Offset + off) - 3.0 * Math.PI / 180.0) > 1e-12)
                throw new InvalidOperationException(
                    "Measured direction-2 draft template layout changed; refusing to patch an unknown slot.");
            BitConverter.GetBytes(angle).CopyTo(block, d4[0].Offset + off);
        }
    }

    private static string NormalizedBaseSha(byte[] stream, int marker)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
