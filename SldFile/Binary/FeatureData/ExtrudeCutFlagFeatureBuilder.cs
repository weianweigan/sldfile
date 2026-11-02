using System;
using System.Linq;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured template authoring for the 0x71 flip-side and normal cut layouts. The full native
/// tail is appended to its exact stripped base; the flip-side/normal-cut state is carried by the
/// measured block rather than a re-synthesized scalar. No geometry/option parameter is varied.
/// </summary>
internal static class ExtrudeCutFlagFeatureBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static bool IsFlipTemplate(ExtrudeFeatureDefinitionData definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.FlipSideToCut && !definition.NormalCut;

    internal static bool IsNormalTemplate(ExtrudeFeatureDefinitionData definition) =>
        definition.IsCut && !definition.IsThinFeature && definition.NormalCut && !definition.FlipSideToCut;

    internal static bool IsTemplate(ExtrudeFeatureDefinitionData definition) =>
        definition.IsCut && !definition.IsThinFeature &&
        (definition.FlipSideToCut || definition.NormalCut);

    internal static string GetTemplateName(ExtrudeFeatureDefinitionData definition) =>
        IsNormalTemplate(definition)
            ? ExtrudeCutFlagAuthoringTemplates.NormalSource.Name
            : ExtrudeCutFlagAuthoringTemplates.FlipSource.Name;

    internal static void ValidateDefinition(ExtrudeFeatureDefinitionData definition)
    {
        bool flip = IsFlipTemplate(definition);
        bool normal = IsNormalTemplate(definition);
        if (!flip && !normal)
        {
            throw new NotSupportedException(
                "Flip-side/normal cut authoring requires exactly one of FlipSideToCut or NormalCut. " +
                "No stream changes have been made.");
        }
        Reject(definition.IsThinFeature, nameof(definition.IsThinFeature));
        Reject(!definition.IsCut, nameof(definition.IsCut));
        Reject(definition.EndCondition != 1, nameof(definition.EndCondition));
        Reject(definition.EndConditionDirection2 != 0, nameof(definition.EndConditionDirection2));
        Reject(definition.Depth != 0, nameof(definition.Depth));
        Reject(definition.DepthDirection2 != 0, nameof(definition.DepthDirection2));
        Reject(definition.BothDirections, nameof(definition.BothDirections));
        Reject(definition.ReverseDirection, nameof(definition.ReverseDirection));
        Reject(!definition.Merge, nameof(definition.Merge));
        Reject(definition.DraftWhileExtruding, nameof(definition.DraftWhileExtruding));
        Reject(definition.DraftAngle != 0, nameof(definition.DraftAngle));
        Reject(definition.DraftWhileExtrudingDirection2, nameof(definition.DraftWhileExtrudingDirection2));
        Reject(definition.DraftAngleDirection2 != 0, nameof(definition.DraftAngleDirection2));
        Reject(definition.DraftOutward, nameof(definition.DraftOutward));
        Reject(definition.DraftOutwardDirection2, nameof(definition.DraftOutwardDirection2));
        Reject(definition.FromType != 0, nameof(definition.FromType));
        Reject(definition.FromOffsetDistance != 0, nameof(definition.FromOffsetDistance));
        Reject(definition.FromOffsetReverse, nameof(definition.FromOffsetReverse));
        Reject(definition.AssemblyFeatureScope, nameof(definition.AssemblyFeatureScope));
        Reject(definition.AutoSelectComponents, nameof(definition.AutoSelectComponents));
        Reject(definition.PropagateFeatureToParts, nameof(definition.PropagateFeatureToParts));
        Reject(definition.OptimizeGeometry, nameof(definition.OptimizeGeometry));
        Reject(definition.ThinWallType != 0, nameof(definition.ThinWallType));
        Reject(definition.WallThickness != 0, nameof(definition.WallThickness));
        Reject(definition.WallThicknessDirection2 != 0, nameof(definition.WallThicknessDirection2));
        Reject(definition.CapEnds, nameof(definition.CapEnds));
        Reject(definition.CapThickness != 0, nameof(definition.CapThickness));
        Reject(definition.LinkToThickness, nameof(definition.LinkToThickness));
        Reject(definition.TranslateSurface, nameof(definition.TranslateSurface));
        Reject(definition.TranslateSurfaceDirection2, nameof(definition.TranslateSurfaceDirection2));
        Reject(definition.ReverseOffset, nameof(definition.ReverseOffset));
        Reject(definition.ReverseOffsetDirection2, nameof(definition.ReverseOffsetDirection2));
        Reject(definition.AutoFilletCorners, nameof(definition.AutoFilletCorners));
        Reject(definition.AutoFilletRadius != 0, nameof(definition.AutoFilletRadius));
        Reject(definition.SketchSelectionMode == SketchSelectionMode.ExplicitSelection, nameof(definition.SketchSelectionMode));
        Reject(definition.SelectedContours != null && definition.SelectedContours.Count > 0, nameof(definition.SelectedContours));
        Reject(definition.SelectedRegions != null && definition.SelectedRegions.Count > 0, nameof(definition.SelectedRegions));
        Reject(definition.DirectionReference != null, nameof(definition.DirectionReference));
        Reject(definition.EndConditionEntity != null, nameof(definition.EndConditionEntity));
        Reject(definition.BodyScope != null && (!definition.BodyScope.AutoSelect || definition.BodyScope.Flags != 2 ||
            (definition.BodyScope.BodyIds != null && definition.BodyScope.BodyIds.Count > 0) ||
            (definition.BodyScope.Bodies != null && definition.BodyScope.Bodies.Count > 0)), nameof(definition.BodyScope));
    }

    internal static byte[] Build(
        byte[] currentStream, ExtrudeFeatureDefinitionData definition, string featureName)
    {
        ValidateDefinition(definition);
        var template = SelectByBaseLength(currentStream, IsNormalTemplate(definition));
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cut template authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream.Length != template.BaseLength || BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                "Flip-side/normal cut authoring requires the exact measured stripped base and folder count. " +
                "No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 0) != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Flip-side/normal cut authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Flip-side/normal cut authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Flip-side/normal cut authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)template.NativeFolderCount).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static ExtrudeCutFlagAuthoringTemplate SelectByBaseLength(byte[] currentStream, bool normal)
    {
        int length = currentStream?.Length ?? 0;
        var source = normal
            ? ExtrudeCutFlagAuthoringTemplates.NormalSource
            : ExtrudeCutFlagAuthoringTemplates.FlipSource;
        var resave = normal
            ? ExtrudeCutFlagAuthoringTemplates.NormalResave
            : ExtrudeCutFlagAuthoringTemplates.FlipResave;
        if (length == source.BaseLength) return source;
        if (length == resave.BaseLength) return resave;
        throw new NotSupportedException(
            $"Flip-side/normal cut authoring requires the measured stripped base length " +
            $"{source.BaseLength} or {resave.BaseLength}; the document stream is {length} bytes. " +
            "No stream changes have been made.");
    }

    private static void Reject(bool condition, string property)
    {
        if (condition)
            throw new NotSupportedException(
                $"Flip-side/normal cut authoring does not support {property}. No stream changes have been made.");
    }

    private static string NormalizedBaseSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        int marker = stream.Length - MarkerDistanceFromFeature;
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
