using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>
/// Decoder for the explicit selected-contour envelope established by the
/// whole/contour/region controlled samples (selection_dual_rect_v2/v3).
///
/// Byte evidence (Contents/Config-0-ResolvedFeatures, sample contour.SLDPRT):
///   moContourChooser_c   tag @ 10789, DataStart 10813, 18-byte payload:
///       u16 0 | u32 1 | u16 surface reference | u32 selected count |
///       u32 selected count + 1 | u16 1
///     single contour : 00 00 | 01 00 00 00 | BF 00 | 01 00 00 00 | 02 00 00 00 | 01 00
///     two contours   : 00 00 | 01 00 00 00 | C0 00 | 02 00 00 00 | 03 00 00 00 | 01 00
///     The payload is byte-identical for contour and contour_second (same count),
///     whose only difference is the chain entity-id list below. It therefore
///     encodes the selector kind/count, not the contour ordinal. The surface
///     reference at +6 is retained as opaque.
///   suObArray            tag @ 10831 (chooser.DataStart + 18), payload u16 = 1
///     (number of selected contours).
///   moSketchChain_c      tag @ 10848 (suObArray.DataStart + 2), payload:
///       u16 entityCount | entityCount * u32 chain entity ids |
///       u16 closed == 1 | u16 flag | ...
///     contour        -> count 4, ids [1, 4, 3, 2]  (native SketchContour ids)
///     contour_second -> count 4, ids [5, 8, 7, 6]  (native SketchContour ids)
///
/// Multi-contour selection (SelectionAuthoring/select_two_contours, stream
/// version 0x71): suObArray count = 2 and the chain payload contains two
/// records separated by 00 00 F3 80, terminated by
/// 00 00 01 00 00 00 01 00. Chain order matches the native
/// ISweepFeatureData/IExtrudeFeatureData contour array order
/// ([5,8,7,6] then [1,4,3,2] for that specimen). Records are 60 + 4*count
/// bytes; the u16 at closedOffset+36 is the 1-based chain index.
///
/// A suObArray count that does not match the number of decoded records, an
/// unknown separator or a malformed record is reported as unresolved instead
/// of being guessed.
/// </summary>
internal static class SelectedSketchContourParser
{
    /// <summary>Number of bytes in the explicit contour chooser envelope.</summary>
    internal const int ChooserEnvelopeLength = 18;

    internal const int MaxSelectedContours = 32;

    /// <summary>Separator between explicit multi-contour chain records (00 00 F3 80).</summary>
    private static readonly byte[] MultiContourSeparator = { 0x00, 0x00, 0xF3, 0x80 };

    /// <summary>Terminator after the last explicit chain record (00 00 01 00 00 00 01 00).</summary>
    private static readonly byte[] ChainTerminator = { 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00 };

