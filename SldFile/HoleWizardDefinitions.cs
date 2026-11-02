using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// One decoded hole-wizard sketch point location (metres, sketch coordinates).
/// The measured legacy and tapped layouts store one point at the hole position.
/// </summary>
public sealed class HoleWizardSketchPointLocation
{
    public double X { get; set; }
    public double Y { get; set; }
    public double? Z { get; set; }
}

/// <summary>
/// Offline model for <c>IWizardHoleFeatureData2</c>; the measured SW2026 stream uses the
/// <c>moHoleWzd_c</c> class tag. The legacy simple-hole layout uses FolderHeader TypeId 0x54
/// (stream 0x71); the tapped-hole layout uses TypeId 0x52 (stream 0x84). Covers hole standards,
/// fastener/thread data, counterbore/countersink/counterdrill and termination fields. Every
/// value is nullable and unresolved by default; only fields with controlled stream evidence are
/// populated.
/// </summary>
public sealed class HoleWizardFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "moHoleWzd_c";
    public override uint TypeId => 0x54u; // legacy measured FolderHeader TypeId; tapped variant uses 0x52

    /// <summary>Measured FolderHeader TypeId of this instance (0x54 legacy, 0x52 tapped).</summary>
    public uint? FolderTypeId { get; set; }

    // Hole type, standard and fastener selection.
    public int? Type { get; set; }
    public int? EndCondition { get; set; }
    public string? Standard { get; set; }
    public int? Standard2 { get; set; }
    public string? FastenerType { get; set; }
    public int? FastenerType2 { get; set; }
    public string? FastenerSize { get; set; }
    public string? ThreadClass { get; set; }
    public int? ThreadEndCondition { get; set; }
    public int? TapType { get; set; }
    public int? CosmeticThreadType { get; set; }
    public int? HoleFit { get; set; }
    public int? HeadClearanceType { get; set; }

    // Diameters.
    public double? Diameter { get; set; }
    public double? HoleDiameter { get; set; }
    public double? ThruHoleDiameter { get; set; }
    public double? CounterBoreDiameter { get; set; }
    public double? CounterDrillDiameter { get; set; }
    public double? CounterSinkDiameter { get; set; }
    public double? NearCounterSinkDiameter { get; set; }
    public double? MidCounterSinkDiameter { get; set; }
    public double? FarCounterSinkDiameter { get; set; }
    public double? MinorDiameter { get; set; }
    public double? MajorDiameter { get; set; }
    public double? TapDrillDiameter { get; set; }
    public double? ThruTapDrillDiameter { get; set; }
    public double? ThreadDiameter { get; set; }
    public double? UnderHeadCounterSinkDiameter { get; set; }

    // Depths, angles, clearance and offset.
    public double? Depth { get; set; }
    public double? HoleDepth { get; set; }
    public double? ThruHoleDepth { get; set; }
    public double? CounterBoreDepth { get; set; }
    public double? CounterDrillDepth { get; set; }
    public double? TapDrillDepth { get; set; }
    public double? ThruTapDrillDepth { get; set; }
    public double? ThreadDepth { get; set; }
    public double? CounterDrillAngle { get; set; }
    public double? CounterSinkAngle { get; set; }
    public double? NearCounterSinkAngle { get; set; }
    public double? MidCounterSinkAngle { get; set; }
    public double? FarCounterSinkAngle { get; set; }
    public double? DrillAngle { get; set; }
    public double? ThreadAngle { get; set; }
    public double? UnderHeadCounterSinkAngle { get; set; }
    public double? HeadClearance { get; set; }
    public double? OffsetDistance { get; set; }
    public double? Length { get; set; }

    // Switches and scope.
    public bool? ReverseDirection { get; set; }
    public bool? NearSideCounterSink { get; set; }
    public bool? FarSideCounterSink { get; set; }
    public bool? UnderHeadCounterSink { get; set; }
    public bool? FeatureScope { get; set; }
    public bool? AutoSelect { get; set; }
    public bool? AssemblyFeatureScope { get; set; }
    public bool? AutoSelectComponents { get; set; }
    public bool? PropagateFeatureToParts { get; set; }

    // Selections.
    /// <summary>Feature-local moSingleFaceRef_w face record (legacy layout; tapped holes: null).</summary>
    public FeatureReferenceData? Face { get; set; }

    /// <summary>Feature-local moCompFace_c face record (legacy layout; tapped holes: null).</summary>
    public FeatureReferenceData? IFace { get; set; }

    public FeatureReferenceData? Vertex { get; set; }
    public FeatureReferenceData? IVertex { get; set; }

    /// <summary>
    /// Termination selection. Never populated from the measured legacy moCompSolidBody_c:
    /// that block carries the EndCondition u32 but no body id/class word.
    /// </summary>
    public FeatureReferenceData? EndConditionReference { get; set; }
    public int? EndConditionReferenceType { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }

    /// <summary>
    /// Opaque sketch-point context. The measured hole folder contains no sgPointHandle element
    /// (the only sgPointHandle belongs to the base sketch), so the entries carry no entity id;
    /// the measured coordinates are exposed separately in <see cref="SketchPointLocations"/>.
    /// </summary>
    public IReadOnlyList<FeatureReferenceData>? SketchPoints { get; set; }

    /// <summary>Decoded sketch-point coordinates in sketch space (metres).</summary>
    public IReadOnlyList<HoleWizardSketchPointLocation>? SketchPointLocations { get; set; }

    /// <summary>Mirrors IWizardHoleFeatureData2.GetEndConditionReference.</summary>
    public FeatureReferenceData? GetEndConditionReference(out int referenceType)
    {
        referenceType = EndConditionReferenceType ?? 0;
        return EndConditionReference;
    }

    /// <summary>Mirrors IWizardHoleFeatureData2.GetSketchPointCount.</summary>
    public int? GetSketchPointCount() => SketchPoints?.Count;

    /// <summary>Mirrors IWizardHoleFeatureData2.GetFeatureScopeBodiesCount.</summary>
    public int? GetFeatureScopeBodiesCount() => FeatureScopeBodies?.Count;
}

