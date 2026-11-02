using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Per-field evidence flags for <see cref="SweepFeatureDefinitionData"/>.
/// A false flag means the value is not decoded from the ResolvedFeatures stream.
/// </summary>
public sealed class SweepFeatureParameterResolution
{
    /// <summary>Display name was read from the leading MFC WChar string.</summary>
    public bool DisplayName { get; internal set; }

    /// <summary>Opaque folder-header type code at header offset +0x08 was read.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Selection count and ordered reference-id list were read from the header.</summary>
    public bool SelectionReferenceList { get; internal set; }

    /// <summary>Profile/path sketch identities behind the selection references (decoded by SweepLoftSelectionDecoder).</summary>
    public bool ProfileAndPathReferences { get; internal set; }

    /// <summary>Guide-curve sketch identities (decoded by SweepLoftSelectionDecoder).</summary>
    public bool GuideCurveReferences { get; internal set; }

    /// <summary>Thin-feature layout and wall thickness (decoded).</summary>
    public bool ThinFeature { get; internal set; }

    /// <summary>Circular-profile layout and diameter (decoded).</summary>
    public bool CircularProfile { get; internal set; }

    /// <summary>Merge result decoded from the measured sweep option slot.</summary>
    public bool Merge { get; internal set; }

    /// <summary>Path alignment type decoded from the legacy family slot (0 or 10 only).</summary>
    public bool PathAlignmentType { get; internal set; }

    /// <summary>Twist control type decoded from the measured sweep option slot (0, 1 or 8).</summary>
    public bool TwistControlType { get; internal set; }

    /// <summary>Twist angle decoded from the measured sweep option slot.</summary>
    public bool TwistAngle { get; internal set; }

    /// <summary>Merge / feature scope / auto-select. Not decoded.</summary>
    public bool MergeAndScope { get; internal set; }

    /// <summary>Twist, tangency, path alignment, direction and remaining sweep options. Not decoded.</summary>
    public bool SweepParameters { get; internal set; }
}

/// <summary>
/// Structural sweep feature data read from a serialized <c>moSweep_c</c> node
/// (SolidWorks 2026 <c>Contents/Config-0-ResolvedFeatures</c>).
/// <para>
/// Evidence from six native samples (<c>SldFileTests/TestFiles/Features2026/SweepOptions</c>):
/// the folder header stores an opaque type code and an ordered selection-reference
/// list, and the selection layer is decoded by
/// <c>SweepLoftSelectionDecoder</c> into sketch persistent references with roles
/// (profile/path/guide), circular-profile diameter and thin-wall thickness.
/// Header selection ids are correlated by position only. Merge/scope, twist,
/// tangency, path alignment and all remaining options are still undecoded.
/// See <see cref="Resolution"/>.
/// </para>
/// </summary>
public sealed class SweepFeatureDefinitionData : IFeatureData
{
    internal SweepFeatureDefinitionData(
        string displayName,
        uint serializedTypeId,
        IReadOnlyList<ushort> selectionReferenceIds,
        IReadOnlyList<SketchCurveReference> references,
        bool circularProfileLayout,
        double? circularProfileDiameter,
        bool thinFeatureLayout,
        double? thinWallThickness,
        bool isHeaderSelectionIdMappingPositional,
        bool? merge = null,
        int? twistControlType = null,
        double? twistAngleRad = null,
        int? pathAlignmentType = null)
    {
        DisplayName = displayName ?? string.Empty;
        SerializedTypeId = serializedTypeId;
        SelectionReferenceIds = selectionReferenceIds ?? Array.Empty<ushort>();
        References = references ?? Array.Empty<SketchCurveReference>();
        CircularProfileLayout = circularProfileLayout;
        CircularProfileDiameter = circularProfileDiameter;
        ThinFeatureLayout = thinFeatureLayout;
        ThinWallThickness = thinWallThickness;
        IsHeaderSelectionIdMappingPositional = isHeaderSelectionIdMappingPositional;
        Merge = merge;
        TwistControlType = twistControlType;
        TwistAngleRad = twistAngleRad;
        PathAlignmentType = pathAlignmentType;

        bool hasProfileOrPath = false;
        bool hasGuide = false;
        foreach (var reference in References)
        {
            if (reference.Role == SketchCurveRole.Profile || reference.Role == SketchCurveRole.Path)
                hasProfileOrPath = true;
            else if (reference.Role == SketchCurveRole.Guide)
                hasGuide = true;
        }

        Resolution = new SweepFeatureParameterResolution
        {
            DisplayName = !string.IsNullOrEmpty(DisplayName),
            SerializedTypeId = true,
            SelectionReferenceList = SelectionReferenceIds.Count > 0,
            ProfileAndPathReferences = hasProfileOrPath,
            GuideCurveReferences = hasGuide,
            ThinFeature = ThinFeatureLayout,
            CircularProfile = CircularProfileLayout,
            Merge = Merge.HasValue,
            PathAlignmentType = PathAlignmentType.HasValue,
            TwistControlType = TwistControlType.HasValue,
            TwistAngle = TwistAngleRad.HasValue,
            MergeAndScope = false,
            SweepParameters = false,
        };
    }

