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
/// Unified strongly-typed model for Chamfer_c edge chamfer feature.
/// Follows the <see cref="IMfcSerializable"/> contract for symmetrical reading and writing.
/// </summary>
public sealed class ChamferFeatureModel : MfcFeatureModelBase, IMfcFeatureModel
{
    public const string MfcClassName = "Chamfer_c";
    public const uint HeaderTypeId = 0x52u;

    public const int DistanceParameterOffset = 833;
    public const int SecondDistanceParameterOffset = 2576;
    public static readonly int[] DistancePositiveSlots = { 865, 905, 1183, 1425, 1449 };
    public static readonly int[] DistanceNegativeSlots = { 1102, 1263, 1287 };
    public static readonly int[] SecondDistancePositiveSlots = { 2608, 2640, 2854, 3096, 3120 };
    public static readonly int[] SecondDistanceNegativeSlots = { 2765, 2926, 2950 };
    public const int AngleParameterOffset = 0;
    public static readonly int[] AngleSlots = { 32, 570 };
    public const int AngleParameterDistanceCopySlot = 120;
    public const int ExpectedCompEdgeLengthDistanceDistance = 3925;
    public const int ExpectedCompEdgeLengthAngleDistance = 1899;
    public const int ExpectedAngleParamLength = 1412;
    public const string BasePrefixSha256DistanceDistance =
        "ad6ceea8e703cf26a97f8fdb78f88bac48d8ac1be95b8415c9bfd65e20d1d4f2";
    public const string BasePrefixSha256AngleDistance =
        "0b676fa95264ef7a0894ba50999636e5de6e7322d3927589c2c54d42e9dc1043";

