namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Path Mate (moMatePath_c, TypeId 15).
/// </summary>
public sealed class PathMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Path;
    public double PathValue { get; set; }
    public bool IsFreeAlongPath => PathValue == 0;
    public string ComponentPointEntity { get; set; } = string.Empty;
    public string PathCurveEntity { get; set; } = string.Empty;
}
