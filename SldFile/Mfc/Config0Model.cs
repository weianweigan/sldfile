using System;
using System.Collections.Generic;

namespace SldFile.Mfc;

/// <summary>
/// Unified model for Contents/Config-0 binary stream using MFC CArchive serialization.
/// Symmetrically reads and writes su_CStringArray users, moLogs_c log entries, material, density, and trailing body data.
/// </summary>
internal sealed class Config0Model : IMfcSerializable
{
    public const string CStringArrayClassName = "su_CStringArray";
    public const string LogsClassName = "moLogs_c";

    public List<string> Users { get; } = new();
    public List<string> LogEntries { get; } = new();
    public MoMaterialModel? Material { get; set; }
    public MoDensityParameterModel? DensityParameter { get; set; }
    public Dictionary<string, double> Parameters { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] BodyData { get; set; } = Array.Empty<byte>();

    public byte[]? RawData { get; set; }

    public Config0Model() { }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            // Storing from scratch
            // 1. su_CStringArray
            ushort csaTag = 0;
            ar.WriteClassTag(CStringArrayClassName, ref csaTag, schema: 1);
            ushort uCount = (ushort)Users.Count;
            ar.U16(ref uCount);
            foreach (var user in Users)
            {
                string? u = user;
                ar.WChar(ref u);
            }
            ushort term = 0;
            ar.U16(ref term);
            ar.U16(ref term);

            // 2. moLogs_c
            ushort logsTag = 0;
            ar.WriteClassTag(LogsClassName, ref logsTag, schema: 1);
            ushort logVer = 1;
            ar.U16(ref logVer);

            foreach (var entry in LogEntries)
            {
                string? action = entry;
                ar.WChar(ref action);
            }

            // Sentinel 0xFFFFFFFF
            uint sentinel = 0xFFFFFFFF;
            ar.U32(ref sentinel);

            // 3. moMaterial_c
            if (Material != null)
            {
                ushort matTag = 0;
                ar.WriteClassTag(MoMaterialModel.MfcClassName, ref matTag, schema: 1);
                string? mat = Material.MaterialName; ar.WChar(ref mat);
                string? db = Material.DatabaseName; ar.WChar(ref db);
                string? cat = Material.Category; ar.WChar(ref cat);
            }

            // 4. moDensityParameter_c
            if (DensityParameter != null)
            {
                ushort densTag = 0;
                ar.WriteClassTag(MoDensityParameterModel.MfcClassName, ref densTag, schema: 1);
                double rho = DensityParameter.Density; ar.F64(ref rho);
                double e = DensityParameter.ElasticModulus; ar.F64(ref e);
                double nu = DensityParameter.PoissonRatio; ar.F64(ref nu);
            }

            // Body data
            if (BodyData != null && BodyData.Length > 0)
            {
                ar.WriteBytes(BodyData);
            }
        }
        else
        {
            Users.Clear();
            LogEntries.Clear();
            Parameters.Clear();
            Material = null;
            DensityParameter = null;

            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length == 0) return;

            var doc = MfcDocument.Parse(data);

            // Extract user names from su_CStringArray
            if (doc.FindFirst(CStringArrayClassName) is { } cStringArray)
            {
                var reader = cStringArray.CreateReader();
                while (reader.HasMore)
                {
                    if (reader.IsWCharPrefix)
                    {
                        var name = reader.ReadWChar();
                        if (!string.IsNullOrEmpty(name))
                            Users.Add(name!);
                    }
                    else if (reader.IsCStringArrayTerminator)
                    {
                        reader.SkipCStringArrayTerminator();
                    }
                    else
                    {
                        reader.Skip(1);
                    }
                }
            }

            // Extract log entries from WChar strings in moLogs_c
            if (doc.FindFirst(LogsClassName) is { } logs)
            {
                var reader = logs.CreateReader();
                while (reader.Remaining > 3)
                {
                    if (reader.IsWCharPrefix)
                    {
                        var action = reader.ReadWChar();
                        if (!string.IsNullOrEmpty(action))
                            LogEntries.Add(action!);
                    }
                    else if (reader.IsMfcTag)
                    {
                        break;
                    }
                    else
                    {
                        reader.Skip(1);
                    }
                }
            }

            // Extract Material if present
            if (doc.FindFirst(MoMaterialModel.MfcClassName) is { } matElem)
            {
                var mat = new MoMaterialModel();
                var r = matElem.CreateReader();
                var strs = new List<string>();
                while (r.Remaining > 0)
                {
                    if (r.IsWCharPrefix)
                    {
                        var s = r.ReadWChar();
                        if (!string.IsNullOrWhiteSpace(s)) strs.Add(s!);
                    }
                    else r.Skip(1);
                }
                if (strs.Count > 0) mat.MaterialName = strs[0];
                if (strs.Count > 1) mat.DatabaseName = strs[1];
                if (strs.Count > 2) mat.Category = strs[2];
                Material = mat;
            }

            // Extract DensityParameter if present
            if (doc.FindFirst(MoDensityParameterModel.MfcClassName) is { } densElem)
            {
                var dens = new MoDensityParameterModel();
                var r = densElem.CreateReader();
                var dbls = new List<double>();
                while (r.Remaining >= 8)
                {
                    double d = BitConverter.ToDouble(r.Data, r.Offset);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && d > 0.001)
                    {
                        dbls.Add(d);
                        r.Skip(8);
                        continue;
                    }
                    r.Skip(1);
                }
                if (dbls.Count > 0) dens.Density = dbls[0];
                if (dbls.Count > 1) dens.ElasticModulus = dbls[1];
                if (dbls.Count > 2) dens.PoissonRatio = dbls[2];
                DensityParameter = dens;
            }

            // Body data: find sentinel 0xFFFFFFFF
            int bodyStart = 0;
            var span = data.AsSpan();
            for (int i = 0; i <= data.Length - 4; i++)
            {
                if (span[i] == 0xFF && span[i + 1] == 0xFF &&
                    span[i + 2] == 0xFF && span[i + 3] == 0xFF)
                {
                    bodyStart = i + 4;
                    break;
                }
            }

            if (bodyStart > 0 && bodyStart < data.Length)
            {
                BodyData = new byte[data.Length - bodyStart];
                Array.Copy(data, bodyStart, BodyData, 0, BodyData.Length);
            }
            else if (bodyStart == 0 && data.Length > 0)
            {
                int maxEnd = 0;
                foreach (var element in doc.AllElements)
                {
                    if (element.DataEnd > maxEnd)
                        maxEnd = element.DataEnd;
                }
                if (maxEnd > 0 && maxEnd < data.Length)
                {
                    BodyData = new byte[data.Length - maxEnd];
                    Array.Copy(data, maxEnd, BodyData, 0, BodyData.Length);
                }
                else
                {
                    BodyData = data;
                }
            }
        }
    }
}