    public static readonly byte[] HeaderSuffixDistanceDistance = ChamferAuthoringSegments.HeaderSuffixDistanceDistance;
    public static readonly byte[] HeaderSuffixAngleDistance = ChamferAuthoringSegments.HeaderSuffixAngleDistance;
    public static readonly byte[] SurfaceIdRepDataDistanceDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x96, 0x4C, 0xE4, 0x6A
    };
    public static readonly byte[] SurfaceIdRepDataAngleDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x92, 0x4C, 0xE4, 0x6A
    };
    public static readonly byte[] FilletSurfIdRepDataDistanceDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x96, 0x4C, 0xE4, 0x6A, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00
    };
    public static readonly byte[] FilletSurfIdRepDataAngleDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x92, 0x4C, 0xE4, 0x6A, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00
    };
    public static readonly byte[] EdgeRefDataDistanceDistance = ChamferAuthoringSegments.EdgeRefDataDistanceDistance;
    public static readonly byte[] EdgeRefDataAngleDistance = ChamferAuthoringSegments.EdgeRefDataAngleDistance;
    public static readonly byte[] CompEdgeDataDistanceDistance = ChamferAuthoringSegments.CompEdgeDataDistanceDistance;
    public static readonly byte[] CompEdgeDataAngleDistance = ChamferAuthoringSegments.CompEdgeDataAngleDistance;
    public static readonly byte[] DisplayAngularDimData = ChamferAuthoringSegments.DisplayAngularDimData;
    public static readonly byte[] AngleDimData = new byte[] { 0x01, 0x00, 0x00, 0x00 };
    public static readonly byte[] AngleParamData = ChamferAuthoringSegments.AngleParamData;

    public string FeatureName { get; set; } = "倒角1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get; set; } = HeaderTypeId;
    public string ClassName => MfcClassName;
        
    public double? Distance0 { get => EdgeChamferDistanceSide0; set => EdgeChamferDistanceSide0 = value; }
    public double? Distance1 { get => EdgeChamferDistanceSide1; set => EdgeChamferDistanceSide1 = value; }
    public double? Angle { get => EdgeChamferAngle; set => EdgeChamferAngle = value; }
    public bool IsAngleDistance { get => (Type ?? 1) == 1; set => Type = value ? 1 : 2; }
    public double? EdgeChamferDistanceSide0 { get; set; }
    public double? EdgeChamferDistanceSide1 { get; set; }
    public double? EdgeChamferAngle { get; set; }
    public int? Type { get; set; }
    public bool? EqualDistance { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Faces { get; set; }
    public FeatureReferenceData? Vertex { get; set; }
    public int? LoopCount { get; set; }
    public IReadOnlyList<FeatureReferenceData>? Loops { get; set; }
    public FeatureReferenceData? IVertex { get; set; }
    public bool? KeepFeatures { get; set; }
    public bool? TangentPropagation { get; set; }
    public bool? PropagateFeatureToParts { get; set; }
    public double? VertexChamferDistanceSide0 { get; set; }
    public double? VertexChamferDistanceSide1 { get; set; }
    private readonly Dictionary<uint, bool> _flippedEntities = new Dictionary<uint, bool>();
    public bool? GetFlip(uint entityId) => _flippedEntities.TryGetValue(entityId, out var flip) ? flip : (bool?)null;
    public void SetFlip(uint entityId, bool flip) => _flippedEntities[entityId] = flip;
    public IReadOnlyList<FeatureReferenceData>? Edges { get; set; }

    public OpaqueObject? HeaderSuffix { get; set; }
    public OpaqueObject? SurfaceIdRep { get; set; }
    public OpaqueObject? FilletSurfIdRep { get; set; }
    public OpaqueObject? EdgeRef { get; set; }

    public ChamferFeatureModel() { }

    public ChamferFeatureModel(bool angleDistance)
    {
        IsAngleDistance = angleDistance;
        HeaderSuffix = new OpaqueObject(string.Empty, angleDistance
            ? HeaderSuffixAngleDistance
            : HeaderSuffixDistanceDistance);
        SurfaceIdRep = new OpaqueObject("moSurfaceIdRep_c", angleDistance
            ? SurfaceIdRepDataAngleDistance
            : SurfaceIdRepDataDistanceDistance);
        FilletSurfIdRep = new OpaqueObject("moFilletSurfIdRep_c", angleDistance
            ? FilletSurfIdRepDataAngleDistance
            : FilletSurfIdRepDataDistanceDistance);
        EdgeRef = new OpaqueObject("moEdgeRef_c", angleDistance
            ? EdgeRefDataAngleDistance
            : EdgeRefDataDistanceDistance);
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref refTag);

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

            double d0 = Distance0 ?? 0.002;
            ar.F64(ref d0);

            bool isAngle = IsAngleDistance;
            ar.Boolean(ref isAngle);

            if (isAngle)
            {
                double ang = Angle ?? (Math.PI / 4.0);
                ar.F64(ref ang);
            }
            else
            {
                double d1 = Distance1 ?? (Distance0 ?? 0.002);
                ar.F64(ref d1);
            }

            HeaderSuffix?.Serialize(ar);
            SurfaceIdRep?.Serialize(ar);
            FilletSurfIdRep?.Serialize(ar);
            EdgeRef?.Serialize(ar);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) FeatureName = name!;

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
                double d0 = 0;
                ar.F64(ref d0);
                Distance0 = d0;
            }

            if (ar.Remaining >= 1)
            {
                bool isAngle = true;
                ar.Boolean(ref isAngle);
                IsAngleDistance = isAngle;
            }

            if (ar.Remaining >= 8)
            {
                if (IsAngleDistance)
                {
                    double ang = 0;
                    ar.F64(ref ang);
                    Angle = ang;
                }
                else
                {
                    double d1 = 0;
                    ar.F64(ref d1);
                    Distance1 = d1;
                }
            }
        }
    }

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ChamferSegments.cs
// ------------------------------------------------------------------------
/// <summary>
/// Measured Chamfer_c sub-object payloads from the controlled ChamferOptions samples
/// (distance-distance 2/3 mm and angle-distance 2 mm/45 deg, SW2026, stream 0x70).
/// Distance/angle scalars and their display copies are written structurally.
/// </summary>
internal static class ChamferSegments
{
    internal const string ClassName = "Chamfer_c";
    internal const uint HeaderTypeId = 0x52u;
    internal const int DistanceParameterOffset = 833;
    internal const int SecondDistanceParameterOffset = 2576;
    internal static readonly int[] DistancePositiveSlots = { 865, 905, 1183, 1425, 1449 };
    internal static readonly int[] DistanceNegativeSlots = { 1102, 1263, 1287 };
    internal static readonly int[] SecondDistancePositiveSlots = { 2608, 2640, 2854, 3096, 3120 };
    internal static readonly int[] SecondDistanceNegativeSlots = { 2765, 2926, 2950 };
    internal const int AngleParameterOffset = 0;
    internal static readonly int[] AngleSlots = { 32, 570 };
    internal const int AngleParameterDistanceCopySlot = 120;
    internal const int ExpectedCompEdgeLengthDistanceDistance = 3925;
    internal const int ExpectedCompEdgeLengthAngleDistance = 1899;
    internal const int ExpectedAngleParamLength = 1412;
    internal const string BasePrefixSha256DistanceDistance =
        "ad6ceea8e703cf26a97f8fdb78f88bac48d8ac1be95b8415c9bfd65e20d1d4f2";
    internal const string BasePrefixSha256AngleDistance =
        "0b676fa95264ef7a0894ba50999636e5de6e7322d3927589c2c54d42e9dc1043";

