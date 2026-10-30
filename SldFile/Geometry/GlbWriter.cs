using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace SldFile.Geometry;

/// <summary>
/// 将 <see cref="DisplayMesh"/> 转换为标准的二进制 glTF 2.0 (.glb) 容器。
/// 零第三方依赖，完全符合 Khronos glTF 2.0 工业标准。
/// </summary>
internal static class GlbWriter
{
    private const uint GlbMagic = 0x46546C67;    // "glTF"
    private const uint GlbVersion = 2;
    private const uint ChunkTypeJson = 0x4E4F534A; // "JSON"
    private const uint ChunkTypeBin = 0x004E4942;  // "BIN\0"

    private const int ComponentTypeFloat = 5126;
    private const int ComponentTypeUnsignedShort = 5123;
    private const int ComponentTypeUnsignedInt = 5125;

    private const int TargetArrayBuffer = 34962;
    private const int TargetElementArrayBuffer = 34963;

    public static void Write(DisplayMesh mesh, Stream outputStream, float scale = 0.001f)
    {
        if (mesh == null) throw new ArgumentNullException(nameof(mesh));
        if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));

        int vertexCount = mesh.Positions.Count;
        int indexCount = mesh.TriangleIndices.Count;
        bool hasNormals = mesh.Normals.Count == vertexCount && vertexCount > 0;
        bool useShort = vertexCount <= 65535;

        // 计算几何包围盒并转换缩放
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        if (vertexCount > 0)
        {
            for (int i = 0; i < vertexCount; i++)
            {
                var p = mesh.Positions[i] * scale;
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Z < minZ) minZ = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
                if (p.Z > maxZ) maxZ = p.Z;
            }
        }
        else
        {
            minX = minY = minZ = maxX = maxY = maxZ = 0.0f;
        }

        // 1. 组装二进制缓冲区 (BIN Chunk)
        int posByteLength = vertexCount * 12;
        int normByteLength = hasNormals ? (vertexCount * 12) : 0;
        int idxByteLength = indexCount * (useShort ? 2 : 4);

        int totalBinLength = posByteLength + normByteLength + idxByteLength;
        int binPadding = (4 - (totalBinLength % 4)) % 4;
        int paddedBinLength = totalBinLength + binPadding;

        byte[] binData = new byte[paddedBinLength];
        using (var binStream = new MemoryStream(binData))
        using (var binWriter = new BinaryWriter(binStream))
        {
            // 写入 Positions
            for (int i = 0; i < vertexCount; i++)
            {
                var p = mesh.Positions[i] * scale;
                binWriter.Write(p.X);
                binWriter.Write(p.Y);
                binWriter.Write(p.Z);
            }

            // 写入 Normals
            if (hasNormals)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    var n = mesh.Normals[i];
                    binWriter.Write(n.X);
                    binWriter.Write(n.Y);
                    binWriter.Write(n.Z);
                }
            }

            // 写入 Indices
            for (int i = 0; i < indexCount; i++)
            {
                int idx = mesh.TriangleIndices[i];
                if (useShort)
                    binWriter.Write((ushort)idx);
                else
                    binWriter.Write((uint)idx);
            }

            // 尾部填充 0x00
            for (int i = 0; i < binPadding; i++)
            {
                binWriter.Write((byte)0);
            }
        }

        // 2. 组装 JSON 元数据 (JSON Chunk)
        var sb = new StringBuilder(1024);
        var inv = CultureInfo.InvariantCulture;

        sb.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"SldFile glTF Exporter\"},");
        sb.Append("\"scenes\":[{\"nodes\":[0]}],\"scene\":0,");
        sb.Append("\"nodes\":[{\"mesh\":0}],");

        // meshes
        sb.Append("\"meshes\":[{\"primitives\":[{");
        if (hasNormals)
        {
            sb.Append("\"attributes\":{\"POSITION\":0,\"NORMAL\":1},\"indices\":2");
        }
        else
        {
            sb.Append("\"attributes\":{\"POSITION\":0},\"indices\":1");
        }
        sb.Append("}]}],");

        // accessors
        sb.Append("\"accessors\":[");
        // Accessor 0: POSITION
        sb.Append(string.Format(inv,
            "{{\"bufferView\":0,\"byteOffset\":0,\"componentType\":{0},\"count\":{1},\"type\":\"VEC3\",\"min\":[{2:F6},{3:F6},{4:F6}],\"max\":[{5:F6},{6:F6},{7:F6}]}}",
            ComponentTypeFloat, vertexCount, minX, minY, minZ, maxX, maxY, maxZ));

        int accessorIndex = 1;
        if (hasNormals)
        {
            // Accessor 1: NORMAL
            sb.Append(string.Format(inv,
                ",{{\"bufferView\":1,\"byteOffset\":0,\"componentType\":{0},\"count\":{1},\"type\":\"VEC3\"}}",
                ComponentTypeFloat, vertexCount));
            accessorIndex++;
        }

        // Accessor: INDICES
        sb.Append(string.Format(inv,
            ",{{\"bufferView\":{0},\"byteOffset\":0,\"componentType\":{1},\"count\":{2},\"type\":\"SCALAR\"}}",
            hasNormals ? 2 : 1, useShort ? ComponentTypeUnsignedShort : ComponentTypeUnsignedInt, indexCount));
        sb.Append("],");

        // bufferViews
        sb.Append("\"bufferViews\":[");
        // BufferView 0: POSITION
        sb.Append(string.Format(inv,
            "{{\"buffer\":0,\"byteOffset\":0,\"byteLength\":{0},\"target\":{1}}}",
            posByteLength, TargetArrayBuffer));

        int currentOffset = posByteLength;
        if (hasNormals)
        {
            // BufferView 1: NORMAL
            sb.Append(string.Format(inv,
                ",{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":{2}}}",
                currentOffset, normByteLength, TargetArrayBuffer));
            currentOffset += normByteLength;
        }

        // BufferView: INDICES
        sb.Append(string.Format(inv,
            ",{{\"buffer\":0,\"byteOffset\":{0},\"byteLength\":{1},\"target\":{2}}}",
            currentOffset, idxByteLength, TargetElementArrayBuffer));
        sb.Append("],");

        // buffers
        sb.Append(string.Format(inv,
            "\"buffers\":[{{\"byteLength\":{0}}}]}}",
            paddedBinLength));

        byte[] jsonBytes = Encoding.UTF8.GetBytes(sb.ToString());
        int jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
        int paddedJsonLength = jsonBytes.Length + jsonPadding;

        // 3. 计算 GLB 总长度并写入输出流
        uint totalGlbLength = (uint)(12 + (8 + paddedJsonLength) + (8 + paddedBinLength));

        using var writer = new BinaryWriter(outputStream, Encoding.UTF8, leaveOpen: true);
        // Header (12 bytes)
        writer.Write(GlbMagic);
        writer.Write(GlbVersion);
        writer.Write(totalGlbLength);

        // JSON Chunk (8 + paddedJsonLength bytes)
        writer.Write((uint)paddedJsonLength);
        writer.Write(ChunkTypeJson);
        writer.Write(jsonBytes);
        for (int i = 0; i < jsonPadding; i++)
        {
            writer.Write((byte)0x20); // glTF 规范要求 JSON padding 使用空格 (0x20)
        }

        // BIN Chunk (8 + paddedBinLength bytes)
        writer.Write((uint)paddedBinLength);
        writer.Write(ChunkTypeBin);
        writer.Write(binData);
        writer.Flush();
    }
}
