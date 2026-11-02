using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.Topology;

/// <summary>One 20-byte moFromSktEntSurfIdRep_c mapping record.</summary>
internal sealed class FromSktMappingEntry
{
    /// <summary>u16 at +2 (class reference; high byte 0x80/0x81; varies by feature instance).</summary>
    public ushort ClassA { get; set; }

    /// <summary>u16 at +4 (paired class reference).</summary>
    public ushort ClassB { get; set; }

    /// <summary>
    /// u32 at +6: serialized feature class (0x4F extrusion, 0x5E sheet-metal base
    /// flange). Matches ExtrudeFeatureDefinitionData.TypeId.
    /// </summary>
    public uint FeatureClass { get; set; }

    /// <summary>u32 at +10: per-feature archive serial.</summary>
    public uint ArchiveSerial { get; set; }

    /// <summary>u32 at +14: sketch entity index (1-based, same id space as moSketchChain_c).</summary>
    public uint EntityIndex { get; set; }

    /// <summary>u16 at +18: surface class tag (0 on the last entry).</summary>
    public ushort ClassTag { get; set; }

    public override string ToString() =>
        $"FromSktEntry(Entity={EntityIndex}, FeatureClass=0x{FeatureClass:X2}, Serial=0x{ArchiveSerial:X8}, Tag=0x{ClassTag:X4})";
}

/// <summary>Decoded moFromSktEntSurfIdRep_c payload.</summary>
internal sealed class FromSktSelectionData
{
    public uint FeatureClass { get; set; }

    public ushort EntryClassA { get; set; }

    public ushort EntryClassB { get; set; }

    /// <summary>Leading mapping list (one entry per entity of the selected loop).</summary>
    public IReadOnlyList<FromSktMappingEntry> PrimaryMappings { get; set; } = Array.Empty<FromSktMappingEntry>();

    /// <summary>u32 sketch/feature id of the persist reference after the mapping list; 0 in every specimen.</summary>
    public uint ParentReferenceId { get; set; }

    /// <summary>True when the persist reference after the mapping list is the null reference (id 0).</summary>
    public bool ParentReferenceIsNull => ParentReferenceId == 0;

    /// <summary>Class reference that introduces the secondary (sketch-wide) list.</summary>
    public ushort TailClassReference { get; set; }

    /// <summary>Secondary list mappings; first entry may carry the 0x86A0 profile marker.</summary>
    public IReadOnlyList<FromSktMappingEntry> TailMappings { get; set; } = Array.Empty<FromSktMappingEntry>();

    /// <summary>True when the primary mapping entity set equals the given ids (order-insensitive).</summary>
    public bool MatchesEntitySet(IEnumerable<uint> ids)
    {
        if (ids == null) return false;
        var a = PrimaryMappings.Select(m => m.EntityIndex).Distinct().OrderBy(x => x).ToArray();
        var b = ids.Distinct().OrderBy(x => x).ToArray();
        return a.Length > 0 && a.SequenceEqual(b);
    }

    /// <summary>True when the primary entity ids equal the chain record ids in reverse order.</summary>
    public bool MatchesReversedChain(IReadOnlyList<uint> chainIds)
    {
        if (chainIds == null || chainIds.Count != PrimaryMappings.Count) return false;
        for (int i = 0; i < chainIds.Count; i++)
            if (PrimaryMappings[i].EntityIndex != chainIds[chainIds.Count - 1 - i]) return false;
        return true;
    }
}

/// <summary>
/// Decoder for moFromSktEntSurfIdRep_c.
///
/// Specimens: ExtrudeOptions, SelectionModes, SelectionAuthoring, Fillet and
/// SheetMetalBaseFlange samples. Payload structure (270 bytes for a 4-edge loop,
/// 724 bytes for the 6-edge L-shape):
///
///   +0    N * 20-byte mapping records (N = entity count of the selected loop):
///           u16 0 | u16 classA | u16 classB | u32 feature class (0x4F/0x5E) |
///           u32 archive serial | u32 entity index | u16 surface class tag
///         The last record's class tag is 0.
///   +20N  8 zero bytes
///   +20N+8  persist-reference magic 38 4A 00 00 followed by u32 id
///         -> id is 0 in every observed specimen (NULL parent reference). No
///         sketch persistent id is stored in this object.
///   +20N+16 u32 0, u16 tail class reference (e.g. 0x8114/0x81BD/0x8169),
///         then a header and a secondary list of the same 20-byte records
///         (sketch-wide; the first secondary entry may carry tag 0x86A0).
///
/// Entity mapping evidence:
///   - extrusion rectangle specimen: chain ids [1,2,3,4]; primary mappings
///     [4,3,2,1] (reverse chain order);
///   - contour: chain [1,4,3,2], primary [2,3,4,1]; contour_second chain
///     [5,8,7,6], primary [6,7,8,5];
///   - region specimens: primary set equals the region chooser boundary set
///     (L-shape {3,4,5,6,7,8}, overlap {3,4,5,6});
///   - whole sketch and explicit contour produce the same 270-byte layout with
///     the same primary entity list (only class refs/serial differ), so this
///     object neither proves nor rules out an explicit selection.
///
/// Any layout mismatch returns null; nothing is guessed.
/// </summary>
internal static class FromSktEntSurfIdRepParser
{
    internal const int MappingRecordLength = 20;
    private const int MaxMappings = 4096;
    private const uint MinFeatureClass = 0x40;
    private const uint MaxFeatureClass = 0x70;

