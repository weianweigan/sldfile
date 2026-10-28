using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class HistorySchemaMapper
{
    private const uint EndMarker = 0x03800100;

    public static void Map(byte[] data, HistoryData model)
    {
        model.RawData = data;
        if (data.Length < 24) return;

        var doc = MfcDocument.Parse(data);
        var root = doc.FindFirst("moVersionHistory_c");
        int offset = root?.DataStart ?? 0;

        // Fallback: scan for the first MFC tag if not located via doc
        if (offset == 0 && root == null)
        {
            for (int i = 0; i < data.Length - 24; i++)
            {
                if (MfcPrimitives.IsMfcTag(data, i))
                {
                    int p = i;
                    var tag = MfcPrimitives.ReadMfcTag(data, ref p);
                    if (tag?.ClassName == "moVersionHistory_c")
                    {
                        offset = tag.DataStart;
                        break;
                    }
                }
            }
        }

        var reader = new MfcReader(data, offset);

        // Header: version_count (u32), zero (u32), pad (u16), header_ts (u32)
        model.VersionCount = reader.ReadU32();
        reader.Skip(4); // zero (u32)
        reader.Skip(2); // pad (u16)
        model.HeaderTimestamp = reader.ReadU32();

        // Skip optional empty WChar to find post_wchar_value
        if (reader.IsWCharPrefix)
            reader.ReadWChar();

        model.PostWcharValue = reader.ReadU32();

        // Parse version entries
        var taggedRecord = doc.FindFirst("moDateCodeHistory_c");
        if (taggedRecord != null)
        {
            var recordReader = taggedRecord.CreateReader();
            var entry = ParseRecord(recordReader);
            if (entry != null)
            {
                entry.IsTagged = true;
                model.VersionsBuilder.Add(entry);
                reader.Seek(taggedRecord.DataEnd);
            }
        }

        // Parse untagged continuation records
        uint count = taggedRecord != null ? 1u : 0u;
        while (count < model.VersionCount && reader.Remaining >= 8)
        {
            if (reader.PeekWChar() != null && reader.ReadU32() == 0xFFFFFFFF)
                break;

            int preOffset = reader.Offset;
            if (reader.Remaining >= 4 && BitConverter.ToUInt32(reader.Data, preOffset) == 0xFFFFFFFF)
                break;

            var entry = ParseUntaggedRecord(reader);
            if (entry != null)
            {
                entry.IsTagged = false;
                model.VersionsBuilder.Add(entry);
                count++;
            }
            else
            {
                break;
            }
        }

        model.Freeze();
    }

    private static HistoryVersionEntry? ParseRecord(MfcReader reader)
    {
        if (reader.Remaining < 4) return null;
        var entry = new HistoryVersionEntry();

        entry.FieldCount = reader.ReadU32();
        int fc = (int)Math.Min(entry.FieldCount, 2);
        var dateCodes = new List<uint>();
        for (int i = 0; i < fc && reader.Remaining >= 4; i++)
        {
            dateCodes.Add(reader.ReadU32());
        }
        entry.DateCodes = dateCodes.ToArray();

        if (reader.Remaining >= 4)
        {
            entry.Tag = reader.ReadU16();
            entry.Flags = reader.ReadU16();
        }

        if (reader.Remaining >= 4)
        {
            entry.PostTagValue = reader.ReadU32();
        }

        // Skip empty WCHAR
        if (reader.IsWCharPrefix)
            reader.ReadWChar();

        if (reader.Remaining >= 4)
            entry.Counter = reader.ReadU32();

        return entry;
    }

    private static HistoryVersionEntry? ParseUntaggedRecord(MfcReader reader)
    {
        if (reader.Remaining < 4) return null;

        var entry = new HistoryVersionEntry();
        uint firstVal = BitConverter.ToUInt32(reader.Data, reader.Offset);

        if (firstVal <= 10)
        {
            entry.FieldCount = reader.ReadU32();
        }

        int fc = (int)Math.Min(entry.FieldCount, 2);
        var dateCodes = new List<uint>();
        for (int i = 0; i < fc && reader.Remaining >= 4; i++)
        {
            dateCodes.Add(reader.ReadU32());
        }
        entry.DateCodes = dateCodes.ToArray();

        if (reader.Remaining >= 4)
        {
            entry.Tag = reader.ReadU16();
            entry.Flags = reader.ReadU16();
        }

        if (reader.Remaining >= 4)
        {
            entry.PostTagValue = reader.ReadU32();
        }

        if (reader.IsWCharPrefix)
            reader.ReadWChar();

        if (reader.Remaining >= 4)
            entry.Counter = reader.ReadU32();

        // Skip end marker if present
        if (reader.Remaining >= 4 && BitConverter.ToUInt32(reader.Data, reader.Offset) == EndMarker)
            reader.Skip(4);

        return entry;
    }
}
