using System.IO;
using System.Text;
using SldFile;
using Xunit;

namespace SldFileTests;

public class DisplayMeshTests
{
    [Fact]
    public void DisplayMesh_WriteGlb_BoundsMatchBinaryPositionsWithoutRounding()
    {
        var mesh = new DisplayMesh
        {
            Positions = new[]
            {
                new System.Numerics.Vector3(0.0001234567f, -1.234567f, 2.345678f),
                new System.Numerics.Vector3(0.0002345678f, -0.1234567f, 3.456789f),
                new System.Numerics.Vector3(0.0003456789f, -0.2345678f, 4.567891f)
            },
            TriangleIndices = new[] { 0, 1, 2 }
        };
        using var output = new MemoryStream();
        mesh.WriteGlb(output);
        byte[] bytes = output.ToArray();
        int jsonLength = System.BitConverter.ToInt32(bytes, 12);
        using var json = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(bytes, 20, jsonLength));
        var positionAccessor = json.RootElement.GetProperty("accessors")[0];
        int positionOffset = 20 + jsonLength + 8;
        for (int axis = 0; axis < 3; axis++)
        {
            var values = Enumerable.Range(0, 3)
                .Select(i => System.BitConverter.ToSingle(bytes, positionOffset + i * 12 + axis * 4)).ToArray();
            Assert.Equal(values.Min(), positionAccessor.GetProperty("min")[axis].GetSingle());
            Assert.Equal(values.Max(), positionAccessor.GetProperty("max")[axis].GetSingle());
        }
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void DisplayMesh_2012CubePart_DecodesCorrectly()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var mesh = file.GetDisplayMesh();

        Assert.NotNull(mesh);
        // Cube has 6 faces, each face is 2 triangles (total 12 triangles)
        Assert.Equal(12, mesh!.TriangleCount);
        Assert.Equal(6, mesh.Parts.Count);

        // Dimensions of the model
        var size = mesh.Max - mesh.Min;
        Assert.True(size.X > 0 && size.Y > 0 && size.Z > 0, $"Size was {size}");

        // Outline edges: 6 faces * 4 edges each = 24 face-local boundary edges
        Assert.Equal(24, mesh.OutlineEdges.Count);

