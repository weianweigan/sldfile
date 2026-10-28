using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class BiographySchemaMapper
{
    public static void Map(byte[] data, BiographyData model)
    {
        model.RawData = data;
        var doc = MfcDocument.Parse(data);

        // Scan all named tags' data regions for WChar strings
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

        // First WChar is Service Pack version, rest are paths
        var paths = new List<string>();
        for (int i = 0; i < allWchars.Count; i++)
        {
            var wc = allWchars[i];
            if (i == 0 && wc.value.Length > 3)
            {
                model.ServicePackVersion = wc.value;
            }
            else if (wc.value.Length > 3)
            {
                paths.Add(wc.value);
            }
        }

        // Parse path entries from raw data starting at the first MFC tag
        int offset = doc.AllElements.Count > 0 ? doc.AllElements[0].TagOffset : 0;
        var pathReader = new MfcReader(data, offset);

        // Skip through data looking for delimiter-based path entries
        // Each entry: u32 delimiter (3), timestamp bytes, WChar path
        while (pathReader.Remaining >= 4)
        {
            uint val = pathReader.ReadU32();
            if (val == 0xFFFFFFFF)
                break;

            var entry = new BiographyPathEntry { Delimiter = val };

            if (val == 3 || val < 100)
            {
                int tsStart = pathReader.Offset;

                // Scan for next delimiter or WChar
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

                model.PathEntriesBuilder.Add(entry);
            }
            else
            {
                pathReader.Seek(pathReader.Offset - 3); // step forward by 1 byte from initial ReadU32
            }
        }

        model.Freeze();
    }
}
