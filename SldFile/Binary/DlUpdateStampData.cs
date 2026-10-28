using System;

namespace SldFile.Binary;

/// <summary>
/// DLUpdateStamp stream (6 bytes): type flag + zero padding.
/// Located at _DL_VERSION_5000/DLUpdateStamp.
/// </summary>
internal sealed class DlUpdateStampData
{
    /// <summary>Type flag identifying the document variant.</summary>
    public uint TypeFlag { get; set; }

    /// <summary>Always zero.</summary>
    public ushort ZeroPad { get; set; }

    internal byte[]? RawData;

    internal static DlUpdateStampData Parse(byte[] data)
    {
        var model = new DlUpdateStampData { RawData = data };
        if (data.Length >= 4)
            model.TypeFlag = BitConverter.ToUInt32(data, 0);
        if (data.Length >= 6)
            model.ZeroPad = BitConverter.ToUInt16(data, 4);
        return model;
    }

    internal byte[] Serialize()
    {
        var buf = new byte[6];
        BitConverter.GetBytes(TypeFlag).CopyTo(buf, 0);
        BitConverter.GetBytes(ZeroPad).CopyTo(buf, 4);
        return buf;
    }
}
