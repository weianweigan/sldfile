using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

using SldFile.Binary;

namespace SldFile.Mfc;

/// <summary>
/// Central registry that maps MFC class names to their feature-data parsers.
/// Add new feature types by calling <see cref="Register"/> or <see cref="RegisterFeature"/> in the static constructor.
/// </summary>
internal static class MfcFeatureRegistry
{
    private static readonly Dictionary<string, Func<MfcElement, IFeatureData?>> _parsers
        = new(StringComparer.Ordinal);

    // Feature-level parsers need the owning document and feature range, not a single element.
    private static readonly Dictionary<string, Func<MfcDocument, string, uint, int, int, IFeatureData?>> _featureParsers
        = new(StringComparer.Ordinal);

    static MfcFeatureRegistry()
    {
        // ── Folder classes with extra data beyond the 90-byte FolderHeader ──
        Register("moLengthParameter_ct", FeatureDataParsers.ParseLengthParameter);

        // ── Reference geometry ────────────────────────────────────────────
        Register("moDefaultRefPlnData_c", FeatureDataParsers.ParseDefaultRefPlane);
        Register("moCompRefPlane_c", FeatureDataParsers.ParseCompRefPlane);

        // ── Sketch & sketch entities ──────────────────────────────────────
        Register("sgSketch", FeatureDataParsers.ParseSketch);
        Register("sgPointHandle", FeatureDataParsers.ParsePointHandle);
        Register("sgLineHandle", FeatureDataParsers.ParseLineHandle);
        Register("sgArcHandle", FeatureDataParsers.ParseArcHandle);
        Register("sgCircleDim", FeatureDataParsers.ParseCircleDim);
        Register("sgPntPntDist", FeatureDataParsers.ParsePntPntDist);
        Register("sgExtEnt_c", FeatureDataParsers.ParseExtEnt);
        Register("moSketchExtRef_w", FeatureDataParsers.ParseSketchExtRef);
        Register("moCompSketchEntHandle_c", FeatureDataParsers.ParseCompSketchEntHandle);
        Register("sgEntHandle", FeatureDataParsers.ParseEntHandle);

        // ── Sketch chain ──────────────────────────────────────────────────
        Register("moSketchChain_c", FeatureDataParsers.ParseSketchChain);

        // ── Dimensions ────────────────────────────────────────────────────
        Register("moDisplayDistanceDim_c", FeatureDataParsers.ParseDisplayDistanceDim);
        Register("moDisplayRadialDim_c", FeatureDataParsers.ParseDisplayRadialDim);
        Register("moSkDimHandleValG2_c", FeatureDataParsers.ParseSkDimHandleValG2);
        Register("moSkDimHandleRadial_c", FeatureDataParsers.ParseSkDimHandleRadial);
        Register("moFeatureDimHandle_c", FeatureDataParsers.ParseFeatureDimHandle);
        Register("ParallelPlaneDistanceDim_c", FeatureDataParsers.ParseParallelPlaneDistanceDim);
        Register("moFavoriteHandle_c", FeatureDataParsers.ParseFavoriteHandle);

        // ── .ske-specific sketch dimension classes ───────────────────────
        Register("sgPntLineDist", FeatureDataParsers.ParsePntLineDist);
        Register("sgLLDist", FeatureDataParsers.ParseLLDistance);
        Register("sgAnglDim", FeatureDataParsers.ParseAngularDim);
        Register("moNodeName_c", FeatureDataParsers.ParseNodeName);
        Register("moLengthParameter_c", FeatureDataParsers.ParseLengthParameterC);
        Register("moAngleParameter_c", FeatureDataParsers.ParseAngleParameterC);
        Register("moDisplayAngularDim_c", FeatureDataParsers.ParseDisplayAngularDim);
        Register("suObList", FeatureDataParsers.ParseObList);

        // ── Phase 3.1 sweep / loft (header structure validated by 2026 samples) ──
        Register(SweepFeatureModel.BossClassName, SweepFeatureModel.Parse);
        Register(SweepFeatureModel.CutClassName, SweepFeatureModel.Parse);
        Register(SweepFeatureModel.SurfaceClassName, SweepFeatureModel.Parse);
        Register(LoftFeatureModel.BossClassName, LoftFeatureModel.Parse);
        Register(LoftFeatureModel.CutClassName, LoftFeatureModel.Parse);
        Register(LoftFeatureModel.SurfaceClassName, LoftFeatureModel.Parse);
        // Legacy tutorial sweep fallback; only invoked when the modern parser returned null.
        RegisterFeature(SweepFeatureModel.BossClassName, SweepFeatureModel.ParseLegacySweep);

        // ── Phase 3.2 shell (measured 2026 layout; thickness + one removed face) ──
        Register(ShellFeatureModel.MfcClassName, ShellFeatureModel.Parse);

        // ── Phase 3.3 delete body (measured 2026 default-selection layout) ──
        Register(DeleteBodyFeatureModel.MfcClassName, DeleteBodyFeatureModel.Parse);

        // ── Phase 3.2 draft (measured 2026 3-degree neutral-plane layout) ──
        Register(DraftFeatureModel.MfcClassName, DraftFeatureModel.Parse);

        // ── Legacy tutorial samples (stream versions 0x006D-0x0334) ─────
        RegisterFeature(RibFeatureModel.MfcClassName, RibFeatureModel.Parse);
        RegisterFeature(GussetFeatureModel.ClassNameConstant, GussetFeatureModel.Parse);
        RegisterFeature(CombineBodiesFeatureModel.MfcClassName, CombineBodiesFeatureModel.Parse);
        RegisterFeature(SplitBodyFeatureModel.LegacyClassName, SplitBodyFeatureModel.Parse);
        RegisterFeature(SplitBodyFeatureModel.ModernClassName, SplitBodyFeatureModel.Parse);
        RegisterFeature(SurfaceTrimFeatureModel.MfcClassName, (doc, _, _, start, _) =>
        {
            var elem = doc.AllElements.FirstOrDefault(e => e.TagOffset == start);
            return elem == null ? null : SurfaceTrimFeatureModel.Parse(elem);
        });
        RegisterFeature(StructuralMemberFeatureModel.LegacyClassName, StructuralMemberFeatureModel.ParseLegacy);

        // ── Phase 4.1 surface extrude / revolve / planar ──────────────────
        Register(SurfaceExtrudeFeatureParser.ClassName, SurfaceExtrudeFeatureParser.Parse);
        Register(SurfaceRevolveFeatureParser.ClassName, SurfaceRevolveFeatureParser.Parse);
        Register(PlanarSurfaceFeatureModel.MfcClassName, PlanarSurfaceFeatureModel.Parse);

        // ── Phase 4.2/4.3 surface operations (measured 2026 fixed layouts) ──
        Register(SurfaceKnitFeatureModel.MfcClassName, SurfaceKnitFeatureModel.Parse);
        Register(SurfaceExtendFeatureModel.MfcClassName, SurfaceExtendFeatureModel.Parse);
        Register(SurfaceOffsetFeatureModel.MfcClassName, SurfaceOffsetFeatureModel.Parse);
        Register(SurfaceFillFeatureModel.MfcClassName, SurfaceFillFeatureModel.Parse);
        Register(SurfaceTrimFeatureModel.MfcClassName, SurfaceFeatureParsers.ParseSurfaceTrim);

        // ── Phase 4.4 surface sweep / surface loft (measured 2026 layouts) ──
        Register(SurfaceSweepFeatureModel.MfcClassName, elem => SurfaceSweepFeatureModel.Parse(elem) ?? (IFeatureData?)SweepFeatureModel.Parse(elem));
        Register(SurfaceLoftFeatureModel.MfcClassName, elem => SurfaceLoftFeatureModel.Parse(elem) ?? (IFeatureData?)LoftFeatureModel.Parse(elem));

        // ── Phase 5 structural member (measured 2026 weldment layout) ──
        Register(StructuralMemberFeatureModel.MfcClassName, StructuralMemberFeatureModel.Parse);
        Register(EndCapFeatureModel.MfcClassName, EndCapFeatureModel.Parse);
        Register(CosmeticWeldBeadFeatureModel.MfcClassName, CosmeticWeldBeadFeatureModel.Parse);

        // ── Phase 2.2 edge / miter flange recognition ─────────────────────
        Register(EdgeFlangeFeatureModel.SerializedClassName, EdgeFlangeFeatureModel.Parse);
        Register(MiterFlangeFeatureModel.SerializedClassName, MiterFlangeFeatureModel.Parse);

        // ── Task 2.3 / 2.4 forming, normal cut, flat pattern, fold recognition ──
        Register(SheetMetalFormingParsers.HemClassName, SheetMetalFormingParsers.ParseHem);
        Register(SheetMetalFormingParsers.JogClassName, SheetMetalFormingParsers.ParseJog);
        Register(SheetMetalFormingParsers.LoftedBendClassName, SheetMetalFormingParsers.ParseLoftedBend);
        Register(SheetMetalFormingParsers.NormalCutClassName, SheetMetalFormingParsers.ParseNormalCut);
        Register(SheetMetalFormingParsers.FlatPatternClassName, SheetMetalFormingParsers.ParseFlatPattern);
        Register(SheetMetalFormingParsers.FoldClassName, SheetMetalFormingParsers.ParseFold);
        Register(SheetMetalFormingParsers.UnFoldClassName, SheetMetalFormingParsers.ParseUnFold);

        // Feature-window parsers for SheetMetalFormingParsers
        RegisterElementWindowFeature(SheetMetalFormingParsers.HemClassName, SheetMetalFormingParsers.ParseHem);
        RegisterElementWindowFeature(SheetMetalFormingParsers.JogClassName, SheetMetalFormingParsers.ParseJog);
        RegisterElementWindowFeature(SheetMetalFormingParsers.FoldClassName, SheetMetalFormingParsers.ParseFold);
        RegisterElementWindowFeature(SheetMetalFormingParsers.UnFoldClassName, SheetMetalFormingParsers.ParseUnFold);
        RegisterElementWindowFeature(SheetMetalFormingParsers.LoftedBendClassName, SheetMetalFormingParsers.ParseLoftedBend);
        RegisterFeature(SheetMetalFormingParsers.FlatPatternClassName, (doc, _, _, start, _) =>
        {
            var elem = doc.AllElements.FirstOrDefault(e => e.TagOffset == start);
            return elem == null ? null : SheetMetalFormingParsers.ParseFlatPattern(doc, elem, doc.SourceData.Length);
        });

        // ── Revolve feature-window parsers ────────────────────────────────
        RegisterFeature("moRevolution_c", RevolveFeatureParser.Parse);
        RegisterFeature("moRevolutionThin_c", RevolveFeatureParser.Parse);
        RegisterFeature("moRevCut_c", RevolveFeatureParser.Parse);

        // ── Task 1.3/1.4/1.5 controlled fillet/chamfer, hole wizard, and patterns ──
        RegisterFeature(FilletChamferFeatureParser.FilletClassName, FilletChamferFeatureParser.ParseFillet);
        RegisterFeature(FilletChamferFeatureParser.VariableFilletClassName, FilletChamferFeatureParser.ParseVariableFillet);
        RegisterFeature(HoleWizardFeatureParser.HoleWizardClassName, HoleWizardFeatureParser.ParseHoleWizard);
        RegisterFeature(FilletChamferFeatureParser.ChamferClassName, FilletChamferFeatureParser.ParseChamfer);
        RegisterFeature(PatternFeatureParser.LinearPatternClassName, PatternFeatureParser.ParseLinearPattern);
        RegisterFeature(PatternFeatureParser.CircularPatternClassName, PatternFeatureParser.ParseCircularPattern);
        RegisterFeature(PatternFeatureParser.MirrorPatternClassName, PatternFeatureParser.ParseMirrorPattern);

        // ── Extrusion sub-objects ─────────────────────────────────────────
        Register("moPerBodyChooserData_c", FeatureDataParsers.ParsePerBodyChooser);
        Register("moFaceRef_c", FeatureDataParsers.ParseFaceRef);
        Register("moEndFaceSurfIdRep_c", FeatureDataParsers.ParseEndFaceSurfIdRep);
        Register("moFR_c", FeatureDataParsers.ParseFr);
        Register("moFromSktEntSurfIdRep_c", FeatureDataParsers.ParseFromSktEntSurfIdRep);
        Register("moBBoxCenterData_c", FeatureDataParsers.ParseBBoxCenter);
        Register("moEndSpec_c", FeatureDataParsers.ParseEndSpec);
        Register("moFromEndSpec_c", FeatureDataParsers.ParseFromEndSpec);

        // ── Persistent Topology & Selection primitives ────────────────────
        Register("moSketchRegion_c", TopologyParsers.ParseSketchRegion);
        Register("moEdgeRef_c", TopologyParsers.ParseEdgeRef);
        Register("moCompEdge_c", TopologyParsers.ParseCompEdge);
        Register("moFaceRefPlnData_c", TopologyParsers.ParseFaceRefPln);
        Register("moCompFace_c", TopologyParsers.ParseCompFace);
        Register("moCompSolidBody_c", TopologyParsers.ParseCompSolidBody);
        Register("moSurfaceIdRep_c", TopologyParsers.ParseSurfaceIdRep);
        Register("moEndFace3IntSurfIdRep_c", TopologyParsers.ParseEndFace3IntSurfIdRep);
        Register("moFromSktEnt3IntSurfIdRep_c", TopologyParsers.ParseFromSktEnt3IntSurfIdRep);
        Register("moFilletSurfIdRep_c", TopologyParsers.ParseFilletSurfIdRep);
    }

