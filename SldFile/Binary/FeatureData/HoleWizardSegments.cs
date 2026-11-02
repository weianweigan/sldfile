using System;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured offsets for the SW2026 Hole Wizard layouts (<c>moHoleWzd_c</c>):
/// - legacy simple hole: FolderHeader TypeId 0x54, stream 0x71, header payload 535, rest 505;
/// - tapped hole: FolderHeader TypeId 0x52, stream 0x84, header payload 545, rest 505;
/// - native ISO M6 counterbore: TypeId 0x52, stream 0x71, header rest 505, counterbore values
///   in Chinese named parameters inside moAngleParameter_c.
/// Both store one sketch point at rest +261/+269. The legacy diameter is the sgLLDist
/// dimension value at +38 with a copy in moSkDimHandleValG2_c +453; the termination is the
/// final u32 of moCompSolidBody_c. Tapped holes carry thread diameter/depth, type 48 and the
/// ISO/M6 fastener selection in the measured objects below.
/// </summary>
internal static class HoleWizardSegments
{
    internal const string ClassName = "moHoleWzd_c";
    internal const uint LegacyTypeId = 0x54u;
    internal const uint TappedTypeId = 0x52u;
    internal const ushort LegacyStreamVersion = 0x71;
    internal const ushort TappedStreamVersion = 0x84;

    internal const int HeaderRestLength = 505;
    internal const int LegacyHeaderLength = 535;
    internal const int TappedHeaderLength = 545;
    internal const int PointXRestOffset = 261;
    internal const int PointYRestOffset = 269;

    // Legacy simple layout.
    internal const int LegacySgLLDistLength = 140;
    internal const int LegacyDiameterOffset = 38;
    internal const int LegacySkDimHandleLength = 2448;
    internal const int LegacyDiameterCopyOffset = 453;
    internal const int LegacyCompSolidBodyLength = 351;
    internal const int LegacyEndConditionOffset = 347;
    internal const int LegacyFaceRefPlnDataLength = 145;
    internal const int LegacyCompFaceLength = 2301;
    internal const int LegacySingleFaceRefLength = 659;
    internal const int LegacyTypeValue = 0;

    // Feature-local 40-byte face reference record (one in moCompFace_c and one in the
    // legacy moSingleFaceRef_w). The 38-byte moFaceRef_c at the head of the stream belongs
    // to the base extrusion folder and is deliberately not attributed to the hole.
    internal const int FaceRecordLength = 40;
    internal const ushort FaceRecordMarker = 0x80E7;
    internal const uint FaceRecordClassWord = 0x200u;
    internal const int FaceRecordIdOffset = 10;
    internal const int FaceRecordClassWordOffset = 14;
    internal const int FaceRecordPersistentIdOffset = 18;
    internal const int FaceRecordGuidOffset = 22;
    internal const int FaceRecordTailOffset = 38;
    internal const int LegacyCompFaceRecordOffset = 0x56;
    internal const int LegacySingleFaceRecordOffset = 0x58;
    internal const string FaceArchiveGuid = "7dc39425ad49b2547dc39425ad49b254";

    // Counterbore layout (native API ISO M6 socket-head cap screw, stream 0x71, TypeId 0x52).
    internal const int CounterBoreTypeValue = 14;
    internal const int CounterBoreCompFaceLength = 4121;
    internal const int CounterBoreSgPntPntDistLength = 140;
    internal const int CounterBoreSgLLDistLength = 424;
    internal const int CounterBoreSgPntLineDistLength = 2126;
    internal const int CounterBoreSgAnglDimLength = 4;
    internal const int CounterBoreAngleParameterLength = 14642;
    internal const int CounterBoreSkDimHandleLength = 5384;
    internal const int CounterBoreFixedRefPlnDataLength = 544;
    internal const int CounterBoreCompSolidBodyLength = 730;
    internal const int CounterBoreAnsiCompSolidBodyLength = 760;
    internal const int CounterBoreStandard2Offset = 0x21F;
    internal const int CounterBoreFastenerType2Offset = 0x223;
    internal const int CounterBoreStandardOffset = 0x195;
    internal const int CounterBoreFastenerTypeOffset = 0x19F;
    internal const int CounterBoreFastenerSizeOffset = 0x1BD;
    internal const int CounterBoreAnsiStandard2Offset = 0x23D;
    internal const int CounterBoreAnsiFastenerType2Offset = 0x241;
    internal const int CounterBoreAnsiStandardOffset = 0x195;
    internal const int CounterBoreAnsiFastenerTypeOffset = 0x1AF;
    internal const int CounterBoreAnsiFastenerSizeOffset = 0x1DD;
    internal const string CounterBoreStandard = "ISO";
    internal const string CounterBoreFastenerType = "六角凹头 ISO 4762";
    internal const string CounterBoreFastenerSize = "M6";
    internal const string CounterBoreAnsiStandard = "ANSI Metric";
    internal const string CounterBoreAnsiFastenerType = "凹头盖螺钉 - ANSI B18.3.1M";
    internal const string ThruHoleDiameterName = "通孔孔直径";
    internal const string ThruHoleDepthName = "通孔孔深度";
    internal const string CounterBoreDiameterName = "柱形沉头孔直径";
    internal const string CounterBoreDepthName = "柱形沉头孔深度";

