using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

using SldFile.Binary;
namespace SldFile.Mfc;

/// <summary>
/// Maps a generic MfcDocument tree to the typed ResolvedFeaturesData model.
/// Replaces the tag filtering and tree-building logic previously in
/// ResolvedFeaturesStreamReader.Parse.
/// </summary>
internal sealed class ResolvedFeaturesModel : IMfcSerializable
{
    public ushort StreamVersion { get; set; }
    public int DocumentVersion { get; set; }
    public string FileType { get; set; } = "Unknown";
    public List<FeatureFolder> Folders { get; } = new();
    public byte[]? RawData { get; set; }

    internal List<FeatureFolder> FoldersBuilder = new List<FeatureFolder>();

    public ResolvedFeaturesModel() { }

    public void Freeze()
    {
        Folders.Clear();
        Folders.AddRange(FoldersBuilder);
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

            ushort ver = StreamVersion;
            ar.U16(ref ver);
        }
        else
        {
            Folders.Clear();
            FoldersBuilder.Clear();
            byte[] data = ar.ReadRemainingBytes();
            RawData = data;
            if (data == null || data.Length < 2) return;

            var doc = MfcDocument.Parse(data);
            Map(doc, this);
        }
    }

    public static void Map(MfcDocument doc, ResolvedFeaturesModel model)
    {
        model.RawData = doc.SourceData;
        model.FoldersBuilder.Clear();

        // Stream version: first u16 from raw data
        if (doc.SourceData.Length >= 2)
        {
            int offset = 0;
            model.StreamVersion = MfcPrimitives.ReadU16(doc.SourceData, ref offset);
            model.FileType = MfcPrimitives.ClassifyStreamType(model.StreamVersion);
        }

        // Build folder tree from _ct/_cM elements using depth-based stacking.
        // Position-based containment from flat element list cannot correctly
        // handle dense tag sequences where consecutive siblings have no
        // untagged data between them, so we use class-name-based depth instead.
        // This approach matches the Python reference parser's _depth_from_name().
        var folderStack = new Stack<FeatureFolder>();
        var folderByTagOffset = new Dictionary<int, FeatureFolder>();

        foreach (var elem in doc.AllElements)
        {
            if (elem.TagByte == 0x01 && IsFolderClass(elem.ClassName))
            {
                var folder = new FeatureFolder
                {
                    ClassName = elem.ClassName,
                    DisplayName = elem.AsWChar() ?? string.Empty,
                    TagOffset = elem.TagOffset,
                    DataStart = elem.DataStart,
                    Header = ParseFolderHeader(elem)
                };

                // Try to parse feature-specific data beyond the FolderHeader
                if (FeatureDataRegistry.TryParse(elem, out var folderData))
                {
                    // Propagate TypeId from the FolderHeader for folder classes that carry it
                    if (folderData is LengthParameterData lpd)
                        lpd.TypeId = folder.Header.TypeId;
                    folder.FeatureData = folderData;
                    if (folderData is SurfaceTrimFeatureModel stm && (!stm.SerializedTypeId.HasValue || stm.SerializedTypeId.Value == 0 || stm.SerializedTypeId.Value == SurfaceTrimFeatureModel.HeaderTypeId))
                    {
                        stm.SerializedTypeId = folder.Header.TypeId;
                        stm.Resolution.SerializedTypeId = true;
                    }
                }

                folderByTagOffset[elem.TagOffset] = folder;

                // Pop until we find the right parent based on expected depth
                int expectedDepth = GetExpectedDepth(folder.ClassName);
                while (folderStack.Count > expectedDepth)
                    folderStack.Pop();

                if (folderStack.Count > 0)
                    folderStack.Peek().ChildrenBuilder.Add(folder);
                else
                    model.FoldersBuilder.Add(folder);

                folderStack.Push(folder);
            }
        }

        // Second pass: parse non-folder named elements and attach to parent folders.
        // Find the nearest preceding folder by TagOffset for each non-folder element.
        AttachChildFeatureData(doc, folderByTagOffset);

        // Synthesize feature definitions across feature windows
        SynthesizeFeatureDefinitions(doc, folderByTagOffset);

        // Freeze all folder children
        FreezeFolders(model.FoldersBuilder);
        model.Freeze();
    }

    /// <summary>
    /// Walk ALL named non-folder elements from the flat element list,
    /// parse their feature data, and attach to the nearest preceding folder
    /// (by TagOffset position in the stream).
    /// </summary>
    private static void AttachChildFeatureData(MfcDocument doc, Dictionary<int, FeatureFolder> folderByTagOffset)
    {
        var sortedFolders = new List<KeyValuePair<int, FeatureFolder>>(folderByTagOffset);
        sortedFolders.Sort((a, b) => a.Key.CompareTo(b.Key));

        foreach (var elem in doc.AllElements)
        {
            if (elem.TagByte != 0x01 || elem.ClassName.Length == 0 || IsFolderClass(elem.ClassName))
                continue;

            if (FeatureDataRegistry.TryParse(elem, out var childData) && childData != null)
            {
                FeatureFolder? parent = null;
                foreach (var kvp in sortedFolders)
                {
                    if (kvp.Key < elem.TagOffset)
                        parent = kvp.Value;
                    else
                        break;
                }

                if (parent != null)
                {
                    parent.ChildFeatureDataBuilder.Add(childData);
                }
            }
        }
    }

    // ── Unified Folder Class & Depth Registry ────────────────────────────────

    private static readonly HashSet<string> TopLevelClassPrefixes = new(StringComparer.Ordinal)
    {
        "moFavoriteFolder_",
        "moHistoryFolder_",
        "moSelectionSetFolder_",
        "moSensorFolder_",
        "moDocsFolder_",
        "moDetailCabinet_",
        "moSolidBodyFolder_",
        "moSurfaceBodyFolder_",
        "moCommentsFolder_",
        "moEqnFolder_",
        "moMaterialFolder_",
        "moRefPlane_",
        "moOriginProfileFeature_",
        "moProfileFeature_",
        "moExtrusion_",
        "moExtruRefSurface_",
        "moRevolvRefSurf_",
        "moPlanarSurface_",
        "moSewRefSurface_",
        "moExtendRefSurface_",
        "moOffsetRefSurface_",
        "moFillRefSurface_",
        "moSweepRefSurface_",
        "moBlendRefSurface_",
        "moWeldMemberFeat_",
        "moEndCap_",
        "moCosmeticWeldCutList_",
        "moICE_",
        "moRevolution_",
        "Fillet_",
        "Chamfer_",
        "moChamfer_",
        "moLPattern_",
        "moCirPattern_",
        "moSweep_",
        "moLoft_",
        "moBlend_",
        "moShell_",
        "moDraft_",
        "moRib_",
        "moGusset_",
        "moCombineBodies_",
        "moSplit_",
        "moTrimRefSurface_",
        "moSplitBody_",
        "moDeleteBody_",
        "moThicken_",
        "moTemplateSheetMetal_",
        "moSMBaseFlange_",
        "moEdgeFlange_",
        "moMiterFlange_",
        "moHem_",
        "moJog_",
        "moLoftedBend_",
        "moSMNormalCut_",
        "moFold_",
        "moUnFold_",
        "moSheetMetal_",
        "moTemplateFlatPattern_",
        "moFlatPattern_",
        "moNotesAreaFtrFolder_",
        "moInkMarkupFolder_",
        "moConfigTableFolder_",
        "moNativeConfigurationTableFeature_",
        "moMateGroup_",
        "moFtrFolder_"
    };

    private static readonly HashSet<string> ModernFolderClasses = BuildModernFolderClasses();

    private static HashSet<string> BuildModernFolderClasses()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (string prefix in TopLevelClassPrefixes)
        {
            set.Add(prefix + "c");
        }
        // Additional modern folder classes whose expected tree depth is >= 1
        set.Add("moFoldUpDownSketchFolder_c");
        set.Add("moCosmeticWeldSubFolder_c");
        set.Add("moCosmeticWeldBead_c");
        set.Add("moExtruThin_c");
        set.Add("moRevolutionThin_c");
        set.Add("moRevCut_c");
        set.Add("VarFillet_c");
        set.Add("moMirrorPattern_c");
        set.Add("moHoleWzd_c");
        return set;
    }

    internal static bool IsFolderClass(string className) =>
        className.EndsWith("_ct", StringComparison.Ordinal) ||
        className.EndsWith("_cM", StringComparison.Ordinal) ||
        ModernFolderClasses.Contains(className);

    /// <summary>
    /// Expected tree depth for known folder/feature classes.
    /// Matches the Python reference parser's _depth_from_name() function.
    /// </summary>
    private static int GetExpectedDepth(string className)
    {
        int lastUnderscore = className.LastIndexOf('_');
        if (lastUnderscore >= 0)
        {
            string prefix = className.Substring(0, lastUnderscore + 1);
            if (TopLevelClassPrefixes.Contains(prefix))
                return 0;
            if (prefix == "moCosmeticWeldSubFolder_")
                return 1;
            if (prefix == "moCosmeticWeldBead_")
                return 2;
        }

        // Depth 1+: nested features (parameters, sub-features, etc.)
        return 1;
    }

    private static void FreezeFolders(List<FeatureFolder> folders)
    {
        foreach (var f in folders)
        {
            FreezeFolders(f.ChildrenBuilder);
            f.Freeze();
        }
    }

    /// <summary>
    /// Parses the folder_header structure shared by all _ct/_cM classes.
    /// Uses marker-based scanning within the element's raw data to locate
    /// magic, MarkerE, and ChildFlag regardless of exact byte offsets.
    /// </summary>
    public static FolderHeader ParseFolderHeader(MfcElement element)
    {
        var header = new FolderHeader();
        byte[] data = element.RawData;
        int dataEnd = data.Length;

        // Skip the display_name WChar to find header start
        int offset = 0;
        while (offset + 3 <= dataEnd && !MfcPrimitives.IsWCharPrefix(data, offset))
            offset++;
        if (offset + 3 <= dataEnd)
            MfcPrimitives.ReadWChar(data, ref offset);

        if (offset + 8 > dataEnd)
            return header;

        int savedOffset = offset;

        header.Timestamp = MfcPrimitives.ReadF64(data, ref offset);
        header.TypeId = MfcPrimitives.ReadU32(data, ref offset);

        int searchEnd = Math.Min(dataEnd, data.Length);

        // Search for magic: byte pattern 00 FF FF FF FF followed by 8 bytes
        int magicOffset = -1;
        byte[] magicPattern = { 0x00, 0xFF, 0xFF, 0xFF, 0xFF };
        for (int i = offset + 80; i < searchEnd - 13; i++)
        {
            bool match = true;
            for (int j = 0; j < magicPattern.Length; j++)
            {
                if (data[i + j] != magicPattern[j])
                    { match = false; break; }
            }
            if (match)
            {
                magicOffset = i + magicPattern.Length;
                header.Magic = BitConverter.ToUInt64(data, magicOffset);
                if (magicOffset + 20 <= searchEnd)
                {
                    header.SubType = BitConverter.ToUInt32(data, magicOffset + 8);
                    header.VersionFlag = BitConverter.ToUInt32(data, magicOffset + 12);
                    header.HasData = BitConverter.ToUInt32(data, magicOffset + 16);
                }
                break;
            }
        }

        // Search for MarkerE in the range [+20, +60) from header start
        for (int i = offset + 20; i < Math.Min(offset + 60, searchEnd - 4); i++)
        {
            uint candidate = BitConverter.ToUInt32(data, i);
            if (candidate > 0 && candidate < 1000)
            {
                header.MarkerE = candidate;
                if (i + 8 <= searchEnd)
                    header.SizeIndicator = BitConverter.ToUInt32(data, i + 4);
                if (i > offset)
                    header.Reserved1 = MfcPrimitives.CopyRange(data, offset, i);
                break;
            }
        }

        if (header.MarkerE == 0)
            header.Reserved1 = MfcPrimitives.CopyRange(data, offset, Math.Min(offset + 20, dataEnd));

        // Search for ChildFlag in the [+90, +120) region
        for (int i = offset + 90; i < Math.Min(offset + 120, searchEnd - 4); i++)
        {
            uint candidate = BitConverter.ToUInt32(data, i);
            if (candidate == FolderHeader.KnownChildFlag || (candidate > 0 && candidate < 16))
            {
                header.ChildFlag = candidate;
                break;
            }
        }

        // Read ExtraValue: first non-zero f64 after the first 24 bytes
        int evScan = offset + 24;
        while (evScan + 8 <= searchEnd)
        {
            double d = BitConverter.ToDouble(data, evScan);
            if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) > 0.0001)
            {
                header.ExtraValue = d;
                break;
            }
            evScan += 8;
        }

        header.IsValid = header.Magic != 0;

        return header;
    }

    private static double FindFirstParameterValue(byte[] raw, int start, int end)
    {
        if (raw == null || raw.Length < 30) return 0;
        int max = Math.Min(end, raw.Length) - 30;
        for (int i = Math.Max(0, start); i <= max; i++)
        {
            if (raw[i] != 0x00) continue;
            var val = MfcParameterReader.ReadDoubleDescriptorAfterName(raw, i);
            if (val.HasValue && val.Value > 0)
                return val.Value;
        }
        return 0;
    }

    // ── Feature Synthesis Pipeline ───────────────────────────────────────────

    private static void SynthesizeFeatureDefinitions(MfcDocument doc, Dictionary<int, FeatureFolder> folderByTagOffset)
    {
        var raw = doc.SourceData;
        var sortedFolders = folderByTagOffset.Values.OrderBy(f => f.TagOffset).ToList();

        for (int i = 0; i < sortedFolders.Count; i++)
        {
            var folder = sortedFolders[i];
            if (folder.FeatureData != null)
                continue;

            int start = folder.TagOffset;
            int end = (i + 1 < sortedFolders.Count) ? sortedFolders[i + 1].TagOffset : raw.Length;

            // 1. Dispatch any registered standalone feature-window parser first
            if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? registeredFeature))
            {
                folder.FeatureData = registeredFeature;
                continue;
            }

            // 2. Dispatch multi-folder context-dependent synthesizers
            if (folder.ClassName == "moEdgeFlange_c")
            {
                int windowStart = i > 0 ? sortedFolders[i - 1].TagOffset : 0;
                var edgeFlangeElement = doc.AllElements.FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (edgeFlangeElement != null)
                {
                    folder.FeatureData = EdgeFlangeFeatureModel.Parse(
                        doc, edgeFlangeElement, windowStart, end);
                }
                continue;
            }

            if (folder.ClassName != ExtrudeThinFeatureParser.ClassName &&
                folder.ClassName != "moExtrusion_c" &&
                folder.ClassName != "moExtrusion_ct" &&
                folder.ClassName != "moICE_c" &&
                folder.ClassName != "moSMBaseFlange_c")
            {
                continue;
            }

            var parentSketchFolder = sortedFolders.Take(i).LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
            var parentSketchElement = parentSketchFolder == null
                ? null
                : doc.AllElements.FirstOrDefault(e => e.TagOffset == parentSketchFolder.TagOffset);
            var parentSketch = ParentSketchReferenceParser.TryResolve(doc, parentSketchElement);
            string? sketchName = parentSketch?.DisplayName
                ?? (string.IsNullOrEmpty(parentSketchFolder?.DisplayName) ? null : parentSketchFolder!.DisplayName);
            var chain = GetFirstChild<SketchChainData>(parentSketchFolder);
            var regions = GetAllChildren<SketchRegionData>(parentSketchFolder);

            var fromSkt = GetFirstChild<FromSktEntSurfIdRepData>(folder);
            var fromSkt3Int = GetFirstChild<FromSktEnt3IntSurfIdRepData>(folder);
            var faceRef = GetFirstChild<FaceRefData>(folder);
            var endFace = GetFirstChild<EndFaceSurfIdRepData>(folder);
            var endFace3Int = GetFirstChild<EndFace3IntSurfIdRepData>(folder);
            var compFace = GetFirstChild<CompFaceData>(folder);
            var edgeRef = GetFirstChild<EdgeRefData>(folder);
            var compEdge = GetFirstChild<CompEdgeData>(folder);
            var compRefPlane = GetFirstChild<CompRefPlaneData>(folder);
            var faceRefPln = GetFirstChild<FaceRefPlnData>(folder);
            var chooser = GetFirstChild<PerBodyChooserData>(folder);
            var compBodies = GetAllChildren<CompSolidBodyData>(folder);

            if (folder.ClassName == ExtrudeThinFeatureParser.ClassName)
            {
                ushort thinVersion = raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0;
                if (ExtrudeThinFeatureParser.TryParse(doc, folder, thinVersion, start, end, out var thinData) &&
                    thinData != null)
                {
                    thinData.SketchName = sketchName;
                    thinData.AvailableSketchContours = TopologyDecoder.BuildContourReferences(chain, fromSkt, fromSkt3Int, includeCandidates: true);
                    thinData.AvailableSketchRegions = TopologyDecoder.BuildRegionReferences(regions);
                    thinData.DirectionReference = TopologyDecoder.BuildMfcDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
                    thinData.BodyScope = TopologyDecoder.BuildMfcBodyScope(chooser, compBodies);
                    if (thinData.BodyScope != null)
                    {
                        thinData.AutoSelect = thinData.BodyScope.AutoSelect;
                        thinData.FeatureScope = !thinData.BodyScope.AutoSelect;
                    }
                    thinData.IsBaseExtrude = !sortedFolders.Take(i).Any(f =>
                        f.ClassName == "moExtrusion_c" || f.ClassName == "moSMBaseFlange_c" ||
                        f.ClassName == ExtrudeThinFeatureParser.ClassName);
                    folder.FeatureData = thinData;
                }
            }
            else if (folder.ClassName == "moExtrusion_c" || folder.ClassName == "moExtrusion_ct" || folder.ClassName == "moICE_c")
            {
                SynthesizeExtrude(
                    doc, raw, sortedFolders, i, folder, start, end,
                    parentSketchFolder, sketchName, chain, regions,
                    fromSkt, fromSkt3Int, faceRef, endFace, endFace3Int, compFace,
                    edgeRef, compEdge, compRefPlane, faceRefPln, chooser, compBodies);
            }
            else if (folder.ClassName == "moSMBaseFlange_c")
            {
                SynthesizeSheetMetalBaseFlange(
                    raw, sortedFolders, i, folder, start, end,
                    sketchName, chain, regions,
                    edgeRef, compEdge, compRefPlane, faceRefPln, chooser, compBodies);
            }
        }
    }

    private static T? GetFirstChild<T>(FeatureFolder? folder) where T : class
    {
        if (folder == null) return null;
        return folder.ChildFeatureDataBuilder.OfType<T>().FirstOrDefault()
            ?? folder.ChildFeatureData.OfType<T>().FirstOrDefault();
    }

    private static IEnumerable<T> GetAllChildren<T>(FeatureFolder? folder) where T : class
    {
        if (folder == null) return Enumerable.Empty<T>();
        return folder.ChildFeatureDataBuilder.OfType<T>()
            .Concat(folder.ChildFeatureData.OfType<T>());
    }

    private static void SynthesizeExtrude(
        MfcDocument doc,
        byte[] raw,
        List<FeatureFolder> sortedFolders,
        int folderIndex,
        FeatureFolder folder,
        int start,
        int end,
        FeatureFolder? parentSketchFolder,
        string? sketchName,
        SketchChainData? chain,
        IEnumerable<SketchRegionData> regions,
        FromSktEntSurfIdRepData? fromSkt,
        FromSktEnt3IntSurfIdRepData? fromSkt3Int,
        FaceRefData? faceRef,
        EndFaceSurfIdRepData? endFace,
        EndFace3IntSurfIdRepData? endFace3Int,
        CompFaceData? compFace,
        EdgeRefData? edgeRef,
        CompEdgeData? compEdge,
        CompRefPlaneData? compRefPlane,
        FaceRefPlnData? faceRefPln,
        PerBodyChooserData? chooser,
        IEnumerable<CompSolidBodyData> compBodies)
    {
        var extData = new ExtrudeFeatureModel();
        extData.IsCut = folder.ClassName == "moICE_c" ||
                        folder.DisplayName.Contains("切除") ||
                        folder.DisplayName.IndexOf("cut", StringComparison.OrdinalIgnoreCase) >= 0;

        var endSpec = GetFirstChild<EndSpecData>(folder);
        if (endSpec != null)
        {
            extData.EndCondition = endSpec.EndCondition;
            var reverse = ExtrudeOptionParser.ReadReverseDirection(
                raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0, folder.ClassName, endSpec.RawData);
            if (reverse.HasValue)
            {
                extData.ReverseDirection = reverse.Value;
                extData.MarkResolvedInternal(nameof(ExtrudeFeatureModel.ReverseDirection));
            }
        }

        double depth = FindFirstParameterValue(raw, start, end);
        if (depth > 0)
        {
            extData.Depth = depth;
        }
        else
        {
            var ppDim = folder.ChildFeatureData.OfType<ParallelPlaneDistanceDimData>().FirstOrDefault(d => d.DimValue > 0);
            if (ppDim != null) extData.Depth = ppDim.DimValue;
        }

        bool structuralEndFace = false;
        bool structuralBodyScope = false;
        if (folder.ClassName == "moICE_c")
        {
            var singleFaceElement = doc.AllElements.FirstOrDefault(e =>
                e.ClassName == "moSingleFaceRef_w" && e.TagOffset >= start && e.TagOffset < end);
            var singleFace = singleFaceElement != null
                ? PersistentFaceReferenceParser.ReadSingleFaceRef(doc, singleFaceElement)
                : null;
            if (singleFace != null && singleFace.FaceId > 0)
            {
                extData.EndCondition = 4;
                extData.MarkResolvedInternal(nameof(ExtrudeFeatureModel.EndCondition));
                extData.EndConditionEntity = new EndConditionEntityReference
                {
                    EntityType = SelectionEntityType.Face,
                    EntityId = singleFace.FaceId,
                    FaceCount = 1,
                    SurfaceName = $"Face_0x{singleFace.FaceId:X}"
                };
                extData.MarkResolvedInternal(nameof(ExtrudeFeatureModel.EndConditionEntity));
                structuralEndFace = true;
            }

            var compBodyElement = doc.AllElements.FirstOrDefault(e =>
                e.ClassName == "moCompSolidBody_c" && e.TagOffset >= start && e.TagOffset < end);
            var bodyRef = compBodyElement != null
                ? CompSolidBodyReferenceParser.Read(doc, compBodyElement)
                : null;
            if (bodyRef != null && bodyRef.IsExplicitBodySelection)
            {
                extData.BodyScope = new BodyScopeData
                {
                    AutoSelect = false,
                    Flags = chooser?.ChooserFlags ?? 0,
                    BodyIds = new[] { bodyRef.BodyId },
                    Bodies = new[]
                    {
                        new EntityReference
                        {
                            EntityId = bodyRef.BodyId,
                            EntityType = SelectionEntityType.Body,
                            Name = $"SolidBody_{bodyRef.BodyId}"
                        }
                    }
                };
                extData.AutoSelect = false;
                extData.FeatureScope = true;
                extData.MarkResolvedInternal(nameof(ExtrudeFeatureModel.BodyScope));
                extData.MarkResolvedInternal(nameof(ExtrudeFeatureModel.AutoSelect));
                extData.MarkResolvedInternal(nameof(ExtrudeFeatureModel.FeatureScope));
                structuralBodyScope = true;
            }
        }

        extData.SketchName = sketchName;
        extData.AvailableSketchContours = TopologyDecoder.BuildContourReferences(chain, fromSkt, fromSkt3Int, includeCandidates: true);
        extData.AvailableSketchRegions = TopologyDecoder.BuildRegionReferences(regions);

        ushort selectionHeader = raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0;
        if ((selectionHeader == 0x70 || selectionHeader == 0x71) &&
            folder.ClassName == "moExtrusion_c" && parentSketchFolder != null &&
            sortedFolders.Take(folderIndex).Count(f => f.ClassName.StartsWith("moProfileFeature_")) == 1)
        {
            var selection = SketchSelectionParser.Read(doc, parentSketchFolder.TagOffset, start);
            if (selection.Kind == SketchSelectionEnvelopeKind.None)
            {
                extData.SketchSelectionMode = SketchSelectionMode.WholeSketch;
            }
            else if (selection.Kind == SketchSelectionEnvelopeKind.Contour)
            {
                extData.SketchSelectionMode = SketchSelectionMode.ExplicitSelection;
                extData.SelectedContours = selection.Contours;
            }
            else if (selection.Kind == SketchSelectionEnvelopeKind.Region)
            {
                extData.SketchSelectionMode = SketchSelectionMode.ExplicitSelection;
                extData.SelectedRegions = selection.Regions;
            }
            else
            {
                extData.SketchSelectionMode = SketchSelectionMode.Unknown;
            }
        }

        extData.DirectionReference = TopologyDecoder.BuildMfcDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
        if (!structuralEndFace)
        {
            extData.EndConditionEntity = extData.EndCondition == 0 || extData.EndCondition == 6
                ? null
                : TopologyDecoder.BuildMfcEndConditionEntity(faceRef, endFace, compFace, endFace3Int);
        }
        if (!structuralBodyScope)
        {
            extData.BodyScope = TopologyDecoder.BuildMfcBodyScope(chooser, compBodies);
            if (extData.BodyScope != null)
            {
                extData.AutoSelect = extData.BodyScope.AutoSelect;
                extData.FeatureScope = !extData.BodyScope.AutoSelect;
            }
        }
        extData.BothDirections = extData.EndCondition == 6;
        ExtrudeAdditionalOptionParser.Apply(doc, folder.ClassName, start, end, extData);
        ExtrudeCutOptionParser.Apply(doc,
            raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0,
            folder.ClassName, start, end, extData);
        extData.IsBaseExtrude = !sortedFolders.Take(folderIndex).Any(f => f.ClassName == "moExtrusion_c" || f.ClassName == "moSMBaseFlange_c");

        folder.FeatureData = extData;
    }

    private static void SynthesizeSheetMetalBaseFlange(
        byte[] raw,
        List<FeatureFolder> sortedFolders,
        int folderIndex,
        FeatureFolder folder,
        int start,
        int end,
        string? sketchName,
        SketchChainData? chain,
        IEnumerable<SketchRegionData> regions,
        EdgeRefData? edgeRef,
        CompEdgeData? compEdge,
        CompRefPlaneData? compRefPlane,
        FaceRefPlnData? faceRefPln,
        PerBodyChooserData? chooser,
        IEnumerable<CompSolidBodyData> compBodies)
    {
        var smData = new SheetMetalBaseFlangeModel();

        var template = sortedFolders.Take(folderIndex).LastOrDefault(f => f.ClassName == "moTemplateSheetMetal_c");
        var previousBase = sortedFolders.Take(folderIndex).LastOrDefault(f => f.ClassName == "moSMBaseFlange_c");
        if (template != null && (previousBase == null || template.TagOffset > previousBase.TagOffset))
            SheetMetalTemplateParser.Read(raw, template.TagOffset, start, smData);

        SheetMetalBaseFlangeParser.Read(raw, start, end, smData);

        double? sheetMetalK = null;
        bool conflictingK = false;
        uint? allowanceType = null;
        bool conflictingAllowanceType = false;
        double? bendAllowance = null;
        bool conflictingBendAllowance = false;
        double? bendDeduction = null;
        bool conflictingBendDeduction = false;
        bool? useDefaultBendRelief = null;
        bool conflictingUseDefaultBendRelief = false;
        string? bendTableFile = null;
        bool conflictingBendTableFile = false;
        for (int j = 0; j < sortedFolders.Count; j++)
        {
            if (sortedFolders[j].ClassName != "moSheetMetal_c") continue;
            int globalEnd = j + 1 < sortedFolders.Count ? sortedFolders[j + 1].TagOffset : raw.Length;
            if (SheetMetalBaseFlangeParser.TryReadKFactor(raw, sortedFolders[j].TagOffset, globalEnd, out double k))
            {
                conflictingK |= sheetMetalK.HasValue && sheetMetalK.Value != k;
                sheetMetalK = k;
            }
            if (SheetMetalBaseFlangeParser.TryReadBendTableFile(
                    raw, sortedFolders[j].TagOffset, globalEnd, out string tablePath))
            {
                conflictingBendTableFile |= bendTableFile != null &&
                    !string.Equals(bendTableFile, tablePath, StringComparison.Ordinal);
                bendTableFile = tablePath;
            }

            var allowanceProbe = new SheetMetalBaseFlangeModel();
            if (SheetMetalBaseFlangeParser.TryReadBendAllowance(
                    raw, sortedFolders[j].TagOffset, globalEnd, allowanceProbe))
            {
                if (allowanceProbe.ParameterResolution.BendAllowanceType)
                {
                    conflictingAllowanceType |= allowanceType.HasValue &&
                        allowanceType.Value != allowanceProbe.BendAllowanceType;
                    allowanceType = allowanceProbe.BendAllowanceType;
                }
                if (allowanceProbe.ParameterResolution.BendAllowance)
                {
                    conflictingBendAllowance |= bendAllowance.HasValue &&
                        bendAllowance.Value != allowanceProbe.BendAllowance;
                    bendAllowance = allowanceProbe.BendAllowance;
                }
                if (allowanceProbe.ParameterResolution.BendDeduction)
                {
                    conflictingBendDeduction |= bendDeduction.HasValue &&
                        bendDeduction.Value != allowanceProbe.BendDeduction;
                    bendDeduction = allowanceProbe.BendDeduction;
                }
            }

            var reliefProbe = new SheetMetalBaseFlangeModel();
            if (SheetMetalBaseFlangeParser.TryReadUseDefaultBendRelief(
                    raw, sortedFolders[j].TagOffset, reliefProbe))
            {
                conflictingUseDefaultBendRelief |= useDefaultBendRelief.HasValue &&
                    useDefaultBendRelief.Value != reliefProbe.UseDefaultBendRelief;
                useDefaultBendRelief = reliefProbe.UseDefaultBendRelief;
            }
        }
        if (sheetMetalK.HasValue && !conflictingK)
        {
            smData.KFactor = sheetMetalK.Value;
            smData.ParameterResolution.KFactor = true;
        }
        if (bendTableFile != null && !conflictingBendTableFile)
        {
            smData.BendTableFile = bendTableFile;
            smData.ParameterResolution.BendTableFile = true;
        }
        if (allowanceType.HasValue && !conflictingAllowanceType)
        {
            smData.BendAllowanceType = allowanceType.Value;
            smData.ParameterResolution.BendAllowanceType = true;
        }
        if (bendAllowance.HasValue && !conflictingBendAllowance)
        {
            smData.BendAllowance = bendAllowance.Value;
            smData.ParameterResolution.BendAllowance = true;
        }
        if (bendDeduction.HasValue && !conflictingBendDeduction)
        {
            smData.BendDeduction = bendDeduction.Value;
            smData.ParameterResolution.BendDeduction = true;
        }
        if (useDefaultBendRelief.HasValue && !conflictingUseDefaultBendRelief)
        {
            smData.UseDefaultBendRelief = useDefaultBendRelief.Value;
            smData.ParameterResolution.UseDefaultBendRelief = true;
            if (!useDefaultBendRelief.Value)
            {
                smData.AutoReliefType = 0;
                smData.ParameterResolution.AutoReliefType = false;
            }
        }

        smData.SketchName = sketchName;
        smData.SelectedContours = TopologyDecoder.BuildContourReferences(chain, null);
        smData.SelectedRegions = TopologyDecoder.BuildRegionReferences(regions);
        smData.DirectionReference = TopologyDecoder.BuildMfcDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
        smData.BodyScope = TopologyDecoder.BuildMfcBodyScope(chooser, compBodies);

        folder.FeatureData = smData;
    }
}


