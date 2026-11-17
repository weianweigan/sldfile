namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Hinge Mate (moMateHinge, TypeId 94).
/// </summary>
public sealed class HingeMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Hinge;
    public string CoaxialEntity1 { get; set; } = string.Empty;
    public string CoaxialEntity2 { get; set; } = string.Empty;
    public string CoincidentEntity1 { get; set; } = string.Empty;
    public string CoincidentEntity2 { get; set; } = string.Empty;
    public bool AngleSelection { get; set; }
    public double Angle { get; set; }
    public double MinVal { get; set; }
    public double MaxVal { get; set; }
}
