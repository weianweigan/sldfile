using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed authoring for the measured tutorial HoleWizard families whose hole folder is the
/// last folder in the ResolvedFeatures stream. Templates are keyed by the exact stripped-base
/// length plus normalized SHA-256; the measured native block is appended only after the
/// first-u16 version, stripped folder count, absent existing hole, marker and measured feature
/// name all match. Optional definition fields are compared with the native API snapshot of the
/// same sample; unsupported non-null fields are rejected before any stream change.
/// </summary>
internal static class LegacyHoleWizardTailBuilder
{
    internal static LegacyHoleWizardTailAuthoringTemplate? FindTemplate(byte[] stream) =>
        LegacyHoleWizardTailAuthoringTemplates.FindByBase(stream);

    internal static byte[] Build(
        byte[] currentStream,
        LegacyHoleWizardTailAuthoringTemplate template,
        HoleWizardFeatureDefinitionData definition,
        string featureName)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        ValidateDefinition(template, definition);
        if (currentStream == null || currentStream.Length != template.BaseLength)
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring requires the measured stripped base length " +
                $"{template.BaseLength} for {template.Sample}. No stream changes have been made.");
        if (BitConverter.ToUInt16(currentStream, 0) != template.StreamVersion)
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring requires first stream value " +
                $"0x{template.StreamVersion:X} for {template.Sample}. No stream changes have been made.");
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring requires stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, HoleWizardSegments.ClassName) > 0)
            throw new NotSupportedException(
                "Legacy tutorial HoleWizard authoring requires a base without an existing hole. " +
                "No stream changes have been made.");
        if (template.MarkerOffset >= 0)
        {
            if (template.MarkerOffset + 8 > currentStream.Length ||
                currentStream[template.MarkerOffset] != 0x67 ||
                currentStream[template.MarkerOffset + 1] != 0 ||
                currentStream[template.MarkerOffset + 2] != 0 ||
                currentStream[template.MarkerOffset + 3] != 0 ||
                BitConverter.ToUInt32(currentStream, template.MarkerOffset + 4) != template.MarkerValue)
                throw new NotSupportedException(
                    "Legacy tutorial HoleWizard authoring marker does not match the measured base. " +
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
                "Legacy tutorial HoleWizard authoring requires the exact measured base " +
                "(normalized signature mismatch). No stream changes have been made.");
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        var newStream = new byte[currentStream.Length + template.BlockLength];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.BlockLength);
        return newStream;
    }

    private static void ValidateDefinition(
        LegacyHoleWizardTailAuthoringTemplate template, HoleWizardFeatureDefinitionData definition)
    {
        RequireInt(definition.Type, template.Type, nameof(definition.Type));
        RequireInt(definition.EndCondition, template.EndCondition, nameof(definition.EndCondition));
        RequireString(definition.Standard, template.Standard, nameof(definition.Standard));
        RequireString(definition.FastenerType, template.FastenerType, nameof(definition.FastenerType));
        RequireString(definition.FastenerSize, template.FastenerSize, nameof(definition.FastenerSize));
        RequireDouble(definition.ThruHoleDiameter, template.ThruHoleDiameter, nameof(definition.ThruHoleDiameter));
        RequireDouble(definition.ThruHoleDepth, template.ThruHoleDepth, nameof(definition.ThruHoleDepth));
        RequireDouble(definition.CounterBoreDiameter, template.CounterBoreDiameter, nameof(definition.CounterBoreDiameter));
        RequireDouble(definition.CounterBoreDepth, template.CounterBoreDepth, nameof(definition.CounterBoreDepth));
        RequireDouble(definition.CounterSinkDiameter, template.CounterSinkDiameter, nameof(definition.CounterSinkDiameter));
        RequireDouble(definition.CounterSinkAngle, template.CounterSinkAngle, nameof(definition.CounterSinkAngle));
        RequireDouble(definition.ThreadDiameter, template.ThreadDiameter, nameof(definition.ThreadDiameter));
        RequireDouble(definition.ThreadDepth, template.ThreadDepth, nameof(definition.ThreadDepth));
        if (definition.SketchPoints != null && template.SketchPointCount.HasValue &&
            definition.SketchPoints.Count != template.SketchPointCount.Value)
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring keeps {template.SketchPointCount} sketch point(s). " +
                "No stream changes have been made.");

        foreach (var property in typeof(HoleWizardFeatureDefinitionData)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(FeatureDefinitionData.ResolvedProperties):
                case nameof(HoleWizardFeatureDefinitionData.FolderTypeId):
                case nameof(HoleWizardFeatureDefinitionData.Type):
                case nameof(HoleWizardFeatureDefinitionData.EndCondition):
                case nameof(HoleWizardFeatureDefinitionData.Standard):
                case nameof(HoleWizardFeatureDefinitionData.Standard2):
                case nameof(HoleWizardFeatureDefinitionData.FastenerType):
                case nameof(HoleWizardFeatureDefinitionData.FastenerType2):
                case nameof(HoleWizardFeatureDefinitionData.FastenerSize):
                case nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter):
                case nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth):
                case nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter):
                case nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth):
                case nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter):
                case nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle):
                case nameof(HoleWizardFeatureDefinitionData.ThreadDiameter):
                case nameof(HoleWizardFeatureDefinitionData.ThreadDepth):
                case nameof(HoleWizardFeatureDefinitionData.HoleDiameter):
                case nameof(HoleWizardFeatureDefinitionData.HoleDepth):
                case nameof(HoleWizardFeatureDefinitionData.Depth):
                case nameof(HoleWizardFeatureDefinitionData.SketchPoints):
                case nameof(HoleWizardFeatureDefinitionData.SketchPointLocations):
                    continue;
            }
            if (property.GetValue(definition) != null)
                throw new NotSupportedException(
                    $"Legacy tutorial HoleWizard authoring does not support {property.Name}. " +
                    "No stream changes have been made.");
        }
    }

    private static void RequireInt(int? actual, int? expected, string property)
    {
        if (!actual.HasValue) return;
        if (!expected.HasValue || actual.Value != expected.Value)
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring keeps {property}={expected?.ToString() ?? "null"}. " +
                "No stream changes have been made.");
    }

    private static void RequireDouble(double? actual, double? expected, string property)
    {
        if (!actual.HasValue) return;
        if (!expected.HasValue || Math.Abs(actual.Value - expected.Value) > 1e-12)
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring keeps {property}={expected?.ToString() ?? "null"}. " +
                "No stream changes have been made.");
    }

    private static void RequireString(string? actual, string? expected, string property)
    {
        if (actual == null) return;
        if (expected == null || !string.Equals(actual, expected, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Legacy tutorial HoleWizard authoring keeps {property}='{expected ?? "null"}'. " +
                "No stream changes have been made.");
    }
}
