using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SldFile.Geometry;

/// <summary>
/// Extracts genuine Parasolid binary transmit files (X_B format) from SolidWorks partition streams.
/// Supports modern chained multi-frame wrappers, direct transmit payloads, and fallback compressed blocks.
/// </summary>
public static class ParasolidExtractor
{
    private static readonly byte[] PsMagic = { (byte)'P', (byte)'S', 0, 0 };

    /// <summary>
    /// Magic 16-byte GUID used by SolidWorks to wrap embedded Parasolid streams:
    /// {231DD571-81DA-A248-A858-98B21B89EF99}
    /// </summary>
    public static readonly byte[] ParasolidWrapperGuid =
    {
        0x23, 0x1d, 0xd5, 0x71, 0xda, 0x81, 0x48, 0xa2,
        0xa8, 0x58, 0x98, 0xb2, 0x1b, 0x89, 0xef, 0x99
    };

    private const int HeaderSize = 16 + 8; // 16 bytes GUID + 4 bytes uncompressed_size + 4 bytes compressed_size
    private const int MaxStreamSize = 128 * 1024 * 1024; // 128 MB sanity limit

    /// <summary>
    /// Attempts to extract clean Parasolid X_B binary bytes from a raw partition payload.
    /// Returns null if no valid Parasolid stream is found.
    /// </summary>
    public static byte[]? Extract(byte[] payload)
    {
        if (payload == null || payload.Length < 4)
            return null;

        // 1. Direct uncompressed Parasolid stream starting with PS\0\0
        int directIdx = IndexOf(payload, PsMagic);
        if (directIdx == 0)
        {
            return (byte[])payload.Clone();
        }
        if (directIdx > 0 && directIdx + 4 <= payload.Length)
        {
            // If preceded by small padding, return the stream slice
            byte[] direct = new byte[payload.Length - directIdx];
            Buffer.BlockCopy(payload, directIdx, direct, 0, direct.Length);
            return direct;
        }

        // 2. Chained or standalone wrapped Parasolid member using the 16-byte GUID marker
        int searchPos = 0;
        while (searchPos < payload.Length)
        {
            int markerPos = IndexOf(payload, ParasolidWrapperGuid, searchPos);
            if (markerPos < 0) break;

            if (payload.Length - markerPos >= HeaderSize)
            {
                var extracted = TryExtractWrappedStream(payload, markerPos);
                if (extracted != null && extracted.Length >= 4 &&
                    extracted[0] == PsMagic[0] && extracted[1] == PsMagic[1] &&
                    extracted[2] == PsMagic[2] && extracted[3] == PsMagic[3])
                {
                    return extracted;
                }
            }

            searchPos = markerPos + 1;
        }

        // 3. Fallback: heuristic search for zlib blocks (0x78 0x01 / 0x9C / 0xDA)
        return ExtractFallbackZlib(payload);
    }