    private static void RegisterElementWindowFeature(
        string className,
        Func<MfcDocument, MfcElement, int, IFeatureData?> parser)
    {
        RegisterFeature(className, (doc, _, _, start, end) =>
        {
            var elem = doc.AllElements.FirstOrDefault(e => e.TagOffset == start);
            return elem == null ? null : parser(doc, elem, end);
        });
    }

    /// <summary>
    /// Register a parser for a given MFC class name.
    /// Call during static initialization; not thread-safe afterwards.
    /// </summary>
    public static void Register(string className, Func<MfcElement, IFeatureData?> parser)
    {
        _parsers[className] = parser;
    }

    /// <summary>
    /// Register a feature-level parser needing the owning document and feature range.
    /// Call during static initialization; not thread-safe afterwards.
    /// </summary>
    public static void RegisterFeature(
        string className, Func<MfcDocument, string, uint, int, int, IFeatureData?> parser)
    {
        _featureParsers[className] = parser;
    }

    /// <summary>True when a folder/element parser is registered for the given MFC class name.</summary>
    internal static bool IsRegistered(string className) =>
        !string.IsNullOrEmpty(className) && _parsers.ContainsKey(className);

    /// <summary>True when a feature-level parser is registered for the given MFC class name.</summary>
    internal static bool IsFeatureRegistered(string className) =>
        !string.IsNullOrEmpty(className) && _featureParsers.ContainsKey(className);

