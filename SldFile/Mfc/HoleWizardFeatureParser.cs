using System;
using System.Linq;
using System.Text;
using SldFile.Binary;

namespace SldFile.Mfc;

internal static class HoleWizardFeatureParser
{
    internal const string HoleWizardClassName = HoleWizardSegments.ClassName;
    internal const uint LegacyHoleTypeId = HoleWizardSegments.LegacyTypeId;
    internal const uint TappedHoleTypeId = HoleWizardSegments.TappedTypeId;
    internal const uint HoleWizardTypeId = LegacyHoleTypeId;
    internal const string AdvancedHoleClassName = "moAdvHoleWizard_c";
    internal const uint AdvancedHoleTypeId = 0x52u;

    public static HoleWizardFeatureModel? ParseHoleWizard(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != HoleWizardClassName) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var legacy = LegacyHoleWizardTutorialParser.TryParse(doc, start, end);
        if (legacy != null) return legacy;

        var ctx = FeatureWindowContext.TryCreate(doc, className, typeId, start, end, expectedClassName: HoleWizardClassName);
        if (ctx == null) return null;

        if (typeId == LegacyHoleTypeId && ctx.StreamVersion == HoleWizardSegments.LegacyStreamVersion)
            return ParseLegacy(ctx);
        if (typeId == HoleWizardSegments.TwoPointTypeId && ctx.StreamVersion == HoleWizardSegments.LegacyStreamVersion)
            return ParseTwoPointCounterBore(ctx);
        if (typeId == TappedHoleTypeId && ctx.StreamVersion == HoleWizardSegments.LegacyStreamVersion)
        {
            if (ctx.GetSingleNode("moDisplayAngularDim_c") != null)
                return ParseCounterSink(ctx);
            return ParseCounterBoreBlind(ctx) ?? ParseCounterBore(ctx);
        }
        if (typeId == TappedHoleTypeId && ctx.StreamVersion == HoleWizardSegments.TappedStreamVersion)
            return ParseTapped(ctx);

