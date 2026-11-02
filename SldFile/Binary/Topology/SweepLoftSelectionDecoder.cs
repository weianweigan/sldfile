using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.Topology;

/// <summary>Feature family decoded by <see cref="SweepLoftSelectionDecoder"/>.</summary>
internal enum SweepLoftSelectionKind
{
    Unknown = 0,
    Sweep = 1,
    Loft = 2
}

/// <summary>Decoded sweep/loft selection layer.</summary>
internal sealed class SweepLoftSelectionData
{
    public SweepLoftSelectionKind Kind { get; set; }

    public string ClassName { get; set; } = string.Empty;

    /// <summary>Opaque folder-header type code (+0x08 after the display name).</summary>
    public uint SerializedTypeCode { get; set; }

    /// <summary>Opaque ordered selection ids from the folder header (+0x1A/+0x1C).</summary>
    public IReadOnlyList<ushort> HeaderSelectionIds { get; set; } = Array.Empty<ushort>();

    /// <summary>Decoded sketch references in wrapper order (profiles first, then path/guides).</summary>
    public IReadOnlyList<SketchCurveReference> References { get; set; } = Array.Empty<SketchCurveReference>();

    /// <summary>True for the circular-profile sweep layout (single reference is the path).</summary>
    public bool CircularProfileLayout { get; set; }

    /// <summary>True when the thin-feature side-face class / wire-contact thin flag is present.</summary>
    public bool ThinFeatureLayout { get; set; }

    /// <summary>Circular profile diameter located via ThreeDdiameterDim_c + moLengthParameter_c.</summary>
    public double? CircularProfileDiameter { get; set; }

    /// <summary>Thin wall thickness located via the wire-contact flag/value and dimension pair.</summary>
    public double? ThinWallThickness { get; set; }

    public bool IsHeaderSelectionIdMappingPositional =>
        References.Count > 0 && References.Any(r => r.IsHeaderSelectionIdPositional);
}

/// <summary>
/// Decoder for the sweep/loft selection layer established by the 2026
/// SweepOptions/LoftOptions controlled samples.
///
/// Evidence (Contents/Config-0-ResolvedFeatures, see
/// reverse_workspace/notes/agent4-sweep-loft-evidence.md for the folder header):
///   Sweep class moSweep_c, loft class moBlend_c. The feature window contains no
///   moFromSktEntSurfIdRep_c / moSketchChain_c. Sketch references are serialized
///   as 52-byte records:
///     +0  u32 0x4A38 (19000)
///     +4  u32 sketch persistent id
///     +8  u32 archive serial (same for records created by one feature)
///     +12 u32 0/1, +16 u32 0
///     +20 u32 opaque (0x65 for plain refs, 0xA2/0xA5/0x9F for the sweep primary)
///     +24 u32 0, +28 u32 0 or 0xFFFFFFFF, +32 u32 0
///     +36..+44 3 x 0xFFFFCFC7 (-12345 sentinel), +48 u32 0
///   Null placeholders repeat the magic with sketch id 0 (different tail layout).
///
///   moCompProfile_c holds the profile list: first record at wrapper +81, object
///   stride 188 (record 52 + null placeholder 52 + 84 bytes). The number of
///   objects is 1 for sweep, 2/3 for loft. For a circular-profile sweep the single
///   object references the path sketch (native Profile=null, Path=Path1).
///   moWireContact_c holds secondary curves: first record at wrapper +147, object
///   stride 190. Sweep: object 0 = path, further objects = guides. Loft: objects
///   are guides. Loft non-guide samples have no moWireContact_c.
///
///   Selected ids in the folder header (+0x1A count, +0x1C ids) are opaque 16-bit
///   values. Their count always equals the number of non-null wrapper records, and
///   no byte-level link to sketch ids exists in any observed sample, so the
///   header id is correlated by position only (SketchCurveReference
///   .IsHeaderSelectionIdPositional = true). The positional correlation is
///   corroborated by the Agent 5 creation harness: it calls SelectByID2 in the
///   order profile (mark 1) -> path (mark 4) -> guide (mark 2), i.e. the same
///   order as the wrapper records; the header lists are not numerically sorted
///   (sweep_guide1: C5, DC, D1), which is consistent with insertion order.
///
///   Thin sweep: moSideFace3IntSurfIdRep_c replaces moSweepSideSurfIdRep_c and
///   moWireContact_c[+285] == 1 with f64 0.001 at +286; the same 0.001 appears in
///   moLengthParameter_c[+32] (name "D1") after ParallelPlaneDistanceDim_c.
///   Circular sweep: ThreeDdiameterDim_c is followed by moLengthParameter_c
///   (name "D3") with f64 0.012 at +32.
///
/// Unknown/tampered layouts return null instead of guessed values.
/// </summary>
internal static class SweepLoftSelectionDecoder
{
    internal const string SweepClassName = "moSweep_c";
    internal const string LoftClassName = "moBlend_c";

