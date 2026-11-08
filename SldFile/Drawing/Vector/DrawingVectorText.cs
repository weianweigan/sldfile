using System;
using System.Text.RegularExpressions;

namespace SldFile.Drawing.Vector;

/// <summary>
/// Represents a 2D vector text element (e.g. note, dimension label, title block field)
/// extracted from SolidWorks drawing definition streams.
/// </summary>
public sealed class DrawingVectorText
{
    private static readonly Regex FontTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    /// Raw text content as stored in the drawing (may contain format tags like &lt;FONT size=5PTS&gt;).
    /// </summary>
    public string RawText { get; }

    /// <summary>
    /// Clean display text with SolidWorks internal markup tags stripped out.
    /// </summary>
    public string CleanText { get; }

    /// <summary>
    /// Physical X position on sheet in millimeters (CAD coordinate, origin at sheet lower-left).
    /// </summary>
    public double X { get; }

    /// <summary>
    /// Physical Y position on sheet in millimeters (CAD coordinate, origin at sheet lower-left).
    /// </summary>
    public double Y { get; }

    /// <summary>
    /// Physical Z position or depth on sheet in millimeters.
    /// </summary>
    public double Z { get; }

    /// <summary>
    /// Font family name (e.g. "新宋体", "Century Gothic", "SimSun").
    /// </summary>
    public string FontName { get; }

    /// <summary>
    /// Nominal text height in millimeters. Defaults to 2.5 mm if unspecified or negative.
    /// </summary>
    public double HeightMm { get; }

    /// <summary>
    /// Text rotation angle in radians (counter-clockwise).
    /// </summary>
    public double AngleRad { get; }

    /// <summary>
    /// Computed base pixel X position on reference preview bitmap.
    /// </summary>
    public double PixelX { get; internal set; }

    /// <summary>
    /// Computed base pixel Y position on reference preview bitmap (origin at top-left).
    /// </summary>
    public double PixelY { get; internal set; }

    /// <summary>
    /// Computed base pixel height on reference preview bitmap.
    /// </summary>
    public double PixelHeight { get; internal set; }

    public DrawingVectorText(
        string rawText,
        double xMm,
        double yMm,
        double zMm = 0.0,
        string fontName = "",
        double heightMm = 2.5,
        double angleRad = 0.0)
    {
        RawText = rawText ?? string.Empty;
        CleanText = CleanRawText(RawText);
        X = xMm;
        Y = yMm;
        Z = zMm;
        FontName = string.IsNullOrWhiteSpace(fontName) ? "SimSun" : fontName.Trim();
        HeightMm = heightMm > 0.1 && heightMm < 50.0 ? heightMm : 2.5;
        AngleRad = angleRad;
    }

    private static string CleanRawText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        string cleaned = FontTagRegex.Replace(raw, string.Empty).Trim();
        // Replace special symbols if needed
        cleaned = cleaned.Replace("<MOD-DIAM>", "Ø");
        return cleaned;
    }

    public override string ToString() => $"[({X:F2}, {Y:F2}) H={HeightMm:F2}mm] {CleanText}";
}
