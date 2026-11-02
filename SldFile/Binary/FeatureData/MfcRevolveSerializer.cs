using System;
using SldFile.Binary;

namespace SldFile.Binary.FeatureData;

/// <summary>
/// Serializes a single-direction Blind solid revolution (moRevolution_c) from the measured
/// RevolveOptions layouts. The emitted feature block follows the native partial/full element
/// sequences; the angle, reverse flag and surface references are written structurally. The
/// sketch/feature persistent serials come from the target base's moCompFeature_c records:
/// CString surface slots and partial FromSkt use the feature serial, CString offset +19 uses
/// sketchSerial-1, and moCompSketchEntHandle_c uses the sketch serial.
/// </summary>
internal static class MfcRevolveSerializer
{
    internal const double FullRevolution = 2.0 * Math.PI;

    // Controlled profile of the verified sketch (metres): rectangle 5..15 mm from the axis, 10 mm high,
    // revolved around the sketch construction line on the Y axis.
    private const double InnerRadius = 0.005;
    private const double OuterRadius = 0.015;
    private const double ProfileHeight = 0.010;

    internal static bool IsFullRevolution(double angle) =>
        Math.Abs(angle - FullRevolution) <= 1e-12;

    public static byte[] SerializeRevolve(
        RevolveFeatureDefinitionData def,
        string featureName,
        uint sketchSerial,
        uint featureSerial,
        uint featureId)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "旋转1";
        if (sketchSerial == 0 || featureSerial == 0)
            throw new InvalidOperationException("The controlled sketch/feature serials must be non-zero.");

        bool full = IsFullRevolution(def.Angle);
        using var writer = new MfcStreamWriter(4096);

