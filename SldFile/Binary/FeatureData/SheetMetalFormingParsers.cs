using System;
using System.Linq;
using SldFile.Binary.FeatureData;
using SldFile.Binary.Topology;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Strict parsers for Task 2.3 / 2.4 sheet metal forming features.
/// <para>
/// Hem is sample-confirmed: class <c>moHem_c</c>, FolderHeader TypeId <b>0x67</b>
/// (the catalog/TODO candidate 0x65 was not observed), stream version 0x73.
/// Lofted bend is sample-confirmed as <c>moLoftedBend_c</c> / TypeId 0x72
/// (version 0x74); flat pattern is sample-confirmed as <c>moFlatPattern_c</c> /
/// TypeId 0x5D or 0x5E. Jog, normal cut, fold and unfold remain candidate
/// skeletons until controlled samples establish their layouts.
/// </para>
/// </summary>
internal static class SheetMetalFormingParsers
{
    internal const string HemClassName = HemFeatureDefinitionData.SerializedClassName;
    internal const string JogClassName = JogFeatureDefinitionData.SerializedClassName;
    internal const string LoftedBendClassName = LoftedBendFeatureDefinitionData.SerializedClassName;
    internal const string NormalCutClassName = NormalCutFeatureDefinitionData.SerializedClassName;
    internal const string FlatPatternClassName = FlatPatternFeatureDefinitionData.SerializedClassName;
    internal const string FoldClassName = FoldFeatureDefinitionData.SerializedClassName;
    internal const string UnFoldClassName = UnFoldFeatureDefinitionData.SerializedClassName;

    internal const ushort HemStreamVersion = 0x73;

    internal static string HemDisplayName(int type) => HemAuthoringSegments.GetDisplayName(type);

    /// <summary>
    /// Offset from the D8 value to the serialized hem Type. All open samples
    /// (baseline, length-10, gap-1, radius-2 and angle-135, plus resaves) store
    /// u16 0 = swHemTypes_e Open; the closed samples store u16 1 = Closed.
    /// </summary>
    private const int HemTypeOffsetFromGapValue = 0x6E;

    /// <summary>
    /// True when the element is the observed hem node: class <c>moHem_c</c>,
    /// tag byte 0x01, FolderHeader TypeId 0x67.
    /// </summary>
    internal static bool IsHemNode(MfcElement element)
    {
        if (element == null || element.ClassName != HemClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == HemFeatureDefinitionData.ObservedSerializedTypeId;
    }

    public static IFeatureData? ParseHem(MfcElement element)
    {
        if (!IsHemNode(element))
            return null;
        return null;
    }

    /// <summary>
    /// Strict window parser for the controlled hem samples. Resolves Type (u16 at
    /// D8 value + 0x6E: 0 Open, 1 Closed), Length (D7), GapDistance (D8 after D7),
    /// Angle (D4), Radius (D1 between D4 and D7), MiterGap (D9 after D7) and
    /// EdgeCount (single moEdgeRef_c). BendPosition, ReverseDirection and
    /// UseDefaultBendAllowance are constant in every sample and stay unresolved.
    /// Unknown layouts return null.
    /// </summary>
    internal static IFeatureData? ParseHem(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2 || BitConverter.ToUInt16(raw, 0) != HemStreamVersion) return null;
        if (!IsHemNode(element)) return null;

        windowEnd = Math.Min(raw.Length, windowEnd);
        if (element.TagOffset >= windowEnd) return null;

        var elements = doc.AllElements
            .Where(e => e.TagOffset >= element.TagOffset && e.TagOffset < windowEnd)
            .ToList();

        // The hem selection block follows the feature folder (unlike the edge
        // flange specimen where it precedes it). Only the one-edge layout is known.
        var edgeRefs = elements.Where(e => e.ClassName == "moEdgeRef_c").ToList();
        var compEdges = elements.Where(e => e.ClassName == "moCompEdge_c").ToList();
        if (edgeRefs.Count != 1 || compEdges.Count != 1) return null;
        var edgeRef = edgeRefs[0];
        if (edgeRef.DataEnd - edgeRef.DataStart < 4) return null;
        uint edgeCount = BitConverter.ToUInt32(raw, edgeRef.DataStart);
        if (edgeCount != 1) return null;

        if (!TryReadUniqueNamed(raw, "D4", element.TagOffset, windowEnd,
                out double angle, out int d4Name))
            return null;
        // The native 180-degree value is stored as 3.1415926535898, a few ULPs
        // above Math.PI; allow a small tolerance at the upper bound.
        if (!SheetMetalTemplateParser.PositiveFinite(angle) || angle > Math.PI + 1e-9)
            return null;

        if (!TryReadUniqueNamed(raw, "D7", element.TagOffset, windowEnd,
                out double length, out int d7Name))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(length)) return null;

