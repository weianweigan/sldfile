using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

public sealed class SheetMetalBaseFlangeParameterResolution
{
    public bool Thickness { get; internal set; }
    public bool BendRadius { get; internal set; }
    public bool Depth { get; internal set; }
    public bool BothDirections { get; internal set; }
    public bool DepthDirection2 { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool ReverseThickness { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool KFactor { get; internal set; }
    public bool BendAllowanceType { get; internal set; }
    public bool AutoReliefType { get; internal set; }
    public bool ReliefRatio { get; internal set; }
    public bool ReliefWidth { get; internal set; }
    public bool ReliefDepth { get; internal set; }
    public bool UseDefaultBendRelief { get; internal set; }
    public bool UseGaugeTable { get; internal set; }
    public bool BendAllowance { get; internal set; }
    public bool BendDeduction { get; internal set; }
    public bool GaugeTablePath { get; internal set; }
    public bool ThicknessTableName { get; internal set; }
    public bool BendTableFile { get; internal set; }
}

/// <summary>
/// Unified model for moSMBaseFlange_c (Sheet Metal Base Flange feature).
/// Follows <see cref="IMfcSerializable"/> for symmetric reading and writing.
/// </summary>
public sealed class SheetMetalBaseFlangeModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "moSMBaseFlange_c";
    public const uint HeaderTypeId = 0x6Du;

    public string FeatureName { get; set; } = "基体-法兰1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get; set; } = HeaderTypeId;
    public string ClassName => MfcClassName;

    public SheetMetalBaseFlangeParameterResolution ParameterResolution { get; } = new();
        
    public double Thickness { get; set; }
    public double BendRadius { get; set; }
    public double KFactor { get; set; } = 0.5;
    public string? SketchName { get; set; } = string.Empty;
    public double Depth { get; set; }
    public double DepthDirection2 { get; set; }
    public bool BothDirections { get; set; }
    public bool ReverseDirection { get; set; }
    public bool ReverseThickness { get; set; }
    public bool UseDefaultBendRadius { get; set; } = true;
    public uint BendAllowanceType { get; set; }
    public double? BendAllowance { get; set; }
    public double? BendDeduction { get; set; }
    public uint AutoReliefType { get; set; }
    public double ReliefRatio { get; set; } = 0.5;
    public double ReliefWidth { get; set; }
    public double ReliefDepth { get; set; }
    public bool UseDefaultBendRelief { get; set; } = true;
    public string? BendTableFile { get; set; }
    public bool UseGaugeTable { get; set; }
    public string? GaugeTablePath { get; set; }
    public string? ThicknessTableName { get; set; }
    public DirectionReference? DirectionReference { get; set; }
    public BodyScopeData? BodyScope { get; set; }
    public IReadOnlyList<SketchContourReference>? SelectedContours { get; set; }
    public IReadOnlyList<SketchRegionReference>? SelectedRegions { get; set; }

    public SheetMetalBaseFlangeModel() { }

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

            double th = Thickness;
            ar.F64(ref th);

            double rad = BendRadius;
            ar.F64(ref rad);

            double k = KFactor;
            ar.F64(ref k);

            string? sk = SketchName;
            ar.WChar(ref sk);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

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
            TypeId = tid;

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 8)
            {
                double th = 0;
                ar.F64(ref th);
                Thickness = th;
            }

            if (ar.Remaining >= 8)
            {
                double rad = 0;
                ar.F64(ref rad);
                BendRadius = rad;
            }

            if (ar.Remaining >= 8)
            {
                double k = 0;
                ar.F64(ref k);
                KFactor = k;
            }

            if (ar.Remaining > 0)
            {
                string? sk = null;
                ar.WChar(ref sk);
                if (sk != null) SketchName = sk;
            }
        }
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalBaseFlangeSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured sheet-metal subsystem block for the controlled open-L specimen
/// (SheetMetalOptions/sm_l_d10.SLDPRT, ResolvedFeatures version 0x73).
/// <para>
/// The block starts at the native <c>moTemplateSheetMetal_c</c> tag (0x1D61) and
/// runs to the end of the stream. It contains the sheet-metal template and its
/// D1/D2/D3/D6/D8/D9 dimension records, the base-flange feature and its
/// dimensions, <c>moCompSheetmetalFeat_c</c>, the flat-pattern template, per-body
/// sheet metal data, the flat pattern and fold-sketch objects. Prefix (sketch-only)
/// + this suffix reconstructs the native stream byte-for-byte. The block is
/// gzip/base64 encoded only to keep the source readable; its SHA-256 is verified
/// before use.
/// </para>
/// </summary>
internal static class SheetMetalBaseFlangeSegments
{
    internal const string SuffixSha256 =
        "FF3FBC56CBDBEDDF7FE793EC2AEBEA051C9EBA1F6EBB15989BFDE758ACCEEFE1";

