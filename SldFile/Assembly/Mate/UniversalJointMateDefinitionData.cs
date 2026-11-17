namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Universal Joint Mate (moMateUniversalJoint_c, TypeId 98).
/// </summary>
public sealed class UniversalJointMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.UniversalJoint;
    public string Shaft1Entity { get; set; } = string.Empty;
    public string Shaft2Entity { get; set; } = string.Empty;
    public bool DefineJointPoint { get; set; }
    public string JointPointEntity { get; set; } = string.Empty;
}
