using System;

namespace SldFile.Mfc;

/// <summary>
/// Unified strongly-typed model for moCompRefPlane_c Reference Plane feature.
/// Implements IMfcSerializable to serialize plane offset distance, angle, and reference face/plane names.
/// </summary>
internal sealed class RefPlaneFeatureModel : IMfcSerializable
{
    public const string ClassName = "moCompRefPlane_c";
    public const uint HeaderTypeId = 0x5Bu;

    public string FeatureName { get; set; } = "基准面1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get; set; } = HeaderTypeId;

    public double OffsetDistance { get; set; } = 0.050; // 50mm
    public double Angle { get; set; }
    public bool ReverseDirection { get; set; }
    public string ReferencePlaneName { get; set; } = "Front Plane";

    public RefPlaneFeatureModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = TypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            double dist = OffsetDistance;
            ar.F64(ref dist);

            double ang = Angle;
            ar.F64(ref ang);

            bool rev = ReverseDirection;
            ar.Boolean(ref rev);

            string? rname = ReferencePlaneName;
            ar.WChar(ref rname);
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) FeatureName = name!;

            double v = 0;
            ar.F64(ref v);
            Version = v;

            uint tid = 0;
            ar.U32(ref tid);
            TypeId = tid;

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 8)
            {
                double dist = 0;
                ar.F64(ref dist);
                OffsetDistance = dist;
            }

            if (ar.Remaining >= 8)
            {
                double ang = 0;
                ar.F64(ref ang);
                Angle = ang;
            }

            if (ar.Remaining >= 1)
            {
                bool rev = false;
                ar.Boolean(ref rev);
                ReverseDirection = rev;
            }

            if (ar.Remaining > 0)
            {
                string? rname = null;
                ar.WChar(ref rname);
                if (rname != null) ReferencePlaneName = rname;
            }
        }
    }
}

/// <summary>
/// Unified strongly-typed model for moRefAxis_c Reference Axis feature.
/// Implements IMfcSerializable to serialize reference axis specifications.
/// </summary>
internal sealed class RefAxisFeatureModel : IMfcSerializable
{
    public const string ClassName = "moRefAxis_c";
    public const uint HeaderTypeId = 0x5Cu;

    public string FeatureName { get; set; } = "基准轴1";
    public double Version { get; set; } = 2.0;
    public uint TypeId { get; set; } = HeaderTypeId;

    public int AxisType { get; set; } // 0=OneLine, 1=TwoPlanes, 2=TwoPoints, 3=Cylinder
    public string EntityName1 { get; set; } = "CylinderFace1";
    public string? EntityName2 { get; set; }

    public RefAxisFeatureModel() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0x8004;
            ar.U16(ref schema);

            string? name = FeatureName;
            ar.WChar(ref name);

            double v = Version;
            ar.F64(ref v);

            uint tid = TypeId;
            ar.U32(ref tid);

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = string.Empty;
            ar.WChar(ref empty);

            int at = AxisType;
            ar.I32(ref at);

            string? en1 = EntityName1;
            ar.WChar(ref en1);

            bool hasEn2 = !string.IsNullOrEmpty(EntityName2);
            ar.Boolean(ref hasEn2);
            if (hasEn2)
            {
                string? en2 = EntityName2;
                ar.WChar(ref en2);
            }
        }
        else
        {
            ushort refTag = 0;
            ar.ClassTagOrRef(ClassName, ref refTag);

            ushort schema = 0;
            ar.U16(ref schema);

            string? name = null;
            ar.WChar(ref name);
            if (!string.IsNullOrEmpty(name)) FeatureName = name!;

            double v = 0;
            ar.F64(ref v);
            Version = v;

            uint tid = 0;
            ar.U32(ref tid);
            TypeId = tid;

            uint zero = 0;
            ar.U32(ref zero);

            string? empty = null;
            ar.WChar(ref empty);

            if (ar.Remaining >= 4)
            {
                int at = 0;
                ar.I32(ref at);
                AxisType = at;
            }

            if (ar.Remaining > 0)
            {
                string? en1 = null;
                ar.WChar(ref en1);
                if (en1 != null) EntityName1 = en1;
            }

            if (ar.Remaining >= 1)
            {
                bool hasEn2 = false;
                ar.Boolean(ref hasEn2);
                if (hasEn2 && ar.Remaining > 0)
                {
                    string? en2 = null;
                    ar.WChar(ref en2);
                    EntityName2 = en2;
                }
            }
        }
    }
}