    /// <summary>Chamfer_c data after schema/WChar name/double 2.0/TypeId 0x52/u32 0/empty WChar, distance-distance control.</summary>
    internal static readonly byte[] HeaderSuffixDistanceDistance = SldFile.Mfc.ChamferAuthoringSegments.HeaderSuffixDistanceDistance;

    /// <summary>Chamfer_c data after the same fixed prefix, angle-distance control (contains the measured 0.016 display cache at +41).</summary>
    internal static readonly byte[] HeaderSuffixAngleDistance = SldFile.Mfc.ChamferAuthoringSegments.HeaderSuffixAngleDistance;

    /// <summary>moSurfaceIdRep_c payload (distance-distance sample surface tag).</summary>
    internal static readonly byte[] SurfaceIdRepDataDistanceDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x96, 0x4C, 0xE4, 0x6A
    };

    /// <summary>moSurfaceIdRep_c payload (angle-distance sample surface tag).</summary>
    internal static readonly byte[] SurfaceIdRepDataAngleDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x92, 0x4C, 0xE4, 0x6A
    };

    /// <summary>moFilletSurfIdRep_c payload (distance-distance sample surface tag).</summary>
    internal static readonly byte[] FilletSurfIdRepDataDistanceDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x96, 0x4C, 0xE4, 0x6A, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00
    };

    /// <summary>moFilletSurfIdRep_c payload (angle-distance sample surface tag).</summary>
    internal static readonly byte[] FilletSurfIdRepDataAngleDistance = new byte[] {
        0x1C, 0x01, 0xEA, 0x80, 0xED, 0x00, 0x52, 0x00, 0x00, 0x00, 0x92, 0x4C, 0xE4, 0x6A, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00
    };

    /// <summary>moEdgeRef_c payload for the controlled single target edge (distance-distance sample).</summary>
    internal static readonly byte[] EdgeRefDataDistanceDistance = SldFile.Mfc.ChamferAuthoringSegments.EdgeRefDataDistanceDistance;

    /// <summary>moEdgeRef_c payload for the controlled single target edge (angle-distance sample).</summary>
    internal static readonly byte[] EdgeRefDataAngleDistance = SldFile.Mfc.ChamferAuthoringSegments.EdgeRefDataAngleDistance;

    /// <summary>moCompEdge_c payload; embeds the D1 (2 mm) and D3 (3 mm) chamfer distance named parameters.</summary>
    internal static readonly byte[] CompEdgeDataDistanceDistance = SldFile.Mfc.ChamferAuthoringSegments.CompEdgeDataDistanceDistance;

    /// <summary>moCompEdge_c payload; embeds the D1 distance named parameter (angle lives in moAngleParameter_c D2).</summary>
    internal static readonly byte[] CompEdgeDataAngleDistance = SldFile.Mfc.ChamferAuthoringSegments.CompEdgeDataAngleDistance;

    /// <summary>moDisplayAngularDim_c payload (angle-distance display dimension).</summary>
    internal static readonly byte[] DisplayAngularDimData = SldFile.Mfc.ChamferAuthoringSegments.DisplayAngularDimData;

    /// <summary>AngleDim_c payload (u16 2 + padding).</summary>
    internal static readonly byte[] AngleDimData = new byte[] {
        0x01, 0x00, 0x00, 0x00
    };

    /// <summary>moAngleParameter_c payload; D2 angle at +32/+570 and a D1 distance display copy at +120.</summary>
    internal static readonly byte[] AngleParamData = SldFile.Mfc.ChamferAuthoringSegments.AngleParamData;

}

