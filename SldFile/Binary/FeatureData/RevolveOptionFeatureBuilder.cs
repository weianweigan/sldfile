using System;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled template authoring for the measured thin/both-directions/cut revolutions.
/// The target must be the exact native sample truncated at its measured class tag with the header
/// folder count and the preceding 0x67 value zeroed. The complete native feature block is appended
/// after patching only the measured named-parameter slots. Geometry/display caches remain those of
/// the template; native verification of rebuild/volume is delegated to Agent 5.
/// </summary>
internal static class RevolveOptionFeatureBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static string GetTemplateName(RevolveFeatureDefinitionData definition)
    {
        if (definition.IsThinFeature)
            return IsFull(definition.Angle)
                ? RevolveOptionAuthoringTemplates.Thin360.Name
                : RevolveOptionAuthoringTemplates.Thin90.Name;
        if (definition.IsCut)
            return RevolveOptionAuthoringTemplates.Cut360.Name;
        if (definition.BothDirections)
            return RevolveOptionAuthoringTemplates.BothDirections.Name;
        throw new NotSupportedException("This revolution layout has no template authoring block.");
    }

    internal static byte[] Build(
        byte[] currentStream, RevolveFeatureDefinitionData definition, string featureName)
    {
        var template = SelectTemplate(definition);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Revolution template authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream == null || currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires the measured stripped base of length " +
                $"{template.BaseLength} bytes; the document stream is {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires the stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Revolution template authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        if (definition.IsThinFeature)
            PatchThinThickness(block, definition.Thickness1);
        else if (definition.BothDirections)
            PatchBothAngles(block, definition.Angle, definition.AngleDirection2);

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static RevolveAuthoringTemplate SelectTemplate(RevolveFeatureDefinitionData definition)
    {
        if (definition.IsThinFeature)
            return IsFull(definition.Angle)
                ? RevolveOptionAuthoringTemplates.Thin360
                : RevolveOptionAuthoringTemplates.Thin90;
        if (definition.IsCut) return RevolveOptionAuthoringTemplates.Cut360;
        if (definition.BothDirections) return RevolveOptionAuthoringTemplates.BothDirections;
        throw new NotSupportedException("This revolution layout has no template authoring block.");
    }

    private static void PatchThinThickness(byte[] block, double thickness)
    {
        var d3 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D3");
        if (d3.Count != 1)
            throw new InvalidOperationException("Measured thin revolve template is missing D3.");
        int value = d3[0].Offset;
        foreach (int off in new[] { 0x20, 0x1A6, 0x1BE, 0x250, 0x268 })
        {
            Require(block, value + off, 0.001);
            BitConverter.GetBytes(thickness).CopyTo(block, value + off);
        }
    }

    private static void PatchBothAngles(byte[] block, double angle1, double angle2)
    {
        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D1");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D2");
        if (d1.Count != 1 || d2.Count != 1)
            throw new InvalidOperationException("Measured both-directions revolve template is missing D1/D2.");
        foreach (int off in new[] { 0x20, 0x23A })
        {
            Require(block, d1[0].Offset + off, Math.PI / 2.0);
            BitConverter.GetBytes(angle1).CopyTo(block, d1[0].Offset + off);
            Require(block, d2[0].Offset + off, Math.PI / 4.0);
            BitConverter.GetBytes(angle2).CopyTo(block, d2[0].Offset + off);
        }
    }

    private static bool IsFull(double angle) => Math.Abs(angle - 2.0 * Math.PI) <= 1e-12;

    private static void Require(byte[] block, int offset, double expected)
    {
        if (offset < 0 || offset + 8 > block.Length ||
            Math.Abs(BitConverter.ToDouble(block, offset) - expected) > 1e-12)
            throw new InvalidOperationException(
                "Measured revolution template layout changed; refusing to patch an unknown slot.");
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