    /// <summary>Attempts a feature-level parse; returns false for unregistered classes or null results.</summary>
    public static bool TryParseFeature(
        MfcDocument doc, string className, uint typeId, int start, int end, out IFeatureData? data)
    {
        if (_featureParsers.TryGetValue(className, out var parser))
        {
            data = parser(doc, className, typeId, start, end);
            return data != null;
        }
        data = null;
        return false;
    }

    /// <summary>
    /// Attempt to parse feature-specific data from an MFC element.
    /// Returns true if a parser is registered for <c>element.ClassName</c>.
    /// </summary>
    public static bool TryParse(MfcElement element, out IFeatureData? data)
    {
        if (_parsers.TryGetValue(element.ClassName, out var parser))
        {
            data = parser(element);
            return data != null;
        }
        data = null;
        return false;
    }
}


internal static class FeatureDataRegistry
{
    public static void Register(string className, Func<MfcElement, IFeatureData?> parser) =>
        MfcFeatureRegistry.Register(className, parser);

    public static void RegisterFeature(string className, Func<MfcDocument, string, uint, int, int, IFeatureData?> parser) =>
        MfcFeatureRegistry.RegisterFeature(className, parser);

    internal static bool IsRegistered(string className) =>
        MfcFeatureRegistry.IsRegistered(className);

    internal static bool IsFeatureRegistered(string className) =>
        MfcFeatureRegistry.IsFeatureRegistered(className);

    public static bool TryParseFeature(MfcDocument doc, string className, uint typeId, int start, int end, out IFeatureData? data) =>
        MfcFeatureRegistry.TryParseFeature(doc, className, typeId, start, end, out data);

    public static bool TryParse(MfcElement element, out IFeatureData? data) =>
        MfcFeatureRegistry.TryParse(element, out data);
}