internal sealed class FeatureFolder : IMfcSerializable
{
    public string ClassName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int TagOffset { get; set; }
    public int DataStart { get; set; }
    public FolderHeader Header { get; set; } = new FolderHeader();
    public IFeatureData? FeatureData { get; set; }
    public IReadOnlyList<FeatureFolder> Children { get; set; } = Array.Empty<FeatureFolder>();
    public IReadOnlyList<IFeatureData> ChildFeatureData { get; set; } = Array.Empty<IFeatureData>();

    internal List<FeatureFolder> ChildrenBuilder = new List<FeatureFolder>();
    internal List<IFeatureData> ChildFeatureDataBuilder = new List<IFeatureData>();

    internal void Freeze()
    {
        Children = ChildrenBuilder.AsReadOnly();
        ChildFeatureData = ChildFeatureDataBuilder.AsReadOnly();
    }

    public void Serialize(MfcArchive ar)
    {
        string? cls = ClassName;
        ar.CString(ref cls);
        ClassName = cls ?? string.Empty;

        string? name = DisplayName;
        ar.WChar(ref name);
        DisplayName = name ?? string.Empty;

        Header.Serialize(ar);

        bool hasData = FeatureData is IMfcSerializable;
        ar.Boolean(ref hasData);
        if (hasData)
        {
            if (ar.IsStoring)
            {
                string? featCls = FeatureData!.ClassName;
                ar.CString(ref featCls);
                ((IMfcSerializable)FeatureData).Serialize(ar);
            }
            else
            {
                string? featCls = null;
                ar.CString(ref featCls);
                var created = CreateFeatureModel(featCls);
                if (created != null)
                {
                    created.Serialize(ar);
                    FeatureData = (IFeatureData)created;
                }
            }
        }

        if (ar.IsStoring)
        {
            ushort childCount = (ushort)Children.Count;
            ar.U16(ref childCount);
            foreach (var child in Children)
            {
                child.Serialize(ar);
            }
        }
        else
        {
            ushort childCount = 0;
            ar.U16(ref childCount);
            ChildrenBuilder.Clear();
            for (int i = 0; i < childCount; i++)
            {
                var child = new FeatureFolder();
                child.Serialize(ar);
                ChildrenBuilder.Add(child);
            }
            Children = ChildrenBuilder.AsReadOnly();
        }
    }

