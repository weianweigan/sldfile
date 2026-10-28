using OpenMcdf;
using SldFile.Binary;
using SldFile.Diagnostics;
using SldFile.Ole;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SldFile;

public abstract partial class SwFile : IDisposable
{
    private readonly RootStorage? _root;
    private readonly SldStreamRegistry _registry = new();
    private readonly bool _readOnly;
    private readonly bool _isSectionContainer;

    // Original section container state, retained for incremental saves.
    private byte[]? _originalFileData;
    private byte _fileKey;

    public string FilePath { get; }

    #region Streams
    /// <summary>Read-only view of standard OLE summary information (Windows Explorer compatible).</summary>
    internal Summary Summary { get; } = new Summary();

    /// <summary>Read-only view of standard OLE document summary information (Windows Explorer compatible).</summary>
    internal DocumentSummary DocumentSummary { get; } = new DocumentSummary();

    /// <summary>Primary source of file information. Edit metadata here.</summary>
    internal SolidWorksInformation SolidWorksInformation { get; } = new SolidWorksInformation();

    /// <summary>Raw internal model timestamps.</summary>
    internal ModelStamps ModelStamps { get; } = new ModelStamps();

    /// <summary>Access configuration-specific OLE properties mapped by configuration name.</summary>
    internal Dictionary<string, ConfigProperties> ConfigurationProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Dictionary mapping stream paths to ZLB-compressed data (XML or binary).</summary>
    internal Dictionary<string, ZlbData> ZlbContents { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Header2 stream data: users, revision logs, and external file references.</summary>
    internal Header2Data Header2 { get; } = new();

    /// <summary>ResolvedFeatures stream data: FeatureManager design tree.</summary>
    internal ResolvedFeaturesData ResolvedFeatures { get; } = new();

    /// <summary>DLUpdateStamp stream data (6 bytes): type flag + zero pad.</summary>
    internal DlUpdateStampData DlUpdateStamp { get; } = new();

    /// <summary>VisualStates_DisplayPane stream data (8 bytes): display value.</summary>
    internal VisualStatesDisplayPaneData VisualStatesDisplayPane { get; } = new();
    internal byte[]? VisualStatesRawData { get; set; }
    internal string? CompInstanceTreeXml { get; set; }
    internal Dictionary<string, byte[]> RawStreams { get; set; } = new();

    /// <summary>CMMotionLoadMapU stream data (40 bytes): motion load map header.</summary>
    internal CmMotionLoadMapUData CmMotionLoadMapU { get; } = new();

    /// <summary>SheetPreviews/SheetNames stream data: drawing sheet names.</summary>
    internal SheetNamesData SheetNames { get; } = new();

    /// <summary>CMgrHdr2 stream data: configuration manager header.</summary>
    internal CmgrHdr2Data CmgrHdr2 { get; } = new();

    /// <summary>Biography stream data: file access path history.</summary>
    internal BiographyData Biography { get; } = new();

    /// <summary>History stream data: version history with date-code stamps.</summary>
    internal HistoryData History { get; } = new();

    /// <summary>LWDATA stream data: lightweight display data.</summary>
    internal LwData LwData { get; } = new();

    /// <summary>CMgr stream data: configuration manager.</summary>
    internal CmgrData Cmgr { get; } = new();

    /// <summary>MatesList stream data: assembly mate constraints.</summary>
    internal MatesListData MatesList { get; } = new();

    /// <summary>Definition stream data: document-wide settings.</summary>
    internal DefinitionData Definition { get; } = new();

    /// <summary>Config-0 stream data: per-config document data with Header2-like front-end.</summary>
    internal Config0Data Config0 { get; } = new();

    /// <summary>DisplayLists stream data: display settings, lights, materials (ZLB-compressed MFC).</summary>
    internal DisplayListsData DisplayLists { get; } = new();

    /// <summary>User Units Table stream data: unit system configuration (ZLB-compressed MFC).</summary>
    internal UserUnitsTableData UserUnitsTable { get; } = new();

    /// <summary>View Orientation Data stream: view orientation settings (ZLB-compressed, often empty).</summary>
    internal ViewOrientationData ViewOrientation { get; } = new();

    /// <summary>Preview image raw bytes (BMP format).</summary>
    internal byte[]? PreviewImage { get; set; }

    /// <summary>Preview PNG image raw bytes.</summary>
    internal byte[]? PreviewPng { get; set; }

    /// <summary>MATERIALTREE XML text (UTF-16LE).</summary>
    internal string? MaterialTreeXml { get; set; }

    /// <summary>Parasolid geometry data from GhostPartition.</summary>
    internal byte[]? GhostPartition { get; set; }

    /// <summary>Parasolid geometry data from Partition.</summary>
    internal byte[]? Partition { get; set; }
    #endregion

    protected SwFile(string filePath, bool readOnly)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A file path is required.", nameof(filePath));
        FilePath = filePath;
        _readOnly = readOnly;

        if (IsSectionContainer(filePath))
        {
            _isSectionContainer = true;

            var data = File.ReadAllBytes(filePath);
            var reader = new SectionContainerReader(data);
            _fileKey = reader.Key;
            _originalFileData = data;
            var streams = reader.ExtractAllStreams();
            if (!streams.ContainsKey("Header2"))
                throw new InvalidDataException("The document has no supported model data.");
            _registry.LoadFromStreams(streams, this);
            InitializeUserData(streams);
        }
        else
        {
            if (readOnly)
                _root = RootStorage.OpenRead(filePath);
            else
                _root = RootStorage.Open(filePath, FileMode.Open, FileAccess.ReadWrite, StorageModeFlags.Transacted);

            try
            {
                var streams = StorageStreams.Read(_root);
                _registry.LoadFromStreams(streams, this);
                InitializeUserData(streams);
            }
            catch
            {
                _root.Dispose();
                throw;
            }
        }
    }

