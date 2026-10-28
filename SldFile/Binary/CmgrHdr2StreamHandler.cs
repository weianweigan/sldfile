using System;

namespace SldFile.Binary;

internal static class CmgrHdr2StreamHandler
{
    public static void Parse(byte[] data, CmgrHdr2Data model)
    {
        CmgrHdr2SchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(CmgrHdr2Data model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
