using System;
using System.Collections.Generic;
using System.Linq;

namespace SldFile.Drawing.Vector;

/// <summary>Compatibility facade over the native preview command serializer.</summary>
public static class DrawingGeometryReader
{
    public static (IReadOnlyList<DrawingVectorLine> Lines, IReadOnlyList<DrawingViewGeometry> Views) ReadSheetGeometryAndViews(
        byte[]? displayListsStream, byte[]? vbListsStream, byte[]? definitionStream, double paperWidthMm, double paperHeightMm)
    {
        var data = DrawingVectorReader.ReadVectorData(definitionStream, null, displayListsStream: displayListsStream, vbListsStream: vbListsStream);
        return (data.Lines, Array.Empty<DrawingViewGeometry>());
    }

    public static (IReadOnlyList<DrawingVectorLine> Lines, IReadOnlyList<DrawingViewGeometry> Views) ReadSheetGeometryAndViews(
        byte[]? vbListsStream, byte[]? definitionStream, double paperWidthMm, double paperHeightMm) =>
        ReadSheetGeometryAndViews(null, vbListsStream, definitionStream, paperWidthMm, paperHeightMm);

    public static IReadOnlyList<DrawingVectorLine> ReadSheetGeometry(
        byte[]? displayListsStream, byte[]? vbListsStream, byte[]? definitionStream, double paperWidthMm, double paperHeightMm) =>
        ReadSheetGeometryAndViews(displayListsStream, vbListsStream, definitionStream, paperWidthMm, paperHeightMm).Lines;

    public static IReadOnlyList<DrawingVectorLine> ReadSheetGeometry(
        byte[]? vbListsStream, byte[]? definitionStream, double paperWidthMm, double paperHeightMm) =>
        ReadSheetGeometryAndViews(null, vbListsStream, definitionStream, paperWidthMm, paperHeightMm).Lines;

    public static List<DrawingVectorLine> ExtractLinesFromDisplayLists(byte[] displayListsStream, double paperWidthMm, double paperHeightMm) =>
        ReadSheetGeometry(displayListsStream, null, null, paperWidthMm, paperHeightMm).ToList();

    // No byte-pattern or bounding-box fitting fallback: these streams cannot independently establish sheet geometry.
    public static List<DrawingViewGeometry> ExtractViewsFromDefinition(byte[] definitionStream, double paperWidthMm, double paperHeightMm) =>
        new List<DrawingViewGeometry>();

    public static List<DrawingVectorLine> ExtractViewGeometryFromVBLists(byte[] vbListsStream,
        List<DrawingViewGeometry> views, double paperWidthMm, double paperHeightMm) => new List<DrawingVectorLine>();
}
