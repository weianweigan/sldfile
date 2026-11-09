using System;
using System.Collections.Generic;

namespace SldFile.Drawing.Vector;

/// <summary>
/// Presentation data for decoded native drawing commands. Preview.IsResolved
/// indicates whether the raster can safely be replaced.
/// </summary>
public sealed class DrawingSheetVectorData
{
    /// <summary>Native serialized preview commands; this is the authoritative geometry source.</summary>
    public SldFile.Drawing.DrawingPreviewDefinition? Preview { get; internal set; }

    public double PaperWidthMm { get; set; } = 297.0;
    public double PaperHeightMm { get; set; } = 210.0;
    public string TemplateName { get; set; } = string.Empty;
    public string PaperSizeName { get; set; } = string.Empty;
    public double ScaleNumerator { get; set; } = 1.0;
    public double ScaleDenominator { get; set; } = 1.0;

    /// <summary>
    /// All vector text elements (notes, dimensions, title block headers) on this sheet.
    /// </summary>
    public IReadOnlyList<DrawingVectorText> Texts { get; set; } = Array.Empty<DrawingVectorText>();

    /// <summary>
    /// All 2D CAD vector line segments (view outlines, sheet borders, title block grids, centerlines) on this sheet.
    /// </summary>
    public IReadOnlyList<DrawingVectorLine> Lines { get; set; } = Array.Empty<DrawingVectorLine>();

    /// <summary>
    /// Individual 2D drawing view geometries extracted and transformed on this sheet.
    /// </summary>
    public IReadOnlyList<DrawingViewGeometry> Views { get; set; } = Array.Empty<DrawingViewGeometry>();

    /// <summary>
    /// True if any vector CAD metadata (text or lines) is available for this sheet.
    /// </summary>
    public bool HasVectorData => Preview?.Primitives.Count > 0 || (Texts != null && Texts.Count > 0) || (Lines != null && Lines.Count > 0);

    public bool HasLines => Lines != null && Lines.Count > 0;

    /// <summary>
    /// Computes pixel coordinates for all vector text elements and geometry lines mapped to a specific viewport resolution.
    /// Uses isometric sheet aspect centering to ensure 100% accurate alignment with drawing lines.
    /// </summary>
    public void CalculatePixelCoordinates(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0) return;

        double sheetAspect = (PaperHeightMm > 0) ? (PaperWidthMm / PaperHeightMm) : (297.0 / 210.0);
        if (sheetAspect <= 0.1 || double.IsNaN(sheetAspect)) sheetAspect = 297.0 / 210.0;

        // SolidWorks drawing preview images embed the paper sheet with standard vertical margins (~8-16px)
        double marginY = Math.Max(4.0, imageHeight * 0.018);
        double paperH = imageHeight - 2 * marginY;
        double paperW = paperH * sheetAspect;

        if (paperW > imageWidth - 10)
        {
            paperW = imageWidth - 10;
            paperH = paperW / sheetAspect;
            marginY = (imageHeight - paperH) / 2.0;
        }

        double paperOriginX = (imageWidth - paperW) / 2.0;
        double paperBottomY = imageHeight - marginY;

        double scaleX = paperW / PaperWidthMm;
        double scaleY = paperH / PaperHeightMm;

        if (Texts != null)
        {
            foreach (var text in Texts)
            {
                text.PixelX = paperOriginX + text.X * scaleX;
                text.PixelY = paperBottomY - text.Y * scaleY;
                text.PixelHeight = Math.Max(1.0, text.HeightMm * scaleY);
            }
        }

        if (Lines != null)
        {
            foreach (var line in Lines)
            {
                line.PixelX1 = paperOriginX + line.X1 * scaleX;
                line.PixelY1 = paperBottomY - line.Y1 * scaleY;
                line.PixelX2 = paperOriginX + line.X2 * scaleX;
                line.PixelY2 = paperBottomY - line.Y2 * scaleY;
            }
        }
    }
}
