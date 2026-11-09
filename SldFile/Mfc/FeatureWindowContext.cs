using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Unified 5-Part Feature Anatomy extractor for any feature window <c>[Start, End)</c> inside
/// <c>ResolvedFeatures</c>. Encapsulates:
/// <list type="number">
/// <item>Node Header (<see cref="FolderHeader"/> and stream version)</item>
/// <item>Sub-node sequence and payload slicing (safe against internal <c>FFFF</c> sentinels)</item>
/// <item>Parameter Bag (<see cref="Parameters"/>: named <c>0x8004 + WChar</c> and <c>WChar</c> parameters)</item>
/// <item>Selection Bag (<see cref="Selections"/>: topology &amp; selection sub-objects for edges, faces, bodies, points)</item>
/// </list>
/// </summary>
internal sealed class FeatureWindowContext
{
    private FeatureParameterBag? _parameters;
    private FeatureSelectionBag? _selections;

    public MfcDocument Document { get; }
    public byte[] RawData => Document.SourceData;
    public ushort StreamVersion { get; }
    public string ClassName { get; }
    public uint TypeId { get; }
    public int Start { get; }
    public int End { get; }
    public int HeaderStart { get; }
    public MfcElement? RootElement { get; }
    public IReadOnlyList<MfcElement> Nodes { get; }

    /// <summary>
    /// Structured Parameter Bag for querying named double/integer parameters within this feature window
    /// or sub-ranges thereof.
    /// </summary>
    public FeatureParameterBag Parameters => _parameters ??= new FeatureParameterBag(this);

    /// <summary>
    /// Structured Selection Bag for extracting topology references (edges, faces, bodies, sketch points)
    /// within this feature window.
    /// </summary>
    public FeatureSelectionBag Selections => _selections ??= new FeatureSelectionBag(this);

    private FeatureWindowContext(
        MfcDocument doc,
        ushort streamVersion,
        string className,
        uint typeId,
        int start,
        int end,
        List<MfcElement> nodes,
        int headerStart = -1,
        MfcElement? rootElement = null)
    {
        Document = doc;
        StreamVersion = streamVersion;
        ClassName = className;
        TypeId = typeId;
        Start = start;
        End = end;
        Nodes = nodes;
        HeaderStart = headerStart;
        RootElement = rootElement;
    }

    /// <summary>
    /// Creates a bounded feature window context if the range and stream header are valid.
    /// </summary>
    public static FeatureWindowContext? TryCreate(
        MfcDocument doc,
        string className,
        uint typeId,
        int start,
        int end,
        string? expectedClassName = null,
        uint? expectedTypeId = null,
        ushort? expectedStreamVersion = null)
    {
        if (doc == null || doc.SourceData.Length < 2) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        ushort version = BitConverter.ToUInt16(doc.SourceData, 0);
        if (expectedClassName != null && !string.Equals(className, expectedClassName, StringComparison.Ordinal))
            return null;
        if (expectedTypeId.HasValue && typeId != expectedTypeId.Value)
            return null;
        if (expectedStreamVersion.HasValue && version != expectedStreamVersion.Value)
            return null;

        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();

        return new FeatureWindowContext(doc, version, className, typeId, start, end, nodes, rootElement: nodes.FirstOrDefault(e => e.TagOffset == start));
    }

