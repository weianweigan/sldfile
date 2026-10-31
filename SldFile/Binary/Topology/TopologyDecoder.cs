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
        FromSktEnt3IntSurfIdRepData? fromSkt3Int = null)
    {
        var list = new List<SketchContourReference>();

        if (chain != null && (chain.EntityIndices.Count > 0 || chain.Count > 0))
        {
            var contour = new SketchContourReference
            {
                EntityId = chain.SketchId > 0 ? chain.SketchId : 1,
                ChainId = chain.Count > 0 ? chain.Count : (uint)chain.EntityIndices.Count,
                EntityIndices = chain.EntityIndices.Count > 0
                    ? chain.EntityIndices.ToArray()
                    : chain.Indices.Where(i => i > 0).ToArray(),
                IsClosed = chain.IsClosed
            };
            list.Add(contour);
        }
        else if (fromSkt != null && fromSkt.Mappings.Count > 0)
        {
            var entities = fromSkt.Mappings
                .Select(m => m.EntityIndex)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var contour = new SketchContourReference
            {
                EntityId = fromSkt.FeatureTypeId > 0 ? fromSkt.FeatureTypeId : 1,
                ChainId = 1,
                EntityIndices = entities,
                IsClosed = true
            };
            list.Add(contour);
        }
        else if (fromSkt3Int != null && fromSkt3Int.Mappings.Count > 0)
        {
            var entities = fromSkt3Int.Mappings
                .Select(m => m.EntityIndex)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            var contour = new SketchContourReference
            {
                EntityId = fromSkt3Int.FeatureTypeId > 0 ? fromSkt3Int.FeatureTypeId : 1,
                ChainId = 1,
                EntityIndices = entities,
                IsClosed = true
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
        if (compEdge != null || (edgeRef != null && edgeRef.EdgeIndices.Count > 0))
        {
            uint edgeId = compEdge?.EdgeIndex ?? (edgeRef!.EdgeIndices.Count > 0 ? edgeRef.EdgeIndices[0] : 1);
            return new DirectionReference
            {
                EntityType = SelectionEntityType.Edge,
                EntityId = edgeId,
                ReferenceName = compEdge != null ? $"Edge_{compEdge.EdgeIndex}" : $"Edge_{edgeId}"
            };
        }

        if (faceRefPln != null)
        {
            return new DirectionReference
            {
                EntityType = SelectionEntityType.Plane,
                EntityId = 1,
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
                ReferenceName = !string.IsNullOrEmpty(compRefPlane.Guid) ? compRefPlane.Guid : $"Plane_{compRefPlane.RefPlaneId}"
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

        uint faceId = compFace?.FaceIndex
            ?? (faceRef != null && faceRef.FaceIds.Count > 0 ? faceRef.FaceIds[0] : (endFace3Int != null ? endFace3Int.FaceIndex : 1));

        string surfName = !string.IsNullOrEmpty(endFace?.SurfaceName)
            ? endFace!.SurfaceName
            : (endFace3Int != null ? $"EndFace_{endFace3Int.FaceIndex}" : (compFace != null ? $"Face_{compFace.FaceIndex}" : string.Empty));

        return new EndConditionEntityReference
        {
            EntityType = SelectionEntityType.Face,
            EntityId = faceId,
            FaceCount = faceRef?.FaceCount ?? 1,
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