        // 1. Folder header: fixed prefix written structurally, then the measured revolve suffix.
        writer.WriteMfcTag("moRevolution_c");
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(0x4E);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);

        var header = Clone(RevolveSegments.HeaderSuffix);
        // Display cache: the 180/360-degree controls store zero; the measured 90-degree control
        // stores a different derived value. Rebuild is expected to recompute it.
        WriteF64At(header, RevolveSegments.HeaderSuffixBBoxXOffset, 0.0);
        WriteU32At(header, RevolveSegments.HeaderSuffixFeatureIdOffset, featureId);
        writer.WriteBytes(header);

        // 2. Body scope and end-face references.
        writer.WriteMfcTag("moPerBodyChooserData_c");
        writer.WriteBytes(RevolveSegments.PerBodyChooserData);
        writer.WriteMfcTag("moFaceRef_c");
        writer.WriteBytes(full ? RevolveSegments.FaceRefDataFull : RevolveSegments.FaceRefDataPartial);
        if (!full)
        {
            writer.WriteMfcTag("moEndFaceSurfIdRep_c");
            writer.WriteBytes(RevolveSegments.EndFaceDataPartial);
        }
        else
        {
            // Full layout: the empty sketch-surface mapping precedes FR/ExtObject/CString.
            writer.WriteMfcTag("moFromSktEntSurfIdRep_c");
            writer.WriteBytes(RevolveSegments.FromSktDataFull);
        }
        writer.WriteMfcTag("moFR_c");
        writer.WriteMfcTag("moExtObject_c");

        // 3. Persistent string / sketch-surface reference blocks.
        // The serials come from the target base's moCompFeature_c records: the first
        // record is the sketch surface, the second is the consuming feature surface.
        // Forward 90/180 and full 360 controls use equal serials; the measured reverse
        // control uses sketch+1 for the feature, so both are patched separately.
        writer.WriteMfcTag("moCStringHandle_c");
        var cstring = Clone(full ? RevolveSegments.CStringDataFull : RevolveSegments.CStringDataPartial);
        if (!full && def.ReverseDirection)
            cstring[RevolveSegments.CStringPartialReverseMarkerOffset] =
                RevolveSegments.CStringPartialReverseMarkerValue;
        WriteTagSlots(cstring,
            full ? RevolveSegments.CStringFullSurfaceTagOffsets : RevolveSegments.CStringPartialSurfaceTagOffsets,
            featureSerial);
        WriteTagSlots(cstring,
            full ? RevolveSegments.CStringFullSurfaceTagMinusOneOffsets : RevolveSegments.CStringPartialSurfaceTagMinusOneOffsets,
            sketchSerial - 1);
        writer.WriteBytes(cstring);

        if (!full)
        {
            writer.WriteMfcTag("moFromSktEntSurfIdRep_c");
            var fromSkt = Clone(def.ReverseDirection
                ? RevolveSegments.FromSktDataPartialReverse
                : RevolveSegments.FromSktDataPartialForward);
            WriteTagSlots(fromSkt, RevolveSegments.FromSktSurfaceTagOffsets, featureSerial);
            writer.WriteBytes(fromSkt);
        }
        // For full revolutions the empty moFromSktEntSurfIdRep_c was emitted before FR.

        // 4. Bounding box and construction-axis reference.
        var bbox = Clone(full ? RevolveSegments.BBoxDataFull : RevolveSegments.BBoxDataPartial);
        ApplyBoundingBox(bbox, def.Angle, def.ReverseDirection, full);
        writer.WriteMfcTag("moBBoxCenterData_c");
        writer.WriteBytes(bbox);

        writer.WriteMfcTag("moLineRef_w");
        var compSketch = Clone(RevolveSegments.CompSketchData);
        WriteU32At(compSketch, RevolveSegments.CompSketchSurfaceTagOffset, sketchSerial);
        writer.WriteMfcTag("moCompSketchEntHandle_c");
        writer.WriteBytes(compSketch);

        // 5. Blind single-direction end specification and angular display dimension stack.
        writer.WriteMfcTag("moRevEndSpec_c");
        writer.WriteBytes(RevolveSegments.RevEndSpecData);
        writer.WriteMfcTag("moDisplayAngularDim_c");
        writer.WriteBytes(RevolveSegments.DisplayAngularDimData);
        writer.WriteMfcTag("moFeatureDimHandle_c");
        writer.WriteBytes(RevolveSegments.FeatureDimHandleData);
        writer.WriteMfcTag("AngleDim_c");
        writer.WriteBytes(RevolveSegments.AngleDimData);

        var parameter = Clone(full
            ? RevolveSegments.AngleParamDataFull
            : def.ReverseDirection
                ? RevolveSegments.AngleParamDataPartialReverse
                : RevolveSegments.AngleParamDataPartialForward);
        WriteF64At(parameter, RevolveSegments.AngleParameterFirstSlot, def.Angle);
        WriteF64At(parameter, RevolveSegments.AngleParameterSecondSlot, def.Angle);
        writer.WriteMfcTag("moAngleParameter_c");
        writer.WriteBytes(parameter);

        var favorite = Clone(full
            ? RevolveSegments.FavoriteDataFull
            : def.ReverseDirection
                ? RevolveSegments.FavoriteDataPartialReverse
                : RevolveSegments.FavoriteDataPartialForward);
        writer.WriteMfcTag("moFavoriteHandle_c");
        writer.WriteBytes(favorite);

        return writer.ToArray();
    }

    /// <summary>
    /// Bounding box of the controlled rectangle sector. The profile spans radii 5..15 mm and
    /// height 10 mm; the sweep runs from the sketch plane in the measured negative-Z direction
    /// (positive Z when ReverseDirection is set). Values are written into both measured copies.
    /// </summary>
    private static void ApplyBoundingBox(byte[] bbox, double angle, bool reverse, bool full)
    {
        ComputeExtents(angle, reverse, out double xMin, out double xMax, out double zMin, out double zMax);
        double centerX = (xMin + xMax) / 2.0;
        double centerY = ProfileHeight / 2.0;
        double centerZ = (zMin + zMax) / 2.0;
        double dx = xMax - xMin;
        double dz = zMax - zMin;
        double radius = Math.Sqrt(dx * dx + ProfileHeight * ProfileHeight + dz * dz);

        int[] offsets = full
            ? RevolveSegments.FullBBoxDoubleOffsets
            : RevolveSegments.PartialBBoxDoubleOffsets;
        double[] values = { centerX, centerY, centerZ, radius, centerX, centerY, centerZ, radius };
        for (int i = 0; i < offsets.Length; i++)
            WriteF64At(bbox, offsets[i], values[i]);

        WriteU32At(bbox,
            full ? RevolveSegments.FullBBoxReverseOffset : RevolveSegments.PartialBBoxReverseOffset,
            reverse ? 1u : 0u);
    }

    private static void ComputeExtents(
        double angle, bool reverse,
        out double xMin, out double xMax, out double zMin, out double zMax)
    {
        xMax = OuterRadius;
        if (angle <= Math.PI / 2.0)
            xMin = InnerRadius * Math.Cos(angle);
        else if (angle < Math.PI)
            xMin = OuterRadius * Math.Cos(angle);
        else
            xMin = -OuterRadius;

        if (angle <= Math.PI / 2.0)
            zMin = -OuterRadius * Math.Sin(angle);
        else
            zMin = -OuterRadius;

        if (angle <= Math.PI)
            zMax = 0.0;
        else if (angle < 3.0 * Math.PI / 2.0)
            zMax = -OuterRadius * Math.Sin(angle);
        else
            zMax = OuterRadius;

        if (reverse)
        {
            double flippedMin = -zMax;
            double flippedMax = -zMin;
            zMin = flippedMin;
            zMax = flippedMax;
        }
    }

    private static byte[] Clone(byte[] source) => (byte[])source.Clone();

    private static void WriteTagSlots(byte[] block, int[] offsets, uint value)
    {
        foreach (int offset in offsets)
            WriteU32At(block, offset, value);
    }

    private static void WriteU32At(byte[] block, int offset, uint value)
    {
        if (offset < 0 || offset + 4 > block.Length)
            throw new InvalidOperationException($"Revolve segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }

    private static void WriteF64At(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException($"Revolve segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }
}