    internal static IReadOnlyList<SketchContourReference> Read(MfcDocument doc, int sketchStart, int extrusionStart)
    {
        var empty = Array.Empty<SketchContourReference>();
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
    /// Decodes one explicit contour chooser. Returns an empty list when the
    /// payload does not match the structurally established single-contour
    /// envelope or when the adjacent suObArray/chain objects are missing.
    /// </summary>
    internal static IReadOnlyList<SketchContourReference> TryRead(MfcDocument doc, MfcElement chooser)
    {
        var empty = Array.Empty<SketchContourReference>();
        if (doc == null || chooser == null) return empty;
        var source = doc.SourceData;
        if (!IsChooserEnvelope(source, chooser, out int envelopeCount)) return empty;

        int index = IndexOf(doc, chooser);
        if (index < 0 || index + 2 >= doc.AllElements.Count) return empty;
        var array = doc.AllElements[index + 1];
        var chain = doc.AllElements[index + 2];
        if (array.ClassName != "suObArray" || array.TagOffset != chooser.DataStart + ChooserEnvelopeLength)
            return empty;
        if (chain.ClassName != "moSketchChain_c" || chain.TagOffset != array.DataStart + 2)
            return empty;

        // suObArray payload is the selected-contour count (SelectionAuthoring
        // select_two_contours specimen: 2). Each contour is one chain record.
        if (array.DataStart + 2 > source.Length) return empty;
        ushort selectedCount = BitConverter.ToUInt16(source, array.DataStart);
        if (selectedCount == 0 || selectedCount > MaxSelectedContours || selectedCount != envelopeCount)
            return empty;

        int chainEnd = index + 3 < doc.AllElements.Count
            ? doc.AllElements[index + 3].TagOffset
            : Math.Min(chooser.DataEnd, source.Length);
        if (chainEnd <= chain.DataStart || chainEnd > source.Length) return empty;
        byte[] raw = MfcPrimitives.CopyRange(source, chain.DataStart, chainEnd);
        if (raw.Length < 2) return empty;

        var contours = new List<SketchContourReference>(selectedCount);
        int cursor = 0;
        for (int record = 0; record < selectedCount; record++)
        {
            if (!TryReadChainRecord(raw, ref cursor, record + 1, out uint[] ids)) return empty;
            contours.Add(new SketchContourReference { EntityIndices = ids, IsClosed = true });
            if (record + 1 < selectedCount)
            {
                // Explicit multi-contour records are separated by 00 00 F3 80
                // (select_two_contours specimen, chain length 164 for two 4-edge
                // chains: body 76 + separator 4 + body 76 + terminator 8).
                if (cursor + MultiContourSeparator.Length > raw.Length) return empty;
                for (int i = 0; i < MultiContourSeparator.Length; i++)
                    if (raw[cursor + i] != MultiContourSeparator[i]) return empty;
                cursor += MultiContourSeparator.Length;
            }
        }

        // Terminator observed after the last chain record.
        if (cursor + ChainTerminator.Length > raw.Length) return empty;
        for (int i = 0; i < ChainTerminator.Length; i++)
            if (raw[cursor + i] != ChainTerminator[i]) return empty;
        cursor += ChainTerminator.Length;
        if (cursor != raw.Length) return empty;
        return contours;
    }

    /// <summary>
    /// Reads one 76-byte (for a 4-edge loop) chain record:
    /// u16 count | count * u32 ids | u16 closed == 1 | u16 flag (0 first,
    /// 1 for later records) | u16 0 | u32 sketch id | 3 x u32 FFFFFFFF |
    /// u32 5 | u32 5 | empty WChar | u16 0 | u16 chain index (1-based) |
    /// 20 zero bytes. Body length = 60 + 4 * count.
    /// </summary>
    private static bool TryReadChainRecord(byte[] raw, ref int cursor, int expectedIndex, out uint[] ids)
    {
        ids = Array.Empty<uint>();
        if (cursor < 0 || cursor + 2 > raw.Length) return false;
        int count = BitConverter.ToUInt16(raw, cursor);
        if (count == 0 || count > 4096) return false;
        int bodyLength = 60 + 4 * count;
        if (cursor + bodyLength > raw.Length) return false;
        int closedOffset = cursor + 2 + count * 4;
        if (BitConverter.ToUInt16(raw, closedOffset) != 1) return false;
        int indexOffset = closedOffset + 36;
        if (indexOffset + 2 > cursor + bodyLength) return false;
        if (BitConverter.ToUInt16(raw, indexOffset) != expectedIndex) return false;
        var idsRead = new uint[count];
        for (int i = 0; i < count; i++)
        {
            idsRead[i] = BitConverter.ToUInt32(raw, cursor + 2 + i * 4);
            if (idsRead[i] == 0) return false;
        }
        if (idsRead.Distinct().Count() != count) return false;
        ids = idsRead;
        cursor += bodyLength;
        return true;
    }

    /// <summary>
    /// Checks the 18-byte explicit-contour chooser envelope:
    ///   u16 0 | u32 1 | u16 surface reference (opaque, 0xBF single / 0xC0 multi) |
    ///   u32 selected contour count | u32 count + 1 | u16 1.
    /// Region choosers are much larger and start with u16 = 1 followed by class
    /// reference 0x80EC/0x80EE, so they never match this signature.
    /// </summary>
    internal static bool IsChooserEnvelope(byte[] source, MfcElement chooser, out int selectedCount)
    {
        selectedCount = 0;
        int start = chooser.DataStart;
        if (start < 0 || start + ChooserEnvelopeLength > source.Length) return false;
        if (start + ChooserEnvelopeLength > chooser.DataEnd) return false;
        byte[] prefix = MfcPrimitives.CopyRange(source, start, start + ChooserEnvelopeLength);
        if (prefix.Length != ChooserEnvelopeLength || BitConverter.ToUInt16(prefix, 0) != 0) return false;
        if (BitConverter.ToUInt32(prefix, 2) != 1) return false;
        uint count = BitConverter.ToUInt32(prefix, 8);
        if (count == 0 || count > MaxSelectedContours) return false;
        if (BitConverter.ToUInt32(prefix, 12) != count + 1) return false;
        if (BitConverter.ToUInt16(prefix, 16) != 1) return false;
        // The u16 at +6 is the surface reference (0xBF single, 0xC0 multi) and is
        // retained as opaque; it is not a selected-contour identifier.
        selectedCount = (int)count;
        return true;
    }

    private static int IndexOf(MfcDocument doc, MfcElement element)
    {
        if (element.Index >= 0 && element.Index < doc.AllElements.Count &&
            ReferenceEquals(doc.AllElements[element.Index], element))
            return element.Index;
        for (int i = 0; i < doc.AllElements.Count; i++)
        {
            if (ReferenceEquals(doc.AllElements[i], element)) return i;
        }
        return -1;
    }
}
