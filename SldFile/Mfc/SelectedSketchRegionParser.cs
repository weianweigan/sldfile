using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>
/// Decoder for explicit selected-region choosers.
///
/// Established specimens:
///   - SelectionModes/region + selection_dual_rect_v2/v3 region/region_second
///     (rectangles, 4 boundary edges, prefix class refs 0x80EC/0x80EE,
///      variant bytes 04 03, "legacy verbose" units);
///   - SelectionAuthoring/select_region (L-shape, 6 edges, prefix 0x80EE/0x80F0,
///     04 03, "extended verbose" units) and its resave ("compact" units);
///   - SelectionAuthoring/select_region_overlap (4 edges, prefix 0x80EE/0x80F0,
///     06 03, extended verbose) and its resave (compact).
///
/// Payload prefix (46 bytes), common to every variant:
///   +0  u16 selected region count (1)
///   +2  u16 0x80EC (legacy) or 0x80EE (extended)
///   +4  u16 boundary edge count N
///   +6  u16 0x80EE (legacy) or 0x80F0 (extended)
///   +8  u16 1; +10 u32 0; +14 u16 0; +16 u32 5
///   +20 bytes 04 03 (legacy/L-shape) or 06 03 (overlap)
///   +22 6 zero bytes; +28/+36 equal non-zero archive ids; +44 u16 0
///
/// The selection layer is N groups of four unit records (1 start unit with edge
/// field 0 + 3 edge units). Unit grammars:
///   legacy verbose   class F0 (start) / F9,FD (edge), +2..+14
///                    00 00 F2 80 F4 80 F7 00 F8 00 02 00 00, edge u32 @+81,
///                    lengths 87/89; groups separated by 56 bytes.
///   extended verbose class F2 (start) / FB,FF (edge), +2..+14
///                    00 00 F4 80 F6 80 F9 00 FA 00 02 00 00, edge u32 @+81,
///                    lengths 87/89; groups separated by 56 bytes
///                    (8-byte magic 38 4A 00 00 00 00 00 00 + 40-byte prefix tail).
///   compact          class F2 (start) / FB,FE,FF (edge), +2..+8
///                    00 00 F4 80 F7 00, u32 sketch id @+8, u32 serial @+12,
///                    edge u32 @+16, lengths 22/24; groups separated by 56 bytes.
///
/// Differential evidence for the union rule:
///   L-shape  (6 edges) unions to {3,4,5,6,7,8} = native edge endpoints;
///   overlap  (4 edges) unions to {3,4,5,6} = native edge endpoints;
///   rectangles union to the chain id set of the selected rectangle.
/// The selected region ordinal/handle and loop count are not persisted.
/// Unknown layouts are rejected instead of guessed.
/// </summary>
internal static class SelectedSketchRegionParser
{
    internal const int PrefixLength = 46;
    private const int MaxBoundaryEdges = 64;
    private const int UnitsPerGroup = 4;
    private const int MaxGroupGap = 112;

    private static readonly byte[] LegacyVerboseSignature =
        { 0x00, 0x00, 0xF2, 0x80, 0xF4, 0x80, 0xF7, 0x00, 0xF8, 0x00, 0x02, 0x00, 0x00 };

    private static readonly byte[] ExtendedVerboseSignature =
        { 0x00, 0x00, 0xF4, 0x80, 0xF6, 0x80, 0xF9, 0x00, 0xFA, 0x00, 0x02, 0x00, 0x00 };

    private static readonly byte[] CompactSignature =
        { 0x00, 0x00, 0xF4, 0x80, 0xF7, 0x00 };

    private static readonly byte[] UnitEmptyStrings =
        { 0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00 };

    private static readonly byte[] UnitConstant =
        { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0x02, 0xD8, 0x9E, 0xA4, 0x8B };

    private enum UnitLayout
    {
        None = 0,
        LegacyVerbose = 1,
        ExtendedVerbose = 2,
        Compact = 3
    }

