using System;

namespace SldFile.Binary;

internal static class MatesListStreamHandler
{
    public static void Parse(byte[] data, MatesListData model)
    {
        MatesListSchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(MatesListData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
