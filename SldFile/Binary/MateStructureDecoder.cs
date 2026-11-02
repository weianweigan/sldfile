using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>Serialized layout family observed for a mate object.</summary>
internal enum MateStructureLayout
{
    Unknown = 0,

    /// <summary>2012 layout: reference chain is nested under moPlaneRef_w / moPointRef_w.</summary>
    LegacyNested = 1,

    /// <summary>2026 layout: wrappers carry deferred text references; shared topology records follow.</summary>
    DeferredReference2026 = 2,

    /// <summary>
    /// 2026 controlled RefPlane layout (run7 Coincident / run8 Distance):
    /// two moAbsoluteRefPlnData_c plane blocks follow moPlaneRef_w, each carrying a
    /// component instance name and a feature@component@assembly selection name.
    /// </summary>
    PlaneReference2026 = 3,

    /// <summary>
    /// 2026 controlled mate header (188-byte record) without decodeable RefPlane
    /// name pairs: mate type and alignment are established, entity topology is not
    /// (Tangent plane+cylinder faces, Concentric cylinder faces).
    /// </summary>
    ControlledHeader2026 = 4,

    /// <summary>Measured moMateWidth layout: four planar face component references and a D1 distance.</summary>
    Width2026 = 5,

    /// <summary>Measured moMateRackPinionDim_c layout: rack line + pinion circular face and D1 diameter dimension.</summary>
    RackPinion2026 = 6,

    /// <summary>Measured moMatePath_c layout: point selection + general-curve path reference and D1 path value.</summary>
    Path2026 = 7,

    /// <summary>Measured tutorial Hinge/Cam/PlanarAngle/Limit/InPlace layouts.</summary>
    MeasuredMechanical2026 = 8
}

/// <summary>Entity kind of a mate reference, established only by the wrapper/chain class names.</summary>
internal enum MateReferenceEntityKind
{
    Unknown = 0,
    Face = 1,
    SketchEntity = 2,
    Plane = 3,
    Cylinder = 4,
    Edge = 5,
    Circle = 6,
    Path = 7
}

/// <summary>
/// One decoded entity reference: a RefPlane selection (full name path) or a
/// planar/cylindrical face component (component instance + kind only).
/// </summary>
internal sealed class MateEntityReferenceStructure
{
    /// <summary>Component instance selection id, e.g. "cube-1@assembly_mate".</summary>
    public string ComponentInstanceName { get; set; } = string.Empty;

    /// <summary>Feature name before the first '@', e.g. "前视基准面"; empty for face references.</summary>
    public string FeatureName { get; set; } = string.Empty;

    /// <summary>Full serialized selection name; empty for face/cylinder references.</summary>
    public string SelectionName { get; set; } = string.Empty;

    /// <summary>Legacy face-reference class-specific id; null when not available per entity.</summary>
    public uint? PersistentId { get; set; }

    public MateReferenceEntityKind Kind { get; set; } = MateReferenceEntityKind.Plane;
}

/// <summary>One non-zero 32-bit header field candidate with its offset from the header start.</summary>
internal sealed class MateHeaderField
{
    public int Offset { get; set; }
    public uint Value { get; set; }
}

/// <summary>Decoded structure of one serialized mate object (no mate type/alignment claims).</summary>
internal sealed class MateStructureData
{
    public string ClassName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public MateStructureLayout Layout { get; set; }

    /// <summary>Offset of the first header field after the display-name terminator.</summary>
    public int HeaderStart { get; set; }

    /// <summary>Header length in bytes (header start to first child tag).</summary>
    public int HeaderLength { get; set; }

    /// <summary>Non-zero u32 header fields. Candidate-only: semantics are not established.</summary>
    public IReadOnlyList<MateHeaderField> HeaderFields { get; set; } = Array.Empty<MateHeaderField>();

    public IReadOnlyList<MateReferenceStructure> References { get; set; } = Array.Empty<MateReferenceStructure>();

    /// <summary>Deferred topology reference texts, e.g. "moEndFaceSurfIdRep_c,75,0,0, \n".</summary>
    public IReadOnlyList<string> DeferredReferenceTexts { get; set; } = Array.Empty<string>();

    /// <summary>All WChar strings found in the mate window (component names, configuration names, paths).</summary>
    public IReadOnlyList<string> WCharStrings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Native <c>swMateType_e</c> value, decoded only for the controlled 2026
    /// header layout: 0 Coincident, 1 Concentric, 2 Perpendicular, 3 Parallel,
    /// 4 Tangent, 5 Distance, 6 Angle. Null for every other layout.
    /// </summary>
    public uint? MateTypeId { get; set; }

    /// <summary>
    /// Native <c>swMateAlign_e</c> value from the controlled 2026 header:
    /// 0 Aligned, 1 AntiAligned, 2 Closest. Null when the header is not recognized.
    /// </summary>
    public uint? AlignmentId { get; set; }

    /// <summary>Distance in meters (file unit), only for a validated Distance/LimitDistance mate.</summary>
    public double? Distance { get; set; }

    /// <summary>Angle in radians (file unit), only for a validated Angle/LimitAngle mate.</summary>
    public double? Angle { get; set; }

    /// <summary>Gear ratio numerator (D1 of moMateGearDim_c); null when not decoded.</summary>
    public double? GearRatioNumerator { get; set; }

    /// <summary>Gear ratio denominator (D2 of moMateGearDim_c); null when not decoded.</summary>
    public double? GearRatioDenominator { get; set; }

    /// <summary>Screw revolution value (D1 of moMateScrew_c); null when not decoded.</summary>
    public double? ScrewRevolutionsPerUnit { get; set; }

    /// <summary>Measured Width constraint id (swMateWidthOptions_e); null when not established.</summary>
    public uint? WidthConstraintTypeId { get; set; }

    /// <summary>Measured Width distance-from-end (D1 of moLengthParameter_c, meters); null when not dimensioned.</summary>
    public double? WidthDistanceFromEnd { get; set; }

    /// <summary>Measured Width FlipDimension flag; null because the measured stream has no stable field.</summary>
    public bool? WidthFlipDimension { get; set; }

    /// <summary>Measured RackPinion diameter option (swRackPinionMateDistanceOptions_e); null when not decoded.</summary>
    public uint? RackPinionDiameterTypeId { get; set; }

    /// <summary>RackPinion diameter value in meters, derived from D1 and the diameter option.</summary>
    public double? RackPinionDiameterValue { get; set; }

    /// <summary>Measured RackPinion reverse flag; null when the flag pattern is absent.</summary>
    public bool? RackPinionReverse { get; set; }

    /// <summary>Measured Path D1 value (0 = free along path); null when not decoded.</summary>
    public double? PathValue { get; set; }

    /// <summary>True when the measured Path D1 value is zero.</summary>
    public bool? PathIsFree { get; set; }

    /// <summary>
    /// Two controlled-2026 entity references: full RefPlane selections for
    /// Coincident/Distance/Perpendicular/Parallel/Angle, or component+kind planar/
    /// cylindrical face references for Tangent/Concentric.
    /// </summary>
    public IReadOnlyList<MateEntityReferenceStructure> EntityReferences { get; set; } = Array.Empty<MateEntityReferenceStructure>();

    public uint? GetHeaderField(int offset)
    {
        foreach (var field in HeaderFields)
        {
            if (field.Offset == offset)
                return field.Value;
        }
        return null;
    }
}

/// <summary>One wrapper reference (moPlaneRef_w / moPointRef_w) and its decoded chain.</summary>
internal sealed class MateReferenceStructure
{
    public string WrapperClassName { get; set; } = string.Empty;
    public MateReferenceEntityKind Kind { get; set; }
    public IReadOnlyList<string> ChainClassNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> WCharStrings { get; set; } = Array.Empty<string>();
    public FaceReferenceStructure? Face { get; set; }
    public SketchReferenceStructure? Sketch { get; set; }
}

/// <summary>
/// Face reference record (moFaceRef_c) plus the surrounding component/feature data.
/// Offsets are structural: +0 FaceCount, +8 TopologyType, +20/+28 two 8-byte magics.
/// </summary>
internal sealed class FaceReferenceStructure
{
    public uint FaceCount { get; set; }
    public uint TopologyType { get; set; }
    public uint Field12 { get; set; }
    public uint Field16 { get; set; }
    public ulong Magic1 { get; set; }
    public ulong Magic2 { get; set; }

    /// <summary>Feature type id from the first moFromSktEntSurfIdRep_c mapping record.</summary>
    public uint? FeatureTypeId { get; set; }

    /// <summary>Serial/timestamp from the first mapping record.</summary>
    public uint? Serial { get; set; }

    /// <summary>Mapping indices in record order (observed 4,3,2,1).</summary>
    public IReadOnlyList<uint> MappingIndices { get; set; } = Array.Empty<uint>();

    /// <summary>Per-record feature type ids.</summary>
    public IReadOnlyList<uint> MappingFeatureTypes { get; set; } = Array.Empty<uint>();
}

