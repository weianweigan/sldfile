using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for _MO_VERSION_*/History binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes moVersionHistory_c and moDateCodeHistory_c entries.
/// </summary>
internal sealed class HistoryModel : IMfcSerializable
{
    public const string VersionHistoryClassName = "moVersionHistory_c";
    public const string DateCodeHistoryClassName = "moDateCodeHistory_c";
    private const uint EndMarker = 0x03800100;

    public uint VersionCount { get; set; }
    public uint HeaderTimestamp { get; set; }
    public uint PostWcharValue { get; set; }
    public List<HistoryVersionEntry> Versions { get; } = new();

    public byte[]? RawData { get; set; }

    public HistoryModel() { }



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
            ushort histTag = 0;
            ar.WriteClassTag(VersionHistoryClassName, ref histTag, schema: 1);

            uint count = (uint)Versions.Count;
            ar.U32(ref count);
            uint zero = 0;
            ar.U32(ref zero);
            ushort pad = 0;
            ar.U16(ref pad);
            uint ts = HeaderTimestamp;
            ar.U32(ref ts);

            // Optional empty wchar
            string? emptyWchar = string.Empty;
            ar.WChar(ref emptyWchar);

            uint postVal = PostWcharValue;
            ar.U32(ref postVal);

            // Entries
            bool first = true;
            ushort dateCodeTag = 0;
            foreach (var entry in Versions)
            {
                if (first || entry.IsTagged)
                {
                    ar.WriteClassTag(DateCodeHistoryClassName, ref dateCodeTag, schema: 1);
                    first = false;
                }

                uint fc = entry.FieldCount;
                ar.U32(ref fc);
                for (int i = 0; i < entry.DateCodes.Length; i++)
                {
                    uint dc = entry.DateCodes[i];
                    ar.U32(ref dc);
                }

                ushort tag = entry.Tag;
                ar.U16(ref tag);
                ushort flags = entry.Flags;
                ar.U16(ref flags);

                uint postTag = entry.PostTagValue;
                ar.U32(ref postTag);

                string? ew = string.Empty;
                ar.WChar(ref ew);

                uint counter = entry.Counter;
                ar.U32(ref counter);
            }
        }
        else
        {
            Versions.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length < 24) return;

            var doc = MfcDocument.Parse(data);
            var root = doc.FindFirst(VersionHistoryClassName);
            int offset = root?.DataStart ?? 0;

            if (offset == 0 && root == null)
            {
                for (int i = 0; i < data.Length - 24; i++)
                {
                    if (MfcPrimitives.IsMfcTag(data, i))
                    {
                        int p = i;
                        var tag = MfcPrimitives.ReadMfcTag(data, ref p);
                        if (tag?.ClassName == VersionHistoryClassName)
                        {
                            offset = tag.DataStart;
                            break;
                        }
                    }
                }
            }

            var reader = new MfcReader(data, offset);
            VersionCount = reader.ReadU32();
            reader.Skip(4); // zero
            reader.Skip(2); // pad
            HeaderTimestamp = reader.ReadU32();

            if (reader.IsWCharPrefix)
                reader.ReadWChar();

            PostWcharValue = reader.ReadU32();

            var taggedRecord = doc.FindFirst(DateCodeHistoryClassName);
            if (taggedRecord != null)
            {
                var recordReader = taggedRecord.CreateReader();
                var entry = ParseRecord(recordReader);
                if (entry != null)
                {
                    entry.IsTagged = true;
                    Versions.Add(entry);
                    reader.Seek(taggedRecord.DataEnd);
                }
            }

            uint count = taggedRecord != null ? 1u : 0u;
            while (count < VersionCount && reader.Remaining >= 8)
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
                    Versions.Add(entry);
                    count++;
                }
                else
                {
                    break;
                }
            }
        }
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

        if (reader.Remaining >= 4 && BitConverter.ToUInt32(reader.Data, reader.Offset) == EndMarker)
            reader.Skip(4);

        return entry;
    }
}

internal sealed class HistoryVersionEntry
{
    public uint FieldCount { get; set; }
    public uint[] DateCodes { get; set; } = Array.Empty<uint>();
    public ushort Tag { get; set; }
    public ushort Flags { get; set; }
    public uint PostTagValue { get; set; }
    public uint Counter { get; set; }
    public bool IsTagged { get; set; }
}