    internal static bool IsAllowedCounterBoreThruDiameter(double value) =>
        Near(value, 0.006) || Near(value, 0.008) || Near(value, 0.010);
    internal static bool IsAllowedCounterBoreDiameter(double value) =>
        Near(value, 0.011) || Near(value, 0.013);
    internal static bool IsAllowedCounterBoreDepth(double value) =>
        Near(value, 0.004) || Near(value, 0.006) || Near(value, 0.008);
    internal static bool IsAllowedCounterSinkDiameter(string? size, double value) =>
        size == "M6" ? Near(value, 0.011) || Near(value, 0.013) || Near(value, 0.017)
                     : Near(value, 0.015);
    internal static bool IsAllowedCounterSinkAngle(double value) =>
        Near(value, Math.PI / 2.0) ||
        Near(value, 100.0 * Math.PI / 180.0) ||
        Near(value, 120.0 * Math.PI / 180.0);
    private static bool Near(double value, double expected) => Math.Abs(value - expected) <= 1e-12;

    // Blind counterbore layout (native CreateCounterBore + IWizardHoleFeatureData2
    // EndCondition=Blind + ThruHoleDepth through ModifyDefinition, stream 0x71, TypeId 0x52).
    // The 730-byte through-all moCompSolidBody_c becomes 557 bytes followed by a measured
    // empty-class tail element spanning 167 bytes.
    internal const int BlindCompSolidBodyLength = 557;
    internal const int BlindEndConditionOffset = 0x15B;
    internal const int BlindThruHoleDepthOffset = 0x161;
    internal const int BlindFlagOffset = 0x169;
    internal const int BlindTailElementSpan = 167;

    // Two-point counterbore layout (HoleWizard5 with two EXTSKETCHPOINT selections,
    // stream 0x71, TypeId 0x53). Header rest is 8 bytes; the feature carries an embedded
    // mo3DProfileFeature_c and moPointBackedUpData_c with one measured coordinate triple each.
    internal const uint TwoPointTypeId = 0x53u;
    internal const int TwoPointHeaderLength = 96;
    internal const int TwoPointHeaderRestLength = 8;
    internal const int TwoPoint3DProfileLength = 390;
    internal const int TwoPointPointBackupLength = 1120;
    internal const int TwoPointCompSolidBodyLength = 742;
    internal const int TwoPointProfilePointOffset = 0xFF;
    internal const int TwoPointBackupPointOffset = 0xF1;

    // Countersink layout (native API ISO flat-head, stream 0x71, TypeId 0x52).
    internal const int CounterSinkTypeValue = 44;
    internal const int CounterSinkCompFaceLength = 4101;
    internal const int CounterSinkAngleParameterLength = 18475;
    internal const int CounterSinkSkDimHandleLength = 3809;
    internal const int CounterSinkDisplayAngularDimLength = 1588;
    internal const int CounterSinkCompSolidBodyLength = 734;
    internal const int CounterSinkStandard2Offset = 0x223;
    internal const int CounterSinkFastenerType2Offset = 0x227;
    internal const int CounterSinkStandardOffset = 0x195;
    internal const int CounterSinkFastenerTypeOffset = 0x19F;
    internal const int CounterSinkFastenerSizeOffset = 0x1C1;
    internal const string CounterSinkStandard = "ISO";
    internal const string CounterSinkFastenerType = "锥孔平头 ISO 7046-1";
    internal const string CounterSinkDiameterName = "近端锥形沉头孔直径";
    internal const string CounterSinkAngleName = "近端锥形沉头孔角度";

    // Tapped layout.
    internal const int TappedCompSolidBodyLength = 653;    internal const int TappedEndConditionOffset = 649;
    internal const int TappedThreadDimLength = 1122;
    internal const int TappedThreadDiameterOffset = 38;
    internal const int TappedCThreadDataLength = 2091;
    internal const int TappedThreadDiameterCopyOffset = 29;
    internal const int TappedThreadDepthOffset = 65;
    internal static readonly int[] TappedThreadDepthCopyOffsets = { 1105, 1286 };
    internal const int TappedAngleParameterLength = 19426;
    internal const int TappedTypeOffset = 14811;
    internal const int TappedTypeValue = 48;
    internal const int TappedSingleFaceRefLength = 719;
    internal const int TappedStandardOffset = 432;
    internal const int TappedFastenerTypeOffset = 442;
    internal const int TappedFastenerSizeOffset = 452;
    internal const int TappedStandard2Offset = 532;
    internal const int TappedFastenerType2Offset = 536;
    internal const int TappedCosmeticThreadTypeOffset = 548;
    internal const string TappedStandard = "ISO";
    internal const string TappedFastenerType = "螺纹孔";
    internal const string TappedFastenerSize = "M6";
    internal const int TappedStandard2Value = 8;
    internal const int TappedFastenerType2Value = 147;
    internal const int TappedCosmeticThreadTypeValue = 1;
}
