namespace SldFile.Binary;

internal static class ViewOrientationStreamHandler
{
    public static void Parse(byte[] data, ViewOrientationData model)
    {
        model.RawData = data;
        model.HasData = data != null && data.Length > 0;
    }

    public static byte[] Serialize(ViewOrientationData model)
    {
        if (model.RawData != null && model.RawData.Length > 0)
            return model.RawData;
        return System.Array.Empty<byte>();
    }
}
