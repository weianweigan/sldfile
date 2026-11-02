using System;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// First writable sheet-metal base-flange combination: the measured open-L
/// specimen from <c>SheetMetalOptions/sm_l_d10.SLDPRT</c> (ResolvedFeatures
/// version 0x73), a stripped sketch-only baseline, and parameter replacement for
/// thickness, bend radius and depth only.
/// <para>
/// The complete native sheet-metal subsystem is stored as a measured, hash-pinned
/// suffix (see <see cref="SheetMetalBaseFlangeSegments"/>). The builder verifies
/// the controlled L-sketch signature (chain ids and sgLineHandle SHA-256), refuses
/// any other feature/selection/option combination, appends the measured suffix,
/// restores the measured folder count and sketch child count, and patches only the
/// proven scalar slots. The ResolvedFeatures thickness slots are display/dimension
/// copies; the authoritative rebuild source is the Config-0 length parameter
/// "厚度"/"Thickness", patched separately by
/// <see cref="SheetMetalConfigThicknessPatcher"/>. Bounding-box/display caches that
/// depend on more than the three patched scalars are intentionally preserved
/// (lazy) and must be recomputed by the native rebuild.
/// </para>
/// </summary>
internal static class SheetMetalBaseFlangeBuilder
{
    public const string DefaultFeatureName = "基体-法兰1";

    /// <summary>
    /// The measured suffix was serialized in a version-0x73 stream. No native
    /// sheet-metal sample with version 0x70 exists; forcing 0x70 would mix archive
    /// layouts, so this first combination is deliberately gated to 0x73.
    /// </summary>
    internal const ushort SupportedStreamVersion = 0x73;

    private const string SketchChainClass = "moSketchChain_c";
    private const string SketchLineClass = "sgLineHandle";

