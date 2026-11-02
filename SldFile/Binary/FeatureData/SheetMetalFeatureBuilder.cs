using System;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Validation and (future) stream emitter for sheet metal base flanges.
/// Reading is established for thickness, radius, depth, direction 2, reverse flags,
/// K-factor, relief ratio/type and the template dimension records. The write side is
/// deliberately not enabled yet: no controlled sample proves the insertion ordering,
/// compact class references, flat-pattern template and per-body sheet metal blocks
/// required for a complete synthesized feature. Rejecting the operation here is
/// preferable to emitting a partially understood template that SolidWorks would
/// silently repair or discard.
/// </summary>
internal static class SheetMetalFeatureBuilder
{
    /// <summary>
    /// Validates a definition for authoring. Values that have no evidenced
    /// stream layout are rejected instead of being silently dropped.
    /// </summary>
    internal static void ValidateDefinition(SheetMetalBaseFlangeDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (double.IsNaN(def.Thickness) || double.IsInfinity(def.Thickness) || def.Thickness <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Thickness),
                $"Sheet metal thickness must be a positive finite number, but received {def.Thickness}.");
        }

        if (double.IsNaN(def.BendRadius) || double.IsInfinity(def.BendRadius) || def.BendRadius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.BendRadius),
                $"Bend radius must be a positive finite number, but received {def.BendRadius}.");
        }

        if (double.IsNaN(def.Depth) || double.IsInfinity(def.Depth) || def.Depth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Depth),
                $"Base flange depth must be a positive finite number, but received {def.Depth}.");
        }

        // Authoring still accepts only the untouched option set. Several options
        // are now decoded for reading (K, allowance type/value, relief ratio/type
        // and default flag, gauge table, reverse flags, direction 2), but none has
        // a proven write/insertion layout yet.
        RejectUnsupported(def.ReverseThickness, nameof(def.ReverseThickness));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.DepthDirection2 != 0, nameof(def.DepthDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(!def.UseDefaultBendRadius, nameof(def.UseDefaultBendRadius));
        RejectUnsupported(def.KFactor != 0.5, nameof(def.KFactor));
        RejectUnsupported(def.BendAllowanceType != 0, nameof(def.BendAllowanceType));
        RejectUnsupported(def.BendAllowance.HasValue, nameof(def.BendAllowance));
        RejectUnsupported(def.BendDeduction.HasValue, nameof(def.BendDeduction));
        // 0 is the unresolved/model sentinel, 1 is the evidenced Rectangular default.
        RejectUnsupported(def.AutoReliefType > 1, nameof(def.AutoReliefType));
        RejectUnsupported(def.ReliefRatio != 0.5, nameof(def.ReliefRatio));
        RejectUnsupported(def.ReliefWidth != 0, nameof(def.ReliefWidth));
        RejectUnsupported(def.ReliefDepth != 0, nameof(def.ReliefDepth));
        RejectUnsupported(!def.UseDefaultBendRelief, nameof(def.UseDefaultBendRelief));
        RejectUnsupported(def.UseGaugeTable, nameof(def.UseGaugeTable));
        RejectUnsupported(!string.IsNullOrEmpty(def.GaugeTablePath), nameof(def.GaugeTablePath));
        RejectUnsupported(!string.IsNullOrEmpty(def.ThicknessTableName), nameof(def.ThicknessTableName));
        RejectUnsupported(!string.IsNullOrEmpty(def.BendTableFile), nameof(def.BendTableFile));

        RejectUnsupported(def.SelectedContours != null && def.SelectedContours.Count > 0,
            nameof(def.SelectedContours));
        RejectUnsupported(def.SelectedRegions != null && def.SelectedRegions.Count > 0, nameof(def.SelectedRegions));
        RejectUnsupported(def.DirectionReference != null, nameof(def.DirectionReference));
        RejectUnsupported(def.BodyScope != null && (!def.BodyScope.AutoSelect || def.BodyScope.Flags != 2 ||
            (def.BodyScope.BodyIds != null && def.BodyScope.BodyIds.Count > 0) ||
            (def.BodyScope.Bodies != null && def.BodyScope.Bodies.Count > 0)), nameof(def.BodyScope));
    }

    /// <summary>
    /// Builds the modified ResolvedFeatures stream. The first writable combination
    /// is the measured open-L specimen: thickness, bend radius and depth are
    /// replaced in the hash-pinned measured subsystem; every other option and any
    /// uncontrolled sketch/version is rejected before the stream is touched.
    /// </summary>
    internal static byte[] BuildBaseFlangeStream(
        byte[] currentStream,
        SheetMetalBaseFlangeDefinitionData def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);
        return SheetMetalBaseFlangeBuilder.Build(
            currentStream, def, targetSketchFolder, sketchEndOffset, featureName);
    }

    private static void RejectUnsupported(bool unsupported, string property)
    {
        if (unsupported)
            throw new NotSupportedException(
                $"Sheet metal base flange authoring does not yet support {property}. No stream changes have been made.");
    }
}
