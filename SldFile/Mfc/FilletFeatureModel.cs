using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;


/// <summary>
/// Unified strongly-typed model for Fillet_c edge fillet feature.
/// Shared between reading and writing via IMfcSerializable.
/// Reading decodes the stream window into this object; writing serializes this object into stream bytes.
/// Modifying the radius updates all internal dimension and comp face parameters automatically.
/// </summary>
public sealed class FilletFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "Fillet_c";
    public const uint HeaderTypeId = 0x51u;

    public string ClassName => MfcClassName;
    public string FeatureName { get; set; } = "圆角1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get; set; } = HeaderTypeId;
    public double? DefaultRadius { get; set; }
    public double Radius { get => DefaultRadius ?? 0.002; set => DefaultRadius = value; }
    public int? Type { get; set; }
    public int? OverflowType { get; set; }
    public bool? IsMultipleRadius { get; set; }
    public bool? RoundCorners { get; set; }
    public bool? PropagateToTangentFaces { get; set; }
    public int? FilletItemsCount { get; set; }
    public bool? KeepFeatures { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Edges { get; set; }
    public IReadOnlyList<FeatureReferenceData>? FaceReferences { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Loops { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Features { get; set; }
    public IReadOnlyList<FeatureReferenceData>? HoldLines { get; set; }
    public bool? CurvatureContinuous { get; set; }
    public FeatureReferenceData? HelpPoint { get; set; }
    public bool? OmitAttachedEdges { get; set; }
    public bool? ConstantWidth { get; set; }
    public bool? ReverseFaceNormal { get; set; }
    public int? TrimAndAttachSurfaces { get; set; }
    public bool? PropagateFeatureToParts { get; set; }
    public int? ConicTypeForCrossSectionProfile { get; set; }
    public double? DefaultConicRhoOrRadius { get; set; }
    public bool? AsymmetricFillet { get; set; }
    public double? DefaultDistance { get; set; }
    public bool? EnablePartialEdgeParameters { get; set; }

    private readonly Dictionary<uint, double> _itemRadii = new Dictionary<uint, double>();
    public IReadOnlyDictionary<uint, double>? ItemRadii => _itemRadii.Count == 0 ? null : _itemRadii;
    public double? GetRadius(uint itemIndex) => _itemRadii.TryGetValue(itemIndex, out var r) ? r : (double?)null;
    public void SetRadius(uint itemIndex, double radius) => _itemRadii[itemIndex] = radius;

    public OpaqueObject HeaderSuffix { get; set; }
    public OpaqueObject SurfaceIdRep { get; set; }
    public OpaqueObject FilletSurfIdRep { get; set; }
    public OpaqueObject EdgeRef { get; set; }
    public OpaqueObject CompEdge { get; set; }
    public OpaqueObject DisplayRadialDim { get; set; }
    public ThreeDRadiusDimObject RadiusDim { get; set; }
    public CompFaceRadiusObject CompFace { get; set; }

    /// <summary>
    /// 统一尺寸参数驱动层模型 (moLengthParameter_c).
    /// </summary>
    public MoLengthParameterModel Parameter => new MoLengthParameterModel("Radius@" + FeatureName, Radius);

    /// <summary>
    /// 统一尺寸显示几何布局层模型 (moDisplayRadialDim_c).
    /// </summary>
    public MoDisplayRadialDimModel DisplayDimension => new MoDisplayRadialDimModel();

    /// <summary>
    /// 统一尺寸对象模型.
    /// </summary>
    public DimensionModel Dimension => new DimensionModel(Parameter, DisplayDimension);

    /// <summary>
    /// 特征所包含的统一尺寸列表.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<DimensionModel> Dimensions => new[] { Dimension };

    public FilletFeatureModel()
    {
        HeaderSuffix = new OpaqueObject(string.Empty, FilletSegments.HeaderSuffix);
        SurfaceIdRep = new OpaqueObject("moSurfaceIdRep_c", FilletSegments.SurfaceIdRepData);
        FilletSurfIdRep = new OpaqueObject("moFilletSurfIdRep_c", FilletSegments.FilletSurfIdRepData);
        EdgeRef = new OpaqueObject("moEdgeRef_c", FilletSegments.EdgeRefData);
        CompEdge = new OpaqueObject("moCompEdge_c", FilletSegments.CompEdgeData);
        DisplayRadialDim = new OpaqueObject("moDisplayRadialDim_c", FilletSegments.DisplayRadialDimData);
        RadiusDim = new ThreeDRadiusDimObject(FilletSegments.ThreeDRadiusDimData, Radius);
        CompFace = new CompFaceRadiusObject(FilletSegments.CompFaceData, Radius);
    }

    /// <summary>
    /// Clones this model and sets a new radius, updating all dependent representations.
    /// </summary>
    public FilletFeatureModel CloneWithRadius(double newRadius, string? newFeatureName = null)
    {
        var clone = new FilletFeatureModel
        {
            FeatureName = newFeatureName ?? FeatureName,
            Version = Version,
            TypeId = TypeId,
            Radius = newRadius,
            HeaderSuffix = new OpaqueObject(HeaderSuffix.ClassName, (byte[])HeaderSuffix.RawData.Clone()),
            SurfaceIdRep = new OpaqueObject(SurfaceIdRep.ClassName, (byte[])SurfaceIdRep.RawData.Clone()),
            FilletSurfIdRep = new OpaqueObject(FilletSurfIdRep.ClassName, (byte[])FilletSurfIdRep.RawData.Clone()),
            EdgeRef = new OpaqueObject(EdgeRef.ClassName, (byte[])EdgeRef.RawData.Clone()),
            CompEdge = new OpaqueObject(CompEdge.ClassName, (byte[])CompEdge.RawData.Clone()),
            DisplayRadialDim = new OpaqueObject(DisplayRadialDim.ClassName, (byte[])DisplayRadialDim.RawData.Clone()),
            RadiusDim = new ThreeDRadiusDimObject((byte[])RadiusDim.TemplatePayload.Clone(), newRadius),
            CompFace = new CompFaceRadiusObject((byte[])CompFace.TemplatePayload.Clone(), newRadius)
        };
        return clone;
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

            double rad = Radius;
            ar.F64(ref rad);

            HeaderSuffix.Serialize(ar);

            // References & selections
            EmitTagged(ar, SurfaceIdRep);
            EmitTagged(ar, FilletSurfIdRep);
            EmitTagged(ar, EdgeRef);
            EmitTagged(ar, CompEdge);

            ushort edgeRadiusTag = 0;
            ar.ClassTagOrRef("edgeRadiusObject_c", ref edgeRadiusTag);

            EmitTagged(ar, DisplayRadialDim);

            // Radius parameters & comp face
            RadiusDim.Radius = Radius;
            RadiusDim.Serialize(ar);

            CompFace.Radius = Radius;
            CompFace.Serialize(ar);
        }
        else
        {
            // Reading
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
                double rad = 0;
                ar.F64(ref rad);
                Radius = rad;
            }

            // Remainder contains subnodes parsed into bags
        }
    }

    private static void EmitTagged(MfcArchive ar, OpaqueObject obj)
    {
        ushort tag = 0;
        ar.ClassTagOrRef(obj.ClassName, ref tag);
        ar.WriteBytes(obj.RawData);
    }
}

