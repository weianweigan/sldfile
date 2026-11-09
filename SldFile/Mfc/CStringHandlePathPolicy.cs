using System;
using System.IO;
using System.Text;

namespace SldFile.Mfc;

/// <summary>
/// Path policy for the moCStringHandle_c payload that accompanies contour and
/// region selection blocks.
///
/// Payload layout: `FF FE FF [len]` WChar path (UTF-16LE) followed by an opaque
/// handle tail (sketch id / serial / flags). The WChar stores the source document
/// path. Re-using a captured block in a different document with the original path
/// makes SolidWorks reject the file (Agent 5: OpenDoc6=65536), so a complete
/// block may only be emitted when:
///   - the target document path equals the embedded path (verbatim), or
///   - the embedded path is rewritten to the target document path.
/// If no safe target path is supplied, the writer must reject the emission.
/// </summary>
internal static class CStringHandlePathPolicy
{
    /// <summary>Reads the WChar path and the trailing opaque bytes.</summary>
    internal static bool TryRead(byte[] payload, out string path, out byte[] tail)
    {
        path = string.Empty;
        tail = Array.Empty<byte>();
        if (payload == null || payload.Length < 4) return false;
        if (payload[0] != 0xFF || payload[1] != 0xFE || payload[2] != 0xFF) return false;
        int cursor = 3;
        uint charCount = payload[cursor++];
        if (charCount == byte.MaxValue)
        {
            if (cursor + 2 > payload.Length) return false;
            charCount = BitConverter.ToUInt16(payload, cursor);
            cursor += 2;
            if (charCount == ushort.MaxValue)
            {
                if (cursor + 4 > payload.Length) return false;
                charCount = BitConverter.ToUInt32(payload, cursor);
                cursor += 4;
            }
        }
        if (charCount > (uint)((payload.Length - cursor) / 2)) return false;
        int byteCount = (int)charCount * 2;
        path = Encoding.Unicode.GetString(payload, cursor, byteCount);
        if (path.Length == 0) return false;
        int tailStart = cursor + byteCount;
        tail = new byte[payload.Length - tailStart];
        Array.Copy(payload, tailStart, tail, 0, tail.Length);
        return true;
    }

    /// <summary>Builds a payload with the WChar path replaced; the tail is preserved.</summary>
    internal static byte[] Rewrite(byte[] payload, string targetPath)
    {
        if (!TryRead(payload, out _, out byte[] tail))
            throw new InvalidOperationException("moCStringHandle_c payload has no readable source path.");
        if (!TryNormalizeDocumentPath(targetPath, out string normalized))
            throw new ArgumentException("A valid absolute target document path is required.", nameof(targetPath));
        var chars = Encoding.Unicode.GetBytes(normalized);
        int charCount = chars.Length / 2;
        using var ms = new MemoryStream();
        ms.WriteByte(0xFF);
        ms.WriteByte(0xFE);
        ms.WriteByte(0xFF);
        if (charCount < byte.MaxValue)
        {
            ms.WriteByte((byte)charCount);
        }
        else if (charCount < ushort.MaxValue)
        {
            ms.WriteByte(0xFF);
            ms.WriteByte((byte)charCount);
            ms.WriteByte((byte)(charCount >> 8));
        }
        else
        {
            ms.WriteByte(0xFF);
            ms.WriteByte(0xFF);
            ms.WriteByte(0xFF);
            WriteU32(ms, (uint)charCount);
        }
        ms.Write(chars, 0, chars.Length);
        ms.Write(tail, 0, tail.Length);
        return ms.ToArray();
    }

    /// <summary>Normalizes an absolute document path; rejects null/empty/relative paths.</summary>
    internal static bool TryNormalizeDocumentPath(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path!.IndexOf('\0') >= 0) return false;
        if (!Path.IsPathRooted(path)) return false;
        try
        {
            normalized = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }
        return normalized.Length > 0;
    }

    internal static bool SameDocument(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void WriteU32(Stream stream, uint value)
    {
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }
}
