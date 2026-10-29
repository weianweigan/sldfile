using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SldFile;
using SldFile.Resources;
using SldFileViewer.Models;

namespace SldFileViewer.Services;

/// <summary>
/// WPF service providing SolidWorks feature and tree node icons as WPF <see cref="ImageSource"/> objects.
/// </summary>
public static class SolidWorksIconService
{
    private static readonly ConcurrentDictionary<string, ImageSource?> _sourceCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Converts a PNG byte array into a frozen WPF <see cref="ImageSource"/>, with caching.
    /// </summary>
    public static ImageSource? GetImageSource(byte[]? bytes, string? cacheKey = null)
    {
        if (bytes == null || bytes.Length == 0) return null;

        if (cacheKey != null && _sourceCache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            if (cacheKey != null)
                _sourceCache[cacheKey] = bitmap;

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets an ImageSource for the given SolidWorks base resource name and preferred size.
    /// </summary>
    public static ImageSource? GetIcon(string baseResourceName, int preferredSize = 20)
    {
        string cacheKey = $"RES::{baseResourceName}::{preferredSize}";
        if (_sourceCache.TryGetValue(cacheKey, out var cached))
            return cached;

        byte[]? bytes = SolidWorksIconResolver.GetIcon(baseResourceName, preferredSize);
        return GetImageSource(bytes, cacheKey);
    }

    /// <summary>
    /// Gets an ImageSource for the given FeatureKind and feature name.
    /// </summary>
    public static ImageSource? GetFeatureIcon(FeatureKind kind, string? featureName = null, int preferredSize = 20)
    {
        string cacheKey = $"FEAT::{kind}::{featureName}::{preferredSize}";
        if (_sourceCache.TryGetValue(cacheKey, out var cached))
            return cached;

        byte[]? bytes = SolidWorksIconResolver.GetFeatureIcon(kind, featureName, preferredSize);
        return GetImageSource(bytes, cacheKey);
    }

    /// <summary>
    /// Gets an ImageSource for an arbitrary feature name or type string.
    /// </summary>
    public static ImageSource? GetFeatureIcon(string featureNameOrType, int preferredSize = 20)
    {
        string cacheKey = $"NAME::{featureNameOrType}::{preferredSize}";
        if (_sourceCache.TryGetValue(cacheKey, out var cached))
            return cached;

        byte[]? bytes = SolidWorksIconResolver.GetFeatureIcon(featureNameOrType, preferredSize);
        return GetImageSource(bytes, cacheKey);
    }

    /// <summary>
    /// Resolves the appropriate SolidWorks icon for a given TreeNodeKind and context.
    /// </summary>
    public static ImageSource? GetNodeIcon(TreeNodeKind kind, string title, object? tag = null, int preferredSize = 128)
    {
        switch (kind)
        {
            case TreeNodeKind.DocumentRoot:
                if (tag is SwFile swFile)
                {
                    if (swFile.Kind == DocumentKind.Assembly)
                        return GetIcon("PNG_ASSEMBLY", preferredSize);
                    if (swFile.Kind == DocumentKind.Drawing)
                        return GetIcon("PNG_ACTIVE_SHEET_LEFT", preferredSize);
                }
                return GetIcon("PNG_FILENEW_PART_TEMPLATE", preferredSize) ?? GetIcon("PNG_ASSEMBLIES_NEW_PART", preferredSize);

            case TreeNodeKind.CustomProperties:
                return GetIcon("PNG_CALLOUT_BLOCKPART", preferredSize);

            case TreeNodeKind.Equations:
                return GetIcon("PNG_SKETCH_EQ_DRIVEN_CURVE", preferredSize);

            case TreeNodeKind.GeometryPreview:
                return GetIcon("PNG_I3DVIEW_DATUM_REF_PLANE", preferredSize);

            case TreeNodeKind.Bom:
                return GetIcon("PNG_TABLE_WELD_CUT_LIST", preferredSize) ?? GetIcon("PNG_CALLOUT_BLOCKPART", preferredSize);

            case TreeNodeKind.FeaturePlane:
                return GetIcon("PNG_REFGEO_PLANE", preferredSize);

            case TreeNodeKind.FeatureOrigin:
                return GetIcon("PNG_TREE_ORIGIN", preferredSize);

            case TreeNodeKind.FeatureSketch:
                return (title.IndexOf("3D", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? GetIcon("PNG_SKETCH_3D_SKETCH", preferredSize)
                    : GetIcon("PNG_SKETCH_SKETCH", preferredSize);

            case TreeNodeKind.FeatureExtrude:
                return (title.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? GetIcon("PNG_FEATURES_EXTRUDE_CUT", preferredSize)
                    : GetIcon("PNG_FEATURES_EXTRUDE_BOSS", preferredSize);

            case TreeNodeKind.FeatureCut:
                return GetIcon("PNG_FEATURES_EXTRUDE_CUT", preferredSize);

            case TreeNodeKind.FeatureRevolve:
                return (title.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? GetIcon("PNG_FEATURES_REVOLVED_CUT", preferredSize)
                    : GetIcon("PNG_FEATURES_REVOLVED_BOSS_BASE", preferredSize);

            case TreeNodeKind.FeatureSweep:
                return (title.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? GetIcon("PNG_FEATURES_SWEPT_CUT", preferredSize)
                    : GetIcon("PNG_FEATURES_SWEPT_BOSS", preferredSize);

            case TreeNodeKind.FeatureLoft:
                return (title.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? GetIcon("PNG_FEATURES_LOFTED_CUT", preferredSize)
                    : GetIcon("PNG_FEATURES_LOFTED_BOSS", preferredSize);

            case TreeNodeKind.FeatureFillet:
                return GetIcon("PNG_FEATURES_FILLET", preferredSize);

            case TreeNodeKind.FeatureChamfer:
                return GetIcon("PNG_FEATURES_CHAMFER", preferredSize);

            case TreeNodeKind.FeatureHole:
                return GetIcon("PNG_FEATURES_HOLE_WIZARD", preferredSize);

            case TreeNodeKind.FeaturePattern:
                if (title.IndexOf("圆周", StringComparison.OrdinalIgnoreCase) >= 0 || title.IndexOf("Cir", StringComparison.OrdinalIgnoreCase) >= 0)
                    return GetIcon("PNG_FEATURES_CIRCULAR_PATTERN", preferredSize);
                return GetIcon("PNG_FEATURES_LINEAR_PATTERN", preferredSize);

            case TreeNodeKind.FeatureMirror:
                return GetIcon("PNG_FEATURES_MIRROR", preferredSize);

            case TreeNodeKind.FeatureRib:
                return GetIcon("PNG_FEATURES_RIB", preferredSize);

            case TreeNodeKind.FeatureShell:
                return GetIcon("PNG_FEATURES_SHELL", preferredSize);

            case TreeNodeKind.FeatureDraft:
                return GetIcon("PNG_FEATURES_DRAFT", preferredSize);

            case TreeNodeKind.FeatureThread:
                return GetIcon("PNG_FEATURES_THREAD_WIZARD", preferredSize);

            case TreeNodeKind.FeatureFolder:
                return GetIcon("PNG_FEAT_EMPTY_FOLDER", preferredSize);

            case TreeNodeKind.AssemblyComponent:
                return GetIcon("PNG_ASSEMBLIES_NEW_PART", preferredSize);

            case TreeNodeKind.AssemblyMate:
                return GetIcon("PNG_ASSEMBLIES_MATE", preferredSize);

            case TreeNodeKind.DrawingSheet:
                return GetIcon("PNG_ACTIVE_SHEET_LEFT", preferredSize);

            case TreeNodeKind.FeatureGeneric:
            default:
                if (tag is Feature feat)
                {
                    return GetFeatureIcon(feat.Kind, feat.Name, preferredSize)
                        ?? GetFeatureIcon(feat.TypeName, preferredSize);
                }
                return GetFeatureIcon(title, preferredSize);
        }
    }
}
