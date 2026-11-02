using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured flat-pattern feature block from the controlled 0x5D specimen
/// (sm_base_flange_10mm.SLDPRT, ResolvedFeatures version 0x73).
/// <para>
/// The prefix runs from the stream start to the moFlatPattern_c class tag
/// (0x7380) and keeps the base flange, sheet-metal template and flat-pattern
/// template context. The suffix starts at that tag and runs to the stream end.
/// Prefix + suffix reconstructs the native stream byte-for-byte. The normalized
/// prefix hash zeroes the folder-count field at offset 4 before hashing, matching
/// the other task builders.
/// </para>
/// </summary>
internal static class FlatPatternAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x7380;
    internal const ushort SupportedStreamVersion = 0x73;
    internal const ushort MeasuredFullFolderCount = 22;
    internal const int MarkerOffset = 0x2281;
    internal const uint MeasuredMarkerValue = 22;
    internal const string MeasuredDisplayName = "平板型式6";

    /// <summary>Option-block anchor relative to the suffix start (moFaceRef_c.DataEnd).</summary>
    internal const int OptionAnchorSuffixOffset = 0xEA7;

    internal const string MeasuredNormalizedPrefixSha256 =
        "4B9BFDEAEA285DF315D03E8672A69AA33EA8719B2E55EAFB2F04BAFBEECB3E72";
    internal const string MeasuredSuffixSha256 =
        "77D7847BDD282A873DF46B10C0F102BE5FFACE41716CB14AA14911C303602890";

    private const string SuffixGzipBase64 =
        "H4sIAAAAAAAACvv/n5GBnyE33y0nsSQgsaQktSgvPpml4f+//6zFcfXp3eH88WYMjAwQEAul///7D2UxMxxugKiekaQfr8uwv84IKsPgEI2hGhWkgghFJAELLwRb57kcTOv//wwMzP+hgIGhYT9W00Cq////vyLtLuPF5MbLTAy7l7LCHY4AWxsYqAjSMUxjBEMGsjBEP5J//v0He75hP8hnIBlICGAHjGS5FxdgRjVxQRuEs6tBp4GJWDMg7kUG2NSAYh0s16vOsBzhPgeYerzJRwHNIEqTD4yNK/ngAthUEq+bWMCCwlNsJE7XB3s0FyEEoABHqkBX9wFn0OHXhxOA3PMXFOp4go8ujgWl9p/Q6AenKyQALeBYdvb7lLplIIq3GCgDPX3C/DJavJFcvOHLMZQXb4xDr3hDN2h4F2+oJo4Wb3Qp3kA5TwrUAMzPSQktcMkvzwvOTi1JzgDxU4tgbUHP5n9hOf1JyYawSHKIh+nHl36VaNC6++XbhKV1B/KGLBZvRKTlF+W6JJYkxidjGsjIII2ixS01sQSHBsaB8dYAZkt0g4QbaVI2ZoGLI2z6GRl4GHLznfNzC9wSk1PjkyHWO0C0ZFHJerA93KBUkJicGpSaFp8M8zcbiGBiYDjrxMBQe3iK6lrPTSEwGqJNhCE33zUvBaQzuLQozTMlKLUAlGpAcmwgI4Pik0FsXpC6ihL/pKzU5BKIkCDIZ8ElRZl56R6JeSk5qfHJoGRT0vj/33/mb9O++VmArO6Z/TALXEtCMSoA5er///4z3Zi3pBvda3EMDAy9sx9mwTwDslMc5KSi/Nzg7BLXvBIUFxc0FjPCtIAameWNqGLMWMSYsIihphnkrMHAkNlImwjMBVuKsBm9n1Qtss79YVWJPTobphaEq2lT74PKyL7ZD6FepX6FSSwARSkyRg57ZD8AAO+eTNMNEAAA";

    private static readonly Lazy<byte[]> Suffix =
        new(() => Decompress(SuffixGzipBase64, MeasuredSuffixSha256), isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

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
            throw new InvalidOperationException("Measured flat-pattern suffix hash mismatch.");
        return bytes;
    }
}
