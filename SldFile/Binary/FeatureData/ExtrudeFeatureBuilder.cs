using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Mfc;
using SldFile.Sketches;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Parametric serializer and injector for SolidWorks extrusion features (moExtrusion_c and moICE_c).
/// Generates compliant MFC 4.2 CArchive binary structures for insertion into ResolvedFeatures streams.
/// </summary>
internal static class ExtrudeFeatureBuilder
{
    /// <summary>
    /// Builds a new ResolvedFeatures stream containing the injected extrusion feature.
    /// </summary>
    public static byte[] BuildExtrusionStream(
        byte[] currentStream,
        ExtrudeFeatureDefinitionData def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName,
        string? targetDocumentPath = null,
        SketchFeature? targetSketch = null)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        EnsureSupportedStreamVersion(currentStream, def);

        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        // Measured thin class authoring uses the complete native moExtruThin_c block on its own
        // stripped base (OneDirection / CapEnds templates). It never goes through the solid
        // moExtrusion_c serializer.
        if (def.IsThinFeature)
        {
            return ExtrudeThinFeatureBuilder.Build(
                currentStream, def, targetSketchFolder, sketchEndOffset, featureName);
        }
        if (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(def) ||
            ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(def))
        {
            return ExtrudeCutTemplateBuilder.Build(currentStream, def, featureName);
        }
        if (ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(def))
        {
            return ExtrudeUpToSurfaceTemplateBuilder.Build(currentStream, def, featureName);
        }
        if (ExtrudeCutFlagFeatureBuilder.IsTemplate(def))
        {
            return ExtrudeCutFlagFeatureBuilder.Build(currentStream, def, featureName);
        }

        // Explicit contour/region selection is only emitted on the measured SelectionAuthoring
        // bases with captured native context. The selection window is replaced in place and
        // the measured selection-specific feature block is appended.
        if (def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection)
        {
            if (def.SelectedRegions != null && def.SelectedRegions.Count > 0)
            {
                return RegionSelectionExtrudeBuilder.Build(
                    currentStream, def, targetSketchFolder, sketchEndOffset, featureName,
                    targetDocumentPath, targetSketch);
            }
            return ExplicitSelectionExtrudeBuilder.Build(
                currentStream, def, targetSketchFolder, sketchEndOffset, featureName, targetDocumentPath);
        }

        // 1. Require a structurally decoded sketch chain and a real sketch child-count marker.
        // Synthesis on sketches without a chain (e.g. unconsumed sketches in version 0x6E/0x70
        // parts) produced native-unloadable files, so this path fails fast before any mutation.
        var entityIds = MfcExtrudeSerializer.ExtractEntityIds(targetSketchFolder, currentStream, sketchEndOffset);
        int markerOffset = FindSketchChildCountMarker(currentStream, targetSketchFolder, sketchEndOffset);

        // 2. Determine unique surface tag and feature id based on existing extrusion counts
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        int existingBossCount = CountOccurrences(currentStream, "moExtrusion_c");
        int existingCutCount = CountOccurrences(currentStream, "moICE_c");

        uint surfaceTag = 0x6AE19B71 + ((uint)existingBossCount * 0x10);
        uint cutSurfaceTag = 0x6AE19B83 + ((uint)existingCutCount * 0x10);
        uint baseSurfaceTag = 0x6AE19B82;
        uint uniqueFeatureId = def.IsCut
            ? 0xCD0C81A9 + (uint)existingCutCount
            : 0xC28F06D1 + (uint)existingBossCount;

