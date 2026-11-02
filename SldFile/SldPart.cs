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
    /// <remarks>
    /// Authoring supports full-sketch blind Boss/Cut, reversed blind Boss, mid-plane Boss,
    /// two-direction blind Boss (DepthDirection2), direction-1 draft and start offset (FromType=3)
    /// on the evidenced layouts. It requires a ResolvedFeatures stream version 0x70/0x71 whose
    /// target sketch has a parsed moSketchChain_c and a discoverable 0x67 child-count marker;
    /// option authoring additionally requires version 0x70. Other versions, chain-less sketches
    /// and unsupported selections are rejected with NotSupportedException before any stream
    /// change. Use a new definition containing the supported parameters; definitions read from a
    /// file may contain selections that cannot yet be authored.
    /// <para>
    /// <see cref="SketchSelectionMode.ExplicitSelection"/> is supported on the controlled
    /// SelectionAuthoring dual-rectangle bases stripped at their extrusion tag: one or two
    /// closed contours matching the measured chain sequences, or one measured region
    /// (4-edge overlap / 6-edge L-shape), plain Boss Blind at the measured 0.010 m template
    /// depth. The native context/reference suffix is captured from the target document and
    /// the moCStringHandle_c source path is kept or rewritten to this document's
    /// <see cref="SwFile.FilePath"/>; region loop order is derived from the target sketch
    /// geometry. A missing context, ambiguous window, other sketch, other depth or any
    /// option-bearing combination is rejected before the stream is modified. Explicit
    /// selection does not use the whole-sketch 0x67 marker.
    /// </para>
    /// <para>
    /// The measured legacy tutorial bases 01_pin (Boss-Extrude2, UpToSurface) and 03_circlip
    /// (Base-Extrude, UpToSurface) are detected by their exact ResolvedFeatures signature and
    /// accept only the measured native block. The definition must keep EndCondition=4,
    /// ReverseDirection, FeatureScope, AutoSelect and the measured (empty) body scope, and the
    /// measured feature name is required; custom names, modified bases and other definitions are
    /// rejected before any stream change. Other bases take the normal authoring path.
    /// </para>
    /// <para>
    /// Four measured Task 1.1 handoff bases are fixed templates: DraftOutward=true cut,
    /// thin CapEnds, cut FromType=3/FromOffsetReverse and thin AutoFilletCorners. They are
    /// selected by exact stripped-base signature; the measured feature name and every decoded
    /// field (depth, draft, offset, thin wall/cap, auto-fillet radius) must match. UpToVertex
    /// remains blocked because its 0x72 stream stores the feature as a nested inline record.
    /// </para>
    /// The explicit target sketch name takes precedence over definition.SketchName.
    /// </remarks>
    /// <exception cref="NotSupportedException">A parameter or selection cannot yet be serialized.</exception>
    public Feature AddExtrude(ExtrudeFeatureDefinitionData definition, string? targetSketchName = null, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        // Legacy tutorial fixed authoring: a matched stripped base embeds the measured sketch
        // context and accepts only its measured native block.
        string legacyTutorialExtrudePath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? legacyTutorialExtrudeStream = GetStreamBytes(legacyTutorialExtrudePath) ?? ResolvedFeatures.RawData;
        var legacyTutorialExtrude = legacyTutorialExtrudeStream == null || legacyTutorialExtrudeStream.Length < 16
            ? null
            : LegacyTutorialFeatureBuilder.FindBase(legacyTutorialExtrudeStream, revolve: false);
        if (legacyTutorialExtrude != null)
        {
            if (!string.IsNullOrWhiteSpace(targetSketchName))
                throw new NotSupportedException(
                    "Legacy tutorial Extrude authoring embeds the measured sketch context; " +
                    "an explicit target sketch is not supported. No stream changes have been made.");
            string legacyName = string.IsNullOrWhiteSpace(featureName)
                ? legacyTutorialExtrude.FeatureName
                : featureName!;
            var legacyNewStream = LegacyTutorialFeatureBuilder.BuildExtrude(
                legacyTutorialExtrudeStream!, definition, legacyName);
            SetPendingStream(legacyTutorialExtrudePath, legacyNewStream);
            ReParseResolvedFeatures(legacyNewStream);
            return Features.FirstOrDefault(f => f.Name == legacyName)
                ?? new Feature(legacyName,
                    legacyTutorialExtrude.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion,
                    definition.ClassName, (int)definition.TypeId, null, null, null, false, true,
                    definition);
        }

        var task11Extrude = legacyTutorialExtrudeStream == null || legacyTutorialExtrudeStream.Length < 16
            ? null
            : Task11FeatureAuthoringTemplates.FindByBase(legacyTutorialExtrudeStream, revolve: false);
        if (task11Extrude != null)
        {
            if (!string.IsNullOrWhiteSpace(targetSketchName))
                throw new NotSupportedException(
                    "Task 1.1/1.2 handoff Extrude authoring embeds the measured sketch context; " +
                    "an explicit target sketch is not supported. No stream changes have been made.");
            string task11Name = string.IsNullOrWhiteSpace(featureName)
                ? task11Extrude.FeatureName
                : featureName!;
            var task11NewStream = Task11FeatureAuthoringBuilder.BuildExtrude(
                legacyTutorialExtrudeStream!, task11Extrude, definition, task11Name);
            SetPendingStream(legacyTutorialExtrudePath, task11NewStream);
            ReParseResolvedFeatures(task11NewStream);
            return Features.FirstOrDefault(f => f.Name == task11Name)
                ?? new Feature(task11Name,
                    task11Extrude.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion,
                    task11Extrude.ClassName, null, null, null, null,
                    false, true, definition);
        }

        if (!SldFile.Binary.FeatureData.ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(definition) &&
            !SldFile.Binary.FeatureData.ExtrudeCutFlagFeatureBuilder.IsTemplate(definition) &&
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
            if (SldFile.Binary.FeatureData.ExtrudeCutFlagFeatureBuilder.IsTemplate(definition))
            {
                resolvedFeatureName = SldFile.Binary.FeatureData.ExtrudeCutFlagFeatureBuilder.GetTemplateName(definition);
            }
            else if (SldFile.Binary.FeatureData.ExtrudeUpToSurfaceTemplateBuilder.IsTemplate(definition))
            {
                resolvedFeatureName = SldFile.Binary.FeatureData.ExtrudeUpToSurfaceTemplateBuilder.GetTemplateName(definition);
            }
            else if (definition.IsCut &&
                (SldFile.Binary.FeatureData.ExtrudeCutTemplateBuilder.IsDraftDirection2Template(definition) ||
                 SldFile.Binary.FeatureData.ExtrudeCutTemplateBuilder.IsMergeFalseTemplate(definition)))
            {
                resolvedFeatureName = SldFile.Binary.FeatureData.ExtrudeCutTemplateBuilder.GetTemplateName(definition);
            }
            else if (definition.IsCut)
            {
                int cutCount = Features.Count(f => f.Kind == FeatureKind.Cut || f.Name.StartsWith("切除-拉伸"));
                resolvedFeatureName = $"切除-拉伸{cutCount + 1}";
            }
            else if (definition.IsThinFeature)
            {
                // Thin authoring reuses the measured native block; a different name would resize it.
                resolvedFeatureName = SldFile.Binary.FeatureData.ExtrudeThinFeatureBuilder.GetTemplateName(definition);
            }
            else
            {
                int bossCount = Features.Count(f => (f.Kind == FeatureKind.Extrusion && !f.Name.StartsWith("切除-拉伸")) || f.Name.StartsWith("凸台-拉伸"));
                resolvedFeatureName = $"凸台-拉伸{bossCount + 1}";
            }
        }

        // Build new stream with synthesized feature block
        var targetSketchFeature = Sketches
            .FirstOrDefault(s => string.Equals(s.Name, targetSketchFolder.DisplayName, StringComparison.OrdinalIgnoreCase));
        var newStream = ExtrudeFeatureBuilder.BuildExtrusionStream(
            currentStream, definition, targetSketchFolder, sketchEndOffset, resolvedFeatureName,
            FilePath, targetSketchFeature);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        var createdFeature = Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, definition.IsCut ? FeatureKind.Cut : FeatureKind.Extrusion, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);

        return createdFeature;
    }

    /// <summary>
    /// Adds a measured single-direction solid, thin one-direction, both-directions or cut-full revolution.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    /// <remarks>
    /// The solid serializer path requires a ResolvedFeatures stream version 0x70 and the controlled
    /// rectangle + construction-axis sketch from the RevolveOptions experiments (forward partial,
    /// reverse partial or full 360 measured bases). Thin 90/360, both-directions and cut 360 use
    /// complete measured native blocks on their exact stripped bases. Mismatched/modified bases,
    /// wrong folder/child counts, unsupported angles or option combinations are rejected with
    /// <see cref="NotSupportedException"/> before any stream change.
    /// <para>
    /// The measured legacy tutorial bases 05_s_632_1 (Boss-Revolve1) and
    /// 09_strain_relief_straight (Cut-Revolve1, explicit body scope) are detected by their exact
    /// ResolvedFeatures signature and accept only the measured native block. IsCut and
    /// ReverseDirection must match the measured definition, the measured feature name is
    /// required, and custom names or modified bases are rejected before any stream change.
    /// Other bases take the normal authoring path.
    /// </para>
    /// <para>
    /// The Task 1.1 revolve axis=model-edge handoff sample (Angle=2π, AxisType=1, Edge axis)
    /// uses its own exact stripped-base fixed template. Other axis types, angles and modified
    /// bases are rejected; reference-axis and UpToSurface revolve remain blocked.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">A parameter, stream layout or sketch is not yet supported.</exception>
    public Feature AddRevolve(RevolveFeatureDefinitionData definition, string? targetSketchName = null, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        // Legacy tutorial fixed authoring: a matched stripped base embeds the measured sketch
        // and axis context and accepts only its measured native block.
        string legacyTutorialRevolvePath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? legacyTutorialRevolveStream = GetStreamBytes(legacyTutorialRevolvePath) ?? ResolvedFeatures.RawData;
        var legacyTutorialRevolve = legacyTutorialRevolveStream == null || legacyTutorialRevolveStream.Length < 16
            ? null
            : LegacyTutorialFeatureBuilder.FindBase(legacyTutorialRevolveStream, revolve: true);
        if (legacyTutorialRevolve != null)
        {
            if (!string.IsNullOrWhiteSpace(targetSketchName))
                throw new NotSupportedException(
                    "Legacy tutorial Revolve authoring embeds the measured sketch and axis context; " +
                    "an explicit target sketch is not supported. No stream changes have been made.");
            string legacyName = string.IsNullOrWhiteSpace(featureName)
                ? legacyTutorialRevolve.FeatureName
                : featureName!;
            var legacyNewStream = LegacyTutorialFeatureBuilder.BuildRevolve(
                legacyTutorialRevolveStream!, definition, legacyName);
            SetPendingStream(legacyTutorialRevolvePath, legacyNewStream);
            ReParseResolvedFeatures(legacyNewStream);
            return Features.FirstOrDefault(f => f.Name == legacyName)
                ?? new Feature(legacyName, FeatureKind.Revolve, definition.ClassName,
                    (int)definition.TypeId, null, null, null, false, true, definition);
        }

        var task11Revolve = legacyTutorialRevolveStream == null || legacyTutorialRevolveStream.Length < 16
            ? null
            : Task11FeatureAuthoringTemplates.FindByBase(legacyTutorialRevolveStream, revolve: true);
        if (task11Revolve != null)
        {
            if (!string.IsNullOrWhiteSpace(targetSketchName))
                throw new NotSupportedException(
                    "Task 1.1/1.2 handoff Revolve authoring embeds the measured sketch and axis context; " +
                    "an explicit target sketch is not supported. No stream changes have been made.");
            string task11Name = string.IsNullOrWhiteSpace(featureName)
                ? task11Revolve.FeatureName
                : featureName!;
            var task11NewStream = Task11FeatureAuthoringBuilder.BuildRevolve(
                legacyTutorialRevolveStream!, task11Revolve, definition, task11Name);
            SetPendingStream(legacyTutorialRevolvePath, task11NewStream);
            ReParseResolvedFeatures(task11NewStream);
            return Features.FirstOrDefault(f => f.Name == task11Name)
                ?? new Feature(task11Name, FeatureKind.Revolve, task11Revolve.ClassName,
                    null, null, null, null, false, true, definition);
        }

        RevolveFeatureBuilder.ValidateDefinition(definition);

        if (string.IsNullOrWhiteSpace(targetSketchName))
            targetSketchName = definition.SketchName;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

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

        int sketchEndOffset = currentStream.Length;
        foreach (var folder in allFolders)
        {
            if (folder.TagOffset > targetSketchFolder.TagOffset && folder.TagOffset < sketchEndOffset)
                sketchEndOffset = folder.TagOffset;
        }

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName) &&
            (definition.IsThinFeature || definition.IsCut || definition.BothDirections))
        {
            // Template authoring reuses the measured native block; its name must not be resized.
            resolvedFeatureName = SldFile.Binary.FeatureData.RevolveOptionFeatureBuilder.GetTemplateName(definition);
        }
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int revolveCount = Features.Count(f => f.Kind == FeatureKind.Revolve || f.Name.StartsWith("旋转"));
            resolvedFeatureName = $"旋转{revolveCount + 1}";
        }

        var newStream = RevolveFeatureBuilder.BuildRevolveStream(
            currentStream, definition, targetSketchFolder, sketchEndOffset, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Revolve, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds a constant-radius fillet to the controlled single target edge of the fillet_2mm base body.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    /// <remarks>
    /// Only <see cref="FilletFeatureDefinitionData.DefaultRadius"/> is authorable in this slice;
    /// Type, explicit edge selections, multi-radius and every other unverified field are rejected
    /// with <see cref="NotSupportedException"/> before any stream change. The document must be a
    /// stripped version of the controlled fillet_2mm base body with a ResolvedFeatures stream
    /// version 0x70, a parsed sketch chain and the 0x67 sketch marker.
    /// </remarks>
    public Feature AddFillet(FilletFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        // Type=2 face fillet uses the measured face-blend layout and its own fixed templates.
        if (definition.Type == SldFile.Binary.FeatureData.FaceFilletSegments.NativeType ||
            definition.FaceReferences != null)
        {
            SldFile.Binary.FeatureData.FaceFilletFeatureBuilder.ValidateDefinition(definition);
            string faceName = string.IsNullOrWhiteSpace(featureName)
                ? SldFile.Binary.FeatureData.FaceFilletAuthoringTemplates.FeatureName
                : featureName!;
            var faceStream = SldFile.Binary.FeatureData.FaceFilletFeatureBuilder.BuildFaceFilletStream(
                currentStream, definition, faceName);
            SetPendingStream(streamPath, faceStream);
            ReParseResolvedFeatures(faceStream);
            return Features.FirstOrDefault(f => f.Name == faceName)
                ?? new Feature(faceName, FeatureKind.Fillet, definition.ClassName,
                    (int)definition.TypeId, null, null, null, false, true, definition);
        }

        FilletFeatureBuilder.ValidateDefinition(definition);

        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        var sketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
        if (sketchFolder == null)
            throw new InvalidOperationException("No sketch found in document to validate the fillet base body.");

        int sketchEndOffset = currentStream.Length;
        foreach (var folder in allFolders)
        {
            if (folder.TagOffset > sketchFolder.TagOffset && folder.TagOffset < sketchEndOffset)
                sketchEndOffset = folder.TagOffset;
        }

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int filletCount = Features.Count(f => f.Kind == FeatureKind.Fillet || f.Name.StartsWith("圆角"));
            resolvedFeatureName = $"圆角{filletCount + 1}";
        }

        var newStream = FilletFeatureBuilder.BuildFilletStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Fillet, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds the fixed measured variable-radius fillet (one edge, two control points 2 mm / 4 mm)
    /// on the controlled stripped FilletOptions variable base. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The complete measured VarFillet_c block is appended to its exact source or resave stripped
    /// base (stream 0x70, folder count 20, 0x67 value 0). Radius, edge, vertex and transition values
    /// are template-fixed; a mismatched definition, base signature, folder/marker or custom feature
    /// name is rejected before any stream change.
    /// </remarks>
    public Feature AddVariableFillet(
        VariableFilletFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        SldFile.Binary.FeatureData.VariableFilletFeatureBuilder.ValidateDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SldFile.Binary.FeatureData.VariableFilletFeatureBuilder.TemplateName
            : featureName!;
        var newStream = SldFile.Binary.FeatureData.VariableFilletFeatureBuilder.BuildVariableFilletStream(
            currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Fillet, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds a controlled distance-distance or angle-distance chamfer to the measured single edge
    /// of the ChamferOptions base body. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// Distance-distance requires EdgeChamferDistanceSide0 and Side1 (unequal); angle-distance
    /// requires EdgeChamferDistanceSide0 and EdgeChamferAngle in (0, pi/2). Type (1/2) and
    /// EqualDistance=false may be supplied consistently. Face/vertex selections, equal distances,
    /// tangent propagation and every other unverified field are rejected before any stream change.
    /// </remarks>
    public Feature AddChamfer(ChamferFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        ChamferFeatureBuilder.ValidateDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        var sketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
        if (sketchFolder == null)
            throw new InvalidOperationException("No sketch found in document to validate the chamfer base body.");

        int sketchEndOffset = currentStream.Length;
        foreach (var folder in allFolders)
        {
            if (folder.TagOffset > sketchFolder.TagOffset && folder.TagOffset < sketchEndOffset)
                sketchEndOffset = folder.TagOffset;
        }

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int chamferCount = Features.Count(f => f.Kind == FeatureKind.Chamfer || f.Name.StartsWith("倒角"));
            resolvedFeatureName = $"倒角{chamferCount + 1}";
        }

        var newStream = ChamferFeatureBuilder.BuildChamferStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Chamfer, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds a controlled two-direction linear pattern on the measured PatternOptions base body.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// Only D1/D2 instance counts (>=2) and spacings (>0) are authorable; direction/source
    /// selection references stay measured. Geometry pattern, vary-sketch, skipped items, source
    /// arrays and all other unverified fields are rejected before any stream change. Requires
    /// ResolvedFeatures stream version 0x72 and the controlled stripped base body.
    /// </remarks>
    public Feature AddLinearPattern(LinearPatternFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        PatternFeatureBuilder.ValidateLinearDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var (sketchFolder, sketchEndOffset) = ResolveLastSketch(currentStream);
        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            resolvedFeatureName =
                PatternFeatureBuilder.NativeFeatureName(currentStream, PatternNativeMode.Linear) ??
                $"线性阵列{Features.Count(f => f.Kind == FeatureKind.Pattern || f.Name.StartsWith("线性阵列")) + 1}";
        }

        var newStream = PatternFeatureBuilder.BuildLinearPatternStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Pattern, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds a controlled equal-spacing circular pattern on the measured PatternOptions base body.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// Only TotalInstances (>=2) and Spacing in (0, 2*pi] are authorable; the per-instance angle is
    /// derived as Spacing/(TotalInstances-1). The axis/source selection references stay measured.
    /// Reverse/symmetric/geometry-pattern/vary and all other unverified fields are rejected.
    /// </remarks>
    public Feature AddCircularPattern(CircularPatternFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        PatternFeatureBuilder.ValidateCircularDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var (sketchFolder, sketchEndOffset) = ResolveLastSketch(currentStream);
        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            resolvedFeatureName =
                PatternFeatureBuilder.NativeFeatureName(currentStream, PatternNativeMode.Circular) ??
                $"圆周阵列{Features.Count(f => f.Kind == FeatureKind.Pattern || f.Name.StartsWith("圆周阵列")) + 1}";
        }

        var newStream = PatternFeatureBuilder.BuildCircularPatternStream(
            currentStream, definition, sketchFolder, sketchEndOffset, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Pattern, definition.ClassName, (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds the controlled mirror pattern on the measured PatternOptions/mirror_plane base.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    /// <remarks>
    /// The document must be the native mirror sample stripped at its moMirrorPattern_c tag
    /// (folder count 22, stream 0x72). The measured payload carries the BaseBox source-feature
    /// persistent-reference blob and the Right-Plane reference; only the feature name and
    /// allocated feature id are written. GeometryPattern, feature scope, mirror faces and
    /// visual-property propagation are rejected with <see cref="NotSupportedException"/> before
    /// any stream change.
    /// </remarks>
    /// <exception cref="NotSupportedException">A parameter, stream layout or base body is not yet supported.</exception>
    public Feature AddMirrorPattern(MirrorPatternFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        MirrorPatternFeatureBuilder.ValidateDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            int count = Features.Count(f => f.Kind == FeatureKind.Mirror || f.Name.StartsWith("镜像"));
            resolvedFeatureName = $"镜像{count + 1}";
        }

        var newStream = MirrorPatternFeatureBuilder.BuildMirrorPatternStream(
            currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Mirror, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds a controlled Hole Wizard hole: a legacy simple hole (ThroughAll or the measured
    /// Blind layout) or the measured tapped M6 layout.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    /// <remarks>
    /// The document must be the matching native sample truncated at the moHoleWzd_c tag
    /// (folder count 20): legacy stream 0x71/TypeId 0x54 for ThroughAll/Blind, or the tapped
    /// M6 stream 0x84/TypeId 0x52. Legacy authoring parameterizes only Diameter; tapped
    /// authoring parameterizes only ThreadDiameter and ThreadDepth while keeping ISO/M6,
    /// Type 48, EndCondition 1 and the measured face/sketch-point context. All other fields
    /// are rejected with <see cref="NotSupportedException"/> before any stream change.
    /// <para>
    /// Two measured 2026 variants use fixed native blocks: the blind ISO M6 counterbore
    /// (EndCondition=0, ThruHoleDepth 0.010 or 0.008) and the two-point ISO M6 counterbore
    /// created from two selected EXTSKETCHPOINT sketch points. Both require their exact
    /// stripped base signature, the measured feature name and the measured ISO/CBORE values;
    /// custom names, other depths, other point locations and tampered bases are rejected.
    /// </para>
    /// <para>
    /// Tutorial HoleWizard families whose hole folder is the last folder in the stream are
    /// selected by their exact stripped-base signature: NPSM/pipe tapped, ANSI clearance and
    /// drill-size holes, #6/M3.5 counterbores, M2 countersink, M8/M10/1/2-20 tapped and the
    /// legacy blind Tap family. Any non-null definition field must equal the native API
    /// snapshot; the measured feature name and exact base are required.
    /// </para>
    /// <para>
    /// The ISO M6 counterbore block accepts the native single-variable parameter sets
    /// through Ø6/Ø8/Ø10, counterbore Ø11/Ø13 and counterbore depth 4/6/8 mm; the ISO M6
    /// countersink block accepts Ø11/Ø13/Ø17 and 90/100/120 degrees. The measured slot copies
    /// are patched and re-read natively. Through-all ThruHoleDepth and two-point coordinates
    /// remain fixed; other values are rejected before any stream change.
    /// </para>
    /// </remarks>
    /// <exception cref="NotSupportedException">A parameter, stream layout or base body is not yet supported.</exception>
    public Feature AddHoleWizard(HoleWizardFeatureDefinitionData definition, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        // Measured tutorial families whose hole folder is the stream tail are selected by the
        // exact stripped-base signature, so an empty definition is valid and every non-null
        // definition field must agree with the native API snapshot.
        string tailStreamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? tailStream = GetStreamBytes(tailStreamPath) ?? ResolvedFeatures.RawData;
        if (tailStream != null && tailStream.Length >= 16)
        {
            var tailTemplate = SldFile.Binary.FeatureData.LegacyHoleWizardTailBuilder.FindTemplate(tailStream);
            if (tailTemplate != null)
            {
                string tailName = string.IsNullOrWhiteSpace(featureName)
                    ? tailTemplate.FeatureName
                    : featureName!;
                var tailNewStream = SldFile.Binary.FeatureData.LegacyHoleWizardTailBuilder.Build(
                    tailStream, tailTemplate, definition, tailName);
                SetPendingStream(tailStreamPath, tailNewStream);
                ReParseResolvedFeatures(tailNewStream);
                return Features.FirstOrDefault(f => f.Name == tailName)
                    ?? new Feature(tailName, FeatureKind.HoleWizard, definition.ClassName,
                        (int)definition.TypeId, null, null, null, false, true, definition);
            }
        }

        if (SldFile.Binary.FeatureData.LegacyHoleWizardTutorialBuilder.IsLegacyDefinition(definition))
        {
            string legacyStreamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
            byte[]? legacyStream = GetStreamBytes(legacyStreamPath) ?? ResolvedFeatures.RawData;
            if (legacyStream == null || legacyStream.Length < 16)
                throw new InvalidOperationException("No ResolvedFeatures stream available in document.");
            var legacyTemplate = SldFile.Binary.FeatureData.LegacyHoleWizardTutorialBuilder.GetTemplate(definition);
            string legacyName = string.IsNullOrWhiteSpace(featureName)
                ? legacyTemplate.FeatureName
                : featureName!;
            var legacyNewStream = SldFile.Binary.FeatureData.LegacyHoleWizardTutorialBuilder.Build(
                legacyStream, definition, legacyName);
            SetPendingStream(legacyStreamPath, legacyNewStream);
            ReParseResolvedFeatures(legacyNewStream);
            return Features.FirstOrDefault(f => f.Name == legacyName)
                ?? new Feature(legacyName, FeatureKind.HoleWizard, definition.ClassName,
                    (int)definition.TypeId, null, null, null, false, true, definition);
        }

        HoleWizardFeatureBuilder.ValidateDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        string resolvedFeatureName = featureName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedFeatureName))
        {
            if (HoleWizardFeatureBuilder.IsCounterSinkDefinition(definition))
            {
                resolvedFeatureName =
                    HoleWizardFeatureBuilder.CounterSinkFeatureNameFor(definition) ??
                    SldFile.Binary.FeatureData.HoleWizardCounterSinkAuthoringTemplates.M6.FeatureName;
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

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.HoleWizard, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    private (FeatureFolder SketchFolder, int SketchEndOffset) ResolveLastSketch(byte[] currentStream)
    {
        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        var sketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
        if (sketchFolder == null)
            throw new InvalidOperationException("No sketch found in document to validate the pattern base body.");

        int sketchEndOffset = currentStream.Length;
        foreach (var folder in allFolders)
        {
            if (folder.TagOffset > sketchFolder.TagOffset && folder.TagOffset < sketchEndOffset)
                sketchEndOffset = folder.TagOffset;
        }
        return (sketchFolder, sketchEndOffset);
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

    /// <summary>
    /// Adds a sheet metal base flange feature based on a sketch.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    /// <remarks>
    /// The first writable combination is the controlled open-L specimen from
    /// <c>SheetMetalOptions/sm_l_d10.SLDPRT</c> (ResolvedFeatures version 0x73):
    /// the sketch chain must be entity ids [2,1] with the measured sgLineHandle
    /// signature, and only Thickness, BendRadius and Depth are replaced in the
    /// hash-pinned measured sheet-metal subsystem. Thickness is written to the
    /// authoritative Config-0 length parameter ("厚度"/"Thickness") as well as the
    /// ResolvedFeatures display/dimension copies. Every other option, selection,
    /// direction reference, body scope, custom feature name, other stream version
    /// and any preceding solid feature is rejected with <see cref="NotSupportedException"/>
    /// before either stream is modified.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The document is read-only or has no sketch.</exception>
    /// <exception cref="ArgumentException">The named target sketch does not exist.</exception>
    /// <exception cref="NotSupportedException">The sketch, stream version or an option is outside the measured combination.</exception>
    public Feature AddSheetMetalBaseFlange(
        SheetMetalBaseFlangeDefinitionData definition,
        string? targetSketchName = null,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        SheetMetalFeatureBuilder.ValidateDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        if (string.IsNullOrWhiteSpace(targetSketchName))
            targetSketchName = definition.SketchName;

        var allFolders = FlattenFolders(ResolvedFeatures.Folders);
        FeatureFolder? targetSketchFolder = null;
        int sketchEndOffset = currentStream.Length;
        if (!string.IsNullOrWhiteSpace(targetSketchName))
        {
            targetSketchFolder = allFolders.FirstOrDefault(f =>
                f.ClassName.StartsWith("moProfileFeature_") &&
                string.Equals(f.DisplayName, targetSketchName, StringComparison.OrdinalIgnoreCase));
            if (targetSketchFolder == null)
            {
                var match = FindSketchesInStream(currentStream).FirstOrDefault(s =>
                    string.Equals(s.Name, targetSketchName, StringComparison.OrdinalIgnoreCase));
                if (match.Name == null)
                {
                    throw new ArgumentException($"Target sketch '{targetSketchName}' was not found in the document.",
                        nameof(targetSketchName));
                }
                targetSketchFolder = new FeatureFolder
                {
                    ClassName = "moProfileFeature_c",
                    DisplayName = match.Name,
                    TagOffset = match.TagOffset
                };
                sketchEndOffset = match.EndOffset;
            }
        }
        else
        {
            targetSketchFolder = allFolders.LastOrDefault(f => f.ClassName.StartsWith("moProfileFeature_"));
            if (targetSketchFolder == null)
            {
                var streamSketches = FindSketchesInStream(currentStream);
                if (streamSketches.Count == 0)
                    throw new InvalidOperationException("No sketch found in document to create a sheet metal base flange.");
                var last = streamSketches[streamSketches.Count - 1];
                targetSketchFolder = new FeatureFolder
                {
                    ClassName = "moProfileFeature_c",
                    DisplayName = last.Name,
                    TagOffset = last.TagOffset
                };
                sketchEndOffset = last.EndOffset;
            }
        }

        sketchEndOffset = BoundSketchEndByNextFolder(allFolders, targetSketchFolder.TagOffset, sketchEndOffset);

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SheetMetalBaseFlangeBuilder.DefaultFeatureName
            : featureName!;

        var newStream = SheetMetalFeatureBuilder.BuildBaseFlangeStream(
            currentStream, definition, targetSketchFolder, sketchEndOffset, resolvedFeatureName);

        // The authoritative thickness is the Config-0 length parameter named
        // "厚度"/"Thickness", not the ResolvedFeatures D3 display copies. Patch it
        // before any pending stream is queued so a failure leaves the document
        // untouched.
        string configStreamPath = streamPath.EndsWith("-ResolvedFeatures", StringComparison.Ordinal)
            ? streamPath.Substring(0, streamPath.Length - "-ResolvedFeatures".Length)
            : $"Contents/Config-{ActiveConfigurationIndex}";
        byte[]? configStream = GetStreamBytes(configStreamPath);
        if (configStream == null)
        {
            throw new NotSupportedException(
                $"Cannot author a sheet metal base flange: the configuration stream '{configStreamPath}' " +
                "was not found. No stream changes have been made.");
        }
        byte[] patchedConfig = SheetMetalConfigThicknessPatcher.Patch(configStream, definition.Thickness);

        SetPendingStream(streamPath, newStream);
        SetPendingStream(configStreamPath, patchedConfig);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Extrusion, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds one of the four fixed base-flange option templates measured on
    /// 2026-10-31 (KFactor 0.3, Direct 0.001, Deduction 0.001 or gauge table).
    /// Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    /// <remarks>
    /// The document must be the stripped template base of the matching retry
    /// sample: the ResolvedFeatures stream truncated at the moSMBaseFlange_c tag
    /// (0x3EF1 bytes, version 0x73, folder count 22, normalized prefix SHA-256
    /// pinned per template). The measured suffix is appended verbatim, so the
    /// authored ResolvedFeatures stream is byte-identical to the native source.
    /// Only Thickness 0.001 m, Depth 0.010 m, the template radius (0.002 m, or
    /// 0.001 m for the gauge sample) and the matching option combination are
    /// accepted; other values, custom names, another template's base and a
    /// non-stripped document are rejected before the stream is modified.
    /// </remarks>
    public Feature AddSheetMetalBaseFlangeFromOptionTemplate(
        SheetMetalBaseFlangeDefinitionData definition,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        SheetMetalBaseFlangeOptionBuilder.SelectTemplate(definition);
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SheetMetalBaseFlangeOptionBuilder.DefaultFeatureName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = SheetMetalBaseFlangeOptionBuilder.Build(
            currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f =>
                   f.Kind == FeatureKind.Extrusion &&
                   string.Equals(f.Name, resolvedFeatureName, StringComparison.Ordinal))
            ?? new Feature(resolvedFeatureName, FeatureKind.Extrusion, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds one of the fixed Jog/Fold/UnFold/FlatPattern templates recovered
    /// from the SolidWorks 2026 tutorial corpus (2026-10-31 scan). Call
    /// <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of the matching tutorial
    /// sample, ending at the feature tag. The template pins the stream version,
    /// prefix length, folder count and normalized prefix SHA-256; the measured
    /// suffix is appended verbatim, so the authored ResolvedFeatures stream is
    /// byte-identical to the native document. FlatPattern templates additionally
    /// accept only the measured Flat-Pattern1/Flat-Pattern2 display name. Custom
    /// names, another template's base and non-stripped documents are rejected
    /// before the stream changes.
    /// </remarks>
    public Feature AddSheetMetalTutorialFeature(
        TutorialSheetMetalFeatureTemplate template,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        bool isFlatPattern = template is
            TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane or
            TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart or
            TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled;
        var newStream = isFlatPattern
            ? TutorialSheetMetalFeatureBuilder.BuildFlatPattern(currentStream, template, featureName)
            : TutorialSheetMetalFeatureBuilder.Build(currentStream, template, featureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        var (kind, className, displayName) = template switch
        {
            TutorialSheetMetalFeatureTemplate.JogComplexPart =>
                (FeatureKind.Jog, "moJog_c", "Jog1"),
            TutorialSheetMetalFeatureTemplate.UnFoldPaperAirplane =>
                (FeatureKind.UnFold, "moUnFold_c", "Unfold2"),
            TutorialSheetMetalFeatureTemplate.FoldPaperAirplane =>
                (FeatureKind.Fold, "moFold_c", "Fold2"),
            TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane =>
                (FeatureKind.FlatPattern, "moFlatPattern_c", "Flat-Pattern2"),
            TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart =>
                (FeatureKind.FlatPattern, "moFlatPattern_c", "Flat-Pattern1"),
            TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled =>
                (FeatureKind.FlatPattern, "moFlatPattern_c", "Flat-Pattern1"),
            _ => throw new ArgumentOutOfRangeException(nameof(template))
        };
        return Features.FirstOrDefault(f => f.Kind == kind && f.Name == displayName)
            ?? new Feature(displayName, kind, className, null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the first controlled edge-flange combination to a stripped sheet-metal
    /// base specimen. Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    /// <remarks>
    /// Only the exact stripped prefix of FlangeOptions/edge_flange_90_len10
    /// (ResolvedFeatures version 0x74, prefix SHA-256 pinned) is accepted. The
    /// single measured edge selection is reused; BendAngle and BendRadius are the
    /// only replaceable parameters. Every other field must be null (unresolved)
    /// or equal the measured create4 native default; persistent edge references,
    /// custom bend allowance, other stream versions, an existing edge flange and
    /// custom feature names are rejected before the stream is modified.
    /// </remarks>
    public Feature AddEdgeFlange(
        EdgeFlangeFeatureDefinitionData definition,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        EdgeFlangeFeatureBuilder.ValidateDefinition(definition);

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? EdgeFlangeFeatureBuilder.DefaultFeatureName
            : featureName!;

        var newStream = EdgeFlangeFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Other, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds a fresh single-edge EdgeFlange using the measured 0x75 tutorial
    /// template (base flange + one EdgeFlange, ResolvedFeatures version 0x74).
    /// Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped prefix of
    /// SheetMetalFormingOptions/EdgeFlangeFresh/edge_flange_fresh_baseline
    /// (0x804B bytes, folder count 24, normalized prefix SHA-256 pinned). The
    /// patchable fields are UseDefaultBendRadius/BendRadius,
    /// ReliefWidth/ReliefDepth, UseDefaultBendRelief/UseReliefRatio,
    /// UsePositionOffset/PositionOffsetDistance, OffsetDistance and the measured
    /// BendAngle/EdgeCount/PositionType. PositionOffsetType=2 is rejected because
    /// native readback reverts it to 1. Custom display names and other bases are
    /// rejected before the stream is modified.
    /// </remarks>
    public Feature AddFreshEdgeFlange(
        EdgeFlangeFeatureDefinitionData definition,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? FreshEdgeFlangeAuthoringSegments.DisplayName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = FreshEdgeFlangeAuthoringBuilder.Build(
            currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Other, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds the Paper Airplane tutorial BaseFlange with a patched custom bend
    /// table path. Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped prefix of Paper Airplane.SLDPRT
    /// ending at the moSMBaseFlange_c tag (0x630F bytes, version 0x155, folder
    /// count 38, normalized prefix SHA-256 pinned). The bend table path is
    /// replaced in-place and must be exactly 98 characters (the measured path
    /// length) and end in .xls/.xlsx/.csv. Native reopen reports the patched path
    /// from both ISheetMetalFeatureData and IBaseFlangeFeatureData.
    /// </remarks>
    public Feature AddTutorialBaseFlangeBendTable(
        string bendTableFile,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? TutorialBendTableAuthoringSegments.DisplayName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = TutorialBendTableAuthoringBuilder.Build(
            currentStream, bendTableFile, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Extrusion,
                TutorialBendTableAuthoringSegments.ClassName, null, null, null, null,
                false, true, null);
    }

    /// <summary>
    /// Adds the controlled cross-bend SMNormalCut feature from the fixed 0x75
    /// template. Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the stripped prefix ending at the moSMNormalCut_c tag
    /// (0xE81B bytes, ResolvedFeatures 0x75, folder count 27, normalized prefix
    /// SHA-256 pinned). The measured suffix from the tag to the stream end is
    /// appended verbatim; custom names and other bases are rejected.
    /// </remarks>
    public Feature AddSMNormalCutFromTemplate(string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SMNormalCutAuthoringSegments.DisplayName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = SMNormalCutFeatureBuilder.Build(currentStream, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Other,
                SMNormalCutAuthoringSegments.ClassName, 0x87, null, null, null,
                false, true, null);
    }

    /// <summary>
    /// Adds one of the two controlled hem combinations (Type 0 Open or Type 1
    /// Closed) to the matching stripped hem base specimen. Call
    /// <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    /// <remarks>
    /// Only the exact stripped prefix of SheetMetalFormingOptions/
    /// sheetmetal_hem_open or sheetmetal_hem_closed (ResolvedFeatures version
    /// 0x73, prefix SHA-256 pinned, folder count 23) is accepted. The measured
    /// suffix reconstructs the native stream byte-for-byte. Teardrop/rolled/
    /// double hems and parameter changes not present in the samples are rejected
    /// before the stream is modified.
    /// </remarks>
    public Feature AddHem(
        HemFeatureDefinitionData definition,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        HemFeatureBuilder.ValidateDefinition(definition);
        int type = definition.Type!.Value;
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SheetMetalFormingParsers.HemDisplayName(type)
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = HemFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f => f.Name == resolvedFeatureName)
            ?? new Feature(resolvedFeatureName, FeatureKind.Hem, definition.ClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds the fixed measured flat-pattern block from the controlled 0x5D
    /// specimen. Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    /// <remarks>
    /// The document must be the hash-pinned stripped prefix of
    /// sm_base_flange_10mm.SLDPRT (ResolvedFeatures version 0x73, length 0x7380,
    /// folder count 22 and the measured 0x67/22 marker). Only the measured fixed
    /// face and default option combination are accepted; altered options, custom
    /// names, an unstripped document and unsupported versions are rejected before
    /// the stream is modified.
    /// </remarks>
    public Feature AddFlatPattern(
        FlatPatternFeatureDefinitionData? definition = null,
        string? featureName = null,
        bool? mergeFace = null,
        bool? simplifyBends = null,
        bool? cornerTreatment = null,
        int? breakCornerType = null,
        double? breakCornerRadius = null,
        int? cornerTrimReliefType = null,
        double? cornerTrimReliefDistance = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        definition ??= new FlatPatternFeatureDefinitionData();
        if (mergeFace.HasValue) definition.MergeFace = mergeFace.Value;
        if (simplifyBends.HasValue) definition.SimplifyBends = simplifyBends.Value;
        if (cornerTreatment.HasValue) definition.CornerTreatment = cornerTreatment.Value;
        if (breakCornerType.HasValue) definition.BreakCornerType = breakCornerType.Value;
        if (breakCornerRadius.HasValue) definition.BreakCornerRadius = breakCornerRadius.Value;
        if (cornerTrimReliefType.HasValue) definition.CornerTrimReliefType = cornerTrimReliefType.Value;
        if (cornerTrimReliefDistance.HasValue)
            definition.CornerTrimReliefDistance = cornerTrimReliefDistance.Value;

        FlatPatternFeatureBuilder.ValidateDefinition(definition);

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? FlatPatternFeatureBuilder.DefaultFeatureName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = FlatPatternFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f =>
                   f.Kind == FeatureKind.FlatPattern &&
                   string.Equals(f.Name, resolvedFeatureName, StringComparison.Ordinal))
            ?? new Feature(resolvedFeatureName, FeatureKind.FlatPattern,
                FlatPatternFeatureDefinitionData.SerializedClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds the inline FlatPattern 0x66 layout from
    /// sm_base_flange_after_fillet.SLDPRT (version 0x74, folder count 25) and
    /// patches the proven option slots. Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped prefix ending at the
    /// moFlatPattern_c tag (0x9B02 bytes, normalized prefix SHA-256 pinned).
    /// Patchable fields: MergeFace, CornerTreatment, SimplifyBends,
    /// BreakCornerType/Radius, CornerTrimReliefType, the shared
    /// CornerTrimReliefDistance/RatioToThickness double, UseRatioToThickness and
    /// ShowSlitInCornerRelief. FixedFace/FixedFace2/ExcludedFaces are rejected
    /// because they did not persist independently in the native API probes.
    /// </remarks>
    public Feature AddFlatPatternInline066(
        FlatPatternFeatureDefinitionData? definition = null,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        definition ??= new FlatPatternFeatureDefinitionData();
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? FlatPatternInline066AuthoringSegments.DisplayName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = FlatPatternInline066FeatureBuilder.Build(
            currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f =>
                   f.Kind == FeatureKind.FlatPattern &&
                   string.Equals(f.Name, resolvedFeatureName, StringComparison.Ordinal))
            ?? new Feature(resolvedFeatureName, FeatureKind.FlatPattern,
                FlatPatternFeatureDefinitionData.SerializedClassName,
                0x66, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Adds the fixed measured lofted-bend block from the controlled 0x72 specimen
    /// to its stripped base. Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    /// <remarks>
    /// The document must be the hash-pinned stripped prefix of
    /// LoftedBendOptions/lofted_bend_2lines.SLDPRT (ResolvedFeatures version 0x74,
    /// length 0x6043, folder count 26 and the measured 0x67/19 marker). The fixed
    /// combination is Thickness 0.001 m, NumberOfBendLines 2 and the two line
    /// profiles carried by the base. Persistent profile references, other
    /// thickness/bend-line values, custom names, an unstripped document and
    /// unsupported versions are rejected before the stream is modified.
    /// </remarks>
    public Feature AddLoftedBend(
        LoftedBendFeatureDefinitionData? definition = null,
        string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        definition ??= new LoftedBendFeatureDefinitionData();
        LoftedBendFeatureBuilder.ValidateDefinition(definition);

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? LoftedBendFeatureBuilder.DefaultFeatureName
            : featureName!;

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var newStream = LoftedBendFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        SetPendingStream(streamPath, newStream);
        ReParseResolvedFeatures(newStream);

        return Features.FirstOrDefault(f =>
                   f.Kind == FeatureKind.LoftedBend &&
                   string.Equals(f.Name, resolvedFeatureName, StringComparison.Ordinal))
            ?? new Feature(resolvedFeatureName, FeatureKind.LoftedBend,
                LoftedBendFeatureDefinitionData.SerializedClassName,
                (int)definition.TypeId, null, null, null, false, true, definition);
    }

    /// <summary>
    /// Bounds a sketch's stream end by the next design-tree folder. Stream-name scans only know
    /// the next sketch (or stream end), which for the last sketch includes every following
    /// feature; using that end hid the sketch's chain/0x67 marker ranges from authoring.
    /// </summary>
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

    /// <summary>
    /// Adds one of the measured Sweep combinations from the controlled 2026 SweepOptions samples.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of one measured sweep sample: ResolvedFeatures
    /// stream version 0x70, the sample's stripped folder count, the 0x67 marker with value 0, and the
    /// sample's sketches. Supported layouts: <c>sweep_circle10_path40</c> (Profile1/Path1),
    /// <c>sweep_circular_profile12</c> (Path1 only, CircularProfile Ø12), <c>sweep_thin_one_dir_t1</c>
    /// (Profile1/Path1, thin 0.001), <c>sweep_guide1</c> (Profile1/Path1/Guide1), and
    /// <c>sweep_profile_rect</c> (Profile1/Path1, rectangular section).
    /// The complete measured feature block is appended verbatim; selection references, sketch
    /// geometry, path length, diameter/thickness and non-parameterized options remain the measured
    /// native values. <c>merge</c>, <c>twistControlType</c> (0/1/8) and <c>twistAngleRad</c> patch only
    /// their measured option slots. Custom feature/sketch names or any other base are rejected before
    /// the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base, sketches or name do not match a measured combination.</exception>
    public Feature AddSweep(
        string? featureName = null,
        string? profileSketchName = null,
        string? pathSketchName = null,
        string? guideSketchName = null,
        bool? merge = null,
        int? twistControlType = null,
        double? twistAngleRad = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        IReadOnlyList<string>? requestedProfiles = profileSketchName == null
            ? null
            : new[] { profileSketchName };
        return AddMeasuredSweepOrLoft(
            "moSweep_c",
            featureName,
            requestedProfiles,
            pathSketchName,
            guideSketchName,
            FeatureKind.Sweep,
            merge,
            twistControlType,
            twistAngleRad);
    }

    /// <summary>
    /// Adds one of the measured Loft combinations from the controlled 2026 LoftOptions samples.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of one measured loft sample: ResolvedFeatures
    /// stream version 0x70, the sample's stripped folder count, the 0x67 marker with value 0, and the
    /// sample's profiles/guide. Supported layouts: <c>loft_circle10_20</c> (Profile1/Profile2),
    /// <c>loft_three_sections</c> (Profile1/Profile2/Profile3), <c>loft_guide1</c>
    /// (Profile1/Profile2 + Guide1), and <c>loft_start_end_tangency</c> (Profile1/Profile2).
    /// <c>centerline: true</c> requires the measured <c>loft_centerline</c> stripped base and
    /// appends its measured centerline reference. The complete measured feature block is appended
    /// verbatim; selection references and other loft options remain measured values. Custom
    /// feature/sketch names or any other base are rejected.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base, sketches or name do not match a measured combination.</exception>
    public Feature AddLoft(
        string? featureName = null,
        IReadOnlyList<string>? profileSketchNames = null,
        string? guideSketchName = null,
        bool centerline = false)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        return AddMeasuredSweepOrLoft(
            "moBlend_c",
            featureName,
            profileSketchNames,
            null,
            guideSketchName,
            FeatureKind.Loft,
            centerline: centerline);
    }

    /// <summary>
    /// Adds one of the measured Surface Sweep combinations from the controlled 2026
    /// surface samples. The document must be the exact stripped base of one measured
    /// sample; the complete measured block is appended verbatim.
    /// </summary>
    /// <remarks>
    /// Supported layouts: <c>surf_sweep_basic</c> (diameter 0.006 m, 40 mm path),
    /// <c>surf_sweep_path60</c> (0.006 m, 60 mm path), and <c>surf_sweep_profile12</c>
    /// (0.012 m, 40 mm path). The path sketch is <c>Path1</c>; profile, merge, SweepType
    /// and all other options remain the measured values. Custom names or any other base
    /// are rejected before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base, sketch or name does not match a measured combination.</exception>
    public Feature AddSurfaceSweep(string? featureName = null, string? pathSketchName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        return AddMeasuredSweepOrLoft(
            "moSweepRefSurface_c",
            featureName,
            null,
            pathSketchName,
            null,
            FeatureKind.SurfaceSweep);
    }

    /// <summary>
    /// Adds one of the measured Surface Loft combinations from the controlled 2026
    /// surface samples. The document must be the exact stripped base of one measured
    /// sample; the complete measured block is appended verbatim.
    /// </summary>
    /// <remarks>
    /// Supported layouts: <c>surf_loft_2sections</c> (Profile1/Profile2),
    /// <c>surf_loft_3sections</c> (Profile1/Profile2/Profile3), and <c>surf_loft_guide</c>
    /// (Profile1/Profile2 + Guide1). Custom names or any other base are rejected before
    /// the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base, sketches or name do not match a measured combination.</exception>
    public Feature AddSurfaceLoft(
        string? featureName = null,
        IReadOnlyList<string>? profileSketchNames = null,
        string? guideSketchName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        return AddMeasuredSweepOrLoft(
            "moBlendRefSurface_c",
            featureName,
            profileSketchNames,
            null,
            guideSketchName,
            FeatureKind.SurfaceLoft);
    }

    /// <summary>
    /// Adds the measured Shell combination (one removed face, direction 0) on the exact
    /// stripped base of <c>shell_t2_remove_top</c> or <c>shell_t3_remove_top</c>.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of one ShellOptions sample: ResolvedFeatures
    /// stream version 0x70, stripped folder count 20, the measured 0x67 marker and the same
    /// normalized SHA-256. Only the thickness is parameterized; it is patched into the six
    /// validated thickness slots. Removed-face selection, direction, multi-thickness faces and
    /// all other fields remain the measured values. Any mismatch, an existing shell feature,
    /// a non-positive/oversized thickness or a custom name is rejected before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base or name does not match a measured Shell sample.</exception>
    public Feature AddShell(double thickness, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.ShellFeatureBuilder.Build(currentStream, thickness, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.Shell, "moShell_c", null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the measured DeleteBody combination (API default body selection: first body
    /// deleted, second kept) on the exact stripped base of <c>delete_body_second</c>.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of the sample: ResolvedFeatures stream
    /// version 0x71, stripped folder count 22 and the same normalized SHA-256. The measured
    /// feature block is appended verbatim; the body choice, KeepBodies and all other fields
    /// remain the measured default-selection values. A different body choice is not
    /// parameterizable with the current evidence. Custom names or any other base are rejected
    /// before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddDeleteBody(string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.DeleteBodyFeatureBuilder.Build(currentStream, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.DeleteBody, "moDeleteBody_c", null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the fixed measured Rib block from the official CHM block20 recipe artifact.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// Only the exact stripped base of the official artifact is accepted (pinned length,
    /// stream version, folder count and normalized SHA-256). The feature block is appended
    /// verbatim; no rib option is parameterized. Custom names or other bases are rejected.
    /// </remarks>
    public Feature AddRib(string? featureName = null)
        => AddOfficialChmFeature("moRib_c", FeatureKind.Rib, featureName);

    /// <summary>
    /// Adds the fixed measured CombineBodies block from the official CHM multi_inter recipe artifact.
    /// Call <see cref="SwFile.Save()"/> afterwards. Only the exact stripped base is accepted.
    /// </summary>
    public Feature AddCombineBodies(string? featureName = null)
        => AddOfficialChmFeature("moCombineBodies_c", FeatureKind.CombineBodies, featureName);

    /// <summary>
    /// Adds the fixed measured Split block from the official CHM multi_inter recipe artifact.
    /// Call <see cref="SwFile.Save()"/> afterwards. Only the exact stripped base is accepted.
    /// </summary>
    public Feature AddSplitBody(string? featureName = null)
        => AddOfficialChmFeature("moSplit_c", FeatureKind.SplitBody, featureName);

    /// <summary>
    /// Adds the fixed measured SurfaceTrim block from the official CHM SurfaceTrimFeature artifact.
    /// Call <see cref="SwFile.Save()"/> afterwards. Only the exact stripped base is accepted.
    /// </summary>
    public Feature AddSurfaceTrim(string? featureName = null)
        => AddOfficialChmFeature("moTrimRefSurface_c", FeatureKind.SurfaceTrim, featureName);

    /// <summary>
    /// Adds the fixed measured Gusset block at its original in-place position from the
    /// official CHM weldment_box3 artifact. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// Only the exact stripped base is accepted. Unlike the tail-appended official blocks,
    /// the Gusset block is inserted at the measured offset so the following feature blocks
    /// and their order are preserved. The rebuilt stream must be byte-identical to the
    /// measured native primary/resave stream.
    /// </remarks>
    public Feature AddGusset(string? featureName = null)
        => AddOfficialChmFeature("moGusset_c", FeatureKind.Gusset, featureName);

    private Feature AddOfficialChmFeature(
        string className, FeatureKind featureKind, string? featureName)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.OfficialChmFeatureBuilder.Build(
            currentStream, className, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, featureKind, className,
                null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the measured Draft combination (side face, 3 degrees, bottom neutral plane)
    /// on the exact stripped base of <c>draft_side_3deg_neutral_bottom</c>.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of the sample: ResolvedFeatures stream
    /// version 0x70, stripped folder count 20 and the same normalized SHA-256. The measured
    /// block is appended verbatim; angle, drafted face, neutral plane and all options remain
    /// the measured values. A 6-degree/outward/parting-line variant has no sample and is not
    /// parameterizable. Custom names or any other base are rejected before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddDraft(string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.DraftFeatureBuilder.Build(currentStream, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.Draft, "moDraft_c", null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the measured surface-extrude combination (Blind depth, no draft/cap) on the
    /// exact stripped base of <c>surf_extrude_10mm</c>. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of the sample: ResolvedFeatures stream
    /// version 0x70, stripped folder count 19, the measured 0x67 marker with value 0 and the
    /// same normalized SHA-256. Only the depth is parameterized (two validated slots); sketch
    /// selection, direction and every other option remain measured values. Custom names or any
    /// other base are rejected before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddSurfaceExtrude(double depth, string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.SurfaceExtrudeFeatureBuilder.Build(currentStream, depth, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.SurfaceExtrude, "moExtruRefSurface_c", null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the measured 360-degree surface-revolve combination on the exact stripped
    /// base of <c>surf_revolve_360</c>. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of the sample: ResolvedFeatures stream
    /// version 0x70, stripped folder count 19, the measured 0x67 marker with value 0 and the
    /// same normalized SHA-256. The measured block is appended verbatim; angle, axis/profile
    /// selections and all options remain measured values. Partial angles/custom names or any
    /// other base are rejected before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddSurfaceRevolve(string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.SurfaceRevolveFeatureBuilder.Build(currentStream, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.SurfaceRevolve, "moRevolvRefSurf_c", null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the measured planar-surface combination (closed rectangle region) on the exact
    /// stripped base of <c>surf_planar_region</c>. Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    /// <remarks>
    /// The document must be the exact stripped base of the sample: ResolvedFeatures stream
    /// version 0x70, stripped folder count 19, the measured 0x67 marker with value 0 and the
    /// same normalized SHA-256. The measured block is appended verbatim; the sketch boundary
    /// selection and generated face remain measured values. Custom names or any other base are
    /// rejected before the stream changes.
    /// </remarks>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddPlanarSurface(string? featureName = null)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.SurfacePlanarFeatureBuilder.Build(currentStream, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.PlanarSurface, "moPlanarSurface_c", null, null, null, null, false, true, null);
    }

    /// <summary>Adds the measured knit (two surface bodies to one) combination on its exact stripped base.</summary>
    public Feature AddSurfaceKnit(string? featureName = null) =>
        AddMeasuredSurfaceOperation("moSewRefSurface_c", FeatureKind.SurfaceKnit, featureName);

    /// <summary>Adds the measured 0.005 m edge-extend combination on its exact stripped base.</summary>
    public Feature AddSurfaceExtend(string? featureName = null) =>
        AddMeasuredSurfaceOperation("moExtendRefSurface_c", FeatureKind.SurfaceExtend, featureName);

    /// <summary>Adds the measured 0.005 m body-offset combination on its exact stripped base.</summary>
    public Feature AddSurfaceOffset(string? featureName = null) =>
        AddMeasuredSurfaceOperation("moOffsetRefSurface_c", FeatureKind.SurfaceOffset, featureName);

    /// <summary>Adds the measured closed-region fill-surface combination on its exact stripped base.</summary>
    public Feature AddFillSurface(string? featureName = null) =>
        AddMeasuredSurfaceOperation("moFillRefSurface_c", FeatureKind.FillSurface, featureName);

    /// <summary>
    /// Adds the measured closed-region fill-surface combination and patches the measured
    /// ResolutionControl slot (1 or 2). All other fill options remain measured values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">ResolutionControl is not 1 or 2.</exception>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddFillSurface(int resolutionControl, string? featureName = null) =>
        AddMeasuredFillSurface(resolutionControl, featureName);

    private Feature AddMeasuredFillSurface(int resolutionControl, string? featureName)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.SurfaceOperationFeatureBuilder.Build(
            currentStream, "moFillRefSurface_c", featureName, resolutionControl);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.FillSurface, "moFillRefSurface_c",
                null, null, null, null, false, true, null);
    }

    /// <summary>
    /// Adds the measured 2026 structural member (al tube square profile, one path segment)
    /// on its exact stripped base. The profile path and segment selection are fixed by the
    /// measured block.
    /// </summary>
    /// <exception cref="NotSupportedException">The base or name does not match the measured sample.</exception>
    public Feature AddStructuralMember(string? featureName = null) =>
        AddMeasuredStructuralMember(featureName, selected: false);

    /// <summary>
    /// Adds the measured 2026 structural member that was authored from an explicit
    /// segment selection (native feature name <c>StructuralMemberSelected</c>).
    /// </summary>
    public Feature AddStructuralMemberSelected(string? featureName = null) =>
        AddMeasuredStructuralMember(featureName, selected: true);

    /// <summary>
    /// Adds the measured nested EndCap feature (2 mm) on its exact stripped base.
    /// <paramref name="chamfer"/> selects the measured 1 mm chamfer/corner-treatment variant.
    /// </summary>
    public Feature AddEndCap(bool chamfer = false, string? featureName = null) =>
        AddMeasuredWeldmentChild("moEndCap_c", FeatureKind.EndCap, featureName, chamfer);

    /// <summary>
    /// Adds the measured nested cosmetic weld bead (0.003 m, two-edge reference) on its
    /// exact stripped base.
    /// </summary>
    public Feature AddWeldBead(string? featureName = null) =>
        AddMeasuredWeldmentChild("moCosmeticWeldBead_c", FeatureKind.WeldBead, featureName, null);

    private Feature AddMeasuredWeldmentChild(
        string className, FeatureKind featureKind, string? featureName, bool? chamfer)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.WeldmentChildFeatureBuilder.Build(
            currentStream, className, featureName, chamfer);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, featureKind, className,
                null, null, null, null, false, true, null);
    }

    private Feature AddMeasuredStructuralMember(string? featureName, bool selected)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.StructuralMemberFeatureBuilder.Build(
            currentStream, featureName, selected);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, FeatureKind.StructuralMember,
                StructuralMemberFeatureBuilder.StructuralMemberClassName,
                null, null, null, null, false, true, null);
    }

    private Feature AddMeasuredSurfaceOperation(string className, FeatureKind featureKind, string? featureName)
    {
        if (IsReadOnly)
            throw new InvalidOperationException("Cannot add feature to a read-only document.");

        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.SurfaceOperationFeatureBuilder.Build(
            currentStream, className, featureName);
        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, featureKind, className, null, null, null, null, false, true, null);
    }

    private Feature AddMeasuredSweepOrLoft(
        string className,
        string? featureName,
        IReadOnlyList<string>? requestedProfiles,
        string? requestedPath,
        string? requestedGuide,
        FeatureKind featureKind,
        bool? merge = null,
        int? twistControlType = null,
        double? twistAngleRad = null,
        bool centerline = false)
    {
        string streamPath = ResolvedFeaturesStreamPath ?? $"Contents/Config-{ActiveConfigurationIndex}-ResolvedFeatures";
        byte[]? currentStream = GetStreamBytes(streamPath) ?? ResolvedFeatures.RawData;
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("No ResolvedFeatures stream available in document.");

        var result = SldFile.Binary.FeatureData.SweepLoftFeatureBuilder.Build(
            currentStream,
            className,
            featureName,
            requestedProfiles,
            requestedPath,
            requestedGuide,
            merge,
            twistControlType,
            twistAngleRad,
            centerline);

        SetPendingStream(streamPath, result.Stream);
        ReParseResolvedFeatures(result.Stream);

        return Features.FirstOrDefault(f => string.Equals(f.Name, result.FeatureName, StringComparison.Ordinal))
            ?? new Feature(result.FeatureName, featureKind, className, null, null, null, null, false, true, null);
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