    private const uint RefMagic = 0x4A38;
    private const uint RefSentinel = 0xFFFFCFC7;
    private const int RefRecordLength = 52;
    private const int PrimaryFirstRecordOffset = 81;
    private const int PrimaryRecordStride = 188;
    private const int SecondaryFirstRecordOffset = 147;
    private const int SecondaryRecordStride = 190;

    internal static SweepLoftSelectionData? Decode(MfcDocument doc, MfcElement featureElement)
    {
        if (doc == null || featureElement == null) return null;
        var tags = CollectWindow(doc, featureElement);
        return DecodeCore(featureElement, tags);
    }

    /// <summary>
    /// Decodes without a document by scanning class tags forward from the feature
    /// tag until the next design-tree container. The MFC tree scanner truncates
    /// feature windows when a wrapper payload contains end-marker byte sequences,
    /// so <see cref="MfcElement.DataEnd"/> is deliberately not used here.
    /// </summary>
    internal static SweepLoftSelectionData? Decode(MfcElement featureElement)
    {
        if (featureElement == null) return null;
        var source = featureElement.SourceData;
        if (featureElement.TagOffset < 0 || featureElement.TagOffset >= source.Length) return null;
        var tags = ScanWindow(source, featureElement.TagOffset);
        return DecodeCore(featureElement, tags);
    }

