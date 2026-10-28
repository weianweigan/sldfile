using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Typed data model for a SolidWorks .ske file — a sketch-based feature definition
/// (hole, counterbore, countersink, pipe tap, stud wizard, etc.).
///
/// Parsed via <see cref="SkeFileReader"/> using the MfcDocument tree model.
/// The <see cref="FeatureData"/> list contains all recognized named objects
/// in file order, each backed by an <see cref="IFeatureData"/> instance.
/// </summary>
internal sealed class SkeFileData
{
    /// <summary>
    /// File size indicator from the 4-byte header.
    /// 1127 (Wzscbord/Wzscbore), 1137 (standard features), 14164 (stud wizard).
    /// </summary>
    public uint HeaderU32 { get; internal set; }

    /// <summary>
    /// First u32 in sgSketch — entity/feature count (observed range: 9–19).
    /// </summary>
    public uint FeatureCount { get; internal set; }

    /// <summary>
    /// All recognized named MFC objects in file order, each represented as
    /// an <see cref="IFeatureData"/> instance with its RawData preserved.
    /// </summary>
    public IReadOnlyList<IFeatureData> FeatureData { get; internal set; } = Array.Empty<IFeatureData>();

    /// <summary>
    /// Convenience: sgSketch data (always the first element if present).
    /// </summary>
    public SketchData? Sketch => FindFirst<SketchData>();

    /// <summary>
    /// Convenience: all moNodeName_c dimension names (e.g. "D1", "Diameter").
    /// </summary>
    public IReadOnlyList<string> DimensionNames
    {
        get
        {
            var names = new List<string>();
            foreach (var fd in FeatureData)
            {
                if (fd is NodeNameData nn && nn.Name.Length > 0)
                    names.Add(nn.Name);
            }
            return names.AsReadOnly();
        }
    }

    /// <summary>
    /// Convenience: all moLengthParameter_c / moAngleParameter_c names
    /// (e.g. "Drill Angle", "D7", "D9").
    /// </summary>
    public IReadOnlyList<string> ParameterNames
    {
        get
        {
            var names = new List<string>();
            foreach (var fd in FeatureData)
            {
                if (fd is LengthParameterCData lp && lp.Name.Length > 0)
                    names.Add(lp.Name);
                else if (fd is AngleParameterCData ap && ap.Name.Length > 0)
                    names.Add(ap.Name);
            }
            return names.AsReadOnly();
        }
    }

    /// <summary>Original raw bytes for roundtrip fidelity.</summary>
    internal byte[]? RawData;

    /// <summary>
    /// Find the first <see cref="IFeatureData"/> of a given type.
    /// </summary>
    public T? FindFirst<T>() where T : class, IFeatureData
    {
        foreach (var fd in FeatureData)
        {
            if (fd is T t)
                return t;
        }
        return null;
    }

    /// <summary>
    /// Find all <see cref="IFeatureData"/> of a given type.
    /// </summary>
    public List<T> FindAll<T>() where T : class, IFeatureData
    {
        var result = new List<T>();
        foreach (var fd in FeatureData)
        {
            if (fd is T t)
                result.Add(t);
        }
        return result;
    }
}
