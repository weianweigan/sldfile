using System;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed option-template authoring for the 2026-10-31 base-flange retry group
/// (ResolvedFeatures version 0x73, controlled open-L sketch, stream lengths
/// 43,270/43,272/43,685 bytes).
/// <para>
/// The caller must start from the exact stripped prefix of the matching native
/// sample (first 0x3EF1 bytes, ending at the <c>moSMBaseFlange_c</c> tag). Each
/// template's normalized prefix SHA-256, folder count, 0x67 markers and version
/// are pinned. The measured suffix is appended verbatim; no scalar is patched,
/// so the authored ResolvedFeatures stream is byte-identical to the native
/// source sample. Only the measured combination is accepted; other values,
/// custom names, another template's base and a non-stripped document are
/// rejected before the stream is touched.
/// </para>
/// </summary>
internal static class SheetMetalBaseFlangeOptionBuilder
{
    internal const string DefaultFeatureName = SheetMetalBaseFlangeOptionSegments.MeasuredFeatureName;

    internal static SheetMetalBaseFlangeOptionSegments.OptionTemplate SelectTemplate(
        SheetMetalBaseFlangeDefinitionData def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        RejectCommon(def);

        if (def.UseGaugeTable)
        {
            RejectUnlessNullOrClose(def.KFactor, 0.5, nameof(def.KFactor));
            if (def.BendAllowanceType != 0 && def.BendAllowanceType != 2)
                throw Unsupported(nameof(def.BendAllowanceType), def.BendAllowanceType);
            RequireNull(def.BendAllowance, nameof(def.BendAllowance));
            RequireNull(def.BendDeduction, nameof(def.BendDeduction));
            if (def.GaugeTablePath != null &&
                !string.Equals(def.GaugeTablePath,
                    SheetMetalBaseFlangeOptionSegments.MeasuredGaugeTablePath, StringComparison.Ordinal))
                throw Unsupported(nameof(def.GaugeTablePath), def.GaugeTablePath);
            if (def.ThicknessTableName != null &&
                !string.Equals(def.ThicknessTableName,
                    SheetMetalBaseFlangeOptionSegments.MeasuredGaugeTableName, StringComparison.Ordinal))
                throw Unsupported(nameof(def.ThicknessTableName), def.ThicknessTableName);
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.GaugeTable;
        }

        if (def.BendAllowanceType == 2)
        {
            RejectUnlessClose(def.KFactor, SheetMetalBaseFlangeOptionSegments.MeasuredKFactor,
                nameof(def.KFactor));
            RequireNull(def.BendAllowance, nameof(def.BendAllowance));
            RequireNull(def.BendDeduction, nameof(def.BendDeduction));
            RequireNull(def.GaugeTablePath, nameof(def.GaugeTablePath));
            RequireNull(def.ThicknessTableName, nameof(def.ThicknessTableName));
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.KFactor;
        }

        if (def.BendAllowanceType == 3)
        {
            RejectUnlessClose(def.KFactor, 0.5, nameof(def.KFactor));
            RequireClose(def.BendAllowance,
                SheetMetalBaseFlangeOptionSegments.MeasuredBendAllowance, nameof(def.BendAllowance));
            RequireNull(def.BendDeduction, nameof(def.BendDeduction));
            RequireNull(def.GaugeTablePath, nameof(def.GaugeTablePath));
            RequireNull(def.ThicknessTableName, nameof(def.ThicknessTableName));
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.Direct;
        }

        if (def.BendAllowanceType == 4)
        {
            RejectUnlessClose(def.KFactor, 0.5, nameof(def.KFactor));
            RequireClose(def.BendDeduction,
                SheetMetalBaseFlangeOptionSegments.MeasuredBendDeduction, nameof(def.BendDeduction));
            RequireNull(def.BendAllowance, nameof(def.BendAllowance));
            RequireNull(def.GaugeTablePath, nameof(def.GaugeTablePath));
            RequireNull(def.ThicknessTableName, nameof(def.ThicknessTableName));
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.Deduction;
        }

        throw new NotSupportedException(
            "Base-flange option authoring supports only the measured combinations: " +
            "KFactor (BendAllowanceType=2, KFactor=0.3), Direct (type=3, BendAllowance=0.001), " +
            "Deduction (type=4, BendDeduction=0.001) or UseGaugeTable=true with the k-factor mm " +
            $"sample. Received BendAllowanceType={def.BendAllowanceType}. No stream changes have been made.");
    }

