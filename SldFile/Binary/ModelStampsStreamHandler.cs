using System;
using System.Buffers.Binary;

namespace SldFile.Binary;

internal static class ModelStampsStreamHandler
{
    public static void Parse(byte[] data, ModelStamps model)
    {
        model.RawData = data;
        if (data != null && data.Length >= 12)
        {
            var span = data.AsSpan();
            model.Timestamp1 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
            model.Timestamp2 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
            model.Timestamp3 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4));
        }
    }

    public static byte[] Serialize(ModelStamps model)
    {
        if (model.RawData != null && model.RawData.Length > 0 && model.RawData.Length != 12)
            return model.RawData;

        var buf = new byte[12];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0, 4), model.Timestamp1);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), model.Timestamp2);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), model.Timestamp3);
        return buf;
    }
}
