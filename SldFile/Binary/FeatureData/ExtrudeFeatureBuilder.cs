using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Parametric serializer and injector for SolidWorks extrusion features (moExtrusion_c and moICE_c).
/// Generates compliant MFC 4.2 CArchive binary structures for insertion into ResolvedFeatures streams.
/// </summary>
internal static class ExtrudeFeatureBuilder
{
    /// <summary>
    /// Builds a new ResolvedFeatures stream containing the injected extrusion feature.
    /// </summary>
    public static byte[] BuildExtrusionStream(
        byte[] currentStream,
        ExtrudeFeatureDefinitionData def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        // 1. Extract dynamic entity sequence from target sketch folder
        var entityIds = MfcExtrudeSerializer.ExtractEntityIds(targetSketchFolder, currentStream, sketchEndOffset);

        // 2. Determine unique surface tag and feature id based on existing extrusion counts
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        int existingBossCount = CountOccurrences(currentStream, "moExtrusion_c");
        int existingCutCount = CountOccurrences(currentStream, "moICE_c");

        uint surfaceTag = 0x6AE19B71 + ((uint)existingBossCount * 0x10);
        uint cutSurfaceTag = 0x6AE19B83 + ((uint)existingCutCount * 0x10);
        uint baseSurfaceTag = 0x6AE19B82;
        uint uniqueFeatureId = def.IsCut
            ? 0xCD0C81A9 + (uint)existingCutCount
            : 0xC28F06D1 + (uint)existingBossCount;

        // 3. Synthesize feature block via modular serializer
        byte[] featureBlock = def.IsCut
            ? MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds, cutSurfaceTag, baseSurfaceTag, uniqueFeatureId)
            : MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds, surfaceTag, uniqueFeatureId);

        // 4. Allocate new stream buffer
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);

        // 5. Increment stream header folder count at offset 4
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);

        // 6. Update target sketch child count flag (tag 0x67)
        int scanStart = Math.Max(targetSketchFolder.TagOffset, sketchEndOffset - 256);
        int markerOffset = -1;
        for (int i = sketchEndOffset - 4; i >= scanStart; i--)
        {
            if (newStream[i] == 0x67 && newStream[i + 1] == 0x00 && newStream[i + 2] == 0x00 && newStream[i + 3] == 0x00)
            {
                markerOffset = i;
                break;
            }
        }

        if (markerOffset >= 0 && markerOffset + 4 < newStream.Length)
        {
            uint currentChildCount = BitConverter.ToUInt32(newStream, markerOffset + 4);
            BitConverter.GetBytes(currentChildCount + 1).CopyTo(newStream, markerOffset + 4);
        }

        // 7. Append synthesized feature block
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    /// <summary>
    /// Backward-compatible overload resolving sketch parameters from stream analysis.
    /// </summary>
    public static byte[] BuildExtrusionStream(byte[] currentStream, ExtrudeFeatureDefinitionData def, string? targetSketchName = null)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);

        string featureName = def.IsCut ? "切除-拉伸1" : "凸台-拉伸1";
        byte[] featureBlock = def.IsCut
            ? BuildCutExtrudeBlock(def, featureName, 1)
            : BuildBossExtrudeBlock(def, featureName, 4);

        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);

        // Increment folder count
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);

        // Update preceding sketch 0x67 marker in trailing window
        for (int i = currentStream.Length - 4; i >= Math.Max(0, currentStream.Length - 128); i--)
        {
            if (newStream[i] == 0x67 && newStream[i + 1] == 0x00 && newStream[i + 2] == 0x00 && newStream[i + 3] == 0x00)
            {
                uint currentCount = BitConverter.ToUInt32(newStream, i + 4);
                BitConverter.GetBytes(currentCount + 1).CopyTo(newStream, i + 4);
                break;
            }
        }

        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    /// <summary>
    /// Synthesizes a complete Boss Extrude (moExtrusion_c) binary block with parametric dimensions.
    /// </summary>
    internal static byte[] BuildBossExtrudeBlock(
        ExtrudeFeatureDefinitionData def,
        string featureName = "凸台-拉伸1",
        IReadOnlyList<uint>? entityIds = null,
        uint surfaceTag = 0x6AE19B71,
        uint uniqueFeatureId = 0xC28F06D1)
    {
        ValidateDefinition(def);
        entityIds ??= new uint[] { 1, 2, 3, 4 };
        return MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds, surfaceTag, uniqueFeatureId);
    }

    /// <summary>
    /// Overload accepting entity count.
    /// </summary>
    internal static byte[] BuildBossExtrudeBlock(ExtrudeFeatureDefinitionData def, string featureName, int entityCount)
    {
        ValidateDefinition(def);
        var entityIds = (entityCount > 0)
            ? Enumerable.Range(1, entityCount).Select(i => (uint)i).ToList()
            : new List<uint> { 1, 2, 3, 4 };
        return MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds);
    }

    /// <summary>
    /// Synthesizes a complete Cut Extrude (moICE_c) binary block with parametric dimensions.
    /// </summary>
    internal static byte[] BuildCutExtrudeBlock(
        ExtrudeFeatureDefinitionData def,
        string featureName = "切除-拉伸1",
        IReadOnlyList<uint>? entityIds = null,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        uint uniqueFeatureId = 0xCD0C81A9)
    {
        ValidateDefinition(def);
        entityIds ??= new uint[] { 5 };
        return MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds, cutSurfaceTag, baseSurfaceTag, uniqueFeatureId);
    }

    /// <summary>
    /// Overload accepting entity count.
    /// </summary>
    internal static byte[] BuildCutExtrudeBlock(ExtrudeFeatureDefinitionData def, string featureName, int entityCount)
    {
        ValidateDefinition(def);
        var entityIds = (entityCount == 1)
            ? new List<uint> { 5 }
            : (entityCount > 0)
                ? Enumerable.Range(1, entityCount).Select(i => (uint)i).ToList()
                : new List<uint> { 5 };
        return MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds);
    }

    private static void ValidateDefinition(ExtrudeFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (double.IsNaN(def.Depth) || double.IsInfinity(def.Depth) || def.Depth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Depth),
                $"Extrusion depth must be a positive finite number, but received {def.Depth}.");
        }

        if (def.BothDirections && (double.IsNaN(def.DepthDirection2) || double.IsInfinity(def.DepthDirection2) || def.DepthDirection2 <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(def.DepthDirection2),
                $"Direction 2 extrusion depth must be a positive finite number, but received {def.DepthDirection2}.");
        }
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
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                count++;
                i += pattern.Length - 1;
            }
        }
        return count;
    }
}
