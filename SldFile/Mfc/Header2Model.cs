using System;
using System.Collections.Generic;
using System.Text;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Header2 binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes su_CStringArray, suObList, moLogs_c, and moExtObject_c.
/// </summary>
internal sealed class Header2Model : IMfcSerializable
{
    public const string CStringArrayClassName = "su_CStringArray";
    public const string ObListClassName = "suObList";
    public const string LogsClassName = "moLogs_c";
    public const string StampClassName = "moStamp_c";
    public const string ExtObjectClassName = "moExtObject_c";
    public const string CStringHandleClassName = "moCStringHandle_c";

    public List<string> Users { get; } = new();
    public int ObjectCount { get; set; }
    public uint BaseTimestamp { get; set; }
    public List<LogEntry> LogEntries { get; } = new();
    public List<ExternalReference> ExternalReferences { get; } = new();

    public byte[]? RawData { get; set; }

    public Header2Model() { }



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
            // 1. su_CStringArray
            ushort csaTag = 0;
            ar.WriteClassTag(CStringArrayClassName, ref csaTag, schema: 1);
            ushort uCount = (ushort)Users.Count;
            ar.U16(ref uCount);
            foreach (var user in Users)
            {
                string? u = user;
                ar.WChar(ref u);
            }
            ushort term = 0;
            ar.U16(ref term);
            ar.U16(ref term);

            // 2. suObList
            ushort obTag = 0;
            ar.WriteClassTag(ObListClassName, ref obTag, schema: 1);
            ushort objCount = (ushort)ObjectCount;
            ar.U16(ref objCount);

            // 3. moLogs_c
            ushort logsTag = 0;
            ar.WriteClassTag(LogsClassName, ref logsTag, schema: 1);
            ushort logVer = 1;
            ar.U16(ref logVer);

            ushort stampTag = 0;
            ar.WriteClassTag(StampClassName, ref stampTag, schema: 1);
            uint bts = BaseTimestamp;
            ar.U32(ref bts);

            foreach (var entry in LogEntries)
            {
                string? action = entry.Action;
                ar.WChar(ref action);

                if (entry.RecordId.HasValue)
                {
                    uint rid = entry.RecordId.Value;
                    ar.U32(ref rid);

                    string? fname = entry.FeatureName ?? string.Empty;
                    ar.WChar(ref fname);

                    uint comp = entry.CompositeField;
                    ar.U32(ref comp);
                }

                ulong handle = entry.Handle;
                ar.U64(ref handle);

                if (!entry.IsTerminal && entry.Timestamp.HasValue)
                {
                    uint ts = entry.Timestamp.Value;
                    ar.U32(ref ts);
                }
            }

