using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SldFile.Binary;

/// <summary>Updates indexed ZIP-shaped records, preserving opaque records and gaps.</summary>
internal class SectionContainerWriter
{
    private readonly byte[] _originalData;
    private readonly byte _key;
    public SectionContainerWriter(byte[] originalData, byte key)
    {
        _originalData = originalData ?? throw new ArgumentNullException(nameof(originalData));
        _key = key;
    }

    public byte[] Write(Dictionary<string, byte[]> updatedStreams)
    {
        var directory = SectionArchiveDirectory.Read(_originalData, _key);
        if (updatedStreams.Keys.Any(name => !directory.Entries.Any(e => e.Name == name)))
            throw new NotSupportedException("Adding archive streams is not supported.");
        using var output = new MemoryStream();
        var offsets = new Dictionary<SectionArchiveDirectory.Entry, int>();
        var values = new Dictionary<SectionArchiveDirectory.Entry, (uint Crc, int Compressed, int Size)>();
        int cursor = 0;
        foreach (var entry in directory.Entries.OrderBy(e => e.LocalOffset))
        {
            output.Write(_originalData, cursor, entry.LocalOffset - cursor);
            offsets.Add(entry, checked((int)output.Position));
            int originalEnd = entry.LocalOffset + entry.HeaderLength + entry.CompressedSize;
            if (updatedStreams.TryGetValue(entry.Name, out var data))
            {
                directory.Extract(_originalData, entry);
                byte[] stored = data;
                if (entry.Method == 8)
                {
                    using var compressed = new MemoryStream();
                    using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, true)) deflate.Write(data, 0, data.Length);
                    stored = compressed.ToArray();
                }
                var header = new byte[entry.HeaderLength];
                Array.Copy(_originalData, entry.LocalOffset, header, 0, header.Length);
                uint crc = SectionArchiveDirectory.Crc32(data);
                SectionArchiveDirectory.Put32(header, 14, crc);
                SectionArchiveDirectory.Put32(header, 18, stored.Length);
                SectionArchiveDirectory.Put32(header, 22, data.Length);
                output.Write(header, 0, header.Length);
                output.Write(stored, 0, stored.Length);
                values.Add(entry, (crc, stored.Length, data.Length));
            }
            else output.Write(_originalData, entry.LocalOffset, originalEnd - entry.LocalOffset);
            cursor = originalEnd;
        }
        output.Write(_originalData, cursor, directory.DirectoryOffset - cursor);
        int newDirectoryOffset = checked((int)output.Position);
        var tail = new byte[_originalData.Length - directory.DirectoryOffset];
        Array.Copy(_originalData, directory.DirectoryOffset, tail, 0, tail.Length);
        foreach (var entry in directory.Entries)
        {
            int relative = entry.CentralOffset - directory.DirectoryOffset;
            SectionArchiveDirectory.Put32(tail, relative + 42, offsets[entry] - 8);
            if (values.TryGetValue(entry, out var value))
            {
                SectionArchiveDirectory.Put32(tail, relative + 16, value.Crc);
                SectionArchiveDirectory.Put32(tail, relative + 20, value.Compressed);
                SectionArchiveDirectory.Put32(tail, relative + 24, value.Size);
            }
        }
        SectionArchiveDirectory.Put32(tail, directory.EndOffset - directory.DirectoryOffset + 16, newDirectoryOffset - 8);
        output.Write(tail, 0, tail.Length);
        return output.ToArray();
    }
}
