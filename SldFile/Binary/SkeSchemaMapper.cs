using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Maps a generic <see cref="MfcDocument"/> tree to the typed <see cref="SkeFileData"/> model.
/// Uses <see cref="FeatureDataRegistry"/> to parse each recognized named element.
/// Raw segment data between named elements is preserved in the SketchData.SketchBody region
/// and the parent element's RawData.
///
/// Tree structure:
///   sgSketch (root)
///     ├── [raw segment blocks — preserved in SketchData.SketchBody]
///     ├── sgLineHandle, sgPointHandle (handles)
///     ├── sgPntLineDist, sgLLDist, sgAnglDim, sgPntPntDist (dimension types)
///     ├── moLengthParameter_c, moAngleParameter_c (parameters)
///     ├── moNodeName_c (dimension names)
///     ├── suObList (list terminator)
///     ├── moDisplayDistanceDim_c, moDisplayAngularDim_c (display dims)
///     └── moSkDimHandleValG2_c (dimension handle values)
/// </summary>
internal static class SkeSchemaMapper
{
    /// <summary>
    /// MFC class names recognized in .ske files (whitelist for tree filtering).
    /// </summary>
    private static readonly HashSet<string> KnownSkeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "sgSketch", "sgLineHandle", "sgPointHandle",
        "sgPntLineDist", "sgPntPntDist", "sgLLDist", "sgAnglDim",
        "moLengthParameter_c", "moAngleParameter_c",
        "moNodeName_c",
        "moDisplayDistanceDim_c", "moDisplayAngularDim_c",
        "moSkDimHandleValG2_c", "suObList",
    };

    public static void Map(MfcDocument doc, SkeFileData model)
    {
        model.RawData = doc.SourceData;
        var data = doc.SourceData;

        // Header u32 at offset 0
        if (data.Length >= 4)
            model.HeaderU32 = BitConverter.ToUInt32(data, 0);

        var featureDataList = new List<IFeatureData>();

        // Walk all elements from the tree, filtering to known .ske class names
        foreach (var elem in doc.AllElements)
        {
            if (!KnownSkeNames.Contains(elem.ClassName))
                continue;

            if (FeatureDataRegistry.TryParse(elem, out var fd) && fd != null)
            {
                featureDataList.Add(fd);

                // Extract FeatureCount from sgSketch
                if (fd is SketchData sk)
                    model.FeatureCount = sk.EntityCount;
            }
        }

        model.FeatureData = featureDataList.AsReadOnly();
    }
}