    internal static IReadOnlyList<SketchRegionReference> Read(MfcDocument doc, int sketchStart, int extrusionStart)
    {
        var empty = Array.Empty<SketchRegionReference>();
        if (doc == null) return empty;
        var source = doc.SourceData;
        ushort streamVersion = source.Length >= 2 ? BitConverter.ToUInt16(source, 0) : (ushort)0;
        if ((streamVersion != 0x70 && streamVersion != 0x71) ||
            sketchStart < 0 || extrusionStart <= sketchStart || extrusionStart > source.Length) return empty;
        var choosers = doc.AllElements
            .Where(e => e.ClassName == "moContourChooser_c" &&
                        e.TagOffset >= sketchStart && e.TagOffset < extrusionStart)
            .OrderBy(e => e.TagOffset)
            .ToList();
        if (choosers.Count != 1) return empty;
        return TryRead(doc, choosers[0]);
    }

    /// <summary>
    /// Decodes one region chooser payload. Returns a single region reference with
    /// the boundary chain entity set, or an empty list when the payload does not
    /// match an established variant.
    /// </summary>
    internal static IReadOnlyList<SketchRegionReference> TryRead(MfcDocument doc, MfcElement chooser)
    {
        var empty = Array.Empty<SketchRegionReference>();
        if (doc == null || chooser == null) return empty;
        var source = doc.SourceData;
        int start = chooser.DataStart;
        int end = Math.Min(chooser.DataEnd, source.Length);
        if (start < 0 || end - start < PrefixLength) return empty;
        byte[] raw = MfcPrimitives.CopyRange(source, start, end);
        return TryReadPayload(raw);
    }

    internal static IReadOnlyList<SketchRegionReference> TryReadPayload(byte[] raw)
    {
        var empty = Array.Empty<SketchRegionReference>();
        if (!TryValidatePrefix(raw, out int edgeCount)) return empty;
        if (!TryReadSelectionGroups(raw, edgeCount, collectFields: false,
                out var edges, out _, out _)) return empty;

        var distinct = edges.Distinct().OrderBy(x => x).ToArray();
        if (distinct.Length != edgeCount) return empty;

        return new[]
        {
            new SketchRegionReference
            {
                EntityId = 0,      // Persistent region handle is not stored in this block.
                RegionIndex = 0,   // Region ordinal is not persisted; boundary edges identify it.
                LoopCount = 0,     // Loop count layout is not established by the specimens.
                EdgeIds = distinct
            }
        };
    }

    /// <summary>
    /// Locates the selection group layout for re-emission: returns the number of
    /// groups and, for each group, the absolute offsets of the three edge-id u32
    /// fields. Used by <see cref="SketchSelectionWriter"/> to patch native
    /// chooser payloads while preserving the sketch-wide tail.
    /// </summary>
    internal static bool TryLocateSelectionGroups(byte[] raw, out int groupCount,
        out IReadOnlyList<int[]> edgeFieldOffsets)
    {
        groupCount = 0;
        edgeFieldOffsets = Array.Empty<int[]>();
        if (!TryValidatePrefix(raw, out int edgeCount)) return false;
        var groups = new List<int[]>();
        if (!TryReadSelectionGroups(raw, edgeCount, collectFields: true,
                out _, out _, out groups)) return false;
        groupCount = edgeCount;
        edgeFieldOffsets = groups.AsReadOnly();
        return true;
    }

    /// <summary>
    /// Same as <see cref="TryLocateSelectionGroups"/> but also returns each group's
    /// three edge ids in serialized order. Native group order follows:
    ///   group g = [ e[(N-g) mod N], e[(N-2-g) mod N], e[(N-1-g) mod N] ]
    /// where e is the boundary loop traversal order.
    /// </summary>
    internal static bool TryLocateSelectionGroupsWithEdges(byte[] raw, out int groupCount,
        out IReadOnlyList<int[]> edgeFieldOffsets, out IReadOnlyList<uint[]> groupEdges)
    {
        groupCount = 0;
        edgeFieldOffsets = Array.Empty<int[]>();
        groupEdges = Array.Empty<uint[]>();
        if (!TryValidatePrefix(raw, out int edgeCount)) return false;
        var fields = new List<int[]>();
        var groupsEdges = new List<uint[]>();
        if (!TryReadSelectionGroups(raw, edgeCount, collectFields: true,
                out _, out _, out fields, groupsEdges)) return false;
        groupCount = edgeCount;
        edgeFieldOffsets = fields.AsReadOnly();
        groupEdges = groupsEdges.AsReadOnly();
        return true;
    }

