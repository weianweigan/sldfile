namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for standard geometric mates (Coincident, Concentric, Distance, Parallel, etc.).
/// </summary>
public sealed class StandardMateDefinitionData : AssemblyMateDefinitionData
{
    private readonly AssemblyMateType _mateType;
    public override AssemblyMateType MateType => _mateType;
    public double? Distance { get; set; }
    public double? Angle { get; set; }

    public StandardMateDefinitionData(AssemblyMateType mateType)
    {
        _mateType = mateType;
    }
}
