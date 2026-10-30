using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile.Binary;
using SldFile.Binary.FeatureData;

namespace SldFile;

public class SldPart : SwFile
{
    public SldPart(string filePath, bool readOnly = true) : base(filePath, readOnly)
    {
    }

    /// <summary>
    /// Adds an extrusion (Boss or Cut) feature based on a sketch, updating the ResolvedFeatures stream.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddExtrude(ExtrudeFeatureDefinitionData definition, string? targetSketchName = null, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (double.IsNaN(definition.Depth) || double.IsInfinity(definition.Depth) || definition.Depth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition.Depth),
                $"Extrusion depth must be a positive finite number, but received {definition.Depth}.");
        }

        if (definition.BothDirections && (double.IsNaN(definition.DepthDirection2) || double.IsInfinity(definition.DepthDirection2) || definition.DepthDirection2 <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(definition.DepthDirection2),
                $"Direction 2 extrusion depth must be a positive finite number, but received {definition.DepthDirection2}.");
        }

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        // Resolve target sketch folder
        FeatureFolder? targetSketchFolder = null;
        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        var streamSketches = FindSketchesInStream(currentStream);
        int sketchEndOffset = currentStream.Length;

        if (!string.IsNullOrWhiteSpace(targetSketchName))
        {
            targetSketchFolder = allFolders.FirstOrDefault(f =>
                f.ClassName.StartsWith("moProfileFeature_") &&
                string.Equals(f.DisplayName, targetSketchName, StringComparison.OrdinalIgnoreCase));

            if (targetSketchFolder == null)
            {
                var match = streamSketches.FirstOrDefault(s => string.Equals(s.Name, targetSketchName, StringComparison.OrdinalIgnoreCase));
                if (match.Name != null)
                {
                    targetSketchFolder = new FeatureFolder
                    {
                        ClassName = "moProfileFeature_c",
                        DisplayName = match.Name,
                        TagOffset = match.TagOffset
                    };
                    sketchEndOffset = match.EndOffset;
                }
                else
                {
                    throw new ArgumentException($"Target sketch '{targetSketchName}' was not found in the document.", nameof(targetSketchName));
                }
            }
        }
        else
        {
            if (streamSketches.Count > 0)
            {
                var lastStreamSketch = streamSketches[streamSketches.Count - 1];
                var lastFolderSketch = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
                if (lastFolderSketch == null || lastStreamSketch.TagOffset > lastFolderSketch.TagOffset)
                {
                    targetSketchFolder = new FeatureFolder
                    {
                        ClassName = "moProfileFeature_c",
                        DisplayName = lastStreamSketch.Name,
                        TagOffset = lastStreamSketch.TagOffset
                    };
                    sketchEndOffset = lastStreamSketch.EndOffset;
                }
                else
                {
                    targetSketchFolder = lastFolderSketch;
                }
            }
            else
            {
                targetSketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
            }

            if (targetSketchFolder == null)
            {
                throw new InvalidOperationException("No sketch found in document to extrude.");
            }
        }

        // Determine sketch end offset in stream if not already determined
        if (sketchEndOffset == currentStream.Length)
        {
            int sketchIdx = allFolders.IndexOf(targetSketchFolder);
            if (sketchIdx >= 0)
            {
                for (int i = sketchIdx + 1; i < allFolders.Count; i++)
                {
                    if (allFolders[i].TagOffset > targetSketchFolder.TagOffset)
                    {
                        sketchEndOffset = allFolders[i].TagOffset;
                        break;
                    }
                }
            }
        }

        // Determine sequential feature name if not provided
        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            if (definition.IsCut)
            {
                int cutCount = Features.Count(f => f.Kind == FeatureKind.Cut || f.Name.StartsWith("切除-拉伸"));
                resolvedFeatureName = $"切除-拉伸{cutCount + 1}";
            }
            else
            {
                int bossCount = Features.Count(f => (f.Kind == FeatureKind.Extrusion && !f.Name.StartsWith("切除-拉伸")) || f.Name.StartsWith("凸台-拉伸"));
                resolvedFeatureName = $"凸台-拉伸{bossCount + 1}";
            }
        }

        // Build new stream with synthesized feature block
        var newStream = ExtrudeFeatureBuilder.BuildExtrusionStream(
            currentStream, definition, targetSketchFolder, sketchEndOffset, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        var createdFeature = Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, definition.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);

        return createdFeature;
    }

    /// <summary>
    /// Adds an extrusion feature with a specified depth in meters (e.g. 0.015 for 15mm).
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddExtrude(double depth, bool isCut = false, uint endCondition = 0, string? targetSketchName = null, string? featureName = null)
    {
        var def = new ExtrudeFeatureDefinitionData
        {
            Depth = depth,
            IsCut = isCut,
            EndCondition = endCondition
        };
        return AddExtrude(def, targetSketchName, featureName);
    }

    private static List<FeatureFolder> FlattenFolders(IEnumerable<FeatureFolder> folders)
    {
        var list = new List<FeatureFolder>();
        void Walk(IEnumerable<FeatureFolder> current)
        {
            foreach (var f in current)
            {
                list.Add(f);
                if (f.Children.Count > 0)
                    Walk(f.Children);
            }
        }
        Walk(folders);
        return list;
    }

    private static List<(string Name, int TagOffset, int EndOffset)> FindSketchesInStream(byte[] stream)
    {
        var list = new List<(string Name, int TagOffset, int EndOffset)>();
        for (int i = 0; i < stream.Length - 30; i++)
        {
            if (stream[i] == 0x04 && stream[i + 1] == 0x80 && stream[i + 2] == 0xFF && stream[i + 3] == 0xFE && stream[i + 4] == 0xFF)
            {
                byte charCount = stream[i + 5];
                if (charCount > 0 && charCount < 50 && i + 6 + charCount * 2 + 12 <= stream.Length)
                {
                    string name = Encoding.Unicode.GetString(stream, i + 6, charCount * 2);
                    int typeIdOffset = i + 6 + charCount * 2 + 8;
                    uint typeId = BitConverter.ToUInt32(stream, typeIdOffset);
                    if (typeId == 0x49 || typeId == 0x50 || name.StartsWith("草图") || name.StartsWith("Sketch"))
                    {
                        list.Add((name, i - 2, 0));
                    }
                }
            }
        }
        for (int i = 0; i < list.Count; i++)
        {
            int end = (i + 1 < list.Count) ? list[i + 1].TagOffset : stream.Length;
            list[i] = (list[i].Name, list[i].TagOffset, end);
        }
        return list;
    }
}
