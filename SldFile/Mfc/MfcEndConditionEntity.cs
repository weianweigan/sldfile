using System;

namespace SldFile.Mfc;

/// <summary>
/// Unified strongly-typed model for moCompFace_c / moSingleFace_c end-condition terminating target entities.
/// Implements <see cref="IMfcSerializable"/> to serialize target faces, planes, vertices, or bodies.
/// </summary>
public sealed class MfcEndConditionEntity : EndConditionEntityReference, IMfcSerializable
{
    public const string ClassName = "moCompFace_c";

    public MfcEndConditionEntity() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 1;
            ar.U16(ref schema);

            int et = (int)EntityType;
            ar.I32(ref et);

            uint eid = EntityId;
            ar.U32(ref eid);

            string? sname = SurfaceName;
            ar.WChar(ref sname);

            uint fc = FaceCount;
            ar.U32(ref fc);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            if (ar.Remaining >= 4)
            {
                int et = 0;
                ar.I32(ref et);
                EntityType = (SelectionEntityType)et;
            }

            if (ar.Remaining >= 4)
            {
                uint eid = 0;
                ar.U32(ref eid);
                EntityId = eid;
            }

            if (ar.Remaining > 0)
            {
                string? sname = null;
                ar.WChar(ref sname);
                if (sname != null) SurfaceName = sname;
            }

            if (ar.Remaining >= 4)
            {
                uint fc = 1;
                ar.U32(ref fc);
                FaceCount = fc;
            }
        }
    }
}
