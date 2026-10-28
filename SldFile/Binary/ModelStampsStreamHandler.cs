using System;
using System.Buffers.Binary;

namespace SldFile.Binary;

internal sealed class ModelStampsStreamReader : BinaryStreamReader<ModelStamps>
{
    public override string StreamName => "ModelStamps";

    public override void Parse(byte[] data, ModelStamps model)
    {
        model.RawData = data;
        if (data.Length >= 12)
        {
            var span = data.AsSpan();
            model.Timestamp1 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(0, 4));
            model.Timestamp2 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(4, 4));
            model.Timestamp3 = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(8, 4));
        }
    }
}