    private static bool TryValidatePrefix(byte[] raw, out int edgeCount)
    {
        edgeCount = 0;
        if (raw == null || raw.Length < PrefixLength) return false;
        if (BitConverter.ToUInt16(raw, 0) != 1) return false;          // selected region count
        ushort class1 = BitConverter.ToUInt16(raw, 2);
        if (class1 != 0x80EC && class1 != 0x80EE) return false;
        int count = BitConverter.ToUInt16(raw, 4);
        if (count < 2 || count > MaxBoundaryEdges) return false;
        ushort class2 = BitConverter.ToUInt16(raw, 6);
        if (class2 != 0x80EE && class2 != 0x80F0) return false;
        if (BitConverter.ToUInt16(raw, 8) != 1) return false;
        if (BitConverter.ToUInt32(raw, 10) != 0) return false;
        if (BitConverter.ToUInt16(raw, 14) != 0) return false;
        if (BitConverter.ToUInt32(raw, 16) != 5) return false;
        bool variant0403 = raw[20] == 0x04 && raw[21] == 0x03;
        bool variant0603 = raw[20] == 0x06 && raw[21] == 0x03;
        if (!variant0403 && !variant0603) return false;
        for (int i = 22; i < 28; i++) if (raw[i] != 0) return false;
        ulong archiveId1 = BitConverter.ToUInt64(raw, 28);
        ulong archiveId2 = BitConverter.ToUInt64(raw, 36);
        if (archiveId1 == 0 || archiveId1 != archiveId2) return false;
        if (raw[44] != 0 || raw[45] != 0) return false;
        edgeCount = count;
        return true;
    }

    private static bool TryReadSelectionGroups(byte[] raw, int edgeCount, bool collectFields,
        out List<uint> edges, out int consumed, out List<int[]> fields, List<uint[]>? groupEdges = null)
    {
        edges = new List<uint>(edgeCount * UnitsPerGroup);
        fields = new List<int[]>(edgeCount);
        consumed = 0;
        int pos = PrefixLength;
        UnitLayout layout = UnitLayout.None;
        for (int group = 0; group < edgeCount; group++)
        {
            if (!TryMatchUnit(raw, pos, out UnitLayout found, out bool isStart, out uint edge, out int length))
                return false;
            if (layout == UnitLayout.None) layout = found;
            if (found != layout) return false;
            if (!isStart || edge != 0) return false;
            pos += length;

            int fieldOffset = layout == UnitLayout.Compact ? 16 : 81;
            var groupFields = collectFields ? new int[UnitsPerGroup - 1] : null;
            var groupEdgeValues = groupEdges != null ? new uint[UnitsPerGroup - 1] : null;
            for (int k = 1; k < UnitsPerGroup; k++)
            {
                int unitStart = pos;
                if (!TryMatchUnit(raw, pos, out UnitLayout edgeLayout, out bool edgeIsStart, out edge, out length))
                    return false;
                if (edgeLayout != layout || edgeIsStart || edge == 0) return false;
                edges.Add(edge);
                if (groupFields != null) groupFields[k - 1] = unitStart + fieldOffset;
                if (groupEdgeValues != null) groupEdgeValues[k - 1] = edge;
                pos += length;
            }
            if (groupFields != null) fields.Add(groupFields);
            if (groupEdgeValues != null) groupEdges!.Add(groupEdgeValues);

            if (group < edgeCount - 1)
            {
                int next = FindNextStartUnit(raw, pos, layout);
                if (next < 0) return false;
                pos = next;
            }
        }
        consumed = pos;
        return true;
    }

