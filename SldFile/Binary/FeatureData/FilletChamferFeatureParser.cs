using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary.Topology;
using SldFile.Mfc;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Strict parser for the measured constant-radius edge fillet layout (<c>Fillet_c</c>,
/// FolderHeader TypeId 0x51, stream version 0x70, Type=1), the Type=2 face-fillet layout,
/// the variable-radius layout (<c>VarFillet_c</c>, FolderHeader TypeId 0x52,
/// stream version 0x70) and the chamfer layout (<c>Chamfer_c</c>, TypeId 0x52).
/// Radius/chamfer parameters, the single measured persistent edge reference
/// (<c>moEdgeRef_c</c> with one <c>moCompEdge_c</c>) and the two measured face references are
/// decoded; other selection/transition fields remain unresolved because no controlled evidence
/// establishes them yet.
/// </summary>
internal static class FilletChamferFeatureParser
{
    internal const string FilletClassName = "Fillet_c";
    internal const uint FilletTypeId = 0x51u;
    internal const string VariableFilletClassName = VariableFilletSegments.ClassName;
    internal const uint VariableFilletTypeId = VariableFilletSegments.TypeId;
    internal const string ChamferClassName = "Chamfer_c";
    internal const uint ChamferTypeId = 0x52u;

    /// <summary>
    /// Parses the controlled constant-radius fillet layout. Returns null for unknown classes,
    /// ids, stream versions, malformed blocks or inconsistent radius copies.
    /// </summary>
    public static FilletFeatureDefinitionData? ParseFillet(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != FilletClassName || typeId != FilletTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x70) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();
        bool faceLayout = nodes.Any(e => e.ClassName == "moFaceBlendItem_c" ||
                                         e.ClassName == "moFaceBlendItemPair_c" ||
                                         e.ClassName == "moFaceBlendSurfIdRep_c");
        if (faceLayout) return ParseFaceFillet(doc, nodes, start, end);

        var radiusDims = nodes.Where(e => e.ClassName == "ThreeDRadiusDim_c").ToList();
        if (radiusDims.Count != 1) return null;
        var compFaces = nodes.Where(e => e.ClassName == "moCompFace_c").ToList();
        if (compFaces.Count != 1) return null;
        var edgeRefs = nodes.Where(e => e.ClassName == "moEdgeRef_c").ToList();
        var compEdges = nodes.Where(e => e.ClassName == "moCompEdge_c").ToList();
        if (edgeRefs.Count != 1 || compEdges.Count != 1) return null;
        byte[]? edgeRaw = SliceToNextTag(doc, edgeRefs[0], end);
        byte[]? compEdgeRaw = SliceToNextTag(doc, compEdges[0], end);
        if (edgeRaw == null || compEdgeRaw == null || compEdgeRaw.Length != 178) return null;
        var edgeReference = PersistentEdgeReferenceParser.ParseSingle(edgeRaw);
        if (edgeReference == null) return null;

        // The generic MFC scanner truncates ThreeDRadiusDim_c at internal sentinels. The tag/data
        // start is reliable; slice the measured payload length and require the next class tag to
        // be moCompFace_c so an altered/truncated block is rejected.
        int dataStart = radiusDims[0].DataStart;
        int dataEnd = dataStart + FilletSegments.ThreeDRadiusExpectedLength;
        if (dataStart < 0 || dataEnd > end || dataEnd > doc.SourceData.Length) return null;
        if (!MfcPrimitives.IsMfcTag(doc.SourceData, dataEnd)) return null;
        int nextTagOffset = dataEnd;
        var nextTag = MfcPrimitives.ReadMfcTag(doc.SourceData, ref nextTagOffset);
        if (nextTag == null || nextTag.ClassName != "moCompFace_c") return null;

        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, dataStart, dataEnd);
        if (raw.Length != FilletSegments.ThreeDRadiusExpectedLength) return null;

        // The measured ThreeDRadiusDim_c payload embeds a named parameter (schema 0x8004, D1).
        double? radius = ExtrudeAdditionalOptionParser.ReadNamedParameter(
            raw, FilletSegments.ThreeDRadiusParameterOffset);
        if (!radius.HasValue || double.IsNaN(radius.Value) || double.IsInfinity(radius.Value) ||
            radius.Value <= 0.0)
            return null;

        // Every measured positive display copy and the negative copy must agree with the parameter.
        foreach (int slot in FilletSegments.ThreeDRadiusPositiveSlots)
        {
            if (slot + 8 > raw.Length) return null;
            double copy = BitConverter.ToDouble(raw, slot);
            if (Math.Abs(copy - radius.Value) > 1e-12) return null;
        }
        double negative = BitConverter.ToDouble(raw, FilletSegments.ThreeDRadiusNegativeSlot);
        if (Math.Abs(negative + radius.Value) > 1e-12) return null;

        var definition = new FilletFeatureDefinitionData
        {
            DefaultRadius = radius.Value,
            Edges = new[] { edgeReference }
        };
        definition.MarkResolvedInternal(nameof(FilletFeatureDefinitionData.DefaultRadius));
        definition.MarkResolvedInternal(nameof(FilletFeatureDefinitionData.Edges));
        return definition;
    }

    /// <summary>
    /// Parses the controlled variable-radius fillet layout (<c>VarFillet_c</c> / TypeId 0x52,
    /// stream 0x70). The measured sample has one edge with two control-point radii: D0 in the
    /// embedded ThreeDRadiusDim_c block and D01 in the moVertexRef_c block. Every measured radius
    /// copy (including the negative copy of D0 in the vertex block) must agree; unknown layouts,
    /// truncated/tampered blocks and the constant-radius layout return null.
    /// </summary>
    public static VariableFilletFeatureDefinitionData? ParseVariableFillet(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != VariableFilletClassName || typeId != VariableFilletTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x70) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();
        var feature = nodes.FirstOrDefault(e => e.ClassName == VariableFilletClassName &&
                                                e.TagOffset == start);
        if (feature == null) return null;
        int featureLength = NextTagOffset(doc, feature, end) - feature.DataStart;
        if (featureLength != VariableFilletSegments.HeaderExpectedLength) return null;

        // The variable layout uses a vertex-control-point structure; constant fillet objects
        // (moCompFace_c / edgeRadiusObject_c / Fillet_c) must be absent.
        if (nodes.Any(e => e.ClassName == "moCompFace_c" ||
                           e.ClassName == "edgeRadiusObject_c" ||
                           e.ClassName == FilletClassName ||
                           e.ClassName == ChamferClassName))
            return null;

        if (!HasExactlyOneWithLength(doc, nodes, "moSurfaceIdRep_c", VariableFilletSegments.SurfaceIdRepLength, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moFilletSurfIdRep_c", VariableFilletSegments.FilletSurfIdRepLength, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moEdgeRef_c", VariableFilletSegments.EdgeRefLength, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moEdgeDim_c", 0, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moCompEdge_c", VariableFilletSegments.CompEdgeLength, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moVertDim_c", 0, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moDisplayRadialDim_c", VariableFilletSegments.DisplayRadialDimLength, end) ||
            !HasExactlyOneWithLength(doc, nodes, "moCompVertex_c", VariableFilletSegments.CompVertexLength, end) ||
            !HasExactlyOne(nodes, "ThreeDRadiusDim_c") ||
            !HasExactlyOne(nodes, "moVertexRef_c"))
            return null;

        var radiusDim = nodes.Single(e => e.ClassName == "ThreeDRadiusDim_c");
        int radiusStart = radiusDim.DataStart;
        int radiusEnd = radiusStart + VariableFilletSegments.ThreeDRadiusExpectedLength;
        if (radiusStart < 0 || radiusEnd > end || radiusEnd > doc.SourceData.Length) return null;
        if (!MfcPrimitives.IsMfcTag(doc.SourceData, radiusEnd)) return null;
        int radiusCursor = radiusEnd;
        var radiusNext = MfcPrimitives.ReadMfcTag(doc.SourceData, ref radiusCursor);
        if (radiusNext == null || radiusNext.ClassName != "moCompVertex_c") return null;

        byte[] radiusRaw = MfcPrimitives.CopyRange(doc.SourceData, radiusStart, radiusEnd);
        if (radiusRaw.Length != VariableFilletSegments.ThreeDRadiusExpectedLength) return null;
        if (!ParameterNameIs(radiusRaw, VariableFilletSegments.ThreeDRadiusParameterOffset,
                             VariableFilletSegments.FirstParameterName))
            return null;
        double? firstRadius = ExtrudeAdditionalOptionParser.ReadNamedParameter(
            radiusRaw, VariableFilletSegments.ThreeDRadiusParameterOffset);
        if (!IsPositiveFinite(firstRadius)) return null;
        if (!SlotsMatch(radiusRaw, VariableFilletSegments.FirstRadiusPositiveSlots, firstRadius!.Value))
            return null;

        var vertexRef = nodes.Single(e => e.ClassName == "moVertexRef_c");
        int vertexStart = vertexRef.DataStart;
        int vertexEnd = vertexStart + VariableFilletSegments.VertexRefExpectedLength;
        if (vertexStart < 0 || vertexEnd > end || vertexEnd > doc.SourceData.Length) return null;
        if (vertexEnd != end && !MfcPrimitives.IsMfcTag(doc.SourceData, vertexEnd)) return null;

        byte[] vertexRaw = MfcPrimitives.CopyRange(doc.SourceData, vertexStart, vertexEnd);
        if (vertexRaw.Length != VariableFilletSegments.VertexRefExpectedLength) return null;
        if (!ParameterNameIs(vertexRaw, VariableFilletSegments.VertexRefParameterOffset,
                             VariableFilletSegments.SecondParameterName))
            return null;
        double? secondRadius = ExtrudeAdditionalOptionParser.ReadNamedParameter(
            vertexRaw, VariableFilletSegments.VertexRefParameterOffset);
        if (!IsPositiveFinite(secondRadius)) return null;
        if (!SlotsMatch(vertexRaw, VariableFilletSegments.SecondRadiusPositiveSlots, secondRadius!.Value))
            return null;

        int negativeSlot = VariableFilletSegments.VertexRefSecondRadiusHalfNegativeSlot;
        if (negativeSlot < 0 || negativeSlot + 8 > vertexRaw.Length) return null;
        if (Math.Abs(BitConverter.ToDouble(vertexRaw, negativeSlot) + secondRadius.Value / 2.0) > 1e-12)
            return null;

        var definition = new VariableFilletFeatureDefinitionData();
        definition.SetControlPointRadii(new[] { firstRadius.Value, secondRadius.Value });
        var edgeRef = nodes.Single(e => e.ClassName == "moEdgeRef_c");
        byte[]? edgeRaw = SliceToNextTag(doc, edgeRef, end);
        var edgeReference = edgeRaw == null ? null : PersistentEdgeReferenceParser.ParseSingle(edgeRaw);
        if (edgeReference == null) return null;
        definition.Edges = new[] { edgeReference };
        definition.MarkResolvedInternal(nameof(VariableFilletFeatureDefinitionData.ControlPointRadii));
        definition.MarkResolvedInternal(nameof(VariableFilletFeatureDefinitionData.ControlPointCount));
        definition.MarkResolvedInternal(nameof(VariableFilletFeatureDefinitionData.Edges));
        return definition;
    }

    private static bool HasExactlyOne(List<MfcElement> nodes, string className) =>
        nodes.Count(e => e.ClassName == className) == 1;

    private static bool HasExactlyOneWithLength(
        MfcDocument doc, List<MfcElement> nodes, string className, int length, int featureEnd)
    {
        var matches = nodes.Where(e => e.ClassName == className).ToList();
        if (matches.Count != 1) return false;
        int payloadEnd = NextTagOffset(doc, matches[0], featureEnd);
        return payloadEnd - matches[0].DataStart == length;
    }

    private static bool ParameterNameIs(byte[] raw, int offset, string expected)
    {
        if (offset < 0 || offset + 6 > raw.Length ||
            BitConverter.ToUInt16(raw, offset) != 0x8004 ||
            !MfcPrimitives.IsWCharPrefix(raw, offset + 2)) return false;
        int position = offset + 2;
        return string.Equals(MfcPrimitives.ReadWChar(raw, ref position), expected, StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses the controlled distance-distance and angle-distance chamfer layouts
    /// (<c>Chamfer_c</c>, FolderHeader TypeId 0x52, stream 0x70). Only the D1/D3 distances,
    /// the D2 angle and the mode-derived Type/EqualDistance flags are populated; edge
    /// selections and all other fields remain unresolved.
    /// </summary>
    public static ChamferFeatureDefinitionData? ParseChamfer(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != ChamferClassName || typeId != ChamferTypeId) return null;
        if (doc.SourceData.Length < 2 || BitConverter.ToUInt16(doc.SourceData, 0) != 0x70) return null;
        if (start < 0 || end <= start || end > doc.SourceData.Length) return null;

        var nodes = doc.AllElements
            .Where(e => e.TagOffset >= start && e.TagOffset < end)
            .ToList();
        var compEdges = nodes.Where(e => e.ClassName == "moCompEdge_c").ToList();
        if (compEdges.Count != 1) return null;
        var edgeRefs = nodes.Where(e => e.ClassName == "moEdgeRef_c").ToList();
        if (edgeRefs.Count != 1 ||
            !nodes.Any(e => e.ClassName == "moSurfaceIdRep_c") ||
            !nodes.Any(e => e.ClassName == "moFilletSurfIdRep_c"))
            return null;
        byte[]? edgeRaw = SliceToNextTag(doc, edgeRefs[0], end);
        var edgeReference = edgeRaw == null ? null : PersistentEdgeReferenceParser.ParseSingle(edgeRaw);
        if (edgeReference == null) return null;

        var compEdgeElement = compEdges[0];
        int compEdgeStart = compEdgeElement.DataStart;
        int compEdgeEnd = NextTagOffset(doc, compEdgeElement, end);
        if (compEdgeStart < 0 || compEdgeEnd <= compEdgeStart || compEdgeEnd > doc.SourceData.Length)
            return null;
        byte[] compEdge = MfcPrimitives.CopyRange(doc.SourceData, compEdgeStart, compEdgeEnd);
        int compEdgeLength = compEdge.Length;

        double? distance0 = ExtrudeAdditionalOptionParser.ReadNamedParameter(
            compEdge, ChamferSegments.DistanceParameterOffset);
        if (!IsPositiveFinite(distance0)) return null;
        if (!SlotsMatch(compEdge, ChamferSegments.DistancePositiveSlots, distance0!.Value) ||
            !SlotsMatch(compEdge, ChamferSegments.DistanceNegativeSlots, -distance0.Value))
            return null;

        var definition = new ChamferFeatureDefinitionData
        {
            EdgeChamferDistanceSide0 = distance0.Value,
            Edges = new[] { edgeReference }
        };
        definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.EdgeChamferDistanceSide0));
        definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.Edges));

        if (compEdgeLength == ChamferSegments.ExpectedCompEdgeLengthDistanceDistance)
        {
            if (nodes.Any(e => e.ClassName == "moAngleParameter_c")) return null;
            double? distance1 = ExtrudeAdditionalOptionParser.ReadNamedParameter(
                compEdge, ChamferSegments.SecondDistanceParameterOffset);
            if (!IsPositiveFinite(distance1)) return null;
            if (!SlotsMatch(compEdge, ChamferSegments.SecondDistancePositiveSlots, distance1!.Value) ||
                !SlotsMatch(compEdge, ChamferSegments.SecondDistanceNegativeSlots, -distance1.Value))
                return null;

            definition.EdgeChamferDistanceSide1 = distance1.Value;
            definition.Type = 2; // native IChamferFeatureData2.Type for distance-distance
            definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.EdgeChamferDistanceSide1));
            definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.Type));
            if (Math.Abs(distance0.Value - distance1.Value) > 1e-12)
            {
                definition.EqualDistance = false;
                definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.EqualDistance));
            }
            return definition;
        }

        if (compEdgeLength == ChamferSegments.ExpectedCompEdgeLengthAngleDistance)
        {
            var angleParams = nodes.Where(e => e.ClassName == "moAngleParameter_c").ToList();
            if (angleParams.Count != 1) return null;
            int angleStart = angleParams[0].DataStart;
            int angleEnd = NextTagOffset(doc, angleParams[0], end);
            if (angleStart < 0 || angleEnd <= angleStart || angleEnd > doc.SourceData.Length) return null;
            byte[] angleRaw = MfcPrimitives.CopyRange(doc.SourceData, angleStart, angleEnd);
            if (angleRaw.Length != ChamferSegments.ExpectedAngleParamLength) return null;

            double? angle = ExtrudeAdditionalOptionParser.ReadNamedParameter(
                angleRaw, ChamferSegments.AngleParameterOffset);
            if (!angle.HasValue || double.IsNaN(angle.Value) || double.IsInfinity(angle.Value) ||
                angle.Value <= 0.0 || angle.Value >= Math.PI / 2.0)
                return null;
            if (!SlotsMatch(angleRaw, ChamferSegments.AngleSlots, angle.Value)) return null;
            if (Math.Abs(BitConverter.ToDouble(angleRaw, ChamferSegments.AngleParameterDistanceCopySlot) -
                         distance0.Value) > 1e-12)
                return null;

            definition.EdgeChamferAngle = angle.Value;
            definition.Type = 1; // native IChamferFeatureData2.Type for angle-distance
            definition.EqualDistance = false; // measured native snapshot for this layout
            definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.EdgeChamferAngle));
            definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.Type));
            definition.MarkResolvedInternal(nameof(ChamferFeatureDefinitionData.EqualDistance));
            return definition;
        }

        return null;
    }

    /// <summary>
    /// Parses the measured Type=2 face fillet (fillet_face_3mm / fillet_face_5mm):
    /// two moFaceBlend item/surface pairs, a two-block moCompFace_c face reference set and the
    /// D1 radius in ThreeDRadiusDim_c. Type=2 is the measured native mapping for this layout;
    /// no scalar type field exists in the Fillet_c header. Radius is limited to the two
    /// controlled 3/5 mm specimens because the 3/5 diff also changes derived tiny-tolerance
    /// fields that are not linearly scalable.
    /// </summary>
    private static FilletFeatureDefinitionData? ParseFaceFillet(
        MfcDocument doc, List<MfcElement> nodes, int start, int end)
    {
        if (end - start != FaceFilletSegments.BlockLength) return null;
        var feature = nodes.FirstOrDefault(e =>
            e.ClassName == FilletClassName && e.TagOffset == start);
        if (feature == null) return null;

        // The face layout cannot contain Type=1 edge/blend objects or a generated-face wrapper.
        foreach (var e in nodes)
        {
            if (e.ClassName == "moEdgeRef_c" || e.ClassName == "moCompEdge_c" ||
                e.ClassName == "edgeRadiusObject_c" || e.ClassName == "moSurfaceIdRep_c" ||
                e.ClassName == "moFilletSurfIdRep_c" || e.ClassName == "moFaceRef_c" ||
                e.ClassName == "moSingleFaceRef_w")
                return null;
        }

        var surfIdReps = nodes.Where(e => e.ClassName == "moFaceBlendSurfIdRep_c").ToList();
        var itemPairs = nodes.Where(e => e.ClassName == "moFaceBlendItemPair_c").ToList();
        var items = nodes.Where(e => e.ClassName == "moFaceBlendItem_c").ToList();
        var compFaces = nodes.Where(e => e.ClassName == "moCompFace_c").ToList();
        var displayDims = nodes.Where(e => e.ClassName == "moDisplayRadialDim_c").ToList();
        var radiusDims = nodes.Where(e => e.ClassName == "ThreeDRadiusDim_c").ToList();
        if (surfIdReps.Count != 1 || itemPairs.Count != 1 || items.Count != 1 ||
            compFaces.Count != 1 || displayDims.Count != 1 || radiusDims.Count != 1)
            return null;
        if (nodes.Count != 7) return null;

        int headerEnd = NextTagOffset(doc, feature, end);
        if (headerEnd <= feature.DataStart || headerEnd > doc.SourceData.Length) return null;
        byte[] header = MfcPrimitives.CopyRange(doc.SourceData, feature.DataStart, headerEnd);
        if (!TryReadFilletHeader(header, out int restStart) ||
            header.Length - restStart != FaceFilletSegments.HeaderRestLength)
            return null;

        foreach (var surf in surfIdReps)
            if (!HasExactPayload(doc, surf, end, FaceFilletSegments.BlendSurfIdRepLength)) return null;
        foreach (var pair in itemPairs)
            if (!HasExactPayload(doc, pair, end, FaceFilletSegments.BlendItemPairLength)) return null;
        foreach (var item in items)
        {
            byte[]? payload = SliceToNextTag(doc, item, end);
            if (payload == null || !TryReadBlendItemFaceIds(payload))
                return null;
        }

        byte[]? compFaceRaw = SliceToNextTag(doc, compFaces[0], end);
        if (compFaceRaw == null || compFaceRaw.Length != FaceFilletSegments.CompFaceLength)
            return null;
        var compFace = PersistentFaceReferenceParser.ReadCompFacePayload(compFaceRaw);
        if (compFace == null || compFace.Layout != CompFaceLayout.Full ||
            compFace.Blocks.Count != FaceFilletSegments.CompFaceBlockCount)
            return null;

        uint[] expectedIds = { FaceFilletSegments.FirstFaceId, FaceFilletSegments.SecondFaceId };
        byte[] expectedFlags = { 0x00, 0x40 };
        var faceRefs = new List<FeatureReferenceData>(expectedIds.Length);
        for (int i = 0; i < expectedIds.Length; i++)
        {
            var block = compFace.Blocks[i];
            var faceRef = block.FaceReference;
            if (block.BlockLength != FaceFilletSegments.CompFaceBlockLength ||
                block.HeaderFlag != expectedFlags[i] ||
                faceRef.FaceCount != FaceFilletSegments.FaceCount ||
                faceRef.ClassWord != FaceFilletSegments.FaceClassWord ||
                faceRef.FaceId != expectedIds[i] ||
                faceRef.SurfaceClass != FaceFilletSegments.SurfaceClass ||
                faceRef.MappingClassReference != FaceFilletSegments.MappingClass ||
                !string.Equals(faceRef.ArchiveIdHex, FaceFilletSegments.ArchiveIdHex,
                    StringComparison.OrdinalIgnoreCase) ||
                block.Mappings.Count != 1)
                return null;
            faceRefs.Add(new FeatureReferenceData
            {
                EntityType = SelectionEntityType.Face,
                EntityId = faceRef.FaceId,
                ClassWord = faceRef.ClassWord,
                ArchiveGuid = FaceFilletSegments.ArchiveGuid,
                RecordCount = checked((int)faceRef.FaceCount),
                ReferenceName = $"FaceFilletFace{i + 1}"
            });
        }

        if (!HasExactPayload(doc, displayDims[0], end, FaceFilletSegments.DisplayRadialDimLength))
            return null;
        byte[]? radiusRaw = SliceToNextTag(doc, radiusDims[0], end);
        if (radiusRaw == null || radiusRaw.Length != FaceFilletSegments.ThreeDRadiusLength)
            return null;
        if (BitConverter.ToUInt32(radiusRaw, 0) != 1) return null; // face layout discriminator

        double? radius = ExtrudeAdditionalOptionParser.ReadNamedParameter(
            radiusRaw, FaceFilletSegments.ThreeDRadiusParameterOffset);
        if (!IsPositiveFinite(radius) || !FaceFilletSegments.IsMeasuredRadius(radius!.Value))
            return null;
        if (Math.Abs(BitConverter.ToDouble(radiusRaw, FaceFilletSegments.ThreeDRadiusParameterValueOffset) -
                     radius.Value) > 1e-12)
            return null;
        if (!SlotsMatch(radiusRaw, FaceFilletSegments.PositiveRadiusSlots, radius.Value) ||
            !SlotsMatch(radiusRaw, FaceFilletSegments.NegativeRadiusSlots, -radius.Value))
            return null;
        if (Math.Abs(BitConverter.ToDouble(radiusRaw, FaceFilletSegments.NegativeHalfRadiusSlot) +
                     radius.Value / 2.0) > 1e-12)
            return null;
        double tolerance = FaceFilletSegments.ToleranceFor(radius.Value);
        if (BitConverter.DoubleToInt64Bits(
                BitConverter.ToDouble(radiusRaw, FaceFilletSegments.ToleranceSlot1)) !=
            BitConverter.DoubleToInt64Bits(tolerance) ||
            BitConverter.DoubleToInt64Bits(
                BitConverter.ToDouble(radiusRaw, FaceFilletSegments.ToleranceSlot2)) !=
            BitConverter.DoubleToInt64Bits(tolerance))
            return null;
        byte[] tiny = FaceFilletSegments.TinyFor(radius.Value);
        if (!radiusRaw.AsSpan(FaceFilletSegments.TinySlot1, 8).SequenceEqual(tiny) ||
            !radiusRaw.AsSpan(FaceFilletSegments.TinySlot2, 8).SequenceEqual(tiny))
            return null;

        var definition = new FilletFeatureDefinitionData
        {
            Type = FaceFilletSegments.NativeType,
            DefaultRadius = radius.Value,
            FaceReferences = faceRefs.AsReadOnly()
        };
        definition.MarkResolvedInternal(nameof(FilletFeatureDefinitionData.Type));
        definition.MarkResolvedInternal(nameof(FilletFeatureDefinitionData.DefaultRadius));
        definition.MarkResolvedInternal(nameof(FilletFeatureDefinitionData.FaceReferences));
        return definition;
    }

    private static bool TryReadFilletHeader(byte[] payload, out int restStart)
    {
        restStart = 0;
        if (payload.Length < 2 || BitConverter.ToUInt16(payload, 0) != 0x8004) return false;
        int cursor = 2;
        string? name = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (string.IsNullOrEmpty(name)) return false;
        if (cursor + 8 + 4 + 4 + 4 > payload.Length) return false;
        double headerDouble = BitConverter.ToDouble(payload, cursor);
        if (Math.Abs(Math.Abs(headerDouble) - 2.0) > 1e-12) return false;
        cursor += 8;
        if (BitConverter.ToUInt32(payload, cursor) != FaceFilletSegments.HeaderTypeId) return false;
        cursor += 4;
        if (BitConverter.ToUInt32(payload, cursor) != 0) return false;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (!string.IsNullOrEmpty(empty)) return false;
        restStart = cursor;
        return true;
    }

    private static bool HasExactPayload(
        MfcDocument doc, MfcElement element, int featureEnd, int expectedLength)
    {
        int payloadStart = element.DataStart;
        int payloadEnd = NextTagOffset(doc, element, featureEnd);
        return payloadStart >= 0 && payloadEnd >= payloadStart &&
               payloadEnd - payloadStart == expectedLength &&
               payloadEnd <= doc.SourceData.Length;
    }

    private static bool TryReadBlendItemFaceIds(byte[] payload)
    {
        if (payload.Length != FaceFilletSegments.BlendItemLength) return false;
        var ids = new List<uint>();
        for (int i = 0; i + 40 <= payload.Length; i++)
        {
            if (BitConverter.ToUInt16(payload, i) != FaceFilletSegments.BlendItemMarker ||
                BitConverter.ToUInt16(payload, i + 2) != 1 ||
                BitConverter.ToUInt32(payload, i + 4) != 0 ||
                BitConverter.ToUInt16(payload, i + 8) != 0) continue;
            if (BitConverter.ToUInt32(payload, i + 10) != FaceFilletSegments.FaceCount ||
                BitConverter.ToUInt32(payload, i + 14) != FaceFilletSegments.BlendItemClassWord ||
                BitConverter.ToUInt16(payload, i + 38) != 0) return false;
            string guid = BitConverter.ToString(payload, i + 22, 16)
                .Replace("-", string.Empty).ToLowerInvariant();
            if (guid != FaceFilletSegments.ArchiveGuid) return false;
            uint id = BitConverter.ToUInt32(payload, i + 18);
            if (id == 0) return false;
            ids.Add(id);
            i += 39;
        }
        return ids.Count == 2 &&
               ids[0] == FaceFilletSegments.FirstFaceId &&
               ids[1] == FaceFilletSegments.SecondFaceId;
    }

    private static int NextTagOffset(MfcDocument doc, MfcElement element, int featureEnd)
    {
        var next = doc.AllElements
            .Where(e => e.TagOffset > element.TagOffset)
            .OrderBy(e => e.TagOffset)
            .FirstOrDefault();
        return next != null && next.TagOffset < featureEnd ? next.TagOffset : featureEnd;
    }

    private static byte[]? SliceToNextTag(MfcDocument doc, MfcElement element, int featureEnd)
    {
        int start = element.DataStart;
        int end = NextTagOffset(doc, element, featureEnd);
        if (start < 0 || end <= start || end > featureEnd || end > doc.SourceData.Length) return null;
        return MfcPrimitives.CopyRange(doc.SourceData, start, end);
    }

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0.0;

    private static bool SlotsMatch(byte[] data, int[] slots, double expected)
    {
        foreach (int slot in slots)
        {
            if (slot < 0 || slot + 8 > data.Length) return false;
            if (Math.Abs(BitConverter.ToDouble(data, slot) - expected) > 1e-12) return false;
        }
        return true;
    }
}
