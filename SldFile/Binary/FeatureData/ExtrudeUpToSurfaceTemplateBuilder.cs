using System;
using System.Linq;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled fixed-measurement UpToSurface cut authoring on the extrude_upto_surface_modify source
/// and its native resave bases. The complete measured block carries the target face, explicit body
/// scope and cut context; only the exact measured geometry/target is accepted. OffsetFromSurface,
/// UpToVertex, UpToBody and other end conditions are rejected by the normal cut validation path.
/// </summary>
internal static class ExtrudeUpToSurfaceTemplateBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static bool IsTemplate(ExtrudeFeatureDefinitionData definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.EndCondition == 4;

    internal static string GetTemplateName(ExtrudeFeatureDefinitionData definition) =>
        ExtrudeUpToSurfaceAuthoringTemplates.ModifySource.Name;

    internal static byte[] Build(
        byte[] currentStream, ExtrudeFeatureDefinitionData definition, string featureName)
    {
        var template = SelectByBaseLength(currentStream);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"UpToSurface authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (definition.EndConditionEntity != null)
        {
            if (definition.EndConditionEntity.EntityType != SelectionEntityType.Face ||
                definition.EndConditionEntity.EntityId != template.FaceId)
            {
                throw new NotSupportedException(
                    $"UpToSurface authoring is bound to the measured target face 0x{template.FaceId:X} " +
                    "of this stripped base; a different face id cannot be re-resolved. " +
                    "No stream changes have been made.");
            }
        }
        if (definition.BodyScope?.BodyIds != null && definition.BodyScope.BodyIds.Count > 0)
        {
            if (definition.BodyScope.BodyIds.Count != 1 || definition.BodyScope.BodyIds[0] != template.BodyId)
            {
                throw new NotSupportedException(
                    $"UpToSurface authoring is bound to the measured body scope {template.BodyId} " +
                    "of this stripped base. No stream changes have been made.");
            }
        }
        if (definition.BodyScope?.Bodies != null && definition.BodyScope.Bodies.Count > 0 &&
            definition.BodyScope.Bodies.Any(b => b == null || b.EntityId != template.BodyId))
        {
            throw new NotSupportedException(
                $"UpToSurface authoring is bound to the measured body scope {template.BodyId}. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"UpToSurface authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"UpToSurface authoring requires the stripped folder count {template.StrippedFolderCount}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "UpToSurface authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"UpToSurface authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static ExtrudeUpToSurfaceAuthoringTemplate SelectByBaseLength(byte[] currentStream)
    {
        int length = currentStream?.Length ?? 0;
        if (length == ExtrudeUpToSurfaceAuthoringTemplates.ModifySource.BaseLength)
            return ExtrudeUpToSurfaceAuthoringTemplates.ModifySource;
        if (length == ExtrudeUpToSurfaceAuthoringTemplates.ModifyResave.BaseLength)
            return ExtrudeUpToSurfaceAuthoringTemplates.ModifyResave;
        throw new NotSupportedException(
            $"UpToSurface authoring requires the measured stripped base length " +
            $"{ExtrudeUpToSurfaceAuthoringTemplates.ModifySource.BaseLength} or " +
            $"{ExtrudeUpToSurfaceAuthoringTemplates.ModifyResave.BaseLength}; the document stream is " +
            $"{length} bytes. No stream changes have been made.");
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
