using System;
using System.Collections.Generic;
using SldFile;
using SldFile.Binary;

using System.Linq;

namespace SldFile.Mfc;

/// <summary>
/// Direct strongly-typed MFC model for moRib_c (Rib feature).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric reading and writing.
/// Fully integrates with parameter driving models (MoLengthParameterModel, MoAngleParameterModel).
/// </summary>
public sealed class RibFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moRib_c";
    public const uint StandardTypeId = 0x51u; // 81 in modern 2026
    public const uint LegacyTypeId = 0x29u;   // 41 in 0x6C
    public const uint SwFeatureId = 38u;     // swFeatureNameID_e.swFmRib

    internal static IFeatureData? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreateWithFolderHeader(
            doc, className, typeId, start, end, out var header, requiredClassName: MfcClassName);
        if (ctx == null)
            return null;

        var data = new RibFeatureModel
        {
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        double? thickness = FindRibThickness(ctx.RawData, ctx.Start, ctx.FindNextTopLevelFeatureBoundary());
        if (thickness.HasValue)
        {
            data.Thickness = thickness;
            data.Resolution.Thickness = true;
        }

        if (TryReadRibOptions(ctx, out var options))
        {
            data.IsTwoSided = options.IsTwoSided;
            data.ReverseThicknessDir = options.ReverseThicknessDir;
            data.EnableDraft = options.EnableDraft;
            data.DraftOutward = options.DraftOutward;
            data.DraftFromWall = options.DraftFromWall;
            data.ExtrusionDirection = options.ExtrusionDirection;
            data.DraftAngle = options.DraftAngle;
            data.RefSketchIndex = options.RefSketchIndex;
            data.FlipSide = options.FlipSide;
            data.Resolution.IsTwoSided = true;
            data.Resolution.ReverseThicknessDir = true;
            data.Resolution.EnableDraft = true;
            data.Resolution.DraftOutward = true;
            data.Resolution.DraftFromWall = true;
            data.Resolution.ExtrusionDirection = true;
            data.Resolution.DraftAngle = options.DraftAngle.HasValue || !options.EnableDraft;
            data.Resolution.RefSketchIndex = true;
            data.Resolution.FlipSide = true;
        }

        return data;
    }

    internal static double? FindRibThickness(byte[] raw, int start, int end)
    {
        if (raw == null || start < 0) return null;
        int limit = Math.Min(end, raw.Length);
        for (int i = start; i + 30 <= limit; i++)
        {
            double? thickness = MfcParameterReader.ReadDoubleDescriptorAfterName(raw, i);
            if (thickness.HasValue && thickness.Value > 0 && thickness.Value <= 0.1)
                return thickness.Value;
        }
        return null;
    }

    private readonly struct RibOptions
    {
        internal RibOptions(bool isTwoSided, bool reverseThicknessDir, bool enableDraft,
            bool draftOutward, bool draftFromWall, int extrusionDirection, double? draftAngle,
            int refSketchIndex, bool flipSide)
        {
            IsTwoSided = isTwoSided;
            ReverseThicknessDir = reverseThicknessDir;
            EnableDraft = enableDraft;
            DraftOutward = draftOutward;
            DraftFromWall = draftFromWall;
            ExtrusionDirection = extrusionDirection;
            DraftAngle = draftAngle;
            RefSketchIndex = refSketchIndex;
            FlipSide = flipSide;
        }

        internal bool IsTwoSided { get; }
        internal bool ReverseThicknessDir { get; }
        internal bool EnableDraft { get; }
        internal bool DraftOutward { get; }
        internal bool DraftFromWall { get; }
        internal int ExtrusionDirection { get; }
        internal double? DraftAngle { get; }
        internal int RefSketchIndex { get; }
        internal bool FlipSide { get; }
    }

    private static bool TryReadRibOptions(FeatureWindowContext ctx, out RibOptions options)
    {
        options = default;
        byte[] raw = ctx.RawData;
        int start = ctx.Start;
        if (ctx.StreamVersion != 0x006C || start + 0x158 > raw.Length)
            return false;
        for (int i = 0x140; i <= 0x14D; i++)
            if (raw[start + i] != 0) return false;
        byte a = raw[start + 0x14E];
        byte b = raw[start + 0x14F];
        byte refSketch = raw[start + 0x152];
        if ((a & ~0x97) != 0 || (b & ~0x09) != 0) return false;
        if (refSketch < 1 || refSketch > 3) return false;
        if (raw[start + 0x150] != 0 || raw[start + 0x151] != 0 ||
            raw[start + 0x153] != 0 ||
            raw[start + 0x154] != 0 || raw[start + 0x155] != 0 ||
            raw[start + 0x156] != 0x03 || raw[start + 0x157] != 0x81)
            return false;

        bool enableDraft = (a & 0x80) != 0;
        double? draftAngle = null;
        if (enableDraft)
        {
            var display = ctx.Nodes.FirstOrDefault(e =>
                e.ClassName == "moDisplayAngularDim_c" && e.TagOffset > start);
            if (display != null &&
                ctx.Parameters.TryReadVerifiedDoubleSlots(
                    display.TagOffset,
                    new[] { 0x300 },
                    out double value,
                    minExclusive: 0.0,
                    maxInclusive: Math.PI / 2 - 1e-15))
            {
                draftAngle = value;
            }
        }
        else
        {
            draftAngle = 0;
        }

        options = new RibOptions(
            (a & 0x02) != 0,
            (a & 0x04) != 0,
            enableDraft,
            (b & 0x01) != 0,
            (b & 0x08) != 0,
            (a & 0x01) != 0 ? 1 : 0,
            draftAngle,
            3 - refSketch,
            (a & 0x10) != 0);
        return true;
    }

    internal static RibFeatureModel? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;
        try
        {
            var ar = MfcArchive.CreateReader(element.RawData);
            var model = new RibFeatureModel();
            model.Serialize(ar);
            return model;
        }
        catch { return null; }
    }

    public string ClassName => MfcClassName;
    public string? FeatureName { get; set; }
    public double? Version { get; set; }

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public double? Timestamp { get; set; }

    /// <summary>Rib thickness in meters (e.g. 0.002 = 2mm).</summary>
    public double? Thickness { get; set; }

    /// <summary>swRibType_e: 0 Linear, 1 Natural.</summary>
    public int? Type { get; set; }

    /// <summary>swRibExtrusionDirection_e: 0 ParallelToSketch, 1 NormalToSketch.</summary>
    public int? ExtrusionDirection { get; set; }

    /// <summary>True creates symmetric thickness on both sides of sketch line.</summary>
    public bool? IsTwoSided { get; set; }

    /// <summary>Reverse thickness direction (for single-sided ribs).</summary>
    public bool? ReverseThicknessDir { get; set; }

    /// <summary>Flip side of material.</summary>
    public bool? FlipSide { get; set; }

    /// <summary>Enable draft along rib wall.</summary>
    public bool? EnableDraft { get; set; }

    /// <summary>Draft angle in radians.</summary>
    public double? DraftAngle { get; set; }

    /// <summary>Draft outward.</summary>
    public bool? DraftOutward { get; set; }

    /// <summary>Draft from wall.</summary>
    public bool? DraftFromWall { get; set; }

    /// <summary>Reference sketch index.</summary>
    public int? RefSketchIndex { get; set; }

    /// <summary>Explicit target body in multi-body part.</summary>
    public EntityReference? Body { get; set; }
    public MfcCompSolidBodyModel? BodyModel { get; set; }

    public RibFeatureParameterResolution Resolution { get; } = new();

    // ── Dimension Layer Integration ──
    public MoLengthParameterModel? Parameter =>
        Thickness.HasValue ? new MoLengthParameterModel("Thickness@" + (FeatureName ?? "筋1"), Thickness.Value) : null;

    public MoAngleParameterModel? DraftParameter =>
        DraftAngle.HasValue ? new MoAngleParameterModel("DraftAngle@" + (FeatureName ?? "筋1"), DraftAngle.Value) : null;

    public MoDisplayDistanceDimModel? DisplayDimension =>
        Thickness.HasValue ? new MoDisplayDistanceDimModel() : null;

    public MoDisplayAngularDimModel? DisplayDraftDimension =>
        DraftAngle.HasValue ? new MoDisplayAngularDimModel { ArcAngle = DraftAngle.Value } : null;

    public IReadOnlyList<DimensionModel>? Dimensions =>
        (Thickness.HasValue && Parameter != null && DisplayDimension != null)
            ? (EnableDraft == true && DraftParameter != null && DisplayDraftDimension != null
                ? new[] { new DimensionModel(Parameter, DisplayDimension), new DimensionModel(DraftParameter, DisplayDraftDimension) }
                : new[] { new DimensionModel(Parameter, DisplayDimension) })
            : null;

    public byte[]? RawBlock { get; set; }

    public RibFeatureModel()
    {
    }

    public RibFeatureModel(
        double thickness,
        bool isTwoSided = true,
        int extrusionDirection = 0,
        bool enableDraft = false,
        double draftAngle = 0.0)
    {
        Thickness = thickness;
        IsTwoSided = isTwoSided;
        ExtrusionDirection = extrusionDirection;
        EnableDraft = enableDraft;
        DraftAngle = draftAngle;
        SerializedTypeId = StandardTypeId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0 && FeatureName == null)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (Thickness.HasValue && block.Length >= 0x40)
                {
                    int thkOff = MfcPrimitives.FindAscii(block, "moLengthParameter_c");
                    if (thkOff > 0 && thkOff + 32 + 8 <= block.Length)
                        BitConverter.GetBytes(Thickness.Value).CopyTo(block, thkOff + 32);
                }
                ar.WriteBytes(block);
                return;
            }

            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);

            string? name = FeatureName ?? "筋1";
            ar.WChar(ref name);
            FeatureName = name;

            double ts = Timestamp ?? 1234567.89; ar.F64(ref ts); Timestamp = ts;
            uint tid = TypeId != 0 ? TypeId : StandardTypeId; ar.U32(ref tid);
            ulong magic = FolderHeader.PartMagic; ar.U64(ref magic);

            // Thickness dimension parameter
            double thk = Thickness ?? 0.002;
            ar.F64(ref thk);

            // Draft angle parameter
            double draftAng = DraftAngle ?? 0.0;
            ar.F64(ref draftAng);

            // Bitfield bytes a and b
            byte a = 0;
            if (ExtrusionDirection.HasValue && ExtrusionDirection.Value != 0) a |= 0x01;
            if (IsTwoSided == true) a |= 0x02;
            if (ReverseThicknessDir == true) a |= 0x04;
            if (FlipSide == true) a |= 0x10;
            if (EnableDraft == true) a |= 0x80;
            ar.Byte(ref a);

            byte b = 0;
            if (DraftOutward == true) b |= 0x01;
            if (DraftFromWall == true) b |= 0x08;
            ar.Byte(ref b);

            byte rsk = (byte)(3 - (RefSketchIndex ?? 0));
            ar.Byte(ref rsk);

            // Target body if present
            bool hasBody = BodyModel != null || Body != null;
            ar.Boolean(ref hasBody);
            if (hasBody)
            {
                ushort bodyTag = 0;
                ar.ClassTagOrRef(MfcCompSolidBodyModel.MfcClassName, ref bodyTag);
                var bm = BodyModel ?? new MfcCompSolidBodyModel { RefBodyId = (ushort)(Body?.EntityId ?? 0) };
                bm.Serialize(ar);
                BodyModel = bm;
            }
        }
        else
        {
            ushort classTag = 0;
            var kind = ar.ReadClassTag(out string cls, out classTag, out _);
            if (!string.IsNullOrEmpty(cls))
            {
                Resolution.ClassName = true;
            }

            if (ar.Remaining >= 2)
            {
                string? name = null;
                ar.WChar(ref name);
                if (!string.IsNullOrEmpty(name))
                {
                    FeatureName = name!;
                    Resolution.ClassName = true;
                }
            }

            if (ar.Remaining >= 20)
            {
                double ts = 0; ar.F64(ref ts); Timestamp = ts;
                uint tid = 0; ar.U32(ref tid); TypeId = tid;
                ulong magic = 0; ar.U64(ref magic);
                Resolution.SerializedTypeId = true;
            }

            if (ar.Remaining >= 8)
            {
                double thk = 0; ar.F64(ref thk);
                Thickness = thk;
                Resolution.Thickness = true;
            }

            if (ar.Remaining >= 8)
            {
                double draftAng = 0; ar.F64(ref draftAng);
                DraftAngle = draftAng;
                Resolution.DraftAngle = true;
            }

            if (ar.Remaining >= 3)
            {
                byte a = 0; ar.Byte(ref a);
                ExtrusionDirection = (a & 0x01) != 0 ? 1 : 0;
                IsTwoSided = (a & 0x02) != 0;
                ReverseThicknessDir = (a & 0x04) != 0;
                FlipSide = (a & 0x10) != 0;
                EnableDraft = (a & 0x80) != 0;

                byte b = 0; ar.Byte(ref b);
                DraftOutward = (b & 0x01) != 0;
                DraftFromWall = (b & 0x08) != 0;

                byte rsk = 0; ar.Byte(ref rsk);
                RefSketchIndex = (rsk >= 1 && rsk <= 3) ? (3 - rsk) : 0;

                Resolution.ExtrusionDirection = true;
                Resolution.IsTwoSided = true;
                Resolution.ReverseThicknessDir = true;
                Resolution.FlipSide = true;
                Resolution.EnableDraft = true;
                Resolution.DraftOutward = true;
                Resolution.DraftFromWall = true;
                Resolution.RefSketchIndex = true;
            }

            if (ar.Remaining >= 1)
            {
                bool hasBody = false;
                ar.Boolean(ref hasBody);
                if (hasBody && ar.Remaining > 0)
                {
                    ushort bodyTag = 0;
                    ar.ClassTagOrRef(MfcCompSolidBodyModel.MfcClassName, ref bodyTag);
                    var bm = new MfcCompSolidBodyModel();
                    bm.Serialize(ar);
                    BodyModel = bm;
                    Body = new EntityReference
                    {
                        EntityType = SelectionEntityType.Body,
                        EntityId = bm.RefBodyId,
                        Name = $"moSolidRef_w:{bm.RefBodyId}"
                    };
                    Resolution.Body = true;
                }
            }

            if (ar.Remaining > 0)
            {
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}
