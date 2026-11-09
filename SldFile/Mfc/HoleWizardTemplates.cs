using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;


#region Measured Segments & Offsets

/// <summary>
/// Measured offsets and layouts for SW2026 Hole Wizard layouts (<c>moHoleWzd_c</c>).
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

    // Feature-local 40-byte face reference record.
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

    // Counterbore layout.
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

    // Blind counterbore layout.
    internal const int BlindCompSolidBodyLength = 557;
    internal const int BlindEndConditionOffset = 0x15B;
    internal const int BlindThruHoleDepthOffset = 0x161;
    internal const int BlindFlagOffset = 0x169;
    internal const int BlindTailElementSpan = 167;

    // Two-point counterbore layout.
    internal const uint TwoPointTypeId = 0x53u;
    internal const int TwoPointHeaderLength = 96;
    internal const int TwoPointHeaderRestLength = 8;
    internal const int TwoPoint3DProfileLength = 390;
    internal const int TwoPointPointBackupLength = 1120;
    internal const int TwoPointCompSolidBodyLength = 742;
    internal const int TwoPointProfilePointOffset = 0xFF;
    internal const int TwoPointBackupPointOffset = 0xF1;

    // Countersink layout.
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
    internal const int TappedCompSolidBodyLength = 653;
    internal const int TappedEndConditionOffset = 649;
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

#endregion

#region Parameter Slots

/// <summary>
/// Measured single-variable slot map for the ISO M6 counterbore and countersink authoring blocks.
/// </summary>
internal static class HoleWizardParameterSlots
{
    internal static readonly int[] CounterBoreThruDiameterDeltas = { 0, 0x1383 };
    internal static readonly int[] CounterBoreDiameterDeltas = { 0, 0x1B8D };
    internal static readonly int[] CounterBoreDepthDeltas =
        { -0x429A, -0x4156, 0, 0xDC9, 0x1937, 0x1ECC, 0x1EE4, 0x1F6E, 0x1F86 };
    internal static readonly int[] CounterSinkDiameterDeltas = { 0, 0x1DB2 };
    internal static readonly int[] CounterSinkAngleDeltas = { 0, 0x21C0 };

    internal static bool TryFindNamedDouble(
        byte[] payload, string name, out int valueOffset, out double value)
    {
        valueOffset = -1;
        value = 0;
        if (payload == null) return false;
        int found = -1;
        double foundValue = 0;
        int count = 0;
        for (int i = 0; i + 6 <= payload.Length; i++)
        {
            if (payload[i] != 0x04 || payload[i + 1] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(payload, i + 2)) continue;
            int position = i + 2;
            string? candidate = MfcPrimitives.ReadWChar(payload, ref position);
            if (!string.Equals(candidate, name, StringComparison.Ordinal)) continue;
            if (position + 30 > payload.Length ||
                BitConverter.ToDouble(payload, position) != 2.0 ||
                BitConverter.ToUInt32(payload, position + 8) != uint.MaxValue ||
                BitConverter.ToUInt32(payload, position + 12) != 0 ||
                !MfcPrimitives.IsWCharPrefix(payload, position + 16) ||
                payload[position + 19] != 0 ||
                BitConverter.ToUInt16(payload, position + 20) != 0)
                return false;
            found = position + 22;
            foundValue = BitConverter.ToDouble(payload, found);
            count++;
        }
        if (count != 1) return false;
        valueOffset = found;
        value = foundValue;
        return true;
    }

