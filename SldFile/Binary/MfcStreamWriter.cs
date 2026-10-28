using System;
using System.IO;
using System.Text;

namespace SldFile.Binary;

/// <summary>
/// High-performance stream writer for MFC 4.2 CArchive binary format.
/// Emits little-endian primitives, MFC class tags, and WChar strings without unnecessary allocations.
/// </summary>
internal sealed class MfcStreamWriter : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly BinaryWriter _writer;

    public MfcStreamWriter(int initialCapacity = 256)
    {
        _stream = new MemoryStream(initialCapacity);
        _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
    }

    public void WriteU8(byte v) => _writer.Write(v);
    public void WriteU16(ushort v) => _writer.Write(v);
    public void WriteU32(uint v) => _writer.Write(v);
    public void WriteU64(ulong v) => _writer.Write(v);
    public void WriteF32(float v) => _writer.Write(v);
    public void WriteF64(double v) => _writer.Write(v);

    public void WriteBytes(byte[]? v)
    {
        if (v != null && v.Length > 0)
            _writer.Write(v);
    }

    public void WriteMfcTag(string className)
    {
        _writer.Write((byte)0xFF);
        _writer.Write((byte)0xFF);
        _writer.Write((byte)0x01); // new class instance
        _writer.Write((byte)0x00);

        byte[] nameBytes = Encoding.ASCII.GetBytes(className ?? string.Empty);
        _writer.Write((ushort)nameBytes.Length);
        if (nameBytes.Length > 0)
            _writer.Write(nameBytes);
    }

    public void WriteWChar(string? s)
    {
        _writer.Write((byte)0xFF);
        _writer.Write((byte)0xFE);
        _writer.Write((byte)0xFF);

        if (string.IsNullOrEmpty(s))
        {
            _writer.Write((byte)0);
            return;
        }

        byte[] utf16 = Encoding.Unicode.GetBytes(s);
        int charCount = utf16.Length / 2;
        if (charCount > 255)
            charCount = 255;

        _writer.Write((byte)charCount);
        if (charCount > 0)
            _writer.Write(utf16, 0, charCount * 2);
    }

    public byte[] ToArray()
    {
        _writer.Flush();
        return _stream.ToArray();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _stream.Dispose();
    }
}
