using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Sketches;

namespace SldFile.Binary.Topology;

/// <summary>
/// Byte-block templates for the structurally decoded sketch-selection envelopes.
/// The output is an MFC-tagged block that can be embedded between a sketch and a
/// consuming feature inside Contents/Config-0-ResolvedFeatures.
///
/// Scope of this round: the templates reproduce the layouts verified by the
/// controlled samples and round-trip through the SldFile decoders. Fields whose
/// semantics are not established (archive serial numbers, trailer variant bytes,
/// the region tail containing the sketch-wide loop table) use the observed
/// constants; native SolidWorks rebuild validation is still pending.
/// </summary>
internal static class SketchSelectionWriter
{
    private const string ContourChooserClass = "moContourChooser_c";
    private const string SuObArrayClass = "suObArray";
    private const string SketchChainClass = "moSketchChain_c";

    /// <summary>Observed archive id used by both 8-byte fields of the region prefix/trailer.</summary>
    private const ulong RegionArchiveId = 0x54B249AD2594C37DUL;

    /// <summary>Observed per-unit archive serial base (id2 = id1 + 1).</summary>
    internal const uint DefaultRegionSerial = 0x6AE40CD1;

    /// <summary>Default surface reference for a single-contour chooser (0xBF specimen).</summary>
    internal const ushort DefaultContourSurfaceId = 0xBF;

    /// <summary>Default surface reference for the two-contour chooser (0xC0 specimen).</summary>
    internal const ushort MultiContourSurfaceId = 0xC0;

    private static readonly byte[] UnitSignature =
        { 0x00, 0x00, 0xF2, 0x80, 0xF4, 0x80, 0xF7, 0x00, 0xF8, 0x00, 0x02, 0x00, 0x00 };

    private static readonly byte[] UnitEmptyStrings =
        { 0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00 };

    private static readonly byte[] UnitConstant =
        { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0x02, 0xD8, 0x9E, 0xA4, 0x8B };

    /// <summary>
    /// The 18-byte explicit-contour chooser envelope:
    /// u16 0 | u32 1 | u16 surface reference | u32 selected count | u32 count+1 | u16 1.
    /// The surface reference is opaque (0xBF single-contour specimen, 0xC0
    /// two-contour specimen) and is parameterizable.
    /// </summary>
    internal static byte[] BuildContourChooserPayload(ushort surfaceId, int selectedCount)
    {
        if (selectedCount < 1 || selectedCount > 2)
            throw new ArgumentOutOfRangeException(nameof(selectedCount),
                "Only one- and two-contour chooser envelopes have controlled specimens.");
        using var ms = new MemoryStream();
        WriteU16(ms, 0);
        WriteU32(ms, 1);
        WriteU16(ms, surfaceId);
        WriteU32(ms, (uint)selectedCount);
        WriteU32(ms, (uint)selectedCount + 1);
        WriteU16(ms, 1);
        return ms.ToArray();
    }

    /// <summary>Single-contour chooser envelope with the observed 0xBF surface id.</summary>
    internal static byte[] BuildContourChooserPayload() => BuildContourChooserPayload(DefaultContourSurfaceId, 1);

    /// <summary>Builds the suObArray payload (selected-contour count, u16).</summary>
    internal static byte[] BuildSuObArrayPayload(ushort selectedContourCount)
    {
        if (selectedContourCount == 0 || selectedContourCount > SelectedSketchContourParser.MaxSelectedContours)
            throw new ArgumentOutOfRangeException(nameof(selectedContourCount));
        return new[] { (byte)selectedContourCount, (byte)(selectedContourCount >> 8) };
    }