        return null;
    }

    private static HoleWizardFeatureModel? ParseCounterBore(FeatureWindowContext ctx)
    {
        var header = ctx.GetSingleNode(HoleWizardSegments.ClassName);
        var angleParam = ctx.GetSingleNode("moAngleParameter_c");
        var compBody = ctx.GetSingleNode("moCompSolidBody_c");
        if (header == null || angleParam == null || compBody == null)
            return null;

        if (!ctx.RequireSingleNodesWithLengths(
                ("moCompFace_c", HoleWizardSegments.CounterBoreCompFaceLength),
                ("sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength),
                ("sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength),
                ("sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength),
                ("sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength),
                ("moAngleParameter_c", HoleWizardSegments.CounterBoreAngleParameterLength),
                ("moSkDimHandleValG2_c", HoleWizardSegments.CounterBoreSkDimHandleLength),
                ("moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength)) ||
            !ctx.HasSingleNodeWithLengthOneOf("moCompSolidBody_c",
                HoleWizardSegments.CounterBoreCompSolidBodyLength,
                HoleWizardSegments.CounterBoreAnsiCompSolidBodyLength))
            return null;

        byte[]? headerRaw = ctx.SlicePayload(header);
        byte[]? angleRaw = ctx.SlicePayload(angleParam, HoleWizardSegments.CounterBoreAngleParameterLength);
        byte[]? bodyRaw = ctx.SlicePayloadOneOf(compBody,
            HoleWizardSegments.CounterBoreCompSolidBodyLength,
            HoleWizardSegments.CounterBoreAnsiCompSolidBodyLength);
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

        double? thruDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? thruDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? boreDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.CounterBoreDiameterName);
        double? boreDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.CounterBoreDepthName);
        if (!IsPositiveFinite(thruDiameter) || !IsPositiveFinite(thruDepth) ||
            !IsPositiveFinite(boreDiameter) || !IsPositiveFinite(boreDepth))
            return null;
        if (!HoleWizardSegments.IsAllowedCounterBoreThruDiameter(thruDiameter!.Value) ||
            Math.Abs(thruDepth!.Value - 0.010) > 1e-12 ||
            !HoleWizardSegments.IsAllowedCounterBoreDiameter(boreDiameter!.Value) ||
            !HoleWizardSegments.IsAllowedCounterBoreDepth(boreDepth!.Value))
            return null;

        var definition = new HoleWizardFeatureModel
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
        MarkCounterBoreResolved(definition);
        return definition;
    }

    private static HoleWizardFeatureModel? ParseCounterBoreBlind(FeatureWindowContext ctx)
    {
        var header = ctx.GetSingleNode(HoleWizardSegments.ClassName);
        var angleParam = ctx.GetSingleNode("moAngleParameter_c");
        var compBody = ctx.GetSingleNode("moCompSolidBody_c");
        if (header == null || angleParam == null || compBody == null)
            return null;

        if (!ctx.RequireSingleNodesWithLengths(
                ("moCompFace_c", HoleWizardSegments.CounterBoreCompFaceLength),
                ("sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength),
                ("sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength),
                ("sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength),
                ("sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength),
                ("moAngleParameter_c", HoleWizardSegments.CounterBoreAngleParameterLength),
                ("moSkDimHandleValG2_c", HoleWizardSegments.CounterBoreSkDimHandleLength),
                ("moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength),
                ("moCompSolidBody_c", HoleWizardSegments.BlindCompSolidBodyLength)))
            return null;

        int tailTag = ctx.NextTagOffset(compBody);
        var tail = ctx.Document.AllElements.FirstOrDefault(e => e.TagOffset == tailTag && tailTag < ctx.End);
        if (tail == null || tail.ClassName.Length != 0 ||
            ctx.NextTagOffset(tail) - tail.DataStart != HoleWizardSegments.BlindTailElementSpan)
            return null;

        byte[]? headerRaw = ctx.SlicePayload(header);
        byte[]? angleRaw = ctx.SlicePayload(angleParam, HoleWizardSegments.CounterBoreAngleParameterLength);
        byte[]? bodyRaw = ctx.SlicePayload(compBody, HoleWizardSegments.BlindCompSolidBodyLength);
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
        double bodyDepth = BitConverter.ToDouble(bodyRaw, HoleWizardSegments.BlindThruHoleDepthOffset);
        if (Math.Abs(bodyDepth - 0.010) > 1e-12) return null;

        double? thruDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? namedDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? boreDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.CounterBoreDiameterName);
        double? boreDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.CounterBoreDepthName);
        if (!IsPositiveFinite(thruDiameter) || !IsPositiveFinite(namedDepth) ||
            !IsPositiveFinite(boreDiameter) || !IsPositiveFinite(boreDepth))
            return null;
        if (Math.Abs(thruDiameter!.Value - 0.006) > 1e-12 ||
            Math.Abs(boreDiameter!.Value - 0.011) > 1e-12 ||
            Math.Abs(boreDepth!.Value - 0.006) > 1e-12)
            return null;

        var definition = new HoleWizardFeatureModel
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

    private static HoleWizardFeatureModel? ParseTwoPointCounterBore(FeatureWindowContext ctx)
    {
        var header = ctx.GetSingleNode(HoleWizardSegments.ClassName);
        var sgExtEnt = ctx.GetSingleNode("sgExtEnt_c");
        var sketchRef = ctx.GetSingleNode("moSketchExtRef_w");
        var angleParam = ctx.GetSingleNode("moAngleParameter_c");
        var compBody = ctx.GetSingleNode("moCompSolidBody_c");
        var profile = ctx.GetSingleNode("mo3DProfileFeature_c");
        var backup = ctx.GetSingleNode("moPointBackedUpData_c");
        if (header == null || sgExtEnt == null || sketchRef == null || angleParam == null ||
            compBody == null || profile == null || backup == null)
            return null;

        if (!ctx.RequireSingleNodesWithLengths(
                (HoleWizardSegments.ClassName, HoleWizardSegments.TwoPointHeaderLength),
                ("moCompSketchEntHandle_c", 171),
                ("sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength),
                ("sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength),
                ("sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength),
                ("sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength),
                ("moAngleParameter_c", HoleWizardSegments.CounterBoreAngleParameterLength),
                ("moSkDimHandleValG2_c", HoleWizardSegments.CounterBoreSkDimHandleLength),
                ("moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength),
                ("moCompSolidBody_c", HoleWizardSegments.TwoPointCompSolidBodyLength),
                ("mo3DProfileFeature_c", HoleWizardSegments.TwoPoint3DProfileLength),
                ("moPointBackedUpData_c", HoleWizardSegments.TwoPointPointBackupLength)))
            return null;

        byte[]? headerRaw = ctx.SlicePayload(header);
        byte[]? angleRaw = ctx.SlicePayload(angleParam, HoleWizardSegments.CounterBoreAngleParameterLength);
        byte[]? bodyRaw = ctx.SlicePayload(compBody, HoleWizardSegments.TwoPointCompSolidBodyLength);
        byte[]? profileRaw = ctx.SlicePayload(profile, HoleWizardSegments.TwoPoint3DProfileLength);
        byte[]? backupRaw = ctx.SlicePayload(backup, HoleWizardSegments.TwoPointPointBackupLength);
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

        double? thruDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? thruDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? boreDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.CounterBoreDiameterName);
        double? boreDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
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

        var definition = new HoleWizardFeatureModel
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
        return definition;
    }

    private static void MarkCounterBoreResolved(HoleWizardFeatureModel definition)
    {
        foreach (string name in new[]
        {
            nameof(HoleWizardFeatureModel.FolderTypeId),
            nameof(HoleWizardFeatureModel.Type),
            nameof(HoleWizardFeatureModel.EndCondition),
            nameof(HoleWizardFeatureModel.Standard),
            nameof(HoleWizardFeatureModel.Standard2),
            nameof(HoleWizardFeatureModel.FastenerType),
            nameof(HoleWizardFeatureModel.FastenerType2),
            nameof(HoleWizardFeatureModel.FastenerSize),
            nameof(HoleWizardFeatureModel.HoleDiameter),
            nameof(HoleWizardFeatureModel.Depth),
            nameof(HoleWizardFeatureModel.HoleDepth),
            nameof(HoleWizardFeatureModel.ThruHoleDiameter),
            nameof(HoleWizardFeatureModel.ThruHoleDepth),
            nameof(HoleWizardFeatureModel.CounterBoreDiameter),
            nameof(HoleWizardFeatureModel.CounterBoreDepth),
            nameof(HoleWizardFeatureModel.CounterSinkDiameter),
            nameof(HoleWizardFeatureModel.CounterSinkAngle),
            nameof(HoleWizardFeatureModel.ThreadDiameter),
            nameof(HoleWizardFeatureModel.ThreadDepth),
            nameof(HoleWizardFeatureModel.SketchPoints),
            nameof(HoleWizardFeatureModel.SketchPointLocations)
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

    private static HoleWizardFeatureModel? ParseCounterSink(FeatureWindowContext ctx)
    {
        var header = ctx.GetSingleNode(HoleWizardSegments.ClassName);
        var angleParam = ctx.GetSingleNode("moAngleParameter_c");
        var compBody = ctx.GetSingleNode("moCompSolidBody_c");
        if (header == null || angleParam == null || compBody == null)
            return null;

        if (!ctx.RequireSingleNodesWithLengths(
                ("moCompFace_c", HoleWizardSegments.CounterSinkCompFaceLength),
                ("sgPntPntDist", HoleWizardSegments.CounterBoreSgPntPntDistLength),
                ("sgLLDist", HoleWizardSegments.CounterBoreSgLLDistLength),
                ("sgPntLineDist", HoleWizardSegments.CounterBoreSgPntLineDistLength),
                ("sgAnglDim", HoleWizardSegments.CounterBoreSgAnglDimLength),
                ("moAngleParameter_c", HoleWizardSegments.CounterSinkAngleParameterLength),
                ("moSkDimHandleValG2_c", HoleWizardSegments.CounterSinkSkDimHandleLength),
                ("moDisplayAngularDim_c", HoleWizardSegments.CounterSinkDisplayAngularDimLength),
                ("moFixedRefPlnData_c", HoleWizardSegments.CounterBoreFixedRefPlnDataLength),
                ("moCompSolidBody_c", HoleWizardSegments.CounterSinkCompSolidBodyLength)))
            return null;

        byte[]? headerRaw = ctx.SlicePayload(header);
        byte[]? angleRaw = ctx.SlicePayload(angleParam, HoleWizardSegments.CounterSinkAngleParameterLength);
        byte[]? bodyRaw = ctx.SlicePayload(compBody, HoleWizardSegments.CounterSinkCompSolidBodyLength);
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

        double? thruDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDiameterName);
        double? thruDepth = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.ThruHoleDepthName);
        double? csDiameter = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
            angleRaw, HoleWizardSegments.CounterSinkDiameterName);
        double? csAngle = MfcParameterReader.ReadUniqueNamedDoubleInSlice(
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

        var definition = new HoleWizardFeatureModel
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
        MarkCounterBoreResolved(definition);
        return definition;
    }

    private static string? ReadWCharAt(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length || !MfcPrimitives.IsWCharPrefix(data, offset))
            return null;
        int cursor = offset;
        return MfcPrimitives.ReadWChar(data, ref cursor);
    }

    private static HoleWizardFeatureModel? ParseLegacy(FeatureWindowContext ctx)
    {
        var header = ctx.GetSingleNode(HoleWizardSegments.ClassName);
        var diameterDim = ctx.GetSingleNode("sgLLDist");
        var dimHandle = ctx.GetSingleNode("moSkDimHandleValG2_c");
        var solidBody = ctx.GetSingleNode("moCompSolidBody_c");
        var singleFaceElement = ctx.GetSingleNode("moSingleFaceRef_w");
        var compFaceElement = ctx.GetSingleNode("moCompFace_c");
        if (header == null || diameterDim == null || dimHandle == null || solidBody == null ||
            singleFaceElement == null || compFaceElement == null) return null;

        if (!ctx.RequireSingleNodesWithLengths(
                ("moFaceRefPlnData_c", HoleWizardSegments.LegacyFaceRefPlnDataLength),
                ("moCompFace_c", HoleWizardSegments.LegacyCompFaceLength),
                ("moSingleFaceRef_w", HoleWizardSegments.LegacySingleFaceRefLength)) ||
            !ctx.HasExactNodeCount("sgPntPntDist", 1) ||
            !ctx.HasExactNodeCount("moFixedRefPlnData_c", 1))
            return null;

        byte[]? headerRaw = ctx.SlicePayload(header);
        byte[]? diameterRaw = ctx.SlicePayload(diameterDim, HoleWizardSegments.LegacySgLLDistLength);
        byte[]? handleRaw = ctx.SlicePayload(dimHandle, HoleWizardSegments.LegacySkDimHandleLength);
        byte[]? bodyRaw = ctx.SlicePayload(solidBody, HoleWizardSegments.LegacyCompSolidBodyLength);
        byte[]? singleFaceRaw = ctx.SlicePayload(singleFaceElement, HoleWizardSegments.LegacySingleFaceRefLength);
        byte[]? compFaceRaw = ctx.SlicePayload(compFaceElement, HoleWizardSegments.LegacyCompFaceLength);
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

        var definition = new HoleWizardFeatureModel
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
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.FolderTypeId));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.Type));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.EndCondition));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.Diameter));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.Face));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.IFace));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.SketchPoints));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.SketchPointLocations));
        return definition;
    }

    private static HoleWizardFeatureModel? ParseTapped(FeatureWindowContext ctx)
    {
        var header = ctx.GetSingleNode(HoleWizardSegments.ClassName);
        var solidBody = ctx.GetSingleNode("moCompSolidBody_c");
        var threadDim = ctx.GetSingleNode("ThreeDdiameterDim_c");
        var threadData = ctx.GetSingleNode("moCThreadData_c");
        var angleParameter = ctx.GetSingleNode("moAngleParameter_c");
        var faceRef = ctx.GetSingleNode("moSingleFaceRef_w");
        if (header == null || solidBody == null || threadDim == null || threadData == null ||
            angleParameter == null || faceRef == null) return null;

        if (!ctx.RequireSingleNodesWithLengths(
                ("moFaceRefPlnData_c", 4228),
                ("sgPntPntDist", 140),
                ("sgLLDist", 424),
                ("sgPntLineDist", 2126),
                ("sgAnglDim", 4),
                ("moSkDimHandleValG2_c", 2458),
                ("moFixedRefPlnData_c", 357),
                ("moCosmeticThread_c", 165),
                ("moCompEdge_c", 86),
                ("moEdgeRef_c", 212),
                ("moDisplayRadialDim_c", 661),
                ("moCylinderRef_w", 615),
                ("moCompSolidBody_c", HoleWizardSegments.TappedCompSolidBodyLength),
                ("moSingleFaceRef_w", HoleWizardSegments.TappedSingleFaceRefLength),
                ("ThreeDdiameterDim_c", HoleWizardSegments.TappedThreadDimLength),
                ("moCThreadData_c", HoleWizardSegments.TappedCThreadDataLength),
                ("moAngleParameter_c", HoleWizardSegments.TappedAngleParameterLength)))
            return null;

        byte[]? headerRaw = ctx.SlicePayload(header);
        byte[]? bodyRaw = ctx.SlicePayload(solidBody, HoleWizardSegments.TappedCompSolidBodyLength);
        byte[]? threadDimRaw = ctx.SlicePayload(threadDim, HoleWizardSegments.TappedThreadDimLength);
        byte[]? threadDataRaw = ctx.SlicePayload(threadData, HoleWizardSegments.TappedCThreadDataLength);
        byte[]? angleRaw = ctx.SlicePayload(angleParameter, HoleWizardSegments.TappedAngleParameterLength);
        byte[]? faceRefRaw = ctx.SlicePayload(faceRef, HoleWizardSegments.TappedSingleFaceRefLength);
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
        if (!IsPositiveFinite(threadDepth) ||
            !MfcParameterReader.VerifyDoubleSlots(threadDataRaw, HoleWizardSegments.TappedThreadDepthCopyOffsets, threadDepth, tolerance: 0.0))
            return null;

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

        var definition = new HoleWizardFeatureModel
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
            nameof(HoleWizardFeatureModel.FolderTypeId),
            nameof(HoleWizardFeatureModel.Type),
            nameof(HoleWizardFeatureModel.EndCondition),
            nameof(HoleWizardFeatureModel.Standard),
            nameof(HoleWizardFeatureModel.Standard2),
            nameof(HoleWizardFeatureModel.FastenerType),
            nameof(HoleWizardFeatureModel.FastenerType2),
            nameof(HoleWizardFeatureModel.FastenerSize),
            nameof(HoleWizardFeatureModel.ThreadDiameter),
            nameof(HoleWizardFeatureModel.ThreadDepth),
            nameof(HoleWizardFeatureModel.CosmeticThreadType),
            nameof(HoleWizardFeatureModel.SketchPoints),
            nameof(HoleWizardFeatureModel.SketchPointLocations)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        return definition;
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
        return IsFinite(x) && IsFinite(y) && x >= 0.0 && x <= 0.02 && y >= 0.0 && y <= 0.02;
    }

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

    public static AdvancedHoleFeatureModel? ParseAdvancedHole(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != AdvancedHoleClassName || typeId != AdvancedHoleTypeId) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;
        return null;
    }
}

