using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Recovers mate records serialized with an MFC class reference instead of an
/// inline class tag (the MFC scanner does not expose them as elements).
///
/// Controlled evidence:
///   - 2026 streams use <c>C1 80 06 80 FF FE FF ...</c> for moMateCoincident
///     (TestFiles/Solidworks2026 assembly Coincident5/6, run6 Coincident5/6/重合1);
///   - the 2012 stream uses <c>EA 80 4D 80 FF FE FF ...</c> for moMateCoincidentM
///     (models/assembly.SLDASM Coincident5/6).
/// Both codes occur only at mate records in these samples and only when the
/// corresponding inline mate class is present in the same stream.
///
/// 2026 records are accepted only with exactly two RefPlane name pairs; alignment
/// is additionally decoded when the controlled 174-byte header signature holds.
/// 2012 records are accepted only with the legacy mate header shape and expose
/// class/display plus an inferred moPlaneRef_w wrapper; face topology stays
/// unresolved because the nested reference chain is class-ref encoded.
/// </summary>
internal static class MatesListClassRefParser
{
    private static readonly byte[] Coincident2026Prefix = { 0xC1, 0x80, 0x06, 0x80 };
    private static readonly byte[] Coincident2012Prefix = { 0xEA, 0x80, 0x4D, 0x80 };
    private const int MaxClassRefRecords = 64;

    internal static List<(int Offset, MateEntry Entry)> Scan(byte[] data, MfcDocument doc)
    {
        var result = new List<(int, MateEntry)>();
        if (data == null || doc == null) return result;
        bool has2026 = HasInlineClass(doc, "moMateCoincident");
        bool has2012 = HasInlineClass(doc, "moMateCoincidentM");
        bool hasPath = HasInlineClass(doc, "moMatePath_c");
        bool hasHinge = HasInlineClass(doc, "moMateHinge");
        bool hasPlanarAngle = HasInlineClass(doc, "moMatePlanarAngleDim_c");
        if (!has2026 && !has2012 && !hasPath && !hasHinge && !hasPlanarAngle) return result;

        for (int i = 0; i + 12 < data.Length && result.Count < MaxClassRefRecords; i++)
        {
            if (Matches(data, i, Coincident2026Prefix) && MfcPrimitives.IsWCharPrefix(data, i + 4))
            {
                if (!has2026) continue;
                var entry = TryRead2026(data, i);
                if (entry != null) result.Add((i, entry));
            }
            else if (Matches(data, i, Coincident2012Prefix) && MfcPrimitives.IsWCharPrefix(data, i + 4))
            {
                if (!has2012) continue;
                var entry = TryRead2012(data, i);
                if (entry != null) result.Add((i, entry));
            }
        }
        if (hasPath)
            ScanPath(data, result);
        if (hasHinge)
            ScanMechanicalClassRefs(data, result, "Hinge", 22u, isAngle: false, allowZero: false);
        if (hasPlanarAngle)
            ScanMechanicalClassRefs(data, result, "Angle", 6u, isAngle: true, allowZero: false);
        return result;
    }

    /// <summary>
    /// Recovers repeated Hinge/PlanarAngle records serialized with a document-local
    /// two-byte class reference followed by 06 80 and the display WChar. The code
    /// is discovered from the first matching class-reference record.
    /// </summary>
    private static void ScanMechanicalClassRefs(
        byte[] data, List<(int Offset, MateEntry Entry)> result,
        string displayPrefix, uint typeId, bool isAngle, bool allowZero)
    {
        byte code0 = 0, code1 = 0;
        bool found = false;
        for (int i = 0; i + 12 < data.Length; i++)
        {
            if ((data[i + 1] != 0x80 && data[i + 1] != 0x81) ||
                data[i + 2] != 0x06 || data[i + 3] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(data, i + 4))
                continue;
            int cursor = i + 4;
            string? name = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
            if (name == null || !name.StartsWith(displayPrefix, StringComparison.Ordinal) ||
                name.Length == displayPrefix.Length)
                continue;
            code0 = data[i];
            code1 = data[i + 1];
            found = true;
            break;
        }
        if (!found) return;

        for (int i = 0; i + 12 < data.Length && result.Count < MaxClassRefRecords; i++)
        {
            if (data[i] != code0 || data[i + 1] != code1 ||
                data[i + 2] != 0x06 || data[i + 3] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(data, i + 4))
                continue;
            int cursor = i + 4;
            string? display = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
            if (display == null || !display.StartsWith(displayPrefix, StringComparison.Ordinal) ||
                display.Length == displayPrefix.Length)
                continue;
            int windowEnd = FindMechanicalWindowEnd(data, i + 4, code0, code1);
            if (windowEnd <= i) continue;
            string className = typeId == 22u ? "moMateHinge" : "moMatePlanarAngleDim_c";
            var entities = MateStructureDecoder.ReadMechanicalEntities(data, i, windowEnd);
            double? value = null;
            if (isAngle)
                value = MateStructureDecoder.ReadFirstD1(data, i, windowEnd, isAngle: true, allowZero: allowZero);
            var structure = new MateStructureData
            {
                ClassName = className,
                DisplayName = display,
                Layout = MateStructureLayout.MeasuredMechanical2026,
                HeaderStart = i,
                HeaderLength = windowEnd - i,
                HeaderFields = Array.Empty<MateHeaderField>(),
                References = Array.Empty<MateReferenceStructure>(),
                DeferredReferenceTexts = Array.Empty<string>(),
                WCharStrings = Array.Empty<string>(),
                MateTypeId = typeId,
                Angle = value,
                EntityReferences = entities.AsReadOnly()
            };
            result.Add((i, new MateEntry
            {
                ClassName = className,
                DisplayName = display,
                RawData = MfcPrimitives.CopyRange(data, i, windowEnd),
                References = Array.Empty<MateReferenceEntry>(),
                Structure = structure
            }));
        }
    }

