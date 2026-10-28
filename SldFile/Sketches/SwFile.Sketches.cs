using System;
using System.Collections.Generic;
using SldFile.Binary;
using SldFile.Sketches;

namespace SldFile;

public abstract partial class SwFile
{
    private static readonly IReadOnlyList<SketchFeature> EmptySketches = Array.AsReadOnly(Array.Empty<SketchFeature>());

    /// <summary>
    /// Sketches in configuration index 0.
    /// Content is read-only and reports incomplete decoding explicitly.
    /// </summary>
    public IReadOnlyList<SketchFeature> Sketches => GetSketches();

    /// <summary>
    /// Reads the selected configuration's ResolvedFeatures and joins XML feature identities when available.
    /// </summary>
    public IReadOnlyList<SketchFeature> GetSketches(int configurationIndex = 0)
    {
        ThrowIfDisposed();
        if (configurationIndex < 0) throw new ArgumentOutOfRangeException(nameof(configurationIndex));
        string path = "Contents/Config-" + configurationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-ResolvedFeatures";
        byte[]? data = ReadSketchStream(path);
        if (data == null && configurationIndex == 0)
        {
            foreach (var candidate in new[] { "Contents/Config-0/ResolvedFeatures", "ResolvedFeatures" })
            {
                data = ReadSketchStream(candidate);
                if (data != null) { path = candidate; break; }
            }
        }
        if (data == null) return EmptySketches;
        return SketchBinaryReader.Read(data, ReadSketchStream("swXmlContents/KeyWords"), path, configurationIndex, Metadata.Units?.Length);
    }

    private byte[]? ReadSketchStream(string path)
    {
        if (RawStreams.TryGetValue(path, out var bytes)) return bytes;
        if (ZlbContents.TryGetValue(path + "__ZLB", out var decoded))
            return decoded.RawData ?? (decoded.IsXml ? System.Text.Encoding.UTF8.GetBytes(decoded.XmlContent) : null);
        if (RawStreams.TryGetValue(path + "__ZLB", out var compressed))
        {
            var value = new ZlbData(); new ZlbStreamReader().Read(compressed, value);
            return value.RawData ?? (value.XmlContent == null ? null : System.Text.Encoding.UTF8.GetBytes(value.XmlContent));
        }
        return null;
    }
}
