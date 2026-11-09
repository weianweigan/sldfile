using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Mfc;

/// <summary>
/// Serializable entity reference item for multi-body scoping.
/// </summary>
public sealed class MfcEntityReferenceItem : EntityReference, IMfcSerializable
{
    public MfcEntityReferenceItem() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            uint eid = EntityId;
            ar.U32(ref eid);

            int et = (int)EntityType;
            ar.I32(ref et);

            string? name = Name;
            ar.WChar(ref name);
        }
        else
        {
            if (ar.Remaining >= 4)
            {
                uint eid = 0;
                ar.U32(ref eid);
                EntityId = eid;
            }

            if (ar.Remaining >= 4)
            {
                int et = 0;
                ar.I32(ref et);
                EntityType = (SelectionEntityType)et;
            }

            if (ar.Remaining > 0)
            {
                string? name = null;
                ar.WChar(ref name);
                if (name != null) Name = name;
            }
        }
    }
}

/// <summary>
/// Unified strongly-typed model for moCompSolidBody_c multi-body feature scope.
/// Implements <see cref="IMfcSerializable"/> to serialize AutoSelect flags and target bodies.
/// </summary>
public sealed class MfcBodyScope : BodyScopeData, IMfcSerializable
{
    public const string ClassName = "moCompSolidBody_c";

    public MfcBodyScope() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 1;
            ar.U16(ref schema);

            bool auto = AutoSelect;
            ar.Boolean(ref auto);

            ushort flg = Flags;
            ar.U16(ref flg);

            var bodyIds = BodyIds ?? Array.Empty<uint>();
            uint idCount = (uint)bodyIds.Count;
            ar.Count(ref idCount);
            for (int i = 0; i < bodyIds.Count; i++)
            {
                uint id = bodyIds[i];
                ar.U32(ref id);
            }

            var bodies = Bodies ?? Array.Empty<EntityReference>();
            uint bodyCount = (uint)bodies.Count;
            ar.Count(ref bodyCount);
            foreach (var b in bodies)
            {
                if (b is MfcEntityReferenceItem mfcItem)
                    mfcItem.Serialize(ar);
                else
                {
                    var newItem = new MfcEntityReferenceItem
                    {
                        EntityId = b.EntityId,
                        EntityType = b.EntityType,
                        Name = b.Name
                    };
                    newItem.Serialize(ar);
                }
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            if (ar.Remaining >= 1)
            {
                bool auto = true;
                ar.Boolean(ref auto);
                AutoSelect = auto;
            }

            if (ar.Remaining >= 2)
            {
                ushort flg = 0;
                ar.U16(ref flg);
                Flags = flg;
            }

            if (ar.Remaining >= 4)
            {
                uint idCount = 0;
                ar.Count(ref idCount);
                var idList = new List<uint>();
                for (int i = 0; i < idCount && ar.Remaining >= 4; i++)
                {
                    uint id = 0;
                    ar.U32(ref id);
                    idList.Add(id);
                }
                BodyIds = idList;
            }

            if (ar.Remaining >= 4)
            {
                uint bodyCount = 0;
                ar.Count(ref bodyCount);
                var bodyList = new List<EntityReference>();
                for (int i = 0; i < bodyCount && ar.Remaining > 0; i++)
                {
                    var item = new MfcEntityReferenceItem();
                    item.Serialize(ar);
                    bodyList.Add(item);
                }
                Bodies = bodyList;
            }
        }
    }
}
