using System;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parsers for the controlled PatternOptions layouts: linear (one/two directions) and
/// circular (equal spacing on/off), including the native BossSeed variant samples. Counts,
/// spacings and the circular equal-spacing layout are bound; direction and source-feature
/// persistent references are exposed as presence-only edge/feature references when the measured
/// selection envelope is present. Geometry-pattern flags remain unresolved. Mirror patterns use
/// their own measured layout.
/// </summary>
internal static class PatternFeatureParser
{
    internal const string LinearPatternClassName = PatternSegments.LinearPatternClassName;
    internal const uint LinearPatternTypeId = PatternSegments.LinearPatternTypeId;
    internal const string CircularPatternClassName = PatternSegments.CircularPatternClassName;
    internal const uint CircularPatternTypeId = PatternSegments.CircularPatternTypeId;
    internal const string MirrorPatternClassName = MirrorPatternSegments.ClassName;
    internal const uint MirrorPatternTypeId = MirrorPatternSegments.TypeId;

    /// <summary>Parses the controlled linear-pattern layout (D1/D2 counts and spacings).</summary>
    public static LinearPatternFeatureDefinitionData? ParseLinearPattern(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != LinearPatternClassName || typeId != LinearPatternTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x72) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();
        var integerParams = nodes.Where(e => e.ClassName == "moIntegerParameter_c").ToList();
        var compEdges = nodes.Where(e => e.ClassName == "moCompEdge_c").ToList();
        if (integerParams.Count != 1 || compEdges.Count != 1) return null;

        byte[] integerRaw = SliceElement(doc, integerParams[0], end, PatternSegments.LinearIntegerParameterLength);
        if (integerRaw.Length != PatternSegments.LinearIntegerParameterLength) return null;

        uint? count1 = ReadNamedInteger(integerRaw, PatternSegments.LinearD1CountParameterOffset, "D1");
        double? spacing1 = ReadNamedDouble(integerRaw, PatternSegments.LinearD1SpacingParameterOffset, "D3");
        uint? count2 = ReadNamedInteger(integerRaw, PatternSegments.LinearD2CountParameterOffset, "D2");
        double? spacing2 = ReadNamedDouble(integerRaw, PatternSegments.LinearD2SpacingParameterOffset, "D4");
        if (!count1.HasValue || count1.Value < 2 || count1.Value > 1000 ||
            !spacing1.HasValue || spacing1.Value <= 0.0 ||
            !count2.HasValue || count2.Value < 1 || count2.Value > 1000 ||
            !spacing2.HasValue || spacing2.Value <= 0.0)
            return null;

        byte[] compEdge = SliceElement(doc, compEdges[0], end, PatternSegments.LinearCompEdgeLength);
        if (compEdge.Length != PatternSegments.LinearCompEdgeLength) return null;
        // Direction-reference envelope: the measured D5/D6 slots must be present and zero.
        if (ReadNamedDouble(compEdge, 1534, "D5") != 0.0 ||
            ReadNamedDouble(compEdge, 3117, "D6") != 0.0)
            return null;

