using System.IO;
using System.Text;
using SldFile;
using Xunit;

namespace SldFileTests;

public class ParasolidExtractionTests
{
    private static readonly byte[] PsMagic = { (byte)'P', (byte)'S', 0, 0 };

    [Fact]
    public void Parasolid_2012CubePart_ExtractsValidXb()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        byte[]? xb = file.ExtractParasolid();

        Assert.NotNull(xb);
        Assert.True(xb!.Length > 1000);

        // Verify Parasolid X_B magic PS\0\0
        Assert.Equal(PsMagic[0], xb[0]);
        Assert.Equal(PsMagic[1], xb[1]);
        Assert.Equal(PsMagic[2], xb[2]);
        Assert.Equal(PsMagic[3], xb[3]);

        // Verify text header
        string header = Encoding.ASCII.GetString(xb, 0, 100);
        Assert.Contains("TRANSMIT FILE", header);
        Assert.Contains("SCH_", header);

        // Test export to stream
        using var ms = new MemoryStream();
        file.ExportParasolid(ms);
        Assert.Equal(xb.Length, ms.Length);
    }

    [Fact]
    public void Parasolid_2012CylinderPart_ExtractsValidXb()
    {
        using var file = SwFile.Open(TestFileProvider.CylinderPart, readOnly: true);
        byte[]? xb = file.ExtractParasolid();

        Assert.NotNull(xb);
        Assert.True(xb!.Length > 1000);
        Assert.Equal(PsMagic[0], xb[0]);
        Assert.Equal(PsMagic[1], xb[1]);
    }

    [Fact]
    public void Parasolid_2026CubePart_ExtractsValidXb()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart2026, readOnly: true);
        byte[]? xb = file.ExtractParasolid();

        Assert.NotNull(xb);
        Assert.True(xb!.Length > 1000);

        Assert.Equal(PsMagic[0], xb[0]);
        Assert.Equal(PsMagic[1], xb[1]);
        Assert.Equal(PsMagic[2], xb[2]);
        Assert.Equal(PsMagic[3], xb[3]);

        string header = Encoding.ASCII.GetString(xb, 0, 100);
        Assert.Contains("TRANSMIT FILE", header);
        Assert.Contains("SCH_3701229", header);
    }

    [Fact]
    public void Parasolid_ExportParasolidFile_WritesToFile()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        string tempPath = Path.Combine(Path.GetTempPath(), $"cube_export_{System.Guid.NewGuid():N}.x_b");
        try
        {
            file.ExportParasolid(tempPath);
            Assert.True(File.Exists(tempPath));
            byte[] exported = File.ReadAllBytes(tempPath);
            Assert.True(exported.Length > 1000);
            Assert.Equal(PsMagic[0], exported[0]);
            Assert.Equal(PsMagic[1], exported[1]);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
