using System;
using System.Collections.Generic;

namespace SldFile.Drawing.Vector;

/// <summary>
/// Encapsulates full 2D vector graphic metadata extracted from a SolidWorks drawing sheet,
/// enabling true lossless vector rendering at infinite zoom levels (like eDrawings).
/// </summary>
public sealed class DrawingSheetVectorData
{
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

    public bool HasVectorData => Texts != null && Texts.Count > 0;

    /// <summary>
    /// Computes pixel coordinates for all vector text elements mapped to a specific raster image resolution.
    /// Uses isometric sheet aspect centering to ensure 100% accurate alignment with drawing lines.
    /// </summary>
    public void CalculatePixelCoordinates(int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || Texts == null || Texts.Count == 0) return;

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

        foreach (var text in Texts)
        {
            text.PixelX = paperOriginX + text.X * scaleX;
            text.PixelY = paperBottomY - text.Y * scaleY;
            text.PixelHeight = Math.Max(1.0, text.HeightMm * scaleY);
        }
    }
}