    private static SweepLoftSelectionData? DecodeCore(MfcElement featureElement, List<WindowTag> tags)
    {
        if (featureElement == null || tags == null) return null;
        var kind = featureElement.ClassName == SweepClassName ? SweepLoftSelectionKind.Sweep
            : featureElement.ClassName == LoftClassName ? SweepLoftSelectionKind.Loft
            : SweepLoftSelectionKind.Unknown;
        if (kind == SweepLoftSelectionKind.Unknown) return null;

        if (!TryReadHeader(featureElement, out uint typeCode, out var headerIds)) return null;

        var primaryTag = tags.FirstOrDefault(t => t.ClassName == "moCompProfile_c");
        if (primaryTag == null) return null;
        if (!TryReadRefList(featureElement.SourceData, primaryTag, PrimaryFirstRecordOffset,
                PrimaryRecordStride, out var primary, out bool primaryLayoutValid))
            return null;
        if (!primaryLayoutValid || primary.Count == 0) return null;

        var secondaryTag = tags.FirstOrDefault(t => t.ClassName == "moWireContact_c");
        var secondary = new List<RefEntry>();
        if (secondaryTag != null)
        {
            if (!TryReadRefList(featureElement.SourceData, secondaryTag, SecondaryFirstRecordOffset,
                    SecondaryRecordStride, out secondary, out bool secondaryLayoutValid))
                return null;
            if (!secondaryLayoutValid) return null;
        }

        int total = primary.Count + secondary.Count;
        if (total != headerIds.Count) return null;

        var references = new List<SketchCurveReference>(total);
        var data = new SweepLoftSelectionData
        {
            Kind = kind,
            ClassName = featureElement.ClassName,
            SerializedTypeCode = typeCode,
            HeaderSelectionIds = headerIds
        };

        if (kind == SweepLoftSelectionKind.Sweep)
        {
            bool circular = headerIds.Count == 1 && primary.Count == 1 && secondary.Count == 0;
            if (circular)
            {
                data.CircularProfileLayout = true;
                AddReference(references, primary[0], SketchCurveRole.Path, headerIds[0]);
            }
            else
            {
                if (primary.Count != 1) return null; // no multi-profile sweep specimen
                AddReference(references, primary[0], SketchCurveRole.Profile, headerIds[0]);
                for (int i = 0; i < secondary.Count; i++)
                {
                    var role = i == 0 ? SketchCurveRole.Path : SketchCurveRole.Guide;
                    AddReference(references, secondary[i], role, headerIds[1 + i]);
                }
            }

            bool thinClass = tags.Any(t => t.ClassName == "moSideFace3IntSurfIdRep_c");
            bool wireThin = false;
            double wireThickness = 0;
            if (secondaryTag != null)
            {
                var source = featureElement.SourceData;
                int wireStart = secondaryTag.DataStart;
                int wireEnd = secondaryTag.DataEnd;
                if (wireEnd - wireStart >= 294 && source[wireStart + 285] == 1)
                {
                    double value = BitConverter.ToDouble(source, wireStart + 286);
                    if (value > 0 && value <= 0.1 && !double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        wireThin = true;
                        wireThickness = value;
                    }
                }
            }
            data.ThinFeatureLayout = thinClass || wireThin;

            if (data.CircularProfileLayout)
                data.CircularProfileDiameter = FindDimensionValue(tags, featureElement.SourceData, "ThreeDdiameterDim_c");
            if (data.ThinFeatureLayout)
            {
                double? dimension = FindDimensionValue(tags, featureElement.SourceData, "ParallelPlaneDistanceDim_c");
                if (wireThin && dimension.HasValue && Math.Abs(dimension.Value - wireThickness) > 1e-12)
                    data.ThinWallThickness = null; // conflicting evidence
                else
                    data.ThinWallThickness = wireThin ? wireThickness : dimension;
            }
        }
        else
        {
            if (primary.Count < 2) return null; // a loft section list needs at least two profiles
            for (int i = 0; i < primary.Count; i++)
                AddReference(references, primary[i], SketchCurveRole.Profile, headerIds[i]);
            for (int i = 0; i < secondary.Count; i++)
                AddReference(references, secondary[i], SketchCurveRole.Guide, headerIds[primary.Count + i]);
        }

        data.References = references.AsReadOnly();
        return data;
    }

    private static void AddReference(List<SketchCurveReference> list, RefEntry entry, SketchCurveRole role, ushort headerId)
    {
        list.Add(new SketchCurveReference
        {
            Role = role,
            EntityId = entry.SketchId,
            ArchiveSerial = entry.Serial,
            HeaderSelectionId = headerId,
            IsHeaderSelectionIdPositional = true
        });
    }

    private static bool TryReadHeader(MfcElement element, out uint typeCode, out IReadOnlyList<ushort> selectionIds)
    {
        typeCode = 0;
        selectionIds = Array.Empty<ushort>();
        byte[] data = element.SourceData;
        int start = element.DataStart;
        // The MFC tree DataEnd can be truncated by end-marker byte patterns inside
        // wrapper payloads, so the header is validated against the stream end using
        // its fixed offsets instead.
        int end = data.Length;
        if (start < 0 || end - start < 0x1E) return false;

        int nameOffset = start;
        while (nameOffset <= end - 3 && nameOffset < start + 16 && !MfcPrimitives.IsWCharPrefix(data, nameOffset))
            nameOffset++;
        if (nameOffset > end - 3 || !MfcPrimitives.IsWCharPrefix(data, nameOffset)) return false;
        int cursor = nameOffset;
        if (MfcPrimitives.ReadWChar(data, ref cursor, end) == null) return false;

        int header = cursor;
        if (header + 0x1C + 2 > end) return false;
        if (data[header + 0x10] != 0xFF || data[header + 0x11] != 0xFE ||
            data[header + 0x12] != 0xFF || data[header + 0x13] != 0x00) return false;
        for (int i = 0x14; i < 0x1A; i++)
            if (data[header + i] != 0) return false;

        uint code = BitConverter.ToUInt32(data, header + 0x08);
        if (code < 0x4B || code > 0x50) return false;
        ushort count = BitConverter.ToUInt16(data, header + 0x1A);
        if (count == 0 || count > 32) return false;
        int idsStart = header + 0x1C;
        if (idsStart + count * 2 > end) return false;
        var ids = new ushort[count];
        for (int i = 0; i < count; i++) ids[i] = BitConverter.ToUInt16(data, idsStart + i * 2);
        typeCode = code;
        selectionIds = Array.AsReadOnly(ids);
        return true;
    }

