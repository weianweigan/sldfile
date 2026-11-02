using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary.FeatureData;
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
            className.StartsWith("moExtruRefSurface_") ||
            className.StartsWith("moRevolvRefSurf_") ||
            className.StartsWith("moPlanarSurface_") ||
            className.StartsWith("moSewRefSurface_") ||
            className.StartsWith("moExtendRefSurface_") ||
            className.StartsWith("moOffsetRefSurface_") ||
            className.StartsWith("moFillRefSurface_") ||
            className.StartsWith("moSweepRefSurface_") ||
            className.StartsWith("moBlendRefSurface_") ||
            className.StartsWith("moWeldMemberFeat_") ||
            className.StartsWith("moEndCap_") ||
            className.StartsWith("moCosmeticWeldCutList_") ||
            className.StartsWith("moICE_") ||
            className.StartsWith("moRevolution_") ||
            className.StartsWith("Fillet_") ||
            className.StartsWith("Chamfer_") ||
            className.StartsWith("moChamfer_") ||
            className.StartsWith("moLPattern_") ||
            className.StartsWith("moCirPattern_") ||
            className.StartsWith("moSweep_") ||
            className.StartsWith("moLoft_") ||
            className.StartsWith("moBlend_") ||
            className.StartsWith("moShell_") ||
            className.StartsWith("moDraft_") ||
            className.StartsWith("moRib_") ||
            className.StartsWith("moGusset_") ||
            className.StartsWith("moCombineBodies_") ||
            className.StartsWith("moSplit_") ||
            className.StartsWith("moTrimRefSurface_") ||
            className.StartsWith("moSplitBody_") ||
            className.StartsWith("moDeleteBody_") ||
            className.StartsWith("moThicken_") ||
            className.StartsWith("moTemplateSheetMetal_") ||
            className.StartsWith("moSMBaseFlange_") ||
            // Task 2.2 catalog/TODO candidates; recognition only, pending samples.
            className.StartsWith("moEdgeFlange_") ||
            className.StartsWith("moMiterFlange_") ||
            // Task 2.3 / 2.4 catalog candidates; recognition only, pending samples.
            className.StartsWith("moHem_") ||
            className.StartsWith("moJog_") ||
            className.StartsWith("moLoftedBend_") ||
            className.StartsWith("moSMNormalCut_") ||
            className.StartsWith("moFold_") ||
            className.StartsWith("moUnFold_") ||
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

        if (className.StartsWith("moCosmeticWeldSubFolder_"))
            return 1;
        if (className.StartsWith("moCosmeticWeldBead_"))
            return 2;

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
        "moExtruRefSurface_c",
        "moRevolvRefSurf_c",
        "moPlanarSurface_c",
        "moSewRefSurface_c",
        "moExtendRefSurface_c",
        "moOffsetRefSurface_c",
        "moFillRefSurface_c",
        // Measured 2026 surface sweep/loft classes.
        "moSweepRefSurface_c",
        "moBlendRefSurface_c",
        // Measured 2026 structural-member class.
        "moWeldMemberFeat_c",
        "moEndCap_c",
        "moCosmeticWeldCutList_c",
        "moCosmeticWeldSubFolder_c",
        "moCosmeticWeldBead_c",
        "moICE_c",
        // Measured thin extrusion class (ExtrudeOptions thin samples, TypeId 0x52).
        "moExtruThin_c",
        "moRevolution_c",
        // Measured thin/cut revolve classes (RevolveOptions 2026 controlled samples).
        "moRevolutionThin_c",
        "moRevCut_c",
        "Fillet_c",
        // Measured variable-radius fillet class (SW2026 FilletOptions variable sample).
        // FolderHeader TypeId is also 0x52 (same numeric value as Chamfer_c); the class
        // name disambiguates the two layouts.
        "VarFillet_c",
        "moChamfer_c",
        // Measured serialized class for chamfer features in the 2026 ChamferOptions
        // samples (the catalog's moChamfer_c was not present in any sample).
        "Chamfer_c",
        // Measured serialized classes for pattern features in the 2026 PatternOptions samples.
        "moLPattern_c",
        "moCirPattern_c",
        "moMirrorPattern_c",
        // Measured serialized class for the 2026 Hole Wizard samples (legacy 0x54 / tapped 0x52).
        "moHoleWzd_c",
        "moSweep_c",
        "moLoft_c",
        // Observed serialized class for loft features in the 2026 LoftOptions
        // samples (the catalog's moLoft_c was not present in any sample).
        "moBlend_c",
        // Phase 3 feature classes: recognized as design-tree feature nodes only.
        // No payload parser is registered; FeatureData stays null until controlled
        // samples establish their layouts. Class names follow the catalog/TODO
        // lists; TypeIds are deliberately not encoded here (sources disagree).
        "moShell_c",
        "moDraft_c",
        "moRib_c",
        // Legacy tutorial sample classes (SW2000-SW2026 tutorial models).
        "moGusset_c",
        "moSplit_c",
        "moTrimRefSurface_c",
        "moCombineBodies_c",
        "moSplitBody_c",
        "moDeleteBody_c",
        "moThicken_c",
        "moTemplateSheetMetal_c",
        "moSMBaseFlange_c",
        // Task 2.2 catalog/TODO candidates. Recognized as design-tree nodes only;
        // payload parsers return null until controlled samples confirm the class
        // names and option layouts. TypeId candidates 0x63/0x64 are not encoded.
        "moEdgeFlange_c",
        "moMiterFlange_c",
        // Task 2.3 / 2.4 catalog candidates. Recognition only; payload parsers
        // return null until controlled samples confirm class names and layouts.
        // TypeId candidates 0x65/0x66/0x69/0x67/0x62/0x6D/0x6E are not encoded.
        "moHem_c",
        "moJog_c",
        "moLoftedBend_c",
        "moSMNormalCut_c",
        "moFold_c",
        "moUnFold_c",
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
            // Structural parent-sketch resolution: the profile payload stores the
            // sketch persistent id (displayNameEnd+8), verified against the root
            // moCompFeature_c reference list. The fromSkt object itself stores a
            // null parent reference, so this node is the only structural source.
            // Fallback: the MFC profile node's own display name (never its id) when
            // the payload layout is not established.
            var parentSketchElement = parentSketchFolder == null
                ? null
                : doc.AllElements.FirstOrDefault(e => e.TagOffset == parentSketchFolder.TagOffset);
            var parentSketch = ParentSketchReferenceParser.TryResolve(doc, parentSketchElement);
            string? sketchName = parentSketch?.DisplayName
                ?? (string.IsNullOrEmpty(parentSketchFolder?.DisplayName) ? null : parentSketchFolder!.DisplayName);
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

            if (LegacyTutorialFeatureParsers.IsSupported(folder.ClassName) && folder.FeatureData == null)
            {
                // Legacy tutorial samples (stream versions 0x006D-0x0334) store
                // Rib/Gusset/Combine/Split/SurfaceTrim without the modern payload
                // markers. The measured parsers validate the FolderHeader and only
                // promote fields with an exact anchor across controlled variants.
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? legacyData))
                {
                    folder.FeatureData = legacyData;
                }
            }
            else if (folder.ClassName == LegacyStructuralMemberParser.ClassName && folder.FeatureData == null)
            {
                // Legacy tutorial structural member: the modern 0x4D parser rejects
                // these FolderHeader TypeIds. Read only profile path and opaque group
                // count; corner treatment / rotation stay unresolved.
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? legacyStructuralData))
                {
                    folder.FeatureData = legacyStructuralData;
                }
            }
            else if (folder.ClassName == LegacySweepLoftFeatureParsers.SweepClassName && folder.FeatureData == null)
            {
                // Legacy tutorial sweep: modern selection decoder rejected the layout.
                // Only measured option slots are promoted; absent/unknown values stay null.
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? legacySweepData))
                {
                    folder.FeatureData = legacySweepData;
                }
            }
            else if (folder.ClassName == ExtrudeThinFeatureParser.ClassName && folder.FeatureData == null)
            {
                // Measured thin extrusion class. Unknown/conflicting thin layouts stay unresolved
                // (FeatureData null) instead of promoting approximate values.
                ushort thinVersion = raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0;
                if (ExtrudeThinFeatureParser.TryParse(doc, folder, thinVersion, start, end, out var thinData) &&
                    thinData != null)
                {
                    thinData.SketchName = sketchName;
                    thinData.AvailableSketchContours = TopologyDecoder.BuildContourReferences(chain, fromSkt, fromSkt3Int, includeCandidates: true);
                    thinData.AvailableSketchRegions = TopologyDecoder.BuildRegionReferences(regions);
                    thinData.DirectionReference = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
                    thinData.BodyScope = TopologyDecoder.BuildBodyScope(chooser, compBodies);
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
            else if ((folder.ClassName == "moExtrusion_c" || folder.ClassName == "moExtrusion_ct" || folder.ClassName == "moICE_c") && folder.FeatureData == null)
            {
                var extData = new ExtrudeFeatureDefinitionData();
                extData.IsCut = folder.ClassName == "moICE_c" || folder.DisplayName.Contains("切除") || folder.DisplayName.IndexOf("cut", StringComparison.OrdinalIgnoreCase) >= 0;

                var endSpec = folder.ChildFeatureDataBuilder.OfType<EndSpecData>().FirstOrDefault() ?? folder.ChildFeatureData.OfType<EndSpecData>().FirstOrDefault();
                if (endSpec != null)
                {
                    extData.EndCondition = endSpec.EndCondition;
                    var reverse = ExtrudeOptionParser.ReadReverseDirection(
                        raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0, folder.ClassName, endSpec.RawData);
                    if (reverse.HasValue)
                    {
                        extData.ReverseDirection = reverse.Value;
                        extData.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.ReverseDirection));
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

                // UpToSurface structural end-condition selection (moICE_c specimen):
                // the terminating entity is moSingleFaceRef_w, a 473-byte single-face
                // persistent reference with its surface mappings. The base body's
                // moFaceRef_c/moCompFace_c records describe generated faces and are
                // never promoted to an end target.
                //
                // The moFromEndSpec_c payload serialized before the feature tag is
                // shared with blind cuts: its word at +260 is 4 for both the blind
                // extrude_cut_5mm and the UpToSurface specimen, so it is not an
                // end-condition discriminator and is deliberately not read here.
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
                        // Native UpToSurface specimen: endConditionForward = 4 and
                        // endConditionEntities[0] = Face. Blind/through-all cuts have
                        // no moSingleFaceRef_w and keep their decoded/default extent.
                        extData.EndCondition = 4;
                        extData.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.EndCondition));
                        extData.EndConditionEntity = new EndConditionEntityReference
                        {
                            EntityType = SelectionEntityType.Face,
                            EntityId = singleFace.FaceId,
                            FaceCount = 1,
                            SurfaceName = $"Face_0x{singleFace.FaceId:X}"
                        };
                        extData.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.EndConditionEntity));
                        structuralEndFace = true;
                    }

                    // Explicit body scope: only the 327-byte moCompSolidBody_c record
                    // with the 0xFFFFFFFF owner class word is an explicitly selected
                    // scope body (UpToSurface native: featureScope=true, autoSelect=false,
                    // featureScopeBodiesCount=1). Blind/through-all cuts carry the same
                    // 327-byte record with the creating feature class (0x4F) and remain
                    // on the auto-select path.
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
                        extData.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.BodyScope));
                        extData.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.AutoSelect));
                        extData.MarkResolvedInternal(nameof(ExtrudeFeatureDefinitionData.FeatureScope));
                        structuralBodyScope = true;
                    }
                }

                // Selection & Topology binding
                extData.SketchName = sketchName;
                // Candidacy fallbacks (moFromSktEntSurfIdRep_c byte-pattern scans) are
                // flagged IsCandidateOnly and never become selected references.
                extData.AvailableSketchContours = TopologyDecoder.BuildContourReferences(chain, fromSkt, fromSkt3Int, includeCandidates: true);
                extData.AvailableSketchRegions = TopologyDecoder.BuildRegionReferences(regions);
                // Controlled whole/contour/region samples establish this discriminator
                // for 0x70 explicit selections and 0x70/0x71 whole-sketch moExtrusion_c,
                // with a single preceding candidate sketch (0x71 whole has two bodies).
                // A recognized chooser envelope decodes the selected persistent
                // contours/regions; an unrecognized chooser stays Unknown.
                ushort selectionHeader = raw.Length >= 2 ? BitConverter.ToUInt16(raw, 0) : (ushort)0;
                if ((selectionHeader == 0x70 || selectionHeader == 0x71) &&
                    folder.ClassName == "moExtrusion_c" && parentSketchFolder != null &&
                    sortedFolders.Take(i).Count(f => f.ClassName.StartsWith("moProfileFeature_")) == 1)
                {
                    var selection = SketchSelectionParser.Read(doc, parentSketchFolder.TagOffset, start);
                    if (selection.Kind == SketchSelectionEnvelopeKind.None)
                    {
                        extData.SketchSelectionMode = SketchSelectionMode.WholeSketch;
                    }
                    else if (selection.Kind == SketchSelectionEnvelopeKind.Contour)
                    {
                        // Recognized contour envelope (0x70 single, 0x71 multi-contour
                        // specimen select_two_contours).
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
                extData.DirectionReference = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
                // Blind and mid-plane extents do not select a terminating entity.
                // Surface mappings also occur for generated faces and must not be
                // interpreted as an explicit end-condition selection. A structurally
                // decoded moSingleFaceRef_w takes precedence for moICE_c.
                if (!structuralEndFace)
                {
                    extData.EndConditionEntity = extData.EndCondition == 0 || extData.EndCondition == 6
                        ? null
                        : TopologyDecoder.BuildEndConditionEntity(faceRef, endFace, compFace, endFace3Int);
                }
                if (!structuralBodyScope)
                {
                    extData.BodyScope = TopologyDecoder.BuildBodyScope(chooser, compBodies);
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
                extData.IsBaseExtrude = !sortedFolders.Take(i).Any(f => f.ClassName == "moExtrusion_c" || f.ClassName == "moSMBaseFlange_c");

                folder.FeatureData = extData;
            }
            else if ((folder.ClassName == "moRevolution_c" ||
                      folder.ClassName == "moRevolutionThin_c" ||
                      folder.ClassName == "moRevCut_c") && folder.FeatureData == null)
            {
                folder.FeatureData = RevolveFeatureParser.Parse(doc, folder.ClassName, folder.Header.TypeId, start, end);
            }
            else if (folder.ClassName == FilletChamferFeatureParser.FilletClassName && folder.FeatureData == null)
            {
                // Task 1.3 controlled constant-radius fillet (measured Fillet_c / 0x51).
                // Unknown layouts return null and leave the definition unresolved.
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? filletData))
                {
                    folder.FeatureData = filletData;
                }
            }
            else if (folder.ClassName == FilletChamferFeatureParser.VariableFilletClassName && folder.FeatureData == null)
            {
                // Task 1.3 controlled variable-radius fillet (measured VarFillet_c / 0x52).
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? variableFilletData))
                {
                    folder.FeatureData = variableFilletData;
                }
            }
            else if (folder.ClassName == FilletChamferFeatureParser.ChamferClassName && folder.FeatureData == null)
            {
                // Task 1.3 controlled distance-distance / angle-distance chamfer (measured Chamfer_c / 0x52).
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? chamferData))
                {
                    folder.FeatureData = chamferData;
                }
            }
            else if (folder.ClassName == PatternFeatureParser.LinearPatternClassName && folder.FeatureData == null)
            {
                // Task 1.5 controlled linear pattern (measured moLPattern_c / 0x5D).
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? linearPatternData))
                {
                    folder.FeatureData = linearPatternData;
                }
            }
            else if (folder.ClassName == PatternFeatureParser.CircularPatternClassName && folder.FeatureData == null)
            {
                // Task 1.5 controlled circular pattern (measured moCirPattern_c / 0x63).
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? circularPatternData))
                {
                    folder.FeatureData = circularPatternData;
                }
            }
            else if (folder.ClassName == PatternFeatureParser.MirrorPatternClassName && folder.FeatureData == null)
            {
                // Task 1.5 controlled mirror pattern (measured moMirrorPattern_c / 0x57).
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? mirrorPatternData))
                {
                    folder.FeatureData = mirrorPatternData;
                }
            }
            else if (folder.ClassName == HoleWizardFeatureParser.HoleWizardClassName && folder.FeatureData == null)
            {
                // Task 1.4 controlled Hole Wizard layouts (legacy 0x54 / tapped 0x52).
                if (FeatureDataRegistry.TryParseFeature(
                    doc, folder.ClassName, folder.Header.TypeId, start, end, out IFeatureData? holeWizardData))
                {
                    folder.FeatureData = holeWizardData;
                }
            }
            else if (folder.ClassName == "moSMBaseFlange_c" && folder.FeatureData == null)
            {
                var smData = new SheetMetalBaseFlangeDefinitionData();

                // Dimensions belong to the preceding sheet-metal template,
                // not the first radial/distance dimension in the document.
                // A previous fillet may already have declared those MFC
                // classes, leaving only compact class references here.
                var template = sortedFolders.Take(i).LastOrDefault(f => f.ClassName == "moTemplateSheetMetal_c");
                var previousBase = sortedFolders.Take(i).LastOrDefault(f => f.ClassName == "moSMBaseFlange_c");
                if (template != null && (previousBase == null || template.TagOffset > previousBase.TagOffset))
                    SheetMetalTemplateParser.Read(raw, template.TagOffset, start, smData);

                // Depth is a named D2 parameter inside this feature's own window
                // (feature tag .. next tree folder). Do not use the first scalar
                // in the window: that is the bend-radius parameter.
                SheetMetalBaseFlangeParser.Read(raw, start, end, smData);

                // K-factor lives in the global moSheetMetal_c parameter block
                // (named D2), not in the base-flange template. Controlled samples
                // sm_l_k03_fixed/sm_l_k04_fixed change it to 0.3/0.4 while the
                // template D2 stays 0.5. Use every global block; conflicting
                // per-body values are left unresolved.
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

                    var allowanceProbe = new SheetMetalBaseFlangeDefinitionData();
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

                    var reliefProbe = new SheetMetalBaseFlangeDefinitionData();
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
                        // Global AutoRelief override: the code before the flange
                        // still describes the default, not the overridden type.
                        smData.AutoReliefType = 0;
                        smData.ParameterResolution.AutoReliefType = false;
                    }
                }

                // Selection & Topology binding
                smData.SketchName = sketchName;
                // Selected contours must come from a structurally decoded chain;
                // moFromSktEntSurfIdRep_c byte-pattern scans are candidate-only.
                // In the controlled base-flange samples the only such records are
                // nested under moFaceRef_c for flat-pattern faces, not a profile
                // selection, so they are not promoted here.
                smData.SelectedContours = TopologyDecoder.BuildContourReferences(chain, null);
                smData.SelectedRegions = TopologyDecoder.BuildRegionReferences(regions);
                smData.DirectionReference = TopologyDecoder.BuildDirectionReference(edgeRef, compEdge, compRefPlane, faceRefPln);
                smData.BodyScope = TopologyDecoder.BuildBodyScope(chooser, compBodies);

                folder.FeatureData = smData;
            }
            else if (folder.ClassName == "moEdgeFlange_c" && folder.FeatureData == null)
            {
                // First single-variable edge-flange specimen: the selection block
                // (moEdgeRef_c / moCompEdge_c) precedes the feature tag, while the
                // D1/D4 dimension records follow it up to the next top-level folder.
                int windowStart = i > 0 ? sortedFolders[i - 1].TagOffset : 0;
                var edgeFlangeElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (edgeFlangeElement != null)
                {
                    folder.FeatureData = FlangeFeatureParsers.ParseEdgeFlange(
                        doc, edgeFlangeElement, windowStart, end);
                }
            }
            else if (folder.ClassName == SheetMetalFormingParsers.HemClassName && folder.FeatureData == null)
            {
                // Controlled open/closed hem samples (version 0x73, TypeId 0x67).
                // The selection block follows the feature folder; dimensions and
                // the anonymous type flag run up to the next tree folder.
                var hemElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (hemElement != null)
                {
                    folder.FeatureData = SheetMetalFormingParsers.ParseHem(
                        doc, hemElement, end);
                }
            }
            else if (folder.ClassName == SheetMetalFormingParsers.JogClassName && folder.FeatureData == null)
            {
                // Tutorial jog sample (Complex Sheet Metal Part, version 0x1AA).
                var jogElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (jogElement != null)
                    folder.FeatureData = SheetMetalFormingParsers.ParseJog(doc, jogElement, end);
            }
            else if (folder.ClassName == SheetMetalFormingParsers.FoldClassName && folder.FeatureData == null)
            {
                // Tutorial fold samples (Paper Airplane 0x155, drive sled 0x1AA).
                var foldElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (foldElement != null)
                    folder.FeatureData = SheetMetalFormingParsers.ParseFold(doc, foldElement, end);
            }
            else if (folder.ClassName == SheetMetalFormingParsers.UnFoldClassName && folder.FeatureData == null)
            {
                var unfoldElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (unfoldElement != null)
                    folder.FeatureData = SheetMetalFormingParsers.ParseUnFold(doc, unfoldElement, end);
            }
            else if (folder.ClassName == SheetMetalFormingParsers.LoftedBendClassName && folder.FeatureData == null)
            {
                // First controlled non-formed LoftedBend sample (version 0x74,
                // TypeId 0x72). Only Thickness and NumberOfBendLines are proven.
                var loftedBendElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (loftedBendElement != null)
                {
                    folder.FeatureData = SheetMetalFormingParsers.ParseLoftedBend(
                        doc, loftedBendElement, end);
                }
            }
            else if (folder.ClassName == SheetMetalFormingParsers.FlatPatternClassName && folder.FeatureData == null)
            {
                // Existing sheet-metal samples contain the native flat pattern
                // (TypeId 0x5D in sm_base_flange_*, 0x5E in SheetMetalOptions).
                // Only the persistent fixed-face selection is structurally proven.
                var flatPatternElement = doc.AllElements
                    .FirstOrDefault(e => e.TagOffset == folder.TagOffset);
                if (flatPatternElement != null)
                {
                    // The face selection follows the fold sketch folder, so the
                    // flat-pattern search window runs to the end of the stream.
                    folder.FeatureData = SheetMetalFormingParsers.ParseFlatPattern(
                        doc, flatPatternElement, raw.Length);
                }
            }
        }
    }
}
