using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Decoded hole-wizard sketch point location (metres, sketch coordinates).
/// </summary>
public sealed class HoleWizardSketchPointLocation
{
    public double X { get; set; }
    public double Y { get; set; }
    public double? Z { get; set; }
}

/// <summary>
/// Strongly-typed MFC feature model for moHoleWzd_c.
/// Implements IMfcFeatureModel with full property tracking.
/// </summary>
public sealed class HoleWizardFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "moHoleWzd_c";
    public string ClassName => MfcClassName;
    public uint TypeId => FolderTypeId ?? 0x54u;

    public uint? FolderTypeId { get; set; }
    public string? FeatureName { get; set; }

    // Hole type, standard and fastener selection
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

    // Diameters
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

    // Depths, angles, clearance and offset
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

    // Switches and scope
    public bool? ReverseDirection { get; set; }
    public bool? NearSideCounterSink { get; set; }
    public bool? FarSideCounterSink { get; set; }
    public bool? UnderHeadCounterSink { get; set; }
    public bool? FeatureScope { get; set; }
    public bool? AutoSelect { get; set; }
    public bool? AssemblyFeatureScope { get; set; }
    public bool? AutoSelectComponents { get; set; }
    public bool? PropagateFeatureToParts { get; set; }

    // Selections
    public FeatureReferenceData? Face { get; set; }
    public FeatureReferenceData? IFace { get; set; }
    public FeatureReferenceData? Vertex { get; set; }
    public FeatureReferenceData? IVertex { get; set; }
    public FeatureReferenceData? EndConditionReference { get; set; }
    public int? EndConditionReferenceType { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }
    public IReadOnlyList<FeatureReferenceData>? SketchPoints { get; set; }
    public IReadOnlyList<HoleWizardSketchPointLocation>? SketchPointLocations { get; set; }

    public FeatureReferenceData? GetEndConditionReference(out int referenceType)
    {
        referenceType = EndConditionReferenceType ?? 0;
        return EndConditionReference;
    }

    public int? GetSketchPointCount() => SketchPoints?.Count;
    public int? GetFeatureScopeBodiesCount() => FeatureScopeBodies?.Count;

    public byte[]? RawFeatureData { get; set; }

    public void Serialize(MfcArchive ar)
    {
        if (ar == null) throw new ArgumentNullException(nameof(ar));

        if (ar.IsStoring)
        {
            if (RawFeatureData != null && RawFeatureData.Length > 0)
            {
                ar.WriteBytes(RawFeatureData);
                return;
            }

            byte[] block = HoleWizardFeatureBuilder.BuildFeatureBlock(this, FeatureName ?? "孔1");
            ar.WriteBytes(block);
        }
        else
        {
            if (ar.Remaining > 0)
            {
                RawFeatureData = ar.ReadRemainingBytes();
            }
        }
    }
}

/// <summary>
/// Strongly-typed MFC feature model for moAdvHoleWizard_c (0x52).
/// </summary>
public sealed class AdvancedHoleFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "moAdvHoleWizard_c";
    public string ClassName => MfcClassName;
    public uint TypeId => 0x52u;

    public int? NearSideElementsCount { get; set; }
    public int? FarSideElementsCount { get; set; }
    public bool? ReverseDirection { get; set; }
    public bool? UseCustomCalloutOrder { get; set; }
    public string? BaselineString { get; set; }

    public void Serialize(MfcArchive ar)
    {
    }
}
