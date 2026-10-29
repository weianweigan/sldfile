using System;
using System.Collections.Generic;
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
}
