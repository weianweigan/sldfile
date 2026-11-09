using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SldFile.Binary;

/// <summary>
/// Stream handler for __ZLB compressed streams.
/// Symmetrically reads and writes the 24-byte prefix and Deflate compressed content.
/// </summary>
internal static class ZlbStreamHandler
{
    private const int PrefixLength = 24;

    public static void Parse(byte[] data, ZlbData model) => Decompress(data, model);

    public static void Decompress(byte[] data, ZlbData model)
    {
        if (data == null || data.Length == 0)
        {
            model.Prefix = Array.Empty<byte>();
            model.XmlContent = string.Empty;
            return;
        }

        if (data.Length <= PrefixLength)
        {
            model.Prefix = data;
            model.XmlContent = string.Empty;
            return;
        }

        // Standard ZLB format: 24-byte prefix (GUID + 2x UInt32 sizes), then zlib stream.
        if (TryDecompress(data, PrefixLength, model))
            return;

        // Fallback: scan for zlib header within first 128 bytes.
        int zlibIndex = -1;
        for (int i = 0; i < Math.Min(128, data.Length - 2); i++)
        {
            if (data[i] == 0x78 && (data[i + 1] == 0x01 || data[i + 1] == 0x5E ||
                                     data[i + 1] == 0x9C || data[i + 1] == 0xDA))
            {
                zlibIndex = i;
                break;
            }
        }

        if (zlibIndex != -1 && zlibIndex != PrefixLength && TryDecompress(data, zlibIndex, model))
            return;

        // No valid zlib data found 鈥?store all bytes as prefix
        model.Prefix = data;
        model.XmlContent = string.Empty;
    }

    public static byte[] Serialize(ZlbData model) => Compress(model);

    public static byte[] Compress(ZlbData model)
    {
        byte[] payload = model.IsXml
            ? Encoding.UTF8.GetBytes(model.XmlContent)
            : (model.RawData ?? Array.Empty<byte>());

        if (payload.Length == 0 && (model.Prefix == null || model.Prefix.Length <= PrefixLength))
            return model.Prefix ?? Array.Empty<byte>();

        // Compress payload using Deflate with zlib header
        using var compressedMs = new MemoryStream();
        // Zlib header (deflate, default compression)
        compressedMs.WriteByte(0x78);
        compressedMs.WriteByte(0x9C);

        using (var ds = new DeflateStream(compressedMs, CompressionLevel.Optimal, leaveOpen: true))
        {
            ds.Write(payload, 0, payload.Length);
        }

        // Adler32 checksum
        uint adler = CalculateAdler32(payload);
        compressedMs.WriteByte((byte)((adler >> 24) & 0xFF));
        compressedMs.WriteByte((byte)((adler >> 16) & 0xFF));
        compressedMs.WriteByte((byte)((adler >> 8) & 0xFF));
        compressedMs.WriteByte((byte)(adler & 0xFF));

        byte[] compressedData = compressedMs.ToArray();

        // Build 24-byte prefix
        byte[] prefix = new byte[PrefixLength];
        if (model.Prefix != null && model.Prefix.Length >= 16)
        {
            Array.Copy(model.Prefix, 0, prefix, 0, 16);
        }
        else
        {
            Array.Copy(ZlbData.ExpectedGuid, 0, prefix, 0, 16);
        }

        BitConverter.GetBytes((uint)payload.Length).CopyTo(prefix, 16);
        BitConverter.GetBytes((uint)compressedData.Length).CopyTo(prefix, 20);

        var result = new byte[PrefixLength + compressedData.Length];
        Array.Copy(prefix, 0, result, 0, PrefixLength);
        Array.Copy(compressedData, 0, result, PrefixLength, compressedData.Length);
        return result;
    }

    private static bool TryDecompress(byte[] data, int zlibOffset, ZlbData model)
    {
        if (zlibOffset + 2 > data.Length)
            return false;
        if (data[zlibOffset] != 0x78)
            return false;
        if (data[zlibOffset + 1] != 0x01 && data[zlibOffset + 1] != 0x5E &&
            data[zlibOffset + 1] != 0x9C && data[zlibOffset + 1] != 0xDA)
            return false;

        model.Prefix = new byte[zlibOffset];
        Array.Copy(data, 0, model.Prefix, 0, zlibOffset);

        int deflateStart = zlibOffset + 2;
        int deflateLength = data.Length - deflateStart - 4;
        if (deflateLength <= 0)
        {
            model.XmlContent = string.Empty;
            return true;
        }

        try
        {
            using var ms = new MemoryStream(data, deflateStart, deflateLength);
            using var ds = new DeflateStream(ms, CompressionMode.Decompress);
            using var rs = new MemoryStream();
            ds.CopyTo(rs);

            var decodedData = rs.ToArray();
            model.SetDecompressedContent(decodedData);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static uint CalculateAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        const uint mod = 65521;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + data[i]) % mod;
            b = (b + a) % mod;
        }
        return (b << 16) | a;
    }
}
