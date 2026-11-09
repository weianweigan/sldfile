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


public enum PatternNativeMode
{
    Linear,
    Circular
}

/// <summary>
/// Strongly-typed MFC feature model for moLPattern_c (linear pattern).
/// Implements IMfcSerializable for symmetric reading and writing.
/// </summary>
public sealed class LinearPatternFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moLPattern_c";
    public const uint HeaderTypeId = 0x5Du;
    public const ushort StreamVersion = 0x72;

    public string ClassName => MfcClassName;
    public uint TypeId => HeaderTypeId;
    public string FeatureName { get; set; } = "线性阵列1";
    public double Version { get; set; } = 2.0;

    public int? D1TotalInstances { get; set; }
    public double? D1Spacing { get; set; }
    public int? D2TotalInstances { get; set; }
    public double? D2Spacing { get; set; }

    public FeatureReferenceData? D1Axis { get; set; }
    public FeatureReferenceData? D2Axis { get; set; }
    public int? D1AxisType { get; set; }
    public int? D2AxisType { get; set; }
    public bool? D1ReverseDirection { get; set; }
    public bool? D2ReverseDirection { get; set; }
    public bool? VarySketch { get; set; }
    public bool? GeometryPattern { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFeatureArray { get; set; }
    public IReadOnlyList<uint>? SkippedItemArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFaceArray { get; set; }
    public bool? D2PatternSeedOnly { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternBodyArray { get; set; }
    public bool? PropagateVisualProperty { get; set; }
    public int? PatternElement { get; set; }
    public bool? BodyPattern { get; set; }

    public int? D1EndCondition { get; set; }
    public FeatureReferenceData? D1EndReference { get; set; }
    public double? D1EndRefOffset { get; set; }
    public bool? D1EndRefReverseOffset { get; set; }
    public bool? D1EndUseSeedReference { get; set; }
    public FeatureReferenceData? D1EndSeedReference { get; set; }
    public bool? D1EndUseSpacing { get; set; }

    public int? D2EndCondition { get; set; }
    public FeatureReferenceData? D2EndReference { get; set; }
    public double? D2EndRefOffset { get; set; }
    public bool? D2EndRefReverseOffset { get; set; }
    public bool? D2EndUseSeedReference { get; set; }
    public FeatureReferenceData? D2EndSeedReference { get; set; }
    public bool? D2EndUseSpacing { get; set; }

    public bool? FeatureScope { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }
    public bool? AutoSelect { get; set; }
    public bool? InstancesToVary { get; set; }
    public IReadOnlyList<FeatureReferenceData>? StructureSystemToPatternArray { get; set; }

    public int? GetD1AxisType() => D1AxisType;
    public int? GetD2AxisType() => D2AxisType;
    public int? GetPatternFeatureCount() => PatternFeatureArray?.Count;
    public int? GetSkippedItemCount() => SkippedItemArray?.Count;
    public int? GetPatternFaceCount() => PatternFaceArray?.Count;
    public int? GetPatternBodyCount() => PatternBodyArray?.Count;
    public bool IsDirection2Specified() =>
        (D2Axis != null) || (D2TotalInstances.HasValue) || (D2Spacing.HasValue);

    public void SetFeatureScope(bool featureScope, bool autoSelectBodies,
        IReadOnlyList<FeatureReferenceData>? bodies)
    {
        FeatureScope = featureScope;
        AutoSelect = autoSelectBodies;
        FeatureScopeBodies = bodies;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = TypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            uint c1 = (uint)(D1TotalInstances ?? 2);
            ar.U32(ref c1);
            double s1 = D1Spacing ?? 0.050;
            ar.F64(ref s1);

            uint c2 = (uint)(D2TotalInstances ?? 1);
            ar.U32(ref c2);
            double s2 = D2Spacing ?? 0.050;
            ar.F64(ref s2);
        }
        else
        {
            ar.ReadClassTag(out _, out _, out _);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) FeatureName = name!;

            double v = 0;
            ar.F64(ref v);
            Version = v;

            uint tid = 0;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 4)
            {
                uint c1 = 0;
                ar.U32(ref c1);
                D1TotalInstances = (int)c1;
            }
            if (ar.Remaining >= 8)
            {
                double s1 = 0;
                ar.F64(ref s1);
                D1Spacing = s1;
            }
            if (ar.Remaining >= 4)
            {
                uint c2 = 0;
                ar.U32(ref c2);
                D2TotalInstances = (int)c2;
            }
            if (ar.Remaining >= 8)
            {
                double s2 = 0;
                ar.F64(ref s2);
                D2Spacing = s2;
            }
        }
    }
}

