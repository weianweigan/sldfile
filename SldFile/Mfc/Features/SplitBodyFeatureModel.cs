using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct MFC model for moSplitBody_c / moSplit_c (Split Body feature).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric reading and writing.
/// Aligned with SolidWorks ISplitBodyFeatureData.
/// </summary>
public sealed class SplitBodyFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string ModernClassName = "moSplitBody_c";
    public const string LegacyClassName = "moSplit_c";
    public const uint StandardTypeId = 0x56u; // 86 in modern 2026
    public const uint LegacyTypeId = 0x5Au;   // 90 in legacy 0x80
    public const uint SwFeatureId = 46u;     // swFeatureNameID_e.swFmSplitBody

    internal static IFeatureData? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        if (className != LegacyClassName && className != ModernClassName)
            return null;
        var ctx = FeatureWindowContext.TryCreateWithFolderHeader(
            doc, className, typeId, start, end, out var header);
        if (ctx == null)
            return null;

        var data = new SplitBodyFeatureModel
        {
            SerializedClassName = className,
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;

        if (TryReadSplitParameters(ctx, out bool? consume, out int? trimToolsCount))
        {
            if (consume.HasValue)
            {
                data.Consume = consume;
                data.Resolution.Consume = true;
            }
            if (trimToolsCount.HasValue)
            {
                data.DecodedTrimToolsCount = trimToolsCount;
                data.Resolution.TrimToolsCount = true;
            }
        }
        else if (ctx.StreamVersion == 0x0080)
        {
            data.Consume = false;
            data.Resolution.Consume = true;
        }

        return data;
    }

    private static bool TryReadSplitParameters(
        FeatureWindowContext ctx,
        out bool? consume,
        out int? trimToolsCount)
    {
        consume = null;
        trimToolsCount = null;
        if (ctx.StreamVersion != 0x0080 && ctx.StreamVersion != 0x0082)
            return false;

        byte[] raw = ctx.RawData;
        int start = ctx.Start;
        int limit = ctx.End;
        if (start + 0x180 > limit)
            return false;

        byte a = raw[start + 0xE2];
        byte b = raw[start + 0xE6];
        if ((a == 0 || a == 1) && (b == 1 || b == 2) &&
            ((a == 0 && b == 1) || (a == 1 && b == 2)) &&
            raw[start + 0xE0] == 0 && raw[start + 0xE1] == 0 &&
            raw[start + 0xE3] == 0 && raw[start + 0xE4] == 0 &&
            raw[start + 0xE5] == 0 && raw[start + 0xE7] == 0)
        {
            consume = a == 0;
        }

        var planeRef = ctx.Nodes.FirstOrDefault(e =>
            e.ClassName == "moPlaneRef_w" && e.TagOffset >= start + 0x100);
        if (planeRef != null && planeRef.TagOffset >= 2)
        {
            ushort count = BitConverter.ToUInt16(raw, planeRef.TagOffset - 2);
            if (count > 0 && count < 4096)
                trimToolsCount = count;
        }

        return consume.HasValue || trimToolsCount.HasValue;
    }

    internal static SplitBodyFeatureModel? Parse(MfcElement element)
    {
        if (element == null || (element.ClassName != ModernClassName && element.ClassName != LegacyClassName))
            return null;
        try
        {
            var ar = MfcArchive.CreateReader(element.RawData);
            var model = new SplitBodyFeatureModel();
            model.Serialize(ar);
            return model;
        }
        catch { return null; }
    }

    public string ClassName => SerializedClassName ?? ModernClassName;
    public string? SerializedClassName { get; set; }
    public string? FeatureName { get; set; }
    public double? Version { get; set; }

    public uint? SerializedTypeId { get; set; }
    public uint TypeId
    {
        get => SerializedTypeId ?? 0;
        set => SerializedTypeId = value;
    }

    public double? Timestamp { get; set; }

    /// <summary>True consumes the split body; false keeps it.</summary>
    public bool? Consume { get; set; }

    /// <summary>swSplitBodyType_e: 0 Show, 1 Hide, 2 Consume.</summary>
    public int? State { get; set; }

    /// <summary>Cutting trim tools (planar faces or reference planes).</summary>
    public IReadOnlyList<EntityReference>? TrimTools { get; set; }
    public List<MfcCompFaceModel>? TrimToolsModels { get; set; }

    public int? DecodedTrimToolsCount { get; set; }
    public int? TrimToolsCount => TrimTools?.Count ?? (TrimToolsModels != null && TrimToolsModels.Count > 0 ? (int?)TrimToolsModels.Count : DecodedTrimToolsCount);

    /// <summary>Target body being split.</summary>
    public EntityReference? TargetBody { get; set; }
    public MfcCompSolidBodyModel? TargetBodyModel { get; set; }

    /// <summary>Template path used when exporting split bodies.</summary>
    public string? TemplatePath { get; set; }

    public bool? OverrideDefaultTemplateSettings { get; set; }

    public SplitBodyFeatureParameterResolution Resolution { get; } = new();

    public byte[]? RawBlock { get; set; }

    public SplitBodyFeatureModel()
    {
    }

    public SplitBodyFeatureModel(bool consume, IEnumerable<EntityReference>? trimTools = null)
    {
        Consume = consume;
        State = consume ? 2 : 0;
        if (trimTools != null)
            TrimTools = trimTools.ToList().AsReadOnly();
        SerializedTypeId = StandardTypeId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0 && FeatureName == null)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (Consume.HasValue && block.Length >= 0x100)
                {
                    block[0xE2] = Consume.Value ? (byte)0 : (byte)1;
                    block[0xE6] = Consume.Value ? (byte)1 : (byte)2;
                }
                ar.WriteBytes(block);
                return;
            }

            ushort classTag = 0;
            ar.ClassTagOrRef(ClassName, ref classTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName ?? "分割1";
            ar.WChar(ref name);
            FeatureName = name;

            double ts = Timestamp ?? 1234567.89; ar.F64(ref ts); Timestamp = ts;
            uint tid = TypeId != 0 ? TypeId : StandardTypeId; ar.U32(ref tid);
            ulong magic = FolderHeader.PartMagic; ar.U64(ref magic);

            // Bitfield options
            bool consume = Consume ?? true; ar.Boolean(ref consume);
            int state = State ?? 2; ar.I32(ref state);

            // TrimTools array with ushort count prefix
            var tools = (TrimToolsModels != null && TrimToolsModels.Count > 0)
                ? TrimToolsModels
                : (TrimTools != null
                    ? TrimTools.Select(t => new MfcCompFaceModel { RefFaceId = (ushort)t.EntityId }).ToList()
                    : new List<MfcCompFaceModel>());

            ushort toolCount = (ushort)tools.Count;
            ar.U16(ref toolCount);
            foreach (var tool in tools)
            {
                ushort planeRefTag = 0;
                ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);
                tool.Serialize(ar);
            }

            // Target body
            bool hasTargetBody = TargetBodyModel != null || TargetBody != null;
            ar.Boolean(ref hasTargetBody);
            if (hasTargetBody)
            {
                ushort solidRefTag = 0;
                ar.ClassTagOrRef("moSolidRef_w", ref solidRefTag);
                var tm = TargetBodyModel ?? new MfcCompSolidBodyModel { RefBodyId = (ushort)(TargetBody?.EntityId ?? 0) };
                tm.Serialize(ar);
                TargetBodyModel = tm;
            }

            // Template paths
            string? tpath = TemplatePath;
            ar.WChar(ref tpath);
            bool overrideTpl = OverrideDefaultTemplateSettings ?? false;
            ar.Boolean(ref overrideTpl);
        }
        else
        {
            ushort classTag = 0;
            var kind = ar.ReadClassTag(out string cls, out classTag, out _);
            if (!string.IsNullOrEmpty(cls))
            {
                SerializedClassName = cls;
                Resolution.ClassName = true;
            }

            ushort schema = 0;
            ar.U16(ref schema);

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

            if (ar.Remaining >= 1)
            {
                bool consume = false;
                ar.Boolean(ref consume);
                Consume = consume;
                Resolution.Consume = true;
            }

            if (ar.Remaining >= 4)
            {
                int state = 0;
                ar.I32(ref state);
                State = state;
                Resolution.State = true;
            }

            if (ar.Remaining >= 2)
            {
                ushort toolCount = 0;
                ar.U16(ref toolCount);
                TrimToolsModels ??= new List<MfcCompFaceModel>();
                TrimToolsModels.Clear();
                var entList = new List<EntityReference>();
                for (int i = 0; i < toolCount; i++)
                {
                    ushort planeRefTag = 0;
                    ar.ClassTagOrRef("moPlaneRef_w", ref planeRefTag);
                    var tool = new MfcCompFaceModel();
                    tool.Serialize(ar);
                    TrimToolsModels.Add(tool);
                    entList.Add(new EntityReference
                    {
                        EntityType = SelectionEntityType.Face,
                        EntityId = tool.RefFaceId,
                        Name = $"moPlaneRef_w:{tool.RefFaceId}"
                    });
                }
                if (entList.Count > 0)
                {
                    TrimTools = entList.AsReadOnly();
                    Resolution.TrimTools = true;
                    Resolution.TrimToolsCount = true;
                }
            }

            // Read Target body before template paths
            if (ar.Remaining >= 1)
            {
                bool hasTargetBody = false;
                ar.Boolean(ref hasTargetBody);
                if (hasTargetBody && ar.Remaining > 0)
                {
                    ushort solidRefTag = 0;
                    ar.ClassTagOrRef("moSolidRef_w", ref solidRefTag);
                    var tm = new MfcCompSolidBodyModel();
                    tm.Serialize(ar);
                    TargetBodyModel = tm;
                    TargetBody = new EntityReference
                    {
                        EntityType = SelectionEntityType.Body,
                        EntityId = tm.RefBodyId,
                        Name = $"moSolidRef_w:{tm.RefBodyId}"
                    };
                }
            }

            if (ar.Remaining >= 2)
            {
                string? tpath = null;
                ar.WChar(ref tpath);
                TemplatePath = tpath;
            }

            if (ar.Remaining >= 1)
            {
                bool overrideTpl = false;
                ar.Boolean(ref overrideTpl);
                OverrideDefaultTemplateSettings = overrideTpl;
            }

            if (ar.Remaining > 0)
            {
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}