    internal static void PatchCounterBore(
        byte[] block, double thruDiameter, double counterBoreDiameter, double counterBoreDepth)
    {
        RequirePositiveFinite(thruDiameter, nameof(thruDiameter));
        RequirePositiveFinite(counterBoreDiameter, nameof(counterBoreDiameter));
        RequirePositiveFinite(counterBoreDepth, nameof(counterBoreDepth));
        if (!TryFindNamedDouble(block, HoleWizardSegments.ThruHoleDiameterName,
                out int thruOffset, out double thruMeasured) ||
            Math.Abs(thruMeasured - 0.006) > 1e-12)
            throw new NotSupportedException(
                "Counterbore parameterization requires the measured 脴6 named-parameter layout. " +
                "No stream changes have been made.");
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterBoreDiameterName,
                out int boreOffset, out double boreMeasured) ||
            Math.Abs(boreMeasured - 0.011) > 1e-12)
            throw new NotSupportedException(
                "Counterbore parameterization requires the measured 脴11 named-parameter layout. " +
                "No stream changes have been made.");
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterBoreDepthName,
                out int depthOffset, out double depthMeasured) ||
            Math.Abs(depthMeasured - 0.006) > 1e-12)
            throw new NotSupportedException(
                "Counterbore parameterization requires the measured 6 mm depth named-parameter layout. " +
                "No stream changes have been made.");
        PatchDeltas(block, thruOffset, CounterBoreThruDiameterDeltas, 0.006, thruDiameter);
        PatchDeltas(block, boreOffset, CounterBoreDiameterDeltas, 0.011, counterBoreDiameter);
        PatchDeltas(block, depthOffset, CounterBoreDepthDeltas, 0.006, counterBoreDepth);
    }

    internal static void PatchCounterSink(
        byte[] block, bool m6, double counterSinkDiameter, double counterSinkAngle)
    {
        RequirePositiveFinite(counterSinkDiameter, nameof(counterSinkDiameter));
        RequirePositiveFinite(counterSinkAngle, nameof(counterSinkAngle));
        double measuredDiameter = m6 ? 0.011 : 0.015;
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterSinkDiameterName,
                out int diameterOffset, out double diameterMeasured) ||
            Math.Abs(diameterMeasured - measuredDiameter) > 1e-12)
            throw new NotSupportedException(
                "Countersink parameterization requires the measured diameter named-parameter layout. " +
                "No stream changes have been made.");
        if (!TryFindNamedDouble(block, HoleWizardSegments.CounterSinkAngleName,
                out int angleOffset, out double angleMeasured) ||
            Math.Abs(angleMeasured - Math.PI / 2.0) > 1e-12)
            throw new NotSupportedException(
                "Countersink parameterization requires the measured 90-degree named-parameter layout. " +
                "No stream changes have been made.");
        PatchDeltas(block, diameterOffset, CounterSinkDiameterDeltas, measuredDiameter, counterSinkDiameter);
        PatchDeltas(block, angleOffset, CounterSinkAngleDeltas, Math.PI / 2.0, counterSinkAngle);
    }

    private static void PatchDeltas(
        byte[] block, int baseOffset, int[] deltas, double measuredValue, double newValue)
    {
        if (Math.Abs(measuredValue - newValue) <= 1e-15) return;
        foreach (int delta in deltas)
        {
            int offset = baseOffset + delta;
            if (offset < 0 || offset + 8 > block.Length)
                throw new NotSupportedException(
                    "Counterbore/countersink parameter slot lies outside the measured block. " +
                    "No stream changes have been made.");
            if (Math.Abs(BitConverter.ToDouble(block, offset) - measuredValue) > 1e-12)
                throw new NotSupportedException(
                    "Counterbore/countersink parameter slot does not carry the measured template value. " +
                    "No stream changes have been made.");
            BitConverter.GetBytes(newValue).CopyTo(block, offset);
        }
    }

    private static void RequirePositiveFinite(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(name, $"{name} must be a positive finite number.");
    }
}

#endregion

#region Measured CounterBore Templates

internal sealed class HoleWizardMeasuredCounterBoreTemplate
{
    internal HoleWizardMeasuredCounterBoreTemplate(
        string sample, string featureName, int baseLength, ushort strippedFolderCount,
        string baseSha256, int blockLength, byte[] block, int endCondition,
        double thruHoleDepth, int sketchPointCount, double point0X, double point0Y, double point1X, double point1Y)
    {
        Sample = sample; FeatureName = featureName; BaseLength = baseLength; StrippedFolderCount = strippedFolderCount;
        BaseSha256 = baseSha256; BlockLength = blockLength; Block = block;
        EndCondition = endCondition; ThruHoleDepth = thruHoleDepth; SketchPointCount = sketchPointCount;
        Point0X = point0X; Point0Y = point0Y; Point1X = point1X; Point1Y = point1Y;
    }
    internal string Sample { get; }
    internal string FeatureName { get; }
    internal int BaseLength { get; }
    internal ushort StrippedFolderCount { get; }
    internal string BaseSha256 { get; }
    internal int BlockLength { get; }
    internal byte[] Block { get; }
    internal int EndCondition { get; }
    internal double ThruHoleDepth { get; }
    internal int SketchPointCount { get; }
    internal double Point0X { get; }
    internal double Point0Y { get; }
    internal double Point1X { get; }
    internal double Point1Y { get; }
}

