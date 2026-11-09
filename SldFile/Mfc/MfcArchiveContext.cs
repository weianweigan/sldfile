using System;
using System.Collections.Generic;
using System.Buffers.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Represents the MFC 4.2 CArchive runtime class and object reference symbol table (<c>m_pLoadArray</c> / <c>m_mapStore</c>).
/// In MFC serialization, the first occurrence of a <c>CRuntimeClass</c> emits a full class tag
/// (<c>FF FF [schema:u16] [len:u16] [asciiName]</c>) and allocates a 15-bit index in the archive table.
/// Subsequent occurrences of the same class emit a compact 2-byte class reference (<c>0x8000 | index</c>).
/// </summary>
public sealed class MfcArchiveContext
{
    public const ushort ClassRefBit = 0x8000;
    public const ushort NewClassTagMarker = 0xFFFF;

    public sealed class ClassEntry
    {
        public string ClassName { get; }
        public ushort ReferenceTag { get; }
        public int FirstTagOffset { get; }

        public ClassEntry(string className, ushort referenceTag, int firstTagOffset)
        {
            ClassName = className;
            ReferenceTag = referenceTag;
            FirstTagOffset = firstTagOffset;
        }
    }

    private readonly Dictionary<string, ClassEntry> _byClassName = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, ClassEntry> _byRefTag = new();
    private readonly List<ClassEntry> _orderedClasses = new();
    private ushort _nextIndex;

    /// <summary>SolidWorks document version threshold context.</summary>
    public int DocumentVersion { get; set; }

    /// <summary>Indicates whether native explicit tag words (0x8006, 0x804D) or 2-byte secondary prefixes were encountered.</summary>
    public bool HasNativeExplicitTags { get; set; }

    public IReadOnlyDictionary<string, ClassEntry> ClassesByName => _byClassName;
    public IReadOnlyDictionary<ushort, ClassEntry> ClassesByTag => _byRefTag;
    public IReadOnlyList<ClassEntry> OrderedClasses => _orderedClasses;

    public MfcArchiveContext(ushort initialIndex = 1)
    {
        _nextIndex = initialIndex;
    }

    /// <summary>
    /// Builds an archive context from a parsed <see cref="MfcDocument"/>, indexing all declared
    /// MFC classes in stream order and discovering compact 2-byte class-reference prefixes.
    /// </summary>
    internal static MfcArchiveContext FromDocument(MfcDocument doc)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        var ctx = new MfcArchiveContext();
        foreach (var elem in doc.AllElements)
        {
            if (!string.IsNullOrEmpty(elem.ClassName) && !ctx._byClassName.ContainsKey(elem.ClassName))
            {
                ushort tag = (ushort)(ClassRefBit | ctx._nextIndex++);
                var entry = new ClassEntry(elem.ClassName, tag, elem.TagOffset);
                ctx._byClassName[elem.ClassName] = entry;
                ctx._byRefTag[tag] = entry;
            }
        }
        return ctx;
    }

    /// <summary>
    /// Registers a class explicitly with a known 2-byte reference tag (e.g. <c>0x8109</c>) or allocates the next tag.
    /// Returns <c>true</c> if this is the first time the class was registered (full tag should be written),
    /// or <c>false</c> if the class was already registered (compact 2-byte <paramref name="referenceTag"/> should be written).
    /// </summary>
    public bool RegisterOrGetClass(string className, out ushort referenceTag, int tagOffset = -1, ushort? explicitTag = null)
    {
        if (string.IsNullOrEmpty(className))
            throw new ArgumentException("Class name cannot be null or empty.", nameof(className));

        if (_byClassName.TryGetValue(className, out var existing))
        {
            referenceTag = existing.ReferenceTag;
            return false;
        }

        referenceTag = explicitTag ?? (ushort)(ClassRefBit | _nextIndex++);
        var entry = new ClassEntry(className, referenceTag, tagOffset);
        _byClassName[className] = entry;
        _byRefTag[referenceTag] = entry;
        _orderedClasses.Add(entry);
        return true;
    }

    /// <summary>
    /// Resolves a 2-byte MFC class reference tag (with high bit <c>0x8000</c> set) to its class name.
    /// </summary>
    public bool TryResolveClassReference(ushort referenceTag, out string className)
    {
        if (_byRefTag.TryGetValue(referenceTag, out var entry))
        {
            className = entry.ClassName;
            return true;
        }

        // Fallback: 1-based index (tag & 0x7FFF) matching Wire Format
        int index = (referenceTag & ~ClassRefBit);
        if (index > 0 && index <= _orderedClasses.Count)
        {
            className = _orderedClasses[index - 1].ClassName;
            return true;
        }

        className = string.Empty;
        return false;
    }

    /// <summary>
    /// Resolves a 1-based class index directly to its class name.
    /// </summary>
    public bool TryResolveClassByIndex(int index, out string className)
    {
        if (index > 0 && index <= _orderedClasses.Count)
        {
            className = _orderedClasses[index - 1].ClassName;
            return true;
        }
        className = string.Empty;
        return false;
    }

    /// <summary>
    /// Checks whether the 2-byte value at <paramref name="offset"/> is a compact MFC class/object reference tag
    /// (<c>0x8000..0x83FF</c>).
    /// </summary>
    public static bool IsCompactReferenceTag(ReadOnlySpan<byte> data, int offset, out ushort tag)
    {
        tag = 0;
        if (offset < 0 || offset + 2 > data.Length) return false;
        ushort candidate = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
        if ((candidate & 0xFC00) == ClassRefBit && candidate != ClassRefBit)
        {
            tag = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Scans <paramref name="data"/> for repeated records that start with a 2-byte compact class reference
    /// followed by a 2-byte schema word (e.g. <c>0x8006</c> or <c>0x800A</c>) and a WChar display name.
    /// Used to uniformly recover repeated MFC objects that omit inline class names.
    /// </summary>
    public static List<(int Offset, ushort ClassRefTag, string DisplayName)> ScanRepeatedNamedRecords(
        byte[] data,
        ushort expectedSchemaWord,
        Func<string, bool>? displayNamePredicate = null,
        int maxRecords = 64)
    {
        var results = new List<(int Offset, ushort ClassRefTag, string DisplayName)>();
        if (data == null || data.Length < 12) return results;

        byte schemaLo = (byte)(expectedSchemaWord & 0xFF);
        byte schemaHi = (byte)((expectedSchemaWord >> 8) & 0xFF);

        ushort discoveredTag = 0;
        for (int i = 0; i + 12 < data.Length && results.Count < maxRecords; i++)
        {
            if ((data[i + 1] != 0x80 && data[i + 1] != 0x81) ||
                data[i + 2] != schemaLo ||
                data[i + 3] != schemaHi ||
                !MfcPrimitives.IsWCharPrefix(data, i + 4))
            {
                continue;
            }

            ushort candidateTag = BitConverter.ToUInt16(data, i);
            if (discoveredTag != 0 && candidateTag != discoveredTag)
                continue;

            int cursor = i + 4;
            string? name = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
            if (string.IsNullOrEmpty(name))
                continue;

            if (displayNamePredicate != null && !displayNamePredicate(name!))
                continue;

            if (discoveredTag == 0)
                discoveredTag = candidateTag;

            results.Add((i, candidateTag, name!));
        }

        return results;
    }
}