// ------------------------------------------------------------------------
// Inlined from SldFile/Binary/FeatureData/ChamferFeatureBuilder.cs
// ------------------------------------------------------------------------
/// <summary>
/// Fail-fast builder for the controlled chamfer authoring slices:
/// distance-distance (D1/D3) and angle-distance (D1 + D2 angle) on the measured single edge
/// of the ChamferOptions base body. Stream 0x70, parsed sketch chain, 0x67 marker and the exact
/// controlled base prefix are required; every other field is rejected before the stream changes.
/// </summary>
internal static class ChamferFeatureBuilder
{
    private const string SketchChainClass = "moSketchChain_c";
    private const int ChamferTypeAngleDistance = 1;
    private const int ChamferTypeDistanceDistance = 2;

    public static byte[] BuildChamferStream(
        byte[] currentStream,
        ChamferFeatureModel def,
        FeatureFolder sketchFolder,
        int sketchEndOffset,
        string featureName)
    {
        bool angleDistance = ValidateDefinition(def);

        if (currentStream == null || currentStream.Length < 16)
            throw new ArgumentException("Invalid ResolvedFeatures stream data.", nameof(currentStream));
        if (sketchFolder == null)
            throw new ArgumentNullException(nameof(sketchFolder));

        ushort version = BitConverter.ToUInt16(currentStream, 0);
        if (version != 0x70)
        {
            throw new NotSupportedException(
                $"Chamfer authoring is only supported for ResolvedFeatures streams version 0x70, " +
                $"but the document uses 0x{version:X2}. No stream changes have been made.");
        }

        if (ExtrudeFeatureBuilder.CountOccurrences(currentStream, ChamferSegments.ClassName) > 0)
        {
            throw new NotSupportedException(
                "Chamfer authoring currently supports only a stripped base body without an existing " +
                "Chamfer_c feature. No stream changes have been made.");
        }

        RequireSketchChain(currentStream, sketchFolder, sketchEndOffset, version);
        RequireSketchChildCountMarker(currentStream, version);
        RequireControlledBase(currentStream, version, angleDistance);

        byte[] featureBlock = MfcChamferSerializer.SerializeChamfer(def, featureName, angleDistance);
        ushort folderCount = BitConverter.ToUInt16(currentStream, 4);
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Array.Copy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Array.Copy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);
        return newStream;
    }

    /// <summary>Validates the definition; returns true for angle-distance, false for distance-distance.</summary>
    internal static bool ValidateDefinition(ChamferFeatureModel def)
    {
        if (def == null)
            throw new ArgumentNullException(nameof(def));

        if (!def.EdgeChamferDistanceSide0.HasValue ||
            double.IsNaN(def.EdgeChamferDistanceSide0.Value) ||
            double.IsInfinity(def.EdgeChamferDistanceSide0.Value) ||
            def.EdgeChamferDistanceSide0.Value <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(def.EdgeChamferDistanceSide0),
                $"Chamfer distance must be a positive finite number, but received {def.EdgeChamferDistanceSide0}.");
        }

        bool hasSide1 = def.EdgeChamferDistanceSide1.HasValue;
        bool hasAngle = def.EdgeChamferAngle.HasValue;
        if (hasSide1 == hasAngle)
        {
            throw new NotSupportedException(
                "Chamfer authoring requires exactly one of EdgeChamferDistanceSide1 (distance-distance) " +
                "or EdgeChamferAngle (angle-distance). No stream changes have been made.");
        }

        bool angleDistance = hasAngle;
        if (hasSide1)
        {
            double distance1 = def.EdgeChamferDistanceSide1!.Value;
            if (double.IsNaN(distance1) || double.IsInfinity(distance1) || distance1 <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.EdgeChamferDistanceSide1),
                    $"Chamfer side-1 distance must be a positive finite number, but received {distance1}.");
            }
            if (Math.Abs(distance1 - def.EdgeChamferDistanceSide0.Value) <= 1e-12)
            {
                throw new NotSupportedException(
                    "Chamfer authoring does not yet support equal distances (EqualDistance=true has no " +
                    "controlled sample). No stream changes have been made.");
            }
        }
        else
        {
            double angle = def.EdgeChamferAngle!.Value;
            if (double.IsNaN(angle) || double.IsInfinity(angle) || angle <= 0.0 || angle >= Math.PI / 2.0)
            {
                throw new ArgumentOutOfRangeException(nameof(def.EdgeChamferAngle),
                    $"Chamfer angle must be a finite value in (0, pi/2) radians, but received {angle}.");
            }
        }

        int expectedType = angleDistance ? ChamferTypeAngleDistance : ChamferTypeDistanceDistance;
        if (def.Type.HasValue && def.Type.Value != expectedType)
        {
            throw new NotSupportedException(
                $"Chamfer authoring expects Type={expectedType} for this mode, but received {def.Type.Value}. " +
                "No stream changes have been made.");
        }
        if (def.EqualDistance == true)
        {
            throw new NotSupportedException(
                "Chamfer authoring does not yet support EqualDistance=true. No stream changes have been made.");
        }

        // Only the fields bound by the measured layouts are authorable.
        foreach (var property in typeof(ChamferFeatureModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            switch (property.Name)
            {
                case nameof(ChamferFeatureModel.EdgeChamferDistanceSide0):
                case nameof(ChamferFeatureModel.EdgeChamferDistanceSide1):
                case nameof(ChamferFeatureModel.EdgeChamferAngle):
                case nameof(ChamferFeatureModel.Type):
                case nameof(ChamferFeatureModel.EqualDistance):
                case nameof(IFeatureData.ClassName):
                case nameof(IFeatureData.TypeId):
                case nameof(MfcFeatureModelBase.ResolvedProperties):
                case nameof(ChamferFeatureModel.FeatureName):
                case nameof(ChamferFeatureModel.Version):
                case nameof(ChamferFeatureModel.Distance0):
                case nameof(ChamferFeatureModel.Distance1):
                case nameof(ChamferFeatureModel.Angle):
                case nameof(ChamferFeatureModel.IsAngleDistance):
                case nameof(ChamferFeatureModel.HeaderSuffix):
                case nameof(ChamferFeatureModel.SurfaceIdRep):
                case nameof(ChamferFeatureModel.FilletSurfIdRep):
                case nameof(ChamferFeatureModel.EdgeRef):
                    continue;
            }

            object? value = property.GetValue(def);
            if (value != null)
            {
                throw new NotSupportedException(
                    $"Chamfer authoring does not yet support {property.Name}. No stream changes have been made.");
            }
        }

        return angleDistance;
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
                $"Cannot author a chamfer on the base body of sketch '{sketchName}' " +
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
            $"Cannot author a chamfer on the controlled base body (ResolvedFeatures version 0x{version:X2}): " +
            "the 0x67 sketch child-count marker was not found. No stream changes have been made.");
    }

    private static void RequireControlledBase(byte[] currentStream, ushort version, bool angleDistance)
    {
        byte[] normalized = (byte[])currentStream.Clone();
        if (normalized.Length >= 6)
        {
            normalized[4] = 0;
            normalized[5] = 0;
        }

        string sha = Sha256Hex(normalized);
        string expected = angleDistance
            ? ChamferSegments.BasePrefixSha256AngleDistance
            : ChamferSegments.BasePrefixSha256DistanceDistance;
        if (!string.Equals(sha, expected, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Chamfer authoring is limited to the controlled ChamferOptions base body for the " +
                $"{(angleDistance ? "angle-distance" : "distance-distance")} layout " +
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
// Inlined from SldFile/Binary/FeatureData/MfcChamferSerializer.cs
// ------------------------------------------------------------------------
/// <summary>
/// Serializes the measured Chamfer_c block (FolderHeader TypeId 0x52) for the controlled
/// distance-distance and angle-distance layouts. D1/D3 distances and the D2 angle are written
/// into the measured named-parameter slots and display copies; the selection/header metadata
/// remains the measured payload for the controlled single target edge.
/// </summary>
internal static class MfcChamferSerializer
{
    public static byte[] SerializeChamfer(
        ChamferFeatureModel def, string featureName, bool angleDistance)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (!def.EdgeChamferDistanceSide0.HasValue)
            throw new ArgumentException("Chamfer distance must be set.", nameof(def));
        if (string.IsNullOrEmpty(featureName)) featureName = "鍊掕1";

        double distance0 = def.EdgeChamferDistanceSide0.Value;
        using var writer = new MfcStreamWriter(6144);

        // 1. Chamfer_c header: structural prefix + mode-specific measured suffix.
        writer.WriteMfcTag(ChamferSegments.ClassName);
        writer.WriteU16(0x8004);
        writer.WriteWChar(featureName);
        writer.WriteF64(2.0);
        writer.WriteU32(ChamferSegments.HeaderTypeId);
        writer.WriteU32(0);
        writer.WriteWChar(string.Empty);
        writer.WriteBytes(angleDistance
            ? ChamferSegments.HeaderSuffixAngleDistance
            : ChamferSegments.HeaderSuffixDistanceDistance);

        // 2. Measured selection blocks for the controlled single edge.
        WriteTagged(writer, "moSurfaceIdRep_c", angleDistance
            ? ChamferSegments.SurfaceIdRepDataAngleDistance
            : ChamferSegments.SurfaceIdRepDataDistanceDistance);
        WriteTagged(writer, "moFilletSurfIdRep_c", angleDistance
            ? ChamferSegments.FilletSurfIdRepDataAngleDistance
            : ChamferSegments.FilletSurfIdRepDataDistanceDistance);
        WriteTagged(writer, "moEdgeRef_c", angleDistance
            ? ChamferSegments.EdgeRefDataAngleDistance
            : ChamferSegments.EdgeRefDataDistanceDistance);

        // 3. moCompEdge_c: shared edge prefix + D1 (and D3 for distance-distance) slots.
        var compEdge = Clone(angleDistance
            ? ChamferSegments.CompEdgeDataAngleDistance
            : ChamferSegments.CompEdgeDataDistanceDistance);
        foreach (int slot in ChamferSegments.DistancePositiveSlots)
            WriteF64At(compEdge, slot, distance0);
        foreach (int slot in ChamferSegments.DistanceNegativeSlots)
            WriteF64At(compEdge, slot, -distance0);

        if (!angleDistance)
        {
            double distance1 = def.EdgeChamferDistanceSide1!.Value;
            foreach (int slot in ChamferSegments.SecondDistancePositiveSlots)
                WriteF64At(compEdge, slot, distance1);
            foreach (int slot in ChamferSegments.SecondDistanceNegativeSlots)
                WriteF64At(compEdge, slot, -distance1);
        }
        writer.WriteMfcTag("moCompEdge_c");
        writer.WriteBytes(compEdge);

        // 4. Angle-distance adds the measured angular display stack and D2 parameter.
        if (angleDistance)
        {
            double angle = def.EdgeChamferAngle!.Value;
            var angleParam = Clone(ChamferSegments.AngleParamData);
            foreach (int slot in ChamferSegments.AngleSlots)
                WriteF64At(angleParam, slot, angle);
            WriteF64At(angleParam, ChamferSegments.AngleParameterDistanceCopySlot, distance0);

            WriteTagged(writer, "moDisplayAngularDim_c", ChamferSegments.DisplayAngularDimData);
            WriteTagged(writer, "AngleDim_c", ChamferSegments.AngleDimData);
            WriteTagged(writer, "moAngleParameter_c", angleParam);
        }

        return writer.ToArray();
    }

    private static void WriteTagged(MfcStreamWriter writer, string className, byte[] data)
    {
        writer.WriteMfcTag(className);
        writer.WriteBytes(data);
    }

    private static byte[] Clone(byte[] source) => (byte[])source.Clone();

    private static void WriteF64At(byte[] block, int offset, double value)
    {
        if (offset < 0 || offset + 8 > block.Length)
            throw new InvalidOperationException($"Chamfer segment slot {offset} is outside the measured block.");
        BitConverter.GetBytes(value).CopyTo(block, offset);
    }
}


