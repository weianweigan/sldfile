using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed measured template for the fresh single-edge EdgeFlange layout created
/// by InsertSheetMetalEdgeFlange2 on a simple L-shaped base flange
/// (ResolvedFeatures 0x74, FolderHeader TypeId 0x75, one base flange plus one
/// edge flange). The suffix slots are patched for the fields that persisted in
/// the native single-variable save/reopen matrix (2026-10-31 edgepersist).
/// </summary>
internal static class FreshEdgeFlangeAuthoringSegments
{
    internal const string Name = "FreshSingleEdgeFlange";
    internal const string ClassName = "moEdgeFlange_c";
    internal const string DisplayName = "边线-法兰2";
    internal const int PrefixLength = 0x804B;
    internal const ushort StreamVersion = 0x74;
    internal const ushort FolderCount = 24;
    internal const int SuffixLength = 43552;
    internal const string NormalizedPrefixSha256 =
        "A0282F0B6E85DE9E8E7529110B08B37D90CAB586334EF25BC09F1BA7CB6D9E2D";
    internal const string SuffixSha256 =
        "61A60204FD8F1CEE2A5FE6CC298408B32529A37A4351E9E2BE268570526F426B";

    // Slots relative to the moEdgeFlange_c tag (start of the suffix).
    internal const int RadiusOffset = 0x431;                    // named D1 value
    internal const int UseDefaultBendRadiusOffset = 0x8BB;      // D1 name + 0x4A8
    internal const int ReliefWidthOffset = 0x3D8B;               // relief block + 0x08
    internal const int ReliefDepthOffset = 0x3D93;               // relief block + 0x10
    internal const int ReliefRatioOffset = 0x3D83;               // relief block + 0x00
    internal const int UseReliefRatioOffset = 0x3D9B;            // relief block + 0x18
    internal const int PositionTypeOffset = 0x3DA3;              // relief block + 0x20
    internal const int UseDefaultBendReliefOffset = 0x3DCB;      // relief block + 0x48
    internal const int PositionOffsetDistanceOffset = 0x3A07;    // named D6 value
    internal const int UsePositionOffsetOffset = 0x3A6F;         // D6 name + 0x86
    internal const int OffsetDistanceOffset = 0x408E;            // named D7 value

    // Position-offset template (native edgepersist_position_offset_2 save).
    internal const int PositionPrefixLength = 33143;             // 0x8177
    internal const int PositionSuffixLength = 43706;
    internal const string PositionNormalizedPrefixSha256 =
        "56F7762F4502C9E38ADCF96DF349C50B5497CE5B02DA3789614CEB0FE857648D";
    internal const string PositionSuffixSha256 =
        "C124DF9E36C0DFB4AAA479F32945E460194BB3670A7E48AA3AD2EA7943329500";
    internal const int PositionRadiusOffset = 0x431;
    internal const int PositionUseDefaultBendRadiusOffset = 0x8BB;
    internal const int PositionReliefWidthOffset = 0x3E25;
    internal const int PositionReliefDepthOffset = 0x3E2D;
    internal const int PositionReliefRatioOffset = 0x3E1D;
    internal const int PositionUseReliefRatioOffset = 0x3E35;
    internal const int PositionTemplateTypeOffset = 0x3E3D;
    internal const int PositionUseDefaultBendReliefOffset = 0x3E65;
    internal const int PositionUsePositionOffsetOffset = 0x3A6F;
    internal const int PositionOffsetDistanceD7Offset = 0x4128;

    // Radius template (native edgepersist_radius_003 save).
    internal const int RadiusPrefixLength = 33115;               // 0x815B
    internal const int RadiusSuffixLength = 43552;
    internal const string RadiusNormalizedPrefixSha256 =
        "1EF796A3AF7EAEA5FB0A56844F076A6E385C89704B56B478DAC1875653023F18";
    internal const string RadiusSuffixSha256 =
        "10BB686C7414BACB621DE18B7EEC7FD10572A1612B3180D5A57DD8BF7D20DA2B";

    private const string SuffixGzipBase64 = "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";

    private const string PositionSuffixGzipBase64 = "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";

    private const string RadiusSuffixGzipBase64 = "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";

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBase64, SuffixSha256, SuffixLength), isThreadSafe: true);
    private static readonly Lazy<byte[]> PositionSuffix =
        new(() => Decompress(PositionSuffixGzipBase64, PositionSuffixSha256, PositionSuffixLength),
            isThreadSafe: true);
    private static readonly Lazy<byte[]> RadiusSuffix =
        new(() => Decompress(RadiusSuffixGzipBase64, RadiusSuffixSha256, RadiusSuffixLength),
            isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    internal static byte[] GetPositionSuffix() => (byte[])PositionSuffix.Value.Clone();

    internal static byte[] GetRadiusSuffix() => (byte[])RadiusSuffix.Value.Clone();

    private static byte[] Decompress(string base64, string expectedSha256, int expectedLength)
    {
        byte[] gzip = Convert.FromBase64String(base64);
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (bytes.Length != expectedLength)
            throw new InvalidOperationException("Measured fresh EdgeFlange suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured fresh EdgeFlange suffix hash mismatch.");
        return bytes;
    }
}