        // Normals should match vertex count
        Assert.Equal(mesh.VertexCount, mesh.Normals.Count);
    }

    [Fact]
    public void DisplayMesh_2012CylinderPart_DecodesCorrectly()
    {
        using var file = SwFile.Open(TestFileProvider.CylinderPart, readOnly: true);
        var mesh = file.GetDisplayMesh();

        Assert.NotNull(mesh);
        Assert.True(mesh!.TriangleCount > 12);
        Assert.True(mesh.VertexCount > 8);
        Assert.NotEmpty(mesh.Parts);
    }

    [Fact]
    public void DisplayMesh_2026CubePart_DecodesCorrectly()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart2026, readOnly: true);
        var mesh = file.GetDisplayMesh();

        Assert.NotNull(mesh);
        Assert.Equal(12, mesh!.TriangleCount);
        Assert.Equal(6, mesh.Parts.Count);

        var size = mesh.Max - mesh.Min;
        Assert.True(size.X > 0 && size.Y > 0 && size.Z > 0, $"Size was {size}");
    }

    [Fact]
    public void DisplayMesh_2012Assembly_DecodesCorrectly()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var mesh = file.GetDisplayMesh();

        Assert.NotNull(mesh);
        Assert.True(mesh!.TriangleCount > 0);
        Assert.NotEmpty(mesh.Parts);
    }

    [Fact]
    public void DisplayMesh_2026Assembly_ExtractsModernFaceTessellations()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly2026, readOnly: true);
        var mesh = file.GetDisplayMesh();
        // 2026 sample assembly stores tessellation in modern FaceTessellations streams
        Assert.NotNull(mesh);
        Assert.True(mesh!.VertexCount > 0);
        Assert.True(mesh.TriangleCount > 0);
        Assert.NotEmpty(mesh.Parts);
    }

    [Fact]
    public void DisplayMesh_MirrorAssembly_IfPresent_ExtractsMesh()
    {
        string realPath = @"E:\01_Work\93_开拔\21_镜像\AA\镜向001A.SLDASM";
        if (File.Exists(realPath))
        {
            using var file = SwFile.Open(realPath, readOnly: true);
            var mesh = file.GetDisplayMesh();
            Assert.NotNull(mesh);
            Assert.True(mesh!.VertexCount > 8000);
            Assert.True(mesh.TriangleCount > 5000);
        }
    }

    [Fact]
    public void DisplayMesh_WriteObj_GeneratesValidFormat()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var mesh = file.GetDisplayMesh();
        Assert.NotNull(mesh);

        using var sw = new StringWriter();
        mesh!.WriteObj(sw);
        string obj = sw.ToString();

        Assert.Contains("v ", obj);
        Assert.Contains("vn ", obj);
        Assert.Contains("f ", obj);

        // Verify line count has 12 face definitions
        int faceCount = 0;
        using var reader = new StringReader(obj);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("f ")) faceCount++;
        }
        Assert.Equal(12, faceCount);
    }

    [Fact]
    public void DisplayMesh_WriteStl_GeneratesValidBinaryStl()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var mesh = file.GetDisplayMesh();
        Assert.NotNull(mesh);

        using var ms = new MemoryStream();
        mesh!.WriteStl(ms);

        // Binary STL format: 80 bytes header + 4 bytes triangle count + 50 bytes per triangle
        byte[] stlBytes = ms.ToArray();
        int expectedSize = 80 + 4 + (12 * 50);
        Assert.Equal(expectedSize, stlBytes.Length);

        // Verify header and triangle count
        uint triCount = System.BitConverter.ToUInt32(stlBytes, 80);
        Assert.Equal(12u, triCount);
    }

    [Fact]
    public void DisplayMesh_WriteGlb_GeneratesValidGlbFormat()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var mesh = file.GetDisplayMesh();
        Assert.NotNull(mesh);

        using var ms = new MemoryStream();
        mesh!.WriteGlb(ms);

        byte[] glbBytes = ms.ToArray();
        Assert.True(glbBytes.Length >= 20);

        // 1. 验证 GLB 头部 (12 字节)
        uint magic = System.BitConverter.ToUInt32(glbBytes, 0);
        uint version = System.BitConverter.ToUInt32(glbBytes, 4);
        uint totalLength = System.BitConverter.ToUInt32(glbBytes, 8);

        Assert.Equal(0x46546C67u, magic); // glTF
        Assert.Equal(2u, version);
        Assert.Equal((uint)glbBytes.Length, totalLength);

        // 2. 验证 JSON Chunk
        uint jsonChunkLength = System.BitConverter.ToUInt32(glbBytes, 12);
        uint jsonChunkType = System.BitConverter.ToUInt32(glbBytes, 16);
        Assert.Equal(0x4E4F534Au, jsonChunkType); // JSON

        string json = Encoding.UTF8.GetString(glbBytes, 20, (int)jsonChunkLength);
        Assert.Contains("\"asset\"", json);
        Assert.Contains("\"version\":\"2.0\"", json);
        Assert.Contains("\"POSITION\"", json);
        Assert.Contains("\"NORMAL\"", json);

        // 3. 验证 BIN Chunk
        int binChunkHeaderOffset = 20 + (int)jsonChunkLength;
        uint binChunkLength = System.BitConverter.ToUInt32(glbBytes, binChunkHeaderOffset);
        uint binChunkType = System.BitConverter.ToUInt32(glbBytes, binChunkHeaderOffset + 4);
        Assert.Equal(0x004E4942u, binChunkType); // BIN\0
        Assert.True(binChunkLength > 0);
    }
}