    /// <summary>Builds the moSketchChain_c payload for one chain record (ids + terminator).</summary>
    internal static byte[] BuildSketchChainPayload(IReadOnlyList<uint> entityIds, uint sketchId = 1)
    {
        if (entityIds == null) throw new ArgumentNullException(nameof(entityIds));
        var ids = ValidateChainIds(entityIds);
        using var ms = new MemoryStream();
        WriteChainPayload(ms, ids, sketchId, chainIndex: 1, isFirst: true);
        WriteChainTerminator(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a tagged single-contour selection block:
    /// moContourChooser_c (18-byte envelope) + suObArray (count 1) +
    /// moSketchChain_c (chain record + list terminator).
    /// </summary>
    /// <param name="entityIds">Chain entity ids in the observed order (native SketchContour ids).</param>
    /// <param name="sketchId">
    /// Opaque archive reference at record offset 24; observed as 1 for explicit
    /// contour selection and 6 for whole-sketch chains. The decoder does not
    /// interpret or validate it.
    /// </param>
    /// <param name="surfaceId">Opaque chooser surface reference; 0xBF for the single-contour specimen.</param>
    /// <param name="suffix">
    /// Native trim/reference suffix (see <see cref="SelectedSketchContourSuffixParser"/>).
    /// When supplied, the block is natively rebuildable; envelope-only blocks are
    /// rejected by SolidWorks at rebuild (RPC_E_SERVERFAULT, Agent 5 evidence).
    /// </param>
    /// <param name="targetDocumentPath">
    /// Absolute path of the document receiving the block. Required when
    /// <paramref name="suffix"/> carries a moCStringHandle_c source path: the path
    /// is kept only when it equals this target, otherwise it is rewritten. A
    /// missing/relative target throws instead of emitting a foreign path
    /// (cross-file reuse of the source path makes SolidWorks reject the file).
    /// </param>
    internal static byte[] BuildContourSelectionBlock(IReadOnlyList<uint> entityIds, uint sketchId = 1,
        ushort surfaceId = DefaultContourSurfaceId, SketchContourSuffixData? suffix = null,
        string? targetDocumentPath = null)
    {
        if (entityIds == null) throw new ArgumentNullException(nameof(entityIds));
        return BuildContourSelectionBlock(new[] { entityIds }, sketchId, surfaceId, suffix, targetDocumentPath);
    }

    /// <summary>
    /// Builds a tagged explicit contour selection block for one or two contours
    /// (SelectionAuthoring select_one_of_two_contours / select_two_contours):
    /// suObArray count = number of contours, chain records separated by
    /// 00 00 F3 80, list terminated by 00 00 01 00 00 00 01 00.
    /// More than two contours has no controlled chooser envelope specimen.
    /// </summary>
    internal static byte[] BuildContourSelectionBlock(
        IReadOnlyList<IReadOnlyList<uint>> contours, uint sketchId = 1,
        ushort? surfaceId = null, SketchContourSuffixData? suffix = null,
        string? targetDocumentPath = null)
    {
        if (contours == null) throw new ArgumentNullException(nameof(contours));
        if (contours.Count == 0 || contours.Count > 2)
            throw new ArgumentException("One or two contours are required (controlled chooser specimens).", nameof(contours));
        var validated = contours.Select(ValidateChainIds).ToArray();
        ushort chooserSurfaceId = surfaceId ??
            (validated.Length == 1 ? DefaultContourSurfaceId : MultiContourSurfaceId);

        // Resolve the handle path before writing anything: a foreign source path
        // must never be emitted, and a missing target must fail explicitly.
        byte[]? cstringPayload = suffix?.PrepareCStringHandlePayload(targetDocumentPath);

        using var ms = new MemoryStream();
        WriteClassTag(ms, ContourChooserClass);
        var chooserPayload = BuildContourChooserPayload(chooserSurfaceId, validated.Length);
        ms.Write(chooserPayload, 0, chooserPayload.Length);
        WriteClassTag(ms, SuObArrayClass);
        WriteU16(ms, (ushort)validated.Length);
        WriteClassTag(ms, SketchChainClass);
        for (int i = 0; i < validated.Length; i++)
        {
            if (i > 0) WriteBytes(ms, new byte[] { 0x00, 0x00, 0xF3, 0x80 });
            WriteChainPayload(ms, validated[i], sketchId, chainIndex: i + 1, isFirst: i == 0);
        }
        WriteChainTerminator(ms);
        if (suffix != null) WriteContourSuffix(ms, suffix, cstringPayload!);
        return ms.ToArray();
    }

    /// <summary>
    /// Emits the native trim/reference suffix after the chain block. The payloads
    /// are sketch-wide and therefore re-emitted from a captured native suffix (see
    /// <see cref="SelectedSketchContourSuffixParser.Read"/>); the CStringHandle
    /// payload is prepared by the path policy so a foreign source path is never
    /// embedded.
    /// </summary>
    private static void WriteContourSuffix(Stream ms, SketchContourSuffixData suffix, byte[] cstringPayload)
    {
        WriteClassTag(ms, "moSimpleSurfIdRep_c");
        WriteU16(ms, suffix.SurfaceId);
        WriteClassTag(ms, "moFR_c");
        WriteBytes(ms, suffix.FrPayload);
        WriteClassTag(ms, "moExtObject_c");
        WriteBytes(ms, suffix.ExtObjectPayload);
        WriteClassTag(ms, "moCStringHandle_c");
        WriteBytes(ms, cstringPayload);
        WriteClassTag(ms, "moSegmLeftTrimEdgeIdRep_c");
        WriteBytes(ms, suffix.LeftTrimPayload);
        WriteClassTag(ms, "moSegmRightTrimEdgeIdRep_c");
        WriteBytes(ms, suffix.RightTrimPayload);
    }

    private static uint[] ValidateChainIds(IReadOnlyList<uint> entityIds)
    {
        if (entityIds == null) throw new ArgumentNullException(nameof(entityIds));
        if (entityIds.Count == 0) throw new ArgumentException("At least one chain entity id is required.", nameof(entityIds));
        var ids = entityIds.ToArray();
        if (ids.Any(id => id == 0)) throw new ArgumentException("Chain entity ids must be non-zero.", nameof(entityIds));
        if (ids.Distinct().Count() != ids.Length) throw new ArgumentException("Chain entity ids must be distinct.", nameof(entityIds));
        return ids;
    }

    /// <summary>
    /// Builds a structural region selection block (moContourChooser_c prefix +
    /// selection groups only). The inter-group headers and the sketch-wide tail
    /// that native SolidWorks requires are NOT included; use
    /// <see cref="BuildRegionSelectionBlock(IReadOnlyList{uint}, RegionChooserTemplate)"/>
    /// with a captured native template for native-openable authoring.
    /// </summary>
    internal static byte[] BuildRegionSelectionBlock(IReadOnlyList<uint> boundaryEdgeIds, uint serial = DefaultRegionSerial)
    {
        var payload = BuildRegionChooserPayload(boundaryEdgeIds, serial);
        using var ms = new MemoryStream();
        WriteClassTag(ms, ContourChooserClass);
        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a region selection block from a captured native chooser template,
    /// preserving the inter-group headers and the sketch-wide tail that the
    /// structural template alone does not contain. The output is byte-identical to
    /// the native payload when the requested edge set equals the captured set.
    /// </summary>
    /// <summary>
    /// Builds a region selection block from a captured native chooser template.
    /// The requested boundary edge set must equal the captured set (order is
    /// irrelevant); for a different set use
    /// <see cref="BuildRegionSelectionBlockFromLoopOrder"/> with the boundary loop
    /// traversal order.
    /// </summary>
    internal static byte[] BuildRegionSelectionBlock(
        IReadOnlyList<uint> boundaryEdgeIds, RegionChooserTemplate template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        var payload = template.BuildPayload(boundaryEdgeIds);
        using var ms = new MemoryStream();
        WriteClassTag(ms, ContourChooserClass);
        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    /// <summary>
    /// Builds a region chooser for a different boundary edge set on the same
    /// sketch. <paramref name="boundaryLoopOrder"/> is the connected boundary
    /// traversal order; groups are emitted as
    /// group g = [e[(N-g) mod N], e[(N-2-g) mod N], e[(N-1-g) mod N]].
    /// </summary>
    internal static byte[] BuildRegionSelectionBlockFromLoopOrder(
        IReadOnlyList<uint> boundaryLoopOrder, RegionChooserTemplate template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        var payload = template.BuildPayloadFromLoopOrder(boundaryLoopOrder);
        using var ms = new MemoryStream();
        WriteClassTag(ms, ContourChooserClass);
        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    /// <summary>Loop-order region block with target-document validation.</summary>
    internal static byte[] BuildRegionSelectionBlockFromLoopOrder(
        IReadOnlyList<uint> boundaryLoopOrder, RegionChooserTemplate template, string? targetDocumentPath)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.ValidateTargetDocument(targetDocumentPath, rewriteRequired: false);
        return BuildRegionSelectionBlockFromLoopOrder(boundaryLoopOrder, template);
    }

    /// <summary>
    /// Builds a region selection block for any boundary edge set, deriving the
    /// loop traversal order from the target sketch geometry
    /// (<see cref="SketchRegionLoopOrderResolver"/>). Throws when the geometry is
    /// missing/ambiguous instead of guessing an order.
    /// </summary>
    internal static byte[] BuildRegionSelectionBlockFromSketch(
        IReadOnlyCollection<uint> boundaryEdgeIds, RegionChooserTemplate template, SketchFeature? sketch)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        var payload = template.BuildPayloadFromSketch(sketch, boundaryEdgeIds);
        using var ms = new MemoryStream();
        WriteClassTag(ms, ContourChooserClass);
        ms.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    /// <summary>Sketch-based region block with target-document validation.</summary>
    internal static byte[] BuildRegionSelectionBlockFromSketch(
        IReadOnlyCollection<uint> boundaryEdgeIds, RegionChooserTemplate template,
        SketchFeature? sketch, string? targetDocumentPath)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.ValidateTargetDocument(targetDocumentPath, rewriteRequired: false);
        return BuildRegionSelectionBlockFromSketch(boundaryEdgeIds, template, sketch);
    }

    /// <summary>
    /// Complete region context block for any boundary edge set, with the loop order
    /// derived from the target sketch geometry and the CStringHandle path rewritten
    /// for the target document.
    /// </summary>
    internal static byte[] BuildRegionSelectionContextFromSketch(
        IReadOnlyCollection<uint> boundaryEdgeIds, RegionChooserTemplate template,
        SketchFeature? sketch, string? targetDocumentPath)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        var payload = template.BuildPayloadFromSketch(sketch, boundaryEdgeIds);
        return template.BuildContextFromPayload(payload, targetDocumentPath);
    }

    /// <summary>
    /// Chooser-only region block with target-document validation. The chooser
    /// payload carries no path, but when the template context was captured from a
    /// document, a foreign target is rejected unless a context rewrite is used.
    /// </summary>
    internal static byte[] BuildRegionSelectionBlock(
        IReadOnlyList<uint> boundaryEdgeIds, RegionChooserTemplate template, string? targetDocumentPath)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        template.ValidateTargetDocument(targetDocumentPath, rewriteRequired: false);
        return BuildRegionSelectionBlock(boundaryEdgeIds, template);
    }

    /// <summary>
    /// Builds the complete region selection context (captured context tags +
    /// chooser block). The moCStringHandle_c source path is rewritten to
    /// <paramref name="targetDocumentPath"/> (or kept verbatim for the same
    /// document); a missing/relative target or unreadable embedded path throws.
    /// </summary>
    internal static byte[] BuildRegionSelectionContext(
        IReadOnlyList<uint> boundaryEdgeIds, RegionChooserTemplate template, string? targetDocumentPath)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        return template.BuildContext(boundaryEdgeIds, targetDocumentPath);
    }