    private static readonly uint[] KnownFeatureClasses = { 0x4F, 0x56, 0x5E };

    internal static FromSktSelectionData? Read(MfcDocument doc, MfcElement element)
    {
        if (doc == null || element == null || element.ClassName != "moFromSktEntSurfIdRep_c") return null;
        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, element.DataStart,
            Math.Min(element.DataEnd, doc.SourceData.Length));
        return ReadPayload(raw);
    }

    internal static FromSktSelectionData? ReadPayload(byte[] raw)
    {
        if (raw == null || raw.Length < MappingRecordLength) return null;
        var primary = new List<FromSktMappingEntry>();
        int pos = 0;
        while (pos + MappingRecordLength <= raw.Length)
        {
            if (!TryReadMapping(raw, pos, out var entry)) break;
            primary.Add(entry);
            pos += MappingRecordLength;
            if (primary.Count > MaxMappings) return null;
        }
        if (primary.Count == 0) return null;

        ushort entryClassA = primary[0].ClassA;
        ushort entryClassB = primary[0].ClassB;
        for (int i = 1; i < primary.Count; i++)
        {
            if (primary[i].ClassA != entryClassA || primary[i].ClassB != entryClassB) return null;
            if (primary[i].FeatureClass != primary[0].FeatureClass) return null;
        }
        if (!KnownFeatureClasses.Contains(primary[0].FeatureClass) &&
            (primary[0].FeatureClass < MinFeatureClass || primary[0].FeatureClass > MaxFeatureClass))
            return null;

        // 8 zero bytes, then a persist reference whose id must be decodable.
        if (pos + 16 > raw.Length) return null;
        for (int i = 0; i < 8; i++) if (raw[pos + i] != 0) return null;
        int refOffset = pos + 8;
        if (BitConverter.ToUInt32(raw, refOffset) != 0x4A38) return null;
        uint parentId = BitConverter.ToUInt32(raw, refOffset + 4);

        var data = new FromSktSelectionData
        {
            FeatureClass = primary[0].FeatureClass,
            EntryClassA = entryClassA,
            EntryClassB = entryClassB,
            PrimaryMappings = primary.AsReadOnly(),
            ParentReferenceId = parentId
        };

        // Optional secondary list: u32 0 + u16 class ref at refOffset+12, then
        // records with the same class pair. The first secondary record may carry
        // the 0x86A0 profile marker; later ones carry the primary class tag.
        int tailHeader = refOffset + 8;
        if (tailHeader + 6 <= raw.Length)
        {
            for (int i = 0; i < 4; i++) if (raw[tailHeader + i] != 0) return data;
            data.TailClassReference = BitConverter.ToUInt16(raw, tailHeader + 4);
            var tail = new List<FromSktMappingEntry>();
            for (int scan = tailHeader + 6; scan + MappingRecordLength <= raw.Length; scan++)
            {
                if (!TryReadMapping(raw, scan, out var entry)) continue;
                if (entry.ClassA != entryClassA || entry.ClassB != entryClassB) continue;
                if (entry.FeatureClass != data.FeatureClass) continue;
                tail.Add(entry);
                scan += MappingRecordLength - 1;
            }
            data.TailMappings = tail.AsReadOnly();
        }
        return data;
    }

    internal static bool TryReadMapping(byte[] raw, int pos, out FromSktMappingEntry entry,
        bool allowZeroEntityIndex = false)
    {
        entry = null!;
        if (pos < 0 || pos + MappingRecordLength > raw.Length) return false;
        if (BitConverter.ToUInt16(raw, pos) != 0) return false;
        ushort classA = BitConverter.ToUInt16(raw, pos + 2);
        ushort classB = BitConverter.ToUInt16(raw, pos + 4);
        if (classA < 0x8080 || classA > 0x81FF) return false;
        if (classB == 0) return false;
        uint featureClass = BitConverter.ToUInt32(raw, pos + 6);
        if (featureClass < MinFeatureClass || featureClass > MaxFeatureClass) return false;
        uint entityIndex = BitConverter.ToUInt32(raw, pos + 14);
        if (entityIndex > 1_000_000 || (!allowZeroEntityIndex && entityIndex == 0)) return false;
        entry = new FromSktMappingEntry
        {
            ClassA = classA,
            ClassB = classB,
            FeatureClass = featureClass,
            ArchiveSerial = BitConverter.ToUInt32(raw, pos + 10),
            EntityIndex = entityIndex,
            ClassTag = BitConverter.ToUInt16(raw, pos + 18)
        };
        return true;
    }
}
