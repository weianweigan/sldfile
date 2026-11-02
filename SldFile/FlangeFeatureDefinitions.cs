using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Custom bend allowance payload returned by
/// <c>IEdgeFlangeFeatureData::GetCustomBendAllowance</c> /
/// <c>IMiterFlangeFeatureData::GetCustomBendAllowance</c>.
/// All fields are nullable: no value is supplied until a controlled sample
/// establishes the serialized layout.
/// </summary>
public sealed class FlangeCustomBendAllowanceData
{
    /// <summary>Custom bend allowance type (swBendAllowanceTypes_e). Not decoded.</summary>
    public int? Type { get; set; }

    /// <summary>K-factor value. Not decoded.</summary>
    public double? KFactor { get; set; }

    /// <summary>Bend allowance value. Not decoded.</summary>
    public double? BendAllowance { get; set; }

    /// <summary>Bend deduction value. Not decoded.</summary>
    public double? BendDeduction { get; set; }

    /// <summary>Bend table file path. Not decoded.</summary>
    public string? BendTableFile { get; set; }

    /// <summary>Bend calculation table file path. Not decoded.</summary>
    public string? BendCalculationTableFile { get; set; }
}

/// <summary>
/// Per-field evidence flags for <see cref="EdgeFlangeFeatureDefinitionData"/>.
/// Every flag is false until a controlled sample proves the corresponding
/// serialized layout. A false flag means the matching value must be treated as
/// unknown, not as a native default.
/// </summary>
public sealed class EdgeFlangeParameterResolution
{
    public bool Edge { get; internal set; }
    public bool Edges { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool BendRadius { get; internal set; }
    public bool BendAngle { get; internal set; }
    public bool OffsetType { get; internal set; }
    public bool OffsetReference { get; internal set; }
    public bool ReverseOffset { get; internal set; }
    public bool OffsetDistance { get; internal set; }
    public bool OffsetDimType { get; internal set; }
    public bool PositionType { get; internal set; }
    public bool UsePositionTrimSideBends { get; internal set; }
    public bool UsePositionOffset { get; internal set; }
    public bool PositionOffsetType { get; internal set; }
    public bool PositionOffsetReference { get; internal set; }
    public bool PositionOffsetDistance { get; internal set; }
    public bool ReversePositionOffset { get; internal set; }
    public bool AutoReliefType { get; internal set; }
    public bool UseReliefRatio { get; internal set; }
    public bool ReliefRatio { get; internal set; }
    public bool ReliefWidth { get; internal set; }
    public bool ReliefDepth { get; internal set; }
    public bool ReliefTearType { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool UseDefaultBendRelief { get; internal set; }
    public bool GapDistance { get; internal set; }
    public bool AngleReference { get; internal set; }
    public bool LockAngle { get; internal set; }
    public bool PerpendicularToFace { get; internal set; }
    public bool NormalToFlangePlane { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool EdgeCount { get; internal set; }
    public bool PositionReferenceType { get; internal set; }
}

/// <summary>
/// Edge flange feature definition data. Corresponds to the SolidWorks API
/// <c>IEdgeFlangeFeatureData</c> interface (TypeName2 <c>EdgeFlange</c>).
/// <para>
/// Native sample evidence (samples4/edge_flange_90_len10.SLDPRT and the six
/// FlangeOptions variants, ResolvedFeatures version 0x74): the serialized class
/// is <c>moEdgeFlange_c</c> and the FolderHeader TypeId is <b>0x67</b> (not the
/// catalog/TODO candidate 0x63). BendAngle (named D4), BendRadius (the
/// EdgeFlange's own D1), EdgeCount (moEdgeRef_c count, one-edge layout),
/// OffsetDistance (named D7), PositionType and ReliefWidth/ReliefDepth (the
/// measured anonymous relief block) are resolved against native API snapshots.
/// Every other value is nullable with no default and remains unresolved; the
/// parser returns null for any layout it cannot validate.
/// </para>
/// </summary>
public sealed class EdgeFlangeFeatureDefinitionData : IFeatureData
{
    /// <summary>Observed MFC class name in the native edge-flange sample.</summary>
    public const string SerializedClassName = "moEdgeFlange_c";

    /// <summary>Observed FolderHeader TypeId in the native edge-flange sample (version 0x74).</summary>
    public const uint ObservedSerializedTypeId = 0x67;

    /// <inheritdoc />
    public string ClassName => SerializedClassName;

    /// <summary>FolderHeader TypeId observed in the native sample. Payload semantics remain unresolved.</summary>
    public uint TypeId { get; internal set; } = ObservedSerializedTypeId;

    /// <summary>Per-field evidence flags. All false until a sample is decoded.</summary>
    public EdgeFlangeParameterResolution ParameterResolution { get; } = new();

    // ── Edge / profile selection ──

    /// <summary>Single attachment edge. Corresponds to IEdgeFlangeFeatureData::Edge. Not decoded.</summary>
    public EntityReference? Edge { get; set; }

    /// <summary>Attachment edges (multiple-edge flange). Corresponds to IEdgeFlangeFeatureData::Edges. Not decoded.</summary>
    public IReadOnlyList<EntityReference>? Edges { get; set; }

    // ── Bend parameters ──

    /// <summary>Whether to use the sheet metal default bend radius. Corresponds to UseDefaultBendRadius. Not decoded.</summary>
    public bool? UseDefaultBendRadius { get; set; }

    /// <summary>Bend radius in meters. Corresponds to BendRadius. Resolved from the EdgeFlange's own D1 for the observed 0x74 layout.</summary>
    public double? BendRadius { get; set; }

    /// <summary>Bend angle in radians. Corresponds to BendAngle. Resolved from the named D4 parameter for the observed 0x74 layout.</summary>
    public double? BendAngle { get; set; }

    // ── Flange length / offset ──

    /// <summary>Flange length end condition. Corresponds to OffsetType. Not decoded.</summary>
    public int? OffsetType { get; set; }

    /// <summary>Flange length reference. Corresponds to OffsetReference. Not decoded.</summary>
    public EntityReference? OffsetReference { get; set; }

    /// <summary>Whether to flip the flange length. Corresponds to ReverseOffset. Not decoded.</summary>
    public bool? ReverseOffset { get; set; }

    /// <summary>Flange length in meters. Corresponds to OffsetDistance. Resolved from named D7: the length-20 sample changes D7 from 0.010 to 0.020 while D6 stays 0.010.</summary>
    public double? OffsetDistance { get; set; }

    /// <summary>Flange length dimension origin type. Corresponds to OffsetDimType. Not decoded.</summary>
    public int? OffsetDimType { get; set; }

    // ── Flange position ──

    /// <summary>Flange position (swFlangePositionTypes_e). Corresponds to PositionType. Resolved from the measured anonymous relief/position block (native samples: 1 MaterialInside, 2 MaterialOutside, 3 BendOutside).</summary>
    public int? PositionType { get; set; }

    /// <summary>Whether to trim side bends. Corresponds to UsePositionTrimSideBends. Not decoded.</summary>
    public bool? UsePositionTrimSideBends { get; set; }

    /// <summary>Whether to offset the flange from the sheet metal body. Corresponds to UsePositionOffset. Resolved from the u32 at D6 value + 0x68 (0 original series, 1 position-offset samples).</summary>
    public bool? UsePositionOffset { get; set; }

    /// <summary>Position offset end condition. Corresponds to PositionOffsetType. Not decoded: constant 1 in every native snapshot with no disambiguating sample.</summary>
    public int? PositionOffsetType { get; set; }

    /// <summary>Position offset reference. Corresponds to PositionOffsetReference. Not decoded.</summary>
    public EntityReference? PositionOffsetReference { get; set; }

    /// <summary>Position offset distance in meters. Corresponds to PositionOffsetDistance. Resolved from named D6 (0.005/0.010 single-variable samples; 0.010 in the original series).</summary>
    public double? PositionOffsetDistance { get; set; }

    /// <summary>Whether to reverse the position offset. Corresponds to ReversePositionOffset. Not decoded.</summary>
    public bool? ReversePositionOffset { get; set; }

    // ── Relief ──

    /// <summary>Auto-relief type. Corresponds to AutoReliefType. Not decoded.</summary>
    public int? AutoReliefType { get; set; }

    /// <summary>Whether to use the relief ratio. Corresponds to UseReliefRatio. Not decoded.</summary>
    public bool? UseReliefRatio { get; set; }

    /// <summary>Relief ratio. Corresponds to ReliefRatio. Not decoded.</summary>
    public double? ReliefRatio { get; set; }

    /// <summary>Relief width in meters. Corresponds to ReliefWidth. Resolved from the measured anonymous relief block (relief_custom sample: 0.0015); D8/D9 are display copies of the relief depth, not the authoritative width.</summary>
    public double? ReliefWidth { get; set; }

    /// <summary>Relief depth in meters. Corresponds to ReliefDepth. Resolved from the measured anonymous relief block (relief_custom sample: 0.0025); D8/D9 display the same depth.</summary>
    public double? ReliefDepth { get; set; }

    /// <summary>Relief tear type. Corresponds to ReliefTearType. Not decoded.</summary>
    public int? ReliefTearType { get; set; }

    /// <summary>Whether to use the default bend allowance. Corresponds to UseDefaultBendAllowance. Not decoded.</summary>
    public bool? UseDefaultBendAllowance { get; set; }

    /// <summary>Whether to use the default bend relief. Corresponds to UseDefaultBendRelief. Not decoded.</summary>
    public bool? UseDefaultBendRelief { get; set; }

    /// <summary>Tear gap distance in meters. Corresponds to GapDistance. Not decoded.</summary>
    public double? GapDistance { get; set; }

    // ── Angle reference (SW2021+) ──

    /// <summary>Reference face for the bend angle. Corresponds to AngleReference. Not decoded.</summary>
    public EntityReference? AngleReference { get; set; }

    /// <summary>Whether to lock the bend angle. Corresponds to LockAngle. Not decoded.</summary>
    public bool? LockAngle { get; set; }

    /// <summary>Whether the flange is perpendicular to the angle reference face. Corresponds to PerpendicularToFace. Not decoded.</summary>
    public bool? PerpendicularToFace { get; set; }

    /// <summary>Whether the Up-To-Vertex plane is normal to the flange end face. Corresponds to NormalToFlangePlane. Not decoded.</summary>
    public bool? NormalToFlangePlane { get; set; }

    // ── Method-derived data (GetCustomBendAllowance / GetEdgeCount / GetPositionReferenceType) ──

    /// <summary>Custom bend allowance from GetCustomBendAllowance(). Not decoded.</summary>
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    /// <summary>Edge count from GetEdgeCount(). Resolved for the observed one-edge layout via moEdgeRef_c; persistent edge references remain undecoded.</summary>
    public int? EdgeCount { get; set; }

    /// <summary>Position reference type from GetPositionReferenceType(). Not decoded.</summary>
    public int? PositionReferenceType { get; set; }
}

/// <summary>
/// Per-field evidence flags for <see cref="MiterFlangeFeatureDefinitionData"/>.
/// Every flag is false until a controlled sample proves the layout.
/// </summary>
public sealed class MiterFlangeParameterResolution
{
    public bool Edges { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool BendRadius { get; internal set; }
    public bool PositionType { get; internal set; }
    public bool UsePositionTrimSideBends { get; internal set; }
    public bool GapDistance { get; internal set; }
    public bool StartOffset { get; internal set; }
    public bool EndOffset { get; internal set; }
    public bool ReliefType { get; internal set; }
    public bool UseReliefRatio { get; internal set; }
    public bool ReliefRatio { get; internal set; }
    public bool ReliefWidth { get; internal set; }
    public bool ReliefDepth { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool UseDefaultBendRelief { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool EdgeCount { get; internal set; }
}

/// <summary>
/// Miter flange feature definition data. Corresponds to the SolidWorks API
/// <c>IMiterFlangeFeatureData</c> interface (TypeName2 <c>SMMiteredFlange</c>).
/// <para>
/// No native miter-flange sample has been processed yet. Every API value is
/// nullable and has no default; <see cref="ParameterResolution"/> starts fully
/// false. The MFC class name is the catalog/TODO candidate
/// <c>moMiterFlange_c</c> and must be confirmed by a controlled sample. The
/// catalog/TODO TypeId candidate <c>0x64</c> is deliberately not encoded
/// (<see cref="TypeId"/> returns 0).
/// </para>
/// </summary>
public sealed class MiterFlangeFeatureDefinitionData : IFeatureData
{
    /// <summary>Catalog/TODO candidate MFC class name; pending sample confirmation.</summary>
    public const string SerializedClassName = "moMiterFlange_c";

    /// <inheritdoc />
    public string ClassName => SerializedClassName;

    /// <summary>
    /// Serialized type id. Returns 0 because the catalog/TODO candidate 0x64 has
    /// not been confirmed against a native sample; do not hardcode it.
    /// </summary>
    public uint TypeId => 0;

    /// <summary>Per-field evidence flags. All false until a sample is decoded.</summary>
    public MiterFlangeParameterResolution ParameterResolution { get; } = new();

    /// <summary>Attachment edges. Corresponds to IMiterFlangeFeatureData::Edges. Not decoded.</summary>
    public IReadOnlyList<EntityReference>? Edges { get; set; }

    /// <summary>Whether to use the sheet metal default bend radius. Corresponds to UseDefaultBendRadius. Not decoded.</summary>
    public bool? UseDefaultBendRadius { get; set; }

    /// <summary>Bend radius in meters. Corresponds to BendRadius. Not decoded.</summary>
    public double? BendRadius { get; set; }

    /// <summary>Flange position (swFlangePositionTypes_e). Corresponds to PositionType. Not decoded.</summary>
    public int? PositionType { get; set; }

    /// <summary>Whether to trim side bends. Corresponds to UsePositionTrimSideBends. Not decoded.</summary>
    public bool? UsePositionTrimSideBends { get; set; }

    /// <summary>Tear gap distance in meters. Corresponds to GapDistance. Not decoded.</summary>
    public double? GapDistance { get; set; }

    /// <summary>Start offset in meters. Corresponds to StartOffset. Not decoded.</summary>
    public double? StartOffset { get; set; }

    /// <summary>End offset in meters. Corresponds to EndOffset. Not decoded.</summary>
    public double? EndOffset { get; set; }

    /// <summary>Relief type. Corresponds to ReliefType. Not decoded.</summary>
    public int? ReliefType { get; set; }

    /// <summary>Whether to use the relief ratio. Corresponds to UseReliefRatio. Not decoded.</summary>
    public bool? UseReliefRatio { get; set; }

    /// <summary>Relief ratio. Corresponds to ReliefRatio. Not decoded.</summary>
    public double? ReliefRatio { get; set; }

    /// <summary>Relief width in meters. Corresponds to ReliefWidth. Not decoded.</summary>
    public double? ReliefWidth { get; set; }

    /// <summary>Relief depth in meters. Corresponds to ReliefDepth. Not decoded.</summary>
    public double? ReliefDepth { get; set; }

    /// <summary>Whether to use the default bend allowance. Corresponds to UseDefaultBendAllowance. Not decoded.</summary>
    public bool? UseDefaultBendAllowance { get; set; }

    /// <summary>Whether to use the default bend relief. Corresponds to UseDefaultBendRelief. Not decoded.</summary>
    public bool? UseDefaultBendRelief { get; set; }

    /// <summary>Custom bend allowance from GetCustomBendAllowance(). Not decoded.</summary>
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    /// <summary>Edge count from IGetEdgesCount()/GetEdgeCount. Not decoded.</summary>
    public int? EdgeCount { get; set; }
}
