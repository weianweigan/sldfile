using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured-combination authoring for the 2026 SweepOptions/LoftOptions samples.
/// <para>
/// The target must be the exact stripped base of one measured sample: same ResolvedFeatures
/// stream version 0x70, same length, folder count and 0x67 marker value zeroed, and the same
/// normalized SHA-256. The complete measured feature block is appended verbatim; selection
/// references, geometry caches and dimensions are never rewritten. Only the header folder
/// count and the 0x67 marker value are patched. Any mismatch, an existing sweep/loft feature,
/// a custom feature name or a mismatched sketch name is rejected before the stream is changed.
/// </para>
/// </summary>
internal static class SweepLoftFeatureBuilder
{
    internal const int MarkerDistanceFromFeature = 0x10;

    internal static (byte[] Stream, string FeatureName) Build(
        byte[] currentStream,
        string className,
        string? featureName,
        IReadOnlyList<string>? requestedProfiles,
        string? requestedPath,
        string? requestedGuide,
        bool? merge = null,
        int? twistControlType = null,
        double? twistAngleRad = null,
        bool centerline = false)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (string.IsNullOrEmpty(className))
            throw new ArgumentException("A feature class name is required.", nameof(className));
        if (twistControlType.HasValue && twistControlType.Value != 0 &&
            twistControlType.Value != 1 && twistControlType.Value != 8)
            throw new ArgumentOutOfRangeException(nameof(twistControlType), "Sweep TwistControlType must be 0, 1 or 8.");
        if (twistAngleRad.HasValue &&
            (double.IsNaN(twistAngleRad.Value) || double.IsInfinity(twistAngleRad.Value) ||
             twistAngleRad.Value < 0 || twistAngleRad.Value > Math.PI * 2 + 1e-9))
            throw new ArgumentOutOfRangeException(nameof(twistAngleRad), "Sweep TwistAngle must be within [0, 2π].");
        if (twistControlType.HasValue && twistControlType.Value == 0 &&
            twistAngleRad.HasValue && twistAngleRad.Value > 1e-12)
            throw new ArgumentException("TwistAngle requires TwistControlType=1 or 8.");
        if ((merge.HasValue || twistControlType.HasValue || twistAngleRad.HasValue) &&
            !string.Equals(className, "moSweep_c", StringComparison.Ordinal))
            throw new NotSupportedException("Sweep option authoring requires the solid sweep class moSweep_c.");
        if (centerline && !string.Equals(className, "moBlend_c", StringComparison.Ordinal))
            throw new NotSupportedException("Centerline authoring requires the solid loft class moBlend_c.");
        if (centerline && (merge.HasValue || twistControlType.HasValue || twistAngleRad.HasValue))
            throw new NotSupportedException("Centerline authoring cannot be combined with sweep options.");

