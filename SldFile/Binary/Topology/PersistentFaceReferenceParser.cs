using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.Topology;

/// <summary>
/// One persistent face reference embedded in moCompFace_c.
///
/// Record at block offset +81 (47 bytes):
///   +0  u32 0x4A38 (persist-reference magic)
///   +4  u8  0
///   +5  u16 surface class reference (0x80E6/0x80EA/0x80F2/0x8125...)
///   +7  u32 1
///   +11 u32 0
///   +15 u32 face count   (6 for the first fillet/cut block, 4 for later blocks)
///   +19 u32 class word   (0x200 or 0x300)
///   +23 u32 face id      (opaque; 0x3A11 / 0x26E6 / 0)
///   +27 16-byte archive id (same constant GUID as moFaceRef_c @+20)
///   +43 u16 0
///   +45 u16 mapping-list class reference (0x80E8/0x80EC/0x80F1...)
/// The 20-byte surface mapping records start at block +128.
/// </summary>
internal sealed class PersistentFaceReferenceData
{
    public uint FaceCount { get; set; }

    public uint ClassWord { get; set; }

    public uint FaceId { get; set; }

    public ushort SurfaceClass { get; set; }

    public ushort MappingClassReference { get; set; }

    public ulong ArchiveIdLow { get; set; }

    public ulong ArchiveIdHigh { get; set; }

    public string ArchiveIdHex => $"{ArchiveIdLow:X16}{ArchiveIdHigh:X16}";
}

/// <summary>One header+face-reference block of moCompFace_c.</summary>
internal sealed class CompFaceBlock
{
    public int BlockOffset { get; set; }

    public int BlockLength { get; set; }

    /// <summary>Header flag byte at +6: 0x40 for cut / sheet-metal specimens, 0x00 otherwise.</summary>
    public byte HeaderFlag { get; set; }

    public PersistentFaceReferenceData FaceReference { get; set; } = new PersistentFaceReferenceData();

    /// <summary>Embedded 20-byte surface mapping records (same grammar as fromSkt).</summary>
    public IReadOnlyList<FromSktMappingEntry> Mappings { get; set; } = Array.Empty<FromSktMappingEntry>();

    /// <summary>Bytes after the mapping list up to the next block / payload end; not interpreted.</summary>
    public int TrailerOffset { get; set; }

    public int TrailerLength { get; set; }
}

internal enum CompFaceLayout
{
    Unknown = 0,

    /// <summary>86-byte sheet-metal stub: header + magic + 0x00, no face blocks.</summary>
    Stub = 1,

    /// <summary>One or more full face blocks (cut 572 B, fillet 926 B, after-fillet 3173 B).</summary>
    Full = 2
}

internal sealed class CompFaceReferenceData
{
    public CompFaceLayout Layout { get; set; }

    public IReadOnlyList<CompFaceBlock> Blocks { get; set; } = Array.Empty<CompFaceBlock>();
}

/// <summary>Decoded 38-byte moFaceRef_c generated-face reference.</summary>
internal sealed class GeneratedFaceReferenceData
{
    public uint FaceCount { get; set; }

    public uint TopologyType { get; set; }

    public uint ClassWord { get; set; }

    public uint ClassSpecificId { get; set; }

    public ulong ArchiveIdLow { get; set; }

    public ulong ArchiveIdHigh { get; set; }

    /// <summary>True for the established 38-byte generated-face layout.</summary>
    public bool IsGeneratedFaceReference { get; set; }
}

/// <summary>14-byte moSurfaceIdRep_c surface reference record.</summary>
internal sealed class SurfaceReferenceRecordData
{
    public ushort Marker { get; set; }

    public ushort ClassA { get; set; }

    public ushort ClassB { get; set; }

    public uint FeatureClass { get; set; }

    public uint ArchiveSerial { get; set; }
}

/// <summary>Decoded moSingleFaceRef_w terminating-face reference (473-byte layout).</summary>
internal sealed class SingleFaceReferenceData
{
    public byte HeaderFlag { get; set; }

    public PersistentFaceReferenceData FaceReference { get; set; } = new PersistentFaceReferenceData();

    public IReadOnlyList<FromSktMappingEntry> Mappings { get; set; } = Array.Empty<FromSktMappingEntry>();

