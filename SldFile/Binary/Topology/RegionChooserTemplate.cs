using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Mfc;
using SldFile.Sketches;

namespace SldFile.Binary.Topology;

/// <summary>One captured tag (class + raw payload) of the region selection context.</summary>
internal sealed class RegionContextTag
{
    public string ClassName { get; set; } = string.Empty;

    public int TagOffset { get; set; }

    public byte[] Payload { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Captured native region chooser payload used as a re-emission template.
///
/// A region chooser payload contains sketch-wide data after the selection groups
/// (inter-group 8-byte magic + 40-byte prefix tails and a final ~387-475 byte
/// tail with bounding-box doubles and the sketch-wide loop/edge table). That tail
/// cannot be synthesized from the boundary edge set alone, so native-openable
/// region authoring preserves a payload captured from the same sketch and patches
/// only the selection-group edge fields.
///
/// The surrounding selection context tags (moOneContour_c, moEdgeRef_c,
/// moSimpleSurfIdRep_c, moFR_c, moExtObject_c, moCStringHandle_c, left/right trim
/// records, moBBoxCenterData_c) may also be captured so a complete context block
/// can be emitted. The moCStringHandle_c payload stores the source document path;
/// <see cref="BuildContext"/> rewrites it to the target document path (or keeps it
/// verbatim for the same document) and rejects the emission when no safe target
/// path is known.
///
/// Captured specimens: SelectionAuthoring/select_region (6 edges, extended
/// verbose units) and select_region_overlap (4 edges, 06 03 variant), plus their
/// resave compact-unit forms.
/// </summary>
internal sealed class RegionChooserTemplate
{
    private static readonly HashSet<string> ContextClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "moOneContour_c",
        "moEdgeRef_c",
        "moSimpleSurfIdRep_c",
        "moFR_c",
        "moExtObject_c",
        "moCStringHandle_c",
        "moSegmLeftTrimEdgeIdRep_c",
        "moSegmRightTrimEdgeIdRep_c",
        "moBBoxCenterData_c"
    };

    private RegionChooserTemplate(byte[] payload, IReadOnlyList<int[]> edgeFieldOffsets,
        IReadOnlyList<uint> nativeEdgeIds, IReadOnlyList<uint[]> nativeGroupEdges)
    {
        Payload = payload;
        EdgeFieldOffsets = edgeFieldOffsets;
        NativeEdgeIds = nativeEdgeIds;
        NativeGroupEdges = nativeGroupEdges;
        NativeLoopOrder = RecoverLoopOrder(nativeGroupEdges);
        OrderingRuleVerified = VerifyLoopOrder(nativeGroupEdges, NativeLoopOrder);
    }

    /// <summary>Native chooser payload (without the MFC class tag).</summary>
    public byte[] Payload { get; }

    /// <summary>Absolute offsets of the 3 edge-id u32 fields per selection group.</summary>
    public IReadOnlyList<int[]> EdgeFieldOffsets { get; }

    public int GroupCount => EdgeFieldOffsets.Count;

    public IReadOnlyList<uint> NativeEdgeIds { get; }

    /// <summary>Native edge ids per group, in serialized order (3 ids each).</summary>
    public IReadOnlyList<uint[]> NativeGroupEdges { get; }

    /// <summary>
    /// Boundary loop traversal order recovered from the native groups:
    ///   group g = [ e[(N-g) mod N], e[(N-2-g) mod N], e[(N-1-g) mod N] ]
    /// Empty when the captured groups do not follow this rule.
    /// </summary>
    public IReadOnlyList<uint> NativeLoopOrder { get; }

    /// <summary>True when the captured native groups match the established ordering rule.</summary>
    public bool OrderingRuleVerified { get; }

    /// <summary>Selection-context tags preceding the chooser, in serialized order.</summary>
    public IReadOnlyList<RegionContextTag> ContextTags { get; private set; } = Array.Empty<RegionContextTag>();

    /// <summary>Source document path decoded from the captured moCStringHandle_c.</summary>
    public string? SourceDocumentPath { get; private set; }

    /// <summary>True when the captured context contains conflicting CStringHandle paths.</summary>
    public bool ContextPathAmbiguous { get; private set; }

    public static RegionChooserTemplate? Capture(byte[] payload)
    {
        if (payload == null) return null;
        if (!SelectedSketchRegionParser.TryLocateSelectionGroupsWithEdges(
                payload, out int groupCount, out var fields, out var groupEdges))
            return null;
        var decoded = SelectedSketchRegionParser.TryReadPayload(payload);
        if (decoded.Count != 1 || fields.Count != groupCount) return null;
        return new RegionChooserTemplate((byte[])payload.Clone(), fields, decoded[0].EdgeIds, groupEdges);
    }

    /// <summary>
    /// Recovers the boundary loop traversal order from the native group edge lists.
    /// Group g's first edge is e[(N-g) mod N], so e[j] = firstEdgeOfGroup[(N-j) mod N].
    /// </summary>
    private static IReadOnlyList<uint> RecoverLoopOrder(IReadOnlyList<uint[]> groupEdges)
    {
        int n = groupEdges.Count;
        if (n == 0) return Array.Empty<uint>();
        var order = new uint[n];
        for (int j = 0; j < n; j++)
        {
            int group = ((n - j) % n + n) % n;
            if (groupEdges[group].Length == 0) return Array.Empty<uint>();
            order[j] = groupEdges[group][0];
        }
        return Array.AsReadOnly(order);
    }

    /// <summary>Verifies every group against the ordering rule and loop uniqueness.</summary>
    private static bool VerifyLoopOrder(IReadOnlyList<uint[]> groupEdges, IReadOnlyList<uint> loopOrder)
    {
        int n = groupEdges.Count;
        if (n < 4 || loopOrder.Count != n) return false;
        if (loopOrder.Distinct().Count() != n || loopOrder.Any(id => id == 0)) return false;
        for (int g = 0; g < n; g++)
        {
            var expected = GroupEdgeIds(loopOrder, g);
            if (groupEdges[g].Length != expected.Length) return false;
            for (int k = 0; k < expected.Length; k++)
                if (groupEdges[g][k] != expected[k]) return false;
        }
        return true;
    }

    /// <summary>
    /// Group g edge order (native evidence):
    /// [ e[(N-g) mod N], e[(N-2-g) mod N], e[(N-1-g) mod N] ].
    /// </summary>
    private static uint[] GroupEdgeIds(IReadOnlyList<uint> loopOrder, int group)
    {
        int n = loopOrder.Count;
        return new[]
        {
            loopOrder[((n - group) % n + n) % n],
            loopOrder[((n - 2 - group) % n + n) % n],
            loopOrder[((n - 1 - group) % n + n) % n]
        };
    }

    /// <summary>
    /// Captures the single region chooser between <paramref name="sketchStart"/> and
    /// <paramref name="featureStart"/> from an existing document, including the
    /// selection-context tags that precede it.
    /// </summary>
    public static RegionChooserTemplate? Capture(MfcDocument doc, int sketchStart, int featureStart)
    {
        if (doc == null) return null;
        var choosers = doc.AllElements
            .Where(e => e.ClassName == "moContourChooser_c" &&
                        e.TagOffset >= sketchStart && e.TagOffset < featureStart)
            .OrderBy(e => e.TagOffset)
            .ToList();
        if (choosers.Count != 1) return null;
        var chooser = choosers[0];
        // Bound the chooser payload by the next class tag (or the feature tag):
        // MfcElement.DataEnd can be over-extended when the payload contains
        // end-marker-like byte patterns.
        int payloadEnd = NextTagOffset(doc, chooser, featureStart);
        byte[] payload = MfcPrimitives.CopyRange(doc.SourceData, chooser.DataStart, payloadEnd);
        var template = Capture(payload);
        if (template == null) return null;

        var context = new List<RegionContextTag>();
        var elements = doc.AllElements
            .Where(e => e.TagOffset >= sketchStart && e.TagOffset < chooser.TagOffset &&
                        ContextClasses.Contains(e.ClassName))
            .OrderBy(e => e.TagOffset)
            .ToList();
        foreach (var element in elements)
        {
            int end = NextTagOffset(doc, element, chooser.TagOffset);
            context.Add(new RegionContextTag
            {
                ClassName = element.ClassName,
                TagOffset = element.TagOffset,
                Payload = MfcPrimitives.CopyRange(doc.SourceData, element.DataStart, end)
            });
        }
        template.ContextTags = context.AsReadOnly();

        var paths = new List<string>();
        foreach (var tag in context.Where(t => t.ClassName == "moCStringHandle_c"))
        {
            if (CStringHandlePathPolicy.TryRead(tag.Payload, out string path, out _)) paths.Add(path);
        }
        var distinct = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count == 1)
            template.SourceDocumentPath = distinct[0];
        else if (distinct.Count > 1)
            template.ContextPathAmbiguous = true;
        return template;
    }