        // 3. Synthesize feature block via modular serializer
        byte[] featureBlock = def.IsCut
            ? MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds, cutSurfaceTag, baseSurfaceTag, uniqueFeatureId)
            : MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds, surfaceTag, uniqueFeatureId);

        // 4. Allocate new stream buffer
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);

        // 5. Increment stream header folder count at offset 4
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);

        // 6. Update target sketch child count flag (tag 0x67); the marker was validated above.
        uint currentChildCount = BitConverter.ToUInt32(currentStream, markerOffset + 4);
        BitConverter.GetBytes(currentChildCount + 1).CopyTo(newStream, markerOffset + 4);

        // 7. Append synthesized feature block
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    /// <summary>
    /// Synthesizes a complete Boss Extrude (moExtrusion_c) binary block with parametric dimensions.
    /// </summary>
    internal static byte[] BuildBossExtrudeBlock(
        ExtrudeFeatureDefinitionData def,
        string featureName = "凸台-拉伸1",
        IReadOnlyList<uint>? entityIds = null,
        uint surfaceTag = 0x6AE19B71,
        uint uniqueFeatureId = 0xC28F06D1)
    {
        ValidateDefinition(def);
        entityIds ??= new uint[] { 1, 2, 3, 4 };
        return MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds, surfaceTag, uniqueFeatureId);
    }

    /// <summary>
    /// Overload accepting entity count.
    /// </summary>
    internal static byte[] BuildBossExtrudeBlock(ExtrudeFeatureDefinitionData def, string featureName, int entityCount)
    {
        ValidateDefinition(def);
        var entityIds = (entityCount > 0)
            ? Enumerable.Range(1, entityCount).Select(i => (uint)i).ToList()
            : new List<uint> { 1, 2, 3, 4 };
        return MfcExtrudeSerializer.SerializeBossExtrude(def, featureName, entityIds);
    }

    /// <summary>
    /// Synthesizes a complete Cut Extrude (moICE_c) binary block with parametric dimensions.
    /// </summary>
    internal static byte[] BuildCutExtrudeBlock(
        ExtrudeFeatureDefinitionData def,
        string featureName = "切除-拉伸1",
        IReadOnlyList<uint>? entityIds = null,
        uint cutSurfaceTag = 0x6AE19B83,
        uint baseSurfaceTag = 0x6AE19B82,
        uint uniqueFeatureId = 0xCD0C81A9)
    {
        ValidateDefinition(def);
        entityIds ??= new uint[] { 5 };
        return MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds, cutSurfaceTag, baseSurfaceTag, uniqueFeatureId);
    }

    /// <summary>
    /// Overload accepting entity count.
    /// </summary>
    internal static byte[] BuildCutExtrudeBlock(ExtrudeFeatureDefinitionData def, string featureName, int entityCount)
    {
        ValidateDefinition(def);
        var entityIds = (entityCount == 1)
            ? new List<uint> { 5 }
            : (entityCount > 0)
                ? Enumerable.Range(1, entityCount).Select(i => (uint)i).ToList()
                : new List<uint> { 5 };
        return MfcExtrudeSerializer.SerializeCutExtrude(def, featureName, entityIds);
    }

    internal static void ValidateDefinition(ExtrudeFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (ExtrudeCutFlagFeatureBuilder.IsTemplate(def))
        {
            ExtrudeCutFlagFeatureBuilder.ValidateDefinition(def);
            return;
        }

        if (ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(def))
        {
            ValidateUpToSurfaceTemplate(def);
            return;
        }

        if (double.IsNaN(def.Depth) || double.IsInfinity(def.Depth) || def.Depth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Depth),
                $"Extrusion depth must be a positive finite number, but received {def.Depth}.");
        }

        if (def.BothDirections && (double.IsNaN(def.DepthDirection2) || double.IsInfinity(def.DepthDirection2) || def.DepthDirection2 <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(def.DepthDirection2),
                $"Direction 2 extrusion depth must be a positive finite number, but received {def.DepthDirection2}.");
        }

        if (def.IsThinFeature)
        {
            ValidateThinDefinition(def);
            return;
        }
        if (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(def))
        {
            ValidateDraftDirection2Template(def);
            return;
        }
        if (ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(def))
        {
            ValidateMergeFalseTemplate(def);
            return;
        }
        // Only the evidenced full-sketch Boss/Blind combos are emitted. The supported option
        // slices are documented in TestFiles/Features2026/ExtrudeOptions and every other
        // combination is rejected rather than silently degraded.
        RejectUnsupported(def.EndCondition != 0 && def.EndCondition != 6, nameof(def.EndCondition));
        RejectUnsupported(def.IsCut && def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.IsCut && def.ReverseDirection, nameof(def.ReverseDirection));

        bool twoDirections = def.BothDirections;
        bool draft = def.DraftWhileExtruding;
        bool startOffset = def.FromType == 3;

        if (twoDirections)
        {
            // Evidenced layout: solid Boss, Blind in both directions, forward direction 1,
            // single sketch and no other option.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
            RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
            RejectUnsupported(draft, nameof(def.DraftWhileExtruding));
            RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        }
        else
        {
            RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
            RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        }

        if (draft)
        {
            // Evidenced layout: solid Boss Blind, forward direction 1, direction-1 draft only.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
            RejectUnsupported(twoDirections, nameof(def.BothDirections));
            RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
            if (double.IsNaN(def.DraftAngle) || double.IsInfinity(def.DraftAngle) ||
                def.DraftAngle <= 0.0 || def.DraftAngle >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.DraftAngle),
                    $"Draft angle must be a finite value between 0 and pi/2 radians, but received {def.DraftAngle}.");
            }
        }
        else
        {
            RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        }
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));

        if (startOffset)
        {
            // Evidenced layout: solid Boss Blind, forward direction 1, unflipped start offset.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
            RejectUnsupported(twoDirections, nameof(def.BothDirections));
            RejectUnsupported(draft, nameof(def.DraftWhileExtruding));
            if (double.IsNaN(def.FromOffsetDistance) || double.IsInfinity(def.FromOffsetDistance) ||
                def.FromOffsetDistance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.FromOffsetDistance),
                    $"Start offset distance must be a positive finite number, but received {def.FromOffsetDistance}.");
            }
        }
        else
        {
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        }

        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        RejectUnsupported(def.FromOffsetReverse, nameof(def.FromOffsetReverse));
        RejectUnsupported(def.FeatureScope, nameof(def.FeatureScope));
        RejectUnsupported(def.AssemblyFeatureScope, nameof(def.AssemblyFeatureScope));
        RejectUnsupported(def.AutoSelectComponents, nameof(def.AutoSelectComponents));
        RejectUnsupported(def.PropagateFeatureToParts, nameof(def.PropagateFeatureToParts));
        RejectUnsupported(def.FlipSideToCut, nameof(def.FlipSideToCut));
        RejectUnsupported(def.NormalCut, nameof(def.NormalCut));
        RejectUnsupported(def.OptimizeGeometry, nameof(def.OptimizeGeometry));
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
        RejectUnsupported(def.LinkToThickness, nameof(def.LinkToThickness));
        RejectUnsupported(def.TranslateSurface, nameof(def.TranslateSurface));
        RejectUnsupported(def.TranslateSurfaceDirection2, nameof(def.TranslateSurfaceDirection2));
        RejectUnsupported(def.ReverseOffset, nameof(def.ReverseOffset));
        RejectUnsupported(def.ReverseOffsetDirection2, nameof(def.ReverseOffsetDirection2));
        RejectUnsupported(def.AutoFilletCorners, nameof(def.AutoFilletCorners));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.WallThickness != 0, nameof(def.WallThickness));
        RejectUnsupported(def.WallThicknessDirection2 != 0, nameof(def.WallThicknessDirection2));
        RejectUnsupported(def.CapThickness != 0, nameof(def.CapThickness));
        RejectUnsupported(def.AutoFilletRadius != 0, nameof(def.AutoFilletRadius));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(!def.AutoSelect, nameof(def.AutoSelect));

        if (def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection)
        {
            // Measured explicit-selection slices on the controlled dual-rectangle
            // SelectionAuthoring sketch: one/two closed contours or one region (measured
            // 4-edge overlap / 6-edge L-shape), plain Boss Blind at the template depth.
            RejectUnsupported(def.IsCut, nameof(def.IsCut));
            RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
            RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
            RejectUnsupported(def.FromType != 0, nameof(def.FromType));
            RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));

            var selectedContours = def.SelectedContours;
            var selectedRegions = def.SelectedRegions;
            bool hasContours = selectedContours != null && selectedContours.Count > 0;
            bool hasRegions = selectedRegions != null && selectedRegions.Count > 0;
            if (hasContours && hasRegions)
            {
                throw new NotSupportedException(
                    "Explicit selection authoring accepts either SelectedContours or SelectedRegions, not both. " +
                    "No stream changes have been made.");
            }
            if (hasRegions)
            {
                if (selectedRegions!.Count != 1)
                {
                    throw new NotSupportedException(
                        "Region selection authoring supports exactly one selected region. " +
                        "No stream changes have been made.");
                }
                var region = selectedRegions[0];
                if (region == null)
                    throw new ArgumentException("SelectedRegions must not contain null entries.", nameof(def));
                if (region.LoopCount > 1)
                {
                    throw new NotSupportedException(
                        "Regions with holes or multiple boundary loops are not supported. " +
                        "No stream changes have been made.");
                }
                var edges = region.EdgeIds;
                if (edges == null || edges.Count == 0)
                    throw new ArgumentException("The selected region requires non-empty EdgeIds.", nameof(def));
                if (edges.Count != 4 && edges.Count != 6)
                {
                    throw new NotSupportedException(
                        "Region selection authoring supports the measured 4-edge and 6-edge straight-line " +
                        "single loops. No stream changes have been made.");
                }
                if (edges.Any(id => id == 0) || edges.Distinct().Count() != edges.Count)
                    throw new ArgumentException("Region edge ids must be non-zero and distinct.", nameof(def));
            }
            else if (hasContours)
            {
                if (selectedContours!.Count > 2)
                {
                    throw new NotSupportedException(
                        "Explicit contour selection authoring supports one or two contours. " +
                        "No stream changes have been made.");
                }
                foreach (var contour in selectedContours)
                {
                    if (contour == null)
                        throw new ArgumentException("SelectedContours must not contain null entries.", nameof(def));
                    if (contour.IsCandidateOnly)
                    {
                        throw new NotSupportedException(
                            "Candidate-only contours are not accepted for authoring. No stream changes have been made.");
                    }
                    if (contour.EntityIndices == null || contour.EntityIndices.Count == 0)
                        throw new ArgumentException("Each selected contour requires non-empty EntityIndices.", nameof(def));
                    if (contour.EntityIndices.Any(id => id == 0) ||
                        contour.EntityIndices.Distinct().Count() != contour.EntityIndices.Count)
                    {
                        throw new ArgumentException("Contour entity ids must be non-zero and distinct.", nameof(def));
                    }
                }
            }
            else
            {
                throw new NotSupportedException(
                    "Explicit selection authoring requires one or two contours or one region. " +
                    "No stream changes have been made.");
            }

            if (Math.Abs(def.Depth - SelectionExtrudeTemplates.TemplateDepth) > 1e-12)
            {
                throw new NotSupportedException(
                    $"Explicit selection authoring is limited to the measured template depth " +
                    $"{SelectionExtrudeTemplates.TemplateDepth} m. No stream changes have been made.");
            }
        }
        else
        {
            RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0, nameof(def.SelectedContours));
            RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        }
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(def.EndConditionEntity != null, nameof(def.EndConditionEntity));
        RejectUnsupported(def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));
    }

    private static void ValidateThinDefinition(ExtrudeFeatureDefinitionData def)
    {
        // Measured OneDirection (0), MidPlane (2) and TwoDirection (3) templates.
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        RejectUnsupported(def.FromType != 0, nameof(def.FromType));
        RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        RejectUnsupported(def.FromOffsetReverse, nameof(def.FromOffsetReverse));
        RejectUnsupported(def.FeatureScope, nameof(def.FeatureScope));
        RejectUnsupported(def.AssemblyFeatureScope, nameof(def.AssemblyFeatureScope));
        RejectUnsupported(def.AutoSelectComponents, nameof(def.AutoSelectComponents));
        RejectUnsupported(def.PropagateFeatureToParts, nameof(def.PropagateFeatureToParts));
        RejectUnsupported(def.FlipSideToCut, nameof(def.FlipSideToCut));
        RejectUnsupported(def.NormalCut, nameof(def.NormalCut));
        RejectUnsupported(def.OptimizeGeometry, nameof(def.OptimizeGeometry));
        RejectUnsupported(def.LinkToThickness, nameof(def.LinkToThickness));
        RejectUnsupported(def.TranslateSurface, nameof(def.TranslateSurface));
        RejectUnsupported(def.TranslateSurfaceDirection2, nameof(def.TranslateSurfaceDirection2));
        RejectUnsupported(def.ReverseOffset, nameof(def.ReverseOffset));
        RejectUnsupported(def.ReverseOffsetDirection2, nameof(def.ReverseOffsetDirection2));
        RejectUnsupported(def.AutoFilletCorners, nameof(def.AutoFilletCorners));
        RejectUnsupported(def.AutoFilletRadius != 0, nameof(def.AutoFilletRadius));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(!def.AutoSelect, nameof(def.AutoSelect));
        RejectUnsupported(def.CapThickness != 0, nameof(def.CapThickness));
        RejectUnsupported(!IsPositiveFinite(def.WallThickness), nameof(def.WallThickness));
        RejectUnsupported(def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection, nameof(def.SketchSelectionMode));
        RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0, nameof(def.SelectedContours));
        RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(def.EndConditionEntity != null, nameof(def.EndConditionEntity));
        RejectUnsupported(def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));

        switch (def.ThinWallType)
        {
            case 0:
            case 2:
                RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
                RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
                RejectUnsupported(def.WallThicknessDirection2 != 0 &&
                                 Math.Abs(def.WallThicknessDirection2 - 0.001) > 1e-12,
                    nameof(def.WallThicknessDirection2));
                if (def.ThinWallType == 2)
                    RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
                break;
            case 3:
                RejectUnsupported(!def.BothDirections, nameof(def.BothDirections));
                RejectUnsupported(!IsPositiveFinite(def.DepthDirection2), nameof(def.DepthDirection2));
                RejectUnsupported(!IsPositiveFinite(def.WallThicknessDirection2), nameof(def.WallThicknessDirection2));
                RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
                break;
            default:
                throw new NotSupportedException(
                    "Thin extrusion authoring supports ThinWallType OneDirection (0), MidPlane (2) " +
                    "and TwoDirection (3). No stream changes have been made.");
        }
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static void ValidateDraftDirection2Template(ExtrudeFeatureDefinitionData def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(!def.IsCut, nameof(def.IsCut));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(!def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(Math.Abs(def.Depth - 0.010) > 1e-12, nameof(def.Depth));
        RejectUnsupported(Math.Abs(def.DepthDirection2 - 0.005) > 1e-12, nameof(def.DepthDirection2));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(!def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        if (double.IsNaN(def.DraftAngleDirection2) || double.IsInfinity(def.DraftAngleDirection2) ||
            def.DraftAngleDirection2 <= 0 || def.DraftAngleDirection2 >= Math.PI / 2.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.DraftAngleDirection2),
                "Direction-2 draft angle must be a finite value between 0 and pi/2 radians.");
        }
        ValidateCutTemplateCommon(def);
    }

    private static void ValidateMergeFalseTemplate(ExtrudeFeatureDefinitionData def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(!def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.Merge, nameof(def.Merge));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.EndCondition != 0, nameof(def.EndCondition));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(Math.Abs(def.Depth - 0.010) > 1e-12, nameof(def.Depth));
        RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        ValidateCutTemplateCommon(def);
    }

    private static void ValidateCutTemplateCommon(ExtrudeFeatureDefinitionData def, bool upToSurfaceContext = false)
    {
        RejectUnsupported(def.FromType != 0, nameof(def.FromType));
        RejectUnsupported(def.FromOffsetDistance != 0, nameof(def.FromOffsetDistance));
        RejectUnsupported(def.FromOffsetReverse, nameof(def.FromOffsetReverse));
        RejectUnsupported(!upToSurfaceContext && def.FeatureScope, nameof(def.FeatureScope));
        RejectUnsupported(def.AssemblyFeatureScope, nameof(def.AssemblyFeatureScope));
        RejectUnsupported(def.AutoSelectComponents, nameof(def.AutoSelectComponents));
        RejectUnsupported(def.PropagateFeatureToParts, nameof(def.PropagateFeatureToParts));
        RejectUnsupported(def.FlipSideToCut, nameof(def.FlipSideToCut));
        RejectUnsupported(def.NormalCut, nameof(def.NormalCut));
        RejectUnsupported(def.OptimizeGeometry, nameof(def.OptimizeGeometry));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.WallThickness != 0, nameof(def.WallThickness));
        RejectUnsupported(def.WallThicknessDirection2 != 0, nameof(def.WallThicknessDirection2));
        RejectUnsupported(def.CapEnds, nameof(def.CapEnds));
        RejectUnsupported(def.CapThickness != 0, nameof(def.CapThickness));
        RejectUnsupported(def.LinkToThickness, nameof(def.LinkToThickness));
        RejectUnsupported(def.TranslateSurface, nameof(def.TranslateSurface));
        RejectUnsupported(def.TranslateSurfaceDirection2, nameof(def.TranslateSurfaceDirection2));
        RejectUnsupported(def.ReverseOffset, nameof(def.ReverseOffset));
        RejectUnsupported(def.ReverseOffsetDirection2, nameof(def.ReverseOffsetDirection2));
        RejectUnsupported(def.AutoFilletCorners, nameof(def.AutoFilletCorners));
        RejectUnsupported(def.AutoFilletRadius != 0, nameof(def.AutoFilletRadius));
        RejectUnsupported(def.SketchSelectionMode == SketchSelectionMode.ExplicitSelection, nameof(def.SketchSelectionMode));
        RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0, nameof(def.SelectedContours));
        RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(!upToSurfaceContext && def.EndConditionEntity != null, nameof(def.EndConditionEntity));
        RejectUnsupported(!upToSurfaceContext && def.BodyScope != null &&
            (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
             (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
             (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));
    }

    private static void ValidateUpToSurfaceTemplate(ExtrudeFeatureDefinitionData def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(!def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.EndCondition != 4, nameof(def.EndCondition));
        RejectUnsupported(def.Depth != 0, nameof(def.Depth));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.EndConditionDirection2 != 0, nameof(def.EndConditionDirection2));
        RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        RejectUnsupported(def.DraftWhileExtruding, nameof(def.DraftWhileExtruding));
        RejectUnsupported(def.DraftAngle != 0, nameof(def.DraftAngle));
        RejectUnsupported(def.DraftWhileExtrudingDirection2, nameof(def.DraftWhileExtrudingDirection2));
        RejectUnsupported(def.DraftAngleDirection2 != 0, nameof(def.DraftAngleDirection2));
        RejectUnsupported(def.DraftOutward, nameof(def.DraftOutward));
        RejectUnsupported(def.DraftOutwardDirection2, nameof(def.DraftOutwardDirection2));
        ValidateCutTemplateCommon(def, upToSurfaceContext: true);
        if (def.EndConditionEntity != null &&
            (def.EndConditionEntity.EntityType != SelectionEntityType.Face ||
             def.EndConditionEntity.EntityId == 0))
        {
            throw new NotSupportedException(
                "UpToSurface authoring requires a measured Face end-condition entity. " +
                "No stream changes have been made.");
        }
        if (def.BodyScope != null && !def.BodyScope.AutoSelect &&
            (def.BodyScope.BodyIds == null || def.BodyScope.BodyIds.Count != 1))
        {
            throw new NotSupportedException(
                "UpToSurface authoring requires exactly the measured body id in an explicit body scope. " +
                "No stream changes have been made.");
        }
    }

    /// <summary>
    /// Basic Boss/Cut authoring is only evidenced on ResolvedFeatures stream versions 0x70 and
    /// 0x71. The option layouts (D2/D3/D19, extended favorites and moFromEndSpec_c) were
    /// established on 0x70 only. Other layouts are rejected instead of being silently written.
    /// </summary>
    private static void EnsureSupportedStreamVersion(byte[] currentStream, ExtrudeFeatureDefinitionData def)
    {
        ushort version = currentStream.Length >= 2 ? BitConverter.ToUInt16(currentStream, 0) : (ushort)0;

        // Flip-side cuts use 0x71; the normal-cut sheet-metal sample uses 0x74. The exact base
        // signature and template StreamVersion are validated by the flag-template builder.
        if (ExtrudeCutFlagFeatureBuilder.IsTemplate(def))
        {
            if (version != 0x71 && version != 0x74)
            {
                throw new NotSupportedException(
                    $"Flip-side/normal cut authoring requires ResolvedFeatures version 0x71 or 0x74, " +
                    $"but the document uses 0x{version:X2}. No stream changes have been made.");
            }
            return;
        }

        if (version != 0x70 && version != 0x71)
        {
            throw new NotSupportedException(
                $"Extrusion authoring is only supported for ResolvedFeatures streams version 0x70/0x71, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        // The measured 0x71 cut templates carry both-directions/draft/merge-false/UpToSurface
        // options on version 0x71 itself; their exact base signature is validated by the builders.
        if (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(def) ||
            ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(def) ||
            ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(def))
        {
            if (version != 0x71)
            {
                throw new NotSupportedException(
                    $"This cut option template is only supported on ResolvedFeatures version 0x71, " +
                    $"but the document uses 0x{version:X2}. No stream changes have been made.");
            }
            return;
        }

        bool hasOptions = def.BothDirections || def.DraftWhileExtruding || def.FromType == 3;
        if (hasOptions && version != 0x70)
        {
            throw new NotSupportedException(
                $"Extrusion option authoring is only supported for ResolvedFeatures streams with the evidenced " +
                $"version 0x70, but the document uses 0x{version:X2}. No stream changes have been made.");
        }
    }

    /// <summary>
    /// Locates the 0x67 sketch child-count marker inside the target sketch's trailing range.
    /// A missing marker means the sketch layout is not understood; silently appending the feature
    /// produced native-unloadable documents, so fail fast before touching the stream.
    /// </summary>
    internal static int FindSketchChildCountMarker(byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset)
    {
        int scanStart = Math.Max(targetSketchFolder.TagOffset, sketchEndOffset - 256);
        int scanEnd = Math.Min(sketchEndOffset, currentStream.Length);
        for (int i = scanEnd - 4; i >= scanStart; i--)
        {
            if (currentStream[i] == 0x67 && currentStream[i + 1] == 0x00 &&
                currentStream[i + 2] == 0x00 && currentStream[i + 3] == 0x00)
            {
                if (i + 8 > currentStream.Length)
                    break;
                return i;
            }
        }

        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;
        throw new NotSupportedException(
            $"Cannot author an extrusion on sketch '{sketchName}': the 0x67 sketch child-count marker was not " +
            "found in the expected sketch range. No stream changes have been made.");
    }

    private static void RejectUnsupported(bool unsupported, string property)
    {
        if (unsupported)
            throw new NotSupportedException($"Extrusion authoring does not yet support {property}. No stream changes have been made.");
    }

    internal static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                count++;
                i += pattern.Length - 1;
            }
        }
        return count;
    }
}