    internal static IMfcSerializable? CreateFeatureModel(string? className) => className switch
    {
        "moExtrusion_c" or "moExtruThin_c" => new ExtrudeFeatureModel(),
        "moRevolution_c" or "moRevCut_c" or "moRevolutionThin_c" => new RevolveFeatureModel(),
        "moFillet_c" or "VarFillet_c" or "Fillet_c" => new FilletFeatureModel(),
        "moChamfer_c" or "Chamfer_c" => new ChamferFeatureModel(),
        "moSketch_c" or "sgSketch" or "moProfileFeature_c" => new SketchFeatureModel(),
        "moSheetMetal_c" or "moSMBaseFlange_c" => new SheetMetalBaseFlangeModel(),
        "moHoleWzd_c" => new HoleWizardFeatureModel(),
        _ => null
    };

    /// <summary>
    /// Maps this internal MFC folder node to a public domain <see cref="Feature"/> object.
    /// </summary>
    public Feature ToFeature() => SwFile.MapFolder(this);
}

internal sealed class FolderHeader : IMfcSerializable
{
    public double Timestamp { get; set; }
    public uint TypeId { get; set; }
    public uint SizeIndicator { get; set; }
    public uint MarkerE { get; set; }
    public double ExtraValue { get; set; }
    public uint ChildFlag { get; set; }
    public ulong Magic { get; set; } = PartMagic;
    public uint SubType { get; set; }
    public uint VersionFlag { get; set; }
    public uint HasData { get; set; }
    public bool IsValid { get; internal set; }