    /// <summary>Checks whether a file uses the observed section container.</summary>
    private static bool IsSectionContainer(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = new byte[8];
        if (fs.Read(header, 0, 8) < 8)
            return false;
        return SectionContainerReader.IsSectionContainer(header);
    }

    public static SwFile Open(string filePath, bool readOnly = true)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext switch
        {
            ".sldprt" => new SldPart(filePath, readOnly),
            ".sldasm" => new SldAssembly(filePath, readOnly),
            ".slddrw" => new SldDrawing(filePath, readOnly),
            _ => throw new NotSupportedException($"Not supported file format: {ext}")
        };
    }

    public void Save()
    {
        ThrowIfDisposed();
        if (_readOnly)
            throw new SldFileException("File is opened in read-only mode.");

        var updatedStreams = _metadataStore.PrepareChanges(Metadata, CustomProperties, Configurations);
        if (updatedStreams.Count == 0) return;

        if (_isSectionContainer)
        {
            if (!File.ReadAllBytes(FilePath).SequenceEqual(_originalFileData!))
                throw new IOException("The document changed on disk. Reopen it before saving.");
            var writer = new SectionContainerWriter(_originalFileData!, _fileKey);
            var newFileData = writer.Write(updatedStreams);
            var reader = new SectionContainerReader(newFileData);
            var savedStreams = reader.ExtractAllStreams();
            foreach (var pair in updatedStreams)
                if (!savedStreams.TryGetValue(pair.Key, out var saved) || !saved.SequenceEqual(pair.Value))
                    throw new InvalidDataException("The document container cannot preserve the edited properties.");

            var tempPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(tempPath, newFileData);
                File.Replace(tempPath, FilePath, null);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            _originalFileData = newFileData;
            _fileKey = reader.Key;
        }
        else
        {
            foreach (var pair in updatedStreams) StorageStreams.Write(_root!, pair.Key, pair.Value);
            _root!.Commit();
        }
        _metadataStore.AcceptChanges(updatedStreams, Metadata, CustomProperties, Configurations);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _root?.Dispose();
        _disposed = true;
    }
}
