using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Fixed inline FlatPattern 0x66 template from sm_base_flange_after_fillet
/// (ResolvedFeatures 0x74, folder count 25, 3900-byte suffix from the class tag).
/// The suffix slots are patched for MergeFace/CornerTreatment, SimplifyBends,
/// BreakCornerType/Radius, CornerTrimReliefType and the shared distance/ratio
/// double, UseRatioToThickness and ShowSlitInCornerRelief.
/// </summary>
internal static class FlatPatternInline066AuthoringSegments
{
    internal const string Name = "FlatPatternInline066AfterFillet";
    internal const string ClassName = "moFlatPattern_c";
    internal const string DisplayName = "平板型式7";
    internal const int PrefixLength = 0x9B02;      // 39682
    internal const ushort StreamVersion = 0x74;
    internal const ushort FolderCount = 25;
    internal const int SuffixLength = 3900;
    internal const string NormalizedPrefixSha256 =
        "F615C7B30F845EE2D0261BBA283505F1EE3CCC5F2A57820C652F2FCB0C04C3D7";
    internal const string SuffixSha256 =
        "1E81FEF1B11531F191A633AA2DB96812593C54ACC3E623AFE7C34CC553017ABE";

    internal const int FlagsOffset = 0xDF7;
    internal const int TrimTypeOffset = 0xE0D;
    internal const int TrimOffset = 0xE11;
    internal const int BreakTypeOffset = 0xE19;
    internal const int BreakRadiusOffset = 0xE1D;
    internal const int UseRatioOffset = 0xE29;
    internal const int SimplifyOffset = 0xE2D;
    internal const int SlitOffset = 0xE31;

    private const string SuffixGzipBase64 = "H4sIAAAAAAACCvv/n5GBnyE33y0nsSQgsaQktSgvPpml4f+//6zFcfXp3eH88eYMjAwQkAalgbJQFjPD2QaI6hlJ+vG6DPvrjKAyDA4pGKpRQSqIUEQSsPBCsHWey8G0/gdi5v9QwMDQsJ8BBwDJS2fcZbw/I3EyE8PupaxwhyPA/gYGKoIMDNMYwZCBLMyARMMCDuJjkM8YoT7E5RZGstyLCzCjmrigDcI50qDTwER8+PxHA9jUgGIdLNerzrAR4T4HmHq8yUeByskHxsaVfEgJfEYGagMWFN7xRuJ0fbBHcxFCAAoaCGmE8veTaiERofYXFOp4go8ujgWl9p/Q6E9FUwAt4Fh29vuUumUgirdUHMUbI62LtyN7hmvxxkhiDiOteGMcesUbw4gq3hhHizf6F2+gnCcFagDm56SEFrjkl+cFZ6eWJGeA+KlFsLagZ/O/sJz+pGRDmLsdMohp3SnRoPj7+isZS/EH8oYsFm9EpOUX5bokliTGJzNg8bk0iha31MQSHBoYae0txqfJkwdVtkQ3aGcjTcrGbHBxhAn8odY5QJRkU8k6hk8NMI+xQUpChhROBobaw1NU13puCoHRDAxfgBnxW8NPhnSgoomcT7Jguv6iibNgEWPGIsaERYwRZwFPO/8LMqFGLnq3olpknfvDqhJ7dDYDUk1eQ5tqMgMaLLSqX0hpZiPjbCx+AbEBB1blEjwPAAA=";

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
            throw new InvalidOperationException("Measured 0x66 flat-pattern suffix length mismatch.");
        string hash;
        using (var sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        if (!string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured 0x66 flat-pattern suffix hash mismatch.");
        return bytes;
    }
}
