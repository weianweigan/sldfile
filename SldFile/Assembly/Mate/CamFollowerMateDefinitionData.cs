using System.Collections.Generic;

namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Cam Follower Mate (moMateCamTangent / moMateCamCoincident, TypeId 91).
/// </summary>
public sealed class CamFollowerMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Cam;
    public bool IsTangent { get; set; } = true;
    public string FollowerEntity { get; set; } = string.Empty;
    public List<string> CamProfileFaces { get; } = new();
}
