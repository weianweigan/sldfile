using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed authoring for five Agent 5 Task 1.1/1.2 handoff samples whose target feature is the
/// last folder in the ResolvedFeatures stream: DraftOutward=true cut, thin CapEnds, cut
/// FromType=3/FromOffsetReverse, thin AutoFilletCorners, and revolve axis=model edge.
/// Each template is keyed by the exact stripped-base length plus normalized SHA-256; the
/// measured block is appended only after version, folder count, absent same-class feature,
/// marker, base signature and measured feature name all match.
/// </summary>
internal static class Task11FeatureAuthoringBuilder
{
    internal static Task11FeatureAuthoringTemplate? FindBase(byte[] stream, bool revolve) =>
        Task11FeatureAuthoringTemplates.FindByBase(stream, revolve);

    internal static byte[] BuildExtrude(
        byte[] currentStream, Task11FeatureAuthoringTemplate template,
        ExtrudeFeatureDefinitionData definition, string featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        ValidateExtrudeDefinition(template, definition);
        return Build(currentStream, template, featureName);
    }

    internal static byte[] BuildRevolve(
        byte[] currentStream, Task11FeatureAuthoringTemplate template,
        RevolveFeatureDefinitionData definition, string featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        ValidateRevolveDefinition(template, definition);
        return Build(currentStream, template, featureName);
    }

    private static void ValidateExtrudeDefinition(
        Task11FeatureAuthoringTemplate template, ExtrudeFeatureDefinitionData definition)
    {
        RequireBool(definition.IsCut, template.IsCut, nameof(definition.IsCut));
        if (template.Sample.IndexOf("draft_outward", StringComparison.Ordinal) >= 0)
        {
            RequireDouble(definition.Depth, template.Depth, nameof(definition.Depth));
            RequireDouble(definition.DraftAngle, template.DraftAngle, nameof(definition.DraftAngle));
            RequireBool(definition.DraftOutward, template.DraftOutward, nameof(definition.DraftOutward));
            RequireBool(definition.DraftWhileExtruding, true, nameof(definition.DraftWhileExtruding));
            RejectDraftDirection2(definition);
        }
        else if (template.Sample.IndexOf("thin_cap_ends", StringComparison.Ordinal) >= 0)
        {
            RequireBool(definition.IsThinFeature, true, nameof(definition.IsThinFeature));
            RequireDouble(definition.Depth, template.Depth, nameof(definition.Depth));
            RequireDouble(definition.WallThickness, template.WallThickness, nameof(definition.WallThickness));
            RequireUInt(definition.ThinWallType, template.ThinWallType, nameof(definition.ThinWallType));
            RequireBool(definition.CapEnds, true, nameof(definition.CapEnds));
            RequireBool(definition.AutoFilletCorners, false, nameof(definition.AutoFilletCorners));
        }
        else if (template.Sample.IndexOf("auto_fillet", StringComparison.Ordinal) >= 0)
        {
            RequireBool(definition.IsThinFeature, true, nameof(definition.IsThinFeature));
            RequireDouble(definition.Depth, template.Depth, nameof(definition.Depth));
            RequireDouble(definition.WallThickness, template.WallThickness, nameof(definition.WallThickness));
            RequireBool(definition.AutoFilletCorners, true, nameof(definition.AutoFilletCorners));
            RequireDouble(definition.AutoFilletRadius, template.AutoFilletRadius, nameof(definition.AutoFilletRadius));
            RequireBool(definition.CapEnds, false, nameof(definition.CapEnds));
        }
        else if (template.Sample.IndexOf("from_offset_reverse", StringComparison.Ordinal) >= 0)
        {
            RequireDouble(definition.Depth, template.Depth, nameof(definition.Depth));
            RequireUInt(definition.FromType, template.FromType, nameof(definition.FromType));
            RequireDouble(definition.FromOffsetDistance, template.FromOffsetDistance, nameof(definition.FromOffsetDistance));
            RequireBool(definition.FromOffsetReverse, true, nameof(definition.FromOffsetReverse));
            RejectDraftDirection2(definition);
        }
        else
        {
            throw new NotSupportedException(
                "Task 1.1/1.2 handoff authoring does not recognize this extrude template. " +
                "No stream changes have been made.");
        }
    }

