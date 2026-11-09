using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>
/// Decoded trim/reference suffix that follows an explicit contour selection block
/// (SelectionAuthoring native specimens and SelectionContours/contour):
///
///   moSimpleSurfIdRep_c             2 bytes: u16 surface reference
///                                   (0xBF single-contour, 0xC0 two-contour)
///   moFR_c                          empty payload
///   moExtObject_c                   empty payload
///   moCStringHandle_c               WChar source path + opaque handle tail
///   moSegmLeftTrimEdgeIdRep_c       22-byte records separated by 2-byte class
///                                   references (0x80FD/0x8102/0x8104/0x80FF...)
///   moSegmRightTrimEdgeIdRep_c      same record grammar, six records for the
///                                   dual-rectangle sketch (all sketch edges)
///
/// Trim record (22 bytes): u16 surface marker, u16 0x80F6/0x80F8, u16 0x00F9/
/// 0x00FB, u32 sketch id, u32 archive serial, u32 edge id, u32 0.
/// The trim payloads are sketch-wide: contour and contour_second produce
/// byte-identical left/right trim payloads for the same sketch. This decoder
/// therefore preserves the raw payloads for re-emission (native rebuild requires
/// them; an envelope-only block raises RPC_E_SERVERFAULT).
/// </summary>
internal sealed class SketchContourSuffixData
{
    public ushort SurfaceId { get; set; }

    public byte[] FrPayload { get; set; } = Array.Empty<byte>();

    public byte[] ExtObjectPayload { get; set; } = Array.Empty<byte>();

    public byte[] CStringHandlePayload { get; set; } = Array.Empty<byte>();

    /// <summary>Source document path embedded in moCStringHandle_c (null when absent/unreadable).</summary>
    public string? SourceDocumentPath { get; set; }

    /// <summary>Opaque bytes after the WChar path in moCStringHandle_c.</summary>
    public byte[] CStringHandleTail { get; set; } = Array.Empty<byte>();

    public byte[] LeftTrimPayload { get; set; } = Array.Empty<byte>();

    public byte[] RightTrimPayload { get; set; } = Array.Empty<byte>();

    /// <summary>Sketch id and archive serial decoded from the first trim record (evidence view).</summary>
    public uint SketchId { get; set; }

    public uint ArchiveSerial { get; set; }

    public IReadOnlyList<uint> LeftTrimEdges { get; set; } = Array.Empty<uint>();

    public IReadOnlyList<uint> RightTrimEdges { get; set; } = Array.Empty<uint>();

    public bool IsValid =>
        LeftTrimPayload.Length > 0 && RightTrimPayload.Length > 0 &&
        SniffTrimEdges(LeftTrimPayload).Count > 0 && SniffTrimEdges(RightTrimPayload).Count > 0;

    /// <summary>
    /// Returns the moCStringHandle_c payload that may be emitted into a document.
    /// The embedded source path is kept only when the target document is the same
    /// file; otherwise it is rewritten to the target path. A missing embedded path
    /// or a missing/relative target path is rejected (never emit a foreign path).
    /// </summary>
    internal byte[] PrepareCStringHandlePayload(string? targetDocumentPath)
    {
        if (CStringHandlePayload.Length == 0 || string.IsNullOrEmpty(SourceDocumentPath))
            throw new InvalidOperationException(
                "moCStringHandle_c source path could not be decoded; refusing to emit an unsafe block.");
        if (!CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalized))
            throw new ArgumentException(
                "A valid absolute target document path is required for a complete selection block.",
                nameof(targetDocumentPath));
        if (!CStringHandlePathPolicy.TryRead(CStringHandlePayload, out string embedded, out _))
            throw new InvalidOperationException(
                "moCStringHandle_c source path could not be decoded; refusing to emit an unsafe block.");
        if (CStringHandlePathPolicy.SameDocument(embedded, normalized))
            return (byte[])CStringHandlePayload.Clone();
        return CStringHandlePathPolicy.Rewrite(CStringHandlePayload, normalized);
    }

    internal static List<uint> SniffTrimEdges(byte[] payload)
    {
        var edges = new List<uint>();
        if (payload == null) return edges;
        int pos = 0;
        while (pos + 22 <= payload.Length)
        {
            ushort marker = BitConverter.ToUInt16(payload, pos);
            if (marker != 0xBF && marker != 0xC0) break;
            edges.Add(BitConverter.ToUInt32(payload, pos + 14));
            pos += 22;
            if (pos + 2 <= payload.Length)
            {
                ushort next = BitConverter.ToUInt16(payload, pos);
                if (next != 0xBF && next != 0xC0) pos += 2; // class-reference separator
            }
        }
        return edges;
    }
}

