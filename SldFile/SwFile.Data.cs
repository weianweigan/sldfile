using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using SldFile.Binary;

namespace SldFile;

public abstract partial class SwFile
{
    private MetadataStore _metadataStore = null!;
    private bool _disposed;

    public bool IsReadOnly => _readOnly;
    public DocumentKind Kind => this is SldAssembly ? DocumentKind.Assembly : this is SldDrawing ? DocumentKind.Drawing : DocumentKind.Part;
    public DocumentMetadata Metadata { get; private set; } = null!;
    public PropertyCollection CustomProperties { get; } = new PropertyCollection();
    public IReadOnlyList<Configuration> Configurations { get; private set; } = Array.Empty<Configuration>();
    public IReadOnlyList<string> Users { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<Revision> Revisions { get; private set; } = Array.Empty<Revision>();
    public IReadOnlyList<DocumentReference> References { get; private set; } = Array.Empty<DocumentReference>();
    public IReadOnlyList<Feature> Features { get; private set; } = Array.Empty<Feature>();
    public IReadOnlyList<string> DrawingSheetNames => SheetNames.Names;

    /// <summary>Returns a copy of the PNG preview, or null when the document has none.</summary>
    public byte[]? GetPreviewPng() => PreviewPng == null ? null : (byte[])PreviewPng.Clone();

    /// <summary>Returns a copy of the Preview stream converted to standard BMP format, or null when none is present.</summary>
    public byte[]? GetPreviewBitmap() => DibBitmapConverter.ConvertToBmp(PreviewImage);

    /// <summary>Returns a copy of the raw Preview stream bytes (4-byte length prefix + DIB), or null when none is present.</summary>
    public byte[]? GetRawPreviewDib() => PreviewImage == null ? null : (byte[])PreviewImage.Clone();

    /// <summary>Decodes the 3D display tessellation mesh from the model's DisplayLists, or null if not present.</summary>
    public DisplayMesh? GetDisplayMesh()
    {
        byte[]? dlBytes = DisplayLists.RawData;
        if (dlBytes == null || dlBytes.Length == 0)
        {
            if (ZlbContents.TryGetValue("Contents/DisplayLists__ZLB", out var zlb) && zlb.RawData != null)
                dlBytes = zlb.RawData;
        }
        if (dlBytes == null || dlBytes.Length == 0)
            return null;

        byte[]? vsBytes = VisualStatesRawData;
        if (vsBytes == null && ZlbContents.TryGetValue("ThirdPtyStore/VisualStates__ZLB", out var vsZlb))
            vsBytes = vsZlb.RawData;

        return DisplayMeshReader.ReadMesh(dlBytes, vsBytes);
    }

    /// <summary>
    /// Gets the assembly component tree for the specified configuration (or active configuration if null).
    /// Returns an empty list if the file is not an assembly or contains no components.
    /// </summary>
    public IReadOnlyList<AssemblyComponentNode> GetAssemblyTree(string? configurationName = null)
    {
        string? xml = CompInstanceTreeXml;
        if (string.IsNullOrEmpty(xml) && ZlbContents.TryGetValue("swXmlContents/COMPINSTANCETREE__ZLB", out var zlb))
        {
            xml = zlb.XmlContent;
        }
        if (string.IsNullOrEmpty(xml))
            return Array.Empty<AssemblyComponentNode>();

        return Assembly.AssemblyTreeReader.ParseAssemblyTree(xml!, configurationName);
    }

    /// <summary>
    /// Generates a Bill of Materials (BOM) for the assembly in the specified configuration.
    /// </summary>
    public IReadOnlyList<BomItem> GetBom(string? configurationName = null)
    {
        var components = GetAssemblyTree(configurationName);
        return Assembly.AssemblyTreeReader.BuildBom(components);
    }

    /// <summary>
    /// Retrieves raw bytes of an embedded virtual component document.
    /// </summary>
    public byte[]? GetVirtualComponentBytes(string componentName)
    {
        string key1 = $"VirtualComp/{componentName}";
        if (RawStreams.TryGetValue(key1, out var bytes)) return bytes;
        string key2 = $"VirtualComp/{componentName}__ZLB";
        if (ZlbContents.TryGetValue(key2, out var zlb) && zlb.RawData != null) return zlb.RawData;
        return null;
    }

    /// <summary>
    /// Extracts the raw Parasolid binary transmit data (X_B format) for the specified configuration.
    /// Returns null if the document does not contain Parasolid geometry.
    /// </summary>
    public byte[]? ExtractParasolid(int configIndex = 0)
    {
        byte[]? partition = null;
        string key1 = $"Contents/Config-{configIndex}-Partition";
        if (RawStreams.TryGetValue(key1, out var bytes))
            partition = bytes;
        else if (Partition != null && configIndex == 0)
            partition = Partition;

        if (partition == null)
            return null;

        return Geometry.ParasolidExtractor.Extract(partition);
    }

    /// <summary>
    /// Exports the Parasolid binary transmit data (X_B) to the specified output stream.
    /// </summary>
    public void ExportParasolid(Stream outputStream, int configIndex = 0)
    {
        if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
        byte[]? data = ExtractParasolid(configIndex);
        if (data == null)
            throw new InvalidOperationException($"No Parasolid geometry found for configuration {configIndex}.");
        outputStream.Write(data, 0, data.Length);
    }

    /// <summary>
    /// Exports the Parasolid binary transmit data (X_B) to the specified file.
    /// </summary>
    public void ExportParasolid(string filePath, int configIndex = 0)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        ExportParasolid(fs, configIndex);
    }

