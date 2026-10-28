using System;

namespace SldFile.Binary;

internal static class CmgrStreamHandler
{
    public static void Parse(byte[] data, CmgrData model)
    {
        CmgrSchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(CmgrData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