    internal static void ValidateTemplateGeometry(
        SheetMetalBaseFlangeDefinitionData def,
        SheetMetalBaseFlangeOptionSegments.OptionTemplate template,
        byte[] currentStream)
    {
        double expectedRadius = template == SheetMetalBaseFlangeOptionSegments.OptionTemplate.GaugeTable
            ? 0.001
            : 0.002;
        if (!NearEquals(def.Thickness, 0.001))
            throw Unsupported(nameof(def.Thickness), def.Thickness);
        if (!NearEquals(def.BendRadius, expectedRadius))
            throw Unsupported(nameof(def.BendRadius), def.BendRadius);
        if (!NearEquals(def.Depth, 0.010))
            throw Unsupported(nameof(def.Depth), def.Depth);

        if (currentStream == null ||
            currentStream.Length != SheetMetalBaseFlangeOptionSegments.MeasuredPrefixLength)
            throw new NotSupportedException(
                $"Base-flange option authoring requires the stripped template base " +
                $"({SheetMetalBaseFlangeOptionSegments.MeasuredPrefixLength} bytes, ending at the " +
                $"moSMBaseFlange_c tag), but received {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != SheetMetalBaseFlangeOptionSegments.SupportedStreamVersion)
            throw new NotSupportedException(
                $"Base-flange option authoring is only supported for ResolvedFeatures version " +
                $"0x{SheetMetalBaseFlangeOptionSegments.SupportedStreamVersion:X2}, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) !=
            SheetMetalBaseFlangeOptionSegments.MeasuredFullFolderCount)
            throw new NotSupportedException(
                $"Base-flange option authoring requires folder count " +
                $"{SheetMetalBaseFlangeOptionSegments.MeasuredFullFolderCount}. " +
                "No stream changes have been made.");

        byte[] markerBytes = SheetMetalBaseFlangeOptionSegments.MarkerBytes;
        foreach (int offset in SheetMetalBaseFlangeOptionSegments.MarkerOffsets)
        {
            if (offset < 0 || offset + markerBytes.Length > currentStream.Length)
                throw new NotSupportedException(
                    "Base-flange option authoring requires the measured 0x67 marker block. " +
                    "No stream changes have been made.");
            for (int i = 0; i < markerBytes.Length; i++)
                if (currentStream[offset + i] != markerBytes[i])
                    throw new NotSupportedException(
                        $"Base-flange option authoring requires the measured 0x67 marker at " +
                        $"0x{offset:X}. No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moSMBaseFlange_c") != 0)
            throw new NotSupportedException(
                "Base-flange option authoring requires a stripped base without an existing base flange. " +
                "No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        string expected = SheetMetalBaseFlangeOptionSegments.GetNormalizedPrefixSha256(template);
        if (!string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"Base-flange option authoring requires the '{template}' stripped template base: " +
                $"normalized prefix SHA-256 {hash.Substring(0, 12)} does not match the measured " +
                $"signature {expected.Substring(0, 12)}. No stream changes have been made.");
    }

    internal static byte[] Build(
        byte[] currentStream,
        SheetMetalBaseFlangeDefinitionData def,
        string? featureName)
    {
        var template = SelectTemplate(def);
        ValidateTemplateGeometry(def, template, currentStream);

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? DefaultFeatureName
            : featureName!;
        if (!string.Equals(resolvedName, DefaultFeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Base-flange option authoring supports only the measured display name " +
                $"'{DefaultFeatureName}'; received '{resolvedName}'. No stream changes have been made.");

        byte[] suffix = SheetMetalBaseFlangeOptionSegments.GetSuffix(template);
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        BitConverter.GetBytes(SheetMetalBaseFlangeOptionSegments.MeasuredFullFolderCount)
            .CopyTo(newStream, 4);
        return newStream;
    }

    private static void RejectCommon(SheetMetalBaseFlangeDefinitionData def)
    {
        if (def.ReverseThickness) throw Unsupported(nameof(def.ReverseThickness), true);
        if (def.BothDirections) throw Unsupported(nameof(def.BothDirections), true);
        if (def.DepthDirection2 != 0) throw Unsupported(nameof(def.DepthDirection2), def.DepthDirection2);
        if (def.ReverseDirection) throw Unsupported(nameof(def.ReverseDirection), true);
        if (!def.UseDefaultBendRadius) throw Unsupported(nameof(def.UseDefaultBendRadius), false);
        if (def.AutoReliefType > 1) throw Unsupported(nameof(def.AutoReliefType), def.AutoReliefType);
        if (!NearEquals(def.ReliefRatio, 0.5)) throw Unsupported(nameof(def.ReliefRatio), def.ReliefRatio);
        if (def.ReliefWidth != 0) throw Unsupported(nameof(def.ReliefWidth), def.ReliefWidth);
        if (def.ReliefDepth != 0) throw Unsupported(nameof(def.ReliefDepth), def.ReliefDepth);
        if (!def.UseDefaultBendRelief) throw Unsupported(nameof(def.UseDefaultBendRelief), false);
        if (!string.IsNullOrEmpty(def.BendTableFile))
            throw Unsupported(nameof(def.BendTableFile), def.BendTableFile);
        if (def.SelectedContours != null && def.SelectedContours.Count > 0)
            throw Unsupported(nameof(def.SelectedContours), def.SelectedContours.Count);
        if (def.SelectedRegions != null && def.SelectedRegions.Count > 0)
            throw Unsupported(nameof(def.SelectedRegions), def.SelectedRegions.Count);
        if (def.DirectionReference != null) throw Unsupported(nameof(def.DirectionReference), "set");
        if (def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)))
            throw Unsupported(nameof(def.BodyScope), "non-default");
    }

    private static void RejectUnlessClose(double value, double measured, string property)
    {
        if (!NearEquals(value, measured)) throw Unsupported(property, value);
    }

    private static void RejectUnlessNullOrClose(double? value, double measured, string property)
    {
        if (value.HasValue && !NearEquals(value.Value, measured))
            throw Unsupported(property, value.Value);
    }

    private static void RequireClose(double? value, double measured, string property)
    {
        if (!value.HasValue || !NearEquals(value.Value, measured))
            throw Unsupported(property, value?.ToString() ?? "null");
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null) throw Unsupported(property, value);
    }

    private static bool NearEquals(double a, double b) => Math.Abs(a - b) <= 1e-12;

    private static NotSupportedException Unsupported(string property, object? value) =>
        new($"Base-flange option authoring does not support {property}={value}. " +
            "No stream changes have been made.");

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}
