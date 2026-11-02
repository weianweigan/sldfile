using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

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

    private const string KFactorSuffixGzipBase64 = "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";
    private const string DirectSuffixGzipBase64 = "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";
    private const string DeductionSuffixGzipBase64 = "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";
    private const string GaugeTableSuffixGzipBase64 = "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";

    private static readonly Lazy<byte[]> KFactorSuffix =
        new(() => Decompress(KFactorSuffixGzipBase64, KFactorSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> DirectSuffix =
        new(() => Decompress(DirectSuffixGzipBase64, DirectSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> DeductionSuffix =
        new(() => Decompress(DeductionSuffixGzipBase64, DeductionSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> GaugeTableSuffix =
        new(() => Decompress(GaugeTableSuffixGzipBase64, GaugeTableSuffixSha256), isThreadSafe: true);

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

    private static byte[] Decompress(string base64, string expectedSha256)
    {
        byte[] gzip = Convert.FromBase64String(base64);
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
