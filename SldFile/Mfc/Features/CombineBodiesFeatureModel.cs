using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct MFC model for moCombineBodies_c (Combine Bodies feature).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric reading and writing.
/// Aligned with SolidWorks ICombineBodiesFeatureData.
/// </summary>
public sealed class CombineBodiesFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moCombineBodies_c";
    public const uint StandardTypeId = 0x55u; // 85 in modern 2026
    public const uint LegacyTypeId = 0x5Au;   // 90 in legacy 0x81
    public const uint SwFeatureId = 45u;     // swFeatureNameID_e.swFmCombineBodies

    internal static IFeatureData? Parse(
        MfcDocument doc, string className, uint typeId, int start, int end)
    {
        var ctx = FeatureWindowContext.TryCreateWithFolderHeader(
            doc, className, typeId, start, end, out var header, requiredClassName: MfcClassName);
        if (ctx == null)
            return null;

        var data = new CombineBodiesFeatureModel
        {
            SerializedTypeId = header.TypeId
        };
        data.Resolution.ClassName = true;
        data.Resolution.SerializedTypeId = true;
        return data;
    }

    internal static CombineBodiesFeatureModel? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;
        try
        {
            var ar = MfcArchive.CreateReader(element.RawData);
            var model = new CombineBodiesFeatureModel();
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

    /// <summary>
    /// swCombineBodiesOperationType_e:
    /// 0: Add (SWBODYADD), 1: Subtract (SWBODYSUBTRACT), 2: Common (SWBODYCOMMON)
    /// </summary>
    public int? OperationType { get; set; }

    /// <summary>Main body to retain or subtract from.</summary>
    public EntityReference? MainBody { get; set; }
    public MfcCompSolidBodyModel? MainBodyModel { get; set; }

    /// <summary>Tool bodies to combine or subtract.</summary>
    public IReadOnlyList<EntityReference>? BodiesToCombine { get; set; }
    public List<MfcCompSolidBodyModel>? BodiesToCombineModels { get; set; }

    public int? BodiesToCombineCount => BodiesToCombine?.Count ?? (BodiesToCombineModels != null && BodiesToCombineModels.Count > 0 ? (int?)BodiesToCombineModels.Count : null);

    public string? OperationGuid { get; set; }

    public CombineBodiesFeatureParameterResolution Resolution { get; } = new();

    public byte[]? RawBlock { get; set; }

    public CombineBodiesFeatureModel()
    {
    }

    public CombineBodiesFeatureModel(int operationType, EntityReference? mainBody = null, IEnumerable<EntityReference>? bodiesToCombine = null)
    {
        OperationType = operationType;
        MainBody = mainBody;
        if (bodiesToCombine != null)
            BodiesToCombine = bodiesToCombine.ToList().AsReadOnly();
        SerializedTypeId = StandardTypeId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (OperationType.HasValue && block.Length >= 0x4BC)
                {
                    BitConverter.GetBytes(OperationType.Value).CopyTo(block, 0x4B4);
                }
                ar.WriteBytes(block);
                return;
            }

            // 1. Folder element tag and class name
            ushort classTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref classTag);

            // 2. Schema
            ushort schema = 0x8004;
            ar.U16(ref schema);

            // 3. Feature Name WChar
            string? name = FeatureName ?? "组合1";
            ar.WChar(ref name);
            FeatureName = name;

            // 4. Header payload
            double ts = Timestamp ?? 1234567.89; ar.F64(ref ts); Timestamp = ts;
            uint tid = TypeId != 0 ? TypeId : StandardTypeId; ar.U32(ref tid);
            ulong magic = FolderHeader.PartMagic; ar.U64(ref magic);

            // 5. Operation GUID
            string? guid = OperationGuid ?? Guid.NewGuid().ToString().ToUpperInvariant();
            ar.WChar(ref guid);
            OperationGuid = guid;

            // 6. Operation type
            int opType = OperationType ?? 0;
            ar.I32(ref opType);

            // 7. Main Body with explicit presence flag
            bool hasMainBody = MainBodyModel != null || MainBody != null;
            ar.Boolean(ref hasMainBody);
            if (hasMainBody)
            {
                var mb = MainBodyModel ?? new MfcCompSolidBodyModel { RefBodyId = (ushort)(MainBody?.EntityId ?? 0) };
                mb.Serialize(ar);
                MainBodyModel = mb;
            }

            // 8. Tool bodies wrapped in moSolidRef_w
            var tools = (BodiesToCombineModels != null && BodiesToCombineModels.Count > 0)
                ? BodiesToCombineModels
                : (BodiesToCombine != null
                    ? BodiesToCombine.Select(b => new MfcCompSolidBodyModel { RefBodyId = (ushort)b.EntityId }).ToList()
                    : new List<MfcCompSolidBodyModel>());

            ushort toolCount = (ushort)tools.Count;
            ar.U16(ref toolCount);
            foreach (var body in tools)
            {
                ushort solidRefTag = 0;
                ar.ClassTagOrRef("moSolidRef_w", ref solidRefTag);
                body.Serialize(ar);
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

            // Read GUID if present
            if (ar.Remaining >= 2)
            {
                string? guid = null;
                ar.WChar(ref guid);
                if (!string.IsNullOrEmpty(guid)) OperationGuid = guid!;
            }

            // Read OperationType
            if (ar.Remaining >= 4)
            {
                int opType = 0;
                ar.I32(ref opType);
                OperationType = opType;
                Resolution.OperationType = true;
            }

            // Read Main Body
            if (ar.Remaining >= 1)
            {
                bool hasMainBody = false;
                ar.Boolean(ref hasMainBody);
                if (hasMainBody && ar.Remaining >= 8)
                {
                    var mb = new MfcCompSolidBodyModel();
                    mb.Serialize(ar);
                    MainBodyModel = mb;
                    MainBody = new EntityReference
                    {
                        EntityType = SelectionEntityType.Body,
                        EntityId = mb.RefBodyId,
                        Name = $"moSolidRef_w:{mb.RefBodyId}"
                    };
                    Resolution.MainBody = true;
                }
            }

            // Read tool bodies
            if (ar.Remaining >= 2)
            {
                ushort toolCount = 0;
                ar.U16(ref toolCount);
                BodiesToCombineModels ??= new List<MfcCompSolidBodyModel>();
                BodiesToCombineModels.Clear();
                var entList = new List<EntityReference>();
                for (int i = 0; i < toolCount; i++)
                {
                    ushort solidRefTag = 0;
                    ar.ClassTagOrRef("moSolidRef_w", ref solidRefTag);
                    var body = new MfcCompSolidBodyModel();
                    body.Serialize(ar);
                    BodiesToCombineModels.Add(body);
                    entList.Add(new EntityReference
                    {
                        EntityType = SelectionEntityType.Body,
                        EntityId = body.RefBodyId,
                        Name = $"moSolidRef_w:{body.RefBodyId}"
                    });
                }
                if (entList.Count > 0)
                {
                    BodiesToCombine = entList.AsReadOnly();
                    Resolution.BodiesToCombine = true;
                }
            }

            if (ar.Remaining > 0)
            {
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}
