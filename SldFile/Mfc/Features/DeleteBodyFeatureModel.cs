using System;
using System.Collections.Generic;
using System.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Direct MFC model for moDeleteBody_c (Delete/Keep Body feature).
/// Implements IMfcFeatureModel and IMfcSerializable for symmetric reading and writing.
/// Aligned with SolidWorks IDeleteBodyFeatureData.
/// </summary>
public sealed class DeleteBodyFeatureModel : MfcFeatureModelBase, IMfcFeatureModel, IMfcSerializable
{
    public const string MfcClassName = "moDeleteBody_c";
    public const string DataClassName = "moDeleteBodyData_c";
    public const uint StandardTypeId = 0x57u; // 87 in modern 2026
    public const uint SwFeatureId = 54u;     // swFeatureNameID_e.swFmDeleteBody

    internal static DeleteBodyFeatureModel? Parse(MfcElement element)
    {
        if (element == null || element.ClassName != MfcClassName)
            return null;

        var ctx = FeatureWindowContext.TryCreateFromElement(
            element,
            expectedClassName: MfcClassName,
            minTypeId: StandardTypeId,
            maxTypeId: StandardTypeId,
            expectedStreamVersion: 0x71);
        if (ctx != null)
        {
            var dataChildren = element.Children.Where(c => c.ClassName == DataClassName).ToList();
            if (dataChildren.Count == 1)
            {
                var bodyData = dataChildren[0];
                if (bodyData.DataLength >= 2 && BitConverter.ToUInt16(ctx.RawData, bodyData.DataStart) == 1)
                {
                    var bodyChildren = bodyData.Children.Where(c => c.ClassName == "moCompSolidBody_c").ToList();
                    if (bodyChildren.Count == 1)
                    {
                        var body = FeatureSelectionBag.TryParseComponentEntityAt(
                            ctx.RawData,
                            bodyChildren[0].DataStart,
                            "moCompSolidBody_c",
                            SelectionEntityType.Body);
                        if (body != null)
                        {
                            var definition = new DeleteBodyFeatureModel
                            {
                                SerializedTypeId = ctx.TypeId,
                                Bodies = new[] { body }
                            };
                            definition.Resolution.ClassName = true;
                            definition.Resolution.SerializedTypeId = true;
                            definition.Resolution.Bodies = true;
                            return definition;
                        }
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
            var model = new DeleteBodyFeatureModel();
            model.Serialize(ar);
            if (model.Resolution.ClassName && model.Bodies != null && model.Bodies.Count > 0)
            {
                model.Resolution.ClassName = true;
                model.Resolution.SerializedTypeId = true;
                model.Resolution.Bodies = true;
                return model;
            }
        }
        catch { }

        return null;
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
    /// True: Keep selected bodies and delete others.
    /// False: Delete selected bodies and keep others.
    /// </summary>
    public bool? KeepBodies { get; set; }

    /// <summary>Selected bodies targeted by the delete/keep operation.</summary>
    public IReadOnlyList<EntityReference>? Bodies { get; set; }
    public List<MfcCompSolidBodyModel>? BodiesModels { get; set; }

    public int? BodiesCount => Bodies?.Count ?? (BodiesModels != null && BodiesModels.Count > 0 ? (int?)BodiesModels.Count : null);

    public DeleteBodyFeatureParameterResolution Resolution { get; } = new();

    public byte[]? RawBlock { get; set; }

    public DeleteBodyFeatureModel()
    {
    }

    public DeleteBodyFeatureModel(bool keepBodies, IEnumerable<EntityReference>? bodies = null)
    {
        KeepBodies = keepBodies;
        if (bodies != null)
            Bodies = bodies.ToList().AsReadOnly();
        SerializedTypeId = StandardTypeId;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawBlock != null && RawBlock.Length > 0)
            {
                byte[] block = (byte[])RawBlock.Clone();
                if (KeepBodies.HasValue && block.Length >= 0x00EB)
                {
                    block[0x00E7] = KeepBodies.Value ? (byte)1 : (byte)0;
                }
                ar.WriteBytes(block);
                return;
            }

            // 1. moDeleteBody_c header
            ushort folderTag = 0;
            ar.ClassTagOrRef(MfcClassName, ref folderTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName ?? "DeleteBody";
            ar.WChar(ref name);
            FeatureName = name;

            double ts = Timestamp ?? 1234567.89; ar.F64(ref ts); Timestamp = ts;
            uint tid = TypeId != 0 ? TypeId : StandardTypeId; ar.U32(ref tid);
            ulong magic = FolderHeader.PartMagic; ar.U64(ref magic);

            // 2. KeepBodies flag
            bool keep = KeepBodies ?? false;
            ar.Boolean(ref keep);

            // 3. Child moDeleteBodyData_c container
            ushort dataTag = 0;
            ar.ClassTagOrRef(DataClassName, ref dataTag);

            var bList = (BodiesModels != null && BodiesModels.Count > 0)
                ? BodiesModels
                : (Bodies != null
                    ? Bodies.Select(b => new MfcCompSolidBodyModel { RefBodyId = (ushort)b.EntityId }).ToList()
                    : new List<MfcCompSolidBodyModel>());

            ushort bodyCount = (ushort)bList.Count;
            ar.U16(ref bodyCount);

            // 4. Nested moCompSolidBody_c elements
            foreach (var b in bList)
            {
                ushort bodyTag = 0;
                ar.ClassTagOrRef(MfcCompSolidBodyModel.MfcClassName, ref bodyTag);
                b.Serialize(ar);
            }
        }
        else
        {
            ushort folderTag = 0;
            var kind = ar.ReadClassTag(out string cls, out folderTag, out _);
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

            if (ar.Remaining >= 1)
            {
                bool keep = false;
                ar.Boolean(ref keep);
                KeepBodies = keep;
                Resolution.KeepBodies = true;
            }

            // Read moDeleteBodyData_c
            if (ar.Remaining >= 2)
            {
                ushort dataTag = 0;
                ar.ClassTagOrRef(DataClassName, ref dataTag);

                ushort bodyCount = 0;
                ar.U16(ref bodyCount);

                BodiesModels ??= new List<MfcCompSolidBodyModel>();
                BodiesModels.Clear();
                var entList = new List<EntityReference>();
                for (int i = 0; i < bodyCount; i++)
                {
                    ushort bodyTag = 0;
                    ar.ClassTagOrRef(MfcCompSolidBodyModel.MfcClassName, ref bodyTag);
                    var b = new MfcCompSolidBodyModel();
                    b.Serialize(ar);
                    BodiesModels.Add(b);
                    entList.Add(new EntityReference
                    {
                        EntityType = SelectionEntityType.Body,
                        EntityId = b.RefBodyId,
                        Name = $"moCompSolidBody_c:{b.RefBodyId}"
                    });
                }
                if (entList.Count > 0)
                {
                    Bodies = entList.AsReadOnly();
                    Resolution.Bodies = true;
                }
            }

            if (ar.Remaining > 0)
            {
                RawBlock = ar.ReadRemainingBytes();
            }
        }
    }
}