/// <summary>
/// Strongly-typed MFC feature model for moCirPattern_c (circular pattern).
/// Implements IMfcSerializable for symmetric reading and writing.
/// </summary>
public sealed class CircularPatternFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moCirPattern_c";
    public const uint HeaderTypeId = 0x63u;
    public const ushort StreamVersion = 0x72;

    public string ClassName => MfcClassName;
    public uint TypeId => HeaderTypeId;
    public string FeatureName { get; set; } = "圆周阵列1";
    public double Version { get; set; } = 2.0;

    public FeatureReferenceData? Axis { get; set; }
    public int? AxisType { get; set; }
    public bool? ReverseDirection { get; set; }
    public double? Spacing { get; set; }
    public int? TotalInstances { get; set; }
    public bool? EqualSpacing { get; set; }
    public bool? VarySketch { get; set; }
    public bool? GeometryPattern { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFeatureArray { get; set; }
    public IReadOnlyList<uint>? SkippedItemArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFaceArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternBodyArray { get; set; }
    public bool? PropagateVisualProperty { get; set; }
    public int? PatternElement { get; set; }
    public bool? BodyPattern { get; set; }
    public bool? Direction2 { get; set; }
    public bool? Symmetric { get; set; }
    public bool? EqualSpacing2 { get; set; }
    public double? Spacing2 { get; set; }
    public int? TotalInstances2 { get; set; }
    public bool? FeatureScope { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }
    public bool? AutoSelect { get; set; }
    public bool? InstancesToVary { get; set; }
    public IReadOnlyList<FeatureReferenceData>? StructureSystemToPatternArray { get; set; }

    public int? GetAxisType() => AxisType;
    public int? GetPatternFeatureCount() => PatternFeatureArray?.Count;
    public int? GetSkippedItemCount() => SkippedItemArray?.Count;
    public int? GetPatternFaceCount() => PatternFaceArray?.Count;
    public int? GetPatternBodyCount() => PatternBodyArray?.Count;

    public void SetFeatureScope(bool featureScope, bool autoSelectBodies,
        IReadOnlyList<FeatureReferenceData>? bodies)
    {
        FeatureScope = featureScope;
        AutoSelect = autoSelectBodies;
        FeatureScopeBodies = bodies;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = TypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            uint inst = (uint)(TotalInstances ?? 4);
            ar.U32(ref inst);
            double sp = Spacing ?? (2.0 * Math.PI);
            ar.F64(ref sp);
            bool eq = EqualSpacing ?? true;
            ar.Boolean(ref eq);
        }
        else
        {
            ar.ReadClassTag(out _, out _, out _);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) FeatureName = name!;

            double v = 0;
            ar.F64(ref v);
            Version = v;

            uint tid = 0;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 4)
            {
                uint inst = 0;
                ar.U32(ref inst);
                TotalInstances = (int)inst;
            }
            if (ar.Remaining >= 8)
            {
                double sp = 0;
                ar.F64(ref sp);
                Spacing = sp;
            }
            if (ar.Remaining >= 1)
            {
                bool eq = true;
                ar.Boolean(ref eq);
                EqualSpacing = eq;
            }
        }
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/PatternSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured offsets and payloads for the controlled PatternOptions layouts (SW2026,
/// ResolvedFeatures stream version 0x72). Linear: 2x3 instances, 10/15 mm spacings.
/// Circular: 4 instances, 90-degree equal spacing. Authoring writes only the measured
/// named-parameter scalars and their display copies; selection/flag metadata is measured.
/// </summary>
internal static class PatternSegments
{
    internal const string LinearPatternClassName = "moLPattern_c";
    internal const uint LinearPatternTypeId = 0x5Du;
    internal const string CircularPatternClassName = "moCirPattern_c";
    internal const uint CircularPatternTypeId = 0x63u;
    internal const string MirrorPatternClassName = "moMirrorPattern_c";
    internal const uint MirrorPatternTypeId = 0x57u;
    internal const ushort StreamVersion = 0x72;

    internal const int LinearIntegerParameterLength = 5815;
    internal const int LinearD1CountParameterOffset = 0;
    internal const int LinearD1SpacingParameterOffset = 1501;
    internal const int LinearD2CountParameterOffset = 3242;
    internal const int LinearD2SpacingParameterOffset = 4743;
    internal const int LinearCompEdgeLength = 4042;
    internal const int LinearD1CountValueSlot = 32;
    internal const int LinearD2CountValueSlot = 3274;
    internal const int LinearD1SpacingValueSlot = 1533;
    internal static readonly int[] LinearD1SpacingCopySlots = { 1581, 1605, 1923, 1947, 2093, 2117, 3009, 4823, 4847 };
    internal const int LinearD2SpacingValueSlot = 4775;
    internal static readonly int[] LinearD2SpacingCopySlots = { 5165, 5189, 5335, 5359 };
    internal const int LinearDisplayDimSpacingSlot = 440;

    internal const int CircularIntegerParameterLength = 826;
    internal const int CircularCountParameterOffset = 0;
    internal const int CircularCompEdgeLength = 3830;
    internal const int CircularSpacingParameterOffset = 1089;
    internal const int CircularAngleParameterLength = 4068;
    internal const int CircularAngleParameterOffset = 0;
    internal const int CircularCountValueSlot = 32;
    internal const int CircularSpacingValueSlot = 1121;
    internal static readonly int[] CircularSpacingCopySlots = { 1659 };
    internal const int CircularAngleValueSlot = 32;

    internal const string LinearBasePrefixSha256 =
        "8c116424714abf4c951e4805aae587d3890d1322c3ded5c3ab1b0d61c052dd31";
    internal const string CircularBasePrefixSha256 =
        "652ab7b79afb4fbb53dcccc984a8f716f4928ec7ff7d1d67a35616eae77e631d";

    /// <summary>moLPattern_c data after schema/WChar name/double 2.0/TypeId 0x5D/u32 0/empty WChar.</summary>
    internal static readonly byte[] LinearHeaderSuffix = PatternAuthoringSegments.LinearHeaderSuffix;

    /// <summary>moDisplayDim_c payload (D1 spacing display dimension; 0.010 at +440).</summary>
    internal static readonly byte[] LinearDisplayDimData = PatternAuthoringSegments.LinearSpacingParameterData;

    /// <summary>moNumberDim_c payload (u32 1).</summary>
    internal static readonly byte[] LinearNumberDimData = new byte[] {
        0x01, 0x00, 0x00, 0x00
    };

    /// <summary>moIntegerParameter_c payload; D1/D3/D2/D4 named parameters and spacing display copies.</summary>
    internal static readonly byte[] LinearIntegerParameterData = PatternAuthoringSegments.LinearCountParameterData;

    /// <summary>moCompEdge_c payload (direction/source reference envelope).</summary>
    internal static readonly byte[] LinearCompEdgeData = PatternAuthoringSegments.LinearCompEdgeData;

    /// <summary>moCirPattern_c data after schema/WChar name/double 2.0/TypeId 0x63/u32 0/empty WChar.</summary>
    internal static readonly byte[] CircularHeaderSuffix = PatternAuthoringSegments.LinearDirection2SpacingData;

    /// <summary>moDisplayDim_c payload.</summary>
    internal static readonly byte[] CircularDisplayDimData = PatternAuthoringSegments.LinearDirection2CountData;

    /// <summary>moNumberDim_c payload (u32 1).</summary>
    internal static readonly byte[] CircularNumberDimData = new byte[] {
        0x01, 0x00, 0x00, 0x00
    };

    /// <summary>moIntegerParameter_c payload (D1 instance count at +32).</summary>
    internal static readonly byte[] CircularIntegerParameterData = PatternAuthoringSegments.LinearDirection2CompEdgeData;

    /// <summary>moDisplayAngularDim_c payload.</summary>
    internal static readonly byte[] CircularDisplayAngularDimData = PatternAuthoringSegments.CircularHeaderSuffix;

    /// <summary>AngleDim_c payload (u16 2 + padding).</summary>
    internal static readonly byte[] CircularAngleDimData = new byte[] {
        0x01, 0x00, 0x00, 0x00
    };

    /// <summary>moAngleParameter_c payload; D2 per-instance angle at +32.</summary>
    internal static readonly byte[] CircularAngleParameterData = PatternAuthoringSegments.CircularAngleParameterData;

    /// <summary>moCompEdge_c payload; D3 spacing at +1121 and a display copy at +1659.</summary>
    internal static readonly byte[] CircularCompEdgeData = PatternAuthoringSegments.CircularCompEdgeData;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/PatternNativeAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// One measured native linear/circular pattern template. The complete feature block is carried
/// verbatim; its base signature identifies the exact native document lineage whose other OLE
/// streams (DisplayLists, Config-0-Partition, caches) already contain the matching instance
/// geometry. Authoring therefore appends the native block to the corresponding stripped base
/// instead of regenerating instance caches that live outside ResolvedFeatures.
/// </summary>
internal sealed class PatternNativeAuthoringTemplate
{
    internal PatternNativeAuthoringTemplate(
        PatternNativeMode mode, string sampleName, string className, string featureName,
        int baseLength, string baseSha256, int strippedFolderCount, ushort streamVersion,
        byte[] block, int? d1Count, double? d1Spacing, int? d2Count, double? d2Spacing,
        int? totalInstances, double? spacing, bool? equalSpacing)
    {
        Mode = mode;
        SampleName = sampleName;
        ClassName = className;
        FeatureName = featureName;
        BaseLength = baseLength;
        BaseSha256 = baseSha256;
        StrippedFolderCount = strippedFolderCount;
        StreamVersion = streamVersion;
        Block = block;
        D1Count = d1Count;
        D1Spacing = d1Spacing;
        D2Count = d2Count;
        D2Spacing = d2Spacing;
        TotalInstances = totalInstances;
        Spacing = spacing;
        EqualSpacing = equalSpacing;
    }

    internal PatternNativeMode Mode { get; }
    internal string SampleName { get; }
    internal string ClassName { get; }
    internal string FeatureName { get; }
    internal int BaseLength { get; }
    internal string BaseSha256 { get; }
    internal int StrippedFolderCount { get; }
    internal ushort StreamVersion { get; }
    internal byte[] Block { get; }
    internal int? D1Count { get; }
    internal double? D1Spacing { get; }
    internal int? D2Count { get; }
    internal double? D2Spacing { get; }
    internal int? TotalInstances { get; }
    internal double? Spacing { get; }
    internal bool? EqualSpacing { get; }
}

internal static class PatternNativeAuthoringTemplates
{
    internal const int StrippedFolderCount = 22;
    internal const ushort StreamVersion = 0x72;

    internal static readonly PatternNativeAuthoringTemplate Linear2x3_10_15 = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Linear, "lpattern_native_2dir_2x3_10_15", "moLPattern_c", "LinearPattern",
        19541, "42e0fd2bc8efde24794cd704bf6414185cccbf0426a848bcb25eb6d85a07aad7", 22, 0x72,
        PatternAuthoringSegments.Native0, 2, 0.010, 3, 0.015, null, null, null);

    internal static readonly PatternNativeAuthoringTemplate Linear1dir3x15 = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Linear, "lpattern_1dir_3x15", "moLPattern_c", "LinearPattern",
        19541, "5b187d10d47f31cdde00206f0eec50d62474d9a2cc893065da04b33e0e089d80", 22, 0x72,
        PatternAuthoringSegments.Native1, 3, 0.015, 1, 0.015, null, null, null);

    internal static readonly PatternNativeAuthoringTemplate Linear2x3_20_25 = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Linear, "lpattern_2dir_2x3_20_25", "moLPattern_c", "LinearPattern",
        19541, "4eaa0788ebcfb743e029e8348b88712f58cc922cf17aff658b93be3f517f99a5", 22, 0x72,
        PatternAuthoringSegments.Native2, 2, 0.020, 3, 0.025, null, null, null);

    internal static readonly PatternNativeAuthoringTemplate LinearGeometryOn = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Linear, "lpattern_geometry_on", "moLPattern_c", "LinearPattern",
        19541, "48c9ea5238debe5f3a3eccc1386fe1cbd7f2e6c8555e5c265a3bb29f6e27aea7", 22, 0x72,
        PatternAuthoringSegments.Native3, 2, 0.010, 3, 0.015, null, null, null);

    internal static readonly PatternNativeAuthoringTemplate LinearGeometryOff = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Linear, "lpattern_geometry_off", "moLPattern_c", "LinearPattern",
        19541, "b2a4be47f8c56384d42ad6475589675d4c1e5389773d6022b1d1dcc72decf7b2", 22, 0x72,
        PatternAuthoringSegments.Native4, 2, 0.010, 3, 0.015, null, null, null);

    internal static readonly PatternNativeAuthoringTemplate Circular4x90 = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Circular, "cirpattern_native_4x90", "moCirPattern_c", "CircularPattern",
        19541, "d28b1a4d189284dbfb72846e3492ddf72ec8f49458524ff3d57f830e1262fa4c", 22, 0x72,
        PatternAuthoringSegments.Native5, null, null, null, null, 4, Math.PI / 2.0, true);

    internal static readonly PatternNativeAuthoringTemplate Circular6x60 = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Circular, "cirpattern_6x60", "moCirPattern_c", "CircularPattern",
        19542, "f0ebaa3c7bf66fd1b5c32d14de71531a9567b948f16a202638555348d957c834", 22, 0x72,
        PatternAuthoringSegments.Native6, null, null, null, null, 6, Math.PI / 3.0, true);

    internal static readonly PatternNativeAuthoringTemplate CircularEqualOn = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Circular, "cirpattern_equal_on", "moCirPattern_c", "CircularPattern",
        19541, "3a7e2950db9b51e64754045b0e57c134b6b539b00b9428cc386cd80df708a58e", 22, 0x72,
        PatternAuthoringSegments.Native7, null, null, null, null, 4, Math.PI / 2.0, true);

    internal static readonly PatternNativeAuthoringTemplate CircularEqualOff = new PatternNativeAuthoringTemplate(
        PatternNativeMode.Circular, "cirpattern_equal_off", "moCirPattern_c", "CircularPattern",
        19541, "b2d716d92ffac6b5636cce37eb3684252afce9f98bc7026df5b19e615d93be5a", 22, 0x72,
        PatternAuthoringSegments.Native8, null, null, null, null, 4, Math.PI / 2.0, false);

    internal static readonly PatternNativeAuthoringTemplate[] All =
    {
        Linear2x3_10_15, Linear1dir3x15, Linear2x3_20_25, LinearGeometryOn, LinearGeometryOff,
        Circular4x90, Circular6x60, CircularEqualOn, CircularEqualOff
    };
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/PatternFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fail-fast builders for the controlled PatternOptions authoring slices:
/// linear (one/two direction) and circular (equal spacing on/off) patterns.
/// Legacy fixed bases use the measured serializer; native variant bases use complete measured
/// blocks so the instance caches in the other OLE streams stay consistent. Requires stream
/// version 0x72, a parsed sketch chain, the 0x67 marker, the exact base signature and the
/// measured parameter set for that base.
/// </summary>
internal static class PatternFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";

    public static byte[] BuildLinearPatternStream(
        byte[] currentStream,
        LinearPatternFeatureModel def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateLinearDefinition(def);
        var native = TryGetNativeTemplate(currentStream, PatternNativeMode.Linear);
        if (native != null)
        {
            ValidateNativeLinearValues(def, native, featureName);
            EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
                native.ClassName, native.BaseSha256, "linear");
            return Append(currentStream, (byte[])native.Block.Clone());
        }

        ValidateLegacyLinearValues(def);
        EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
            PatternSegments.LinearPatternClassName, PatternSegments.LinearBasePrefixSha256, "linear");
        byte[] block = MfcPatternSerializer.SerializeLinearPattern(def, featureName);
        return Append(currentStream, block);
    }

    public static byte[] BuildCircularPatternStream(
        byte[] currentStream,
        CircularPatternFeatureModel def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateCircularDefinition(def);
        var native = TryGetNativeTemplate(currentStream, PatternNativeMode.Circular);
        if (native != null)
        {
            ValidateNativeCircularValues(def, native, featureName);
            EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
                native.ClassName, native.BaseSha256, "circular");
            return Append(currentStream, (byte[])native.Block.Clone());
        }

        ValidateLegacyCircularValues(def);
        EnsureCommon(currentStream, sketchFolder, sketchEndOffset,
            PatternSegments.CircularPatternClassName, PatternSegments.CircularBasePrefixSha256, "circular");
        byte[] block = MfcPatternSerializer.SerializeCircularPattern(def, featureName);
        return Append(currentStream, block);
    }

    /// <summary>
    /// Returns the native linear/circular template whose stripped base signature matches the
    /// current ResolvedFeatures stream, or null for the legacy fixed templates.
    /// </summary>
    internal static PatternNativeAuthoringTemplate? TryGetNativeTemplate(
        byte[] currentStream, PatternNativeMode mode)
    {
        if (currentStream == null || currentStream.Length < 16) return null;
        string sha = NormalizedSha(currentStream);
        return PatternNativeAuthoringTemplates.All.FirstOrDefault(t =>
            t.Mode == mode && t.BaseLength == currentStream.Length &&
            string.Equals(t.BaseSha256, sha, StringComparison.Ordinal));
    }

    internal static void ValidateLinearDefinition(LinearPatternFeatureModel def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));

        ValidateCount(def.D1TotalInstances, nameof(def.D1TotalInstances));
        ValidateCountAllowOne(def.D2TotalInstances, nameof(def.D2TotalInstances));
        ValidateSpacing(def.D1Spacing, nameof(def.D1Spacing));
        ValidateSpacing(def.D2Spacing, nameof(def.D2Spacing));
        ValidateAxisType(def.D1AxisType, nameof(def.D1AxisType));
        ValidateAxisType(def.D2AxisType, nameof(def.D2AxisType));

        foreach (var property in typeof(LinearPatternFeatureModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(LinearPatternFeatureModel.D1TotalInstances):
                case nameof(LinearPatternFeatureModel.D2TotalInstances):
                case nameof(LinearPatternFeatureModel.D1Spacing):
                case nameof(LinearPatternFeatureModel.D2Spacing):
                case nameof(LinearPatternFeatureModel.D1AxisType):
                case nameof(LinearPatternFeatureModel.D2AxisType):
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(MfcFeatureModelBase.ResolvedProperties):
                case "FeatureName":
                case "Version":
                    continue;
                case nameof(LinearPatternFeatureModel.GeometryPattern):
                case nameof(LinearPatternFeatureModel.VarySketch):
                case nameof(LinearPatternFeatureModel.InstancesToVary):
                    if (property.GetValue(def) != null)
                        throw new NotSupportedException(
                            $"Linear pattern authoring requires {property.Name} to be left at the measured " +
                            "template default (null); the template flag value is not structurally decoded. " +
                            "No stream changes have been made.");
                    continue;
            }

            if (property.GetValue(def) != null)
            {
                throw new NotSupportedException(
                    $"Linear pattern authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }
    }

    internal static void ValidateCircularDefinition(CircularPatternFeatureModel def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));

        ValidateCount(def.TotalInstances, nameof(def.TotalInstances));
        if (!def.Spacing.HasValue || double.IsNaN(def.Spacing.Value) || double.IsInfinity(def.Spacing.Value) ||
            def.Spacing.Value <= 0.0 || def.Spacing.Value > 2.0 * Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Spacing),
                $"Circular pattern spacing must be a finite value in (0, 2*pi] radians, but received {def.Spacing}.");
        }
        ValidateAxisType(def.AxisType, nameof(def.AxisType));

        foreach (var property in typeof(CircularPatternFeatureModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(CircularPatternFeatureModel.TotalInstances):
                case nameof(CircularPatternFeatureModel.Spacing):
                case nameof(CircularPatternFeatureModel.AxisType):
                case nameof(CircularPatternFeatureModel.EqualSpacing):
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(MfcFeatureModelBase.ResolvedProperties):
                case "FeatureName":
                case "Version":
                    continue;
                case nameof(CircularPatternFeatureModel.GeometryPattern):
                case nameof(CircularPatternFeatureModel.ReverseDirection):
                case nameof(CircularPatternFeatureModel.Symmetric):
                case nameof(CircularPatternFeatureModel.Direction2):
                case nameof(CircularPatternFeatureModel.InstancesToVary):
                    if (property.GetValue(def) != null)
                        throw new NotSupportedException(
                            $"Circular pattern authoring requires {property.Name} to be left at the measured " +
                            "template default (null); the template flag value is not structurally decoded. " +
                            "No stream changes have been made.");
                    continue;
            }

            if (property.GetValue(def) != null)
            {
                throw new NotSupportedException(
                    $"Circular pattern authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }
    }

    private static void ValidateLegacyLinearValues(LinearPatternFeatureModel def)
    {
        // The legacy serializer regenerates only the ResolvedFeatures block; its other OLE
        // streams are the old 2x3/10/15 specimen, so only that exact parameter set is safe.
        if (def.D1TotalInstances != 2 || def.D2TotalInstances != 3 ||
            Math.Abs(def.D1Spacing!.Value - 0.010) > 1e-12 ||
            Math.Abs(def.D2Spacing!.Value - 0.015) > 1e-12)
        {
            throw new NotSupportedException(
                "Linear pattern authoring on the legacy base is limited to the measured 2x3 " +
                "instances with 10 mm / 15 mm spacings; use the matching native variant base " +
                "for other measured parameter sets. No stream changes have been made.");
        }
    }

    private static void ValidateLegacyCircularValues(CircularPatternFeatureModel def)
    {
        if (def.TotalInstances != 4 || Math.Abs(def.Spacing!.Value - Math.PI / 2.0) > 1e-12)
        {
            throw new NotSupportedException(
                "Circular pattern authoring on the legacy base is limited to the measured 4 " +
                "instances with 90-degree spacing; use the matching native variant base for " +
                "other measured parameter sets. No stream changes have been made.");
        }
        if (def.EqualSpacing == false)
        {
            throw new NotSupportedException(
                "The legacy circular template supports equal spacing only. " +
                "No stream changes have been made.");
        }
    }

    private static void ValidateNativeLinearValues(
        LinearPatternFeatureModel def, PatternNativeAuthoringTemplate template,
        string featureName)
    {
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Native linear-pattern authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        }
        if (def.D1TotalInstances != template.D1Count ||
            def.D2TotalInstances != template.D2Count ||
            Math.Abs(def.D1Spacing!.Value - template.D1Spacing!.Value) > 1e-12 ||
            Math.Abs(def.D2Spacing!.Value - template.D2Spacing!.Value) > 1e-12)
        {
            throw new NotSupportedException(
                $"Linear pattern authoring on native base '{template.SampleName}' is fixed to " +
                $"D1={template.D1Count}/{template.D1Spacing:R} m and D2={template.D2Count}/" +
                $"{template.D2Spacing:R} m. No stream changes have been made.");
        }
    }

    private static void ValidateNativeCircularValues(
        CircularPatternFeatureModel def, PatternNativeAuthoringTemplate template,
        string featureName)
    {
        if (!string.Equals(featureName, template.FeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Native circular-pattern authoring uses the measured feature name " +
                $"'{template.FeatureName}'. No stream changes have been made.");
        }
        if (def.TotalInstances != template.TotalInstances ||
            Math.Abs(def.Spacing!.Value - template.Spacing!.Value) > 1e-12 ||
            (def.EqualSpacing.HasValue && def.EqualSpacing.Value != template.EqualSpacing!.Value))
        {
            throw new NotSupportedException(
                $"Circular pattern authoring on native base '{template.SampleName}' is fixed to " +
                $"{template.TotalInstances} instances, spacing {template.Spacing:R} rad and " +
                $"equalSpacing={template.EqualSpacing}. No stream changes have been made.");
        }
    }

    private static void ValidateCount(int? count, string property)
    {
        if (!count.HasValue || count.Value < 2 || count.Value > 1000)
        {
            throw new ArgumentOutOfRangeException(property,
                $"{property} must be an integer between 2 and 1000, but received {count}.");
        }
    }

    private static void ValidateCountAllowOne(int? count, string property)
    {
        if (!count.HasValue || count.Value < 1 || count.Value > 1000)
        {
            throw new ArgumentOutOfRangeException(property,
                $"{property} must be an integer between 1 and 1000, but received {count}.");
        }
    }

    private static void ValidateSpacing(double? spacing, string property)
    {
        if (!spacing.HasValue || double.IsNaN(spacing.Value) || double.IsInfinity(spacing.Value) ||
            spacing.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(property,
                $"{property} must be a positive finite number, but received {spacing}.");
        }
    }

    private static void ValidateAxisType(int? axisType, string property)
    {
        if (axisType.HasValue && axisType.Value != 1)
        {
            throw new NotSupportedException(
                $"{property} must be 1 (component edge) for the controlled layout, but received {axisType.Value}. " +
                "No stream changes have been made.");
        }
    }

    private static void EnsureCommon(
        byte[] currentStream, FeatureFolder sketchFolder, int sketchEndOffset,
        string patternClassName, string expectedPrefixSha, string modeName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (sketchFolder == null)
            throw new ArgumentNullException(nameof(sketchFolder));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != PatternSegments.StreamVersion)
        {
            throw new NotSupportedException(
                $"Pattern authoring is only supported for ResolvedFeatures streams version " +
                $"0x{PatternSegments.StreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, patternClassName) > 0)
        {
            throw new NotSupportedException(
                $"Pattern authoring currently supports only a stripped base body without an existing " +
                $"{patternClassName} feature. No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) != PatternNativeAuthoringTemplates.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Pattern authoring requires the stripped folder count " +
                $"{PatternNativeAuthoringTemplates.StrippedFolderCount}. No stream changes have been made.");
        }

        RequireSketchChain(currentStream, sketchFolder, sketchEndOffset, version);
        RequireSketchChildCountMarker(currentStream, version);
        RequireControlledBase(currentStream, version, expectedPrefixSha, modeName);
    }

    private static void RequireSketchChain(
        byte[] currentStream, FeatureFolder sketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(sketchFolder.DisplayName)
            ? $"<tag {sketchFolder.TagOffset}>"
            : sketchFolder.DisplayName;
        int end = sketchEndOffset > sketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;
        bool hasChain = MfcPrimitives.ScanMfcTags(currentStream)
            .Any(t => t.ClassName == SketchChainClass &&
                      t.TagOffset >= sketchFolder.TagOffset &&
                      t.TagOffset < end);
        if (!hasChain)
        {
            throw new NotSupportedException(
                $"Cannot author a pattern on the base body of sketch '{sketchName}' " +
                $"(ResolvedFeatures version 0x{version:X2}): no parseable {SketchChainClass} was found. " +
                "No stream changes have been made.");
        }
    }

    private static void RequireSketchChildCountMarker(byte[] currentStream, ushort version)
    {
        for (int i = 0; i <= currentStream.Length - 4; i++)
        {
            if (currentStream[i] == 0x67 && currentStream[i + 1] == 0x00 &&
                currentStream[i + 2] == 0x00 && currentStream[i + 3] == 0x00)
                return;
        }

        throw new NotSupportedException(
            $"Cannot author a pattern on the controlled base body (ResolvedFeatures version 0x{version:X2}): " +
            "the 0x67 sketch child-count marker was not found. No stream changes have been made.");
    }

    private static void RequireControlledBase(
        byte[] currentStream, ushort version, string expectedPrefixSha, string modeName)
    {
        string sha = NormalizedSha(currentStream);
        if (!string.Equals(sha, expectedPrefixSha, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Pattern authoring is limited to the controlled PatternOptions base body for the " +
                $"{modeName} layout (ResolvedFeatures version 0x{version:X2}): base prefix signature " +
                $"{sha.Substring(0, 12)} does not match the measured BaseBox/BossSeed/edge-reference specimen. " +
                "No stream changes have been made.");
        }
    }

    private static string NormalizedSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }
        return Sha256Hex(normalized);
    }

    internal static string? NativeFeatureName(byte[] currentStream, PatternNativeMode mode) =>
        TryGetNativeTemplate(currentStream, mode)?.FeatureName;

    private static byte[] Append(byte[] currentStream, byte[] featureBlock)
    {
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/PatternFeatureParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict parsers for the controlled PatternOptions layouts: linear (one/two directions) and
/// circular (equal spacing on/off), including the native BossSeed variant samples. Counts,
/// spacings and the circular equal-spacing layout are bound; direction and source-feature
/// persistent references are exposed as presence-only edge/feature references when the measured
/// selection envelope is present. Geometry-pattern flags remain unresolved. Mirror patterns use
/// their own measured layout.
/// </summary>
internal static class PatternFeatureParser
{
    internal const string LinearPatternClassName = PatternSegments.LinearPatternClassName;
    internal const uint LinearPatternTypeId = PatternSegments.LinearPatternTypeId;
    internal const string CircularPatternClassName = PatternSegments.CircularPatternClassName;
    internal const uint CircularPatternTypeId = PatternSegments.CircularPatternTypeId;
    internal const string MirrorPatternClassName = MirrorPatternSegments.ClassName;
    internal const uint MirrorPatternTypeId = MirrorPatternSegments.TypeId;

    /// <summary>Parses the controlled linear-pattern layout (D1/D2 counts and spacings).</summary>
    public static LinearPatternFeatureModel? ParseLinearPattern(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != LinearPatternClassName || typeId != LinearPatternTypeId) return null;
        if (!FeatureWindowContext.TryCreate(doc, start, end, 0x72, out var ctx) || ctx == null)
            return null;

        var integerParam = ctx.GetSingleNode("moIntegerParameter_c");
        var compEdgeElem = ctx.GetSingleNode("moCompEdge_c");
        if (integerParam == null || compEdgeElem == null) return null;

        byte[] integerRaw = SliceElement(doc, integerParam, end, PatternSegments.LinearIntegerParameterLength);
        if (integerRaw.Length != PatternSegments.LinearIntegerParameterLength) return null;

        uint? count1 = MfcParameterReader.ReadNamedUInt32(integerRaw, PatternSegments.LinearD1CountParameterOffset, "D1");
        double? spacing1 = MfcParameterReader.ReadNamedDouble(integerRaw, PatternSegments.LinearD1SpacingParameterOffset, "D3");
        uint? count2 = MfcParameterReader.ReadNamedUInt32(integerRaw, PatternSegments.LinearD2CountParameterOffset, "D2");
        double? spacing2 = MfcParameterReader.ReadNamedDouble(integerRaw, PatternSegments.LinearD2SpacingParameterOffset, "D4");
        if (!count1.HasValue || count1.Value < 2 || count1.Value > 1000 ||
            !spacing1.HasValue || spacing1.Value <= 0.0 ||
            !count2.HasValue || count2.Value < 1 || count2.Value > 1000 ||
            !spacing2.HasValue || spacing2.Value <= 0.0)
            return null;

        byte[] compEdge = SliceElement(doc, compEdgeElem, end, PatternSegments.LinearCompEdgeLength);
        if (compEdge.Length != PatternSegments.LinearCompEdgeLength) return null;
        // Direction-reference envelope: the measured D5/D6 slots must be present and zero.
        if (MfcParameterReader.ReadNamedDouble(compEdge, 1534, "D5") != 0.0 ||
            MfcParameterReader.ReadNamedDouble(compEdge, 3117, "D6") != 0.0)
            return null;

        var definition = new LinearPatternFeatureModel
        {
            D1TotalInstances = (int)count1.Value,
            D1Spacing = spacing1.Value,
            D2TotalInstances = (int)count2.Value,
            D2Spacing = spacing2.Value,
            D1AxisType = 1, // controlled direction references are component edges
            D1Axis = new FeatureReferenceData { EntityType = SelectionEntityType.Edge },
            D2AxisType = count2.Value > 1 ? 1 : (int?)null,
            D2Axis = count2.Value > 1
                ? new FeatureReferenceData { EntityType = SelectionEntityType.Edge }
                : null
        };
        foreach (string name in new[]
        {
            nameof(LinearPatternFeatureModel.D1TotalInstances),
            nameof(LinearPatternFeatureModel.D1Spacing),
            nameof(LinearPatternFeatureModel.D2TotalInstances),
            nameof(LinearPatternFeatureModel.D2Spacing),
            nameof(LinearPatternFeatureModel.D1AxisType),
            nameof(LinearPatternFeatureModel.D1Axis)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        if (count2.Value > 1)
        {
            definition.MarkResolvedInternal(nameof(LinearPatternFeatureModel.D2AxisType));
            definition.MarkResolvedInternal(nameof(LinearPatternFeatureModel.D2Axis));
        }
        return definition;
    }

    /// <summary>Parses the measured circular-pattern layouts (equal spacing on/off).</summary>
    public static CircularPatternFeatureModel? ParseCircularPattern(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != CircularPatternClassName || typeId != CircularPatternTypeId) return null;
        if (!FeatureWindowContext.TryCreate(doc, start, end, 0x72, out var ctx) || ctx == null)
            return null;

        var integerParam = ctx.GetSingleNode("moIntegerParameter_c");
        var compEdgeElem = ctx.GetSingleNode("moCompEdge_c");
        var angleParam = ctx.GetSingleNode("moAngleParameter_c");
        if (integerParam == null || compEdgeElem == null || angleParam == null) return null;

        byte[] integerRaw = SliceElement(doc, integerParam, end, PatternSegments.CircularIntegerParameterLength);
        byte[] compEdge = ctx.SlicePayload(compEdgeElem) ?? Array.Empty<byte>();
        byte[] angleRaw = ctx.SlicePayload(angleParam) ?? Array.Empty<byte>();
        if (integerRaw.Length != PatternSegments.CircularIntegerParameterLength ||
            compEdge.Length == 0 || angleRaw.Length == 0)
            return null;

        uint? count = MfcParameterReader.ReadNamedUInt32(integerRaw, PatternSegments.CircularCountParameterOffset, "D1");
        double? spacing = MfcParameterReader.ReadNamedDouble(compEdge, PatternSegments.CircularSpacingParameterOffset, "D3")
                          ?? ReadNamedDoubleByName(compEdge, "D3");
        double? perInstanceAngle = MfcParameterReader.ReadNamedDouble(angleRaw, PatternSegments.CircularAngleParameterOffset, "D2");
        if (!count.HasValue || count.Value < 2 || count.Value > 1000 ||
            !spacing.HasValue || spacing.Value <= 0.0 || spacing.Value > 2.0 * Math.PI ||
            !perInstanceAngle.HasValue || perInstanceAngle.Value < 0.0)
            return null;

        // Layout A: equal-spacing samples (old 4x90 baseline or the native 4x90/6x60/equal-on
        // layout, which adds four bytes to moCompEdge_c). Layout B: equal-spacing-off sample.
        bool layoutA = compEdge.Length is 3830 or 3834;
        bool layoutB = compEdge.Length == 3724 && angleRaw.Length == 4174;
        if (!layoutA && !layoutB) return null;
        if (ReadNamedDoubleByName(compEdge, "D6") != 0.0) return null;

        // Native equal-on API Spacing = D3 and per-instance angle = D3/(count-1);
        // native equal-off API Spacing = D2 and per-instance angle = D3/count.
        bool equalSpacing;
        double apiSpacing;
        if (Math.Abs(perInstanceAngle.Value - spacing.Value / (count.Value - 1)) <= 1e-9)
        {
            equalSpacing = true;
            apiSpacing = spacing.Value;
        }
        else if (Math.Abs(perInstanceAngle.Value - spacing.Value / count.Value) <= 1e-9)
        {
            equalSpacing = false;
            apiSpacing = perInstanceAngle.Value;
        }
        else return null;

        var definition = new CircularPatternFeatureModel
        {
            TotalInstances = (int)count.Value,
            Spacing = apiSpacing,
            EqualSpacing = equalSpacing,
            AxisType = 1, // controlled axis reference is a component edge
            Axis = new FeatureReferenceData { EntityType = SelectionEntityType.Edge }
        };
        foreach (string name in new[]
        {
            nameof(CircularPatternFeatureModel.TotalInstances),
            nameof(CircularPatternFeatureModel.Spacing),
            nameof(CircularPatternFeatureModel.EqualSpacing),
            nameof(CircularPatternFeatureModel.AxisType),
            nameof(CircularPatternFeatureModel.Axis)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        return definition;
    }

    /// <summary>
    /// Parses the controlled mirror-pattern layout (<c>moMirrorPattern_c</c> / FolderHeader
    /// TypeId 0x57, stream 0x72). The measured sample mirrors one source extrusion across the
    /// Right Plane. The parser binds the plane-reference form (MirrorPlaneType=1), a
    /// presence-only plane reference and one presence-only source-feature reference; the
    /// source persistent-reference blob and plane block must match the measured bytes, and
    /// geometry-pattern/feature-scope flags remain unresolved.
    /// </summary>
    public static MirrorFeatureModel? ParseMirrorPattern(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != MirrorPatternClassName || typeId != MirrorPatternTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x72) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var feature = doc.AllElements.FirstOrDefault(e =>
            e.ClassName == MirrorPatternClassName && e.TagOffset == start);
        if (feature == null) return null;

        // The feature name is variable-length; the payload ends at the next class tag (or the
        // feature/folder end). The measured rest after the header is always 1163 bytes.
        int payloadEnd = end;
        foreach (var candidate in doc.AllElements)
        {
            if (candidate.TagOffset > feature.TagOffset && candidate.TagOffset < payloadEnd)
                payloadEnd = candidate.TagOffset;
        }
        byte[] payload = MfcPrimitives.CopyRange(doc.SourceData, feature.DataStart, payloadEnd);
        if (payload.Length < 2) return null;

        // Header: schema 0x8004 | WChar name | double 2.0/-2.0 | TypeId 0x57 | u32 0 | empty WChar.
        if (BitConverter.ToUInt16(payload, 0) != 0x8004) return null;
        int cursor = 2;
        string? name = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (string.IsNullOrEmpty(name)) return null;
        if (cursor + 8 + 4 + 4 + 4 > payload.Length) return null;
        double headerDouble = BitConverter.ToDouble(payload, cursor);
        if (Math.Abs(Math.Abs(headerDouble) - 2.0) > 1e-12) return null;
        cursor += 8;
        if (BitConverter.ToUInt32(payload, cursor) != MirrorPatternSegments.TypeId) return null;
        cursor += 4;
        if (BitConverter.ToUInt32(payload, cursor) != 0) return null;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (!string.IsNullOrEmpty(empty)) return null;
        if (payload.Length - cursor != MirrorPatternSegments.HeaderRestLength) return null;

        byte[] rest = MfcPrimitives.CopyRange(payload, cursor, payload.Length);
        if (rest.Length != MirrorPatternSegments.HeaderRestLength) return null;
        // Feature id magic and allocated id inside the measured rest.
        if (rest[105] != 0x56 || rest[106] != 0x68 || rest[107] != 0xDD || rest[108] != 0x01)
            return null;
        if (BitConverter.ToUInt32(rest, MirrorPatternSegments.FeatureIdOffsetInRest) == 0)
            return null;

        // Source-feature persistent-reference blob and Right-Plane reference block, relative
        // to the rest start so custom feature-name lengths are accepted.
        int sourceOffset = MirrorPatternSegments.SourceReferenceOffset - MirrorPatternSegments.HeaderRestOffset;
        int planeOffset = MirrorPatternSegments.PlaneReferenceOffset - MirrorPatternSegments.HeaderRestOffset;
        if (!RangeEquals(rest, sourceOffset, MirrorPatternSegments.SourceReferenceData)) return null;
        if (!RangeEquals(rest, planeOffset, MirrorPatternSegments.PlaneReferenceData)) return null;

        // Every other measured rest byte must match the controlled template. The allocated
        // feature-id slot and the single main/resave archive byte are allowed to vary.
        if (MirrorPatternSegments.HeaderRest.Length != rest.Length) return null;
        for (int i = 0; i < rest.Length; i++)
        {
            if (i >= MirrorPatternSegments.FeatureIdOffsetInRest &&
                i < MirrorPatternSegments.FeatureIdOffsetInRest + 4) continue;
            if (i == MirrorPatternSegments.ResaveVariantRestOffset) continue;
            if (rest[i] != MirrorPatternSegments.HeaderRest[i]) return null;
        }

        var definition = new MirrorFeatureModel
        {
            MirrorPlaneType = 1,
            Plane = new FeatureReferenceData { EntityType = SelectionEntityType.Plane },
            PatternFeatureArray = new[]
            {
                new FeatureReferenceData { EntityType = SelectionEntityType.None }
            }
        };
        definition.MarkResolvedInternal(nameof(MirrorFeatureModel.MirrorPlaneType));
        definition.MarkResolvedInternal(nameof(MirrorFeatureModel.Plane));
        definition.MarkResolvedInternal(nameof(MirrorFeatureModel.PatternFeatureArray));
        return definition;
    }

    private static bool RangeEquals(byte[] data, int offset, byte[] expected)
    {
        if (offset < 0 || expected == null || offset + expected.Length > data.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (data[offset + i] != expected[i]) return false;
        return true;
    }

    private static byte[] SliceElement(
        MfcDocument doc, MfcElement element, int featureEnd, int expectedLength)
    {
        int start = element.DataStart;
        if (start < 0 || expectedLength <= 0 ||
            start + expectedLength > featureEnd ||
            start + expectedLength > doc.SourceData.Length)
            return Array.Empty<byte>();
        return MfcPrimitives.CopyRange(doc.SourceData, start, start + expectedLength);
    }

    private static double? ReadNamedDoubleByName(byte[] data, string expectedName)
    {
        var matches = MfcParameterReader.FindNamedDoubles(data, 0, data.Length, expectedName);
        return matches.Count > 0 ? matches[0].Value : null;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/MfcPatternSerializer.cs
// ------------------------------------------------------------------------
/// <summary>
/// Serializes the controlled PatternOptions blocks for linear (moLPattern_c / 0x5D) and
/// circular (moCirPattern_c / 0x63) patterns. Only the measured instance-count, spacing and
/// per-instance-angle slots and their display copies are written; the direction/axis/source
/// selection metadata and header suffix remain the measured payloads.
/// </summary>
internal static class MfcPatternSerializer
{
    public static byte[] SerializeLinearPattern(
        LinearPatternFeatureModel def, string featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "绾挎€ч樀鍒?";

        int count1 = def.D1TotalInstances!.Value;
        double spacing1 = def.D1Spacing!.Value;
        int count2 = def.D2TotalInstances!.Value;
        double spacing2 = def.D2Spacing!.Value;

        using var writer = new MfcStreamWriter(12288);
        WriteHeader(writer, PatternSegments.LinearPatternClassName,
            PatternSegments.LinearPatternTypeId, featureName, PatternSegments.LinearHeaderSuffix);

        var displayDim = Clone(PatternSegments.LinearDisplayDimData);
        WriteF64At(displayDim, PatternSegments.LinearDisplayDimSpacingSlot, spacing1);
        WriteTagged(writer, "moDisplayDim_c", displayDim);
        WriteTagged(writer, "moNumberDim_c", PatternSegments.LinearNumberDimData);

        var integerParam = Clone(PatternSegments.LinearIntegerParameterData);
        WriteU32At(integerParam, PatternSegments.LinearD1CountValueSlot, (uint)count1);
        WriteF64At(integerParam, PatternSegments.LinearD1SpacingValueSlot, spacing1);
        foreach (int slot in PatternSegments.LinearD1SpacingCopySlots)
            WriteF64At(integerParam, slot, spacing1);
        WriteU32At(integerParam, PatternSegments.LinearD2CountValueSlot, (uint)count2);
        WriteF64At(integerParam, PatternSegments.LinearD2SpacingValueSlot, spacing2);
        foreach (int slot in PatternSegments.LinearD2SpacingCopySlots)
            WriteF64At(integerParam, slot, spacing2);
        WriteTagged(writer, "moIntegerParameter_c", integerParam);

        writer.WriteMfcTag("moLineRef_w");
        WriteTagged(writer, "moCompEdge_c", PatternSegments.LinearCompEdgeData);
        return writer.ToArray();
    }

    public static byte[] SerializeCircularPattern(
        CircularPatternFeatureModel def, string featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "鍦嗗懆闃靛垪1";

        int count = def.TotalInstances!.Value;
        double spacing = def.Spacing!.Value;
        double perInstanceAngle = spacing / (count - 1);

        using var writer = new MfcStreamWriter(10240);
        WriteHeader(writer, PatternSegments.CircularPatternClassName,
            PatternSegments.CircularPatternTypeId, featureName, PatternSegments.CircularHeaderSuffix);

        WriteTagged(writer, "moDisplayDim_c", PatternSegments.CircularDisplayDimData);
        WriteTagged(writer, "moNumberDim_c", PatternSegments.CircularNumberDimData);

        var integerParam = Clone(PatternSegments.CircularIntegerParameterData);
        WriteU32At(integerParam, PatternSegments.CircularCountValueSlot, (uint)count);
        WriteTagged(writer, "moIntegerParameter_c", integerParam);

        WriteTagged(writer, "moDisplayAngularDim_c", PatternSegments.CircularDisplayAngularDimData);
        WriteTagged(writer, "AngleDim_c", PatternSegments.CircularAngleDimData);

        var angleParam = Clone(PatternSegments.CircularAngleParameterData);
        WriteF64At(angleParam, PatternSegments.CircularAngleValueSlot, perInstanceAngle);
        WriteTagged(writer, "moAngleParameter_c", angleParam);

        writer.WriteMfcTag("moLineRef_w");
        var compEdge = Clone(PatternSegments.CircularCompEdgeData);
        WriteF64At(compEdge, PatternSegments.CircularSpacingValueSlot, spacing);
        foreach (int slot in PatternSegments.CircularSpacingCopySlots)
            WriteF64At(compEdge, slot, spacing);
        WriteTagged(writer, "moCompEdge_c", compEdge);
        return writer.ToArray();
    }

    private static void WriteHeader(
        MfcStreamWriter writer, string className, uint typeId, string featureName, byte[] suffix)
    {
        writer.WriteMfcTag(className);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(typeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(suffix);
    }

    private static void WriteTagged(MfcStreamWriter writer, string className, byte[] data)
    {
        writer.WriteMfcTag(className);
        writer.WriteBytes(data);
    }

    private static byte[] Clone(byte[] source) => (byte[])source.Clone();

    private static void WriteU32At(byte[] block, int offset, uint value)
    {
        if (offset < 0 || offset + 4 > block.Length)
            throw new InvalidOperationException($"Pattern segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }

    private static void WriteF64At(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException($"Pattern segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }
}


