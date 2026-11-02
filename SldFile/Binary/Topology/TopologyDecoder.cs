using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;

namespace SldFile.Binary.Topology;

/// <summary>
/// Converts low-level MFC topology and selection data structures into public high-level selection contracts.
/// Used across all feature types (Extrusion, Revolution, Fillet, SheetMetal, Mates, etc.).
/// </summary>
internal static class TopologyDecoder
{
    public static IReadOnlyList<SketchContourReference> BuildContourReferences(
        SketchChainData? chain,
        FromSktEntSurfIdRepData? fromSkt,
        FromSktEnt3IntSurfIdRepData? fromSkt3Int = null,
        bool includeCandidates = false)
    {
        var list = new List<SketchContourReference>();

        if (chain != null && (chain.EntityIndices.Count > 0 || chain.Count > 0))
        {
            var contour = new SketchContourReference
            {
                EntityId = chain.SketchId,
                ChainId = 0, // Entity count is not a persistent chain identifier.
                EntityIndices = chain.EntityIndices.Count > 0
                    ? chain.EntityIndices.ToArray()
                    : chain.Indices.Where(i => i > 0).ToArray(),
                IsClosed = chain.IsClosed
            };
            list.Add(contour);
        }
        else if (includeCandidates && fromSkt != null && fromSkt.Mappings?.Count > 0)
        {
            var entities = fromSkt.Mappings
                .Select(m => m.EntityIndex)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var contour = new SketchContourReference
            {
                EntityId = 0, // A class ID is not a persistent entity ID.
                ChainId = 0,
                EntityIndices = entities,
                IsClosed = true,
                IsCandidateOnly = true
            };
            list.Add(contour);
        }
        else if (includeCandidates && fromSkt3Int != null && fromSkt3Int.Mappings?.Count > 0)
        {
            var entities = fromSkt3Int.Mappings
                .Select(m => m.EntityIndex)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var contour = new SketchContourReference
            {
                EntityId = 0,
                ChainId = 0,
                EntityIndices = entities,
                IsClosed = true,
                IsCandidateOnly = true
            };
            list.Add(contour);
        }

        return list.AsReadOnly();
    }

    public static IReadOnlyList<SketchRegionReference> BuildRegionReferences(
        IEnumerable<SketchRegionData> regions)
    {
        var list = new List<SketchRegionReference>();
        if (regions == null) return list.AsReadOnly();

        foreach (var reg in regions)
        {
            list.Add(new SketchRegionReference
            {
                EntityId = reg.Handle1,
                RegionIndex = reg.RegionIndex,
                LoopCount = reg.LoopCount
            });
        }
        return list.AsReadOnly();
    }

    public static DirectionReference? BuildDirectionReference(
        EdgeRefData? edgeRef,
        CompEdgeData? compEdge,
        CompRefPlaneData? compRefPlane,
        FaceRefPlnData? faceRefPln)
    {
        // moEdgeRef_c edge indices are candidate-only (byte-pattern scan), so they
        // are deliberately not promoted to a direction target. Only structurally
        // decoded component edge/plane references may yield a direction.
        _ = edgeRef;
        if (compEdge?.EdgeIndex > 0)
        {
            uint edgeId = compEdge.EdgeIndex;
            return new DirectionReference
            {
                EntityType = SelectionEntityType.Edge,
                EntityId = edgeId,
                ReferenceName = edgeId > 0 ? $"Edge_{edgeId}" : string.Empty
            };
        }

        if (faceRefPln != null && new DirectionReference { Vector = faceRefPln.DirectionVector }.IsValid)
        {
            return new DirectionReference
            {
                EntityType = SelectionEntityType.Plane,
                EntityId = 0,
                Vector = faceRefPln.DirectionVector,
                ReferenceName = "PlanarFaceDirection"
            };
        }

        if (compRefPlane != null)
        {
            return new DirectionReference
            {
                EntityType = SelectionEntityType.Plane,
                EntityId = compRefPlane.RefPlaneId,
                ReferenceName = !string.IsNullOrEmpty(compRefPlane.Guid) ? compRefPlane.Guid : (compRefPlane.RefPlaneId > 0 ? $"Plane_{compRefPlane.RefPlaneId}" : string.Empty)
            };
        }

        return null;
    }

    public static EndConditionEntityReference? BuildEndConditionEntity(
        FaceRefData? faceRef,
        EndFaceSurfIdRepData? endFace,
        CompFaceData? compFace,
        EndFace3IntSurfIdRepData? endFace3Int = null)
    {
        if (faceRef == null && endFace == null && compFace == null && endFace3Int == null)
            return null;

        uint faceId = compFace?.FaceIndex ?? 0;
        if (faceId == 0 && faceRef != null && faceRef.FaceIds.Count > 0)
            faceId = faceRef.FaceIds.FirstOrDefault(id => id > 0);
        if (faceId == 0)
            faceId = endFace3Int?.FaceIndex ?? 0;

        string surfName = !string.IsNullOrEmpty(endFace?.SurfaceName)
            ? endFace!.SurfaceName
            : (faceId == 0 ? string.Empty : (compFace?.FaceIndex == faceId ? $"Face_{faceId}" : $"EndFace_{faceId}"));

        return new EndConditionEntityReference
        {
            EntityType = SelectionEntityType.Face,
            EntityId = faceId,
            FaceCount = faceRef?.FaceCount ?? (faceId > 0 ? 1u : 0u),
            SurfaceName = surfName
        };
    }

    public static BodyScopeData BuildBodyScope(
        PerBodyChooserData? chooser,
        IEnumerable<CompSolidBodyData>? compSolidBodies)
    {
        var scope = new BodyScopeData();
        if (chooser != null)
        {
            scope.Flags = chooser.ChooserFlags;
            scope.AutoSelect = (chooser.ChooserFlags & 0x0002) != 0 || chooser.ChooserFlags == 0;
        }

        if (compSolidBodies != null)
        {
            var bodyList = compSolidBodies.ToList();
            if (bodyList.Count > 0)
            {
                scope.BodyIds = bodyList.Select(b => (uint)b.BodyIndex).ToArray();
                scope.Bodies = bodyList.Select(b => new EntityReference
                {
                    EntityId = b.BodyIndex,
                    EntityType = SelectionEntityType.Body,
                    Name = $"SolidBody_{b.BodyIndex}"
                }).ToArray();
            }
        }

        return scope;
    }
}
