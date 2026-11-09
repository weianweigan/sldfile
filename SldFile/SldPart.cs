using SldFile.Mfc;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SldFile.Binary;

namespace SldFile;

public partial class SldPart : SwFile
{
    public SldPart(string filePath, bool readOnly = true) : base(filePath, readOnly)
    {
    }

    /// <summary>Returns precomputed mass properties for the active configuration, or null if none exist.</summary>
    public MassProperties? MassProperties => ActiveConfiguration?.MassProperties;

    /// <summary>Returns cut list items for the active configuration, or an empty list if none exist.</summary>
    public IReadOnlyList<CutListItem> CutListItems => ActiveConfiguration?.CutListItems ?? Array.Empty<CutListItem>();

    /// <summary>
    /// Exports the geometry of this part to a standard ISO-10303-21 STEP AP214 file.
    /// Prefers genuine Parasolid B-Rep geometry if present; falls back to faceted mesh representations.
    /// </summary>
    public void ExportStep(string filePath, string? configurationName = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
        using var fs = File.Create(filePath);
        ExportStep(fs, configurationName);
    }

    /// <summary>
    /// Exports the geometry of this part to a standard ISO-10303-21 STEP AP214 stream.
    /// </summary>
    public void ExportStep(Stream outputStream, string? configurationName = null)
    {
        if (outputStream == null) throw new ArgumentNullException(nameof(outputStream));
        using var writer = new StreamWriter(outputStream, Encoding.ASCII, 4096, leaveOpen: true);

        string partName = Path.GetFileNameWithoutExtension(FilePath) ?? "Part";
        byte[]? psData = ExtractParasolid(configurationName);
        if (psData != null && psData.Length > 16)
        {
            var xtModel = SldFile.Geometry.Parasolid.XtParser.Parse(psData);
            SldFile.Geometry.Step.StepWriter.Write(xtModel, writer, partName);
            return;
        }

        var mesh = GetDisplayMesh();
        if (mesh != null && mesh.Positions.Count >= 3)
        {
            SldFile.Geometry.Step.StepWriter.WriteMesh(mesh, writer, partName);
            return;
        }

        throw new InvalidOperationException("No Parasolid geometry or display mesh found to export as STEP.");
    }

    // ── Shared Authoring Stream Pipeline Helpers ─────────────────────────────

