using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Measured reading support for legacy tutorial samples. These classes were found
/// in the SOLIDWORKS 2026 public tutorial models with stream versions from 0x006D
/// to 0x0334, where the FolderHeader payload is much older than the 0x70/0x71/0x72
/// layouts used by the controlled 2026 samples.
/// <para>
/// Only the serialized class name and the FolderHeader TypeId are promoted for
/// Gusset, Combine, Split and SurfaceTrim. Rib additionally promotes Thickness
/// because every measured variant shares one exact local anchor:
/// <c>2.0 double, 8 marker bytes, 6 marker bytes, thickness double</c>. No other
/// field is guessed; options without a controlled variation stay unresolved.
/// </para>
/// </summary>
internal static class LegacyTutorialFeatureParsers
{
    public const string RibClassName = "moRib_c";
    public const string GussetClassName = "moGusset_c";
    public const string CombineBodiesClassName = "moCombineBodies_c";
    public const string SplitClassName = "moSplit_c";
    public const string SplitBodyClassName = "moSplitBody_c";
    public const string TrimRefSurfaceClassName = "moTrimRefSurface_c";

    private static readonly HashSet<string> SupportedClasses = new(StringComparer.Ordinal)
    {
        RibClassName,
        GussetClassName,
        CombineBodiesClassName,
        SplitClassName,
        SplitBodyClassName,
        TrimRefSurfaceClassName
    };

    private static readonly byte[] RibThicknessMarkerA =
        { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 };

    private static readonly byte[] RibThicknessMarkerB =
        { 0xFF, 0xFE, 0xFF, 0x00, 0x00, 0x00 };

    public static bool IsSupported(string className) => SupportedClasses.Contains(className);

