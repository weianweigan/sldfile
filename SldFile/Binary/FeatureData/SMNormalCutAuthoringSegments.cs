using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed template for the first controlled cross-bend SMNormalCut sample
/// (ResolvedFeatures 0x75, FolderHeader TypeId 0x87). The suffix begins at the
/// moSMNormalCut_c tag and includes the following flat-pattern data.
/// </summary>
internal static class SMNormalCutAuthoringSegments
{
    internal const string Name = "SMNormalCutCrossBend";
    internal const string ClassName = "moSMNormalCut_c";
    internal const string DisplayName = "SMNormalCut";
    internal const int PrefixLength = 0xE81B;      // 59419
    internal const ushort StreamVersion = 0x75;
    internal const ushort FolderCount = 27;
    internal const int SuffixLength = 39509;
    internal const string NormalizedPrefixSha256 =
        "A1DD786FE4DB68D877D9A40BADB864FFBFAEDB5EDEA64ADB848A97BB7B819485";
    internal const string SuffixSha256 =
        "7F4A90224E8633351EE37C40ED75E45D1FD9729B44FB36810A8DCDB5512492F3";

    private const string SuffixGzipBase64 = "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";

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBase64, SuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress(string base64, string expectedSha256)
    {
        byte[] gzip = Convert.FromBase64String(base64);
        using var input = new MemoryStream(gzip, writable: false);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        byte[] bytes = output.ToArray();
        if (bytes.Length != SuffixLength)
            throw new InvalidOperationException("Measured SMNormalCut suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured SMNormalCut suffix hash mismatch.");
        return bytes;
    }
}