/// <summary>
/// Strongly-typed MFC feature model for VarFillet_c (Variable Fillet).
/// </summary>
public sealed class VariableFilletFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "VarFillet_c";
    public string ClassName => MfcClassName;
    public uint TypeId => 0x52u;

    public double? DefaultRadius { get; set; }
    public double? DefaultDistance { get; set; }
    public int? OverflowType { get; set; }
    public int? TransitionType { get; set; }
    public bool? PropagateToTangentFaces { get; set; }
    public bool? PropagateFeatureToParts { get; set; }
    public int? ConicTypeForCrossSectionProfile { get; set; }
    public double? DefaultConicRhoOrRadius { get; set; }
    public bool? AsymmetricFillet { get; set; }
    public bool? CurvatureContinuous { get; set; }
    public int? FilletEdgeCount { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Edges { get; set; }

    private readonly List<double> _controlPointRadii = new();

    public IReadOnlyList<double>? ControlPointRadii =>
        _controlPointRadii.Count == 0 ? null : _controlPointRadii.AsReadOnly();

    public int? ControlPointCount => _controlPointRadii.Count == 0 ? null : _controlPointRadii.Count;

    public double? GetControlPointRadiusAtIndex(int index) =>
        index >= 0 && index < _controlPointRadii.Count ? _controlPointRadii[index] : (double?)null;

    public void SetControlPointRadiusAtIndex(int index, double radius)
    {
        if (index < 0 || index >= _controlPointRadii.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _controlPointRadii[index] = radius;
    }

    public void SetControlPointRadii(IEnumerable<double> radii)
    {
        _controlPointRadii.Clear();
        if (radii != null) _controlPointRadii.AddRange(radii);
    }

    public void Serialize(MfcArchive ar) { }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FilletSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured Fillet_c sub-object payloads from the controlled fillet_2mm sample (SW2026,
/// stream version 0x70). The radius slots are written structurally; the remaining bytes are
/// the measured selection/display metadata for the controlled single edge.
/// </summary>
internal static class FilletSegments
{
    internal const string ClassName = "Fillet_c";
    internal const uint HeaderTypeId = 0x51u;
    internal const int ThreeDRadiusParameterOffset = 6;
    internal static readonly int[] ThreeDRadiusPositiveSlots = { 38, 318, 552, 576, 1024 };
    internal const int ThreeDRadiusNegativeSlot = 1056;
    internal const int CompFaceRadiusSlot = 582;
    internal const int ThreeDRadiusExpectedLength = 1323;
    internal const int CompFaceExpectedLength = 926;

    /// <summary>Fillet_c data after schema/WChar name/double 2.0/TypeId 0x51/u32 0/empty WChar, measured on fillet_2mm.</summary>
    internal static readonly byte[] HeaderSuffix = FilletAuthoringSegments.HeaderSuffix;

    /// <summary>moSurfaceIdRep_c payload (measured surface tag/reference).</summary>
    internal static readonly byte[] SurfaceIdRepData = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x51, 0x00, 0x00, 0x00, 0x8A, 0x9B, 0xE1, 0x6A
    };

    /// <summary>moFilletSurfIdRep_c payload (measured surface tag/reference).</summary>
    internal static readonly byte[] FilletSurfIdRepData = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x51, 0x00, 0x00, 0x00, 0x8A, 0x9B, 0xE1, 0x6A, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00
    };

    /// <summary>moEdgeRef_c payload for the controlled single target edge.</summary>
    internal static readonly byte[] EdgeRefData = FilletAuthoringSegments.EdgeRefData;

    /// <summary>moCompEdge_c payload; byte-identical in the 2 mm and 5 mm controls.</summary>
    internal static readonly byte[] CompEdgeData = FilletAuthoringSegments.CompEdgeData;

    /// <summary>moDisplayRadialDim_c payload (display dimension metadata).</summary>
    internal static readonly byte[] DisplayRadialDimData = FilletAuthoringSegments.DisplayRadialDimData;

    /// <summary>ThreeDRadiusDim_c payload; radius slots at +38/+318/+552/+576/+1024 and -radius at +1056.</summary>
    internal static readonly byte[] ThreeDRadiusDimData = FilletAuthoringSegments.ThreeDRadiusDimData;

    /// <summary>moCompFace_c payload; direct radius copy at +582.</summary>
    internal static readonly byte[] CompFaceData = FilletAuthoringSegments.CompFaceData;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FilletFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fail-fast builder for the first controlled fillet authoring slice:
/// constant radius on the measured single target edge of the fillet_2mm base body.
/// Requires stream version 0x70, a parsed sketch chain, the 0x67 sketch marker and the exact
/// controlled base-body prefix signature. Type, explicit edge selections and every other
/// unverified field are rejected before the stream is touched.
/// </summary>
internal static class FilletFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";

    // SHA-256 of the stripped fillet_2mm base prefix (bytes 0..Fillet_c tag) with the folder
    // count u16 at offset 4 normalized to zero. This pins the extruded cube and its target edge.
    private const string ExpectedBasePrefixSha256 =
        "63a536b5d8a3d364e0c3c1a115bb6464e820d5eb6d05ea012980fbcdfa3e0447";

    public static byte[] BuildFilletStream(
        byte[] currentStream,
        FilletFeatureModel def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (sketchFolder == null)
            throw new ArgumentNullException(nameof(sketchFolder));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70)
        {
            throw new NotSupportedException(
                $"Fillet authoring is only supported for ResolvedFeatures streams version 0x70, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, FilletSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Fillet authoring currently supports only a stripped base body without an existing " +
                "Fillet_c feature. No stream changes have been made.");
        }

        // The fillet is not a sketch child, so the 0x67 marker is required as a boundary sanity
        // check but is not updated. In the controlled fillet sample the marker sits before the
        // sketch folder, so search the base prefix rather than the extrude-style trailing window.
        RequireSketchChain(currentStream, sketchFolder, sketchEndOffset, version);
        RequireSketchChildCountMarker(currentStream, version);
        RequireControlledBase(currentStream, version);

        byte[] featureBlock = MfcFilletSerializer.SerializeFillet(def, featureName);
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    internal static void ValidateDefinition(FilletFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.DefaultRadius.HasValue ||
            double.IsNaN(def.DefaultRadius.Value) || double.IsInfinity(def.DefaultRadius.Value) ||
            def.DefaultRadius.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.DefaultRadius),
                $"Fillet radius must be a positive finite number, but received {def.DefaultRadius}.");
        }

        // Only the radius is structurally verified. Type, edge selections, multi-radius and every
        // other API field remain unresolved and must not be silently accepted.
        foreach (var property in typeof(FilletFeatureModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(FilletFeatureModel.DefaultRadius) ||
                property.Name == nameof(IFeatureData.ClassName) ||
                property.Name == nameof(IFeatureData.TypeId) ||
                property.Name == nameof(MfcFeatureModelBase.ResolvedProperties) ||
                property.Name == nameof(FilletFeatureModel.FeatureName) ||
                property.Name == nameof(FilletFeatureModel.Version) ||
                property.Name == nameof(FilletFeatureModel.Radius) ||
                property.Name == nameof(FilletFeatureModel.HeaderSuffix) ||
                property.Name == nameof(FilletFeatureModel.SurfaceIdRep) ||
                property.Name == nameof(FilletFeatureModel.FilletSurfIdRep) ||
                property.Name == nameof(FilletFeatureModel.EdgeRef) ||
                property.Name == nameof(FilletFeatureModel.CompEdge) ||
                property.Name == nameof(FilletFeatureModel.DisplayRadialDim) ||
                property.Name == nameof(FilletFeatureModel.RadiusDim) ||
                property.Name == nameof(FilletFeatureModel.CompFace) ||
                property.Name == nameof(FilletFeatureModel.Parameter) ||
                property.Name == nameof(FilletFeatureModel.DisplayDimension) ||
                property.Name == nameof(FilletFeatureModel.Dimension) ||
                property.Name == nameof(FilletFeatureModel.Dimensions))
                continue;

            object? value = property.GetValue(def);
            if (value != null)
            {
                throw new NotSupportedException(
                    $"Fillet authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }
    }

    private static void RequireSketchChain(
        byte[] currentStream, FeatureFolder sketchFolder, int sketchEndOffset, ushort version)
    {
        string sketchName = string.IsNullOrEmpty(sketchFolder.DisplayName)
            ? $"<tag {sketchFolder.TagOffset}>"
            : sketchFolder.DisplayName;
        int end = sketchEndOffset > sketchFolder.TagOffset
            ? Math.Min(sketchEndOffset, currentStream.Length)
            : currentStream.Length;
        bool hasChain = MfcPrimitives.ScanMfcTags(currentStream)
            .Any(t => t.ClassName == SketchChainClass &&
                      t.TagOffset >= sketchFolder.TagOffset &&
                      t.TagOffset < end);
        if (!hasChain)
        {
            throw new NotSupportedException(
                $"Cannot author a fillet on the base body of sketch '{sketchName}' " +
                $"(ResolvedFeatures version 0x{version:X2}): no parseable {SketchChainClass} was found. " +
                "No stream changes have been made.");
        }
    }

    private static void RequireSketchChildCountMarker(byte[] currentStream, ushort version)
    {
        for (int i = 0; i <= currentStream.Length - 4; i++)
        {
            if (currentStream[i] == 0x67 && currentStream[i + 1] == 0x00 &&
                currentStream[i + 2] == 0x00 && currentStream[i + 3] == 0x00)
                return;
        }

        throw new NotSupportedException(
            $"Cannot author a fillet on the controlled base body (ResolvedFeatures version 0x{version:X2}): " +
            "the 0x67 sketch child-count marker was not found. No stream changes have been made.");
    }

    private static void RequireControlledBase(byte[] currentStream, ushort version)
    {
        byte[] normalized = (byte[])currentStream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }

        string sha = Sha256Hex(normalized);
        if (!string.Equals(sha, ExpectedBasePrefixSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Fillet authoring is limited to the controlled fillet_2mm base body " +
                $"(ResolvedFeatures version 0x{version:X2}): base prefix signature {sha.Substring(0, 12)} " +
                "does not match the measured extruded cube/target edge. No stream changes have been made.");
        }
    }

    private static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/MfcFilletSerializer.cs
// ------------------------------------------------------------------------
/// <summary>
/// Serializes the constant-radius fillet block via the unified <see cref="FilletFeatureModel"/>.
/// Clones the model, updates radius and name, and serializes symmetrically via <see cref="MfcArchive"/>.
/// </summary>
internal static class MfcFilletSerializer
{
    private static readonly FilletFeatureModel DefaultTemplateModel = new();

    public static byte[] SerializeFillet(FilletFeatureModel def, string featureName)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (!def.DefaultRadius.HasValue)
            throw new ArgumentException("Fillet DefaultRadius must be set.", nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "圆角1";

        double radius = def.DefaultRadius.Value;

        // Clone model and modify field (Clone + Modify paradigm)
        var model = DefaultTemplateModel.CloneWithRadius(radius, featureName);

        using var ar = MfcArchive.CreateWriter(4096);
        model.Serialize(ar);
        return ar.ToArray();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FilletChamferFeatureParser.cs
// ------------------------------------------------------------------------
/// <summary>
/// Strict parser for the measured constant-radius edge fillet layout (<c>Fillet_c</c>,
/// FolderHeader TypeId 0x51, stream version 0x70, Type=1), the Type=2 face-fillet layout,
/// the variable-radius layout (<c>VarFillet_c</c>, FolderHeader TypeId 0x52,
/// stream version 0x70) and the chamfer layout (<c>Chamfer_c</c>, TypeId 0x52).
/// Radius/chamfer parameters, the single measured persistent edge reference
/// (<c>moEdgeRef_c</c> with one <c>moCompEdge_c</c>) and the two measured face references are
/// decoded; other selection/transition fields remain unresolved because no controlled evidence
/// establishes them yet.
/// </summary>
internal static class FilletChamferFeatureParser
{
    internal const string FilletClassName = "Fillet_c";
    internal const uint FilletTypeId = 0x51u;
    internal const string VariableFilletClassName = VariableFilletSegments.ClassName;
    internal const uint VariableFilletTypeId = VariableFilletSegments.TypeId;
    internal const string ChamferClassName = "Chamfer_c";
    internal const uint ChamferTypeId = 0x52u;

    /// <summary>
    /// Parses the controlled constant-radius fillet layout. Returns null for unknown classes,
    /// ids, stream versions, malformed blocks or inconsistent radius copies.
    /// </summary>
    public static FilletFeatureModel? ParseFillet(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != FilletClassName || typeId != FilletTypeId) return null;
        if (!FeatureWindowContext.TryCreate(doc, start, end, 0x70, out var ctx) || ctx == null)
            return null;

        bool faceLayout = ctx.Nodes.Any(e => e.ClassName == "moFaceBlendItem_c" ||
                                             e.ClassName == "moFaceBlendItemPair_c" ||
                                             e.ClassName == "moFaceBlendSurfIdRep_c");
        if (faceLayout) return ParseFaceFillet(ctx, start, end);

        var radiusDim = ctx.GetSingleNode("ThreeDRadiusDim_c");
        if (radiusDim == null || !ctx.HasExactNodeCount("moCompFace_c", 1)) return null;
        if (!ctx.TryReadSingleEdgeReference(out var edgeReference, expectedCompEdgeLength: 178) ||
            edgeReference == null)
            return null;

        // The generic MFC scanner truncates ThreeDRadiusDim_c at internal sentinels. The tag/data
        // start is reliable; slice the measured payload length and require the next class tag to
        // be moCompFace_c so an altered/truncated block is rejected.
        int dataStart = radiusDim.DataStart;
        int dataEnd = dataStart + FilletSegments.ThreeDRadiusExpectedLength;
        if (dataStart < 0 || dataEnd > end || dataEnd > doc.SourceData.Length) return null;
        if (!MfcPrimitives.IsMfcTag(doc.SourceData, dataEnd)) return null;
        int nextTagOffset = dataEnd;
        var nextTag = MfcPrimitives.ReadMfcTag(doc.SourceData, ref nextTagOffset);
        if (nextTag == null || nextTag.ClassName != "moCompFace_c") return null;

        byte[] raw = MfcPrimitives.CopyRange(doc.SourceData, dataStart, dataEnd);
        if (raw.Length != FilletSegments.ThreeDRadiusExpectedLength) return null;

        // The measured ThreeDRadiusDim_c payload embeds a named parameter (schema 0x8004, D1).
        double? radius = MfcParameterReader.ReadNamedDouble(
            raw, FilletSegments.ThreeDRadiusParameterOffset, requireDNumberFormat: true);
        if (!IsPositiveFinite(radius)) return null;

        // Every measured positive display copy and the negative copy must agree with the parameter.
        if (!MfcParameterReader.VerifyDoubleSlots(
                raw, FilletSegments.ThreeDRadiusPositiveSlots, new[] { FilletSegments.ThreeDRadiusNegativeSlot }, radius!.Value))
            return null;

        var definition = new FilletFeatureModel
        {
            DefaultRadius = radius.Value,
            Edges = new[] { edgeReference }
        };
        definition.MarkResolvedInternal(nameof(FilletFeatureModel.DefaultRadius));
        definition.MarkResolvedInternal(nameof(FilletFeatureModel.Edges));
        return definition;
    }

    /// <summary>
    /// Parses the controlled variable-radius fillet layout (<c>VarFillet_c</c> / TypeId 0x52,
    /// stream 0x70). The measured sample has one edge with two control-point radii: D0 in the
    /// embedded ThreeDRadiusDim_c block and D01 in the moVertexRef_c block. Every measured radius
    /// copy (including the negative copy of D0 in the vertex block) must agree; unknown layouts,
    /// truncated/tampered blocks and the constant-radius layout return null.
    /// </summary>
    public static VariableFilletFeatureModel? ParseVariableFillet(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != VariableFilletClassName || typeId != VariableFilletTypeId) return null;
        if (!FeatureWindowContext.TryCreate(doc, start, end, 0x70, out var ctx) || ctx == null)
            return null;

        var feature = ctx.Nodes.FirstOrDefault(e => e.ClassName == VariableFilletClassName &&
                                                    e.TagOffset == start);
        if (feature == null) return null;
        if (ctx.NextTagOffset(feature) - feature.DataStart != VariableFilletSegments.HeaderExpectedLength)
            return null;

        // The variable layout uses a vertex-control-point structure; constant fillet objects
        // (moCompFace_c / edgeRadiusObject_c / Fillet_c) must be absent.
        if (ctx.Nodes.Any(e => e.ClassName == "moCompFace_c" ||
                               e.ClassName == "edgeRadiusObject_c" ||
                               e.ClassName == FilletClassName ||
                               e.ClassName == ChamferClassName))
            return null;

        if (!ctx.HasSingleNodeWithLength("moSurfaceIdRep_c", VariableFilletSegments.SurfaceIdRepLength) ||
            !ctx.HasSingleNodeWithLength("moFilletSurfIdRep_c", VariableFilletSegments.FilletSurfIdRepLength) ||
            !ctx.HasSingleNodeWithLength("moEdgeRef_c", VariableFilletSegments.EdgeRefLength) ||
            !ctx.HasSingleNodeWithLength("moEdgeDim_c", 0) ||
            !ctx.HasSingleNodeWithLength("moCompEdge_c", VariableFilletSegments.CompEdgeLength) ||
            !ctx.HasSingleNodeWithLength("moVertDim_c", 0) ||
            !ctx.HasSingleNodeWithLength("moDisplayRadialDim_c", VariableFilletSegments.DisplayRadialDimLength) ||
            !ctx.HasSingleNodeWithLength("moCompVertex_c", VariableFilletSegments.CompVertexLength) ||
            !ctx.HasExactNodeCount("ThreeDRadiusDim_c", 1) ||
            !ctx.HasExactNodeCount("moVertexRef_c", 1))
            return null;

        var radiusDim = ctx.GetSingleNode("ThreeDRadiusDim_c")!;
        int radiusStart = radiusDim.DataStart;
        int radiusEnd = radiusStart + VariableFilletSegments.ThreeDRadiusExpectedLength;
        if (radiusStart < 0 || radiusEnd > end || radiusEnd > doc.SourceData.Length) return null;
        if (!MfcPrimitives.IsMfcTag(doc.SourceData, radiusEnd)) return null;
        int radiusCursor = radiusEnd;
        var radiusNext = MfcPrimitives.ReadMfcTag(doc.SourceData, ref radiusCursor);
        if (radiusNext == null || radiusNext.ClassName != "moCompVertex_c") return null;

        byte[] radiusRaw = MfcPrimitives.CopyRange(doc.SourceData, radiusStart, radiusEnd);
        if (radiusRaw.Length != VariableFilletSegments.ThreeDRadiusExpectedLength) return null;
        double? firstRadius = MfcParameterReader.ReadNamedDouble(
            radiusRaw, VariableFilletSegments.ThreeDRadiusParameterOffset,
            VariableFilletSegments.FirstParameterName, requireDNumberFormat: true);
        if (!IsPositiveFinite(firstRadius)) return null;
        if (!MfcParameterReader.VerifyDoubleSlots(radiusRaw, VariableFilletSegments.FirstRadiusPositiveSlots, null, firstRadius!.Value))
            return null;

        var vertexRef = ctx.GetSingleNode("moVertexRef_c")!;
        int vertexStart = vertexRef.DataStart;
        int vertexEnd = vertexStart + VariableFilletSegments.VertexRefExpectedLength;
        if (vertexStart < 0 || vertexEnd > end || vertexEnd > doc.SourceData.Length) return null;
        if (vertexEnd != end && !MfcPrimitives.IsMfcTag(doc.SourceData, vertexEnd)) return null;

        byte[] vertexRaw = MfcPrimitives.CopyRange(doc.SourceData, vertexStart, vertexEnd);
        if (vertexRaw.Length != VariableFilletSegments.VertexRefExpectedLength) return null;
        double? secondRadius = MfcParameterReader.ReadNamedDouble(
            vertexRaw, VariableFilletSegments.VertexRefParameterOffset,
            VariableFilletSegments.SecondParameterName, requireDNumberFormat: true);
        if (!IsPositiveFinite(secondRadius)) return null;
        if (!MfcParameterReader.VerifyDoubleSlots(vertexRaw, VariableFilletSegments.SecondRadiusPositiveSlots, null, secondRadius!.Value))
            return null;

        int negativeSlot = VariableFilletSegments.VertexRefSecondRadiusHalfNegativeSlot;
        if (negativeSlot < 0 || negativeSlot + 8 > vertexRaw.Length) return null;
        if (Math.Abs(BitConverter.ToDouble(vertexRaw, negativeSlot) + secondRadius.Value / 2.0) > 1e-12)
            return null;

        var definition = new VariableFilletFeatureModel();
        definition.SetControlPointRadii(new[] { firstRadius.Value, secondRadius.Value });
        if (!ctx.TryReadSingleEdgeReference(out var edgeReference) || edgeReference == null) return null;
        definition.Edges = new[] { edgeReference };
        definition.MarkResolvedInternal(nameof(VariableFilletFeatureModel.ControlPointRadii));
        definition.MarkResolvedInternal(nameof(VariableFilletFeatureModel.ControlPointCount));
        definition.MarkResolvedInternal(nameof(VariableFilletFeatureModel.Edges));
        return definition;
    }

    /// <summary>
    /// Parses the controlled distance-distance and angle-distance chamfer layouts
    /// (<c>Chamfer_c</c>, FolderHeader TypeId 0x52, stream 0x70). Only the D1/D3 distances,
    /// the D2 angle and the mode-derived Type/EqualDistance flags are populated; edge
    /// selections and all other fields remain unresolved.
    /// </summary>
    public static ChamferFeatureModel? ParseChamfer(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (doc == null) throw new ArgumentNullException(nameof(doc));
        if (className != ChamferClassName || typeId != ChamferTypeId) return null;
        if (!FeatureWindowContext.TryCreate(doc, start, end, 0x70, out var ctx) || ctx == null)
            return null;

        var compEdgeElement = ctx.GetSingleNode("moCompEdge_c");
        if (compEdgeElement == null ||
            !ctx.Nodes.Any(e => e.ClassName == "moSurfaceIdRep_c") ||
            !ctx.Nodes.Any(e => e.ClassName == "moFilletSurfIdRep_c"))
            return null;
        if (!ctx.TryReadSingleEdgeReference(out var edgeReference) || edgeReference == null)
            return null;

        byte[]? compEdge = ctx.SlicePayload(compEdgeElement);
        if (compEdge == null) return null;
        int compEdgeLength = compEdge.Length;

        double? distance0 = MfcParameterReader.ReadNamedDouble(
            compEdge, ChamferSegments.DistanceParameterOffset, requireDNumberFormat: true);
        if (!IsPositiveFinite(distance0)) return null;
        if (!MfcParameterReader.VerifyDoubleSlots(
                compEdge, ChamferSegments.DistancePositiveSlots, ChamferSegments.DistanceNegativeSlots, distance0!.Value))
            return null;

        var definition = new ChamferFeatureModel
        {
            EdgeChamferDistanceSide0 = distance0.Value,
            Edges = new[] { edgeReference }
        };
        definition.MarkResolvedInternal(nameof(ChamferFeatureModel.EdgeChamferDistanceSide0));
        definition.MarkResolvedInternal(nameof(ChamferFeatureModel.Edges));

        if (compEdgeLength == ChamferSegments.ExpectedCompEdgeLengthDistanceDistance)
        {
            if (ctx.Nodes.Any(e => e.ClassName == "moAngleParameter_c")) return null;
            double? distance1 = MfcParameterReader.ReadNamedDouble(
                compEdge, ChamferSegments.SecondDistanceParameterOffset, requireDNumberFormat: true);
            if (!IsPositiveFinite(distance1)) return null;
            if (!MfcParameterReader.VerifyDoubleSlots(
                    compEdge, ChamferSegments.SecondDistancePositiveSlots, ChamferSegments.SecondDistanceNegativeSlots, distance1!.Value))
                return null;

            definition.EdgeChamferDistanceSide1 = distance1.Value;
            definition.Type = 2; // native IChamferFeatureData2.Type for distance-distance
            definition.MarkResolvedInternal(nameof(ChamferFeatureModel.EdgeChamferDistanceSide1));
            definition.MarkResolvedInternal(nameof(ChamferFeatureModel.Type));
            if (Math.Abs(distance0.Value - distance1.Value) > 1e-12)
            {
                definition.EqualDistance = false;
                definition.MarkResolvedInternal(nameof(ChamferFeatureModel.EqualDistance));
            }
            return definition;
        }

        if (compEdgeLength == ChamferSegments.ExpectedCompEdgeLengthAngleDistance)
        {
            var angleParam = ctx.GetSingleNode("moAngleParameter_c");
            if (angleParam == null) return null;
            byte[]? angleRaw = ctx.SlicePayload(angleParam);
            if (angleRaw == null || angleRaw.Length != ChamferSegments.ExpectedAngleParamLength) return null;

            double? angle = MfcParameterReader.ReadNamedDouble(
                angleRaw, ChamferSegments.AngleParameterOffset, requireDNumberFormat: true);
            if (!angle.HasValue || double.IsNaN(angle.Value) || double.IsInfinity(angle.Value) ||
                angle.Value <= 0.0 || angle.Value >= Math.PI / 2.0)
                return null;
            if (!MfcParameterReader.VerifyDoubleSlots(angleRaw, ChamferSegments.AngleSlots, null, angle.Value))
                return null;
            if (Math.Abs(BitConverter.ToDouble(angleRaw, ChamferSegments.AngleParameterDistanceCopySlot) -
                         distance0.Value) > 1e-12)
                return null;

            definition.EdgeChamferAngle = angle.Value;
            definition.Type = 1; // native IChamferFeatureData2.Type for angle-distance
            definition.EqualDistance = false; // measured native snapshot for this layout
            definition.MarkResolvedInternal(nameof(ChamferFeatureModel.EdgeChamferAngle));
            definition.MarkResolvedInternal(nameof(ChamferFeatureModel.Type));
            definition.MarkResolvedInternal(nameof(ChamferFeatureModel.EqualDistance));
            return definition;
        }

        return null;
    }

    /// <summary>
    /// Parses the measured Type=2 face fillet (fillet_face_3mm / fillet_face_5mm):
    /// two moFaceBlend item/surface pairs, a two-block moCompFace_c face reference set and the
    /// D1 radius in ThreeDRadiusDim_c. Type=2 is the measured native mapping for this layout;
    /// no scalar type field exists in the Fillet_c header. Radius is limited to the two
    /// controlled 3/5 mm specimens because the 3/5 diff also changes derived tiny-tolerance
    /// fields that are not linearly scalable.
    /// </summary>
    private static FilletFeatureModel? ParseFaceFillet(
        FeatureWindowContext ctx, int start, int end)
    {
        var nodes = ctx.Nodes;
        if (end - start != FaceFilletSegments.BlockLength) return null;
        var feature = nodes.FirstOrDefault(e =>
            e.ClassName == FilletClassName && e.TagOffset == start);
        if (feature == null) return null;

        // The face layout cannot contain Type=1 edge/blend objects or a generated-face wrapper.
        foreach (var e in nodes)
        {
            if (e.ClassName == "moEdgeRef_c" || e.ClassName == "moCompEdge_c" ||
                e.ClassName == "edgeRadiusObject_c" || e.ClassName == "moSurfaceIdRep_c" ||
                e.ClassName == "moFilletSurfIdRep_c" || e.ClassName == "moFaceRef_c" ||
                e.ClassName == "moSingleFaceRef_w")
                return null;
        }

        var item = ctx.GetSingleNode("moFaceBlendItem_c");
        var compFaceNode = ctx.GetSingleNode("moCompFace_c");
        var radiusDim = ctx.GetSingleNode("ThreeDRadiusDim_c");
        if (item == null || compFaceNode == null || radiusDim == null ||
            !ctx.HasSingleNodeWithLength("moFaceBlendSurfIdRep_c", FaceFilletSegments.BlendSurfIdRepLength) ||
            !ctx.HasSingleNodeWithLength("moFaceBlendItemPair_c", FaceFilletSegments.BlendItemPairLength) ||
            !ctx.HasSingleNodeWithLength("moDisplayRadialDim_c", FaceFilletSegments.DisplayRadialDimLength) ||
            nodes.Count != 7)
            return null;

        byte[]? header = ctx.SlicePayload(feature);
        if (header == null ||
            !TryReadFilletHeader(header, out int restStart) ||
            header.Length - restStart != FaceFilletSegments.HeaderRestLength)
            return null;

        byte[]? itemPayload = ctx.SlicePayload(item);
        if (itemPayload == null || !TryReadBlendItemFaceIds(itemPayload))
            return null;

        byte[]? compFaceRaw = ctx.SlicePayload(compFaceNode);
        if (compFaceRaw == null || compFaceRaw.Length != FaceFilletSegments.CompFaceLength)
            return null;
        var compFace = PersistentFaceReferenceParser.ReadCompFacePayload(compFaceRaw);
        if (compFace == null || compFace.Layout != CompFaceLayout.Full ||
            compFace.Blocks.Count != FaceFilletSegments.CompFaceBlockCount)
            return null;

        uint[] expectedIds = { FaceFilletSegments.FirstFaceId, FaceFilletSegments.SecondFaceId };
        byte[] expectedFlags = { 0x00, 0x40 };
        var faceRefs = new List<FeatureReferenceData>(expectedIds.Length);
        for (int i = 0; i < expectedIds.Length; i++)
        {
            var block = compFace.Blocks[i];
            var faceRef = block.FaceReference;
            if (block.BlockLength != FaceFilletSegments.CompFaceBlockLength ||
                block.HeaderFlag != expectedFlags[i] ||
                faceRef.FaceCount != FaceFilletSegments.FaceCount ||
                faceRef.ClassWord != FaceFilletSegments.FaceClassWord ||
                faceRef.FaceId != expectedIds[i] ||
                faceRef.SurfaceClass != FaceFilletSegments.SurfaceClass ||
                faceRef.MappingClassReference != FaceFilletSegments.MappingClass ||
                !string.Equals(faceRef.ArchiveIdHex, FaceFilletSegments.ArchiveIdHex,
                    StringComparison.OrdinalIgnoreCase) ||
                block.Mappings.Count != 1)
                return null;
            faceRefs.Add(new FeatureReferenceData
            {
                EntityType = SelectionEntityType.Face,
                EntityId = faceRef.FaceId,
                ClassWord = faceRef.ClassWord,
                ArchiveGuid = FaceFilletSegments.ArchiveGuid,
                RecordCount = checked((int)faceRef.FaceCount),
                ReferenceName = $"FaceFilletFace{i + 1}"
            });
        }

        byte[]? radiusRaw = ctx.SlicePayload(radiusDim);
        if (radiusRaw == null || radiusRaw.Length != FaceFilletSegments.ThreeDRadiusLength)
            return null;
        if (BitConverter.ToUInt32(radiusRaw, 0) != 1) return null; // face layout discriminator

        double? radius = MfcParameterReader.ReadNamedDouble(
            radiusRaw, FaceFilletSegments.ThreeDRadiusParameterOffset, requireDNumberFormat: true);
        if (!IsPositiveFinite(radius) || !FaceFilletSegments.IsMeasuredRadius(radius!.Value))
            return null;
        if (Math.Abs(BitConverter.ToDouble(radiusRaw, FaceFilletSegments.ThreeDRadiusParameterValueOffset) -
                     radius.Value) > 1e-12)
            return null;
        if (!MfcParameterReader.VerifyDoubleSlots(
                radiusRaw, FaceFilletSegments.PositiveRadiusSlots, FaceFilletSegments.NegativeRadiusSlots, radius.Value))
            return null;
        if (Math.Abs(BitConverter.ToDouble(radiusRaw, FaceFilletSegments.NegativeHalfRadiusSlot) +
                     radius.Value / 2.0) > 1e-12)
            return null;
        double tolerance = FaceFilletSegments.ToleranceFor(radius.Value);
        if (BitConverter.DoubleToInt64Bits(
                BitConverter.ToDouble(radiusRaw, FaceFilletSegments.ToleranceSlot1)) !=
            BitConverter.DoubleToInt64Bits(tolerance) ||
            BitConverter.DoubleToInt64Bits(
                BitConverter.ToDouble(radiusRaw, FaceFilletSegments.ToleranceSlot2)) !=
            BitConverter.DoubleToInt64Bits(tolerance))
            return null;
        byte[] tiny = FaceFilletSegments.TinyFor(radius.Value);
        if (!radiusRaw.AsSpan(FaceFilletSegments.TinySlot1, 8).SequenceEqual(tiny) ||
            !radiusRaw.AsSpan(FaceFilletSegments.TinySlot2, 8).SequenceEqual(tiny))
            return null;

        var definition = new FilletFeatureModel
        {
            Type = FaceFilletSegments.NativeType,
            DefaultRadius = radius.Value,
            FaceReferences = faceRefs.AsReadOnly()
        };
        definition.MarkResolvedInternal(nameof(FilletFeatureModel.Type));
        definition.MarkResolvedInternal(nameof(FilletFeatureModel.DefaultRadius));
        definition.MarkResolvedInternal(nameof(FilletFeatureModel.FaceReferences));
        return definition;
    }

    private static bool TryReadFilletHeader(byte[] payload, out int restStart)
    {
        restStart = 0;
        if (payload.Length < 2 || BitConverter.ToUInt16(payload, 0) != 0x8004) return false;
        int cursor = 2;
        string? name = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (string.IsNullOrEmpty(name)) return false;
        if (cursor + 8 + 4 + 4 + 4 > payload.Length) return false;
        double headerDouble = BitConverter.ToDouble(payload, cursor);
        if (Math.Abs(Math.Abs(headerDouble) - 2.0) > 1e-12) return false;
        cursor += 8;
        if (BitConverter.ToUInt32(payload, cursor) != FaceFilletSegments.HeaderTypeId) return false;
        cursor += 4;
        if (BitConverter.ToUInt32(payload, cursor) != 0) return false;
        cursor += 4;
        string? empty = MfcPrimitives.ReadWChar(payload, ref cursor);
        if (!string.IsNullOrEmpty(empty)) return false;
        restStart = cursor;
        return true;
    }

    private static bool TryReadBlendItemFaceIds(byte[] payload)
    {
        if (payload.Length != FaceFilletSegments.BlendItemLength) return false;
        var ids = new List<uint>();
        for (int i = 0; i + 40 <= payload.Length; i++)
        {
            if (BitConverter.ToUInt16(payload, i) != FaceFilletSegments.BlendItemMarker ||
                BitConverter.ToUInt16(payload, i + 2) != 1 ||
                BitConverter.ToUInt32(payload, i + 4) != 0 ||
                BitConverter.ToUInt16(payload, i + 8) != 0) continue;
            if (BitConverter.ToUInt32(payload, i + 10) != FaceFilletSegments.FaceCount ||
                BitConverter.ToUInt32(payload, i + 14) != FaceFilletSegments.BlendItemClassWord ||
                BitConverter.ToUInt16(payload, i + 38) != 0) return false;
            string guid = BitConverter.ToString(payload, i + 22, 16)
                .Replace("-", string.Empty).ToLowerInvariant();
            if (guid != FaceFilletSegments.ArchiveGuid) return false;
            uint id = BitConverter.ToUInt32(payload, i + 18);
            if (id == 0) return false;
            ids.Add(id);
            i += 39;
        }
        return ids.Count == 2 &&
               ids[0] == FaceFilletSegments.FirstFaceId &&
               ids[1] == FaceFilletSegments.SecondFaceId;
    }

    private static bool IsPositiveFinite(double? value) =>
        value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value.Value > 0.0;
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FaceFilletAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// One measured face-fillet template base (fillet_face_3mm/5mm, source or resave).
/// The complete 3190-byte Fillet_c block is carried verbatim; radius, face pair, face
/// records and session context are fixed to the native specimen.
/// </summary>
internal sealed class FaceFilletAuthoringTemplate
{
    internal FaceFilletAuthoringTemplate(
        double radius, int baseLength, string baseSha256, int strippedFolderCount,
        ushort streamVersion, byte[] block, double expectedVolume, double expectedSurfaceArea)
    {
        Radius = radius;
        BaseLength = baseLength;
        BaseSha256 = baseSha256;
        StrippedFolderCount = strippedFolderCount;
        StreamVersion = streamVersion;
        Block = block;
        ExpectedVolume = expectedVolume;
        ExpectedSurfaceArea = expectedSurfaceArea;
    }

    internal double Radius { get; }
    internal int BaseLength { get; }
    internal string BaseSha256 { get; }
    internal int StrippedFolderCount { get; }
    internal ushort StreamVersion { get; }
    internal byte[] Block { get; }
    internal double ExpectedVolume { get; }
    internal double ExpectedSurfaceArea { get; }
}

/// <summary>
/// Fixed measured face-fillet authoring templates. All four bases use the native feature
/// name "FaceFillet", stream version 0x70, folder count 21 (stripped 20), the single 0x67
/// marker at 0x2347 with value 1, and a 3190-byte measured feature block. The 3 mm and 5 mm
/// feature blocks differ in radius copies and two derived tiny-tolerance fields; no other
/// radius value is established because those derived slots do not scale linearly.
/// </summary>
internal static class FaceFilletAuthoringTemplates
{
    internal const string FeatureName = "FaceFillet";
    internal const double Radius3mm = 0.003;
    internal const double Radius5mm = 0.005;
    internal const int StrippedFolderCount = 20;
    internal const ushort StreamVersion = 0x70;
    internal const int BlockLength = 3190;

    internal static readonly FaceFilletAuthoringTemplate Source3mm = new FaceFilletAuthoringTemplate(
        Radius3mm, 12266, "0fd5b5b8653d26ded1f13e23fa9dc87d3d56c40d6ed6d23b58301f8dc76b5dd9", 20, 0x70,
        FilletAuthoringSegments.FaceFillet0, 3.980685834705771e-6, 0.0015832610567450011);

    internal static readonly FaceFilletAuthoringTemplate Resave3mm = new FaceFilletAuthoringTemplate(
        Radius3mm, 12562, "5afed93221e4db8f91e4e5370ffcfcb290e6c99b3adeeeada275882fd68253fc", 20, 0x70,
        FilletAuthoringSegments.FaceFillet1, 3.980685834705771e-6, 0.0015832610567450011);

    internal static readonly FaceFilletAuthoringTemplate Source5mm = new FaceFilletAuthoringTemplate(
        Radius5mm, 12266, "6f34ee2f8f7e727458fafb801153d6ced1c7407d587e6bd7311d33a1af9c98ea", 20, 0x70,
        FilletAuthoringSegments.FaceFillet2, 3.946349540849362e-6, 0.001567809724509617);

    internal static readonly FaceFilletAuthoringTemplate Resave5mm = new FaceFilletAuthoringTemplate(
        Radius5mm, 12546, "972c8f031ce2c8f8ddf544b64e136e713ab50308feb645626573f0abccf42f92", 20, 0x70,
        FilletAuthoringSegments.FaceFillet3, 3.946349540849362e-6, 0.001567809724509617);

    internal static readonly FaceFilletAuthoringTemplate[] All =
    {
        Source3mm, Resave3mm, Source5mm, Resave5mm
    };
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FaceFilletFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed measured Type=2 face-fillet authoring. Supports only the controlled 3 mm and 5 mm
/// two-face specimens on their exact source/resave stripped bases. The complete 3190-byte
/// native block is appended verbatim, so radius and face records are byte-identical to the
/// measured specimen. General radius parameterization is not exposed: the 3/5 mm diff also
/// changes derived tiny-tolerance slots that do not scale linearly.
/// </summary>
internal static class FaceFilletFeatureBuilder
{
    internal static void ValidateDefinition(FilletFeatureModel definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (definition.Type != FaceFilletSegments.NativeType)
        {
            throw new NotSupportedException(
                "Face-fillet authoring requires Fillet Type = 2. No stream changes have been made.");
        }
        double radius = definition.DefaultRadius ??
            throw new ArgumentOutOfRangeException(nameof(definition.DefaultRadius),
                "Face-fillet authoring requires the measured 0.003 or 0.005 m radius.");
        if (double.IsNaN(radius) || double.IsInfinity(radius) ||
            !FaceFilletSegments.IsMeasuredRadius(radius))
        {
            throw new NotSupportedException(
                "Face-fillet authoring is fixed to the measured 0.003 m and 0.005 m radii; " +
                "other radii are not established by the derived-tolerance slots. " +
                "No stream changes have been made.");
        }

        if (definition.FaceReferences != null && definition.FaceReferences.Count > 0)
        {
            if (definition.FaceReferences.Count != 2)
            {
                throw new NotSupportedException(
                    "Face-fillet authoring requires exactly the two measured faces. " +
                    "No stream changes have been made.");
            }
            uint[] expectedIds = { FaceFilletSegments.FirstFaceId, FaceFilletSegments.SecondFaceId };
            for (int i = 0; i < expectedIds.Length; i++)
            {
                var face = definition.FaceReferences[i];
                if (face == null || face.EntityType != SelectionEntityType.Face ||
                    face.EntityId != expectedIds[i] ||
                    face.ClassWord != FaceFilletSegments.FaceClassWord ||
                    !string.Equals(face.ArchiveGuid, FaceFilletSegments.ArchiveGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException(
                        "Face-fillet authoring carries the measured face pair in serialized order " +
                        "(0x5D2 then 0x5CE, class word 0x300); face overrides are rejected. " +
                        "No stream changes have been made.");
                }
            }
        }

        foreach (var property in typeof(FilletFeatureModel)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == nameof(FilletFeatureModel.Type) ||
                property.Name == nameof(FilletFeatureModel.DefaultRadius) ||
                property.Name == nameof(FilletFeatureModel.FaceReferences) ||
                property.Name == nameof(IFeatureData.ClassName) ||
                property.Name == nameof(IFeatureData.TypeId) ||
                property.Name == nameof(MfcFeatureModelBase.ResolvedProperties) ||
                property.Name == nameof(FilletFeatureModel.FeatureName) ||
                property.Name == nameof(FilletFeatureModel.Version) ||
                property.Name == nameof(FilletFeatureModel.Radius) ||
                property.Name == nameof(FilletFeatureModel.HeaderSuffix) ||
                property.Name == nameof(FilletFeatureModel.SurfaceIdRep) ||
                property.Name == nameof(FilletFeatureModel.FilletSurfIdRep) ||
                property.Name == nameof(FilletFeatureModel.EdgeRef) ||
                property.Name == nameof(FilletFeatureModel.CompEdge) ||
                property.Name == nameof(FilletFeatureModel.DisplayRadialDim) ||
                property.Name == nameof(FilletFeatureModel.RadiusDim) ||
                property.Name == nameof(FilletFeatureModel.CompFace) ||
                property.Name == nameof(FilletFeatureModel.Parameter) ||
                property.Name == nameof(FilletFeatureModel.DisplayDimension) ||
                property.Name == nameof(FilletFeatureModel.Dimension) ||
                property.Name == nameof(FilletFeatureModel.Dimensions))
                continue;
            if (property.GetValue(definition) != null)
            {
                throw new NotSupportedException(
                    $"Face-fillet authoring does not support {property.Name}. " +
                    "No stream changes have been made.");
            }
        }
    }

    internal static byte[] BuildFaceFilletStream(
        byte[] currentStream, FilletFeatureModel definition, string featureName)
    {
        ValidateDefinition(definition);
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        double radius = definition.DefaultRadius!.Value;
        var template = SelectTemplate(currentStream, radius);
        if (!string.Equals(featureName, FaceFilletAuthoringTemplates.FeatureName,
                StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Face-fillet authoring uses the measured native feature name " +
                $"'{FaceFilletAuthoringTemplates.FeatureName}'; a custom name would resize the " +
                "serialized block. No stream changes have been made.");
        }

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires ResolvedFeatures version " +
                $"0x{template.StreamVersion:X2}, but the document uses 0x{version:X2}. " +
                "No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires the stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        }
        if (FindSingleSketchMarker(currentStream) < 0)
        {
            throw new NotSupportedException(
                "Face-fillet authoring requires the base's single measured 0x67 sketch " +
                "child-count marker with value 1. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires the exact measured fillet_face_{radius * 1000:0}mm " +
                $"stripped base (normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        if (block.Length != FaceFilletAuthoringTemplates.BlockLength)
            throw new InvalidOperationException("Face-fillet measured block length mismatch.");
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static FaceFilletAuthoringTemplate SelectTemplate(byte[] currentStream, double radius)
    {
        var candidates = FaceFilletAuthoringTemplates.All
            .Where(t => Math.Abs(t.Radius - radius) <= 1e-12 &&
                        t.BaseLength == currentStream.Length)
            .ToList();
        if (candidates.Count == 0)
        {
            var lengths = string.Join(", ", FaceFilletAuthoringTemplates.All
                .Where(t => Math.Abs(t.Radius - radius) <= 1e-12)
                .Select(t => t.BaseLength));
            throw new NotSupportedException(
                $"Face-fillet authoring for radius {radius:R} m requires one of the measured " +
                $"stripped base lengths [{lengths}]; the document stream is {currentStream.Length} " +
                "bytes. No stream changes have been made.");
        }
        string sha = NormalizedBaseSha(currentStream);
        var match = candidates.FirstOrDefault(t =>
            string.Equals(t.BaseSha256, sha, StringComparison.Ordinal));
        if (match == null)
        {
            throw new NotSupportedException(
                $"Face-fillet authoring requires an exact measured base signature for radius " +
                $"{radius:R} m; {sha.Substring(0, 12)} does not match the controlled " +
                "source/resave bases. No stream changes have been made.");
        }
        return match;
    }

    private static int FindSingleSketchMarker(byte[] stream)
    {
        int found = -1;
        for (int i = 0; i <= stream.Length - 8; i++)
        {
            if (stream[i] != 0x67 || stream[i + 1] != 0 || stream[i + 2] != 0 || stream[i + 3] != 0)
                continue;
            if (BitConverter.ToUInt32(stream, i + 4) != 1) return -1;
            if (found >= 0) return -1;
            found = i;
        }
        return found;
    }

    private static string NormalizedBaseSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized))
            .Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/FaceFilletSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured SW2026 face-fillet layout (<c>Fillet_c</c>, FolderHeader TypeId 0x51, stream 0x70,
/// native ISimpleFilletFeatureData2.Type = 2). Two selected faces are serialized as two
/// moFaceBlend item/surface pairs and a two-block moCompFace_c; the radius is the D1 named
/// parameter in ThreeDRadiusDim_c plus its measured display copies.
///
/// Measured block (3190 bytes, fillet_face_3mm / fillet_face_5mm, source and resave):
///   Fillet_c 309 | moFaceBlendSurfIdRep_c 20 | moFaceBlendItemPair_c 0 |
///   moFaceBlendItem_c 354 | moCompFace_c 536 | (second pair) |
///   moDisplayRadialDim_c 665 | ThreeDRadiusDim_c 1147.
/// </summary>
internal static class FaceFilletSegments
{
    internal const string ClassName = "Fillet_c";
    internal const uint HeaderTypeId = 0x51u;
    internal const ushort StreamVersion = 0x70;
    internal const int NativeType = 2;

    internal const int BlockLength = 3190;
    internal const int HeaderRestLength = 263;
    internal const int BlendSurfIdRepLength = 20;
    internal const int BlendItemPairLength = 0;
    internal const int BlendItemLength = 354;
    internal const int CompFaceLength = 536;
    internal const int DisplayRadialDimLength = 665;
    internal const int ThreeDRadiusLength = 1147;

    internal const int CompFaceBlockCount = 2;
    internal const int CompFaceBlockLength = 268;
    internal const uint FaceCount = 6;
    internal const uint FaceClassWord = 0x300u;
    internal const uint BlendItemClassWord = 0x200u;
    internal const ushort SurfaceClass = 0x80E6;
    internal const ushort MappingClass = 0x80F1;
    internal const ushort BlendItemMarker = 0x80E6;
    internal const uint FirstFaceId = 0x5D2u;
    internal const uint SecondFaceId = 0x5CEu;
    internal const string ArchiveGuid = "7dc39425ad49b2547dc39425ad49b254";
    internal const string ArchiveIdHex = "54B249AD2594C37D54B249AD2594C37D";

    internal const int ThreeDRadiusParameterOffset = 6;
    internal const int ThreeDRadiusParameterValueOffset = 0x26;
    internal static readonly int[] PositiveRadiusSlots = { 0x26, 0x46, 0x4E, 0x5E, 0x66, 0xF6, 0x1E0, 0x1F8 };
    internal static readonly int[] NegativeRadiusSlots = { 0x93, 0x9B, 0x3B8, 0x3D0 };
    internal const int NegativeHalfRadiusSlot = 0xEE;

    internal const int ToleranceSlot1 = 0xD6;
    internal const int ToleranceSlot2 = 0x198;
    internal const int TinySlot1 = 0x3C0;
    internal const int TinySlot2 = 0x3D8;
    internal const double Tolerance3mm = 2.8912057932946783e-16;
    internal const double Tolerance5mm = 0.0;
    internal static readonly byte[] Tiny3mm = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x3C };
    internal static readonly byte[] Tiny5mm = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x3C };

    internal static bool IsMeasuredRadius(double radius) =>
        System.Math.Abs(radius - 0.003) <= 1e-12 || System.Math.Abs(radius - 0.005) <= 1e-12;

    internal static double ToleranceFor(double radius) =>
        System.Math.Abs(radius - 0.003) <= 1e-12 ? Tolerance3mm : Tolerance5mm;

    internal static byte[] TinyFor(double radius) =>
        System.Math.Abs(radius - 0.003) <= 1e-12 ? Tiny3mm : Tiny5mm;
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/VariableFilletAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured VarFillet_c feature block and stripped-base signature. The block starts at the class
/// tag and runs to the end of the native ResolvedFeatures stream; the base is truncated there with
/// the header folder count and the preceding 0x67 value zeroed for signature normalization.
/// </summary>
internal sealed class VariableFilletAuthoringTemplate
{
    internal VariableFilletAuthoringTemplate(string name, string baseSha256, int baseLength,
        ushort streamVersion, int strippedFolderCount, int markerValue, byte[] block)
    {
        Name = name;
        BaseSha256 = baseSha256;
        BaseLength = baseLength;
        StreamVersion = streamVersion;
        StrippedFolderCount = strippedFolderCount;
        MarkerValue = markerValue;
        Block = block;
    }

    internal string Name { get; }
    internal string BaseSha256 { get; }
    internal int BaseLength { get; }
    internal ushort StreamVersion { get; }
    internal int StrippedFolderCount { get; }
    internal int MarkerValue { get; }
    internal byte[] Block { get; }
}

internal static class VariableFilletAuthoringTemplates
{
    /// <summary>source measured variable fillet template from fillet_variable_2_4mm.resolved.bin.</summary>
    internal static readonly VariableFilletAuthoringTemplate Source = new VariableFilletAuthoringTemplate(
        "VariableFillet",
        "daf1db073c790910046f92b92082e02a08745d453b7ab4972822bf3b4f5646dd",
        12266,
        0x70,
        20,
        0,
        FilletAuthoringSegments.VariableFillet0);

    /// <summary>native resave measured variable fillet template from fillet_variable_2_4mm_resave.resolved.bin.</summary>
    internal static readonly VariableFilletAuthoringTemplate Resave = new VariableFilletAuthoringTemplate(
        "VariableFillet",
        "d4bac8691411de73f625c182ead348720e9950261ccd49287fbfbee1cb7da30e",
        12546,
        0x70,
        20,
        0,
        FilletAuthoringSegments.VariableFillet1);

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/VariableFilletFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fixed-measurement template authoring for the controlled variable-radius fillet
/// (VarFillet_c / TypeId 0x52, one edge, two control points). Supports the measured
/// 1/3, 2/4 and 3/5 mm pairs on their exact native source/resave bases, plus the legacy
/// 2/4 mm base. The complete measured block carries edge/vertex/control-point context and
/// every radius copy; no slot is scaled because the pair diffs also change derived fields.
/// </summary>
internal static class VariableFilletFeatureBuilder
{
    internal static string TemplateName => VariableFilletAuthoringTemplates.Source.Name;

    internal static void ValidateDefinition(VariableFilletFeatureModel definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        var radii = definition.ControlPointRadii;
        if (radii == null || radii.Count != 2 || !IsMeasuredPair(radii[0], radii[1]))
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring is fixed to the measured control-point pairs " +
                "1/3 mm, 2/4 mm or 3/5 mm. No stream changes have been made.");
        }
        if (definition.FilletEdgeCount.HasValue && definition.FilletEdgeCount.Value != 1)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring supports the measured single edge only. " +
                "No stream changes have been made.");
        }
        if (definition.Edges != null && definition.Edges.Count > 0)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring does not accept edge overrides; the measured " +
                "edge reference is carried by the template. No stream changes have been made.");
        }
        if (definition.DefaultRadius.HasValue || definition.DefaultDistance.HasValue ||
            definition.OverflowType.HasValue || definition.TransitionType.HasValue ||
            definition.PropagateToTangentFaces.HasValue || definition.PropagateFeatureToParts.HasValue ||
            definition.ConicTypeForCrossSectionProfile.HasValue || definition.DefaultConicRhoOrRadius.HasValue ||
            definition.AsymmetricFillet.HasValue || definition.CurvatureContinuous.HasValue)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring does not accept default/transition/vertex overrides " +
                "in this measured slice. No stream changes have been made.");
        }
    }

    internal static byte[] BuildVariableFilletStream(
        byte[] currentStream, VariableFilletFeatureModel definition, string featureName)
    {
        ValidateDefinition(definition);
        var radii = definition.ControlPointRadii!;
        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));

        var native = FindNativeTemplate(currentStream);
        if (native != null)
        {
            if (Math.Abs(radii[0] - native.Radius0) > 1e-12 ||
                Math.Abs(radii[1] - native.Radius1) > 1e-12)
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring on the measured {native.Radius0 * 1000:0}/" +
                    $"{native.Radius1 * 1000:0} mm base requires that exact measured pair. " +
                    "No stream changes have been made.");
            }
            if (!string.Equals(featureName, VariableFilletNativeAuthoringTemplates.FeatureName,
                    StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring uses the measured native feature name " +
                    $"'{VariableFilletNativeAuthoringTemplates.FeatureName}'. " +
                    "No stream changes have been made.");
            }
            ushort nativeVersion = BitConverter.ToUInt16(currentStream, 0);
            if (nativeVersion != native.StreamVersion)
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring requires ResolvedFeatures version " +
                    $"0x{native.StreamVersion:X2}. No stream changes have been made.");
            }
            if (BitConverter.ToUInt16(currentStream, 4) != native.StrippedFolderCount)
            {
                throw new NotSupportedException(
                    $"Variable-radius fillet authoring requires the stripped folder count " +
                    $"{native.StrippedFolderCount}. No stream changes have been made.");
            }
            if (FindSingleSketchMarker(currentStream) < 0)
            {
                throw new NotSupportedException(
                    "Variable-radius fillet authoring requires the base's single measured 0x67 " +
                    "sketch child-count marker with value 1. No stream changes have been made.");
            }
            return Append(currentStream, native.StrippedFolderCount, native.Block);
        }

        var template = SelectByBaseLength(currentStream);
        if (!string.Equals(featureName, template.Name, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring uses the measured native feature name '{template.Name}'. " +
                $"A custom name '{featureName}' would resize the serialized block. No stream changes have been made.");
        }
        if (currentStream.Length != template.BaseLength)
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires the measured stripped base length " +
                $"{VariableFilletAuthoringTemplates.Source.BaseLength} or " +
                $"{VariableFilletAuthoringTemplates.Resave.BaseLength}; the document stream is " +
                $"{currentStream.Length} bytes. No stream changes have been made.");
        }
        if (Math.Abs(radii[0] - 0.002) > 1e-12 || Math.Abs(radii[1] - 0.004) > 1e-12)
        {
            throw new NotSupportedException(
                "The legacy variable-fillet base supports only the measured 2/4 mm pair. " +
                "No stream changes have been made.");
        }
        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != template.StreamVersion)
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires ResolvedFeatures version 0x{template.StreamVersion:X2}, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }
        if (BitConverter.ToUInt16(currentStream, 4) != template.StrippedFolderCount)
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires the stripped folder count " +
                $"{template.StrippedFolderCount}. No stream changes have been made.");
        }
        int marker = FindSingleSketchMarker(currentStream);
        if (marker < 0)
        {
            throw new NotSupportedException(
                "Variable-radius fillet authoring requires the base's single measured 0x67 sketch " +
                "child-count marker with value 1. No stream changes have been made.");
        }
        string baseSha = NormalizedBaseSha(currentStream);
        if (!string.Equals(baseSha, template.BaseSha256, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Variable-radius fillet authoring requires the exact {template.Name} stripped base " +
                $"(normalized signature {baseSha.Substring(0, 12)} does not match). " +
                "No stream changes have been made.");
        }

        byte[] block = (byte[])template.Block.Clone();
        var newStream = new byte[currentStream.Length + block.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(template.StrippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(block, 0, newStream, currentStream.Length, block.Length);
        return newStream;
    }

    private static bool IsMeasuredPair(double radius0, double radius1) =>
        (Math.Abs(radius0 - 0.001) <= 1e-12 && Math.Abs(radius1 - 0.003) <= 1e-12) ||
        (Math.Abs(radius0 - 0.002) <= 1e-12 && Math.Abs(radius1 - 0.004) <= 1e-12) ||
        (Math.Abs(radius0 - 0.003) <= 1e-12 && Math.Abs(radius1 - 0.005) <= 1e-12);

    private static VariableFilletNativeAuthoringTemplate? FindNativeTemplate(byte[] currentStream)
    {
        string sha = NormalizedBaseSha(currentStream);
        return VariableFilletNativeAuthoringTemplates.All.FirstOrDefault(t =>
            t.BaseLength == currentStream.Length &&
            string.Equals(t.BaseSha256, sha, StringComparison.Ordinal));
    }

    private static byte[] Append(byte[] currentStream, int strippedFolderCount, byte[] block)
    {
        byte[] copy = (byte[])block.Clone();
        var newStream = new byte[currentStream.Length + copy.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(strippedFolderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(copy, 0, newStream, currentStream.Length, copy.Length);
        return newStream;
    }

    private static VariableFilletAuthoringTemplate SelectByBaseLength(byte[] currentStream)
    {
        int length = currentStream?.Length ?? 0;
        if (length == VariableFilletAuthoringTemplates.Source.BaseLength)
            return VariableFilletAuthoringTemplates.Source;
        if (length == VariableFilletAuthoringTemplates.Resave.BaseLength)
            return VariableFilletAuthoringTemplates.Resave;
        throw new NotSupportedException(
            $"Variable-radius fillet authoring requires the measured stripped base length " +
            $"{VariableFilletAuthoringTemplates.Source.BaseLength} or " +
            $"{VariableFilletAuthoringTemplates.Resave.BaseLength}; the document stream is " +
            $"{length} bytes. No stream changes have been made.");
    }

    private static int FindSingleSketchMarker(byte[] stream)
    {
        int found = -1;
        for (int i = 0; i <= stream.Length - 8; i++)
        {
            if (stream[i] != 0x67 || stream[i + 1] != 0 || stream[i + 2] != 0 || stream[i + 3] != 0)
                continue;
            if (BitConverter.ToUInt32(stream, i + 4) != 1) return -1;
            if (found >= 0) return -1;
            found = i;
        }
        return found;
    }

    private static string NormalizedBaseSha(byte[] stream)
    {
        byte[] normalized = (byte[])stream.Clone();
        BitConverter.GetBytes((ushort)0).CopyTo(normalized, 4);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(normalized)).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/VariableFilletNativeAuthoringTemplates.cs
// ------------------------------------------------------------------------
/// <summary>
/// One measured native variable-fillet template (1/3, 2/4 or 3/5 mm pair, source or resave).
/// The complete 4953-byte VarFillet_c block is carried verbatim. A pair-specific full block is
/// required: the 1/3, 2/4 and 3/5 diffs also change derived control-point/session fields that
/// are not linearly scalable from the two radii alone.
/// </summary>
internal sealed class VariableFilletNativeAuthoringTemplate
{
    internal VariableFilletNativeAuthoringTemplate(
        double radius0, double radius1, int baseLength, string baseSha256,
        int strippedFolderCount, ushort streamVersion, byte[] block)
    {
        Radius0 = radius0;
        Radius1 = radius1;
        BaseLength = baseLength;
        BaseSha256 = baseSha256;
        StrippedFolderCount = strippedFolderCount;
        StreamVersion = streamVersion;
        Block = block;
    }

    internal double Radius0 { get; }
    internal double Radius1 { get; }
    internal int BaseLength { get; }
    internal string BaseSha256 { get; }
    internal int StrippedFolderCount { get; }
    internal ushort StreamVersion { get; }
    internal byte[] Block { get; }
}

internal static class VariableFilletNativeAuthoringTemplates
{
    internal const string FeatureName = "VariableFillet";
    internal const int BlockLength = 4953;
    internal const int StrippedFolderCount = 20;
    internal const ushort StreamVersion = 0x70;

    internal static readonly VariableFilletNativeAuthoringTemplate Source1_3 = new VariableFilletNativeAuthoringTemplate(
        0.001, 0.003, 12266, "1fc7c08934fe40e6b2b25fb2a78d2a08346c611ca7a968d7158b11938ef0bb3c", 20, 0x70, FilletAuthoringSegments.VariableFilletNative0);
    internal static readonly VariableFilletNativeAuthoringTemplate Resave1_3 = new VariableFilletNativeAuthoringTemplate(
        0.001, 0.003, 12582, "7e036d7f83b5aec9e441c8673a8f38f69894b62105e9473e1fe30e12453794b8", 20, 0x70, FilletAuthoringSegments.VariableFilletNative1);
    internal static readonly VariableFilletNativeAuthoringTemplate Source2_4 = new VariableFilletNativeAuthoringTemplate(
        0.002, 0.004, 12266, "b4ef95f3100095fbdce7c50cb51be7d7df9a72f7a43e1d8942feb07c7f06b2fa", 20, 0x70, FilletAuthoringSegments.VariableFilletNative2);
    internal static readonly VariableFilletNativeAuthoringTemplate Resave2_4 = new VariableFilletNativeAuthoringTemplate(
        0.002, 0.004, 12590, "adef00616aaabd4ca92a49ecadbbeb753c096128055bcd8a4ccbfde178284c32", 20, 0x70, FilletAuthoringSegments.VariableFilletNative3);
    internal static readonly VariableFilletNativeAuthoringTemplate Source3_5 = new VariableFilletNativeAuthoringTemplate(
        0.003, 0.005, 12266, "3bbeada280e26ee12618bf104fa33c463bb73f1ed6d5923817ea21e53cbb6df0", 20, 0x70, FilletAuthoringSegments.VariableFilletNative4);
    internal static readonly VariableFilletNativeAuthoringTemplate Resave3_5 = new VariableFilletNativeAuthoringTemplate(
        0.003, 0.005, 12582, "0bab527ecab67eaa7ad082c6bb482b751dff40a96a23a46f9f04c70f9681f2e8", 20, 0x70, FilletAuthoringSegments.VariableFilletNative5);

    internal static readonly VariableFilletNativeAuthoringTemplate[] All =
    {
        Source1_3, Resave1_3, Source2_4, Resave2_4, Source3_5, Resave3_5
    };
}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/VariableFilletSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured variable-radius fillet layout from the SW2026
/// <c>FilletOptions/fillet_variable_2_4mm</c> control and its resave:
/// <c>VarFillet_c</c> / FolderHeader TypeId 0x52, one edge and two control-point radii
/// (D0=0.002 m, D01=0.004 m). The radii live in the named-parameter blocks embedded in
/// <c>ThreeDRadiusDim_c</c> (first control point) and <c>moVertexRef_c</c> (second
/// control point plus the negative copy of the first radius).
/// </summary>
internal static class VariableFilletSegments
{
    internal const string ClassName = "VarFillet_c";
    internal const uint TypeId = 0x52u;

    /// <summary>Measured VarFillet_c payload length (main and resave).</summary>
    internal const int HeaderExpectedLength = 311;

    // First control point: ThreeDRadiusDim_c.
    internal const int ThreeDRadiusExpectedLength = 1100;
    internal const int ThreeDRadiusParameterOffset = 6;
    internal const string FirstParameterName = "D0";
    internal static readonly int[] FirstRadiusPositiveSlots =
        { 38, 70, 94, 219, 318, 552, 576, 1024, 1048 };

    // Second control point: the radius parameter is embedded in moVertexRef_c at +830
    // (schema 0x8004) / D01 name at +832 / value at +864.
    internal const int VertexRefExpectedLength = 2228;
    internal const int VertexRefParameterOffset = 830;
    internal const string SecondParameterName = "D01";
    internal static readonly int[] SecondRadiusPositiveSlots =
        { 864, 904, 928, 1045, 1144, 1378, 1402, 1850, 1874 };
    /// <summary>Measured negative copy of half the second radius inside moVertexRef_c (-D01/2).</summary>
    internal const int VertexRefSecondRadiusHalfNegativeSlot = 1136;

    // Surrounding measured objects (presence/length gates for the strict layout).
    internal const int SurfaceIdRepLength = 14;
    internal const int FilletSurfIdRepLength = 20;
    internal const int EdgeRefLength = 146;
    internal const int CompEdgeLength = 162;
    internal const int DisplayRadialDimLength = 665;
    internal const int CompVertexLength = 86;
}


