using System;
using System.Collections.Generic;
using System.Text;
using SldFile.Diagnostics;

namespace SldFile.Ole;

/// <summary>
/// Low-level reader for OLE Property Set binary format (MS-OLEPS).
/// Provides safe, bounds-checked access to the property array, section header,
/// and individual typed values (VT_LPSTR, VT_I4, VT_BOOL, VT_FILETIME, etc.).
///
/// This class encapsulates all raw byte[] parsing so that higher-level stream
/// readers never touch <see cref="BitConverter"/> or offset arithmetic directly.
/// </summary>
internal sealed class OlePropertySetReader
{
    private readonly byte[] _bytes;
    private readonly string _streamName;

    /// <summary>Absolute byte offset where the first (and usually only) section begins.</summary>
    public int SectionOffset { get; }

    /// <summary>Number of property ID/offset pairs declared in the section header.</summary>
    public int PropertyCount { get; }

    /// <summary>
    /// Initializes a new reader over <paramref name="bytes"/>.
    /// Validates the minimum header size and section offset.
    /// </summary>
    /// <exception cref="StreamCorruptedException">
    /// Thrown when the byte array is too short or the section offset is out of range.
    /// </exception>
    public OlePropertySetReader(byte[] bytes, string streamName)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _streamName = streamName;

        // Minimum: 28 bytes header + 20 bytes FMTID/offset list entry ⟶ section offset sits at byte 44.
        if (bytes.Length < 48)
            throw new StreamCorruptedException(streamName, $"Stream is too short ({bytes.Length} bytes); expected at least 48.");

        SectionOffset = BitConverter.ToInt32(bytes, 44);
        if (SectionOffset < 0 || SectionOffset >= bytes.Length)
            throw new StreamCorruptedException(streamName, $"Section offset {SectionOffset} is out of range [0, {bytes.Length}).");

        if (SectionOffset + 8 > bytes.Length)
            throw new StreamCorruptedException(streamName, "Section header is truncated.");

        PropertyCount = BitConverter.ToInt32(bytes, SectionOffset + 4);
        if (PropertyCount < 0)
            throw new StreamCorruptedException(streamName, $"Property count {PropertyCount} is negative.");
    }

    /// <summary>
    /// Enumerates all property entries in the section, yielding (PropertyId, AbsoluteOffset) pairs.
    /// Entries whose ID/offset pair falls outside the byte array are silently skipped.
    /// </summary>
    public IEnumerable<(uint PropertyId, int AbsoluteOffset)> EnumerateProperties()
    {
        for (int i = 0; i < PropertyCount; i++)
        {
            int entryBase = SectionOffset + 8 + i * 8;
            if (entryBase + 8 > _bytes.Length) yield break;

            uint pId = BitConverter.ToUInt32(_bytes, entryBase);
            int pOff = BitConverter.ToInt32(_bytes, entryBase + 4);
            int absOff = SectionOffset + pOff;

            if (absOff < 0 || absOff >= _bytes.Length) continue;

            yield return (pId, absOff);
        }
    }

    /// <summary>
    /// Reads the VT type tag (DWORD) at <paramref name="absoluteOffset"/>.
    /// Returns -1 if the offset is out of range.
    /// </summary>
    public int ReadVtType(int absoluteOffset)
    {
        if (absoluteOffset + 4 > _bytes.Length) return -1;
        return BitConverter.ToInt32(_bytes, absoluteOffset);
    }

    /// <summary>
    /// Reads a VT_LPSTR value at <paramref name="absoluteOffset"/>.
    /// SolidWorks stores UTF-8 strings despite the OLE spec prescribing ANSI.
    /// The trailing null byte is excluded from the returned string.
    /// </summary>
    /// <exception cref="PropertyReadException">
    /// Thrown when the string length header or payload exceeds the byte array.
    /// </exception>
    public string ReadLpstr(int absoluteOffset, uint propertyId)
    {
        if (absoluteOffset + 8 > _bytes.Length)
            throw new PropertyReadException(_streamName, propertyId, "VT_LPSTR length header is truncated.");

        int strLen = BitConverter.ToInt32(_bytes, absoluteOffset + 4);
        if (strLen < 0)
            throw new PropertyReadException(_streamName, propertyId, $"VT_LPSTR declares negative length {strLen}.");

        int payloadEnd = absoluteOffset + 8 + strLen;
        if (payloadEnd > _bytes.Length)
            throw new PropertyReadException(_streamName, propertyId,
                $"VT_LPSTR payload ({strLen} bytes at offset {absoluteOffset + 8}) exceeds stream length {_bytes.Length}.");

        // Exclude trailing null byte
        int readLen = strLen > 0 ? strLen - 1 : 0;
        return Encoding.UTF8.GetString(_bytes, absoluteOffset + 8, readLen);
    }

    /// <summary>
    /// Reads a VT_I4 (32-bit signed integer) at <paramref name="absoluteOffset"/>.
    /// </summary>
    /// <exception cref="PropertyReadException">
    /// Thrown when the value DWORD exceeds the byte array.
    /// </exception>
    public int ReadInt32(int absoluteOffset, uint propertyId)
    {
        if (absoluteOffset + 8 > _bytes.Length)
            throw new PropertyReadException(_streamName, propertyId, "VT_I4 value is truncated.");

        return BitConverter.ToInt32(_bytes, absoluteOffset + 4);
    }

    /// <summary>
    /// Reads a VT_BOOL at <paramref name="absoluteOffset"/>.
    /// Returns true if the 32-bit value is non-zero.
    /// </summary>
    /// <exception cref="PropertyReadException">
    /// Thrown when the value DWORD exceeds the byte array.
    /// </exception>
    public bool ReadBool(int absoluteOffset, uint propertyId)
    {
        return ReadInt32(absoluteOffset, propertyId) != 0;
    }

    /// <summary>
    /// Returns the underlying raw bytes. Used by the patching / save pipeline.
    /// </summary>
    public byte[] GetRawBytes() => _bytes;
}