    private readonly struct RefEntry
    {
        public RefEntry(uint sketchId, uint serial)
        {
            SketchId = sketchId;
            Serial = serial;
        }

        public uint SketchId { get; }
        public uint Serial { get; }
    }

    /// <summary>
    /// Reads the non-null records of one wrapper. Each non-null record must be
    /// followed by a null placeholder (magic + id 0) at +52 of the object.
    /// The loop stops at the first position that does not start a valid record,
    /// which is how sweep wrappers end before unrelated trailing payload.
    /// </summary>
    private static bool TryReadRefList(
        byte[] source,
        WindowTag wrapper,
        int firstOffset,
        int stride,
        out List<RefEntry> entries,
        out bool layoutValid)
    {
        entries = new List<RefEntry>();
        layoutValid = false;
        int start = wrapper.DataStart + firstOffset;
        int end = wrapper.DataEnd;
        if (start + RefRecordLength > end)
        {
            // Wrapper too short to contain any reference record (e.g. the circular
            // sweep's 62-byte moWireContact_c). Valid with zero records.
            layoutValid = true;
            return true;
        }

        int pos = start;
        while (pos + RefRecordLength <= end)
        {
            if (!IsRefRecord(source, pos, end, requireNonNull: true, out uint sketchId, out uint serial))
                break;
            if (!IsRefRecord(source, pos + RefRecordLength, end, requireNonNull: false, out uint nullId, out _) ||
                nullId != 0)
            {
                return false;
            }
            entries.Add(new RefEntry(sketchId, serial));
            pos += stride;
        }

        if (entries.Count == 0)
        {
            // Long wrapper without a decodable reference: layout not established.
            return false;
        }
        layoutValid = true;
        return true;
    }

    private static bool IsRefRecord(byte[] source, int pos, int limit, bool requireNonNull, out uint sketchId, out uint serial)
    {
        sketchId = 0;
        serial = 0;
        if (pos < 0 || pos + RefRecordLength > limit) return false;
        if (BitConverter.ToUInt32(source, pos) != RefMagic) return false;
        sketchId = BitConverter.ToUInt32(source, pos + 4);
        if (sketchId == 0)
        {
            return !requireNonNull;
        }
        if (requireNonNull)
        {
            if (BitConverter.ToUInt32(source, pos + 36) != RefSentinel ||
                BitConverter.ToUInt32(source, pos + 40) != RefSentinel ||
                BitConverter.ToUInt32(source, pos + 44) != RefSentinel ||
                BitConverter.ToUInt32(source, pos + 48) != 0)
                return false;
        }
        serial = BitConverter.ToUInt32(source, pos + 8);
        return true;
    }

    /// <summary>
    /// Locates a dimension value: the first moLengthParameter_c after the given
    /// dimension-kind class tag. moLengthParameter_c payload layout:
    /// +0 u16 0x8004, +2 empty-prefix WChar name (e.g. "D3"), +32 f64 value.
    /// </summary>
    private static double? FindDimensionValue(List<WindowTag> tags, byte[] source, string dimensionClass)
    {
        int dimensionIndex = tags.FindIndex(t => t.ClassName == dimensionClass);
        if (dimensionIndex < 0) return null;
        for (int i = dimensionIndex + 1; i < tags.Count; i++)
        {
            var tag = tags[i];
            if (tag.ClassName != "moLengthParameter_c") continue;
            int valueOffset = tag.DataStart + 32;
            if (valueOffset + 8 > tag.DataEnd || valueOffset + 8 > source.Length) return null;
            double value = BitConverter.ToDouble(source, valueOffset);
            return value > 0 && value <= 1 && !double.IsNaN(value) && !double.IsInfinity(value) ? value : (double?)null;
        }
        return null;
    }