    /// <summary>
    /// Returns a payload for the same native boundary edge set (order-insensitive).
    /// The captured payload is returned byte-identically. For a different edge set
    /// the boundary loop traversal order is required (see
    /// <see cref="BuildPayloadFromLoopOrder"/>); a set-only request is rejected
    /// instead of guessing an order that native SolidWorks would reject.
    /// </summary>
    public byte[] BuildPayload(IReadOnlyList<uint> boundaryEdgeIds)
    {
        if (boundaryEdgeIds == null) throw new ArgumentNullException(nameof(boundaryEdgeIds));
        var edges = boundaryEdgeIds.Distinct().ToArray();
        if (edges.Length != GroupCount || edges.Any(id => id == 0))
            throw new ArgumentException("Boundary edge count must match the captured group count.", nameof(boundaryEdgeIds));

        var nativeSet = NativeEdgeIds.OrderBy(x => x).ToArray();
        var requestedSet = edges.OrderBy(x => x).ToArray();
        if (nativeSet.SequenceEqual(requestedSet))
            return (byte[])Payload.Clone();
        throw new ArgumentException(
            "A different boundary set requires its loop traversal order; use BuildPayloadFromLoopOrder.",
            nameof(boundaryEdgeIds));
    }

    /// <summary>
    /// Returns a payload with the captured non-selection bytes and the selection
    /// groups written from the boundary loop traversal order. The native ordering
    /// rule is:
    ///   group g = [ e[(N-g) mod N], e[(N-2-g) mod N], e[(N-1-g) mod N] ].
    /// When the requested loop equals <see cref="NativeLoopOrder"/> the payload is
    /// byte-identical to the captured one.
    /// </summary>
    public byte[] BuildPayloadFromLoopOrder(IReadOnlyList<uint> boundaryLoopOrder)
    {
        if (boundaryLoopOrder == null) throw new ArgumentNullException(nameof(boundaryLoopOrder));
        var order = boundaryLoopOrder.ToArray();
        if (order.Length != GroupCount || order.Any(id => id == 0) || order.Distinct().Count() != order.Length)
            throw new ArgumentException(
                "Loop order must contain one distinct non-zero id per captured group.", nameof(boundaryLoopOrder));
        if (!OrderingRuleVerified)
            throw new InvalidOperationException(
                "The captured native groups do not follow the established ordering rule; cannot re-emit a different edge set.");

        var copy = (byte[])Payload.Clone();
        for (int group = 0; group < EdgeFieldOffsets.Count; group++)
        {
            var fields = EdgeFieldOffsets[group];
            var edgeIds = GroupEdgeIds(order, group);
            for (int k = 0; k < fields.Length && k < edgeIds.Length; k++)
                BitConverter.GetBytes(edgeIds[k]).CopyTo(copy, fields[k]);
        }
        return copy;
    }