        var definition = new LinearPatternFeatureDefinitionData
        {
            D1TotalInstances = (int)count1.Value,
            D1Spacing = spacing1.Value,
            D2TotalInstances = (int)count2.Value,
            D2Spacing = spacing2.Value,
            D1AxisType = 1, // controlled direction references are component edges
            D1Axis = new FeatureReferenceData { EntityType = SelectionEntityType.Edge },
            D2AxisType = count2.Value > 1 ? 1 : (int?)null,
            D2Axis = count2.Value > 1
                ? new FeatureReferenceData { EntityType = SelectionEntityType.Edge }
                : null
        };
        foreach (string name in new[]
        {
            nameof(LinearPatternFeatureDefinitionData.D1TotalInstances),
            nameof(LinearPatternFeatureDefinitionData.D1Spacing),
            nameof(LinearPatternFeatureDefinitionData.D2TotalInstances),
            nameof(LinearPatternFeatureDefinitionData.D2Spacing),
            nameof(LinearPatternFeatureDefinitionData.D1AxisType),
            nameof(LinearPatternFeatureDefinitionData.D1Axis)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        if (count2.Value > 1)
        {
            definition.MarkResolvedInternal(nameof(LinearPatternFeatureDefinitionData.D2AxisType));
            definition.MarkResolvedInternal(nameof(LinearPatternFeatureDefinitionData.D2Axis));
        }
        return definition;
    }

    /// <summary>Parses the measured circular-pattern layouts (equal spacing on/off).</summary>
    public static CircularPatternFeatureDefinitionData? ParseCircularPattern(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != CircularPatternClassName || typeId != CircularPatternTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x72) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();
        var integerParams = nodes.Where(e => e.ClassName == "moIntegerParameter_c").ToList();
        var compEdges = nodes.Where(e => e.ClassName == "moCompEdge_c").ToList();
        var angleParams = nodes.Where(e => e.ClassName == "moAngleParameter_c").ToList();
        if (integerParams.Count != 1 || compEdges.Count != 1 || angleParams.Count != 1) return null;

        byte[] integerRaw = SliceElement(doc, integerParams[0], end, PatternSegments.CircularIntegerParameterLength);
        byte[] compEdge = SliceElement(doc, compEdges[0], end,
            compEdges[0].DataStart >= 0 ? Math.Max(0, NextTagOffset(doc, compEdges[0], end) - compEdges[0].DataStart) : 0);
        byte[] angleRaw = SliceElement(doc, angleParams[0], end,
            angleParams[0].DataStart >= 0 ? Math.Max(0, NextTagOffset(doc, angleParams[0], end) - angleParams[0].DataStart) : 0);
        if (integerRaw.Length != PatternSegments.CircularIntegerParameterLength ||
            compEdge.Length == 0 || angleRaw.Length == 0)
            return null;

        uint? count = ReadNamedInteger(integerRaw, PatternSegments.CircularCountParameterOffset, "D1");
        double? spacing = ReadNamedDouble(compEdge, PatternSegments.CircularSpacingParameterOffset, "D3")
                          ?? ReadNamedDoubleByName(compEdge, "D3");
        double? perInstanceAngle = ReadNamedDouble(angleRaw, PatternSegments.CircularAngleParameterOffset, "D2");
        if (!count.HasValue || count.Value < 2 || count.Value > 1000 ||
            !spacing.HasValue || spacing.Value <= 0.0 || spacing.Value > 2.0 * Math.PI ||
            !perInstanceAngle.HasValue || perInstanceAngle.Value < 0.0)
            return null;

        // Layout A: equal-spacing samples (old 4x90 baseline or the native 4x90/6x60/equal-on
        // layout, which adds four bytes to moCompEdge_c). Layout B: equal-spacing-off sample.
        bool layoutA = compEdge.Length is 3830 or 3834;
        bool layoutB = compEdge.Length == 3724 && angleRaw.Length == 4174;
        if (!layoutA && !layoutB) return null;
        if (ReadNamedDoubleByName(compEdge, "D6") != 0.0) return null;

        // Native equal-on API Spacing = D3 and per-instance angle = D3/(count-1);
        // native equal-off API Spacing = D2 and per-instance angle = D3/count.
        bool equalSpacing;
        double apiSpacing;
        if (Math.Abs(perInstanceAngle.Value - spacing.Value / (count.Value - 1)) <= 1e-9)
        {
            equalSpacing = true;
            apiSpacing = spacing.Value;
        }
        else if (Math.Abs(perInstanceAngle.Value - spacing.Value / count.Value) <= 1e-9)
        {
            equalSpacing = false;
            apiSpacing = perInstanceAngle.Value;
        }
        else return null;

        var definition = new CircularPatternFeatureDefinitionData
        {
            TotalInstances = (int)count.Value,
            Spacing = apiSpacing,
            EqualSpacing = equalSpacing,
            AxisType = 1, // controlled axis reference is a component edge
            Axis = new FeatureReferenceData { EntityType = SelectionEntityType.Edge }
        };
        foreach (string name in new[]
        {
            nameof(CircularPatternFeatureDefinitionData.TotalInstances),
            nameof(CircularPatternFeatureDefinitionData.Spacing),
            nameof(CircularPatternFeatureDefinitionData.EqualSpacing),
            nameof(CircularPatternFeatureDefinitionData.AxisType),
            nameof(CircularPatternFeatureDefinitionData.Axis)
        })
        {
            definition.MarkResolvedInternal(name);
        }
        return definition;
    }

