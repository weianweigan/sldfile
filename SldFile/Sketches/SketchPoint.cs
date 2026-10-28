namespace SldFile.Sketches;

public sealed class SketchPoint
{
    /// <summary>Identifier stored in the sketch point record, distinct from its zero-based table position.</summary>
    public uint Id { get; internal set; }
    public int TableIndex { get; internal set; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double Z { get; internal set; }
    public int SourceOffset { get; internal set; }
}