/// <summary>
/// Sketch-entity reference chain (moCompSketchEntHandle_c -> moChainComponent_c ->
/// moReferenceChain_c -> moExtFeatureList_c) with counts and sketch persistent ids.
/// </summary>
internal sealed class SketchReferenceStructure
{
    public uint? ChainComponentCount { get; set; }
    public uint? ReferenceChainCount { get; set; }
    public uint? ExtFeatureCount { get; set; }
    public uint? ExtFeatureTypeId { get; set; }
    public uint? ExtFeatureSerial { get; set; }

    /// <summary>Sketch persistent ids found as 0x4A38 + u32 records in the window.</summary>
    public IReadOnlyList<uint> SketchReferenceIds { get; set; } = Array.Empty<uint>();

    /// <summary>Component instance names (e.g. cube-1@assembly) found in the window.</summary>
    public IReadOnlyList<string> ComponentInstanceNames { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Structural decoder for MoMate* objects in Contents/Config-0-MatesList.
/// <para>
/// Evidence from the 2012, 2026 and controlled RefPlane mate samples:
/// header candidate fields are compared per instance; the 2012 layout nests the
/// reference chain under the wrapper, while the older 2026 layout stores deferred
/// text references (e.g. "moEndFaceSurfIdRep_c,75,0,0, \n") and serializes the
/// shared topology records later in the stream.
/// </para>
/// <para>
/// The controlled 2026 mate samples (run7 Coincident, run8 Distance, plus the
/// variants4 set: Distance aligned/anti-aligned/10/20, Parallel, Perpendicular,
/// Angle30, Tangent, Concentric) establish a common 188-byte mate record:
/// mate type from the exact class name (0 Coincident, 1 Concentric,
/// 2 Perpendicular, 3 Parallel, 4 Tangent, 5 Distance, 6 Angle), alignment from
/// header+0xA1 (0x01 Aligned, 0x02 AntiAligned, 0x00 Closest), and the strict D1
/// parameter for Distance/Angle. Plane-based types also carry two RefPlane
/// name pairs; Tangent/Concentric expose two component+kind planar/cylindrical
/// face references (per-entity persistent ids are not present and stay empty).
/// </para>
/// <para>
/// 2012 (stream 0x0004) classes map to native types from the source assembly's
/// snapshot: moMateCoordinateM -> 20 (Coordinate), moMateCoincidentM -> 0
/// (Coincident). Coincident4's tagged moFaceRef_c and its F2 80 class-reference
/// face instance yield two face-specific ids; component identity is not
/// serialized. 2012 alignment has no correlating header field (C3/C4/C5/C6
/// headers differ only by instance id and wrapper) and stays unresolved.
/// </para>
/// <para>
/// The MFC tree scanner truncates windows on end-marker byte patterns inside
/// payloads, so this decoder scans forward from the mate tag itself and stops at
/// the next mate tag or a bounded window end.
/// </para>
/// </summary>
internal static class MateStructureDecoder
{
    private const int MaxWindowBytes = 0x40000;
    private const int MaxHeaderFields = 64;
    private const int MaxStrings = 256;
    private const int MaxStringLength = 200;
    private const int MaxMappingRecords = 16;
    private const int MappingStride = 20;
    private const uint SketchRefMagic = 0x4A38;

    internal static MateStructureData? Decode(MfcElement? element)
    {
        if (element == null || element.TagByte != 0x01)
            return null;
        if (!IsMateClass(element.ClassName))
            return null;

        byte[] data = element.SourceData;
        int start = element.TagOffset;
        if (start < 0 || start + 8 > data.Length)
            return null;

        if (!TryReadDisplayName(data, element.DataStart, out string displayName, out int headerStart))
            return null;

        var tags = ScanWindow(data, start, out int windowEnd);
        if (tags.Count < 2)
            return null;

        if (element.ClassName == "moMatePath_c")
        {
            var path = TryDecodePath2026(element, data, tags, windowEnd, displayName, headerStart);
            if (path != null)
                return path;
        }

        if (element.ClassName == "moMateWidth" || element.ClassName == "moMateRackPinionDim_c")
        {
            var measured = TryDecodeWidthRackPinion2026(
                element, data, tags, windowEnd, displayName, headerStart);
            if (measured != null)
                return measured;
        }

        // Controlled 2026 mate header (188-byte record, seven measured classes):
        // mate type and alignment come from the header signature; RefPlane name
        // pairs and the D1 Distance/Angle parameter are decoded when present.
        // Legacy/deferred layouts fail the signature and fall through below.
        var controlled = TryDecodeControlled2026(
            element, data, tags, windowEnd, displayName, headerStart);
        if (controlled != null)
            return controlled;

        if (IsMeasuredMechanicalClass(element.ClassName))
        {
            var mechanical = TryDecodeMeasuredMechanical2026(
                element, data, tags, windowEnd, displayName, headerStart);
            if (mechanical != null)
                return mechanical;
        }

        var wrapper = tags.FirstOrDefault(t =>
            t.TagOffset > start &&
            (t.ClassName == "moPlaneRef_w" || t.ClassName == "moPointRef_w"));
        if (wrapper == null)
            return null;

        if (wrapper.TagOffset <= headerStart || wrapper.TagOffset > windowEnd)
            return null;

        var headerFields = ReadHeaderFields(data, headerStart, wrapper.TagOffset);
        if (headerFields.Count == 0)
            return null;

        var wchars = CollectWCharStrings(data, wrapper.DataStart, windowEnd, MaxStrings);
        bool hasDeferredText = wchars.Any(IsDeferredFaceReference);
        bool hasFaceted = tags.Any(t => t.ClassName == "moFacetedData_c");
        bool hasFaceRef = tags.Any(t => t.ClassName == "moFaceRef_c");
        bool hasSketchChain = tags.Any(t => t.ClassName == "moChainComponent_c") &&
                              tags.Any(t => t.ClassName == "moExtFeatureList_c");

        MateReferenceEntityKind kind;
        if (wrapper.ClassName == "moPlaneRef_w")
            kind = (hasFaceRef || hasDeferredText) ? MateReferenceEntityKind.Face : MateReferenceEntityKind.Unknown;
        else
            kind = hasSketchChain ? MateReferenceEntityKind.SketchEntity : MateReferenceEntityKind.Unknown;

        if (kind == MateReferenceEntityKind.Unknown)
            return null;

        uint? legacyTypeId = ResolveLegacyMateTypeId(element.ClassName, data, headerStart);
        var legacyEntities = new List<MateEntityReferenceStructure>();
        if (kind == MateReferenceEntityKind.Face && legacyTypeId.HasValue &&
            BitConverter.ToUInt16(data, 0) == 0x0004)
        {
            // 2012 face mates (Coincident4/5/6) serialize one tagged moFaceRef_c and
            // further face records with the F2 80 class reference. No component name
            // or 0x4A38 persistent marker is present, so only the face-reference
            // class-specific id is exposed per entity.
            foreach (var face in ReadLegacyFaceReferences(data, tags, windowEnd))
            {
                legacyEntities.Add(new MateEntityReferenceStructure
                {
                    Kind = MateReferenceEntityKind.Plane,
                    PersistentId = face.Field16
                });
            }
        }

        var layout = (hasDeferredText || hasFaceted)
            ? MateStructureLayout.DeferredReference2026
            : MateStructureLayout.LegacyNested;

        var reference = new MateReferenceStructure
        {
            WrapperClassName = wrapper.ClassName,
            Kind = kind,
            ChainClassNames = tags
                .Where(t => t.TagOffset >= wrapper.TagOffset && !string.IsNullOrEmpty(t.ClassName))
                .Select(t => t.ClassName)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            WCharStrings = wchars
        };

        if (kind == MateReferenceEntityKind.Face)
            reference.Face = DecodeFace(data, tags, windowEnd);
        else
            reference.Sketch = DecodeSketch(data, tags, windowEnd, wchars);

        var deferred = wchars.Where(IsDeferredFaceReference).ToArray();

        return new MateStructureData
        {
            ClassName = element.ClassName,
            DisplayName = displayName,
            Layout = layout,
            HeaderStart = headerStart,
            HeaderLength = wrapper.TagOffset - headerStart,
            HeaderFields = headerFields,
            References = new[] { reference },
            DeferredReferenceTexts = deferred,
            WCharStrings = wchars,
            MateTypeId = legacyTypeId,
            EntityReferences = legacyEntities.AsReadOnly()
        };
    }

    private static bool IsMateClass(string className) =>
        className.StartsWith("moMate", StringComparison.Ordinal) &&
        !className.StartsWith("moMaterial", StringComparison.Ordinal);

    /// <summary>
    /// Child/auxiliary mate records that must not terminate a mate window.
    /// Measured PathMate1 carries moMateEntityHelpData_c and
    /// moMateAnalysisData_c before its path/display blocks.
    /// </summary>
    private static bool IsMateWindowBreakClass(string className) =>
        IsMateClass(className) &&
        className != "moMateEntityHelpData_c" &&
        className != "moMateAnalysisData_c";

    private static bool ContainsPattern(byte[] data, int start, int end, byte[] pattern)
    {
        int limit = Math.Min(end, data.Length);
        for (int i = Math.Max(0, start); i + pattern.Length <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    // ── Controlled 2026 mate header ────────────────────────────────────

    private const int ControlledHeaderLength = 174;
    private const int MaxComponentPlaneGap = 200;

    private sealed class WCharEntry
    {
        public int Offset { get; set; }
        public int EndOffset { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private static bool IsMeasuredMechanicalClass(string className) => className switch
    {
        "moMateHinge" => true,
        "moMateCamTangent" => true,
        "moMateCamCoincident" => true,
        "moMatePlanarAngleDim_c" => true,
        "moMateLimitPlanarAngleDim_c" => true,
        "moMateLimitDistanceDim_c" => true,
        "moMateInPlace" => true,
        _ => false
    };

    /// <summary>
    /// Measured tutorial mechanical/advanced records. Type comes from the exact
    /// class (Hinge 22, Cam 9, PlanarAngle/LimitAngle 6, LimitDistance 5;
    /// InPlace has no swMateType_e value and stays class-only). Entities use
    /// RefPlane pairs when present, otherwise the deferred face/cylinder
    /// reference texts plus component names. D1 is decoded for the angle/distance
    /// records; upper/lower limits are not present as stable fields in these
    /// measured MatesList records and stay null.
    /// </summary>
    private static MateStructureData? TryDecodeMeasuredMechanical2026(
        MfcElement element,
        byte[] data,
        List<WindowTag> tags,
        int windowEnd,
        string displayName,
        int headerStartHint)
    {
        uint? typeId = element.ClassName switch
        {
            "moMateHinge" => 22u,
            "moMateCamTangent" => 9u,
            "moMateCamCoincident" => 9u,
            "moMatePlanarAngleDim_c" => 6u,
            "moMateLimitPlanarAngleDim_c" => 6u,
            "moMateLimitDistanceDim_c" => 5u,
            _ => null
        };
        int localEnd = FindMeasuredDisplayBoundary(data, element.TagOffset, displayName, windowEnd);
        var localTags = tags.Where(t => t.TagOffset >= element.TagOffset && t.TagOffset < localEnd).ToList();
        var entities = element.ClassName == "moMateInPlace"
            ? new List<MateEntityReferenceStructure>()
            : ReadMechanicalEntities(data, element.TagOffset, localEnd);

        double? angle = null;
        double? distance = null;
        if (element.ClassName == "moMatePlanarAngleDim_c")
            angle = ReadFirstD1(data, element.TagOffset, localEnd, isAngle: true, allowZero: false);
        else if (element.ClassName == "moMateLimitPlanarAngleDim_c")
            angle = ReadFirstD1(data, element.TagOffset, localEnd, isAngle: true, allowZero: true);
        else if (element.ClassName == "moMateLimitDistanceDim_c")
            distance = ReadFirstD1(data, element.TagOffset, localEnd, isAngle: false, allowZero: true);

        uint? alignmentId = typeId.HasValue &&
            TryReadControlledHeaderForClass(element.ClassName, data, headerStartHint, out uint alignment)
            ? alignment
            : (uint?)null;
        int headerStart = FindMeasuredHeaderStart(data, headerStartHint, localEnd);
        int firstChild = localTags
            .Where(t => t.TagOffset > headerStart)
            .Select(t => t.TagOffset)
            .DefaultIfEmpty(localEnd)
            .Min();
        var strings = CollectWCharEntries(data, element.TagOffset, localEnd);

        return new MateStructureData
        {
            ClassName = element.ClassName,
            DisplayName = displayName,
            Layout = MateStructureLayout.MeasuredMechanical2026,
            HeaderStart = headerStart,
            HeaderLength = Math.Max(0, firstChild - headerStart),
            HeaderFields = headerStart > 0
                ? ReadHeaderFields(data, headerStart, Math.Min(firstChild, headerStart + 512))
                : (IReadOnlyList<MateHeaderField>)Array.Empty<MateHeaderField>(),
            References = Array.Empty<MateReferenceStructure>(),
            DeferredReferenceTexts = strings
                .Where(s => IsDeferredMechanicalReference(s.Value))
                .Select(s => s.Value)
                .ToArray(),
            WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray(),
            MateTypeId = typeId,
            AlignmentId = alignmentId,
            Distance = distance,
            Angle = angle,
            EntityReferences = entities.AsReadOnly()
        };
    }

    internal static List<MateEntityReferenceStructure> ReadMechanicalEntities(byte[] data, int start, int end)
    {
        var strings = CollectWCharEntries(data, start, end);
        var pairs = ReadPlanePairs(strings);
        if (pairs.Count >= 1)
            return pairs.Take(2).ToList();

        var result = new List<MateEntityReferenceStructure>();
        foreach (var entry in strings)
        {
            if (!IsDeferredMechanicalReference(entry.Value)) continue;
            var component = strings.FirstOrDefault(c =>
                c.Offset > entry.Offset && c.Offset <= entry.Offset + 300 &&
                IsPathComponentName(c.Value));
            if (component == null) continue;
            result.Add(new MateEntityReferenceStructure
            {
                Kind = ClassifyMechanicalReference(entry.Value),
                ComponentInstanceName = component.Value
            });
        }
        return result;
    }

    internal static double? ReadFirstD1(byte[] data, int start, int end, bool isAngle, bool allowZero)
    {
        byte[] d1WChar = { 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x31, 0x00 };
        int limit = Math.Min(end, data.Length);
        for (int i = Math.Max(0, start); i + d1WChar.Length + 40 <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < d1WChar.Length; j++)
            {
                if (data[i + j] != d1WChar[j]) { match = false; break; }
            }
            if (!match) continue;
            for (int delta = 2; delta >= 0; delta--)
            {
                int pos = i - delta;
                if (pos < start) continue;
                if (!TryReadPathD1Record(data, pos, limit, out double value)) continue;
                if (!allowZero && value <= 0) continue;
                if (isAngle)
                {
                    if (value < 0 || value > 2 * Math.PI) continue;
                }
                else if (value > 1000) continue;
                return value;
            }
        }
        return null;
    }

    private static bool IsDeferredMechanicalReference(string value) =>
        value.StartsWith("moPLineInterSurfIdRep_c,", StringComparison.Ordinal) ||
        value.StartsWith("moEndFaceSurfIdRep_c,", StringComparison.Ordinal) ||
        value.StartsWith("moFromSktEntSurfIdRep_c,", StringComparison.Ordinal) ||
        value.StartsWith("moEndFace3IntSurfIdRep_c,", StringComparison.Ordinal) ||
        value.StartsWith("moSimpleSurfIdRep_c,", StringComparison.Ordinal) ||
        value.StartsWith("moFromSktEnt3IntSurfIdRep_c,", StringComparison.Ordinal);

    private static MateReferenceEntityKind ClassifyMechanicalReference(string value)
    {
        if (value.StartsWith("moPLineInterSurfIdRep_c,", StringComparison.Ordinal) ||
            value.StartsWith("moSimpleSurfIdRep_c,", StringComparison.Ordinal))
            return MateReferenceEntityKind.Cylinder;
        if (value.StartsWith("moEndFaceSurfIdRep_c,", StringComparison.Ordinal) ||
            value.StartsWith("moFromSktEntSurfIdRep_c,", StringComparison.Ordinal) ||
            value.StartsWith("moEndFace3IntSurfIdRep_c,", StringComparison.Ordinal))
            return MateReferenceEntityKind.Plane;
        return MateReferenceEntityKind.Unknown;
    }

    private static int FindMeasuredDisplayBoundary(byte[] data, int start, string displayName, int fallback)
    {
        string prefix = displayName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (prefix.Length == 0) return fallback;
        bool seenSelf = false;
        int i = Math.Max(0, start);
        int limit = Math.Min(fallback, data.Length);
        while (i + 4 <= limit)
        {
            if (!MfcPrimitives.IsWCharPrefix(data, i)) { i++; continue; }
            int cursor = i;
            string? value = MfcPrimitives.ReadWChar(data, ref cursor, limit);
            if (value == null) { i++; continue; }
            if (!seenSelf)
            {
                if (string.Equals(value, displayName, StringComparison.Ordinal)) seenSelf = true;
            }
            else if (value.Length > prefix.Length &&
                     value.StartsWith(prefix, StringComparison.Ordinal) &&
                     value.Skip(prefix.Length).All(char.IsDigit))
            {
                return i;
            }
            i = Math.Max(i + 1, cursor);
        }
        return fallback;
    }

    /// <summary>
    /// Measured moMatePath_c layout (motion_path_tutorial): moPointRef_w carries
    /// a component instance and a PointN selection name; moGeneralCurveRef_w /
    /// moCompProfile_c / moSketchChain_c carry the path; a D1 record (value 0 in
    /// the measured free path mates) lives in moDisplayPathDim_c. The path
    /// component is not repeated as a WChar in this record, so only its kind is
    /// exposed. Alignment is accepted when the controlled header signature holds.
    /// </summary>
    private static MateStructureData? TryDecodePath2026(
        MfcElement element,
        byte[] data,
        List<WindowTag> tags,
        int windowEnd,
        string displayName,
        int headerStartHint)
    {
        var pointTag = tags.FirstOrDefault(t => t.ClassName == "moPointRef_w");
        bool hasPath = tags.Any(t =>
            t.ClassName == "moGeneralCurveRef_w" || t.ClassName == "moCompProfile_c" ||
            t.ClassName == "moSketchChain_c");
        if (pointTag == null || !hasPath) return null;
        var strings = CollectWCharEntries(data, element.TagOffset, windowEnd);
        var component = strings.FirstOrDefault(e =>
            e.Offset > pointTag.TagOffset && IsPathComponentName(e.Value));
        if (component == null) return null;
        var selection = strings.FirstOrDefault(e =>
            e.Offset > component.Offset && e.Value.StartsWith("Point", StringComparison.Ordinal) &&
            e.Value.IndexOf('@') > 0);
        if (selection == null) return null;
        string feature = selection.Value.Substring(0, selection.Value.IndexOf('@'));

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.SketchEntity,
                ComponentInstanceName = component.Value,
                FeatureName = feature,
                SelectionName = selection.Value
            },
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Path
            }
        };

        double? pathValue = null;
        bool? pathFree = null;
        var d1 = strings.FirstOrDefault(e => string.Equals(e.Value, "D1", StringComparison.Ordinal));
        if (d1 != null)
        {
            for (int delta = 2; delta >= 0; delta--)
            {
                int pos = d1.Offset - delta;
                if (pos < element.TagOffset) continue;
                if (TryReadPathD1Record(data, pos, windowEnd, out double value))
                {
                    pathValue = value;
                    pathFree = value == 0;
                    break;
                }
            }
        }

        int headerStart = FindMeasuredHeaderStart(data, headerStartHint, windowEnd);
        int firstChild = tags
            .Where(t => t.TagOffset > headerStart)
            .Select(t => t.TagOffset)
            .DefaultIfEmpty(windowEnd)
            .Min();
        uint? alignmentId = TryReadControlledHeaderForClass(
            element.ClassName, data, headerStartHint, out uint alignment)
            ? alignment
            : (uint?)null;
        var references = new List<MateReferenceStructure>
        {
            new MateReferenceStructure
            {
                WrapperClassName = "moPointRef_w",
                Kind = MateReferenceEntityKind.SketchEntity,
                ChainClassNames = tags
                    .Where(t => t.ClassName == "moPointRef_w" || t.ClassName == "moCompRefPoint_c" ||
                                t.ClassName == "moRefPoint_c" || t.ClassName == "moRefPointData_c")
                    .Select(t => t.ClassName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray()
            },
            new MateReferenceStructure
            {
                WrapperClassName = "moGeneralCurveRef_w",
                Kind = MateReferenceEntityKind.Path,
                ChainClassNames = tags
                    .Where(t => t.ClassName == "moGeneralCurveRef_w" || t.ClassName == "moCompProfile_c" ||
                                t.ClassName == "moSketchChain_c" || t.ClassName == "moContourChooser_c")
                    .Select(t => t.ClassName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                WCharStrings = Array.Empty<string>()
            }
        };

        return new MateStructureData
        {
            ClassName = element.ClassName,
            DisplayName = displayName,
            Layout = MateStructureLayout.Path2026,
            HeaderStart = headerStart,
            HeaderLength = Math.Max(0, firstChild - headerStart),
            HeaderFields = headerStart > 0
                ? ReadHeaderFields(data, headerStart, Math.Min(firstChild, headerStart + 512))
                : (IReadOnlyList<MateHeaderField>)Array.Empty<MateHeaderField>(),
            References = references.AsReadOnly(),
            DeferredReferenceTexts = Array.Empty<string>(),
            WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray(),
            MateTypeId = 15u,
            AlignmentId = alignmentId,
            PathValue = pathValue,
            PathIsFree = pathFree,
            EntityReferences = entities.AsReadOnly()
        };
    }

    private static bool TryReadPathD1Record(byte[] data, int pos, int end, out double value)
    {
        value = 0;
        int limit = Math.Min(end, data.Length);
        if (pos < 0 || pos + 40 > limit) return false;
        int cursor = pos + 2;
        if (MfcPrimitives.ReadWChar(data, ref cursor, limit) != "D1" || cursor != pos + 10) return false;
        if (BitConverter.ToDouble(data, cursor) != 2.0) return false;
        if (BitConverter.ToUInt32(data, cursor + 8) != uint.MaxValue) return false;
        if (BitConverter.ToUInt32(data, cursor + 12) != 0) return false;
        int empty = cursor + 16;
        if (!MfcPrimitives.IsWCharPrefix(data, empty)) return false;
        int emptyCursor = empty;
        if (MfcPrimitives.ReadWChar(data, ref emptyCursor, limit) != string.Empty) return false;
        if (emptyCursor + 10 > limit) return false;
        double v = BitConverter.ToDouble(data, emptyCursor + 2);
        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0 || v > 1000) return false;
        value = v;
        return true;
    }

    /// <summary>
    /// Measured Width/RackPinion layouts (2026 tutorial assemblies resaved by
    /// SOLIDWORKS 2026). They do not match the 174-byte controlled header tail;
    /// type comes from the class name, entities from the deferred reference
    /// blocks, and parameters from D1 plus measured trailing fields.
    /// </summary>
    private static MateStructureData? TryDecodeWidthRackPinion2026(
        MfcElement element,
        byte[] data,
        List<WindowTag> tags,
        int windowEnd,
        string displayName,
        int headerStartHint)
    {
        var strings = CollectWCharEntries(data, element.TagOffset, windowEnd);
        if (strings.Count == 0) return null;
        int headerStart = FindMeasuredHeaderStart(data, headerStartHint, windowEnd);
        int firstChild = tags
            .Where(t => t.TagOffset > headerStart)
            .Select(t => t.TagOffset)
            .DefaultIfEmpty(windowEnd)
            .Min();
        int headerLength = Math.Max(0, firstChild - headerStart);
        var headerFields = headerStart > 0
            ? ReadHeaderFields(data, headerStart, Math.Min(firstChild, headerStart + 512))
            : (IReadOnlyList<MateHeaderField>)Array.Empty<MateHeaderField>();

        if (element.ClassName == "moMateWidth")
        {
            var faceRefs = new List<(WCharEntry Reference, WCharEntry Component)>();
            foreach (var entry in strings)
            {
                bool widthRef = entry.Value.StartsWith("moEndFaceSurfIdRep_c,", StringComparison.Ordinal);
                bool tabRef = entry.Value.StartsWith("moFromSktEntSurfIdRep_c,", StringComparison.Ordinal);
                if (!widthRef && !tabRef) continue;
                var component = strings.FirstOrDefault(c =>
                    c.Offset > entry.Offset && c.Offset <= entry.Offset + 300 &&
                    IsComponentInstanceName(c.Value));
                if (component != null)
                    faceRefs.Add((entry, component));
            }
            var widthFaces = faceRefs
                .Where(r => r.Reference.Value.StartsWith("moEndFaceSurfIdRep_c,", StringComparison.Ordinal))
                .ToArray();
            var tabFaces = faceRefs
                .Where(r => r.Reference.Value.StartsWith("moFromSktEntSurfIdRep_c,", StringComparison.Ordinal))
                .ToArray();
            if (widthFaces.Length != 2 || tabFaces.Length != 2)
                return null;

            // Native IMate2 order: two width faces followed by two tab faces.
            var entities = new List<MateEntityReferenceStructure>(4);
            foreach (var pair in widthFaces.Concat(tabFaces))
            {
                entities.Add(new MateEntityReferenceStructure
                {
                    Kind = MateReferenceEntityKind.Plane,
                    ComponentInstanceName = pair.Component.Value
                });
            }

            double? distance = ReadD1Parameter(data, tags, windowEnd, "moLengthParameter_c", false);
            // The dimensioned form is established by its D1 parameter. The measured
            // stream has no stable field for Centered/Free/Percent.
            uint? constraint = distance.HasValue ? 2u : null;
            bool? flipDimension = null;
            if (distance.HasValue)
            {
                var lengthParameter = tags.FirstOrDefault(t => t.ClassName == "moLengthParameter_c");
                if (lengthParameter != null && lengthParameter.DataStart + 161 < windowEnd)
                {
                    // Measured sign byte in the dimension metadata: 0xBF for
                    // FlipDimension=false, 0x3F for true (two independent D1 pairs).
                    byte sign = data[lengthParameter.DataStart + 161];
                    if (sign == 0xBF) flipDimension = false;
                    else if (sign == 0x3F) flipDimension = true;
                }
            }
            return new MateStructureData
            {
                ClassName = element.ClassName,
                DisplayName = displayName,
                Layout = MateStructureLayout.Width2026,
                HeaderStart = headerStart,
                HeaderLength = headerLength,
                HeaderFields = headerFields,
                References = Array.Empty<MateReferenceStructure>(),
                DeferredReferenceTexts = strings
                    .Where(s => s.Value.StartsWith("moEndFaceSurfIdRep_c,", StringComparison.Ordinal) ||
                                s.Value.StartsWith("moFromSktEntSurfIdRep_c,", StringComparison.Ordinal))
                    .Select(s => s.Value)
                    .ToArray(),
                WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray(),
                MateTypeId = 11u,
                WidthConstraintTypeId = constraint,
                WidthDistanceFromEnd = distance,
                WidthFlipDimension = flipDimension,
                EntityReferences = entities.AsReadOnly()
            };
        }

        if (element.ClassName == "moMateRackPinionDim_c")
        {
            var rackTag = tags.FirstOrDefault(t => t.ClassName == "moCompRefLine_c");
            var pinionTag = tags.FirstOrDefault(t => t.ClassName == "moCylinderRef_w");
            if (rackTag == null || pinionTag == null) return null;
            var rackComponent = strings.FirstOrDefault(e =>
                e.Offset > rackTag.TagOffset && e.Offset < pinionTag.TagOffset &&
                IsComponentInstanceName(e.Value));
            var pinionComponent = strings.FirstOrDefault(e =>
                e.Offset > pinionTag.TagOffset && e.Offset < windowEnd &&
                IsComponentInstanceName(e.Value));
            if (rackComponent == null || pinionComponent == null) return null;

            double? distance = ReadNamedParameter(data, element.TagOffset, windowEnd, "D1");
            uint? diameterType = null;
            if (windowEnd - 4 >= element.TagOffset)
            {
                uint candidate = BitConverter.ToUInt32(data, windowEnd - 4);
                if (candidate <= 1) diameterType = candidate;
            }
            bool? reverse = FindRackPinionReverseFlag(data, element.TagOffset, windowEnd);
            double? diameter = distance.HasValue && diameterType.HasValue
                ? (diameterType.Value == 0 ? distance.Value : distance.Value * Math.PI)
                : (double?)null;

            var entities = new[]
            {
                new MateEntityReferenceStructure
                {
                    Kind = MateReferenceEntityKind.Edge,
                    ComponentInstanceName = rackComponent.Value
                },
                new MateEntityReferenceStructure
                {
                    Kind = MateReferenceEntityKind.Circle,
                    ComponentInstanceName = pinionComponent.Value
                }
            };

            return new MateStructureData
            {
                ClassName = element.ClassName,
                DisplayName = displayName,
                Layout = MateStructureLayout.RackPinion2026,
                HeaderStart = headerStart,
                HeaderLength = headerLength,
                HeaderFields = headerFields,
                References = Array.Empty<MateReferenceStructure>(),
                DeferredReferenceTexts = strings
                    .Where(s => s.Value.StartsWith("moSimpleSurfIdRep_c,", StringComparison.Ordinal))
                    .Select(s => s.Value)
                    .ToArray(),
                WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray(),
                MateTypeId = 13u,
                RackPinionDiameterTypeId = diameterType,
                RackPinionDiameterValue = diameter,
                RackPinionReverse = reverse,
                EntityReferences = entities
            };
        }

        return null;
    }

    private static int FindMeasuredHeaderStart(byte[] data, int hint, int windowEnd)
    {
        int limit = Math.Min(windowEnd, data.Length);
        for (int start = Math.Max(2, hint); start + 6 <= limit && start <= hint + 8; start++)
        {
            if (data[start] != 0 || data[start + 1] != 0 || data[start + 2] != 0 ||
                data[start + 3] != 0 || data[start + 4] != 0 || data[start + 5] != 0x40)
                continue;
            if (data[start - 2] == 0 && data[start - 1] <= 2)
                return start;
        }
        return hint;
    }

    /// <summary>
    /// Measured RackPinion reverse flag: an empty WChar (FF FE FF 00) followed by
    /// 0/1, 00, 7A 81 inside moFavoriteHandle_c. 0 measured native Reverse=true,
    /// 1 measured native Reverse=false.
    /// </summary>
    private static bool? FindRackPinionReverseFlag(byte[] data, int start, int end)
    {
        int limit = Math.Min(end, data.Length);
        bool? result = null;
        int count = 0;
        for (int i = Math.Max(0, start); i + 8 <= limit; i++)
        {
            if (data[i] != 0xFF || data[i + 1] != 0xFE || data[i + 2] != 0xFF || data[i + 3] != 0x00)
                continue;
            if (data[i + 4] > 1 || data[i + 5] != 0x00 || data[i + 6] != 0x7A || data[i + 7] != 0x81)
                continue;
            if (count == 0) result = data[i + 4] == 0;
            count++;
            if (count > 1) return null;
        }
        return count == 1 ? result : null;
    }

    /// <summary>
    /// Measured 2026 mate classes and their native <c>swMateType_e</c> values.
    /// </summary>
    private static uint? ResolveMateTypeId(string className) => className switch
    {
        "moMateCoincident" => 0u,
        "moMateConcentric" => 1u,
        "moMatePerpendicular" => 2u,
        "moMateParallel" => 3u,
        "moMateTangent" => 4u,
        "moMateDistanceDim_c" => 5u,
        "moMateLimitDistanceDim_c" => 5u,
        "moMatePlanarAngleDim_c" => 6u,
        "moMateLimitPlanarAngleDim_c" => 6u,
        "moMateGearDim_c" => 10u,
        "moMateWidth" => 11u,
        "moMateRackPinionDim_c" => 13u,
        "moMatePath_c" => 15u,
        "moMateScrew_c" => 17u,
        "moMateUniversalJoint_c" => 19u,
        _ => null
    };

    /// <summary>
    /// Decodes the controlled 2026 mate record:
    ///   mate header (174 bytes after the display name) with serialized alignment;
    ///   mate type from the exact class name;
    ///   two RefPlane name pairs for Coincident/Distance/Perpendicular/Parallel/Angle;
    ///   two component-instance/kind face references for Tangent/Concentric;
    ///   one strict D1 moLengthParameter_c (Distance, meters) or
    ///   moAngleParameter_c (Angle, radians).
    /// Face/cylinder persistent ids are not per-entity in the controlled samples,
    /// so Tangent/Concentric entity ids stay empty while component+kind are kept.
    /// Legacy 2012 and deferred-face 2026 layouts fail the header signature.
    /// </summary>
    private static MateStructureData? TryDecodeControlled2026(
        MfcElement element,
        byte[] data,
        List<WindowTag> tags,
        int windowEnd,
        string displayName,
        int headerStart)
    {
        uint? typeId = ResolveMateTypeId(element.ClassName);
        if (!typeId.HasValue) return null;
        if (!TryReadControlledHeaderForClass(element.ClassName, data, headerStart, out uint alignmentId))
            return null;

        var strings = CollectWCharEntries(data, element.TagOffset, windowEnd);
        bool planeType = typeId.Value == 0u || typeId.Value == 2u || typeId.Value == 3u ||
                         typeId.Value == 5u || typeId.Value == 6u;
        var pairs = new List<MateEntityReferenceStructure>();
        bool planeLayout = false;
        bool hasPlaneTags = tags.Any(t => t.ClassName == "moPlaneRef_w") &&
                            tags.Any(t => t.ClassName == "moAbsoluteRefPlnData_c");
        // Repeated plane wrappers after the first inline record are serialized with
        // a document-local MFC class reference (measured AE 80 / C4 80 variants), so
        // the wrapper class name is not present as a tag. The nested plane payload
        // signature A5 80 3B 80 is stable and identifies those blocks.
        bool hasPlaneRefPayload = ContainsPattern(data, element.TagOffset, windowEnd,
            new byte[] { 0xA5, 0x80, 0x3B, 0x80 });
        if (planeType &&
            (hasPlaneTags || hasPlaneRefPayload) &&
            !tags.Any(t => t.ClassName == "moFacetedData_c"))
        {
            bool deferred = false;
            foreach (var entry in strings)
            {
                if (IsDeferredFaceReference(entry.Value)) { deferred = true; break; }
            }
            if (!deferred)
            {
                pairs = ReadPlanePairs(strings);
                if (pairs.Count == 2 &&
                    !string.Equals(pairs[0].ComponentInstanceName, pairs[1].ComponentInstanceName, StringComparison.Ordinal) &&
                    !string.Equals(pairs[0].SelectionName, pairs[1].SelectionName, StringComparison.Ordinal))
                {
                    planeLayout = true;
                }
                else
                {
                    pairs = new List<MateEntityReferenceStructure>();
                }
            }
        }
        else if (typeId.Value == 1u || typeId.Value == 4u ||
                 typeId.Value == 10u || typeId.Value == 17u)
        {
            // Tangent / Concentric / Gear / Screw: no feature@component selection
            // names. The two component blocks are classified by the nearest
            // preceding wrapper class (moPlaneRef_w = planar face,
            // moCylinderRef_w = cylindrical face).
            pairs = ReadFaceComponentEntities(strings, tags);
        }

        double? distance = null;
        double? angle = null;
        double? gearNumerator = null;
        double? gearDenominator = null;
        double? screwValue = null;
        if (typeId.Value == 5u)
            distance = ReadD1Parameter(data, tags, windowEnd, "moLengthParameter_c", isAngle: false);
        else if (typeId.Value == 6u)
            angle = ReadD1Parameter(data, tags, windowEnd, "moAngleParameter_c", isAngle: true);
        else if (typeId.Value == 10u)
        {
            gearNumerator = ReadD1Parameter(data, tags, windowEnd, "moLengthParameter_c", isAngle: false);
            gearDenominator = ReadNamedParameter(data, element.TagOffset, windowEnd, "D2");
        }
        else if (typeId.Value == 17u)
            screwValue = ReadD1Parameter(data, tags, windowEnd, "moScalerParameter_c", isAngle: false);

        string[] allStrings = strings
            .Select(s => s.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var references = new List<MateReferenceStructure>();
        if (planeLayout)
        {
            references.Add(new MateReferenceStructure
            {
                WrapperClassName = "moPlaneRef_w",
                Kind = MateReferenceEntityKind.Plane,
                ChainClassNames = tags
                    .Where(t => t.ClassName == "moPlaneRef_w" || t.ClassName == "moAbsoluteRefPlnData_c" ||
                                t.ClassName == "moChainComponent_c" || t.ClassName == "moReferenceChain_c" ||
                                t.ClassName == "moExtFeatureList_c")
                    .Select(t => t.ClassName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                WCharStrings = allStrings
            });
        }

        return new MateStructureData
        {
            ClassName = element.ClassName,
            DisplayName = displayName,
            Layout = planeLayout
                ? MateStructureLayout.PlaneReference2026
                : MateStructureLayout.ControlledHeader2026,
            HeaderStart = headerStart,
            HeaderLength = ControlledHeaderLength,
            HeaderFields = ReadHeaderFields(data, headerStart, headerStart + ControlledHeaderLength),
            References = references.AsReadOnly(),
            DeferredReferenceTexts = Array.Empty<string>(),
            WCharStrings = allStrings,
            MateTypeId = typeId,
            AlignmentId = alignmentId,
            Distance = distance,
            Angle = angle,
            GearRatioNumerator = gearNumerator,
            GearRatioDenominator = gearDenominator,
            ScrewRevolutionsPerUnit = screwValue,
            EntityReferences = pairs.AsReadOnly()
        };
    }

    /// <summary>
    /// Universal-joint specimen header: the standard 174-byte template with the
    /// trailing marker moved from +0xA6 to +0xAC (measured universal_joint sample:
    /// 0,1,0,0,0,0,0,0,2,0 at +0xA0..0xA9; mode 0 = Closest, native alignment 2).
    /// </summary>
    private static bool TryReadUniversalJointHeader(byte[] data, int headerStart, out uint alignmentId)
    {
        alignmentId = 0;
        if (headerStart < 2 || headerStart + ControlledHeaderLength > data.Length)
            return false;
        for (int i = 0; i < 4; i++)
            if (data[headerStart + i] != 0) return false;
        if (data[headerStart + 4] != 0 || data[headerStart + 5] != 0x40 || data[headerStart + 7] != 0)
            return false;
        if (data[headerStart + 14] != 0xFF || data[headerStart + 15] != 0xFE ||
            data[headerStart + 16] != 0xFF || data[headerStart + 17] != 0 || data[headerStart + 18] != 0)
            return false;
        if (data[headerStart + 160] != 0 || data[headerStart + 162] != 0 ||
            data[headerStart + 163] != 1 || data[headerStart + 164] != 0 ||
            data[headerStart + 165] != 0 || data[headerStart + 166] != 0 ||
            data[headerStart + 172] != 2 || data[headerStart + 173] != 0)
            return false;
        for (int i = 167; i < 172; i++)
            if (data[headerStart + i] != 0) return false;
        // Only the Closest alignment has a controlled universal-joint specimen.
        if (data[headerStart + 161] != 0) return false;
        alignmentId = 2;
        return true;
    }

    /// <summary>
    /// Controlled 2026 mate header signature after the display-name terminator:
    ///   +0x00 u32 0 | +0x04 u16 0x4000 | +0x06 u16 instance serial |
    ///   +0x0E empty WChar | +0xA0 00 | +0xA1 serialized alignment |
    ///   +0xA2 00 01 00 00 02 00 00 00 00 00 00.
    /// Alignment bytes measured against native swMateAlign_e:
    ///   0x01 = Aligned(0), 0x02 = AntiAligned(1), 0x00 = Closest(2);
    /// the byte at headerStart-1 is 1 only for the anti-aligned form.
    /// </summary>
    internal static bool TryReadControlledHeader(byte[] data, int headerStart, out uint alignmentId)
    {
        alignmentId = 0;
        if (!HasControlledHeaderSignature(data, headerStart, out byte mode))
            return false;
        byte flip = data[headerStart - 1];
        if (mode == 1)
        {
            // Resave and class-reference records set the post-name byte to 1 while
            // keeping the Aligned mode; it is not an alignment discriminator there.
            alignmentId = 0;
            return true;
        }
        if (mode == 2)
        {
            if (flip != 1) return false;
            alignmentId = 1;
            return true;
        }
        if (mode == 0)
        {
            // Closest records also appear with the resave post-name byte = 1.
            alignmentId = 2;
            return true;
        }
        return false;
    }

    internal static bool TryReadControlledHeaderForClass(
        string className, byte[] data, int headerStart, out uint alignmentId)
    {
        if (className == "moMateUniversalJoint_c")
        {
            // Main specimen uses a shifted tail marker; the resave uses the
            // standard template. Accept either validated layout.
            return TryReadControlledHeader(data, headerStart, out alignmentId) ||
                   TryReadUniversalJointHeader(data, headerStart, out alignmentId);
        }
        return TryReadControlledHeader(data, headerStart, out alignmentId);
    }

    /// <summary>
    /// Class-reference mate records reuse the controlled header signature, but the
    /// two bytes after the display name are not the inline terminator/flip pair
    /// (run6 class-ref records measure flip=1 with the Aligned mode). Only the
    /// aligned mode has a control for this record kind; mode 0x00/0x02 stay null
    /// rather than guessing through the unreliable flip byte.
    /// </summary>
    internal static bool TryReadControlledHeaderForClassRef(byte[] data, int headerStart, out uint alignmentId)
    {
        alignmentId = 0;
        if (!HasControlledHeaderSignature(data, headerStart, out byte mode)) return false;
        if (mode != 1) return false;
        alignmentId = 0;
        return true;
    }

    private static bool HasControlledHeaderSignature(byte[] data, int headerStart, out byte mode)
    {
        mode = 0;
        if (headerStart < 2 || headerStart + ControlledHeaderLength > data.Length)
            return false;
        for (int i = 0; i < 4; i++)
            if (data[headerStart + i] != 0) return false;
        if (data[headerStart + 4] != 0 || data[headerStart + 5] != 0x40 || data[headerStart + 7] != 0)
            return false;
        if (data[headerStart + 14] != 0xFF || data[headerStart + 15] != 0xFE ||
            data[headerStart + 16] != 0xFF || data[headerStart + 17] != 0 || data[headerStart + 18] != 0)
            return false;
        if (data[headerStart + 160] != 0 || data[headerStart + 162] != 0 ||
            data[headerStart + 163] != 1 || data[headerStart + 164] != 0 ||
            data[headerStart + 165] != 0 || data[headerStart + 166] != 2 ||
            data[headerStart + 167] != 0)
            return false;
        for (int i = 168; i < ControlledHeaderLength; i++)
            if (data[headerStart + i] != 0) return false;
        if (data[headerStart - 2] != 0) return false;
        mode = data[headerStart + 161];
        return mode <= 2;
    }

    /// <summary>
    /// Tangent/Concentric entity blocks: exactly two component-instance names in
    /// serialization order. Each entity kind comes from the nearest preceding
    /// wrapper class tag (moPlaneRef_w = planar face, moCylinderRef_w = cylindrical
    /// face); a later block without its own inline tag inherits the last wrapper.
    /// Feature name, selection name and persistent id are not available per entity.
    /// </summary>
    private static List<MateEntityReferenceStructure> ReadFaceComponentEntities(
        IReadOnlyList<WCharEntry> entries, List<WindowTag> tags)
    {
        var components = new List<WCharEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!IsComponentInstanceName(entry.Value)) continue;
            if (!seen.Add(entry.Value)) continue;
            components.Add(entry);
            if (components.Count > 2) return new List<MateEntityReferenceStructure>();
        }
        if (components.Count != 2) return new List<MateEntityReferenceStructure>();

        var anchors = tags
            .Where(t => t.ClassName == "moPlaneRef_w" || t.ClassName == "moCylinderRef_w")
            .OrderBy(t => t.TagOffset)
            .ToList();
        if (anchors.Count == 0) return new List<MateEntityReferenceStructure>();

        var result = new List<MateEntityReferenceStructure>(2);
        foreach (var component in components)
        {
            MateReferenceEntityKind kind = MateReferenceEntityKind.Unknown;
            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                if (anchors[i].TagOffset >= component.Offset) continue;
                kind = anchors[i].ClassName == "moCylinderRef_w"
                    ? MateReferenceEntityKind.Cylinder
                    : MateReferenceEntityKind.Plane;
                break;
            }
            if (kind == MateReferenceEntityKind.Unknown)
                return new List<MateEntityReferenceStructure>();
            result.Add(new MateEntityReferenceStructure
            {
                ComponentInstanceName = component.Value,
                FeatureName = string.Empty,
                SelectionName = string.Empty,
                Kind = kind
            });
        }
        return result;
    }

    internal static List<MateEntityReferenceStructure> ReadPlanePairs(byte[] data, int start, int end) =>
        ReadPlanePairs(CollectWCharEntries(data, start, end));

    private static List<MateEntityReferenceStructure> ReadPlanePairs(IReadOnlyList<WCharEntry> entries)
    {
        var pairs = new List<MateEntityReferenceStructure>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++)
        {
            var component = entries[i];
            if (!IsComponentInstanceName(component.Value)) continue;
            for (int j = i + 1; j < entries.Count; j++)
            {
                var plane = entries[j];
                if (plane.Offset - component.EndOffset > MaxComponentPlaneGap) break;
                if (!TryParseFeatureSelectionName(plane.Value, component.Value, out string feature)) continue;
                string key = component.Value + "\n" + plane.Value;
                if (seen.Add(key))
                {
                    pairs.Add(new MateEntityReferenceStructure
                    {
                        ComponentInstanceName = component.Value,
                        FeatureName = feature,
                        SelectionName = plane.Value,
                        Kind = MateReferenceEntityKind.Plane
                    });
                }
                break;
            }
        }
        return pairs;
    }

    private static bool IsComponentInstanceName(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 120) return false;
        int at = value.IndexOf('@');
        if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1) return false;
        for (int i = 0; i < value.Length; i++)
            if (value[i] < 0x20) return false;
        return true;
    }

    /// <summary>
    /// Path point components can be nested component-instance paths with more
    /// than one '@' (e.g. chain_link1-1@assembly/pin-1@chain_link1). Selection
    /// names start with the feature token (Point2/Point3), so accept multi-@
    /// values that are not feature selections.
    /// </summary>
    private static bool IsPathComponentName(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 220) return false;
        if (value.IndexOf('@') <= 0) return false;
        string[] featurePrefixes = { "Point", "Line", "Face", "Plane", "Axis", "Curve" };
        foreach (var prefix in featurePrefixes)
            if (value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return true;
    }

    private static bool TryParseFeatureSelectionName(string value, string component, out string feature)
    {
        feature = string.Empty;
        if (string.IsNullOrEmpty(value)) return false;
        int first = value.IndexOf('@');
        if (first <= 0) return false;
        int second = value.IndexOf('@', first + 1);
        if (second <= first + 1 || value.LastIndexOf('@') != second) return false;
        // "<feature>@<component>" where <component> itself contains one '@':
        // everything after the first separator must equal the component instance id.
        if (!string.Equals(value.Substring(first + 1), component, StringComparison.Ordinal)) return false;
        for (int i = 0; i < value.Length; i++)
            if (value[i] < 0x20) return false;
        feature = value.Substring(0, first);
        return true;
    }

    private static List<WCharEntry> CollectWCharEntries(byte[] data, int start, int end)
    {
        var result = new List<WCharEntry>();
        int limit = Math.Min(end, data.Length);
        for (int i = Math.Max(0, start); i + 4 <= limit; i++)
        {
            if (!MfcPrimitives.IsWCharPrefix(data, i)) continue;
            int cursor = i;
            string? value = MfcPrimitives.ReadWChar(data, ref cursor, limit);
            if (value == null || value.Length > MaxStringLength) continue;
            if (!string.IsNullOrEmpty(value))
            {
                result.Add(new WCharEntry
                {
                    Offset = i,
                    EndOffset = cursor,
                    Value = value
                });
            }
            i = Math.Max(i, cursor - 1);
        }
        return result;
    }

    /// <summary>
    /// Strict D1 parameter record measured in Distance (moLengthParameter_c) and
    /// Angle (moAngleParameter_c) mates:
    ///   +0  u16 0x8006 | +2 WChar "D1" (cursor +10) | +10 f64 2.0 |
    ///   +18 u32 0xFFFFFFFF | +22 u32 0 | +26 empty WChar | +30 u16 0 |
    ///   +32 f64 value (meters or radians).
    /// Exactly one tag of the expected class and one complete record is required.
    /// </summary>
    private static double? ReadD1Parameter(
        byte[] data, List<WindowTag> tags, int windowEnd, string parameterClassName, bool isAngle)
    {
        var candidates = tags.Where(t => t.ClassName == parameterClassName).ToList();
        if (candidates.Count != 1) return null;
        int pos = candidates[0].DataStart;
        int limit = Math.Min(windowEnd, data.Length);
        if (!TryReadD1Record(data, pos, limit, out double value)) return null;
        if (isAngle) return value > 0 && value < 2 * Math.PI ? value : (double?)null;
        return value > 0 && value <= 1000 ? value : (double?)null;
    }

    /// <summary>
    /// Scans for a strict named parameter record (D1/D2 layout measured on the
    /// Distance/Angle/Gear specimens). Exactly one valid record is required.
    /// </summary>
    private static double? ReadNamedParameter(byte[] data, int start, int end, string name)
    {
        int limit = Math.Min(end, data.Length);
        double? result = null;
        int count = 0;
        for (int i = Math.Max(0, start); i + 6 <= limit; i++)
        {
            if (data[i] != 0x06 || data[i + 1] != 0x80 || !MfcPrimitives.IsWCharPrefix(data, i + 2))
                continue;
            int cursor = i + 2;
            string? found = MfcPrimitives.ReadWChar(data, ref cursor, limit);
            if (!string.Equals(found, name, StringComparison.Ordinal)) continue;
            if (!TryReadNamedRecord(data, i, limit, name, out double value) ||
                value <= 0 || value > 1000)
                return null;
            if (count > 0) return null;
            result = value;
            count++;
        }
        return count == 1 ? result : null;
    }

    internal static bool TryReadD1Record(byte[] data, int pos, int end, out double value) =>
        TryReadNamedRecord(data, pos, end, "D1", out value);

    internal static bool TryReadNamedRecord(byte[] data, int pos, int end, string name, out double value)
    {
        value = 0;
        if (pos < 0 || pos + 40 > end) return false;
        int cursor = pos + 2;
        if (MfcPrimitives.ReadWChar(data, ref cursor, end) != name || cursor != pos + 10) return false;
        if (BitConverter.ToDouble(data, cursor) != 2.0) return false;
        if (BitConverter.ToUInt32(data, cursor + 8) != uint.MaxValue) return false;
        if (BitConverter.ToUInt32(data, cursor + 12) != 0) return false;
        int empty = cursor + 16;
        if (!MfcPrimitives.IsWCharPrefix(data, empty)) return false;
        int emptyCursor = empty;
        if (MfcPrimitives.ReadWChar(data, ref emptyCursor, end) != string.Empty) return false;
        if (emptyCursor + 2 > end || BitConverter.ToUInt16(data, emptyCursor) != 0) return false;
        if (emptyCursor + 10 > end) return false;
        double v = BitConverter.ToDouble(data, emptyCursor + 2);
        if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0 || v > 1000) return false;
        value = v;
        return true;
    }

    private static bool IsDeferredFaceReference(string value) =>
        value.StartsWith("moEndFaceSurfIdRep_c,", StringComparison.Ordinal);

    internal static bool TryReadDisplayName(byte[] data, int dataStart, out string displayName, out int headerStart)
    {
        displayName = string.Empty;
        headerStart = 0;
        int limit = Math.Min(data.Length, dataStart + 16);
        int offset = dataStart;
        while (offset <= limit - 3 && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset > limit - 3 || !MfcPrimitives.IsWCharPrefix(data, offset))
            return false;

        int cursor = offset;
        string? name = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
        if (name == null || cursor + 2 > data.Length)
            return false;

        // Skip the 2-byte string terminator; the header starts after it.
        displayName = name;
        headerStart = cursor + 2;
        return true;
    }

    private sealed class WindowTag
    {
        public string ClassName { get; set; } = string.Empty;
        public int TagOffset { get; set; }
        public int DataStart { get; set; }
        public int DataEnd { get; set; }
    }

    private static List<WindowTag> ScanWindow(byte[] data, int mateTagOffset, out int windowEnd)
    {
        var tags = new List<WindowTag>();
        int limit = Math.Min(data.Length, mateTagOffset + MaxWindowBytes);
        int pos = mateTagOffset;
        bool first = true;
        int breakOffset = limit;
        while (pos <= limit - 6)
        {
            if (MfcPrimitives.IsEndMarker(data, pos))
            {
                pos += 6;
                continue;
            }
            if (MfcPrimitives.IsMfcTag(data, pos))
            {
                int saved = pos;
                var tag = MfcPrimitives.ReadMfcTag(data, ref pos);
                if (tag != null)
                {
                    if (!first && IsMateWindowBreakClass(tag.ClassName))
                    {
                        // Bound the window at the next inline mate so a following
                        // record's selection strings cannot leak into this one.
                        breakOffset = tag.TagOffset;
                        break;
                    }
                    if (!string.IsNullOrEmpty(tag.ClassName))
                    {
                        tags.Add(new WindowTag
                        {
                            ClassName = tag.ClassName,
                            TagOffset = tag.TagOffset,
                            DataStart = tag.DataStart
                        });
                    }
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

        windowEnd = tags.Count > 0 ? breakOffset : limit;
        for (int i = 0; i < tags.Count; i++)
            tags[i].DataEnd = i + 1 < tags.Count ? tags[i + 1].TagOffset : windowEnd;
        return tags;
    }

    private static List<MateHeaderField> ReadHeaderFields(byte[] data, int headerStart, int headerEnd)
    {
        var fields = new List<MateHeaderField>();
        int length = headerEnd - headerStart;
        for (int offset = 0; offset + 4 <= length && fields.Count < MaxHeaderFields; offset += 4)
        {
            uint value = BitConverter.ToUInt32(data, headerStart + offset);
            if (value != 0)
                fields.Add(new MateHeaderField { Offset = offset, Value = value });
        }
        return fields;
    }

    private static FaceReferenceStructure? DecodeFace(byte[] data, List<WindowTag> tags, int windowEnd)
    {
        var faceRef = tags.FirstOrDefault(t => t.ClassName == "moFaceRef_c");
        if (faceRef == null)
            return null;
        if (!TryReadFaceRefRecord(data, faceRef.DataStart, windowEnd, out var face))
            return null;

        var mapping = tags.FirstOrDefault(t => t.ClassName == "moFromSktEntSurfIdRep_c");
        if (mapping != null)
        {
            var indices = new List<uint>();
            var types = new List<uint>();
            int pos = mapping.DataStart + 2;
            for (int i = 0; i < MaxMappingRecords && pos + MappingStride <= windowEnd && pos + MappingStride <= data.Length; i++)
            {
                uint featureType = BitConverter.ToUInt32(data, pos + 4);
                uint index = BitConverter.ToUInt32(data, pos + 12);
                if (featureType == 0 || featureType > 0x200 || index == 0)
                    break;
                types.Add(featureType);
                indices.Add(index);
                pos += MappingStride;
            }

            if (indices.Count > 0)
            {
                face.MappingIndices = indices.AsReadOnly();
                face.MappingFeatureTypes = types.AsReadOnly();
                face.FeatureTypeId = types[0];
                face.Serial = BitConverter.ToUInt32(data, mapping.DataStart + 2 + 8);
            }
        }

        return face;
    }

    /// <summary>
    /// Strict 38-byte moFaceRef_c record: face count, zero field, topology type,
    /// class word 0x200/0x300, class-specific id, two 8-byte archive magics and a
    /// zero trailer. Used for tagged records and F2 80 class-reference instances.
    /// </summary>
    private static bool TryReadFaceRefRecord(byte[] data, int start, int end, out FaceReferenceStructure face)
    {
        face = new FaceReferenceStructure();
        if (start < 0 || start + 38 > end || start + 38 > data.Length)
            return false;
        uint faceCount = BitConverter.ToUInt32(data, start);
        if (faceCount == 0 || faceCount > 1024) return false;
        if (BitConverter.ToUInt32(data, start + 4) != 0) return false;
        uint topology = BitConverter.ToUInt32(data, start + 8);
        if (topology == 0 || topology > 0x20) return false;
        uint classWord = BitConverter.ToUInt32(data, start + 12);
        if (classWord != 0x200 && classWord != 0x300) return false;
        ulong magic1 = BitConverter.ToUInt64(data, start + 20);
        ulong magic2 = BitConverter.ToUInt64(data, start + 28);
        if (magic1 == 0 || magic2 == 0) return false;
        if (BitConverter.ToUInt16(data, start + 36) != 0) return false;
        face.FaceCount = faceCount;
        face.TopologyType = topology;
        face.Field12 = classWord;
        face.Field16 = BitConverter.ToUInt32(data, start + 16);
        face.Magic1 = magic1;
        face.Magic2 = magic2;
        return true;
    }

    /// <summary>
    /// All legacy face records in a 2012 mate window: the tagged moFaceRef_c plus
    /// untagged instances serialized with the measured F2 80 class reference.
    /// </summary>
    private static List<FaceReferenceStructure> ReadLegacyFaceReferences(
        byte[] data, List<WindowTag> tags, int windowEnd)
    {
        var result = new List<FaceReferenceStructure>();
        var seen = new HashSet<int>();
        foreach (var tag in tags.Where(t => t.ClassName == "moFaceRef_c").OrderBy(t => t.TagOffset))
        {
            if (TryReadFaceRefRecord(data, tag.DataStart, windowEnd, out var face) && seen.Add(tag.TagOffset))
                result.Add(face);
        }
        int limit = Math.Min(windowEnd, data.Length);
        for (int i = 0; i + 40 <= limit; i++)
        {
            if (data[i] != 0xF2 || data[i + 1] != 0x80) continue;
            int payload = i + 2;
            if (TryReadFaceRefRecord(data, payload, limit, out var face) && seen.Add(payload))
                result.Add(face);
        }
        return result;
    }

    /// <summary>
    /// 2012 mate type mapping established by the source assembly's native snapshot
    /// (variants.json matesBefore): moMateCoordinateM -> swMateType_e 20 and
    /// moMateCoincidentM -> 0. Only applies to the 0x0004 stream + legacy header.
    /// </summary>
    private static uint? ResolveLegacyMateTypeId(string className, byte[] data, int headerStart)
    {
        if (data.Length < 2 || BitConverter.ToUInt16(data, 0) != 0x0004) return null;
        if (headerStart < 0 || headerStart + 8 > data.Length) return null;
        if (data[headerStart] != 0 || data[headerStart + 1] != 0 ||
            data[headerStart + 4] != 0 || data[headerStart + 5] != 0x40 ||
            data[headerStart + 7] != 0)
            return null;
        return className switch
        {
            "moMateCoordinateM" => 20u,
            "moMateCoincidentM" => 0u,
            _ => null
        };
    }

    private static SketchReferenceStructure DecodeSketch(
        byte[] data, List<WindowTag> tags, int windowEnd, IReadOnlyList<string> wchars)
    {
        var sketch = new SketchReferenceStructure();
        var chain = tags.FirstOrDefault(t => t.ClassName == "moChainComponent_c");
        if (chain != null && chain.DataStart + 2 <= windowEnd)
            sketch.ChainComponentCount = BitConverter.ToUInt16(data, chain.DataStart);

        var refChain = tags.FirstOrDefault(t => t.ClassName == "moReferenceChain_c");
        if (refChain != null && refChain.DataStart + 2 <= windowEnd)
            sketch.ReferenceChainCount = BitConverter.ToUInt16(data, refChain.DataStart);

        var extFeature = tags.FirstOrDefault(t => t.ClassName == "moExtFeatureList_c");
        if (extFeature != null && extFeature.DataStart + 10 <= windowEnd)
        {
            sketch.ExtFeatureCount = BitConverter.ToUInt32(data, extFeature.DataStart);
            sketch.ExtFeatureTypeId = BitConverter.ToUInt16(data, extFeature.DataStart + 4);
            sketch.ExtFeatureSerial = BitConverter.ToUInt32(data, extFeature.DataStart + 6);
        }

        var ids = new List<uint>();
        int searchStart = chain?.DataStart ?? 0;
        for (int i = searchStart; i + 8 <= windowEnd && i + 8 <= data.Length; i++)
        {
            if (BitConverter.ToUInt32(data, i) != SketchRefMagic)
                continue;
            uint id = BitConverter.ToUInt32(data, i + 4);
            if (id != 0 && !ids.Contains(id))
                ids.Add(id);
            i += 7;
        }
        sketch.SketchReferenceIds = ids.AsReadOnly();
        sketch.ComponentInstanceNames = wchars
            .Where(s => s.IndexOf("@assembly", StringComparison.OrdinalIgnoreCase) >= 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return sketch;
    }

    private static List<string> CollectWCharStrings(byte[] data, int start, int end, int maxStrings)
    {
        var result = new List<string>();
        int limit = Math.Min(end, data.Length);
        for (int i = Math.Max(0, start); i + 4 <= limit && result.Count < maxStrings; i++)
        {
            if (!MfcPrimitives.IsWCharPrefix(data, i))
                continue;
            int cursor = i;
            string? value = MfcPrimitives.ReadWChar(data, ref cursor, limit);
            if (value == null)
                continue;
            if (value.Length > MaxStringLength)
                continue;
            if (!string.IsNullOrEmpty(value) && !result.Contains(value))
                result.Add(value);
            i = Math.Max(i, cursor - 1);
        }
        return result;
    }
}
