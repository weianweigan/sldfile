using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct strongly-typed MFC model for Draft feature (moDraft_c, FolderHeader TypeId 0x51).
/// Implements IMfcFeatureModel with symmetric bi-directional CArchive serialization.
/// Aligned with SolidWorks IDraftFeatureData2.
/// </summary>
public sealed class DraftFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moDraft_c";
    public const uint HeaderTypeId = 0x51u;
    public const uint SwFeatureId = 3u; // swFeatureNameID_e.swFmDraft

    public const int AngleDimRelativeOffset = 0x3EC;
    public const int AngleParameterRelativeOffset = 0x400;
    public const int AngleValueRelativeOffset = 0x438;
    public const int SecondAngleRelativeOffset = 0x652;
    public const int DirectionSpecRelativeOffset = 0x83B;
    public const int PlaneRefRelativeOffset = 0x858;
    public const int CompFaceRelativeOffset = 0x86A;
    public static readonly int[] AngleRelativeOffsets = { AngleValueRelativeOffset, SecondAngleRelativeOffset };

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

    /// <summary>Draft angle in radians.</summary>
    public double? Angle { get; set; }

    /// <summary>swDraftType_e: 0 NeutralPlane, 1 PartingLine, 2 StepDraft.</summary>
    public int? Type { get; set; }

    /// <summary>Reverse draft direction.</summary>
    public bool? ReverseDirection { get; set; }

    /// <summary>Allow reduced angle.</summary>
    public bool? AllowReducedAngle { get; set; }

    /// <summary>Neutral plane reference (mark 2 in native SW).</summary>
    public EntityReference? NeutralPlane { get; set; }

    /// <summary>Pull direction reference.</summary>
    public DirectionReference? DirectionPull { get; set; }

    /// <summary>Ordered faces to draft (mark 1 in native SW).</summary>
    public IReadOnlyList<EntityReference>? FacesToDraft { get; set; }

    /// <summary>Ordered parting-line edges.</summary>
    public IReadOnlyList<EntityReference>? PartingLines { get; set; }

    /// <summary>swDraftFacePropagationType_e: 0 None, 1 Tangent, 2 AllLoops, etc.</summary>
    public int? FacePropagation { get; set; }

    /// <summary>swDraftStepType_e: 3 TaperedStep, 6 Perpendicular.</summary>
    public int? StepType { get; set; }

    /// <summary>Per-field evidence resolution flags for backward compatibility.</summary>
    public DraftFeatureParameterResolution Resolution { get; } = new();

    /// <summary>Unified dimension parameter model (moAngleParameter_c).</summary>
    public MoAngleParameterModel? Parameter =>
        Angle.HasValue ? new MoAngleParameterModel("Angle@" + (FeatureName ?? "Draft3deg"), Angle.Value) : null;

    /// <summary>Unified dimension display model.</summary>
    public MoDisplayAngularDimModel? DisplayDimension =>
        Angle.HasValue ? new MoDisplayAngularDimModel() : null;

    /// <summary>Unified dimension object model.</summary>
    public DimensionModel? Dimension =>
        Parameter != null && DisplayDimension != null ? new DimensionModel(Parameter, DisplayDimension) : null;

    public IReadOnlyList<DimensionModel>? Dimensions =>
        Dimension != null ? new[] { Dimension } : null;

    // Raw payload blocks for lossless roundtrip
    public byte[]? DisplayDimPayload { get; set; }
    public byte[]? AngleParamPayload { get; set; }
    public byte[]? DirectionSpecPayload { get; set; }
    public byte[]? RawBlock { get; set; }

    public DraftFeatureModel()
    {
    }

    public DraftFeatureModel(double angleRadians, EntityReference? neutralPlane = null, IEnumerable<EntityReference>? facesToDraft = null)
    {
        FeatureName = "Draft3deg";
        SerializedTypeId = HeaderTypeId;
        Angle = angleRadians;
        NeutralPlane = neutralPlane;
        if (facesToDraft != null)
            FacesToDraft = facesToDraft.ToList().AsReadOnly();
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (Angle.HasValue && block.Length > SecondAngleRelativeOffset + 8)
                {
                    int[] slots = { AngleValueRelativeOffset, SecondAngleRelativeOffset };
                    MfcParameterReader.PatchDoubleSlots(block, slots, Angle.Value);
                }
                ar.WriteBytes(block);
                return;
            }

            // Direct structured MFC serialization
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);
            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName ?? "Draft";
            ar.WChar(ref name);
            FeatureName = name;

            double ver = Version ?? 2.0;
            ar.F64(ref ver);

            uint tid = TypeId != 0 ? TypeId : HeaderTypeId;
            ar.U32(ref tid);

            double ang = Angle ?? (3.0 * Math.PI / 180.0);
            ar.F64(ref ang);

            int type = Type ?? 0;
            ar.I32(ref type);

            bool rev = ReverseDirection ?? false;
            ar.Boolean(ref rev);

            // Neutral plane
            bool hasNeutralPlane = NeutralPlane != null;
            ar.Boolean(ref hasNeutralPlane);
            if (hasNeutralPlane)
            {
                ushort compFaceTag = 0;
                ar.ClassTagOrRef("moCompFace_c", ref compFaceTag);
                ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
                ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
                uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
                uint compId = 0x6A; ar.U32(ref compId);
                uint entId = NeutralPlane!.EntityId; ar.U32(ref entId);
            }

            // Faces to draft
            var draftFaces = FacesToDraft ?? Array.Empty<EntityReference>();
            ushort draftCount = (ushort)draftFaces.Count;
            ar.U16(ref draftCount);
            foreach (var face in draftFaces)
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

                double ang = 0;
                ar.F64(ref ang);
                Angle = ang;

                int type = 0;
                ar.I32(ref type);
                Type = type;

                bool rev = false;
                ar.Boolean(ref rev);
                ReverseDirection = rev;

                bool hasNeutralPlane = false;
                ar.Boolean(ref hasNeutralPlane);
                if (hasNeutralPlane)
                {
                    ushort compFaceTag = 0;
                    ar.ReadClassTag(out string compCls, out compFaceTag, out _);
                    ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                    ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                    uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                    uint compId = 0; ar.U32(ref compId);
                    uint entId = 0; ar.U32(ref entId);
                    NeutralPlane = new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId}" };
                }

                ushort draftCount = 0;
                ar.U16(ref draftCount);
                var list = new List<EntityReference>();
                for (int i = 0; i < draftCount; i++)
                {
                    ushort compFaceTag = 0;
                    ar.ReadClassTag(out string compCls, out compFaceTag, out _);
                    ushort compFaceRef = 0; ar.U16(ref compFaceRef);
                    ushort compFaceSchema = 0; ar.U16(ref compFaceSchema);
                    uint compFaceFlags = 0; ar.U32(ref compFaceFlags);
                    uint compId = 0; ar.U32(ref compId);
                    uint entId = 0; ar.U32(ref entId);
                    list.Add(new EntityReference { EntityId = entId, EntityType = SelectionEntityType.Face, Name = $"moCompFace_c:{entId}" });
                }
                FacesToDraft = list.AsReadOnly();
                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;
                Resolution.Angle = true;
                Resolution.NeutralPlane = NeutralPlane != null;
            }
            else
            {
                ar.Position = startPos;
                byte[] block = ar.ReadRemainingBytes();
                RawBlock = block;
                SerializedTypeId = HeaderTypeId;
                Resolution.ClassName = true;
                Resolution.SerializedTypeId = true;

                if (block.Length > SecondAngleRelativeOffset + 8)
                {
                    double ang = BitConverter.ToDouble(block, AngleValueRelativeOffset);
                    if (ang > 0 && ang < Math.PI / 2 && !double.IsNaN(ang))
                    {
                        Angle = ang;
                        Resolution.Angle = true;
                    }
                }
                ParseSubObjects(block);
            }
        }
    }

    private void ParseSubObjects(byte[] data)
    {
        // 1. Locate moAngleParameter_c and read angle
        int angleParamOffset = MfcPrimitives.FindAscii(data, "moAngleParameter_c");
        if (angleParamOffset > 0)
        {
            int slot1 = angleParamOffset + 56;
            if (slot1 + 8 <= data.Length)
            {
                double ang = BitConverter.ToDouble(data, slot1);
                if (ang > 0 && ang < Math.PI / 2 && !double.IsNaN(ang))
                {
                    Angle = ang;
                    Resolution.Angle = true;
                }
            }
        }

        // 2. Locate neutral plane moCompFace_c
        int compFaceOffset = MfcPrimitives.FindAscii(data, "moCompFace_c");
        if (compFaceOffset > 0)
        {
            int dataStart = compFaceOffset + "moCompFace_c".Length;
            var face = FeatureSelectionBag.TryParseComponentEntityAt(
                data, dataStart, "moCompFace_c", SelectionEntityType.Face);
            if (face != null)
            {
                NeutralPlane = face;
                Resolution.NeutralPlane = true;
            }
        }
    }

    private byte[] GetSynchronizedAngleBlock(double angle)
    {
        if (AngleParamPayload != null && AngleParamPayload.Length > 0)
        {
            byte[] block = (byte[])AngleParamPayload.Clone();
            int[] slots = { AngleValueRelativeOffset, SecondAngleRelativeOffset };
            MfcParameterReader.PatchDoubleSlots(block, slots, angle);
            return block;
        }

        byte[] template = DraftShellAuthoringSegments.DraftTemplate;
        byte[] cloned = (byte[])template.Clone();
        int[] defaultSlots = { AngleValueRelativeOffset, SecondAngleRelativeOffset };
        MfcParameterReader.PatchDoubleSlots(cloned, defaultSlots, angle);
        return cloned;
    }

    private void EmitDefaultDirectionSpecPayload(MfcArchive ar)
    {
        ushort compFaceRef = 0x802C; ar.U16(ref compFaceRef);
        ushort compFaceSchema = 0x0002; ar.U16(ref compFaceSchema);
        uint compFaceFlags = 0x00400000; ar.U32(ref compFaceFlags);
        uint compId = 0x6A; ar.U32(ref compId);
        uint entId = NeutralPlane?.EntityId != 0 ? (NeutralPlane?.EntityId ?? 0x6A) : 0x6A;
        ar.U32(ref entId);
    }

    internal static DraftFeatureModel? Parse(MfcElement element)
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
            var angleDim = ctx.Nodes.FirstOrDefault(t => t.ClassName == "AngleDim_c" && t.TagOffset > ctx.Start);
            var angleParam = ctx.Nodes.FirstOrDefault(t => t.ClassName == "moAngleParameter_c" && t.TagOffset > ctx.Start);
            var directionSpec = ctx.Nodes.FirstOrDefault(t => t.ClassName == "moDirectionSpec_c" && t.TagOffset > ctx.Start);
            var planeRef = ctx.Nodes.FirstOrDefault(t => t.ClassName == "moPlaneRef_w" && t.TagOffset > ctx.Start);
            var compFace = ctx.Nodes.FirstOrDefault(t => t.ClassName == "moCompFace_c" && t.TagOffset > ctx.Start);

            if (angleDim != null && angleDim.TagOffset - ctx.Start == AngleDimRelativeOffset &&
                angleParam != null && angleParam.TagOffset - ctx.Start == AngleParameterRelativeOffset &&
                directionSpec != null && directionSpec.TagOffset - ctx.Start == DirectionSpecRelativeOffset &&
                planeRef != null && planeRef.TagOffset - ctx.Start == PlaneRefRelativeOffset &&
                compFace != null && compFace.TagOffset - ctx.Start == CompFaceRelativeOffset)
            {
                if (ctx.Parameters.TryReadVerifiedDoubleSlots(
                        ctx.Start,
                        AngleRelativeOffsets,
                        out double angle,
                        minExclusive: 0.0,
                        maxInclusive: Math.PI / 2 - 1e-15,
                        tolerance: 1e-15))
                {
                    var neutralPlane = FeatureSelectionBag.TryParseComponentEntityAt(
                        ctx.RawData,
                        compFace.DataStart,
                        "moCompFace_c",
                        SelectionEntityType.Face);
                    if (neutralPlane != null)
                    {
                        var definition = new DraftFeatureModel
                        {
                            SerializedTypeId = ctx.TypeId,
                            Angle = angle,
                            NeutralPlane = neutralPlane
                        };
                        definition.Resolution.ClassName = true;
                        definition.Resolution.SerializedTypeId = true;
                        definition.Resolution.Angle = true;
                        definition.Resolution.NeutralPlane = true;
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
            var model = new DraftFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.ClassName && model.Angle.HasValue && model.Angle.Value >= 0.0001 && model.Angle.Value <= Math.PI * 2 && !double.IsNaN(model.Angle.Value))
            {
                model.Resolution.ClassName = true;
                model.Resolution.SerializedTypeId = true;
                model.Resolution.Angle = true;
                if (model.NeutralPlane != null)
                    model.Resolution.NeutralPlane = true;
                return model;
            }
        }
        catch { }

        return null;
    }
}
