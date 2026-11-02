using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled Hole Wizard authoring on the measured extrusion-stripped bases:
/// - legacy simple hole (moHoleWzd_c / TypeId 0x54 / stream 0x71): through-all or the
///   measured blind template, only Diameter parameterized;
/// - tapped M6 hole (moHoleWzd_c / TypeId 0x52 / stream 0x84): ThreadDiameter and
///   ThreadDepth are fixed at the measured 0.006 / 0.010 m; null is accepted and uses
///   those values. Other diameters/depths are rejected because native verification showed
///   they do not change the rebuilt geometry/volume. The measured ISO/M6 flags, Type 48,
///   EndCondition 1 and face/sketch-point context stay template-measured.
/// All other fields and the face/sketch-point context stay template-measured.
/// </summary>
internal static class HoleWizardFeatureBuilder
{
    internal static byte[] BuildHoleWizardStream(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        ValidateDefinition(definition);
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        if (IsCounterSinkDefinition(definition))
            return BuildCounterSink(currentStream, definition, featureName);
        if (IsTwoPointCounterBoreDefinition(definition))
            return BuildTwoPointCounterBore(currentStream, definition, featureName);
        if (IsBlindCounterBoreDefinition(definition))
            return BuildBlindCounterBore(currentStream, definition, featureName);
        if (IsCounterBoreDefinition(definition))
            return BuildCounterBore(currentStream, definition, featureName);
        return IsTappedDefinition(definition)
            ? BuildTapped(currentStream, definition, featureName)
            : BuildLegacy(currentStream, definition, featureName);
    }