    /// <summary>Opaque face id at face-reference offset +23 (0x7CB in the UpToSurface specimen).</summary>
    public uint FaceId => FaceReference.FaceId;
}

/// <summary>
/// Decoded moCompSolidBody_c body reference (327-byte layout):
///   81-byte component header | surface/body ref at +81 | mapping list |
///   body ref at +243 (magic, owner class word at +263, body id at +305,
///   16-byte archive id at +309).
///
/// Owner class word evidence (all resolved-features specimens):
///   0xFFFFFFFF - UpToSurface cut after ModifyDefinition (native featureScope=true,
///                autoSelect=false, featureScopeBodiesCount=1): the body is an
///                explicitly selected scope body;
///   0x4F       - blind/through-all/draft cuts and their resaves (native
///                UseFeatScope=false, UseAutoSelect=true): the body is the
///                auto-resolved target of the cut, not a scope selection;
///   0x5E       - Normal Cut specimen (283-byte layout, parsed separately).
/// </summary>
internal sealed class CompSolidBodyReferenceData
{
    public uint BodyId { get; set; }

    /// <summary>u32 at +263 of the 327-byte layout.</summary>
    public uint BodyClassWord { get; set; }

    /// <summary>True only for the 0xFFFFFFFF class word measured on the explicit scope body.</summary>
    public bool IsExplicitBodySelection => BodyClassWord == uint.MaxValue;

    public ulong ArchiveIdLow { get; set; }

    public ulong ArchiveIdHigh { get; set; }
}

/// <summary>Decoder for the 327-byte explicit body-selection layout.</summary>
internal static class CompSolidBodyReferenceParser
{
    internal const int LayoutLength = 327;
    private const int BodyClassWordOffset = 263;
    private const int BodyIdOffset = 305;
    private const int ArchiveIdOffset = 309;

    internal static CompSolidBodyReferenceData? Read(MfcDocument doc, MfcElement element)
    {
        if (doc == null || element == null || element.ClassName != "moCompSolidBody_c") return null;
        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, element.DataStart,
            PersistentFaceReferenceParser.PayloadEnd(doc, element));
        return ReadPayload(raw);
    }

    internal static CompSolidBodyReferenceData? ReadPayload(byte[] raw)
    {
        if (raw == null || raw.Length != LayoutLength) return null;
        if (!PersistentFaceReferenceParser.IsHeader(raw, 0, out byte flag) || flag != 0x00) return null;
        if (BitConverter.ToUInt32(raw, 81) != 0x4A38) return null;
        if (BitConverter.ToUInt32(raw, 243) != 0x4A38) return null;
        uint bodyId = BitConverter.ToUInt32(raw, BodyIdOffset);
        if (bodyId == 0 || bodyId == uint.MaxValue) return null;
        ulong idLow = BitConverter.ToUInt64(raw, ArchiveIdOffset);
        ulong idHigh = BitConverter.ToUInt64(raw, ArchiveIdOffset + 8);
        if (idLow == 0 || idHigh == 0) return null;
        return new CompSolidBodyReferenceData
        {
            BodyId = bodyId,
            BodyClassWord = BitConverter.ToUInt32(raw, BodyClassWordOffset),
            ArchiveIdLow = idLow,
            ArchiveIdHigh = idHigh
        };
    }
}

/// <summary>
/// Decoders for persistent face references:
///   - moCompFace_c (86-byte sheet-metal stub and multi-block full layout);
///   - moFaceRef_c (38-byte generated-face reference);
///   - moSurfaceIdRep_c (14-byte surface reference record).
///
/// Specimens: fillet_2mm (926 B, 3 blocks), extrude_cut_5mm (572 B, 1 block),
/// sm_base_flange_{10,20,thick2,rad3} (86 B stub), sm_base_flange_after_fillet
/// (3173 B, 3 blocks), plus FaceRef in extrude/sweep/revolve/SheetMetal samples.
///
/// Relation evidence:
///   - the 16-byte archive id in the compFace record equals the one in the
///     feature's moFaceRef_c record; both describe the same generated face group;
///   - moSurfaceIdRep_c (14 B: u16 marker | u16 classA | u16 classB |
///     u32 feature class | u32 serial) shares its class pair with the compFace
///     face-reference surface class and the first embedded mapping record
///     (fillet: 0x80EA/0x00ED, feature class 0x51);
///   - moEndFace3IntSurfIdRep_c (26 B) is one 20-byte mapping record followed by
///     six zero bytes.
///
/// moFaceRef_c is present in blind extrusions, sweeps, revolves, fillets and
/// sheet-metal flanges, i.e. without any terminating-face selection: it is a
/// generated-face reference, not an UpToSurface selection.
/// </summary>
internal static class PersistentFaceReferenceParser
{
    internal const int HeaderLength = 81;
    internal const int FaceReferenceLength = 47;
    internal const int MappingStart = HeaderLength + FaceReferenceLength; // 128
    internal const int StubLength = 86;