    /// <summary>Complete region context block for a different edge set (loop traversal order).</summary>
    internal static byte[] BuildRegionSelectionContextFromLoopOrder(
        IReadOnlyList<uint> boundaryLoopOrder, RegionChooserTemplate template, string? targetDocumentPath)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        return template.BuildContextFromLoopOrder(boundaryLoopOrder, targetDocumentPath);
    }

    internal static byte[] BuildRegionChooserPayload(IReadOnlyList<uint> boundaryEdgeIds, uint serial = DefaultRegionSerial)
    {
        if (boundaryEdgeIds == null) throw new ArgumentNullException(nameof(boundaryEdgeIds));
        var edges = boundaryEdgeIds.ToArray();
        // Only the 4-edge (rectangle / overlap) and 6-edge (L-shape) group
        // layouts have controlled native specimens; other counts are rejected
        // rather than guessed.
        if (edges.Length != 4 && edges.Length != 6)
            throw new ArgumentException("Boundary edge count must be 4 or 6 (established region layouts).", nameof(boundaryEdgeIds));
        if (edges.Any(id => id == 0))
            throw new ArgumentException("Boundary edge ids must be non-zero.", nameof(boundaryEdgeIds));
        if (edges.Distinct().Count() != edges.Length)
            throw new ArgumentException("Boundary edge ids must be distinct.", nameof(boundaryEdgeIds));

        using var ms = new MemoryStream();
        ushort n = (ushort)edges.Length;
        WriteU16(ms, 1);              // selected region count
        WriteU16(ms, 0x80EC);         // archive class reference
        WriteU16(ms, n);              // boundary edge count
        WriteU16(ms, 0x80EE);         // archive class reference
        WriteU16(ms, 1);
        WriteU32(ms, 0);
        WriteU16(ms, 0);
        WriteU32(ms, 5);
        WriteByte(ms, 0x04);
        WriteByte(ms, 0x03);
        for (int i = 0; i < 6; i++) WriteByte(ms, 0);
        WriteU64(ms, RegionArchiveId);
        WriteU64(ms, RegionArchiveId);
        WriteU16(ms, 0);

        for (int group = 0; group < edges.Length; group++)
        {
            WriteStartUnit(ms, serial);
            for (int k = 0; k < 3; k++)
            {
                // Every group carries exactly three edge units (observed for both
                // the 4-edge and 6-edge specimens); group g omits a different
                // boundary edge so the union over all groups is the complete set.
                int source = (group + 1 + k) % edges.Length;
                WriteEdgeUnit(ms, serial, edges[source]);
            }
            if (group < edges.Length - 1)
                WriteGroupTrailer(ms, firstTrailer: group == 0);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Writes one chain record body (60 + 4*count bytes):
    /// u16 count | ids | u16 closed=1 | u16 flag | u16 0 | u32 sketch id |
    /// 3 x FFFFFFFF | 5 | 5 | empty WChar | u16 0 | u16 chain index | 20 zeros.
    /// Flag is 0 for the first record and 1 for subsequent records (observed in
    /// SelectionAuthoring/select_two_contours).
    /// </summary>
    private static void WriteChainPayload(Stream ms, IReadOnlyList<uint> ids, uint sketchId,
        int chainIndex, bool isFirst)
    {
        WriteU16(ms, (ushort)ids.Count);
        foreach (var id in ids) WriteU32(ms, id);
        WriteU16(ms, 1);                       // closed
        WriteU16(ms, isFirst ? (ushort)0 : (ushort)1); // flag
        WriteU16(ms, 0);
        WriteU32(ms, sketchId);
        WriteU32(ms, 0xFFFFFFFF);
        WriteU32(ms, 0xFFFFFFFF);
        WriteU32(ms, 0xFFFFFFFF);
        WriteU32(ms, 5);
        WriteU32(ms, 5);
        WriteBytes(ms, new byte[] { 0xFF, 0xFE, 0xFF, 0x00 }); // empty WChar
        WriteU16(ms, 0);
        WriteU16(ms, (ushort)chainIndex);      // 1-based chain index
        for (int i = 0; i < 20; i++) WriteByte(ms, 0);
    }

    /// <summary>Chain list terminator observed after the last record.</summary>
    private static void WriteChainTerminator(Stream ms)
    {
        WriteU16(ms, 0);
        WriteU16(ms, 1);
        WriteU16(ms, 0);
        WriteU16(ms, 1);
    }

    private static void WriteStartUnit(Stream ms, uint serial)
    {
        WriteU16(ms, 0x80F0);
        WriteBytes(ms, UnitSignature);
        WriteU32(ms, serial);
        WriteBytes(ms, UnitEmptyStrings);
        for (int i = 0; i < 14; i++) WriteByte(ms, 0);
        WriteBytes(ms, UnitConstant);
        for (int i = 0; i < 16; i++) WriteByte(ms, 0);
        WriteU32(ms, 0x49);
        WriteU32(ms, unchecked(serial + 1));
        WriteU32(ms, 0); // start unit has no edge id
        WriteU16(ms, 0);
    }

    private static void WriteEdgeUnit(Stream ms, uint serial, uint edge)
    {
        WriteU16(ms, 0x80F9);
        WriteBytes(ms, UnitSignature);
        WriteU32(ms, serial);
        WriteBytes(ms, UnitEmptyStrings);
        for (int i = 0; i < 14; i++) WriteByte(ms, 0);
        WriteBytes(ms, UnitConstant);
        for (int i = 0; i < 16; i++) WriteByte(ms, 0);
        WriteU32(ms, 0x49);
        WriteU32(ms, unchecked(serial + 1));
        WriteU32(ms, edge);
        WriteU32(ms, 0);
    }

    private static void WriteGroupTrailer(Stream ms, bool firstTrailer)
    {
        for (int i = 0; i < 8; i++) WriteByte(ms, 0);
        WriteU16(ms, 0x4A38);
        for (int i = 0; i < 6; i++) WriteByte(ms, 0);
        WriteU16(ms, 0x80EE);
        WriteU16(ms, 1);
        for (int i = 0; i < 6; i++) WriteByte(ms, 0);
        WriteU32(ms, 5);
        WriteByte(ms, firstTrailer ? (byte)0x04 : (byte)0x06);
        WriteByte(ms, 0x03);
        for (int i = 0; i < 6; i++) WriteByte(ms, 0);
        WriteU64(ms, RegionArchiveId);
        WriteU64(ms, RegionArchiveId);
        WriteU16(ms, 0);
    }

    private static void WriteClassTag(Stream ms, string className)
    {
        WriteByte(ms, 0xFF);
        WriteByte(ms, 0xFF);
        WriteByte(ms, 0x01);
        WriteByte(ms, 0x00);
        WriteU16(ms, (ushort)className.Length);
        var ascii = Encoding.ASCII.GetBytes(className);
        WriteBytes(ms, ascii);
    }

    private static void WriteBytes(Stream ms, byte[] data) => ms.Write(data, 0, data.Length);

    private static void WriteByte(Stream ms, byte value) => ms.WriteByte(value);

    private static void WriteU16(Stream ms, ushort value)
    {
        ms.WriteByte((byte)value);
        ms.WriteByte((byte)(value >> 8));
    }

    private static void WriteU32(Stream ms, uint value)
    {
        ms.WriteByte((byte)value);
        ms.WriteByte((byte)(value >> 8));
        ms.WriteByte((byte)(value >> 16));
        ms.WriteByte((byte)(value >> 24));
    }

    private static void WriteU64(Stream ms, ulong value)
    {
        WriteU32(ms, (uint)value);
        WriteU32(ms, (uint)(value >> 32));
    }
}
