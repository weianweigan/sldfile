using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SldFile.Mfc;

/// <summary>
/// Unified bi-directional serializer/deserializer for MFC 4.2 CArchive streams.
/// Encapsulates load array mapping, class registration, primitive I/O, and string serialization.
/// Guarantees symmetric read/write semantics without code duplication.
/// </summary>
public sealed class MfcArchive : IDisposable
{
    private readonly MemoryStream _memoryStream;
    private readonly BinaryReader? _reader;
    private readonly BinaryWriter? _writer;

    public bool IsStoring { get; }
    public bool IsLoading => !IsStoring;
    public ushort StreamVersion { get; set; }
    public int DocumentVersion
    {
        get => Context.DocumentVersion;
        set => Context.DocumentVersion = value;
    }
    public MfcArchiveContext Context { get; }

    public int Position
    {
        get => checked((int)_memoryStream.Position);
        set => _memoryStream.Position = value;
    }

    public int Length => checked((int)_memoryStream.Length);
    public int Remaining => checked((int)Math.Max(0, _memoryStream.Length - _memoryStream.Position));

    /// <summary>
    /// Creates an MfcArchive for loading (deserializing) from a byte array.
    /// </summary>
    public static MfcArchive CreateReader(byte[] data, MfcArchiveContext? context = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return new MfcArchive(new MemoryStream(data, writable: false), isStoring: false, context);
    }