/// <summary>
/// Offline model for <c>IAdvancedHoleFeatureData</c> / <c>moAdvHoleWizard_c</c> (0x52).
/// Only the near/far-side element lists and callout flags are exposed; the element payloads
/// stay untyped until the advanced-hole samples are decoded.
/// </summary>
public sealed class AdvancedHoleFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "moAdvHoleWizard_c";
    public override uint TypeId => 0x52u;

    public int? NearSideElementsCount { get; set; }
    public int? FarSideElementsCount { get; set; }
    public bool? CustomizeCallout { get; set; }
    public bool? UseBaselineDimensions { get; set; }
    public IReadOnlyList<FeatureReferenceData>? NearSideElements { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FarSideElements { get; set; }

    /// <summary>Mirrors IAdvancedHoleFeatureData.GetNearSideElements.</summary>
    public IReadOnlyList<FeatureReferenceData>? GetNearSideElements() => NearSideElements;

    /// <summary>Mirrors IAdvancedHoleFeatureData.GetFarSideElements.</summary>
    public IReadOnlyList<FeatureReferenceData>? GetFarSideElements() => FarSideElements;

    /// <summary>Mirrors IAdvancedHoleFeatureData.SetNearSideElements.</summary>
    public void SetNearSideElements(IReadOnlyList<FeatureReferenceData>? elements)
    {
        NearSideElements = elements;
        NearSideElementsCount = elements?.Count;
    }

    /// <summary>Mirrors IAdvancedHoleFeatureData.SetFarSideElements.</summary>
    public void SetFarSideElements(IReadOnlyList<FeatureReferenceData>? elements)
    {
        FarSideElements = elements;
        FarSideElementsCount = elements?.Count;
    }
}