    /// <summary>
    /// Creates a feature window context starting from a root <see cref="MfcElement"/>, reusing
    /// <see cref="MfcElement.Document"/> (or parsing once if detached) and validating the WChar display name
    /// and FolderHeader TypeId.
    /// </summary>
    public static FeatureWindowContext? TryCreateFromElement(
        MfcElement? element,
        string? expectedClassName = null,
        uint? minTypeId = null,
        uint? maxTypeId = null,
        ushort? expectedStreamVersion = null,
        ushort? altStreamVersion = null,
        bool requireEmptyWCharAt10 = false,
        bool allowTwoByteHeaderPadding = false,
        int? explicitEnd = null)
    {
        if (element == null || element.TagByte != 0x01)
            return null;
        if (expectedClassName != null && !string.Equals(element.ClassName, expectedClassName, StringComparison.Ordinal))
            return null;

        byte[] data = element.SourceData;
        if (data == null || data.Length < 16 || element.TagOffset < 0)
            return null;

        ushort version = BitConverter.ToUInt16(data, 0);
        if (expectedStreamVersion.HasValue && version != expectedStreamVersion.Value &&
            (!altStreamVersion.HasValue || version != altStreamVersion.Value))
        {
            return null;
        }

        if (!TryReadElementHeader(
                data,
                element.DataStart,
                minTypeId,
                maxTypeId,
                requireEmptyWCharAt10,
                allowTwoByteHeaderPadding,
                out uint typeId,
                out int headerStart))
        {
            return null;
        }

        var doc = element.Document ?? MfcDocument.Parse(data);
        int end = explicitEnd.HasValue ? Math.Min(explicitEnd.Value, data.Length) : data.Length;
        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= element.TagOffset && e.TagOffset < end)
            .ToList();

