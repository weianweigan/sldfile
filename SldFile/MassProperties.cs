using System;
using System.Globalization;
using System.Numerics;

namespace SldFile;

/// <summary>
/// Precomputed mass properties cached by SolidWorks at save time.
/// All values are expressed in SI base units (metres, kilograms).
/// </summary>
public sealed class MassProperties
{
    /// <summary>Centre of gravity [X, Y, Z] in metres.</summary>
    public Vector3 CenterOfGravity { get; set; }

    /// <summary>Total volume in cubic metres (m³).</summary>
    public double Volume { get; set; }

    /// <summary>Total surface area in square metres (m²).</summary>
    public double SurfaceArea { get; set; }

    /// <summary>Total mass in kilograms (kg).</summary>
    public double Mass { get; set; }

    /// <summary>Principal moments of inertia [Ixx, Iyy, Izz] about axes through the centre of gravity, in kg·m².</summary>
    public Vector3 MomentsOfInertia { get; set; }

    /// <summary>Products of inertia [Ixy, Ixz, Iyz] at the centre of gravity, in kg·m².</summary>
    public Vector3 ProductsOfInertia { get; set; }

    /// <summary>
    /// Parses the comma-separated or space-separated 14-double string stored by SolidWorks
    /// under the property key "SW-MassProp-Config-N".
    /// Format: CoG (X, Y, Z), Volume, SurfaceArea, Mass, Moments (Ixx, Iyy, Izz), Products (Ixy, Ixz, Iyz), ...
    /// </summary>
    public static bool TryParse(string? text, out MassProperties massProperties)
    {
        massProperties = null!;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var tokens = text!.Split(new[] { ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 12) return false;

        double[] values = new double[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!double.TryParse(tokens[i], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        massProperties = new MassProperties
        {
            CenterOfGravity = new Vector3((float)values[0], (float)values[1], (float)values[2]),
            Volume = values[3],
            SurfaceArea = values[4],
            Mass = values[5],
            MomentsOfInertia = new Vector3((float)values[6], (float)values[7], (float)values[8]),
            ProductsOfInertia = new Vector3((float)values[9], (float)values[10], (float)values[11])
        };

        return true;
    }
}
