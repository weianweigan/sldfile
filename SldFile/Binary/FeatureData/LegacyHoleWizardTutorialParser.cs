using System;
using System.Linq;
using System.Text;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict read-only decoder for the legacy tutorial HoleWizard families. Family matching is by
/// the measured stream version, folder count, FolderHeader type id and header rest length; each
/// exposed field is addressed by an exact class-tag-relative payload offset and must equal the
/// native snapshot value. No legacy authoring is implemented.
/// </summary>
internal static class LegacyHoleWizardTutorialParser
{
    internal static LegacyHoleWizardFamily? MatchFamily(
        MfcDocument doc, int start, int end)
    {
        var hole = doc.AllElements.FirstOrDefault(e =>
            e.ClassName == "moHoleWzd_c" && e.TagOffset == start)
            ?? doc.AllElements.FirstOrDefault(e =>
                e.ClassName == "moHoleWzd_c" && e.TagOffset >= start && e.TagOffset < end);
        if (hole == null) return null;
        byte[] raw = doc.SourceData;
        int headerEnd = NextTag(doc, hole.TagOffset, end);
        if (hole.DataStart + 2 > headerEnd) return null;
        int cursor = hole.DataStart + 2;
        string? name = MfcPrimitives.ReadWChar(raw, ref cursor);
        cursor += 8;
        uint headerTypeId = BitConverter.ToUInt32(raw, cursor);
        cursor += 4;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(raw, ref cursor);
        int restLength = headerEnd - cursor;
        ushort version = raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0;
        ushort folderCount = raw.Length >= 6 ? BitConverter.ToUInt16(raw, 4) : (ushort)0;
        return LegacyHoleWizardTutorialTemplates.Families.FirstOrDefault(f =>
            f.Version == version && f.FolderCount == folderCount &&
            f.HeaderTypeId == headerTypeId && f.RestLength == restLength);
    }


    internal static HoleWizardFeatureDefinitionData? TryParse(
        MfcDocument doc, int start, int end)
    {
        if (doc == null || start < 0 || end <= start || end > doc.SourceData.Length) return null;
        var hole = doc.AllElements.FirstOrDefault(e =>
            e.ClassName == "moHoleWzd_c" && e.TagOffset == start)
            ?? doc.AllElements.FirstOrDefault(e =>
                e.ClassName == "moHoleWzd_c" && e.TagOffset >= start && e.TagOffset < end);
        if (hole == null) return null;

        byte[] raw = doc.SourceData;
        int headerEnd = NextTag(doc, hole.TagOffset, end);
        if (hole.DataStart + 2 > headerEnd) return null;
        int cursor = hole.DataStart + 2;
        string? name = MfcPrimitives.ReadWChar(raw, ref cursor);
        cursor += 8;
        uint headerTypeId = BitConverter.ToUInt32(raw, cursor);
        cursor += 4;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(raw, ref cursor);
        int restLength = headerEnd - cursor;

        ushort version = raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0;
        ushort folderCount = raw.Length >= 6 ? BitConverter.ToUInt16(raw, 4) : (ushort)0;
        var family = LegacyHoleWizardTutorialTemplates.Families.FirstOrDefault(f =>
            f.Version == version && f.FolderCount == folderCount &&
            f.HeaderTypeId == headerTypeId && f.RestLength == restLength);
        if (family == null) return null;

        var definition = new HoleWizardFeatureDefinitionData
        {
            FolderTypeId = headerTypeId,
            Type = family.Type,
            EndCondition = family.EndCondition
        };
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.FolderTypeId));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.Type));
        definition.MarkResolvedInternal(nameof(HoleWizardFeatureDefinitionData.EndCondition));

        foreach (var field in family.Fields)
        {
            int tagOffset = start + field.ClassTagOffset;
            var element = doc.AllElements.FirstOrDefault(e =>
                e.TagOffset == tagOffset && e.ClassName == field.ClassName);
            if (element == null) return null;
            int payloadEnd = NextTag(doc, element.TagOffset, end);
            int length = payloadEnd - element.DataStart;
            if (field.Offset < 0 || field.Offset >= length) return null;
            byte[] payload = MfcPrimitives.CopyRange(raw, element.DataStart, payloadEnd);
            if (field.IsString)
            {
                byte[] expectedBytes = Encoding.Unicode.GetBytes(field.ExpectedString!);
                if (field.Offset + expectedBytes.Length > payload.Length) return null;
                for (int i = 0; i < expectedBytes.Length; i++)
                    if (payload[field.Offset + i] != expectedBytes[i]) return null;
                SetString(definition, field.Property, field.ExpectedString!);
            }
            else
            {
                if (field.Offset + 8 > payload.Length) return null;
                double value = BitConverter.ToDouble(payload, field.Offset);
                if (BitConverter.DoubleToInt64Bits(value) !=
                    BitConverter.DoubleToInt64Bits(field.ExpectedDouble)) return null;
                SetDouble(definition, field.Property, value);
            }
            definition.MarkResolvedInternal(field.Property);
        }
        return definition;
    }

    private static int NextTag(MfcDocument doc, int afterTag, int end)
    {
        int next = end;
        foreach (var e in doc.AllElements)
            if (e.TagOffset > afterTag && e.TagOffset < next) next = e.TagOffset;
        return next;
    }

    private static void SetString(
        HoleWizardFeatureDefinitionData d, string property, string value)
    {
        switch (property)
        {
            case "Standard": d.Standard = value; break;
            case "FastenerType": d.FastenerType = value; break;
            case "FastenerSize": d.FastenerSize = value; break;
            default: throw new InvalidOperationException("Unknown legacy hole string field " + property);
        }
    }

    private static void SetDouble(
        HoleWizardFeatureDefinitionData d, string property, double value)
    {
        switch (property)
        {
            case "ThruHoleDiameter": d.ThruHoleDiameter = value; break;
            case "ThruHoleDepth": d.ThruHoleDepth = value; break;
            case "CounterBoreDiameter": d.CounterBoreDiameter = value; break;
            case "CounterBoreDepth": d.CounterBoreDepth = value; break;
            case "CounterSinkDiameter": d.CounterSinkDiameter = value; break;
            case "CounterSinkAngle": d.CounterSinkAngle = value; break;
            case "ThreadDiameter": d.ThreadDiameter = value; break;
            case "ThreadDepth": d.ThreadDepth = value; break;
            default: throw new InvalidOperationException("Unknown legacy hole numeric field " + property);
        }
    }
}