    private static int FindNextStartUnit(byte[] raw, int from, UnitLayout layout)
    {
        int limit = Math.Min(from + MaxGroupGap, raw.Length);
        for (int candidate = from; candidate < limit; candidate++)
        {
            if (!TryMatchUnit(raw, candidate, out UnitLayout found, out bool isStart, out uint edge, out _))
                continue;
            if (found == layout && isStart && edge == 0) return candidate;
        }
        return -1;
    }

    private static bool TryMatchUnit(byte[] raw, int pos, out UnitLayout layout, out bool isStart,
        out uint edge, out int length)
    {
        layout = UnitLayout.None;
        isStart = false;
        edge = 0;
        length = 0;
        if (pos < 0 || pos + 24 > raw.Length) return false;

        ushort cls = BitConverter.ToUInt16(raw, pos);
        if (MatchesSignature(raw, pos, LegacyVerboseSignature) &&
            (cls == 0x80F0 || cls == 0x80F9 || cls == 0x80FD))
        {
            isStart = cls == 0x80F0;
            length = isStart ? 87 : 89;
            if (!TryReadVerboseUnit(raw, pos, length, serialDelta: 1, out edge)) return false;
            layout = UnitLayout.LegacyVerbose;
            return true;
        }
        if (MatchesSignature(raw, pos, ExtendedVerboseSignature) &&
            (cls == 0x80F2 || cls == 0x80FB || cls == 0x80FF))
        {
            isStart = cls == 0x80F2;
            length = isStart ? 87 : 89;
            if (!TryReadVerboseUnit(raw, pos, length, serialDelta: 2, out edge)) return false;
            layout = UnitLayout.ExtendedVerbose;
            return true;
        }
        if (MatchesSignature(raw, pos, CompactSignature) &&
            (cls == 0x80F2 || cls == 0x80FB || cls == 0x80FE || cls == 0x80FF))
        {
            isStart = cls == 0x80F2;
            length = isStart ? 22 : 24;
            if (pos + length > raw.Length) return false;
            uint sketchId = BitConverter.ToUInt32(raw, pos + 8);
            if (sketchId == 0) return false;
            edge = BitConverter.ToUInt32(raw, pos + 16);
            int tail = isStart ? 2 : 4;
            for (int i = 20; i < 20 + tail; i++) if (raw[pos + i] != 0) return false;
            layout = UnitLayout.Compact;
            return true;
        }
        return false;
    }

    private static bool MatchesSignature(byte[] raw, int pos, byte[] signature)
    {
        if (pos + 2 + signature.Length > raw.Length) return false;
        for (int i = 0; i < signature.Length; i++)
            if (raw[pos + 2 + i] != signature[i]) return false;
        return true;
    }

    /// <summary>
    /// Validates the verbose unit template (serial at +15, serial+1 at +77,
    /// 0x49 at +73, three empty WChar strings, constant block, zero runs, tail).
    /// </summary>
    private static bool TryReadVerboseUnit(byte[] raw, int pos, int length, int serialDelta, out uint edge)
    {
        edge = 0;
        if (pos + length > raw.Length) return false;
        uint serial = BitConverter.ToUInt32(raw, pos + 15);
        for (int i = 0; i < UnitEmptyStrings.Length; i++)
            if (raw[pos + 19 + i] != UnitEmptyStrings[i]) return false;
        for (int i = 31; i < 45; i++) if (raw[pos + i] != 0) return false;
        for (int i = 0; i < UnitConstant.Length; i++)
            if (raw[pos + 45 + i] != UnitConstant[i]) return false;
        for (int i = 57; i < 73; i++) if (raw[pos + i] != 0) return false;
        if (BitConverter.ToUInt32(raw, pos + 73) != 0x49) return false;
        if (BitConverter.ToUInt32(raw, pos + 77) != unchecked(serial + (uint)serialDelta)) return false;
        edge = BitConverter.ToUInt32(raw, pos + 81);
        int tail = length == 87 ? 2 : 4;
        for (int i = 85; i < 85 + tail; i++) if (raw[pos + i] != 0) return false;
        return true;
    }
}
