using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using SldFile;
using SldFile.Geometry;
using SldFile.Geometry.Parasolid;
using Xunit;

namespace SldFileTests;

public class ParasolidFeatureTests
{
    [Fact]
    public void Extract_DirectPsStream_ReturnsStreamSlice()
    {
        byte[] psHeader = Encoding.ASCII.GetBytes("PS\0\0: TRANSMIT FILE created today");
        byte[] payload = new byte[psHeader.Length + 10];
        Buffer.BlockCopy(psHeader, 0, payload, 5, psHeader.Length);

        byte[]? result = ParasolidExtractor.Extract(payload);
        Assert.NotNull(result);
        Assert.Equal((byte)'P', result![0]);
        Assert.Equal((byte)'S', result[1]);
        Assert.Equal(0, result[2]);
        Assert.Equal(0, result[3]);
    }

    [Fact]
    public void Extract_WrappedParasolidChainedFrames_ExtractsAndConcatenates()
    {
        // Construct two uncompressed blocks of PS data
        byte[] block1 = Encoding.ASCII.GetBytes("PS\0\0Part1-Payload-12345678");
        byte[] block2 = Encoding.ASCII.GetBytes("-Continuation-87654321");

        // Compress blocks using Deflate
        byte[] comp1 = CompressZlib(block1);
        byte[] comp2 = CompressZlib(block2);

        // Build wrapped frame section with 16-byte GUID
        using var ms = new MemoryStream();
        // 4 bytes chain length placeholder
        uint totalSectionLen = (uint)(16 + (8 + comp1.Length) + (8 + comp2.Length));
        ms.Write(BitConverter.GetBytes(totalSectionLen), 0, 4);

        // 16-byte GUID
        ms.Write(ParasolidExtractor.ParasolidWrapperGuid, 0, 16);

        // Frame 1 header: uncompressed (4) + compressed (4)
        ms.Write(BitConverter.GetBytes((uint)block1.Length), 0, 4);
        ms.Write(BitConverter.GetBytes((uint)comp1.Length), 0, 4);
        ms.Write(comp1, 0, comp1.Length);

        // Frame 2 header: uncompressed (4) + compressed (4)
        ms.Write(BitConverter.GetBytes((uint)block2.Length), 0, 4);
        ms.Write(BitConverter.GetBytes((uint)comp2.Length), 0, 4);
        ms.Write(comp2, 0, comp2.Length);

        byte[] payload = ms.ToArray();
        byte[]? extracted = ParasolidExtractor.Extract(payload);

        Assert.NotNull(extracted);
        Assert.Equal(block1.Length + block2.Length, extracted!.Length);
        string extractedStr = Encoding.ASCII.GetString(extracted);
        Assert.StartsWith("PS\0\0Part1-Payload-12345678-Continuation-87654321", extractedStr);
    }

    [Fact]
    public void Extract_InvalidOrTruncatedPayload_ReturnsNullSafely()
    {
        Assert.Null(ParasolidExtractor.Extract(null!));
        Assert.Null(ParasolidExtractor.Extract(Array.Empty<byte>()));
        Assert.Null(ParasolidExtractor.Extract(new byte[] { 1, 2, 3 }));

        // Only GUID without frame data
        byte[] partial = new byte[20];
        Buffer.BlockCopy(ParasolidExtractor.ParasolidWrapperGuid, 0, partial, 0, 16);
        Assert.Null(ParasolidExtractor.Extract(partial));
    }

    [Fact]
    public void XtHeader_TryParse_ValidTransmitAndSchema()
    {
        string desc = ": TRANSMIT FILE (partition) test model";
        byte[] descBytes = Encoding.ASCII.GetBytes(desc);
        byte[] schemaBytes = Encoding.ASCII.GetBytes("SCH_3400000_12345");

        using var ms = new MemoryStream();
        ms.Write(new byte[] { (byte)'P', (byte)'S', 0, 0 }, 0, 4);
        ms.WriteByte((byte)(descBytes.Length >> 8));
        ms.WriteByte((byte)(descBytes.Length & 0xFF));
        ms.Write(descBytes, 0, descBytes.Length);
        ms.WriteByte((byte)schemaBytes.Length);
        ms.Write(schemaBytes, 0, schemaBytes.Length);

        bool success = XtHeader.TryParse(ms.ToArray(), out var header);
        Assert.True(success);
        Assert.Equal(XtStreamKind.Partition, header.Kind);
        Assert.Equal(desc, header.Description);
        Assert.Contains("SCH_3400000", header.Schema);
    }

    [Fact]
    public void XtParser_Parse_BuildsBasicTopologyAndVertices()
    {
        using var ms = new MemoryStream();
        byte[] headerBytes = Encoding.ASCII.GetBytes("PS\0\0: TRANSMIT FILE test");
        ms.Write(headerBytes, 0, headerBytes.Length);
        // Pad to offset 32
        while (ms.Length < 32) ms.WriteByte(0);

        // Write two double points (10, 20, 30) and (40, 50, 60)
        ms.Write(BitConverter.GetBytes(10.0), 0, 8);
        ms.Write(BitConverter.GetBytes(20.0), 0, 8);
        ms.Write(BitConverter.GetBytes(30.0), 0, 8);

        ms.Write(BitConverter.GetBytes(40.0), 0, 8);
        ms.Write(BitConverter.GetBytes(50.0), 0, 8);
        ms.Write(BitConverter.GetBytes(60.0), 0, 8);

        var model = XtParser.Parse(ms.ToArray());
        Assert.NotNull(model);
        Assert.Single(model.Bodies);
        Assert.True(model.Geometries.Count >= 2);
    }

    [Fact]
    public void Part_ExtractParasolid_OnRealSample_ExecutesWithoutException()
    {
        if (!File.Exists(TestFileProvider.CubePart)) return;

        using var part = new SldPart(TestFileProvider.CubePart, readOnly: true);
        byte[]? ps = part.ExtractParasolid();
        // May be null if CubePart does not cache Parasolid or returns bytes
        if (ps != null)
        {
            Assert.True(ps.Length >= 4);
            Assert.Equal((byte)'P', ps[0]);
            Assert.Equal((byte)'S', ps[1]);
        }
    }

    private static byte[] CompressZlib(byte[] data)
    {
        using var outMs = new MemoryStream();
        // Write standard 2-byte zlib header (0x78 0x9C)
        outMs.WriteByte(0x78);
        outMs.WriteByte(0x9C);
        using (var deflate = new DeflateStream(outMs, CompressionMode.Compress, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        return outMs.ToArray();
    }
}
