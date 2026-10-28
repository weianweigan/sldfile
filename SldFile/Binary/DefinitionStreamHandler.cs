using System;

namespace SldFile.Binary;

internal static class DefinitionStreamHandler
{
    public static void Parse(byte[] data, DefinitionData model)
    {
        DefinitionSchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(DefinitionData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return Array.Empty<byte>();
    }
}