    /// <summary>Serialized MFC class name; observed value is <c>moSweep_c</c>.</summary>
    public string ClassName => "moSweep_c";

    /// <summary>
    /// Opaque folder-header type code at header offset +0x08. Observed values:
    /// 0x4B (circular profile, 1 selection), 0x4C (profile+path, 2 selections),
    /// 0x4D (profile+path+guide, 3 selections). Not a stable class identifier.
    /// </summary>
    public uint TypeId => SerializedTypeId;

    /// <summary>Opaque folder-header type code; same value as <see cref="TypeId"/>.</summary>
    public uint SerializedTypeId { get; }

    /// <summary>FeatureManager display name.</summary>
    public string DisplayName { get; }

    /// <summary>Number of serialized selection references.</summary>
    public int SelectionCount => SelectionReferenceIds.Count;

    /// <summary>
    /// Ordered opaque selection-reference ids from the folder header. They are not
    /// sketch persistent ids; each id is correlated to <see cref="References"/> by
    /// position only (<see cref="IsHeaderSelectionIdMappingPositional"/>).
    /// </summary>
    public IReadOnlyList<ushort> SelectionReferenceIds { get; }

    /// <summary>
    /// Decoded sketch references in wrapper order (profile first, then path/guides).
    /// Each reference carries its sketch persistent id, role and positional header id.
    /// </summary>
    public IReadOnlyList<SketchCurveReference> References { get; }

    /// <summary>True when the circular-profile sweep layout was decoded (single path reference).</summary>
    public bool CircularProfileLayout { get; }

    /// <summary>Decoded circular profile diameter in meters, or null when unresolved/not applicable.</summary>
    public double? CircularProfileDiameter { get; }

    /// <summary>True when the thin-feature layout was decoded.</summary>
    public bool ThinFeatureLayout { get; }

    /// <summary>Decoded thin wall thickness in meters, or null when unresolved/conflicting.</summary>
    public double? ThinWallThickness { get; }

    /// <summary>Merge result decoded from the measured sweep option slot; null when unresolved.</summary>
    public bool? Merge { get; }

    /// <summary>Path alignment type decoded from legacy family slots (0 or 10); null when unresolved.</summary>
    public int? PathAlignmentType { get; }

    /// <summary>Twist control type (native readbacks: 0 none, 1 follow path, 8 constant twist); null when unresolved.</summary>
    public int? TwistControlType { get; }

    /// <summary>Twist angle in radians; null when unresolved.</summary>
    public double? TwistAngleRad { get; }

    /// <summary>
    /// True when header selection ids were correlated to sketch references by list
    /// position only; no byte-level link exists in the observed samples.
    /// </summary>
    public bool IsHeaderSelectionIdMappingPositional { get; }