    private sealed class WindowTag
    {
        public string ClassName { get; set; } = string.Empty;
        public int TagOffset { get; set; }
        public int DataStart { get; set; }
        public int DataEnd { get; set; }
    }

    private static List<WindowTag> CollectWindow(MfcDocument doc, MfcElement feature)
    {
        var source = doc.SourceData;
        var tags = new List<WindowTag>();
        int startIndex = -1;
        for (int i = 0; i < doc.AllElements.Count; i++)
        {
            var element = doc.AllElements[i];
            if (ReferenceEquals(element, feature) || element.TagOffset == feature.TagOffset)
            {
                startIndex = i;
                break;
            }
        }
        if (startIndex < 0) return tags;
        for (int i = startIndex; i < doc.AllElements.Count; i++)
        {
            var element = doc.AllElements[i];
            if (i > startIndex && IsFeatureBoundary(element.ClassName)) break;
            tags.Add(new WindowTag
            {
                ClassName = element.ClassName,
                TagOffset = element.TagOffset,
                DataStart = element.DataStart
            });
        }
        FinalizeDataEnds(tags, source.Length);
        return tags;
    }

    private static List<WindowTag> ScanWindow(byte[] source, int featureOffset)
    {
        var tags = new List<WindowTag>();
        int pos = featureOffset;
        bool first = true;
        while (pos <= source.Length - 6)
        {
            if (MfcPrimitives.IsEndMarker(source, pos))
            {
                pos += 6;
                continue;
            }
            if (MfcPrimitives.IsMfcTag(source, pos))
            {
                int saved = pos;
                var tag = MfcPrimitives.ReadMfcTag(source, ref pos);
                if (tag != null)
                {
                    if (!first && IsFeatureBoundary(tag.ClassName)) break;
                    tags.Add(new WindowTag
                    {
                        ClassName = tag.ClassName,
                        TagOffset = tag.TagOffset,
                        DataStart = tag.DataStart
                    });
                    first = false;
                    continue;
                }
                pos = saved + 1;
            }
            else
            {
                pos++;
            }
        }
        FinalizeDataEnds(tags, source.Length);
        return tags;
    }

    private static readonly HashSet<string> FeatureContainerClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "moProfileFeature_c",
        "mo3DProfileFeature_c",
        "moOriginProfileFeature_c",
        "moNativeConfigurationTableFeature_c",
        "moRefPlane_c",
        "moExtrusion_c",
        "moICE_c",
        "moRevolution_c",
        "moSweep_c",
        "moBlend_c",
        "moLoft_c",
        "moChamfer_c",
        "Fillet_c",
        "moSMBaseFlange_c",
        "moShell_c",
        "moDraft_c",
        "moRib_c",
        "moCombineBodies_c",
        "moSplitBody_c",
    };

    private static bool IsFeatureBoundary(string className)
    {
        if (string.IsNullOrEmpty(className)) return false;
        return className.EndsWith("Folder_c", StringComparison.Ordinal) ||
               className.EndsWith("Cabinet_c", StringComparison.Ordinal) ||
               className.EndsWith("_ct", StringComparison.Ordinal) ||
               className.EndsWith("_cM", StringComparison.Ordinal) ||
               FeatureContainerClasses.Contains(className);
    }

    private static void FinalizeDataEnds(List<WindowTag> tags, int windowEnd)
    {
        for (int i = 0; i < tags.Count; i++)
            tags[i].DataEnd = i + 1 < tags.Count ? tags[i + 1].TagOffset : windowEnd;
    }
}