    /// <summary>
    /// Resolves the boundary loop order from the target sketch geometry and emits
    /// the payload. When the requested set equals the captured native set, the
    /// derived cycle is rotated to the native start edge so the output is
    /// byte-identical; for a different set the resolver's deterministic start
    /// (smallest edge id, clockwise) is used. Unresolvable geometry throws.
    /// </summary>
    public byte[] BuildPayloadFromSketch(SketchFeature? sketch, IReadOnlyCollection<uint> boundaryEdgeIds)
    {
        if (boundaryEdgeIds == null) throw new ArgumentNullException(nameof(boundaryEdgeIds));
        var requested = boundaryEdgeIds.Distinct().OrderBy(x => x).ToArray();
        var native = NativeEdgeIds.OrderBy(x => x).ToArray();
        uint? preferred = requested.SequenceEqual(native) && NativeLoopOrder.Count > 0
            ? NativeLoopOrder[0]
            : (uint?)null;
        var loopOrder = SketchRegionLoopOrderResolver.Resolve(sketch, boundaryEdgeIds, preferred);
        if (loopOrder == null)
            throw new InvalidOperationException(
                "The region boundary loop order could not be derived from the sketch geometry.");
        return BuildPayloadFromLoopOrder(loopOrder);
    }

    /// <summary>
    /// Validates the CStringHandle path against the target document. The chooser
    /// payload itself contains no path, so a foreign target is only rejected when
    /// the captured context carries a source path; use <see cref="BuildContext"/>
    /// to emit a rewritten context.
    /// </summary>
    internal void ValidateTargetDocument(string? targetDocumentPath, bool rewriteRequired)
    {
        if (ContextPathAmbiguous)
            throw new InvalidOperationException("Captured region context contains conflicting CStringHandle paths.");
        if (SourceDocumentPath == null)
        {
            if (rewriteRequired)
                throw new InvalidOperationException(
                    "Captured region context has no readable moCStringHandle_c source path; refusing to emit an unsafe context.");
            return;
        }
        if (!CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalized))
            throw new ArgumentException(
                "A valid absolute target document path is required for a region selection context.",
                nameof(targetDocumentPath));
        if (!CStringHandlePathPolicy.SameDocument(SourceDocumentPath, normalized) && !rewriteRequired)
            throw new InvalidOperationException(
                "The captured region context belongs to another document; use BuildContext to rewrite the CStringHandle path.");
    }

    /// <summary>
    /// Emits the captured selection-context tags followed by the chooser block.
    /// The moCStringHandle_c source path is kept only when the target document is
    /// the same file; otherwise it is rewritten to <paramref name="targetDocumentPath"/>.
    /// A missing/relative target path or an unreadable embedded path is rejected.
    /// </summary>
    internal byte[] BuildContext(IReadOnlyList<uint> boundaryEdgeIds, string? targetDocumentPath)
    {
        return BuildContextCore(BuildPayload(boundaryEdgeIds), targetDocumentPath);
    }

    /// <summary>Complete context block for a different edge set, using the loop traversal order.</summary>
    internal byte[] BuildContextFromLoopOrder(IReadOnlyList<uint> boundaryLoopOrder, string? targetDocumentPath)
    {
        return BuildContextCore(BuildPayloadFromLoopOrder(boundaryLoopOrder), targetDocumentPath);
    }

    /// <summary>Complete context block around an already-built chooser payload.</summary>
    internal byte[] BuildContextFromPayload(byte[] chooserPayload, string? targetDocumentPath)
    {
        if (chooserPayload == null) throw new ArgumentNullException(nameof(chooserPayload));
        return BuildContextCore(chooserPayload, targetDocumentPath);
    }

    private byte[] BuildContextCore(byte[] chooserPayload, string? targetDocumentPath)
    {
        ValidateTargetDocument(targetDocumentPath, rewriteRequired: true);
        CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalized);
        using var ms = new MemoryStream();
        foreach (var tag in ContextTags)
        {
            WriteClassTag(ms, tag.ClassName);
            byte[] payload = tag.Payload;
            if (tag.ClassName == "moCStringHandle_c")
            {
                payload = CStringHandlePathPolicy.SameDocument(SourceDocumentPath!, normalized)
                    ? (byte[])tag.Payload.Clone()
                    : CStringHandlePathPolicy.Rewrite(tag.Payload, normalized);
            }
            ms.Write(payload, 0, payload.Length);
        }
        WriteClassTag(ms, "moContourChooser_c");
        ms.Write(chooserPayload, 0, chooserPayload.Length);
        return ms.ToArray();
    }

    private static int NextTagOffset(MfcDocument doc, MfcElement element, int limit)
    {
        int end = limit;
        foreach (var candidate in doc.AllElements)
        {
            if (candidate.TagOffset > element.TagOffset && candidate.TagOffset < end)
                end = candidate.TagOffset;
        }
        return Math.Min(end, doc.SourceData.Length);
    }

    private static void WriteClassTag(Stream stream, string className)
    {
        stream.WriteByte(0xFF);
        stream.WriteByte(0xFF);
        stream.WriteByte(0x01);
        stream.WriteByte(0x00);
        int length = className.Length;
        stream.WriteByte((byte)length);
        stream.WriteByte((byte)(length >> 8));
        foreach (char c in className) stream.WriteByte((byte)c);
    }
}
