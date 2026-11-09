using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for _MO_VERSION_*/Biography binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes service pack version and biography path history entries.
/// </summary>
internal sealed class BiographyModel : IMfcSerializable
{
    public string ServicePackVersion { get; set; } = string.Empty;
    public List<BiographyPathEntry> PathEntries { get; } = new();

    public byte[]? RawData { get; set; }

    public BiographyModel() { }



    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            // Storing from scratch
            string? sp = ServicePackVersion;
            ar.WChar(ref sp);

            foreach (var entry in PathEntries)
            {
                uint delim = entry.Delimiter;
                ar.U32(ref delim);
                if (entry.TimestampData != null && entry.TimestampData.Length > 0)
                {
                    ar.WriteBytes(entry.TimestampData);
                }
                string? p = entry.PathString;
                ar.WChar(ref p);
            }
        }
        else
        {
            PathEntries.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var doc = MfcDocument.Parse(data);
            var allWchars = new List<(int pos, string value)>();

            foreach (var element in doc.AllElements)
            {
                if (string.IsNullOrEmpty(element.ClassName))
                    continue;

                var reader = element.CreateReader();
                while (reader.Remaining > 3)
                {
                    if (reader.IsWCharPrefix)
                    {
                        var s = reader.ReadWChar();
                        if (s != null)
                            allWchars.Add((reader.Offset, s));
                        continue;
                    }
                    if (reader.IsMfcTag)
                        break;
                    reader.Skip(1);
                }
            }

            var paths = new List<string>();
            for (int i = 0; i < allWchars.Count; i++)
            {
                var wc = allWchars[i];
                if (i == 0 && wc.value.Length > 3)
                {
                    ServicePackVersion = wc.value;
                }
                else if (wc.value.Length > 3)
                {
                    paths.Add(wc.value);
                }
            }

            int offset = doc.AllElements.Count > 0 ? doc.AllElements[0].TagOffset : 0;
            var pathReader = new MfcReader(data, offset);

            while (pathReader.Remaining >= 4)
            {
                uint val = pathReader.ReadU32();
                if (val == 0xFFFFFFFF)
                    break;

                var entry = new BiographyPathEntry { Delimiter = val };

                if (val == 3 || val < 100)
                {
                    int tsStart = pathReader.Offset;

                    while (pathReader.Remaining >= 4)
                    {
                        uint nextVal = BitConverter.ToUInt32(pathReader.Data, pathReader.Offset);
                        if (pathReader.IsWCharPrefix)
                            break;
                        if (nextVal == 0xFFFFFFFF)
                            break;
                        pathReader.Skip(1);
                    }

                    if (pathReader.Offset > tsStart)
                    {
                        entry.TimestampData = new byte[pathReader.Offset - tsStart];
                        Array.Copy(data, tsStart, entry.TimestampData, 0, entry.TimestampData.Length);
                    }

                    if (pathReader.IsWCharPrefix)
                    {
                        entry.PathString = pathReader.ReadWChar() ?? string.Empty;
                    }

                    PathEntries.Add(entry);
                }
                else
                {
                    pathReader.Seek(pathReader.Offset - 3);
                }
            }
        }
    }
}

internal sealed class BiographyPathEntry
{
    public uint Delimiter { get; set; }
    public byte[] TimestampData { get; set; } = Array.Empty<byte>();
    public string PathString { get; set; } = string.Empty;
}