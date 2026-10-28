namespace SldFile.Sketches;

/// <summary>A segment references entries in the owning sketch's point table; missing references remain null.</summary>
public sealed class SketchSegment
{
    public uint Id { get; internal set; }
    public SketchSegmentKind Kind { get; internal set; }
    public bool IsConstruction { get; internal set; }
    public SketchPoint? StartPoint { get; internal set; }
    public SketchPoint? EndPoint { get; internal set; }
    public SketchPoint? CenterPoint { get; internal set; }
    public SketchPoint? MajorPoint { get; internal set; }
    public SketchPoint? MinorPoint { get; internal set; }
    /// <summary>Computed from decoded center and start points; not a separately stored driver parameter.</summary>
    public double? Radius { get; internal set; }
    public bool? IsCircle { get; internal set; }
    public int? Direction { get; internal set; }
    public string? Text { get; internal set; }
    public int SourceOffset { get; internal set; }
}
