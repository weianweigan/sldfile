using System.Collections.Generic;

namespace SldFile.Assembly.Mate;

/// <summary>
/// Public definition data for Width Mate (moMateWidth, TypeId 100).
/// </summary>
public sealed class WidthMateDefinitionData : AssemblyMateDefinitionData
{
    public override AssemblyMateType MateType => AssemblyMateType.Width;
    public AssemblyMateWidthConstraint ConstraintType { get; set; } = AssemblyMateWidthConstraint.Centered;
    public double DistanceFromEnd { get; set; }
    public double PercentDistanceFromEnd { get; set; }
    public bool FlipDimension { get; set; }
    public List<string> WidthEntities { get; } = new(2);
    public List<string> TabEntities { get; } = new(2);
}
