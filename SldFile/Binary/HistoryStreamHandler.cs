using System;

namespace SldFile.Binary;

internal static class HistoryStreamHandler
{
    public static void Parse(byte[] data, HistoryData model)
    {
        HistorySchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(HistoryData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
