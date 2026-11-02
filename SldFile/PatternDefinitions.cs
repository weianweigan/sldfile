using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Offline model for <c>ILinearPatternFeatureData</c> / <c>moLPattern_c</c> (measured TypeId 0x5D).
/// Covers direction references, spacing/instance counts, end conditions, geometry pattern,
/// skipped items, body/faced patterns and feature scope. Values are nullable and unresolved by
/// default. The controlled PatternOptions layout reads D1/D2 counts, spacings and direction
/// references; <see cref="SldPart.AddLinearPattern"/> only authors D1/D2 counts (>= 2) and
/// spacings (&gt; 0) on the measured two-direction base body.
/// </summary>
public sealed class LinearPatternFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "moLPattern_c";
    public override uint TypeId => 0x5Du; // measured FolderHeader TypeId (catalog says 0x54)

    public FeatureReferenceData? D1Axis { get; set; }
    public FeatureReferenceData? D2Axis { get; set; }
    public int? D1AxisType { get; set; }
    public int? D2AxisType { get; set; }
    public bool? D1ReverseDirection { get; set; }
    public bool? D2ReverseDirection { get; set; }
    public double? D1Spacing { get; set; }
    public double? D2Spacing { get; set; }
    public int? D1TotalInstances { get; set; }
    public int? D2TotalInstances { get; set; }
    public bool? VarySketch { get; set; }
    public bool? GeometryPattern { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFeatureArray { get; set; }
    public IReadOnlyList<uint>? SkippedItemArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFaceArray { get; set; }
    public bool? D2PatternSeedOnly { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternBodyArray { get; set; }
    public bool? PropagateVisualProperty { get; set; }
    public int? PatternElement { get; set; }
    public bool? BodyPattern { get; set; }

    public int? D1EndCondition { get; set; }
    public FeatureReferenceData? D1EndReference { get; set; }
    public double? D1EndRefOffset { get; set; }
    public bool? D1EndRefReverseOffset { get; set; }
    public bool? D1EndUseSeedReference { get; set; }
    public FeatureReferenceData? D1EndSeedReference { get; set; }
    public bool? D1EndUseSpacing { get; set; }

    public int? D2EndCondition { get; set; }
    public FeatureReferenceData? D2EndReference { get; set; }
    public double? D2EndRefOffset { get; set; }
    public bool? D2EndRefReverseOffset { get; set; }
    public bool? D2EndUseSeedReference { get; set; }
    public FeatureReferenceData? D2EndSeedReference { get; set; }
    public bool? D2EndUseSpacing { get; set; }

    public bool? FeatureScope { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }
    public bool? AutoSelect { get; set; }
    public bool? InstancesToVary { get; set; }
    public IReadOnlyList<FeatureReferenceData>? StructureSystemToPatternArray { get; set; }

    /// <summary>Mirrors ILinearPatternFeatureData.GetD1AxisType.</summary>
    public int? GetD1AxisType() => D1AxisType;

    /// <summary>Mirrors ILinearPatternFeatureData.GetD2AxisType.</summary>
    public int? GetD2AxisType() => D2AxisType;

    /// <summary>Mirrors ILinearPatternFeatureData.GetPatternFeatureCount.</summary>
    public int? GetPatternFeatureCount() => PatternFeatureArray?.Count;

    /// <summary>Mirrors ILinearPatternFeatureData.GetSkippedItemCount.</summary>
    public int? GetSkippedItemCount() => SkippedItemArray?.Count;

    /// <summary>Mirrors ILinearPatternFeatureData.GetPatternFaceCount.</summary>
    public int? GetPatternFaceCount() => PatternFaceArray?.Count;

    /// <summary>Mirrors ILinearPatternFeatureData.GetPatternBodyCount.</summary>
    public int? GetPatternBodyCount() => PatternBodyArray?.Count;

    /// <summary>Mirrors ILinearPatternFeatureData.IsDirection2Specified.</summary>
    public bool IsDirection2Specified() =>
        (D2Axis != null) || (D2TotalInstances.HasValue) || (D2Spacing.HasValue);

    /// <summary>Mirrors ILinearPatternFeatureData.SetFeatureScope.</summary>
    public void SetFeatureScope(bool featureScope, bool autoSelectBodies,
        IReadOnlyList<FeatureReferenceData>? bodies)
    {
        FeatureScope = featureScope;
        AutoSelect = autoSelectBodies;
        FeatureScopeBodies = bodies;
    }
}

/// <summary>
/// Offline model for <c>ICircularPatternFeatureData</c> / <c>moCirPattern_c</c> (measured TypeId 0x63).
/// Covers the axis, spacing/instance counts (both directions), symmetric/equal-spacing flags,
/// geometry pattern, skipped items, body/faced patterns and feature scope. The controlled
/// PatternOptions layout reads the total instance count, spacing and equal-spacing relation;
/// <see cref="SldPart.AddCircularPattern"/> only authors those two scalars on the measured base body.
/// </summary>
public sealed class CircularPatternFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "moCirPattern_c";
    public override uint TypeId => 0x63u; // measured FolderHeader TypeId (catalog says 0x53)

    public FeatureReferenceData? Axis { get; set; }
    public int? AxisType { get; set; }
    public bool? ReverseDirection { get; set; }
    public double? Spacing { get; set; }
    public int? TotalInstances { get; set; }
    public bool? EqualSpacing { get; set; }
    public bool? VarySketch { get; set; }
    public bool? GeometryPattern { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFeatureArray { get; set; }
    public IReadOnlyList<uint>? SkippedItemArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFaceArray { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternBodyArray { get; set; }
    public bool? PropagateVisualProperty { get; set; }
    public int? PatternElement { get; set; }
    public bool? BodyPattern { get; set; }
    public bool? Direction2 { get; set; }
    public bool? Symmetric { get; set; }
    public bool? EqualSpacing2 { get; set; }
    public double? Spacing2 { get; set; }
    public int? TotalInstances2 { get; set; }
    public bool? FeatureScope { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }
    public bool? AutoSelect { get; set; }
    public bool? InstancesToVary { get; set; }
    public IReadOnlyList<FeatureReferenceData>? StructureSystemToPatternArray { get; set; }

    /// <summary>Mirrors ICircularPatternFeatureData.GetAxisType.</summary>
    public int? GetAxisType() => AxisType;

    /// <summary>Mirrors ICircularPatternFeatureData.GetPatternFeatureCount.</summary>
    public int? GetPatternFeatureCount() => PatternFeatureArray?.Count;

    /// <summary>Mirrors ICircularPatternFeatureData.GetSkippedItemCount.</summary>
    public int? GetSkippedItemCount() => SkippedItemArray?.Count;

    /// <summary>Mirrors ICircularPatternFeatureData.GetPatternFaceCount.</summary>
    public int? GetPatternFaceCount() => PatternFaceArray?.Count;

    /// <summary>Mirrors ICircularPatternFeatureData.GetPatternBodyCount.</summary>
    public int? GetPatternBodyCount() => PatternBodyArray?.Count;

    /// <summary>Mirrors ICircularPatternFeatureData.SetFeatureScope.</summary>
    public void SetFeatureScope(bool featureScope, bool autoSelectBodies,
        IReadOnlyList<FeatureReferenceData>? bodies)
    {
        FeatureScope = featureScope;
        AutoSelect = autoSelectBodies;
        FeatureScopeBodies = bodies;
    }
}

/// <summary>
/// Offline model for <c>IMirrorPatternFeatureData</c> / <c>moMirrorPattern_c</c> (measured
/// FolderHeader TypeId 0x57; the catalog's 0x55 was not observed in the 2026 mirror sample).
/// Covers the mirror plane, source features/faces, geometry pattern and feature scope.
/// </summary>
public sealed class MirrorPatternFeatureDefinitionData : FeatureDefinitionData
{
    public override string ClassName => "moMirrorPattern_c";
    public override uint TypeId => 0x57u;

    public FeatureReferenceData? Plane { get; set; }
    public int? MirrorPlaneType { get; set; }
    public IReadOnlyList<FeatureReferenceData>? PatternFeatureArray { get; set; }
    public bool? GeometryPattern { get; set; }
    public IReadOnlyList<FeatureReferenceData>? MirrorFaceArray { get; set; }
    public bool? PropagateVisualProperty { get; set; }
    public int? FeatureScope { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FeatureScopeBodies { get; set; }

    /// <summary>Mirrors IMirrorPatternFeatureData.GetMirrorPlaneType.</summary>
    public int? GetMirrorPlaneType() => MirrorPlaneType;

    /// <summary>Mirrors IMirrorPatternFeatureData.GetPatternFeatureCount.</summary>
    public int? GetPatternFeatureCount() => PatternFeatureArray?.Count;

    /// <summary>Mirrors IMirrorPatternFeatureData.GetMirrorFaceCount.</summary>
    public int? GetMirrorFaceCount() => MirrorFaceArray?.Count;

    /// <summary>Mirrors IMirrorPatternFeatureData.GetFeatureScopeBodiesCount.</summary>
    public int? GetFeatureScopeBodiesCount() => FeatureScopeBodies?.Count;
}