    /// <summary>
    /// Parses the controlled mirror-pattern layout (<c>moMirrorPattern_c</c> / FolderHeader
    /// TypeId 0x57, stream 0x72). The measured sample mirrors one source extrusion across the
    /// Right Plane. The parser binds the plane-reference form (MirrorPlaneType=1), a
    /// presence-only plane reference and one presence-only source-feature reference; the
    /// source persistent-reference blob and plane block must match the measured bytes, and
    /// geometry-pattern/feature-scope flags remain unresolved.
    /// </summary>
    public static MirrorPatternFeatureDefinitionData? ParseMirrorPattern(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != MirrorPatternClassName || typeId != MirrorPatternTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x72) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var feature = doc.AllElements.FirstOrDefault(e =>
            e.ClassName == MirrorPatternClassName && e.TagOffset == start);
        if (feature == null) return null;

        // The feature name is variable-length; the payload ends at the next class tag (or the
        // feature/folder end). The measured rest after the header is always 1163 bytes.
        int payloadEnd = end;
        foreach (var candidate in doc.AllElements)
        {
            if (candidate.TagOffset > feature.TagOffset && candidate.TagOffset < payloadEnd)
                payloadEnd = candidate.TagOffset;
        }
        byte[] payload = MfcPrimitives.CopyRange(doc.SourceData, feature.DataStart, payloadEnd);
        if (payload.Length < 2) return null;

        // Header: schema 0x8004 | WChar name | double 2.0/-2.0 | TypeId 0x57 | u32 0 | empty WChar.
        if (BitConverter.ToUInt16(payload, 0) != 0x8004) return null;
        int cursor = 2;
        string? name = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (string.IsNullOrEmpty(name)) return null;
        if (cursor + 8 + 4 + 4 + 4 > payload.Length) return null;
        double headerDouble = BitConverter.ToDouble(payload, cursor);
        if (Math.Abs(Math.Abs(headerDouble) - 2.0) > 1e-12) return null;
        cursor += 8;
        if (BitConverter.ToUInt32(payload, cursor) != MirrorPatternSegments.TypeId) return null;
        cursor += 4;
        if (BitConverter.ToUInt32(payload, cursor) != 0) return null;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (!string.IsNullOrEmpty(empty)) return null;
        if (payload.Length - cursor != MirrorPatternSegments.HeaderRestLength) return null;

        byte[] rest = MfcPrimitives.CopyRange(payload, cursor, payload.Length);
        if (rest.Length != MirrorPatternSegments.HeaderRestLength) return null;
        // Feature id magic and allocated id inside the measured rest.
        if (rest[105] != 0x56 || rest[106] != 0x68 || rest[107] != 0xDD || rest[108] != 0x01)
            return null;
        if (BitConverter.ToUInt32(rest, MirrorPatternSegments.FeatureIdOffsetInRest) == 0)
            return null;

        // Source-feature persistent-reference blob and Right-Plane reference block, relative
        // to the rest start so custom feature-name lengths are accepted.
        int sourceOffset = MirrorPatternSegments.SourceReferenceOffset - MirrorPatternSegments.HeaderRestOffset;
        int planeOffset = MirrorPatternSegments.PlaneReferenceOffset - MirrorPatternSegments.HeaderRestOffset;
        if (!RangeEquals(rest, sourceOffset, MirrorPatternSegments.SourceReferenceData)) return null;
        if (!RangeEquals(rest, planeOffset, MirrorPatternSegments.PlaneReferenceData)) return null;

        // Every other measured rest byte must match the controlled template. The allocated
        // feature-id slot and the single main/resave archive byte are allowed to vary.
        if (MirrorPatternSegments.HeaderRest.Length != rest.Length) return null;
        for (int i = 0; i < rest.Length; i++)
        {
            if (i >= MirrorPatternSegments.FeatureIdOffsetInRest &&
                i < MirrorPatternSegments.FeatureIdOffsetInRest + 4) continue;
            if (i == MirrorPatternSegments.ResaveVariantRestOffset) continue;
            if (rest[i] != MirrorPatternSegments.HeaderRest[i]) return null;
        }

