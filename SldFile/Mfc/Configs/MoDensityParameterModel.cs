namespace SldFile.Mfc.Configs;

/// <summary>
/// Direct strongly-typed MFC model for physical density and mechanical properties (<c>moDensityParameter_c</c>).
/// Implements IMfcSerializable for symmetric bi-directional reading and writing.
/// </summary>
public sealed class MoDensityParameterModel : IMfcSerializable
{
    public const string MfcClassName = "moDensityParameter_c";

    /// <summary>
    /// Default unit of density is kg/m^3 (SI).
    /// </summary>
    public double Density { get; set; } = 8000.0; // kg/m^3

    /// <summary>
    /// Deafult unit of Young's modulus is Pa (SI).
    /// </summary>
    public double ElasticModulus { get; set; } = 1.9e11; // Pa

    /// <summary>
    /// Default unit of Poisson's ratio is dimensionless (SI).
    /// </summary>
    public double PoissonRatio { get; set; } = 0.29;

    /// <summary>
    /// Deafult unit of shear modulus is Pa (SI).
    /// </summary>
    public double YieldStrength { get; set; } = 2.068e8; // Pa

    /// <summary>
    /// Default unit of tensile strength is Pa (SI).
    /// </summary>
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
