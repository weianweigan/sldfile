using System;

namespace SldFile.Binary;

internal static class LwDataStreamHandler
{
    public static void Parse(byte[] data, LwData model)
    {
        LwDataSchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(LwData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
