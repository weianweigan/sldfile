using System;
using System.Linq;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed-measurement template authoring for the controlled variable-radius fillet
/// (VarFillet_c / TypeId 0x52, one edge, two control points). Supports the measured
/// 1/3, 2/4 and 3/5 mm pairs on their exact native source/resave bases, plus the legacy
/// 2/4 mm base. The complete measured block carries edge/vertex/control-point context and
/// every radius copy; no slot is scaled because the pair diffs also change derived fields.
/// </summary>
internal static class VariableFilletFeatureBuilder
{
    internal static string TemplateName => VariableFilletAuthoringTemplates.Source.Name;

    internal static void ValidateDefinition(VariableFilletFeatureDefinitionData definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        var radii = definition.ControlPointRadii;
        if (radii == null || radii.Count != 2 || !IsMeasuredPair(radii[0], radii[1]))
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring is fixed to the measured control-point pairs " +
                "1/3 mm, 2/4 mm or 3/5 mm. No stream changes have been made.");
        }
        if (definition.FilletEdgeCount.HasValue && definition.FilletEdgeCount.Value != 1)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring supports the measured single edge only. " +
                "No stream changes have been made.");
        }
        if (definition.Edges != null && definition.Edges.Count > 0)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring does not accept edge overrides; the measured " +
                "edge reference is carried by the template. No stream changes have been made.");
        }
        if (definition.DefaultRadius.HasValue || definition.DefaultDistance.HasValue ||
            definition.OverflowType.HasValue || definition.TransitionType.HasValue ||
            definition.PropagateToTangentFaces.HasValue || definition.PropagateFeatureToParts.HasValue ||
            definition.ConicTypeForCrossSectionProfile.HasValue || definition.DefaultConicRhoOrRadius.HasValue ||
            definition.AsymmetricFillet.HasValue || definition.CurvatureContinuous.HasValue)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring does not accept default/transition/vertex overrides " +
                "in this measured slice. No stream changes have been made.");
        }
    }

    internal static byte[] BuildVariableFilletStream(
        byte[] currentStream, VariableFilletFeatureDefinitionData definition, string featureName)
    {
        ValidateDefinition(definition);
        var radii = definition.ControlPointRadii!;
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        var native = FindNativeTemplate(currentStream);
        if (native != null)
        {
            if (Math.Abs(radii[0] - native.Radius0) > 1e-12 ||
                Math.Abs(radii[1] - native.Radius1) > 1e-12)
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring on the measured {native.Radius0 * 1000:0}/" +
                    $"{native.Radius1 * 1000:0} mm base requires that exact measured pair. " +
                    "No stream changes have been made.");
            }
            if (!string.Equals(featureName, VariableFilletNativeAuthoringTemplates.FeatureName,
                    StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring uses the measured native feature name " +
                    $"'{VariableFilletNativeAuthoringTemplates.FeatureName}'. " +
                    "No stream changes have been made.");
            }
            ushort nativeVersion = BitConverter.ToUInt16(currentStream, 0);
            if (nativeVersion != native.StreamVersion)
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring requires ResolvedFeatures version " +
                    $"0x{native.StreamVersion:X2}. No stream changes have been made.");
            }
            if (BitConverter.ToUInt16(currentStream, 4) != native.StrippedFolderCount)
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring requires the stripped folder count " +
                    $"{native.StrippedFolderCount}. No stream changes have been made.");
            }
            if (FindSingleSketchMarker(currentStream) < 0)
            {
                throw new NotSupportedException(
                    "Variable-radius fillet authoring requires the base's single measured 0x67 " +
                    "sketch child-count marker with value 1. No stream changes have been made.");
            }
            return Append(currentStream, native.StrippedFolderCount, native.Block);
        }

        var template = SelectByBaseLength(currentStream);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires the measured stripped base length " +
                $"{VariableFilletAuthoringTemplates.Source.BaseLength} or " +
                $"{VariableFilletAuthoringTemplates.Resave.BaseLength}; the document stream is " +
                $"{currentStream.Length} bytes. No stream changes have been made.");
        }
        if (Math.Abs(radii[0] - 0.002) > 1e-12 || Math.Abs(radii[1] - 0.004) > 1e-12)
        {
            throw new NotSupportedException(
                "The legacy variable-fillet base supports only the measured 2/4 mm pair. " +
                "No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires the stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        }
        int marker = FindSingleSketchMarker(currentStream);
        if (marker < 0)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring requires the base's single measured 0x67 sketch " +
                "child-count marker with value 1. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static bool IsMeasuredPair(double radius0, double radius1) =>
        (Math.Abs(radius0 - 0.001) <= 1e-12 && Math.Abs(radius1 - 0.003) <= 1e-12) ||
        (Math.Abs(radius0 - 0.002) <= 1e-12 && Math.Abs(radius1 - 0.004) <= 1e-12) ||
        (Math.Abs(radius0 - 0.003) <= 1e-12 && Math.Abs(radius1 - 0.005) <= 1e-12);

    private static VariableFilletNativeAuthoringTemplate? FindNativeTemplate(byte[] currentStream)
    {
        string sha = NormalizedBaseSha(currentStream);
        return VariableFilletNativeAuthoringTemplates.All.FirstOrDefault(t =>
            t.BaseLength == currentStream.Length &&
            string.Equals(t.BaseSha256, sha, StringComparison.Ordinal));
    }

    private static byte[] Append(byte[] currentStream, int strippedFolderCount, byte[] block)
    {
        byte[] copy = (byte[])block.Clone();
        var newStream = new byte[currentStream.Length + copy.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(strippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(copy, 0, newStream, currentStream.Length, copy.Length);
        return newStream;
    }

    private static VariableFilletAuthoringTemplate SelectByBaseLength(byte[] currentStream)
    {
        int length = currentStream?.Length ?? 0;
        if (length == VariableFilletAuthoringTemplates.Source.BaseLength)
            return VariableFilletAuthoringTemplates.Source;
        if (length == VariableFilletAuthoringTemplates.Resave.BaseLength)
            return VariableFilletAuthoringTemplates.Resave;
        throw new NotSupportedException(
            $"Variable-radius fillet authoring requires the measured stripped base length " +
            $"{VariableFilletAuthoringTemplates.Source.BaseLength} or " +
            $"{VariableFilletAuthoringTemplates.Resave.BaseLength}; the document stream is " +
            $"{length} bytes. No stream changes have been made.");
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
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}
