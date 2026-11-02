using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary.Topology;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Maps a generic MfcDocument tree to the typed ResolvedFeaturesData model.
/// Replaces the tag filtering and tree-building logic previously in
/// ResolvedFeaturesStreamReader.Parse.
/// </summary>
internal static class ResolvedFeaturesSchemaMapper
{
    public static void Map(MfcDocument doc, ResolvedFeaturesData model)
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

        // Synthesize ExtrudeFeatureDefinitionData and SheetMetalBaseFlangeDefinitionData
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
        // Build a sorted list of (tagOffset, folder) for efficient lookup
        var sortedFolders = new List<KeyValuePair<int, FeatureFolder>>(folderByTagOffset);
        sortedFolders.Sort((a, b) => a.Key.CompareTo(b.Key));

        foreach (var elem in doc.AllElements)
        {
            if (elem.TagByte != 0x01 || elem.ClassName.Length == 0 || IsFolderClass(elem.ClassName))
                continue;

            if (FeatureDataRegistry.TryParse(elem, out var childData) && childData != null)
            {
                // Find the folder with the largest TagOffset that is still less
                // than this element's TagOffset (nearest preceding folder).
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

    /// <summary>
    /// Expected tree depth for known folder/feature classes.
    /// Matches the Python reference parser's _depth_from_name() function.
    /// </summary>
    private static int GetExpectedDepth(string className)
    {
        // Depth 0: Top-level folders and features in FeatureManager design tree
        if (className.StartsWith("moFavoriteFolder_") ||
            className.StartsWith("moHistoryFolder_") ||
            className.StartsWith("moSelectionSetFolder_") ||
            className.StartsWith("moSensorFolder_") ||
            className.StartsWith("moDocsFolder_") ||
            className.StartsWith("moDetailCabinet_") ||
            className.StartsWith("moSolidBodyFolder_") ||
            className.StartsWith("moSurfaceBodyFolder_") ||
            className.StartsWith("moCommentsFolder_") ||
            className.StartsWith("moEqnFolder_") ||
            className.StartsWith("moMaterialFolder_") ||
            className.StartsWith("moRefPlane_") ||
            className.StartsWith("moOriginProfileFeature_") ||
            className.StartsWith("moProfileFeature_") ||
            className.StartsWith("moExtrusion_") ||
            className.StartsWith("moICE_") ||
            className.StartsWith("moRevolution_") ||
            className.StartsWith("Fillet_") ||
            className.StartsWith("moChamfer_") ||
            className.StartsWith("moSweep_") ||
            className.StartsWith("moLoft_") ||
            className.StartsWith("moThicken_") ||
            className.StartsWith("moTemplateSheetMetal_") ||
            className.StartsWith("moSMBaseFlange_") ||
            className.StartsWith("moSheetMetal_") ||
            className.StartsWith("moTemplateFlatPattern_") ||
            className.StartsWith("moFlatPattern_") ||
            className.StartsWith("moNotesAreaFtrFolder_") ||
            className.StartsWith("moInkMarkupFolder_") ||
            className.StartsWith("moConfigTableFolder_") ||
            className.StartsWith("moNativeConfigurationTableFeature_") ||
            className.StartsWith("moMateGroup_") ||
            className.StartsWith("moFtrFolder_"))
            return 0;

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
    internal static FolderHeader ParseFolderHeader(MfcElement element)
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

        // Advance offset to after the header
        if (magicOffset > 0 && magicOffset + 20 <= dataEnd)
            offset = magicOffset + 20;
        else
            offset = Math.Min(savedOffset + 128, dataEnd);

        return header;
    }

    private static readonly HashSet<string> ModernFolderClasses = new(StringComparer.Ordinal)
    {
        "moFavoriteFolder_c",
        "moHistoryFolder_c",
        "moSelectionSetFolder_c",
        "moSensorFolder_c",
        "moDocsFolder_c",
        "moDetailCabinet_c",
        "moNotesAreaFtrFolder_c",
        "moFtrFolder_c",
        "moInkMarkupFolder_c",
        "moSolidBodyFolder_c",
        "moSurfaceBodyFolder_c",
        "moCommentsFolder_c",
        "moEqnFolder_c",
        "moMaterialFolder_c",
        "moConfigTableFolder_c",
        "moFoldUpDownSketchFolder_c",
        "moRefPlane_c",
        "moOriginProfileFeature_c",
        "moNativeConfigurationTableFeature_c",
        "moProfileFeature_c",
        "moExtrusion_c",
        "moICE_c",
        "moRevolution_c",
        "Fillet_c",
        "moChamfer_c",
        "moSweep_c",
        "moLoft_c",
        "moThicken_c",
        "moTemplateSheetMetal_c",
        "moSMBaseFlange_c",
        "moTemplateFlatPattern_c",
        "moSheetMetal_c",
        "moFlatPattern_c",
        "moMateGroup_c"
    };

    private static bool IsFolderClass(string className)
    {
        return className.EndsWith("_ct") || className.EndsWith("_cM") || ModernFolderClasses.Contains(className);
    }

    private static readonly byte[] ParamSignature = new byte[] {
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x40,
        0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFE, 0xFF,
        0x00, 0x00, 0x00
    };

    private static double FindFirstParameterValue(byte[] raw, int start, int end)
    {
        if (raw == null || raw.Length < ParamSignature.Length + 8) return 0;
        int max = Math.Min(end, raw.Length) - (ParamSignature.Length + 8);
        for (int i = start; i <= max; i++)
        {
            if (raw[i] != 0x00) continue;
            bool match = true;
            for (int j = 1; j < ParamSignature.Length; j++)
            {
                if (raw[i + j] != ParamSignature[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                double val = BitConverter.ToDouble(raw, i + ParamSignature.Length);
                if (val > 0)
                    return val;
            }
        }
        return 0;
    }

    private static void SynthesizeFeatureDefinitions(MfcDocument doc, Dictionary<int, FeatureFolder> folderByTagOffset)
    {
        var raw = doc.SourceData;
        var sortedFolders = folderByTagOffset.Values.OrderBy(f => f.TagOffset).ToList();

        for (int i = 0; i < sortedFolders.Count; i++)
        {
            var folder = sortedFolders[i];
            int start = folder.TagOffset;
            int end = (i + 1 < sortedFolders.Count) ? sortedFolders[i + 1].TagOffset : raw.Length;

            // Find parent or preceding sketch folder for contour/region selection
            var parentSketchFolder = sortedFolders.Take(i).LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
            string? sketchName = parentSketchFolder?.DisplayName;
            var chain = parentSketchFolder?.ChildFeatureDataBuilder.OfType<SketchChainData>().FirstOrDefault()
                ?? parentSketchFolder?.ChildFeatureData.OfType<SketchChainData>().FirstOrDefault();
            var regions = (parentSketchFolder?.ChildFeatureDataBuilder.OfType<SketchRegionData>() ?? Enumerable.Empty<SketchRegionData>())
                .Concat(parentSketchFolder?.ChildFeatureData.OfType<SketchRegionData>() ?? Enumerable.Empty<SketchRegionData>());

            // Topology / selection sub-objects within the feature folder
            var fromSkt = folder.ChildFeatureDataBuilder.OfType<FromSktEntSurfIdRepData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<FromSktEntSurfIdRepData>().FirstOrDefault();
            var fromSkt3Int = folder.ChildFeatureDataBuilder.OfType<FromSktEnt3IntSurfIdRepData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<FromSktEnt3IntSurfIdRepData>().FirstOrDefault();
            var faceRef = folder.ChildFeatureDataBuilder.OfType<FaceRefData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<FaceRefData>().FirstOrDefault();
            var endFace = folder.ChildFeatureDataBuilder.OfType<EndFaceSurfIdRepData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<EndFaceSurfIdRepData>().FirstOrDefault();
            var endFace3Int = folder.ChildFeatureDataBuilder.OfType<EndFace3IntSurfIdRepData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<EndFace3IntSurfIdRepData>().FirstOrDefault();
            var compFace = folder.ChildFeatureDataBuilder.OfType<CompFaceData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<CompFaceData>().FirstOrDefault();
            var edgeRef = folder.ChildFeatureDataBuilder.OfType<EdgeRefData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<EdgeRefData>().FirstOrDefault();
            var compEdge = folder.ChildFeatureDataBuilder.OfType<CompEdgeData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<CompEdgeData>().FirstOrDefault();
            var compRefPlane = folder.ChildFeatureDataBuilder.OfType<CompRefPlaneData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<CompRefPlaneData>().FirstOrDefault();
            var faceRefPln = folder.ChildFeatureDataBuilder.OfType<FaceRefPlnData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<FaceRefPlnData>().FirstOrDefault();
            var chooser = folder.ChildFeatureDataBuilder.OfType<PerBodyChooserData>().FirstOrDefault()
                ?? folder.ChildFeatureData.OfType<PerBodyChooserData>().FirstOrDefault();
            var compBodies = folder.ChildFeatureDataBuilder.OfType<CompSolidBodyData>()
                .Concat(folder.ChildFeatureData.OfType<CompSolidBodyData>());

            if ((folder.ClassName == "moExtrusion_c" || folder.ClassName == "moExtrusion_ct" || folder.ClassName == "moICE_c") && folder.FeatureData == null)
            {
                var extData = new ExtrudeFeatureDefinitionData();
                extData.IsCut = folder.ClassName == "moICE_c" || folder.DisplayName.Contains("切除") || folder.DisplayName.IndexOf("cut", StringComparison.OrdinalIgnoreCase) >= 0;

                var endSpec = folder.ChildFeatureDataBuilder.OfType<EndSpecData>().FirstOrDefault() ?? folder.ChildFeatureData.OfType<EndSpecData>().FirstOrDefault();
                if (endSpec != null)
                {
                    extData.EndCondition = endSpec.EndCondition;
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

                // Selection & Topology binding
                extData.SketchName = sketchName;
                extData.SelectedContours = TopologyDecoder.BuildContourReferences(chain, fromSkt, fromSkt3Int);
                extData.SelectedRegions = TopologyDecoder.BuildRegionReferences(regions);
                extData.DirectionReference = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
                extData.EndConditionEntity = TopologyDecoder.BuildEndConditionEntity(faceRef, endFace, compFace, endFace3Int);
                extData.BodyScope = TopologyDecoder.BuildBodyScope(chooser, compBodies);
                if (extData.BodyScope != null)
                {
                    extData.AutoSelect = extData.BodyScope.AutoSelect;
                    extData.FeatureScope = !extData.BodyScope.AutoSelect;
                }
                extData.BothDirections = extData.EndCondition == 6;
                extData.IsBaseExtrude = !sortedFolders.Take(i).Any(f => f.ClassName == "moExtrusion_c" || f.ClassName == "moSMBaseFlange_c");

                folder.FeatureData = extData;
            }
            else if (folder.ClassName == "moSMBaseFlange_c" && folder.FeatureData == null)
            {
                var smData = new SheetMetalBaseFlangeDefinitionData();
                smData.Depth = FindFirstParameterValue(raw, start, end);

                var threeDRad = doc.AllElements.FirstOrDefault(e => e.ClassName == "ThreeDRadiusDim_c");
                if (threeDRad != null && threeDRad.TagOffset + 0x3D + 8 <= raw.Length)
                {
                    double rad = BitConverter.ToDouble(raw, threeDRad.TagOffset + 0x3D);
                    if (rad > 0) smData.BendRadius = rad;
                }

                var ppDim = doc.AllElements.FirstOrDefault(e => e.ClassName == "ParallelPlaneDistanceDim_c" && e.TagOffset > (threeDRad?.TagOffset ?? 0));
                if (ppDim != null && ppDim.TagOffset + 0x1A3 + 8 <= raw.Length)
                {
                    double thick = BitConverter.ToDouble(raw, ppDim.TagOffset + 0x1A3);
                    if (thick > 0) smData.Thickness = thick;
                }

                if (smData.Thickness <= 0) smData.Thickness = 0.001;
                if (smData.BendRadius <= 0) smData.BendRadius = 0.002;

                // Selection & Topology binding
                smData.SketchName = sketchName;
                smData.SelectedContours = TopologyDecoder.BuildContourReferences(chain, fromSkt);
                smData.SelectedRegions = TopologyDecoder.BuildRegionReferences(regions);
                smData.DirectionReference = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
                smData.BodyScope = TopologyDecoder.BuildBodyScope(chooser, compBodies);

                folder.FeatureData = smData;
            }
        }
    }
}