internal static class HoleWizardBlindCounterBoreTemplates
{
    internal static readonly HoleWizardMeasuredCounterBoreTemplate Depth10 =
        new HoleWizardMeasuredCounterBoreTemplate("blind_cbore_thru_depth.SLDPRT", "Hole_BlindCbore_thru_depth", 12600, 20, "0b0f4552a04c704d8b50e10844a0bdb7fac614c880e6be54ca35b5f666ae39f8", 30216, HoleWizardAuthoringSegments.BlindTwoPoint0, 0, 0.01, 1, 0.009986487374987973, 0.010014305598872397, 0, 0);
    internal static readonly HoleWizardMeasuredCounterBoreTemplate Depth8 =
        new HoleWizardMeasuredCounterBoreTemplate("blind_cbore_thru_depth8.SLDPRT", "Hole_BlindCbore_thru_depth8", 12604, 20, "37b95e3d6db9913a0bd3cf2a4d0b482b133365dbd87fce1a8916865a74f244a5", 30218, HoleWizardAuthoringSegments.BlindTwoPoint1, 0, 0.008, 1, 0.009986487374987973, 0.010014305598872397, 0, 0);
    internal static readonly HoleWizardMeasuredCounterBoreTemplate[] All = { Depth10, Depth8 };
    internal static HoleWizardMeasuredCounterBoreTemplate? FindByDepth(double depth) =>
        Array.Find(All, t => Math.Abs(t.ThruHoleDepth - depth) <= 1e-12);
    internal static HoleWizardMeasuredCounterBoreTemplate? FindByBase(byte[] stream, double depth)
    {
        if (stream == null) return null;
        byte[] norm = (byte[])stream.Clone(); if (norm.Length >= 6) { norm[4] = 0; norm[5] = 0; }
        string sha; using (var h = System.Security.Cryptography.SHA256.Create()) sha = BitConverter.ToString(h.ComputeHash(norm)).Replace("-", "").ToLowerInvariant();
        return Array.Find(All, t => t.BaseLength == stream.Length && t.BaseSha256 == sha && Math.Abs(t.ThruHoleDepth - depth) <= 1e-12);
    }
}

internal static class HoleWizardTwoPointCounterBoreTemplates
{
    internal static readonly HoleWizardMeasuredCounterBoreTemplate Source =
        new HoleWizardMeasuredCounterBoreTemplate("two_point_sketch_points_ext.SLDPRT", "Hole_TwoPoint_sketch_points_ext", 16125, 21, "ca7ffa3462a33df8e84ff0fc153083d8948f303528dbe77f66b2bba9efd0dddf", 30586, HoleWizardAuthoringSegments.BlindTwoPoint2, 1, 0.01, 2, 0.015, 0.015, 0.005, 0.005);
    internal static readonly HoleWizardMeasuredCounterBoreTemplate[] All = { Source };
    internal static HoleWizardMeasuredCounterBoreTemplate? FindByBase(byte[] stream)
    {
        if (stream == null) return null;
        byte[] norm = (byte[])stream.Clone(); if (norm.Length >= 6) { norm[4] = 0; norm[5] = 0; }
        string sha; using (var h = System.Security.Cryptography.SHA256.Create()) sha = BitConverter.ToString(h.ComputeHash(norm)).Replace("-", "").ToLowerInvariant();
        return Array.Find(All, t => t.BaseLength == stream.Length && t.BaseSha256 == sha);
    }
}

internal static class HoleWizardCounterBoreAuthoringTemplates
{
    internal const string FeatureName = "Hole_cb_iso_socketcap_m6";
    internal const uint FolderTypeId = 0x52u;
    internal const ushort StreamVersion = 0x71;
    internal const int BaseLength = 12266;
    internal const ushort StrippedFolderCount = 20;
    internal const int BlockLength = 30212;
    internal const string BaseSha256 =
        "2f4348e193e3f0ab4a8cd333fec62e67798b44ecfcde0c14cbaff3cd67065b74";

    internal static readonly byte[] Block = HoleWizardAuthoringSegments.CounterBore0;
}

internal static class HoleWizardCounterBoreAnsiAuthoringTemplates
{
    internal const string FeatureName = "Hole_cb_ansi_metric_socket_m6";
    internal const uint FolderTypeId = 0x52u;
    internal const ushort StreamVersion = 0x71;
    internal const ushort StrippedFolderCount = 20;
    internal const int BlockLength = 30252;
    internal const string Standard = "ANSI Metric";
    internal const int Standard2 = 1;
    internal const string FastenerType = "凹头盖螺钉 - ANSI B18.3.1M";
    internal const int FastenerType2 = 33;

    internal static readonly byte[] SourceBlock = HoleWizardAuthoringSegments.CounterBoreAnsi0;
    internal static readonly byte[] ResaveBlock = HoleWizardAuthoringSegments.CounterBoreAnsi1;

    internal static readonly (int Length, string Sha256, byte[] Block) Source =
        (12266, "9264a51783f851bdd813dd55f5cd75d5281ff28b3c5f2c060e4ce7ab721ae1c4", SourceBlock);
    internal static readonly (int Length, string Sha256, byte[] Block) Resave =
        (12574, "ca481e88c2c05126a1f8030c234f39d3c699a7564ec1ee8c039852498d0e417a", ResaveBlock);
}

#endregion

#region Measured CounterSink Templates

