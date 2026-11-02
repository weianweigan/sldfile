using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured Type=2 face-fillet authoring. Supports only the controlled 3 mm and 5 mm
/// two-face specimens on their exact source/resave stripped bases. The complete 3190-byte
/// native block is appended verbatim, so radius and face records are byte-identical to the
/// measured specimen. General radius parameterization is not exposed: the 3/5 mm diff also
/// changes derived tiny-tolerance slots that do not scale linearly.
/// </summary>
internal static class FaceFilletFeatureBuilder
{
    internal static void ValidateDefinition(FilletFeatureDefinitionData definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (definition.Type != FaceFilletSegments.NativeType)
        {
            throw new NotSupportedException(
                "Face-fillet authoring requires Fillet Type = 2. No stream changes have been made.");
        }
        double radius = definition.DefaultRadius ??
            throw new ArgumentOutOfRangeException(nameof(definition.DefaultRadius),
                "Face-fillet authoring requires the measured 0.003 or 0.005 m radius.");
        if (double.IsNaN(radius) || double.IsInfinity(radius) ||
            !FaceFilletSegments.IsMeasuredRadius(radius))
        {
            throw new NotSupportedException(
                "Face-fillet authoring is fixed to the measured 0.003 m and 0.005 m radii; " +
                "other radii are not established by the derived-tolerance slots. " +
                "No stream changes have been made.");
        }

        if (definition.FaceReferences != null && definition.FaceReferences.Count > 0)
        {
            if (definition.FaceReferences.Count != 2)
            {
                throw new NotSupportedException(
                    "Face-fillet authoring requires exactly the two measured faces. " +
                    "No stream changes have been made.");
            }
            uint[] expectedIds = { FaceFilletSegments.FirstFaceId, FaceFilletSegments.SecondFaceId };
            for (int i = 0; i < expectedIds.Length; i++)
            {
                var face = definition.FaceReferences[i];
                if (face == null || face.EntityType != SelectionEntityType.Face ||
                    face.EntityId != expectedIds[i] ||
                    face.ClassWord != FaceFilletSegments.FaceClassWord ||
                    !string.Equals(face.ArchiveGuid, FaceFilletSegments.ArchiveGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException(
                        "Face-fillet authoring carries the measured face pair in serialized order " +
                        "(0x5D2 then 0x5CE, class word 0x300); face overrides are rejected. " +
                        "No stream changes have been made.");
                }
            }
        }

        foreach (var property in typeof(FilletFeatureDefinitionData)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(FilletFeatureDefinitionData.Type) ||
                property.Name == nameof(FilletFeatureDefinitionData.DefaultRadius) ||
                property.Name == nameof(FilletFeatureDefinitionData.FaceReferences) ||
                property.Name == nameof(IFeatureData.ClassName) ||
                property.Name == nameof(IFeatureData.TypeId) ||
                property.Name == nameof(FeatureDefinitionData.ResolvedProperties))
                continue;
            if (property.GetValue(definition) != null)
            {
                throw new NotSupportedException(
                    $"Face-fillet authoring does not support {property.Name}. " +
                    "No stream changes have been made.");
            }
        }
    }

    internal static byte[] BuildFaceFilletStream(
        byte[] currentStream, FilletFeatureDefinitionData definition, string featureName)
    {
        ValidateDefinition(definition);
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        double radius = definition.DefaultRadius!.Value;
        var template = SelectTemplate(currentStream, radius);
        if (!string.Equals(featureName, FaceFilletAuthoringTemplates.FeatureName,
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Face-fillet authoring uses the measured native feature name " +
                $"'{FaceFilletAuthoringTemplates.FeatureName}'; a custom name would resize the " +
                "serialized block. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires ResolvedFeatures version " +
                $"0x{template.StreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires the stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        }
        if (FindSingleSketchMarker(currentStream) < 0)
        {
            throw new NotSupportedException(
                "Face-fillet authoring requires the base's single measured 0x67 sketch " +
                "child-count marker with value 1. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires the exact measured fillet_face_{radius * 1000:0}mm " +
                $"stripped base (normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        if (block.Length != FaceFilletAuthoringTemplates.BlockLength)
            throw new InvalidOperationException("Face-fillet measured block length mismatch.");
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static FaceFilletAuthoringTemplate SelectTemplate(byte[] currentStream, double radius)
    {
        var candidates = FaceFilletAuthoringTemplates.All
            .Where(t => Math.Abs(t.Radius - radius) <= 1e-12 &&
                        t.BaseLength == currentStream.Length)
            .ToList();
        if (candidates.Count == 0)
        {
            var lengths = string.Join(", ", FaceFilletAuthoringTemplates.All
                .Where(t => Math.Abs(t.Radius - radius) <= 1e-12)
                .Select(t => t.BaseLength));
            throw new NotSupportedException(
                $"Face-fillet authoring for radius {radius:R} m requires one of the measured " +
                $"stripped base lengths [{lengths}]; the document stream is {currentStream.Length} " +
                "bytes. No stream changes have been made.");
        }
        string sha = NormalizedBaseSha(currentStream);
        var match = candidates.FirstOrDefault(t =>
            string.Equals(t.BaseSha256, sha, StringComparison.Ordinal));
        if (match == null)
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires an exact measured base signature for radius " +
                $"{radius:R} m; {sha.Substring(0, 12)} does not match the controlled " +
                "source/resave bases. No stream changes have been made.");
        }
        return match;
    }

    private static int FindSingleSketchMarker(byte[] stream)
    {
        int found = -1;
        for (int i = 0; i <= stream.Length - 8; i++)
        {
            if (stream[i] != 0x67 || stream[i + 1] != 0 || stream[i + 2] != 0 || stream[i + 3] != 0)
                continue;
            if (BitConverter.ToUInt32(stream, i + 4) != 1) return -1;
            if (found >= 0) return -1;
            found = i;
        }
        return found;
    }

    private static string NormalizedBaseSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized))
            .Replace("-", string.Empty).ToLowerInvariant();
    }
}