    /// <summary>
    /// Searches for Parasolid streams in document streams matching the specified configuration.
    /// </summary>
    public static byte[]? ExtractFromStreams(IDictionary<string, byte[]> streams, int configIndex = 0)
    {
        if (streams == null || streams.Count == 0) return null;

        // Try exact config matching first
        string[] candidates = configIndex > 0
            ? new[]
            {
                $"Contents/Config-{configIndex}-PartGeometry",
                $"Contents/Config-{configIndex}-Geometry",
                $"Contents/Config-{configIndex}",
                $"Config-{configIndex}",
                $"Contents/SectionBodies/View-{configIndex}"
            }
            : new[]
            {
                "Contents/Config-0-PartGeometry",
                "Contents/Config-0-Geometry",
                "Contents/Config-0",
                "Config-0"
            };

        foreach (var name in candidates)
        {
            if (streams.TryGetValue(name, out var data) && data != null && data.Length > 0)
            {
                var ps = Extract(data);
                if (ps != null) return ps;
            }
        }

        // Search any stream containing "Config-{configIndex}"
        string configToken = configIndex > 0 ? $"Config-{configIndex}" : "Config-";
        foreach (var pair in streams)
        {
            if (pair.Key.IndexOf(configToken, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !pair.Key.EndsWith("-Preview", StringComparison.OrdinalIgnoreCase) &&
                !pair.Key.EndsWith("-PreviewPNG", StringComparison.OrdinalIgnoreCase) &&
                !pair.Key.EndsWith("-Properties", StringComparison.OrdinalIgnoreCase))
            {
                var ps = Extract(pair.Value);
                if (ps != null) return ps;
            }
        }

        return null;
    }

    private static byte[]? TryExtractWrappedStream(byte[] payload, int markerPos)
    {
        int sectionEnd = payload.Length;
        bool chained = false;

        if (markerPos >= 4)
        {
            uint chainLength = BitConverter.ToUInt32(payload, markerPos - 4);
            if (chainLength >= HeaderSize &&
                (chainLength <= (uint)(payload.Length - markerPos) || markerPos == 4))
            {
                if (chainLength <= (uint)(payload.Length - markerPos))
                {
                    chained = true;
                    sectionEnd = markerPos + (int)chainLength;
                }
            }
        }

        int frameAt = markerPos + ParasolidWrapperGuid.Length;
        using var outMs = new MemoryStream();

        while (frameAt < sectionEnd)
        {
            int remaining = sectionEnd - frameAt;
            if (chained && IsAllZero(payload, frameAt, remaining))
            {
                break;
            }
            if (remaining < 8) break;

            uint uncompressedSize = BitConverter.ToUInt32(payload, frameAt);
            uint compressedSize = BitConverter.ToUInt32(payload, frameAt + 4);
            int payloadOffset = frameAt + 8;

            if (compressedSize == 0 || uncompressedSize == 0) break;
            if (compressedSize > (uint)(sectionEnd - payloadOffset)) break;
            if (uncompressedSize > MaxStreamSize - outMs.Length) break;

            byte[]? inflated = InflatePayload(payload, payloadOffset, (int)compressedSize, (int)uncompressedSize);
            if (inflated == null || inflated.Length != (int)uncompressedSize)
            {
                break;
            }

            outMs.Write(inflated, 0, inflated.Length);
            frameAt = payloadOffset + (int)compressedSize;

            if (!chained)
                break;
        }

        return outMs.Length > 0 ? outMs.ToArray() : null;
    }

    private static bool IsAllZero(byte[] data, int start, int count)
    {
        int end = Math.Min(data.Length, start + count);
        for (int i = start; i < end; i++)
        {
            if (data[i] != 0) return false;
        }
        return true;
    }

    private static byte[]? InflatePayload(byte[] data, int offset, int length, int expectedUncompressedSize)
    {
        // Try skipping zlib 2-byte header if present
        int streamOffset = offset;
        int streamLength = length;

        if (length > 2 && data[offset] == 0x78 && (data[offset + 1] == 0x01 || data[offset + 1] == 0x9C || data[offset + 1] == 0xDA))
        {
            streamOffset += 2;
            streamLength -= 2;
        }

        try
        {
            using var ms = new MemoryStream(data, streamOffset, streamLength);
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream(expectedUncompressedSize > 0 ? expectedUncompressedSize : 4096);
            deflate.CopyTo(outMs);
            byte[] result = outMs.ToArray();
            if (expectedUncompressedSize <= 0 || result.Length == expectedUncompressedSize)
                return result;
        }
        catch
        {
            // If zlib skip failed, try raw
            if (streamOffset != offset)
            {
                try
                {
                    using var msRaw = new MemoryStream(data, offset, length);
                    using var deflateRaw = new DeflateStream(msRaw, CompressionMode.Decompress);
                    using var outMs = new MemoryStream(expectedUncompressedSize > 0 ? expectedUncompressedSize : 4096);
                    deflateRaw.CopyTo(outMs);
                    return outMs.ToArray();
                }
                catch { }
            }
        }

        return null;
    }

    private static byte[]? ExtractFallbackZlib(byte[] payload)
    {
        for (int i = 0; i < payload.Length - 2; i++)
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
                    // Ignore and continue search
                }
            }
        }
        return null;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int startIndex = 0)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length || startIndex < 0) return -1;
        int limit = haystack.Length - needle.Length;
        for (int i = startIndex; i <= limit; i++)
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
