using SldFile.Mfc;
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

    /// <summary>Returns the currently active configuration, or the first configuration if none is explicitly marked active.</summary>
    internal Configuration? ActiveConfiguration => Configurations.FirstOrDefault(c => c.Index == ActiveConfigurationIndex) ?? Configurations.FirstOrDefault();
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
    /// Extracts the raw Parasolid binary transmit data (X_B format) for the specified configuration index (0 = active/default).
    /// Returns null if the document does not contain Parasolid geometry.
    /// </summary>
    public byte[]? ExtractParasolid(int configIndex = 0)
    {
        return Geometry.ParasolidExtractor.ExtractFromStreams(RawStreams, configIndex);
    }

    /// <summary>
    /// Extracts the raw Parasolid binary transmit data (X_B format) for the specified configuration name.
    /// Returns null if the configuration is not found or does not contain Parasolid geometry.
    /// </summary>
    public byte[]? ExtractParasolid(string? configurationName)
    {
        int targetIndex = ActiveConfigurationIndex;
        if (!string.IsNullOrEmpty(configurationName))
        {
            var cfg = Configurations.FirstOrDefault(c => string.Equals(c.Name, configurationName, StringComparison.OrdinalIgnoreCase));
            if (cfg != null) targetIndex = cfg.Index;
        }

        return ExtractParasolid(targetIndex);
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

        return DrawingReader.ReadSheets(keywordsXml, DrawingSheetNames, GetSheetPreviewPng);
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
        MfcArchive.DeserializeInto(newStream, ResolvedFeatures);
        RefreshFeaturesFromFolders(RawStreams);
    }

    internal void RefreshFeaturesFromFolders(Dictionary<string, byte[]> streams)
    {
        var features = new List<Feature>();

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
                ZlbStreamHandler.Parse(zlbRaw, zlbData);
                kwBytes = zlbData.RawData ?? (zlbData.XmlContent != null ? System.Text.Encoding.UTF8.GetBytes(zlbData.XmlContent) : null);
            }

            if (kwBytes != null)
            {
                features.AddRange(XmlFeatureParser.ParseFeatures(kwBytes));
            }
        }
        Features = features.AsReadOnly();
    }

    internal static Feature MapFolder(FeatureFolder folder)
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
            var p = lpd.ToParameterModel();
            p.Name = folder.DisplayName;
            dims.Add(new FeatureDimension(folder.DisplayName, lpd.ParamValue, SketchDimensionType.Distance, lpd.ParamValue.ToString("F3"), p, lpd.ToDisplayDimension()));
        }
        else if (folder.FeatureData is ExtrudeFeatureModel extDef)
        {
            var d1Param = new MoLengthParameterModel("D1@" + folder.DisplayName, extDef.Depth);
            var d1Disp = new MoDisplayDistanceDimModel();
            dims.Add(new FeatureDimension("Depth", extDef.Depth, SketchDimensionType.Distance, (extDef.Depth * 1000).ToString("F2") + "mm", d1Param, d1Disp));

            if (extDef.BothDirections && extDef.DepthDirection2 > 0)
            {
                var d2Param = new MoLengthParameterModel("D2@" + folder.DisplayName, extDef.DepthDirection2);
                var d2Disp = new MoDisplayDistanceDimModel();
                dims.Add(new FeatureDimension("Depth2", extDef.DepthDirection2, SketchDimensionType.Distance, (extDef.DepthDirection2 * 1000).ToString("F2") + "mm", d2Param, d2Disp));
            }
            if (extDef.DraftAngle > 0)
            {
                var draftParam = new MoAngleParameterModel("Draft@" + folder.DisplayName, extDef.DraftAngle);
                var draftDisp = new MoDisplayAngularDimModel();
                dims.Add(new FeatureDimension("DraftAngle", extDef.DraftAngle, SketchDimensionType.Angle, $"{(extDef.DraftAngle * 180.0 / Math.PI):F2}°", draftParam, draftDisp));
            }
        }
        else if (folder.FeatureData is SheetMetalBaseFlangeModel smDef)
        {
            var thkParam = new MoLengthParameterModel("Thickness@" + folder.DisplayName, smDef.Thickness);
            dims.Add(new FeatureDimension("Thickness", smDef.Thickness, SketchDimensionType.Distance, (smDef.Thickness * 1000).ToString("F2") + "mm", thkParam));

            var radParam = new MoLengthParameterModel("BendRadius@" + folder.DisplayName, smDef.BendRadius);
            var radDisp = new MoDisplayRadialDimModel { Radius = smDef.BendRadius };
            dims.Add(new FeatureDimension("BendRadius", smDef.BendRadius, SketchDimensionType.Radius, (smDef.BendRadius * 1000).ToString("F2") + "mm", radParam, radDisp));
        }
        else if (folder.FeatureData is RevolveFeatureModel revDef)
        {
            var angParam = new MoAngleParameterModel("D1@" + folder.DisplayName, revDef.Angle);
            var angDisp = new MoDisplayAngularDimModel { ArcAngle = revDef.Angle };
            dims.Add(new FeatureDimension("Angle", revDef.Angle, SketchDimensionType.Angle, $"{(revDef.Angle * 180.0 / Math.PI):F2}°", angParam, angDisp));

            if (revDef.BothDirections && revDef.AngleDirection2 > 0)
            {
                var ang2Param = new MoAngleParameterModel("D2@" + folder.DisplayName, revDef.AngleDirection2);
                var ang2Disp = new MoDisplayAngularDimModel { ArcAngle = revDef.AngleDirection2 };
                dims.Add(new FeatureDimension("Angle2", revDef.AngleDirection2, SketchDimensionType.Angle, $"{(revDef.AngleDirection2 * 180.0 / Math.PI):F2}°", ang2Param, ang2Disp));
            }
        }
        else if (folder.FeatureData is FilletFeatureModel filletDef)
        {
            if (filletDef.DefaultRadius.HasValue)
            {
                var radParam = new MoLengthParameterModel("Radius@" + folder.DisplayName, filletDef.DefaultRadius.Value);
                var radDisp = new MoDisplayRadialDimModel { Radius = filletDef.DefaultRadius.Value };
                dims.Add(new FeatureDimension("Radius", filletDef.DefaultRadius.Value, SketchDimensionType.Radius, (filletDef.DefaultRadius.Value * 1000).ToString("F2") + "mm", radParam, radDisp));
            }
        }
        else if (folder.FeatureData is ChamferFeatureModel chamferDef)
        {
            if (chamferDef.EdgeChamferDistanceSide0.HasValue)
            {
                var distParam = new MoLengthParameterModel("Distance@" + folder.DisplayName, chamferDef.EdgeChamferDistanceSide0.Value);
                dims.Add(new FeatureDimension("Distance", chamferDef.EdgeChamferDistanceSide0.Value, SketchDimensionType.Distance, (chamferDef.EdgeChamferDistanceSide0.Value * 1000).ToString("F2") + "mm", distParam));
            }
            if (chamferDef.EdgeChamferAngle.HasValue)
            {
                var angParam = new MoAngleParameterModel("Angle@" + folder.DisplayName, chamferDef.EdgeChamferAngle.Value);
                dims.Add(new FeatureDimension("Angle", chamferDef.EdgeChamferAngle.Value, SketchDimensionType.Angle, $"{(chamferDef.EdgeChamferAngle.Value * 180.0 / Math.PI):F2}°", angParam));
            }
        }
        else if (folder.FeatureData is HoleWizardFeatureModel holeDef)
        {
            if (holeDef.Diameter.HasValue)
            {
                var diaParam = new MoLengthParameterModel("Diameter@" + folder.DisplayName, holeDef.Diameter.Value);
                var diaDisp = new MoDisplayDiameterDimModel { Diameter = holeDef.Diameter.Value };
                dims.Add(new FeatureDimension("Diameter", holeDef.Diameter.Value, SketchDimensionType.Diameter, (holeDef.Diameter.Value * 1000).ToString("F2") + "mm", diaParam, diaDisp));
            }
            if (holeDef.Depth.HasValue)
            {
                var depParam = new MoLengthParameterModel("Depth@" + folder.DisplayName, holeDef.Depth.Value);
                dims.Add(new FeatureDimension("Depth", holeDef.Depth.Value, SketchDimensionType.Distance, (holeDef.Depth.Value * 1000).ToString("F2") + "mm", depParam));
            }
        }
        else if (folder.FeatureData is ShellFeatureModel shellDef)
        {
            if (shellDef.Thickness.HasValue)
            {
                var thkParam = new MoLengthParameterModel("Thickness@" + folder.DisplayName, shellDef.Thickness.Value);
                dims.Add(new FeatureDimension("Thickness", shellDef.Thickness.Value, SketchDimensionType.Distance, (shellDef.Thickness.Value * 1000).ToString("F2") + "mm", thkParam));
            }
        }
        if (folder.ChildFeatureData != null)
        {
            foreach (var cd in folder.ChildFeatureData)
            {
                if (cd is LengthParameterData childLpd)
                {
                    var childParam = childLpd.ToParameterModel();
                    dims.Add(new FeatureDimension("D", childLpd.ParamValue, SketchDimensionType.Distance, childLpd.ParamValue.ToString("F3"), childParam));
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

        return new Feature(folder.DisplayName, kind, folder.ClassName, (int)folder.Header.TypeId, attrs, dims.AsReadOnly(), children.AsReadOnly(), isSuppressed: false, isVisible: true, model: folder.FeatureData);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }
}
