using System;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled mirror-pattern authoring on the measured PatternOptions/mirror_plane base.
/// The target must be the native sample stripped at its moMirrorPattern_c tag (folder count
/// 22). The measured payload already carries the BaseBox source-feature persistent-reference
/// blob and the Right-Plane reference block; only the feature name and allocated feature id
/// are written. All other definition fields must stay at the measured/default values.
/// </summary>
internal static class MirrorPatternFeatureBuilder
{
    internal static byte[] BuildMirrorPatternStream(
        byte[] currentStream, MirrorPatternFeatureDefinitionData definition, string featureName)
    {
        ValidateDefinition(definition);
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x72)
        {
            throw new NotSupportedException(
                $"Mirror pattern authoring requires a ResolvedFeatures stream version 0x72, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");
        }
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, MirrorPatternSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring supports only the stripped base without an existing mirror feature. " +
                "No stream changes have been made.");
        }
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != MirrorPatternSegments.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Mirror pattern authoring requires the extrusion-stripped base with folder count " +
                $"{MirrorPatternSegments.StrippedFolderCount}, but the document uses {folderCount}. " +
                "No stream changes have been made.");
        }

        byte[] normalized = (byte[])currentStream.Clone();
        normalized[4] = 0;
        normalized[5] = 0;
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, MirrorPatternSegments.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Mirror pattern authoring is limited to the controlled PatternOptions/mirror_plane base " +
                $"(base signature {sha.Substring(0, 12)} does not match). No stream changes have been made.");
        }

        byte[] featureBlock = BuildFeatureBlock(featureName);

        ushort newFolderCount = (ushort)(folderCount + 1);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes(newFolderCount).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    internal static void ValidateDefinition(MirrorPatternFeatureDefinitionData definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        if (definition.MirrorPlaneType.HasValue && definition.MirrorPlaneType.Value != 1)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring supports only the measured reference-plane layout " +
                "(MirrorPlaneType = 1). No stream changes have been made.");
        }
        if (definition.Plane != null)
        {
            if (definition.Plane.EntityType != SelectionEntityType.Plane ||
                definition.Plane.EntityId.HasValue || !string.IsNullOrEmpty(definition.Plane.ReferenceName))
            {
                throw new NotSupportedException(
                    "Mirror pattern authoring keeps the measured Right-Plane reference; only a presence-only " +
                    "Plane reference is accepted. No stream changes have been made.");
            }
        }
        if (definition.PatternFeatureArray != null)
        {
            if (definition.PatternFeatureArray.Count != 1 ||
                definition.PatternFeatureArray[0] == null ||
                definition.PatternFeatureArray[0].EntityType != SelectionEntityType.None ||
                definition.PatternFeatureArray[0].EntityId.HasValue ||
                !string.IsNullOrEmpty(definition.PatternFeatureArray[0].ReferenceName))
            {
                throw new NotSupportedException(
                    "Mirror pattern authoring keeps the measured BaseBox source feature; only one presence-only " +
                    "source reference is accepted. No stream changes have been made.");
            }
        }
        if (definition.GeometryPattern == true)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support GeometryPattern. No stream changes have been made.");
        }
        if (definition.FeatureScope.HasValue && definition.FeatureScope.Value != 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support feature scope. No stream changes have been made.");
        }
        if (definition.FeatureScopeBodies != null && definition.FeatureScopeBodies.Count > 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support feature-scope bodies. No stream changes have been made.");
        }
        if (definition.MirrorFaceArray != null && definition.MirrorFaceArray.Count > 0)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support mirror faces. No stream changes have been made.");
        }
        if (definition.PropagateVisualProperty.HasValue)
        {
            throw new NotSupportedException(
                "Mirror pattern authoring does not yet support PropagateVisualProperty. No stream changes have been made.");
        }
    }

    private static byte[] BuildFeatureBlock(string featureName)
    {
        if (string.IsNullOrEmpty(featureName)) featureName = "镜像1";
        byte[] rest = (byte[])MirrorPatternSegments.HeaderRest.Clone();
        BitConverter.GetBytes(MirrorPatternSegments.FeatureId)
            .CopyTo(rest, MirrorPatternSegments.FeatureIdOffsetInRest);

        using var writer = new MfcStreamWriter(2048);
        writer.WriteMfcTag(MirrorPatternSegments.ClassName);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(MirrorPatternSegments.TypeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(rest);
        return writer.ToArray();
    }
}
