namespace SldFile.Binary;

internal static class DisplayListsStreamHandler
{
    public static void Parse(byte[] data, DisplayListsData model)
    {
        DisplayListsSchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(DisplayListsData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return System.Array.Empty<byte>();
    }
}