    public static IFeatureData? ParseRib(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (!TryReadHeader(doc, className, typeId, start, out _, out var header))
            return null;

        var data = new RibFeatureDefinitionData
        {
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        double? thickness = FindRibThickness(
            doc.SourceData, start, FindFeatureBoundary(doc, start, end));
        if (thickness.HasValue)
        {
            data.Thickness = thickness;
            data.Resolution.Thickness = true;
        }

        if (TryReadRibOptions(doc, start, end, out var options))
        {
            data.IsTwoSided = options.IsTwoSided;
            data.ReverseThicknessDir = options.ReverseThicknessDir;
            data.EnableDraft = options.EnableDraft;
            data.DraftOutward = options.DraftOutward;
            data.DraftFromWall = options.DraftFromWall;
            data.ExtrusionDirection = options.ExtrusionDirection;
            data.DraftAngle = options.DraftAngle;
            data.RefSketchIndex = options.RefSketchIndex;
            data.FlipSide = options.FlipSide;
            data.Resolution.IsTwoSided = true;
            data.Resolution.ReverseThicknessDir = true;
            data.Resolution.EnableDraft = true;
            data.Resolution.DraftOutward = true;
            data.Resolution.DraftFromWall = true;
            data.Resolution.ExtrusionDirection = true;
            data.Resolution.DraftAngle = options.DraftAngle.HasValue || !options.EnableDraft;
            data.Resolution.RefSketchIndex = true;
            data.Resolution.FlipSide = true;
        }

        return data;
    }

    private readonly struct RibOptions
    {
        internal RibOptions(bool isTwoSided, bool reverseThicknessDir, bool enableDraft,
            bool draftOutward, bool draftFromWall, int extrusionDirection, double? draftAngle,
            int refSketchIndex, bool flipSide)
        {
            IsTwoSided = isTwoSided;
            ReverseThicknessDir = reverseThicknessDir;
            EnableDraft = enableDraft;
            DraftOutward = draftOutward;
            DraftFromWall = draftFromWall;
            ExtrusionDirection = extrusionDirection;
            DraftAngle = draftAngle;
            RefSketchIndex = refSketchIndex;
            FlipSide = flipSide;
        }

        internal bool IsTwoSided { get; }
        internal bool ReverseThicknessDir { get; }
        internal bool EnableDraft { get; }
        internal bool DraftOutward { get; }
        internal bool DraftFromWall { get; }
        internal int ExtrusionDirection { get; }
        internal double? DraftAngle { get; }
        internal int RefSketchIndex { get; }
        internal bool FlipSide { get; }
    }

    /// <summary>
    /// Reads the measured Rib option bitfield used by the 0x6C official block20 layout.
    /// The local signature is pinned before any bit is promoted:
    /// class tag + 0x140..0x14D all zero, option bytes at 0x14E/0x14F, then
    /// 00 00 03 00 00 00 03 81. Known bits are 0x01 norm-to-sketch,
    /// 0x02 two-sided, 0x04 reverse-thickness, 0x10 flip-side, 0x80 draft on;
    /// 0x01 draft-outward and 0x08 draft-from-wall in the second byte.
    /// The ref-sketch byte at 0x152 is 3-index (3,2,1 -> 0,1,2). Unknown bits keep
    /// options null. Draft angle is read from the first following
    /// moDisplayAngularDim_c at +0x300.
    /// </summary>
    private static bool TryReadRibOptions(
        MfcDocument doc, int start, int end, out RibOptions options)
    {
        options = default;
        byte[] raw = doc.SourceData;
        if (start < 0 || start + 0x158 > raw.Length) return false;
        if (BitConverter.ToUInt16(raw, 0) != 0x006C) return false;
        for (int i = 0x140; i <= 0x14D; i++)
            if (raw[start + i] != 0) return false;
        byte a = raw[start + 0x14E];
        byte b = raw[start + 0x14F];
        byte refSketch = raw[start + 0x152];
        if ((a & ~0x97) != 0 || (b & ~0x09) != 0) return false;
        if (refSketch < 1 || refSketch > 3) return false;
        if (raw[start + 0x150] != 0 || raw[start + 0x151] != 0 ||
            raw[start + 0x153] != 0 ||
            raw[start + 0x154] != 0 || raw[start + 0x155] != 0 ||
            raw[start + 0x156] != 0x03 || raw[start + 0x157] != 0x81)
            return false;

        bool enableDraft = (a & 0x80) != 0;
        double? draftAngle = null;
        if (enableDraft)
        {
            var display = doc.AllElements.FirstOrDefault(e =>
                e.ClassName == "moDisplayAngularDim_c" &&
                e.TagOffset > start && e.TagOffset < Math.Min(end, raw.Length));
            if (display != null)
            {
                int p = display.TagOffset + 0x300;
                if (p + 8 <= raw.Length)
                {
                    double value = BitConverter.ToDouble(raw, p);
                    if (!double.IsNaN(value) && !double.IsInfinity(value) &&
                        value > 0 && value < Math.PI / 2)
                        draftAngle = value;
                }
            }
        }
        else
        {
            draftAngle = 0;
        }

        options = new RibOptions(
            (a & 0x02) != 0,
            (a & 0x04) != 0,
            enableDraft,
            (b & 0x01) != 0,
            (b & 0x08) != 0,
            (a & 0x01) != 0 ? 1 : 0,
            draftAngle,
            3 - refSketch,
            (a & 0x10) != 0);
        return true;
    }

    public static IFeatureData? ParseGusset(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (!TryReadHeader(doc, className, typeId, start, out _, out var header))
            return null;

        var data = new GussetFeatureDefinitionData
        {
            SerializedClassName = className,
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        if (TryReadGussetParameters(doc, start, end,
            out double? thickness, out int? profileType, out bool? useAngle))
        {
            if (thickness.HasValue)
            {
                data.Thickness = thickness;
                data.Resolution.Thickness = true;
            }
            if (profileType.HasValue)
            {
                data.ProfileType = profileType;
                data.Resolution.ProfileType = true;
            }
            if (useAngle.HasValue)
            {
                data.UseAngle = useAngle;
                data.Resolution.UseAngle = true;
            }
        }
        return data;
    }

    private static readonly byte[] GussetD1Anchor =
        { 0x04, 0x80, 0xFF, 0xFE, 0xFF, 0x02, 0x44, 0x00, 0x31, 0x00 };

    /// <summary>
    /// Reads the measured Gusset thickness and the profile/use-angle flag from the
    /// class-tagged 0x02A0 weldment Gusset layout. The anchor is the "D1" dimension
    /// handle <c>04 80 FF FE FF 02 44 00 31 00</c>. Thickness is the first
    /// <c>FF FE FF 00 00 00</c> payload double in (0, 0.1]. The flag byte at
    /// anchor + 0x2046 is measured as 0 triangle+angle, 1 polygon+angle,
    /// 2 triangle+use-length. Unknown flag values keep both fields unresolved.
    /// Profile location is not promoted because the class-tagged location variants
    /// did not rebuild natively.
    /// </summary>
    private static bool TryReadGussetParameters(
        MfcDocument doc, int start, int end,
        out double? thickness, out int? profileType, out bool? useAngle)
    {
        thickness = null;
        profileType = null;
        useAngle = null;
        byte[] raw = doc.SourceData;
        int limit = Math.Min(end, raw.Length);
        if (start < 0 || start + GussetD1Anchor.Length > limit) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x029D && version != 0x02A0) return false;

        int anchor = -1;
        for (int i = start; i + GussetD1Anchor.Length <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < GussetD1Anchor.Length; j++)
                if (raw[i + j] != GussetD1Anchor[j]) { match = false; break; }
            if (match) { anchor = i; break; }
        }
        if (anchor < 0) return false;

        for (int i = anchor + GussetD1Anchor.Length; i + 14 <= limit; i++)
        {
            if (raw[i] != 0xFF || raw[i + 1] != 0xFE || raw[i + 2] != 0xFF ||
                raw[i + 3] != 0x00 || raw[i + 4] != 0x00 || raw[i + 5] != 0x00)
                continue;
            double value = BitConverter.ToDouble(raw, i + 6);
            if (!double.IsNaN(value) && !double.IsInfinity(value) &&
                value > 0 && value <= 0.1)
            {
                thickness = value;
                break;
            }
        }

        int flag = anchor + 0x2046;
        if (flag + 5 < limit &&
            raw[flag + 1] == 0 && raw[flag + 2] == 0 &&
            raw[flag + 3] == 0 && raw[flag + 4] != 0 && raw[flag + 5] == 0x84)
        {
            switch (raw[flag])
            {
                case 0x00:
                    profileType = 1;
                    useAngle = true;
                    break;
                case 0x01:
                    profileType = 0;
                    useAngle = true;
                    break;
                case 0x02:
                    profileType = 1;
                    useAngle = false;
                    break;
            }
        }

        return thickness.HasValue || profileType.HasValue || useAngle.HasValue;
    }

