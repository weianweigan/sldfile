using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal static class Config0StreamHandler
{
    public static void Parse(byte[] data, Config0Data model)
    {
        model.RawData = data;
        var doc = MfcDocument.Parse(data);

        // Extract user names from su_CStringArray
        if (doc.FindFirst("su_CStringArray") is { } cStringArray)
        {
            var reader = cStringArray.CreateReader();
            while (reader.HasMore)
            {
                if (reader.IsWCharPrefix)
                {
                    var name = reader.ReadWChar();
                    if (!string.IsNullOrEmpty(name))
                        model.UsersBuilder.Add(name!);
                }
                else if (reader.IsCStringArrayTerminator)
                {
                    reader.SkipCStringArrayTerminator();
                }
                else
                {
                    reader.Skip(1);
                }
            }
        }

        // Extract log entries from WChar strings in moLogs_c children
        if (doc.FindFirst("moLogs_c") is { } logs)
        {
            var reader = logs.CreateReader();
            while (reader.Remaining > 3)
            {
                if (reader.IsWCharPrefix)
                {
                    var action = reader.ReadWChar();
                    if (!string.IsNullOrEmpty(action))
                        model.LogEntriesBuilder.Add(action!);
                }
                else if (reader.IsMfcTag)
                {
                    break;
                }
                else
                {
                    reader.Skip(1);
                }
            }
        }

        // Body data: find the sentinel (FFFFFFFF) that marks the end of the MFC front-end
        int bodyStart = 0;
        var span = data.AsSpan();
        for (int i = 0; i <= data.Length - 4; i++)
        {
            if (span[i] == 0xFF && span[i + 1] == 0xFF &&
                span[i + 2] == 0xFF && span[i + 3] == 0xFF)
            {
                bodyStart = i + 4;
                break;
            }
        }

        if (bodyStart > 0 && bodyStart < data.Length)
        {
            model.BodyData = new byte[data.Length - bodyStart];
            Array.Copy(data, bodyStart, model.BodyData, 0, model.BodyData.Length);
        }
        else if (bodyStart == 0 && data.Length > 0)
        {
            // No sentinel found — use last element's DataEnd as boundary
            int maxEnd = 0;
            foreach (var element in doc.AllElements)
            {
                if (element.DataEnd > maxEnd)
                    maxEnd = element.DataEnd;
            }
            if (maxEnd > 0 && maxEnd < data.Length)
            {
                model.BodyData = new byte[data.Length - maxEnd];
                Array.Copy(data, maxEnd, model.BodyData, 0, model.BodyData.Length);
            }
            else
            {
                model.BodyData = data;
            }
        }

        model.Freeze();
    }

    public static byte[] Serialize(Config0Data model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
