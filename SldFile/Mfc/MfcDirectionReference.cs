using System;

namespace SldFile.Mfc;

/// <summary>
/// Unified strongly-typed model for moDirectionSpec_c extrusion/draft direction references.
/// Implements <see cref="IMfcSerializable"/> to serialize direction vectors and entity references.
/// </summary>
public sealed class MfcDirectionReference : DirectionReference, IMfcSerializable
{
    public const string ClassName = "moDirectionSpec_c";

    public MfcDirectionReference() { }

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

            string? rname = ReferenceName;
            ar.WChar(ref rname);

            bool hasVec = Vector != null && Vector.Length == 3;
            ar.Boolean(ref hasVec);
            if (hasVec)
            {
                double vx = Vector![0];
                ar.F64(ref vx);
                double vy = Vector[1];
                ar.F64(ref vy);
                double vz = Vector[2];
                ar.F64(ref vz);
            }
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
                string? rname = null;
                ar.WChar(ref rname);
                if (rname != null) ReferenceName = rname;
            }

            if (ar.Remaining >= 1)
            {
                bool hasVec = false;
                ar.Boolean(ref hasVec);
                if (hasVec && ar.Remaining >= 24)
                {
                    double vx = 0, vy = 0, vz = 0;
                    ar.F64(ref vx);
                    ar.F64(ref vy);
                    ar.F64(ref vz);
                    Vector = new[] { vx, vy, vz };
                }
            }
        }
    }
}