    public static MfcArchive CreateReader(byte[] data, int offset, MfcArchiveContext? context = null)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        return new MfcArchive(new MemoryStream(data, offset, data.Length - offset, writable: false), isStoring: false, context);
    }

    /// <summary>
    /// Creates an MfcArchive for storing (serializing) to a dynamic buffer.
    /// </summary>
    /// <summary>
    /// Deserializes a model from raw byte data using MfcArchive.
    /// </summary>
    public static T Deserialize<T>(byte[] data, MfcArchiveContext? context = null) where T : IMfcSerializable, new()
    {
        if (data == null || data.Length == 0) return new T();
        using var ar = CreateReader(data, context);
        var model = new T();
        model.Serialize(ar);
        return model;
    }

    /// <summary>
    /// Deserializes into an existing model instance from raw byte data.
    /// </summary>
    public static void DeserializeInto<T>(byte[] data, T model, MfcArchiveContext? context = null) where T : IMfcSerializable
    {
        if (data == null || data.Length == 0) return;
        using var ar = CreateReader(data, context);
        model.Serialize(ar);
    }

    /// <summary>
    /// Serializes an IMfcSerializable model into a byte array using MfcArchive.
    /// </summary>
    public static byte[] Serialize<T>(T model, MfcArchiveContext? context = null) where T : IMfcSerializable
    {
        if (model == null) return Array.Empty<byte>();
        using var ar = CreateWriter(512, context);
        model.Serialize(ar);
        return ar.ToArray();
    }

    public static MfcArchive CreateWriter(int initialCapacity = 512, MfcArchiveContext? context = null)
    {
        return new MfcArchive(new MemoryStream(initialCapacity), isStoring: true, context);
    }

    private MfcArchive(MemoryStream stream, bool isStoring, MfcArchiveContext? context)
    {
        _memoryStream = stream;
        IsStoring = isStoring;
        Context = context ?? new MfcArchiveContext();

        if (isStoring)
        {
            _writer = new BinaryWriter(_memoryStream, Encoding.UTF8, leaveOpen: true);
        }
        else
        {
            _reader = new BinaryReader(_memoryStream, Encoding.UTF8, leaveOpen: true);
        }
    }

    #region Primitive Read/Write (Little-Endian)

    public void Byte(ref byte value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadByte();
    }

    public void U16(ref ushort value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadUInt16();
    }

    public void I16(ref short value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadInt16();
    }

    public void U32(ref uint value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadUInt32();
    }

    public void I32(ref int value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadInt32();
    }

    public void U64(ref ulong value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadUInt64();
    }

    public void I64(ref long value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadInt64();
    }

    public void F32(ref float value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadSingle();
    }

    public void F64(ref double value)
    {
        if (IsStoring) _writer!.Write(value);
        else value = _reader!.ReadDouble();
    }

    public void Boolean(ref bool value)
    {
        if (IsStoring) _writer!.Write(value ? (byte)1 : (byte)0);
        else value = _reader!.ReadByte() != 0;
    }

    public void WriteBytes(byte[]? bytes)
    {
        if (!IsStoring) throw new InvalidOperationException("Cannot call WriteBytes on loading archive.");
        if (bytes != null && bytes.Length > 0)
        {
            _writer!.Write(bytes);
        }
    }

    public byte[] ReadBytes(int count)
    {
        if (IsStoring) throw new InvalidOperationException("Cannot call ReadBytes on storing archive.");
        if (count <= 0) return Array.Empty<byte>();
        return _reader!.ReadBytes(count);
    }

    public byte[] ReadRemainingBytes()
    {
        return ReadBytes(Remaining);
    }

    #endregion

    #region String Serialization

    /// <summary>
    /// Bi-directional serialization for MFC Unicode WChar string (<c>FF FE FF [len] [utf16]</c>).
    /// </summary>
    public void WChar(ref string? value)
    {
        if (IsStoring)
        {
            _writer!.Write((byte)0xFF);
            _writer.Write((byte)0xFE);
            _writer.Write((byte)0xFF);

            if (string.IsNullOrEmpty(value))
            {
                _writer.Write((byte)0);
                return;
            }

            byte[] utf16 = Encoding.Unicode.GetBytes(value);
            int charCount = utf16.Length / 2;
            if (charCount < byte.MaxValue)
            {
                _writer.Write((byte)charCount);
            }
            else
            {
                _writer.Write(byte.MaxValue);
                if (charCount < ushort.MaxValue)
                {
                    _writer.Write((ushort)charCount);
                }
                else
                {
                    _writer.Write(ushort.MaxValue);
                    _writer.Write((uint)charCount);
                }
            }
            _writer.Write(utf16);
        }
        else
        {
            // Validate prefix
            if (Remaining < 4)
            {
                value = null;
                return;
            }

            byte b0 = _reader!.ReadByte();
            byte b1 = _reader.ReadByte();
            byte b2 = _reader.ReadByte();

            if (b0 != 0xFF || b1 != 0xFE || b2 != 0xFF)
            {
                _memoryStream.Position -= 3;
                value = null;
                return;
            }

            byte lenByte = _reader.ReadByte();
            int charCount = lenByte;
            if (lenByte == byte.MaxValue)
            {
                ushort lenU16 = _reader.ReadUInt16();
                charCount = lenU16;
                if (lenU16 == ushort.MaxValue)
                {
                    charCount = checked((int)_reader.ReadUInt32());
                }
            }

            if (charCount == 0)
            {
                value = string.Empty;
                return;
            }

            byte[] strBytes = _reader.ReadBytes(charCount * 2);
            value = Encoding.Unicode.GetString(strBytes);
        }
    }

    #endregion

    #region Class Tag and Object Reference

    /// <summary>
    /// Reads an MFC WriteObject class tag or object reference conforming to MFC 4.2 wire format.
    /// Handles 0x0000 (null), 0x0001..0x7FFF (object ref), 0xFFFF (new class), and 0x8001..0x8FFE (class ref).
    /// </summary>
    public MfcTagKind ReadClassTag(out string className, out ushort classRefTag, out ushort schema)
    {
        className = string.Empty;
        classRefTag = 0;
        schema = 0;

        if (IsStoring) throw new InvalidOperationException("Cannot call ReadClassTag on storing archive.");
        if (Remaining < 2) return MfcTagKind.Invalid;

        int startPos = Position;
        ushort tag = _reader!.ReadUInt16();

        // 0x0000: Null object pointer
        if (tag == 0x0000)
        {
            return MfcTagKind.Null;
        }

        // 0x0001..0x7FFF: Object back-reference
        if (tag <= 0x7FFF)
        {
            classRefTag = tag;
            return MfcTagKind.ObjectReference;
        }

        // 0xFFFF: Brand-new class declaration (schema + nameLen + name + optional native tag)
        if (tag == 0xFFFF)
        {
            if (Remaining < 4)
            {
                Position = startPos;
                return MfcTagKind.Invalid;
            }

            schema = _reader.ReadUInt16();
            ushort nameLen = _reader.ReadUInt16();
            if (Remaining < nameLen)
            {
                Position = startPos;
                return MfcTagKind.Invalid;
            }

            byte[] nameBytes = _reader.ReadBytes(nameLen);
            className = Encoding.ASCII.GetString(nameBytes);

            // Native MFC WriteObject emits (0x8000 | classIndex) right after the class name,
            // followed immediately by the object's WChar DisplayName (0xFF 0xFE 0xFF).
            ushort? explicitRefTag = null;
            if (MfcMateRegistry.IsMateClass(className) && Remaining >= 4)
            {
                int peek = Position;
                ushort nextWord = _reader.ReadUInt16();
                ushort followingWord = _reader.ReadUInt16();
                Position = peek;

                if ((nextWord & 0x8000) != 0 && nextWord != 0xFFFF && nextWord != 0xFEFF && followingWord == 0xFEFF)
                {
                    _reader.ReadUInt16(); // consume nextWord
                    explicitRefTag = nextWord;
                    Context.HasNativeExplicitTags = true;
                }
            }

            Context.RegisterOrGetClass(className, out classRefTag, startPos, explicitRefTag);
            return MfcTagKind.NewClass;
        }

        // 0x8001..0x8FFE: Known class reference or native prefix
        classRefTag = tag;
        if (Context.TryResolveClassReference(tag, out className))
        {
            return MfcTagKind.ClassReference;
        }

        // Check if tag is a 2-byte object/record prefix followed by a valid class reference tag and WChar (native SW wire format)
        if (Remaining >= 4)
        {
            int peek = Position;
            ushort secondaryTag = _reader.ReadUInt16();
            ushort followingWord = _reader.ReadUInt16();
            Position = peek;

            if (Context.TryResolveClassReference(secondaryTag, out className) && followingWord == 0xFEFF)
            {
                _reader.ReadUInt16(); // consume secondaryTag
                classRefTag = secondaryTag;
                Context.HasNativeExplicitTags = true;
                return MfcTagKind.ClassReference;
            }
        }

        className = $"<class_0x{tag:X4}>";
        return MfcTagKind.ClassReference;
    }

    /// <summary>
    /// Writes an MFC WriteObject class tag prefix (0xFFFF 0x0001 [len] [ASCII]) or compact class reference (0x8000 | tag).
    /// </summary>
    public void WriteClassTag(string className, ref ushort refTag, ushort schema = 1)
    {
        if (!IsStoring) throw new InvalidOperationException("Cannot call WriteClassTag on loading archive.");
        int offset = Position;
        if (Context.RegisterOrGetClass(className, out ushort tag, offset))
        {
            _writer!.Write((ushort)0xFFFF);
            _writer.Write(schema);
            byte[] nameBytes = Encoding.ASCII.GetBytes(className);
            _writer.Write((ushort)nameBytes.Length);
            _writer.Write(nameBytes);
            refTag = tag;
        }
        else
        {
            _writer!.Write(tag);
            refTag = tag;
        }
    }

    /// <summary>
    /// Writes or reads a class tag prefix (<c>FF FF 01 00 [len] [ASCII]</c>) or compact class reference (<c>0x8000 | tag</c>).
    /// </summary>
    public void ClassTagOrRef(string className, ref ushort refTag)
    {
        if (IsStoring)
        {
            WriteClassTag(className, ref refTag, schema: 1);
        }
        else
        {
            var kind = ReadClassTag(out string loadedName, out ushort tag, out _);
            if (kind == MfcTagKind.NewClass || kind == MfcTagKind.ClassReference)
            {
                refTag = tag;
            }
        }
    }

    #endregion

    #region Count and String Wire Formats

    /// <summary>
    /// Bi-directional serialization for MFC variable-length collection counts (WORD vs DWORD).
    /// </summary>
    public void Count(ref uint count)
    {
        if (IsStoring)
        {
            if (count < 0xFFFF)
            {
                _writer!.Write((ushort)count);
            }
            else
            {
                _writer!.Write((ushort)0xFFFF);
                _writer.Write(count);
            }
        }
        else
        {
            if (Remaining < 2) return;
            ushort w = _reader!.ReadUInt16();
            if (w == 0xFFFF)
            {
                if (Remaining < 4) return;
                count = _reader.ReadUInt32();
            }
            else
            {
                count = w;
            }
        }
    }

    /// <summary>
    /// Bi-directional serialization for MFC CString wire format supporting Unicode and ANSI with atomic rollback.
    /// </summary>
    public void CString(ref string? value)
    {
        if (IsStoring)
        {
            if (string.IsNullOrEmpty(value))
            {
                _writer!.Write((byte)0x00);
                return;
            }

            _writer!.Write((byte)0xFF);
            _writer.Write((byte)0xFE);
            int charLen = value!.Length;
            if (charLen < 0xFF)
            {
                _writer.Write((byte)charLen);
            }
            else
            {
                _writer.Write((byte)0xFF);
                _writer.Write((byte)0xFE);
                _writer.Write((ushort)charLen);
            }
            _writer.Write(Encoding.Unicode.GetBytes(value));
        }
        else
        {
            int start = Position;
            if (Remaining < 1) { value = null; return; }

            byte b0 = _reader!.ReadByte();
            if (b0 == 0x00)
            {
                value = string.Empty;
                return;
            }

            if (b0 == 0xFF)
            {
                if (Remaining < 1) { Position = start; value = null; return; }
                byte b1 = _reader.ReadByte();

                if (b1 == 0xFE)
                {
                    if (Remaining < 1) { Position = start; value = null; return; }
                    byte b2 = _reader.ReadByte();
                    int charCount = b2;

                    if (b2 == 0xFF)
                    {
                        if (Remaining < 1) { Position = start; value = null; return; }
                        byte b3 = _reader.ReadByte();
                        if (b3 == 0xFE)
                        {
                            if (Remaining < 2) { Position = start; value = null; return; }
                            charCount = _reader.ReadUInt16();
                        }
                        else if (b3 == 0xFF)
                        {
                            if (Remaining < 4) { Position = start; value = null; return; }
                            charCount = checked((int)_reader.ReadUInt32());
                        }
                        else
                        {
                            charCount = b3;
                        }
                    }

                    if (Remaining < charCount * 2) { Position = start; value = null; return; }
                    byte[] strBytes = _reader.ReadBytes(charCount * 2);
                    value = Encoding.Unicode.GetString(strBytes);
                    return;
                }

                if (b1 == 0xFF)
                {
                    if (Remaining < 1) { Position = start; value = null; return; }
                    byte b2 = _reader.ReadByte();
                    if (b2 == 0x00)
                    {
                        value = string.Empty;
                        return;
                    }

                    if (Remaining < b2 * 2) { Position = start; value = null; return; }
                    byte[] strBytes = _reader.ReadBytes(b2 * 2);
                    value = Encoding.Unicode.GetString(strBytes);
                    return;
                }

                int n = b1;
                if (Remaining < n) { Position = start; value = null; return; }
                byte[] asciiBytes = _reader.ReadBytes(n);
                value = Encoding.ASCII.GetString(asciiBytes);
                return;
            }

            int len = b0;
            if (Remaining < len) { Position = start; value = null; return; }
            byte[] bytes = _reader.ReadBytes(len);
            value = Encoding.ASCII.GetString(bytes);
        }
    }

    #endregion

    public byte[] ToArray()
    {
        _writer?.Flush();
        return _memoryStream.ToArray();
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _memoryStream.Dispose();
    }
}
