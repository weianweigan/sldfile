using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured position-offset edge-flange subsystems
/// (FlangeOptions/edge_flange_position_offset and _offset10, version 0x74).
/// <para>
/// The prefix is the base-flange stream before the edge-selection wrapper
/// (0x525A). The suffix starts at the wrapper and contains the position-offset
/// parameter block, the edge flange and the downstream sheet-metal objects.
/// Prefix + matching suffix reconstructs each native sample byte-for-byte.
/// </para>
/// </summary>
internal static class EdgeFlangeOffsetAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x525A;
    internal const ushort MeasuredFullFolderCount = 24;
    internal const ushort SupportedStreamVersion = 0x74;

    internal const double MeasuredOffsetDistance = 0.01;
    internal const double MeasuredBendAngle = Math.PI / 2.0;
    internal const double MeasuredBendRadius = 0.002;
    internal const double MeasuredReliefWidth = 0.001;
    internal const double MeasuredReliefDepth = 0.001;

    internal const double LowPositionOffsetDistance = 0.005;
    internal const double HighPositionOffsetDistance = 0.010;

    internal const string LowPrefixSha256 =
        "C50C4A29FD80835392B43ADEE30C262750597A894940B9DF556104090B0E906F";
    internal const string HighPrefixSha256 =
        "96F76B54D163EEB6357193C5324E60258DE2F7723334592808F2194943A42367";
    internal const string LowSuffixSha256 =
        "6164A696A33D9EC902643C87B8714A2D4140BFF1D35294D8C64204F9D21EC6C9";
    internal const string HighSuffixSha256 =
        "F502698722F5464DEB9579FD76028FBBF74B45CF58EE2B35F85D5523A0F900DC";

    private const string LowSuffixGzipBase64 =
        "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";

    private const string HighSuffixGzipBase64 =
        "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";

    private static readonly Lazy<byte[]> LowSuffix =
        new(() => Decompress(LowSuffixGzipBase64, LowSuffixSha256), isThreadSafe: true);
    private static readonly Lazy<byte[]> HighSuffix =
        new(() => Decompress(HighSuffixGzipBase64, HighSuffixSha256), isThreadSafe: true);

    internal static bool IsSupportedDistance(double distance)
        => distance == LowPositionOffsetDistance || distance == HighPositionOffsetDistance;

    internal static byte[] GetSuffix(double distance)
    {
        return distance == LowPositionOffsetDistance ? (byte[])LowSuffix.Value.Clone()
            : distance == HighPositionOffsetDistance ? (byte[])HighSuffix.Value.Clone()
            : throw new ArgumentOutOfRangeException(nameof(distance));
    }

    internal static string GetPrefixSha256(double distance)
        => distance == LowPositionOffsetDistance ? LowPrefixSha256
            : distance == HighPositionOffsetDistance ? HighPrefixSha256
            : throw new ArgumentOutOfRangeException(nameof(distance));

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
            throw new InvalidOperationException("Measured position-offset edge-flange suffix hash mismatch.");
        return bytes;
    }
}