    private void EnsureWritable()
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
    }

    private (string StreamPath, byte[] CurrentStream) GetActiveResolvedFeaturesStream()
    {
        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");
        return (streamPath, currentStream);
    }

    private Feature CommitResolvedFeaturesStream(
        string streamPath,
        byte[] newStream,
        string featureName,
        FeatureKind kind,
        string className,
        int? typeId = null,
        IFeatureData? featureData = null,
        bool matchKindWhenFinding = false)
    {
        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        var existing = matchKindWhenFinding
            ? Features.FirstOrDefault(f => f.Kind == kind && string.Equals(f.Name, featureName, StringComparison.Ordinal))
            : Features.FirstOrDefault(f => string.Equals(f.Name, featureName, StringComparison.Ordinal));

        if (existing != null)
        {
            if (existing.Model == null && featureData != null)
            {
                return new Feature(
                    existing.Name,
                    existing.Kind,
                    existing.TypeName,
                    existing.Id,
                    existing.Attributes,
                    existing.Dimensions,
                    existing.Children,
                    existing.IsSuppressed,
                    existing.IsVisible,
                    featureData);
            }
            return existing;
        }

        return new Feature(featureName, kind, className, typeId, null, null, null, false, true, featureData);
    }

    /// <summary>
    /// Adds an extrusion (Boss or Cut) feature based on a sketch, updating the ResolvedFeatures stream.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddExtrude(ExtrudeFeatureModel definition, string? targetSketchName = null, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        if (!ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(definition) &&
            !ExtrudeCutFlagFeatureBuilder.IsTemplate(definition) &&
            (double.IsNaN(definition.Depth) || double.IsInfinity(definition.Depth) || definition.Depth <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(definition.Depth),
                $"Extrusion depth must be a positive finite number, but received {definition.Depth}.");
        }

        if (definition.BothDirections && (double.IsNaN(definition.DepthDirection2) || double.IsInfinity(definition.DepthDirection2) || definition.DepthDirection2 <= 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(definition.DepthDirection2),
                $"Direction 2 extrusion depth must be a positive finite number, but received {definition.DepthDirection2}.");
        }

        ExtrudeFeatureBuilder.ValidateDefinition(definition);
        if (string.IsNullOrWhiteSpace(targetSketchName))
            targetSketchName = definition.SketchName;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

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
                    sketchEndOffset = BoundSketchEndByNextFolder(allFolders, match.TagOffset, match.EndOffset);
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
                    sketchEndOffset = BoundSketchEndByNextFolder(allFolders, lastStreamSketch.TagOffset, lastStreamSketch.EndOffset);
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

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            if (ExtrudeCutFlagFeatureBuilder.IsTemplate(definition))
            {
                resolvedFeatureName = ExtrudeCutFlagFeatureBuilder.GetTemplateName(definition);
            }
            else if (ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(definition))
            {
                resolvedFeatureName = ExtrudeUpToSurfaceTemplateBuilder.GetTemplateName(definition);
            }
            else if (definition.IsCut &&
                (ExtrudeCutTemplateBuilder.IsDraftDirection2Template(definition) ||
                 ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(definition)))
            {
                resolvedFeatureName = ExtrudeCutTemplateBuilder.GetTemplateName(definition);
            }
            else if (definition.IsCut)
            {
                int cutCount = Features.Count(f => f.Kind == FeatureKind.Cut || f.Name.StartsWith("切除-拉伸"));
                resolvedFeatureName = $"切除-拉伸{cutCount + 1}";
            }
            else if (definition.IsThinFeature)
            {
                resolvedFeatureName = ExtrudeThinFeatureBuilder.GetTemplateName(definition);
            }
            else
            {
                int bossCount = Features.Count(f => (f.Kind == FeatureKind.Extrusion && !f.Name.StartsWith("切除-拉伸")) || f.Name.StartsWith("凸台-拉伸"));
                resolvedFeatureName = $"凸台-拉伸{bossCount + 1}";
            }
        }

        var targetSketchFeature = Sketches
            .FirstOrDefault(s => string.Equals(s.Name, targetSketchFolder.DisplayName, StringComparison.OrdinalIgnoreCase));
        var newStream = ExtrudeFeatureBuilder.BuildExtrusionStream(
            currentStream, definition, targetSketchFolder, sketchEndOffset, resolvedFeatureName,
            FilePath, targetSketchFeature);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            definition.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds an extrusion feature with a specified depth in meters (e.g. 0.015 for 15mm).
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddExtrude(double depth, bool isCut = false, uint endCondition = 0, string? targetSketchName = null, string? featureName = null)
    {
        var def = new ExtrudeFeatureModel
        {
            Depth = depth,
            IsCut = isCut,
            EndCondition = endCondition
        };
        return AddExtrude(def, targetSketchName, featureName);
    }

    /// <summary>
    /// Adds a measured single-direction solid, thin one-direction, both-directions or cut-full revolution.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddRevolve(RevolveFeatureModel definition, string? targetSketchName = null, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        RevolveFeatureBuilder.ValidateDefinition(definition);

        if (string.IsNullOrWhiteSpace(targetSketchName))
            targetSketchName = definition.SketchName;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        FeatureFolder? targetSketchFolder;
        if (!string.IsNullOrWhiteSpace(targetSketchName))
        {
            targetSketchFolder = allFolders.FirstOrDefault(f =>
                f.ClassName.StartsWith("moProfileFeature_") &&
                string.Equals(f.DisplayName, targetSketchName, StringComparison.OrdinalIgnoreCase));
            if (targetSketchFolder == null)
                throw new ArgumentException($"Target sketch '{targetSketchName}' was not found in the document.", nameof(targetSketchName));
        }
        else
        {
            targetSketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
            if (targetSketchFolder == null)
                throw new InvalidOperationException("No sketch found in document to revolve.");
        }

        int sketchEndOffset = BoundSketchEndByNextFolder(allFolders, targetSketchFolder.TagOffset, currentStream.Length);

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName) &&
            (definition.IsThinFeature || definition.IsCut || definition.BothDirections))
        {
            resolvedFeatureName = RevolveOptionFeatureBuilder.GetTemplateName(definition);
        }
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int revolveCount = Features.Count(f => f.Kind == FeatureKind.Revolve || f.Name.StartsWith("旋转"));
            resolvedFeatureName = $"旋转{revolveCount + 1}";
        }

        var newStream = RevolveFeatureBuilder.BuildRevolveStream(
            currentStream, definition, targetSketchFolder, sketchEndOffset, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Revolve,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds a constant-radius fillet to the controlled single target edge of the fillet_2mm base body.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddFillet(FilletFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        // Type=2 face fillet uses the measured face-blend layout and its own fixed templates.
        if (definition.Type == FaceFilletSegments.NativeType ||
            definition.FaceReferences != null)
        {
            FaceFilletFeatureBuilder.ValidateDefinition(definition);
            string faceName = string.IsNullOrWhiteSpace(featureName)
                ? FaceFilletAuthoringTemplates.FeatureName
                : featureName!;
            var faceStream = FaceFilletFeatureBuilder.BuildFaceFilletStream(
                currentStream, definition, faceName);
            return CommitResolvedFeaturesStream(
                streamPath,
                faceStream,
                faceName,
                FeatureKind.Fillet,
                definition.ClassName,
                (int)definition.TypeId,
                definition);
        }

        FilletFeatureBuilder.ValidateDefinition(definition);

        var (sketchFolder, sketchEndOffset) = ResolveLastSketch(currentStream, "fillet");

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int filletCount = Features.Count(f => f.Kind == FeatureKind.Fillet || f.Name.StartsWith("圆角"));
            resolvedFeatureName = $"圆角{filletCount + 1}";
        }

        var newStream = FilletFeatureBuilder.BuildFilletStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Fillet,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds the fixed measured variable-radius fillet (one edge, two control points 2 mm / 4 mm)
    /// on the controlled stripped FilletOptions variable base. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddVariableFillet(
        VariableFilletFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        VariableFilletFeatureBuilder.ValidateDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? VariableFilletFeatureBuilder.TemplateName
            : featureName!;
        var newStream = VariableFilletFeatureBuilder.BuildVariableFilletStream(
            currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Fillet,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds a controlled distance-distance or angle-distance chamfer to the measured single edge
    /// of the ChamferOptions base body. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddChamfer(ChamferFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        ChamferFeatureBuilder.ValidateDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var (sketchFolder, sketchEndOffset) = ResolveLastSketch(currentStream, "chamfer");

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int chamferCount = Features.Count(f => f.Kind == FeatureKind.Chamfer || f.Name.StartsWith("倒角"));
            resolvedFeatureName = $"倒角{chamferCount + 1}";
        }

        var newStream = ChamferFeatureBuilder.BuildChamferStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Chamfer,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds a controlled two-direction linear pattern on the measured PatternOptions base body.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddLinearPattern(LinearPatternFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        PatternFeatureBuilder.ValidateLinearDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var (sketchFolder, sketchEndOffset) = ResolveLastSketch(currentStream, "pattern");

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            resolvedFeatureName =
                PatternFeatureBuilder.NativeFeatureName(currentStream, PatternNativeMode.Linear) ??
                $"线性阵列{Features.Count(f => f.Kind == FeatureKind.Pattern || f.Name.StartsWith("线性阵列")) + 1}";
        }

        var newStream = PatternFeatureBuilder.BuildLinearPatternStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Pattern,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds a controlled equal-spacing circular pattern on the measured PatternOptions base body.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddCircularPattern(CircularPatternFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        PatternFeatureBuilder.ValidateCircularDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var (sketchFolder, sketchEndOffset) = ResolveLastSketch(currentStream, "pattern");

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            resolvedFeatureName =
                PatternFeatureBuilder.NativeFeatureName(currentStream, PatternNativeMode.Circular) ??
                $"圆周阵列{Features.Count(f => f.Kind == FeatureKind.Pattern || f.Name.StartsWith("圆周阵列")) + 1}";
        }

        var newStream = PatternFeatureBuilder.BuildCircularPatternStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Pattern,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds the controlled mirror pattern on the measured PatternOptions/mirror_plane base.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddMirrorPattern(MirrorFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        MirrorPatternFeatureBuilder.ValidateDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int count = Features.Count(f => f.Kind == FeatureKind.Mirror || f.Name.StartsWith("镜像"));
            resolvedFeatureName = $"镜像{count + 1}";
        }

        var newStream = MirrorPatternFeatureBuilder.BuildMirrorPatternStream(
            currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Mirror,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds a controlled Hole Wizard hole: a legacy simple hole (ThroughAll or the measured
    /// Blind layout) or the measured tapped M6 layout.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddHoleWizard(HoleWizardFeatureModel definition, string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string tailStreamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        if (LegacyHoleWizardTutorialBuilder.IsLegacyDefinition(definition))
        {
            var (legacyStreamPath, legacyStream) = GetActiveResolvedFeaturesStream();
            var legacyTemplate = LegacyHoleWizardTutorialBuilder.GetTemplate(definition);
            string legacyName = string.IsNullOrWhiteSpace(featureName)
                ? legacyTemplate.FeatureName
                : featureName!;
            var legacyNewStream = LegacyHoleWizardTutorialBuilder.Build(
                legacyStream, definition, legacyName);
            return CommitResolvedFeaturesStream(
                legacyStreamPath,
                legacyNewStream,
                legacyName,
                FeatureKind.HoleWizard,
                definition.ClassName,
                (int)definition.TypeId,
                definition);
        }

        HoleWizardFeatureBuilder.ValidateDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            if (HoleWizardFeatureBuilder.IsCounterSinkDefinition(definition))
            {
                resolvedFeatureName =
                    HoleWizardFeatureBuilder.CounterSinkFeatureNameFor(definition) ??
                    HoleWizardCounterSinkAuthoringTemplates.M6.FeatureName;
            }
            else if (HoleWizardFeatureBuilder.IsCounterBoreDefinition(definition))
            {
                resolvedFeatureName =
                    HoleWizardFeatureBuilder.CounterBoreFeatureNameFor(definition);
            }
            else
            {
                bool tapped = definition.FolderTypeId == 0x52u || definition.Type == 48 ||
                              definition.ThreadDiameter.HasValue || definition.ThreadDepth.HasValue;
                string prefix = tapped ? "M6 螺纹孔" : "孔";
                int count = Features.Count(f => f.Kind == FeatureKind.HoleWizard ||
                                                f.Name.StartsWith(prefix, StringComparison.Ordinal));
                resolvedFeatureName = $"{prefix}{count + 1}";
            }
        }

        var newStream = HoleWizardFeatureBuilder.BuildHoleWizardStream(
            currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.HoleWizard,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    private (FeatureFolder SketchFolder, int SketchEndOffset) ResolveLastSketch(byte[] currentStream, string contextName = "pattern")
    {
        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        var sketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
        if (sketchFolder == null)
            throw new InvalidOperationException($"No sketch found in document to validate the {contextName} base body.");

        int sketchEndOffset = BoundSketchEndByNextFolder(allFolders, sketchFolder.TagOffset, currentStream.Length);
        return (sketchFolder, sketchEndOffset);
    }

    private static int BoundSketchEndByNextFolder(List<FeatureFolder> allFolders, int sketchTagOffset, int currentEnd)
    {
        int nextFolder = int.MaxValue;
        foreach (var folder in allFolders)
        {
            if (folder.TagOffset > sketchTagOffset && folder.TagOffset < nextFolder)
                nextFolder = folder.TagOffset;
        }
        return Math.Min(currentEnd, nextFolder);
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