    internal static void ValidateDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (IsCounterSinkDefinition(definition))
            ValidateCounterSinkDefinition(definition);
        else if (IsTwoPointCounterBoreDefinition(definition))
            ValidateTwoPointCounterBoreDefinition(definition);
        else if (IsBlindCounterBoreDefinition(definition))
            ValidateBlindCounterBoreDefinition(definition);
        else if (IsCounterBoreDefinition(definition))
            ValidateCounterBoreDefinition(definition);
        else if (IsTappedDefinition(definition))
            ValidateTappedDefinition(definition);
        else
            ValidateLegacyDefinition(definition);
    }

    internal static bool IsCounterSinkDefinition(HoleWizardFeatureDefinitionData definition) =>
        definition.Type == HoleWizardSegments.CounterSinkTypeValue;

    internal static bool IsCounterBoreDefinition(HoleWizardFeatureDefinitionData definition) =>
        definition.Type == HoleWizardSegments.CounterBoreTypeValue;

    internal static bool IsBlindCounterBoreDefinition(HoleWizardFeatureDefinitionData definition) =>
        definition.Type == HoleWizardSegments.CounterBoreTypeValue && definition.EndCondition == 0;

    internal static bool IsTwoPointCounterBoreDefinition(HoleWizardFeatureDefinitionData definition) =>
        definition.Type == HoleWizardSegments.CounterBoreTypeValue && definition.EndCondition == 1 &&
        definition.SketchPoints?.Count == 2;

    internal static bool IsAnsiCounterBoreDefinition(HoleWizardFeatureDefinitionData definition) =>
        definition.Standard == HoleWizardSegments.CounterBoreAnsiStandard ||
        definition.FastenerType2 == 33 ||
        definition.FastenerType == HoleWizardSegments.CounterBoreAnsiFastenerType;

    internal static string CounterBoreFeatureNameFor(HoleWizardFeatureDefinitionData definition)
    {
        if (IsTwoPointCounterBoreDefinition(definition))
            return HoleWizardTwoPointCounterBoreTemplates.Source.FeatureName;
        if (IsBlindCounterBoreDefinition(definition))
            return (definition.ThruHoleDepth.HasValue
                       ? HoleWizardBlindCounterBoreTemplates.FindByDepth(definition.ThruHoleDepth.Value)
                       : null)?.FeatureName ??
                   HoleWizardBlindCounterBoreTemplates.Depth10.FeatureName;
        return IsAnsiCounterBoreDefinition(definition)
            ? HoleWizardCounterBoreAnsiAuthoringTemplates.FeatureName
            : HoleWizardCounterBoreAuthoringTemplates.FeatureName;
    }

    private static bool IsTappedDefinition(HoleWizardFeatureDefinitionData definition) =>
        definition.FolderTypeId == HoleWizardTappedAuthoringTemplates.FolderTypeId ||
        definition.Type == HoleWizardTappedAuthoringTemplates.TypeValue ||
        (definition.ThreadDiameter.HasValue && definition.ThreadDepth.HasValue) ||
        definition.Standard != null ||
        definition.Standard2.HasValue ||
        definition.FastenerType != null ||
        definition.FastenerType2.HasValue ||
        definition.FastenerSize != null ||
        definition.CosmeticThreadType.HasValue;

    private static void ValidateLegacyDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FolderTypeId.HasValue &&
            definition.FolderTypeId.Value != HoleWizardAuthoringTemplates.LegacyTypeId)
        {
            throw new NotSupportedException(
                "Hole Wizard authoring supports only the legacy layout (FolderTypeId 0x54). " +
                "No stream changes have been made.");
        }
        if (definition.Type.HasValue && definition.Type.Value != 0)
        {
            throw new NotSupportedException(
                "Hole Wizard authoring currently supports only Type = 0 (legacy simple hole). " +
                "No stream changes have been made.");
        }
        if (!definition.EndCondition.HasValue ||
            (definition.EndCondition.Value != HoleWizardAuthoringTemplates.ThroughAllEndCondition &&
             definition.EndCondition.Value != HoleWizardAuthoringTemplates.BlindEndCondition &&
             definition.EndCondition.Value != HoleWizardUpToNextAuthoringTemplates.EndCondition))
        {
            throw new NotSupportedException(
                "Hole Wizard authoring supports ThroughAll (EndCondition=1), the measured Blind " +
                "layout (EndCondition=0) and UpToNext (EndCondition=11). No stream changes have been made.");
        }
        if (!definition.Diameter.HasValue || double.IsNaN(definition.Diameter.Value) ||
            double.IsInfinity(definition.Diameter.Value) || definition.Diameter.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.Diameter),
                "Hole Wizard diameter must be a positive finite number.");
        }
        if (definition.Depth.HasValue &&
            Math.Abs(definition.Depth.Value - HoleWizardAuthoringTemplates.TemplateDepth) > 1e-12)
        {
            throw new NotSupportedException(
                $"Hole Wizard authoring keeps the measured template depth " +
                $"{HoleWizardAuthoringTemplates.TemplateDepth} m; only null or that value is accepted. " +
                "No stream changes have been made.");
        }

        RejectNonNullProperties(definition, new[]
        {
            nameof(IFeatureData.ClassName),
            nameof(IFeatureData.TypeId),
            nameof(FeatureDefinitionData.ResolvedProperties),
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Diameter),
            nameof(HoleWizardFeatureDefinitionData.Depth)
        });
    }

    private static void ValidateBlindCounterBoreDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FolderTypeId.HasValue && definition.FolderTypeId.Value != 0x52u)
            throw new NotSupportedException(
                "Blind counterbore Hole Wizard authoring requires FolderTypeId 0x52. No stream changes have been made.");
        if (definition.Type.HasValue && definition.Type.Value != HoleWizardSegments.CounterBoreTypeValue)
            throw new NotSupportedException(
                "Blind counterbore Hole Wizard authoring requires Type = 14. No stream changes have been made.");
        if (definition.EndCondition != 0)
            throw new NotSupportedException(
                "Blind counterbore Hole Wizard authoring requires EndCondition = 0. No stream changes have been made.");
        var template = GetBlindCounterBoreTemplate(definition);
        ValidateIsoCounterBoreSelection(definition, template.ThruHoleDepth);
        if (definition.SketchPoints != null && definition.SketchPoints.Count != 1)
            throw new NotSupportedException(
                "Blind counterbore Hole Wizard authoring keeps the measured single sketch point. " +
                "No stream changes have been made.");
        RequireMeasuredPoint(definition.SketchPointLocations, 0, template.Point0X, template.Point0Y);
        RejectNonNullProperties(definition, new[]
        {
            nameof(IFeatureData.ClassName),
            nameof(IFeatureData.TypeId),
            nameof(FeatureDefinitionData.ResolvedProperties),
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.SketchPoints),
            nameof(HoleWizardFeatureDefinitionData.SketchPointLocations)
        });
    }

    private static void ValidateTwoPointCounterBoreDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FolderTypeId.HasValue &&
            definition.FolderTypeId.Value != HoleWizardSegments.TwoPointTypeId)
            throw new NotSupportedException(
                "Two-point counterbore Hole Wizard authoring requires FolderTypeId 0x53. No stream changes have been made.");
        if (definition.Type.HasValue && definition.Type.Value != HoleWizardSegments.CounterBoreTypeValue)
            throw new NotSupportedException(
                "Two-point counterbore Hole Wizard authoring requires Type = 14. No stream changes have been made.");
        if (definition.EndCondition != 1)
            throw new NotSupportedException(
                "Two-point counterbore Hole Wizard authoring requires EndCondition = 1. No stream changes have been made.");
        var template = HoleWizardTwoPointCounterBoreTemplates.Source;
        ValidateIsoCounterBoreSelection(definition, template.ThruHoleDepth);
        if (definition.SketchPoints == null || definition.SketchPoints.Count != 2)
            throw new NotSupportedException(
                "Two-point counterbore Hole Wizard authoring requires exactly the two measured sketch points. " +
                "No stream changes have been made.");
        if (definition.SketchPointLocations == null || definition.SketchPointLocations.Count != 2)
            throw new NotSupportedException(
                "Two-point counterbore Hole Wizard authoring requires the two measured sketch point locations. " +
                "No stream changes have been made.");
        RequireMeasuredPoint(definition.SketchPointLocations, 0, template.Point0X, template.Point0Y);
        RequireMeasuredPoint(definition.SketchPointLocations, 1, template.Point1X, template.Point1Y);
        RejectNonNullProperties(definition, new[]
        {
            nameof(IFeatureData.ClassName),
            nameof(IFeatureData.TypeId),
            nameof(FeatureDefinitionData.ResolvedProperties),
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.SketchPoints),
            nameof(HoleWizardFeatureDefinitionData.SketchPointLocations)
        });
    }

    private static void ValidateIsoCounterBoreSelection(
        HoleWizardFeatureDefinitionData definition, double thruHoleDepth)
    {
        RequireMeasuredValue(definition.Standard, HoleWizardSegments.CounterBoreStandard, nameof(definition.Standard));
        RequireMeasuredValue(definition.Standard2, 8, nameof(definition.Standard2));
        RequireMeasuredValue(definition.FastenerType, HoleWizardSegments.CounterBoreFastenerType, nameof(definition.FastenerType));
        RequireMeasuredValue(definition.FastenerType2, 139, nameof(definition.FastenerType2));
        RequireMeasuredValue(definition.FastenerSize, "M6", nameof(definition.FastenerSize));
        RequireMeasuredValue(definition.HoleDiameter, 0.0, nameof(definition.HoleDiameter));
        RequireMeasuredValue(definition.Depth, 0.0, nameof(definition.Depth));
        RequireMeasuredValue(definition.HoleDepth, 0.0, nameof(definition.HoleDepth));
        RequireMeasuredValue(definition.ThruHoleDiameter, 0.006, nameof(definition.ThruHoleDiameter));
        if (!definition.ThruHoleDepth.HasValue ||
            Math.Abs(definition.ThruHoleDepth.Value - thruHoleDepth) > 1e-12)
            throw new NotSupportedException(
                $"Hole Wizard authoring keeps the measured ThruHoleDepth={thruHoleDepth:R}. " +
                "No stream changes have been made.");
        RequireMeasuredValue(definition.CounterBoreDiameter, 0.011, nameof(definition.CounterBoreDiameter));
        RequireMeasuredValue(definition.CounterBoreDepth, 0.006, nameof(definition.CounterBoreDepth));
        RequireMeasuredValue(definition.CounterSinkDiameter, 0.0, nameof(definition.CounterSinkDiameter));
        RequireMeasuredValue(definition.CounterSinkAngle, 0.0, nameof(definition.CounterSinkAngle));
        RequireMeasuredValue(definition.ThreadDiameter, 0.0, nameof(definition.ThreadDiameter));
        RequireMeasuredValue(definition.ThreadDepth, 0.0, nameof(definition.ThreadDepth));
    }

    private static void RequireMeasuredPoint(
        IReadOnlyList<HoleWizardSketchPointLocation>? locations,
        int index, double expectedX, double expectedY)
    {
        if (locations == null) return;
        if (index >= locations.Count)
            throw new NotSupportedException(
                "Hole Wizard authoring keeps the measured sketch point locations. " +
                "No stream changes have been made.");
        var point = locations[index];
        if (Math.Abs(point.X - expectedX) > 1e-9 || Math.Abs(point.Y - expectedY) > 1e-9)
            throw new NotSupportedException(
                $"Hole Wizard authoring keeps the measured sketch point ({expectedX:R}, {expectedY:R}). " +
                "No stream changes have been made.");
    }

    private static HoleWizardMeasuredCounterBoreTemplate GetBlindCounterBoreTemplate(
        HoleWizardFeatureDefinitionData definition)
    {
        if (!definition.ThruHoleDepth.HasValue)
            throw new NotSupportedException(
                "Blind counterbore Hole Wizard authoring requires the measured ThruHoleDepth " +
                "(0.010 m or 0.008 m). No stream changes have been made.");
        return HoleWizardBlindCounterBoreTemplates.FindByDepth(definition.ThruHoleDepth.Value) ??
               throw new NotSupportedException(
                   "Blind counterbore Hole Wizard authoring supports the measured 0.010 m and " +
                   "0.008 m blind layouts only. No stream changes have been made.");
    }

    private static byte[] BuildBlindCounterBore(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        var template = GetBlindCounterBoreTemplate(definition);
        if (BitConverter.ToUInt16(currentStream, 0) != HoleWizardSegments.LegacyStreamVersion)
            throw new NotSupportedException(
                "Blind counterbore Hole Wizard authoring requires a ResolvedFeatures stream version 0x71. " +
                "No stream changes have been made.");
        EnsureNoExistingHoleAndFolderCount(currentStream, template.StrippedFolderCount);
        if (currentStream.Length != template.BaseLength)
            throw new NotSupportedException(
                $"Blind counterbore Hole Wizard authoring requires the measured stripped base length " +
                $"{template.BaseLength}. No stream changes have been made.");
        RequireBaseSha(currentStream, template.BaseSha256, "blind counterbore " + template.Sample);
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Blind counterbore Hole Wizard authoring uses the measured native feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        return AppendToStream(currentStream, (byte[])template.Block.Clone());
    }

    private static byte[] BuildTwoPointCounterBore(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        var template = HoleWizardTwoPointCounterBoreTemplates.Source;
        if (BitConverter.ToUInt16(currentStream, 0) != HoleWizardSegments.LegacyStreamVersion)
            throw new NotSupportedException(
                "Two-point counterbore Hole Wizard authoring requires a ResolvedFeatures stream version 0x71. " +
                "No stream changes have been made.");
        EnsureNoExistingHoleAndFolderCount(currentStream, template.StrippedFolderCount);
        if (currentStream.Length != template.BaseLength)
            throw new NotSupportedException(
                $"Two-point counterbore Hole Wizard authoring requires the measured stripped base length " +
                $"{template.BaseLength}. No stream changes have been made.");
        RequireBaseSha(currentStream, template.BaseSha256, "two-point counterbore " + template.Sample);
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Two-point counterbore Hole Wizard authoring uses the measured native feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        return AppendToStream(currentStream, (byte[])template.Block.Clone());
    }

    private static void ValidateCounterBoreDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FolderTypeId.HasValue && definition.FolderTypeId.Value != 0x52u)
            throw new NotSupportedException(
                "Counterbore Hole Wizard authoring requires FolderTypeId 0x52. No stream changes have been made.");
        if (definition.Type.HasValue && definition.Type.Value != HoleWizardSegments.CounterBoreTypeValue)
            throw new NotSupportedException(
                "Counterbore Hole Wizard authoring requires Type = 14. No stream changes have been made.");
        if (definition.EndCondition.HasValue && definition.EndCondition.Value != 1)
            throw new NotSupportedException(
                "Counterbore Hole Wizard authoring supports EndCondition = 1 only. No stream changes have been made.");
        RequireMeasuredValue(definition.EndCondition, 1, nameof(definition.EndCondition));
        bool ansi = IsAnsiCounterBoreDefinition(definition);
        if (ansi)
        {
            RequireMeasuredValue(definition.Standard, HoleWizardSegments.CounterBoreAnsiStandard, nameof(definition.Standard));
            RequireMeasuredValue(definition.Standard2, 1, nameof(definition.Standard2));
            RequireMeasuredValue(definition.FastenerType, HoleWizardSegments.CounterBoreAnsiFastenerType, nameof(definition.FastenerType));
            RequireMeasuredValue(definition.FastenerType2, 33, nameof(definition.FastenerType2));
        }
        else
        {
            RequireMeasuredValue(definition.Standard, "ISO", nameof(definition.Standard));
            RequireMeasuredValue(definition.Standard2, 8, nameof(definition.Standard2));
            RequireMeasuredValue(definition.FastenerType, "六角凹头 ISO 4762", nameof(definition.FastenerType));
            RequireMeasuredValue(definition.FastenerType2, 139, nameof(definition.FastenerType2));
        }
        RequireMeasuredValue(definition.FastenerSize, "M6", nameof(definition.FastenerSize));
        RequireMeasuredValue(definition.HoleDiameter, 0.0, nameof(definition.HoleDiameter));
        RequireMeasuredValue(definition.Depth, 0.0, nameof(definition.Depth));
        RequireMeasuredValue(definition.HoleDepth, 0.0, nameof(definition.HoleDepth));
        if (definition.ThruHoleDiameter.HasValue &&
            !HoleWizardSegments.IsAllowedCounterBoreThruDiameter(definition.ThruHoleDiameter.Value))
            throw new NotSupportedException(
                "Counterbore Hole Wizard authoring supports the measured Ø6/Ø8/Ø10 through-hole " +
                "diameters. No stream changes have been made.");
        RequireMeasuredValue(definition.ThruHoleDepth, 0.010, nameof(definition.ThruHoleDepth));
        if (definition.CounterBoreDiameter.HasValue &&
            !HoleWizardSegments.IsAllowedCounterBoreDiameter(definition.CounterBoreDiameter.Value))
            throw new NotSupportedException(
                "Counterbore Hole Wizard authoring supports the measured Ø11/Ø13 counterbores. " +
                "No stream changes have been made.");
        if (definition.CounterBoreDepth.HasValue &&
            !HoleWizardSegments.IsAllowedCounterBoreDepth(definition.CounterBoreDepth.Value))
            throw new NotSupportedException(
                "Counterbore Hole Wizard authoring supports the measured 4/6/8 mm counterbore depths. " +
                "No stream changes have been made.");
        RequireMeasuredValue(definition.CounterSinkDiameter, 0.0, nameof(definition.CounterSinkDiameter));
        RequireMeasuredValue(definition.CounterSinkAngle, 0.0, nameof(definition.CounterSinkAngle));
        RequireMeasuredValue(definition.ThreadDiameter, 0.0, nameof(definition.ThreadDiameter));
        RequireMeasuredValue(definition.ThreadDepth, 0.0, nameof(definition.ThreadDepth));

        RejectNonNullProperties(definition, new[]
        {
            nameof(IFeatureData.ClassName),
            nameof(IFeatureData.TypeId),
            nameof(FeatureDefinitionData.ResolvedProperties),
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth)
        });
    }

    private static byte[] BuildCounterBore(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != HoleWizardCounterBoreAuthoringTemplates.StreamVersion)
        {
            throw new NotSupportedException(
                $"Counterbore Hole Wizard authoring requires a ResolvedFeatures stream version 0x71, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        EnsureNoExistingHoleAndFolderCount(
            currentStream, HoleWizardCounterBoreAuthoringTemplates.StrippedFolderCount);
        if (IsAnsiCounterBoreDefinition(definition))
        {
            byte[] normalized = (byte[])currentStream.Clone();
            normalized[4] = 0;
            normalized[5] = 0;
            string sha;
            using (var algorithm = SHA256.Create())
                sha = BitConverter.ToString(algorithm.ComputeHash(normalized))
                    .Replace("-", string.Empty).ToLowerInvariant();
            byte[]? block = null;
            if (currentStream.Length == HoleWizardCounterBoreAnsiAuthoringTemplates.Source.Length &&
                string.Equals(sha, HoleWizardCounterBoreAnsiAuthoringTemplates.Source.Sha256,
                    StringComparison.Ordinal))
                block = HoleWizardCounterBoreAnsiAuthoringTemplates.Source.Block;
            else if (currentStream.Length == HoleWizardCounterBoreAnsiAuthoringTemplates.Resave.Length &&
                     string.Equals(sha, HoleWizardCounterBoreAnsiAuthoringTemplates.Resave.Sha256,
                         StringComparison.Ordinal))
                block = HoleWizardCounterBoreAnsiAuthoringTemplates.Resave.Block;
            if (block == null)
                throw new NotSupportedException(
                    "ANSI Metric counterbore authoring requires the exact measured source or " +
                    "native resave base (signature mismatch). No stream changes have been made.");
            if (!string.Equals(featureName, HoleWizardCounterBoreAnsiAuthoringTemplates.FeatureName,
                    StringComparison.Ordinal))
                throw new NotSupportedException(
                    $"ANSI Metric counterbore authoring uses the measured native feature name " +
                    $"'{HoleWizardCounterBoreAnsiAuthoringTemplates.FeatureName}'. " +
                    "No stream changes have been made.");
            return AppendToStream(currentStream, (byte[])block.Clone());
        }

        RequireBaseSha(currentStream, HoleWizardCounterBoreAuthoringTemplates.BaseSha256,
            "hole_counterbore_iso_m6_11_6");
        if (!string.Equals(featureName, HoleWizardCounterBoreAuthoringTemplates.FeatureName,
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Counterbore Hole Wizard authoring uses the measured native feature name " +
                $"'{HoleWizardCounterBoreAuthoringTemplates.FeatureName}'. No stream changes have been made.");
        }
        var isoBlock = (byte[])HoleWizardCounterBoreAuthoringTemplates.Block.Clone();
        HoleWizardParameterSlots.PatchCounterBore(
            isoBlock,
            definition.ThruHoleDiameter ?? 0.006,
            definition.CounterBoreDiameter ?? 0.011,
            definition.CounterBoreDepth ?? 0.006);
        return AppendToStream(currentStream, isoBlock);
    }

    private static void ValidateCounterSinkDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FolderTypeId.HasValue && definition.FolderTypeId.Value != 0x52u)
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring requires FolderTypeId 0x52. No stream changes have been made.");
        if (definition.Type.HasValue && definition.Type.Value != HoleWizardSegments.CounterSinkTypeValue)
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring requires Type = 44. No stream changes have been made.");
        if (definition.EndCondition.HasValue && definition.EndCondition.Value != 1)
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring supports EndCondition = 1 only. No stream changes have been made.");
        RequireMeasuredValue(definition.Standard, "ISO", nameof(definition.Standard));
        RequireMeasuredValue(definition.Standard2, 8, nameof(definition.Standard2));
        RequireMeasuredValue(definition.FastenerType, "锥孔平头 ISO 7046-1", nameof(definition.FastenerType));
        RequireMeasuredValue(definition.FastenerType2, 141, nameof(definition.FastenerType2));
        if (definition.FastenerSize != null && definition.FastenerSize != "M6" && definition.FastenerSize != "M8")
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring supports the measured M6 and M8 sizes. " +
                "No stream changes have been made.");
        RequireMeasuredValue(definition.HoleDiameter, 0.0, nameof(definition.HoleDiameter));
        RequireMeasuredValue(definition.Depth, 0.0, nameof(definition.Depth));
        RequireMeasuredValue(definition.HoleDepth, 0.0, nameof(definition.HoleDepth));
        RequireMeasuredValue(definition.ThruHoleDepth, 0.010, nameof(definition.ThruHoleDepth));
        RequireMeasuredValue(definition.CounterBoreDiameter, 0.0, nameof(definition.CounterBoreDiameter));
        RequireMeasuredValue(definition.CounterBoreDepth, 0.0, nameof(definition.CounterBoreDepth));
        if (definition.CounterSinkAngle.HasValue &&
            !HoleWizardSegments.IsAllowedCounterSinkAngle(definition.CounterSinkAngle.Value))
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring supports the measured 90/100/120-degree angles. " +
                "No stream changes have been made.");
        RequireMeasuredValue(definition.ThreadDiameter, 0.0, nameof(definition.ThreadDiameter));
        RequireMeasuredValue(definition.ThreadDepth, 0.0, nameof(definition.ThreadDepth));

        if (definition.FastenerSize != null && definition.CounterSinkDiameter.HasValue &&
            !IsAllowedCounterSinkPair(definition.FastenerSize, definition.ThruHoleDiameter ?? 0,
                definition.CounterSinkDiameter.Value))
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring supports M6/Ø6 thru with Ø11/Ø13/Ø17 countersink " +
                "and M8/Ø8 thru with Ø15 countersink. No stream changes have been made.");

        RejectNonNullProperties(definition, new[]
        {
            nameof(IFeatureData.ClassName),
            nameof(IFeatureData.TypeId),
            nameof(FeatureDefinitionData.ResolvedProperties),
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth)
        });
    }

    private static bool IsAllowedCounterSinkPair(
        string size, double thruDiameter, double counterSinkDiameter) =>
        size == "M6"
            ? Math.Abs(thruDiameter - 0.006) <= 1e-12 &&
              HoleWizardSegments.IsAllowedCounterSinkDiameter("M6", counterSinkDiameter)
            : size == "M8" && Math.Abs(thruDiameter - 0.008) <= 1e-12 &&
              HoleWizardSegments.IsAllowedCounterSinkDiameter("M8", counterSinkDiameter);

    internal static string? CounterSinkFeatureNameFor(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FastenerSize != null && definition.CounterSinkDiameter.HasValue)
            return HoleWizardCounterSinkAuthoringTemplates.FeatureNameFor(
                definition.FastenerSize, definition.CounterSinkDiameter.Value);
        return null;
    }

    private static byte[] BuildCounterSink(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != HoleWizardCounterSinkAuthoringTemplates.StreamVersion)
        {
            throw new NotSupportedException(
                $"Countersink Hole Wizard authoring requires a ResolvedFeatures stream version 0x71, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        EnsureNoExistingHoleAndFolderCount(
            currentStream, HoleWizardCounterSinkAuthoringTemplates.StrippedFolderCount);
        if (definition.FastenerSize == null || !definition.CounterSinkDiameter.HasValue ||
            !definition.ThruHoleDiameter.HasValue)
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring requires FastenerSize, ThruHoleDiameter and " +
                "CounterSinkDiameter. No stream changes have been made.");
        byte[] normalized = (byte[])currentStream.Clone();
        normalized[4] = 0;
        normalized[5] = 0;
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(normalized))
                .Replace("-", string.Empty).ToLowerInvariant();
        var template = HoleWizardCounterSinkAuthoringTemplates.SelectByBase(
            definition.FastenerSize, definition.ThruHoleDiameter.Value,
            currentStream.Length, sha);
        if (template == null)
            throw new NotSupportedException(
                "Countersink Hole Wizard authoring requires the exact measured M6/M8 source or " +
                "native resave base (signature mismatch). No stream changes have been made.");
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Countersink Hole Wizard authoring uses the measured native feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        var block = (byte[])template.Block.Clone();
        HoleWizardParameterSlots.PatchCounterSink(
            block,
            string.Equals(template.FastenerSize, "M6", StringComparison.Ordinal),
            definition.CounterSinkDiameter ?? template.CounterSinkDiameter,
            definition.CounterSinkAngle ?? Math.PI / 2.0);
        return AppendToStream(currentStream, block);
    }

    private static void ValidateTappedDefinition(HoleWizardFeatureDefinitionData definition)
    {
        if (definition.FolderTypeId.HasValue &&
            definition.FolderTypeId.Value != HoleWizardTappedAuthoringTemplates.FolderTypeId)
        {
            throw new NotSupportedException(
                "Tapped Hole Wizard authoring requires FolderTypeId 0x52. No stream changes have been made.");
        }
        if (definition.Type.HasValue && definition.Type.Value != HoleWizardTappedAuthoringTemplates.TypeValue)
        {
            throw new NotSupportedException(
                "Tapped Hole Wizard authoring requires Type = 48. No stream changes have been made.");
        }
        if (definition.EndCondition.HasValue &&
            definition.EndCondition.Value != HoleWizardTappedAuthoringTemplates.EndCondition)
        {
            throw new NotSupportedException(
                "Tapped Hole Wizard authoring currently supports EndCondition = 1 only. " +
                "No stream changes have been made.");
        }
        if (definition.ThreadDiameter.HasValue)
        {
            if (!IsPositiveFinite(definition.ThreadDiameter))
            {
                throw new ArgumentOutOfRangeException(nameof(definition.ThreadDiameter),
                    "Tapped Hole Wizard thread diameter must be a positive finite number.");
            }
            if (Math.Abs(definition.ThreadDiameter.Value -
                         HoleWizardTappedAuthoringTemplates.MeasuredThreadDiameter) > 1e-12)
            {
                throw new NotSupportedException(
                    $"Tapped Hole Wizard authoring keeps the measured ThreadDiameter " +
                    $"{HoleWizardTappedAuthoringTemplates.MeasuredThreadDiameter} m; only null or that value " +
                    "is accepted. Native verification showed that other thread diameters change only the " +
                    "callout/definition, not the hole geometry or volume. No stream changes have been made.");
            }
        }
        if (definition.ThreadDepth.HasValue &&
            Math.Abs(definition.ThreadDepth.Value - HoleWizardAuthoringTemplates.TemplateDepth) > 1e-12)
        {
            throw new NotSupportedException(
                $"Tapped Hole Wizard authoring keeps the measured ThreadDepth " +
                $"{HoleWizardAuthoringTemplates.TemplateDepth} m; only null or that value is accepted. " +
                "No stream changes have been made.");
        }
        RequireMeasuredValue(definition.Standard, "ISO", nameof(definition.Standard));
        RequireMeasuredValue(definition.Standard2, 8, nameof(definition.Standard2));
        RequireMeasuredValue(definition.FastenerType, "螺纹孔", nameof(definition.FastenerType));
        RequireMeasuredValue(definition.FastenerType2, 147, nameof(definition.FastenerType2));
        RequireMeasuredValue(definition.FastenerSize, "M6", nameof(definition.FastenerSize));
        RequireMeasuredValue(definition.CosmeticThreadType, 1, nameof(definition.CosmeticThreadType));

        RejectNonNullProperties(definition, new[]
        {
            nameof(IFeatureData.ClassName),
            nameof(IFeatureData.TypeId),
            nameof(FeatureDefinitionData.ResolvedProperties),
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.CosmeticThreadType)
        });
    }

    private static byte[] BuildLegacy(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != HoleWizardAuthoringTemplates.StreamVersion)
        {
            throw new NotSupportedException(
                $"Legacy Hole Wizard authoring requires a ResolvedFeatures stream version 0x71, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        EnsureNoExistingHoleAndFolderCount(
            currentStream, HoleWizardAuthoringTemplates.ExpectedStrippedFolderCount);

        bool blind = definition.EndCondition == HoleWizardAuthoringTemplates.BlindEndCondition;
        bool upToNext = definition.EndCondition == HoleWizardUpToNextAuthoringTemplates.EndCondition;
        string expectedSha = upToNext
            ? HoleWizardUpToNextAuthoringTemplates.BaseSha256
            : blind
                ? HoleWizardAuthoringTemplates.BlindBaseSha256
                : HoleWizardAuthoringTemplates.ThroughAllBaseSha256;
        RequireBaseSha(currentStream, expectedSha,
            upToNext ? "hole_up_to_next" : blind ? "hole_simple_d5_blind10" : "hole_simple_d5_throughall");

        byte[] tail = (byte[])(upToNext
            ? HoleWizardUpToNextAuthoringTemplates.Tail
            : blind
                ? HoleWizardAuthoringTemplates.BlindTail
                : HoleWizardAuthoringTemplates.ThroughAllTail).Clone();
        BitConverter.GetBytes(definition.Diameter!.Value)
            .CopyTo(tail, HoleWizardAuthoringTemplates.DiameterTailOffset);
        BitConverter.GetBytes(definition.Diameter.Value)
            .CopyTo(tail, HoleWizardAuthoringTemplates.DiameterCopyTailOffset);
        byte[] headerRest = (byte[])(upToNext
            ? HoleWizardUpToNextAuthoringTemplates.HeaderRest
            : blind
                ? HoleWizardAuthoringTemplates.BlindHeaderRest
                : HoleWizardAuthoringTemplates.ThroughAllHeaderRest).Clone();
        byte[] block = Append(headerRest, tail, definition, featureName);
        return AppendToStream(currentStream, block);
    }

    private static byte[] BuildTapped(
        byte[] currentStream, HoleWizardFeatureDefinitionData definition, string featureName)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != HoleWizardTappedAuthoringTemplates.StreamVersion)
        {
            throw new NotSupportedException(
                $"Tapped Hole Wizard authoring requires a ResolvedFeatures stream version 0x84, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        EnsureNoExistingHoleAndFolderCount(
            currentStream, HoleWizardTappedAuthoringTemplates.ExpectedStrippedFolderCount);
        RequireBaseSha(currentStream, HoleWizardTappedAuthoringTemplates.BaseSha256,
            "hole_tapped_m6");

        byte[] tail = (byte[])HoleWizardTappedAuthoringTemplates.Tail.Clone();
        double threadDiameter = definition.ThreadDiameter ??
            HoleWizardTappedAuthoringTemplates.MeasuredThreadDiameter;
        double threadDepth = HoleWizardAuthoringTemplates.TemplateDepth;
        foreach (int slot in HoleWizardTappedAuthoringTemplates.ThreadDiameterTailOffsets)
            BitConverter.GetBytes(threadDiameter).CopyTo(tail, slot);
        BitConverter.GetBytes(threadDiameter)
            .CopyTo(tail, HoleWizardTappedAuthoringTemplates.ThreadDiameterDisplayCopyTailOffset);
        foreach (int slot in HoleWizardTappedAuthoringTemplates.ThreadDepthTailOffsets)
            BitConverter.GetBytes(threadDepth).CopyTo(tail, slot);
        byte[] headerRest = (byte[])HoleWizardTappedAuthoringTemplates.HeaderRest.Clone();
        byte[] block = Append(headerRest, tail, definition, featureName,
            HoleWizardTappedAuthoringTemplates.FolderTypeId);
        return AppendToStream(currentStream, block);
    }

    private static byte[] AppendToStream(byte[] currentStream, byte[] featureBlock)
    {
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    private static byte[] Append(
        byte[] headerRest, byte[] tail, HoleWizardFeatureDefinitionData definition,
        string featureName, uint typeId = HoleWizardAuthoringTemplates.LegacyTypeId)
    {
        if (string.IsNullOrEmpty(featureName)) featureName = "孔1";
        using var writer = new MfcStreamWriter(41000);
        writer.WriteMfcTag(HoleWizardSegments.ClassName);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(typeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(headerRest);
        writer.WriteBytes(tail);
        return writer.ToArray();
    }

    private static void EnsureNoExistingHoleAndFolderCount(byte[] currentStream, ushort expectedFolderCount)
    {
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, HoleWizardSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Hole Wizard authoring supports only the stripped base without an existing hole feature. " +
                "No stream changes have been made.");
        }
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != expectedFolderCount)
        {
            throw new NotSupportedException(
                $"Hole Wizard authoring requires the hole-stripped base with folder count " +
                $"{expectedFolderCount}, but the document uses {folderCount}. No stream changes have been made.");
        }
    }

    private static void RequireBaseSha(byte[] currentStream, string expectedSha, string baseName)
    {
        byte[] normalized = (byte[])currentStream.Clone();
        normalized[4] = 0;
        normalized[5] = 0;
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, expectedSha, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Hole Wizard authoring is limited to the controlled {baseName} stripped base " +
                $"(base signature {sha.Substring(0, 12)} does not match). No stream changes have been made.");
        }
    }

    private static void RejectNonNullProperties(
        HoleWizardFeatureDefinitionData definition, string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in typeof(HoleWizardFeatureDefinitionData)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (allowedSet.Contains(property.Name)) continue;
            if (property.GetValue(definition) != null)
            {
                throw new NotSupportedException(
                    $"Hole Wizard authoring does not yet support {property.Name}. " +
                    "No stream changes have been made.");
            }
        }
    }

    private static void RequireMeasuredValue<T>(T? actual, T expected, string property) where T : struct
    {
        if (actual.HasValue && !actual.Value.Equals(expected))
        {
            throw new NotSupportedException(
                $"Tapped Hole Wizard authoring keeps the measured {property} default ({expected}). " +
                "No stream changes have been made.");
        }
    }

    private static void RequireMeasuredValue(string? actual, string expected, string property)
    {
        if (actual != null && !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Tapped Hole Wizard authoring keeps the measured {property} default ({expected}). " +
                "No stream changes have been made.");
        }
    }

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) &&
        value.Value > 0.0;
}
