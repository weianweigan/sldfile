using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
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
    /// <summary>Returns a copy of the PNG preview for the specified configuration (or active configuration if 0), or null when none is present.</summary>
    public byte[]? GetPreviewPng(int configIndex = 0)
    {
        if (configIndex > 0)
        {
            string key1 = $"Config-{configIndex}-PreviewPNG";
            if (RawStreams.TryGetValue(key1, out var bytes) && bytes != null && bytes.Length > 0)
                return (byte[])bytes.Clone();

            string key2 = $"Contents/Config-{configIndex}-PreviewPNG";
            if (RawStreams.TryGetValue(key2, out var bytes2) && bytes2 != null && bytes2.Length > 0)
                return (byte[])bytes2.Clone();
        }
        return PreviewPng == null ? null : (byte[])PreviewPng.Clone();
    }

    /// <summary>Returns a copy of the Preview stream converted to standard BMP format, or null when none is present.</summary>
    /// <summary>Returns a copy of the Preview stream converted to standard BMP format for the specified configuration, or null when none is present.</summary>
    public byte[]? GetPreviewBitmap(int configIndex = 0)
    {
        if (configIndex > 0)
        {
            string key1 = $"Config-{configIndex}-Preview";
            if (RawStreams.TryGetValue(key1, out var bytes) && bytes != null && bytes.Length > 0)
                return DibBitmapConverter.ConvertToBmp(bytes);

            string key2 = $"Contents/Config-{configIndex}-Preview";
            if (RawStreams.TryGetValue(key2, out var bytes2) && bytes2 != null && bytes2.Length > 0)
                return DibBitmapConverter.ConvertToBmp(bytes2);
        }
        return DibBitmapConverter.ConvertToBmp(PreviewImage);
    }

    /// <summary>
    /// Returns the best available preview image data (preferring high-resolution BMP over thumbnail PNG, or vice versa).
    /// </summary>
    public byte[]? GetBestPreviewImage(int configIndex = 0)
    {
        var bmp = GetPreviewBitmap(configIndex);
        var png = GetPreviewPng(configIndex);
        if (bmp != null && png != null)
        {
            var (bw, bh) = GetImageDimensions(bmp);
            var (pw, ph) = GetImageDimensions(png);
            return (bw * (long)bh >= pw * (long)ph) ? bmp : png;
        }
        return bmp ?? png;
    }

    /// <summary>
    /// Parses width and height from PNG (IHDR) or BMP headers.
    /// </summary>
    public static (int width, int height) GetImageDimensions(byte[]? imageBytes)
    {
        if (imageBytes == null || imageBytes.Length < 24) return (0, 0);

        // PNG signature: 89 50 4E 47
        if (imageBytes.Length >= 24 && imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
        {
            int w = (imageBytes[16] << 24) | (imageBytes[17] << 16) | (imageBytes[18] << 8) | imageBytes[19];
            int h = (imageBytes[20] << 24) | (imageBytes[21] << 16) | (imageBytes[22] << 8) | imageBytes[23];
            return (Math.Max(0, w), Math.Max(0, h));
        }

        // BMP signature: 42 4D ('BM')
        if (imageBytes.Length >= 26 && imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)
        {
            int w = BitConverter.ToInt32(imageBytes, 18);
            int h = Math.Abs(BitConverter.ToInt32(imageBytes, 22));
            return (Math.Max(0, w), Math.Max(0, h));
        }

        return (0, 0);
    }

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
        {
            foreach (var kvp in ZlbContents)
            {
                if (kvp.Key.EndsWith("DisplayLists__ZLB", StringComparison.OrdinalIgnoreCase) && kvp.Value.RawData != null && kvp.Value.RawData.Length > 0)
                {
                    dlBytes = kvp.Value.RawData;
                    break;
                }
            }
        }
        if (dlBytes == null || dlBytes.Length == 0)
        {
            foreach (var kvp in RawStreams)
            {
                if (kvp.Key.EndsWith("DisplayLists", StringComparison.OrdinalIgnoreCase) && kvp.Value != null && kvp.Value.Length > 0)
                {
                    dlBytes = kvp.Value;
                    break;
                }
            }
        }
        if (dlBytes != null && dlBytes.Length > 0)
        {
            byte[]? vsBytes = VisualStatesRawData;
            if (vsBytes == null && ZlbContents.TryGetValue("ThirdPtyStore/VisualStates__ZLB", out var vsZlb))
                vsBytes = vsZlb.RawData;
            if (vsBytes == null)
            {
                foreach (var kvp in ZlbContents)
                {
                    if (kvp.Key.EndsWith("VisualStates__ZLB", StringComparison.OrdinalIgnoreCase) && kvp.Value.RawData != null && kvp.Value.RawData.Length > 0)
                    {
                        vsBytes = kvp.Value.RawData;
                        break;
                    }
                }
            }
            var mesh = DisplayMeshReader.ReadMesh(dlBytes, vsBytes);
            if (mesh != null && mesh.VertexCount > 0)
                return mesh;
        }

        // 尝试从现代 FaceTessellations 分块流中读取 3D 网格
        var ftMesh = DisplayMeshReader.ReadMeshFromFaceTessellations(RawStreams, ZlbContents);
        if (ftMesh != null && ftMesh.VertexCount > 0)
            return ftMesh;

        // 若是零件文件且无本地网格，尝试检查外部引用实体 (如 MirrorStock / 镜像零件)
        if (Kind == DocumentKind.Part)
        {
            string? derivedBasePath = FindDerivedBasePartPath();
            if (!string.IsNullOrEmpty(derivedBasePath) && File.Exists(derivedBasePath))
            {
                try
                {
                    using var baseSw = SwFile.Open(derivedBasePath!, readOnly: true);
                    var baseMesh = baseSw.GetDisplayMesh();
                    if (baseMesh != null && baseMesh.VertexCount > 0)
                        return baseMesh;
                }
                catch { }
            }
        }

        return null;
    }

    private string? FindDerivedBasePartPath()
    {
        if (!RawStreams.TryGetValue("swXmlContents/Features", out var featBytes) || featBytes.Length == 0)
            return null;
        try
        {
            string xml = Encoding.UTF8.GetString(featBytes);
            int sIdx = xml.IndexOf('<');
            if (sIdx > 0) xml = xml.Substring(sIdx);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            var root = doc.Root;
            if (root == null) return null;

            var extFeat = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "swExtFeature" &&
                (e.Attribute("swFeatureType")?.Value == "MirrorStock" || e.Attribute("swFeatureType")?.Value == "Stock"));
            if (extFeat == null) return null;

            string? modelRef = extFeat.Attribute("swModelRef")?.Value;
            if (string.IsNullOrEmpty(modelRef)) return null;

            var modelElem = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "swModel" &&
                e.Attribute("id")?.Value == modelRef);
            if (modelElem == null) return null;

            string? fileRef = modelElem.Attribute("swFileRef")?.Value;
            if (string.IsNullOrEmpty(fileRef)) return null;

            var fileElem = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "swFile" &&
                e.Attribute("id")?.Value == fileRef);
            if (fileElem == null) return null;

            string? rawPath = fileElem.Attribute("swPath")?.Value;
            if (string.IsNullOrEmpty(rawPath)) return null;

            if (File.Exists(rawPath)) return rawPath;
            if (!string.IsNullOrEmpty(FilePath))
            {
                string fileDir = Path.GetDirectoryName(FilePath)!;
                string localCand = Path.Combine(fileDir, Path.GetFileName(rawPath));
                if (File.Exists(localCand)) return localCand;
            }
            return rawPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 当装配体自身未内嵌 3D 网格或包含外部子装配体时，根据装配树和各子组件的外部零件文件动态变换拼装出整机网格。
    /// </summary>
    public DisplayMesh? StitchAssemblyMeshFromComponents(string? configurationName = null)
    {
        var components = GetAssemblyTree(configurationName);
        if (components == null || components.Count == 0) return null;

        var leafComponents = new List<AssemblyComponentNode>();
        void CollectLeaves(IEnumerable<AssemblyComponentNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.IsSuppressed) continue;
                if (n.Children != null && n.Children.Count > 0)
                {
                    CollectLeaves(n.Children);
                }
                else
                {
                    leafComponents.Add(n);
                }
            }
        }
        CollectLeaves(components);
        if (leafComponents.Count == 0) return null;

        var allPositions = new List<System.Numerics.Vector3>();
        var allNormals = new List<System.Numerics.Vector3>();
        var allIndices = new List<int>();
        var allEdges = new List<(int, int)>();
        var parts = new List<DisplayMeshPart>();

        System.Numerics.Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        System.Numerics.Vector3 max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        var cachedPartMeshes = new Dictionary<string, DisplayMesh>(StringComparer.OrdinalIgnoreCase);
        string? asmDir = !string.IsNullOrEmpty(FilePath) ? Path.GetDirectoryName(FilePath) : null;

        // 优先从装配体自身的 DisplayLists 中提取各组件烘焙的最新原型网格
        // 彻底解决 SolidWorks 多实体派生零件 (SavedExtBody / 焊接件切断) 在装配体中已更新但外部零件文件未单独保存导致的几何错位与断裂断口问题
        Dictionary<string, DisplayMesh>? embeddedPrototypeMeshes = null;
        if (RawStreams.TryGetValue("Contents/DisplayLists", out var dlBytes) && dlBytes.Length > 0)
        {
            try
            {
                var nativeMesh = GetDisplayMesh();
                if (nativeMesh != null && nativeMesh.Parts != null && nativeMesh.Parts.Count > 0)
                {
                    embeddedPrototypeMeshes = ExtractEmbeddedPrototypeMeshes(dlBytes, nativeMesh);
                }
            }
            catch { }
        }

        // 收集所有去重后的外部子零件路径，利用多核 CPU 进行异步并行预解码，显著提升装配体首次拼装速度
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var comp in leafComponents)
        {
            string compFileName = Path.GetFileNameWithoutExtension(comp.FilePath);
            if (embeddedPrototypeMeshes != null &&
                (embeddedPrototypeMeshes.ContainsKey(comp.InstanceName) ||
                 embeddedPrototypeMeshes.ContainsKey(compFileName) ||
                 embeddedPrototypeMeshes.ContainsKey(comp.Name)))
            {
                continue; // 已在内嵌原型网格中命中，无需重复从磁盘加载外部零件文件
            }

            string actualPath = comp.FilePath;
            if (!File.Exists(actualPath) && !string.IsNullOrEmpty(asmDir))
            {
                string candidate = Path.Combine(asmDir, Path.GetFileName(comp.FilePath));
                if (File.Exists(candidate)) actualPath = candidate;
            }
            if (!string.IsNullOrEmpty(actualPath) && File.Exists(actualPath))
            {
                uniquePaths.Add(actualPath);
            }
        }

        if (uniquePaths.Count > 1)
        {
            Parallel.ForEach(uniquePaths, path =>
            {
                try
                {
                    using var psw = SwFile.Open(path, readOnly: true);
                    var m = psw.GetDisplayMesh();
                    if (m != null && m.Positions.Count > 0)
                    {
                        lock (cachedPartMeshes)
                        {
                            cachedPartMeshes[path] = m;
                        }
                    }
                }
                catch { }
            });
        }

        foreach (var comp in leafComponents)
        {
            DisplayMesh? childMesh = null;
            string compFileName = Path.GetFileNameWithoutExtension(comp.FilePath);

            // 1. 优先尝试从装配体内嵌烘焙的最新网格字典中获取原型几何
            if (embeddedPrototypeMeshes != null)
            {
                if (!embeddedPrototypeMeshes.TryGetValue(comp.InstanceName, out childMesh))
                {
                    if (!embeddedPrototypeMeshes.TryGetValue(compFileName, out childMesh))
                    {
                        embeddedPrototypeMeshes.TryGetValue(comp.Name, out childMesh);
                    }
                }
            }

            // 2. 若内嵌网格未命中，则回退到读取外部 .SLDPRT 零件文件
            if (childMesh == null)
            {
                string actualPath = comp.FilePath;
                if (!File.Exists(actualPath) && !string.IsNullOrEmpty(asmDir))
                {
                    string candidate = Path.Combine(asmDir, Path.GetFileName(comp.FilePath));
                    if (File.Exists(candidate)) actualPath = candidate;
                }

                if (!string.IsNullOrEmpty(actualPath) && File.Exists(actualPath))
                {
                    if (!cachedPartMeshes.TryGetValue(actualPath, out childMesh))
                    {
                        try
                        {
                            using var psw = SwFile.Open(actualPath, readOnly: true);
                            childMesh = psw.GetDisplayMesh();
                            if (childMesh != null)
                            {
                                cachedPartMeshes[actualPath] = childMesh;
                            }
                        }
                        catch { }
                    }
                }
            }

            if (childMesh == null || childMesh.Positions.Count == 0) continue;

            int vStart = allPositions.Count;
            int tStart = allIndices.Count / 3;
            int eStart = allEdges.Count;

            float tx = comp.Transform.M41 * 1000f;
            float ty = comp.Transform.M42 * 1000f;
            float tz = comp.Transform.M43 * 1000f;

            for (int i = 0; i < childMesh.Positions.Count; i++)
            {
                var p = childMesh.Positions[i];
                float x = p.X * comp.Transform.M11 + p.Y * comp.Transform.M21 + p.Z * comp.Transform.M31 + tx;
                float y = p.X * comp.Transform.M12 + p.Y * comp.Transform.M22 + p.Z * comp.Transform.M32 + ty;
                float z = p.X * comp.Transform.M13 + p.Y * comp.Transform.M23 + p.Z * comp.Transform.M33 + tz;
                var transformedP = new System.Numerics.Vector3(x, y, z);
                allPositions.Add(transformedP);
                min = System.Numerics.Vector3.Min(min, transformedP);
                max = System.Numerics.Vector3.Max(max, transformedP);
            }

            for (int i = 0; i < childMesh.Normals.Count; i++)
            {
                var n = childMesh.Normals[i];
                float nx = n.X * comp.Transform.M11 + n.Y * comp.Transform.M21 + n.Z * comp.Transform.M31;
                float ny = n.X * comp.Transform.M12 + n.Y * comp.Transform.M22 + n.Z * comp.Transform.M32;
                float nz = n.X * comp.Transform.M13 + n.Y * comp.Transform.M23 + n.Z * comp.Transform.M33;
                var transformedN = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(nx, ny, nz));
                allNormals.Add(transformedN);
            }

            for (int i = 0; i < childMesh.TriangleIndices.Count; i++)
            {
                allIndices.Add(childMesh.TriangleIndices[i] + vStart);
            }

            for (int i = 0; i < childMesh.OutlineEdges.Count; i++)
            {
                var (e0, e1) = childMesh.OutlineEdges[i];
                allEdges.Add((e0 + vStart, e1 + vStart));
            }

            var surfRef = new[] { new DisplaySurfaceReference { Identity = comp.InstanceName } };

            if (childMesh.Parts != null && childMesh.Parts.Count > 0)
            {
                foreach (var cp in childMesh.Parts)
                {
                    parts.Add(new DisplayMeshPart
                    {
                        Id = $"stitched:{comp.ComponentId}:{parts.Count}",
                        StreamIndex = 0,
                        TableIndex = parts.Count,
                        TableOffset = 0,
                        VertexStart = vStart + cp.VertexStart,
                        VertexCount = cp.VertexCount,
                        TriangleStart = tStart + cp.TriangleStart,
                        TriangleCount = cp.TriangleCount,
                        EdgeStart = eStart + cp.EdgeStart,
                        EdgeCount = cp.EdgeCount,
                        StripCount = cp.StripCount,
                        NormalsDerived = cp.NormalsDerived,
                        Color = cp.Color,
                        ColorSource = cp.ColorSource,
                        AppearanceName = !string.IsNullOrEmpty(comp.InstanceName) ? comp.InstanceName : cp.AppearanceName,
                        SurfaceReferences = surfRef
                    });
                }
            }
            else
            {
                parts.Add(new DisplayMeshPart
                {
                    Id = $"stitched:{comp.ComponentId}:{parts.Count}",
                    StreamIndex = 0,
                    TableIndex = parts.Count,
                    TableOffset = 0,
                    VertexStart = vStart,
                    VertexCount = childMesh.Positions.Count,
                    TriangleStart = tStart,
                    TriangleCount = childMesh.TriangleIndices.Count / 3,
                    EdgeStart = eStart,
                    EdgeCount = childMesh.OutlineEdges.Count,
                    StripCount = 0,
                    NormalsDerived = false,
                    AppearanceName = comp.InstanceName,
                    SurfaceReferences = surfRef
                });
            }
        }

        if (allPositions.Count == 0) return null;

        return new DisplayMesh
        {
            Positions = allPositions.AsReadOnly(),
            Normals = allNormals.AsReadOnly(),
            TriangleIndices = allIndices.AsReadOnly(),
            OutlineEdges = allEdges.AsReadOnly(),
            Parts = parts.AsReadOnly(),
            Min = min,
            Max = max
        };
    }

    /// <summary>
    /// 从装配体 DisplayLists 数据流中按组件 UTF-16 标记（&lt;InstanceName&gt;@&lt;AssemblyName&gt;）切分并提取内嵌的原型几何网格。
    /// 当装配体由多实体保存实体（SavedExtBody）派生且外部子零件在磁盘上未单独保存时，此处包含 SolidWorks 保存时刻各组件的最新物理真实几何。
    /// </summary>
    private static Dictionary<string, DisplayMesh> ExtractEmbeddedPrototypeMeshes(byte[] dlBytes, DisplayMesh nativeMesh)
    {
        var prototypeMeshes = new Dictionary<string, DisplayMesh>(StringComparer.OrdinalIgnoreCase);
        if (dlBytes == null || dlBytes.Length < 8 || nativeMesh == null || nativeMesh.Parts == null || nativeMesh.Parts.Count == 0)
            return prototypeMeshes;

        var instances = new List<(int Offset, string Name)>();
        for (int i = 0; i <= dlBytes.Length - 5; i++)
        {
            if (dlBytes[i] == 0xFF && dlBytes[i + 1] == 0xFE && dlBytes[i + 2] == 0xFF)
            {
                int len = dlBytes[i + 3];
                int textOffset = i + 4;
                if (len == 0xFF && i + 5 < dlBytes.Length)
                {
                    len = BitConverter.ToUInt16(dlBytes, i + 4);
                    textOffset = i + 6;
                }
                if (len > 0 && textOffset + len * 2 <= dlBytes.Length)
                {
                    string s = Encoding.Unicode.GetString(dlBytes, textOffset, len * 2);
                    if (s.Contains("@") && !s.EndsWith("_COMPPATEND", StringComparison.OrdinalIgnoreCase))
                    {
                        instances.Add((i, s));
                    }
                }
            }
        }

        if (instances.Count == 0) return prototypeMeshes;

        for (int i = 0; i < instances.Count; i++)
        {
            int startOff = instances[i].Offset;
            int endOff = (i + 1 < instances.Count) ? instances[i + 1].Offset : dlBytes.Length;

            string s = instances[i].Name;
            int atIdx = s.IndexOf('@');
            string compPart = atIdx > 0 ? s.Substring(0, atIdx) : s;
            int slashIdx = compPart.LastIndexOf('/');
            if (slashIdx >= 0) compPart = compPart.Substring(slashIdx + 1);

            string fullName = compPart;
            string baseName = fullName;
            int dashIdx = fullName.LastIndexOf('-');
            if (dashIdx > 0 && int.TryParse(fullName.Substring(dashIdx + 1), out _))
            {
                baseName = fullName.Substring(0, dashIdx);
            }

            if (prototypeMeshes.ContainsKey(baseName) && prototypeMeshes.ContainsKey(fullName))
                continue;

            var matchedParts = nativeMesh.Parts.Where(p => p.TableOffset >= startOff && p.TableOffset < endOff).ToList();
            if (matchedParts.Count > 0)
            {
                var subPositions = new List<System.Numerics.Vector3>();
                var subNormals = new List<System.Numerics.Vector3>();
                var subIndices = new List<int>();
                var subEdges = new List<(int, int)>();
                var subParts = new List<DisplayMeshPart>();
                foreach (var p in matchedParts)
                {
                    int pVStart = subPositions.Count;
                    int pTStart = subIndices.Count / 3;
                    int pEStart = subEdges.Count;

                    for (int vIdx = 0; vIdx < p.VertexCount; vIdx++)
                    {
                        subPositions.Add(nativeMesh.Positions[p.VertexStart + vIdx]);
                        if (p.VertexStart + vIdx < nativeMesh.Normals.Count)
                            subNormals.Add(nativeMesh.Normals[p.VertexStart + vIdx]);
                    }
                    for (int tIdx = 0; tIdx < p.TriangleCount * 3; tIdx++)
                    {
                        subIndices.Add(nativeMesh.TriangleIndices[p.TriangleStart * 3 + tIdx] - p.VertexStart + pVStart);
                    }
                    for (int eIdx = 0; eIdx < p.EdgeCount; eIdx++)
                    {
                        if (p.EdgeStart + eIdx < nativeMesh.OutlineEdges.Count)
                        {
                            var (e0, e1) = nativeMesh.OutlineEdges[p.EdgeStart + eIdx];
                            subEdges.Add((e0 - p.VertexStart + pVStart, e1 - p.VertexStart + pVStart));
                        }
                    }
                    subParts.Add(new DisplayMeshPart
                    {
                        Id = p.Id,
                        TableOffset = p.TableOffset,
                        VertexStart = pVStart,
                        VertexCount = p.VertexCount,
                        TriangleStart = pTStart,
                        TriangleCount = p.TriangleCount,
                        EdgeStart = pEStart,
                        EdgeCount = p.EdgeCount,
                        StripCount = p.StripCount,
                        NormalsDerived = p.NormalsDerived,
                        Color = p.Color,
                        ColorSource = p.ColorSource,
                        AppearanceName = p.AppearanceName,
                        SurfaceReferences = p.SurfaceReferences
                    });
                }
                var protoMesh = new DisplayMesh
                {
                    Positions = subPositions.AsReadOnly(),
                    Normals = subNormals.AsReadOnly(),
                    TriangleIndices = subIndices.AsReadOnly(),
                    OutlineEdges = subEdges.AsReadOnly(),
                    Parts = subParts.AsReadOnly(),
                    Min = new System.Numerics.Vector3(subPositions.Min(v => v.X), subPositions.Min(v => v.Y), subPositions.Min(v => v.Z)),
                    Max = new System.Numerics.Vector3(subPositions.Max(v => v.X), subPositions.Max(v => v.Y), subPositions.Max(v => v.Z))
                };
                if (!prototypeMeshes.ContainsKey(baseName))
                    prototypeMeshes[baseName] = protoMesh;
                if (!prototypeMeshes.ContainsKey(fullName))
                    prototypeMeshes[fullName] = protoMesh;
            }
        }

        return prototypeMeshes;
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

        string? asmDir = !string.IsNullOrEmpty(FilePath) ? Path.GetDirectoryName(FilePath) : null;
        return Assembly.AssemblyTreeReader.ParseAssemblyTree(xml!, configurationName, asmDir);
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
    /// Gets the best available preview image bytes for the specified drawing sheet (0-based),
    /// preferring high-resolution preview bitmap (e.g. 2850x1436) when available.
    /// </summary>
    public byte[]? GetBestSheetPreviewImage(int sheetIndex)
    {
        var sheetPng = GetSheetPreviewPng(sheetIndex);
        bool isMultiSheet = (DrawingSheetNames != null && DrawingSheetNames.Count > 1)
            || RawStreams.ContainsKey("Images/Sheet_1")
            || RawStreams.ContainsKey("SheetPreviews/Sheet_1");

        // Only for single-sheet drawings does the document-level Preview bitmap
        // (which is a snapshot of whichever sheet was active on save) guaranteed to be sheet 0.
        // For multi-sheet drawings, using the document root preview causes
        // the active sheet at save time to be mistakenly shown on all sheets or on sheet 0.
        if (sheetIndex == 0 && !isMultiSheet)
        {
            var highResBmp = GetPreviewBitmap(0);
            if (highResBmp != null)
            {
                if (sheetPng == null) return highResBmp;
                var (bw, bh) = GetImageDimensions(highResBmp);
                var (pw, ph) = GetImageDimensions(sheetPng);
                if (bw * (long)bh >= pw * (long)ph) return highResBmp;
            }
        }
        return sheetPng ?? (sheetIndex == 0 ? GetPreviewBitmap(0) : null);
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

        RawStreams.TryGetValue("Contents/Definition", out var defnStream);
        RawStreams.TryGetValue("Contents/VBLists", out var vbListsStream);
        RawStreams.TryGetValue("Contents/DisplayLists", out var displayListsStream);
        return Drawing.DrawingReader.ReadSheets(keywordsXml, DrawingSheetNames, GetSheetPreviewPng, GetBestSheetPreviewImage, defnStream, vbListsStream, displayListsStream);
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
        if (!string.IsNullOrEmpty(SolidWorksInformation.ConfigurationName) && Configurations != null && Configurations.Count > 0)
        {
            for (int i = 0; i < Configurations.Count; i++)
            {
                if (string.Equals(Configurations[i].Name, SolidWorksInformation.ConfigurationName, StringComparison.OrdinalIgnoreCase))
                {
                    ActiveConfigurationIndex = i;
                    break;
                }
            }
        }
        RefreshFeaturesFromFolders(streams);
    }

    internal void ReParseResolvedFeatures(byte[] newStream)
    {
        var reader = new SldFile.Binary.ResolvedFeaturesStreamReader();
        reader.Parse(newStream, ResolvedFeatures);
        RefreshFeaturesFromFolders(RawStreams);
    }

    internal void RefreshFeaturesFromFolders(Dictionary<string, byte[]> streams)
    {
        var features = new List<Feature>();
        Feature MapFolder(FeatureFolder folder)
        {
            var name = folder.ClassName;
            var kind = name.StartsWith("moRefPlane", StringComparison.Ordinal) ? FeatureKind.ReferencePlane
                : name.StartsWith("moProfileFeature", StringComparison.Ordinal) ? FeatureKind.Sketch
                : name.StartsWith("moExtruRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceExtrude
                : name.StartsWith("moRevolvRefSurf", StringComparison.Ordinal) ? FeatureKind.SurfaceRevolve
                : name.StartsWith("moPlanarSurface", StringComparison.Ordinal) ? FeatureKind.PlanarSurface
                : name.StartsWith("moSewRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceKnit
                : name.StartsWith("moExtendRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceExtend
                : name.StartsWith("moOffsetRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceOffset
                : name.StartsWith("moFillRefSurface", StringComparison.Ordinal) ? FeatureKind.FillSurface
                : name.StartsWith("moSweepRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceSweep
                : name.StartsWith("moBlendRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceLoft
                : name.StartsWith("moWeldMemberFeat", StringComparison.Ordinal) ? FeatureKind.StructuralMember
                : name.StartsWith("moEndCap", StringComparison.Ordinal) ? FeatureKind.EndCap
                : name.StartsWith("moCosmeticWeldBead", StringComparison.Ordinal) ? FeatureKind.WeldBead
                : (name.StartsWith("moExtrusion", StringComparison.Ordinal) || name == "moICE_c" ||
                   name.StartsWith("moExtruThin", StringComparison.Ordinal)) ? (folder.DisplayName.Contains("切除") || name == "moICE_c" ? FeatureKind.Cut : FeatureKind.Extrusion)
                : name.StartsWith("moRevolution", StringComparison.Ordinal) ? FeatureKind.Revolve
                : name.StartsWith("moRevCut", StringComparison.Ordinal) ? FeatureKind.Cut
                : name.StartsWith("moSweep", StringComparison.Ordinal) ? FeatureKind.Sweep
                : name.StartsWith("moLoftedBend_", StringComparison.Ordinal) ? FeatureKind.LoftedBend
                : name.StartsWith("moJog_", StringComparison.Ordinal) ? FeatureKind.Jog
                : name.StartsWith("moFold_", StringComparison.Ordinal) ? FeatureKind.Fold
                : name.StartsWith("moUnFold_", StringComparison.Ordinal) ? FeatureKind.UnFold
                : name.StartsWith("moShell", StringComparison.Ordinal) ? FeatureKind.Shell
                : name.StartsWith("moDeleteBody", StringComparison.Ordinal) ? FeatureKind.DeleteBody
                : name.StartsWith("moDraft", StringComparison.Ordinal) ? FeatureKind.Draft
                : name.StartsWith("moRib", StringComparison.Ordinal) ? FeatureKind.Rib
                : name.StartsWith("moGusset", StringComparison.Ordinal) ? FeatureKind.Gusset
                : name.StartsWith("moCombineBodies", StringComparison.Ordinal) ? FeatureKind.CombineBodies
                : name.StartsWith("moSplit", StringComparison.Ordinal) ? FeatureKind.SplitBody
                : name.StartsWith("moTrimRefSurface", StringComparison.Ordinal) ? FeatureKind.SurfaceTrim
                : (name.StartsWith("moBlend", StringComparison.Ordinal) || name.StartsWith("moLoft", StringComparison.Ordinal)) ? FeatureKind.Loft
                : name.StartsWith("VarFillet", StringComparison.Ordinal) ? FeatureKind.Fillet
                : name.StartsWith("Fillet_", StringComparison.Ordinal) ? FeatureKind.Fillet
                : name.StartsWith("Chamfer_", StringComparison.Ordinal) ? FeatureKind.Chamfer
                : (name.StartsWith("moLPattern_", StringComparison.Ordinal) ||
                   name.StartsWith("moCirPattern_", StringComparison.Ordinal)) ? FeatureKind.Pattern
                : name.StartsWith("moMirrorPattern_", StringComparison.Ordinal) ? FeatureKind.Mirror
                : name.StartsWith("moHoleWzd", StringComparison.Ordinal) ? FeatureKind.HoleWizard
                : name.StartsWith("moSMBaseFlange", StringComparison.Ordinal) ? FeatureKind.Extrusion
                : name.StartsWith("moHem_", StringComparison.Ordinal) ? FeatureKind.Hem
                : name.StartsWith("moFlatPattern_", StringComparison.Ordinal) ? FeatureKind.FlatPattern
                : name.StartsWith("moLengthParameter", StringComparison.Ordinal) ? FeatureKind.Dimension
                : name.Contains("Folder") ? FeatureKind.Folder : FeatureKind.Other;

            var dims = new List<FeatureDimension>();
            if (folder.FeatureData is LengthParameterData lpd)
            {
                dims.Add(new FeatureDimension(folder.DisplayName, lpd.ParamValue.ToString("F3")));
            }
            else if (folder.FeatureData is ExtrudeFeatureDefinitionData extDef)
            {
                dims.Add(new FeatureDimension("Depth", (extDef.Depth * 1000).ToString("F2") + "mm"));
            }
            else if (folder.FeatureData is SheetMetalBaseFlangeDefinitionData smDef)
            {
                dims.Add(new FeatureDimension("Thickness", (smDef.Thickness * 1000).ToString("F2") + "mm"));
                dims.Add(new FeatureDimension("BendRadius", (smDef.BendRadius * 1000).ToString("F2") + "mm"));
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

            return new Feature(folder.DisplayName, kind, folder.ClassName, (int)folder.Header.TypeId, attrs, dims.AsReadOnly(), children.AsReadOnly(), isSuppressed: false, isVisible: true, definition: folder.FeatureData);
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
