using System;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed authoring for four controlled legacy tutorial Extrude/Revolve features whose feature
/// tree tail is only that feature: Boss-Extrude2 (01_pin, UpToSurface), Base-Extrude
/// (03_circlip, UpToSurface), Boss-Revolve1 (05_s_632_1) and Cut-Revolve1
/// (09_strain_relief_straight, explicit body scope). Each template is keyed by the exact
/// stripped base length plus normalized SHA-256; the measured native block is appended after
/// validating the stream version, stripped folder count, marker, absent same-class feature and
/// measured feature name. No value or layout is fabricated.
/// </summary>
internal static class LegacyTutorialFeatureBuilder
{
    internal static LegacyTutorialFeatureAuthoringTemplate? FindBase(byte[] stream, bool revolve) =>
        LegacyTutorialFeatureAuthoringTemplates.FindByBase(stream, revolve);

    internal static byte[] BuildExtrude(
        byte[] currentStream, ExtrudeFeatureDefinitionData definition, string featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        var template = RequireTemplate(currentStream, revolve: false);
        ValidateExtrudeDefinition(definition, template);
        return Build(currentStream, template, featureName);
    }

    internal static byte[] BuildRevolve(
        byte[] currentStream, RevolveFeatureDefinitionData definition, string featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        var template = RequireTemplate(currentStream, revolve: true);
        ValidateRevolveDefinition(definition, template);
        return Build(currentStream, template, featureName);
    }

    private static LegacyTutorialFeatureAuthoringTemplate RequireTemplate(byte[] currentStream, bool revolve) =>
        FindBase(currentStream, revolve) ??
        throw new NotSupportedException(
            "Legacy tutorial Extrude/Revolve authoring requires one of the four measured stripped " +
            "bases (01_pin, 03_circlip, 05_s_632_1, 09_strain_relief_straight). " +
            "No stream changes have been made.");

    private static void ValidateExtrudeDefinition(
        ExtrudeFeatureDefinitionData definition, LegacyTutorialFeatureAuthoringTemplate template)
    {
        if (definition.IsCut != template.IsCut)
            Reject($"IsCut={template.IsCut}");
        if (definition.EndCondition != (uint)template.EndCondition)
            Reject($"EndCondition={template.EndCondition}");
        if (definition.EndConditionDirection2 != 0)
            Reject("EndConditionDirection2=0");
        if (definition.BothDirections)
            Reject("BothDirections=false");
        if (definition.ReverseDirection != template.ReverseDirection)
            Reject($"ReverseDirection={template.ReverseDirection}");
        if (definition.FromType != 0 || definition.FromOffsetDistance != 0 || definition.FromOffsetReverse)
            Reject("FromType=0 with no offset");
        if (definition.DraftAngle != 0 || definition.DraftOutward ||
            definition.DraftAngleDirection2 != 0 || definition.DraftOutwardDirection2 ||
            definition.DraftWhileExtruding || definition.DraftWhileExtrudingDirection2)
            Reject("no draft");
        if (definition.FlipSideToCut || definition.NormalCut || definition.OptimizeGeometry)
            Reject("no cut-side/normal-cut/optimize options");
        if (definition.IsThinFeature || definition.ThinWallType != 0 ||
            definition.WallThickness != 0 || definition.WallThicknessDirection2 != 0)
            Reject("no thin feature");
        if (definition.CapEnds || definition.CapThickness != 0 || definition.LinkToThickness)
            Reject("no cap ends");
        if (definition.TranslateSurface || definition.TranslateSurfaceDirection2 ||
            definition.ReverseOffset || definition.ReverseOffsetDirection2)
            Reject("no translate/reverse-offset options");
        if (definition.AutoFilletCorners || definition.AutoFilletRadius != 0)
            Reject("no auto-fillet corners");
        if (definition.FeatureScope != template.FeatureScope)
            Reject($"FeatureScope={template.FeatureScope}");
        if (definition.AutoSelect != template.AutoSelect)
            Reject($"AutoSelect={template.AutoSelect}");
        int scopeBodies = definition.BodyScope?.Bodies?.Count ?? 0;
        int scopeBodyIds = definition.BodyScope?.BodyIds?.Count ?? 0;
        if (scopeBodies != template.ScopeBodiesCount || scopeBodyIds != 0)
            Reject($"BodyScope body count={template.ScopeBodiesCount}");
        if (definition.SketchSelectionMode == SketchSelectionMode.ExplicitSelection)
            Reject("whole-sketch selection (the measured block embeds the sketch context)");
        if ((definition.SelectedContours?.Count ?? 0) != 0 ||
            (definition.SelectedRegions?.Count ?? 0) != 0 ||
            (definition.AvailableSketchContours?.Count ?? 0) != 0 ||
            (definition.AvailableSketchRegions?.Count ?? 0) != 0 ||
            definition.DirectionReference != null || definition.EndConditionEntity != null)
            Reject("no explicit selection references");
        if (!string.IsNullOrWhiteSpace(definition.SketchName))
            Reject("an empty SketchName (the measured block embeds the sketch)");
    }

    private static void ValidateRevolveDefinition(
        RevolveFeatureDefinitionData definition, LegacyTutorialFeatureAuthoringTemplate template)
    {
        if (definition.IsCut != template.IsCut)
            Reject($"IsCut={template.IsCut}");
        if (definition.IsThinFeature)
            Reject("a solid revolve");
        if (definition.BothDirections || definition.AngleDirection2 != 0)
            Reject("a single-direction revolve");
        if (definition.ReverseDirection != template.ReverseDirection)
            Reject($"ReverseDirection={template.ReverseDirection}");
        if (!string.IsNullOrWhiteSpace(definition.SketchName))
            Reject("an empty SketchName (the measured block embeds the sketch and axis)");
    }

    internal static byte[] Build(
        byte[] currentStream, LegacyTutorialFeatureAuthoringTemplate template, string featureName)
    {
        if (currentStream == null || currentStream.Length != template.BaseLength)
            throw new NotSupportedException(
                $"Legacy tutorial Extrude/Revolve authoring requires the measured stripped base " +
                $"length {template.BaseLength} for {template.Sample}. No stream changes have been made.");
        if (BitConverter.ToUInt16(currentStream, 0) != template.StreamVersion)
            throw new NotSupportedException(
                $"Legacy tutorial Extrude/Revolve authoring requires first stream value " +
                $"0x{template.StreamVersion:X} for {template.Sample}. No stream changes have been made.");
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
            throw new NotSupportedException(
                $"Legacy tutorial Extrude/Revolve authoring requires stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, template.ClassName) > 0)
            throw new NotSupportedException(
                $"Legacy tutorial Extrude/Revolve authoring requires a base without an existing " +
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
                    "Legacy tutorial Extrude/Revolve authoring marker does not match the measured " +
                    "base. No stream changes have been made.");
        }
        byte[] normalized = (byte[])currentStream.Clone();
        normalized[4] = normalized[5] = 0;
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(normalized))
                .Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, template.BaseSha256, StringComparison.Ordinal))
            throw new NotSupportedException(
                "Legacy tutorial Extrude/Revolve authoring requires the exact measured base " +
                "(normalized signature mismatch). No stream changes have been made.");
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Legacy tutorial Extrude/Revolve authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        var newStream = new byte[currentStream.Length + template.BlockLength];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.BlockLength);
        return newStream;
    }

    private static void Reject(string requirement) =>
        throw new NotSupportedException(
            $"Legacy tutorial Extrude/Revolve authoring keeps {requirement}. " +
            "No stream changes have been made.");
}
