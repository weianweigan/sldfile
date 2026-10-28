using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SldFile.Binary;

/// <summary>The ZIP-shaped directory observed behind the eight-byte 2026 wrapper.
/// Signatures and names are transformed; sizes, CRC32 and relative offsets follow ZIP.
/// Unindexed noise records are deliberately retained, never treated as model streams.</summary>
internal sealed class SectionArchiveDirectory
{
    internal int DirectoryOffset { get; private set; }
    internal int EndOffset { get; private set; }
    internal List<Entry> Entries { get; } = new List<Entry>();
    internal sealed class Entry
    {
        internal string Name = "";
        internal int LocalOffset, CentralOffset, HeaderLength, CompressedSize, Size;
        internal ushort Method;
        internal uint Crc;
    }

    internal static SectionArchiveDirectory Read(byte[] data, byte key)
    {
        // EOCD can have trailing obfuscation records. Match the directory relationship,
        // then corroborate every counted central entry against its local header.
        for (int end = data.Length - 22; end >= 8; end--)
        {
            if (U32(data, end + 4) != 0 || U16(data, end + 8) == 0 || U16(data, end + 8) != U16(data, end + 10)) continue;
            uint size = U32(data, end + 12), offset = U32(data, end + 16);
            if ((long)offset + 8 + size != end || offset + 8L >= end) continue;
            var result = new SectionArchiveDirectory { DirectoryOffset = checked((int)offset + 8), EndOffset = end };
            try { result.ReadEntries(data, key, U16(data, end + 10)); return result; }
            catch (InvalidDataException) { }
        }
        throw new InvalidDataException("No validated section archive directory was found.");
    }

    private void ReadEntries(byte[] data, byte key, int count)
    {
        int cursor = DirectoryOffset;
        uint centralSignature = U32(data, cursor);
        uint? localSignature = null;
        for (int i = 0; i < count; i++)
        {
            if (cursor > EndOffset - 46 || U32(data, cursor) != centralSignature) throw new InvalidDataException("Invalid central record.");
            int nameLength = U16(data, cursor + 28), extra = U16(data, cursor + 30), comment = U16(data, cursor + 32);
            int next = cursor + 46 + nameLength + extra + comment;
            if (nameLength == 0 || next > EndOffset || U16(data, cursor + 34) != 0) throw new InvalidDataException("Unsupported central record.");
            uint relative = U32(data, cursor + 42), compressed = U32(data, cursor + 20), size = U32(data, cursor + 24);
            if (relative + 8L > DirectoryOffset - 30 || compressed > int.MaxValue || size > int.MaxValue) throw new InvalidDataException("Invalid local range.");
            int local = checked((int)relative + 8);
            localSignature = localSignature ?? U32(data, local);
            if (U32(data, local) != localSignature || U16(data, local + 4) != 20 || U16(data, local + 6) != U16(data, cursor + 8)
                || U16(data, local + 8) != U16(data, cursor + 10) || U32(data, local + 14) != U32(data, cursor + 16)
                || U32(data, local + 18) != compressed || U32(data, local + 22) != size || U16(data, local + 26) != nameLength)
                throw new InvalidDataException("Local and central headers disagree.");
            int header = 30 + nameLength + U16(data, local + 28);
            if ((long)local + header + compressed > DirectoryOffset) throw new InvalidDataException("Overlapping archive data.");
            var chars = new char[nameLength];
            for (int n = 0; n < nameLength; n++)
            {
                byte encoded = data[cursor + 46 + n];
                if (data[local + 30 + n] != encoded) throw new InvalidDataException("Local and central names disagree.");
                byte decoded = SectionContainerReader.Rol(encoded, key);
                if (decoded < 32 || decoded > 126) throw new InvalidDataException("Unsupported stream name.");
                chars[n] = (char)decoded;
            }
            ushort method = U16(data, local + 8);
            if ((U16(data, local + 6) & 1) != 0) throw new InvalidDataException("Encrypted archive entries are unsupported.");
            if (method != 0 && method != 8) throw new InvalidDataException("Unsupported compression method.");
            Entries.Add(new Entry { Name = new string(chars), LocalOffset = local, CentralOffset = cursor, HeaderLength = header,
                CompressedSize = (int)compressed, Size = (int)size, Crc = U32(data, local + 14), Method = method });
            cursor = next;
        }
        int previousEnd = 8;
        foreach (var entry in Entries.OrderBy(e => e.LocalOffset))
        {
            if (entry.LocalOffset < previousEnd) throw new InvalidDataException("Overlapping local entries.");
            previousEnd = entry.LocalOffset + entry.HeaderLength + entry.CompressedSize;
        }
    }

    internal byte[] Extract(byte[] data, Entry entry)
    {
        using var input = new MemoryStream(data, entry.LocalOffset + entry.HeaderLength, entry.CompressedSize);
        using var output = new MemoryStream();
        if (entry.Method == 0) input.CopyTo(output);
        else
        {
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            var buffer = new byte[8192];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (output.Length + read > entry.Size) throw new InvalidDataException("Decompressed stream exceeds its declared size.");
                output.Write(buffer, 0, read);
            }
        }
        byte[] bytes = output.ToArray();
        if (bytes.Length != entry.Size || Crc32(bytes) != entry.Crc) throw new InvalidDataException("Archive stream size or CRC32 mismatch.");
        return bytes;
    }

    internal static uint Crc32(byte[] bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        return ~crc;
    }
    internal static ushort U16(byte[] bytes, int offset) => (ushort)(bytes[offset] | bytes[offset + 1] << 8);
    internal static uint U32(byte[] bytes, int offset) => (uint)(bytes[offset] | bytes[offset + 1] << 8 | bytes[offset + 2] << 16 | bytes[offset + 3] << 24);
    internal static void Put32(byte[] bytes, int offset, int value) => Put32(bytes, offset, unchecked((uint)value));
    internal static void Put32(byte[] bytes, int offset, uint value)
    {
        for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8));
    }
}
