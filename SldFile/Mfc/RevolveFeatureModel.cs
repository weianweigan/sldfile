using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;


/// <summary>
/// Unified strongly-typed model for moRevolution_c revolve feature.
/// Follows the IMfcSerializable contract: symmetrical reading/writing and Clone + Modify.
/// </summary>
public sealed class RevolveFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "moRevolution_c";
    public const uint HeaderTypeId = 0x4Eu;

    public string ClassName => IsThinFeature ? "moRevolutionThin_c" : IsCut ? "moRevCut_c" : "moRevolution_c";
    public string FeatureName { get; set; } = "旋转1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get => FolderTypeId ?? (IsThinFeature ? 0x50u : IsCut ? 0x55u : HeaderTypeId); set => FolderTypeId = value; }
    public double Angle { get; set; } = 2.0 * Math.PI;
    public bool BothDirections { get; set; }
    public double AngleDirection2 { get; set; }
    public uint FeatureId { get; set; } = 1;

    public int Type { get; set; }
    public int? AxisType { get; set; }
    public FeatureReferenceData? AxisReference { get; set; }
    public uint? FolderTypeId { get; set; }
    public bool ReverseDirection { get; set; }
    public bool IsCut { get; set; }
    public bool IsThinFeature { get; set; }
    public uint ThinWallType { get; set; }
    public double Thickness1 { get; set; }
    public double Thickness2 { get; set; }
    public bool Merge { get; set; } = true;
    public string? SketchName { get; set; }

    public double GetRevolutionAngle(bool forward) => forward ? Angle : AngleDirection2;

    public RevolveFeatureModel(double angle, bool reverseDirection) : this()
    {
        Angle = angle;
        ReverseDirection = reverseDirection;
    }

    /// <summary>
    /// 统一尺寸参数驱动层模型 (moAngleParameter_c).
    /// </summary>
    public MoAngleParameterModel Parameter => new MoAngleParameterModel("Angle@" + FeatureName, Angle);

    /// <summary>
    /// 统一尺寸显示几何布局层模型 (moDisplayAngularDim_c).
    /// </summary>
    public MoDisplayAngularDimModel DisplayDimension => new MoDisplayAngularDimModel();

    /// <summary>
    /// 统一尺寸对象模型.
    /// </summary>
    public DimensionModel Dimension => new DimensionModel(Parameter, DisplayDimension);

    /// <summary>
    /// 特征所包含的统一尺寸列表.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<DimensionModel> Dimensions
    {
        get
        {
            var list = new System.Collections.Generic.List<DimensionModel> { Dimension };
            if (BothDirections && AngleDirection2 > 0)
            {
                var p2 = new MoAngleParameterModel("Angle2@" + FeatureName, AngleDirection2);
                var d2 = new MoDisplayAngularDimModel();
                list.Add(new DimensionModel(p2, d2));
            }
            return list.AsReadOnly();
        }
    }

    public OpaqueObject HeaderSuffix { get; set; }

    public RevolveFeatureModel()
    {
        HeaderSuffix = new OpaqueObject(string.Empty, RevolveSegments.HeaderSuffix);
    }

    public RevolveFeatureModel CloneWithAngle(double angle, string? featureName = null, uint? featureId = null)
    {
        return new RevolveFeatureModel
        {
            FeatureName = featureName ?? FeatureName,
            Version = Version,
            TypeId = TypeId,
            Angle = angle,
            FeatureId = featureId ?? FeatureId,
            HeaderSuffix = new OpaqueObject(HeaderSuffix.ClassName, (byte[])HeaderSuffix.RawData.Clone())
        };
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = TypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            double angle = Angle;
            ar.F64(ref angle);

            var headerBytes = (byte[])HeaderSuffix.RawData.Clone();
            if (RevolveSegments.HeaderSuffixBBoxXOffset + 8 <= headerBytes.Length)
                BitConverter.GetBytes(0.0).CopyTo(headerBytes, RevolveSegments.HeaderSuffixBBoxXOffset);
            if (RevolveSegments.HeaderSuffixFeatureIdOffset + 4 <= headerBytes.Length)
                BitConverter.GetBytes(FeatureId).CopyTo(headerBytes, RevolveSegments.HeaderSuffixFeatureIdOffset);

            ar.WriteBytes(headerBytes);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            FeatureName = name ?? string.Empty;

            double v = 0;
            ar.F64(ref v);
            Version = v;

            uint tid = 0;
            ar.U32(ref tid);
            TypeId = tid;

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 8)
            {
                double angle = 0;
                ar.F64(ref angle);
                Angle = angle;
            }
        }
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/MfcRevolveSerializer.cs
// ------------------------------------------------------------------------
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
        RevolveFeatureModel def,
        string featureName,
        uint sketchSerial,
        uint featureSerial,
        uint featureId)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "鏃嬭浆1";
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

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RevolveSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured moRevolution_c sub-object payloads from the controlled RevolveOptions samples
/// (revolve_180 forward/reverse and revolve_360). Option-bearing scalars, surface tags and
/// sketch-selection references are written by MfcRevolveSerializer; these arrays hold only
/// the surrounding measured payload/display metadata. Verified scope: single-direction Blind
/// solid revolution on the controlled rectangle + construction-axis sketch.
/// </summary>
internal static class RevolveSegments
{
    internal const int HeaderSuffixBBoxXOffset = 43;
    internal const int HeaderSuffixFeatureIdOffset = 111;
    internal const int AngleParameterFirstSlot = 32;
    internal const int AngleParameterSecondSlot = 570;
    internal const int CompSketchSurfaceTagOffset = 89;
    internal static readonly int[] CStringPartialSurfaceTagOffsets = { 81 };
    internal static readonly int[] CStringPartialSurfaceTagMinusOneOffsets = { 19 };
    /// <summary>Partial reverse layout differs from the forward control at this archive byte (0x39 vs 0x38).</summary>
    internal const int CStringPartialReverseMarkerOffset = 14;
    internal const byte CStringPartialReverseMarkerValue = 0x39;
    internal static readonly int[] CStringFullSurfaceTagOffsets = { 81, 101, 123, 205, 225, 247 };
    internal static readonly int[] CStringFullSurfaceTagMinusOneOffsets = { 19 };
    internal static readonly int[] FromSktSurfaceTagOffsets = { 10, 30, 50, 70, 152, 176, 196, 216, 236 };
    internal static readonly int[] PartialBBoxDoubleOffsets = { 4, 12, 20, 28, 230, 238, 246, 254 };
    internal static readonly int[] FullBBoxDoubleOffsets = { 4, 12, 20, 28, 222, 230, 238, 246 };
    internal const int PartialBBoxReverseOffset = 366;
    internal const int FullBBoxReverseOffset = 354;