        return new FeatureWindowContext(
            doc,
            version,
            element.ClassName,
            typeId,
            element.TagOffset,
            end,
            nodes,
            headerStart: headerStart,
            rootElement: element);
    }

    /// <summary>
    /// Creates a bounded feature window context for a folder feature whose <see cref="FolderHeader"/>
    /// is parsed through <see cref="ResolvedFeaturesSchemaMapper.ParseFolderHeader"/>.
    /// </summary>
    public static FeatureWindowContext? TryCreateWithFolderHeader(
        MfcDocument? doc,
        string className,
        uint expectedOrZeroTypeId,
        int start,
        int end,
        out FolderHeader header,
        string? requiredClassName = null)
    {
        header = new FolderHeader();
        if (doc == null || start < 0 || doc.SourceData.Length < 2)
            return null;
        if (requiredClassName != null && !string.Equals(className, requiredClassName, StringComparison.Ordinal))
            return null;

        var element = doc.AllElements.FirstOrDefault(e =>
            e.TagByte == 0x01 && e.TagOffset == start && e.ClassName == className);
        if (element == null)
            return null;

        header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (!header.IsValid || header.TypeId == 0)
            return null;
        if (expectedOrZeroTypeId != 0 && header.TypeId != expectedOrZeroTypeId)
            return null;

        int safeEnd = Math.Min(Math.Max(start + 1, end), doc.SourceData.Length);
        return TryCreate(doc, className, header.TypeId, start, safeEnd, rootElementHint: element);
    }

    private static FeatureWindowContext? TryCreate(
        MfcDocument doc,
        string className,
        uint typeId,
        int start,
        int end,
        MfcElement? rootElementHint)
    {
        ushort version = BitConverter.ToUInt16(doc.SourceData, 0);
        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();
        return new FeatureWindowContext(doc, version, className, typeId, start, end, nodes, rootElement: rootElementHint);
    }

    /// <summary>
    /// Reads the WChar display name at <paramref name="dataStart"/> and extracts the header start offset
    /// and 32-bit TypeId at <c>header + 8</c>.
    /// </summary>
    public static bool TryReadElementHeader(
        byte[] data,
        int dataStart,
        uint? minTypeId,
        uint? maxTypeId,
        bool requireEmptyWCharAt10,
        bool allowTwoByteHeaderPadding,
        out uint typeId,
        out int headerStart)
    {
        typeId = 0;
        headerStart = -1;
        if (data == null || dataStart < 0) return false;
        int limit = Math.Min(data.Length, dataStart + 16);
        int offset = dataStart;
        while (offset <= limit - 3 && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset > limit - 3 || !MfcPrimitives.IsWCharPrefix(data, offset))
            return false;

        int cursor = offset;
        if (MfcPrimitives.ReadWChar(data, ref cursor, data.Length) == null)
            return false;

        int[] candidates = allowTwoByteHeaderPadding ? new[] { cursor, cursor + 2 } : new[] { cursor };
        foreach (int candidate in candidates)
        {
            if (candidate < 0 || candidate + 12 > data.Length)
                continue;
            if (requireEmptyWCharAt10)
            {
                if (candidate + 0x14 > data.Length ||
                    data[candidate + 0x10] != 0xFF || data[candidate + 0x11] != 0xFE ||
                    data[candidate + 0x12] != 0xFF || data[candidate + 0x13] != 0x00)
                {
                    continue;
                }
            }
            uint code = BitConverter.ToUInt32(data, candidate + 8);
            if (minTypeId.HasValue && code < minTypeId.Value)
                continue;
            if (maxTypeId.HasValue && code > maxTypeId.Value)
                continue;

            typeId = code;
            headerStart = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Reads the <c>u16</c> selection ID array at <c>HeaderStart + 0x1A</c> (count followed by <c>count</c> UInt16 IDs).
    /// </summary>
    public bool TryReadHeaderSelectionIds(out IReadOnlyList<ushort> selectionIds, int maxCount = 32)
    {
        selectionIds = Array.Empty<ushort>();
        if (HeaderStart < 0 || HeaderStart + 0x1C > RawData.Length)
            return false;
        ushort count = BitConverter.ToUInt16(RawData, HeaderStart + 0x1A);
        if (count == 0 || count > maxCount || HeaderStart + 0x1C + count * 2 > RawData.Length)
            return false;
        var ids = new ushort[count];
        for (int i = 0; i < count; i++)
            ids[i] = BitConverter.ToUInt16(RawData, HeaderStart + 0x1C + i * 2);
        selectionIds = Array.AsReadOnly(ids);
        return true;
    }

    /// <summary>
    /// Narrows the window end to the next top-level (<c>Depth == 0</c>, <c>_c</c>) MFC element after <see cref="Start"/>.
    /// Useful for legacy tutorial streams where subsequent features use compact class references.
    /// </summary>
    public int FindNextTopLevelFeatureBoundary()
    {
        int boundary = End;
        foreach (var element in Document.AllElements)
        {
            if (element.TagOffset <= Start || element.TagOffset >= boundary)
                continue;
            if (element.TagByte != 0x01 || element.Depth != 0)
                continue;
            if (element.ClassName.IndexOf("_c", StringComparison.Ordinal) < 0)
                continue;
            boundary = element.TagOffset;
        }
        return boundary;
    }

    /// <summary>
    /// Convenience overload that populates <paramref name="context"/> and returns <c>true</c> if the window is valid.
    /// </summary>
    public static bool TryCreate(
        MfcDocument doc,
        int start,
        int end,
        ushort? expectedStreamVersion,
        out FeatureWindowContext? context,
        string className = "",
        uint typeId = 0)
    {
        context = TryCreate(
            doc,
            className,
            typeId,
            start,
            end,
            expectedStreamVersion: expectedStreamVersion);
        return context != null;
    }

    public List<MfcElement> GetNodes(string subClassName) =>
        Nodes.Where(e => e.ClassName == subClassName).ToList();

    public MfcElement? GetSingleNode(string subClassName)
    {
        MfcElement? match = null;
        foreach (var node in Nodes)
        {
            if (node.ClassName == subClassName)
            {
                if (match != null) return null;
                match = node;
            }
        }
        return match;
    }

    public bool HasExactNodeCount(string subClassName, int expectedCount) =>
        Nodes.Count(e => e.ClassName == subClassName) == expectedCount;

    /// <summary>
    /// Returns the offset of the next MFC tag after <paramref name="element"/> within this feature window,
    /// or <see cref="End"/> if <paramref name="element"/> is the last tag in the window.
    /// </summary>
    public int NextTagOffset(MfcElement element)
    {
        int next = End;
        foreach (var candidate in Document.AllElements)
        {
            if (candidate.TagOffset > element.TagOffset && candidate.TagOffset < next)
                next = candidate.TagOffset;
        }
        return next;
    }

    /// <summary>
    /// Slices the payload of <paramref name="element"/> up to the next MFC tag in the window
    /// (bypassing generic scanner truncation on internal <c>FFFF</c> sentinels), optionally requiring
    /// an exact byte length.
    /// </summary>
    public byte[]? SlicePayload(MfcElement? element, int? expectedLength = null)
    {
        if (element == null) return null;
        int payloadEnd = expectedLength.HasValue
            ? element.DataStart + expectedLength.Value
            : NextTagOffset(element);

        if (element.DataStart < 0 || payloadEnd <= element.DataStart || payloadEnd > End || payloadEnd > RawData.Length)
            return null;

        if (expectedLength.HasValue && NextTagOffset(element) - element.DataStart != expectedLength.Value)
        {
            // When slicing a fixed-length block that contains internal FFFF sentinels, verify
            // that the end of the fixed block aligns with either the window end or a valid MFC tag.
            if (payloadEnd != End && !MfcPrimitives.IsMfcTag(RawData, payloadEnd))
                return null;
        }

        byte[] slice = MfcPrimitives.CopyRange(RawData, element.DataStart, payloadEnd);
        if (expectedLength.HasValue && slice.Length != expectedLength.Value)
            return null;
        return slice;
    }

    public byte[]? SlicePayloadOneOf(MfcElement? element, params int[] expectedLengths)
    {
        if (element == null || expectedLengths == null || expectedLengths.Length == 0)
            return null;
        foreach (int len in expectedLengths)
        {
            byte[]? slice = SlicePayload(element, len);
            if (slice != null)
                return slice;
        }
        return null;
    }

    public bool HasSingleNodeWithLength(string subClassName, int expectedLength)
    {
        var single = GetSingleNode(subClassName);
        if (single == null) return false;
        return NextTagOffset(single) - single.DataStart == expectedLength;
    }

    public bool HasSingleNodeWithLengthOneOf(string subClassName, params int[] expectedLengths)
    {
        var single = GetSingleNode(subClassName);
        if (single == null || expectedLengths == null) return false;
        int actual = NextTagOffset(single) - single.DataStart;
        foreach (int len in expectedLengths)
        {
            if (actual == len)
                return true;
        }
        return false;
    }

    public bool RequireSingleNodesWithLengths(params (string ClassName, int ExpectedLength)[] specs)
    {
        if (specs == null) return true;
        foreach (var (className, expectedLength) in specs)
        {
            if (!HasSingleNodeWithLength(className, expectedLength))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks whether <paramref name="targetNode"/> is immediately preceded by the specified sequence of
    /// sibling class names in <see cref="Nodes"/>.
    /// </summary>
    public bool HasPrecedingSequence(MfcElement targetNode, params string[] precedingClassNames)
    {
        if (precedingClassNames == null || precedingClassNames.Length == 0)
            return true;

        int idx = -1;
        for (int i = 0; i < Nodes.Count; i++)
        {
            if (ReferenceEquals(Nodes[i], targetNode))
            {
                idx = i;
                break;
            }
        }

        if (idx < precedingClassNames.Length)
            return false;

        int startIdx = idx - precedingClassNames.Length;
        for (int j = 0; j < precedingClassNames.Length; j++)
        {
            if (Nodes[startIdx + j].ClassName != precedingClassNames[j])
                return false;
        }

        return true;
    }

    // 鈹€鈹€ Named Parameters (shortcuts forwarding to Parameters bag) 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public List<(int Offset, double Value)> FindDoubleParameters(string name) =>
        Parameters.FindAll(name);

    public bool TryGetUniqueDoubleParameter(string name, out double value) =>
        Parameters.TryGetUnique(name, out value);

    public bool TryGetUniqueDoubleByWChar(
        string name,
        out double value,
        out int wcharOffset,
        int? customStart = null,
        int? customEnd = null,
        bool requireSchemaPrefix = false) =>
        Parameters.TryGetByWChar(
            name,
            out value,
            out wcharOffset,
            customStart: customStart,
            customEnd: customEnd,
            requireSchemaPrefix: requireSchemaPrefix);

    // 鈹€鈹€ Topology Extraction (shortcuts forwarding to Selections bag) 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€

    public FeatureReferenceData? TryReadSingleEdgeReference() =>
        Selections.TryReadSingleEdgeReference();

    public bool TryReadSingleEdgeReference(
        out FeatureReferenceData? edgeReference,
        int? expectedCompEdgeLength = null) =>
        Selections.TryReadSingleEdgeReference(out edgeReference, expectedCompEdgeLength);
}

/// <summary>
/// Parameter Bag for a <see cref="FeatureWindowContext"/>. Provides unified access to all named
/// double and integer parameters (<c>0x8004 + WChar</c> or raw <c>WChar + scalar descriptor</c>)
/// inside the feature window or a sub-interval.
/// </summary>
internal sealed class FeatureParameterBag
{
    private readonly FeatureWindowContext _ctx;
    private IReadOnlyList<(string Name, int Offset, double Value)>? _allSchemaDoubles;

    internal FeatureParameterBag(FeatureWindowContext ctx)
    {
        _ctx = ctx;
    }

    /// <summary>
    /// All <c>0x8004</c>-prefixed named double parameters in the feature window, in stream order.
    /// </summary>
    public IReadOnlyList<(string Name, int Offset, double Value)> All =>
        _allSchemaDoubles ??= MfcParameterReader.ScanAllNamedDoubles(_ctx.RawData, _ctx.Start, _ctx.End);

    public List<(int Offset, double Value)> FindAll(string name) =>
        MfcParameterReader.FindNamedDoubles(_ctx.RawData, _ctx.Start, _ctx.End, name);

    public bool TryGetUnique(string name, out double value)
    {
        var matches = FindAll(name);
        if (matches.Count == 1)
        {
            value = matches[0].Value;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Reads a unique named parameter by its WChar name within <c>[customStart ?? Start, customEnd ?? End)</c>.
    /// Supports sheet-metal style consistent positive parameter extraction (<paramref name="onlyPositive"/> +
    /// <paramref name="allowIdenticalDuplicates"/>).
    /// </summary>
    public bool TryGetByWChar(
        string name,
        out double value,
        out int wcharOffset,
        int? customStart = null,
        int? customEnd = null,
        bool requireSchemaPrefix = false,
        bool allowIdenticalDuplicates = false,
        bool onlyPositive = false,
        bool allowZero = true)
    {
        return MfcParameterReader.TryReadUniqueDoubleByWChar(
            _ctx.RawData,
            name,
            customStart ?? _ctx.Start,
            customEnd ?? _ctx.End,
            out value,
            out wcharOffset,
            requireSchemaPrefix: requireSchemaPrefix,
            allowIdenticalDuplicates: allowIdenticalDuplicates,
            onlyPositive: onlyPositive,
            allowZero: allowZero);
    }

    /// <summary>
    /// Reads a consistent positive named double parameter (matching sheet-metal <c>D1</c>/<c>D4</c>/<c>D6</c>/<c>D7</c>
    /// semantics where identical positive copies are coalesced and non-positive copies are ignored).
    /// </summary>
    public bool TryGetPositiveConsistent(
        string name,
        out double value,
        out int wcharOffset,
        int? customStart = null,
        int? customEnd = null,
        bool allowZero = false,
        bool allowIdenticalDuplicates = true)
    {
        return TryGetByWChar(
            name,
            out value,
            out wcharOffset,
            customStart: customStart,
            customEnd: customEnd,
            requireSchemaPrefix: false,
            allowIdenticalDuplicates: allowIdenticalDuplicates,
            onlyPositive: true,
            allowZero: allowZero);
    }

    /// <summary>
    /// Reads the first valid named double parameter matching <paramref name="name"/> in <c>[customStart ?? Start, customEnd ?? End)</c>.
    /// </summary>
    public bool TryGetFirst(
        string name,
        out double value,
        out int wcharOffset,
        int? customStart = null,
        int? customEnd = null)
    {
        return MfcParameterReader.TryReadFirstDoubleByWChar(
            _ctx.RawData,
            name,
            customStart ?? _ctx.Start,
            customEnd ?? _ctx.End,
            out value,
            out wcharOffset);
    }

    /// <summary>
    /// Reads and validates a double parameter across one or more relative byte offsets from <paramref name="baseOffset"/>,
    /// requiring all copies to agree within <paramref name="tolerance"/> and fall in <c>(minExclusive, maxInclusive]</c>.
    /// </summary>
    public bool TryReadVerifiedDoubleSlots(
        int baseOffset,
        int[] relativeOffsets,
        out double value,
        double minExclusive = 0.0,
        double maxInclusive = double.MaxValue,
        double tolerance = 1e-12)
    {
        value = 0;
        if (relativeOffsets == null || relativeOffsets.Length == 0)
            return false;

        byte[] data = _ctx.RawData;
        int firstOffset = baseOffset + relativeOffsets[0];
        if (firstOffset < 0 || firstOffset + 8 > data.Length)
            return false;

        double first = BitConverter.ToDouble(data, firstOffset);
        if (double.IsNaN(first) || double.IsInfinity(first) || first <= minExclusive || first > maxInclusive)
            return false;

        for (int i = 1; i < relativeOffsets.Length; i++)
        {
            int pos = baseOffset + relativeOffsets[i];
            if (pos < 0 || pos + 8 > data.Length)
                return false;
            double copy = BitConverter.ToDouble(data, pos);
            if (double.IsNaN(copy) || double.IsInfinity(copy) || Math.Abs(first - copy) > tolerance)
                return false;
        }

        value = first;
        return true;
    }
}

/// <summary>
/// Selection Bag for a <see cref="FeatureWindowContext"/>. Centralizes extraction of topology
/// references (<c>moEdgeRef_c</c>, <c>moCompEdge_c</c>, <c>moFaceRef_c</c>, <c>moCompFace_c</c>).
/// </summary>
internal sealed class FeatureSelectionBag
{
    private readonly FeatureWindowContext _ctx;

    internal FeatureSelectionBag(FeatureWindowContext ctx)
    {
        _ctx = ctx;
    }

    public FeatureReferenceData? TryReadSingleEdgeReference()
    {
        var edgeRef = _ctx.GetSingleNode("moEdgeRef_c");
        if (edgeRef == null) return null;
        byte[]? edgeRaw = _ctx.SlicePayload(edgeRef);
        return edgeRaw == null ? null : PersistentEdgeReferenceParser.ParseSingle(edgeRaw);
    }

    public bool TryReadSingleEdgeReference(
        out FeatureReferenceData? edgeReference,
        int? expectedCompEdgeLength = null)
    {
        edgeReference = null;
        var edgeRef = _ctx.GetSingleNode("moEdgeRef_c");
        var compEdge = _ctx.GetSingleNode("moCompEdge_c");
        if (edgeRef == null || compEdge == null)
            return false;

        if (expectedCompEdgeLength.HasValue)
        {
            byte[]? compEdgeRaw = _ctx.SlicePayload(compEdge);
            if (compEdgeRaw == null || compEdgeRaw.Length != expectedCompEdgeLength.Value)
                return false;
        }

        byte[]? edgeRaw = _ctx.SlicePayload(edgeRef);
        if (edgeRaw == null) return false;
        edgeReference = PersistentEdgeReferenceParser.ParseSingle(edgeRaw);
        return edgeReference != null;
    }

    /// <summary>
    /// Validates that exactly one <c>moEdgeRef_c</c> and one <c>moCompEdge_c</c> exist in the optional
    /// sub-range <c>[minOffset, maxOffset)</c> and reads the 32-bit edge count from <c>moEdgeRef_c</c>.
    /// </summary>
    public bool TryReadSingleEdgeCount(
        out int edgeCount,
        int? minOffset = null,
        int? maxOffset = null)
    {
        edgeCount = 0;
        int lo = minOffset ?? _ctx.Start;
        int hi = maxOffset ?? _ctx.End;

        var edgeRefs = _ctx.Nodes.Where(e => e.ClassName == "moEdgeRef_c" && e.TagOffset >= lo && e.TagOffset < hi).ToList();
        var compEdges = _ctx.Nodes.Where(e => e.ClassName == "moCompEdge_c" && e.TagOffset >= lo && e.TagOffset < hi).ToList();
        if (edgeRefs.Count != 1 || compEdges.Count != 1)
            return false;

        var edgeRef = edgeRefs[0];
        if (edgeRef.DataEnd - edgeRef.DataStart < 4)
            return false;

        uint count = BitConverter.ToUInt32(_ctx.RawData, edgeRef.DataStart);
        if (count == 0 || count > int.MaxValue)
            return false;

        edgeCount = (int)count;
        return true;
    }

    /// <summary>
    /// Extracts a single fixed-face <see cref="EntityReference"/> from <c>moFaceRef_c</c> + <c>moCompFace_c</c>
    /// (and optional <c>moEndFaceSurfIdRep_c</c> / <c>moEndFace3IntSurfIdRep_c</c>) in the window.
    /// </summary>
    public bool TryReadSingleFaceEntityReference(
        out EntityReference? faceEntity,
        out MfcElement? faceRefElement)
    {
        faceEntity = null;
        faceRefElement = null;

        var faceRefs = _ctx.GetNodes("moFaceRef_c");
        var compFaces = _ctx.GetNodes("moCompFace_c");
        if (faceRefs.Count != 1 || compFaces.Count != 1)
            return false;

        var faceRef = FeatureDataParsers.ParseFaceRef(faceRefs[0]);
        var compFace = TopologyParsers.ParseCompFace(compFaces[0]);
        if (faceRef == null || compFace == null)
            return false;

        var endFaceElement = _ctx.Nodes.FirstOrDefault(e => e.ClassName == "moEndFaceSurfIdRep_c");
        var endFace = endFaceElement == null ? null : FeatureDataParsers.ParseEndFaceSurfIdRep(endFaceElement);
        var endFace3Element = _ctx.Nodes.FirstOrDefault(e => e.ClassName == "moEndFace3IntSurfIdRep_c");
        var endFace3Int = endFace3Element == null ? null : TopologyParsers.ParseEndFace3IntSurfIdRep(endFace3Element);

        uint faceId = compFace.FaceIndex;
        if (faceId == 0)
            faceId = faceRef.FaceIds.FirstOrDefault(id => id > 0);
        if (faceId == 0 && endFace3Int != null)
            faceId = endFace3Int.FaceIndex;

        string surfaceName = endFace?.SurfaceName ?? string.Empty;
        if (faceId == 0 && string.IsNullOrEmpty(surfaceName))
            return false;

        faceRefElement = faceRefs[0];
        faceEntity = new EntityReference
        {
            EntityId = faceId,
            EntityType = SelectionEntityType.Face,
            Name = !string.IsNullOrEmpty(surfaceName) ? surfaceName : $"Face_{faceId}"
        };
        return true;
    }

    /// <summary>
    /// Parses a single 20-byte <c>0x802C</c> component entity reference (<c>moCompFace_c</c>, <c>moCompEdge_c</c>,
    /// <c>moCompSolidBody_c</c>, <c>moCompSurfaceBody_c</c>) at <paramref name="dataStart"/>.
    /// </summary>
    public static EntityReference? TryParseComponentEntityAt(
        byte[] data,
        int dataStart,
        string childClass,
        SelectionEntityType entityType)
    {
        if (data == null || dataStart < 0 || dataStart + 20 > data.Length)
            return null;
        if (BitConverter.ToUInt16(data, dataStart) != 0x802C ||
            BitConverter.ToUInt16(data, dataStart + 2) != 2)
            return null;
        uint entityId = BitConverter.ToUInt32(data, dataStart + 12);
        if (entityId == 0)
            return null;
        uint componentId = BitConverter.ToUInt32(data, dataStart + 8);
        return new EntityReference
        {
            EntityType = entityType,
            EntityId = entityId,
            Name = $"{childClass}:{componentId:X8}:{entityId:X8}"
        };
    }

    /// <summary>
    /// Reads all <c>0x802C</c> component entity references of <paramref name="childClass"/> after <see cref="FeatureWindowContext.Start"/>.
    /// </summary>
    public IReadOnlyList<EntityReference>? TryReadComponentEntities(
        string childClass,
        SelectionEntityType entityType,
        int? maxCount = null)
    {
        var children = _ctx.Nodes
            .Where(e => e.ClassName == childClass && e.TagOffset > _ctx.Start)
            .ToList();
        if (children.Count == 0 || (maxCount.HasValue && children.Count > maxCount.Value))
            return null;

        var references = new List<EntityReference>(children.Count);
        foreach (var child in children)
        {
            var reference = TryParseComponentEntityAt(_ctx.RawData, child.DataStart, childClass, entityType);
            if (reference == null)
                return null;
            references.Add(reference);
        }
        return references.AsReadOnly();
    }

    /// <summary>
    /// Reads the first <c>0x802C</c> component entity reference of <paramref name="childClass"/> after <see cref="FeatureWindowContext.Start"/>.
    /// </summary>
    public EntityReference? TryReadFirstComponentEntity(string childClass, SelectionEntityType entityType)
    {
        var child = _ctx.Nodes.FirstOrDefault(e => e.ClassName == childClass && e.TagOffset > _ctx.Start);
        return child == null ? null : TryParseComponentEntityAt(_ctx.RawData, child.DataStart, childClass, entityType);
    }

    /// <summary>
    /// Reads a 52-byte <c>0x4A38</c> sketch curve reference record at <paramref name="recordOffset"/>.
    /// </summary>
    public static bool TryReadSketchRefAt(byte[] data, int recordOffset, out uint id, out uint serial)
    {
        id = 0;
        serial = 0;
        if (data == null || recordOffset < 0 || recordOffset + 52 > data.Length ||
            BitConverter.ToUInt32(data, recordOffset) != 0x4A38)
            return false;
        id = BitConverter.ToUInt32(data, recordOffset + 4);
        if (id == 0 ||
            BitConverter.ToUInt32(data, recordOffset + 36) != 0xFFFFCFC7 ||
            BitConverter.ToUInt32(data, recordOffset + 40) != 0xFFFFCFC7 ||
            BitConverter.ToUInt32(data, recordOffset + 44) != 0xFFFFCFC7)
            return false;
        serial = BitConverter.ToUInt32(data, recordOffset + 8);
        return true;
    }

    /// <summary>
    /// Reads a strided chain of 52-byte <c>0x4A38</c> sketch curve reference records starting at <paramref name="startOffset"/>.
    /// </summary>
    public List<(uint Id, uint Serial)> ReadSketchRefChain(int startOffset, int stride, int maxRecords)
    {
        var records = new List<(uint Id, uint Serial)>();
        for (int i = 0; i < maxRecords; i++)
        {
            int pos = startOffset + i * stride;
            if (!TryReadSketchRefAt(_ctx.RawData, pos, out uint id, out uint serial))
                break;
            records.Add((id, serial));
        }
        return records;
    }
}
