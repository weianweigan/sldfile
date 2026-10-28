using System;
using System.Collections.Generic;
using System.IO;

namespace SldFile.Binary;

/// <summary>Reads indexed entries in the section archives observed in the 2026 fixtures.</summary>
internal sealed class SectionContainerReader
{
    private readonly byte[] _data;
    public SectionContainerReader(byte[] data) { _data = data ?? throw new ArgumentNullException(nameof(data)); }
    public byte Key => _data.Length > 7 ? _data[7] : (byte)0;

    public static bool IsSectionContainer(byte[] header)
    {
        if (header == null || header.Length < 8) return false;
        if (header[0] == 0xd0 && header[1] == 0xcf && header[2] == 0x11 && header[3] == 0xe0) return false;
        return header[4] == 0 && header[5] == 0 && header[6] == 0 && header[7] == 4;
    }

    public Dictionary<string, byte[]> ExtractAllStreams()
    {
        var directory = SectionArchiveDirectory.Read(_data, Key);
        var streams = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in directory.Entries)
        {
            if (streams.ContainsKey(entry.Name)) throw new InvalidDataException("Duplicate indexed stream name.");
            streams.Add(entry.Name, directory.Extract(_data, entry));
        }
        return streams;
    }

    internal static byte Rol(byte value, int shift)
    {
        shift &= 7;
        return (byte)((value << shift) | (value >> (8 - shift)));
    }
}