    /// <summary>moRevolution_c data after the fixed prefix (schema/name/double 2.0/TypeId 0x4E/empty WChar). Measured on the 180-degree control; bbox-X and feature id slots are written structurally.</summary>
    internal static readonly byte[] HeaderSuffix = RevolveAuthoringSegments.HeaderSuffix;

    /// <summary>moPerBodyChooserData_c payload (single body, auto select) shared by all controls.</summary>
    internal static readonly byte[] PerBodyChooserData = new byte[] {
        0x02, 0x00
    };

    /// <summary>moFaceRef_c payload for a partial (angle < 2*pi) revolution (180-degree control).</summary>
    internal static readonly byte[] FaceRefDataPartial = new byte[] {
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x7D, 0xC3, 0x94, 0x25, 0xAD, 0x49, 0xB2, 0x54, 0x7D, 0xC3, 0x94, 0x25,
        0xAD, 0x49, 0xB2, 0x54, 0x00, 0x00
    };

    /// <summary>moFaceRef_c payload for a full 2*pi revolution (360-degree control).</summary>
    internal static readonly byte[] FaceRefDataFull = new byte[] {
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00,
        0x3B, 0xD0, 0x00, 0x00, 0x7D, 0xC3, 0x94, 0x25, 0xAD, 0x49, 0xB2, 0x54, 0x7D, 0xC3, 0x94, 0x25,
        0xAD, 0x49, 0xB2, 0x54, 0x00, 0x00
    };

    /// <summary>moEndFaceSurfIdRep_c payload present only for partial revolutions.</summary>
    internal static readonly byte[] EndFaceDataPartial = new byte[] {
        0x00, 0x00
    };

    /// <summary>Empty moFromSktEntSurfIdRep_c payload for a full revolution.</summary>
    internal static readonly byte[] FromSktDataFull = new byte[] {
        0x00, 0x00
    };

    /// <summary>moCStringHandle_c payload for partial revolutions; surface-tag slots are written structurally.</summary>
    internal static readonly byte[] CStringDataPartial = RevolveAuthoringSegments.CStringDataPartial;

    /// <summary>moCStringHandle_c payload for full revolutions; surface-tag slots are written structurally.</summary>
    internal static readonly byte[] CStringDataFull = RevolveAuthoringSegments.CStringDataFull;

    /// <summary>moFromSktEntSurfIdRep_c payload for a forward partial revolution; surface-tag slots are written structurally.</summary>
    internal static readonly byte[] FromSktDataPartialForward = RevolveAuthoringSegments.FromSktDataPartialForward;

    /// <summary>moFromSktEntSurfIdRep_c payload for a reversed partial revolution (entity order follows the measured reverse control).</summary>
    internal static readonly byte[] FromSktDataPartialReverse = RevolveAuthoringSegments.FromSktDataPartialReverse;

    /// <summary>moBBoxCenterData_c payload for partial revolutions; center/radius and reverse slots are written structurally.</summary>
    internal static readonly byte[] BBoxDataPartial = RevolveAuthoringSegments.BBoxDataPartial;

    /// <summary>moBBoxCenterData_c payload for full revolutions; center/radius slots are written structurally.</summary>
    internal static readonly byte[] BBoxDataFull = RevolveAuthoringSegments.BBoxDataFull;

    /// <summary>moCompSketchEntHandle_c payload carrying the construction-axis selection; surface-tag slot at +89.</summary>
    internal static readonly byte[] CompSketchData = RevolveAuthoringSegments.CompSketchData;

    /// <summary>moRevEndSpec_c payload (u32 1 single-direction followed by the measured Blind header).</summary>
    internal static readonly byte[] RevEndSpecData = new byte[] {
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x7B, 0x14, 0xAE, 0x47,
        0xE1, 0x7A, 0x84, 0x3F, 0x7B, 0x14, 0xAE, 0x47, 0xE1, 0x7A, 0x84, 0x3F, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00
    };

    /// <summary>moDisplayAngularDim_c payload shared by all controls.</summary>
    internal static readonly byte[] DisplayAngularDimData = RevolveAuthoringSegments.DisplayAngularDimData;

    /// <summary>moFeatureDimHandle_c payload shared by all controls.</summary>
    internal static readonly byte[] FeatureDimHandleData = RevolveAuthoringSegments.FeatureDimHandleData;

    /// <summary>AngleDim_c payload (u16 2 + padding).</summary>
    internal static readonly byte[] AngleDimData = new byte[] {
        0x02, 0x00, 0x00, 0x00
    };

    /// <summary>moAngleParameter_c payload for a forward partial revolution; angle slots +32 and +570 are written structurally.</summary>
    internal static readonly byte[] AngleParamDataPartialForward = RevolveAuthoringSegments.AngleParamDataPartialForward;

    /// <summary>moAngleParameter_c payload for a reversed partial revolution; angle slots +32 and +570 are written structurally.</summary>
    internal static readonly byte[] AngleParamDataPartialReverse = RevolveAuthoringSegments.AngleParamDataPartialReverse;

    /// <summary>moAngleParameter_c payload for a full revolution; angle slots +32 and +570 are written structurally.</summary>
    internal static readonly byte[] AngleParamDataFull = RevolveAuthoringSegments.AngleParamDataFull;

    /// <summary>moFavoriteHandle_c payload for a forward partial revolution (display metadata).</summary>
    internal static readonly byte[] FavoriteDataPartialForward = RevolveAuthoringSegments.FavoriteDataPartialForward;

    /// <summary>moFavoriteHandle_c payload for a reversed partial revolution (display metadata).</summary>
    internal static readonly byte[] FavoriteDataPartialReverse = RevolveAuthoringSegments.FavoriteDataPartialReverse;

    /// <summary>moFavoriteHandle_c payload for a full revolution (display metadata).</summary>
    internal static readonly byte[] FavoriteDataFull = RevolveAuthoringSegments.FavoriteDataFull;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RevolveFeatureParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict reader for the measured revolution layouts:
/// <list type="bullet">
/// <item><c>moRevolution_c</c> / TypeId 0x4E / stream 0x70: single-direction solid (mode 1, D1 angle)
/// and both-directions solid (mode 0, D1 + D2 angles, Type=2).</item>
/// <item><c>moRevolutionThin_c</c> / TypeId 0x50 / stream 0x70: thin one-direction (empty
/// moThinSpec_c payload, D1 angle, D3/D4 wall thicknesses).</item>
/// <item><c>moRevCut_c</c> / TypeId 0x55 / stream 0x71: cut full revolution (D1 angle, IsCut).</item>
/// </list>
/// Unknown layouts return null; nothing is inferred from an approximate value or a default.
/// </summary>
internal static class RevolveFeatureParser
{
    private const string SolidClass = "moRevolution_c";
    private const string ThinClass = "moRevolutionThin_c";
    private const string CutClass = "moRevCut_c";
    private const uint SolidTypeId = 0x4E;
    private const uint ThinTypeId = 0x50;
    private const uint CutTypeId = 0x55;

