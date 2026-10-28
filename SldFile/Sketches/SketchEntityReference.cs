namespace SldFile.Sketches;

/// <summary>
/// MFC handle reference to a sketch point or segment entity.
/// ClassReference is archive-local; Index maps to the local entity table.
/// </summary>
public sealed class SketchEntityReference
{
    public ushort ClassReference { get; internal set; }
    public ushort Index { get; internal set; }
    public int SourceOffset { get; internal set; }

    /// <summary>
    /// The resolved target entity instance (<see cref="SketchPoint"/> or <see cref="SketchSegment"/>),
    /// or null if unresolved.
    /// </summary>
    public object? Target { get; internal set; }

    /// <summary>Owning sketch feature context.</summary>
    public SketchFeature? OwnerSketch { get; internal set; }

    /// <summary>Convenience accessor: returns <see cref="Target"/> cast to <see cref="SketchPoint"/>, or null.</summary>
    public SketchPoint? AsPoint() => Target as SketchPoint;

    /// <summary>Convenience accessor: returns <see cref="Target"/> cast to <see cref="SketchSegment"/>, or null.</summary>
    public SketchSegment? AsSegment() => Target as SketchSegment;

    /// <summary>
    /// Explicitly resolves the target against the given sketch feature context (or <see cref="OwnerSketch"/>).
    /// </summary>
    public object? ResolveTarget(SketchFeature? sketch = null)
    {
        var ctx = sketch ?? OwnerSketch;
        if (ctx == null) return Target;

        // If Target is already resolved, return it
        if (Target != null) return Target;

        // Try resolving by Index against Points or Segments
        // In MFC CArchive relation streams:
        // When ClassReference represents points (e.g. 0x80AD), Index maps to Points[Index].
        // When ClassReference represents segments (e.g. 0x80BD, 0x80C0, 0x80C1), Index maps to Segments[Index].
        if (Index < ctx.Points.Count)
        {
            var pt = ctx.Points[Index];
            if (pt.TableIndex == Index)
            {
                // Likely a point
                Target = pt;
                return Target;
            }
        }

        if (Index < ctx.Segments.Count)
        {
            Target = ctx.Segments[Index];
            return Target;
        }

        return null;
    }
}
