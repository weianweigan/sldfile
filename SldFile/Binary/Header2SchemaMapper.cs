using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Maps a generic MfcDocument tree to the typed Header2Data model.
/// </summary>
internal static class Header2SchemaMapper
{
    public static void Map(MfcDocument doc, Header2Data model)
    {
        model.RawData = doc.SourceData;
        model.UsersBuilder.Clear();
        model.LogEntriesBuilder.Clear();
        model.ExternalReferencesBuilder.Clear();

        var csa = doc.FindFirstDescendant("su_CStringArray");
        if (csa != null)
            ParseCStringArray(csa, model);

        var obList = doc.FindFirstDescendant("suObList");
        if (obList != null)
            model.ObjectCount = obList.AsU16();

        var logs = doc.FindFirstDescendant("moLogs_c");
        if (logs != null)
            ParseLogs(logs, model, doc);

        foreach (var ext in doc.FindAllDescendants("moExtObject_c"))
            ParseFileRefBlock(ext, model, doc);

        model.Freeze();
    }

    // ── su_CStringArray ──────────────────────────────────────────

    private static void ParseCStringArray(MfcElement elem, Header2Data model)
    {
        byte[] data = elem.RawData;
        int offset = 0;

        if (!MfcPrimitives.TryReadU16(data, ref offset, out ushort count))
            return;

        for (int i = 0; i < count; i++)
        {
            var str = MfcPrimitives.ReadWChar(data, ref offset);
            if (str != null)
                model.UsersBuilder.Add(str);
            else
                break;
        }

        MfcPrimitives.SkipCStringArrayTerminator(data, ref offset);
    }

    // ── moLogs_c ─────────────────────────────────────────────────

    private static void ParseLogs(MfcElement logElem, Header2Data model, MfcDocument doc)
    {
        // moLogs_c.RawData includes version u16 (2 bytes) + nested moStamp_c tag + all records.
        // The nested moStamp_c contains the base_timestamp and the log records.
        byte[] data = logElem.RawData;
        int offset = 0;

        // Version u16
        if (offset + 2 > data.Length) return;
        offset += 2; // skip version

        // Find and parse nested moStamp_c to get base_timestamp
        var stampElem = doc.FindChildOf(logElem, "moStamp_c");
        if (stampElem != null)
        {
            byte[] stampData = stampElem.RawData;
            int stampOff = 0;

            // Skip zero padding
            while (stampOff + 4 <= stampData.Length
                   && BitConverter.ToUInt32(stampData, stampOff) == 0)
                stampOff += 4;
            while (stampOff + 2 <= stampData.Length
                   && BitConverter.ToUInt16(stampData, stampOff) == 0)
                stampOff += 2;

            if (stampOff + 4 <= stampData.Length)
            {
                model.BaseTimestamp = BitConverter.ToUInt32(stampData, stampOff);
                stampOff += 4;
            }
        }

        // Advance offset past the moStamp_c MFC tag header within logElem.RawData
        // moStamp_c tag starts at offset 2 (right after version u16)
        if (offset < data.Length && MfcPrimitives.IsMfcTag(data, offset))
        {
            var tag = MfcPrimitives.ReadMfcTag(data, ref offset);
            if (tag != null)
            {
                // offset now points to stamp data start (zeros + base_ts)
                // Skip zeros and base_timestamp
                while (offset + 4 <= data.Length
                       && BitConverter.ToUInt32(data, offset) == 0)
                    offset += 4;
                while (offset + 2 <= data.Length
                       && BitConverter.ToUInt16(data, offset) == 0)
                    offset += 2;
                if (offset + 4 <= data.Length)
                    offset += 4; // skip base_timestamp u32
            }
        }

        // Now parse log records until next structural MFC tag or end of data
        int dataEnd = data.Length;
        while (offset < dataEnd)
        {
            if (MfcPrimitives.IsMfcTag(data, offset))
                break;

            var record = ParseLogRecord(data, ref offset, model.BaseTimestamp);
            if (record != null)
                model.LogEntriesBuilder.Add(record);
            else
                break;
        }

        // Resolve user names from Handle index into Users array
        foreach (var entry in model.LogEntriesBuilder)
        {
            if (entry.Handle < model.UsersBuilder.Count)
                entry.UserName = model.UsersBuilder[(int)entry.Handle];
        }
    }

