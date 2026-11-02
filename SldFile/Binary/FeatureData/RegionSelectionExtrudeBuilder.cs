using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using SldFile.Binary.Topology;
using SldFile.Mfc;
using SldFile.Sketches;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Controlled region-selection extrusion authoring on the measured SelectionAuthoring
/// L-shape (6 edges) and overlap-rectangle (4 edges) bases. The target stream must be
/// the native region part stripped at its extrusion tag: the sketch, the complete
/// region context (moOneContour_c ... moBBoxCenterData_c) and the chooser are retained.
/// The context is re-emitted from the captured RegionChooserTemplate with the loop order
/// derived from the target sketch; the measured region-specific feature block is appended.
/// </summary>
internal static class RegionSelectionExtrudeBuilder
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
        string? targetDocumentPath,
        SketchFeature? targetSketch)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));
        if (def.SelectedContours != null && def.SelectedContours.Count > 0)
        {
            throw new NotSupportedException(
                "Region selection authoring does not accept SelectedContours. No stream changes have been made.");
        }
        if (!CStringHandlePathPolicy.TryNormalizeDocumentPath(targetDocumentPath, out string normalizedTarget))
        {
            throw new ArgumentException(
                "A valid absolute target document path is required for region selection authoring. " +
                "No stream changes have been made.", nameof(targetDocumentPath));
        }
        if (sketchEndOffset != currentStream.Length)
        {
            throw new NotSupportedException(
                "Region selection authoring requires the extrusion-stripped SelectionAuthoring base whose " +
                "region context/chooser ends at the stream end. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70 && version != 0x71)
        {
            throw new NotSupportedException(
                $"Region selection authoring requires ResolvedFeatures version 0x70/0x71, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");
        }

        var doc = MfcDocument.Parse(currentStream);
        var chooser = FindChooser(doc, targetSketchFolder.TagOffset, sketchEndOffset);
        EnsureNoExistingExtrusion(doc, chooser.TagOffset);
        if (doc.AllElements.Any(e => e.TagOffset >= chooser.TagOffset &&
                                     (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass)))
        {
            throw new NotSupportedException(
                "Region selection authoring requires an extrusion-stripped base; the target still contains " +
                "an extrusion after the region chooser. No stream changes have been made.");
        }

        var template = RegionChooserTemplate.Capture(doc, targetSketchFolder.TagOffset, sketchEndOffset);
        if (template == null)
        {
            throw new NotSupportedException(
                "The target document does not contain a complete native region context/chooser " +
                "(moContourChooser_c plus moOneContour_c/moEdgeRef_c/CStringHandle/trim/bbox tags). " +
                "No stream changes have been made.");
        }
        if (template.ContextTags.Count == 0 || template.ContextPathAmbiguous ||
            string.IsNullOrEmpty(template.SourceDocumentPath))
        {
            throw new NotSupportedException(
                "The captured region context has no readable/unique moCStringHandle_c source path; the writer " +
                "refuses to emit the context. No stream changes have been made.");
        }
        if (!template.OrderingRuleVerified)
        {
            throw new NotSupportedException(
                "The captured region groups do not follow the established boundary-loop ordering rule; " +
                "re-emission would be ambiguous. No stream changes have been made.");
        }
        if (targetSketch == null)
        {
            throw new NotSupportedException(
                "The target sketch geometry is unavailable, so the region boundary loop order cannot be " +
                "derived. No stream changes have been made.");
        }

        var region = ValidateRequestedRegion(def, template);
        string baseSha = ComputeNormalizedStreamSha(currentStream);
        byte[] headerRest;
        byte[] tail;
        int featureIdOffset;
        uint[] expectedNativeEdges;
        if (string.Equals(baseSha, RegionSelectionExtrudeTemplates.LShapeBaseSha256, StringComparison.Ordinal))
        {
            headerRest = RegionSelectionExtrudeTemplates.LShapeHeaderRest;
            tail = RegionSelectionExtrudeTemplates.LShapeTail;
            featureIdOffset = RegionSelectionExtrudeTemplates.LShapeFeatureIdOffset;
            expectedNativeEdges = RegionSelectionExtrudeTemplates.LShapeNativeEdges;
        }
        else if (string.Equals(baseSha, RegionSelectionExtrudeTemplates.OverlapBaseSha256, StringComparison.Ordinal))
        {
            headerRest = RegionSelectionExtrudeTemplates.OverlapHeaderRest;
            tail = RegionSelectionExtrudeTemplates.OverlapTail;
            featureIdOffset = RegionSelectionExtrudeTemplates.OverlapFeatureIdOffset;
            expectedNativeEdges = RegionSelectionExtrudeTemplates.OverlapNativeEdges;
        }
        else
        {
            throw new NotSupportedException(
                $"Region selection authoring is limited to the controlled SelectionAuthoring L-shape/overlap " +
                $"bases (base signature {baseSha.Substring(0, 12)} does not match). No stream changes have been made.");
        }

        if (template.GroupCount != expectedNativeEdges.Length ||
            !SetEquals(template.NativeEdgeIds, expectedNativeEdges))
        {
            throw new NotSupportedException(
                "The captured region chooser does not match the measured edge set of the controlled base. " +
                "No stream changes have been made.");
        }

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != RegionSelectionExtrudeTemplates.ExpectedStrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Region selection authoring requires the extrusion-stripped base with folder count " +
                $"{RegionSelectionExtrudeTemplates.ExpectedStrippedFolderCount}, but the document uses " +
                $"{folderCount}. No stream changes have been made.");
        }

        byte[] contextBlock;
        try
        {
            contextBlock = SketchSelectionWriter.BuildRegionSelectionContextFromSketch(
                region.EdgeIds, template, targetSketch, normalizedTarget);
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
        {
            throw new NotSupportedException(
                "The region boundary loop order/context could not be derived from the target sketch: " +
                ex.Message + " No stream changes have been made.");
        }

        uint featureId = 0xC28F06D1u + (uint)ExtrudeFeatureBuilder.CountOccurrences(currentStream, ExtrudeClass);
        byte[] featureBlock = BuildFeatureBlock(featureName, headerRest, tail, featureId, featureIdOffset);

        int contextStart = template.ContextTags[0].TagOffset;
        if (contextStart <= 0 || contextStart >= currentStream.Length)
        {
            throw new NotSupportedException(
                "The captured region context start is not inside the controlled base. No stream changes have been made.");
        }

        byte[] newStream = new byte[contextStart + contextBlock.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, contextStart);
        Array.Copy(contextBlock, 0, newStream, contextStart, contextBlock.Length);
        Array.Copy(featureBlock, 0, newStream, contextStart + contextBlock.Length, featureBlock.Length);
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
                $"Region selection authoring requires exactly one complete {ChooserClass} context after the " +
                $"target sketch, but found {choosers.Count}. The target must be the extrusion-stripped region " +
                "SelectionAuthoring base. No stream changes have been made.");
        }
        return choosers[0];
    }

    private static void EnsureNoExistingExtrusion(MfcDocument doc, int windowStart)
    {
        if (doc.AllElements.Any(e => e.TagOffset >= windowStart &&
                                     (e.ClassName == ExtrudeClass || e.ClassName == CutExtrudeClass)))
        {
            throw new NotSupportedException(
                "Region selection authoring currently supports only a base without any extrusion feature. " +
                "No stream changes have been made.");
        }
    }

    private static SketchRegionReference ValidateRequestedRegion(
        ExtrudeFeatureDefinitionData def, RegionChooserTemplate template)
    {
        var regions = def.SelectedRegions;
        if (regions == null || regions.Count != 1)
        {
            throw new NotSupportedException(
                "Region selection authoring supports exactly one selected region. No stream changes have been made.");
        }
        var region = regions[0];
        if (region == null)
            throw new ArgumentException("SelectedRegions must not contain null entries.", nameof(def));
        if (region.LoopCount > 1)
        {
            throw new NotSupportedException(
                "Regions with holes or multiple boundary loops are not supported (single straight-line loop only). " +
                "No stream changes have been made.");
        }
        var edges = region.EdgeIds;
        if (edges == null || edges.Count == 0)
            throw new ArgumentException("The selected region requires non-empty EdgeIds.", nameof(def));
        if (edges.Count != 4 && edges.Count != 6)
        {
            throw new NotSupportedException(
                $"Region selection authoring supports the measured 4-edge and 6-edge straight-line single loops, " +
                $"but received {edges.Count} edges. No stream changes have been made.");
        }
        if (edges.Any(id => id == 0) || edges.Distinct().Count() != edges.Count)
            throw new ArgumentException("Region edge ids must be non-zero and distinct.", nameof(def));
        if (edges.Count != template.GroupCount || !SetEquals(edges, template.NativeEdgeIds))
        {
            throw new NotSupportedException(
                $"The requested region edges [{string.Join(",", edges)}] do not match the measured boundary " +
                $"set [{string.Join(",", template.NativeEdgeIds)}] of the controlled base. " +
                "No stream changes have been made.");
        }
        return region;
    }

    private static bool SetEquals(IEnumerable<uint> a, IEnumerable<uint> b) =>
        a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));

    private static string ComputeNormalizedStreamSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static byte[] BuildFeatureBlock(
        string featureName, byte[] headerRestTemplate, byte[] tail, uint featureId,
        int featureIdOffset)
    {
        byte[] headerRest = (byte[])headerRestTemplate.Clone();
        BitConverter.GetBytes(featureId).CopyTo(headerRest, featureIdOffset);
        using var writer = new MfcStreamWriter(4096);
        writer.WriteMfcTag(ExtrudeClass);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteBytes(headerRest);
        writer.WriteBytes(tail);
        return writer.ToArray();
    }
}
