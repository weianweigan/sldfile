using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Mfc;

/// <summary>Structural parent-sketch reference decoded from a moProfileFeature_c node.</summary>
internal sealed class ParentSketchReferenceData
{
    /// <summary>Display name stored as the leading WChar of moProfileFeature_c.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Sketch/feature persistent id at payload offset displayNameEnd + 8.
    /// Equals the first persist-reference id of moCompFeature_c in every specimen.
    /// </summary>
    public uint SketchId { get; set; }

    /// <summary>u32 at displayNameEnd + 4 (observed 0x40000000 / 0xC0000000).</summary>
    public uint ClassField { get; set; }

    /// <summary>True when <see cref="SketchId"/> equals the first moCompFeature_c reference id.</summary>
    public bool MatchesComponentFeatureReference { get; set; }
}

/// <summary>
/// Resolves the parent sketch of an extrusion / sheet-metal flange structurally:
///
///   moProfileFeature_c payload (ExtrudeOptions, SelectionModes, SelectionAuthoring,
///   SheetMetal samples):
///       +0   u16 0x8004 + WChar display name
///       +N   u32 class field      (0x40000000 for one-rectangle sketches,
///                                  0xC0000000 for the two-rectangle SelectionSeed)
///       +N+4 u32 sketch persistent id (0x49 in every specimen)
///
///   moCompFeature_c (root component feature list) stores 20-byte persist
///   references; the first reference id equals that sketch id (0x49), followed by
///   the consuming feature class (0x4F extrusion / 0x56 cut / 0x5E sheet-metal
///   base flange). The fromSkt object itself does not store the sketch id (its
///   persist reference after the mapping list is the null reference, id 0).
///
/// The parent association is therefore: the preceding moProfileFeature_ node in
/// the design tree, verified by its decoded sketch id against the root component
/// feature reference. Unknown layouts return null.
/// </summary>
internal static class ParentSketchReferenceParser
{
    internal static ParentSketchReferenceData? TryReadProfileFeature(MfcDocument doc, MfcElement profile)
    {
        if (doc == null || profile == null) return null;
        if (profile.ClassName == null || !profile.ClassName.StartsWith("moProfileFeature_", StringComparison.Ordinal))
            return null;
        byte[] source = doc.SourceData;
        int start = profile.DataStart;
        int end = Math.Min(profile.DataEnd, source.Length);
        if (start < 0 || end - start < 16) return null;

        int nameOffset = start;
        while (nameOffset <= end - 3 && nameOffset < start + 16 && !MfcPrimitives.IsWCharPrefix(source, nameOffset))
            nameOffset++;
        if (nameOffset > end - 3 || !MfcPrimitives.IsWCharPrefix(source, nameOffset)) return null;
        int cursor = nameOffset;
        string? name = MfcPrimitives.ReadWChar(source, ref cursor, end);
        if (name == null) return null;
        int nameEnd = cursor;

        if (nameEnd + 12 > end) return null;
        uint classField = BitConverter.ToUInt32(source, nameEnd + 4);
        uint sketchId = BitConverter.ToUInt32(source, nameEnd + 8);
        if (sketchId == 0 || sketchId == uint.MaxValue) return null;
        // Observed class fields have a zero low 24-bit mantissa (0x40000000 /
        // 0xC0000000); reject other layouts instead of guessing.
        if ((classField & 0x00FFFFFF) != 0) return null;

        var data = new ParentSketchReferenceData
        {
            DisplayName = name,
            SketchId = sketchId,
            ClassField = classField
        };
        var componentIds = ReadComponentFeatureReferenceIds(doc);
        data.MatchesComponentFeatureReference = componentIds.Count > 0 && componentIds[0] == sketchId;
        return data;
    }

    /// <summary>Reads the ordered persist-reference ids of the root moCompFeature_c list.</summary>
    internal static IReadOnlyList<uint> ReadComponentFeatureReferenceIds(MfcDocument doc)
    {
        if (doc == null) return Array.Empty<uint>();
        var component = doc.AllElements.FirstOrDefault(e => e.ClassName == "moCompFeature_c");
        if (component == null) return Array.Empty<uint>();
        byte[] source = doc.SourceData;
        int start = component.DataStart;
        // The MFC tree DataEnd can be truncated by end-marker byte patterns inside
        // the payload; bound by the next class tag instead.
        int index = -1;
        for (int i = 0; i < doc.AllElements.Count; i++)
        {
            if (ReferenceEquals(doc.AllElements[i], component)) { index = i; break; }
        }
        int end = index >= 0 && index + 1 < doc.AllElements.Count
            ? doc.AllElements[index + 1].TagOffset
            : source.Length;
        end = Math.Min(end, source.Length);
        var ids = new List<uint>();
        for (int i = start; i + 8 <= end; i++)
        {
            if (BitConverter.ToUInt32(source, i) != 0x4A38) continue;
            ids.Add(BitConverter.ToUInt32(source, i + 4));
        }
        return ids.AsReadOnly();
    }

    /// <summary>
    /// Resolves the parent sketch for a feature from the preceding profile node.
    /// Returns null when the profile payload does not match the established layout
    /// (the caller may keep the MFC node display name as an explicit fallback).
    /// </summary>
    internal static ParentSketchReferenceData? TryResolve(MfcDocument doc, MfcElement? profileCandidate)
    {
        return profileCandidate == null ? null : TryReadProfileFeature(doc, profileCandidate);
    }
}
