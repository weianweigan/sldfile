using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile.Binary;

namespace SldFile;

/// <summary>
/// SolidWorks sketch-based feature template (.ske) file.
/// Supports hole wizard, counterbore, countersink, pipe tap, stud wizard and other feature definitions.
/// </summary>
public sealed class SketchTemplate
{
    private readonly SkeFileData _data;

    /// <summary>File size/indicator from the 4-byte header.</summary>
    public uint HeaderU32 => _data.HeaderU32;

    /// <summary>Entity/feature count in sgSketch.</summary>
    public uint FeatureCount => _data.FeatureCount;

    /// <summary>All dimension node names (e.g. "D1", "Diameter").</summary>
    public IReadOnlyList<string> DimensionNames { get; }

    /// <summary>All length and angle parameter names (e.g. "Drill Angle", "D7").</summary>
    public IReadOnlyList<string> ParameterNames { get; }

    /// <summary>Raw byte data if loaded from a file or byte array.</summary>
    public byte[]? RawData => _data.RawData;

    internal SkeFileData InternalData => _data;

    private SketchTemplate(SkeFileData data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        DimensionNames = Array.AsReadOnly(data.DimensionNames.ToArray());
        ParameterNames = Array.AsReadOnly(data.ParameterNames.ToArray());
    }

    /// <summary>Opens a .ske file from disk.</summary>
    public static SketchTemplate Open(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A file path is required.", nameof(filePath));
        return Load(File.ReadAllBytes(filePath));
    }

    /// <summary>Loads a .ske file from a byte array.</summary>
    public static SketchTemplate Load(byte[] bytes)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        var data = new SkeFileReader().Parse(bytes);
        return new SketchTemplate(data);
    }

    /// <summary>Loads a .ske file from an input stream.</summary>
    public static SketchTemplate Load(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Load(ms.ToArray());
    }

    /// <summary>Serializes this template back to a .ske byte array.</summary>
    public byte[] ToByteArray()
    {
        return new SkeFileWriter().Serialize(_data);
    }

    /// <summary>Saves this template to a file on disk.</summary>
    public void Save(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A file path is required.", nameof(filePath));
        File.WriteAllBytes(filePath, ToByteArray());
    }

    /// <summary>Saves this template to an output stream.</summary>
    public void Save(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException(nameof(stream));
        byte[] bytes = ToByteArray();
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Renames a dimension node name (e.g. "D1" -> "Hole_Dia").
    /// </summary>
    public bool TryRenameDimension(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return false;
        bool found = false;
        foreach (var fd in _data.FeatureData)
        {
            if (fd is NodeNameData nn && nn.Name == oldName)
            {
                nn.Name = newName;
                found = true;
            }
        }
        return found;
    }

    /// <summary>
    /// Renames a length or angle parameter name.
    /// </summary>
    public bool TryRenameParameter(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName)) return false;
        bool found = false;
        foreach (var fd in _data.FeatureData)
        {
            if (fd is LengthParameterCData lp && lp.Name == oldName)
            {
                lp.Name = newName;
                found = true;
            }
            else if (fd is AngleParameterCData ap && ap.Name == oldName)
            {
                ap.Name = newName;
                found = true;
            }
        }
        return found;
    }
}
