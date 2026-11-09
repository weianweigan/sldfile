using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct strongly-typed MFC model for Shell feature (moShell_c, FolderHeader TypeId 0x51).
/// Implements IMfcFeatureModel with symmetric bi-directional CArchive serialization.
/// Aligned with SolidWorks IShellFeatureData.
/// </summary>
public sealed class ShellFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moShell_c";
    public const uint HeaderTypeId = 0x51u;
    public const uint SwFeatureId = 8u; // swFeatureNameID_e.swFmShell

    /// <summary>Absolute offsets from the moShell_c tag to the six measured thickness slots.</summary>
    public static readonly int[] ThicknessSlotOffsets = { 0x51C, 0x554, 0x6A2, 0x6BA, 0x74C, 0x764 };
    public const int ThreeDRadiusRelativeOffset = 0x4DF;

    public string ClassName => MfcClassName;
    public string? FeatureName { get; set; }
    public double? Version { get; set; }

    /// <summary>Folder-header type code; null until a sample layout is decoded.</summary>
    public uint? SerializedTypeId { get; set; }

    /// <summary>0 when SerializedTypeId is unresolved; see Resolution.</summary>
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    /// <summary>Default wall thickness in meters.</summary>
    public double? Thickness { get; set; }

    /// <summary>Shell direction (0: Inward, 1: Outward).</summary>
    public int? Direction { get; set; }

    /// <summary>Ordered faces to remove.</summary>
    public IReadOnlyList<EntityReference>? FacesRemoved { get; set; }

    /// <summary>Count derived from FacesRemoved.</summary>
    public int? FacesRemovedCount => FacesRemoved?.Count;

    /// <summary>Ordered faces with non-default thickness.</summary>
    public IReadOnlyList<EntityReference>? MultipleThicknessFaces { get; set; }

    /// <summary>Thickness values in meters, index-aligned with MultipleThicknessFaces.</summary>
    public IReadOnlyList<double>? MultipleThicknessValues { get; set; }

    /// <summary>Per-field evidence resolution flags for backward compatibility.</summary>
    public ShellFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Unified dimension parameter model (moLengthParameter_c).</summary>
    public MoLengthParameterModel? Parameter =>
        Thickness.HasValue ? new MoLengthParameterModel("Thickness@" + (FeatureName ?? "Shell"), Thickness.Value) : null;

    /// <summary>Unified dimension display model.</summary>
    public MoDisplayRadialDimModel? DisplayDimension =>
        Thickness.HasValue ? new MoDisplayRadialDimModel() : null;

    /// <summary>Unified dimension object model.</summary>
    public DimensionModel? Dimension =>
        Parameter != null && DisplayDimension != null ? new DimensionModel(Parameter, DisplayDimension) : null;

    public IReadOnlyList<DimensionModel>? Dimensions =>
        Dimension != null ? new[] { Dimension } : null;

    // Payload sub-objects and raw blocks for lossless roundtrips
    public byte[]? HeaderPayload { get; set; }
    public byte[]? ThreeDRadiusDimPayload { get; set; }
    public byte[]? SelectionPayload { get; set; }
    public byte[]? TailPayload { get; set; }
    public byte[]? RawBlock { get; set; }

    public ShellFeatureModel()
    {
    }

    public ShellFeatureModel(double thickness, IEnumerable<EntityReference>? facesRemoved = null, int direction = 0)
    {
        FeatureName = "Shell";
        SerializedTypeId = HeaderTypeId;
        Thickness = thickness;
        Direction = direction;
        if (facesRemoved != null)
            FacesRemoved = facesRemoved.ToList().AsReadOnly();
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (Thickness.HasValue && block.Length > 0x764 + 8)
                {
                    MfcParameterReader.PatchDoubleSlots(block, ThicknessSlotOffsets, Thickness.Value);
                }
                ar.WriteBytes(block);
                return;
            }

            // Direct structured MFC serialization
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName ?? "Shell";
            ar.WChar(ref name);
            FeatureName = name;

            double ver = Version ?? 2.0;
            ar.F64(ref ver);

            uint tid = TypeId != 0 ? TypeId : HeaderTypeId;
            ar.U32(ref tid);

            double th = Thickness ?? 0.002;
            ar.F64(ref th);

            int dir = Direction ?? 0;
            ar.I32(ref dir);

            var faces = FacesRemoved ?? Array.Empty<EntityReference>();
            ushort faceCount = (ushort)faces.Count;
            ar.U16(ref faceCount);
            foreach (var face in faces)
            {
                ushort compFaceTag = 0;
                ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
                ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
                uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = face.EntityId; ar.U32(ref entId);
            }
        }
        else
        {
            int startPos = ar.Position;
            ushort refTag = 0;
            var kind = ar.ReadClassTag(out string cls, out refTag, out _);
            if (cls == MfcClassName)
            {
                ushort schema = 0;
                ar.U16(ref schema);

                string? name = null;
                ar.WChar(ref name);
                FeatureName = name;

                double ver = 0;
                ar.F64(ref ver);
                Version = ver;

                uint tid = 0;
                ar.U32(ref tid);
                SerializedTypeId = tid;

                double th = 0;
                ar.F64(ref th);
                Thickness = th;

                int dir = 0;
                ar.I32(ref dir);
                Direction = dir;

                ushort faceCount = 0;
                ar.U16(ref faceCount);
                var faceList = new List<EntityReference>();
                for (int i = 0; i < faceCount; i++)
                {
                    ushort compFaceTag = 0;
                    ar.ReadClassTag(out string compCls, out compFaceTag, out _);
                    ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                    ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                    uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                    uint compId = 0; ar.U32(ref compId);
                    uint entId = 0; ar.U32(ref entId);
                    faceList.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId}" });
                }
                FacesRemoved = faceList.AsReadOnly();
                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.Thickness = true;
                Resolution.FacesRemoved = true;
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;

                if (block.Length > 0x764 + 8)
                {
                    double th = BitConverter.ToDouble(block, ThicknessSlotOffsets[0]);
                    if (th > 0 && th <= 0.1 && !double.IsNaN(th))
                    {
                        Thickness = th;
                        Resolution.Thickness = true;
                    }
                }
                ParseSubObjects(block);
            }
        }
    }

    private void ParseSubObjects(byte[] data)
    {
        // 1. Locate moCompFace_c for removed faces
        int faceTagOffset = MfcPrimitives.FindAscii(data, "moCompFace_c");
        if (faceTagOffset > 0)
        {
            int dataStart = faceTagOffset + "moCompFace_c".Length;
            var face = FeatureSelectionBag.TryParseComponentEntityAt(
                data, dataStart, "moCompFace_c", SelectionEntityType.Face);
            if (face != null)
            {
                FacesRemoved = new[] { face };
                Resolution.FacesRemoved = true;
            }
        }

        // 2. Locate ThreeDRadiusDim_c and read thickness
        int radiusDimOffset = MfcPrimitives.FindAscii(data, "ThreeDRadiusDim_c");
        if (radiusDimOffset > 0)
        {
            // First positive thickness slot is at radiusDimOffset + 61 (relative 0x51C)
            int slot1 = radiusDimOffset + 61;
            if (slot1 + 8 <= data.Length)
            {
                double th = BitConverter.ToDouble(data, slot1);
                if (th > 0 && th <= 0.1 && !double.IsNaN(th))
                {
                    Thickness = th;
                    Resolution.Thickness = true;
                }
            }
        }
    }

    private byte[] GetSynchronizedRadiusDimBlock(double thickness)
    {
        if (ThreeDRadiusDimPayload != null && ThreeDRadiusDimPayload.Length > 0)
        {
            byte[] block = (byte[])ThreeDRadiusDimPayload.Clone();
            MfcParameterReader.PatchDoubleSlots(block, ThicknessSlotOffsets, thickness);
            return block;
        }

        byte[] template = DraftShellAuthoringSegments.ShellTemplate;
        byte[] cloned = (byte[])template.Clone();
        MfcParameterReader.PatchDoubleSlots(cloned, ThicknessSlotOffsets, thickness);
        return cloned;
    }

    private void EmitDefaultSelectionPayload(MfcArchive ar)
    {
        ushort compFaceTag = 0;
        ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
        if (FacesRemoved != null && FacesRemoved.Count > 0)
        {
            var face = FacesRemoved[0];
            ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
            ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
            uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
            uint compId = 0x6A; ar.U32(ref compId);
            uint entId = face.EntityId != 0 ? face.EntityId : 0x6A; ar.U32(ref entId);
        }
    }

    private void EmitDefaultTailPayload(MfcArchive ar)
    {
        ushort compEdgeTag = 0;
        ar.ClassTagOrRef("moCompEdge_c", ref compEdgeTag);
        ushort edgeRefTag = 0;
        ar.ClassTagOrRef("moEdgeRef_c", ref edgeRefTag);
    }

    internal static ShellFeatureModel? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: HeaderTypeId,
            maxTypeId: HeaderTypeId,
            expectedStreamVersion: 0x70);
        if (ctx != null)
        {
            var radiusDim = ctx.Nodes.FirstOrDefault(t => t.ClassName == "ThreeDRadiusDim_c" && t.TagOffset > ctx.Start);
            if (radiusDim != null && radiusDim.TagOffset - ctx.Start == ThreeDRadiusRelativeOffset)
            {
                if (ctx.Parameters.TryReadVerifiedDoubleSlots(
                        ctx.Start,
                        ThicknessSlotOffsets,
                        out double thickness,
                        minExclusive: 0.0,
                        maxInclusive: 0.1,
                        tolerance: 1e-15))
                {
                    var faceChildren = element.Children.Where(c => c.ClassName == "moCompFace_c").ToList();
                    if (faceChildren.Count == 1)
                    {
                        var face = FeatureSelectionBag.TryParseComponentEntityAt(
                            ctx.RawData,
                            faceChildren[0].DataStart,
                            "moCompFace_c",
                            SelectionEntityType.Face);
                        if (face != null)
                        {
                            var definition = new ShellFeatureModel
                            {
                                SerializedTypeId = ctx.TypeId,
                                Thickness = thickness,
                                FacesRemoved = new[] { face }
                            };
                            definition.Resolution.ClassName = true;
                            definition.Resolution.SerializedTypeId = true;
                            definition.Resolution.Thickness = true;
                            definition.Resolution.FacesRemoved = true;
                            return definition;
                        }
                    }
                }
            }
        }

        try
        {
            byte[] block = element.SourceData;
            int offset = element.TagOffset;
            int length = (element.DataEnd > offset) ? element.DataEnd - offset : (block.Length - offset);
            byte[] rawSlice = new byte[length];
            Buffer.BlockCopy(block, offset, rawSlice, 0, length);
            var ar = MfcArchive.CreateReader(rawSlice);
            var model = new ShellFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.ClassName && model.Thickness.HasValue && model.Thickness.Value >= 0.0001 && model.Thickness.Value <= 1.0 && !double.IsNaN(model.Thickness.Value))
            {
                model.Resolution.ClassName = true;
                model.Resolution.SerializedTypeId = true;
                model.Resolution.Thickness = true;
                if (model.FacesRemoved != null && model.FacesRemoved.Count > 0)
                    model.Resolution.FacesRemoved = true;
                return model;
            }
        }
        catch { }

        return null;
    }
}