    internal byte[] Reserved1 = new byte[32];
    internal byte[] ZeroPad1 = new byte[8];
    internal byte[] Pattern = new byte[4];
    internal byte[] Pad2 = new byte[12];
    internal byte[] Pad3 = new byte[8];

    public const ulong PartMagic = 0xBA83AF6301CB5678;
    public const ulong AssemblyMagic = 0xB92E01B401CB5678;
    public const uint KnownMarkerE = 0x00000065;
    public const uint KnownChildFlag = 0x00000002;
    public const uint KnownMarkerFfff = 0x0000FFFF;

    public void Serialize(MfcArchive ar)
    {
        double ts = Timestamp;
        ar.F64(ref ts);
        Timestamp = ts;

        uint tid = TypeId;
        ar.U32(ref tid);
        TypeId = tid;

        uint markerE = MarkerE;
        ar.U32(ref markerE);
        MarkerE = markerE;

        uint sizeInd = SizeIndicator;
        ar.U32(ref sizeInd);
        SizeIndicator = sizeInd;

        double ev = ExtraValue;
        ar.F64(ref ev);
        ExtraValue = ev;

        uint cf = ChildFlag;
        ar.U32(ref cf);
        ChildFlag = cf;

        ulong magic = Magic;
        ar.U64(ref magic);
        Magic = magic;

        uint subType = SubType;
        ar.U32(ref subType);
        SubType = subType;

        uint verFlag = VersionFlag;
        ar.U32(ref verFlag);
        VersionFlag = verFlag;

        uint hasData = HasData;
        ar.U32(ref hasData);
        HasData = hasData;

        IsValid = Magic != 0;
    }
}
