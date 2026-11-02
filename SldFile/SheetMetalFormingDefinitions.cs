using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Fixed tutorial-derived feature templates. Each value corresponds to one
/// stripped native document and reproduces its ResolvedFeatures stream
/// byte-for-byte; the template cannot be parameterized.
/// </summary>
public enum TutorialSheetMetalFeatureTemplate
{
    /// <summary>Jog1 from Complex Sheet Metal Part.SLDPRT (version 0x1AA).</summary>
    JogComplexPart,

    /// <summary>Unfold2 from Paper Airplane.SLDPRT (version 0x155).</summary>
    UnFoldPaperAirplane,

    /// <summary>Fold2 from Paper Airplane.SLDPRT (version 0x155).</summary>
    FoldPaperAirplane,

    /// <summary>Flat-Pattern2 from Paper Airplane.SLDPRT (version 0x155).</summary>
    FlatPatternPaperAirplane,

    /// <summary>Flat-Pattern1 from Complex Sheet Metal Part.SLDPRT (version 0x1AA).</summary>
    FlatPatternComplexPart,

    /// <summary>Flat-Pattern1 from drive sled.sldprt (version 0xC7).</summary>
    FlatPatternDriveSled
}

// ═══════════════════════════════════════════════════════════════════════════
// Task 2.3 / 2.4 sheet metal forming definitions.
//
// Every value property is nullable and starts as null: a false resolution flag
// (and every flag starts false) means the value is unknown, not a native default.
// Hem (0x67), lofted bend (0x72) and flat pattern (0x5D/0x5E) carry TypeIds
// confirmed by controlled samples. The remaining MFC class names and TypeIds
// below are catalog/TODO candidates only; no controlled sample has confirmed
// them yet, so TypeId returns 0 and the class names must not be treated as
// decoded serialization facts.
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Per-field evidence flags for <see cref="HemFeatureDefinitionData"/>. All start false.</summary>
public sealed class HemParameterResolution
{
    public bool Angle { get; internal set; }
    public bool BendPosition { get; internal set; }
    public bool Edges { get; internal set; }
    public bool GapDistance { get; internal set; }
    public bool Length { get; internal set; }
    public bool MiterGap { get; internal set; }
    public bool Radius { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool Type { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool EdgeCount { get; internal set; }
}

/// <summary>
/// Hem feature definition data. Corresponds to <c>IHemFeatureData</c>
/// (TypeName2 <c>Hem</c>).
/// <para>
/// Native sample evidence (SheetMetalFormingOptions/sheetmetal_hem_open and
/// sheetmetal_hem_closed, ResolvedFeatures version 0x73): the serialized class
/// is <c>moHem_c</c> and the FolderHeader TypeId is <b>0x67</b> (the
/// catalog/TODO candidate 0x65 was not observed). Type, Length, GapDistance,
/// Angle, Radius, MiterGap and EdgeCount are resolved against the native
/// IHemFeatureData snapshots; BendPosition, ReverseDirection and
/// UseDefaultBendAllowance are constant in both samples and remain unresolved.
/// </para>
/// </summary>
public sealed class HemFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moHem_c";

    /// <summary>Observed FolderHeader TypeId in both native hem samples (version 0x73).</summary>
    public const uint ObservedSerializedTypeId = 0x67;

    public string ClassName => SerializedClassName;
    public uint TypeId => ObservedSerializedTypeId;
    public HemParameterResolution ParameterResolution { get; } = new();

    /// <summary>Hem type (swHemTypes_e: 0=Open,1=Closed,2=TearDrop,3=Rolled,4=Double). Resolved from the u16 at D8 value + 0x6E.</summary>
    public int? Type { get; set; }

    /// <summary>Hem position (swHemPositionTypes_e: 0=Inside,1=Outside). Not decoded.</summary>
    public int? BendPosition { get; set; }

    /// <summary>Hem edges. Corresponds to IHemFeatureData::Edges. Not decoded.</summary>
    public IReadOnlyList<EntityReference>? Edges { get; set; }

    /// <summary>Whether to reverse the hem direction. Corresponds to ReverseDirection. Not decoded.</summary>
    public bool? ReverseDirection { get; set; }

    /// <summary>Hem length in meters. Corresponds to Length. Resolved from named D7.</summary>
    public double? Length { get; set; }

    /// <summary>Hem gap distance in meters. Corresponds to GapDistance. Resolved from the D8 record after D7 (open 0.0005, closed 0.0001).</summary>
    public double? GapDistance { get; set; }

    /// <summary>Hem size angle in radians. Corresponds to Angle. Resolved from named D4.</summary>
    public double? Angle { get; set; }

    /// <summary>Hem radius in meters. Corresponds to Radius. Resolved from the D1 record between D4 and D7.</summary>
    public double? Radius { get; set; }

    /// <summary>Miter gap in meters. Corresponds to MiterGap. Resolved from the D9 record after D7 (0.0005 in both samples).</summary>
    public double? MiterGap { get; set; }

    /// <summary>Whether to use the default bend allowance. Corresponds to UseDefaultBendAllowance. Not decoded.</summary>
    public bool? UseDefaultBendAllowance { get; set; }

    /// <summary>Custom bend allowance from GetCustomBendAllowance(). Not decoded.</summary>
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    /// <summary>Edge count from GetEdgesCount(). Resolved for the observed one-edge layout via moEdgeRef_c.</summary>
    public int? EdgeCount { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="JogFeatureDefinitionData"/>. All start false.</summary>
public sealed class JogParameterResolution
{
    public bool BendRadius { get; internal set; }
    public bool DimensionPositionType { get; internal set; }
    public bool FixedFace { get; internal set; }
    public bool FixedPoint { get; internal set; }
    public bool FixProjectedLength { get; internal set; }
    public bool JogAngle { get; internal set; }
    public bool JogPositionType { get; internal set; }
    public bool OffsetDistance { get; internal set; }
    public bool OffsetReference { get; internal set; }
    public bool OffsetType { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool ReverseOffset { get; internal set; }
    public bool UseDefaultBendAllowance { get; internal set; }
    public bool UseDefaultBendRadius { get; internal set; }
    public bool CustomBendAllowance { get; internal set; }
    public bool OffsetReferenceType { get; internal set; }
}

/// <summary>
/// Jog feature definition data. Corresponds to <c>IJogFeatureData</c>
/// (TypeName2 <c>Jog</c>).
/// <para>
/// Native tutorial sample evidence (Complex Sheet Metal Part.SLDPRT,
/// ResolvedFeatures version 0x1AA): the serialized class is <c>moJog_c</c> and
/// the observed FolderHeader TypeId is <b>0x159</b>. BendRadius, JogAngle and
/// OffsetDistance are resolved for that layout (0.00254 m, π/2 rad and
/// 0.0127 m, matching IJogFeatureData). Flags and selections remain unresolved.
/// </para>
/// </summary>
public sealed class JogFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moJog_c";
    public string ClassName => SerializedClassName;

    /// <summary>Observed FolderHeader TypeId; 0 until parsed.</summary>
    public uint TypeId { get; internal set; }

    public JogParameterResolution ParameterResolution { get; } = new();

    /// <summary>Bend radius in meters. Corresponds to BendRadius. Resolved from the first named D1 after the tutorial Jog tag (0.00254 m).</summary>
    public double? BendRadius { get; set; }

    /// <summary>Dimension position (swJogDimensionPositionType_e: 1..3). Corresponds to DimensionPositionType. Resolved from the tutorial flag block (internal 1 -> API 2, 2 -> API 1).</summary>
    public int? DimensionPositionType { get; set; }

    /// <summary>
    /// Fixed face. Corresponds to IJogFeatureData::FixedFace.
    /// Native tutorial readback: IFace2, featureId 31, faceId 0. The Jog window
    /// contains only an moEdgeRef_c/moCompEdge_c pair and the nearest
    /// moCompFace_c belongs to a later face-ref feature, so no provable face-id
    /// slot is decoded; stays null.
    /// </summary>
    public EntityReference? FixedFace { get; set; }

    /// <summary>Fixed point (x,y,z). Corresponds to FixedPoint. Not decoded.</summary>
    public double[]? FixedPoint { get; set; }

    /// <summary>Whether to fix the projected length. Corresponds to FixProjectedLength. Resolved from the tutorial offset flag block.</summary>
    public bool? FixProjectedLength { get; set; }

    /// <summary>Jog angle in radians. Corresponds to JogAngle. Resolved from the first named D4 after the tutorial Jog tag (π/2).</summary>
    public double? JogAngle { get; set; }

    /// <summary>Jog position (swJogPositionType_e: 1..4). Corresponds to JogPositionType. Resolved for the tutorial layout (internal 0 -> API 1, 1 -> API 2).</summary>
    public int? JogPositionType { get; set; }

    /// <summary>Offset distance in meters. Corresponds to OffsetDistance. Resolved as the first positive named D2 after the first D3 (0.0127 m).</summary>
    public double? OffsetDistance { get; set; }

    /// <summary>Offset reference. Corresponds to OffsetReference. Not decoded (native tutorial value is null).</summary>
    public EntityReference? OffsetReference { get; set; }

    /// <summary>Offset type (swJogOffsetTypes_e: 1..5). Corresponds to OffsetType. Resolved for the tutorial layout (internal 0 -> API 1, 3 -> API 2); other codes stay unresolved.</summary>
    public int? OffsetType { get; set; }

    /// <summary>Whether to reverse the direction. Corresponds to ReverseDirection. Resolved from the tutorial flag block.</summary>
    public bool? ReverseDirection { get; set; }

    /// <summary>Whether to reverse the offset. Corresponds to ReverseOffset. Resolved from the tutorial offset flag block.</summary>
    public bool? ReverseOffset { get; set; }

    /// <summary>Whether to use the default bend allowance. Corresponds to UseDefaultBendAllowance. Resolved from the tutorial default flag block.</summary>
    public bool? UseDefaultBendAllowance { get; set; }

    /// <summary>Whether to use the default bend radius. Corresponds to UseDefaultBendRadius. Resolved from the tutorial default flag block.</summary>
    public bool? UseDefaultBendRadius { get; set; }

    /// <summary>Custom bend allowance from GetCustomBendAllowance(). Not decoded.</summary>
    public FlangeCustomBendAllowanceData? CustomBendAllowance { get; set; }

    /// <summary>Offset reference type from GetOffsetReferenceType(). Not decoded.</summary>
    public int? OffsetReferenceType { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="LoftedBendFeatureDefinitionData"/>. All start false.</summary>
public sealed class LoftedBendParameterResolution
{
    public bool BendLineControlOption { get; internal set; }
    public bool Direction { get; internal set; }
    public bool FacetingOption { get; internal set; }
    public bool FacetValue { get; internal set; }
    public bool FormedMethod { get; internal set; }
    public bool MaximumDeviation { get; internal set; }
    public bool NumberOfBendLines { get; internal set; }
    public bool Profiles { get; internal set; }
    public bool ReferToEndPoint { get; internal set; }
    public bool SymmetricThickness { get; internal set; }
    public bool Thickness { get; internal set; }
    public bool ProfileCount { get; internal set; }
}

/// <summary>
/// Lofted bend feature definition data. Corresponds to
/// <c>ILoftedBendsFeatureData</c> (TypeName2 <c>LoftedBend</c>).
/// <para>
/// Native sample evidence (LoftedBendOptions/lofted_bend_2lines.SLDPRT,
/// ResolvedFeatures version 0x74): the serialized class is
/// <c>moLoftedBend_c</c> and the FolderHeader TypeId is <b>0x72</b> (the
/// catalog/TODO candidate 0x69 was not observed). Thickness and
/// NumberOfBendLines are resolved; Direction, BendLineControlOption,
/// MaximumDeviation, ReferToEndPoint, FacetingOption, FacetValue, FormedMethod,
/// SymmetricThickness and the two profile references have no varying sample or
/// reliable serialized slot and remain unresolved.
/// </para>
/// </summary>
public sealed class LoftedBendFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moLoftedBend_c";

    /// <summary>Observed FolderHeader TypeId in the native non-formed sample (version 0x74).</summary>
    public const uint ObservedSerializedTypeId = 0x72;

    public string ClassName => SerializedClassName;
    public uint TypeId => ObservedSerializedTypeId;
    public LoftedBendParameterResolution ParameterResolution { get; } = new();

    /// <summary>Bend line control (swBendLineControlOption_e: 0=NumberOfBendLine,1=MaximumDeviation). Not decoded.</summary>
    public int? BendLineControlOption { get; set; }

    /// <summary>Direction flag. Corresponds to Direction. Not decoded.</summary>
    public bool? Direction { get; set; }

    /// <summary>Faceting option (swLoftedBendFacetOptions_e: 0..3). Corresponds to FacetingOption. Not decoded.</summary>
    public int? FacetingOption { get; set; }

    /// <summary>Facet value. Corresponds to FacetValue. Not decoded.</summary>
    public double? FacetValue { get; set; }

    /// <summary>Whether the formed method is used. Corresponds to FormedMethod. Not decoded.</summary>
    public bool? FormedMethod { get; set; }

    /// <summary>Maximum deviation. Corresponds to MaximumDeviation. Not decoded.</summary>
    public double? MaximumDeviation { get; set; }

    /// <summary>Number of bend lines. Corresponds to NumberOfBendLines. Resolved from named D14.</summary>
    public int? NumberOfBendLines { get; set; }

    /// <summary>Profile sketches. Corresponds to Profiles. Not decoded.</summary>
    public IReadOnlyList<EntityReference>? Profiles { get; set; }

    /// <summary>Whether to refer to the end point. Corresponds to ReferToEndPoint. Not decoded.</summary>
    public bool? ReferToEndPoint { get; set; }

    /// <summary>Symmetric thickness flag. Corresponds to SymmetricThickness. Not decoded.</summary>
    public bool? SymmetricThickness { get; set; }

    /// <summary>Thickness in meters. Corresponds to Thickness. Resolved from the double at D14 name - 0xBB4 (validated by 1.0 gates and a second 0.001 slot).</summary>
    public double? Thickness { get; set; }

    /// <summary>Profile count from GetProfileCount(). Not decoded.</summary>
    public int? ProfileCount { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="NormalCutFeatureDefinitionData"/>. All start false.</summary>
public sealed class NormalCutParameterResolution
{
    public bool AutoPropagation { get; internal set; }
    public bool CutDirection { get; internal set; }
    public bool LayerAdjustmentValue { get; internal set; }
    public bool LinkToKFactor { get; internal set; }
    public bool NormalCutParameters { get; internal set; }
    public bool OffsetPlaneReference { get; internal set; }
    public bool OptimizeGeometry { get; internal set; }
}

/// <summary>
/// Normal cut feature definition data. Corresponds to
/// <c>ISMNormalCutFeatureData2</c> (TypeName2 <c>NormalCut</c>). Candidate MFC
/// class <c>moSMNormalCut_c</c>, candidate TypeId 0x67; not sample-confirmed,
/// so <see cref="TypeId"/> returns 0.
/// <para>
/// Only the input/selection properties are exposed. Group creation/lookup
/// results (CreateGroup/GetGroupByName/GetGroupNames) are outputs of the native
/// operation and are intentionally not modelled as file data.
/// </para>
/// </summary>
public sealed class NormalCutFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moSMNormalCut_c";
    public string ClassName => SerializedClassName;
    public uint TypeId { get; internal set; } = 0;
    public NormalCutParameterResolution ParameterResolution { get; } = new();

    /// <summary>Whether to auto-propagate. Corresponds to AutoPropagation. Not decoded.</summary>
    public bool? AutoPropagation { get; set; }

    /// <summary>Cut direction reference. Corresponds to CutDirection. Not decoded.</summary>
    public DirectionReference? CutDirection { get; set; }

    /// <summary>Layer adjustment value. Corresponds to LayerAdjustmentValue. Not decoded.</summary>
    public double? LayerAdjustmentValue { get; set; }

    /// <summary>Whether to link to the K-factor. Corresponds to LinkToKFactor. Not decoded.</summary>
    public bool? LinkToKFactor { get; set; }

    /// <summary>Normal cut parameters (swNormalCutParameters_e: 0=Extent,1=OffsetPlane). Not decoded.</summary>
    public int? NormalCutParameters { get; set; }

    /// <summary>Offset plane reference. Corresponds to OffsetPlaneReference. Not decoded.</summary>
    public EntityReference? OffsetPlaneReference { get; set; }

    /// <summary>Whether to optimize the geometry. Corresponds to OptimizeGeometry. Not decoded.</summary>
    public bool? OptimizeGeometry { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="FlatPatternFeatureDefinitionData"/>. All start false.</summary>
public sealed class FlatPatternParameterResolution
{
    public bool BreakCornerRadius { get; internal set; }
    public bool BreakCornerType { get; internal set; }
    public bool CornerTreatment { get; internal set; }
    public bool CornerTrimRatioToThickness { get; internal set; }
    public bool CornerTrimReliefDistance { get; internal set; }
    public bool CornerTrimReliefType { get; internal set; }
    public bool ExcludedFaces { get; internal set; }
    public bool FixedFace { get; internal set; }
    public bool FixedFace2 { get; internal set; }
    public bool MergeFace { get; internal set; }
    public bool ShowSlitInCornerRelief { get; internal set; }
    public bool SimplifyBends { get; internal set; }
    public bool UseRatioToThickness { get; internal set; }
    public bool AddCornerTrim { get; internal set; }
    public bool BreakCorners { get; internal set; }
}

/// <summary>
/// Flat pattern feature definition data. Corresponds to
/// <c>IFlatPatternFeatureData</c> (TypeName2 <c>FlatPattern</c>).
/// <para>
/// Native samples (ResolvedFeatures version 0x73) show two serialized layouts of
/// <c>moFlatPattern_c</c>: FolderHeader TypeId <b>0x5D</b> in
/// sm_base_flange_10mm/20mm/thick2/rad3 and <b>0x5E</b> in the
/// SheetMetalOptions series. <see cref="TypeId"/> carries the observed header
/// value when parsed. FixedFace is resolved from the unique persistent
/// moFaceRef_c/moCompFace_c selection; the option block after moFaceRef_c resolves
/// MergeFace, SimplifyBends, CornerTreatment, BreakCornerType/Radius and
/// CornerTrimReliefType/Distance against the variants2 samples.
/// CornerTrimRatioToThickness, UseRatioToThickness, ShowSlitInCornerRelief and
/// ExcludedFaces remain unresolved.
/// </para>
/// <para>
/// The inline <b>0x66</b> layout (ResolvedFeatures 0x74,
/// sm_base_flange_after_fillet) resolves MergeFace, CornerTreatment,
/// SimplifyBends, BreakCornerType/Radius, CornerTrimReliefType, the shared
/// CornerTrimReliefDistance/CornerTrimRatioToThickness double,
/// UseRatioToThickness and ShowSlitInCornerRelief from fixed suffix slots;
/// FixedFace/FixedFace2/ExcludedFaces remain unresolved because the native API
/// did not persist independent values.
/// </para>
/// <para>
/// <c>moTemplateFlatPattern_c</c> is a different template class already used by
/// Task 2.1; it must not be routed to this definition.
/// </para>
/// </summary>
public sealed class FlatPatternFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moFlatPattern_c";

    /// <summary>Observed FolderHeader TypeId in the sm_base_flange_* samples.</summary>
    public const uint LegacySerializedTypeId = 0x5D;

    /// <summary>Observed FolderHeader TypeId in the SheetMetalOptions samples.</summary>
    public const uint CurrentSerializedTypeId = 0x5E;

    public string ClassName => SerializedClassName;

    /// <summary>Observed FolderHeader TypeId (0x5D or 0x5E); 0 until parsed.</summary>
    public uint TypeId { get; internal set; }

    public FlatPatternParameterResolution ParameterResolution { get; } = new();

    /// <summary>Break corner radius in meters. Corresponds to BreakCornerRadius. Resolved from the double after moFaceRef_c (+0x47; 0.005 baseline, 0.002 chamfer sample).</summary>
    public double? BreakCornerRadius { get; set; }

    /// <summary>Break corner type (swBreakCornerTypes_e: 0=Fillet,1=Chamfer). Resolved from the u32 after moFaceRef_c (+0x43).</summary>
    public int? BreakCornerType { get; set; }

    /// <summary>Corner treatment flag. Corresponds to CornerTreatment. Resolved from bit 1 (inverse) of the packed flags byte after moFaceRef_c (+0x21).</summary>
    public bool? CornerTreatment { get; set; }

    /// <summary>Corner trim ratio to thickness. Corresponds to CornerTrimRatioToThickness. Not decoded: the ratio sample stores distance and ratio as the same value and only one double slot changes.</summary>
    public double? CornerTrimRatioToThickness { get; set; }

    /// <summary>Corner trim relief distance in meters. Corresponds to CornerTrimReliefDistance. Resolved from the double after moFaceRef_c (+0x3B; 0.005 baseline, 0.002 square, 2.0 ratio sample).</summary>
    public double? CornerTrimReliefDistance { get; set; }

    /// <summary>Corner trim relief type (swCornerReliefType_e). Resolved from the u32 after moFaceRef_c (+0x37; 0 circular, 1 square).</summary>
    public int? CornerTrimReliefType { get; set; }

    /// <summary>Excluded faces. Corresponds to ExcludedFaces. Not decoded.</summary>
    public IReadOnlyList<EntityReference>? ExcludedFaces { get; set; }

    /// <summary>Fixed face. Corresponds to FixedFace. Resolved from the unique persistent moFaceRef_c/moCompFace_c selection.</summary>
    public EntityReference? FixedFace { get; set; }

    /// <summary>Secondary fixed face. Corresponds to FixedFace2. Not decoded.</summary>
    public EntityReference? FixedFace2 { get; set; }

    /// <summary>Whether to merge faces. Corresponds to MergeFace. Resolved from bit 0 of the packed flags byte after moFaceRef_c (+0x21).</summary>
    public bool? MergeFace { get; set; }

    /// <summary>Whether to show slits in corner reliefs. Corresponds to ShowSlitInCornerRelief. Not decoded: constant true in every sample.</summary>
    public bool? ShowSlitInCornerRelief { get; set; }

    /// <summary>Whether to simplify bends. Corresponds to SimplifyBends. Resolved from the 0/1 byte after moFaceRef_c (+0x57).</summary>
    public bool? SimplifyBends { get; set; }

    /// <summary>Whether to use the ratio to thickness. Corresponds to UseRatioToThickness. Not decoded: constant false in every sample.</summary>
    public bool? UseRatioToThickness { get; set; }

    /// <summary>Result of GetAddCornerTrim(). Not decoded.</summary>
    public bool? AddCornerTrim { get; set; }

    /// <summary>Result of GetBreakCorners(). Not decoded.</summary>
    public bool? BreakCorners { get; set; }
}

/// <summary>Per-field evidence flags shared by fold and unfold definitions. All start false.</summary>
public sealed class FoldsParameterResolution
{
    public bool Bends { get; internal set; }
    public bool FixedFace { get; internal set; }
    public bool BendsCount { get; internal set; }
}

/// <summary>
/// Fold feature definition data. Corresponds to <c>IFoldsFeatureData</c>
/// (TypeName2 <c>Fold</c>).
/// <para>
/// Native tutorial sample evidence (Paper Airplane.SLDPRT, version 0x155,
/// TypeId 0x153; drive sled.sldprt, version 0x1AA, TypeId 0x213): class
/// <c>moFold_c</c>. BendsCount is resolved from the number of serialized
/// <c>moOneBend_c</c>/<c>moSM3dBend_c</c> features (3 and 4, matching
/// IFoldsFeatureData::IGetBendsCount). FixedFace and the individual bend
/// references remain unresolved.
/// </para>
/// </summary>
public sealed class FoldFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moFold_c";
    public string ClassName => SerializedClassName;

    /// <summary>Observed FolderHeader TypeId; 0 until parsed.</summary>
    public uint TypeId { get; internal set; }

    public FoldsParameterResolution ParameterResolution { get; } = new();

    /// <summary>
    /// Bends to fold. Corresponds to IFoldsFeatureData::Bends.
    /// Native tutorial readback (Paper): EdgeBend1/EdgeBend2/SketchBend1
    /// (feature ids 129/224/233); drive sled: EdgeBend7/5/6/3 (ids 602/514/596/395).
    /// Serialized moOneBend_c/moSM3dBend_c records carry the names/ids, but their
    /// document order does not match the native Fold order in the drive-sled
    /// sample and no ordered selection list slot is proven; stays null.
    /// </summary>
    public IReadOnlyList<EntityReference>? Bends { get; set; }

    /// <summary>
    /// Fixed face. Corresponds to IFoldsFeatureData::FixedFace.
    /// Native tutorial readback: IFace2 (featureId 115 in Paper, 358 in drive
    /// sled). The stream stores moFixedRefPlnData_c/moProfileRef_w/moCompProfile_c,
    /// but mapping those records to the native face id is not proven; stays null.
    /// </summary>
    public EntityReference? FixedFace { get; set; }

    /// <summary>Bend count from IGetBendsCount(). Not decoded.</summary>
    public int? BendsCount { get; set; }
}

/// <summary>
/// Unfold feature definition data. Corresponds to <c>IFoldsFeatureData</c>
/// (TypeName2 <c>UnFold</c>).
/// <para>
/// Native tutorial sample evidence (Paper Airplane.SLDPRT, version 0x155,
/// TypeId 0x144; drive sled.sldprt, version 0x1AA, TypeId 0x209): class
/// <c>moUnFold_c</c>. BendsCount is resolved from the number of serialized
/// <c>moOneBend_c</c>/<c>moSM3dBend_c</c> features (3 and 4, matching
/// IFoldsFeatureData::IGetBendsCount). FixedFace and the individual bend
/// references remain unresolved.
/// </para>
/// </summary>
public sealed class UnFoldFeatureDefinitionData : IFeatureData
{
    public const string SerializedClassName = "moUnFold_c";
    public string ClassName => SerializedClassName;

    /// <summary>Observed FolderHeader TypeId; 0 until parsed.</summary>
    public uint TypeId { get; internal set; }

    public FoldsParameterResolution ParameterResolution { get; } = new();

    /// <summary>
    /// Bends to unfold. Corresponds to IFoldsFeatureData::Bends.
    /// Native tutorial readback (Paper): EdgeBend1/EdgeBend2/SketchBend1
    /// (feature ids 129/224/233); drive sled: EdgeBend7/5/6/3
    /// (ids 602/514/596/395). Serialized moOneBend_c/moSM3dBend_c records carry
    /// the names/ids, but their document order does not match the native Fold
    /// order in the drive-sled sample and no ordered selection list slot is
    /// proven; stays null.
    /// </summary>
    public IReadOnlyList<EntityReference>? Bends { get; set; }

    /// <summary>
    /// Fixed face. Corresponds to IFoldsFeatureData::FixedFace.
    /// Native tutorial readback: IFace2 (featureId 115 in Paper, 358 in drive
    /// sled). The stream stores moFixedRefPlnData_c/moProfileRef_w/moCompProfile_c,
    /// but mapping those records to the native face id is not proven; stays null.
    /// </summary>
    public EntityReference? FixedFace { get; set; }

    /// <summary>Bend count from IGetBendsCount(). Not decoded.</summary>
    public int? BendsCount { get; set; }
}