    /// <summary>Per-field evidence flags. Undecoded fields remain false.</summary>
    public SweepFeatureParameterResolution Resolution { get; }
}

/// <summary>
/// Per-field evidence flags for <see cref="LoftFeatureDefinitionData"/>.
/// A false flag means the value is not decoded from the ResolvedFeatures stream.
/// </summary>
public sealed class LoftFeatureParameterResolution
{
    /// <summary>Display name was read from the leading MFC WChar string.</summary>
    public bool DisplayName { get; internal set; }

    /// <summary>Opaque folder-header type code at header offset +0x08 was read.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Selection count and ordered reference-id list were read from the header.</summary>
    public bool SelectionReferenceList { get; internal set; }

    /// <summary>Ordered profile sketch references were decoded.</summary>
    public bool Profiles { get; internal set; }

    /// <summary>Guide-curve sketch references were decoded.</summary>
    public bool GuideCurves { get; internal set; }

    /// <summary>Centerline reference. Not decoded.</summary>
    public bool Centerline { get; internal set; }

    /// <summary>Start/end tangency, draft angle and constraint applicability. Not decoded.</summary>
    public bool StartEndConstraints { get; internal set; }

    /// <summary>Start tangency type decoded from the measured tangency variant (TypeId 0x4D).</summary>
    public bool StartTangencyType { get; internal set; }

    /// <summary>End tangency type decoded from the measured tangency variant (TypeId 0x4D).</summary>
    public bool EndTangencyType { get; internal set; }

    /// <summary>Thin-feature layout and wall thickness were decoded.</summary>
    public bool ThinFeature { get; internal set; }

    /// <summary>Circular-profile layout and diameter were decoded (loft samples: not present).</summary>
    public bool CircularProfile { get; internal set; }

    /// <summary>Merge / feature scope / auto-select. Not decoded.</summary>
    public bool MergeAndScope { get; internal set; }

    /// <summary>Close, pick points, sections and remaining loft options. Not decoded.</summary>
    public bool LoftParameters { get; internal set; }
}

/// <summary>
/// Structural loft feature data read from a serialized <c>moBlend_c</c> node.
/// <para>
/// Evidence from four native samples (<c>SldFileTests/TestFiles/Features2026/LoftOptions</c>):
/// the serialized class is <c>moBlend_c</c> (not <c>moLoft_c</c>); the folder header
/// stores an opaque type code plus an ordered selection-reference list, and
/// <c>SweepLoftSelectionDecoder</c> decodes the profile/guide sketch references and
/// roles. Header selection ids are correlated by position only. Centerline,
/// start/end constraints and all numeric parameters remain undecoded.
/// See <see cref="Resolution"/>.
/// </para>
/// </summary>
public sealed class LoftFeatureDefinitionData : IFeatureData
{
    internal LoftFeatureDefinitionData(
        string displayName,
        uint serializedTypeId,
        IReadOnlyList<ushort> selectionReferenceIds,
        IReadOnlyList<SketchCurveReference> references,
        bool circularProfileLayout,
        double? circularProfileDiameter,
        bool thinFeatureLayout,
        double? thinWallThickness,
        bool isHeaderSelectionIdMappingPositional,
        int? startTangencyType = null,
        int? endTangencyType = null,
        SketchCurveReference? centerline = null)
    {
        DisplayName = displayName ?? string.Empty;
        SerializedTypeId = serializedTypeId;
        SelectionReferenceIds = selectionReferenceIds ?? Array.Empty<ushort>();
        References = references ?? Array.Empty<SketchCurveReference>();
        CircularProfileLayout = circularProfileLayout;
        CircularProfileDiameter = circularProfileDiameter;
        ThinFeatureLayout = thinFeatureLayout;
        ThinWallThickness = thinWallThickness;
        IsHeaderSelectionIdMappingPositional = isHeaderSelectionIdMappingPositional;
        StartTangencyType = startTangencyType;
        EndTangencyType = endTangencyType;
        Centerline = centerline;

        bool hasProfile = false;
        bool hasGuide = false;
        foreach (var reference in References)
        {
            if (reference.Role == SketchCurveRole.Profile)
                hasProfile = true;
            else if (reference.Role == SketchCurveRole.Guide)
                hasGuide = true;
        }

        Resolution = new LoftFeatureParameterResolution
        {
            DisplayName = !string.IsNullOrEmpty(DisplayName),
            SerializedTypeId = true,
            SelectionReferenceList = SelectionReferenceIds.Count > 0,
            Profiles = hasProfile,
            GuideCurves = hasGuide,
            Centerline = Centerline != null,
            StartEndConstraints = false,
            StartTangencyType = StartTangencyType.HasValue,
            EndTangencyType = EndTangencyType.HasValue,
            ThinFeature = ThinFeatureLayout,
            CircularProfile = CircularProfileLayout,
            MergeAndScope = false,
            LoftParameters = false,
        };
    }

