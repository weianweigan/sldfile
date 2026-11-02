using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fail-fast builders for the controlled PatternOptions authoring slices:
/// linear (one/two direction) and circular (equal spacing on/off) patterns.
/// Legacy fixed bases use the measured serializer; native variant bases use complete measured
/// blocks so the instance caches in the other OLE streams stay consistent. Requires stream
/// version 0x72, a parsed sketch chain, the 0x67 marker, the exact base signature and the
/// measured parameter set for that base.
/// </summary>
internal static class PatternFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";

    public static byte[] BuildLinearPatternStream(
        byte[] currentStream,
        LinearPatternFeatureDefinitionData def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateLinearDefinition(def);
        var native = TryGetNativeTemplate(currentStream, PatternNativeMode.Linear);
        if (native != null)
        {
            ValidateNativeLinearValues(def, native, featureName);
            EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
                native.ClassName, native.BaseSha256, "linear");
            return Append(currentStream, (byte[])native.Block.Clone());
        }

        ValidateLegacyLinearValues(def);
        EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
            PatternSegments.LinearPatternClassName, PatternSegments.LinearBasePrefixSha256, "linear");
        byte[] block = MfcPatternSerializer.SerializeLinearPattern(def, featureName);
        return Append(currentStream, block);
    }

    public static byte[] BuildCircularPatternStream(
        byte[] currentStream,
        CircularPatternFeatureDefinitionData def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateCircularDefinition(def);
        var native = TryGetNativeTemplate(currentStream, PatternNativeMode.Circular);
        if (native != null)
        {
            ValidateNativeCircularValues(def, native, featureName);
            EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
                native.ClassName, native.BaseSha256, "circular");
            return Append(currentStream, (byte[])native.Block.Clone());
        }

        ValidateLegacyCircularValues(def);
        EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
            PatternSegments.CircularPatternClassName, PatternSegments.CircularBasePrefixSha256, "circular");
        byte[] block = MfcPatternSerializer.SerializeCircularPattern(def, featureName);
        return Append(currentStream, block);
    }

    /// <summary>
    /// Returns the native linear/circular template whose stripped base signature matches the
    /// current ResolvedFeatures stream, or null for the legacy fixed templates.
    /// </summary>
    internal static PatternNativeAuthoringTemplate? TryGetNativeTemplate(
        byte[] currentStream, PatternNativeMode mode)
    {
        if (currentStream == null || currentStream.Length < 16) return null;
        string sha = NormalizedSha(currentStream);
        return PatternNativeAuthoringTemplates.All.FirstOrDefault(t =>
            t.Mode == mode && t.BaseLength == currentStream.Length &&
            string.Equals(t.BaseSha256, sha, StringComparison.Ordinal));
    }

    internal static void ValidateLinearDefinition(LinearPatternFeatureDefinitionData def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));

        ValidateCount(def.D1TotalInstances, nameof(def.D1TotalInstances));
        ValidateCountAllowOne(def.D2TotalInstances, nameof(def.D2TotalInstances));
        ValidateSpacing(def.D1Spacing, nameof(def.D1Spacing));
        ValidateSpacing(def.D2Spacing, nameof(def.D2Spacing));
        ValidateAxisType(def.D1AxisType, nameof(def.D1AxisType));
        ValidateAxisType(def.D2AxisType, nameof(def.D2AxisType));

        foreach (var property in typeof(LinearPatternFeatureDefinitionData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(LinearPatternFeatureDefinitionData.D1TotalInstances):
                case nameof(LinearPatternFeatureDefinitionData.D2TotalInstances):
                case nameof(LinearPatternFeatureDefinitionData.D1Spacing):
                case nameof(LinearPatternFeatureDefinitionData.D2Spacing):
                case nameof(LinearPatternFeatureDefinitionData.D1AxisType):
                case nameof(LinearPatternFeatureDefinitionData.D2AxisType):
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(FeatureDefinitionData.ResolvedProperties):
                    continue;
                case nameof(LinearPatternFeatureDefinitionData.GeometryPattern):
                case nameof(LinearPatternFeatureDefinitionData.VarySketch):
                case nameof(LinearPatternFeatureDefinitionData.InstancesToVary):
                    if (property.GetValue(def) != null)
                        throw new NotSupportedException(
                            $"Linear pattern authoring requires {property.Name} to be left at the measured " +
                            "template default (null); the template flag value is not structurally decoded. " +
                            "No stream changes have been made.");
                    continue;
            }

            if (property.GetValue(def) != null)
            {
                throw new NotSupportedException(
                    $"Linear pattern authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }
    }

    internal static void ValidateCircularDefinition(CircularPatternFeatureDefinitionData def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));

        ValidateCount(def.TotalInstances, nameof(def.TotalInstances));
        if (!def.Spacing.HasValue || double.IsNaN(def.Spacing.Value) || double.IsInfinity(def.Spacing.Value) ||
            def.Spacing.Value <= 0.0 || def.Spacing.Value > 2.0 * Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Spacing),
                $"Circular pattern spacing must be a finite value in (0, 2*pi] radians, but received {def.Spacing}.");
        }
        ValidateAxisType(def.AxisType, nameof(def.AxisType));

        foreach (var property in typeof(CircularPatternFeatureDefinitionData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(CircularPatternFeatureDefinitionData.TotalInstances):
                case nameof(CircularPatternFeatureDefinitionData.Spacing):
                case nameof(CircularPatternFeatureDefinitionData.AxisType):
                case nameof(CircularPatternFeatureDefinitionData.EqualSpacing):
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(FeatureDefinitionData.ResolvedProperties):
                    continue;
                case nameof(CircularPatternFeatureDefinitionData.GeometryPattern):
                case nameof(CircularPatternFeatureDefinitionData.ReverseDirection):
                case nameof(CircularPatternFeatureDefinitionData.Symmetric):
                case nameof(CircularPatternFeatureDefinitionData.Direction2):
                case nameof(CircularPatternFeatureDefinitionData.InstancesToVary):
                    if (property.GetValue(def) != null)
                        throw new NotSupportedException(
                            $"Circular pattern authoring requires {property.Name} to be left at the measured " +
                            "template default (null); the template flag value is not structurally decoded. " +
                            "No stream changes have been made.");
                    continue;
            }

            if (property.GetValue(def) != null)
            {
                throw new NotSupportedException(
                    $"Circular pattern authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }
    }

    private static void ValidateLegacyLinearValues(LinearPatternFeatureDefinitionData def)
    {
        // The legacy serializer regenerates only the ResolvedFeatures block; its other OLE
        // streams are the old 2x3/10/15 specimen, so only that exact parameter set is safe.
        if (def.D1TotalInstances != 2 || def.D2TotalInstances != 3 ||
            Math.Abs(def.D1Spacing!.Value - 0.010) > 1e-12 ||
            Math.Abs(def.D2Spacing!.Value - 0.015) > 1e-12)
        {
            throw new NotSupportedException(
                "Linear pattern authoring on the legacy base is limited to the measured 2x3 " +
                "instances with 10 mm / 15 mm spacings; use the matching native variant base " +
                "for other measured parameter sets. No stream changes have been made.");
        }
    }

    private static void ValidateLegacyCircularValues(CircularPatternFeatureDefinitionData def)
    {
        if (def.TotalInstances != 4 || Math.Abs(def.Spacing!.Value - Math.PI / 2.0) > 1e-12)
        {
            throw new NotSupportedException(
                "Circular pattern authoring on the legacy base is limited to the measured 4 " +
                "instances with 90-degree spacing; use the matching native variant base for " +
                "other measured parameter sets. No stream changes have been made.");
        }
        if (def.EqualSpacing == false)
        {
            throw new NotSupportedException(
                "The legacy circular template supports equal spacing only. " +
                "No stream changes have been made.");
        }
    }

    private static void ValidateNativeLinearValues(
        LinearPatternFeatureDefinitionData def, PatternNativeAuthoringTemplate template,
        string featureName)
    {
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Native linear-pattern authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        }
        if (def.D1TotalInstances != template.D1Count ||
            def.D2TotalInstances != template.D2Count ||
            Math.Abs(def.D1Spacing!.Value - template.D1Spacing!.Value) > 1e-12 ||
            Math.Abs(def.D2Spacing!.Value - template.D2Spacing!.Value) > 1e-12)
        {
            throw new NotSupportedException(
                $"Linear pattern authoring on native base '{template.SampleName}' is fixed to " +
                $"D1={template.D1Count}/{template.D1Spacing:R} m and D2={template.D2Count}/" +
                $"{template.D2Spacing:R} m. No stream changes have been made.");
        }
    }

    private static void ValidateNativeCircularValues(
        CircularPatternFeatureDefinitionData def, PatternNativeAuthoringTemplate template,
        string featureName)
    {
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Native circular-pattern authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        }
        if (def.TotalInstances != template.TotalInstances ||
            Math.Abs(def.Spacing!.Value - template.Spacing!.Value) > 1e-12 ||
            (def.EqualSpacing.HasValue && def.EqualSpacing.Value != template.EqualSpacing!.Value))
        {
            throw new NotSupportedException(
                $"Circular pattern authoring on native base '{template.SampleName}' is fixed to " +
                $"{template.TotalInstances} instances, spacing {template.Spacing:R} rad and " +
                $"equalSpacing={template.EqualSpacing}. No stream changes have been made.");
        }
    }

    private static void ValidateCount(int? count, string property)
    {
        if (!count.HasValue || count.Value < 2 || count.Value > 1000)
        {
            throw new ArgumentOutOfRangeException(property,
                $"{property} must be an integer between 2 and 1000, but received {count}.");
        }
    }

    private static void ValidateCountAllowOne(int? count, string property)
    {
        if (!count.HasValue || count.Value < 1 || count.Value > 1000)
        {
            throw new ArgumentOutOfRangeException(property,
                $"{property} must be an integer between 1 and 1000, but received {count}.");
        }
    }

    private static void ValidateSpacing(double? spacing, string property)
    {
        if (!spacing.HasValue || double.IsNaN(spacing.Value) || double.IsInfinity(spacing.Value) ||
            spacing.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(property,
                $"{property} must be a positive finite number, but received {spacing}.");
        }
    }

    private static void ValidateAxisType(int? axisType, string property)
    {
        if (axisType.HasValue && axisType.Value != 1)
        {
            throw new NotSupportedException(
                $"{property} must be 1 (component edge) for the controlled layout, but received {axisType.Value}. " +
                "No stream changes have been made.");
        }
    }

    private static void EnsureCommon(
        byte[] currentStream, FeatureFolder sketchFolder, int sketchEndOffset,
        string patternClassName, string expectedPrefixSha, string modeName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (sketchFolder == null)
            throw new ArgumentNullException(nameof(sketchFolder));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != PatternSegments.StreamVersion)
        {
            throw new NotSupportedException(
                $"Pattern authoring is only supported for ResolvedFeatures streams version " +
                $"0x{PatternSegments.StreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, patternClassName) > 0)
        {
            throw new NotSupportedException(
                $"Pattern authoring currently supports only a stripped base body without an existing " +
                $"{patternClassName} feature. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) != PatternNativeAuthoringTemplates.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Pattern authoring requires the stripped folder count " +
                $"{PatternNativeAuthoringTemplates.StrippedFolderCount}. No stream changes have been made.");
        }

        RequireSketchChain(currentStream, sketchFolder, sketchEndOffset, version);
        RequireSketchChildCountMarker(currentStream, version);
        RequireControlledBase(currentStream, version, expectedPrefixSha, modeName);
    }

    private static void RequireSketchChain(
        byte[] currentStream, FeatureFolder sketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(sketchFolder.DisplayName)
            ? $"<tag {sketchFolder.TagOffset}>"
            : sketchFolder.DisplayName;
        int end = sketchEndOffset > sketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;
        bool hasChain = MfcPrimitives.ScanMfcTags(currentStream)
            .Any(t => t.ClassName == SketchChainClass &&
                      t.TagOffset >= sketchFolder.TagOffset &&
                      t.TagOffset < end);
        if (!hasChain)
        {
            throw new NotSupportedException(
                $"Cannot author a pattern on the base body of sketch '{sketchName}' " +
                $"(ResolvedFeatures version 0x{version:X2}): no parseable {SketchChainClass} was found. " +
                "No stream changes have been made.");
        }
    }

    private static void RequireSketchChildCountMarker(byte[] currentStream, ushort version)
    {
        for (int i = 0; i <= currentStream.Length - 4; i++)
        {
            if (currentStream[i] == 0x67 && currentStream[i + 1] == 0x00 &&
                currentStream[i + 2] == 0x00 && currentStream[i + 3] == 0x00)
                return;
        }

        throw new NotSupportedException(
            $"Cannot author a pattern on the controlled base body (ResolvedFeatures version 0x{version:X2}): " +
            "the 0x67 sketch child-count marker was not found. No stream changes have been made.");
    }

    private static void RequireControlledBase(
        byte[] currentStream, ushort version, string expectedPrefixSha, string modeName)
    {
        string sha = NormalizedSha(currentStream);
        if (!string.Equals(sha, expectedPrefixSha, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Pattern authoring is limited to the controlled PatternOptions base body for the " +
                $"{modeName} layout (ResolvedFeatures version 0x{version:X2}): base prefix signature " +
                $"{sha.Substring(0, 12)} does not match the measured BaseBox/BossSeed/edge-reference specimen. " +
                "No stream changes have been made.");
        }
    }

    private static string NormalizedSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }
        return Sha256Hex(normalized);
    }

    internal static string? NativeFeatureName(byte[] currentStream, PatternNativeMode mode) =>
        TryGetNativeTemplate(currentStream, mode)?.FeatureName;

    private static byte[] Append(byte[] currentStream, byte[] featureBlock)
    {
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