internal sealed class HoleWizardCounterSinkAuthoringTemplate
{
    internal HoleWizardCounterSinkAuthoringTemplate(
        string featureName, string fastenerSize, double thruHoleDiameter,
        double counterSinkDiameter, int baseLength, string baseSha256, byte[] block,
        double expectedVolume, double expectedSurfaceArea)
    {
        FeatureName = featureName;
        FastenerSize = fastenerSize;
        ThruHoleDiameter = thruHoleDiameter;
        CounterSinkDiameter = counterSinkDiameter;
        BaseLength = baseLength;
        BaseSha256 = baseSha256;
        Block = block;
        ExpectedVolume = expectedVolume;
        ExpectedSurfaceArea = expectedSurfaceArea;
    }

    internal string FeatureName { get; }
    internal string FastenerSize { get; }
    internal double ThruHoleDiameter { get; }
    internal double CounterSinkDiameter { get; }
    internal int BaseLength { get; }
    internal string BaseSha256 { get; }
    internal byte[] Block { get; }
    internal double ExpectedVolume { get; }
    internal double ExpectedSurfaceArea { get; }
}

internal static class HoleWizardCounterSinkAuthoringTemplates
{
    internal const string FastenerType = "锥孔平头 ISO 7046-1";
    internal const double CounterSinkAngle = Math.PI / 2.0;
    internal const uint FolderTypeId = 0x52u;
    internal const ushort StreamVersion = 0x71;
    internal const ushort StrippedFolderCount = 20;
    internal const int BlockLength = 34055;

    internal static readonly HoleWizardCounterSinkAuthoringTemplate M6 = new HoleWizardCounterSinkAuthoringTemplate(
        "Hole_cs_iso_flathead_m6", "M6", 0.006, 0.011, 12266,
        "e86941ebdbf570df17bec767f6bba88ee170f86cbe8d2c8b16d0a89927e8907e",
        HoleWizardAuthoringSegments.CounterSink0, 3.6419893371846652e-6, 0.0017124754201940065);

    internal static readonly HoleWizardCounterSinkAuthoringTemplate M6Resave = new HoleWizardCounterSinkAuthoringTemplate(
        "Hole_cs_iso_flathead_m6", "M6", 0.006, 0.011, 12546,
        "4ef91ad23619defd1dea1d19929035b530e0c538c16ed5bbfd04bf970d59f83f",
        HoleWizardAuthoringSegments.CounterSink1, 3.6419893371846652e-6, 0.0017124754201940065);

    internal static readonly HoleWizardCounterSinkAuthoringTemplate M8 = new HoleWizardCounterSinkAuthoringTemplate(
        "Hole_cs_iso_flathead_m8", "M8", 0.008, 0.015, 12268,
        "1df9def0e62fb57aecf9b9553bc2c224c7568384ec5317517aaa3beabc9658af",
        HoleWizardAuthoringSegments.CounterSink2, 3.2985085403921812e-6, 0.0017152087870256807);

    internal static readonly HoleWizardCounterSinkAuthoringTemplate M8Resave = new HoleWizardCounterSinkAuthoringTemplate(
        "Hole_cs_iso_flathead_m8", "M8", 0.008, 0.015, 12546,
        "bd4f4a5cb13b2b866946defc7902d3b7f38e509b9f99a07584921fb00c4ee835",
        HoleWizardAuthoringSegments.CounterSink3, 3.2985085403921812e-6, 0.0017152087870256807);

    internal static readonly HoleWizardCounterSinkAuthoringTemplate[] All =
    {
        M6, M6Resave, M8, M8Resave
    };

    internal static HoleWizardCounterSinkAuthoringTemplate? SelectByBase(
        string fastenerSize, double thruHoleDiameter, int baseLength, string baseSha256)
    {
        foreach (var t in All)
        {
            if (t.FastenerSize == fastenerSize &&
                Math.Abs(t.ThruHoleDiameter - thruHoleDiameter) <= 1e-12 &&
                t.BaseLength == baseLength &&
                string.Equals(t.BaseSha256, baseSha256, StringComparison.Ordinal))
                return t;
        }
        return null;
    }

    internal static HoleWizardCounterSinkAuthoringTemplate? Select(
        string fastenerSize, double thruHoleDiameter, double counterSinkDiameter,
        int baseLength, string baseSha256)
    {
        foreach (var t in All)
        {
            if (t.FastenerSize == fastenerSize &&
                Math.Abs(t.ThruHoleDiameter - thruHoleDiameter) <= 1e-12 &&
                Math.Abs(t.CounterSinkDiameter - counterSinkDiameter) <= 1e-12 &&
                t.BaseLength == baseLength &&
                string.Equals(t.BaseSha256, baseSha256, StringComparison.Ordinal))
                return t;
        }
        return null;
    }

    internal static string? FeatureNameFor(string fastenerSize, double counterSinkDiameter) =>
        fastenerSize == "M6" && HoleWizardSegments.IsAllowedCounterSinkDiameter("M6", counterSinkDiameter)
            ? M6.FeatureName
            : fastenerSize == "M8" && HoleWizardSegments.IsAllowedCounterSinkDiameter("M8", counterSinkDiameter)
                ? M8.FeatureName
                : null;
}