    private static int FindMechanicalWindowEnd(byte[] data, int from, byte code0, byte code1)
    {
        for (int i = Math.Max(0, from); i + 8 < data.Length; i++)
        {
            if (data[i] == code0 && data[i + 1] == code1 &&
                data[i + 2] == 0x06 && data[i + 3] == 0x80 &&
                MfcPrimitives.IsWCharPrefix(data, i + 4))
                return i;
            if (MfcPrimitives.IsMfcTag(data, i))
            {
                int position = i;
                var tag = MfcPrimitives.ReadMfcTag(data, ref position);
                if (tag != null && tag.ClassName.StartsWith("moMate", StringComparison.Ordinal) &&
                    !tag.ClassName.StartsWith("moMaterial", StringComparison.Ordinal) &&
                    tag.ClassName != "moMateEntityHelpData_c" &&
                    tag.ClassName != "moMateAnalysisData_c")
                    return i;
            }
        }
        return data.Length;
    }

    /// <summary>
    /// Recovers the measured PathMate2..N class-reference records. The inline
    /// moMatePath_c record (PathMate1) is parsed by the main element scanner; the
    /// repeated records use a document-local two-byte class reference followed by
    /// <c>0A 80</c> and the display WChar. The code is discovered from the first
    /// PathMateN class-reference record rather than hard-coded.
    /// </summary>
    private static void ScanPath(byte[] data, List<(int Offset, MateEntry Entry)> result)
    {
        byte prefix0 = 0, prefix1 = 0;
        bool found = false;
        for (int i = 0; i + 12 < data.Length; i++)
        {
            if (data[i + 1] != 0x80 || data[i + 2] != 0x0A || data[i + 3] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(data, i + 4))
                continue;
            int cursor = i + 4;
            string? name = MfcPrimitives.ReadWChar(data, ref cursor, data.Length);
            if (name == null || !name.StartsWith("PathMate", StringComparison.Ordinal))
                continue;
            prefix0 = data[i];
            prefix1 = data[i + 1];
            found = true;
            break;
        }
        if (!found) return;

        for (int i = 0; i + 12 < data.Length && result.Count < MaxClassRefRecords; i++)
        {
            if (data[i] != prefix0 || data[i + 1] != prefix1 ||
                data[i + 2] != 0x0A || data[i + 3] != 0x80 ||
                !MfcPrimitives.IsWCharPrefix(data, i + 4))
                continue;
            var entry = TryReadPath(data, i, prefix0, prefix1);
            if (entry != null) result.Add((i, entry));
        }
    }

