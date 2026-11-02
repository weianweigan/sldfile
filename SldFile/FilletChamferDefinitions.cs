using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Offline model for <c>ISimpleFilletFeatureData2</c>; the measured SW2026 stream uses the
/// <c>Fillet_c</c> class tag and FolderHeader TypeId 0x51 (not the catalog's moFillet_c/0x4C).
/// All values are nullable and unresolved by default; the strict parser only populates fields
/// established by the controlled fillet_2mm layout.
/// </summary>
public sealed class FilletFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "Fillet_c";
    public override uint TypeId => 0x51u;

    public int? Type { get; set; }
    public double? DefaultRadius { get; set; }
    public int? OverflowType { get; set; }
    public bool? IsMultipleRadius { get; set; }
    public bool? RoundCorners { get; set; }
    public bool? PropagateToTangentFaces { get; set; }
    public int? FilletItemsCount { get; set; }
    public bool? KeepFeatures { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Edges { get; set; }

    /// <summary>
    /// Face selections for the measured Type=2 face-fillet layout, in serialized order.
    /// Each entry carries the measured persistent face id/class word/archive GUID and the
    /// face-group count (6 in both controlled specimens). Null for Type=1 edge fillets.
    /// </summary>
    public IReadOnlyList<FeatureReferenceData>? FaceReferences { get; set; }

    public IReadOnlyList<FeatureReferenceData>? Loops { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Features { get; set; }
    public IReadOnlyList<FeatureReferenceData>? HoldLines { get; set; }
    public bool? CurvatureContinuous { get; set; }
    public FeatureReferenceData? HelpPoint { get; set; }
    public bool? OmitAttachedEdges { get; set; }
    public bool? ConstantWidth { get; set; }
    public bool? ReverseFaceNormal { get; set; }
    public int? TrimAndAttachSurfaces { get; set; }
    public bool? PropagateFeatureToParts { get; set; }
    public int? ConicTypeForCrossSectionProfile { get; set; }
    public double? DefaultConicRhoOrRadius { get; set; }
    public bool? AsymmetricFillet { get; set; }
    public double? DefaultDistance { get; set; }
    public bool? EnablePartialEdgeParameters { get; set; }

    private readonly Dictionary<uint, double> _itemRadii = new Dictionary<uint, double>();

    /// <summary>Per-item radius overrides (API GetRadius/SetRadius). Null when none are stored.</summary>
    public IReadOnlyDictionary<uint, double>? ItemRadii =>
        _itemRadii.Count == 0 ? null : (IReadOnlyDictionary<uint, double>)_itemRadii;

    /// <summary>Mirrors ISimpleFilletFeatureData2.GetRadius.</summary>
    public double? GetRadius(uint filletItemId) =>
        _itemRadii.TryGetValue(filletItemId, out double value) ? value : (double?)null;

    /// <summary>Mirrors ISimpleFilletFeatureData2.SetRadius.</summary>
    public void SetRadius(uint filletItemId, double radius) => _itemRadii[filletItemId] = radius;

    /// <summary>Mirrors ISimpleFilletFeatureData2.GetConicRhoOrRadius.</summary>
    public double? GetConicRhoOrRadius(uint filletItemId) =>
        _itemRadii.TryGetValue(filletItemId, out double value) ? value : (double?)null;

    /// <summary>Mirrors ISimpleFilletFeatureData2.SetConicRhoOrRadius.</summary>
    public void SetConicRhoOrRadius(uint filletItemId, double conicRhoOrRadius) =>
        _itemRadii[filletItemId] = conicRhoOrRadius;

    /// <summary>Mirrors ISimpleFilletFeatureData2.GetDistance.</summary>
    public double? GetDistance(uint filletItemId) => GetRadius(filletItemId);

    /// <summary>Mirrors ISimpleFilletFeatureData2.SetDistance.</summary>
    public void SetDistance(uint filletItemId, double distance) => SetRadius(filletItemId, distance);
}

/// <summary>
/// Offline model for <c>IVariableFilletFeatureData2</c>; the measured SW2026 stream uses the
/// <c>VarFillet_c</c> class tag and FolderHeader TypeId 0x52 (the same numeric TypeId as
/// <c>Chamfer_c</c>; the class name is authoritative). Measured layouts are one edge with two
/// control-point radii: 1/3, 2/4 and 3/5 mm. Only the per-control-point radius list is populated
/// by the parser; default radius/distance, flags and control-point positions remain unresolved.
/// Authoring is fixed to the measured native pair blocks.
/// </summary>
public sealed class VariableFilletFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "VarFillet_c";
    public override uint TypeId => 0x52u;

    public double? DefaultRadius { get; set; }
    public double? DefaultDistance { get; set; }
    public int? OverflowType { get; set; }
    public int? TransitionType { get; set; }
    public bool? PropagateToTangentFaces { get; set; }
    public bool? PropagateFeatureToParts { get; set; }
    public int? ConicTypeForCrossSectionProfile { get; set; }
    public double? DefaultConicRhoOrRadius { get; set; }
    public bool? AsymmetricFillet { get; set; }
    public bool? CurvatureContinuous { get; set; }
    public int? FilletEdgeCount { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Edges { get; set; }

    private readonly List<double> _controlPointRadii = new();

    /// <summary>
    /// Per-control-point radii in serialized order (measured D0 then D01). Null when the
    /// layout is unresolved. The mapping to start/end vertices is not decoded yet.
    /// </summary>
    public IReadOnlyList<double>? ControlPointRadii =>
        _controlPointRadii.Count == 0 ? null : _controlPointRadii.AsReadOnly();

    /// <summary>Number of decoded control-point radii (0 when unresolved).</summary>
    public int? ControlPointCount => _controlPointRadii.Count == 0 ? null : _controlPointRadii.Count;

    /// <summary>Mirrors IVariableFilletFeatureData2.GetControlPointRadiusAtIndex(Index).</summary>
    public double? GetControlPointRadiusAtIndex(int index) =>
        index >= 0 && index < _controlPointRadii.Count ? _controlPointRadii[index] : (double?)null;

    /// <summary>Mirrors IVariableFilletFeatureData2.SetControlPointRadiusAtIndex(Index, Radius).</summary>
    public void SetControlPointRadiusAtIndex(int index, double radius)
    {
        if (index < 0 || index >= _controlPointRadii.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _controlPointRadii[index] = radius;
    }

    /// <summary>
    /// Sets the per-control-point radii in serialized order (D0, D01). Authoring validates that the
    /// list matches the measured 2/4 mm control; arbitrary radii are rejected by the builder.
    /// </summary>
    public void SetControlPointRadii(IReadOnlyList<double> radii)
    {
        _controlPointRadii.Clear();
        if (radii != null) _controlPointRadii.AddRange(radii);
    }
}

/// <summary>
/// Offline model for <c>IChamferFeatureData2</c>; the measured SW2026 stream uses the
/// <c>Chamfer_c</c> class tag and FolderHeader TypeId 0x52 (not the catalog's moChamfer_c/0x4D).
/// Covers distance-distance and angle-distance chamfers; per-side distances mirror
/// GetEdgeChamferDistance/SetEdgeChamferDistance and vertex variants. Values are nullable and
/// unresolved by default so no default can be mistaken for a file value.
/// </summary>
public sealed class ChamferFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "Chamfer_c";
    public override uint TypeId => 0x52u;

    public int? Type { get; set; }
    public double? EdgeChamferAngle { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Faces { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Edges { get; set; }
    public FeatureReferenceData? Vertex { get; set; }
    public int? LoopCount { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Loops { get; set; }
    public bool? EqualDistance { get; set; }
    public FeatureReferenceData? IVertex { get; set; }
    public bool? KeepFeatures { get; set; }
    public bool? TangentPropagation { get; set; }
    public bool? PropagateFeatureToParts { get; set; }

    /// <summary>Edge chamfer distance for API Side=0.</summary>
    public double? EdgeChamferDistanceSide0 { get; set; }

    /// <summary>Edge chamfer distance for API Side=1.</summary>
    public double? EdgeChamferDistanceSide1 { get; set; }

    /// <summary>Vertex chamfer distance for API Side=0.</summary>
    public double? VertexChamferDistanceSide0 { get; set; }

    /// <summary>Vertex chamfer distance for API Side=1.</summary>
    public double? VertexChamferDistanceSide1 { get; set; }

    private readonly Dictionary<uint, bool> _flippedEntities = new Dictionary<uint, bool>();

    /// <summary>Per-entity flip flags (API GetIsFlipped/SetIsFlipped). Null when none are stored.</summary>
    public IReadOnlyDictionary<uint, bool>? FlippedEntities =>
        _flippedEntities.Count == 0 ? null : (IReadOnlyDictionary<uint, bool>)_flippedEntities;

    /// <summary>Mirrors IChamferFeatureData2.GetEdgeChamferDistance(Side). Side must be 0 or 1.</summary>
    public double? GetEdgeChamferDistance(int side) => side switch
    {
        0 => EdgeChamferDistanceSide0,
        1 => EdgeChamferDistanceSide1,
        _ => throw new ArgumentOutOfRangeException(nameof(side), "Chamfer distance side must be 0 or 1.")
    };

    /// <summary>Mirrors IChamferFeatureData2.SetEdgeChamferDistance(Side, Distance).</summary>
    public void SetEdgeChamferDistance(int side, double distance)
    {
        switch (side)
        {
            case 0: EdgeChamferDistanceSide0 = distance; break;
            case 1: EdgeChamferDistanceSide1 = distance; break;
            default: throw new ArgumentOutOfRangeException(nameof(side), "Chamfer distance side must be 0 or 1.");
        }
    }

    /// <summary>Mirrors IChamferFeatureData2.GetVertexChamferDistance(Side).</summary>
    public double? GetVertexChamferDistance(int side) => side switch
    {
        0 => VertexChamferDistanceSide0,
        1 => VertexChamferDistanceSide1,
        _ => throw new ArgumentOutOfRangeException(nameof(side), "Chamfer distance side must be 0 or 1.")
    };

    /// <summary>Mirrors IChamferFeatureData2.SetVertexChamferDistance(Side, Distance).</summary>
    public void SetVertexChamferDistance(int side, double distance)
    {
        switch (side)
        {
            case 0: VertexChamferDistanceSide0 = distance; break;
            case 1: VertexChamferDistanceSide1 = distance; break;
            default: throw new ArgumentOutOfRangeException(nameof(side), "Chamfer distance side must be 0 or 1.");
        }
    }

    /// <summary>Mirrors IChamferFeatureData2.GetIsFlipped.</summary>
    public bool? GetIsFlipped(uint entityId) =>
        _flippedEntities.TryGetValue(entityId, out bool value) ? value : (bool?)null;

    /// <summary>Mirrors IChamferFeatureData2.SetIsFlipped.</summary>
    public void SetIsFlipped(uint entityId, bool flip) => _flippedEntities[entityId] = flip;
}
