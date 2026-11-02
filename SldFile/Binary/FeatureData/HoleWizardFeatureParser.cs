using System;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured SW2026 Hole Wizard layouts (<c>moHoleWzd_c</c>):
/// - legacy simple hole: FolderHeader TypeId 0x54, stream 0x71;
/// - tapped hole: FolderHeader TypeId 0x52, stream 0x84;
/// - ISO M6 counterbore/countersink and blind counterbore: TypeId 0x52, stream 0x71;
/// - two-point counterbore from two EXTSKETCHPOINT selections: TypeId 0x53, stream 0x71.
/// Only fields with controlled stream evidence are bound: legacy Type/EndCondition/Diameter,
/// the one sketch-point location and the two feature-local 40-byte face records
/// (<c>moSingleFaceRef_w</c> -> Face, <c>moCompFace_c</c> -> IFace); tapped
/// Type/EndCondition, ISO/M6 standard/fastener selection, thread diameter/depth and
/// cosmetic-thread type; the counterbore/countersink named parameters; the blind
/// EndCondition/flag plus the 通孔孔深度 named depth; and the two-point coordinate triples.
/// Vertices, scope and all other termination fields remain unresolved. The 38-byte
/// <c>moFaceRef_c</c> near the stream head belongs to the base extrusion folder, not to the
/// hole, and is not attributed here. UpToNext's <c>moCompSolidBody_c</c> carries no body
/// id/class word, so it is only the measured end-condition carrier, not a body terminator.
/// </summary>
internal static class HoleWizardFeatureParser
{
    internal const string HoleWizardClassName = HoleWizardSegments.ClassName;
    internal const uint LegacyHoleTypeId = HoleWizardSegments.LegacyTypeId;
    internal const uint TappedHoleTypeId = HoleWizardSegments.TappedTypeId;
    /// <summary>Compatibility alias for the legacy measured type id.</summary>
    internal const uint HoleWizardTypeId = LegacyHoleTypeId;
    internal const string AdvancedHoleClassName = "moAdvHoleWizard_c";
    internal const uint AdvancedHoleTypeId = 0x52u;

    public static HoleWizardFeatureDefinitionData? ParseHoleWizard(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != HoleWizardClassName) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;
        var legacy = LegacyHoleWizardTutorialParser.TryParse(doc, start, end);
        if (legacy != null) return legacy;
        ushort version = doc.SourceData.Length >= 2 ? BitConverter.ToUInt16(doc.SourceData, 0) : (ushort)0;

