using SldFile.Mfc;

namespace SldFile.Binary;

/// <summary>
/// Strict parser skeletons for Phase 4 surface features.
/// <para>
/// No Phase 4 sample exists, so no serialized layout (MFC class name, FolderHeader
/// offsets, sub-objects) is established. Every entry point deliberately returns
/// <c>null</c> so callers keep <see cref="FeatureFolder.FeatureData"/> null instead of
/// fabricating defaults. When samples arrive, replace each skeleton with a layout
/// validator that returns null on any mismatch.
/// </para>
/// </summary>
internal static class SurfaceFeatureParsers
{
    public static IFeatureData? ParseSurfaceExtrude(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseSurfaceRevolve(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseFillSurface(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseSurfaceKnit(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseSurfaceTrim(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseSurfaceExtend(MfcElement? element) => Reject(element);

    public static IFeatureData? ParseSurfaceOffset(MfcElement? element) => Reject(element);

    private static IFeatureData? Reject(MfcElement? element)
    {
        // A null element, an unknown tag byte or any unvalidated layout is rejected.
        // No field is read and no default-valued definition is produced.
        _ = element;
        return null;
    }
}
