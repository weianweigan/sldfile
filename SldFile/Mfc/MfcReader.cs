using System;
using System.Buffers.Binary;
using System.Text;

namespace SldFile.Mfc;

/// <summary>
/// A forward cursor-based binary reader tailored for MFC 4.2 CArchive streams.
/// Encapsulates bounds checking, little-endian primitive reads, WChar strings, and safe seeking.
/// </summary>
internal sealed class MfcReader
{
    public byte[] Data { get; }
    public int Start { get; }
    public int End { get; }
    public int Offset { get; set; }

    public int Remaining => Math.Max(0, End - Offset);
    public bool HasMore => Remaining > 0;

    public MfcReader(byte[] data, int offset = 0, int length = -1)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Start = Math.Max(0, Math.Min(offset, data.Length));
        int maxLen = data.Length - Start;
        int len = length < 0 ? maxLen : Math.Min(length, maxLen);
        End = Start + len;
        Offset = Start;
    }

    public static MfcReader FromElement(MfcElement element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        return new MfcReader(element.SourceData, element.DataStart, element.DataEnd - element.DataStart);
    }

    public void Skip(int count)
    {
        if (count < 0) return;
        Offset = Math.Min(End, Offset + count);
    }

    public void Seek(int absoluteOffset)
    {
        Offset = Math.Max(Start, Math.Min(End, absoluteOffset));
    }

    public bool CanRead(int count) => count >= 0 && Offset + count <= End;

    #region Primitive Readers (Little Endian)

    public byte ReadU8(byte defaultValue = 0)
    {
        if (Offset >= End) return defaultValue;
        return Data[Offset++];
    }

    public ushort ReadU16(ushort defaultValue = 0)
    {
        if (Offset + 2 > End) return defaultValue;
        ushort val = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(Offset, 2));
        Offset += 2;
        return val;
    }

    public uint ReadU32(uint defaultValue = 0)
    {
        if (Offset + 4 > End) return defaultValue;
        uint val = BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(Offset, 4));
        Offset += 4;
        return val;
    }

    public ulong ReadU64(ulong defaultValue = 0)
    {
        if (Offset + 8 > End) return defaultValue;
        ulong val = BinaryPrimitives.ReadUInt64LittleEndian(Data.AsSpan(Offset, 8));
        Offset += 8;
        return val;
    }

    public float ReadF32(float defaultValue = 0f)
    {
        if (Offset + 4 > End) return defaultValue;
        float val = BitConverter.ToSingle(Data, Offset);
        Offset += 4;
        return val;
    }

    public double ReadF64(double defaultValue = 0.0)
    {
        if (Offset + 8 > End) return defaultValue;
        double val = BitConverter.ToDouble(Data, Offset);
        Offset += 8;
        return val;
    }

    public bool TryReadU16(out ushort value)
    {
        if (Offset + 2 <= End)
        {
            value = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(Offset, 2));
            Offset += 2;
            return true;
        }
        value = 0;
        return false;
    }

    public bool TryReadU32(out uint value)
    {
        if (Offset + 4 <= End)
        {
            value = BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(Offset, 4));
            Offset += 4;
            return true;
        }
        value = 0;
        return false;
    }

    public bool TryReadU64(out ulong value)
    {
        if (Offset + 8 <= End)
        {
            value = BinaryPrimitives.ReadUInt64LittleEndian(Data.AsSpan(Offset, 8));
            Offset += 8;
            return true;
        }
        value = 0;
        return false;
    }

    public bool TryReadF32(out float value)
    {
        if (Offset + 4 <= End)
        {
            value = BitConverter.ToSingle(Data, Offset);
            Offset += 4;
            return true;
        }
        value = 0f;
        return false;
    }

    public bool TryReadF64(out double value)
    {
        if (Offset + 8 <= End)
        {
            value = BitConverter.ToDouble(Data, Offset);
            Offset += 8;
            return true;
        }
        value = 0.0;
        return false;
    }

    public void ReadF64Array(double[] destination)
    {
        if (destination == null) return;
        for (int i = 0; i < destination.Length; i++)
        {
            if (Offset + 8 <= End)
            {
                destination[i] = BitConverter.ToDouble(Data, Offset);
                Offset += 8;
            }
            else
            {
                break;
            }
        }
    }

    #endregion

    #region MFC Primitives & Strings

    public bool IsWCharPrefix => MfcPrimitives.IsWCharPrefix(Data, Offset);

    public bool IsMfcTag => MfcPrimitives.IsMfcTag(Data, Offset);

    public bool IsCStringArrayTerminator => MfcPrimitives.IsCStringArrayTerminator(Data, Offset);

    public string? ReadWChar()
    {
        if (Offset >= End) return null;
        int cur = Offset;
        string? result = MfcPrimitives.ReadWChar(Data, ref cur);
        if (result != null)
        {
            Offset = cur;
        }
        return result;
    }

    public string? PeekWChar() => MfcPrimitives.PeekWChar(Data, Offset);

    public void SkipCStringArrayTerminator()
    {
        int cur = Offset;
        MfcPrimitives.SkipCStringArrayTerminator(Data, ref cur);
        Offset = cur;
    }

    public byte[] ReadBytes(int count)
    {
        if (count <= 0) return Array.Empty<byte>();
        int toRead = Math.Min(count, Remaining);
        var bytes = new byte[toRead];
        Array.Copy(Data, Offset, bytes, 0, toRead);
        Offset += toRead;
        return bytes;
    }

    public byte[] ReadRemainingBytes() => ReadBytes(Remaining);

    #endregion

    #region Probing & Seeking Helpers

    /// <summary>
    /// Searches forward for the next WChar string prefix within this reader's bounds.
    /// Advances <see cref="Offset"/> to that position and returns true if found.
    /// </summary>
    public bool SeekNextWChar()
    {
        while (Offset <= End - 3)
        {
            if (MfcPrimitives.IsWCharPrefix(Data, Offset))
                return true;
            Offset++;
        }
        return false;
    }

    /// <summary>
    /// Searches forward for the next MFC Tag prefix (FF FF {00|01} 00) within this reader's bounds.
    /// Advances <see cref="Offset"/> to that position and returns true if found.
    /// </summary>
    public bool SeekNextMfcTag()
    {
        while (Offset <= End - 4)
        {
            if (MfcPrimitives.IsMfcTag(Data, Offset))
                return true;
            Offset++;
        }
        return false;
    }

    #endregion
}
