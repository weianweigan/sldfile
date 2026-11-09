using System;
using System.Collections.Generic;
using System.Text;

namespace SldFile.Mfc;

/// <summary>
/// Static utility class for MFC 4.2 CArchive binary format primitives (Visual C++ 6.0 era).
/// All multi-byte values are little-endian. All reader methods advance <paramref name="offset"/>
/// past the bytes consumed.
/// </summary>
internal static class MfcPrimitives
{
    // ═══════════════════════════════════════════════════════════════
    // Marker Detection
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Searches for the first occurrence of an ASCII string in a byte array.
    /// </summary>
    public static int FindAscii(byte[] data, string text, int startOffset = 0)
    {
        if (data == null || string.IsNullOrEmpty(text) || startOffset < 0) return -1;
        byte[] pattern = Encoding.ASCII.GetBytes(text);
        if (data.Length - startOffset < pattern.Length) return -1;
        for (int i = startOffset; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Checks for an MFC class tag header: FF FF {0x00|0x01} 00.
    /// </summary>
    public static bool IsMfcTag(byte[] data, int offset)
    {
        return offset + 4 <= data.Length
            && data[offset] == 0xFF
            && data[offset + 1] == 0xFF
            && (data[offset + 2] == 0x00 || data[offset + 2] == 0x01)
            && data[offset + 3] == 0x00;
    }

    /// <summary>
    /// Checks for the empty-name trailing sentinel: FF FF 00 00 00 00.
    /// </summary>
    public static bool IsEndMarker(byte[] data, int offset)
    {
        return offset + 6 <= data.Length
            && data[offset] == 0xFF
            && data[offset + 1] == 0xFF
            && data[offset + 2] == 0x00
            && data[offset + 3] == 0x00
            && data[offset + 4] == 0x00
            && data[offset + 5] == 0x00;
    }

    /// <summary>
    /// Checks for a WChar string prefix: FF FE FF.
    /// </summary>
    public static bool IsWCharPrefix(byte[] data, int offset)
    {
        return offset + 3 <= data.Length
            && data[offset] == 0xFF
            && data[offset + 1] == 0xFE
            && data[offset + 2] == 0xFF;
    }

    /// <summary>
    /// Checks for the su_CStringArray terminator: 03 80 01 00.
    /// </summary>
    public static bool IsCStringArrayTerminator(byte[] data, int offset)
    {
        return offset + 4 <= data.Length
            && data[offset] == 0x03
            && data[offset + 1] == 0x80
            && data[offset + 2] == 0x01
            && data[offset + 3] == 0x00;
    }

    // ═══════════════════════════════════════════════════════════════
    // MFC Class Tag Reading
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads a complete MFC class tag at the current offset.
    /// Handles the _ct / _cM suffix convention where the trailing 't' (0x74)
    /// or 'M' (0x4D) byte sits outside the name_len field, followed by 0x80.
    /// Returns null if no valid tag is found. Advances <paramref name="offset"/>
    /// past the entire tag header to the first data byte.
    /// </summary>
    public static MfcTag? ReadMfcTag(byte[] data, ref int offset)
    {
        if (!IsMfcTag(data, offset))
            return null;

        int tagOffset = offset;
        byte tagByte = data[offset + 2];
        offset += 4; // skip FF FF XX 00

        if (offset + 2 > data.Length)
            return null;

        ushort nameLen = ReadU16(data, ref offset);

        if (nameLen > 100) // sanity check
            return null;

        int nameStart = offset;
        if (offset + nameLen > data.Length)
            return null;

        // Validate name bytes are printable ASCII to reject false positives.
        for (int i = 0; i < nameLen; i++)
        {
            byte b = data[offset + i];
            if (b < 0x20 || b > 0x7E)
                return null;
        }

        string baseName = nameLen > 0
            ? Encoding.ASCII.GetString(data, offset, nameLen)
            : string.Empty;
        offset += nameLen;

        // Resolve _ct / _cM suffix
        string fullName = baseName;
        int dataStart = offset;

        if (offset + 1 < data.Length && data[offset + 1] == 0x80)
        {
            if (data[offset] == 0x74) // 't'
            {
                fullName = baseName + "t";
                dataStart = offset + 2;
            }
            else if (data[offset] == 0x4D) // 'M'
            {
                fullName = baseName + "M";
                dataStart = offset + 2;
            }
        }

        return new MfcTag
        {
            ClassName = fullName,
            TagByte = tagByte,
            TagOffset = tagOffset,
            DataStart = dataStart
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // WChar String Reading, Scanning & Rewriting
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Reads an MFC WChar (wide character) string. Format:
    /// FF FE FF + archive length + count*2 bytes UTF-16LE.
    /// Length 255 escapes to UInt16; 65535 escapes again to UInt32.
    /// Returns null if no WChar prefix is found. Advances offset.
    /// </summary>
    public static string? ReadWChar(byte[] data, ref int offset)
        => ReadWChar(data, ref offset, data.Length);

    internal static string? ReadWChar(byte[] data, ref int offset, int end)
    {
        int cursor = offset;
        if (end < 0 || end > data.Length || cursor < 0 || cursor > end - 4 || !IsWCharPrefix(data, cursor))
            return null;
        cursor += 3;
        uint charCount = data[cursor++];
        if (charCount == byte.MaxValue)
        {
            if (cursor > end - 2) return null;
            charCount = BitConverter.ToUInt16(data, cursor);
            cursor += 2;
            if (charCount == ushort.MaxValue)
            {
                if (cursor > end - 4) return null;
                charCount = BitConverter.ToUInt32(data, cursor);
                cursor += 4;
            }
        }
        if (charCount > (uint)((end - cursor) / 2)) return null;
        int byteLen = (int)charCount * 2;
        string result = Encoding.Unicode.GetString(data, cursor, byteLen);
        offset = cursor + byteLen;
        return result;
    }

    /// <summary>
    /// Peeks at a WChar string without advancing the offset.
    /// Returns null if no WChar prefix is at the given position.
    /// </summary>
    public static string? PeekWChar(byte[] data, int offset)
    {
        int saved = offset;
        return ReadWChar(data, ref saved);
    }

    /// <summary>
    /// Represents a scanned WChar string slice with its byte range <c>[Offset, EndOffset)</c>.
    /// </summary>
    public sealed class WCharField
    {
        public int Offset { get; set; }
        public int EndOffset { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Encodes a string using the MFC 4.2 WChar wire format (<c>FF FE FF</c> + length prefix + UTF-16LE bytes).
    /// </summary>
    public static byte[] EncodeWChar(string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        var bytes = new List<byte>(4 + value.Length * 2) { 0xFF, 0xFE, 0xFF };
        if (value.Length < 255)
        {
            bytes.Add((byte)value.Length);
        }
        else if (value.Length < ushort.MaxValue)
        {
            bytes.Add(0xFF);
            bytes.AddRange(BitConverter.GetBytes((ushort)value.Length));
        }
        else
        {
            bytes.Add(0xFF);
            bytes.Add(0xFF);
            bytes.AddRange(BitConverter.GetBytes((uint)value.Length));
        }
        bytes.AddRange(Encoding.Unicode.GetBytes(value));
        return bytes.ToArray();
    }

    /// <summary>
    /// Scans <c>[start, end)</c> for non-empty WChar fields matching an optional predicate.
    /// </summary>
    public static List<WCharField> ScanWCharFields(
        byte[] data,
        int start,
        int end,
        int maxStringLength = int.MaxValue,
        Func<string, bool>? predicate = null)
    {
        var result = new List<WCharField>();
        if (data == null) return result;
        int limit = Math.Min(end, data.Length);
        for (int i = Math.Max(0, start); i + 4 <= limit; i++)
        {
            if (!IsWCharPrefix(data, i)) continue;
            int cursor = i;
            string? value = ReadWChar(data, ref cursor, limit);
            if (string.IsNullOrEmpty(value) || value!.Length > maxStringLength)
                continue;
            if (predicate != null && !predicate(value))
                continue;
            result.Add(new WCharField
            {
                Offset = i,
                EndOffset = cursor,
                Value = value
            });
            i = Math.Max(i, cursor - 1);
        }
        return result;
    }

    /// <summary>
    /// Rebuilds <paramref name="template"/> by replacing each field in <paramref name="fields"/>
    /// with the encoded WChar string at the corresponding index in <paramref name="replacements"/>.
    /// </summary>
    public static byte[] RewriteWCharFields(
        byte[] template,
        IReadOnlyList<WCharField> fields,
        IReadOnlyList<string> replacements)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (fields == null) throw new ArgumentNullException(nameof(fields));
        if (replacements == null) throw new ArgumentNullException(nameof(replacements));
        if (fields.Count != replacements.Count)
            throw new InvalidOperationException("Unexpected template WChar field count.");

        var output = new List<byte>(template.Length + 64);
        int position = 0;
        for (int f = 0; f < fields.Count; f++)
        {
            var field = fields[f];
            for (int b = position; b < field.Offset; b++)
                output.Add(template[b]);
            output.AddRange(EncodeWChar(replacements[f]));
            position = field.EndOffset;
        }
        for (int b = position; b < template.Length; b++)
            output.Add(template[b]);
        return output.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════
    // Typed Numeric Readers (little-endian)
    // ═══════════════════════════════════════════════════════════════

    public static byte ReadU8(byte[] data, ref int offset)
    {
        return data[offset++];
    }

    public static ushort ReadU16(byte[] data, ref int offset)
    {
        ushort val = BitConverter.ToUInt16(data, offset);
        offset += 2;
        return val;
    }

    public static uint ReadU32(byte[] data, ref int offset)
    {
        uint val = BitConverter.ToUInt32(data, offset);
        offset += 4;
        return val;
    }

    public static ulong ReadU64(byte[] data, ref int offset)
    {
        ulong val = BitConverter.ToUInt64(data, offset);
        offset += 8;
        return val;
    }

    public static float ReadF32(byte[] data, ref int offset)
    {
        float val = BitConverter.ToSingle(data, offset);
        offset += 4;
        return val;
    }

    public static double ReadF64(byte[] data, ref int offset)
    {
        double val = BitConverter.ToDouble(data, offset);
        offset += 8;
        return val;
    }

    /// <summary>
    /// Reads a 16-byte GUID and returns it as an 8-4-4-4-12 hex string.
    /// </summary>
    public static string ReadGuid(byte[] data, ref int offset)
    {
        uint a = ReadU32(data, ref offset);
        ushort b = ReadU16(data, ref offset);
        ushort c = ReadU16(data, ref offset);
        byte[] d = ReadBytes(data, ref offset, 8);

        return $"{a:X8}-{b:X4}-{c:X4}-{d[0]:X2}{d[1]:X2}-{d[2]:X2}{d[3]:X2}{d[4]:X2}{d[5]:X2}{d[6]:X2}{d[7]:X2}";
    }

    public static bool TryReadU16(byte[] data, ref int offset, out ushort value)
    {
        value = 0;
        if (offset + 2 > data.Length) return false;
        value = BitConverter.ToUInt16(data, offset);
        offset += 2;
        return true;
    }

    public static bool TryReadU32(byte[] data, ref int offset, out uint value)
    {
        value = 0;
        if (offset + 4 > data.Length) return false;
        value = BitConverter.ToUInt32(data, offset);
        offset += 4;
        return true;
    }

    public static bool TryReadU64(byte[] data, ref int offset, out ulong value)
    {
        value = 0;
        if (offset + 8 > data.Length) return false;
        value = BitConverter.ToUInt64(data, offset);
        offset += 8;
        return true;
    }

    public static bool TryReadF32(byte[] data, ref int offset, out float value)
    {
        value = 0;
        if (offset + 4 > data.Length) return false;
        value = BitConverter.ToSingle(data, offset);
        offset += 4;
        return true;
    }

    public static bool TryReadF64(byte[] data, ref int offset, out double value)
    {
        value = 0;
        if (offset + 8 > data.Length) return false;
        value = BitConverter.ToDouble(data, offset);
        offset += 8;
        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    // Scanning
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Phase-1 scan: walks the entire byte array to locate every MFC class tag
    /// and computes their data regions. End-marker sentinels (empty name tags)
    /// are excluded. Nesting (Parent/Children/Depth) is NOT computed by this
    /// method — stream-specific parsers determine nesting based on class semantics.
    /// </summary>
    public static List<MfcTag> ScanMfcTags(byte[] data)
    {
        var allTags = new List<MfcTag>();
        int pos = 0;

        // Pass 1: find all tag positions
        while (pos < data.Length - 6)
        {
            if (IsEndMarker(data, pos))
            {
                pos += 6;
                continue;
            }

            if (IsMfcTag(data, pos))
            {
                int saved = pos;
                var tag = ReadMfcTag(data, ref pos);
                if (tag != null)
                {
                    allTags.Add(tag);
                }
                else
                {
                    pos = saved + 1;
                }
            }
            else
            {
                pos++;
            }
        }

        // Pass 2: compute DataEnd for each tag as the next tag's TagOffset (or EOF)
        for (int i = 0; i < allTags.Count; i++)
        {
            allTags[i].DataEnd = (i + 1 < allTags.Count)
                ? allTags[i + 1].TagOffset
                : data.Length;
        }

        return allTags;
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Advances offset past the CStringArray terminator (03 80 01 00) if present,
    /// and past an immediately following empty WChar string.
    /// </summary>
    public static void SkipCStringArrayTerminator(byte[] data, ref int offset)
    {
        if (IsCStringArrayTerminator(data, offset))
        {
            offset += 4;
        }

        // Also skip trailing empty WChar
        if (IsWCharPrefix(data, offset))
        {
            ReadWChar(data, ref offset);
        }
    }

    /// <summary>
    /// Converts an OLE Automation date (double, days since 1899-12-30)
    /// to a DateTime. Returns null for out-of-range values.
    /// </summary>
    public static DateTime? OleDateToDateTime(double oleDate)
    {
        try
        {
            return DateTime.FromOADate(oleDate);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Classifies the file type from the stream_version u16 prefix.
    /// 0x000F = Part (.SLDPRT), 0x000B = Assembly (.SLDASM).
    /// </summary>
    public static string ClassifyStreamType(ushort streamVersion)
    {
        return streamVersion switch
        {
            0x000F => "Part (.SLDPRT)",
            0x000B => "Assembly (.SLDASM)",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Reads <paramref name="count"/> raw bytes from data, advancing offset.
    /// Returns a copy of the bytes.
    /// </summary>
    public static byte[] ReadBytes(byte[] data, ref int offset, int count)
    {
        if (count <= 0) return Array.Empty<byte>();
        if (offset + count > data.Length)
            count = data.Length - offset;

        var result = new byte[count];
        Array.Copy(data, offset, result, 0, count);
        offset += count;
        return result;
    }

    /// <summary>
    /// Returns a copy of the byte range [start, end).
    /// </summary>
    public static byte[] CopyRange(byte[] data, int start, int end)
    {
        if (start >= end) return Array.Empty<byte>();
        int len = Math.Min(end - start, data.Length - start);
        var result = new byte[len];
        Array.Copy(data, start, result, 0, len);
        return result;
    }
}