    /// <summary>Serialized MFC class name; observed value is <c>moBlend_c</c>.</summary>
    public string ClassName => "moBlend_c";

    /// <summary>
    /// Opaque folder-header type code at header offset +0x08. Observed values:
    /// 0x4D (two profiles or two profiles+tangency), 0x4E (two profiles+guide),
    /// 0x50 (three profiles). Not a stable class identifier.
    /// </summary>
    public uint TypeId => SerializedTypeId;

    /// <summary>Opaque folder-header type code; same value as <see cref="TypeId"/>.</summary>
    public uint SerializedTypeId { get; }

    /// <summary>FeatureManager display name.</summary>
    public string DisplayName { get; }

    /// <summary>Number of serialized selection references.</summary>
    public int SelectionCount => SelectionReferenceIds.Count;

    /// <summary>
    /// Ordered opaque selection-reference ids from the folder header. They are not
    /// sketch persistent ids; each id is correlated to <see cref="References"/> by
    /// position only (<see cref="IsHeaderSelectionIdMappingPositional"/>).
    /// </summary>
    public IReadOnlyList<ushort> SelectionReferenceIds { get; }

    /// <summary>
    /// Decoded sketch references in wrapper order (profiles first, then guides).
    /// Each reference carries its sketch persistent id, role and positional header id.
    /// </summary>
    public IReadOnlyList<SketchCurveReference> References { get; }

    /// <summary>True when the circular-profile layout was decoded (not present in loft samples).</summary>
    public bool CircularProfileLayout { get; }

    /// <summary>Decoded circular profile diameter in meters, or null when unresolved/not applicable.</summary>
    public double? CircularProfileDiameter { get; }

    /// <summary>True when the thin-feature layout was decoded (not present in loft samples).</summary>
    public bool ThinFeatureLayout { get; }

    /// <summary>Decoded thin wall thickness in meters, or null when unresolved/conflicting.</summary>
    public double? ThinWallThickness { get; }

    /// <summary>
    /// Start tangency type decoded for the measured TypeId 0x4D tangency variant
    /// (0 = none, 1 = normal-to-profile). Null for other layouts.
    /// </summary>
    public int? StartTangencyType { get; }

    /// <summary>
    /// End tangency type decoded for the measured TypeId 0x4D tangency variant
    /// (0 = none, 1 = normal-to-profile). Null for other layouts.
    /// </summary>
    public int? EndTangencyType { get; }

    /// <summary>
    /// Decoded centerline sketch reference. The measured centerline sample appends a
    /// second sketch reference after the guide wrapper; guide-only lofts keep this null.
    /// </summary>
    public SketchCurveReference? Centerline { get; }

    /// <summary>
    /// True when header selection ids were correlated to sketch references by list
    /// position only; no byte-level link exists in the observed samples.
    /// </summary>
    public bool IsHeaderSelectionIdMappingPositional { get; }

    /// <summary>Per-field evidence flags. Undecoded fields remain false.</summary>
    public LoftFeatureParameterResolution Resolution { get; }
}