    /// <summary>
    /// Parse a single log record using the action-first field order
    /// determined from hex analysis:
    ///
    ///   1. Action (WChar)
    ///   2. [Branch A if next u32 &lt; 10000]:
    ///        record_id (u32) + feature_name (WChar) + composite (u32: tag|flags&lt;&lt;16)
    ///   3. Handle (u64)
    ///   4. [Non-terminal]: Timestamp (u32)
    ///   5. [Branch B, optional]: Value (WChar)
    ///
    /// Terminal detection: composite == base_timestamp → no Timestamp field.
    /// </summary>
    internal static LogEntry? ParseLogRecord(byte[] data, ref int offset, uint baseTimestamp)
    {
        int dataLen = data.Length;

        // 1. Action (WChar) — must be first field
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

        // 2. Branch A detection: peek next u32 as potential record_id
        bool isBranchA = false;
        if (offset + 4 <= dataLen && !MfcPrimitives.IsMfcTag(data, offset))
        {
            uint peekVal = BitConverter.ToUInt32(data, offset);
            if (peekVal < 10000)
            {
                isBranchA = true;
                entry.RecordId = peekVal;
                offset += 4;

                // feature_name (WChar)
                if (MfcPrimitives.IsWCharPrefix(data, offset))
                {
                    entry.FeatureName = MfcPrimitives.ReadWChar(data, ref offset);
                }

                // composite field: tag(u16) | (flags(u16) &lt;&lt; 16)
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

        // Terminal detection: Branch A with composite == base_timestamp
        entry.IsTerminal = isBranchA && entry.CompositeField == baseTimestamp;

        // 3. Handle (u64)
        if (entry.IsTerminal)
        {
            // Terminal record: handle is variable-length, padded to next MFC tag.
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

            // Skip any remaining padding to next tag
            int remaining = dist - handleBytes;
            if (remaining > 0)
                offset += remaining;

            // Terminal records use BaseTimestamp instead of their own Timestamp field
            entry.Timestamp = null;
        }
        else
        {
            // Normal record: u64 handle (lower 32 bits = user index)
            if (offset + 8 <= dataLen)
            {
                ulong fullHandle = BitConverter.ToUInt64(data, offset);
                entry.Handle = (uint)(fullHandle & 0xFFFFFFFF);
                offset += 8;
            }

            // 4. Timestamp (u32)
            if (offset + 4 <= dataLen)
            {
                entry.Timestamp = BitConverter.ToUInt32(data, offset);
                offset += 4;
            }
        }

        // 5. Branch B optional value WChar (not matching action names)
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

    // ── moExtObject_c ────────────────────────────────────────────

    private static void ParseFileRefBlock(MfcElement fileRefElem, Header2Data model, MfcDocument doc)
    {
        var extRef = new ExternalReference();
        byte[] data = fileRefElem.RawData;
        int offset = 0;

        // moCStringHandle_c child (nested) contains only the full-path WChar.
        // Path separator, ShortName, and proprietary meta live in the parent
        // moExtObject_c data after the child element ends.
        var handleChild = doc.FindChildOf(fileRefElem, "moCStringHandle_c");
        if (handleChild != null)
        {
            // Position at child's data start within parent data and read the path.
            // This naturally advances offset past the path WChar.
            offset = handleChild.DataStart - fileRefElem.DataStart;
            extRef.FullPath = MfcPrimitives.ReadWChar(data, ref offset) ?? string.Empty;
        }
        else
        {
            // No child — try reading inline WChar from parent data start
            if (MfcPrimitives.IsWCharPrefix(data, offset))
                extRef.FullPath = MfcPrimitives.ReadWChar(data, ref offset) ?? string.Empty;
        }

        // Path separator: XX 80 (between full path and short name)
        if (offset + 2 <= data.Length && data[offset + 1] == 0x80)
            offset += 2;

        // Short name WChar
        if (MfcPrimitives.IsWCharPrefix(data, offset))
            extRef.ShortName = MfcPrimitives.ReadWChar(data, ref offset) ?? string.Empty;

        // Name separator: 02 00 (between short name and proprietary meta)
        if (offset + 2 <= data.Length && data[offset] == 0x02 && data[offset + 1] == 0x00)
            offset += 2;

        // Proprietary meta: remaining bytes to end of element data
        int metaLen = data.Length - offset;
        if (metaLen > 0)
            extRef.ProprietaryMeta = MfcPrimitives.CopyRange(data, offset, data.Length);

        model.ExternalReferencesBuilder.Add(extRef);
    }
}
