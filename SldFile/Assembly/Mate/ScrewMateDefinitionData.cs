namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Screw Mate (moMateScrew_c, TypeId 97).
/// </summary>
public sealed class ScrewMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Screw;
    public int RevolutionType { get; set; }
    public double RevolutionVal { get; set; } = 0.005;
    public bool Reverse { get; set; }
    public string ScrewEntity1 { get; set; } = string.Empty;
    public string ScrewEntity2 { get; set; } = string.Empty;
}