/// <summary>
/// Direct strongly-typed MFC model for part material specification (<c>moMaterial_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class MoMaterialModel : IMfcSerializable
{
    public const string MfcClassName = "moMaterial_c";

    public string MaterialName { get; set; } = "AISI 304";
    public string DatabaseName { get; set; } = "SOLIDWORKS Materials";
    public string Category { get; set; } = "Steel";

    public byte[]? RawData { get; set; }

    public MoMaterialModel() { }

    public MoMaterialModel(string materialName, string databaseName = "SOLIDWORKS Materials", string category = "Steel")
    {
        MaterialName = materialName;
        DatabaseName = databaseName;
        Category = category;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            string? mat = MaterialName; ar.WChar(ref mat);
            string? db = DatabaseName; ar.WChar(ref db);
            string? cat = Category; ar.WChar(ref cat);
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            string? mat = null; ar.WChar(ref mat); MaterialName = mat ?? string.Empty;
            string? db = null; ar.WChar(ref db); DatabaseName = db ?? string.Empty;
            string? cat = null; ar.WChar(ref cat); Category = cat ?? string.Empty;
        }
    }
}

/// <summary>
/// Direct strongly-typed MFC model for physical density and mechanical properties (<c>moDensityParameter_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class MoDensityParameterModel : IMfcSerializable
{
    public const string MfcClassName = "moDensityParameter_c";

    public double Density { get; set; } = 8000.0; // kg/m^3
    public double ElasticModulus { get; set; } = 1.9e11; // Pa
    public double PoissonRatio { get; set; } = 0.29;
    public double YieldStrength { get; set; } = 2.068e8; // Pa
    public double TensileStrength { get; set; } = 5.17e8; // Pa

    public byte[]? RawData { get; set; }

    public MoDensityParameterModel() { }

    public MoDensityParameterModel(double density, double elasticModulus = 1.9e11, double poisson = 0.29)
    {
        Density = density;
        ElasticModulus = elasticModulus;
        PoissonRatio = poisson;
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsStoring)
        {
            if (RawData != null && RawData.Length > 0)
            {
                ar.WriteBytes(RawData);
                return;
            }

            ushort tag = 0;
            ar.WriteClassTag(MfcClassName, ref tag, schema: 1);

            double rho = Density; ar.F64(ref rho);
            double e = ElasticModulus; ar.F64(ref e);
            double nu = PoissonRatio; ar.F64(ref nu);
            double ys = YieldStrength; ar.F64(ref ys);
            double ts = TensileStrength; ar.F64(ref ts);
        }
        else
        {
            ushort refTag = 0;
            ar.ReadClassTag(out _, out refTag, out _);

            double rho = 0; ar.F64(ref rho); Density = rho;
            double e = 0; ar.F64(ref e); ElasticModulus = e;
            double nu = 0; ar.F64(ref nu); PoissonRatio = nu;
            double ys = 0; ar.F64(ref ys); YieldStrength = ys;
            double ts = 0; ar.F64(ref ts); TensileStrength = ts;
        }
    }
}