    public static byte[] Build(
        byte[] currentStream,
        SheetMetalBaseFlangeDefinitionData def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        SheetMetalFeatureBuilder.ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Sheet metal base flange authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; changing the name would change the measured block layout. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Sheet metal base flange authoring is only supported for ResolvedFeatures streams " +
                $"version 0x{SupportedStreamVersion:X2} (the measured sheet-metal layout), but the document " +
                $"uses 0x{version:X2}. A version-0x70 sheet-metal sample is not available, so it is not guessed. " +
                "No stream changes have been made.");
        }

        VerifyControlledSketch(currentStream, targetSketchFolder, sketchEndOffset, version);
        VerifyControlledPrefix(currentStream);
        VerifyMeasuredSketchMarkers(currentStream);
        byte[] suffix = SheetMetalBaseFlangeSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        int suffixStart = currentStream.Length;
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixTemplateRadiusSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredRadius, def.BendRadius, "template radius");
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixTemplateThicknessSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredThickness, def.Thickness, "template thickness");
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixRadiusSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredRadius, def.BendRadius, "feature/global radius");
        foreach (int slot in SheetMetalBaseFlangeSegments.SuffixDepthSlots)
            PatchDouble(newStream, suffixStart + slot, SheetMetalBaseFlangeSegments.MeasuredDepth, def.Depth, "feature depth/cache");

        // Restore the measured folder count. The strip-only baseline may keep or
        // reset it; the authored stream must match the native full sample because
        // prefix + measured suffix reconstructs it byte-for-byte (apart from the
        // patched scalar slots). The 0x67 markers are preserved unchanged.
        BitConverter.GetBytes(SheetMetalBaseFlangeSegments.MeasuredFullFolderCount).CopyTo(newStream, 4);

        return newStream;
    }

    private static void VerifyControlledSketch(
        byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;

        int end = sketchEndOffset > targetSketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;

        // Use the tree-aware MfcDocument element bounds: flat tag scanning extends
        // one element to the next tag and would include the untagged gap after the
        // line handle, changing its signature.
        var elements = MfcDocument.Parse(currentStream).AllElements
            .Where(t => t.TagOffset >= targetSketchFolder.TagOffset && t.TagOffset < end)
            .ToList();
        var chains = elements.Where(t => t.ClassName == SketchChainClass).ToList();
        var lines = elements.Where(t => t.ClassName == SketchLineClass).ToList();
        if (chains.Count != 1 || lines.Count != 1)
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange on sketch '{sketchName}' " +
                $"(ResolvedFeatures version 0x{version:X2}): the sketch must expose exactly one parsed " +
                $"{SketchChainClass} and one {SketchLineClass}. Authoring is limited to the controlled open-L " +
                "specimen. No stream changes have been made.");
        }

        if (!ChainMatchesControlledProfile(currentStream, chains[0]))
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange on sketch '{sketchName}': the sketch chain does not " +
                "match the controlled open-L specimen (entity ids [2,1]). No stream changes have been made.");
        }

        string lineSha = Sha256Hex(currentStream, lines[0]);
        if (!string.Equals(lineSha, SheetMetalBaseFlangeSegments.ExpectedLineSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange on sketch '{sketchName}': the {SketchLineClass} " +
                $"signature {lineSha.Substring(0, 12)} does not match the controlled open-L specimen. " +
                "The measured sheet-metal topology assumes that exact sketch. No stream changes have been made.");
        }

        // The measured 0x67 markers are validated separately and preserved unchanged.
    }

    /// <summary>
    /// This newer 0x73 layout uses 67 80 00 00 00 00 00 00 markers rather than the
    /// 0x70 67 00 00 00 child-count marker. Both measured positions must be present
    /// and are left untouched; the measured suffix restores the native topology.
    /// </summary>
    private static void VerifyMeasuredSketchMarkers(byte[] currentStream)
    {
        byte[] expected = SheetMetalBaseFlangeSegments.SketchMarkerBytes;
        foreach (int offset in SheetMetalBaseFlangeSegments.SketchMarkerOffsets)
        {
            if (offset < 0 || offset + expected.Length > currentStream.Length)
                throw new NotSupportedException(
                    "Cannot author a sheet metal base flange: the measured 0x67 marker is outside the stream. " +
                    "No stream changes have been made.");
            for (int i = 0; i < expected.Length; i++)
            {
                if (currentStream[offset + i] == expected[i]) continue;
                throw new NotSupportedException(
                    $"Cannot author a sheet metal base flange: the measured 0x67 marker at 0x{offset:X} " +
                    "is missing or has changed. Use the stripped controlled specimen. " +
                    "No stream changes have been made.");
            }
        }
    }

    private static bool ChainMatchesControlledProfile(byte[] currentStream, MfcElement chain)
    {
        int length = chain.DataEnd - chain.DataStart;
        if (length < 2 || chain.DataStart < 0 || chain.DataStart + length > currentStream.Length)
            return false;

        ushort count = BitConverter.ToUInt16(currentStream, chain.DataStart);
        if (count != SheetMetalBaseFlangeSegments.ExpectedChainEntityIds.Length || length < 2 + 4 * count)
            return false;

        for (int i = 0; i < count; i++)
        {
            uint id = BitConverter.ToUInt32(currentStream, chain.DataStart + 2 + 4 * i);
            if (id != SheetMetalBaseFlangeSegments.ExpectedChainEntityIds[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// The suffix contains the complete measured sheet-metal feature graph. It can
    /// only be appended to the matching stripped specimen: no sheet-metal feature
    /// may already exist, and no other solid feature may precede the sketch.
    /// </summary>
    private static void VerifyControlledPrefix(byte[] currentStream)
    {
        void RequireZero(string className, string reason)
        {
            if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, className) != 0)
                throw new NotSupportedException(
                    $"Cannot author a sheet metal base flange: the target still contains {className} " +
                    $"({reason}). Use the stripped sketch-only controlled specimen. No stream changes have been made.");
        }

        RequireZero("moSMBaseFlange_c", "a base flange already exists");
        RequireZero("moCompSheetmetalFeat_c", "sheet-metal body data already exists");
        RequireZero("moTemplateFlatPattern_c", "a flat-pattern template already exists");
        RequireZero("moFlatPattern_c", "a flat pattern already exists");
        RequireZero("moSheetMetal_c", "global sheet-metal data already exists");
        RequireZero("moExtrusion_c", "an extrusion precedes the sketch");
        RequireZero("moRevolution_c", "a revolution precedes the sketch");
        RequireZero("Fillet_c", "a fillet precedes the sketch");
        RequireZero("moChamfer_c", "a chamfer precedes the sketch");

        // Sketch-only baseline: the measured suffix supplies the template and all
        // downstream sheet-metal objects.
        RequireZero("moTemplateSheetMetal_c", "a sheet-metal template already exists");

        int profileCount = ExtrudeFeatureBuilder.CountOccurrences(currentStream, "moProfileFeature_c");
        if (profileCount != 1)
        {
            throw new NotSupportedException(
                "Cannot author a sheet metal base flange: the target must contain exactly one sketch " +
                $"(found {profileCount}). No stream changes have been made.");
        }
    }

    private static void PatchDouble(byte[] data, int offset, double expected, double value, string slotName)
    {
        if (offset < 0 || offset + 8 > data.Length)
            throw new InvalidOperationException($"Sheet-metal slot {slotName} at 0x{offset:X} is outside the stream.");
        double current = BitConverter.ToDouble(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange: measured {slotName} slot at 0x{offset:X} contains " +
                $"{current:R}, expected {expected:R}. The target is not the controlled measured specimen. " +
                "No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static string Sha256Hex(byte[] data, MfcElement tag)
    {
        int start = tag.DataStart;
        int count = Math.Max(0, tag.DataEnd - start);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data, start, count);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
