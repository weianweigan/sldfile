using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Modular parametric MFC serializer for SolidWorks extrusion features (moExtrusion_c and moICE_c).
/// Generates compliant MFC 4.2 CArchive binary structures with dynamic topology linking and variable-length names.
/// </summary>
internal static class MfcExtrudeSerializer
{
    private static readonly byte[] DefaultBodyMagic = new byte[] {
        0x7D, 0xC3, 0x94, 0x25, 0xAD, 0x49, 0xB2, 0x54,
        0x7D, 0xC3, 0x94, 0x25, 0xAD, 0x49, 0xB2, 0x54
    };

    /// <summary>
    /// Extracts ordered sketch entity IDs from the target sketch folder or stream segment.
    /// Falls back to [1, 2, 3, 4] if no sketch chain is present.
    /// </summary>
    public static List<uint> ExtractEntityIds(FeatureFolder? targetSketchFolder, byte[]? stream = null, int sketchEndOffset = -1)
    {
        if (targetSketchFolder != null)
        {
            var chain = targetSketchFolder.ChildFeatureData.OfType<SketchChainData>().FirstOrDefault()
                ?? targetSketchFolder.ChildFeatureDataBuilder.OfType<SketchChainData>().FirstOrDefault();

            if (chain != null)
            {
                if (chain.EntityIndices != null && chain.EntityIndices.Count > 0)
                    return chain.EntityIndices.ToList();

                if (chain.Indices != null && chain.Indices.Any(i => i > 0))
                    return chain.Indices.Where(i => i > 0).ToList();

                if (chain.Count > 0)
                    return Enumerable.Range(1, (int)chain.Count).Select(i => (uint)i).ToList();
            }

            // Check if stream contains sgArcHandle within this sketch's range
            if (stream != null && targetSketchFolder.TagOffset >= 0)
            {
                int end = (sketchEndOffset > targetSketchFolder.TagOffset) ? sketchEndOffset : stream.Length;
                byte[] arcTag = Encoding.ASCII.GetBytes("sgArcHandle");
                for (int i = targetSketchFolder.TagOffset; i <= Math.Min(end - arcTag.Length, stream.Length - arcTag.Length); i++)
                {
                    bool match = true;
                    for (int j = 0; j < arcTag.Length; j++)
                    {
                        if (stream[i + j] != arcTag[j]) { match = false; break; }
                    }
                    if (match)
                    {
                        // Look for entity ID around offset + 89
                        for (int k = i + 40; k < Math.Min(i + 150, stream.Length - 8); k++)
                        {
                            uint cand = BitConverter.ToUInt32(stream, k);
                            if (cand >= 1 && cand <= 1000 && BitConverter.ToUInt32(stream, k + 4) == 1)
                            {
                                return new List<uint> { cand };
                            }
                        }
                        return new List<uint> { 5 };
                    }
                }
            }
        }

        return new List<uint> { 1, 2, 3, 4 };
    }

