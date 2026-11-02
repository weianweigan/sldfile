using System;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled template authoring for the measured thin extrusion classes.
/// The stripped base must be the exact native sample truncated at moExtruThin_c with the header
/// folder count and the preceding 0x67 value zeroed. The complete native feature block is appended
/// after patching the measured depth/wall slots: OneDirection, OneDirection+CapEnds, MidPlane and
/// TwoDirection each use their own measured block. Other combinations are rejected.
/// </summary>
internal static class ExtrudeThinFeatureBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static ThinExtrudeAuthoringTemplate GetTemplate(ExtrudeFeatureDefinitionData definition)
    {
        switch (definition.ThinWallType)
        {
            case 0:
                return definition.CapEnds
                    ? ExtrudeThinAuthoringTemplates.CapEnds
                    : ExtrudeThinAuthoringTemplates.OneDirection;
            case 2:
                return ExtrudeThinAuthoringTemplates.MidPlane;
            case 3:
                return ExtrudeThinAuthoringTemplates.TwoDirection;
            default:
                throw new NotSupportedException(
                    "Thin extrusion templates cover only OneDirection (0), MidPlane (2) and " +
                    "TwoDirection (3). No stream changes have been made.");
        }
    }

    internal static string GetTemplateName(ExtrudeFeatureDefinitionData definition) =>
        GetTemplate(definition).Name;

    internal static byte[] Build(
        byte[] currentStream,
        ExtrudeFeatureDefinitionData definition,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        var template = GetTemplate(definition);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring uses the measured native feature block/name '{template.Name}'. " +
                $"A custom feature name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (featureName != template.Name || targetSketchFolder == null)
            throw new NotSupportedException("Thin extrusion authoring requires the measured sketch and feature name. No stream changes have been made.");

        if (currentStream == null || currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires the measured stripped base of length {template.BaseLength} " +
                $"bytes; the document stream is {currentStream?.Length ?? 0} bytes. No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires the stripped folder count {template.StrippedFolderCount}. " +
                "No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream.Length < 8 ||
            currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Thin extrusion authoring requires the stripped base's 0x67 sketch marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Thin extrusion authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        Patch(block, definition);

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static void Patch(byte[] block, ExtrudeFeatureDefinitionData definition)
    {
        double depth = definition.Depth;
        double depth2 = definition.DepthDirection2;
        double wall1 = definition.WallThickness;
        double wall2 = definition.WallThicknessDirection2;
        uint wallType = definition.ThinWallType;
        bool twoDirections = wallType == 3;

        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D1");
        var d5 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D5");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D2");
        var d6 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D6");
        if (d1.Count != 1 || d5.Count != 1 || (twoDirections && (d2.Count != 1 || d6.Count != 1)))
            throw new InvalidOperationException("Measured thin template is missing required parameters.");
        int depthOffset = d1[0].Offset;
        int wallOffset = d5[0].Offset;

        // Layout guard: every slot must hold the measured value before it is patched.
        foreach (int off in new[] { 0x20, 0x40, 0x58, 0x68, 0x250, 0x268 })
            Require(block, depthOffset + off, off == 0x40 || off == 0x58 ? 0.005 : 0.010);
        foreach (int off in new[] { 0x20, 0x1A6, 0x1BE, 0x250, 0x268 })
            Require(block, wallOffset + off, 0.001);
        Require(block, wallOffset + 0x60, wallType == 2 ? -0.0005 : -0.001);

        var tags = MfcPrimitives.ScanMfcTags(block).ToList();
        var radial = tags.Single(t => t.ClassName == "moDisplayRadialDim_c");
        var bbox = tags.Single(t => t.ClassName == "moBBoxCenterData_c");
        Require(block, radial.TagOffset - 24, 0.001);
        double initialY = wallType == 2 ? 0.0 : wallType == 3 ? 0.0005 : -0.0005;
        double initialZ = twoDirections ? 0.0025 : 0.005;
        double initialDiameter = twoDirections ? 0.018275666882497064 : 0.014177446878757825;
        Require(block, bbox.DataStart + 0x0C, initialY);
        Require(block, bbox.DataStart + 0x14, initialZ);
        Require(block, bbox.DataStart + 0x1C, initialDiameter);
        Require(block, bbox.DataStart + 0xEE, initialY);
        Require(block, bbox.DataStart + 0xF6, initialZ);
        Require(block, bbox.DataStart + 0xFE, initialDiameter);

        double halfDepth = depth / 2.0;
        double halfWall = wall1 / 2.0;
        double centerY = wallType == 2 ? 0.0 : twoDirections ? (wall2 - wall1) / 2.0 : -halfWall;
        double centerZ = twoDirections ? (depth - depth2) / 2.0 : halfDepth;
        double halfY = twoDirections ? (wall1 + wall2) / 2.0 : halfWall;
        double halfZ = twoDirections ? (depth + depth2) / 2.0 : halfDepth;
        double diameter = 2.0 * Math.Sqrt(0.005 * 0.005 + halfY * halfY + halfZ * halfZ);

        WriteDouble(block, depthOffset + 0x20, depth);
        WriteDouble(block, depthOffset + 0x40, halfDepth);
        WriteDouble(block, depthOffset + 0x58, halfDepth);
        WriteDouble(block, depthOffset + 0x68, depth);
        WriteDouble(block, depthOffset + 0x250, depth);
        WriteDouble(block, depthOffset + 0x268, depth);

        if (twoDirections)
        {
            int direction2 = d2[0].Offset;
            int wall2Offset = d6[0].Offset;
            foreach (int off in new[] { 0x20, 0x40, 0x58, 0x250, 0x268 })
                Require(block, direction2 + off, 0.005);
            Require(block, direction2 + 0x68, -0.005);
            WriteDouble(block, direction2 + 0x20, depth2);
            WriteDouble(block, direction2 + 0x40, depth2);
            WriteDouble(block, direction2 + 0x58, depth2);
            WriteDouble(block, direction2 + 0x68, -depth2);
            WriteDouble(block, direction2 + 0x250, depth2);
            WriteDouble(block, direction2 + 0x268, depth2);

            foreach (int off in new[] { 0x20, 0x60, 0x1A6, 0x1BE, 0x250, 0x268 })
                Require(block, wall2Offset + off, 0.002);
            foreach (int off in new[] { 0x20, 0x60, 0x1A6, 0x1BE, 0x250, 0x268 })
                WriteDouble(block, wall2Offset + off, wall2);
        }

        WriteDouble(block, wallOffset + 0x20, wall1);
        WriteDouble(block, wallOffset + 0x60, wallType == 2 ? -halfWall : -wall1);
        WriteDouble(block, wallOffset + 0x1A6, wall1);
        WriteDouble(block, wallOffset + 0x1BE, wall1);
        WriteDouble(block, wallOffset + 0x250, wall1);
        WriteDouble(block, wallOffset + 0x268, wall1);
        WriteDouble(block, radial.TagOffset - 24, wall1);

        WriteDouble(block, bbox.DataStart + 0x0C, centerY);
        WriteDouble(block, bbox.DataStart + 0x14, centerZ);
        WriteDouble(block, bbox.DataStart + 0x1C, diameter);
        WriteDouble(block, bbox.DataStart + 0xEE, centerY);
        WriteDouble(block, bbox.DataStart + 0xF6, centerZ);
        WriteDouble(block, bbox.DataStart + 0xFE, diameter);
    }

    private static void Require(byte[] block, int offset, double expected)
    {
        if (offset < 0 || offset + 8 > block.Length ||
            Math.Abs(BitConverter.ToDouble(block, offset) - expected) > 1e-12)
            throw new InvalidOperationException(
                "Measured thin template layout changed; refusing to patch an unknown slot.");
    }

    private static void WriteDouble(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException("Measured thin template slot is out of range.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
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