    internal static RevolveFeatureModel? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreate(doc, className, typeId, start, end);
        if (ctx == null) return null;

        if (className == SolidClass && typeId == CutTypeId && ctx.StreamVersion == 0x71)
            return ParseAxisEdge(ctx);

        bool solid = className == SolidClass && typeId == SolidTypeId;
        bool thin = className == ThinClass && typeId == ThinTypeId;
        bool cut = className == CutClass && typeId == CutTypeId;
        if (!solid && !thin && !cut) return null;
        if (cut ? ctx.StreamVersion != 0x71 : ctx.StreamVersion != 0x70) return null;

        var specNode = ctx.GetSingleNode("moRevEndSpec_c");
        var angleNode = ctx.GetSingleNode("moAngleParameter_c");
        var axisNode = ctx.GetSingleNode("moLineRef_w");
        if (specNode == null || angleNode == null || axisNode == null) return null;

        var source = ctx.RawData;
        int spec = specNode.DataStart;
        if (spec < 0 || spec + 28 > ctx.End) return null;
        uint mode = BitConverter.ToUInt32(source, spec);
        if (mode > 1) return null;
        for (int i = spec + 4; i < spec + 28; i++)
            if (source[i] != 0) return null;
        bool both = mode == 0;
        if (both && !solid) return null;

        var nodes = ctx.Nodes;
        int index = IndexOf(nodes, angleNode);
        if (index < 0) return null;
        if (cut)
        {
            if (index < 2 ||
                nodes[index - 1].ClassName != "AngleDim_c" ||
                nodes[index - 2].ClassName != "moDisplayAngularDim_c" ||
                nodes[index - 2].TagOffset <= specNode.TagOffset) return null;
        }
        else
        {
            if (index < 3 ||
                nodes[index - 1].ClassName != "AngleDim_c" ||
                nodes[index - 2].ClassName != "moFeatureDimHandle_c" ||
                nodes[index - 3].ClassName != "moDisplayAngularDim_c" ||
                nodes[index - 3].TagOffset <= specNode.TagOffset) return null;
        }

        var angleValue = MfcParameterReader.ReadNamedDouble(source, angleNode.DataStart, requireDNumberFormat: true);
        if (!angleValue.HasValue || !IsAngle(angleValue.Value)) return null;

        var d1 = ctx.Parameters.FindAll("D1");
        var d2 = ctx.Parameters.FindAll("D2");
        var d3 = ctx.Parameters.FindAll("D3");
        var d4 = ctx.Parameters.FindAll("D4");
        if (d1.Count != 1 || Math.Abs(d1[0].Value - angleValue.Value) > 1e-12) return null;

        double angleDirection2 = 0;
        if (both)
        {
            if (d2.Count != 1 || !IsAngle(d2[0].Value)) return null;
            angleDirection2 = d2[0].Value;
        }
        else if (d2.Count != 0)
        {
            return null;
        }

        double thickness1 = 0;
        double thickness2 = 0;
        if (thin)
        {
            var thinSpec = ctx.GetSingleNode("moThinSpec_c");
            if (thinSpec == null) return null;
            int thinIndex = IndexOf(nodes, thinSpec);
            if (thinIndex < 0 || thinIndex + 1 >= nodes.Count ||
                nodes[thinIndex + 1].TagOffset != thinSpec.DataStart) return null;
            if (!ctx.HasExactNodeCount("moDisplayDistanceDim_c", 1) ||
                !ctx.HasExactNodeCount("ParallelPlaneDistanceDim_c", 1) ||
                !ctx.HasExactNodeCount("moLengthParameter_c", 1)) return null;
            if (d3.Count != 1 || d4.Count != 1) return null;
            if (!IsPositiveFinite(d3[0].Value) || !IsNonNegativeFinite(d4[0].Value)) return null;
            thickness1 = d3[0].Value;
            thickness2 = d4[0].Value;
        }
        else if (d3.Count != 0 || d4.Count != 0)
        {
            return null;
        }

        int axis = axisNode.TagOffset;
        if (axis - 32 < ctx.Start || axis >= specNode.TagOffset) return null;
        for (int i = axis - 32; i < axis - 19; i++)
            if (source[i] != 0) return null;
        uint reverse = BitConverter.ToUInt32(source, axis - 19);
        ushort expectedAxisTag = cut ? (ushort)0x8065 : (ushort)0x8064;
        uint expectedAxisValue = thin ? 5u : 3u;
        if (reverse > 1 || source[axis - 15] != 0 || source[axis - 14] != 0 || source[axis - 13] != 0 ||
            BitConverter.ToUInt16(source, axis - 12) != expectedAxisTag ||
            BitConverter.ToUInt16(source, axis - 10) != 0 ||
            BitConverter.ToUInt32(source, axis - 8) != expectedAxisValue ||
            BitConverter.ToUInt32(source, axis - 4) != 1) return null;