#endregion

#region Measured Tapped, UpToNext & Simple Templates

internal static class HoleWizardTappedAuthoringTemplates
{
    internal const ushort StreamVersion = 0x84;
    internal const uint FolderTypeId = 0x52u;
    internal const ushort ExpectedStrippedFolderCount = 20;
    internal const int HeaderRestLength = 505;
    internal const int EndCondition = 1;
    internal const int TypeValue = 48;
    internal const double MeasuredThreadDiameter = 0.006;

    internal const string BaseSha256 =
        "0a0f85d6ed608cbf8c1c89b6ebc2496e34ec1c12336978cb5b8bdc570df825a6";

    internal static readonly int[] ThreadDiameterTailOffsets = { 33838, 34136, 34370, 34394 };
    internal const int ThreadDiameterDisplayCopyTailOffset = 31026;
    internal static readonly int[] ThreadDepthTailOffsets = { 31062, 32102, 32283 };

    internal static readonly byte[] HeaderRest = HoleWizardAuthoringSegments.TappedHeaderRest;
    internal static readonly byte[] Tail = HoleWizardAuthoringSegments.TappedTail;
}

internal static class HoleWizardUpToNextAuthoringTemplates
{
    internal const ushort StreamVersion = 0x71;
    internal const uint FolderTypeId = 0x54u;
    internal const ushort ExpectedStrippedFolderCount = 20;
    internal const int HeaderRestLength = 505;
    internal const int EndCondition = 11;

    internal const string BaseSha256 =
        "9714924e14b7fab57b39ad09946d879272c86a9c47770980ae47d04202c71e10";

    internal const int DiameterTailOffset = 3071;
    internal const int DiameterCopyTailOffset = 5055;

    internal static readonly byte[] HeaderRest = HoleWizardAuthoringSegments.UpToNextHeaderRest;
    internal static readonly byte[] Tail = HoleWizardAuthoringSegments.UpToNextTail;
}

internal static class HoleWizardAuthoringTemplates
{
    internal const ushort StreamVersion = 0x71;
    internal const uint LegacyTypeId = 0x54u;
    internal const ushort ExpectedStrippedFolderCount = 20;
    internal const int HeaderRestLength = 505;
    internal const int ThroughAllEndCondition = 1;
    internal const int BlindEndCondition = 0;
    internal const double TemplateDepth = 0.010;
    internal const int DiameterTailOffset = 3071;
    internal const int DiameterCopyTailOffset = 5055;

    internal const string ThroughAllBaseSha256 =
        "d1134369631e102f04d077f25562a83d40d5cc8ac13871a995b433b5a48fd592";
    internal const string BlindBaseSha256 =
        "4283c22e82cfefdc50b20005d855d798e0225d7ad188a80d46ba16a9bcb2604b";

    internal static readonly byte[] ThroughAllHeaderRest = HoleWizardAuthoringSegments.ThroughAllHeaderRest;
    internal static readonly byte[] ThroughAllTail = HoleWizardAuthoringSegments.ThroughAllTail;
    internal static readonly byte[] BlindHeaderRest = HoleWizardAuthoringSegments.BlindHeaderRest;
    internal static readonly byte[] BlindTail = HoleWizardAuthoringSegments.BlindTail;
}

#endregion

#region Measured Legacy & Tutorial Templates

internal sealed class LegacyHoleWizardAuthoringTemplate
{
    internal LegacyHoleWizardAuthoringTemplate(
        string sample, string featureName, uint folderTypeId, ushort streamVersion, int baseLength,
        ushort strippedFolderCount, string baseSha256, int markerOffset, uint markerValue,
        int blockLength, byte[] block, int type, int endCondition, string? standard,
        string? fastenerType, string? fastenerSize, double thruHoleDiameter, double thruHoleDepth,
        double counterBoreDiameter, double counterBoreDepth, double counterSinkDiameter,
        double counterSinkAngle, double threadDiameter, double threadDepth)
    {
        Sample = sample; FeatureName = featureName; FolderTypeId = folderTypeId; StreamVersion = streamVersion;
        BaseLength = baseLength; StrippedFolderCount = strippedFolderCount; BaseSha256 = baseSha256;
        MarkerOffset = markerOffset; MarkerValue = markerValue; BlockLength = blockLength;
        Block = block;
        Type = type; EndCondition = endCondition; Standard = standard; FastenerType = fastenerType; FastenerSize = fastenerSize;
        ThruHoleDiameter = thruHoleDiameter; ThruHoleDepth = thruHoleDepth; CounterBoreDiameter = counterBoreDiameter; CounterBoreDepth = counterBoreDepth;
        CounterSinkDiameter = counterSinkDiameter; CounterSinkAngle = counterSinkAngle; ThreadDiameter = threadDiameter; ThreadDepth = threadDepth;
    }
    internal string Sample { get; }
    internal string FeatureName { get; }
    internal uint FolderTypeId { get; }
    internal ushort StreamVersion { get; }
    internal int BaseLength { get; }
    internal ushort StrippedFolderCount { get; }
    internal string BaseSha256 { get; }
    internal int MarkerOffset { get; }
    internal double MarkerValue { get; }
    internal int BlockLength { get; }
    internal byte[] Block { get; }
    internal int Type { get; }
    internal int EndCondition { get; }
    internal string? Standard { get; }
    internal string? FastenerType { get; }
    internal string? FastenerSize { get; }
    internal double ThruHoleDiameter { get; }
    internal double ThruHoleDepth { get; }
    internal double CounterBoreDiameter { get; }
    internal double CounterBoreDepth { get; }
    internal double CounterSinkDiameter { get; }
    internal double CounterSinkAngle { get; }
    internal double ThreadDiameter { get; }
    internal double ThreadDepth { get; }
}