        if (typeId == LegacyHoleTypeId && version == HoleWizardSegments.LegacyStreamVersion)
            return ParseLegacy(doc, start, end);
        if (typeId == HoleWizardSegments.TwoPointTypeId &&
            version == HoleWizardSegments.LegacyStreamVersion)
            return ParseTwoPointCounterBore(doc, start, end);
        if (typeId == TappedHoleTypeId && version == HoleWizardSegments.LegacyStreamVersion)
        {
            if (IsCounterSinkLayout(doc, start, end))
                return ParseCounterSink(doc, start, end);
            return ParseCounterBoreBlind(doc, start, end) ??
                   ParseCounterBore(doc, start, end);
        }
        if (typeId == TappedHoleTypeId && version == HoleWizardSegments.TappedStreamVersion)
            return ParseTapped(doc, start, end);
        return null;
    }

    /// <summary>
    /// Parses the native API-created ISO M6 socket-head-cap counterbore layout
    /// (moHoleWzd_c / TypeId 0x52 / stream 0x71): Ø6 through hole, Ø11 x 6 mm counterbore,
    /// through-all. Values come from the measured Chinese named parameters in
    /// moAngleParameter_c and the ISO/fastener selection block in moCompSolidBody_c.
    /// </summary>
    private static HoleWizardFeatureDefinitionData? ParseCounterBore(
        MfcDocument doc, int start, int end)
    {
        var nodes = InRange(doc, start, end);
        var header = Single(nodes, HoleWizardSegments.ClassName);
        var compFace = Single(nodes, "moCompFace_c");
        var pntPnt = Single(nodes, "sgPntPntDist");
        var llDist = Single(nodes, "sgLLDist");
        var pntLine = Single(nodes, "sgPntLineDist");
        var anglDim = Single(nodes, "sgAnglDim");
        var angleParam = Single(nodes, "moAngleParameter_c");
        var skDim = Single(nodes, "moSkDimHandleValG2_c");
        var fixedRef = Single(nodes, "moFixedRefPlnData_c");
        var compBody = Single(nodes, "moCompSolidBody_c");
        if (header == null || compFace == null || pntPnt == null || llDist == null ||
            pntLine == null || anglDim == null || angleParam == null || skDim == null ||
            fixedRef == null || compBody == null)
            return null;
        if (!HasLength(doc, nodes, "moCompFace_c", HoleWizardSegments.CounterBoreCompFaceLength, end) ||
            !HasLength(doc, nodes, "sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength, end) ||
            !HasLength(doc, nodes, "sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength, end) ||
            !HasLength(doc, nodes, "sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength, end) ||
            !HasLength(doc, nodes, "sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength, end) ||
            !HasLength(doc, nodes, "moAngleParameter_c", HoleWizardSegments.CounterBoreAngleParameterLength, end) ||
            !HasLength(doc, nodes, "moSkDimHandleValG2_c", HoleWizardSegments.CounterBoreSkDimHandleLength, end) ||
            !HasLength(doc, nodes, "moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength, end) ||
            !HasLengthOneOf(doc, nodes, "moCompSolidBody_c", end,
                HoleWizardSegments.CounterBoreCompSolidBodyLength,
                HoleWizardSegments.CounterBoreAnsiCompSolidBodyLength))
            return null;

        int headerEnd = NextTagOffset(doc, header, end);
        if (headerEnd <= header.DataStart) return null;
        byte[]? headerRaw = MfcPrimitives.CopyRange(doc.SourceData, header.DataStart, headerEnd);
        byte[]? angleRaw = Slice(doc, angleParam, end, HoleWizardSegments.CounterBoreAngleParameterLength);
        byte[]? bodyRaw = Slice(doc, compBody, end, HoleWizardSegments.CounterBoreCompSolidBodyLength) ??
                          Slice(doc, compBody, end, HoleWizardSegments.CounterBoreAnsiCompSolidBodyLength);
        if (headerRaw == null || angleRaw == null || bodyRaw == null) return null;
        if (!TryReadHeader(headerRaw, HoleWizardSegments.TappedTypeId, out int restStart)) return null;
        if (headerRaw.Length - restStart != HoleWizardSegments.HeaderRestLength) return null;
        if (!TryReadPoint(headerRaw, restStart, out double x, out double y)) return null;

        string standard;
        int standard2;
        string fastenerType;
        int fastenerType2;
        if (bodyRaw.Length == HoleWizardSegments.CounterBoreCompSolidBodyLength)
        {
            standard = HoleWizardSegments.CounterBoreStandard;
            standard2 = 8;
            fastenerType = HoleWizardSegments.CounterBoreFastenerType;
            fastenerType2 = 139;
            if (!WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreStandardOffset, standard) ||
                !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreFastenerTypeOffset, fastenerType) ||
                !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreFastenerSizeOffset,
                    HoleWizardSegments.CounterBoreFastenerSize))
                return null;
            if (BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreStandard2Offset) != 8 ||
                BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreFastenerType2Offset) != 139)
                return null;
        }
        else if (bodyRaw.Length == HoleWizardSegments.CounterBoreAnsiCompSolidBodyLength)
        {
            standard = HoleWizardSegments.CounterBoreAnsiStandard;
            standard2 = 1;
            fastenerType = HoleWizardSegments.CounterBoreAnsiFastenerType;
            fastenerType2 = 33;
            if (!WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreAnsiStandardOffset, standard) ||
                !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreAnsiFastenerTypeOffset, fastenerType) ||
                !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreAnsiFastenerSizeOffset,
                    HoleWizardSegments.CounterBoreFastenerSize))
                return null;
            if (BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreAnsiStandard2Offset) != 1 ||
                BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreAnsiFastenerType2Offset) != 33)
                return null;
        }
        else return null;

        double? thruDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? thruDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? boreDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterBoreDiameterName);
        double? boreDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterBoreDepthName);
        if (!IsPositiveFinite(thruDiameter) || !IsPositiveFinite(thruDepth) ||
            !IsPositiveFinite(boreDiameter) || !IsPositiveFinite(boreDepth))
            return null;
        if (!HoleWizardSegments.IsAllowedCounterBoreThruDiameter(thruDiameter!.Value) ||
            Math.Abs(thruDepth!.Value - 0.010) > 1e-12 ||
            !HoleWizardSegments.IsAllowedCounterBoreDiameter(boreDiameter!.Value) ||
            !HoleWizardSegments.IsAllowedCounterBoreDepth(boreDepth!.Value))
            return null;

        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = HoleWizardSegments.TappedTypeId,
            Type = HoleWizardSegments.CounterBoreTypeValue,
            EndCondition = 1,
            Standard = standard,
            Standard2 = standard2,
            FastenerType = fastenerType,
            FastenerType2 = fastenerType2,
            FastenerSize = HoleWizardSegments.CounterBoreFastenerSize,
            HoleDiameter = 0.0,
            Depth = 0.0,
            HoleDepth = 0.0,
            ThruHoleDiameter = thruDiameter,
            ThruHoleDepth = thruDepth,
            CounterBoreDiameter = boreDiameter,
            CounterBoreDepth = boreDepth,
            CounterSinkDiameter = 0.0,
            CounterSinkAngle = 0.0,
            ThreadDiameter = 0.0,
            ThreadDepth = 0.0,
            SketchPoints = new[]
            {
                new FeatureReferenceData
                {
                    EntityType = SelectionEntityType.Sketch,
                    ReferenceName = "HoleSketchPoint",
                    RecordCount = 1
                }
            },
            SketchPointLocations = new[] { new HoleWizardSketchPointLocation { X = x, Y = y } }
        };
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.SketchPoints),
            nameof(HoleWizardFeatureDefinitionData.SketchPointLocations)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        return definition;
    }

    /// <summary>
    /// Parses the measured blind counterbore layout produced by creating an ISO M6
    /// socket-head-cap counterbore and then setting IWizardHoleFeatureData2.EndCondition to
    /// Blind and ThruHoleDepth to 0.010/0.008 through ModifyDefinition. The 730-byte
    /// through-all moCompSolidBody_c becomes 557 bytes (EndCondition u32 at +0x15B, depth
    /// double at +0x161, flag at +0x169) followed by a measured 167-byte tail element.
    /// </summary>
    private static HoleWizardFeatureDefinitionData? ParseCounterBoreBlind(
        MfcDocument doc, int start, int end)
    {
        var nodes = InRange(doc, start, end);
        var header = Single(nodes, HoleWizardSegments.ClassName);
        var compFace = Single(nodes, "moCompFace_c");
        var pntPnt = Single(nodes, "sgPntPntDist");
        var llDist = Single(nodes, "sgLLDist");
        var pntLine = Single(nodes, "sgPntLineDist");
        var anglDim = Single(nodes, "sgAnglDim");
        var angleParam = Single(nodes, "moAngleParameter_c");
        var skDim = Single(nodes, "moSkDimHandleValG2_c");
        var fixedRef = Single(nodes, "moFixedRefPlnData_c");
        var compBody = Single(nodes, "moCompSolidBody_c");
        if (header == null || compFace == null || pntPnt == null || llDist == null ||
            pntLine == null || anglDim == null || angleParam == null || skDim == null ||
            fixedRef == null || compBody == null)
            return null;
        if (!HasLength(doc, nodes, "moCompFace_c", HoleWizardSegments.CounterBoreCompFaceLength, end) ||
            !HasLength(doc, nodes, "sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength, end) ||
            !HasLength(doc, nodes, "sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength, end) ||
            !HasLength(doc, nodes, "sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength, end) ||
            !HasLength(doc, nodes, "sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength, end) ||
            !HasLength(doc, nodes, "moAngleParameter_c", HoleWizardSegments.CounterBoreAngleParameterLength, end) ||
            !HasLength(doc, nodes, "moSkDimHandleValG2_c", HoleWizardSegments.CounterBoreSkDimHandleLength, end) ||
            !HasLength(doc, nodes, "moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength, end) ||
            !HasLength(doc, nodes, "moCompSolidBody_c", HoleWizardSegments.BlindCompSolidBodyLength, end))
            return null;
        int tailTag = NextTagOffset(doc, compBody, end);
        var tail = doc.AllElements.FirstOrDefault(e => e.TagOffset == tailTag && tailTag < end);
        if (tail == null || tail.ClassName.Length != 0 ||
            NextTagOffset(doc, tail, end) - tail.DataStart != HoleWizardSegments.BlindTailElementSpan)
            return null;

        int headerEnd = NextTagOffset(doc, header, end);
        if (headerEnd <= header.DataStart) return null;
        byte[]? headerRaw = MfcPrimitives.CopyRange(doc.SourceData, header.DataStart, headerEnd);
        byte[]? angleRaw = Slice(doc, angleParam, end, HoleWizardSegments.CounterBoreAngleParameterLength);
        byte[]? bodyRaw = Slice(doc, compBody, end, HoleWizardSegments.BlindCompSolidBodyLength);
        if (headerRaw == null || angleRaw == null || bodyRaw == null) return null;
        if (!TryReadHeader(headerRaw, HoleWizardSegments.TappedTypeId, out int restStart)) return null;
        if (headerRaw.Length - restStart != HoleWizardSegments.HeaderRestLength) return null;
        if (!TryReadPoint(headerRaw, restStart, out double x, out double y)) return null;

        if (!WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreStandardOffset,
                HoleWizardSegments.CounterBoreStandard) ||
            !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreFastenerTypeOffset,
                HoleWizardSegments.CounterBoreFastenerType) ||
            !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreFastenerSizeOffset,
                HoleWizardSegments.CounterBoreFastenerSize) ||
            BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreStandard2Offset) != 8 ||
            BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreFastenerType2Offset) != 139)
            return null;
        if (BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.BlindEndConditionOffset) != 0 ||
            BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.BlindFlagOffset) != 1)
            return null;
        // The body copy stays at the through-all creation depth (0.010); the blind depth is
        // stored in the 通孔孔深度 named parameter (0.010 or the measured 0.008).
        double bodyDepth = BitConverter.ToDouble(bodyRaw, HoleWizardSegments.BlindThruHoleDepthOffset);
        if (Math.Abs(bodyDepth - 0.010) > 1e-12) return null;

        double? thruDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? namedDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? boreDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterBoreDiameterName);
        double? boreDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterBoreDepthName);
        if (!IsPositiveFinite(thruDiameter) || !IsPositiveFinite(namedDepth) ||
            !IsPositiveFinite(boreDiameter) || !IsPositiveFinite(boreDepth))
            return null;
        if (Math.Abs(thruDiameter!.Value - 0.006) > 1e-12 ||
            Math.Abs(boreDiameter!.Value - 0.011) > 1e-12 ||
            Math.Abs(boreDepth!.Value - 0.006) > 1e-12)
            return null;

        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = HoleWizardSegments.TappedTypeId,
            Type = HoleWizardSegments.CounterBoreTypeValue,
            EndCondition = 0,
            Standard = HoleWizardSegments.CounterBoreStandard,
            Standard2 = 8,
            FastenerType = HoleWizardSegments.CounterBoreFastenerType,
            FastenerType2 = 139,
            FastenerSize = HoleWizardSegments.CounterBoreFastenerSize,
            HoleDiameter = 0.0,
            Depth = 0.0,
            HoleDepth = 0.0,
            ThruHoleDiameter = thruDiameter,
            ThruHoleDepth = namedDepth,
            CounterBoreDiameter = boreDiameter,
            CounterBoreDepth = boreDepth,
            CounterSinkDiameter = 0.0,
            CounterSinkAngle = 0.0,
            ThreadDiameter = 0.0,
            ThreadDepth = 0.0,
            SketchPoints = new[]
            {
                new FeatureReferenceData
                {
                    EntityType = SelectionEntityType.Sketch,
                    ReferenceName = "HoleSketchPoint",
                    RecordCount = 1
                }
            },
            SketchPointLocations = new[] { new HoleWizardSketchPointLocation { X = x, Y = y } }
        };
        MarkCounterBoreResolved(definition);
        return definition;
    }

    /// <summary>
    /// Parses the measured two-point counterbore layout (FolderHeader TypeId 0x53, stream 0x71)
    /// created by selecting two EXTSKETCHPOINT sketch points and calling HoleWizard5. The
    /// embedded mo3DProfileFeature_c and moPointBackedUpData_c carry one coordinate triple each;
    /// the native GetSketchPoints order is the profile point followed by the backup point.
    /// </summary>
    private static HoleWizardFeatureDefinitionData? ParseTwoPointCounterBore(
        MfcDocument doc, int start, int end)
    {
        var nodes = InRange(doc, start, end);
        var header = Single(nodes, HoleWizardSegments.ClassName);
        var sgExtEnt = Single(nodes, "sgExtEnt_c");
        var sketchRef = Single(nodes, "moSketchExtRef_w");
        var compSketchHandle = Single(nodes, "moCompSketchEntHandle_c");
        var pntPnt = Single(nodes, "sgPntPntDist");
        var llDist = Single(nodes, "sgLLDist");
        var pntLine = Single(nodes, "sgPntLineDist");
        var anglDim = Single(nodes, "sgAnglDim");
        var angleParam = Single(nodes, "moAngleParameter_c");
        var skDim = Single(nodes, "moSkDimHandleValG2_c");
        var fixedRef = Single(nodes, "moFixedRefPlnData_c");
        var compBody = Single(nodes, "moCompSolidBody_c");
        var profile = Single(nodes, "mo3DProfileFeature_c");
        var backup = Single(nodes, "moPointBackedUpData_c");
        if (header == null || sgExtEnt == null || sketchRef == null || compSketchHandle == null ||
            pntPnt == null || llDist == null ||
            pntLine == null || anglDim == null || angleParam == null || skDim == null ||
            fixedRef == null || compBody == null || profile == null || backup == null)
            return null;
        if (!HasLength(doc, nodes, HoleWizardSegments.ClassName,
                HoleWizardSegments.TwoPointHeaderLength, end) ||
            !HasLength(doc, nodes, "moCompSketchEntHandle_c", 171, end) ||
            !HasLength(doc, nodes, "sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength, end) ||
            !HasLength(doc, nodes, "sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength, end) ||
            !HasLength(doc, nodes, "sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength, end) ||
            !HasLength(doc, nodes, "sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength, end) ||
            !HasLength(doc, nodes, "moAngleParameter_c", HoleWizardSegments.CounterBoreAngleParameterLength, end) ||
            !HasLength(doc, nodes, "moSkDimHandleValG2_c", HoleWizardSegments.CounterBoreSkDimHandleLength, end) ||
            !HasLength(doc, nodes, "moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength, end) ||
            !HasLength(doc, nodes, "moCompSolidBody_c", HoleWizardSegments.TwoPointCompSolidBodyLength, end) ||
            !HasLength(doc, nodes, "mo3DProfileFeature_c", HoleWizardSegments.TwoPoint3DProfileLength, end) ||
            !HasLength(doc, nodes, "moPointBackedUpData_c", HoleWizardSegments.TwoPointPointBackupLength, end))
            return null;

        int headerEnd = NextTagOffset(doc, header, end);
        if (headerEnd <= header.DataStart) return null;
        byte[]? headerRaw = MfcPrimitives.CopyRange(doc.SourceData, header.DataStart, headerEnd);
        byte[]? angleRaw = Slice(doc, angleParam, end, HoleWizardSegments.CounterBoreAngleParameterLength);
        byte[]? bodyRaw = Slice(doc, compBody, end, HoleWizardSegments.TwoPointCompSolidBodyLength);
        byte[]? profileRaw = Slice(doc, profile, end, HoleWizardSegments.TwoPoint3DProfileLength);
        byte[]? backupRaw = Slice(doc, backup, end, HoleWizardSegments.TwoPointPointBackupLength);
        if (headerRaw == null || angleRaw == null || bodyRaw == null ||
            profileRaw == null || backupRaw == null)
            return null;
        if (!TryReadHeader(headerRaw, HoleWizardSegments.TwoPointTypeId, out int restStart)) return null;
        if (headerRaw.Length - restStart != HoleWizardSegments.TwoPointHeaderRestLength) return null;

        if (!WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreStandardOffset,
                HoleWizardSegments.CounterBoreStandard) ||
            !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreFastenerTypeOffset,
                HoleWizardSegments.CounterBoreFastenerType) ||
            !WCharEquals(bodyRaw, HoleWizardSegments.CounterBoreFastenerSizeOffset,
                HoleWizardSegments.CounterBoreFastenerSize) ||
            BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreStandard2Offset) != 8 ||
            BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterBoreFastenerType2Offset) != 139)
            return null;

        double? thruDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? thruDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? boreDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterBoreDiameterName);
        double? boreDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterBoreDepthName);
        if (!IsPositiveFinite(thruDiameter) || !IsPositiveFinite(thruDepth) ||
            !IsPositiveFinite(boreDiameter) || !IsPositiveFinite(boreDepth))
            return null;
        if (Math.Abs(thruDiameter!.Value - 0.006) > 1e-12 ||
            Math.Abs(thruDepth!.Value - 0.010) > 1e-12 ||
            Math.Abs(boreDiameter!.Value - 0.011) > 1e-12 ||
            Math.Abs(boreDepth!.Value - 0.006) > 1e-12)
            return null;

        if (!TryReadPointTriple(profileRaw, HoleWizardSegments.TwoPointProfilePointOffset,
                out double x0, out double y0, out double z0) ||
            !TryReadPointTriple(backupRaw, HoleWizardSegments.TwoPointBackupPointOffset,
                out double x1, out double y1, out double z1))
            return null;

        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = HoleWizardSegments.TwoPointTypeId,
            Type = HoleWizardSegments.CounterBoreTypeValue,
            EndCondition = 1,
            Standard = HoleWizardSegments.CounterBoreStandard,
            Standard2 = 8,
            FastenerType = HoleWizardSegments.CounterBoreFastenerType,
            FastenerType2 = 139,
            FastenerSize = HoleWizardSegments.CounterBoreFastenerSize,
            HoleDiameter = 0.0,
            Depth = 0.0,
            HoleDepth = 0.0,
            ThruHoleDiameter = thruDiameter,
            ThruHoleDepth = thruDepth,
            CounterBoreDiameter = boreDiameter,
            CounterBoreDepth = boreDepth,
            CounterSinkDiameter = 0.0,
            CounterSinkAngle = 0.0,
            ThreadDiameter = 0.0,
            ThreadDepth = 0.0,
            SketchPoints = new[]
            {
                new FeatureReferenceData
                {
                    EntityType = SelectionEntityType.Sketch,
                    ReferenceName = "HoleSketchPoint",
                    RecordCount = 1
                },
                new FeatureReferenceData
                {
                    EntityType = SelectionEntityType.Sketch,
                    ReferenceName = "HoleSketchPoint",
                    RecordCount = 1
                }
            },
            SketchPointLocations = new[]
            {
                new HoleWizardSketchPointLocation { X = x0, Y = y0 },
                new HoleWizardSketchPointLocation { X = x1, Y = y1 }
            }
        };
        MarkCounterBoreResolved(definition);
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.SketchPoints));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.SketchPointLocations));
        return definition;
    }

    private static void MarkCounterBoreResolved(HoleWizardFeatureDefinitionData definition)
    {
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.SketchPoints),
            nameof(HoleWizardFeatureDefinitionData.SketchPointLocations)
        })
        {
            definition.MarkResolvedInternal(name);
        }
    }

    private static bool TryReadPointTriple(
        byte[] data, int offset, out double x, out double y, out double z)
    {
        x = y = z = 0;
        if (data == null || offset < 0 || offset + 24 > data.Length) return false;
        x = BitConverter.ToDouble(data, offset);
        y = BitConverter.ToDouble(data, offset + 8);
        z = BitConverter.ToDouble(data, offset + 16);
        return IsFinite(x) && IsFinite(y) && IsFinite(z) &&
               x >= -10.0 && x <= 10.0 && y >= -10.0 && y <= 10.0 &&
               z >= -10.0 && z <= 10.0;
    }

    private static bool IsCounterSinkLayout(MfcDocument doc, int start, int end) =>
        doc.AllElements.Any(e => e.TagOffset >= start && e.TagOffset < end &&
                                 e.ClassName == "moDisplayAngularDim_c");

    /// <summary>
    /// Parses the native API-created ISO flat-head countersink layout (moHoleWzd_c / TypeId
    /// 0x52 / stream 0x71): measured M6 (Ø6 through, Ø11 x 90°) and M8 (Ø8 through,
    /// Ø15 x 90°) specimens, source and native resave.
    /// </summary>
    private static HoleWizardFeatureDefinitionData? ParseCounterSink(
        MfcDocument doc, int start, int end)
    {
        var nodes = InRange(doc, start, end);
        var header = Single(nodes, HoleWizardSegments.ClassName);
        var compFace = Single(nodes, "moCompFace_c");
        var pntPnt = Single(nodes, "sgPntPntDist");
        var llDist = Single(nodes, "sgLLDist");
        var pntLine = Single(nodes, "sgPntLineDist");
        var anglDim = Single(nodes, "sgAnglDim");
        var angleParam = Single(nodes, "moAngleParameter_c");
        var skDim = Single(nodes, "moSkDimHandleValG2_c");
        var displayAngular = Single(nodes, "moDisplayAngularDim_c");
        var fixedRef = Single(nodes, "moFixedRefPlnData_c");
        var compBody = Single(nodes, "moCompSolidBody_c");
        if (header == null || compFace == null || pntPnt == null || llDist == null ||
            pntLine == null || anglDim == null || angleParam == null || skDim == null ||
            displayAngular == null || fixedRef == null || compBody == null)
            return null;
        if (!HasLength(doc, nodes, "moCompFace_c", HoleWizardSegments.CounterSinkCompFaceLength, end) ||
            !HasLength(doc, nodes, "sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength, end) ||
            !HasLength(doc, nodes, "sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength, end) ||
            !HasLength(doc, nodes, "sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength, end) ||
            !HasLength(doc, nodes, "sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength, end) ||
            !HasLength(doc, nodes, "moAngleParameter_c", HoleWizardSegments.CounterSinkAngleParameterLength, end) ||
            !HasLength(doc, nodes, "moSkDimHandleValG2_c", HoleWizardSegments.CounterSinkSkDimHandleLength, end) ||
            !HasLength(doc, nodes, "moDisplayAngularDim_c", HoleWizardSegments.CounterSinkDisplayAngularDimLength, end) ||
            !HasLength(doc, nodes, "moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength, end) ||
            !HasLength(doc, nodes, "moCompSolidBody_c", HoleWizardSegments.CounterSinkCompSolidBodyLength, end))
            return null;

        int headerEnd = NextTagOffset(doc, header, end);
        if (headerEnd <= header.DataStart) return null;
        byte[]? headerRaw = MfcPrimitives.CopyRange(doc.SourceData, header.DataStart, headerEnd);
        byte[]? angleRaw = Slice(doc, angleParam, end, HoleWizardSegments.CounterSinkAngleParameterLength);
        byte[]? bodyRaw = Slice(doc, compBody, end, HoleWizardSegments.CounterSinkCompSolidBodyLength);
        if (headerRaw == null || angleRaw == null || bodyRaw == null) return null;
        if (!TryReadHeader(headerRaw, HoleWizardSegments.TappedTypeId, out int restStart)) return null;
        if (headerRaw.Length - restStart != HoleWizardSegments.HeaderRestLength) return null;
        if (!TryReadPoint(headerRaw, restStart, out double x, out double y)) return null;

        if (!WCharEquals(bodyRaw, HoleWizardSegments.CounterSinkStandardOffset,
                HoleWizardSegments.CounterSinkStandard) ||
            !WCharEquals(bodyRaw, HoleWizardSegments.CounterSinkFastenerTypeOffset,
                HoleWizardSegments.CounterSinkFastenerType))
            return null;
        string? fastenerSize = ReadWCharAt(bodyRaw, HoleWizardSegments.CounterSinkFastenerSizeOffset);
        if (fastenerSize != "M6" && fastenerSize != "M8") return null;
        if (BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterSinkStandard2Offset) != 8 ||
            BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.CounterSinkFastenerType2Offset) != 141)
            return null;

        double? thruDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? thruDepth = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? csDiameter = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterSinkDiameterName);
        double? csAngle = ReadNamedDoubleByExactName(
            angleRaw, HoleWizardSegments.CounterSinkAngleName);
        if (!IsPositiveFinite(thruDiameter) || !IsPositiveFinite(thruDepth) ||
            !IsPositiveFinite(csDiameter) || !IsPositiveFinite(csAngle))
            return null;
        double expectedThru = fastenerSize == "M6" ? 0.006 : 0.008;
        if (Math.Abs(thruDiameter!.Value - expectedThru) > 1e-12 ||
            Math.Abs(thruDepth!.Value - 0.010) > 1e-12 ||
            !HoleWizardSegments.IsAllowedCounterSinkDiameter(fastenerSize, csDiameter!.Value) ||
            !HoleWizardSegments.IsAllowedCounterSinkAngle(csAngle!.Value))
            return null;

        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = HoleWizardSegments.TappedTypeId,
            Type = HoleWizardSegments.CounterSinkTypeValue,
            EndCondition = 1,
            Standard = HoleWizardSegments.CounterSinkStandard,
            Standard2 = 8,
            FastenerType = HoleWizardSegments.CounterSinkFastenerType,
            FastenerType2 = 141,
            FastenerSize = fastenerSize,
            HoleDiameter = 0.0,
            Depth = 0.0,
            HoleDepth = 0.0,
            ThruHoleDiameter = thruDiameter,
            ThruHoleDepth = thruDepth,
            CounterBoreDiameter = 0.0,
            CounterBoreDepth = 0.0,
            CounterSinkDiameter = csDiameter,
            CounterSinkAngle = csAngle,
            ThreadDiameter = 0.0,
            ThreadDepth = 0.0,
            SketchPoints = new[]
            {
                new FeatureReferenceData
                {
                    EntityType = SelectionEntityType.Sketch,
                    ReferenceName = "HoleSketchPoint",
                    RecordCount = 1
                }
            },
            SketchPointLocations = new[] { new HoleWizardSketchPointLocation { X = x, Y = y } }
        };
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.HoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.Depth),
            nameof(HoleWizardFeatureDefinitionData.HoleDepth),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThruHoleDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterBoreDepth),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkDiameter),
            nameof(HoleWizardFeatureDefinitionData.CounterSinkAngle),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.SketchPoints),
            nameof(HoleWizardFeatureDefinitionData.SketchPointLocations)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        return definition;
    }

    private static string? ReadWCharAt(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length || !MfcPrimitives.IsWCharPrefix(data, offset))
            return null;
        int cursor = offset;
        return MfcPrimitives.ReadWChar(data, ref cursor);
    }

    private static double? ReadNamedDoubleByExactName(byte[] payload, string expectedName)
    {
        double? result = null;
        int count = 0;
        for (int i = 0; i + 6 <= payload.Length; i++)
        {
            if (payload[i] != 0x04 || payload[i + 1] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(payload, i + 2)) continue;
            int position = i + 2;
            string? name = MfcPrimitives.ReadWChar(payload, ref position);
            if (!string.Equals(name, expectedName, StringComparison.Ordinal)) continue;
            if (position + 30 > payload.Length ||
                BitConverter.ToDouble(payload, position) != 2.0 ||
                BitConverter.ToUInt32(payload, position + 8) != uint.MaxValue ||
                BitConverter.ToUInt32(payload, position + 12) != 0 ||
                !MfcPrimitives.IsWCharPrefix(payload, position + 16) ||
                payload[position + 19] != 0 ||
                BitConverter.ToUInt16(payload, position + 20) != 0)
                return null;
            double value = BitConverter.ToDouble(payload, position + 22);
            if (double.IsNaN(value) || double.IsInfinity(value)) return null;
            result = value;
            count++;
        }
        return count == 1 ? result : null;
    }

    private static HoleWizardFeatureDefinitionData? ParseLegacy(MfcDocument doc, int start, int end)
    {
        var nodes = InRange(doc, start, end);
        var header = Single(nodes, HoleWizardSegments.ClassName);
        var diameterDim = Single(nodes, "sgLLDist");
        var dimHandle = Single(nodes, "moSkDimHandleValG2_c");
        var solidBody = Single(nodes, "moCompSolidBody_c");
        var singleFaceElement = Single(nodes, "moSingleFaceRef_w");
        var compFaceElement = Single(nodes, "moCompFace_c");
        if (header == null || diameterDim == null || dimHandle == null || solidBody == null ||
            singleFaceElement == null || compFaceElement == null) return null;
        if (!HasLength(doc, nodes, "moFaceRefPlnData_c", HoleWizardSegments.LegacyFaceRefPlnDataLength, end) ||
            !HasLength(doc, nodes, "moCompFace_c", HoleWizardSegments.LegacyCompFaceLength, end) ||
            !HasLength(doc, nodes, "moSingleFaceRef_w", HoleWizardSegments.LegacySingleFaceRefLength, end) ||
            !HasExactlyOne(nodes, "sgPntPntDist") ||
            !HasExactlyOne(nodes, "moFixedRefPlnData_c"))
            return null;

        int headerEnd = NextTagOffset(doc, header, end);
        if (headerEnd <= header.DataStart) return null;
        byte[]? headerRaw = MfcPrimitives.CopyRange(doc.SourceData, header.DataStart, headerEnd);
        byte[]? diameterRaw = Slice(doc, diameterDim, end, HoleWizardSegments.LegacySgLLDistLength);
        byte[]? handleRaw = Slice(doc, dimHandle, end, HoleWizardSegments.LegacySkDimHandleLength);
        byte[]? bodyRaw = Slice(doc, solidBody, end, HoleWizardSegments.LegacyCompSolidBodyLength);
        byte[]? singleFaceRaw = Slice(doc, singleFaceElement, end, HoleWizardSegments.LegacySingleFaceRefLength);
        byte[]? compFaceRaw = Slice(doc, compFaceElement, end, HoleWizardSegments.LegacyCompFaceLength);
        if (headerRaw == null || diameterRaw == null || handleRaw == null || bodyRaw == null ||
            singleFaceRaw == null || compFaceRaw == null) return null;
        var face = ParseFaceRecord(singleFaceRaw, HoleWizardSegments.LegacySingleFaceRecordOffset, "HoleFaceRef");
        var iFace = ParseFaceRecord(compFaceRaw, HoleWizardSegments.LegacyCompFaceRecordOffset, "HoleCompFace");
        if (face == null || iFace == null) return null;
        if (!TryReadHeader(headerRaw, HoleWizardSegments.LegacyTypeId, out int restStart)) return null;
        if (headerRaw.Length - restStart != HoleWizardSegments.HeaderRestLength) return null;

        double diameter = BitConverter.ToDouble(diameterRaw, HoleWizardSegments.LegacyDiameterOffset);
        double diameterCopy = BitConverter.ToDouble(handleRaw, HoleWizardSegments.LegacyDiameterCopyOffset);
        if (!IsPositiveFinite(diameter) || diameter != diameterCopy) return null;

        uint endCondition = BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.LegacyEndConditionOffset);
        if (endCondition != 0 && endCondition != 1 && endCondition != 11) return null;

        if (!TryReadPoint(headerRaw, restStart, out double x, out double y)) return null;

        // UpToNext carries the same 351-byte moCompSolidBody_c block as every other legacy
        // sample: its embedded 40-byte record has classWord 0 and persistent id 0, and only
        // the final u32 differs (the EndCondition value). It is the measured end-condition
        // carrier, not a body selection, so no body terminator is claimed.
        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = HoleWizardSegments.LegacyTypeId,
            Type = HoleWizardSegments.LegacyTypeValue,
            EndCondition = (int)endCondition,
            Diameter = diameter,
            Face = face,
            IFace = iFace,
            SketchPoints = new[]
            {
                new FeatureReferenceData
                {
                    EntityType = SelectionEntityType.Sketch,
                    ReferenceName = "HoleSketchPoint",
                    RecordCount = 1
                }
            },
            SketchPointLocations = new[] { new HoleWizardSketchPointLocation { X = x, Y = y } }
        };
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.FolderTypeId));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.Type));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.EndCondition));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.Diameter));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.Face));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.IFace));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.SketchPoints));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.SketchPointLocations));
        return definition;
    }

    private static HoleWizardFeatureDefinitionData? ParseTapped(MfcDocument doc, int start, int end)
    {
        var nodes = InRange(doc, start, end);
        var header = Single(nodes, HoleWizardSegments.ClassName);
        var solidBody = Single(nodes, "moCompSolidBody_c");
        var threadDim = Single(nodes, "ThreeDdiameterDim_c");
        var threadData = Single(nodes, "moCThreadData_c");
        var angleParameter = Single(nodes, "moAngleParameter_c");
        var faceRef = Single(nodes, "moSingleFaceRef_w");
        if (header == null || solidBody == null || threadDim == null || threadData == null ||
            angleParameter == null || faceRef == null) return null;

        if (!HasLength(doc, nodes, "moFaceRefPlnData_c", 4228, end) ||
            !HasLength(doc, nodes, "sgPntPntDist", 140, end) ||
            !HasLength(doc, nodes, "sgLLDist", 424, end) ||
            !HasLength(doc, nodes, "sgPntLineDist", 2126, end) ||
            !HasLength(doc, nodes, "sgAnglDim", 4, end) ||
            !HasLength(doc, nodes, "moSkDimHandleValG2_c", 2458, end) ||
            !HasLength(doc, nodes, "moFixedRefPlnData_c", 357, end) ||
            !HasLength(doc, nodes, "moCosmeticThread_c", 165, end) ||
            !HasLength(doc, nodes, "moCompEdge_c", 86, end) ||
            !HasLength(doc, nodes, "moEdgeRef_c", 212, end) ||
            !HasLength(doc, nodes, "moDisplayRadialDim_c", 661, end) ||
            !HasLength(doc, nodes, "moCylinderRef_w", 615, end) ||
            !HasLength(doc, nodes, "moCompSolidBody_c", HoleWizardSegments.TappedCompSolidBodyLength, end) ||
            !HasLength(doc, nodes, "moSingleFaceRef_w", HoleWizardSegments.TappedSingleFaceRefLength, end) ||
            !HasLength(doc, nodes, "ThreeDdiameterDim_c", HoleWizardSegments.TappedThreadDimLength, end) ||
            !HasLength(doc, nodes, "moCThreadData_c", HoleWizardSegments.TappedCThreadDataLength, end) ||
            !HasLength(doc, nodes, "moAngleParameter_c", HoleWizardSegments.TappedAngleParameterLength, end))
            return null;

        int headerEnd = NextTagOffset(doc, header, end);
        if (headerEnd <= header.DataStart) return null;
        byte[]? headerRaw = MfcPrimitives.CopyRange(doc.SourceData, header.DataStart, headerEnd);
        byte[]? bodyRaw = Slice(doc, solidBody, end, HoleWizardSegments.TappedCompSolidBodyLength);
        byte[]? threadDimRaw = Slice(doc, threadDim, end, HoleWizardSegments.TappedThreadDimLength);
        byte[]? threadDataRaw = Slice(doc, threadData, end, HoleWizardSegments.TappedCThreadDataLength);
        byte[]? angleRaw = Slice(doc, angleParameter, end, HoleWizardSegments.TappedAngleParameterLength);
        byte[]? faceRefRaw = Slice(doc, faceRef, end, HoleWizardSegments.TappedSingleFaceRefLength);
        if (headerRaw == null || bodyRaw == null || threadDimRaw == null || threadDataRaw == null ||
            angleRaw == null || faceRefRaw == null) return null;

        if (!TryReadHeader(headerRaw, HoleWizardSegments.TappedTypeId, out int restStart)) return null;
        if (headerRaw.Length - restStart != HoleWizardSegments.HeaderRestLength) return null;
        if (!TryReadPoint(headerRaw, restStart, out double x, out double y)) return null;

        uint endCondition = BitConverter.ToUInt32(bodyRaw, HoleWizardSegments.TappedEndConditionOffset);
        if (endCondition != 1) return null;

        double threadDiameter = BitConverter.ToDouble(threadDimRaw, HoleWizardSegments.TappedThreadDiameterOffset);
        if (!IsPositiveFinite(threadDiameter)) return null;
        double threadDiameterCopy = BitConverter.ToDouble(threadDataRaw, HoleWizardSegments.TappedThreadDiameterCopyOffset);
        if (Math.Abs(threadDiameter - threadDiameterCopy) > 1e-12) return null;

        double threadDepth = BitConverter.ToDouble(threadDataRaw, HoleWizardSegments.TappedThreadDepthOffset);
        if (!IsPositiveFinite(threadDepth)) return null;
        foreach (int slot in HoleWizardSegments.TappedThreadDepthCopyOffsets)
        {
            if (slot < 0 || slot + 8 > threadDataRaw.Length) return null;
            if (BitConverter.ToDouble(threadDataRaw, slot) != threadDepth) return null;
        }

        if (BitConverter.ToUInt32(angleRaw, HoleWizardSegments.TappedTypeOffset) !=
            HoleWizardSegments.TappedTypeValue) return null;

        if (!StringEquals(faceRefRaw, HoleWizardSegments.TappedStandardOffset, HoleWizardSegments.TappedStandard) ||
            !StringEquals(faceRefRaw, HoleWizardSegments.TappedFastenerTypeOffset, HoleWizardSegments.TappedFastenerType) ||
            !StringEquals(faceRefRaw, HoleWizardSegments.TappedFastenerSizeOffset, HoleWizardSegments.TappedFastenerSize))
            return null;
        if (BitConverter.ToUInt32(faceRefRaw, HoleWizardSegments.TappedStandard2Offset) !=
            HoleWizardSegments.TappedStandard2Value) return null;
        if (BitConverter.ToUInt32(faceRefRaw, HoleWizardSegments.TappedFastenerType2Offset) !=
            HoleWizardSegments.TappedFastenerType2Value) return null;
        if (BitConverter.ToUInt32(faceRefRaw, HoleWizardSegments.TappedCosmeticThreadTypeOffset) !=
            HoleWizardSegments.TappedCosmeticThreadTypeValue) return null;

        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = HoleWizardSegments.TappedTypeId,
            Type = HoleWizardSegments.TappedTypeValue,
            EndCondition = 1,
            Standard = HoleWizardSegments.TappedStandard,
            Standard2 = HoleWizardSegments.TappedStandard2Value,
            FastenerType = HoleWizardSegments.TappedFastenerType,
            FastenerType2 = HoleWizardSegments.TappedFastenerType2Value,
            FastenerSize = HoleWizardSegments.TappedFastenerSize,
            ThreadDiameter = threadDiameter,
            ThreadDepth = threadDepth,
            CosmeticThreadType = HoleWizardSegments.TappedCosmeticThreadTypeValue,
            SketchPoints = new[] { new FeatureReferenceData { EntityType = SelectionEntityType.None } },
            SketchPointLocations = new[] { new HoleWizardSketchPointLocation { X = x, Y = y } }
        };
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureDefinitionData.FolderTypeId),
            nameof(HoleWizardFeatureDefinitionData.Type),
            nameof(HoleWizardFeatureDefinitionData.EndCondition),
            nameof(HoleWizardFeatureDefinitionData.Standard),
            nameof(HoleWizardFeatureDefinitionData.Standard2),
            nameof(HoleWizardFeatureDefinitionData.FastenerType),
            nameof(HoleWizardFeatureDefinitionData.FastenerType2),
            nameof(HoleWizardFeatureDefinitionData.FastenerSize),
            nameof(HoleWizardFeatureDefinitionData.ThreadDiameter),
            nameof(HoleWizardFeatureDefinitionData.ThreadDepth),
            nameof(HoleWizardFeatureDefinitionData.CosmeticThreadType),
            nameof(HoleWizardFeatureDefinitionData.SketchPoints),
            nameof(HoleWizardFeatureDefinitionData.SketchPointLocations)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        return definition;
    }

    private static System.Collections.Generic.List<MfcElement> InRange(MfcDocument doc, int start, int end) =>
        doc.AllElements.Where(e => e.TagOffset >= start && e.TagOffset < end).ToList();

    private static MfcElement? Single(System.Collections.Generic.List<MfcElement> nodes, string className)
    {
        var matches = nodes.Where(e => e.ClassName == className).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool HasExactlyOne(System.Collections.Generic.List<MfcElement> nodes, string className) =>
        nodes.Count(e => e.ClassName == className) == 1;

    private static bool HasLength(
        MfcDocument doc, System.Collections.Generic.List<MfcElement> nodes, string className, int length, int featureEnd)
    {
        var element = Single(nodes, className);
        if (element == null) return false;
        int next = NextTagOffset(doc, element, featureEnd);
        return next - element.DataStart == length;
    }

    private static bool HasLengthOneOf(
        MfcDocument doc, System.Collections.Generic.List<MfcElement> nodes,
        string className, int featureEnd, params int[] lengths)
    {
        var element = Single(nodes, className);
        if (element == null) return false;
        int next = NextTagOffset(doc, element, featureEnd);
        int actual = next - element.DataStart;
        foreach (int length in lengths)
            if (actual == length) return true;
        return false;
    }

    private static byte[]? Slice(MfcDocument doc, MfcElement element, int featureEnd, int length)
    {
        int start = element.DataStart;
        if (start < 0 || length <= 0 || start + length > featureEnd || start + length > doc.SourceData.Length)
            return null;
        if (NextTagOffset(doc, element, featureEnd) - start != length) return null;
        return MfcPrimitives.CopyRange(doc.SourceData, start, start + length);
    }

    private static int NextTagOffset(MfcDocument doc, MfcElement element, int featureEnd)
    {
        int end = featureEnd;
        foreach (var candidate in doc.AllElements)
        {
            if (candidate.TagOffset > element.TagOffset && candidate.TagOffset < end)
                end = candidate.TagOffset;
        }
        return end;
    }

    private static bool TryReadHeader(byte[] payload, uint expectedTypeId, out int restStart)
    {
        restStart = 0;
        if (payload.Length < 2 || BitConverter.ToUInt16(payload, 0) != 0x8004) return false;
        int cursor = 2;
        string? name = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (string.IsNullOrEmpty(name)) return false;
        if (cursor + 8 + 4 + 4 + 4 > payload.Length) return false;
        double headerDouble = BitConverter.ToDouble(payload, cursor);
        if (Math.Abs(Math.Abs(headerDouble) - 2.0) > 1e-12) return false;
        cursor += 8;
        if (BitConverter.ToUInt32(payload, cursor) != expectedTypeId) return false;
        cursor += 4;
        if (BitConverter.ToUInt32(payload, cursor) != 0) return false;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (!string.IsNullOrEmpty(empty)) return false;
        restStart = cursor;
        return true;
    }

    private static bool TryReadPoint(byte[] headerRaw, int restStart, out double x, out double y)
    {
        x = y = 0;
        if (restStart < 0 || restStart + HoleWizardSegments.PointXRestOffset + 8 > headerRaw.Length ||
            restStart + HoleWizardSegments.PointYRestOffset + 8 > headerRaw.Length) return false;
        x = BitConverter.ToDouble(headerRaw, restStart + HoleWizardSegments.PointXRestOffset);
        y = BitConverter.ToDouble(headerRaw, restStart + HoleWizardSegments.PointYRestOffset);
        // Controlled specimens place the hole on the 20 x 20 mm top face.
        return IsFinite(x) && IsFinite(y) && x >= 0.0 && x <= 0.02 && y >= 0.0 && y <= 0.02;
    }

    /// <summary>
    /// Reads the measured 40-byte face record embedded at a fixed offset in the legacy
    /// moSingleFaceRef_w (offset 0x58) and moCompFace_c (offset 0x56) payloads:
    ///   +0  u16 marker 0x80E7
    ///   +2  u16 1
    ///   +4  u32 0
    ///   +8  u16 0
    ///   +10 u32 face id (6 in the controlled top-face specimens)
    ///   +14 u32 class word 0x200
    ///   +18 u32 persistent selection id
    ///   +22 16-byte archive GUID
    ///   +38 u16 0
    /// All seven legacy main/resave samples carry the same face id/class word/GUID; the
    /// persistent id is stable across each main/resave pair. Unknown layouts are rejected.
    /// </summary>
    private static FeatureReferenceData? ParseFaceRecord(byte[] payload, int offset, string referenceName)
    {
        if (payload == null || offset < 0 ||
            offset + HoleWizardSegments.FaceRecordLength > payload.Length) return null;
        if (BitConverter.ToUInt16(payload, offset) != HoleWizardSegments.FaceRecordMarker ||
            BitConverter.ToUInt16(payload, offset + 2) != 1 ||
            BitConverter.ToUInt32(payload, offset + 4) != 0 ||
            BitConverter.ToUInt16(payload, offset + 8) != 0) return null;
        uint id = BitConverter.ToUInt32(payload, offset + HoleWizardSegments.FaceRecordIdOffset);
        uint classWord = BitConverter.ToUInt32(payload, offset + HoleWizardSegments.FaceRecordClassWordOffset);
        uint persistentId = BitConverter.ToUInt32(payload, offset + HoleWizardSegments.FaceRecordPersistentIdOffset);
        if (id == 0 || classWord != HoleWizardSegments.FaceRecordClassWord ||
            persistentId == 0 ||
            BitConverter.ToUInt16(payload, offset + HoleWizardSegments.FaceRecordTailOffset) != 0) return null;
        string guid = BitConverter.ToString(
            payload, offset + HoleWizardSegments.FaceRecordGuidOffset, 16)
            .Replace("-", string.Empty).ToLowerInvariant();
        if (guid != HoleWizardSegments.FaceArchiveGuid) return null;
        return new FeatureReferenceData
        {
            EntityType = SelectionEntityType.Face,
            EntityId = id,
            PersistentId = persistentId,
            ClassWord = classWord,
            ArchiveGuid = guid,
            RecordCount = 1,
            ReferenceName = referenceName
        };
    }

    private static bool WCharEquals(byte[] data, int offset, string expected)
    {
        if (offset < 0 || offset >= data.Length || !MfcPrimitives.IsWCharPrefix(data, offset))
            return false;
        int cursor = offset;
        string? value = MfcPrimitives.ReadWChar(data, ref cursor);
        return string.Equals(value, expected, StringComparison.Ordinal);
    }

    private static bool StringEquals(byte[] data, int offset, string expected)
    {
        byte[] bytes = System.Text.Encoding.Unicode.GetBytes(expected);
        if (offset < 0 || offset + bytes.Length > data.Length) return false;
        for (int i = 0; i < bytes.Length; i++)
            if (data[offset + i] != bytes[i]) return false;
        return true;
    }

    private static bool IsPositiveFinite(double value) => IsFinite(value) && value > 0;
    private static bool IsPositiveFinite(double? value) => value.HasValue && IsPositiveFinite(value.Value);
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>Returns null until the advanced hole sample matrix is decoded.</summary>
    public static AdvancedHoleFeatureDefinitionData? ParseAdvancedHole(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != AdvancedHoleClassName || typeId != AdvancedHoleTypeId) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        // Deliberately unimplemented: no moAdvHoleWizard_c sample has been structurally decoded.
        return null;
    }
}