/// <summary>Decoder for the contour selection trim/reference suffix.</summary>
internal static class SelectedSketchContourSuffixParser
{
    private static readonly string[] SuffixClasses =
    {
        "moSimpleSurfIdRep_c",
        "moFR_c",
        "moExtObject_c",
        "moCStringHandle_c",
        "moSegmLeftTrimEdgeIdRep_c",
        "moSegmRightTrimEdgeIdRep_c"
    };

    /// <summary>Reads the suffix that precedes the feature tag inside [sketchStart, featureStart).</summary>
    internal static SketchContourSuffixData? Read(MfcDocument doc, int sketchStart, int featureStart)
    {
        if (doc == null) return null;
        var source = doc.SourceData;
        if (sketchStart < 0 || featureStart <= sketchStart || featureStart > source.Length) return null;
        var tags = doc.AllElements
            .Where(e => e.TagOffset >= sketchStart && e.TagOffset < featureStart)
            .OrderBy(e => e.TagOffset)
            .ToList();

        var elements = new Dictionary<string, MfcElement>(StringComparer.Ordinal);
        foreach (var className in SuffixClasses)
        {
            var matches = tags.Where(t => t.ClassName == className).ToList();
            if (matches.Count != 1) return null;
            elements[className] = matches[0];
        }

        // Suffix tags must appear in the serialized order and be adjacent.
        for (int i = 1; i < SuffixClasses.Length; i++)
        {
            if (elements[SuffixClasses[i]].TagOffset <= elements[SuffixClasses[i - 1]].TagOffset) return null;
        }

        byte[] Payload(string className)
        {
            // Bound the payload by the next class tag, not by MfcElement.DataEnd:
            // the tree scanner can extend DataEnd past the tag when end-marker byte
            // patterns occur in the payload.
            var element = elements[className];
            int end = featureStart;
            foreach (var tag in tags)
            {
                if (tag.TagOffset > element.TagOffset && tag.TagOffset < end) end = tag.TagOffset;
            }
            return MfcPrimitives.CopyRange(source, element.DataStart, Math.Min(end, source.Length));
        }

        var data = new SketchContourSuffixData
        {
            SurfaceId = elements["moSimpleSurfIdRep_c"].DataLength >= 2
                ? BitConverter.ToUInt16(source, elements["moSimpleSurfIdRep_c"].DataStart)
                : (ushort)0,
            FrPayload = Payload("moFR_c"),
            ExtObjectPayload = Payload("moExtObject_c"),
            CStringHandlePayload = Payload("moCStringHandle_c"),
            LeftTrimPayload = Payload("moSegmLeftTrimEdgeIdRep_c"),
            RightTrimPayload = Payload("moSegmRightTrimEdgeIdRep_c")
        };
        var left = SketchContourSuffixData.SniffTrimEdges(data.LeftTrimPayload);
        var right = SketchContourSuffixData.SniffTrimEdges(data.RightTrimPayload);
        if (left.Count == 0 || right.Count == 0) return null;
        data.LeftTrimEdges = left.AsReadOnly();
        data.RightTrimEdges = right.AsReadOnly();
        if (CStringHandlePathPolicy.TryRead(data.CStringHandlePayload, out string sourcePath, out byte[] tail))
        {
            data.SourceDocumentPath = sourcePath;
            data.CStringHandleTail = tail;
        }
        if (data.LeftTrimPayload.Length >= 22)
        {
            data.SketchId = BitConverter.ToUInt32(data.LeftTrimPayload, 6);
            data.ArchiveSerial = BitConverter.ToUInt32(data.LeftTrimPayload, 10);
        }
        return data;
    }
}
