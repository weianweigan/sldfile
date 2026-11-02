using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Writable edge-flange combinations, based on the measured single-edge specimen
/// (FlangeOptions/edge_flange_90_len10.SLDPRT, version 0x74) and the five
/// single-variable variants.
/// <para>
/// The complete native edge-flange subsystem (selection block, moEdgeFlange_c,
/// moOneBend_c, dimensions, per-edge/linear-edge data and the downstream sheet
/// metal objects) is stored as a hash-pinned measured suffix. Authoring is
/// restricted to the exact stripped prefix (SHA-256), one edge, BendAngle/
/// BendRadius, and the verified OffsetDistance, PositionType (1/2/3) and relief
/// width/depth combinations. Every other field must be null (unresolved/use
/// template) or equal the measured native default from create4.json/create12.json;
/// anything else is rejected before the stream is touched. Derived geometry and
/// display caches are preserved as lazy values and must be recomputed by the
/// native rebuild.
/// </para>
/// </summary>
internal static class EdgeFlangeFeatureBuilder
{
    internal const string DefaultFeatureName = "EdgeFlange";
    internal const ushort SupportedStreamVersion = 0x74;

    internal static void ValidateDefinition(EdgeFlangeFeatureDefinitionData def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.BendAngle.HasValue ||
            double.IsNaN(def.BendAngle.Value) || double.IsInfinity(def.BendAngle.Value) ||
            def.BendAngle.Value <= 0.0 || def.BendAngle.Value > Math.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(def.BendAngle),
                $"Edge flange bend angle must be a finite value in (0, pi] radians, but received " +
                $"{def.BendAngle?.ToString() ?? "null"}.");
        }

        if (!def.BendRadius.HasValue ||
            double.IsNaN(def.BendRadius.Value) || double.IsInfinity(def.BendRadius.Value) ||
            def.BendRadius.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.BendRadius),
                $"Edge flange bend radius must be a positive finite number, but received " +
                $"{def.BendRadius?.ToString() ?? "null"}.");
        }

        // Verified variant fields: OffsetDistance from the length-20 sample,
        // PositionType 1/2/3 from the position samples, and relief width/depth
        // from relief_custom. Relief width and depth were only varied together.
        ValidatePositiveOptional(def.OffsetDistance, nameof(def.OffsetDistance));
        if (def.PositionType.HasValue &&
            (def.PositionType.Value < 1 || def.PositionType.Value > 3))
        {
            throw new ArgumentOutOfRangeException(nameof(def.PositionType),
                $"Edge flange position type must be 1 (MaterialInside), 2 (MaterialOutside) " +
                $"or 3 (BendOutside), but received {def.PositionType.Value}.");
        }
        if (def.ReliefWidth.HasValue != def.ReliefDepth.HasValue)
        {
            throw new NotSupportedException(
                "Edge flange authoring supports relief width/depth only as the verified pair " +
                "from edge_flange_relief_custom; setting only one of them is not verified. " +
                "No stream changes have been made.");
        }
        ValidatePositiveOptional(def.ReliefWidth, nameof(def.ReliefWidth));
        ValidatePositiveOptional(def.ReliefDepth, nameof(def.ReliefDepth));

        RequireNullOrEquals(def.EdgeCount, 1, nameof(def.EdgeCount));

        // Persistent selections are not authorable in this combination.
        RequireNull(def.Edge, nameof(def.Edge));
        RequireNull(def.Edges, nameof(def.Edges));
        RequireNull(def.OffsetReference, nameof(def.OffsetReference));
        RequireNull(def.PositionOffsetReference, nameof(def.PositionOffsetReference));
        RequireNull(def.AngleReference, nameof(def.AngleReference));
        RequireNull(def.CustomBendAllowance, nameof(def.CustomBendAllowance));

        // Measured native defaults from create4.json targetDefinition.
        RequireNullOrEquals(def.UseDefaultBendRadius, false, nameof(def.UseDefaultBendRadius));
        RequireNullOrEquals(def.OffsetType, 1, nameof(def.OffsetType));
        RequireNullOrEquals(def.PositionOffsetType, 1, nameof(def.PositionOffsetType));

        if (def.UsePositionOffset == true)
        {
            // Measured position-offset templates: PositionType stays 1, the
            // distance is exactly the two native samples 0.005/0.010, and the
            // fixed offset length remains 0.010.
            RequireNullOrEquals(def.PositionType, 1, nameof(def.PositionType));
            if (!def.PositionOffsetDistance.HasValue)
            {
                throw new NotSupportedException(
                    "Edge flange position-offset authoring requires PositionOffsetDistance=0.005 or 0.010. " +
                    "No stream changes have been made.");
            }
            if (!EdgeFlangeOffsetAuthoringSegments.IsSupportedDistance(def.PositionOffsetDistance.Value))
            {
                throw new NotSupportedException(
                    $"Edge flange position-offset authoring supports only PositionOffsetDistance " +
                    $"0.005 or 0.010, but received {def.PositionOffsetDistance.Value:R}. " +
                    "No stream changes have been made.");
            }
        }
        else
        {
            RequireNullOrEquals(def.UsePositionOffset, false, nameof(def.UsePositionOffset));
            RequireNullOrEquals(def.PositionOffsetDistance, 0.01, nameof(def.PositionOffsetDistance));
        }

        RequireNullOrEquals(def.UseDefaultBendAllowance, true, nameof(def.UseDefaultBendAllowance));
        RequireNullOrEquals(def.UseDefaultBendRelief, false, nameof(def.UseDefaultBendRelief));
        RequireNullOrEquals(def.AutoReliefType, 1, nameof(def.AutoReliefType));
        RequireNullOrEquals(def.ReliefTearType, 1, nameof(def.ReliefTearType));
        RequireNullOrEquals(def.ReliefRatio, 0.5, nameof(def.ReliefRatio));
        RequireNullOrEquals(def.UseReliefRatio, true, nameof(def.UseReliefRatio));
        RequireNullOrEquals(def.GapDistance, 0.001, nameof(def.GapDistance));
        RequireNullOrEquals(def.LockAngle, true, nameof(def.LockAngle));
        RequireNullOrEquals(def.NormalToFlangePlane, true, nameof(def.NormalToFlangePlane));
        RequireNullOrEquals(def.PerpendicularToFace, false, nameof(def.PerpendicularToFace));
        RequireNullOrEquals(def.ReverseOffset, false, nameof(def.ReverseOffset));
        RequireNullOrEquals(def.UsePositionTrimSideBends, false, nameof(def.UsePositionTrimSideBends));

        // Fields not present in the native snapshot stay unresolved: only null is accepted.
        RequireNull(def.OffsetDimType, nameof(def.OffsetDimType));
        RequireNull(def.ReversePositionOffset, nameof(def.ReversePositionOffset));
    }

    internal static byte[] Build(
        byte[] currentStream, EdgeFlangeFeatureDefinitionData def, string featureName)
    {
        ValidateDefinition(def);

        if (def.UsePositionOffset == true)
            return BuildPositionOffset(currentStream, def, featureName);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Edge flange authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; changing the name would change the measured block layout. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Edge flange authoring is only supported for ResolvedFeatures streams version " +
                $"0x{SupportedStreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }

        if (currentStream.Length != EdgeFlangeAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Edge flange authoring requires the stripped sketch+base-flange prefix of the controlled " +
                $"specimen ({EdgeFlangeAuthoringSegments.MeasuredPrefixLength} bytes), but received " +
                $"{currentStream.Length} bytes. No stream changes have been made.");
        }

        string prefixHash = Sha256Hex(currentStream);
        if (!string.Equals(prefixHash, EdgeFlangeAuthoringSegments.MeasuredPrefixSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Edge flange authoring requires the controlled base/edge specimen: prefix SHA-256 " +
                $"{prefixHash.Substring(0, 12)} does not match the measured signature. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moEdgeFlange_c") != 0)
        {
            throw new NotSupportedException(
                "Edge flange authoring requires a stripped baseline without an existing edge flange. " +
                "No stream changes have been made.");
        }

        byte[] suffix = EdgeFlangeAuthoringSegments.GetSuffix();
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);

        int suffixStart = currentStream.Length;
        foreach (int slot in EdgeFlangeAuthoringSegments.BendAngleSlots)
        {
            PatchDouble(newStream, suffixStart + slot,
                EdgeFlangeAuthoringSegments.MeasuredBendAngle, def.BendAngle!.Value, "bend angle");
        }
        foreach (int slot in EdgeFlangeAuthoringSegments.BendRadiusSlots)
        {
            PatchDouble(newStream, suffixStart + slot,
                EdgeFlangeAuthoringSegments.MeasuredBendRadius, def.BendRadius!.Value, "bend radius");
        }

        if (def.OffsetDistance.HasValue)
        {
            foreach (int slot in EdgeFlangeAuthoringSegments.PrefixOffsetDistanceSlots)
            {
                PatchDouble(newStream, slot,
                    EdgeFlangeAuthoringSegments.MeasuredOffsetDistance,
                    def.OffsetDistance.Value, "offset distance");
            }
            foreach (int slot in EdgeFlangeAuthoringSegments.OffsetDistanceSlots)
            {
                PatchDouble(newStream, suffixStart + slot,
                    EdgeFlangeAuthoringSegments.MeasuredOffsetDistance,
                    def.OffsetDistance.Value, "offset distance");
            }
        }

        if (def.PositionType.HasValue)
        {
            PatchUInt32(newStream, suffixStart + EdgeFlangeAuthoringSegments.PositionTypeSlot,
                EdgeFlangeAuthoringSegments.MeasuredPositionType,
                checked((uint)def.PositionType.Value), "position type");
        }

        if (def.ReliefWidth.HasValue && def.ReliefDepth.HasValue)
        {
            PatchDouble(newStream, suffixStart + EdgeFlangeAuthoringSegments.ReliefWidthSlot,
                EdgeFlangeAuthoringSegments.MeasuredReliefWidth,
                def.ReliefWidth.Value, "relief width");
            PatchDouble(newStream, suffixStart + EdgeFlangeAuthoringSegments.ReliefDepthSlot,
                EdgeFlangeAuthoringSegments.MeasuredReliefDepth,
                def.ReliefDepth.Value, "relief depth");
            foreach (int slot in EdgeFlangeAuthoringSegments.ReliefDepthDisplaySlots)
            {
                PatchDouble(newStream, suffixStart + slot,
                    EdgeFlangeAuthoringSegments.MeasuredReliefDepth,
                    def.ReliefDepth.Value, "relief depth display");
            }
        }

        // Restore the measured folder count and sketch child count. The stripped
        // baseline may keep or reset them; the authored stream must match the
        // native full sample because prefix + measured suffix reconstructs it.
        BitConverter.GetBytes(EdgeFlangeAuthoringSegments.MeasuredFullFolderCount).CopyTo(newStream, 4);
        BitConverter.GetBytes(EdgeFlangeAuthoringSegments.MeasuredSketchChildCount)
            .CopyTo(newStream, EdgeFlangeAuthoringSegments.SketchChildCountMarkerOffset + 4);

        return newStream;
    }

    private static byte[] BuildPositionOffset(
        byte[] currentStream, EdgeFlangeFeatureDefinitionData def, string featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        if (!string.IsNullOrEmpty(featureName) &&
            !string.Equals(featureName, DefaultFeatureName, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Edge flange authoring currently supports only the measured display name " +
                $"'{DefaultFeatureName}'; changing the name would change the measured block layout. " +
                "No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != EdgeFlangeOffsetAuthoringSegments.SupportedStreamVersion)
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring is only supported for ResolvedFeatures streams " +
                $"version 0x{EdgeFlangeOffsetAuthoringSegments.SupportedStreamVersion:X2}, but the document " +
                $"uses 0x{version:X2}. No stream changes have been made.");
        }

        if (currentStream.Length != EdgeFlangeOffsetAuthoringSegments.MeasuredPrefixLength)
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring requires the stripped base prefix of the controlled " +
                $"position-offset specimen ({EdgeFlangeOffsetAuthoringSegments.MeasuredPrefixLength} bytes), " +
                $"but received {currentStream.Length} bytes. No stream changes have been made.");
        }

        double distance = def.PositionOffsetDistance!.Value;
        string prefixHash = Sha256Hex(currentStream);
        string expectedPrefix = EdgeFlangeOffsetAuthoringSegments.GetPrefixSha256(distance);
        if (!string.Equals(prefixHash, expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring for PositionOffsetDistance={distance:R} requires " +
                $"the matching controlled stripped base specimen: prefix SHA-256 " +
                $"{prefixHash.Substring(0, 12)} does not match {expectedPrefix.Substring(0, 12)}. " +
                "No stream changes have been made.");
        }

        if (BitConverter.ToUInt16(currentStream, 4) !=
            EdgeFlangeOffsetAuthoringSegments.MeasuredFullFolderCount)
        {
            throw new NotSupportedException(
                $"Edge flange position-offset authoring requires the measured folder count " +
                $"{EdgeFlangeOffsetAuthoringSegments.MeasuredFullFolderCount}. " +
                "No stream changes have been made.");
        }

        if (CountOccurrences(currentStream, "moEdgeFlange_c") != 0)
        {
            throw new NotSupportedException(
                "Edge flange authoring requires a stripped baseline without an existing edge flange. " +
                "No stream changes have been made.");
        }

        byte[] suffix = EdgeFlangeOffsetAuthoringSegments.GetSuffix(distance);
        byte[] newStream = new byte[currentStream.Length + suffix.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        Array.Copy(suffix, 0, newStream, currentStream.Length, suffix.Length);
        return newStream;
    }

    private static void PatchDouble(byte[] data, int offset, double expected, double value, string slotName)
    {
        if (offset < 0 || offset + 8 > data.Length)
            throw new InvalidOperationException($"Edge-flange slot {slotName} at 0x{offset:X} is outside the stream.");
        double current = BitConverter.ToDouble(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Cannot author an edge flange: measured {slotName} slot at 0x{offset:X} contains " +
                $"{current:R}, expected {expected:R}. The target is not the controlled specimen. " +
                "No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void PatchUInt32(byte[] data, int offset, uint expected, uint value, string slotName)
    {
        if (offset < 0 || offset + 4 > data.Length)
            throw new InvalidOperationException($"Edge-flange slot {slotName} at 0x{offset:X} is outside the stream.");
        uint current = BitConverter.ToUInt32(data, offset);
        if (current != expected)
        {
            throw new NotSupportedException(
                $"Cannot author an edge flange: measured {slotName} slot at 0x{offset:X} contains " +
                $"{current}, expected {expected}. The target is not the controlled specimen. " +
                "No stream changes have been made.");
        }
        BitConverter.GetBytes(value).CopyTo(data, offset);
    }

    private static void ValidatePositiveOptional(double? value, string property)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(property,
                $"Edge flange {property} must be a positive finite number, but received " +
                $"{value.Value:R}.");
        }
    }

    private static void RequireNullOrEquals<T>(T? value, T measured, string property) where T : struct
    {
        if (value.HasValue && !EqualityComparer<T>.Default.Equals(value.Value, measured))
        {
            throw new NotSupportedException(
                $"Edge flange authoring does not yet support {property}={value.Value}. " +
                $"The measured template default is {measured}. No stream changes have been made.");
        }
    }

    private static void RequireNull(object? value, string property)
    {
        if (value != null)
        {
            throw new NotSupportedException(
                $"Edge flange authoring does not yet support {property}. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", string.Empty);
    }

    private static int CountOccurrences(byte[] data, string text)
    {
        byte[] pattern = System.Text.Encoding.ASCII.GetBytes(text);
        int count = 0;
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j]) { match = false; break; }
            }
            if (match)
            {
                count++;
                i += pattern.Length - 1;
            }
        }
        return count;
    }
}