internal static class LegacyHoleWizardTutorialParser
{
    internal static LegacyHoleWizardFamily? MatchFamily(MfcDocument doc, int start, int end)
    {
        return TryMatchFamily(doc, start, end, out var family, out _) ? family : null;
    }

    internal static HoleWizardFeatureModel? TryParse(MfcDocument doc, int start, int end)
    {
        if (!TryMatchFamily(doc, start, end, out var family, out var ctx) || family == null || ctx == null)
            return null;

        var definition = new HoleWizardFeatureModel
        {
            FolderTypeId = family.HeaderTypeId,
            Type = family.Type,
            EndCondition = family.EndCondition
        };
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.FolderTypeId));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.Type));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureModel.EndCondition));

        foreach (var field in family.Fields)
        {
            int tagOffset = start + field.ClassTagOffset;
            var element = ctx.Nodes.FirstOrDefault(e =>
                e.TagOffset == tagOffset && e.ClassName == field.ClassName);
            byte[]? payload = ctx.SlicePayload(element);
            if (payload == null || field.Offset < 0 || field.Offset >= payload.Length)
                return null;

            if (field.IsString)
            {
                byte[] expectedBytes = Encoding.Unicode.GetBytes(field.ExpectedString!);
                if (field.Offset + expectedBytes.Length > payload.Length) return null;
                for (int i = 0; i < expectedBytes.Length; i++)
                    if (payload[field.Offset + i] != expectedBytes[i]) return null;
                SetString(definition, field.Property, field.ExpectedString!);
            }
            else
            {
                if (field.Offset + 8 > payload.Length) return null;
                double value = BitConverter.ToDouble(payload, field.Offset);
                if (BitConverter.DoubleToInt64Bits(value) !=
                    BitConverter.DoubleToInt64Bits(field.ExpectedDouble)) return null;
                SetDouble(definition, field.Property, value);
            }
            definition.MarkResolvedInternal(field.Property);
        }
        return definition;
    }

    private static bool TryMatchFamily(
        MfcDocument doc,
        int start,
        int end,
        out LegacyHoleWizardFamily? family,
        out FeatureWindowContext? ctx)
    {
        family = null;
        ctx = FeatureWindowContext.TryCreate(doc, "moHoleWzd_c", 0, start, end);
        if (ctx == null)
            return false;

        var hole = ctx.Nodes.FirstOrDefault(e => e.ClassName == "moHoleWzd_c" && e.TagOffset == start)
            ?? ctx.Nodes.FirstOrDefault(e => e.ClassName == "moHoleWzd_c");
        if (hole == null)
            return false;

        byte[] raw = ctx.RawData;
        int headerEnd = ctx.NextTagOffset(hole);
        if (hole.DataStart + 2 > headerEnd)
            return false;

        int cursor = hole.DataStart + 2;
        MfcPrimitives.ReadWChar(raw, ref cursor);
        cursor += 8;
        if (cursor + 8 > headerEnd)
            return false;
        uint headerTypeId = BitConverter.ToUInt32(raw, cursor);
        cursor += 8;
        MfcPrimitives.ReadWChar(raw, ref cursor);
        int restLength = headerEnd - cursor;

        ushort version = ctx.StreamVersion;
        ushort folderCount = raw.Length >= 6 ? BitConverter.ToUInt16(raw, 4) : (ushort)0;
        family = LegacyHoleWizardTutorialTemplates.Families.FirstOrDefault(f =>
            f.Version == version && f.FolderCount == folderCount &&
            f.HeaderTypeId == headerTypeId && f.RestLength == restLength);
        return family != null;
    }

    private static void SetString(HoleWizardFeatureModel d, string property, string value)
    {
        switch (property)
        {
            case "Standard": d.Standard = value; break;
            case "FastenerType": d.FastenerType = value; break;
            case "FastenerSize": d.FastenerSize = value; break;
            default: throw new InvalidOperationException("Unknown legacy hole string field " + property);
        }
    }

    private static void SetDouble(HoleWizardFeatureModel d, string property, double value)
    {
        switch (property)
        {
            case "ThruHoleDiameter": d.ThruHoleDiameter = value; break;
            case "ThruHoleDepth": d.ThruHoleDepth = value; break;
            case "CounterBoreDiameter": d.CounterBoreDiameter = value; break;
            case "CounterBoreDepth": d.CounterBoreDepth = value; break;
            case "CounterSinkDiameter": d.CounterSinkDiameter = value; break;
            case "CounterSinkAngle": d.CounterSinkAngle = value; break;
            case "ThreadDiameter": d.ThreadDiameter = value; break;
            case "ThreadDepth": d.ThreadDepth = value; break;
            default: throw new InvalidOperationException("Unknown legacy hole numeric field " + property);
        }
    }
}