        var template = SelectTemplate(currentStream, className, centerline);

        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Sweep/Loft measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        ValidateRequestedNames(template, requestedProfiles, requestedPath, requestedGuide);
        RequireSketchNamesInStream(currentStream, template);

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, template.ClassName) > 0)
        {
            throw new NotSupportedException(
                $"The document already contains a {template.ClassName} feature. No stream changes have been made.");
        }

        int marker = template.BaseLength - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 ||
            currentStream[marker + 1] != 0 || currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "The stripped base is missing the 0x67 marker at the measured position with value 0. " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        if (merge.HasValue || twistControlType.HasValue || twistAngleRad.HasValue)
        {
            PatchSweepOptions(newStream, currentStream.Length, className,
                merge, twistControlType, twistAngleRad);
        }
        return (newStream, template.Name);
    }

    /// <summary>
    /// Patches the measured solid-sweep option slots anchored at the feature element end:
    /// merge = end+0x147 (byte 0/1), twist control = end+0x113 (byte 0/1/8), twist angle =
    /// end+0x14F (f64 radians). The existing values must validate before any write.
    /// </summary>
    private static void PatchSweepOptions(
        byte[] stream, int blockStart, string className,
        bool? merge, int? twistControlType, double? twistAngleRad)
    {
        var parsed = MfcDocument.Parse(stream);
        var tag = parsed.AllElements.FirstOrDefault(e =>
            e.TagOffset == blockStart && e.ClassName == className);
        if (tag == null)
            throw new NotSupportedException(
                "The sweep template block does not start with the measured class tag. No stream changes have been made.");
        int end = tag.DataEnd;
        if (end < 0 || end + 0x157 > stream.Length)
            throw new NotSupportedException(
                "The sweep template is missing the measured option slots. No stream changes have been made.");
        byte currentMerge = stream[end + 0x147];
        byte currentTwist = stream[end + 0x113];
        double currentAngle = BitConverter.ToDouble(stream, end + 0x14F);
        if (currentMerge > 1 || (currentTwist != 0 && currentTwist != 1 && currentTwist != 8) ||
            double.IsNaN(currentAngle) || double.IsInfinity(currentAngle) ||
            currentAngle < 0 || currentAngle > Math.PI * 2 + 1e-9)
            throw new NotSupportedException(
                "The sweep template option slots do not match the measured layout. No stream changes have been made.");

        if (merge.HasValue)
            stream[end + 0x147] = merge.Value ? (byte)1 : (byte)0;
        if (twistControlType.HasValue)
            stream[end + 0x113] = (byte)twistControlType.Value;
        if (twistAngleRad.HasValue)
            BitConverter.GetBytes(twistAngleRad.Value).CopyTo(stream, end + 0x14F);
    }

    private static SweepLoftAuthoringTemplate SelectTemplate(
        byte[] currentStream, string className, bool centerline = false)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in AllTemplates())
        {
            bool isCenterline = string.Equals(template.Kind, "LoftCenterline", StringComparison.Ordinal);
            if (isCenterline != centerline)
                continue;
            if (!string.Equals(template.ClassName, className, StringComparison.Ordinal))
                continue;
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
                continue;

            int marker = template.BaseLength - MarkerDistanceFromFeature;
            if (marker < 0 || marker + 4 > currentStream.Length)
                continue;
            if (currentStream[marker] != 0x67 ||
                currentStream[marker + 1] != 0 || currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
                BitConverter.ToUInt32(currentStream, marker + 4) != 0)
                continue;

            if (string.Equals(NormalizedBaseSha(currentStream, marker), template.BaseSha256, StringComparison.Ordinal))
                return template;
        }

        throw new NotSupportedException(
            $"The document is not one of the controlled stripped sweep/loft bases for {className} " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }

    private static IEnumerable<SweepLoftAuthoringTemplate> AllTemplates()
    {
        foreach (var template in SweepLoftAuthoringTemplates.All)
            yield return template;
        foreach (var template in SurfaceSweepLoftAuthoringTemplates.All)
            yield return template;
        foreach (var template in LoftCenterlineAuthoringTemplates.All)
            yield return template;
    }

    private static void ValidateRequestedNames(
        SweepLoftAuthoringTemplate template,
        IReadOnlyList<string>? requestedProfiles,
        string? requestedPath,
        string? requestedGuide)
    {
        if (requestedProfiles != null &&
            !requestedProfiles.SequenceEqual(template.ProfileNames, StringComparer.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"The measured {template.Kind} combination uses profiles " +
                $"[{string.Join(", ", template.ProfileNames)}]; requested " +
                $"[{string.Join(", ", requestedProfiles)}]. No stream changes have been made.");
        }

        if (requestedPath != null &&
            (template.PathName == null || !string.Equals(requestedPath, template.PathName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException(
                $"The measured {template.Kind} combination uses path '{template.PathName ?? "<none>"}'; " +
                $"requested '{requestedPath}'. No stream changes have been made.");
        }

        if (requestedGuide != null &&
            (template.GuideName == null || !string.Equals(requestedGuide, template.GuideName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new NotSupportedException(
                $"The measured {template.Kind} combination uses guide '{template.GuideName ?? "<none>"}'; " +
                $"requested '{requestedGuide}'. No stream changes have been made.");
        }
    }

    private static void RequireSketchNamesInStream(byte[] currentStream, SweepLoftAuthoringTemplate template)
    {
        var names = new List<string>(template.ProfileNames);
        if (template.PathName != null) names.Add(template.PathName);
        if (template.GuideName != null) names.Add(template.GuideName);

        foreach (string name in names)
        {
            if (!ContainsWChar(currentStream, name))
            {
                throw new NotSupportedException(
                    $"The stripped base does not contain the measured sketch '{name}'. " +
                    "No stream changes have been made.");
            }
        }
    }

    private static bool ContainsWChar(byte[] data, string value)
    {
        byte[] pattern = Encoding.Unicode.GetBytes(value);
        if (pattern.Length == 0 || data.Length < pattern.Length)
            return false;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
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
