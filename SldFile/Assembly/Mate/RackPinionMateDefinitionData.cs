namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Rack & Pinion Mate (moMateRackPinionDim_c, TypeId 95).
/// </summary>
public sealed class RackPinionMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.RackPinion;
    public AssemblyMateRackPinionDiameterType DiameterType { get; set; } = AssemblyMateRackPinionDiameterType.PinionPitchDiameter;
    public double DiameterValue { get; set; }
    public bool Reverse { get; set; }

    public string RackEntity
    {
        get => EntitiesToMate.Count > 0 ? EntitiesToMate[0] : string.Empty;
        set { EnsureEntities(1); EntitiesToMate[0] = value; }
    }

    public string PinionEntity
    {
        get => EntitiesToMate.Count > 1 ? EntitiesToMate[1] : string.Empty;
        set { EnsureEntities(2); EntitiesToMate[1] = value; }
    }

    private void EnsureEntities(int count)
    {
        while (EntitiesToMate.Count < count) EntitiesToMate.Add(string.Empty);
    }
}