internal static class LegacyHoleWizardAuthoringTemplates
{
    internal static readonly LegacyHoleWizardAuthoringTemplate[] All =
    {
        new LegacyHoleWizardAuthoringTemplate("11_box.sldprt", "CBORE for #6 Binding Head Machine Screw1", 0x23u, 0x6F, 15036, 20, "05de08e432918a53fd913aab6eca5bb7dd0da90682f7dd27566b631347c50b50", -1, 0u, 34107,
            HoleWizardAuthoringSegments.Legacy0, 18, 1, "Ansi Inch", "Binding Head Screw", "#6", 0.0037972999999999995d, 0.03d, 0.0079375d, 0.0022097999999999996d, 0d, 0d, 0d, 0d),
        new LegacyHoleWizardAuthoringTemplate("12_clamp2.sldprt", "CBORE for #00 Binding Head Machine Screw1", 0x158u, 0x7E, 236616, 46, "943a5b375ddd62f0e2ef93036de6fa3587d3308c898bc63d9791cf02d34ccbb8", 52406, 3u, 14671,
            HoleWizardAuthoringSegments.Legacy1, 14, 1, "ANSI Inch", "Binding Head Screw", "#00", 0.001397d, 0.029717284292134417d, 0.003175d, 0.0007111999999999999d, 0d, 0d, 0d, 0d),
        new LegacyHoleWizardAuthoringTemplate("08_die_insert.sldprt", "Drill&CBore For 1/4 SHCS (Shallow)1", 0x3Cu, 0x95, 27496, 21, "0d0264001d7ef79f511fcda6fc2fc45e4e974487868c214b609a891c29081146", -1, 0u, 41456,
            HoleWizardAuthoringSegments.Legacy2, 14, 1, "MT&D Inch Mounting", "Drill&Cbore For SHCS (Shallow)", "1/4", 0.0071374d, 0.01905d, 0.010312400000000001d, 0.007112d, 0d, 0d, 0d, 0d)
    };
    internal static LegacyHoleWizardAuthoringTemplate? Find(uint folderTypeId) =>
        Array.Find(All, t => t.FolderTypeId == folderTypeId);
}

internal sealed class LegacyHoleWizardField
{
    internal LegacyHoleWizardField(
        string property, string className, int classTagOffset, int offset,
        bool isString, string? expectedString, double expectedDouble)
    {
        Property = property; ClassName = className; ClassTagOffset = classTagOffset; Offset = offset; IsString = isString;
        ExpectedString = expectedString; ExpectedDouble = expectedDouble;
    }
    internal string Property { get; }
    internal string ClassName { get; }
    internal int ClassTagOffset { get; }
    internal int Offset { get; }
    internal bool IsString { get; }
    internal string? ExpectedString { get; }
    internal double ExpectedDouble { get; }
}

internal sealed class LegacyHoleWizardFamily
{
    internal LegacyHoleWizardFamily(
        ushort version, ushort folderCount, uint headerTypeId, int restLength,
        int type, int endCondition, LegacyHoleWizardField[] fields)
    {
        Version = version; FolderCount = folderCount; HeaderTypeId = headerTypeId; RestLength = restLength;
        Type = type; EndCondition = endCondition; Fields = fields;
    }
    internal ushort Version { get; }
    internal ushort FolderCount { get; }
    internal uint HeaderTypeId { get; }
    internal int RestLength { get; }
    internal int Type { get; }
    internal int EndCondition { get; }
    internal LegacyHoleWizardField[] Fields { get; }
}