    /// <summary>
    /// Gets preview PNG bytes for the specified drawing sheet index (0-based).
    /// </summary>
    public byte[]? GetSheetPreviewPng(int sheetIndex)
    {
        string[] candidates = {
            $"Images/Sheet_{sheetIndex}",
            $"SheetPreviews/Sheet_{sheetIndex}",
            $"Images/Sheet_{sheetIndex}.png"
        };
        foreach (var path in candidates)
        {
            if (RawStreams.TryGetValue(path, out var bytes))
                return (byte[])bytes.Clone();
        }
        return null;
    }

    /// <summary>
    /// Gets all drawing sheets with their views and preview images.
    /// </summary>
    public IReadOnlyList<DrawingSheetInfo> GetDrawingSheets()
    {
        string? keywordsXml = null;
        if (RawStreams.TryGetValue("swXmlContents/KeyWords", out var kw))
        {
            int start = Array.IndexOf(kw, (byte)'<');
            if (start >= 0)
                keywordsXml = Encoding.UTF8.GetString(kw, start, kw.Length - start);
        }
        else if (ZlbContents.TryGetValue("swXmlContents/KeyWords__ZLB", out var zlb) && zlb.XmlContent != null)
        {
            keywordsXml = zlb.XmlContent;
        }

        return Drawing.DrawingReader.ReadSheets(keywordsXml, DrawingSheetNames, GetSheetPreviewPng);
    }

    private void InitializeUserData(Dictionary<string, byte[]> streams)
    {
        _metadataStore = new MetadataStore(streams);
        Metadata = _metadataStore.ReadMetadata();
        _metadataStore.ReadCustomProperties(CustomProperties);
        Configurations = _metadataStore.ReadConfigurations();
        Users = Array.AsReadOnly(Header2.Users.ToArray());
        Revisions = Array.AsReadOnly(Header2.LogEntries.Select(e =>
        {
            uint? timestamp = e.Timestamp ?? (e.IsTerminal ? Header2.BaseTimestamp : (uint?)null);
            return new Revision(e.Action, e.FeatureName, e.UserName, timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(timestamp!.Value) : (DateTimeOffset?)null);
        }).ToArray());
        References = Array.AsReadOnly(Header2.ExternalReferences.Select(e => new DocumentReference(e.FullPath, e.ShortName)).ToArray());
        var features = new List<Feature>();
        Feature MapFolder(FeatureFolder folder)
        {
            var name = folder.ClassName;
            var kind = name.StartsWith("moRefPlane", StringComparison.Ordinal) ? FeatureKind.ReferencePlane
                : name.StartsWith("moProfileFeature", StringComparison.Ordinal) ? FeatureKind.Sketch
                : name.StartsWith("moExtrusion", StringComparison.Ordinal) ? FeatureKind.Extrusion
                : name.StartsWith("moLengthParameter", StringComparison.Ordinal) ? FeatureKind.Dimension
                : name.Contains("Folder") ? FeatureKind.Folder : FeatureKind.Other;

            var dims = new List<FeatureDimension>();
            if (folder.FeatureData is LengthParameterData lpd)
            {
                dims.Add(new FeatureDimension(folder.DisplayName, lpd.ParamValue.ToString("F3")));
            }
            if (folder.ChildFeatureData != null)
            {
                foreach (var cd in folder.ChildFeatureData)
                {
                    if (cd is LengthParameterData childLpd)
                    {
                        dims.Add(new FeatureDimension("D", childLpd.ParamValue.ToString("F3")));
                    }
                }
            }

            var children = new List<Feature>();
            foreach (var child in folder.Children)
            {
                children.Add(MapFolder(child));
            }

            var attrs = new Dictionary<string, string>
            {
                ["ClassName"] = folder.ClassName,
                ["TypeId"] = folder.Header.TypeId.ToString(),
                ["TagOffset"] = $"0x{folder.TagOffset:X}"
            };

            return new Feature(folder.DisplayName, kind, folder.ClassName, (int)folder.Header.TypeId, attrs, dims.AsReadOnly(), children.AsReadOnly());
        }

        void Visit(IEnumerable<FeatureFolder> folders)
        {
            foreach (var folder in folders)
            {
                var feat = MapFolder(folder);
                features.Add(feat);
                Visit(folder.Children);
            }
        }
        Visit(ResolvedFeatures.Folders);
        if (features.Count == 0)
        {
            byte[]? kwBytes = null;
            if (streams.TryGetValue("swXmlContents/KeyWords", out var kw))
            {
                kwBytes = kw;
            }
            else if (ZlbContents.TryGetValue("swXmlContents/KeyWords__ZLB", out var zlb) && (zlb.RawData != null || zlb.XmlContent != null))
            {
                kwBytes = zlb.RawData ?? (zlb.XmlContent != null ? System.Text.Encoding.UTF8.GetBytes(zlb.XmlContent) : null);
            }
            else if (streams.TryGetValue("swXmlContents/KeyWords__ZLB", out var zlbRaw))
            {
                var zlbData = new ZlbData();
                new ZlbStreamReader().Read(zlbRaw, zlbData);
                kwBytes = zlbData.RawData ?? (zlbData.XmlContent != null ? System.Text.Encoding.UTF8.GetBytes(zlbData.XmlContent) : null);
            }

            if (kwBytes != null)
            {
                features.AddRange(XmlFeatureParser.ParseFeatures(kwBytes));
            }
        }
        Features = features.AsReadOnly();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }
}
