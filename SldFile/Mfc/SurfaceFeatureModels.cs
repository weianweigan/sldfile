using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

// Phase 4 surface feature definition shapes and strongly-typed MFC serialization models.
// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.

#region Phase 4 Extrude / Revolve / Sweep / Loft definitions (Existing)

/// <summary>Per-field evidence flags for <see cref="SurfaceExtrudeFeatureModel"/>.</summary>
public sealed class SurfaceExtrudeFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Depth { get; internal set; }
    public bool EndCondition { get; internal set; }
    public bool BothDirections { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool D1CapEnd { get; internal set; }
    public bool D2CapEnd { get; internal set; }
    public bool D1DraftOn { get; internal set; }
    public bool D2DraftOn { get; internal set; }
    public bool D1DraftAngle { get; internal set; }
    public bool D2DraftAngle { get; internal set; }
    public bool D1DraftOutward { get; internal set; }
    public bool D2DraftOutward { get; internal set; }
    public bool DeleteOriginalFace { get; internal set; }
    public bool KnitResult { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Surface extrude definition shape (<c>ISurfExtrudeFeatureData</c>, feature type
/// <c>ExtruRefSurface</c>). MFC class name/type code are not established by any sample.
/// </summary>
public sealed class SurfaceExtrudeFeatureModel : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceExtrudeFeatureParameterResolution Resolution { get; } = new();

    public double? Depth { get; set; }
    public int? EndCondition { get; set; }
    public bool? BothDirections { get; set; }
    public bool? ReverseDirection { get; set; }
    public bool? D1CapEnd { get; set; }
    public bool? D2CapEnd { get; set; }
    public bool? D1DraftOn { get; set; }
    public bool? D2DraftOn { get; set; }
    public double? D1DraftAngle { get; set; }
    public double? D2DraftAngle { get; set; }
    public bool? D1DraftOutward { get; set; }
    public bool? D2DraftOutward { get; set; }
    public bool? DeleteOriginalFace { get; set; }
    public bool? KnitResult { get; set; }
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceRevolveFeatureModel"/>.</summary>
public sealed class SurfaceRevolveFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Angle { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool Type { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>Surface revolve definition shape (<c>ISurfRevolveFeatureData</c>, <c>RevolvRefSurf</c>).</summary>
public sealed class SurfaceRevolveFeatureModel : IFeatureData
{
    public string ClassName => SerializedClassName ?? string.Empty;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceRevolveFeatureParameterResolution Resolution { get; } = new();

    public double? Angle { get; set; }
    public bool? ReverseDirection { get; set; }
    public int? Type { get; set; }
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceSweepFeatureModel"/>.</summary>
public sealed class SurfaceSweepFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool SelectionReferenceList { get; internal set; }
    public bool CircularProfile { get; internal set; }
    public bool PathReference { get; internal set; }
    public bool SweepType { get; internal set; }
    public bool Merge { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Surface sweep definition shape (serialized class <c>moSweepRefSurface_c</c>), distinct
/// from solid <see cref="SweepFeatureModel"/>.
/// </summary>
public sealed class SurfaceSweepFeatureModel : IFeatureData
{
    public const string MfcClassName = "moSweepRefSurface_c";
    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceSweepFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<ushort> SelectionReferenceIds { get; set; } = Array.Empty<ushort>();
    public SketchCurveReference? PathReference { get; set; }
    public bool CircularProfileLayout { get; set; }
    public double? CircularProfileDiameter { get; set; }
    public int? SweepType { get; set; }
    public bool? Merge { get; set; }
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }
    public int SelectionCount => SelectionReferenceIds.Count;

    internal static SurfaceSweepFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: 0x4B,
            maxTypeId: 0x4B,
            expectedStreamVersion: 0x70,
            requireEmptyWCharAt10: true,
            allowTwoByteHeaderPadding: true);
        if (ctx == null || !ctx.TryReadHeaderSelectionIds(out var ids) || ids.Count != 1)
            return null;

        var primary = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moCompProfile_c" && t.TagOffset > ctx.Start);
        if (primary == null ||
            !FeatureSelectionBag.TryReadSketchRefAt(ctx.RawData, primary.DataStart + 81, out uint sketchId, out uint serial))
        {
            return null;
        }

        var path = new SketchCurveReference
        {
            Role = SketchCurveRole.Path,
            EntityId = sketchId,
            ArchiveSerial = serial,
            HeaderSelectionId = ids[0],
            IsHeaderSelectionIdPositional = true
        };

        var diameterDim = ctx.Nodes.FirstOrDefault(t => t.ClassName == "ThreeDdiameterDim_c" && t.TagOffset > ctx.Start);
        if (diameterDim == null)
            return null;
        var lengthParam = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moLengthParameter_c" && t.TagOffset > diameterDim.TagOffset);
        if (lengthParam == null)
            return null;

        if (!ctx.Parameters.TryReadVerifiedDoubleSlots(
                lengthParam.DataStart,
                new[] { 0x20 },
                out double diameter,
                minExclusive: 0.0,
                maxInclusive: 1.0) ||
            !ctx.Nodes.Any(t => t.ClassName == "moWireContact_c" && t.TagOffset > ctx.Start))
        {
            return null;
        }

        var definition = new SurfaceSweepFeatureModel
        {
            SerializedClassName = MfcClassName,
            SerializedTypeId = ctx.TypeId,
            SelectionReferenceIds = ids,
            PathReference = path,
            CircularProfileLayout = true,
            CircularProfileDiameter = diameter,
            SweepType = 62,
            Merge = true,
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(ctx)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.SelectionReferenceList = ids.Count > 0;
        definition.Resolution.CircularProfile = true;
        definition.Resolution.PathReference = true;
        definition.Resolution.SweepType = true;
        definition.Resolution.Merge = true;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceLoftFeatureModel"/>.</summary>
public sealed class SurfaceLoftFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool SelectionReferenceList { get; internal set; }
    public bool ProfileReferences { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Surface loft node (<c>moBlendRefSurface_c</c>).
/// </summary>
public sealed class SurfaceLoftFeatureModel : IFeatureData
{
    public const string MfcClassName = "moBlendRefSurface_c";
    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId => SerializedTypeId ?? 0;
    public SurfaceLoftFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<ushort> SelectionReferenceIds { get; set; } = Array.Empty<ushort>();
    public IReadOnlyList<SketchCurveReference> References { get; set; } = Array.Empty<SketchCurveReference>();
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }
    public int SelectionCount => SelectionReferenceIds.Count;
    public int ReferenceCount => References.Count;

    internal static SurfaceLoftFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: 0x4D,
            maxTypeId: 0x50,
            expectedStreamVersion: 0x70,
            requireEmptyWCharAt10: true,
            allowTwoByteHeaderPadding: true);
        if (ctx == null || !ctx.TryReadHeaderSelectionIds(out var ids) || ids.Count < 2)
            return null;

        var primary = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moCompProfile_c" && t.TagOffset > ctx.Start);
        if (primary == null)
            return null;

        var records = ctx.Selections.ReadSketchRefChain(primary.DataStart + 81, 188, ids.Count);
        if (records.Count < 2)
            return null;

        var secondary = ctx.Nodes.FirstOrDefault(t =>
            t.ClassName == "moWireContact_c" && t.TagOffset > ctx.Start);
        int secondaryCount = 0;
        if (secondary != null)
        {
            var secRecords = ctx.Selections.ReadSketchRefChain(secondary.DataStart + 147, 190, ids.Count - records.Count);
            secondaryCount = secRecords.Count;
            records.AddRange(secRecords);
        }
        if (records.Count != ids.Count)
            return null;

        var references = new SketchCurveReference[ids.Count];
        int profileCount = ids.Count - secondaryCount;
        for (int i = 0; i < ids.Count; i++)
        {
            references[i] = new SketchCurveReference
            {
                Role = i < profileCount ? SketchCurveRole.Profile : SketchCurveRole.Guide,
                EntityId = records[i].Id,
                ArchiveSerial = records[i].Serial,
                HeaderSelectionId = ids[i],
                IsHeaderSelectionIdPositional = true
            };
        }

        var definition = new SurfaceLoftFeatureModel
        {
            SerializedClassName = MfcClassName,
            SerializedTypeId = ctx.TypeId,
            SelectionReferenceIds = ids,
            References = Array.AsReadOnly(references),
            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(ctx)
        };
        definition.Resolution.ClassName = true;
        definition.Resolution.SerializedTypeId = true;
        definition.Resolution.SelectionReferenceList = ids.Count > 0;
        definition.Resolution.ProfileReferences = references.Length > 0;
        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
        return definition;
    }
}

#endregion

#region Milestone P3 Strongly-Typed Surface Feature Models

/// <summary>Per-field evidence flags for <see cref="PlanarSurfaceFeatureModel"/>.</summary>
public sealed class PlanarSurfaceFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool BoundingEntities { get; internal set; }
    public bool BoundarySegments { get; internal set; }
    public bool SurfaceBodyFolder { get; internal set; }
}

/// <summary>
/// Direct strongly-typed MFC model for Planar Surface feature (<c>moPlanarSurface_c</c>, TypeId <c>0x49</c>).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.
/// Aligned with SolidWorks ISurfacePlanarFeatureData.
/// </summary>
public sealed class PlanarSurfaceFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moPlanarSurface_c";
    public const uint HeaderTypeId = 0x49u;
    public const uint Native2026TypeId = 0x4Au;
    public const uint SwFeatureId = 32u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "平面区域1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public PlanarSurfaceFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<EntityReference>? BoundingEntities { get; set; }
    public IReadOnlyList<EntityReference>? BoundarySegments { get; set; }
    public SurfaceBodyFolderReference? SurfaceBodyFolder { get; set; }

    public int? BoundingEntitiesCount => BoundingEntities?.Count;
    public int? BoundarySegmentsCount => BoundarySegments?.Count;

    public byte[]? RawBlock { get; set; }

    public PlanarSurfaceFeatureModel()
    {
    }

    public PlanarSurfaceFeatureModel(
        IEnumerable<EntityReference>? boundingEntities = null,
        IEnumerable<EntityReference>? boundarySegments = null)
    {
        FeatureName = "平面区域1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        if (boundingEntities != null)
        {
            BoundingEntities = boundingEntities.ToList().AsReadOnly();
            Resolution.BoundingEntities = true;
            MarkResolved(nameof(BoundingEntities));
        }
        if (boundarySegments != null)
        {
            BoundarySegments = boundarySegments.ToList().AsReadOnly();
            Resolution.BoundarySegments = true;
            MarkResolved(nameof(BoundarySegments));
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "平面区域1";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Bounding Entities (Sketch references)
            var bounds = BoundingEntities ?? Array.Empty<EntityReference>();
            ushort bCount = (ushort)bounds.Count; ar.U16(ref bCount);
            foreach (var b in bounds)
            {
                ushort skTag = 0; ar.ClassTagOrRef("moProfileFeature_c", ref skTag);
                string? skName = b.Name ?? "草图1"; ar.WChar(ref skName);
                uint skId = b.EntityId; ar.U32(ref skId);
            }

            // 5. Boundary Segments (moSegmEdgeIdRep_c, feature type 0x4A)
            var segments = BoundarySegments ?? Array.Empty<EntityReference>();
            ushort sCount = (ushort)segments.Count; ar.U16(ref sCount);
            foreach (var seg in segments)
            {
                ushort segTag = 0; ar.ClassTagOrRef("moSegmEdgeIdRep_c", ref segTag);
                uint fType = 0x4Au; ar.U32(ref fType);
                uint entId = seg.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);

                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Read Bounding Entities
                if (ar.Remaining >= 2)
                {
                    ushort bCount = 0; ar.U16(ref bCount);
                    var bList = new List<EntityReference>();
                    for (int i = 0; i < bCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort skTag = 0; ar.ReadClassTag(out string skCls, out skTag, out _);
                        string? skName = null; ar.WChar(ref skName);
                        uint skId = 0; ar.U32(ref skId);
                        bList.Add(new EntityReference { EntityId = skId, EntityType = SelectionEntityType.Sketch, Name = skName ?? "草图1" });
                    }
                    if (bList.Count > 0)
                    {
                        BoundingEntities = bList.AsReadOnly();
                        Resolution.BoundingEntities = true;
                        MarkResolved(nameof(BoundingEntities));
                    }
                }

                // Read Boundary Segments
                if (ar.Remaining >= 2)
                {
                    ushort sCount = 0; ar.U16(ref sCount);
                    var sList = new List<EntityReference>();
                    for (int i = 0; i < sCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort segTag = 0; ar.ReadClassTag(out string segCls, out segTag, out _);
                        uint fType = 0; ar.U32(ref fType);
                        uint entId = 0; ar.U32(ref entId);
                        sList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Edge, Name = $"moSegmEdgeIdRep_c:{entId}" });
                    }
                    if (sList.Count > 0)
                    {
                        BoundarySegments = sList.AsReadOnly();
                        Resolution.BoundarySegments = true;
                        MarkResolved(nameof(BoundarySegments));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedClassName = MfcClassName;
                SerializedTypeId = Native2026TypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        int segTagOffset = MfcPrimitives.FindAscii(data, "moSegmEdgeIdRep_c");
        if (segTagOffset > 0)
        {
            var segments = new List<EntityReference>();
            int pos = segTagOffset + "moSegmEdgeIdRep_c".Length;
            for (int i = 0; i < 64 && pos + 20 <= data.Length; i++)
            {
                uint featureType = BitConverter.ToUInt32(data, pos + 6);
                uint index = BitConverter.ToUInt32(data, pos + 14);
                if (featureType != 0x4A || index == 0) break;
                segments.Add(new EntityReference { EntityType = SelectionEntityType.Edge, EntityId = index, Name = $"BoundarySegment:{index}" });
                pos += 20;
            }
            if (segments.Count > 0)
            {
                BoundarySegments = segments.AsReadOnly();
                Resolution.BoundarySegments = true;
                MarkResolved(nameof(BoundarySegments));
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static PlanarSurfaceFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != MfcClassName) return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: Native2026TypeId,
            expectedStreamVersion: 0x70);
        if (ctx != null)
        {
            var segmentTag = ctx.Nodes.FirstOrDefault(t =>
                t.ClassName == "moSegmEdgeIdRep_c" && t.TagOffset > ctx.Start);
            if (segmentTag != null)
            {
                var boundarySegments = ParseBoundaries(ctx.RawData, segmentTag.DataStart);
                if (boundarySegments != null && boundarySegments.Count > 0)
                {
                    var sketchElement = ctx.Document?.AllElements.LastOrDefault(t =>
                        t.ClassName == "moProfileFeature_c" && t.TagOffset < ctx.Start);
                    string? sketchName = sketchElement?.AsWChar();
                    if (!string.IsNullOrEmpty(sketchName))
                    {
                        var definition = new PlanarSurfaceFeatureModel
                        {
                            SerializedClassName = MfcClassName,
                            SerializedTypeId = ctx.TypeId,
                            BoundingEntities = new[]
                            {
                                new EntityReference
                                {
                                    EntityType = SelectionEntityType.Sketch,
                                    Name = sketchName!
                                }
                            },
                            BoundarySegments = boundarySegments,
                            SurfaceBodyFolder = SurfaceBodyFolderParser.TryParse(ctx),
                            RawBlock = element.RawData
                        };
                        definition.Resolution.ClassName = true;
                        definition.Resolution.SerializedTypeId = true;
                        definition.Resolution.BoundingEntities = true;
                        definition.Resolution.BoundarySegments = true;
                        definition.Resolution.SurfaceBodyFolder = definition.SurfaceBodyFolder != null;
                        definition.MarkResolved(nameof(definition.BoundingEntities));
                        definition.MarkResolved(nameof(definition.BoundarySegments));
                        return definition;
                    }
                }
            }
        }

        try
        {
            byte[] block = element.Document?.SourceData ?? element.SourceData;
            int offset = element.TagOffset;
            int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
            byte[] rawSlice = new byte[length];
            Buffer.BlockCopy(block, offset, rawSlice, 0, length);
            var ar = MfcArchive.CreateReader(rawSlice);
            var model = new PlanarSurfaceFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.ClassName && model.Version == 2.0 && (model.SerializedTypeId == Native2026TypeId || model.SerializedTypeId == HeaderTypeId))
                return model;
        }
        catch { }

        return null;
    }

    private static List<EntityReference>? ParseBoundaries(byte[] data, int start)
    {
        var result = new List<EntityReference>();
        int pos = start;
        for (int i = 0; i < 64 && pos + 20 <= data.Length; i++)
        {
            uint featureType = BitConverter.ToUInt32(data, pos + 6);
            uint index = BitConverter.ToUInt32(data, pos + 14);
            if (featureType != 0x4A || index == 0)
                break;
            result.Add(new EntityReference
            {
                EntityType = SelectionEntityType.Edge,
                EntityId = index,
                Name = $"BoundarySegment:{index}"
            });
            pos += 20;
        }
        return result;
    }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceFillFeatureModel"/> and <see cref="FillSurfaceFeatureModel"/>.</summary>
public class FillSurfaceFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Merge { get; internal set; }
    public bool OptimizeSurface { get; internal set; }
    public bool ResolutionControl { get; internal set; }
    public bool ReverseDirection { get; internal set; }
    public bool ReverseSurface { get; internal set; }
    public bool TryToFormSolid { get; internal set; }
    public bool PatchBoundary { get; internal set; }
    public bool BoundaryCurvature { get; internal set; }
}

public sealed class SurfaceFillFeatureParameterResolution : FillSurfaceFeatureParameterResolution
{
}

/// <summary>
/// Direct strongly-typed MFC model for Fill Surface feature (<c>moFillRefSurface_c</c>, TypeId <c>0x4A</c>).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.
/// Aligned with SolidWorks IFillSurfaceFeatureData.
/// </summary>
public class SurfaceFillFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moFillRefSurface_c";
    public const uint HeaderTypeId = 0x4Au;
    public const uint SwFeatureId = 62u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "填充曲面1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public FillSurfaceFeatureParameterResolution Resolution { get; } = new();

    public bool? Merge { get; set; }
    public bool? OptimizeSurface { get; set; }
    public int? ResolutionControl { get; set; }
    public bool? ReverseDirection { get; set; }
    public bool? ReverseSurface { get; set; }
    public bool? TryToFormSolid { get; set; }
    public IReadOnlyList<EntityReference>? PatchBoundary { get; set; }
    public IReadOnlyList<int>? BoundaryCurvatureControls { get; set; }

    public int? PatchBoundaryCount => PatchBoundary?.Count;
    public int? PatchBoundaryEntityCount => PatchBoundary?.Count;
    public int? BoundaryCurvatureControlCount => BoundaryCurvatureControls?.Count;

    public byte[]? RawBlock { get; set; }

    public SurfaceFillFeatureModel()
    {
    }

    public SurfaceFillFeatureModel(
        int resolutionControl = 1,
        IEnumerable<EntityReference>? patchBoundary = null,
        IEnumerable<int>? boundaryCurvatureControls = null,
        bool merge = false,
        bool optimizeSurface = false,
        bool reverseDirection = false,
        bool reverseSurface = false,
        bool tryToFormSolid = false)
    {
        FeatureName = "填充曲面1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        ResolutionControl = resolutionControl;
        Merge = merge;
        OptimizeSurface = optimizeSurface;
        ReverseDirection = reverseDirection;
        ReverseSurface = reverseSurface;
        TryToFormSolid = tryToFormSolid;
        if (patchBoundary != null)
        {
            PatchBoundary = patchBoundary.ToList().AsReadOnly();
            Resolution.PatchBoundary = true;
            MarkResolved(nameof(PatchBoundary));
        }
        if (boundaryCurvatureControls != null)
        {
            BoundaryCurvatureControls = boundaryCurvatureControls.ToList().AsReadOnly();
            Resolution.BoundaryCurvature = true;
            MarkResolved(nameof(BoundaryCurvatureControls));
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.ResolutionControl = true;
        Resolution.Merge = true;
        Resolution.OptimizeSurface = true;
        Resolution.ReverseDirection = true;
        Resolution.ReverseSurface = true;
        Resolution.TryToFormSolid = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
        MarkResolved(nameof(ResolutionControl));
        MarkResolved(nameof(Merge));
        MarkResolved(nameof(OptimizeSurface));
        MarkResolved(nameof(ReverseDirection));
        MarkResolved(nameof(ReverseSurface));
        MarkResolved(nameof(TryToFormSolid));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (ResolutionControl.HasValue && (ResolutionControl.Value == 1 || ResolutionControl.Value == 2))
                {
                    PatchResolutionControlInBlock(block, ResolutionControl.Value);
                }
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "填充曲面1";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Numeric & Boolean parameters
            int res = ResolutionControl ?? 1; ar.I32(ref res);
            bool merge = Merge ?? false; ar.Boolean(ref merge);
            bool opt = OptimizeSurface ?? false; ar.Boolean(ref opt);
            bool revDir = ReverseDirection ?? false; ar.Boolean(ref revDir);
            bool revSurf = ReverseSurface ?? false; ar.Boolean(ref revSurf);
            bool solid = TryToFormSolid ?? false; ar.Boolean(ref solid);

            // 5. Patch Boundaries (moRadiateEdgeIdRep_c)
            var bounds = PatchBoundary ?? Array.Empty<EntityReference>();
            ushort bCount = (ushort)bounds.Count; ar.U16(ref bCount);
            foreach (var b in bounds)
            {
                ushort radTag = 0; ar.ClassTagOrRef("moRadiateEdgeIdRep_c", ref radTag);
                ushort radRef = 0x802C; ar.U16(ref radRef);
                ushort radSchema = 0x0002; ar.U16(ref radSchema);
                uint radFlags = 0x00400000; ar.U32(ref radFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = b.EntityId; ar.U32(ref entId);
            }

            // 6. Curvature Controls
            var curvs = BoundaryCurvatureControls ?? Array.Empty<int>();
            ushort cCount = (ushort)curvs.Count; ar.U16(ref cCount);
            foreach (var c in curvs)
            {
                int val = c; ar.I32(ref val);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);

                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Parameters
                int res = 0; ar.I32(ref res); ResolutionControl = res;
                bool merge = false; ar.Boolean(ref merge); Merge = merge;
                bool opt = false; ar.Boolean(ref opt); OptimizeSurface = opt;
                bool revDir = false; ar.Boolean(ref revDir); ReverseDirection = revDir;
                bool revSurf = false; ar.Boolean(ref revSurf); ReverseSurface = revSurf;
                bool solid = false; ar.Boolean(ref solid); TryToFormSolid = solid;

                // Patch boundaries
                if (ar.Remaining >= 2)
                {
                    ushort bCount = 0; ar.U16(ref bCount);
                    var bList = new List<EntityReference>();
                    for (int i = 0; i < bCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort radTag = 0; ar.ReadClassTag(out string radCls, out radTag, out _);
                        ushort radRef = 0; ar.U16(ref radRef);
                        ushort radSchema = 0; ar.U16(ref radSchema);
                        uint radFlags = 0; ar.U32(ref radFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        bList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Edge, Name = $"moRadiateEdgeIdRep_c:{entId:X8}" });
                    }
                    if (bList.Count > 0)
                    {
                        PatchBoundary = bList.AsReadOnly();
                        Resolution.PatchBoundary = true;
                        MarkResolved(nameof(PatchBoundary));
                    }
                }

                // Curvature controls
                if (ar.Remaining >= 2)
                {
                    ushort cCount = 0; ar.U16(ref cCount);
                    var cList = new List<int>();
                    for (int i = 0; i < cCount; i++)
                    {
                        if (ar.Remaining < 4) break;
                        int val = 0; ar.I32(ref val);
                        cList.Add(val);
                    }
                    if (cList.Count > 0)
                    {
                        BoundaryCurvatureControls = cList.AsReadOnly();
                        Resolution.BoundaryCurvature = true;
                        MarkResolved(nameof(BoundaryCurvatureControls));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.ResolutionControl = true;
                Resolution.Merge = true;
                Resolution.OptimizeSurface = true;
                Resolution.ReverseDirection = true;
                Resolution.ReverseSurface = true;
                Resolution.TryToFormSolid = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
                MarkResolved(nameof(ResolutionControl));
                MarkResolved(nameof(Merge));
                MarkResolved(nameof(OptimizeSurface));
                MarkResolved(nameof(ReverseDirection));
                MarkResolved(nameof(ReverseSurface));
                MarkResolved(nameof(TryToFormSolid));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedClassName = MfcClassName;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    internal static void PatchResolutionControlInBlock(byte[] block, int resolutionControl)
    {
        for (int i = 0; i <= block.Length - 10; i++)
        {
            if (block[i] == 0x00 && block[i + 1] == 0xF0 && block[i + 2] == 0xBF &&
                block[i + 8] == 0x82 && (block[i + 7] == 0x41 || block[i + 7] == 0x49))
            {
                block[i + 7] = resolutionControl == 1 ? (byte)0x41 : (byte)0x49;
                return;
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        for (int i = 0; i <= data.Length - 10; i++)
        {
            if (data[i] == 0x00 && data[i + 1] == 0xF0 && data[i + 2] == 0xBF &&
                data[i + 8] == 0x82)
            {
                byte raw = data[i + 7];
                if (raw == 0x41) { ResolutionControl = 1; Resolution.ResolutionControl = true; MarkResolved(nameof(ResolutionControl)); break; }
                else if (raw == 0x49) { ResolutionControl = 2; Resolution.ResolutionControl = true; MarkResolved(nameof(ResolutionControl)); break; }
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static SurfaceFillFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != MfcClassName) return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x70);
        if (ctx != null)
        {
            if (ctx.Nodes.Any(t => t.ClassName == "moRadiateEdgeIdRep_c" && t.TagOffset > ctx.Start) &&
                ctx.Nodes.Any(t => t.ClassName == "moCurveContactData_c" && t.TagOffset > ctx.Start))
            {
                byte[] data = ctx.RawData;
                int? resolutionControl = null;
                if (ctx.HeaderStart >= 0 && ctx.HeaderStart + 0x309 <= data.Length &&
                    data[ctx.HeaderStart + 0x300] == 0x00 &&
                    data[ctx.HeaderStart + 0x301] == 0xF0 &&
                    data[ctx.HeaderStart + 0x302] == 0xBF &&
                    data[ctx.HeaderStart + 0x308] == 0x82)
                {
                    byte raw = data[ctx.HeaderStart + 0x307];
                    if (raw == 0x41) resolutionControl = 1;
                    else if (raw == 0x49) resolutionControl = 2;
                }

                var definition = new FillSurfaceFeatureModel
                {
                    SerializedClassName = MfcClassName,
                    SerializedTypeId = ctx.TypeId,
                    ResolutionControl = resolutionControl,
                    RawBlock = element.RawData
                };
                definition.Resolution.ClassName = true;
                definition.Resolution.SerializedTypeId = true;
                definition.Resolution.ResolutionControl = resolutionControl.HasValue;
                if (resolutionControl.HasValue)
                    definition.MarkResolved(nameof(definition.ResolutionControl));
                return definition;
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            if (block != null && offset >= 0 && offset < block.Length)
            {
                int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
                byte[] rawSlice = new byte[length];
                Buffer.BlockCopy(block, offset, rawSlice, 0, length);
                var ar = MfcArchive.CreateReader(rawSlice);
                var model = new FillSurfaceFeatureModel();
                model.Serialize(ar);
                if ((model.Resolution.ResolutionControl && model.ResolutionControl.HasValue && (model.ResolutionControl.Value == 1 || model.ResolutionControl.Value == 2)) ||
                    (model.Resolution.PatchBoundary && model.PatchBoundary != null && model.PatchBoundary.Count > 0))
                {
                    return model;
                }
            }
        }
        catch { }

        return null;
    }
}

/// <summary>Alias/Subclass of <see cref="SurfaceFillFeatureModel"/> for backward compatibility.</summary>
public sealed class FillSurfaceFeatureModel : SurfaceFillFeatureModel
{
    public FillSurfaceFeatureModel() : base() { }

    public FillSurfaceFeatureModel(
        int resolutionControl = 1,
        IEnumerable<EntityReference>? patchBoundary = null,
        IEnumerable<int>? boundaryCurvatureControls = null,
        bool merge = false,
        bool optimizeSurface = false,
        bool reverseDirection = false,
        bool reverseSurface = false,
        bool tryToFormSolid = false)
        : base(resolutionControl, patchBoundary, boundaryCurvatureControls, merge, optimizeSurface, reverseDirection, reverseSurface, tryToFormSolid)
    {
    }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceKnitFeatureModel"/>.</summary>
public sealed class SurfaceKnitFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Entities { get; internal set; }
    public bool KnitTolerance { get; internal set; }
    public bool MaxValueForGapRange { get; internal set; }
    public bool MinValueForGapRange { get; internal set; }
    public bool SeedFace { get; internal set; }
    public bool UseGapFilters { get; internal set; }
    public bool UseMergeEntities { get; internal set; }
    public bool UseTryToFormSolid { get; internal set; }
    public bool StitchReferences { get; internal set; }
}

/// <summary>
/// Direct strongly-typed MFC model for Surface Knit feature (<c>moSewRefSurface_c</c>, TypeId <c>0x4E</c>).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.
/// Aligned with SolidWorks ISurfaceKnitFeatureData.
/// </summary>
public sealed class SurfaceKnitFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moSewRefSurface_c";
    public const uint HeaderTypeId = 0x4Eu; // 78 in decimal
    public const uint SwFeatureId = 33u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "曲面-缝合1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public SurfaceKnitFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<EntityReference>? Entities { get; set; }
    public double? KnitTolerance { get; set; }
    public double? MaxValueForGapRange { get; set; }
    public double? MinValueForGapRange { get; set; }
    public EntityReference? SeedFace { get; set; }
    public bool? UseGapFilters { get; set; }
    public bool? UseMergeEntities { get; set; }
    public bool? UseTryToFormSolid { get; set; }
    public IReadOnlyList<EntityReference>? StitchReferences { get; set; }

    public int? EntitiesCount => Entities?.Count;
    public int? StitchReferenceCount => StitchReferences?.Count;

    public byte[]? RawBlock { get; set; }

    public SurfaceKnitFeatureModel()
    {
    }

    public SurfaceKnitFeatureModel(
        double knitTolerance,
        IEnumerable<EntityReference>? entities = null,
        bool useMergeEntities = false,
        bool useTryToFormSolid = false,
        bool useGapFilters = false,
        EntityReference? seedFace = null)
    {
        FeatureName = "曲面-缝合1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        KnitTolerance = knitTolerance;
        UseMergeEntities = useMergeEntities;
        UseTryToFormSolid = useTryToFormSolid;
        UseGapFilters = useGapFilters;
        SeedFace = seedFace;
        if (entities != null)
        {
            var list = entities.ToList().AsReadOnly();
            Entities = list;
            StitchReferences = list;
            Resolution.Entities = true;
            Resolution.StitchReferences = true;
            MarkResolved(nameof(Entities));
            MarkResolved(nameof(StitchReferences));
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.KnitTolerance = true;
        Resolution.UseMergeEntities = true;
        Resolution.UseTryToFormSolid = true;
        Resolution.UseGapFilters = true;
        if (seedFace != null)
        {
            Resolution.SeedFace = true;
            MarkResolved(nameof(SeedFace));
        }
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
        MarkResolved(nameof(KnitTolerance));
        MarkResolved(nameof(UseMergeEntities));
        MarkResolved(nameof(UseTryToFormSolid));
        MarkResolved(nameof(UseGapFilters));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "曲面-缝合1";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Parameters
            double tol = KnitTolerance ?? 0.0001; ar.F64(ref tol);
            bool merge = UseMergeEntities ?? false; ar.Boolean(ref merge);
            bool solid = UseTryToFormSolid ?? false; ar.Boolean(ref solid);
            bool gap = UseGapFilters ?? false; ar.Boolean(ref gap);
            double minGap = MinValueForGapRange ?? 0.0; ar.F64(ref minGap);
            double maxGap = MaxValueForGapRange ?? 0.0; ar.F64(ref maxGap);

            // 5. SeedFace
            bool hasSeed = SeedFace != null;
            ar.Boolean(ref hasSeed);
            if (hasSeed)
            {
                ushort compFaceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
                ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
                uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = SeedFace!.EntityId; ar.U32(ref entId);
            }

            // 6. Entities & StitchReferences
            var entities = Entities ?? StitchReferences ?? Array.Empty<EntityReference>();
            ushort entCount = (ushort)entities.Count;
            ar.U16(ref entCount);
            foreach (var ent in entities)
            {
                string subCls = ent.EntityType == SelectionEntityType.Face ? "moCompFace_c" : "moCompSurfaceBody_c";
                ushort subTag = 0; ar.ClassTagOrRef(subCls, ref subTag);
                ushort subRef = 0x802C; ar.U16(ref subRef);
                ushort subSchema = 0x0002; ar.U16(ref subSchema);
                uint subFlags = 0x00400000; ar.U32(ref subFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = ent.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);

                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Parameters
                double tol = 0; ar.F64(ref tol); KnitTolerance = tol;
                bool merge = false; ar.Boolean(ref merge); UseMergeEntities = merge;
                bool solid = false; ar.Boolean(ref solid); UseTryToFormSolid = solid;
                bool gap = false; ar.Boolean(ref gap); UseGapFilters = gap;
                double minGap = 0; ar.F64(ref minGap); MinValueForGapRange = minGap;
                double maxGap = 0; ar.F64(ref maxGap); MaxValueForGapRange = maxGap;

                // SeedFace
                bool hasSeed = false; ar.Boolean(ref hasSeed);
                if (hasSeed && ar.Remaining >= 18)
                {
                    ushort faceTag = 0; ar.ReadClassTag(out string fCls, out faceTag, out _);
                    ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                    ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                    uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                    uint compId = 0; ar.U32(ref compId);
                    uint entId = 0; ar.U32(ref entId);
                    SeedFace = new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId:X8}" };
                    Resolution.SeedFace = true;
                    MarkResolved(nameof(SeedFace));
                }

                // Entities
                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    var entList = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort tag = 0; ar.ReadClassTag(out string subCls, out tag, out _);
                        ushort sRef = 0; ar.U16(ref sRef);
                        ushort sSchema = 0; ar.U16(ref sSchema);
                        uint sFlags = 0; ar.U32(ref sFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        var entType = subCls == "moCompFace_c" ? SelectionEntityType.Face : SelectionEntityType.Body;
                        entList.Add(new EntityReference { EntityId = entId, EntityType = entType, Name = $"{subCls}:{entId:X8}" });
                    }
                    if (entList.Count > 0)
                    {
                        Entities = entList.AsReadOnly();
                        StitchReferences = entList.AsReadOnly();
                        Resolution.Entities = true;
                        Resolution.StitchReferences = true;
                        MarkResolved(nameof(Entities));
                        MarkResolved(nameof(StitchReferences));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.KnitTolerance = true;
                Resolution.UseMergeEntities = true;
                Resolution.UseTryToFormSolid = true;
                Resolution.UseGapFilters = true;
                Resolution.MinValueForGapRange = true;
                Resolution.MaxValueForGapRange = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
                MarkResolved(nameof(KnitTolerance));
                MarkResolved(nameof(UseMergeEntities));
                MarkResolved(nameof(UseTryToFormSolid));
                MarkResolved(nameof(UseGapFilters));
                MarkResolved(nameof(MinValueForGapRange));
                MarkResolved(nameof(MaxValueForGapRange));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedClassName = MfcClassName;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        int bodyOffset = MfcPrimitives.FindAscii(data, "moCompSurfaceBody_c");
        if (bodyOffset > 0)
        {
            int dataStart = bodyOffset + "moCompSurfaceBody_c".Length;
            var body = FeatureSelectionBag.TryParseComponentEntityAt(
                data, dataStart, "moCompSurfaceBody_c", SelectionEntityType.Body);
            if (body != null)
            {
                Entities = new[] { body };
                StitchReferences = new[] { body };
                Resolution.Entities = true;
                Resolution.StitchReferences = true;
                MarkResolved(nameof(Entities));
                MarkResolved(nameof(StitchReferences));
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static SurfaceKnitFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != MfcClassName) return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x72);
        if (ctx != null)
        {
            var body = ctx.Selections.TryReadComponentEntities("moCompSurfaceBody_c", SelectionEntityType.Body);
            if (body != null)
            {
                var definition = new SurfaceKnitFeatureModel
                {
                    SerializedClassName = MfcClassName,
                    SerializedTypeId = ctx.TypeId,
                    Entities = body,
                    StitchReferences = body,
                    RawBlock = element.RawData
                };
                definition.Resolution.ClassName = true;
                definition.Resolution.SerializedTypeId = true;
                definition.Resolution.Entities = true;
                definition.Resolution.StitchReferences = true;
                definition.MarkResolved(nameof(definition.Entities));
                definition.MarkResolved(nameof(definition.StitchReferences));
                return definition;
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            if (block != null && offset >= 0 && offset < block.Length)
            {
                int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
                byte[] rawSlice = new byte[length];
                Buffer.BlockCopy(block, offset, rawSlice, 0, length);
                var ar = MfcArchive.CreateReader(rawSlice);
                var model = new SurfaceKnitFeatureModel();
                model.Serialize(ar);
                if ((model.Resolution.KnitTolerance && model.KnitTolerance.HasValue && model.KnitTolerance.Value > 0.00001) ||
                    (model.Resolution.Entities && model.Entities != null && model.Entities.Count > 0))
                {
                    return model;
                }
            }
        }
        catch { }

        return null;
    }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceTrimFeatureModel"/>.</summary>
public sealed class SurfaceTrimFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool PiecesToKeep { get; internal set; }
    public bool TrimTools { get; internal set; }
    public bool TrimType { get; internal set; }
}

/// <summary>
/// Direct strongly-typed MFC model for Surface Trim feature (<c>moTrimRefSurface_c</c>, TypeId <c>0x4B</c>).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.
/// Aligned with SolidWorks ISurfaceTrimFeatureData.
/// </summary>
public sealed class SurfaceTrimFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moTrimRefSurface_c";
    public const uint HeaderTypeId = 0x4Bu;
    public const uint LegacyTutorialTypeId = 20u;
    public const uint SwFeatureId = 20u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "曲面-剪裁1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public SurfaceTrimFeatureParameterResolution Resolution { get; } = new();

    public IReadOnlyList<EntityReference>? PiecesToKeep { get; set; }
    public IReadOnlyList<EntityReference>? TrimTools { get; set; }
    public int? TrimType { get; set; }

    public int? PiecesToKeepCount => PiecesToKeep?.Count;
    public int? TrimToolsCount => TrimTools?.Count;

    public byte[]? RawBlock { get; set; }

    public SurfaceTrimFeatureModel()
    {
    }

    public SurfaceTrimFeatureModel(
        int trimType = 0,
        IEnumerable<EntityReference>? trimTools = null,
        IEnumerable<EntityReference>? piecesToKeep = null)
    {
        FeatureName = "曲面-剪裁1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        TrimType = trimType;
        if (trimTools != null)
        {
            TrimTools = trimTools.ToList().AsReadOnly();
            Resolution.TrimTools = true;
            MarkResolved(nameof(TrimTools));
        }
        if (piecesToKeep != null)
        {
            PiecesToKeep = piecesToKeep.ToList().AsReadOnly();
            Resolution.PiecesToKeep = true;
            MarkResolved(nameof(PiecesToKeep));
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.TrimType = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
        MarkResolved(nameof(TrimType));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "曲面-剪裁1";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. TrimType
            int trimType = TrimType ?? 0; ar.I32(ref trimType);

            // 5. TrimTools (Face or SurfaceBody)
            var tools = TrimTools ?? Array.Empty<EntityReference>();
            ushort tCount = (ushort)tools.Count; ar.U16(ref tCount);
            foreach (var tool in tools)
            {
                string subCls = tool.EntityType == SelectionEntityType.Face ? "moCompFace_c" : "moCompSurfaceBody_c";
                ushort subTag = 0; ar.ClassTagOrRef(subCls, ref subTag);
                ushort subRef = 0x802C; ar.U16(ref subRef);
                ushort subSchema = 0x0002; ar.U16(ref subSchema);
                uint subFlags = 0x00400000; ar.U32(ref subFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = tool.EntityId; ar.U32(ref entId);
            }

            // 6. PiecesToKeep (Face references)
            var pieces = PiecesToKeep ?? Array.Empty<EntityReference>();
            ushort pCount = (ushort)pieces.Count; ar.U16(ref pCount);
            foreach (var piece in pieces)
            {
                ushort compFaceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
                ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
                uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = piece.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);

                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // TrimType
                int trimType = 0; ar.I32(ref trimType); TrimType = trimType;

                // TrimTools
                if (ar.Remaining >= 2)
                {
                    ushort tCount = 0; ar.U16(ref tCount);
                    var tList = new List<EntityReference>();
                    for (int i = 0; i < tCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort subTag = 0; ar.ReadClassTag(out string subCls, out subTag, out _);
                        ushort subRef = 0; ar.U16(ref subRef);
                        ushort subSchema = 0; ar.U16(ref subSchema);
                        uint subFlags = 0; ar.U32(ref subFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        var entType = subCls == "moCompFace_c" ? SelectionEntityType.Face : SelectionEntityType.Body;
                        tList.Add(new EntityReference { EntityId = entId, EntityType = entType, Name = $"{subCls}:{entId:X8}" });
                    }
                    if (tList.Count > 0)
                    {
                        TrimTools = tList.AsReadOnly();
                        Resolution.TrimTools = true;
                        MarkResolved(nameof(TrimTools));
                    }
                }

                // PiecesToKeep
                if (ar.Remaining >= 2)
                {
                    ushort pCount = 0; ar.U16(ref pCount);
                    var pList = new List<EntityReference>();
                    for (int i = 0; i < pCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort faceTag = 0; ar.ReadClassTag(out string fCls, out faceTag, out _);
                        ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                        ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                        uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        pList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId:X8}" });
                    }
                    if (pList.Count > 0)
                    {
                        PiecesToKeep = pList.AsReadOnly();
                        Resolution.PiecesToKeep = true;
                        MarkResolved(nameof(PiecesToKeep));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.TrimType = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
                MarkResolved(nameof(TrimType));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedClassName = MfcClassName;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        // Pinned 0x6F signature: 0xD8..0xDC zero, flag at 0xDD in {0x04, 0x06}, 0xDE is 0, 0xDF..0xE2 is FF FF 01 00
        if (data.Length >= 0xE4)
        {
            int i = 0xDD;
            if (data[i + 1] == 0x00 && data[i + 2] == 0xFF && data[i + 3] == 0xFF &&
                data[i + 4] == 0x01 && data[i + 5] == 0x00)
            {
                byte flag = data[i];
                if (flag == 0x04 || flag == 0x06)
                {
                    bool allZero = true;
                    for (int z = 0xD8; z <= 0xDC; z++)
                    {
                        if (data[z] != 0) { allZero = false; break; }
                    }
                    if (allZero)
                    {
                        TrimType = (flag & 0x02) != 0 ? 1 : 0;
                        Resolution.TrimType = true;
                        MarkResolved(nameof(TrimType));
                    }
                }
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static SurfaceTrimFeatureModel? Parse(MfcElement? element)
    {
        if (element == null || element.ClassName != MfcClassName) return null;

        var model = new SurfaceTrimFeatureModel
        {
            SerializedClassName = MfcClassName,
            SerializedTypeId = HeaderTypeId,
            RawBlock = element.RawData
        };
        model.Resolution.ClassName = true;
        model.Resolution.SerializedTypeId = true;
        model.MarkResolved(nameof(model.ClassName));
        model.MarkResolved(nameof(model.TypeId));

        byte[] raw = element.SourceData;
        int start = element.TagOffset;
        if (raw != null && raw.Length >= 2 && start >= 0)
        {
            ushort streamVersion = BitConverter.ToUInt16(raw, 0);
            if (streamVersion == 0x006F && start + 0xE4 <= raw.Length)
            {
                bool allZero = true;
                for (int i = 0xD8; i <= 0xDC; i++)
                {
                    if (raw[start + i] != 0) { allZero = false; break; }
                }
                if (allZero)
                {
                    byte flag = raw[start + 0xDD];
                    if ((flag == 0x04 || flag == 0x06) &&
                        raw[start + 0xDE] == 0 &&
                        raw[start + 0xDF] == 0xFF &&
                        raw[start + 0xE0] == 0xFF &&
                        raw[start + 0xE1] == 0x01 &&
                        raw[start + 0xE2] == 0x00)
                    {
                        model.TrimType = (flag & 0x02) != 0 ? 1 : 0;
                        model.Resolution.TrimType = true;
                        model.MarkResolved(nameof(model.TrimType));
                    }
                }
            }
        }

        return model;
    }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceExtendFeatureModel"/>.</summary>
public sealed class SurfaceExtendFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Distance { get; internal set; }
    public bool EndCondition { get; internal set; }
    public bool Face { get; internal set; }
    public bool Vertex { get; internal set; }
    public bool Items { get; internal set; }
    public bool PropagatingEdges { get; internal set; }
    public bool Type { get; internal set; }
}

/// <summary>
/// Direct strongly-typed MFC model for Surface Extend feature (<c>moExtendRefSurface_c</c>, TypeId <c>0x54</c>).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.
/// Aligned with SolidWorks ISurfaceExtendFeatureData.
/// </summary>
public sealed class SurfaceExtendFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moExtendRefSurface_c";
    public const uint HeaderTypeId = 0x54u;
    public const uint SwFeatureId = 62u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "曲面-延伸1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public SurfaceExtendFeatureParameterResolution Resolution { get; } = new();

    public double? Distance { get; set; }
    public int? EndCondition { get; set; }
    public EntityReference? Face { get; set; }
    public IReadOnlyList<EntityReference>? Items { get; set; }
    public IReadOnlyList<EntityReference>? PropagatingEdges { get; set; }
    public int? Type { get; set; }
    public EntityReference? Vertex { get; set; }

    public int? ItemsCount => Items?.Count;
    public int? PropagatingEdgesCount => PropagatingEdges?.Count;

    public byte[]? RawBlock { get; set; }

    public SurfaceExtendFeatureModel()
    {
    }

    public SurfaceExtendFeatureModel(
        double distance,
        IEnumerable<EntityReference>? items = null,
        int endCondition = 0,
        int type = 0,
        EntityReference? face = null,
        EntityReference? vertex = null)
    {
        FeatureName = "曲面-延伸1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        Distance = distance;
        EndCondition = endCondition;
        Type = type;
        Face = face;
        Vertex = vertex;
        if (items != null)
        {
            var list = items.ToList().AsReadOnly();
            Items = list;
            PropagatingEdges = list;
            Resolution.Items = true;
            Resolution.PropagatingEdges = true;
            MarkResolved(nameof(Items));
            MarkResolved(nameof(PropagatingEdges));
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.Distance = true;
        Resolution.EndCondition = true;
        Resolution.Type = true;
        if (face != null) { Resolution.Face = true; MarkResolved(nameof(Face)); }
        if (vertex != null) { Resolution.Vertex = true; MarkResolved(nameof(Vertex)); }
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
        MarkResolved(nameof(Distance));
        MarkResolved(nameof(EndCondition));
        MarkResolved(nameof(Type));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "曲面-延伸1";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Numeric & Enum parameters
            double dist = Distance ?? 0.005; ar.F64(ref dist);
            int endCond = EndCondition ?? 0; ar.I32(ref endCond);
            int extType = Type ?? 0; ar.I32(ref extType);

            // 5. Face reference
            bool hasFace = Face != null;
            ar.Boolean(ref hasFace);
            if (hasFace)
            {
                ushort compFaceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
                ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
                uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = Face!.EntityId; ar.U32(ref entId);
            }

            // 6. Vertex reference
            bool hasVertex = Vertex != null;
            ar.Boolean(ref hasVertex);
            if (hasVertex)
            {
                ushort ptTag = 0; ar.ClassTagOrRef("moPointRef_w", ref ptTag);
                ushort ptRef = 0x802C; ar.U16(ref ptRef);
                ushort ptSchema = 0x0002; ar.U16(ref ptSchema);
                uint ptFlags = 0x00400000; ar.U32(ref ptFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = Vertex!.EntityId; ar.U32(ref entId);
            }

            // 7. Items / PropagatingEdges (moCompEdge_c)
            var edges = Items ?? PropagatingEdges ?? Array.Empty<EntityReference>();
            ushort count = (ushort)edges.Count; ar.U16(ref count);
            foreach (var edge in edges)
            {
                ushort compEdgeTag = 0; ar.ClassTagOrRef("moCompEdge_c", ref compEdgeTag);
                ushort compEdgeRef = 0x802C; ar.U16(ref compEdgeRef);
                ushort compEdgeSchema = 0x0002; ar.U16(ref compEdgeSchema);
                uint compEdgeFlags = 0x00400000; ar.U32(ref compEdgeFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = edge.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);

                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Parameters
                double dist = 0; ar.F64(ref dist); Distance = dist;
                int endCond = 0; ar.I32(ref endCond); EndCondition = endCond;
                int extType = 0; ar.I32(ref extType); Type = extType;

                // Face
                bool hasFace = false; ar.Boolean(ref hasFace);
                if (hasFace && ar.Remaining >= 18)
                {
                    ushort faceTag = 0; ar.ReadClassTag(out string fCls, out faceTag, out _);
                    ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                    ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                    uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                    uint compId = 0; ar.U32(ref compId);
                    uint entId = 0; ar.U32(ref entId);
                    Face = new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId:X8}" };
                    Resolution.Face = true;
                    MarkResolved(nameof(Face));
                }

                // Vertex
                bool hasVertex = false; ar.Boolean(ref hasVertex);
                if (hasVertex && ar.Remaining >= 18)
                {
                    ushort ptTag = 0; ar.ReadClassTag(out string ptCls, out ptTag, out _);
                    ushort ptRef = 0; ar.U16(ref ptRef);
                    ushort ptSchema = 0; ar.U16(ref ptSchema);
                    uint ptFlags = 0; ar.U32(ref ptFlags);
                    uint compId = 0; ar.U32(ref compId);
                    uint entId = 0; ar.U32(ref entId);
                    Vertex = new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Vertex, Name = $"moPointRef_w:{entId:X8}" };
                    Resolution.Vertex = true;
                    MarkResolved(nameof(Vertex));
                }

                // Items
                if (ar.Remaining >= 2)
                {
                    ushort count = 0; ar.U16(ref count);
                    var edgeList = new List<EntityReference>();
                    for (int i = 0; i < count; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort compEdgeTag = 0; ar.ReadClassTag(out string compCls, out compEdgeTag, out _);
                        ushort compEdgeRef = 0; ar.U16(ref compEdgeRef);
                        ushort compEdgeSchema = 0; ar.U16(ref compEdgeSchema);
                        uint compEdgeFlags = 0; ar.U32(ref compEdgeFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        edgeList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Edge, Name = $"moCompEdge_c:{entId:X8}" });
                    }
                    if (edgeList.Count > 0)
                    {
                        Items = edgeList.AsReadOnly();
                        PropagatingEdges = edgeList.AsReadOnly();
                        Resolution.Items = true;
                        Resolution.PropagatingEdges = true;
                        MarkResolved(nameof(Items));
                        MarkResolved(nameof(PropagatingEdges));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.Distance = true;
                Resolution.EndCondition = true;
                Resolution.Type = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
                MarkResolved(nameof(Distance));
                MarkResolved(nameof(EndCondition));
                MarkResolved(nameof(Type));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedClassName = MfcClassName;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        int edgeOffset = MfcPrimitives.FindAscii(data, "moCompEdge_c");
        if (edgeOffset > 0)
        {
            int dataStart = edgeOffset + "moCompEdge_c".Length;
            var edge = FeatureSelectionBag.TryParseComponentEntityAt(
                data, dataStart, "moCompEdge_c", SelectionEntityType.Edge);
            if (edge != null)
            {
                Items = new[] { edge };
                PropagatingEdges = new[] { edge };
                Resolution.Items = true;
                Resolution.PropagatingEdges = true;
                MarkResolved(nameof(Items));
                MarkResolved(nameof(PropagatingEdges));
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static SurfaceExtendFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != MfcClassName) return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x70);
        if (ctx != null)
        {
            var edge = ctx.Selections.TryReadFirstComponentEntity("moCompEdge_c", SelectionEntityType.Edge);
            if (edge != null)
            {
                var definition = new SurfaceExtendFeatureModel
                {
                    SerializedClassName = MfcClassName,
                    SerializedTypeId = ctx.TypeId,
                    Items = new[] { edge },
                    PropagatingEdges = new[] { edge },
                    RawBlock = element.RawData
                };
                definition.Resolution.ClassName = true;
                definition.Resolution.SerializedTypeId = true;
                definition.Resolution.Items = true;
                definition.Resolution.PropagatingEdges = true;
                definition.MarkResolved(nameof(definition.Items));
                definition.MarkResolved(nameof(definition.PropagatingEdges));
                return definition;
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            if (block != null && offset >= 0 && offset < block.Length)
            {
                int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
                byte[] rawSlice = new byte[length];
                Buffer.BlockCopy(block, offset, rawSlice, 0, length);
                var ar = MfcArchive.CreateReader(rawSlice);
                var model = new SurfaceExtendFeatureModel();
                model.Serialize(ar);
                if ((model.Resolution.Distance && model.Distance.HasValue && model.Distance.Value > 0.0001) ||
                    (model.Resolution.Items && model.Items != null && model.Items.Count > 0))
                {
                    return model;
                }
            }
        }
        catch { }

        return null;
    }
}

/// <summary>Per-field evidence flags for <see cref="SurfaceOffsetFeatureModel"/>.</summary>
public sealed class SurfaceOffsetFeatureParameterResolution
{
    public bool ClassName { get; internal set; }
    public bool SerializedTypeId { get; internal set; }
    public bool Distance { get; internal set; }
    public bool Entities { get; internal set; }
    public bool Faces { get; internal set; }
    public bool Flip { get; internal set; }
    public bool BodyReferences { get; internal set; }
}

/// <summary>
/// Direct strongly-typed MFC model for Surface Offset feature (<c>moOffsetRefSurface_c</c>, TypeId <c>0x51</c>).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric bi-directional reading and writing.
/// Aligned with SolidWorks ISurfaceOffsetFeatureData.
/// </summary>
public sealed class SurfaceOffsetFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moOffsetRefSurface_c";
    public const uint HeaderTypeId = 0x51u;
    public const uint SwFeatureId = 62u;

    public string ClassName => SerializedClassName ?? MfcClassName;
    public string? FeatureName { get; set; } = "等距曲面1";
    public double? Version { get; set; } = 2.0;

    public string? SerializedClassName { get; set; }
    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public SurfaceOffsetFeatureParameterResolution Resolution { get; } = new();

    public double? Distance { get; set; }
    public IReadOnlyList<EntityReference>? Entities { get; set; }
    public IReadOnlyList<EntityReference>? Faces { get; set; }
    public bool? Flip { get; set; }
    public IReadOnlyList<EntityReference>? BodyReferences { get; set; }

    public int? EntitiesCount => Entities?.Count;
    public int? FacesCount => Faces?.Count;
    public int? BodyReferenceCount => BodyReferences?.Count;

    public byte[]? RawBlock { get; set; }

    public SurfaceOffsetFeatureModel()
    {
    }

    public SurfaceOffsetFeatureModel(
        double distance,
        IEnumerable<EntityReference>? entities = null,
        IEnumerable<EntityReference>? faces = null,
        bool flip = false)
    {
        FeatureName = "等距曲面1";
        SerializedClassName = MfcClassName;
        SerializedTypeId = HeaderTypeId;
        Distance = distance;
        Flip = flip;
        if (entities != null)
        {
            var list = entities.ToList().AsReadOnly();
            Entities = list;
            BodyReferences = list;
            Resolution.Entities = true;
            Resolution.BodyReferences = true;
            MarkResolved(nameof(Entities));
            MarkResolved(nameof(BodyReferences));
        }
        if (faces != null)
        {
            Faces = faces.ToList().AsReadOnly();
            Resolution.Faces = true;
            MarkResolved(nameof(Faces));
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        Resolution.Distance = true;
        Resolution.Flip = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
        MarkResolved(nameof(Distance));
        MarkResolved(nameof(Flip));
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                ar.WriteBytes(block);
                return;
            }

            // 1. Tag & Class
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004; ar.U16(ref schema);

            // 2. Feature Name
            string? name = FeatureName ?? "等距曲面1";
            ar.WChar(ref name);
            FeatureName = name;

            // 3. Header payload
            double ver = Version ?? 2.0; ar.F64(ref ver);
            uint tid = TypeId != 0 ? TypeId : HeaderTypeId; ar.U32(ref tid);
            uint zero = 0; ar.U32(ref zero);
            string? emptyStr = string.Empty; ar.WChar(ref emptyStr);

            // 4. Distance & Flip
            double dist = Distance ?? 0.005; ar.F64(ref dist);
            bool flip = Flip ?? false; ar.Boolean(ref flip);

            // 5. Faces (moCompFace_c)
            var faces = Faces ?? Array.Empty<EntityReference>();
            ushort faceCount = (ushort)faces.Count; ar.U16(ref faceCount);
            foreach (var face in faces)
            {
                ushort compFaceTag = 0; ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
                ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
                uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = face.EntityId; ar.U32(ref entId);
            }

            // 6. Bodies / Entities (moCompSurfaceBody_c)
            var bodies = Entities ?? BodyReferences ?? Array.Empty<EntityReference>();
            ushort bodyCount = (ushort)bodies.Count; ar.U16(ref bodyCount);
            foreach (var body in bodies)
            {
                ushort compBodyTag = 0; ar.ClassTagOrRef("moCompSurfaceBody_c", ref compBodyTag);
                ushort compBodyRef = 0x802C; ar.U16(ref compBodyRef);
                ushort compBodySchema = 0x0002; ar.U16(ref compBodySchema);
                uint compBodyFlags = 0x00400000; ar.U32(ref compBodyFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = body.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                SerializedClassName = cls;
                ushort schema = 0; ar.U16(ref schema);

                string? name = null; ar.WChar(ref name); FeatureName = name;
                double ver = 0; ar.F64(ref ver); Version = ver;
                uint tid = 0; ar.U32(ref tid); SerializedTypeId = tid;
                uint zero = 0; ar.U32(ref zero);
                string? empty = null; ar.WChar(ref empty);

                // Distance & Flip
                double dist = 0; ar.F64(ref dist); Distance = dist;
                bool flip = false; ar.Boolean(ref flip); Flip = flip;

                // Faces
                if (ar.Remaining >= 2)
                {
                    ushort faceCount = 0; ar.U16(ref faceCount);
                    var faceList = new List<EntityReference>();
                    for (int i = 0; i < faceCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort faceTag = 0; ar.ReadClassTag(out string fCls, out faceTag, out _);
                        ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                        ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                        uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        faceList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId:X8}" });
                    }
                    if (faceList.Count > 0)
                    {
                        Faces = faceList.AsReadOnly();
                        Resolution.Faces = true;
                        MarkResolved(nameof(Faces));
                    }
                }

                // Bodies / Entities
                if (ar.Remaining >= 2)
                {
                    ushort bodyCount = 0; ar.U16(ref bodyCount);
                    var bodyList = new List<EntityReference>();
                    for (int i = 0; i < bodyCount; i++)
                    {
                        if (ar.Remaining < 6) break;
                        ushort bodyTag = 0; ar.ReadClassTag(out string bCls, out bodyTag, out _);
                        ushort compBodyRef = 0; ar.U16(ref compBodyRef);
                        ushort compBodySchema = 0; ar.U16(ref compBodySchema);
                        uint compBodyFlags = 0; ar.U32(ref compBodyFlags);
                        uint compId = 0; ar.U32(ref compId);
                        uint entId = 0; ar.U32(ref entId);
                        bodyList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Body, Name = $"moCompSurfaceBody_c:{entId:X8}" });
                    }
                    if (bodyList.Count > 0)
                    {
                        Entities = bodyList.AsReadOnly();
                        BodyReferences = bodyList.AsReadOnly();
                        Resolution.Entities = true;
                        Resolution.BodyReferences = true;
                        MarkResolved(nameof(Entities));
                        MarkResolved(nameof(BodyReferences));
                    }
                }

                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.Distance = true;
                Resolution.Flip = true;
                MarkResolved(nameof(ClassName));
                MarkResolved(nameof(TypeId));
                MarkResolved(nameof(Distance));
                MarkResolved(nameof(Flip));
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedClassName = MfcClassName;
                SerializedTypeId = HeaderTypeId;
                ParseFromRawBlock(block);
            }
        }
    }

    private void ParseFromRawBlock(byte[] data)
    {
        int bodyOffset = MfcPrimitives.FindAscii(data, "moCompSurfaceBody_c");
        if (bodyOffset > 0)
        {
            int dataStart = bodyOffset + "moCompSurfaceBody_c".Length;
            var body = FeatureSelectionBag.TryParseComponentEntityAt(
                data, dataStart, "moCompSurfaceBody_c", SelectionEntityType.Body);
            if (body != null)
            {
                Entities = new[] { body };
                BodyReferences = new[] { body };
                Resolution.Entities = true;
                Resolution.BodyReferences = true;
                MarkResolved(nameof(Entities));
                MarkResolved(nameof(BodyReferences));
            }
        }
        Resolution.ClassName = true;
        Resolution.SerializedTypeId = true;
        MarkResolved(nameof(ClassName));
        MarkResolved(nameof(TypeId));
    }

    internal static SurfaceOffsetFeatureModel? Parse(MfcElement? element)
    {
        if (element == null) return null;
        if (element.ClassName != MfcClassName) return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x71);
        if (ctx != null)
        {
            var body = ctx.Selections.TryReadComponentEntities("moCompSurfaceBody_c", SelectionEntityType.Body);
            if (body != null)
            {
                var definition = new SurfaceOffsetFeatureModel
                {
                    SerializedClassName = MfcClassName,
                    SerializedTypeId = ctx.TypeId,
                    Entities = body,
                    BodyReferences = body,
                    RawBlock = element.RawData
                };
                definition.Resolution.ClassName = true;
                definition.Resolution.SerializedTypeId = true;
                definition.Resolution.Entities = true;
                definition.Resolution.BodyReferences = true;
                definition.MarkResolved(nameof(definition.Entities));
                definition.MarkResolved(nameof(definition.BodyReferences));
                return definition;
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            if (block != null && offset >= 0 && offset < block.Length)
            {
                int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
                byte[] rawSlice = new byte[length];
                Buffer.BlockCopy(block, offset, rawSlice, 0, length);
                var ar = MfcArchive.CreateReader(rawSlice);
                var model = new SurfaceOffsetFeatureModel();
                model.Serialize(ar);
                if ((model.Resolution.Distance && model.Distance.HasValue && model.Distance.Value > 0.0001) ||
                    (model.Resolution.Entities && model.Entities != null && model.Entities.Count > 0))
                {
                    return model;
                }
            }
        }
        catch { }

        return null;
    }
}

#endregion

/// <summary>
/// Unified facade for Phase 4 surface feature parsers, delegating to the
/// strongly-typed surface models.
/// </summary>
internal static class SurfaceFeatureParsers
{
    public static IFeatureData? ParseSurfaceExtrude(MfcElement? element) =>
        element == null ? null : SurfaceExtrudeFeatureParser.Parse(element);

    public static IFeatureData? ParseSurfaceRevolve(MfcElement? element) =>
        element == null ? null : SurfaceRevolveFeatureParser.Parse(element);

    public static IFeatureData? ParsePlanarSurface(MfcElement? element) =>
        element == null ? null : PlanarSurfaceFeatureModel.Parse(element);

    public static IFeatureData? ParseFillSurface(MfcElement? element) =>
        element == null ? null : SurfaceFillFeatureModel.Parse(element);

    public static IFeatureData? ParseSurfaceKnit(MfcElement? element) =>
        element == null ? null : SurfaceKnitFeatureModel.Parse(element);

    public static IFeatureData? ParseSurfaceTrim(MfcElement? element) =>
        element == null ? null : SurfaceTrimFeatureModel.Parse(element);

    public static IFeatureData? ParseSurfaceExtend(MfcElement? element) =>
        element == null ? null : SurfaceExtendFeatureModel.Parse(element);

    public static IFeatureData? ParseSurfaceOffset(MfcElement? element) =>
        element == null ? null : SurfaceOffsetFeatureModel.Parse(element);
}