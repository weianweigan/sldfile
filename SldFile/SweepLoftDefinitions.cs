using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Per-field evidence flags for SweepFeatureModel.
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
/// Per-field evidence flags for LoftFeatureModel.
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
