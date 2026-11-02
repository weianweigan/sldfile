using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Binary.Topology;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled explicit-contour selection extrusion authoring on the measured
/// SelectionAuthoring dual-rectangle bases. The target stream must be the native
/// explicit-selection part stripped at its extrusion tag: the sketch prefix, the
/// chooser window and the complete trim/reference suffix are retained, while the
/// extrusion feature is absent. The suffix is captured from that window and the
/// selection block is rebuilt with the requested (measured) contour chains.
/// </summary>
internal static class ExplicitSelectionExtrudeBuilder
{
    private const string ChooserClass = "moContourChooser_c";
    private const string ExtrudeClass = "moExtrusion_c";
    private const string CutExtrudeClass = "moICE_c";

    internal static byte[] Build(
        byte[] currentStream,
        ExtrudeFeatureDefinitionData def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName,
        string? targetDocumentPath)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));
        if (def.SelectedRegions != null && def.SelectedRegions.Count > 0)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring does not accept SelectedRegions. No stream changes have been made.");
        }
        if (!CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalizedTarget))
        {
            throw new ArgumentException(
                "A valid absolute target document path is required for explicit contour selection authoring. " +
                "No stream changes have been made.", nameof(targetDocumentPath));
        }
        if (sketchEndOffset != currentStream.Length)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring requires the extrusion-stripped SelectionAuthoring base " +
                "whose selection window ends at the stream end. No stream changes have been made.");
        }

        var doc = MfcDocument.Parse(currentStream);
        var chooser = FindChooser(doc, targetSketchFolder.TagOffset, sketchEndOffset);
        EnsureWindowIsLastData(doc, chooser, sketchEndOffset);
        EnsureNoExistingExtrusion(doc, chooser.TagOffset);

        var available = SelectedSketchContourParser.TryRead(doc, chooser);
        if (available.Count == 0)
        {
            throw new NotSupportedException(
                "The target selection window does not expose a structurally decoded moSketchChain_c contour chain. " +
                "Only the measured explicit-contour SelectionAuthoring bases are supported. No stream changes have been made.");
        }

        var contours = ValidateRequestedContours(def, available);
        var suffix = SelectedSketchContourSuffixParser.Read(doc, chooser.TagOffset, sketchEndOffset);
        if (suffix == null || !suffix.IsValid)
        {
            throw new NotSupportedException(
                "The target document does not contain a complete native trim/reference suffix " +
                "(moSimpleSurfIdRep_c / moCStringHandle_c / trim edge records) for the selected sketch. " +
                "Envelope-only selection authoring is not native-valid and is refused. No stream changes have been made.");
        }
        if (string.IsNullOrEmpty(suffix.SourceDocumentPath))
        {
            throw new NotSupportedException(
                "The target moCStringHandle_c does not expose a readable source document path; the writer refuses " +
                "to emit the selection block. No stream changes have been made.");
        }

        ushort expectedSurface = contours.Count == 1
            ? SelectionExtrudeTemplates.SingleContourSurfaceId
            : SelectionExtrudeTemplates.TwoContourSurfaceId;
        if (suffix.SurfaceId != expectedSurface)
        {
            throw new NotSupportedException(
                $"The captured selection suffix surface 0x{suffix.SurfaceId:X2} does not match the requested " +
                $"{contours.Count}-contour layout (expected 0x{expectedSurface:X2}). No stream changes have been made.");
        }

        string expectedPrefixSha = contours.Count == 1
            ? SelectionExtrudeTemplates.OneContourPrefixSha256
            : SelectionExtrudeTemplates.TwoContourPrefixSha256;
        RequireControlledPrefix(currentStream, chooser.TagOffset, expectedPrefixSha, contours.Count);
        RequireStrippedFolderCount(currentStream);

        // Rebuild the selection window from the captured suffix and the requested chains.
        uint[][] contourIds = contours.Select(c => c.EntityIndices.ToArray()).ToArray();
        byte[] selectionBlock = SketchSelectionWriter.BuildContourSelectionBlock(
            contourIds, sketchId: 1, surfaceId: suffix.SurfaceId, suffix: suffix,
            targetDocumentPath: normalizedTarget);

        uint featureId = 0xC28F06D1u + (uint)ExtrudeFeatureBuilder.CountOccurrences(currentStream, ExtrudeClass);
        byte[] featureBlock = BuildFeatureBlock(def, featureName, contours.Count, featureId);

        byte[] newStream = new byte[chooser.TagOffset + selectionBlock.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, chooser.TagOffset);
        Array.Copy(selectionBlock, 0, newStream, chooser.TagOffset, selectionBlock.Length);
        Array.Copy(featureBlock, 0, newStream, chooser.TagOffset + selectionBlock.Length, featureBlock.Length);

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        return newStream;
    }

    private static MfcElement FindChooser(MfcDocument doc, int sketchStart, int sketchEnd)
    {
        var choosers = doc.AllElements
            .Where(e => e.ClassName == ChooserClass && e.TagOffset >= sketchStart && e.TagOffset < sketchEnd)
            .OrderBy(e => e.TagOffset)
            .ToList();
        if (choosers.Count != 1)
        {
            throw new NotSupportedException(
                $"Explicit contour selection authoring requires exactly one complete {ChooserClass} window " +
                $"after the target sketch, but found {choosers.Count}. The target must be the extrusion-stripped " +
                "SelectionAuthoring base with a captured trim suffix. No stream changes have been made.");
        }
        return choosers[0];
    }

    private static void EnsureWindowIsLastData(MfcDocument doc, MfcElement chooser, int sketchEnd)
    {
        // The controlled base ends with the suffix; no feature objects may follow the window.
        var trailing = doc.AllElements
            .Where(e => e.TagOffset >= chooser.TagOffset && e.TagOffset < sketchEnd &&
                        (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass))
            .ToList();
        if (trailing.Count > 0)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring requires an extrusion-stripped base; the target still " +
                "contains an extrusion after the selection window. No stream changes have been made.");
        }
    }

    private static void EnsureNoExistingExtrusion(MfcDocument doc, int windowStart)
    {
        if (doc.AllElements.Any(e => e.TagOffset >= windowStart &&
                                     (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass)))
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring currently supports only a base without any extrusion feature. " +
                "No stream changes have been made.");
        }
    }

    private static IReadOnlyList<SketchContourReference> ValidateRequestedContours(
        ExtrudeFeatureDefinitionData def, IReadOnlyList<SketchContourReference> available)
    {
        var requested = def.SelectedContours;
        if (requested == null || requested.Count < 1 || requested.Count > 2)
        {
            throw new NotSupportedException(
                "Explicit contour selection authoring supports one or two contours. No stream changes have been made.");
        }
        if (requested.Count != available.Count)
        {
            throw new NotSupportedException(
                $"The target selection window exposes {available.Count} contour chains, but the definition " +
                $"requests {requested.Count}. The controlled base and the requested selection must match. " +
                "No stream changes have been made.");
        }

        var used = new bool[available.Count];
        for (int i = 0; i < requested.Count; i++)
        {
            var contour = requested[i];
            if (contour == null)
                throw new ArgumentException("SelectedContours must not contain null entries.", nameof(def));
            if (contour.IsCandidateOnly)
            {
                throw new NotSupportedException(
                    "Candidate-only contours are not accepted for authoring. No stream changes have been made.");
            }
            var ids = contour.EntityIndices;
            if (ids == null || ids.Count == 0)
                throw new ArgumentException("Each selected contour requires non-empty EntityIndices.", nameof(def));
            if (ids.Any(id => id == 0) || ids.Distinct().Count() != ids.Count)
                throw new ArgumentException("Contour entity ids must be non-zero and distinct.", nameof(def));

            // Only the measured chain sequences are supported; the feature template carries
            // contour-specific header/end-face/topology records for these exact loops.
            // The two-contour specimen also verifies the same loops in either group order,
            // so each requested sequence may match any not-yet-used available chain.
            int match = -1;
            for (int j = 0; j < available.Count; j++)
            {
                if (used[j]) continue;
                var availableIds = available[j].EntityIndices;
                if (availableIds != null && availableIds.SequenceEqual(ids))
                {
                    match = j;
                    break;
                }
            }
            if (match < 0)
            {
                string availableText = string.Join(" | ",
                    available.Select(c => "[" + string.Join(",", c.EntityIndices ?? Array.Empty<uint>()) + "]"));
                throw new NotSupportedException(
                    $"Selected contour {i + 1} [{string.Join(",", ids)}] does not match a measured contour chain " +
                    $"of the target base (available {availableText}). No stream changes have been made.");
            }
            used[match] = true;
        }

        return requested;
    }

    private static void RequireControlledPrefix(
        byte[] stream, int prefixEnd, string expectedSha256, int contourCount)
    {
        byte[] prefix = MfcPrimitives.CopyRange(stream, 0, prefixEnd);
        if (prefix.Length >= 6)
        {
            prefix[4] = 0;
            prefix[5] = 0;
        }
        string sha;
        using (var algorithm = SHA256.Create())
            sha = BitConverter.ToString(algorithm.ComputeHash(prefix)).Replace("-", string.Empty).ToLowerInvariant();
        if (!string.Equals(sha, expectedSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Explicit contour selection authoring is limited to the controlled SelectionAuthoring " +
                $"{contourCount}-contour dual-rectangle sketch (prefix signature {sha.Substring(0, 12)} does not " +
                "match the measured base). No stream changes have been made.");
        }
    }

    private static void RequireStrippedFolderCount(byte[] stream)
    {
        ushort folderCount = BitConverter.ToUInt16(stream, 4);
        if (folderCount != SelectionExtrudeTemplates.ExpectedStrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Explicit contour selection authoring requires the extrusion-stripped base with folder count " +
                $"{SelectionExtrudeTemplates.ExpectedStrippedFolderCount}, but the document uses {folderCount}. " +
                "No stream changes have been made.");
        }
    }

    private static byte[] BuildFeatureBlock(
        ExtrudeFeatureDefinitionData def, string featureName, int contourCount, uint featureId)
    {
        byte[] headerRest = (contourCount == 1
            ? SelectionExtrudeTemplates.OneContourHeaderRest
            : SelectionExtrudeTemplates.TwoContourHeaderRest).ToArray();
        int featureIdOffset = contourCount == 1
            ? SelectionExtrudeTemplates.OneContourFeatureIdOffset
            : SelectionExtrudeTemplates.TwoContourFeatureIdOffset;
        BitConverter.GetBytes(featureId).CopyTo(headerRest, featureIdOffset);
        byte[] tail = contourCount == 1
            ? SelectionExtrudeTemplates.OneContourTail
            : SelectionExtrudeTemplates.TwoContourTail;

        using var writer = new MfcStreamWriter(4096);
        writer.WriteMfcTag(ExtrudeClass);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteBytes(headerRest);
        writer.WriteBytes(tail);
        return writer.ToArray();
    }
}