        var definition = new MirrorPatternFeatureDefinitionData
        {
            MirrorPlaneType = 1,
            Plane = new FeatureReferenceData { EntityType = SelectionEntityType.Plane },
            PatternFeatureArray = new[]
            {
                new FeatureReferenceData { EntityType = SelectionEntityType.None }
            }
        };
        definition.MarkResolvedInternal(nameof(MirrorPatternFeatureDefinitionData.MirrorPlaneType));
        definition.MarkResolvedInternal(nameof(MirrorPatternFeatureDefinitionData.Plane));
        definition.MarkResolvedInternal(nameof(MirrorPatternFeatureDefinitionData.PatternFeatureArray));
        return definition;
    }

    private static bool RangeEquals(byte[] data, int offset, byte[] expected)
    {
        if (offset < 0 || expected == null || offset + expected.Length > data.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (data[offset + i] != expected[i]) return false;
        return true;
    }

    private static byte[] SliceElement(
        MfcDocument doc, MfcElement element, int featureEnd, int expectedLength)
    {
        int start = element.DataStart;
        if (start < 0 || expectedLength <= 0 ||
            start + expectedLength > featureEnd ||
            start + expectedLength > doc.SourceData.Length)
            return Array.Empty<byte>();
        return MfcPrimitives.CopyRange(doc.SourceData, start, start + expectedLength);
    }

    /// <summary>Validates the common named-parameter header and returns the scalar offset.</summary>
    private static int? ReadNamedValueOffset(byte[] data, int offset, string expectedName)
    {
        if (offset < 0 || offset + 6 > data.Length ||
            BitConverter.ToUInt16(data, offset) != 0x8004 ||
            !MfcPrimitives.IsWCharPrefix(data, offset + 2))
            return null;

        int position = offset + 2;
        string? name = MfcPrimitives.ReadWChar(data, ref position);
        if (!string.Equals(name, expectedName, StringComparison.Ordinal)) return null;

        if (position + 22 > data.Length ||
            BitConverter.ToDouble(data, position) != 2.0 ||
            BitConverter.ToUInt32(data, position + 8) != uint.MaxValue ||
            BitConverter.ToUInt32(data, position + 12) != 0 ||
            !MfcPrimitives.IsWCharPrefix(data, position + 16) || data[position + 19] != 0 ||
            BitConverter.ToUInt16(data, position + 20) != 0)
            return null;

        return position + 22;
    }

    private static int NextTagOffset(MfcDocument doc, MfcElement element, int featureEnd)
    {
        var next = doc.AllElements
            .Where(e => e.TagOffset > element.TagOffset)
            .OrderBy(e => e.TagOffset)
            .FirstOrDefault();
        return next != null && next.TagOffset < featureEnd ? next.TagOffset : featureEnd;
    }

    private static double? ReadNamedDoubleByName(byte[] data, string expectedName)
    {
        for (int i = 0; i + 6 <= data.Length; i++)
        {
            if (data[i] != 0x04 || data[i + 1] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(data, i + 2)) continue;
            int pos = i + 2;
            if (!string.Equals(MfcPrimitives.ReadWChar(data, ref pos), expectedName,
                    StringComparison.Ordinal)) continue;
            int? valueOffset = ReadNamedValueOffset(data, i, expectedName);
            if (!valueOffset.HasValue || valueOffset.Value + 8 > data.Length) return null;
            double value = BitConverter.ToDouble(data, valueOffset.Value);
            return double.IsNaN(value) || double.IsInfinity(value) ? null : value;
        }
        return null;
    }

    private static uint? ReadNamedInteger(byte[] data, int offset, string expectedName)
    {
        int? valueOffset = ReadNamedValueOffset(data, offset, expectedName);
        if (!valueOffset.HasValue || valueOffset.Value + 4 > data.Length) return null;
        return BitConverter.ToUInt32(data, valueOffset.Value);
    }

    private static double? ReadNamedDouble(byte[] data, int offset, string expectedName)
    {
        int? valueOffset = ReadNamedValueOffset(data, offset, expectedName);
        if (!valueOffset.HasValue || valueOffset.Value + 8 > data.Length) return null;
        double value = BitConverter.ToDouble(data, valueOffset.Value);
        return double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;
    }
}
