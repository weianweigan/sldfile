using System;
using System.Collections.Generic;
using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Central registry that maps MFC class names to their feature-data parsers.
/// Add new feature types by calling <see cref="Register"/> in the static constructor.
/// </summary>
internal static class FeatureDataRegistry
{
    private static readonly Dictionary<string, Func<MfcElement, IFeatureData?>> _parsers
        = new(StringComparer.Ordinal);

    static FeatureDataRegistry()
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

        // ── Extrusion sub-objects ─────────────────────────────────────────
        Register("moPerBodyChooserData_c", FeatureDataParsers.ParsePerBodyChooser);
        Register("moFaceRef_c", FeatureDataParsers.ParseFaceRef);
        Register("moEndFaceSurfIdRep_c", FeatureDataParsers.ParseEndFaceSurfIdRep);
        Register("moFR_c", FeatureDataParsers.ParseFr);
        Register("moFromSktEntSurfIdRep_c", FeatureDataParsers.ParseFromSktEntSurfIdRep);
        Register("moBBoxCenterData_c", FeatureDataParsers.ParseBBoxCenter);
        Register("moEndSpec_c", FeatureDataParsers.ParseEndSpec);
        Register("moFromEndSpec_c", FeatureDataParsers.ParseFromEndSpec);
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
