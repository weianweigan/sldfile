using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured edge-flange subsystem for the controlled single-edge specimen
/// (FlangeOptions/edge_flange_90_len10.SLDPRT, ResolvedFeatures version 0x74).
/// <para>
/// The block starts at the edge-selection wrapper <c>sgExtEnt_c</c> (0x525A) and
/// runs to the end of the stream. It contains the moCompEdge_c/moEdgeRef_c
/// selection block, moAbsoluteRefPlnData_c, moEdgeFlange_c, moOneBend_c, the
/// angular/radius/scaler dimension records, moMultiEdgeFlangeDataPerEdge_c,
/// moLinearEdgeGeom_c, the flat-pattern template, per-body sheet metal data,
/// the flat pattern and fold objects. Prefix (sketch + base flange) + this suffix
/// reconstructs the native stream byte-for-byte. The block is gzip/base64 encoded
/// only to keep the source readable; its SHA-256 is verified before use. The
/// slots below were measured from the six FlangeOptions variants and their
/// resaves; derived geometry caches remain lazy and are expected to be
/// recomputed by the native rebuild.
/// </para>
/// </summary>
internal static class EdgeFlangeAuthoringSegments
{
    internal const int MeasuredPrefixLength = 0x525A;
    internal const string MeasuredPrefixSha256 =
        "3BCBEFE3F2AEA3E455D9EF1BC9761CBB9038852CF04A5A4F4E2F379A73DA0C02";
    internal const string MeasuredSuffixSha256 =
        "65FD9D42650ACDDC3185FD813330D3C6489724647843F5FFF11A04522F3F26EB";

    /// <summary>Full-sample ResolvedFeatures folder count at stream offset 4.</summary>
    internal const ushort MeasuredFullFolderCount = 24;

    /// <summary>Controlled sketch child-count marker (67 00 00 00 + u32) in the prefix.</summary>
    internal const int SketchChildCountMarkerOffset = 0x236F;
    internal const uint MeasuredSketchChildCount = 22;

    /// <summary>Measured values before any authoring patch.</summary>
    internal const double MeasuredBendAngle = Math.PI / 2.0;
    internal const double MeasuredBendRadius = 0.002;
    internal const double MeasuredOffsetDistance = 0.01;
    internal const double MeasuredReliefWidth = 0.001;
    internal const double MeasuredReliefDepth = 0.001;
    internal const uint MeasuredPositionType = 1;

    /// <summary>
    /// Proven authoring slots (relative to the suffix start). The angle slot is
    /// the named D4 value plus its observed copy; the radius slots are the
    /// EdgeFlange D1 value plus its immediate feature/display copies. Every slot
    /// is checked against the measured value before it is patched.
    /// </summary>
    internal static readonly int[] BendAngleSlots = { 0x1785, 0x199F };
    internal static readonly int[] BendRadiusSlots = { 0xEBD, 0xEED, 0xF05, 0xFD5, 0x10BF, 0x10D7 };

    /// <summary>
    /// OffsetDistance value-slots. The first four are display copies in the
    /// hash-pinned prefix (absolute prefix offsets, measured 0.01; the length-20
    /// native sample changes each to 0.02). The last is the named D7 value in the
    /// suffix (absolute 0x9DD2, measured 0.01; native length-20 = 0.02).
    /// Geometry/extent caches that also move with the length stay lazy.
    /// </summary>
    internal static readonly int[] PrefixOffsetDistanceSlots = { 0x4FF5, 0x5097, 0x5139, 0x51DB };
    internal static readonly int[] OffsetDistanceSlots = { 0x4B78 };

    /// <summary>
    /// PositionType (u32) in the anonymous relief/position block; measured 1,
    /// native pos_outer = 2 and pos_bend_outside = 3 at this slot.
    /// </summary>
    internal const int PositionTypeSlot = 0x488D;

    /// <summary>
    /// ReliefWidth/ReliefDepth doubles in the anonymous relief block (absolute
    /// 0x9ACF/0x9AD7). D8/D9 are display copies of the relief depth (absolute
    /// 0x76C1/0x7C9E, measured 0.001; relief_custom = 0.0025). The width has no
    /// named display copy.
    /// </summary>
    internal const int ReliefWidthSlot = 0x4875;
    internal const int ReliefDepthSlot = 0x487D;
    internal static readonly int[] ReliefDepthDisplaySlots = { 0x2467, 0x2A44 };

    /// <summary>
    /// Radius/angle-derived copies and bbox caches that are intentionally left at
    /// the measured values (lazy). Native rebuild is expected to recompute them.
    /// </summary>
    internal static readonly int[] LazyRadiusCaches =
        { 0x17B5, 0x17E5, 0x1842, 0x18AD, 0x18E5, 0x1B68, 0x1B78, 0x31B1 };

    private const string SuffixGzipBase64 =
        "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" +
        "3/8DWCXV3HKxAAA=";

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
        if (!string.Equals(hash, MeasuredSuffixSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Measured edge-flange suffix hash mismatch.");
        return bytes;
    }
}