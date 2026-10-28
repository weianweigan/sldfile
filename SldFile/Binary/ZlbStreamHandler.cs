using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using OpenMcdf;

namespace SldFile.Binary;

internal sealed class ZlbStreamReader : IBinaryStreamReader<ZlbData>
{
    private const int PrefixLength = 24;

    public string StreamName => "*__ZLB";

    public void Parse(byte[] data, ZlbData model) => Read(data, model);

    /// <summary>
    /// Reads and decompresses a __ZLB stream from a byte array.
    /// </summary>
    public void Read(byte[] data, ZlbData model)
    {
        ReadInternal(data, model);
    }

    /// <summary>
    /// Reads and decompresses a __ZLB stream.
    /// XML vs binary classification is determined by inspecting the decompressed
    /// content (checks for <c>&lt;?xml</c> header), not by the stream path.
    /// </summary>
    public void Read(CfbStream stream, ZlbData model)
    {
        var data = new byte[stream.Length];
        stream.Read(data, 0, (int)stream.Length);
        ReadInternal(data, model);
    }

    private void ReadInternal(byte[] data, ZlbData model)
    {
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

        // No valid zlib data found — store all bytes as prefix
        model.Prefix = data;
        model.XmlContent = string.Empty;
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
}