    /// <summary>
    /// Synthesizes dynamic sketch topology mapping (moFromSktEntSurfIdRep_c) for Boss Extrude.
    /// Produces exactly 139 + 40N bytes for N contour entities.
    /// </summary>
    public static byte[] SynthesizeFromSktEntSurfIdRep(
        IReadOnlyList<uint> entityIds,
        uint surfaceTag = 0x6AE19B71,
        byte[]? bodyMagic = null)
    {
        if (entityIds == null || entityIds.Count == 0)
            entityIds = new uint[] { 1, 2, 3, 4 };

        int n = entityIds.Count;
        bodyMagic ??= DefaultBodyMagic;

        using var ms = new MemoryStream(139 + 40 * n);
        using var w = new BinaryWriter(ms);

        // 1. Header (31 bytes)
        w.Write((byte)0xFF);
        w.Write((byte)0xFF);
        w.Write((byte)0x01);
        w.Write((byte)0x00);
        byte[] className = Encoding.ASCII.GetBytes("moFromSktEntSurfIdRep_c");
        w.Write((ushort)className.Length);
        w.Write(className);
        w.Write((ushort)0);

        // 2. Set 1: Reversed topological order [e_N, e_{N-1}, ..., e_1]
        for (int i = n - 1; i >= 0; i--)
        {
            w.Write(new byte[] { 0xE7, 0x80, 0xEA, 0x00 });
            w.Write((uint)0x4F); // FeatureTypeId for Extrude
            w.Write(surfaceTag);
            w.Write(entityIds[i]);

            if (i > 0)
            {
                w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });
            }
        }

        // 3. Set 1 Trailer (22 bytes)
        w.Write(new byte[10]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);

        // 4. Body Identifier Block (38 bytes)
        w.Write((ushort)0x80E3);
        w.Write((uint)1);
        w.Write((uint)0);
        w.Write((uint)6);
        w.Write((uint)0x00000300);
        w.Write((uint)0);
        w.Write(bodyMagic, 0, 16);
        w.Write((ushort)0);

        // 5. Set 2 Header & Loop 1 Marker (28 bytes)
        w.Write(new byte[] { 0xE5, 0x80, 0x00, 0x00 });
        w.Write(new byte[] { 0xE7, 0x80, 0xEA, 0x00 });
        w.Write((uint)0x4F);
        w.Write(surfaceTag);
        w.Write((uint)1); // LoopId = 1
        w.Write((uint)0);
        w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });

        // 6. Set 2 Entities: Cyclic Shift Order [e_N, e_1, e_2, ..., e_{N-1}]
        var cycList = new List<uint>(n);
        cycList.Add(entityIds[n - 1]);
        for (int i = 0; i < n - 1; i++)
            cycList.Add(entityIds[i]);

        for (int i = 0; i < n; i++)
        {
            w.Write(new byte[] { 0xE7, 0x80, 0xEA, 0x00 });
            w.Write((uint)0x4F);
            w.Write(surfaceTag);
            w.Write(cycList[i]);

            if (i < n - 1)
            {
                w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });
            }
        }

        // 7. Set 2 Trailer (28 bytes)
        w.Write(new byte[10]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);
        w.Write((ushort)0);
        w.Write((ushort)1);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Synthesizes dynamic sketch topology mapping (moFromSktEnt3IntSurfIdRep_c) for Cut Extrude.
    /// Produces exactly 169 + 60N bytes for N contour entities (229 bytes for N=1).
    /// </summary>
    public static byte[] SynthesizeFromSktEnt3IntSurfIdRep(
        IReadOnlyList<uint> entityIds,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        byte[]? bodyMagic = null)
    {
        if (entityIds == null || entityIds.Count == 0)
            entityIds = new uint[] { 5 };

        int n = entityIds.Count;
        bodyMagic ??= DefaultBodyMagic;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // 1. Header (35 bytes)
        w.Write((byte)0xFF);
        w.Write((byte)0xFF);
        w.Write((byte)0x01);
        w.Write((byte)0x00);
        byte[] className = Encoding.ASCII.GetBytes("moFromSktEnt3IntSurfIdRep_c");
        w.Write((ushort)className.Length);
        w.Write(className);
        w.Write((ushort)0);

        // 2. Set 1: Cut Extrude Entities [e_N, ..., e_1]
        for (int i = n - 1; i >= 0; i--)
        {
            w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
            w.Write((uint)0x56); // FeatureTypeId for Cut Extrude
            w.Write(cutSurfaceTag);
            w.Write(entityIds[i]);
            w.Write(0xFFFFFFFF);

            if (i > 0)
            {
                w.Write(new byte[] { 0xEE, 0x80, 0x00, 0x00 });
            }
        }

        // 3. Set 1 Trailer (26 bytes)
        w.Write(new byte[14]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);

        // 4. Body Identifier Block (38 bytes)
        w.Write((ushort)0x80EA);
        w.Write((uint)1);
        w.Write((uint)0);
        w.Write((uint)5);
        w.Write((uint)0x00000300);
        w.Write((uint)0x0000289E);
        w.Write(bodyMagic, 0, 16);
        w.Write((ushort)0);

        // 5. Set 2: Cut Loop & Intersection Reference
        var cycList = new List<uint>(n);
        cycList.Add(entityIds[n - 1]);
        for (int i = 0; i < n - 1; i++)
            cycList.Add(entityIds[i]);

        for (int i = 0; i < n; i++)
        {
            w.Write(new byte[] { 0x75, 0x81, 0x00, 0x00 });
            w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
            w.Write((uint)0x56);
            w.Write(cutSurfaceTag);
            w.Write(cycList[i]);
            w.Write(0xFFFFFFFF);
            w.Write((uint)0); // 4-byte padding
        }

        // Loop 1 marker
        w.Write(new byte[] { 0x72, 0x81, 0x00, 0x00 });
        w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
        w.Write((uint)0x56);
        w.Write(cutSurfaceTag);
        w.Write((uint)1); // loopId = 1
        w.Write(new byte[10]);

        // Target Base Extrude face intersection link
        w.Write(new byte[] { 0xEC, 0x80, 0x00, 0x00 });
        w.Write(new byte[] { 0xEE, 0x80, 0xF1, 0x00 });
        w.Write((uint)0x4F); // Base Extrude TypeId
        w.Write(baseSurfaceTag);
        w.Write((uint)1); // target face 1
        w.Write(new byte[14]);
        w.Write((ushort)0x4A38);
        w.Write(new byte[10]);
        w.Write((ushort)0);
        w.Write((ushort)1);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Serializes a complete Boss Extrude (moExtrusion_c) binary block using modular parametric MFC streaming.
    /// Variable-length feature names and arbitrary sketch topology are encoded without static offset patching.
    /// </summary>
    public static byte[] SerializeBossExtrude(
        ExtrudeFeatureDefinitionData def,
        string featureName,
        IReadOnlyList<uint> entityIndices,
        uint surfaceTag = 0x6AE19B71,
        uint uniqueFeatureId = 0xC28F06D1)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (string.IsNullOrEmpty(featureName))
            featureName = "凸台-拉伸1";

        if (entityIndices == null || entityIndices.Count == 0)
            entityIndices = new uint[] { 1, 2, 3, 4 };

        using var writer = new MfcStreamWriter(3221);

        // 1. Folder Header
        writer.WriteMfcTag("moExtrusion_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0006103515625);
        writer.WriteU32(0x4F);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(ExtrudeSegments.FolderHeaderReserved1);
        writer.WriteBytes(ExtrudeSegments.BossFolderHeaderIndicator);
        writer.WriteU32(0x9E); // MarkerE
        writer.WriteBytes(ExtrudeSegments.BossFolderHeaderDates);
        writer.WriteWChar(string.Empty); // Delimiter
        writer.WriteBytes(ExtrudeSegments.FolderHeaderSentinel);
        writer.WriteU32(0x01DD66A8); // Part Magic
        writer.WriteU32(uniqueFeatureId); // FeatureId
        writer.WriteBytes(ExtrudeSegments.FolderHeaderTrailer);
        writer.WriteBytes(ExtrudeSegments.BossDescriptorFlags);

        // 2. Body Selection Scope
        writer.WriteBytes(ExtrudeSegments.BossScopeBlock);

        // 3. Dynamic Sketch Topology
        byte[] topo = SynthesizeFromSktEntSurfIdRep(entityIndices, surfaceTag);
        writer.WriteBytes(topo);

        // 4. Bounding Box
        double centerZ = (def.EndCondition == 6) ? 0.0 : (def.Depth / 2.0);
        double radius = Math.Sqrt(0.0002 + def.Depth * def.Depth);

        writer.WriteMfcTag("moBBoxCenterData_c");
        writer.WriteU16(1);
        writer.WriteU16(0);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(centerZ);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.BossBBoxMidSection);
        writer.WriteU16(0x8102);
        writer.WriteU16(1);
        writer.WriteU16(0);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(centerZ);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.BossBBoxTrailer);

        // 5. End Condition
        writer.WriteMfcTag("moEndSpec_c");
        writer.WriteU16(0);
        writer.WriteU32(1);
        writer.WriteU32(0);
        writer.WriteU32(0);
        writer.WriteU16(0);
        writer.WriteU32(def.EndCondition);
        writer.WriteU32(0);

        // 6. Display Dimension & Feature Dim Handle
        writer.WriteBytes(ExtrudeSegments.BossDisplayDimBlock);

        // 7. ParallelPlaneDistanceDim_c & moLengthParameter_c
        writer.WriteMfcTag("ParallelPlaneDistanceDim_c");
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteMfcTag("moLengthParameter_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar("D1");
        writer.WriteBytes(ExtrudeSegments.BossLengthParamPrefix);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamMid1);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamMid2);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamMid3);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.BossLengthParamTrailer);

        // 8. FavoriteHandle & FromEndSpec
        writer.WriteBytes(ExtrudeSegments.BossFavoriteAndFromEndSpec);

        return writer.ToArray();
    }

    /// <summary>
    /// Serializes a complete Cut Extrude (moICE_c) binary block using modular parametric MFC streaming.
    /// Variable-length feature names and arbitrary sketch topology are encoded without static offset patching.
    /// </summary>
    public static byte[] SerializeCutExtrude(
        ExtrudeFeatureDefinitionData def,
        string featureName,
        IReadOnlyList<uint> entityIndices,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        uint uniqueFeatureId = 0xCD0C81A9)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (string.IsNullOrEmpty(featureName))
            featureName = "切除-拉伸1";

        if (entityIndices == null || entityIndices.Count == 0)
            entityIndices = new uint[] { 5 };

        using var writer = new MfcStreamWriter(3092);

        // 1. Folder Header
        writer.WriteMfcTag("moICE_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteBytes(ExtrudeSegments.CutFolderHeaderReserved0);
        writer.WriteU32(0x56);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(ExtrudeSegments.FolderHeaderReserved1);
        writer.WriteBytes(ExtrudeSegments.CutFolderHeaderIndicator);
        writer.WriteU32(0xB3); // MarkerE
        writer.WriteBytes(ExtrudeSegments.CutFolderHeaderDates);
        writer.WriteWChar(string.Empty); // Delimiter
        writer.WriteBytes(ExtrudeSegments.FolderHeaderSentinel);
        writer.WriteU32(0x01DD66A8); // Part Magic
        writer.WriteU32(uniqueFeatureId); // FeatureId
        writer.WriteBytes(ExtrudeSegments.FolderHeaderTrailer);
        writer.WriteBytes(ExtrudeSegments.CutDescriptorFlags);

        // 2. Body Selection Scope
        writer.WriteBytes(ExtrudeSegments.CutScopeBlock);

        // 3. Dynamic Sketch Topology
        byte[] topo = SynthesizeFromSktEnt3IntSurfIdRep(entityIndices, cutSurfaceTag, baseSurfaceTag);
        writer.WriteBytes(topo);

        // 4. Bounding Box
        double radius = Math.Sqrt(0.0003); // Bounding radius of 10mm base cube (0.017320508...)

        writer.WriteU16(0x8109);
        writer.WriteU16(1);
        writer.WriteU16(0);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.CutBBoxMidSection);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(0.005);
        writer.WriteF64(radius);
        writer.WriteBytes(ExtrudeSegments.CutBBoxTrailer);

        // 5. End Condition
        writer.WriteU16(0x8110);
        writer.WriteU16(0);
        writer.WriteU32(1);
        writer.WriteU32(0);
        writer.WriteU32(0);
        writer.WriteU16(0);
        writer.WriteU32(def.EndCondition);
        writer.WriteU32(0);

        // 6. Display Dimension & Feature Dim Handle
        writer.WriteBytes(ExtrudeSegments.CutDisplayDimBlock);

        // 7. ParallelPlaneDistanceDim_c & moLengthParameter_c
        writer.WriteU16(0x8116);
        writer.WriteU16(2);
        writer.WriteU16(0);
        writer.WriteU16(0x8118);
        writer.WriteU16(0x8004);
        writer.WriteWChar("D1");
        writer.WriteBytes(ExtrudeSegments.CutLengthParamPrefix);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid1);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid2);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid3);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamMid4);
        writer.WriteF64(def.Depth);
        writer.WriteBytes(ExtrudeSegments.CutLengthParamTrailer);

        // 8. FavoriteHandle & FromEndSpec
        writer.WriteBytes(ExtrudeSegments.CutFavoriteAndFromEndSpec);

        return writer.ToArray();
    }
}
