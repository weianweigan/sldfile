using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

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

    private const string SuffixGzipBase64 =
        "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" +
        "riTJLhvpDW0P8FfZ4DsqWm979uJDZCv5f4fNNs7ZpPzlRlbwaot1+U/Iqq+VdQLvnovLmPRjxvNE3631cdXYYNOipPwppSpDx0yc" +
        "UXX4RL7KtSb5hWjPqburswt6nZyTQoopiTy0v8eQ0CwNpT8ulaPPHkp92N/7+CbMxk+1pEllQSPG3R+52mqkMjZgjWa0G8k86/2A" +
        "usWOA8MeOi6NWzBV3cQ5yl/T2aRaaYvzmITwGSvmPpQrPv/6xDZi049F38fvP7v0/tMfUX1mfXq/Ys2OAnXDfcejEmmPqAvSXwce" +
        "cen5IdxJ6bRLmKrmhqrcAbmTrXbZnsQ0qsuvlgvR+0R+mLviygWFp9npbpcOaOFX0Qxij2gmLQyV6f+m+tSFnwein+w6F6ZgI6dG" +
        "HRJ9ZMfXWc//nqqZ7rl+emRmWE8jSycfW+OM+r3Gi8cq/tprbDNcz8Hj9cRTx21/KVm7bsi0/tlriv6liftUn4dkatp5bz+ouuly" +
        "mpWm5uPS69lbqi/evyobfc+nmOLiUK8Wc1TOKdbv2a7CnMbplX7ZJVkv5RpHhevO21cadSG2eGUNJuq56Yw50/pWZRm+joo+13NU" +
        "hZap/uTk0WM/xAbdUVBbjHZVfzbi/B6LHv7pd9W9zCzOvPf9AupkZmSYbyHIJnxWjgCGCjY5hp9ODW2YWAGpBT5RLfcalTNePcLs" +
        "Ll72kq+7OcbSqUYrNiETq6o1HrMVZ1BrMlB7bEnj0HDaJMLp+bfDFYueov8y/FZYF549XyWnAaW8ZCYqRD5LJ9ZutM9GHG4J1Mvk" +
        "ylFTgx4KlL21RdbqdqZKJVovLp7JT3orezkq+NDNp+7zif4J6tejzO+EA/mmxaBmFjUwdeSlAZHfc5Ryjkb+GrVcbd2kT8QxVL3I" +
        "ATeS4q78Myn58feTaqU/HnmMmXrY+8ItWtivl7d6v/v7wEKj/qe1rzvYyTz7hZoSd1XTLbY0D1/1P2LYOMMVtUaPzkRfn9A3sh+q" +
        "eNTWNPvtwaDueW2sign1Q/VPVNoRvZsTz0UaTDECtJiQjQ69KgySfkykfd0fcfbTeVMXXZfAsR8SIkdRR2DQP6ZNevNSPQG43J5Q" +
        "l54/O7P+vKblbfW6YasP78p/9Cx9XulJZ90Vr3xfLIxJWxrRNFc1K42QQ+v9Wr+yh1uGfrWibNPwsGfffBUyYvdMTs+f6UEePy7d" +
        "oDZnb9Ccv6hFvpsH73sqH24+/38plSB19Jewt5OubNN3yttfLGf90dS0lnD8naJ9wt3VAVlP62TOnE2vHDzONtPm56HJu8vrC8uX" +
        "nZixYLfLbbKuluxc18Pvn88u2HzklGfC4E/jH9iGhqX46UY46tmvzsu/WJFnP9xp77WBu1126zyan/JndvCw6TuTNJR2fR/qoo3f" +
        "tL8wY/MA50q1ozeCPubWKXiNeGx7Omtx3tI+Y8YbvezlG1xhfXP3rJyocVGRcRkXr7w/cmi/A1Y3oFZ5TVzsTauYgqoDbz+XGw7y" +
        "uAmttYrJn7tg37c/+327XURc/nhzxP4s0iq1O9fy932K/mEduE9xde5dgtqAp5mLfO2tPE9FqZ2xqPkQ8mdwD9zKprF+waq/Ms4N" +
        "RTf+NSV2p+nHQq2KN8UHL5yM/mPPx+KL5BNeQzU2xg+7P3OEQVLVgui+xUbklWVLiKOMkgbXb1efULqjx+ZkrQA1Q6OAF1QvlJoa" +
        "tLDS+arjsfEGmVMb3M4U3VyR2MNjR1Fsfm2RdaBrrG7yHHlyUdhLM3DN/PPdvL/LfLXW/vnX/nvbicFpe6DRRiErzWLjoszOptdi" +
        "thFLD3Kus9grQNDOK8Cj9L0+azMjI2ozAy9kHXAEfzz/GtqNYz3MuxYGTLogJF3Y2pk3f3OLZE5qzsbMNlPxnpL0LDJ9kvtGn/V5" +
        "elPk8VMgVaAtjdejm/OpcI78/wEG9ylZg50AAA==";

    private static readonly Lazy<byte[]> Suffix = new(Decompress, isThreadSafe: true);

    internal static byte[] GetSuffix() => (byte[])Suffix.Value.Clone();

    private static byte[] Decompress()
    {
        byte[] gzip = Convert.FromBase64String(SuffixGzipBase64);
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