    /// <summary>
    /// Observed 0x67 sketch markers in the controlled prefix. This newer layout
    /// uses 67 80 00 00 00 00 00 00 at two positions rather than the 0x70
    /// 67 00 00 00 child-count marker; both are preserved unchanged.
    /// </summary>
    internal static readonly int[] SketchMarkerOffsets = { 0x1B70, 0x1C20 };
    internal static readonly byte[] SketchMarkerBytes = { 0x67, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    /// <summary>Full-sample ResolvedFeatures folder count at stream offset 4.</summary>
    internal const ushort MeasuredFullFolderCount = 22;

    /// <summary>Controlled L-sketch chain entity ids from moSketchChain_c.</summary>
    internal static readonly uint[] ExpectedChainEntityIds = { 2, 1 };

    /// <summary>SHA-256 of the single sgLineHandle element data in the controlled sketch.</summary>
    internal const string ExpectedLineSha256 =
        "b7d1b3a1104cc86b1cea310793cf777002db0517281d135a02de079b0ea87c23";

    /// <summary>Double values in the measured block before any authoring patch.</summary>
    internal const double MeasuredThickness = 0.001;
    internal const double MeasuredRadius = 0.002;
    internal const double MeasuredDepth = 0.010;

    /// <summary>
    /// Proven parameter slots. All offsets are relative to the suffix start
    /// (the moTemplateSheetMetal_c tag). Each slot is checked against the measured
    /// value before it is patched.
    /// </summary>
    internal static readonly int[] SuffixTemplateRadiusSlots = { 0x437, 0x4E5, 0x5CF, 0x5E7 };
    /// <summary>
    /// ResolvedFeatures thickness display/dimension copies. The authoritative
    /// rebuild source is the Config-0 length parameter named "厚度"/"Thickness",
    /// patched by <see cref="SheetMetalConfigThicknessPatcher"/>; these slots are
    /// kept in sync for display/readback.
    /// </summary>
    internal static readonly int[] SuffixTemplateThicknessSlots = { 0x14BF, 0x19C3 };
    internal static readonly int[] SuffixRadiusSlots =
        { 0x25C6, 0x2D28, 0x2D58, 0x2DBD, 0x479C, 0x484A, 0x63C1, 0x63E9, 0x6401, 0x64D9, 0x65C3, 0x65DB };
    internal static readonly int[] SuffixDepthSlots =
        { 0x2D30, 0x2D48, 0x2D60, 0x2DC5, 0x4C0B, 0x4ED6, 0x4F1E, 0x505C, 0x5074, 0x5106, 0x511E };

    /// <summary>
    /// Additional depth-valued copies that exist only in this newer layout and
    /// have no controlled depth-variant sample. They are intentionally preserved
    /// (lazy caches) and must be recomputed by the native rebuild.
    /// </summary>
    internal static readonly int[] UnprovenLazyDepthCaches =
        { 0x8401, 0x84BB, 0x8972, 0x89E3, 0x8A90, 0x9D27 };

    private static readonly byte[] SuffixGzipBytes = SheetMetalAuthoringSegments.BaseFlange0;

    private static readonly Lazy<byte[]> Suffix = new(Decompress, isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress()
    {
        byte[] gzip = SuffixGzipBytes;
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, SuffixSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured sheet-metal suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalBaseFlangeOptionSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed option templates measured from the 2026-10-31 base-flange retry
/// samples (ResolvedFeatures version 0x73, controlled open-L sketch). Each
/// template pairs one stripped base (stream prefix to the
/// <c>moSMBaseFlange_c</c> tag, 0x3EF1) with the measured feature block from the
/// corresponding native sample. Prefix + suffix reconstructs that sample's
/// ResolvedFeatures stream byte-for-byte.
/// <para>
/// The four samples were created in separate documents, so their prefixes carry
/// different creation timestamps/GUIDs. The normalized prefix SHA-256 (folder
/// count zeroed at offset 4) identifies the exact base each template requires.
/// The sketch geometry is the same controlled open-L profile as the main
/// authoring template, but the stream generation is different (43,270/43,272/
/// 43,685 bytes vs 47,844 in sm_l_d10), so these templates are a separate fixed
/// geometry group.
/// </para>
/// </summary>
internal static class SheetMetalBaseFlangeOptionSegments
{
    internal enum OptionTemplate
    {
        KFactor,
        Direct,
        Deduction,
        GaugeTable
    }

    internal const int MeasuredPrefixLength = 0x3EF1;
    internal const ushort SupportedStreamVersion = 0x73;
    internal const ushort MeasuredFullFolderCount = 22;
    internal const string MeasuredFeatureName = "基体-法兰1";

    internal static readonly int[] MarkerOffsets = { 0x145F, 0x1B70, 0x1C20 };
    internal static readonly byte[] MarkerBytes = { 0x67, 0x80, 0, 0, 0, 0, 0, 0 };

    internal const double MeasuredKFactor = 0.3;
    internal const double MeasuredBendAllowance = 0.001;
    internal const double MeasuredBendDeduction = 0.001;
    internal const string MeasuredGaugeTableName = "规格表";
    internal const string MeasuredGaugeTablePath =
        "C:\\Program Files\\SOLIDWORKS Corp\\SOLIDWORKS\\lang\\chinese-simplified\\" +
        "Sheet Metal Gauge Tables\\k-factor mm sample.xlsx";

    internal const string KFactorNormalizedPrefixSha256 =
        "979033DA60E7B9DEC09F66D370EC537F45CFA1154F76036AE18DD7A70BAA5791";
    internal const string DirectNormalizedPrefixSha256 =
        "B70EED89E2D6BEE0D60385169BA00E3C7353AD997D51D0DF07651D9AC4CF11D4";
    internal const string DeductionNormalizedPrefixSha256 =
        "E3E029FC315A386D755B4B9AA49C0F1A110A5271979132EDB414D2D0A095A153";
    internal const string GaugeTableNormalizedPrefixSha256 =
        "B298CF467A8066B162BE41FFCC1B3750F7CA546B930477DDB923F9BE6BF243A4";

    internal const string KFactorSuffixSha256 =
        "B40656B78CD23890906B4714E174CE785616CBEF45A1F8C5E40256D9667F7278";
    internal const string DirectSuffixSha256 =
        "47D2E3E8C5A60A3B237C73596EDFF67A3312184A8A2ED2B7964B71F6FADE177C";
    internal const string DeductionSuffixSha256 =
        "4F677E72A000F2B71703AA101C29A1ECFDF6549930CA26B5B01A101701C4F5FF";
    internal const string GaugeTableSuffixSha256 =
        "DE3C8AEFDC2BEA3BCABE8A7E9D26A21B2FAB2F500084411A0AD6C8D01955726C";

    private static readonly byte[] KFactorSuffixGzipBytes = SheetMetalAuthoringSegments.BaseFlangeOption0;
    private static readonly byte[] DirectSuffixGzipBytes = SheetMetalAuthoringSegments.BaseFlangeOption1;
    private static readonly byte[] DeductionSuffixGzipBytes = SheetMetalAuthoringSegments.BaseFlangeOption2;
    private static readonly byte[] GaugeTableSuffixGzipBytes = SheetMetalAuthoringSegments.BaseFlangeOption3;

    private static readonly Lazy<byte[]> KFactorSuffix =
        new(() => Decompress(KFactorSuffixGzipBytes, KFactorSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> DirectSuffix =
        new(() => Decompress(DirectSuffixGzipBytes, DirectSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> DeductionSuffix =
        new(() => Decompress(DeductionSuffixGzipBytes, DeductionSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> GaugeTableSuffix =
        new(() => Decompress(GaugeTableSuffixGzipBytes, GaugeTableSuffixSha256), isThreadSafe: true);

    internal static string GetNormalizedPrefixSha256(OptionTemplate template) => template switch
    {
        OptionTemplate.KFactor => KFactorNormalizedPrefixSha256,
        OptionTemplate.Direct => DirectNormalizedPrefixSha256,
        OptionTemplate.Deduction => DeductionNormalizedPrefixSha256,
        OptionTemplate.GaugeTable => GaugeTableNormalizedPrefixSha256,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    internal static string GetSuffixSha256(OptionTemplate template) => template switch
    {
        OptionTemplate.KFactor => KFactorSuffixSha256,
        OptionTemplate.Direct => DirectSuffixSha256,
        OptionTemplate.Deduction => DeductionSuffixSha256,
        OptionTemplate.GaugeTable => GaugeTableSuffixSha256,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    internal static byte[] GetSuffix(OptionTemplate template) => (byte[])(template switch
    {
        OptionTemplate.KFactor => KFactorSuffix.Value,
        OptionTemplate.Direct => DirectSuffix.Value,
        OptionTemplate.Deduction => DeductionSuffix.Value,
        OptionTemplate.GaugeTable => GaugeTableSuffix.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    }).Clone();

    private static byte[] Decompress(byte[] gzip, string expectedSha256)
    {
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured base-flange option suffix hash mismatch.");
        return bytes;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalBaseFlangeBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// First writable sheet-metal base-flange combination: the measured open-L
/// specimen from <c>SheetMetalOptions/sm_l_d10.SLDPRT</c> (ResolvedFeatures
/// version 0x73), a stripped sketch-only baseline, and parameter replacement for
/// thickness, bend radius and depth only.
/// <para>
/// The complete native sheet-metal subsystem is stored as a measured, hash-pinned
/// suffix (see <see cref="SheetMetalBaseFlangeSegments"/>). The builder verifies
/// the controlled L-sketch signature (chain ids and sgLineHandle SHA-256), refuses
/// any other feature/selection/option combination, appends the measured suffix,
/// restores the measured folder count and sketch child count, and patches only the
/// proven scalar slots. The ResolvedFeatures thickness slots are display/dimension
/// copies; the authoritative rebuild source is the Config-0 length parameter
/// "厚度"/"Thickness", patched separately by
/// <see cref="SheetMetalConfigThicknessPatcher"/>. Bounding-box/display caches that
/// depend on more than the three patched scalars are intentionally preserved
/// (lazy) and must be recomputed by the native rebuild.
/// </para>
/// </summary>
internal static class SheetMetalBaseFlangeBuilder
{
    public const string DefaultFeatureName = "基体-法兰1";

    /// <summary>
    /// The measured suffix was serialized in a version-0x73 stream. No native
    /// sheet-metal sample with version 0x70 exists; forcing 0x70 would mix archive
    /// layouts, so this first combination is deliberately gated to 0x73.
    /// </summary>
    internal const ushort SupportedStreamVersion = 0x73;

    private const string SketchChainClass = "moSketchChain_c";
    private const string SketchLineClass = "sgLineHandle";

    public static byte[] Build(
        byte[] currentStream,
        SheetMetalBaseFlangeModel def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        SheetMetalFeatureBuilder.ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Sheet metal base flange authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; changing the name would change the measured block layout. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Sheet metal base flange authoring is only supported for ResolvedFeatures streams " +
                $"version 0x{SupportedStreamVersion:X2} (the measured sheet-metal layout), but the document " +
                $"uses 0x{version:X2}. A version-0x70 sheet-metal sample is not available, so it is not guessed. " +
                "No stream changes have been made.");
        }

        VerifyControlledSketch(currentStream, targetSketchFolder, sketchEndOffset, version);
        VerifyControlledPrefix(currentStream);
        VerifyMeasuredSketchMarkers(currentStream);
        byte[] suffix = SheetMetalBaseFlangeSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        int suffixStart = currentStream.Length;
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixTemplateRadiusSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredRadius, def.BendRadius, "template radius");
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixTemplateThicknessSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredThickness, def.Thickness, "template thickness");
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixRadiusSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredRadius, def.BendRadius, "feature/global radius");
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixDepthSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredDepth, def.Depth, "feature depth/cache");

        // Restore the measured folder count. The strip-only baseline may keep or
        // reset it; the authored stream must match the native full sample because
        // prefix + measured suffix reconstructs it byte-for-byte (apart from the
        // patched scalar slots). The 0x67 markers are preserved unchanged.
        BitConverter.GetBytes(SheetMetalBaseFlangeSegments.MeasuredFullFolderCount).CopyTo(newStream, 4);

        return newStream;
    }

    private static void VerifyControlledSketch(
        byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;

        int end = sketchEndOffset > targetSketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;

        // Use the tree-aware MfcDocument element bounds: flat tag scanning extends
        // one element to the next tag and would include the untagged gap after the
        // line handle, changing its signature.
        var elements = MfcDocument.Parse(currentStream).AllElements
            .Where(t => t.TagOffset >= targetSketchFolder.TagOffset && t.TagOffset < end)
            .ToList();
        var chains = elements.Where(t => t.ClassName == SketchChainClass).ToList();
        var lines = elements.Where(t => t.ClassName == SketchLineClass).ToList();
        if (chains.Count != 1 || lines.Count != 1)
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange on sketch '{sketchName}' " +
                $"(ResolvedFeatures version 0x{version:X2}): the sketch must expose exactly one parsed " +
                $"{SketchChainClass} and one {SketchLineClass}. Authoring is limited to the controlled open-L " +
                "specimen. No stream changes have been made.");
        }

        if (!ChainMatchesControlledProfile(currentStream, chains[0]))
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange on sketch '{sketchName}': the sketch chain does not " +
                "match the controlled open-L specimen (entity ids [2,1]). No stream changes have been made.");
        }

        string lineSha = Sha256Hex(currentStream, lines[0]);
        if (!string.Equals(lineSha, SheetMetalBaseFlangeSegments.ExpectedLineSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange on sketch '{sketchName}': the {SketchLineClass} " +
                $"signature {lineSha.Substring(0, 12)} does not match the controlled open-L specimen. " +
                "The measured sheet-metal topology assumes that exact sketch. No stream changes have been made.");
        }

        // The measured 0x67 markers are validated separately and preserved unchanged.
    }

    /// <summary>
    /// This newer 0x73 layout uses 67 80 00 00 00 00 00 00 markers rather than the
    /// 0x70 67 00 00 00 child-count marker. Both measured positions must be present
    /// and are left untouched; the measured suffix restores the native topology.
    /// </summary>
    private static void VerifyMeasuredSketchMarkers(byte[] currentStream)
    {
        byte[] expected = SheetMetalBaseFlangeSegments.SketchMarkerBytes;
        foreach (int offset in SheetMetalBaseFlangeSegments.SketchMarkerOffsets)
        {
            if (offset < 0 || offset + expected.Length > currentStream.Length)
                throw new NotSupportedException(
                    "Cannot author a sheet metal base flange: the measured 0x67 marker is outside the stream. " +
                    "No stream changes have been made.");
            for (int i = 0; i < expected.Length; i++)
            {
                if (currentStream[offset + i] == expected[i]) continue;
                throw new NotSupportedException(
                    $"Cannot author a sheet metal base flange: the measured 0x67 marker at 0x{offset:X} " +
                    "is missing or has changed. Use the stripped controlled specimen. " +
                    "No stream changes have been made.");
            }
        }
    }

    private static bool ChainMatchesControlledProfile(byte[] currentStream, MfcElement chain)
    {
        int length = chain.DataEnd - chain.DataStart;
        if (length < 2 || chain.DataStart < 0 || chain.DataStart + length > currentStream.Length)
            return false;

        ushort count = BitConverter.ToUInt16(currentStream, chain.DataStart);
        if (count != SheetMetalBaseFlangeSegments.ExpectedChainEntityIds.Length || length < 2 + 4 * count)
            return false;

        for (int i = 0; i < count; i++)
        {
            uint id = BitConverter.ToUInt32(currentStream, chain.DataStart + 2 + 4 * i);
            if (id != SheetMetalBaseFlangeSegments.ExpectedChainEntityIds[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// The suffix contains the complete measured sheet-metal feature graph. It can
    /// only be appended to the matching stripped specimen: no sheet-metal feature
    /// may already exist, and no other solid feature may precede the sketch.
    /// </summary>
    private static void VerifyControlledPrefix(byte[] currentStream)
    {
        void RequireZero(string className, string reason)
        {
            if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, className) != 0)
                throw new NotSupportedException(
                    $"Cannot author a sheet metal base flange: the target still contains {className} " +
                    $"({reason}). Use the stripped sketch-only controlled specimen. No stream changes have been made.");
        }

        RequireZero("moSMBaseFlange_c", "a base flange already exists");
        RequireZero("moCompSheetmetalFeat_c", "sheet-metal body data already exists");
        RequireZero("moTemplateFlatPattern_c", "a flat-pattern template already exists");
        RequireZero("moFlatPattern_c", "a flat pattern already exists");
        RequireZero("moSheetMetal_c", "global sheet-metal data already exists");
        RequireZero("moExtrusion_c", "an extrusion precedes the sketch");
        RequireZero("moRevolution_c", "a revolution precedes the sketch");
        RequireZero("Fillet_c", "a fillet precedes the sketch");
        RequireZero("moChamfer_c", "a chamfer precedes the sketch");

        // Sketch-only baseline: the measured suffix supplies the template and all
        // downstream sheet-metal objects.
        RequireZero("moTemplateSheetMetal_c", "a sheet-metal template already exists");

        int profileCount = ExtrudeFeatureBuilder.CountOccurrences(currentStream, "moProfileFeature_c");
        if (profileCount != 1)
        {
            throw new NotSupportedException(
                "Cannot author a sheet metal base flange: the target must contain exactly one sketch " +
                $"(found {profileCount}). No stream changes have been made.");
        }
    }

    private static void PatchDouble(byte[] data, int offset, double expected, double value, string slotName)
    {
        if (offset < 0 || offset + 8 > data.Length)
            throw new InvalidOperationException($"Sheet-metal slot {slotName} at 0x{offset:X} is outside the stream.");
        double current = BitConverter.ToDouble(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange: measured {slotName} slot at 0x{offset:X} contains " +
                $"{current:R}, expected {expected:R}. The target is not the controlled measured specimen. " +
                "No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static string Sha256Hex(byte[] data, MfcElement tag)
    {
        int start = tag.DataStart;
        int count = Math.Max(0, tag.DataEnd - start);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data, start, count);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalBaseFlangeOptionBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed option-template authoring for the 2026-10-31 base-flange retry group
/// (ResolvedFeatures version 0x73, controlled open-L sketch, stream lengths
/// 43,270/43,272/43,685 bytes).
/// <para>
/// The caller must start from the exact stripped prefix of the matching native
/// sample (first 0x3EF1 bytes, ending at the <c>moSMBaseFlange_c</c> tag). Each
/// template's normalized prefix SHA-256, folder count, 0x67 markers and version
/// are pinned. The measured suffix is appended verbatim; no scalar is patched,
/// so the authored ResolvedFeatures stream is byte-identical to the native
/// source sample. Only the measured combination is accepted; other values,
/// custom names, another template's base and a non-stripped document are
/// rejected before the stream is touched.
/// </para>
/// </summary>
internal static class SheetMetalBaseFlangeOptionBuilder
{
    internal const string DefaultFeatureName = SheetMetalBaseFlangeOptionSegments.MeasuredFeatureName;

    internal static SheetMetalBaseFlangeOptionSegments.OptionTemplate SelectTemplate(
        SheetMetalBaseFlangeModel def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        RejectCommon(def);

        if (def.UseGaugeTable)
        {
            RejectUnlessNullOrClose(def.KFactor, 0.5, nameof(def.KFactor));
            if (def.BendAllowanceType != 0 && def.BendAllowanceType != 2)
                throw Unsupported(nameof(def.BendAllowanceType), def.BendAllowanceType);
            RequireNull(def.BendAllowance, nameof(def.BendAllowance));
            RequireNull(def.BendDeduction, nameof(def.BendDeduction));
            if (def.GaugeTablePath != null &&
                !string.Equals(def.GaugeTablePath,
                    SheetMetalBaseFlangeOptionSegments.MeasuredGaugeTablePath, StringComparison.Ordinal))
                throw Unsupported(nameof(def.GaugeTablePath), def.GaugeTablePath);
            if (def.ThicknessTableName != null &&
                !string.Equals(def.ThicknessTableName,
                    SheetMetalBaseFlangeOptionSegments.MeasuredGaugeTableName, StringComparison.Ordinal))
                throw Unsupported(nameof(def.ThicknessTableName), def.ThicknessTableName);
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.GaugeTable;
        }

        if (def.BendAllowanceType == 2)
        {
            RejectUnlessClose(def.KFactor, SheetMetalBaseFlangeOptionSegments.MeasuredKFactor,
                nameof(def.KFactor));
            RequireNull(def.BendAllowance, nameof(def.BendAllowance));
            RequireNull(def.BendDeduction, nameof(def.BendDeduction));
            RequireNull(def.GaugeTablePath, nameof(def.GaugeTablePath));
            RequireNull(def.ThicknessTableName, nameof(def.ThicknessTableName));
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.KFactor;
        }

        if (def.BendAllowanceType == 3)
        {
            RejectUnlessClose(def.KFactor, 0.5, nameof(def.KFactor));
            RequireClose(def.BendAllowance,
                SheetMetalBaseFlangeOptionSegments.MeasuredBendAllowance, nameof(def.BendAllowance));
            RequireNull(def.BendDeduction, nameof(def.BendDeduction));
            RequireNull(def.GaugeTablePath, nameof(def.GaugeTablePath));
            RequireNull(def.ThicknessTableName, nameof(def.ThicknessTableName));
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.Direct;
        }

        if (def.BendAllowanceType == 4)
        {
            RejectUnlessClose(def.KFactor, 0.5, nameof(def.KFactor));
            RequireClose(def.BendDeduction,
                SheetMetalBaseFlangeOptionSegments.MeasuredBendDeduction, nameof(def.BendDeduction));
            RequireNull(def.BendAllowance, nameof(def.BendAllowance));
            RequireNull(def.GaugeTablePath, nameof(def.GaugeTablePath));
            RequireNull(def.ThicknessTableName, nameof(def.ThicknessTableName));
            return SheetMetalBaseFlangeOptionSegments.OptionTemplate.Deduction;
        }

        throw new NotSupportedException(
            "Base-flange option authoring supports only the measured combinations: " +
            "KFactor (BendAllowanceType=2, KFactor=0.3), Direct (type=3, BendAllowance=0.001), " +
            "Deduction (type=4, BendDeduction=0.001) or UseGaugeTable=true with the k-factor mm " +
            $"sample. Received BendAllowanceType={def.BendAllowanceType}. No stream changes have been made.");
    }

    internal static void ValidateTemplateGeometry(
        SheetMetalBaseFlangeModel def,
        SheetMetalBaseFlangeOptionSegments.OptionTemplate template,
        byte[] currentStream)
    {
        double expectedRadius = template == SheetMetalBaseFlangeOptionSegments.OptionTemplate.GaugeTable
            ? 0.001
            : 0.002;
        if (!NearEquals(def.Thickness, 0.001))
            throw Unsupported(nameof(def.Thickness), def.Thickness);
        if (!NearEquals(def.BendRadius, expectedRadius))
            throw Unsupported(nameof(def.BendRadius), def.BendRadius);
        if (!NearEquals(def.Depth, 0.010))
            throw Unsupported(nameof(def.Depth), def.Depth);

        if (currentStream == null ||
            currentStream.Length != SheetMetalBaseFlangeOptionSegments.MeasuredPrefixLength)
            throw new NotSupportedException(
                $"Base-flange option authoring requires the stripped template base " +
                $"({SheetMetalBaseFlangeOptionSegments.MeasuredPrefixLength} bytes, ending at the " +
                $"moSMBaseFlange_c tag), but received {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != SheetMetalBaseFlangeOptionSegments.SupportedStreamVersion)
            throw new NotSupportedException(
                $"Base-flange option authoring is only supported for ResolvedFeatures version " +
                $"0x{SheetMetalBaseFlangeOptionSegments.SupportedStreamVersion:X2}, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");

        if (BitConverter.ToUInt16(currentStream, 4) !=
            SheetMetalBaseFlangeOptionSegments.MeasuredFullFolderCount)
            throw new NotSupportedException(
                $"Base-flange option authoring requires folder count " +
                $"{SheetMetalBaseFlangeOptionSegments.MeasuredFullFolderCount}. " +
                "No stream changes have been made.");

        byte[] markerBytes = SheetMetalBaseFlangeOptionSegments.MarkerBytes;
        foreach (int offset in SheetMetalBaseFlangeOptionSegments.MarkerOffsets)
        {
            if (offset < 0 || offset + markerBytes.Length > currentStream.Length)
                throw new NotSupportedException(
                    "Base-flange option authoring requires the measured 0x67 marker block. " +
                    "No stream changes have been made.");
            for (int i = 0; i < markerBytes.Length; i++)
                if (currentStream[offset + i] != markerBytes[i])
                    throw new NotSupportedException(
                        $"Base-flange option authoring requires the measured 0x67 marker at " +
                        $"0x{offset:X}. No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moSMBaseFlange_c") != 0)
            throw new NotSupportedException(
                "Base-flange option authoring requires a stripped base without an existing base flange. " +
                "No stream changes have been made.");

        byte[] normalized = (byte[])currentStream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        string hash = Sha256Hex(normalized);
        string expected = SheetMetalBaseFlangeOptionSegments.GetNormalizedPrefixSha256(template);
        if (!string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                $"Base-flange option authoring requires the '{template}' stripped template base: " +
                $"normalized prefix SHA-256 {hash.Substring(0, 12)} does not match the measured " +
                $"signature {expected.Substring(0, 12)}. No stream changes have been made.");
    }

    internal static byte[] Build(
        byte[] currentStream,
        SheetMetalBaseFlangeModel def,
        string? featureName)
    {
        var template = SelectTemplate(def);
        ValidateTemplateGeometry(def, template, currentStream);

        string resolvedName = string.IsNullOrWhiteSpace(featureName)
            ? DefaultFeatureName
            : featureName!;
        if (!string.Equals(resolvedName, DefaultFeatureName, StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Base-flange option authoring supports only the measured display name " +
                $"'{DefaultFeatureName}'; received '{resolvedName}'. No stream changes have been made.");

        byte[] suffix = SheetMetalBaseFlangeOptionSegments.GetSuffix(template);
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        BitConverter.GetBytes(SheetMetalBaseFlangeOptionSegments.MeasuredFullFolderCount)
            .CopyTo(newStream, 4);
        return newStream;
    }

    private static void RejectCommon(SheetMetalBaseFlangeModel def)
    {
        if (def.ReverseThickness) throw Unsupported(nameof(def.ReverseThickness), true);
        if (def.BothDirections) throw Unsupported(nameof(def.BothDirections), true);
        if (def.DepthDirection2 != 0) throw Unsupported(nameof(def.DepthDirection2), def.DepthDirection2);
        if (def.ReverseDirection) throw Unsupported(nameof(def.ReverseDirection), true);
        if (!def.UseDefaultBendRadius) throw Unsupported(nameof(def.UseDefaultBendRadius), false);
        if (def.AutoReliefType > 1) throw Unsupported(nameof(def.AutoReliefType), def.AutoReliefType);
        if (!NearEquals(def.ReliefRatio, 0.5)) throw Unsupported(nameof(def.ReliefRatio), def.ReliefRatio);
        if (def.ReliefWidth != 0) throw Unsupported(nameof(def.ReliefWidth), def.ReliefWidth);
        if (def.ReliefDepth != 0) throw Unsupported(nameof(def.ReliefDepth), def.ReliefDepth);
        if (!def.UseDefaultBendRelief) throw Unsupported(nameof(def.UseDefaultBendRelief), false);
        if (!string.IsNullOrEmpty(def.BendTableFile))
            throw Unsupported(nameof(def.BendTableFile), def.BendTableFile);
        if (def.SelectedContours != null && def.SelectedContours.Count > 0)
            throw Unsupported(nameof(def.SelectedContours), def.SelectedContours.Count);
        if (def.SelectedRegions != null && def.SelectedRegions.Count > 0)
            throw Unsupported(nameof(def.SelectedRegions), def.SelectedRegions.Count);
        if (def.DirectionReference != null) throw Unsupported(nameof(def.DirectionReference), "set");
        if (def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)))
            throw Unsupported(nameof(def.BodyScope), "non-default");
    }

    private static void RejectUnlessClose(double value, double measured, string property)
    {
        if (!NearEquals(value, measured)) throw Unsupported(property, value);
    }

    private static void RejectUnlessNullOrClose(double? value, double measured, string property)
    {
        if (value.HasValue && !NearEquals(value.Value, measured))
            throw Unsupported(property, value.Value);
    }

    private static void RequireClose(double? value, double measured, string property)
    {
        if (!value.HasValue || !NearEquals(value.Value, measured))
            throw Unsupported(property, value?.ToString() ?? "null");
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null) throw Unsupported(property, value);
    }

    private static bool NearEquals(double a, double b) => Math.Abs(a - b) <= 1e-12;

    private static NotSupportedException Unsupported(string property, object? value) =>
        new($"Base-flange option authoring does not support {property}={value}. " +
            "No stream changes have been made.");

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) { count++; i += pattern.Length - 1; }
        }
        return count;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalBaseFlangeParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Reads dimension parameters and option flags that belong to one
/// <c>moSMBaseFlange_c</c> feature. The base flange folder itself is a small
/// header; its dimensions are serialized as separate named-parameter records and
/// as a small option block in the feature's stream window.
/// </summary>
internal static class SheetMetalBaseFlangeParser
{
    private const string GaugeTableFeatureClass = "moSMEnvTableFeat_c";

    internal static void Read(byte[] raw, int start, int end, SheetMetalBaseFlangeModel data)
    {
        if (raw == null || raw.Length < 2) return;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return;
        start = Math.Max(0, start);
        end = Math.Min(raw.Length, end);

        bool sawD2Record = false, sawBendDimension = false;
        for (int p = start; p <= end - 38; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != 'D' || raw[p + 5] != 0 || raw[p + 7] != 0)
                continue;
            if (!MfcParameterReader.ReadDoubleDescriptorAfterName(raw, p + 8).HasValue) continue;
            if (raw[p + 6] == '4' || raw[p + 6] == '8' || raw[p + 6] == '9')
                sawBendDimension = true;
            if (raw[p + 6] == '2')
                sawD2Record = true;
        }

        if (MfcParameterReader.TryReadUniqueDoubleByWChar(
                raw, "D2", start, end, out double candidateDepth, out _,
                allowIdenticalDuplicates: true, onlyPositive: true, allowZero: false))
        {
            data.Depth = candidateDepth;
            data.ParameterResolution.Depth = true;
        }
        else if (!sawD2Record && TryReadClosedProfileDepth(raw, start, end, out double closedDepth))
        {
            data.Depth = closedDepth;
            data.ParameterResolution.Depth = true;
        }

        if (MfcParameterReader.TryReadUniqueDoubleByWChar(
                raw, "D3", start, end, out double candidateDepth2, out _,
                allowIdenticalDuplicates: true, onlyPositive: true, allowZero: false))
        {
            data.DepthDirection2 = candidateDepth2;
            data.BothDirections = true;
            data.ParameterResolution.DepthDirection2 = true;
            data.ParameterResolution.BothDirections = true;
        }

        if (sawBendDimension)
            TryReadAutoReliefType(raw, start, data);
        TryReadGaugeTable(raw, start, end, data);
        TryReadReverseFlags(raw, start, end, data);
    }

    private static void TryReadGaugeTable(
        byte[] raw, int start, int end, SheetMetalBaseFlangeModel data)
    {
        int gaugeTag = -1, count = 0;
        int last = raw.Length - (6 + GaugeTableFeatureClass.Length);
        for (int p = 0; p <= last; p++)
        {
            if (!IsMfcTagNamed(raw, p, GaugeTableFeatureClass)) continue;
            gaugeTag = p;
            count++;
        }

        if (count == 0)
        {
            data.UseGaugeTable = false;
            data.ParameterResolution.UseGaugeTable = true;
            return;
        }
        if (count != 1) return;

        int baseFlangeCount = 0;
        int baseLast = raw.Length - (6 + "moSMBaseFlange_c".Length);
        for (int p = 0; p <= baseLast; p++)
            if (IsMfcTagNamed(raw, p, "moSMBaseFlange_c")) baseFlangeCount++;
        if (baseFlangeCount != 1) return;

        int dataStart = gaugeTag + 6 + GaugeTableFeatureClass.Length;
        if (dataStart + 6 > raw.Length) return;
        if (raw[dataStart] != 0x04 || raw[dataStart + 1] != 0x80 ||
            !MfcPrimitives.IsWCharPrefix(raw, dataStart + 2))
            return;

        int cursor = dataStart + 2;
        string? name = MfcPrimitives.ReadWChar(raw, ref cursor, raw.Length);
        if (string.IsNullOrWhiteSpace(name) || name!.Length > 128) return;

        data.UseGaugeTable = true;
        data.ParameterResolution.UseGaugeTable = true;
        data.ThicknessTableName = name;
        data.ParameterResolution.ThicknessTableName = true;

        if (TryFindUniqueTablePath(raw, 0, raw.Length, "Sheet Metal Gauge Tables", new[] { ".xlsx", ".xls", ".csv", ".btl" }, out string path))
        {
            data.GaugeTablePath = path;
            data.ParameterResolution.GaugeTablePath = true;
        }
    }

    private static bool TryFindUniqueTablePath(
        byte[] raw, int start, int end, string requiredMarker, string[] extensions, out string path)
    {
        path = string.Empty;
        var matches = MfcPrimitives.ScanWCharFields(
                raw,
                start,
                end,
                maxStringLength: 260,
                predicate: text =>
                    text.Length >= 8 &&
                    text.IndexOf(requiredMarker, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    extensions.Any(ext => text.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Select(static f => f.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (matches.Count != 1) return false;
        path = matches[0];
        return true;
    }

    private static bool TryReadClosedProfileDepth(byte[] raw, int start, int end, out double value)
    {
        value = 0;
        if (end < start + 0x30) return false;
        if (!IsMfcTagNamed(raw, end, "moTemplateFlatPattern_c")) return false;

        int valueOffset = end - 0x28;
        int compFeatureTag = FindLastMfcTag(raw, start, valueOffset, "moCompSheetmetalFeat_c");
        if (compFeatureTag < 0) return false;

        for (int i = valueOffset - 8; i < valueOffset; i++)
            if (raw[i] != 0) return false;
        for (int i = valueOffset + 8; i < end; i++)
            if (raw[i] != 0) return false;

        double candidate = BitConverter.ToDouble(raw, valueOffset);
        if (!SheetMetalTemplateParser.PositiveFinite(candidate)) return false;
        value = candidate;
        return true;
    }

    private static void TryReadAutoReliefType(byte[] raw, int start, SheetMetalBaseFlangeModel data)
    {
        if (start < 0x2C) return;
        if (!IsMfcTagNamed(raw, start, "moSMBaseFlange_c")) return;
        int valueOffset = start - 0x28;
        if (valueOffset + 8 > raw.Length) return;

        for (int i = valueOffset + 4; i < valueOffset + 8; i++)
            if (raw[i] != 0) return;

        uint code = BitConverter.ToUInt32(raw, valueOffset);
        uint apiValue = code switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            4 => 4,
            _ => 0
        };
        if (apiValue == 0) return;
        data.AutoReliefType = apiValue;
        data.ParameterResolution.AutoReliefType = true;
    }

    private static void TryReadReverseFlags(byte[] raw, int start, int end, SheetMetalBaseFlangeModel data)
    {
        int compTag = -1, compCount = 0;
        int last = Math.Min(end, raw.Length) - (6 + "moCompSheetmetalFeat_c".Length);
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (!IsMfcTagNamed(raw, p, "moCompSheetmetalFeat_c")) continue;
            compTag = p;
            compCount++;
        }
        if (compCount != 1 || compTag + 0x93 > end) return;

        if (raw[compTag + 0x89] != 0xC4 && raw[compTag + 0x89] != 0xCB) return;
        if (raw[compTag + 0x8A] != 0) return;

        byte reverseThickness = raw[compTag + 0x8B];
        byte reverseDirection = raw[compTag + 0x8F];
        if (reverseThickness > 1 || reverseDirection > 1) return;
        for (int i = 0x8C; i <= 0x8E; i++) if (raw[compTag + i] != 0) return;
        for (int i = 0x90; i <= 0x92; i++) if (raw[compTag + i] != 0) return;

        data.ReverseThickness = reverseThickness != 0;
        data.ReverseDirection = reverseDirection != 0;
        data.ParameterResolution.ReverseThickness = true;
        data.ParameterResolution.ReverseDirection = true;
    }

    internal static bool TryReadKFactor(byte[] raw, int start, int end, out double value)
    {
        value = 0;
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return false;
        start = Math.Max(0, start);
        end = Math.Min(raw.Length, end);
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;

        return MfcParameterReader.TryReadUniqueDoubleByWChar(
            raw, "D2", start, end, out value, out _,
            allowIdenticalDuplicates: true, onlyPositive: true, allowZero: false);
    }

    internal static bool TryReadBendAllowance(
        byte[] raw, int start, int end, SheetMetalBaseFlangeModel data)
    {
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return false;
        start = Math.Max(0, start);
        end = Math.Min(end, raw.Length);
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;

        if (!MfcParameterReader.TryReadUniqueDoubleByWChar(
                raw, "D2", start, end, out _, out int d2Name, allowIdenticalDuplicates: true))
            return false;

        int typeOffset = d2Name - 0x2A2;
        if (typeOffset - 12 < 0 || typeOffset + 2 >= end) return false;

        bool framed = false;
        for (int q = typeOffset - 12; q <= typeOffset - 2; q++)
        {
            if (raw[q] != 0xFF || raw[q + 1] != 0xFE || raw[q + 2] != 0xFF ||
                raw[q + 3] != 0x00 || raw[q + 4] != 0x01)
                continue;
            bool padding = true;
            for (int k = q + 5; k < typeOffset; k++)
            {
                if (raw[k] != 0x00 && raw[k] != 0x01) { padding = false; break; }
            }
            if (padding) { framed = true; break; }
        }
        if (!framed) return false;

        uint apiType = raw[typeOffset] switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            _ => 0
        };
        if (apiType == 0) return false;

        data.BendAllowanceType = apiType;
        data.ParameterResolution.BendAllowanceType = true;

        if (apiType == 2) return true;
        if (!MfcParameterReader.TryReadUniqueDoubleByWChar(
                raw, "D3", d2Name + 1, end, out double d3, out _, allowIdenticalDuplicates: true))
            return true;
        if (d3 <= 0) return true;
        if (apiType == 3)
        {
            data.BendAllowance = d3;
            data.ParameterResolution.BendAllowance = true;
        }
        else
        {
            data.BendDeduction = d3;
            data.ParameterResolution.BendDeduction = true;
        }
        return true;
    }

    internal static bool TryReadUseDefaultBendRelief(
        byte[] raw, int start, SheetMetalBaseFlangeModel data)
    {
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return false;
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;
        int dataStart = start + 6 + "moSheetMetal_c".Length;
        if (dataStart + 0x20 > raw.Length) return false;
        if (raw[dataStart + 0x18] != 0 || raw[dataStart + 0x19] != 0 ||
            raw[dataStart + 0x1A] != 0 || raw[dataStart + 0x1C] != 0xFF)
            return false;
        byte flags = raw[dataStart + 0x1B];
        if ((flags & ~0x60) != 0) return false;

        data.UseDefaultBendRelief = (flags & 0x40) != 0;
        data.ParameterResolution.UseDefaultBendRelief = true;
        return true;
    }

    internal static bool TryReadBendTableFile(
        byte[] raw, int start, int end, out string path)
    {
        path = string.Empty;
        if (raw == null || raw.Length < 2) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x155 && version != 0x1AA && version != 0xC7) return false;
        start = Math.Max(0, start);
        end = Math.Min(end, raw.Length);
        if (!IsMfcTagNamed(raw, start, "moSheetMetal_c")) return false;

        return TryFindUniqueTablePath(raw, start, end, "Bend Tables", new[] { ".xls", ".xlsx", ".csv" }, out path);
    }

    private static int FindLastMfcTag(byte[] raw, int start, int end, string name)
    {
        int found = -1;
        int last = Math.Min(end, raw.Length) - (6 + name.Length);
        for (int p = Math.Max(0, start); p <= last; p++)
            if (IsMfcTagNamed(raw, p, name)) found = p;
        return found;
    }

    private static bool IsMfcTagNamed(byte[] raw, int tagOffset, string name)
    {
        if (tagOffset < 0 || tagOffset + 6 + name.Length > raw.Length) return false;
        if (raw[tagOffset] != 0xFF || raw[tagOffset + 1] != 0xFF ||
            raw[tagOffset + 2] != 0x01 || raw[tagOffset + 3] != 0x00)
            return false;
        int length = raw[tagOffset + 4] | (raw[tagOffset + 5] << 8);
        if (length != name.Length) return false;
        for (int i = 0; i < name.Length; i++)
            if (raw[tagOffset + 6 + i] != (byte)name[i]) return false;
        return true;
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Validation and (future) stream emitter for sheet metal base flanges.
/// Reading is established for thickness, radius, depth, direction 2, reverse flags,
/// K-factor, relief ratio/type and the template dimension records. The write side is
/// deliberately not enabled yet: no controlled sample proves the insertion ordering,
/// compact class references, flat-pattern template and per-body sheet metal blocks
/// required for a complete synthesized feature. Rejecting the operation here is
/// preferable to emitting a partially understood template that SolidWorks would
/// silently repair or discard.
/// </summary>
internal static class SheetMetalFeatureBuilder
{
    /// <summary>
    /// Validates a definition for authoring. Values that have no evidenced
    /// stream layout are rejected instead of being silently dropped.
    /// </summary>
    internal static void ValidateDefinition(SheetMetalBaseFlangeModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (double.IsNaN(def.Thickness) || double.IsInfinity(def.Thickness) || def.Thickness <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Thickness),
                $"Sheet metal thickness must be a positive finite number, but received {def.Thickness}.");
        }

        if (double.IsNaN(def.BendRadius) || double.IsInfinity(def.BendRadius) || def.BendRadius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.BendRadius),
                $"Bend radius must be a positive finite number, but received {def.BendRadius}.");
        }

        if (double.IsNaN(def.Depth) || double.IsInfinity(def.Depth) || def.Depth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Depth),
                $"Base flange depth must be a positive finite number, but received {def.Depth}.");
        }

        // Authoring still accepts only the untouched option set. Several options
        // are now decoded for reading (K, allowance type/value, relief ratio/type
        // and default flag, gauge table, reverse flags, direction 2), but none has
        // a proven write/insertion layout yet.
        RejectUnsupported(def.ReverseThickness, nameof(def.ReverseThickness));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(!def.UseDefaultBendRadius, nameof(def.UseDefaultBendRadius));
        RejectUnsupported(def.KFactor != 0.5, nameof(def.KFactor));
        RejectUnsupported(def.BendAllowanceType != 0, nameof(def.BendAllowanceType));
        RejectUnsupported(def.BendAllowance.HasValue, nameof(def.BendAllowance));
        RejectUnsupported(def.BendDeduction.HasValue, nameof(def.BendDeduction));
        // 0 is the unresolved/model sentinel, 1 is the evidenced Rectangular default.
        RejectUnsupported(def.AutoReliefType > 1, nameof(def.AutoReliefType));
        RejectUnsupported(def.ReliefRatio != 0.5, nameof(def.ReliefRatio));
        RejectUnsupported(def.ReliefWidth != 0, nameof(def.ReliefWidth));
        RejectUnsupported(def.ReliefDepth != 0, nameof(def.ReliefDepth));
        RejectUnsupported(!def.UseDefaultBendRelief, nameof(def.UseDefaultBendRelief));
        RejectUnsupported(def.UseGaugeTable, nameof(def.UseGaugeTable));
        RejectUnsupported(!string.IsNullOrEmpty(def.GaugeTablePath), nameof(def.GaugeTablePath));
        RejectUnsupported(!string.IsNullOrEmpty(def.ThicknessTableName), nameof(def.ThicknessTableName));
        RejectUnsupported(!string.IsNullOrEmpty(def.BendTableFile), nameof(def.BendTableFile));

        RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0,
            nameof(def.SelectedContours));
        RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));
    }

    /// <summary>
    /// Builds the modified ResolvedFeatures stream. The first writable combination
    /// is the measured open-L specimen: thickness, bend radius and depth are
    /// replaced in the hash-pinned measured subsystem; every other option and any
    /// uncontrolled sketch/version is rejected before the stream is touched.
    /// </summary>
    internal static byte[] BuildBaseFlangeStream(
        byte[] currentStream,
        SheetMetalBaseFlangeModel def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);
        return SheetMetalBaseFlangeBuilder.Build(
            currentStream, def, targetSketchFolder, sketchEndOffset, featureName);
    }

    private static void RejectUnsupported(bool unsupported, string property)
    {
        if (unsupported)
            throw new NotSupportedException(
                $"Sheet metal base flange authoring does not yet support {property}. No stream changes have been made.");
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SheetMetalTemplateParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Reads the observed revision-73/74 template dimensions within one template's
/// ownership window. Compact class references make global class-name searches
/// unsuitable when another feature has already declared the dimension classes.
/// </summary>
internal static class SheetMetalTemplateParser
{
    internal static void Read(byte[] raw, int start, int end, SheetMetalBaseFlangeModel data)
    {
        if (raw.Length < 2) return;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x73 && version != 0x74) return;
        start = Math.Max(0, start);
        end = Math.Min(raw.Length, end);

        double? candidateRadius = null, candidateThickness = null;
        bool conflictingRadius = false, conflictingThickness = false;
        for (int p = start; p <= end - 38; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != 2 || raw[p + 4] != 'D' || raw[p + 5] != 0 || raw[p + 7] != 0)
                continue;
            var scalar = MfcParameterReader.ReadDoubleDescriptorAfterName(raw, p + 8);
            if (!scalar.HasValue) continue;

            if (raw[p + 6] == '1')
            {
                double radius = scalar.Value;
                if (PositiveFinite(radius) && p + 30 + 0xAE + 8 <= end &&
                    BitConverter.ToDouble(raw, p + 30 + 0xAE) == radius)
                {
                    conflictingRadius |= candidateRadius.HasValue && candidateRadius.Value != radius;
                    candidateRadius = radius;
                }
            }
            else if (raw[p + 6] == '3')
            {
                int thicknessOffset = p + 0x3CC;
                if (thicknessOffset + 8 > end || BitConverter.ToUInt32(raw, thicknessOffset - 12) != 2)
                    continue;
                bool reservedZero = true;
                for (int k = thicknessOffset - 8; k < thicknessOffset; k++)
                    reservedZero &= raw[k] == 0;
                var thickness = BitConverter.ToDouble(raw, thicknessOffset);
                if (reservedZero && PositiveFinite(thickness))
                {
                    conflictingThickness |= candidateThickness.HasValue && candidateThickness.Value != thickness;
                    candidateThickness = thickness;
                }
            }
        }

        if (candidateRadius.HasValue && !conflictingRadius)
        {
            data.BendRadius = candidateRadius.Value;
            data.ParameterResolution.BendRadius = true;
        }
        if (candidateThickness.HasValue && !conflictingThickness)
        {
            data.Thickness = candidateThickness.Value;
            data.ParameterResolution.Thickness = true;
        }
        if (MfcParameterReader.TryReadUniqueDoubleByWChar(
                raw, "D6", start, end, out double ratio, out _,
                allowIdenticalDuplicates: true, onlyPositive: true, allowZero: true))
        {
            data.ReliefRatio = ratio;
            data.ParameterResolution.ReliefRatio = true;
        }
    }

    internal static bool PositiveFinite(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);

    internal static bool FiniteNonNegative(double value) => value >= 0 && !double.IsInfinity(value) && !double.IsNaN(value);

    internal static bool IsScalarDescriptor(byte[] raw, int p) =>
        MfcParameterReader.ReadDoubleDescriptorAfterName(raw, p).HasValue;
}