            // 4. moExtObject_c
            foreach (var ext in ExternalReferences)
            {
                ushort extTag = 0;
                ar.WriteClassTag(ExtObjectClassName, ref extTag, schema: 1);

                ushort handleTag = 0;
                ar.WriteClassTag(CStringHandleClassName, ref handleTag, schema: 1);

                string? path = ext.FullPath;
                ar.WChar(ref path);

                ushort sep = 0x8017;
                ar.U16(ref sep);

                string? sname = ext.ShortName;
                ar.WChar(ref sname);

                ushort nsep = 0x0002;
                ar.U16(ref nsep);

                if (ext.ProprietaryMeta != null && ext.ProprietaryMeta.Length > 0)
                {
                    ar.WriteBytes(ext.ProprietaryMeta);
                }
            }
        }
        else
        {
            Users.Clear();
            LogEntries.Clear();
            ExternalReferences.Clear();

            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var doc = MfcDocument.Parse(data);

            var csa = doc.FindFirstDescendant(CStringArrayClassName);
            if (csa != null)
                ParseCStringArray(csa);

            var obList = doc.FindFirstDescendant(ObListClassName);
            if (obList != null)
                ObjectCount = obList.AsU16();

            var logs = doc.FindFirstDescendant(LogsClassName);
            if (logs != null)
                ParseLogs(logs, doc);

            foreach (var ext in doc.FindAllDescendants(ExtObjectClassName))
                ParseFileRefBlock(ext, doc);
        }
    }

    private void ParseCStringArray(MfcElement elem)
    {
        byte[] data = elem.RawData;
        int offset = 0;

        if (!MfcPrimitives.TryReadU16(data, ref offset, out ushort count))
            return;

        for (int i = 0; i < count; i++)
        {
            var str = MfcPrimitives.ReadWChar(data, ref offset);
            if (str != null)
                Users.Add(str);
            else
                break;
        }
    }

    private void ParseLogs(MfcElement logElem, MfcDocument doc)
    {
        byte[] data = logElem.RawData;
        int offset = 0;

        if (offset + 2 > data.Length) return;
        offset += 2; // skip version u16

        var stampElem = doc.FindChildOf(logElem, StampClassName);
        if (stampElem != null)
        {
            byte[] stampData = stampElem.RawData;
            int stampOff = 0;

            while (stampOff + 4 <= stampData.Length && BitConverter.ToUInt32(stampData, stampOff) == 0)
                stampOff += 4;
            while (stampOff + 2 <= stampData.Length && BitConverter.ToUInt16(stampData, stampOff) == 0)
                stampOff += 2;

            if (stampOff + 4 <= stampData.Length)
            {
                BaseTimestamp = BitConverter.ToUInt32(stampData, stampOff);
                stampOff += 4;
            }
        }

        if (offset < data.Length && MfcPrimitives.IsMfcTag(data, offset))
        {
            var tag = MfcPrimitives.ReadMfcTag(data, ref offset);
            if (tag != null)
            {
                while (offset + 4 <= data.Length && BitConverter.ToUInt32(data, offset) == 0)
                    offset += 4;
                while (offset + 2 <= data.Length && BitConverter.ToUInt16(data, offset) == 0)
                    offset += 2;
                if (offset + 4 <= data.Length)
                    offset += 4;
            }
        }

        int dataEnd = data.Length;
        while (offset < dataEnd)
        {
            if (MfcPrimitives.IsMfcTag(data, offset))
                break;

            var record = ParseLogRecord(data, ref offset, BaseTimestamp);
            if (record != null)
                LogEntries.Add(record);
            else
                break;
        }

        foreach (var entry in LogEntries)
        {
            if (entry.Handle < Users.Count)
                entry.UserName = Users[(int)entry.Handle];
        }
    }

    private static LogEntry? ParseLogRecord(byte[] data, ref int offset, uint baseTimestamp)
    {
        int dataLen = data.Length;

        if (!MfcPrimitives.IsWCharPrefix(data, offset))
            return null;

        int savedOffset = offset;
        var action = MfcPrimitives.ReadWChar(data, ref offset);
        if (action == null || (action != "Created" && action != "Modified" && action != "Deleted"))
        {
            offset = savedOffset;
            return null;
        }

        var entry = new LogEntry
        {
            Action = action,
            EventType = action switch
            {
                "Created" => LogEventType.Created,
                "Modified" => LogEventType.Modified,
                "Deleted" => LogEventType.Deleted,
                _ => (LogEventType)(-1)
            }
        };

        bool isBranchA = false;
        if (offset + 4 <= dataLen && !MfcPrimitives.IsMfcTag(data, offset))
        {
            uint peekVal = BitConverter.ToUInt32(data, offset);
            if (peekVal < 10000)
            {
                isBranchA = true;
                entry.RecordId = peekVal;
                offset += 4;

                if (MfcPrimitives.IsWCharPrefix(data, offset))
                {
                    entry.FeatureName = MfcPrimitives.ReadWChar(data, ref offset);
                }

                if (offset + 4 <= dataLen)
                {
                    uint composite = BitConverter.ToUInt32(data, offset);
                    entry.CompositeField = composite;
                    entry.Tag = (ushort)(composite & 0xFFFF);
                    entry.Flags = (ushort)((composite >> 16) & 0xFFFF);
                    offset += 4;
                }
            }
        }

        entry.IsTerminal = isBranchA && entry.CompositeField == baseTimestamp;

        if (entry.IsTerminal)
        {
            int dist = 20;
            for (int k = 0; k < 20; k++)
            {
                if (offset + k + 5 <= dataLen && MfcPrimitives.IsMfcTag(data, offset + k))
                {
                    dist = k;
                    break;
                }
            }

            int handleBytes = Math.Min(8, dist);
            if (handleBytes >= 8 && offset + 8 <= dataLen)
            {
                ulong fullHandle = BitConverter.ToUInt64(data, offset);
                entry.Handle = (uint)(fullHandle & 0xFFFFFFFF);
                offset += 8;
            }
            else if (handleBytes >= 4 && offset + 4 <= dataLen)
            {
                entry.Handle = BitConverter.ToUInt32(data, offset);
                offset += 4;
            }

            int remaining = dist - handleBytes;
            if (remaining > 0)
                offset += remaining;

            entry.Timestamp = null;
        }
        else
        {
            if (offset + 8 <= dataLen)
            {
                ulong fullHandle = BitConverter.ToUInt64(data, offset);
                entry.Handle = (uint)(fullHandle & 0xFFFFFFFF);
                offset += 8;
            }

            if (offset + 4 <= dataLen)
            {
                entry.Timestamp = BitConverter.ToUInt32(data, offset);
                offset += 4;
            }
        }

        if (!isBranchA && MfcPrimitives.IsWCharPrefix(data, offset))
        {
            int peekSave = offset;
            var peekVal = MfcPrimitives.ReadWChar(data, ref peekSave);
            if (peekVal != null && peekVal != "Created" && peekVal != "Modified" && peekVal != "Deleted")
            {
                entry.Value = MfcPrimitives.ReadWChar(data, ref offset);
            }
        }

        return entry;
    }

    private void ParseFileRefBlock(MfcElement fileRefElem, MfcDocument doc)
    {
        var extRef = new ExternalReference();
        byte[] data = fileRefElem.RawData;
        int offset = 0;

        var handleChild = doc.FindChildOf(fileRefElem, CStringHandleClassName);
        if (handleChild != null)
        {
            offset = handleChild.DataStart - fileRefElem.DataStart;
            extRef.FullPath = MfcPrimitives.ReadWChar(data, ref offset) ?? string.Empty;
        }
        else
        {
            if (MfcPrimitives.IsWCharPrefix(data, offset))
                extRef.FullPath = MfcPrimitives.ReadWChar(data, ref offset) ?? string.Empty;
        }

        if (offset + 2 <= data.Length && data[offset + 1] == 0x80)
            offset += 2;

        if (MfcPrimitives.IsWCharPrefix(data, offset))
            extRef.ShortName = MfcPrimitives.ReadWChar(data, ref offset) ?? string.Empty;

        if (offset + 2 <= data.Length && data[offset] == 0x02 && data[offset + 1] == 0x00)
            offset += 2;

        int metaLen = data.Length - offset;
        if (metaLen > 0)
            extRef.ProprietaryMeta = MfcPrimitives.CopyRange(data, offset, data.Length);

        ExternalReferences.Add(extRef);
    }
}

internal enum LogEventType
{
    Created,
    Modified,
    Deleted
}

internal sealed class LogEntry
{
    public LogEventType EventType { get; set; }
    public string Action { get; set; } = string.Empty;
    public uint? RecordId { get; set; }
    public string? FeatureName { get; set; }
    public ushort? Tag { get; set; }
    public ushort? Flags { get; set; }
    public uint CompositeField { get; set; }
    public uint Handle { get; set; }
    public string? UserName { get; set; }
    public uint? Timestamp { get; set; }
    public bool IsTerminal { get; set; }
    public string? Value { get; set; }
}

internal sealed class ExternalReference
{
    public string FullPath { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public byte[] ProprietaryMeta { get; set; } = Array.Empty<byte>();
}