        // Radius is the D1 record between D4 and D7. The first D1 after the
        // feature is a derived half-gap dimension, not the API radius.
        if (!TryReadUniqueNamed(raw, "D1", d4Name, d7Name, out double radius))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(radius)) return null;

        // GapDistance / MiterGap are the D8/D9 records after D7 (the D8/D9
        // records before D7 are unrelated display dimensions).
        if (!TryReadUniqueNamed(raw, "D8", d7Name, windowEnd,
                out double gap, out int d8Name))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(gap)) return null;

        if (!TryReadUniqueNamed(raw, "D9", d7Name, windowEnd, out double miterGap))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(miterGap)) return null;

        // Type: u16 at D8 value + 0x6E. Validated against all five open samples
        // and both closed samples (main + resaves).
        int typeSlot = d8Name + 0x1E + HemTypeOffsetFromGapValue;
        if (typeSlot < 0 || typeSlot + 2 > raw.Length) return null;
        ushort typeValue = BitConverter.ToUInt16(raw, typeSlot);
        if (typeValue > 1) return null;
        int type = typeValue;

        // Layout gate: the first D1 after the feature is the derived half-gap.
        if (!TryReadUniqueNamed(raw, "D1", element.TagOffset, d4Name,
                out double halfGap, out _))
            return null;
        if (!SheetMetalTemplateParser.PositiveFinite(halfGap)) return null;
        if (Math.Abs(halfGap * 2.0 - gap) > 1e-12) return null;

        var data = new HemFeatureDefinitionData
        {
            Type = type,
            Length = length,
            GapDistance = gap,
            Angle = angle,
            Radius = radius,
            MiterGap = miterGap,
            EdgeCount = checked((int)edgeCount)
        };
        data.ParameterResolution.Type = true;
        data.ParameterResolution.Length = true;
        data.ParameterResolution.GapDistance = true;
        data.ParameterResolution.Angle = true;
        data.ParameterResolution.Radius = true;
        data.ParameterResolution.MiterGap = true;
        data.ParameterResolution.EdgeCount = true;
        return data;
    }

    private static bool TryReadUniqueNamed(
        byte[] raw, string name, int start, int end, out double value)
    {
        return TryReadUniqueNamed(raw, name, start, end, out value, out _);
    }

    private static bool TryReadUniqueNamed(
        byte[] raw, string name, int start, int end, out double value, out int nameOffset)
    {
        value = 0;
        nameOffset = -1;
        if (string.IsNullOrEmpty(name)) return false;
        int count = 0;
        double candidate = 0;
        // WChar record: FF FE FF <charCount> <2*charCount bytes>, then the
        // 22-byte scalar descriptor and the 8-byte value at descriptor+0x16.
        int descriptorOffset = 4 + name.Length * 2;
        int valueOffset = descriptorOffset + 0x16;
        int last = Math.Min(end, raw.Length) - (valueOffset + 8);
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != name.Length)
                continue;
            bool nameMatch = true;
            for (int k = 0; k < name.Length; k++)
            {
                if (raw[p + 4 + 2 * k] != (byte)name[k] || raw[p + 5 + 2 * k] != 0)
                {
                    nameMatch = false;
                    break;
                }
            }
            if (!nameMatch) continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + descriptorOffset)) continue;
            double v = BitConverter.ToDouble(raw, p + valueOffset);
            if (!SheetMetalTemplateParser.PositiveFinite(v)) continue;
            if (count > 0 && v != candidate) return false;
            if (count == 0) nameOffset = p;
            candidate = v;
            count++;
        }
        if (count == 0) return false;
        value = candidate;
        return true;
    }

    public static IFeatureData? ParseJog(MfcElement element)
    {
        if (element == null || element.ClassName != JogClassName || element.TagByte != 0x01)
            return null;
        return null;
    }

    private static readonly string[] FeatureClassPrefixes =
    {
        "moBreakCornerFeat", "moFlatPattern", "moFold_", "moUnFold_", "moJog_",
        "moSMBaseFlange", "moMirrorPattern", "moLPattern", "moCirPattern",
        "moEdgeFlange", "moHem_", "moLoftedBend", "moExtru", "moRevolution",
        "moCut", "moChamfer", "Fillet_", "moShell", "moDeleteBody", "moDraft"
    };

    private static int FindNextFeatureTag(MfcDocument doc, int afterOffset, int windowEnd)
    {
        int best = windowEnd;
        foreach (var e in doc.AllElements)
        {
            if (e.TagOffset <= afterOffset || e.TagOffset >= best || e.Depth != 0) continue;
            string name = e.ClassName;
            if (string.IsNullOrEmpty(name)) continue;
            foreach (string prefix in FeatureClassPrefixes)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    best = e.TagOffset;
                    break;
                }
            }
        }
        return best;
    }

    private static bool TryReadFirstNamed(
        byte[] raw, string name, int start, int end, out double value, out int nameOffset)
    {
        value = 0;
        nameOffset = -1;
        if (string.IsNullOrEmpty(name)) return false;
        int descriptorOffset = 4 + name.Length * 2;
        int valueOffset = descriptorOffset + 0x16;
        int last = Math.Min(end, raw.Length) - (valueOffset + 8);
        for (int p = Math.Max(0, start); p <= last; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF ||
                raw[p + 3] != name.Length)
                continue;
            bool match = true;
            for (int k = 0; k < name.Length; k++)
            {
                if (raw[p + 4 + 2 * k] != (byte)name[k] || raw[p + 5 + 2 * k] != 0)
                {
                    match = false;
                    break;
                }
            }
            if (!match) continue;
            if (!SheetMetalTemplateParser.IsScalarDescriptor(raw, p + descriptorOffset)) continue;
            double v = BitConverter.ToDouble(raw, p + valueOffset);
            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
            value = v;
            nameOffset = p;
            return true;
        }
        return false;
    }

    /// <summary>
    /// True when the element is the tutorial-layout jog node: class
    /// <c>moJog_c</c>, tag byte 0x01, FolderHeader TypeId 0x159 (version 0x1AA).
    /// </summary>
    internal static bool IsJogNode(MfcElement element)
    {
        if (element == null || element.ClassName != JogClassName || element.TagByte != 0x01)
            return false;
        return ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId == 0x159;
    }

    /// <summary>
    /// Strict parser for the tutorial Jog layout (Complex Sheet Metal Part,
    /// version 0x1AA, TypeId 0x159). Native IJogFeatureData reads
    /// BendRadius=0.00254 m, JogAngle=π/2 and OffsetDistance=0.0127 m. Radius and
    /// angle are the first named D1/D4 records after the feature; the offset is
    /// the first positive D2 after the first D3 record. Boolean/enum flags are
    /// read from three anchored structures validated by 11 native single-variable
    /// variants. FixedFace and the remaining selections stay unresolved.
    /// </summary>
    internal static IFeatureData? ParseJog(MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2 || BitConverter.ToUInt16(raw, 0) != 0x1AA) return null;
        if (!IsJogNode(element)) return null;

        windowEnd = Math.Min(windowEnd, raw.Length);
        if (element.TagOffset >= windowEnd) return null;
        int localEnd = FindNextFeatureTag(doc, element.TagOffset, windowEnd);
        int numericEnd = Math.Min(localEnd, element.TagOffset + 0x1000);

        if (!TryReadFirstNamed(raw, "D1", element.TagOffset, numericEnd,
                out double radius, out _))
            return null;
        if (radius <= 0 || radius > 1.0) return null;

        if (!TryReadFirstNamed(raw, "D4", element.TagOffset, numericEnd,
                out double angle, out _))
            return null;
        if (angle <= 0 || angle > Math.PI + 1e-9) return null;

        var data = new JogFeatureDefinitionData
        {
            TypeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId,
            BendRadius = radius,
            JogAngle = angle
        };
        data.ParameterResolution.BendRadius = true;
        data.ParameterResolution.JogAngle = true;

        if (TryReadFirstNamed(raw, "D3", element.TagOffset, localEnd,
                out _, out int d3Name) &&
            TryReadFirstNamed(raw, "D2", d3Name + 1, localEnd,
                out double offset, out _) &&
            offset > 0 && offset <= 1.0)
        {
            data.OffsetDistance = offset;
            data.ParameterResolution.OffsetDistance = true;
        }

        TryReadJogFlags(raw, element.TagOffset, windowEnd, data);
        return data;
    }

    private static readonly byte[] JogDefaultFlagsAnchor =
    {
        0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00,
        0xFF, 0xFE, 0xFF, 0x00, 0xFF, 0xFE, 0xFF, 0x00,
        0, 0, 0, 0, 0, 0, 0, 0
    };

    private static readonly byte[] JogOffsetFlagsAnchor =
        { 0x02, 0xFE, 0xB5, 0x4C, 0x94, 0x07, 0xA3, 0x3F };

    /// <summary>
    /// Jog flags from the observed tutorial layout, located by three structural
    /// anchors and validated against native readback + 11 single-variable native
    /// variants (2026-10-31):
    /// - four empty WChars + 8 zero bytes anchor the default/allowance block;
    ///   +0x18 UseDefaultBendAllowance, +0x1C UseDefaultBendRadius,
    ///   +0x48 ReverseDirection, +0x54 DimensionPositionType (1->2, 2->1);
    /// - the double 0x3FA307944CB5FE02 anchors the offset block; -9 OffsetType
    ///   (0->1, 3->2), +0x1A FixProjectedLength, +0x1E ReverseOffset;
    /// - the "24 82" pair anchors JogPositionType at -4 (0->1, 1->2).
    /// Missing anchors or out-of-range bytes leave the affected fields null.
    /// </summary>
    private static void TryReadJogFlags(
        byte[] raw, int tag, int windowEnd, JogFeatureDefinitionData data)
    {
        windowEnd = Math.Min(windowEnd, raw.Length);

        int a = FindPattern(raw, JogDefaultFlagsAnchor, tag, Math.Min(windowEnd, tag + 0x6000));
        if (a >= 0 && a + 0x55 < raw.Length)
        {
            byte allow = raw[a + 0x18];
            byte radius = raw[a + 0x1C];
            byte reverse = raw[a + 0x48];
            byte dimension = raw[a + 0x54];
            if (allow <= 1)
            {
                data.UseDefaultBendAllowance = allow != 0;
                data.ParameterResolution.UseDefaultBendAllowance = true;
            }
            if (radius <= 1)
            {
                data.UseDefaultBendRadius = radius != 0;
                data.ParameterResolution.UseDefaultBendRadius = true;
            }
            if (reverse <= 1)
            {
                data.ReverseDirection = reverse != 0;
                data.ParameterResolution.ReverseDirection = true;
            }
            if (dimension == 1 || dimension == 2)
            {
                data.DimensionPositionType = dimension == 1 ? 2 : 1;
                data.ParameterResolution.DimensionPositionType = true;
            }
        }

        int b = FindPattern(raw, JogOffsetFlagsAnchor,
            tag + 0x6000, Math.Min(windowEnd, tag + 0x7000));
        if (b >= 9 && b + 30 < raw.Length)
        {
            byte offsetType = raw[b - 9];
            byte fixProjected = raw[b + 26];
            byte reverseOffset = raw[b + 30];
            if (offsetType == 0 || offsetType == 3)
            {
                data.OffsetType = offsetType == 0 ? 1 : 2;
                data.ParameterResolution.OffsetType = true;
            }
            if (fixProjected <= 1)
            {
                data.FixProjectedLength = fixProjected != 0;
                data.ParameterResolution.FixProjectedLength = true;
            }
            if (reverseOffset <= 1)
            {
                data.ReverseOffset = reverseOffset != 0;
                data.ParameterResolution.ReverseOffset = true;
            }
        }

        int c = FindPattern(raw, new byte[] { 0x24, 0x82 },
            tag + 0x5A00, Math.Min(windowEnd, tag + 0x5C00));
        if (c >= 4 && raw[c - 4] <= 1)
        {
            data.JogPositionType = raw[c - 4] + 1;
            data.ParameterResolution.JogPositionType = true;
        }
    }

    private static int FindPattern(byte[] raw, byte[] pattern, int start, int end)
    {
        start = Math.Max(0, start);
        end = Math.Min(end, raw.Length);
        for (int p = start; p <= end - pattern.Length; p++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (raw[p + j] != pattern[j]) { match = false; break; }
            }
            if (match) return p;
        }
        return -1;
    }

    public static IFeatureData? ParseLoftedBend(MfcElement element)
    {
        if (!IsLoftedBendNode(element))
            return null;
        return null;
    }

    /// <summary>
    /// True when the element is the observed non-formed lofted-bend node:
    /// class <c>moLoftedBend_c</c>, tag byte 0x01, FolderHeader TypeId 0x72.
    /// </summary>
    internal static bool IsLoftedBendNode(MfcElement element)
    {
        if (element == null || element.ClassName != LoftedBendClassName || element.TagByte != 0x01)
            return false;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        return header.TypeId == LoftedBendFeatureDefinitionData.ObservedSerializedTypeId;
    }

    /// <summary>
    /// Strict parser for the observed 0x72 lofted-bend layout (version 0x74).
    /// Thickness is the double at D14 name - 0xBB4, gated by 1.0 doubles at
    /// -0xBC5/-0xBBD and a second positive value at -0xBAC; NumberOfBendLines is
    /// named D14. All other ILoftedBendsFeatureData values and the profile
    /// references remain unresolved.
    /// </summary>
    internal static IFeatureData? ParseLoftedBend(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2 || BitConverter.ToUInt16(raw, 0) != 0x74) return null;
        if (!IsLoftedBendNode(element)) return null;

        windowEnd = Math.Min(raw.Length, windowEnd);
        if (element.TagOffset >= windowEnd) return null;

        if (!TryReadUniqueNamed(raw, "D14", element.TagOffset, windowEnd,
                out double bendLines, out int d14Name))
            return null;
        if (bendLines < 1.0 || bendLines > 1000.0 ||
            Math.Abs(bendLines - Math.Round(bendLines)) > 1e-9)
            return null;

        int gate1 = d14Name - 0xBC5;
        int gate2 = d14Name - 0xBBD;
        int thicknessSlot = d14Name - 0xBB4;
        int companionSlot = d14Name - 0xBAC;
        if (gate1 < 0 || thicknessSlot < 0 || companionSlot + 8 > raw.Length) return null;
        if (BitConverter.ToDouble(raw, gate1) != 1.0 ||
            BitConverter.ToDouble(raw, gate2) != 1.0)
            return null;
        double thickness = BitConverter.ToDouble(raw, thicknessSlot);
        double companion = BitConverter.ToDouble(raw, companionSlot);
        if (!SheetMetalTemplateParser.PositiveFinite(thickness) ||
            !SheetMetalTemplateParser.PositiveFinite(companion))
            return null;

        var data = new LoftedBendFeatureDefinitionData
        {
            Thickness = thickness,
            NumberOfBendLines = checked((int)Math.Round(bendLines))
        };
        data.ParameterResolution.Thickness = true;
        data.ParameterResolution.NumberOfBendLines = true;
        return data;
    }

    public static IFeatureData? ParseNormalCut(MfcElement element)
    {
        if (element == null || element.ClassName != NormalCutClassName || element.TagByte != 0x01)
            return null;
        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (header.TypeId != 0x87) return null;
        return new NormalCutFeatureDefinitionData { TypeId = 0x87 };
    }

    /// <summary>
    /// True when the element is an observed flat-pattern node: class
    /// <c>moFlatPattern_c</c>, tag byte 0x01, FolderHeader TypeId 0x5D/0x5E for
    /// the controlled base-flange samples or the tutorial-generation layout
    /// TypeIds 0x72 (Paper Airplane), 0x1E (Complex Sheet Metal Part) and 0x164
    /// (drive sled).
    /// </summary>
    internal static bool IsFlatPatternNode(MfcElement element)
    {
        if (element == null || element.ClassName != FlatPatternClassName || element.TagByte != 0x01)
            return false;
        uint typeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
        return typeId == FlatPatternFeatureDefinitionData.LegacySerializedTypeId ||
               typeId == FlatPatternFeatureDefinitionData.CurrentSerializedTypeId ||
               typeId == 0x72 || typeId == 0x1E || typeId == 0x164 ||
               typeId == 0x66;
    }

    public static IFeatureData? ParseFlatPattern(MfcElement element)
    {
        if (!IsFlatPatternNode(element))
            return null;
        return null;
    }

    /// <summary>
    /// Strict flat-pattern parser for the observed 0x5D/0x5E layouts. Only the
    /// fixed-face selection is proven by the existing samples: exactly one
    /// moFaceRef_c/moCompFace_c follows the feature folder. All other
    /// IFlatPatternFeatureData values are constant defaults in every sample and
    /// have no native snapshot, so they remain unresolved rather than guessed.
    /// </summary>
    internal static IFeatureData? ParseFlatPattern(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version == 0x155 || version == 0x1AA || version == 0xC7)
            return ParseTutorialFlatPattern(doc, element, windowEnd);
        if (version == 0x74)
        {
            uint inlineType = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
            if (inlineType == 0x66)
                return ParseInlineFlatPattern066(doc, element);
        }
        // Flat-pattern layouts are observed in both 0x73 (SheetMetalOptions /
        // sm_base_flange_*) and 0x74 (sm_two_base_flanges) streams.
        if (version != 0x73 && version != 0x74) return null;
        if (!IsFlatPatternNode(element)) return null;

        windowEnd = Math.Min(raw.Length, windowEnd);
        if (element.TagOffset >= windowEnd) return null;

        var elements = doc.AllElements
            .Where(e => e.TagOffset >= element.TagOffset && e.TagOffset < windowEnd)
            .ToList();
        var faceRefs = elements.Where(e => e.ClassName == "moFaceRef_c").ToList();
        var compFaces = elements.Where(e => e.ClassName == "moCompFace_c").ToList();
        if (faceRefs.Count != 1 || compFaces.Count != 1) return null;

        var faceRef = FeatureDataParsers.ParseFaceRef(faceRefs[0]);
        var compFace = TopologyParsers.ParseCompFace(compFaces[0]);
        if (faceRef == null || compFace == null) return null;

        var endFaceElement = elements.FirstOrDefault(e => e.ClassName == "moEndFaceSurfIdRep_c");
        var endFace = endFaceElement == null ? null : FeatureDataParsers.ParseEndFaceSurfIdRep(endFaceElement);
        var endFace3Element = elements.FirstOrDefault(e => e.ClassName == "moEndFace3IntSurfIdRep_c");
        var endFace3Int = endFace3Element == null ? null : TopologyParsers.ParseEndFace3IntSurfIdRep(endFace3Element);

        uint faceId = compFace.FaceIndex;
        if (faceId == 0)
            faceId = faceRef.FaceIds.FirstOrDefault(id => id > 0);
        if (faceId == 0 && endFace3Int != null)
            faceId = endFace3Int.FaceIndex;

        string surfaceName = endFace?.SurfaceName ?? string.Empty;
        if (faceId == 0 && string.IsNullOrEmpty(surfaceName)) return null;

        var data = new FlatPatternFeatureDefinitionData
        {
            TypeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId,
            FixedFace = new EntityReference
            {
                EntityId = faceId,
                EntityType = SelectionEntityType.Face,
                Name = !string.IsNullOrEmpty(surfaceName) ? surfaceName : $"Face_{faceId}"
            }
        };
        data.ParameterResolution.FixedFace = true;

        // The flat-pattern option block starts immediately after moFaceRef_c.
        // It is identical in the 0x5D variant samples and the 0x5E
        // SheetMetalOptions series; the face-ref payload length varies only with
        // the embedded file-name path, so all option slots are relative to
        // faceRef.DataEnd.
        if (!TryReadFlatPatternOptions(raw, faceRefs[0].DataEnd, data))
            return null;
        return data;
    }

    /// <summary>
    /// Tutorial-generation flat pattern (Paper Airplane 0x155/TypeId 0x72,
    /// Complex 0x1AA/0x1E, drive sled 0xC7/0x164). The option block is an
    /// untagged record with a stable layout, located by the unique
    /// FFFFFFFF+8-zero anchor and the 2B 80 02 trailing signature:
    /// anchor-0x0E flags (bit0 MergeFace, bit1 inverse CornerTreatment),
    /// +0x08 trim type, +0x0C trim distance/ratio, +0x14 break type,
    /// +0x18 break radius, +0x24 UseRatioToThickness, +0x28 SimplifyBends,
    /// +0x2C ShowSlitInCornerRelief. Validated against 7 native single-variable
    /// variants (merge/simplify/corner/break chamfer/trim square/ratio/slit).
    /// FixedFace/FixedFace2 remain unresolved because no face reference is
    /// stored in this block; TypeId is always resolved from the folder header.
    /// </summary>
    internal static IFeatureData? ParseTutorialFlatPattern(
        MfcDocument doc, MfcElement element, int windowEnd)
    {
        if (doc == null || element == null || element.ClassName != FlatPatternClassName ||
            element.TagByte != 0x01)
            return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(raw, 0);
        uint expectedType = version switch
        {
            0x155 => 0x72,
            0x1AA => 0x1E,
            0xC7 => 0x164,
            _ => 0
        };
        if (expectedType == 0) return null;
        uint typeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
        if (typeId != expectedType) return null;

        var data = new FlatPatternFeatureDefinitionData { TypeId = typeId };
        if (!TryFindTutorialFlatOptionBlock(raw, out int anchor)) return data;
        if (anchor - 0x0E < 0 || anchor + 0x35 > raw.Length) return data;

        byte flags = raw[anchor - 0x0E];
        uint trimType = BitConverter.ToUInt32(raw, anchor + 0x08);
        double trim = BitConverter.ToDouble(raw, anchor + 0x0C);
        uint breakType = BitConverter.ToUInt32(raw, anchor + 0x14);
        double breakRadius = BitConverter.ToDouble(raw, anchor + 0x18);
        byte useRatio = raw[anchor + 0x24];
        byte simplify = raw[anchor + 0x28];
        byte slit = raw[anchor + 0x2C];
        if (trimType > 9 || breakType > 1 || useRatio > 1 ||
            simplify > 1 || slit > 1 || trim <= 0 || trim > 0.1 ||
            breakRadius <= 0 || breakRadius > 0.1)
            return data;

        // The flags byte is at anchor-0x0E for the Paper/Complex families. In the
        // drive-sled family that byte is unrelated (0x97/0x60), so MergeFace and
        // CornerTreatment stay unresolved there rather than reading foreign data.
        if (flags <= 3)
        {
            data.MergeFace = (flags & 1) != 0;
            data.CornerTreatment = (flags & 2) == 0;
            data.ParameterResolution.MergeFace = true;
            data.ParameterResolution.CornerTreatment = true;
        }
        data.CornerTrimReliefType = checked((int)trimType);
        data.CornerTrimReliefDistance = trim;
        // The native API exposes the ratio and distance as the same value in all
        // observed samples; only one double is serialized.
        data.CornerTrimRatioToThickness = trim;
        data.BreakCornerType = checked((int)breakType);
        data.BreakCornerRadius = breakRadius;
        data.UseRatioToThickness = useRatio != 0;
        data.SimplifyBends = simplify != 0;
        data.ShowSlitInCornerRelief = slit != 0;

        data.ParameterResolution.CornerTrimReliefType = true;
        data.ParameterResolution.CornerTrimReliefDistance = true;
        data.ParameterResolution.CornerTrimRatioToThickness = true;
        data.ParameterResolution.BreakCornerType = true;
        data.ParameterResolution.BreakCornerRadius = true;
        data.ParameterResolution.UseRatioToThickness = true;
        data.ParameterResolution.SimplifyBends = true;
        data.ParameterResolution.ShowSlitInCornerRelief = true;
        return data;
    }

    private const int Inline066SuffixLength = 3900;
    private const int Inline066FlagsOffset = 0xDF7;
    private const int Inline066TrimTypeOffset = 0xE0D;
    private const int Inline066TrimOffset = 0xE11;
    private const int Inline066BreakTypeOffset = 0xE19;
    private const int Inline066BreakRadiusOffset = 0xE1D;
    private const int Inline066UseRatioOffset = 0xE29;
    private const int Inline066SimplifyOffset = 0xE2D;
    private const int Inline066SlitOffset = 0xE31;

    /// <summary>
    /// Flat-pattern 0x66 layout measured on sm_base_flange_after_fillet.SLDPRT
    /// (ResolvedFeatures 0x74, folder count 25). The option values live inline in
    /// the 3900-byte suffix starting at the moFlatPattern_c tag:
    /// +0xDF7 flags (bit0 MergeFace, bit1 inverse CornerTreatment),
    /// +0xE0D CornerTrimReliefType, +0xE11 shared distance/ratio double,
    /// +0xE19 BreakCornerType, +0xE1D BreakCornerRadius,
    /// +0xE29 UseRatioToThickness, +0xE2D SimplifyBends,
    /// +0xE31 ShowSlitInCornerRelief. Native single-variable variants prove every
    /// slot; FixedFace/FixedFace2/ExcludedFaces remain unresolved (API could not
    /// persist independent values). Any layout gate mismatch rejects the node.
    /// </summary>
    private static IFeatureData? ParseInlineFlatPattern066(MfcDocument doc, MfcElement element)
    {
        byte[] raw = doc.SourceData;
        if (element == null || element.ClassName != FlatPatternClassName ||
            element.TagByte != 0x01) return null;
        if (ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId != 0x66) return null;
        if (element.TagOffset < 0 ||
            raw.Length - element.TagOffset != Inline066SuffixLength) return null;

        int tag = element.TagOffset;
        byte flags = raw[tag + Inline066FlagsOffset];
        if (flags > 3) return null;
        uint trimType = BitConverter.ToUInt32(raw, tag + Inline066TrimTypeOffset);
        if (trimType > 9) return null;
        double trim = BitConverter.ToDouble(raw, tag + Inline066TrimOffset);
        if (double.IsNaN(trim) || double.IsInfinity(trim) || trim <= 0 || trim > 1000) return null;
        uint breakType = BitConverter.ToUInt32(raw, tag + Inline066BreakTypeOffset);
        if (breakType > 1) return null;
        double breakRadius = BitConverter.ToDouble(raw, tag + Inline066BreakRadiusOffset);
        if (double.IsNaN(breakRadius) || double.IsInfinity(breakRadius) ||
            breakRadius <= 0 || breakRadius > 0.1) return null;
        byte useRatio = raw[tag + Inline066UseRatioOffset];
        byte simplify = raw[tag + Inline066SimplifyOffset];
        byte slit = raw[tag + Inline066SlitOffset];
        if (useRatio > 1 || simplify > 1 || slit > 1) return null;

        var data = new FlatPatternFeatureDefinitionData
        {
            TypeId = 0x66,
            MergeFace = (flags & 1) != 0,
            CornerTreatment = (flags & 2) == 0,
            SimplifyBends = simplify != 0,
            BreakCornerType = checked((int)breakType),
            BreakCornerRadius = breakRadius,
            CornerTrimReliefType = checked((int)trimType),
            CornerTrimReliefDistance = trim,
            CornerTrimRatioToThickness = trim,
            UseRatioToThickness = useRatio != 0,
            ShowSlitInCornerRelief = slit != 0
        };
        data.ParameterResolution.MergeFace = true;
        data.ParameterResolution.CornerTreatment = true;
        data.ParameterResolution.SimplifyBends = true;
        data.ParameterResolution.BreakCornerType = true;
        data.ParameterResolution.BreakCornerRadius = true;
        data.ParameterResolution.CornerTrimReliefType = true;
        data.ParameterResolution.CornerTrimReliefDistance = true;
        data.ParameterResolution.CornerTrimRatioToThickness = true;
        data.ParameterResolution.UseRatioToThickness = true;
        data.ParameterResolution.ShowSlitInCornerRelief = true;
        return data;
    }

    private static bool TryFindTutorialFlatOptionBlock(byte[] raw, out int anchor)
    {
        anchor = -1;
        int count = 0;
        for (int p = 0x0E; p <= raw.Length - 0x36; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFF ||
                raw[p + 2] != 0xFF || raw[p + 3] != 0xFF)
                continue;
            bool zeros = true;
            for (int j = 4; j <= 7; j++)
                if (raw[p + j] != 0) { zeros = false; break; }
            if (!zeros) continue;
            if (BitConverter.ToUInt32(raw, p + 0x08) > 9) continue;
            double trim = BitConverter.ToDouble(raw, p + 0x0C);
            if (trim <= 0 || trim > 0.1) continue;
            if (BitConverter.ToUInt32(raw, p + 0x14) > 1) continue;
            double breakRadius = BitConverter.ToDouble(raw, p + 0x18);
            if (breakRadius <= 0 || breakRadius > 0.1) continue;
            if (raw[p + 0x24] > 1 || raw[p + 0x28] > 1 || raw[p + 0x2C] > 1) continue;
            // Trailing signature shared by all observed tutorial families.
            if (raw[p + 0x32] != 0x2B || raw[p + 0x33] != 0x80 || raw[p + 0x34] != 0x02)
                continue;
            if (count == 0) anchor = p;
            count++;
            if (count > 1) { anchor = -1; return false; }
        }
        return count == 1;
    }

    private const int FlatOptionFlags = 0x21;
    private const int FlatOptionTrimType = 0x37;
    private const int FlatOptionTrimDistance = 0x3B;
    private const int FlatOptionBreakType = 0x43;
    private const int FlatOptionBreakRadius = 0x47;
    private const int FlatOptionSimplifyBends = 0x57;

    /// <summary>
    /// Reads the option block after moFaceRef_c. Observed layout gates:
    /// u16 0xFFFF at +0, u32 0x4A38 at +0x16, u32 1 at +0x1C, byte 1 at
    /// +0x2B and +0x5B. The flags byte at +0x21 packs MergeFace (bit 0) and
    /// the inverse CornerTreatment (bit 1); the single-variable merge/corner
    /// samples produce 0x00/0x01/0x03. SimplifyBends is a 0/1 byte at +0x57.
    /// BreakCornerType/Radius are u32/double at +0x43/+0x47; CornerTrimReliefType
    /// and Distance are u32/double at +0x37/+0x3B. CornerTrimRatioToThickness,
    /// UseRatioToThickness and ShowSlitInCornerRelief have no varying sample and
    /// stay unresolved.
    /// </summary>
    private static bool TryReadFlatPatternOptions(byte[] raw, int anchor, FlatPatternFeatureDefinitionData data)
    {
        if (anchor < 0 || anchor + 0x64 > raw.Length) return false;
        if (raw[anchor] != 0xFF || raw[anchor + 1] != 0xFF) return false;
        if (BitConverter.ToUInt32(raw, anchor + 0x16) != 0x4A38u) return false;
        if (BitConverter.ToUInt32(raw, anchor + 0x1C) != 1u) return false;
        if (raw[anchor + 0x2B] != 1 || raw[anchor + 0x5B] != 1) return false;

        byte flags = raw[anchor + FlatOptionFlags];
        if (flags > 3) return false;
        byte simplify = raw[anchor + FlatOptionSimplifyBends];
        if (simplify > 1) return false;

        uint breakType = BitConverter.ToUInt32(raw, anchor + FlatOptionBreakType);
        if (breakType > 1) return false;
        double breakRadius = BitConverter.ToDouble(raw, anchor + FlatOptionBreakRadius);
        if (!SheetMetalTemplateParser.PositiveFinite(breakRadius)) return false;

        uint trimType = BitConverter.ToUInt32(raw, anchor + FlatOptionTrimType);
        if (trimType > 9) return false;
        double trimDistance = BitConverter.ToDouble(raw, anchor + FlatOptionTrimDistance);
        if (!SheetMetalTemplateParser.PositiveFinite(trimDistance)) return false;

        data.MergeFace = (flags & 1) != 0;
        data.CornerTreatment = (flags & 2) == 0;
        data.SimplifyBends = simplify != 0;
        data.BreakCornerType = checked((int)breakType);
        data.BreakCornerRadius = breakRadius;
        data.CornerTrimReliefType = checked((int)trimType);
        data.CornerTrimReliefDistance = trimDistance;

        data.ParameterResolution.MergeFace = true;
        data.ParameterResolution.CornerTreatment = true;
        data.ParameterResolution.SimplifyBends = true;
        data.ParameterResolution.BreakCornerType = true;
        data.ParameterResolution.BreakCornerRadius = true;
        data.ParameterResolution.CornerTrimReliefType = true;
        data.ParameterResolution.CornerTrimReliefDistance = true;
        return true;
    }

    public static IFeatureData? ParseFold(MfcElement element)
    {
        if (element == null || element.ClassName != FoldClassName || element.TagByte != 0x01)
            return null;
        return null;
    }

    /// <summary>
    /// Tutorial Fold layout (Paper Airplane version 0x155 / TypeId 0x153;
    /// drive sled version 0x1AA / TypeId 0x213). IFoldsFeatureData::IGetBendsCount
    /// equals the number of serialized moOneBend_c/moSM3dBend_c features (3 and
    /// 4). FixedFace and the individual bend references stay unresolved.
    /// </summary>
    internal static IFeatureData? ParseFold(MfcDocument doc, MfcElement element, int windowEnd)
    {
        var data = ParseFolds(doc, element, windowEnd, isUnfold: false);
        if (data == null) return null;
        var result = new FoldFeatureDefinitionData
        {
            TypeId = data.Value.TypeId,
            BendsCount = data.Value.BendsCount
        };
        result.ParameterResolution.BendsCount = true;
        return result;
    }

    /// <summary>
    /// Tutorial UnFold layout (Paper Airplane version 0x155 / TypeId 0x144;
    /// drive sled version 0x1AA / TypeId 0x209). Same bend-count evidence as Fold.
    /// </summary>
    internal static IFeatureData? ParseUnFold(MfcDocument doc, MfcElement element, int windowEnd)
    {
        var data = ParseFolds(doc, element, windowEnd, isUnfold: true);
        if (data == null) return null;
        var result = new UnFoldFeatureDefinitionData
        {
            TypeId = data.Value.TypeId,
            BendsCount = data.Value.BendsCount
        };
        result.ParameterResolution.BendsCount = true;
        return result;
    }

    private static (uint TypeId, int BendsCount)? ParseFolds(
        MfcDocument doc, MfcElement element, int windowEnd, bool isUnfold)
    {
        if (doc == null || element == null) return null;
        byte[] raw = doc.SourceData;
        if (raw.Length < 2) return null;
        ushort version = BitConverter.ToUInt16(raw, 0);
        if (version != 0x155 && version != 0x1AA && version != 0xC7) return null;
        string className = isUnfold ? UnFoldClassName : FoldClassName;
        if (element.ClassName != className || element.TagByte != 0x01) return null;

        uint typeId = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element).TypeId;
        if (typeId == 0) return null;

        int bends = CountBendNames(raw, element.TagOffset);
        if (bends <= 0)
        {
            foreach (var e in doc.AllElements)
            {
                if (e.TagOffset >= element.TagOffset) continue;
                if (e.ClassName == "moOneBend_c" || e.ClassName == "moSM3dBend_c") bends++;
            }
        }
        if (bends <= 0 || bends > 1000) return null;
        return (typeId, bends);
    }

    /// <summary>
    /// Counts unique EdgeBend*/SketchBend* WChar names serialized before the
    /// feature. Paper Airplane has EdgeBend1, EdgeBend2, SketchBend1 (native
    /// IGetBendsCount=3); drive sled has four EdgeBend names (native 4).
    /// </summary>
    private static int CountBendNames(byte[] raw, int beforeOffset)
    {
        var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        int end = Math.Min(beforeOffset, raw.Length);
        for (int p = 0; p <= end - 6; p++)
        {
            if (raw[p] != 0xFF || raw[p + 1] != 0xFE || raw[p + 2] != 0xFF) continue;
            int chars = raw[p + 3];
            if (chars < 8 || chars > 40 || p + 4 + chars * 2 > end) continue;
            string text = System.Text.Encoding.Unicode.GetString(raw, p + 4, chars * 2);
            if (text.StartsWith("EdgeBend", StringComparison.Ordinal) ||
                text.StartsWith("SketchBend", StringComparison.Ordinal))
                names.Add(text);
        }
        return names.Count;
    }

    public static IFeatureData? ParseUnFold(MfcElement element)
    {
        if (element == null || element.ClassName != UnFoldClassName || element.TagByte != 0x01)
            return null;
        return null;
    }
}
