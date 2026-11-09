using SldFile.Mfc;
using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Binary;

namespace SldFile;

public partial class SldPart
{
    /// <summary>
    /// Adds a sheet metal base flange feature based on a sketch.
    /// Call <see cref="SwFile.Save()"/> afterwards to persist the feature to disk.
    /// </summary>
    public Feature AddSheetMetalBaseFlange(
        SheetMetalBaseFlangeModel definition,
        string? targetSketchName = null,
        string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        SheetMetalFeatureBuilder.ValidateDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

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

        SetPendingStream(configStreamPath, patchedConfig);
        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Extrusion,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds one of the four fixed base-flange option templates measured on
    /// 2026-10-31 (KFactor 0.3, Direct 0.001, Deduction 0.001 or gauge table).
    /// Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    public Feature AddSheetMetalBaseFlangeFromOptionTemplate(
        SheetMetalBaseFlangeModel definition,
        string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        SheetMetalBaseFlangeOptionBuilder.SelectTemplate(definition);
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SheetMetalBaseFlangeOptionBuilder.DefaultFeatureName
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = SheetMetalBaseFlangeOptionBuilder.Build(
            currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Extrusion,
            definition.ClassName,
            (int)definition.TypeId,
            definition,
            matchKindWhenFinding: true);
    }