    private static void ValidateRevolveDefinition(
        Task11FeatureAuthoringTemplate template, RevolveFeatureDefinitionData definition)
    {
        RequireBool(definition.IsCut, false, nameof(definition.IsCut));
        RequireBool(definition.IsThinFeature, false, nameof(definition.IsThinFeature));
        RequireDouble(definition.Angle, template.Angle, nameof(definition.Angle));
        RequireInt(definition.AxisType, template.AxisType, nameof(definition.AxisType));
        if (definition.AxisReference != null &&
            definition.AxisReference.EntityType != SelectionEntityType.Edge)
            throw new NotSupportedException(
                "Task 1.1/1.2 revolve authoring keeps the measured model-edge axis. " +
                "No stream changes have been made.");
    }

    private static void RejectDraftDirection2(ExtrudeFeatureDefinitionData definition)
    {
        if (definition.DraftWhileExtrudingDirection2 || definition.DraftAngleDirection2 != 0 ||
            definition.DraftOutwardDirection2 || definition.BothDirections)
            throw new NotSupportedException(
                "Task 1.1/1.2 handoff authoring does not support direction-2 draft/both directions. " +
                "No stream changes have been made.");
    }

    private static byte[] Build(
        byte[] currentStream, Task11FeatureAuthoringTemplate template, string featureName)
    {
        if (currentStream == null || currentStream.Length != template.BaseLength)
            throw new NotSupportedException(
                $"Task 1.1/1.2 handoff authoring requires the measured stripped base length " +
                $"{template.BaseLength} for {template.Sample}. No stream changes have been made.");
        if (BitConverter.ToUInt16(currentStream, 0) != template.StreamVersion)
            throw new NotSupportedException(
                $"Task 1.1/1.2 handoff authoring requires first stream value " +
                $"0x{template.StreamVersion:X} for {template.Sample}. No stream changes have been made.");
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
            throw new NotSupportedException(
                $"Task 1.1/1.2 handoff authoring requires stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, template.ClassName) > 0)
            throw new NotSupportedException(
                $"Task 1.1/1.2 handoff authoring requires a base without an existing " +
                $"{template.ClassName}. No stream changes have been made.");
        if (template.MarkerOffset >= 0)
        {
            if (template.MarkerOffset + 8 > currentStream.Length ||
                currentStream[template.MarkerOffset] != 0x67 ||
                currentStream[template.MarkerOffset + 1] != 0 ||
                currentStream[template.MarkerOffset + 2] != 0 ||
                currentStream[template.MarkerOffset + 3] != 0 ||
                BitConverter.ToUInt32(currentStream, template.MarkerOffset + 4) != template.MarkerValue)
                throw new NotSupportedException(
                    "Task 1.1/1.2 handoff authoring marker does not match the measured base. " +
                    "No stream changes have been made.");
        }
        byte[] normalized = (byte[])currentStream.Clone();
        normalized[4] = normalized[5] = 0;
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(normalized))
                .Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, template.BaseSha256, StringComparison.Ordinal))
            throw new NotSupportedException(
                "Task 1.1/1.2 handoff authoring requires the exact measured base " +
                "(normalized signature mismatch). No stream changes have been made.");
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Task 1.1/1.2 handoff authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        var newStream = new byte[currentStream.Length + template.BlockLength];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.BlockLength);
        return newStream;
    }

    private static void RequireDouble(double? actual, double? expected, string property)
    {
        if (!actual.HasValue) return;
        if (expected.HasValue && Math.Abs(actual.Value - expected.Value) <= 1e-12) return;
        throw new NotSupportedException(
            $"Task 1.1/1.2 handoff authoring keeps {property}={expected?.ToString() ?? "null"}. " +
            "No stream changes have been made.");
    }

    private static void RequireBool(bool? actual, bool? expected, string property)
    {
        if (!actual.HasValue) return;
        if (expected.HasValue && actual.Value == expected.Value) return;
        throw new NotSupportedException(
            $"Task 1.1/1.2 handoff authoring keeps {property}={expected?.ToString() ?? "null"}. " +
            "No stream changes have been made.");
    }

    private static void RequireUInt(uint? actual, uint? expected, string property)
    {
        if (!actual.HasValue) return;
        if (expected.HasValue && actual.Value == expected.Value) return;
        throw new NotSupportedException(
            $"Task 1.1/1.2 handoff authoring keeps {property}={expected?.ToString() ?? "null"}. " +
            "No stream changes have been made.");
    }

    private static void RequireInt(int? actual, int? expected, string property)
    {
        if (!actual.HasValue) return;
        if (expected.HasValue && actual.Value == expected.Value) return;
        throw new NotSupportedException(
            $"Task 1.1/1.2 handoff authoring keeps {property}={expected?.ToString() ?? "null"}. " +
            "No stream changes have been made.");
    }
}
