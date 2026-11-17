namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Profile Center Mate (moMateProfileCenter, TypeId 101).
/// </summary>
public sealed class ProfileCenterMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.ProfileCenter;
    public double OffsetDistance { get; set; }
    public bool FlipDimension { get; set; }
    public bool LockRotation { get; set; }
    public string FirstProfileEntity { get; set; } = string.Empty;
    public string SecondProfileEntity { get; set; } = string.Empty;
}
