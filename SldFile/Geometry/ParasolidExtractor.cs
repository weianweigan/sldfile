using System;
using System.IO;
using System.IO.Compression;

namespace SldFile.Geometry;

/// <summary>
/// Extracts genuine Parasolid binary transmit files (X_B format) from SolidWorks partition streams.
/// </summary>
public static class ParasolidExtractor
{
    private static readonly byte[] PsMagic = { (byte)'P', (byte)'S', 0, 0 };
    private static readonly byte[] WrappedMagic =
    {
        0x23, 0x1d, 0xd5, 0x71, 0xda, 0x81, 0x48, 0xa2,
        0xa8, 0x58, 0x98, 0xb2, 0x1b, 0x89, 0xef, 0x99
    };

    /// <summary>
    /// Attempts to extract clean Parasolid X_B binary bytes from partition payload data.
    /// Returns null if no valid Parasolid stream is found.
    /// </summary>
    public static byte[]? Extract(byte[] payload)
    {
        if (payload == null || payload.Length < 4)
            return null;

        // 1. Direct uncompressed Parasolid stream starting with PS\0\0
        int directIdx = IndexOf(payload, PsMagic);
        if (directIdx >= 0)
        {
            byte[] direct = new byte[payload.Length - directIdx];
            Buffer.BlockCopy(payload, directIdx, direct, 0, direct.Length);
            return direct;
        }

        // 2. Wrapped compressed Parasolid member
        int wrappedIdx = IndexOf(payload, WrappedMagic);
        int searchStart = wrappedIdx >= 0 ? wrappedIdx + WrappedMagic.Length : 0;

        for (int i = searchStart; i < payload.Length - 2; i++)
        {
            if (payload[i] == 0x78 && (payload[i + 1] == 0x01 || payload[i + 1] == 0x9C || payload[i + 1] == 0xDA))
            {
                try
                {
                    using var ms = new MemoryStream(payload, i + 2, payload.Length - (i + 2));
                    using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    deflate.CopyTo(outMs);
                    byte[] candidate = outMs.ToArray();

                    if (candidate.Length >= 4 &&
                        candidate[0] == PsMagic[0] &&
                        candidate[1] == PsMagic[1] &&
                        candidate[2] == PsMagic[2] &&
                        candidate[3] == PsMagic[3])
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore non-zlib false positives and continue searching
                }
            }
        }

        return null;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
        int limit = haystack.Length - needle.Length;
        for (int i = 0; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}
