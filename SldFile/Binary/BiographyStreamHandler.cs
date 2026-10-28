using System;

namespace SldFile.Binary;

internal static class BiographyStreamHandler
{
    public static void Parse(byte[] data, BiographyData model)
    {
        BiographySchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(BiographyData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
