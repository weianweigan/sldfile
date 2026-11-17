namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Gear Mate (moMateGearDim_c, TypeId 96).
/// </summary>
public sealed class GearMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Gear;
    public double GearRatioNumerator { get; set; } = 1.0;
    public double GearRatioDenominator { get; set; } = 1.0;
    public bool Reverse { get; set; }

    public string Pinion1Entity
    {
        get => EntitiesToMate.Count > 0 ? EntitiesToMate[0] : string.Empty;
        set { EnsureEntities(1); EntitiesToMate[0] = value; }
    }

    public string Pinion2Entity
    {
        get => EntitiesToMate.Count > 1 ? EntitiesToMate[1] : string.Empty;
        set { EnsureEntities(2); EntitiesToMate[1] = value; }
    }

    private void EnsureEntities(int count)
    {
        while (EntitiesToMate.Count < count) EntitiesToMate.Add(string.Empty);
    }
}
