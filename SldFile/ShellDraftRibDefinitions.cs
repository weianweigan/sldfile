using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Per-field evidence flags for ShellFeatureModel.
/// </summary>
public sealed class ShellFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Wall thickness was decoded from the feature stream.</summary>
    public bool Thickness { get; internal set; }

    /// <summary>Shell direction was decoded from the feature stream.</summary>
    public bool Direction { get; internal set; }

    /// <summary>Ordered removed-face references were decoded.</summary>
    public bool FacesRemoved { get; internal set; }

    /// <summary>Ordered multiple-thickness face references were decoded.</summary>
    public bool MultipleThicknessFaces { get; internal set; }

    /// <summary>Per-face multiple-thickness values were decoded.</summary>
    public bool MultipleThicknessValues { get; internal set; }
}

/// <summary>
/// Per-field evidence flags for DraftFeatureModel.
/// </summary>
public sealed class DraftFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Draft type (swDraftType_e) was decoded.</summary>
    public bool Type { get; internal set; }

    /// <summary>Draft angle was decoded.</summary>
    public bool Angle { get; internal set; }

    /// <summary>Direction reversal was decoded.</summary>
    public bool ReverseDirection { get; internal set; }

    /// <summary>Reduced-angle allowance was decoded.</summary>
    public bool AllowReducedAngle { get; internal set; }

    /// <summary>Neutral plane reference was decoded.</summary>
    public bool NeutralPlane { get; internal set; }

    /// <summary>Pull direction reference was decoded.</summary>
    public bool DirectionPull { get; internal set; }

    /// <summary>Ordered faces-to-draft references were decoded.</summary>
    public bool FacesToDraft { get; internal set; }

    /// <summary>Ordered parting-line references were decoded.</summary>
    public bool PartingLines { get; internal set; }

    /// <summary>Face propagation type was decoded.</summary>
    public bool FacePropagation { get; internal set; }

    /// <summary>Step draft type was decoded.</summary>
    public bool StepType { get; internal set; }
}

/// <summary>
/// Per-field evidence flags for RibFeatureModel.
/// </summary>
public sealed class RibFeatureParameterResolution
{
    /// <summary>Serialized MFC class name observed in a sample.</summary>
    public bool ClassName { get; internal set; }

    /// <summary>Folder-header type code was read from a sample.</summary>
    public bool SerializedTypeId { get; internal set; }

    /// <summary>Rib thickness was decoded.</summary>
    public bool Thickness { get; internal set; }

    /// <summary>Rib type (swRibType_e) was decoded.</summary>
    public bool Type { get; internal set; }

    /// <summary>Extrusion direction (swRibExtrusionDirection_e) was decoded.</summary>
    public bool ExtrusionDirection { get; internal set; }

    /// <summary>Two-sided flag was decoded.</summary>
    public bool IsTwoSided { get; internal set; }

    /// <summary>Reverse thickness direction was decoded.</summary>
    public bool ReverseThicknessDir { get; internal set; }

    /// <summary>Flip side flag was decoded.</summary>
    public bool FlipSide { get; internal set; }

    /// <summary>Draft enable flag was decoded.</summary>
    public bool EnableDraft { get; internal set; }

    /// <summary>Draft angle was decoded.</summary>
    public bool DraftAngle { get; internal set; }

    /// <summary>Draft outward flag was decoded.</summary>
    public bool DraftOutward { get; internal set; }

    /// <summary>Draft-from-wall flag was decoded.</summary>
    public bool DraftFromWall { get; internal set; }

    /// <summary>Target body reference was decoded.</summary>
    public bool Body { get; internal set; }

    /// <summary>Reference sketch index was decoded.</summary>
    public bool RefSketchIndex { get; internal set; }
}
