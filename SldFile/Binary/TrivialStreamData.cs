using System;
using System.Collections.Generic;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary;

// ── VisualStates_DisplayPane (8 bytes) ──────────────────────────────

/// <summary>
/// VisualStates_DisplayPane stream (8 bytes): display value + zero padding.
/// Located at ThirdPtyStore/VisualStates_DisplayPane.
/// </summary>
internal sealed class VisualStatesDisplayPaneData
{
    public uint DisplayValue { get; set; }
    public uint ZeroPad { get; set; }

    internal byte[]? RawData;

    internal static VisualStatesDisplayPaneData Parse(byte[] data)
    {
        var model = new VisualStatesDisplayPaneData { RawData = data };
        if (data.Length >= 4)
            model.DisplayValue = BitConverter.ToUInt32(data, 0);
        if (data.Length >= 8)
            model.ZeroPad = BitConverter.ToUInt32(data, 4);
        return model;
    }

    internal byte[] Serialize()
    {
        var buf = new byte[8];
        BitConverter.GetBytes(DisplayValue).CopyTo(buf, 0);
        BitConverter.GetBytes(ZeroPad).CopyTo(buf, 4);
        return buf;
    }
}

// ── CMMotionLoadMapU (40 bytes) ─────────────────────────────────────

/// <summary>
/// CMMotionLoadMapU stream (40 bytes): motion load map header.
/// Located at ThirdPty/CMMotionLoadMapU.
/// </summary>
internal sealed class CmMotionLoadMapUData
{
    public uint Version { get; set; }
    public uint FieldCount { get; set; }
    public ulong GuidOrId { get; set; }
    public uint RefValue { get; set; }
    public uint StringLen { get; set; }
    public string Name { get; set; } = string.Empty;

    internal byte[]? RawData;

    internal static CmMotionLoadMapUData Parse(byte[] data)
    {
        var model = new CmMotionLoadMapUData { RawData = data };
        if (data.Length < 40) return model;

        model.Version = BitConverter.ToUInt32(data, 0);
        model.FieldCount = BitConverter.ToUInt32(data, 4);
        model.GuidOrId = BitConverter.ToUInt64(data, 8);
        model.RefValue = BitConverter.ToUInt32(data, 16);
        model.StringLen = BitConverter.ToUInt32(data, 20);
        int len = (int)Math.Min(model.StringLen, 16);
        model.Name = Encoding.ASCII.GetString(data, 24, len).TrimEnd('\0');
        return model;
    }

    internal byte[] Serialize()
    {
        var buf = new byte[40];
        if (RawData != null && RawData.Length == 40)
        {
            Array.Copy(RawData, buf, 40);
        }
        BitConverter.GetBytes(Version).CopyTo(buf, 0);
        BitConverter.GetBytes(FieldCount).CopyTo(buf, 4);
        BitConverter.GetBytes(GuidOrId).CopyTo(buf, 8);
        BitConverter.GetBytes(RefValue).CopyTo(buf, 16);
        BitConverter.GetBytes((uint)Name.Length).CopyTo(buf, 20);
        var nameBytes = Encoding.ASCII.GetBytes(Name);
        Array.Copy(nameBytes, 0, buf, 24, Math.Min(nameBytes.Length, 16));
        return buf;
    }
}

// ── SheetPreviews/SheetNames (variable) ──────────────────────────────

/// <summary>
/// SheetPreviews/SheetNames stream (variable): drawing sheet names.
/// Located at SheetPreviews/SheetNames (Drawing only).
/// </summary>
internal sealed class SheetNamesData
{
    public IReadOnlyList<string> Names { get; internal set; } = Array.Empty<string>();
    internal List<string> NamesBuilder { get; set; } = new();
    internal byte[]? RawData;

    internal void Freeze()
    {
        Names = NamesBuilder.AsReadOnly();
    }

    internal static SheetNamesData Parse(byte[] data)
    {
        var model = new SheetNamesData { RawData = data };
        if (data.Length < 2) return model;

        int offset = 0;
        ushort count = BitConverter.ToUInt16(data, offset);
        offset += 2;

        for (int i = 0; i < count && offset < data.Length; i++)
        {
            if (MfcPrimitives.IsWCharPrefix(data, offset))
            {
                var name = MfcPrimitives.ReadWChar(data, ref offset);
                if (name != null)
                    model.NamesBuilder.Add(name);
            }
            else
            {
                break;
            }
        }

        model.Freeze();
        return model;
    }

    internal byte[] Serialize()
    {
        // Preserve original if no changes
        if (RawData != null)
            return RawData;

        var list = new List<byte>();
        list.AddRange(BitConverter.GetBytes((ushort)NamesBuilder.Count));
        foreach (var name in NamesBuilder)
        {
            // WChar encoding: FF FE FF <count> <UTF-16LE bytes>
            list.Add(0xFF);
            list.Add(0xFE);
            list.Add(0xFF);
            list.Add((byte)name.Length);
            var utf16 = Encoding.Unicode.GetBytes(name);
            list.AddRange(utf16);
        }
        return list.ToArray();
    }
}