    public static IFeatureData? ParseCombineBodies(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (!TryReadHeader(doc, className, typeId, start, out _, out var header))
            return null;

        var data = new CombineBodiesFeatureDefinitionData
        {
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;
        return data;
    }

    public static IFeatureData? ParseSplit(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (!TryReadHeader(doc, className, typeId, start, out _, out var header))
            return null;

        var data = new SplitBodyFeatureDefinitionData
        {
            SerializedClassName = className,
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        if (TryReadSplitParameters(doc, start, end,
            out bool? consume, out int? trimToolsCount))
        {
            if (consume.HasValue)
            {
                data.Consume = consume;
                data.Resolution.Consume = true;
            }
            if (trimToolsCount.HasValue)
            {
                data.DecodedTrimToolsCount = trimToolsCount;
                data.Resolution.TrimToolsCount = true;
            }
        }
        return data;
    }

    /// <summary>
    /// Reads the measured 0x80 Split scalars. Consume is the inverted byte at class tag
    /// + 0xE2 (0 = consumed, 1 = not consumed) inside a pinned local signature; the
    /// trim-tool count is the u16 at +0x160 followed by the moPlaneRef_w child tag.
    /// Individual trim-tool references and the undocumented State property are not promoted.
    /// </summary>
    private static bool TryReadSplitParameters(
        MfcDocument doc, int start, int end,
        out bool? consume, out int? trimToolsCount)
    {
        consume = null;
        trimToolsCount = null;
        byte[] raw = doc.SourceData;
        int limit = Math.Min(end, raw.Length);
        if (start < 0 || start + 0x180 > limit) return false;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x0080 && version != 0x0082) return false;

        byte a = raw[start + 0xE2];
        byte b = raw[start + 0xE6];
        if ((a == 0 || a == 1) && (b == 1 || b == 2) &&
            ((a == 0 && b == 1) || (a == 1 && b == 2)) &&
            raw[start + 0xE0] == 0 && raw[start + 0xE1] == 0 &&
            raw[start + 0xE3] == 0 && raw[start + 0xE4] == 0 &&
            raw[start + 0xE5] == 0 && raw[start + 0xE7] == 0)
        {
            consume = a == 0;
        }

        for (int i = start + 0x100; i + 0x16 <= limit; i++)
        {
            if (raw[i] != 0xFF || raw[i + 1] != 0xFF ||
                raw[i + 2] != 0x01 || raw[i + 3] != 0x00 ||
                raw[i + 4] != 0x0C || raw[i + 5] != 0x00 ||
                !MatchesAscii(raw, i + 6, "moPlaneRef_w"))
                continue;
            if (i < 2) break;
            ushort count = BitConverter.ToUInt16(raw, i - 2);
            if (count > 0 && count < 4096)
                trimToolsCount = count;
            break;
        }

        return consume.HasValue || trimToolsCount.HasValue;
    }

    private static bool MatchesAscii(byte[] raw, int offset, string text)
    {
        if (offset < 0 || offset + text.Length > raw.Length) return false;
        for (int i = 0; i < text.Length; i++)
            if (raw[offset + i] != (byte)text[i]) return false;
        return true;
    }

    public static IFeatureData? ParseTrimRefSurface(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (!TryReadHeader(doc, className, typeId, start, out _, out var header))
            return null;

        var data = new SurfaceTrimFeatureDefinitionData
        {
            SerializedClassName = className,
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        if (TryReadSurfaceTrimType(doc, start, end, out int trimType))
        {
            data.TrimType = trimType;
            data.Resolution.TrimType = true;
        }
        return data;
    }

    /// <summary>
    /// Reads the measured 0x6F SurfaceTrim type bit. The local signature is
    /// class tag + 0xD8..0xDC zero, flag byte at 0xDD in {0x04, 0x06}, zero at
    /// 0xDE and the child class tag FF FF 01 00 at 0xDF. Bit 0x02 is mutual trim.
    /// Trim-tool and pieces-to-keep counts were not isolated by a passable
    /// single-variable native variant and remain unresolved.
    /// </summary>
    private static bool TryReadSurfaceTrimType(
        MfcDocument doc, int start, int end, out int trimType)
    {
        trimType = 0;
        byte[] raw = doc.SourceData;
        if (start < 0 || start + 0xE4 > Math.Min(end, raw.Length)) return false;
        if (BitConverter.ToUInt16(raw, 0) != 0x006F) return false;
        for (int i = 0xD8; i <= 0xDC; i++)
            if (raw[start + i] != 0) return false;
        byte flag = raw[start + 0xDD];
        if (flag != 0x04 && flag != 0x06) return false;
        if (raw[start + 0xDE] != 0 || raw[start + 0xDF] != 0xFF ||
            raw[start + 0xE0] != 0xFF || raw[start + 0xE1] != 0x01 ||
            raw[start + 0xE2] != 0x00)
            return false;
        trimType = (flag & 0x02) != 0 ? 1 : 0;
        return true;
    }

    /// <summary>
    /// Narrows the folder-window end to the next top-level named MFC element.
    /// Legacy tutorial streams encode following features with class references
    /// instead of repeated class tags, so the folder window can span several
    /// features. The Rib thickness anchor must stay inside this feature's own
    /// top-level data block.
    /// </summary>
    private static int FindFeatureBoundary(MfcDocument doc, int start, int hardEnd)
    {
        int boundary = hardEnd;
        foreach (var element in doc.AllElements)
        {
            if (element.TagOffset <= start || element.TagOffset >= boundary)
                continue;
            if (element.TagByte != 0x01 || element.Depth != 0)
                continue;
            if (element.ClassName.IndexOf("_c", StringComparison.Ordinal) < 0)
                continue;
            boundary = element.TagOffset;
        }
        return boundary;
    }

    /// <summary>
    /// Locates the measured Rib thickness anchor. The anchor signature is
    /// <c>2.0 double, 8 marker bytes, 6 marker bytes, thickness double</c>.
    /// Legacy tutorial streams encode following Rib features with class references
    /// rather than repeated class tags, so a feature window can contain anchors for
    /// several ribs; the first anchor belongs to the recognized feature. The value
    /// is accepted only when it is finite and in (0, 0.1] m.
    /// </summary>
    internal static double? FindRibThickness(byte[] raw, int start, int end)
    {
        if (raw == null || start < 0) return null;
        int limit = Math.Min(end, raw.Length);
        for (int i = start; i + 0x16 + sizeof(double) <= limit; i++)
        {
            if (BitConverter.ToDouble(raw, i) != 2.0)
                continue;
            if (!Matches(raw, i + 8, RibThicknessMarkerA) ||
                !Matches(raw, i + 16, RibThicknessMarkerB))
                continue;

            double thickness = BitConverter.ToDouble(raw, i + 0x16);
            if (double.IsNaN(thickness) || double.IsInfinity(thickness) ||
                thickness <= 0 || thickness > 0.1)
                continue;
            return thickness;
        }
        return null;
    }

    private static bool Matches(byte[] raw, int offset, byte[] expected)
    {
        if (offset < 0 || offset + expected.Length > raw.Length) return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (raw[offset + i] != expected[i]) return false;
        }
        return true;
    }

    private static bool TryReadHeader(
        MfcDocument doc, string className, uint typeId, int start,
        out MfcElement element, out FolderHeader header)
    {
        element = doc.AllElements.FirstOrDefault(e =>
            e.TagByte == 0x01 && e.TagOffset == start && e.ClassName == className)!;
        header = new FolderHeader();
        if (element == null) return false;

        header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (!header.IsValid || header.TypeId == 0)
            return false;
        if (typeId != 0 && header.TypeId != typeId)
            return false;
        return true;
    }
}