internal static class LegacyHoleWizardTutorialTemplates
{
    internal static readonly LegacyHoleWizardFamily[] Families =
    {
        new LegacyHoleWizardFamily((ushort)0x3CE, (ushort)52, 0xE2u, 4248, 0, 0, new LegacyHoleWizardField[0]),
        new LegacyHoleWizardFamily((ushort)0x136, (ushort)61, 0x17u, 882, 0, 1, new LegacyHoleWizardField[0]),
        new LegacyHoleWizardFamily((ushort)0x1CC, (ushort)55, 0x112u, 7476, 0, 0, new LegacyHoleWizardField[0]),
        new LegacyHoleWizardFamily((ushort)0xBD, (ushort)40, 0x4Du, 1398, 46, 0, new[]
        {
            new LegacyHoleWizardField("Standard", "moCylinderRef_w", 33951, 2525, true, "ANSI Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moCylinderRef_w", 33951, 2547, true, "Straight Pipe Tapped hole", 0),
            new LegacyHoleWizardField("FastenerSize", "moCylinderRef_w", 33951, 2601, true, "3/4-14 NPSM", 0),
            new LegacyHoleWizardField("ThreadDiameter", "ThreeDdiameterDim_c", 32804, 38, false, null, 0.026263599999999998d),
            new LegacyHoleWizardField("ThreadDepth", "moCThreadData_c", 29995, 41, false, null, 0.007619999999999998d),
        }),
        new LegacyHoleWizardFamily((ushort)0x20D, (ushort)44, 0x69u, 8, 25, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 33724, 997, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 33724, 1019, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 33724, 1055, true, "1/2", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0x2F0, (ushort)37, 0x69u, 8, 22, 4, new[]
        {
            new LegacyHoleWizardField("Standard", "moSingleFaceRef_w", 32953, 446, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moSingleFaceRef_w", 32953, 468, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moSingleFaceRef_w", 32953, 504, true, "1/2", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0xF1, (ushort)47, 0x21u, 8, 48, 2, new LegacyHoleWizardField[0]),
        new LegacyHoleWizardFamily((ushort)0x95, (ushort)22, 0x3Cu, 1666, 14, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 40553, 447, true, "MT&D Inch Mounting", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 40553, 487, true, "Drill&Cbore For SHCS (Shallow)", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 40553, 551, true, "1/4", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 19242, 10144, false, null, 0.0071374d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 19242, 10596, false, null, 0.01905d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 19242, 11040, false, null, 0.010312400000000001d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 19242, 11486, false, null, 0.007112d),
        }),
        new LegacyHoleWizardFamily((ushort)0x1AA, (ushort)50, 0xC8u, 3536, 25, 2, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 33796, 1033, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 33796, 1055, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 33796, 1091, true, "#6", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "sgAnglDim", 11336, 15114, false, null, 0.0037972999999999995d),
            new LegacyHoleWizardField("ThruHoleDepth", "sgAnglDim", 11336, 15566, false, null, 0.001290320000000001d),
        }),
        new LegacyHoleWizardFamily((ushort)0x72, (ushort)31, 0x13u, 1119, 0, 0, new LegacyHoleWizardField[0]),
        new LegacyHoleWizardFamily((ushort)0x6F, (ushort)21, 0x23u, 513, 18, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 33250, 447, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 33250, 469, true, "Binding Head Screw", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 33250, 509, true, "#6", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 8389, 10144, false, null, 0.0037972999999999995d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 8389, 10596, false, null, 0.03d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 8389, 11040, false, null, 0.0079375d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 8389, 11486, false, null, 0.0022097999999999996d),
        }),
        new LegacyHoleWizardFamily((ushort)0x7E, (ushort)47, 0x158u, 388, 14, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 13234, 1021, true, "ANSI Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 13234, 1043, true, "Binding Head Screw", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 13234, 1083, true, "#00", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0x6A, (ushort)24, 0xACu, 1026, 44, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 36591, 1005, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 36591, 1031, true, "Flat Head Screw - ANSI B18.6.7M", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 36591, 1097, true, "M3.5", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "sgAnglDim", 12121, 13410, false, null, 0.0039d),
            new LegacyHoleWizardField("ThruHoleDepth", "sgAnglDim", 12121, 13862, false, null, 0.005000000000000003d),
            new LegacyHoleWizardField("CounterSinkDiameter", "sgAnglDim", 12121, 14316, false, null, 0.008199999999999999d),
            new LegacyHoleWizardField("CounterSinkAngle", "sgAnglDim", 12121, 14772, false, null, 1.5707963267948966d),
        }),
        new LegacyHoleWizardFamily((ushort)0x6F, (ushort)24, 0xACu, 1026, 22, 0, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 31729, 985, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 31729, 1011, true, "Drill sizes", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 31729, 1037, true, "Ø7.0", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0xA0, (ushort)25, 0x38u, 524, 48, 1, new[]
        {
            new LegacyHoleWizardField("ThreadDiameter", "ThreeDdiameterDim_c", 38179, 38, false, null, 0.01d),
            new LegacyHoleWizardField("ThreadDepth", "sgAnglDim", 13734, 15586, false, null, 0.01905d),
        }),
        new LegacyHoleWizardFamily((ushort)0x101, (ushort)72, 0x6Au, 2554, 25, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 35809, 993, true, "ANSI Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 35809, 1015, true, "All Drill sizes", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 35809, 1049, true, "15/64", 0),
        }),
        new LegacyHoleWizardFamily((ushort)0x88, (ushort)27, 0x41u, 8, 14, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 39026, 1005, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 39026, 1031, true, "Hex Bolt - ANSI B18.2.3.5M", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 39026, 1087, true, "M5", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 18395, 10144, false, null, 0.0055d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 18395, 10596, false, null, 0.028d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 18395, 11040, false, null, 0.009000000000000001d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 18395, 11486, false, null, 0.006d),
        }),
        new LegacyHoleWizardFamily((ushort)0x92, (ushort)33, 0x66u, 8, 46, 0, new[]
        {
            new LegacyHoleWizardField("ThreadDiameter", "ThreeDdiameterDim_c", 42088, 38, false, null, 0.008d),
            new LegacyHoleWizardField("ThreadDepth", "moCThreadData_c", 39084, 21, false, null, 0.0159999934d),
        }),
        new LegacyHoleWizardFamily((ushort)0x67, (ushort)23, 0x2Au, 8, 25, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 34486, 476, true, "Ansi Inch", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 34486, 498, true, "Screw Clearances", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 34486, 534, true, "#12", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "sgAnglDim", 11457, 15114, false, null, 0.0057912d),
            new LegacyHoleWizardField("ThruHoleDepth", "sgAnglDim", 11457, 15566, false, null, 0.05436868468130723d),
        }),
        new LegacyHoleWizardFamily((ushort)0x71, (ushort)31, 0x5Au, 2091, 14, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moFixedRefPlnData_c", 34154, 997, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moFixedRefPlnData_c", 34154, 1023, true, "Socket Head Cap Screw", 0),
            new LegacyHoleWizardField("FastenerSize", "moFixedRefPlnData_c", 34154, 1069, true, "M10", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 13491, 10144, false, null, 0.011d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 13491, 10596, false, null, 0.01905d),
            new LegacyHoleWizardField("CounterBoreDiameter", "moAngleParameter_c", 13491, 11040, false, null, 0.018000000000000002d),
            new LegacyHoleWizardField("CounterBoreDepth", "moAngleParameter_c", 13491, 11486, false, null, 0.01d),
        }),
        new LegacyHoleWizardFamily((ushort)0x68, (ushort)26, 0x34u, 490, 25, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 31893, 447, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 31893, 473, true, "Tap Drills", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 31893, 497, true, "M10x1.0", 0),
            new LegacyHoleWizardField("ThruHoleDiameter", "moAngleParameter_c", 8845, 15108, false, null, 0.009000000000000001d),
            new LegacyHoleWizardField("ThruHoleDepth", "moAngleParameter_c", 8845, 15560, false, null, 0.04445000000000001d),
        }),
        new LegacyHoleWizardFamily((ushort)0x6E, (ushort)23, 0x6Du, 1992, 52, 1, new[]
        {
            new LegacyHoleWizardField("Standard", "moCompSolidBody_c", 39259, 463, true, "Ansi Metric", 0),
            new LegacyHoleWizardField("FastenerType", "moCompSolidBody_c", 39259, 489, true, "Tapped hole", 0),
            new LegacyHoleWizardField("FastenerSize", "moCompSolidBody_c", 39259, 515, true, "M10x1.25", 0),
            new LegacyHoleWizardField("ThreadDiameter", "moAngleParameter_c", 16203, 15114, false, null, 0.01d),
            new LegacyHoleWizardField("ThreadDepth", "moAngleParameter_c", 16203, 15576, false, null, 0.012000000000000009d),
        })
    };
}

#endregion

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/HoleWizardFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled Hole Wizard authoring on the measured extrusion-stripped bases:
/// - legacy simple hole (moHoleWzd_c / TypeId 0x54 / stream 0x71): through-all or the
///   measured blind template, only Diameter parameterized;
/// - tapped M6 hole (moHoleWzd_c / TypeId 0x52 / stream 0x84): ThreadDiameter and
///   ThreadDepth are fixed at the measured 0.006 / 0.010 m; null is accepted and uses
///   those values. Other diameters/depths are rejected because native verification showed
///   they do not change the rebuilt geometry/volume. The measured ISO/M6 flags, Type 48,
///   EndCondition 1 and face/sketch-point context stay template-measured.
/// All other fields and the face/sketch-point context stay template-measured.
/// </summary>
// (HoleWizard builders extracted to HoleWizardFeatureBuilder.cs)