    /// <summary>
    /// Adds one of the fixed Jog/Fold/UnFold/FlatPattern templates recovered
    /// from the SolidWorks 2026 tutorial corpus (2026-10-31 scan). Call
    /// <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    public Feature AddSheetMetalTutorialFeature(
        TutorialSheetMetalFeatureTemplate template,
        string? featureName = null)
    {
        EnsureWritable();
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        bool isFlatPattern = template is
            TutorialSheetMetalFeatureTemplate.FlatPatternPaperAirplane or
            TutorialSheetMetalFeatureTemplate.FlatPatternComplexPart or
            TutorialSheetMetalFeatureTemplate.FlatPatternDriveSled;
        var newStream = isFlatPattern
            ? TutorialSheetMetalFeatureBuilder.BuildFlatPattern(currentStream, template, featureName)
            : TutorialSheetMetalFeatureBuilder.Build(currentStream, template, featureName);

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

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            displayName,
            kind,
            className,
            matchKindWhenFinding: true);
    }

    /// <summary>
    /// Adds the first controlled edge-flange combination to a stripped sheet-metal
    /// base specimen. Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    public Feature AddEdgeFlange(
        EdgeFlangeFeatureModel definition,
        string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        EdgeFlangeFeatureBuilder.ValidateDefinition(definition);
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? EdgeFlangeFeatureBuilder.DefaultFeatureName
            : featureName!;

        var newStream = EdgeFlangeFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Other,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds a fresh single-edge EdgeFlange using the measured 0x75 tutorial
    /// template (base flange + one EdgeFlange, ResolvedFeatures version 0x74).
    /// Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    public Feature AddFreshEdgeFlange(
        EdgeFlangeFeatureModel definition,
        string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? FreshEdgeFlangeAuthoringSegments.DisplayName
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = FreshEdgeFlangeAuthoringBuilder.Build(
            currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Other,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds the Paper Airplane tutorial BaseFlange with a patched custom bend
    /// table path. Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    public Feature AddTutorialBaseFlangeBendTable(
        string bendTableFile,
        string? featureName = null)
    {
        EnsureWritable();
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? TutorialBendTableAuthoringSegments.DisplayName
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = TutorialBendTableAuthoringBuilder.Build(
            currentStream, bendTableFile, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Extrusion,
            TutorialBendTableAuthoringSegments.ClassName);
    }

    /// <summary>
    /// Adds the controlled cross-bend SMNormalCut feature from the fixed 0x75
    /// template. Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    public Feature AddSMNormalCutFromTemplate(string? featureName = null)
    {
        EnsureWritable();
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SMNormalCutAuthoringSegments.DisplayName
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = SMNormalCutFeatureBuilder.Build(currentStream, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Other,
            SMNormalCutAuthoringSegments.ClassName,
            0x87);
    }

    /// <summary>
    /// Adds one of the two controlled hem combinations (Type 0 Open or Type 1
    /// Closed) to the matching stripped hem base specimen. Call
    /// <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    public Feature AddHem(
        HemFeatureModel definition,
        string? featureName = null)
    {
        EnsureWritable();
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        HemFeatureBuilder.ValidateDefinition(definition);
        int type = definition.Type!.Value;
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? SheetMetalFormingParsers.HemDisplayName(type)
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = HemFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.Hem,
            definition.ClassName,
            (int)definition.TypeId,
            definition);
    }

    /// <summary>
    /// Adds the fixed measured flat-pattern block from the controlled 0x5D
    /// specimen. Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    public Feature AddFlatPattern(
        FlatPatternFeatureModel? definition = null,
        string? featureName = null,
        bool? mergeFace = null,
        bool? simplifyBends = null,
        bool? cornerTreatment = null,
        int? breakCornerType = null,
        double? breakCornerRadius = null,
        int? cornerTrimReliefType = null,
        double? cornerTrimReliefDistance = null)
    {
        EnsureWritable();

        definition ??= new FlatPatternFeatureModel();
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

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = FlatPatternFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.FlatPattern,
            FlatPatternFeatureModel.SerializedClassName,
            (int)definition.TypeId,
            definition,
            matchKindWhenFinding: true);
    }

    /// <summary>
    /// Adds the inline FlatPattern 0x66 layout from
    /// sm_base_flange_after_fillet.SLDPRT (version 0x74, folder count 25) and
    /// patches the proven option slots. Call <see cref="SwFile.Save"/> afterwards.
    /// </summary>
    public Feature AddFlatPatternInline066(
        FlatPatternFeatureModel? definition = null,
        string? featureName = null)
    {
        EnsureWritable();
        definition ??= new FlatPatternFeatureModel();
        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? FlatPatternInline066AuthoringSegments.DisplayName
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = FlatPatternInline066FeatureBuilder.Build(
            currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.FlatPattern,
            FlatPatternFeatureModel.SerializedClassName,
            0x66,
            definition,
            matchKindWhenFinding: true);
    }

    /// <summary>
    /// Adds the fixed measured lofted-bend block from the controlled 0x72 specimen
    /// to its stripped base. Call <see cref="SwFile.Save()"/> afterwards to persist it.
    /// </summary>
    public Feature AddLoftedBend(
        LoftedBendFeatureModel? definition = null,
        string? featureName = null)
    {
        EnsureWritable();
        definition ??= new LoftedBendFeatureModel();
        LoftedBendFeatureBuilder.ValidateDefinition(definition);

        string resolvedFeatureName = string.IsNullOrWhiteSpace(featureName)
            ? LoftedBendFeatureBuilder.DefaultFeatureName
            : featureName!;

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var newStream = LoftedBendFeatureBuilder.Build(currentStream, definition, resolvedFeatureName);

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedFeatureName,
            FeatureKind.LoftedBend,
            LoftedBendFeatureModel.SerializedClassName,
            (int)definition.TypeId,
            definition,
            matchKindWhenFinding: true);
    }

    /// <summary>
    /// Adds a Sweep combination using a strongly typed SweepFeatureModel.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddSweep(
        string? featureName = null,
        string? profileSketchName = null,
        string? pathSketchName = null,
        string? guideSketchName = null,
        bool? merge = null,
        int? twistControlType = null,
        double? twistAngleRad = null)
    {
        EnsureWritable();
        var model = new SweepFeatureModel
        {
            FeatureName = featureName ?? "扫描1",
            ProfileReference = profileSketchName != null ? new MfcSketchCurveReference(0, SketchCurveRole.Profile) : null,
            PathReference = pathSketchName != null ? new MfcSketchCurveReference(0, SketchCurveRole.Path) : null,
            Merge = merge,
            TwistControlType = twistControlType,
            TwistAngleRad = twistAngleRad
        };
        return AddSweep(model);
    }

    /// <summary>
    /// Adds a Loft combination using a strongly typed LoftFeatureModel.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddLoft(
        string? featureName = null,
        IReadOnlyList<string>? profileSketchNames = null,
        string? guideSketchName = null,
        bool centerline = false)
    {
        EnsureWritable();
        var model = new LoftFeatureModel
        {
            FeatureName = featureName ?? "放样1",
            CenterlineReference = centerline ? new MfcSketchCurveReference(0, SketchCurveRole.Guide) : null
        };
        if (profileSketchNames != null)
        {
            foreach (var _ in profileSketchNames)
                model.ProfileReferences.Add(new MfcSketchCurveReference(0, SketchCurveRole.Profile));
        }
        return AddLoft(model);
    }

    /// <summary>
    /// Adds a Surface Sweep combination using a strongly typed SweepFeatureModel.
    /// </summary>
    public Feature AddSurfaceSweep(string? featureName = null, string? pathSketchName = null)
    {
        EnsureWritable();
        var model = new SweepFeatureModel
        {
            FeatureName = featureName ?? "曲面-扫描1",
            IsSurface = true,
            PathReference = pathSketchName != null ? new MfcSketchCurveReference(0, SketchCurveRole.Path) : null
        };
        return AddSweep(model);
    }

    /// <summary>
    /// Adds a Surface Loft combination using a strongly typed LoftFeatureModel.
    /// </summary>
    public Feature AddSurfaceLoft(
        string? featureName = null,
        IReadOnlyList<string>? profileSketchNames = null,
        string? guideSketchName = null)
    {
        EnsureWritable();
        var model = new LoftFeatureModel
        {
            FeatureName = featureName ?? "曲面-放样1",
            IsSurface = true
        };
        if (profileSketchNames != null)
        {
            foreach (var _ in profileSketchNames)
                model.ProfileReferences.Add(new MfcSketchCurveReference(0, SketchCurveRole.Profile));
        }
        return AddLoft(model);
    }

    /// <summary>
    /// Adds a Shell feature with the given thickness.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddShell(double thickness, string? featureName = null)
    {
        EnsureWritable();
        var model = new ShellFeatureModel(thickness)
        {
            FeatureName = featureName ?? "抽壳1"
        };
        return AddShell(model);
    }

    /// <summary>
    /// Adds a DeleteBody feature.
    /// Call <see cref="SwFile.Save()"/> afterwards.
    /// </summary>
    public Feature AddDeleteBody(string? featureName = null)
    {
        EnsureWritable();
        var model = new DeleteBodyFeatureModel
        {
            FeatureName = featureName ?? "删除实体1"
        };
        return AddDeleteBody(model);
    }

    /// <summary>Adds a Rib feature using a strongly typed RibFeatureModel.</summary>
    public Feature AddRib(string? featureName = null)
    {
        EnsureWritable();
        var model = new RibFeatureModel
        {
            FeatureName = featureName ?? "筋1"
        };
        return AddRib(model);
    }

    /// <summary>Adds a CombineBodies feature using a strongly typed CombineBodiesFeatureModel.</summary>
    public Feature AddCombineBodies(string? featureName = null)
    {
        EnsureWritable();
        var model = new CombineBodiesFeatureModel
        {
            FeatureName = featureName ?? "组合1"
        };
        return AddCombineBodies(model);
    }

    /// <summary>Adds a SplitBody feature using a strongly typed SplitBodyFeatureModel.</summary>
    public Feature AddSplitBody(string? featureName = null)
    {
        EnsureWritable();
        var model = new SplitBodyFeatureModel
        {
            FeatureName = featureName ?? "分割1"
        };
        return AddSplitBody(model);
    }

    /// <summary>Adds a SurfaceTrim feature using a strongly typed SurfaceTrimFeatureModel.</summary>
    public Feature AddSurfaceTrim(string? featureName = null)
    {
        EnsureWritable();
        var model = new SurfaceTrimFeatureModel
        {
            FeatureName = featureName ?? "曲面-剪裁1"
        };
        return AddSurfaceTrim(model);
    }

    /// <summary>Adds a Gusset feature using a strongly typed GussetFeatureModel.</summary>
    public Feature AddGusset(string? featureName = null)
    {
        EnsureWritable();
        var model = new GussetFeatureModel
        {
            FeatureName = featureName ?? "瑙掓拺鏉?"
        };
        return AddGusset(model);
    }
    public Feature AddGusset(GussetFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.Gusset, GussetFeatureModel.ClassNameConstant, "角撑板");
    }

    /// <summary>
    /// Adds a Draft feature using a strongly typed DraftFeatureModel.
    /// </summary>
    public Feature AddDraft(string? featureName = null)
    {
        EnsureWritable();
        var model = new DraftFeatureModel
        {
            FeatureName = featureName ?? "拔模1"
        };
        return AddDraft(model);
    }

    /// <summary>Adds a Sweep feature using a strongly typed SweepFeatureModel.</summary>
    public Feature AddSweep(SweepFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        var kind = model.IsCut ? FeatureKind.Cut : (model.IsSurface ? FeatureKind.SurfaceSweep : FeatureKind.Sweep);
        return AddMfcFeature(model, kind, "moSweep_c", "扫描");
    }

    /// <summary>Adds a Loft feature using a strongly typed LoftFeatureModel.</summary>
    public Feature AddLoft(LoftFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        var kind = model.IsCut ? FeatureKind.Cut : (model.IsSurface ? FeatureKind.SurfaceLoft : FeatureKind.Loft);
        return AddMfcFeature(model, kind, "moBlend_c", "放样");
    }

    /// <summary>Adds a Shell feature using a strongly typed ShellFeatureModel.</summary>
    public Feature AddShell(ShellFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.Shell, "moShell_c", "抽壳");
    }

    /// <summary>Adds a Draft feature using a strongly typed DraftFeatureModel.</summary>
    public Feature AddDraft(DraftFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.Draft, "moDraft_c", "拔模");
    }

    /// <summary>Adds a CombineBodies feature using a strongly typed CombineBodiesFeatureModel.</summary>
    public Feature AddCombineBodies(CombineBodiesFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.CombineBodies, "moCombineBodies_c", "组合");
    }

    /// <summary>Adds a SplitBody feature using a strongly typed SplitBodyFeatureModel.</summary>
    public Feature AddSplitBody(SplitBodyFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.SplitBody, "moSplit_c", "分割");
    }

    /// <summary>Adds a DeleteBody feature using a strongly typed DeleteBodyFeatureModel.</summary>
    public Feature AddDeleteBody(DeleteBodyFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.DeleteBody, "moDeleteBody_c", "删除实体");
    }

    /// <summary>Adds a Rib feature using a strongly typed RibFeatureModel.</summary>
    public Feature AddRib(RibFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.Rib, "moRib_c", "筋");
    }

    /// <summary>Adds a Surface Knit feature using a strongly typed SurfaceKnitFeatureModel.</summary>
    public Feature AddSurfaceKnit(SurfaceKnitFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.SurfaceKnit, "moSewRefSurface_c", "曲面-缝合");
    }

    /// <summary>Adds a Surface Extend feature using a strongly typed SurfaceExtendFeatureModel.</summary>
    public Feature AddSurfaceExtend(SurfaceExtendFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.SurfaceExtend, "moExtendRefSurface_c", "曲面-延伸");
    }

    /// <summary>Adds a Surface Offset feature using a strongly typed SurfaceOffsetFeatureModel.</summary>
    public Feature AddSurfaceOffset(SurfaceOffsetFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.SurfaceOffset, "moOffsetRefSurface_c", "等距曲面");
    }

    /// <summary>Adds a Surface Fill feature using a strongly typed SurfaceFillFeatureModel.</summary>
    public Feature AddSurfaceFill(SurfaceFillFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.FillSurface, "moFillRefSurface_c", "填充曲面");
    }

    /// <summary>Adds a Surface Trim feature using a strongly typed SurfaceTrimFeatureModel.</summary>
    public Feature AddSurfaceTrim(SurfaceTrimFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.SurfaceTrim, "moTrimRefSurface_c", "曲面-剪裁");
    }

    /// <summary>Adds a Planar Surface feature using a strongly typed PlanarSurfaceFeatureModel.</summary>
    public Feature AddPlanarSurface(PlanarSurfaceFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.PlanarSurface, "moPlanarSurface_c", "平面区域");
    }

    private Feature AddMfcFeature<T>(T model, FeatureKind kind, string defaultClassName, string defaultPrefix)
        where T : IMfcFeatureModel, IMfcSerializable
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));

        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        if (currentStream == null || currentStream.Length < 16)
            throw new InvalidOperationException("Invalid ResolvedFeatures stream data.");

        string resolvedName = GetModelFeatureName(model) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedName) ||
            Features.Any(f => string.Equals(f.Name, resolvedName, StringComparison.Ordinal)))
        {
            int count = Features.Count(f => f.Kind == kind || f.Name.StartsWith(defaultPrefix, StringComparison.Ordinal));
            resolvedName = $"{defaultPrefix}{count + 1}";
            while (Features.Any(f => string.Equals(f.Name, resolvedName, StringComparison.Ordinal)))
            {
                count++;
                resolvedName = $"{defaultPrefix}{count + 1}";
            }
            SetModelFeatureName(model, resolvedName);
        }

        byte[] featureBlock;
        using (var ar = MfcArchive.CreateWriter(1024))
        {
            model.Serialize(ar);
            featureBlock = ar.ToArray();
        }

        ushort folderCount = currentStream.Length >= 6 ? BitConverter.ToUInt16(currentStream, 4) : (ushort)0;
        byte[] newStream = new byte[currentStream.Length + featureBlock.Length];
        Buffer.BlockCopy(currentStream, 0, newStream, 0, currentStream.Length);
        BitConverter.GetBytes((ushort)(folderCount + 1)).CopyTo(newStream, 4);
        Buffer.BlockCopy(featureBlock, 0, newStream, currentStream.Length, featureBlock.Length);

        string cls = !string.IsNullOrEmpty(model.ClassName) ? model.ClassName : defaultClassName;
        int typeId = (int)model.TypeId;
        if (typeId == 0)
        {
            typeId = model switch
            {
                ShellFeatureModel => (int)ShellFeatureModel.HeaderTypeId,
                DraftFeatureModel => (int)DraftFeatureModel.HeaderTypeId,
                CombineBodiesFeatureModel => (int)CombineBodiesFeatureModel.StandardTypeId,
                SplitBodyFeatureModel => (int)SplitBodyFeatureModel.StandardTypeId,
                DeleteBodyFeatureModel => (int)DeleteBodyFeatureModel.StandardTypeId,
                RibFeatureModel => (int)RibFeatureModel.StandardTypeId,
                SurfaceKnitFeatureModel => (int)SurfaceKnitFeatureModel.HeaderTypeId,
                SurfaceExtendFeatureModel => (int)SurfaceExtendFeatureModel.HeaderTypeId,
                SurfaceOffsetFeatureModel => (int)SurfaceOffsetFeatureModel.HeaderTypeId,
                SurfaceFillFeatureModel => (int)SurfaceFillFeatureModel.HeaderTypeId,
                SurfaceTrimFeatureModel => (int)SurfaceTrimFeatureModel.HeaderTypeId,
                PlanarSurfaceFeatureModel => (int)PlanarSurfaceFeatureModel.HeaderTypeId,
                StructuralMemberFeatureModel => (int)StructuralMemberFeatureModel.HeaderTypeId,
                EndCapFeatureModel => (int)EndCapFeatureModel.HeaderTypeId,
                CosmeticWeldBeadFeatureModel => (int)CosmeticWeldBeadFeatureModel.HeaderTypeId,
                WeldBeadFeatureModel => (int)WeldBeadFeatureModel.HeaderTypeId,
                GussetFeatureModel => 0x2A,
                _ => 0
            };
        }

        return CommitResolvedFeaturesStream(
            streamPath,
            newStream,
            resolvedName,
            kind,
            cls,
            typeId,
            model);
    }

    private static string? GetModelFeatureName(IMfcFeatureModel model) => model switch
    {
        SweepFeatureModel sw => sw.FeatureName,
        LoftFeatureModel lf => lf.FeatureName,
        ShellFeatureModel sh => sh.FeatureName,
        DraftFeatureModel df => df.FeatureName,
        CombineBodiesFeatureModel cb => cb.FeatureName,
        SplitBodyFeatureModel sp => sp.FeatureName,
        DeleteBodyFeatureModel db => db.FeatureName,
        RibFeatureModel rb => rb.FeatureName,
        SurfaceKnitFeatureModel knit => knit.FeatureName,
        SurfaceExtendFeatureModel ext => ext.FeatureName,
        SurfaceOffsetFeatureModel off => off.FeatureName,
        SurfaceFillFeatureModel fill => fill.FeatureName,
        SurfaceTrimFeatureModel trim => trim.FeatureName,
        PlanarSurfaceFeatureModel planar => planar.FeatureName,
        StructuralMemberFeatureModel sm => sm.FeatureName,
        EndCapFeatureModel ec => ec.FeatureName,
        WeldBeadFeatureModel wb => wb.FeatureName,
        GussetFeatureModel gu => gu.FeatureName,
        _ => null
    };

    private static void SetModelFeatureName(IMfcFeatureModel model, string name)
    {
        switch (model)
        {
            case SweepFeatureModel sw: sw.FeatureName = name; break;
            case LoftFeatureModel lf: lf.FeatureName = name; break;
            case ShellFeatureModel sh: sh.FeatureName = name; break;
            case DraftFeatureModel df: df.FeatureName = name; break;
            case CombineBodiesFeatureModel cb: cb.FeatureName = name; break;
            case SplitBodyFeatureModel sp: sp.FeatureName = name; break;
            case DeleteBodyFeatureModel db: db.FeatureName = name; break;
            case RibFeatureModel rb: rb.FeatureName = name; break;
            case SurfaceKnitFeatureModel knit: knit.FeatureName = name; break;
            case SurfaceExtendFeatureModel ext: ext.FeatureName = name; break;
            case SurfaceOffsetFeatureModel off: off.FeatureName = name; break;
            case SurfaceFillFeatureModel fill: fill.FeatureName = name; break;
            case SurfaceTrimFeatureModel trim: trim.FeatureName = name; break;
            case PlanarSurfaceFeatureModel planar: planar.FeatureName = name; break;
            case StructuralMemberFeatureModel sm: sm.FeatureName = name; break;
            case EndCapFeatureModel ec: ec.FeatureName = name; break;
            case WeldBeadFeatureModel wb: wb.FeatureName = name; break;
            case GussetFeatureModel gu: gu.FeatureName = name; break;
        }
    }

    /// <summary>
    /// Adds the measured surface-extrude combination (Blind depth, no draft/cap) on the
    /// exact stripped base of <c>surf_extrude_10mm</c>.
    /// </summary>
    public Feature AddSurfaceExtrude(double depth, string? featureName = null)
    {
        EnsureWritable();
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var result = SurfaceExtrudeFeatureBuilder.Build(currentStream, depth, featureName);
        return CommitResolvedFeaturesStream(
            streamPath, result.Stream, result.FeatureName, FeatureKind.SurfaceExtrude, "moExtruRefSurface_c");
    }

    /// <summary>
    /// Adds the measured 360-degree surface-revolve combination on the exact stripped
    /// base of <c>surf_revolve_360</c>.
    /// </summary>
    public Feature AddSurfaceRevolve(string? featureName = null)
    {
        EnsureWritable();
        var (streamPath, currentStream) = GetActiveResolvedFeaturesStream();
        var result = SurfaceRevolveFeatureBuilder.Build(currentStream, featureName);
        return CommitResolvedFeaturesStream(
            streamPath, result.Stream, result.FeatureName, FeatureKind.SurfaceRevolve, "moRevolvRefSurf_c");
    }

    /// <summary>
    /// Adds a planar-surface feature.
    /// </summary>
    public Feature AddPlanarSurface(string? featureName = null) =>
        AddPlanarSurface(new PlanarSurfaceFeatureModel { FeatureName = featureName ?? "平面区域1" });

    /// <summary>Adds a knit surface feature.</summary>
    public Feature AddSurfaceKnit(string? featureName = null) =>
        AddSurfaceKnit(new SurfaceKnitFeatureModel { FeatureName = featureName ?? "曲面-缝合1" });

    /// <summary>Adds an edge-extend surface feature.</summary>
    public Feature AddSurfaceExtend(string? featureName = null) =>
        AddSurfaceExtend(new SurfaceExtendFeatureModel { FeatureName = featureName ?? "曲面-延伸1" });

    /// <summary>Adds a body-offset surface feature.</summary>
    public Feature AddSurfaceOffset(string? featureName = null) =>
        AddSurfaceOffset(new SurfaceOffsetFeatureModel { FeatureName = featureName ?? "等距曲面1" });

    /// <summary>Adds a fill-surface feature.</summary>
    public Feature AddFillSurface(string? featureName = null) =>
        AddSurfaceFill(new SurfaceFillFeatureModel { FeatureName = featureName ?? "填充曲面1" });

    /// <summary>
    /// Adds a fill-surface feature with patched resolution control.
    /// </summary>
    public Feature AddFillSurface(int resolutionControl, string? featureName = null) =>
        AddSurfaceFill(new SurfaceFillFeatureModel { FeatureName = featureName ?? "填充曲面1", ResolutionControl = resolutionControl });

    /// <summary>
    /// Adds a structural member feature.
    /// </summary>
    public Feature AddStructuralMember(string? featureName = null) =>
        AddStructuralMember(new StructuralMemberFeatureModel { FeatureName = featureName ?? "结构构件1" });

    /// <summary>
    /// Adds a structural member feature authored with selected segment name.
    /// </summary>
    public Feature AddStructuralMemberSelected(string? featureName = null) =>
        AddStructuralMember(new StructuralMemberFeatureModel { FeatureName = featureName ?? "StructuralMemberSelected" });

    public Feature AddStructuralMember(StructuralMemberFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.StructuralMember, StructuralMemberFeatureModel.MfcClassName, "结构构件");
    }

    /// <summary>
    /// Adds an EndCap feature.
    /// </summary>
    public Feature AddEndCap(bool chamfer = false, string? featureName = null) =>
        AddEndCap(new EndCapFeatureModel { FeatureName = featureName ?? "端盖1" });

    public Feature AddEndCap(EndCapFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.EndCap, EndCapFeatureModel.MfcClassName, "端盖");
    }

    /// <summary>
    /// Adds a cosmetic weld bead feature.
    /// </summary>
    public Feature AddWeldBead(string? featureName = null) =>
        AddWeldBead(new CosmeticWeldBeadFeatureModel { FeatureName = featureName ?? "焊缝1" });

    public Feature AddWeldBead(WeldBeadFeatureModel model)
    {
        EnsureWritable();
        if (model == null) throw new ArgumentNullException(nameof(model));
        return AddMfcFeature(model, FeatureKind.WeldBead, CosmeticWeldBeadFeatureModel.MfcClassName, "焊缝");
    }
}
