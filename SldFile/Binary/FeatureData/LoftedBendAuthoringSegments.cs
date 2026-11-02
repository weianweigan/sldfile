using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured fixed LoftedBend block from the controlled 0x72 specimen
/// (LoftedBendOptions/lofted_bend_2lines.SLDPRT, version 0x74).
/// <para>
/// The prefix runs to the moLoftedBend_c class tag (0x6043) and keeps the base
/// flange and profile context. The suffix starts at that tag and runs to the
/// stream end. Prefix + suffix reconstructs the native stream byte-for-byte.
/// The normalized prefix hash zeroes the folder count at offset 4.
/// </para>
/// </summary>
internal static class LoftedBendAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x6043;
    internal const ushort SupportedStreamVersion = 0x74;
    internal const ushort MeasuredFullFolderCount = 26;
    internal const int MarkerOffset = 0x6033;
    internal const uint MeasuredMarkerValue = 19;
    internal const string MeasuredDisplayName = "LoftedBend";

    internal const double MeasuredThickness = 0.001;
    internal const int MeasuredNumberOfBendLines = 2;

    internal const string MeasuredNormalizedPrefixSha256 =
        "A94F196BD56D6F0014146C5F527C501600AE3EE6D66CD9B8DFC58ABFD5B597DD";
    internal const string MeasuredSuffixSha256 =
        "FB76CA2461DF51899797C161FD607703583E359AB293E2CA02B6437A2A1499D3";

    private const string SuffixGzipBase64 =
        "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";

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
            throw new InvalidOperationException("Measured LoftedBend suffix hash mismatch.");
        return bytes;
    }
}