    internal static CompFaceReferenceData? ReadCompFace(MfcDocument doc, MfcElement element)
    {
        if (doc == null || element == null || element.ClassName != "moCompFace_c") return null;
        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, element.DataStart, PayloadEnd(doc, element));
        return ReadCompFacePayload(raw);
    }

    internal static CompFaceReferenceData? ReadCompFacePayload(byte[] raw)
    {
        if (raw == null || raw.Length < StubLength) return null;
        if (!IsHeader(raw, 0, out byte flag)) return null;

        // Sheet-metal stub: 81-byte header + magic + 0x00.
        if (raw.Length == StubLength)
        {
            if (BitConverter.ToUInt32(raw, HeaderLength) != 0x4A38) return null;
            if (raw[HeaderLength + 4] != 0) return null;
            return new CompFaceReferenceData { Layout = CompFaceLayout.Stub };
        }

        var blockOffsets = new List<int> { 0 };
        for (int candidate = MappingStart; candidate <= raw.Length - HeaderLength; candidate++)
        {
            if (IsHeader(raw, candidate, out _)) blockOffsets.Add(candidate);
        }

        var blocks = new List<CompFaceBlock>();
        for (int b = 0; b < blockOffsets.Count; b++)
        {
            int blockStart = blockOffsets[b];
            int blockEnd = b + 1 < blockOffsets.Count ? blockOffsets[b + 1] : raw.Length;
            if (blockEnd - blockStart < MappingStart) return null;
            if (!TryReadFaceReference(raw, blockStart + HeaderLength, out var faceRef)) return null;

            int pos = blockStart + MappingStart;
            var mappings = ReadMappings(raw, pos, blockEnd, allowZeroEntityIndex: false, out int trailerStart);

            blocks.Add(new CompFaceBlock
            {
                BlockOffset = blockStart,
                BlockLength = blockEnd - blockStart,
                HeaderFlag = flagAt(raw, blockStart),
                FaceReference = faceRef,
                Mappings = mappings,
                TrailerOffset = trailerStart,
                TrailerLength = blockEnd - trailerStart
            });
        }
        if (blocks.Count == 0) return null;
        return new CompFaceReferenceData { Layout = CompFaceLayout.Full, Blocks = blocks.AsReadOnly() };
    }

    /// <summary>
    /// Parses the moSingleFaceRef_w terminating-face reference (473 bytes in the
    /// UpToSurface cut specimen):
    ///   +0    u16 class reference 0x8151
    ///   +2    81-byte component header (flag 0x40)
    ///   +83   47-byte face reference (magic, surface class, face id, archive id...)
    ///   +130  20-byte surface mapping records (first record entity index 0)
    ///   ...   trailer with null persist references
    /// </summary>
    internal static SingleFaceReferenceData? ReadSingleFaceRef(MfcDocument doc, MfcElement element)
    {
        if (doc == null || element == null || element.ClassName != "moSingleFaceRef_w") return null;
        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, element.DataStart, PayloadEnd(doc, element));
        return ReadSingleFaceRefPayload(raw);
    }

    internal static SingleFaceReferenceData? ReadSingleFaceRefPayload(byte[] raw)
    {
        // The 473-byte layout is the only controlled moSingleFaceRef_w specimen
        // (main and resave); truncations and unknown variants are rejected.
        if (raw == null || raw.Length != 473) return null;
        if (BitConverter.ToUInt16(raw, 0) != 0x8151) return null;
        if (!IsHeader(raw, 2, out byte flag)) return null;
        int faceRefOffset = 2 + HeaderLength; // 83
        if (!TryReadFaceReference(raw, faceRefOffset, out var faceRef)) return null;
        int mappingStart = faceRefOffset + FaceReferenceLength; // 130
        var mappings = ReadMappings(raw, mappingStart, raw.Length, allowZeroEntityIndex: true, out _);
        if (mappings.Count == 0) return null;
        return new SingleFaceReferenceData
        {
            HeaderFlag = flag,
            FaceReference = faceRef,
            Mappings = mappings
        };
    }

    /// <summary>
    /// Reads consecutive 20-byte mapping records starting at <paramref name="start"/>,
    /// allowing the observed 2-4 byte class-reference separators.
    /// </summary>
    private static IReadOnlyList<FromSktMappingEntry> ReadMappings(byte[] raw, int start, int end, bool allowZeroEntityIndex,
        out int trailerStart)
    {
        var mappings = new List<FromSktMappingEntry>();
        int pos = start;
        ushort expectedClassA = 0;
        ushort expectedClassB = 0;
        while (pos + 20 <= end)
        {
            if (FromSktEntSurfIdRepParser.TryReadMapping(raw, pos, out var entry, allowZeroEntityIndex))
            {
                if (mappings.Count == 0)
                {
                    expectedClassA = entry.ClassA;
                    expectedClassB = entry.ClassB;
                }
                mappings.Add(entry);
                pos += 20;
                continue;
            }
            // Mapping records can be separated from the first record by a 2-4 byte
            // class-reference gap (`00 00 f5 80` / `00 00 f1 80`).
            if (mappings.Count == 0) break;
            int next = -1;
            int limit = Math.Min(pos + 8, end - 20);
            for (int candidate = pos + 1; candidate <= limit; candidate++)
            {
                if (FromSktEntSurfIdRepParser.TryReadMapping(raw, candidate, out var nextEntry, allowZeroEntityIndex) &&
                    nextEntry.ClassA == expectedClassA && nextEntry.ClassB == expectedClassB)
                {
                    next = candidate;
                    break;
                }
            }
            if (next < 0) break;
            pos = next;
        }
        trailerStart = pos;
        return mappings.AsReadOnly();
    }

    /// <summary>Parses the 38-byte moFaceRef_c generated-face reference.</summary>
    internal static GeneratedFaceReferenceData? ReadFaceRef(MfcDocument doc, MfcElement element)
    {
        if (doc == null || element == null || element.ClassName != "moFaceRef_c") return null;
        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, element.DataStart, PayloadEnd(doc, element));
        return ReadFaceRefPayload(raw);
    }

    internal static GeneratedFaceReferenceData? ReadFaceRefPayload(byte[] raw)
    {
        if (raw == null || raw.Length != 38) return null;
        uint faceCount = BitConverter.ToUInt32(raw, 0);
        if (faceCount == 0 || faceCount > 1000) return null;
        if (BitConverter.ToUInt32(raw, 4) != 0) return null;
        uint topologyType = BitConverter.ToUInt32(raw, 8);
        if (topologyType == 0 || topologyType > 0x20) return null;
        uint classWord = BitConverter.ToUInt32(raw, 12);
        if (classWord != 0x200 && classWord != 0x300) return null;
        ulong idLow = BitConverter.ToUInt64(raw, 20);
        ulong idHigh = BitConverter.ToUInt64(raw, 28);
        if (idLow == 0 || idHigh == 0) return null;
        if (BitConverter.ToUInt16(raw, 36) != 0) return null;
        return new GeneratedFaceReferenceData
        {
            FaceCount = faceCount,
            TopologyType = topologyType,
            ClassWord = classWord,
            ClassSpecificId = BitConverter.ToUInt32(raw, 16),
            ArchiveIdLow = idLow,
            ArchiveIdHigh = idHigh,
            IsGeneratedFaceReference = true
        };
    }

    /// <summary>Parses the 14-byte moSurfaceIdRep_c record.</summary>
    internal static SurfaceReferenceRecordData? ReadSurfaceIdRepPayload(byte[] raw)
    {
        if (raw == null || raw.Length != 14) return null;
        ushort classA = BitConverter.ToUInt16(raw, 2);
        ushort classB = BitConverter.ToUInt16(raw, 4);
        uint featureClass = BitConverter.ToUInt32(raw, 6);
        if (classA < 0x8080 || classA > 0x81FF) return null;
        if (classB == 0) return null;
        if (featureClass < 0x40 || featureClass > 0x70) return null;
        return new SurfaceReferenceRecordData
        {
            Marker = BitConverter.ToUInt16(raw, 0),
            ClassA = classA,
            ClassB = classB,
            FeatureClass = featureClass,
            ArchiveSerial = BitConverter.ToUInt32(raw, 10)
        };
    }

    private static bool TryReadFaceReference(byte[] raw, int offset, out PersistentFaceReferenceData data)
    {
        data = null!;
        if (offset < 0 || offset + FaceReferenceLength > raw.Length) return false;
        if (BitConverter.ToUInt32(raw, offset) != 0x4A38) return false;
        if (raw[offset + 4] != 0) return false;
        ushort surfaceClass = BitConverter.ToUInt16(raw, offset + 5);
        if (surfaceClass < 0x8080 || surfaceClass > 0x81FF) return false;
        if (BitConverter.ToUInt32(raw, offset + 7) != 1) return false;
        if (BitConverter.ToUInt32(raw, offset + 11) != 0) return false;
        uint faceCount = BitConverter.ToUInt32(raw, offset + 15);
        if (faceCount == 0 || faceCount > 1000) return false;
        uint classWord = BitConverter.ToUInt32(raw, offset + 19);
        if (classWord != 0x200 && classWord != 0x300) return false;
        ulong idLow = BitConverter.ToUInt64(raw, offset + 27);
        ulong idHigh = BitConverter.ToUInt64(raw, offset + 35);
        if (idLow == 0 || idHigh == 0) return false;
        ushort mappingClass = BitConverter.ToUInt16(raw, offset + 45);
        if (mappingClass < 0x8080 || mappingClass > 0x81FF) return false;
        data = new PersistentFaceReferenceData
        {
            FaceCount = faceCount,
            ClassWord = classWord,
            FaceId = BitConverter.ToUInt32(raw, offset + 23),
            SurfaceClass = surfaceClass,
            MappingClassReference = mappingClass,
            ArchiveIdLow = idLow,
            ArchiveIdHigh = idHigh
        };
        return true;
    }

    private static byte flagAt(byte[] raw, int blockStart) => raw[blockStart + 6];

    /// <summary>
    /// Payload end bounded by the next class tag. MfcElement.DataEnd is unreliable
    /// for these payloads: the 16-byte 0xFF run in the header produces the
    /// end-marker byte pattern FF FF 00 00 00 00 and truncates the tree scope.
    /// </summary>
    internal static int PayloadEnd(MfcDocument doc, MfcElement element)
    {
        var elements = doc.AllElements;
        int index = -1;
        for (int i = 0; i < elements.Count; i++)
        {
            if (ReferenceEquals(elements[i], element)) { index = i; break; }
        }
        if (index < 0) return Math.Min(element.DataEnd, doc.SourceData.Length);
        return index + 1 < elements.Count ? elements[index + 1].TagOffset : doc.SourceData.Length;
    }

    internal static bool IsHeader(byte[] raw, int offset, out byte flag)
    {
        flag = 0;
        if (offset < 0 || offset + HeaderLength > raw.Length) return false;
        if (raw[offset] != 0x2C || raw[offset + 1] != 0x80 || raw[offset + 2] != 0x02 || raw[offset + 3] != 0x00)
            return false;
        if (raw[offset + 4] != 0 || raw[offset + 5] != 0 || raw[offset + 7] != 0) return false;
        if (raw[offset + 8] != 0x00 || raw[offset + 9] != 0x6A || raw[offset + 10] != 0 || raw[offset + 11] != 0)
            return false;
        if (raw[offset + 12] != 0x00 || raw[offset + 13] != 0x6A || raw[offset + 14] != 0 || raw[offset + 15] != 0)
            return false;
        for (int i = 16; i < 45; i++) if (raw[offset + i] != 0) return false;
        for (int i = 45; i < 61; i++) if (raw[offset + i] != 0xFF) return false;
        for (int i = 61; i < HeaderLength; i++) if (raw[offset + i] != 0) return false;
        flag = raw[offset + 6];
        return flag == 0x00 || flag == 0x40;
    }
}