        var definition = new RevolveFeatureModel
        {
            Angle = angleValue.Value,
            AngleDirection2 = angleDirection2,
            Type = both ? 2 : 0,
            BothDirections = both,
            ReverseDirection = reverse == 1,
            IsCut = cut,
            IsThinFeature = thin,
            ThinWallType = 0,
            Thickness1 = thickness1,
            Thickness2 = thickness2
        };
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.Angle));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.AngleDirection2));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.Type));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.BothDirections));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.ReverseDirection));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.IsCut));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.IsThinFeature));
        if (thin)
        {
            definition.MarkResolvedInternal(nameof(RevolveFeatureModel.ThinWallType));
            definition.MarkResolvedInternal(nameof(RevolveFeatureModel.Thickness1));
            definition.MarkResolvedInternal(nameof(RevolveFeatureModel.Thickness2));
        }
        return definition;
    }

    private static RevolveFeatureModel? ParseAxisEdge(FeatureWindowContext ctx)
    {
        var specNode = ctx.GetSingleNode("moRevEndSpec_c");
        var angleNode = ctx.GetSingleNode("moAngleParameter_c");
        var lineNode = ctx.GetSingleNode("moLineRef_w");
        var edgeNode = ctx.GetSingleNode("moCompEdge_c");
        if (specNode == null || angleNode == null || lineNode == null || edgeNode == null)
            return null;
        if (!ctx.HasExactNodeCount("moThinSpec_c", 0))
            return null;

        var source = ctx.RawData;
        int spec = specNode.DataStart;
        if (spec < 0 || spec + 28 > ctx.End) return null;
        uint mode = BitConverter.ToUInt32(source, spec);
        if (mode > 1) return null;
        for (int i = spec + 4; i < spec + 28; i++) if (source[i] != 0) return null;

        var nodes = ctx.Nodes;
        int index = IndexOf(nodes, angleNode);
        if (index < 2 ||
            nodes[index - 1].ClassName != "AngleDim_c" ||
            nodes[index - 2].ClassName != "moDisplayAngularDim_c" ||
            nodes[index - 2].TagOffset <= specNode.TagOffset) return null;

        var angleValue = MfcParameterReader.ReadNamedDouble(source, angleNode.DataStart, requireDNumberFormat: true);
        if (!angleValue.HasValue || !IsAngle(angleValue.Value)) return null;

        var d1 = ctx.Parameters.FindAll("D1");
        if (d1.Count != 1 || Math.Abs(d1[0].Value - angleValue.Value) > 1e-12 ||
            ctx.Parameters.FindAll("D2").Count != 0 ||
            ctx.Parameters.FindAll("D3").Count != 0 ||
            ctx.Parameters.FindAll("D4").Count != 0) return null;

        var definition = new RevolveFeatureModel
        {
            Angle = angleValue.Value,
            AngleDirection2 = 0,
            Type = 0,
            BothDirections = false,
            ReverseDirection = mode == 0,
            IsCut = false,
            IsThinFeature = false,
            Merge = true,
            FolderTypeId = 0x55u,
            AxisType = 1,
            AxisReference = new FeatureReferenceData
            {
                EntityType = SelectionEntityType.Edge,
                ReferenceName = "moCompEdge_c",
                RecordCount = 1
            }
        };
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.Angle));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.IsCut));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.IsThinFeature));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.FolderTypeId));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.AxisType));
        definition.MarkResolvedInternal(nameof(RevolveFeatureModel.AxisReference));
        return definition;
    }

    private static int IndexOf(IReadOnlyList<MfcElement> nodes, MfcElement element)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (ReferenceEquals(nodes[i], element)) return i;
        return -1;
    }

    private static bool IsAngle(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0 && value <= 2 * Math.PI;

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static bool IsNonNegativeFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RevolveFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fail-fast builder for single-direction Blind solid revolutions (moRevolution_c).
/// Supported scope: ResolvedFeatures stream version 0x70, the layout-matching extrusion-stripped
/// RevolveOptions base (forward 180 / reverse 180_reverse / full 360, pinned by a normalized base
/// signature), the controlled rectangle plus construction-axis sketch, a parsed moSketchChain_c,
/// and the 0x67 sketch child-count marker. The sketch/feature persistent serials are read from
/// moCompFeature_c and patched into the axis/sketch surface references. Everything else is
/// rejected before the stream is touched. Thin/both-directions/cut use separate complete measured
/// block templates in <see cref="RevolveOptionFeatureBuilder"/>; this builder handles the measured
/// single-direction solid serializer path.
/// </summary>
internal static class RevolveFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";
    private const string SketchLineClass = "sgLineHandle";

    // SHA-256 of the controlled sketch geometry (identical in all five RevolveOptions samples).
    // This pins the profile/axis that the measured topology and bounding-box formulas assume.
    private const string ExpectedLineSha256 =
        "9c1b69c5da6118966d652ba9fdc2d179e9e179bc475052984c195c6936f1bf18";

    // Controlled chain: four rectangle entities 2,3,4,5 (the construction axis is entity 1).
    private static readonly uint[] ExpectedChainEntityIds = { 2, 3, 4, 5 };

    // Normalized stripped-base signatures (folder count and 0x67 child value zeroed):
    // the forward/reverse/full layouts have different persistent serials and derived
    // caches, so each template may only be emitted on its own measured base.
    private const string ForwardBaseSha256 =
        "e11b5077364d5b5c926071c738912825980be0b23a957dba1124966dc49d88ae";
    private const string ReverseBaseSha256 =
        "b797b2a94b7645169b537a241f895f19530ec4ac797f7006d37c57669e9e3307";
    private const string FullBaseSha256 =
        "c97b7ee90f214ff1d6058388c9c64c2e7cf6fb2c97bf6536b0d44e6687aa3c7a";

    // moCompFeature_c serial pairs measured on the controlled bases.
    private const uint ForwardSerial = 0x6AE40CAD;
    private const uint ReverseSketchSerial = 0x6AE40CB0;
    private const uint ReverseFeatureSerial = 0x6AE40CB1;
    private const uint FullSerial = 0x6AE40CA3;
    private const ushort StrippedFolderCount = 19;

    private static readonly byte[] CompFeatureRecordMagic = { 0x38, 0x4A, 0x00, 0x00 };

    public static byte[] BuildRevolveStream(
        byte[] currentStream,
        RevolveFeatureModel def,
        FeatureFolder targetSketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (targetSketchFolder == null)
            throw new ArgumentNullException(nameof(targetSketchFolder));

        if (def.IsThinFeature || def.IsCut || def.BothDirections)
        {
            // Measured template blocks are appended to their exact stripped native base; the
            // existing solid serializer/base-signature path below is not used for these layouts.
            return RevolveOptionFeatureBuilder.Build(currentStream, def, featureName);
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70)
        {
            throw new NotSupportedException(
                $"Revolution authoring is only supported for ResolvedFeatures streams version 0x70, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        VerifyControlledSketch(currentStream, targetSketchFolder, sketchEndOffset, version);
        int markerOffset = ExtrudeFeatureBuilder.FindSketchChildCountMarker(
            currentStream, targetSketchFolder, sketchEndOffset);

        bool full = MfcRevolveSerializer.IsFullRevolution(def.Angle);
        string expectedBaseSha = full
            ? FullBaseSha256
            : def.ReverseDirection ? ReverseBaseSha256 : ForwardBaseSha256;
        string baseName = full ? "revolve_360" : def.ReverseDirection ? "revolve_180_reverse" : "revolve_180";
        string baseSha = ComputeControlledBaseSha(currentStream, markerOffset);
        if (!string.Equals(baseSha, expectedBaseSha, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Revolution authoring for this layout requires the controlled extrusion-stripped " +
                $"{baseName} base (normalized base signature {baseSha.Substring(0, 12)} does not match). " +
                "The forward, reverse and full layout templates carry different persistent sketch/feature " +
                "serials and derived caches; changing the base would produce a non-rebuildable feature. " +
                "No stream changes have been made.");
        }

        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        if (folderCount != StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Revolution authoring requires the extrusion-stripped base with folder count " +
                $"{StrippedFolderCount}, but the document uses {folderCount}. No stream changes have been made.");
        }
        uint childCount = BitConverter.ToUInt32(currentStream, markerOffset + 4);
        if (childCount != 0)
        {
            throw new NotSupportedException(
                $"Revolution authoring requires the extrusion-stripped base with a zero 0x67 sketch child " +
                $"count, but the document uses {childCount}. No stream changes have been made.");
        }

        var (sketchSerial, featureSerial) = ReadCompFeatureSerials(currentStream);
        ValidateControlledSerials(sketchSerial, featureSerial, full, def.ReverseDirection);

        int existingCount = ExtrudeFeatureBuilder.CountOccurrences(currentStream, "moRevolution_c");
        uint featureId = 0x6DE654A5 + (uint)existingCount;

        byte[] featureBlock = MfcRevolveSerializer.SerializeRevolve(
            def, featureName, sketchSerial, featureSerial, featureId);

        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);

        BitConverter.GetBytes(childCount + 1).CopyTo(newStream, markerOffset + 4);

        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    internal static void ValidateDefinition(RevolveFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (double.IsNaN(def.Angle) || double.IsInfinity(def.Angle) ||
            def.Angle <= 0.0 || def.Angle > MfcRevolveSerializer.FullRevolution + 1e-12)
        {
            throw new ArgumentOutOfRangeException(nameof(def.Angle),
                $"Revolution angle must be a finite value in (0, 2*pi] radians, but received {def.Angle}.");
        }

        if (def.IsThinFeature)
        {
            ValidateThinTemplateDefinition(def);
            return;
        }
        if (def.IsCut)
        {
            ValidateCutTemplateDefinition(def);
            return;
        }
        if (def.BothDirections)
        {
            ValidateBothTemplateDefinition(def);
            return;
        }

        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.Type != 0, nameof(def.Type));
        RejectUnsupported(def.AngleDirection2 != 0, nameof(def.AngleDirection2));
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness1 != 0, nameof(def.Thickness1));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!def.Merge, nameof(def.Merge));

        bool full = MfcRevolveSerializer.IsFullRevolution(def.Angle);
        // The controlled partial samples are 90 and 180 degrees; a partial sweep greater than
        // 180 degrees has no measured topology (face/selection structure) and is not guessed.
        RejectUnsupported(!full && def.Angle > Math.PI, nameof(def.Angle));

        // A reversed full revolution has no controlled sample; the measured reverse layout is partial only.
        RejectUnsupported(def.ReverseDirection && full, nameof(def.ReverseDirection));
    }

    private static void ValidateThinTemplateDefinition(RevolveFeatureModel def)
    {
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.Type != 0, nameof(def.Type));
        RejectUnsupported(def.AngleDirection2 != 0, nameof(def.AngleDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!IsPositiveFinite(def.Thickness1), nameof(def.Thickness1));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        // Only the two measured thin templates exist; arbitrary angles have no parameterized
        // display/cache evidence.
        if (Math.Abs(def.Angle - Math.PI / 2.0) > 1e-12 &&
            Math.Abs(def.Angle - 2.0 * Math.PI) > 1e-12)
        {
            throw new NotSupportedException(
                "Thin revolution authoring supports only the measured 90-degree and 360-degree " +
                "templates. No stream changes have been made.");
        }
    }

    private static void ValidateCutTemplateDefinition(RevolveFeatureModel def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.BothDirections, nameof(def.BothDirections));
        RejectUnsupported(def.Type != 0, nameof(def.Type));
        RejectUnsupported(def.AngleDirection2 != 0, nameof(def.AngleDirection2));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness1 != 0, nameof(def.Thickness1));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        if (Math.Abs(def.Angle - 2.0 * Math.PI) > 1e-12)
        {
            throw new NotSupportedException(
                "Cut revolution authoring supports only the measured full 360-degree template " +
                "(the native 90-degree cut creation returned NULL). No stream changes have been made.");
        }
    }

    private static void ValidateBothTemplateDefinition(RevolveFeatureModel def)
    {
        RejectUnsupported(def.IsThinFeature, nameof(def.IsThinFeature));
        RejectUnsupported(def.IsCut, nameof(def.IsCut));
        RejectUnsupported(def.Type != 2, nameof(def.Type));
        RejectUnsupported(def.ReverseDirection, nameof(def.ReverseDirection));
        RejectUnsupported(def.ThinWallType != 0, nameof(def.ThinWallType));
        RejectUnsupported(def.Thickness1 != 0, nameof(def.Thickness1));
        RejectUnsupported(def.Thickness2 != 0, nameof(def.Thickness2));
        RejectUnsupported(!def.Merge, nameof(def.Merge));
        if (!IsPositiveFinite(def.Angle) || def.Angle > 2.0 * Math.PI ||
            !IsPositiveFinite(def.AngleDirection2) || def.AngleDirection2 > 2.0 * Math.PI)
        {
            throw new NotSupportedException(
                $"Both-direction revolution {nameof(def.AngleDirection2)} angles must be finite values " +
                "in (0, 2*pi]. No stream changes have been made.");
        }
    }

    private static bool IsPositiveFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private static void VerifyControlledSketch(
        byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(targetSketchFolder.DisplayName)
            ? $"<tag {targetSketchFolder.TagOffset}>"
            : targetSketchFolder.DisplayName;

        var chain = FindSketchElement(currentStream, targetSketchFolder, sketchEndOffset, SketchChainClass);
        var line = FindSketchElement(currentStream, targetSketchFolder, sketchEndOffset, SketchLineClass);
        if (chain == null || line == null)
        {
            throw new NotSupportedException(
                $"Cannot author a revolution on sketch '{sketchName}' (ResolvedFeatures version 0x{version:X2}): " +
                $"the sketch must expose a parsed {SketchChainClass} and {SketchLineClass}. " +
                "Revolution authoring is currently limited to the controlled rectangle + construction-axis sketch. " +
                "No stream changes have been made.");
        }

        string lineSha = Sha256Hex(currentStream, line);
        bool chainMatches = ChainMatchesControlledProfile(currentStream, chain);
        if (!chainMatches || !string.Equals(lineSha, ExpectedLineSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Cannot author a revolution on sketch '{sketchName}' (ResolvedFeatures version 0x{version:X2}): " +
                "the sketch geometry does not match the controlled rectangle + construction-axis specimen " +
                $"(chain {(chainMatches ? "ids-match" : "ids-differ")}, lines {lineSha.Substring(0, 12)}). " +
                "The measured revolution topology and bounding box assume that exact profile/axis. " +
                "No stream changes have been made.");
        }
    }

    private static bool ChainMatchesControlledProfile(byte[] currentStream, MfcTag chain)
    {
        int length = chain.DataEnd - chain.DataStart;
        if (length < 2 || chain.DataStart < 0 || chain.DataStart + length > currentStream.Length)
            return false;

        ushort count = BitConverter.ToUInt16(currentStream, chain.DataStart);
        if (count != ExpectedChainEntityIds.Length || length < 2 + 4 * count)
            return false;

        for (int i = 0; i < count; i++)
        {
            uint id = BitConverter.ToUInt32(currentStream, chain.DataStart + 2 + 4 * i);
            if (id != ExpectedChainEntityIds[i])
                return false;
        }
        return true;
    }

    private static MfcTag? FindSketchElement(
        byte[] currentStream, FeatureFolder targetSketchFolder, int sketchEndOffset, string className)
    {
        int end = sketchEndOffset > targetSketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;
        return MfcPrimitives.ScanMfcTags(currentStream)
            .FirstOrDefault(t => t.ClassName == className &&
                                 t.TagOffset >= targetSketchFolder.TagOffset &&
                                 t.TagOffset < end);
    }

    private static string Sha256Hex(byte[] data, MfcTag tag)
    {
        int start = tag.DataStart;
        int count = Math.Max(0, tag.DataEnd - start);
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data, start, count);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Hashes the extrusion-stripped base with the folder count and the 0x67 sketch
    /// child-count value normalized to zero. This pins the exact measured RevolveOptions
    /// base including its persistent sketch/feature serials and derived caches.
    /// </summary>
    private static string ComputeControlledBaseSha(byte[] stream, int markerOffset)
    {
        byte[] normalized = (byte[])stream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }
        if (markerOffset >= 0 && markerOffset + 8 <= normalized.Length)
            Array.Clear(normalized, markerOffset + 4, 4);

        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }

    /// <summary>
    /// Reads the sketch and consuming-feature persistent serials from the controlled
    /// moCompFeature_c records (first record id 0x49 = sketch, second id 0x4E = feature).
    /// The reverse control uses distinct sketch/feature serials, so both must be patched
    /// independently in the emitted surface-reference blocks.
    /// </summary>
    private static (uint SketchSerial, uint FeatureSerial) ReadCompFeatureSerials(byte[] stream)
    {
        var tag = MfcPrimitives.ScanMfcTags(stream)
            .FirstOrDefault(t => t.ClassName == "moCompFeature_c");
        if (tag == null)
        {
            throw new NotSupportedException(
                "The controlled revolve base does not expose a moCompFeature_c sketch/feature reference record. " +
                "No stream changes have been made.");
        }

        int start = tag.DataStart;
        int limit = Math.Min(start + 512, stream.Length);
        int first = FindCompFeatureRecord(stream, start, limit);
        if (first < 0 || first + 12 > limit)
        {
            throw new NotSupportedException(
                "The controlled revolve base has no readable sketch serial record in moCompFeature_c. " +
                "No stream changes have been made.");
        }
        int second = FindCompFeatureRecord(stream, first + 12, limit);
        if (second < 0 || second + 12 > limit)
        {
            throw new NotSupportedException(
                "The controlled revolve base has no readable feature serial record in moCompFeature_c. " +
                "No stream changes have been made.");
        }

        uint sketchId = BitConverter.ToUInt32(stream, first + 4);
        uint sketchSerial = BitConverter.ToUInt32(stream, first + 8);
        uint featureId = BitConverter.ToUInt32(stream, second + 4);
        uint featureSerial = BitConverter.ToUInt32(stream, second + 8);
        if (sketchId != 0x49 || featureId != 0x4E || sketchSerial == 0 || featureSerial == 0)
        {
            throw new NotSupportedException(
                "The moCompFeature_c records of the controlled revolve base do not match the measured " +
                "sketch/feature serial layout. No stream changes have been made.");
        }
        return (sketchSerial, featureSerial);
    }

    private static int FindCompFeatureRecord(byte[] stream, int start, int limit)
    {
        int found = -1;
        for (int i = Math.Max(0, start); i <= limit - CompFeatureRecordMagic.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < CompFeatureRecordMagic.Length; j++)
            {
                if (stream[i + j] != CompFeatureRecordMagic[j]) { match = false; break; }
            }
            if (match)
            {
                found = i;
                break;
            }
        }
        return found;
    }

    private static void ValidateControlledSerials(
        uint sketchSerial, uint featureSerial, bool full, bool reverse)
    {
        uint expectedSketch = full ? FullSerial : reverse ? ReverseSketchSerial : ForwardSerial;
        uint expectedFeature = full ? FullSerial : reverse ? ReverseFeatureSerial : ForwardSerial;
        if (sketchSerial != expectedSketch || featureSerial != expectedFeature)
        {
            string layout = full ? "full 2*pi" : reverse ? "reverse partial" : "forward partial";
            throw new NotSupportedException(
                $"The controlled revolve base serials (sketch 0x{sketchSerial:X8}, feature 0x{featureSerial:X8}) " +
                $"do not match the measured {layout} layout (expected 0x{expectedSketch:X8}/0x{expectedFeature:X8}). " +
                "No stream changes have been made.");
        }
    }

    private static void RejectUnsupported(bool unsupported, string property)
    {
        if (unsupported)
            throw new NotSupportedException(
                $"Revolution authoring does not yet support {property}. No stream changes have been made.");
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RevolveOptionAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured revolution feature block and stripped-base signature. The block starts at the measured
/// class tag and runs to the end of the native ResolvedFeatures stream; the base is the same sample
/// truncated at that tag with the header folder count and the 0x67 child-count value zeroed for
/// signature normalization.
/// </summary>
internal sealed class RevolveAuthoringTemplate
{
    internal RevolveAuthoringTemplate(string kind, string name, string baseSha256, int baseLength,
        ushort streamVersion, int strippedFolderCount, int markerValue, byte[] block)
    {
        Kind = kind;
        Name = name;
        BaseSha256 = baseSha256;
        BaseLength = baseLength;
        StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount;
        MarkerValue = markerValue;
        Block = block;
    }

    internal string Kind { get; }
    internal string Name { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class RevolveOptionAuthoringTemplates
{
    /// <summary>Thin90 measured template from revolve_thin_90.resolved.bin (9180 base64 chars).</summary>
    internal static readonly RevolveAuthoringTemplate Thin90 = new RevolveAuthoringTemplate(
        "Thin90",
        "ThinRevolve90",
        "9ebbd13ce19ed469db1bf80ed86b5fe9b1cf395127ce84fc6d2fa99231c5a09c",
        9314,
        0x70,
        19,
        7,
        RevolveAuthoringSegments.OptionThinOneDirection);

    /// <summary>Thin360 measured template from revolve_thin_360.resolved.bin (9012 base64 chars).</summary>
    internal static readonly RevolveAuthoringTemplate Thin360 = new RevolveAuthoringTemplate(
        "Thin360",
        "ThinRevolve360",
        "71eb8a114af4459831391bb292fef91019e23e18a3f6e6e2a3e6e3824da41380",
        9314,
        0x70,
        19,
        7,
        RevolveAuthoringSegments.OptionThinMidPlane);

    /// <summary>BothDirections measured template from revolve_both_directions.resolved.bin (6876 base64 chars).</summary>
    internal static readonly RevolveAuthoringTemplate BothDirections = new RevolveAuthoringTemplate(
        "BothDirections",
        "BothDirectionsRevolve",
        "6707ee646bfda4a4c4f7ce926dad47aa1b3fcfbd930d4829238036cf57dca33c",
        9314,
        0x70,
        19,
        6,
        RevolveAuthoringSegments.OptionThinTwoDirection);

    /// <summary>Cut360 measured template from revolve_cut_360.resolved.bin (4600 base64 chars).</summary>
    internal static readonly RevolveAuthoringTemplate Cut360 = new RevolveAuthoringTemplate(
        "Cut360",
        "RevolveCut360",
        "8d082e782e4845b9f31db72faf583db589a5e0a9a4ddfd8dd01942521cdc8ed3",
        15567,
        0x71,
        21,
        13,
        RevolveAuthoringSegments.OptionTwoDirection);

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/RevolveOptionFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Controlled template authoring for the measured thin/both-directions/cut revolutions.
/// The target must be the exact native sample truncated at its measured class tag with the header
/// folder count and the preceding 0x67 value zeroed. The complete native feature block is appended
/// after patching only the measured named-parameter slots. Geometry/display caches remain those of
/// the template; native verification of rebuild/volume is delegated to Agent 5.
/// </summary>
internal static class RevolveOptionFeatureBuilder
{
    private const int MarkerDistanceFromFeature = 0x10;

    internal static string GetTemplateName(RevolveFeatureModel definition)
    {
        if (definition.IsThinFeature)
            return IsFull(definition.Angle)
                ? RevolveOptionAuthoringTemplates.Thin360.Name
                : RevolveOptionAuthoringTemplates.Thin90.Name;
        if (definition.IsCut)
            return RevolveOptionAuthoringTemplates.Cut360.Name;
        if (definition.BothDirections)
            return RevolveOptionAuthoringTemplates.BothDirections.Name;
        throw new NotSupportedException("This revolution layout has no template authoring block.");
    }

    internal static byte[] Build(
        byte[] currentStream, RevolveFeatureModel definition, string featureName)
    {
        var template = SelectTemplate(definition);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Revolution template authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream == null || currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires the measured stripped base of length " +
                $"{template.BaseLength} bytes; the document stream is {currentStream?.Length ?? 0} bytes. " +
                "No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires the stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        }
        int marker = currentStream.Length - MarkerDistanceFromFeature;
        if (marker < 0 || currentStream[marker] != 0x67 || currentStream[marker + 1] != 0 ||
            currentStream[marker + 2] != 0 || currentStream[marker + 3] != 0 ||
            BitConverter.ToUInt32(currentStream, marker + 4) != 0)
        {
            throw new NotSupportedException(
                "Revolution template authoring requires the stripped base's 0x67 marker at the measured " +
                "position with value 0. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream, marker);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Revolution template authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        if (definition.IsThinFeature)
            PatchThinThickness(block, definition.Thickness1);
        else if (definition.BothDirections)
            PatchBothAngles(block, definition.Angle, definition.AngleDirection2);

        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, marker + 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static RevolveAuthoringTemplate SelectTemplate(RevolveFeatureModel definition)
    {
        if (definition.IsThinFeature)
            return IsFull(definition.Angle)
                ? RevolveOptionAuthoringTemplates.Thin360
                : RevolveOptionAuthoringTemplates.Thin90;
        if (definition.IsCut) return RevolveOptionAuthoringTemplates.Cut360;
        if (definition.BothDirections) return RevolveOptionAuthoringTemplates.BothDirections;
        throw new NotSupportedException("This revolution layout has no template authoring block.");
    }

    private static void PatchThinThickness(byte[] block, double thickness)
    {
        var d3 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D3");
        if (d3.Count != 1)
            throw new InvalidOperationException("Measured thin revolve template is missing D3.");
        int value = d3[0].Offset;
        foreach (int off in new[] { 0x20, 0x1A6, 0x1BE, 0x250, 0x268 })
        {
            Require(block, value + off, 0.001);
            BitConverter.GetBytes(thickness).CopyTo(block, value + off);
        }
    }

    private static void PatchBothAngles(byte[] block, double angle1, double angle2)
    {
        var d1 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D1");
        var d2 = ExtrudeAdditionalOptionParser.FindNamedParameters(block, 0, block.Length, "D2");
        if (d1.Count != 1 || d2.Count != 1)
            throw new InvalidOperationException("Measured both-directions revolve template is missing D1/D2.");
        foreach (int off in new[] { 0x20, 0x23A })
        {
            Require(block, d1[0].Offset + off, Math.PI / 2.0);
            BitConverter.GetBytes(angle1).CopyTo(block, d1[0].Offset + off);
            Require(block, d2[0].Offset + off, Math.PI / 4.0);
            BitConverter.GetBytes(angle2).CopyTo(block, d2[0].Offset + off);
        }
    }

    private static bool IsFull(double angle) => Math.Abs(angle - 2.0 * Math.PI) <= 1e-12;

    private static void Require(byte[] block, int offset, double expected)
    {
        if (offset < 0 || offset + 8 > block.Length ||
            Math.Abs(BitConverter.ToDouble(block, offset) - expected) > 1e-12)
            throw new InvalidOperationException(
                "Measured revolution template layout changed; refusing to patch an unknown slot.");
    }

    private static string NormalizedBaseSha(byte[] stream, int marker)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        BitConverter.GetBytes(0u).CopyTo(normalized, marker + 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SurfaceRevolveAuthoringTemplates.cs
// ------------------------------------------------------------------------
internal sealed class SurfaceRevolveAuthoringTemplate
{
    internal SurfaceRevolveAuthoringTemplate(string variant, string name, string className,
        string baseSha256, int baseLength, ushort streamVersion, int strippedFolderCount,
        int markerOffset, uint markerValue, byte[] block)
    {
        Variant = variant; Name = name; ClassName = className;
        BaseSha256 = baseSha256; BaseLength = baseLength; StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount; MarkerOffset = markerOffset; MarkerValue = markerValue;
        Block = block;
    }
    internal string Variant { get; }
    internal string Name { get; }
    internal string ClassName { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerOffset { get; }
    internal uint MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class SurfaceRevolveAuthoringTemplates
{
    internal static readonly SurfaceRevolveAuthoringTemplate SurfaceRevolvePrimary = new SurfaceRevolveAuthoringTemplate(
        "Primary", "SurfaceRevolve360", "moRevolvRefSurf_c",
        "8a0070dfb8a9630031ebb17efdf04a56cafd649ee5db6d1378e3e86b1333020a", 9314, 0x70, 19,
        9298, 10u,
        RevolveAuthoringSegments.SurfaceSource);

    internal static readonly SurfaceRevolveAuthoringTemplate SurfaceRevolveResave = new SurfaceRevolveAuthoringTemplate(
        "Resave", "SurfaceRevolve360", "moRevolvRefSurf_c",
        "0a8a30ad6f26b70d14c509d081151c4fbafa9a198898275f2b658a751e320ce6", 9314, 0x70, 19,
        9298, 10u,
        RevolveAuthoringSegments.SurfaceResave);

    internal static readonly SurfaceRevolveAuthoringTemplate[] All =
    {
        SurfaceRevolvePrimary,
        SurfaceRevolveResave,
    };
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SurfaceRevolveFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured surface-revolve authoring from the controlled 360-degree sample.
/// The target must be the exact stripped base (stream 0x70, folder count 19, 0x67
/// marker with value 0, normalized SHA). The measured block is appended verbatim;
/// angle, axis/profile selections and all options remain measured values. Partial
/// angles have no sample and are not parameterized.
/// </summary>
internal static class SurfaceRevolveFeatureBuilder
{
    internal static (byte[] Stream, string FeatureName) Build(byte[] currentStream, string? featureName)
    {
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        var template = SelectTemplate(currentStream);
        if (!string.IsNullOrWhiteSpace(featureName) &&
            !string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Surface revolve measured authoring uses the native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, SurfaceRevolveFeatureParser.ClassName) > 0)
        {
            throw new NotSupportedException(
                "The document already contains a moRevolvRefSurf_c feature. No stream changes have been made.");
        }

        var newStream = new byte[currentStream.Length + template.Block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        BitConverter.GetBytes(template.MarkerValue).CopyTo(newStream, template.MarkerOffset + 4);
        Array.Copy(template.Block, 0, newStream, currentStream.Length, template.Block.Length);
        return (newStream, template.Name);
    }

    private static SurfaceRevolveAuthoringTemplate SelectTemplate(byte[] currentStream)
    {
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        foreach (var template in SurfaceRevolveAuthoringTemplates.All)
        {
            if (currentStream.Length != template.BaseLength)
                continue;
            if (version != template.StreamVersion)
                continue;
            if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
                continue;
            if (template.MarkerOffset + 8 > currentStream.Length)
                continue;
            if (currentStream[template.MarkerOffset] != 0x67 ||
                currentStream[template.MarkerOffset + 1] != 0 ||
                currentStream[template.MarkerOffset + 2] != 0 ||
                currentStream[template.MarkerOffset + 3] != 0 ||
                BitConverter.ToUInt32(currentStream, template.MarkerOffset + 4) != 0)
                continue;

            byte[] normalized = (byte[])currentStream.Clone();
            BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
            BitConverter.GetBytes(0u).CopyTo(normalized, template.MarkerOffset + 4);
            string sha;
            using (var hash = SHA256.Create())
                sha = BitConverter.ToString(hash.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
            if (string.Equals(sha, template.BaseSha256, StringComparison.Ordinal))
                return template;
        }

        throw new NotSupportedException(
            $"The document is not the controlled stripped surface-revolve base " +
            $"(stream length {currentStream.Length}, version 0x{version:X2}). No stream changes have been made.");
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/SurfaceRevolveFeatureParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict parser for the measured 2026 surface-revolve layout
/// (moRevolvRefSurf_c, stream 0x70). Only the revolution angle (AngleDim_c ->
/// moAngleParameter_c "D1", three identical copies) and the type code are decoded.
/// </summary>
internal static class SurfaceRevolveFeatureParser
{
    internal const string ClassName = "moRevolvRefSurf_c";
    internal const uint ExpectedTypeId = 0x4E;
    internal const ushort ExpectedStreamVersion = 0x70;

    internal const int AngleDimRelativeOffset = 0x80E;
    internal const int AngleParameterRelativeOffset = 0x822;
    internal const int AngleValueFromParameter = 0x20;
    internal const int AngleCopy1FromParameter = 0x222;
    internal const int AngleCopy2FromParameter = 0x23A;

    private static readonly int[] AngleSlotsFromParameter =
    {
        AngleValueFromParameter,
        AngleCopy1FromParameter,
        AngleCopy2FromParameter
    };

    public static SurfaceRevolveFeatureModel? Parse(MfcElement element)
    {
        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: ClassName,
            minTypeId: ExpectedTypeId,
            maxTypeId: ExpectedTypeId,
            expectedStreamVersion: ExpectedStreamVersion);
        if (ctx == null)
            return null;

        var angleDim = ctx.Nodes.FirstOrDefault(t => t.ClassName == "AngleDim_c" && t.TagOffset > ctx.Start);
        if (angleDim == null)
            return null;
        var angleParam = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moAngleParameter_c" && t.TagOffset > angleDim.TagOffset);
        if (angleParam == null)
            return null;

        if (!ctx.Parameters.TryReadVerifiedDoubleSlots(
                angleParam.DataStart,
                AngleSlotsFromParameter,
                out double angle,
                minExclusive: 0.0,
                maxInclusive: 2 * Math.PI,
                tolerance: 1e-15))
        {
            return null;
        }

        var definition = new SurfaceRevolveFeatureModel
        {
            SerializedClassName = ClassName,
            SerializedTypeId = ctx.TypeId,
            Angle = angle,
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(ctx)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.Angle = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }
}


