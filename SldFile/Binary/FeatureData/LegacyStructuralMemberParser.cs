using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Read-only parser for legacy tutorial <c>moWeldMemberFeat_c</c> nodes.
/// <para>
/// Legacy tutorial streams use FolderHeader TypeIds that differ from the measured
/// 2026 structural-member layout (0x4D), so the modern parser rejects them. This
/// parser is registered as a feature-level fallback and is invoked only when the
/// modern parser left <see cref="FeatureFolder.FeatureData"/> null.
/// </para>
/// <para>
/// Promoted fields are limited to values with a strict positional descriptor:
/// the first persisted <c>.sldlfp</c> profile path after the member tag, and the
/// first <c>moWeldmentContours_c</c> group-count u32 that occurs before the next
/// profile path. Group entries are opaque placeholders (no handles are invented).
/// Corner treatment, rotation, configuration name, path segments and locate point
/// remain unresolved until a controlled variation proves their slots.
/// </para>
/// </summary>
internal static class LegacyStructuralMemberParser
{
    internal const string ClassName = "moWeldMemberFeat_c";

    /// <summary>
    /// Measured legacy family descriptors:
    /// (ResolvedFeatures stream version, FolderHeader TypeId) ->
    /// (corner-treatment u32 offset from the sldlfp suffix, rotation f64 offset).
    /// Offsets were proven by native IDispatch ModifyDefinition probes that were
    /// saved, reopened and verified to persist:
    /// box2 0x02A6/0x36, box3 0x029D/0x33, loop 0x0076/0x17,
    /// leg 0x008F/0x2A, framework 0x0074/0x159.
    /// </summary>
    private static readonly Dictionary<(ushort StreamVersion, uint TypeId), (int CornerOffset, int RotationOffset)> Families =
        new()
        {
            [(0x02A6, 0x36)] = (-0xF8, -0xE6),
            [(0x029D, 0x33)] = (-0xFC, -0xEA),
            [(0x0076, 0x17)] = (-0xF8, -0xE6),
            [(0x008F, 0x2A)] = (-0x1AA, -0xAA),
            [(0x0074, 0x159)] = (-0x1AA, -0xAA)
        };

    public static IFeatureData? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null || className != ClassName || start < 0)
            return null;

        var element = doc.AllElements.FirstOrDefault(e =>
            e.TagByte == 0x01 && e.TagOffset == start && e.ClassName == ClassName);
        if (element == null)
            return null;

        var header = ResolvedFeaturesSchemaMapper.ParseFolderHeader(element);
        if (!header.IsValid || header.TypeId == 0)
            return null;
        if (typeId != 0 && header.TypeId != typeId)
            return null;

        byte[] data = doc.SourceData;
        ushort streamVersion = data.Length >= 2 ? BitConverter.ToUInt16(data, 0) : (ushort)0;
        if (!Families.TryGetValue((streamVersion, header.TypeId), out var family))
            return null;

        var paths = FindProfilePaths(data, start, 2);
        if (paths.Count == 0)
            return null;

        var definition = new StructuralMemberFeatureDefinitionData
        {
            SerializedClassName = ClassName,
            SerializedTypeId = header.TypeId,
            WeldmentProfilePath = paths[0].Value
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.WeldmentProfilePath = true;

        int? secondPathOffset = paths.Count >= 2 ? paths[1].PrefixOffset : (int?)null;
        var contours = MfcPrimitives.ScanMfcTags(data).FirstOrDefault(t =>
            t.ClassName == "moWeldmentContours_c" && t.TagOffset > start);

        int? groupCount = null;
        if (contours == null)
        {
            // No contour block anywhere after the member tag: measured tutorial
            // members report zero groups in this layout.
            groupCount = 0;
        }
        else if (secondPathOffset.HasValue && contours.TagOffset < secondPathOffset.Value)
        {
            int dataStart = contours.DataStart;
            if (dataStart >= 0 && dataStart + 4 <= data.Length)
            {
                uint count = BitConverter.ToUInt32(data, dataStart);
                if (count <= 64)
                    groupCount = (int)count;
            }
        }
        else if (secondPathOffset.HasValue)
        {
            // The first contour block belongs to a later member.
            groupCount = 0;
        }

        if (groupCount.HasValue)
        {
            var groups = new EntityReference[groupCount.Value];
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = new EntityReference
                {
                    EntityType = SelectionEntityType.None,
                    EntityId = 0,
                    Name = $"StructuralMemberGroup{i + 1}"
                };
            }
            definition.Groups = Array.AsReadOnly(groups);
            definition.Resolution.Groups = true;
        }

        int suffix = paths[0].SuffixOffset;
        int cornerOffset = suffix + family.CornerOffset;
        if (cornerOffset >= 0 && cornerOffset + 4 <= data.Length)
        {
            uint cornerValue = BitConverter.ToUInt32(data, cornerOffset);
            if (cornerValue <= 2)
            {
                definition.ApplyCornerTreatment = cornerValue != 0;
                definition.CornerTreatmentType = (int)cornerValue;
                definition.Resolution.ApplyCornerTreatment = true;
                definition.Resolution.CornerTreatmentType = true;
            }
        }

        int rotationOffset = suffix + family.RotationOffset;
        if (rotationOffset >= 0 && rotationOffset + 8 <= data.Length)
        {
            double rotation = BitConverter.ToDouble(data, rotationOffset);
            if (!double.IsNaN(rotation) && !double.IsInfinity(rotation) &&
                Math.Abs(rotation) <= Math.PI * 2 + 1e-9)
            {
                definition.RotationAngle = rotation;
                definition.Resolution.RotationAngle = true;
            }
        }

        return definition;
    }

    private static List<(int PrefixOffset, int SuffixOffset, string Value)> FindProfilePaths(
        byte[] data, int start, int max)
    {
        var paths = new List<(int PrefixOffset, int SuffixOffset, string Value)>();
        for (int i = Math.Max(0, start); i + 4 <= data.Length && paths.Count < max; i++)
        {
            if (!MfcPrimitives.IsWCharPrefix(data, i))
                continue;
            int cursor = i;
            string? value = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
            if (string.IsNullOrEmpty(value) || value!.Length > 512)
                continue;
            if (!value.EndsWith(".sldlfp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (paths.Any(p => string.Equals(p.Value, value, StringComparison.OrdinalIgnoreCase)))
                continue;
            int stringStart = cursor - value.Length * 2;
            int suffixOffset = stringStart + (value.Length - 6) * 2;
            if (stringStart < 0 || suffixOffset < 0 || suffixOffset + 6 * 2 > data.Length)
                continue;
            paths.Add((i, suffixOffset, value!));
        }
        return paths;
    }
}
