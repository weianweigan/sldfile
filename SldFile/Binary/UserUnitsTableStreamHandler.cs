namespace SldFile.Binary;

internal static class UserUnitsTableStreamHandler
{
    public static void Parse(byte[] data, UserUnitsTableData model)
    {
        UserUnitsTableSchemaMapper.Map(data, model);
    }

    public static byte[] Serialize(UserUnitsTableData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return System.Array.Empty<byte>();
    }
}