    private static MateEntry? TryReadPath(byte[] data, int classRefStart, byte prefix0, byte prefix1)
    {
        if (!MateStructureDecoder.TryReadDisplayName(data, classRefStart + 4, out string displayName, out _))
            return null;
        int windowEnd = FindPathWindowEnd(data, classRefStart + 4, prefix0, prefix1);
        if (windowEnd <= classRefStart) return null;
        var strings = ReadWChars(data, classRefStart, windowEnd);
        var component = strings.FirstOrDefault(e => IsPathComponentName(e.Value));
        if (component == null) return null;
        var selection = strings.FirstOrDefault(e =>
            e.Offset > component.Offset && e.Value.StartsWith("Point", StringComparison.Ordinal) &&
            e.Value.IndexOf('@') > 0);
        if (selection == null) return null;
        string feature = selection.Value.Substring(0, selection.Value.IndexOf('@'));

        var entities = new List<MateEntityReferenceStructure>
        {
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.SketchEntity,
                ComponentInstanceName = component.Value,
                FeatureName = feature,
                SelectionName = selection.Value
            },
            new MateEntityReferenceStructure
            {
                Kind = MateReferenceEntityKind.Path
            }
        };
        var structure = new MateStructureData
        {
            ClassName = "moMatePath_c",
            DisplayName = displayName,
            Layout = MateStructureLayout.Path2026,
            HeaderStart = classRefStart,
            HeaderLength = Math.Max(0, windowEnd - classRefStart),
            HeaderFields = Array.Empty<MateHeaderField>(),
            References = new[]
            {
                new MateReferenceStructure
                {
                    WrapperClassName = "moPointRef_w",
                    Kind = MateReferenceEntityKind.SketchEntity,
                    ChainClassNames = Array.Empty<string>(),
                    WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray()
                },
                new MateReferenceStructure
                {
                    WrapperClassName = "moGeneralCurveRef_w",
                    Kind = MateReferenceEntityKind.Path,
                    ChainClassNames = Array.Empty<string>(),
                    WCharStrings = Array.Empty<string>()
                }
            },
            DeferredReferenceTexts = Array.Empty<string>(),
            WCharStrings = strings.Select(s => s.Value).Distinct(StringComparer.Ordinal).ToArray(),
            MateTypeId = 15u,
            // Repeated path records share the free D1 state of the inline specimen.
            PathValue = 0,
            PathIsFree = true,
            EntityReferences = entities.AsReadOnly()
        };
        return new MateEntry
        {
            ClassName = "moMatePath_c",
            DisplayName = displayName,
            RawData = MfcPrimitives.CopyRange(data, classRefStart, windowEnd),
            References = new[]
            {
                new MateReferenceEntry
                {
                    WrapperClassName = "moPointRef_w",
                    EntityClassNames = Array.Empty<string>()
                },
                new MateReferenceEntry
                {
                    WrapperClassName = "moGeneralCurveRef_w",
                    EntityClassNames = Array.Empty<string>()
                }
            },
            Structure = structure
        };
    }

    private sealed class PathWChar
    {
        public int Offset { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private static List<PathWChar> ReadWChars(byte[] data, int start, int end)
    {
        var result = new List<PathWChar>();
        int limit = Math.Min(end, data.Length);
        int i = Math.Max(0, start);
        while (i + 4 <= limit)
        {
            if (MfcPrimitives.IsWCharPrefix(data, i))
            {
                int cursor = i;
                string? value = MfcPrimitives.ReadWChar(data, ref cursor, limit);
                if (value != null && value.Length > 0 && value.Length <= 300)
                {
                    result.Add(new PathWChar { Offset = i, Value = value });
                    i = cursor;
                    continue;
                }
            }
            i++;
        }
        return result;
    }

    private static bool IsPathComponentName(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 220) return false;
        if (value.IndexOf('@') <= 0) return false;
        string[] prefixes = { "Point", "Line", "Face", "Plane", "Axis", "Curve" };
        foreach (var prefix in prefixes)
            if (value.StartsWith(prefix, StringComparison.Ordinal)) return false;
        return true;
    }

    private static int FindPathWindowEnd(byte[] data, int from, byte prefix0, byte prefix1)
    {
        for (int i = Math.Max(0, from); i + 8 < data.Length; i++)
        {
            if (data[i] == prefix0 && data[i + 1] == prefix1 &&
                data[i + 2] == 0x0A && data[i + 3] == 0x80 &&
                MfcPrimitives.IsWCharPrefix(data, i + 4))
                return i;
            if (MfcPrimitives.IsMfcTag(data, i))
            {
                int position = i;
                var tag = MfcPrimitives.ReadMfcTag(data, ref position);
                if (tag != null && tag.ClassName.StartsWith("moMate", StringComparison.Ordinal) &&
                    !tag.ClassName.StartsWith("moMaterial", StringComparison.Ordinal) &&
                    tag.ClassName != "moMateEntityHelpData_c" &&
                    tag.ClassName != "moMateAnalysisData_c")
                    return i;
            }
        }
        return data.Length;
    }

    private static MateEntry? TryRead2026(byte[] data, int classRefStart)
    {
        // Payload begins at the 06 80 display prefix; TryReadDisplayName scans
        // forward for the WChar prefix and returns the header start.
        int payloadStart = classRefStart + 2;
        if (!MateStructureDecoder.TryReadDisplayName(data, payloadStart, out string displayName, out int headerStart))
            return null;

        int windowEnd = FindWindowEnd(data, classRefStart + 8);
        if (windowEnd <= headerStart) return null;
        var pairs = MateStructureDecoder.ReadPlanePairs(data, classRefStart, windowEnd);
        if (pairs.Count != 2) return null;
        if (string.Equals(pairs[0].ComponentInstanceName, pairs[1].ComponentInstanceName, StringComparison.Ordinal))
            return null;
        if (string.Equals(pairs[0].SelectionName, pairs[1].SelectionName, StringComparison.Ordinal))
            return null;

        uint? alignmentId = MateStructureDecoder.TryReadControlledHeaderForClassRef(data, headerStart, out uint alignment)
            ? alignment
            : (uint?)null;

        var reference = new MateReferenceStructure
        {
            WrapperClassName = "moPlaneRef_w",
            Kind = MateReferenceEntityKind.Plane,
            ChainClassNames = Array.Empty<string>(),
            WCharStrings = Array.Empty<string>()
        };
        var structure = new MateStructureData
        {
            ClassName = "moMateCoincident",
            DisplayName = displayName,
            Layout = MateStructureLayout.PlaneReference2026,
            HeaderStart = headerStart,
            HeaderLength = 0,
            HeaderFields = Array.Empty<MateHeaderField>(),
            References = new[] { reference },
            DeferredReferenceTexts = Array.Empty<string>(),
            WCharStrings = Array.Empty<string>(),
            MateTypeId = 0u,
            AlignmentId = alignmentId,
            EntityReferences = pairs.AsReadOnly()
        };
        return new MateEntry
        {
            ClassName = "moMateCoincident",
            DisplayName = displayName,
            RawData = MfcPrimitives.CopyRange(data, classRefStart, windowEnd),
            References = new[]
            {
                new MateReferenceEntry
                {
                    WrapperClassName = "moPlaneRef_w",
                    EntityClassNames = Array.Empty<string>()
                }
            },
            Structure = structure
        };
    }

    private static MateEntry? TryRead2012(byte[] data, int classRefStart)
    {
        int payloadStart = classRefStart + 4;
        if (!MateStructureDecoder.TryReadDisplayName(data, payloadStart, out string displayName, out int headerStart))
            return null;
        if (headerStart + 8 > data.Length) return null;
        // Legacy mate header shape (same fields as the inline moMateCoincidentM).
        if (data[headerStart] != 0 || data[headerStart + 1] != 0 ||
            data[headerStart + 4] != 0 || data[headerStart + 5] != 0x40 ||
            data[headerStart + 7] != 0)
            return null;

        int windowEnd = FindWindowEnd(data, classRefStart + 8);
        var reference = new MateReferenceStructure
        {
            WrapperClassName = "moPlaneRef_w",
            Kind = MateReferenceEntityKind.Face,
            ChainClassNames = Array.Empty<string>(),
            WCharStrings = Array.Empty<string>()
        };
        var structure = new MateStructureData
        {
            ClassName = "moMateCoincidentM",
            DisplayName = displayName,
            Layout = MateStructureLayout.LegacyNested,
            HeaderStart = headerStart,
            HeaderLength = 0,
            HeaderFields = Array.Empty<MateHeaderField>(),
            References = new[] { reference },
            DeferredReferenceTexts = Array.Empty<string>(),
            WCharStrings = Array.Empty<string>(),
            MateTypeId = 0u
        };
        return new MateEntry
        {
            ClassName = "moMateCoincidentM",
            DisplayName = displayName,
            RawData = MfcPrimitives.CopyRange(data, classRefStart, windowEnd),
            References = new[]
            {
                new MateReferenceEntry
                {
                    WrapperClassName = "moPlaneRef_w",
                    EntityClassNames = Array.Empty<string>()
                }
            },
            Structure = structure
        };
    }

    private static int FindWindowEnd(byte[] data, int from)
    {
        for (int i = Math.Max(0, from); i + 8 < data.Length; i++)
        {
            if (Matches(data, i, Coincident2026Prefix) || Matches(data, i, Coincident2012Prefix))
                return i;
            if (MfcPrimitives.IsMfcTag(data, i))
            {
                int position = i;
                var tag = MfcPrimitives.ReadMfcTag(data, ref position);
                if (tag != null &&
                    tag.ClassName.StartsWith("moMate", StringComparison.Ordinal) &&
                    !tag.ClassName.StartsWith("moMaterial", StringComparison.Ordinal))
                    return i;
            }
        }
        return data.Length;
    }

    private static bool HasInlineClass(MfcDocument doc, string className)
    {
        foreach (var element in doc.AllElements)
        {
            if (string.Equals(element.ClassName, className, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool Matches(byte[] data, int offset, byte[] pattern)
    {
        if (offset < 0 || offset + pattern.Length > data.Length) return false;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (data[offset + i] != pattern[i]) return false;
        }
        return true;
    }
}
