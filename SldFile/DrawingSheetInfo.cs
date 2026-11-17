using System;
using System.Collections.Generic;
using System.Globalization;
using SldFile.Drawing;

namespace SldFile;

/// <summary>
/// Information about a drawing sheet extracted from SolidWorks drawing metadata.
/// </summary>
public sealed class DrawingSheetInfo
{
    public string Name { get; internal set; } = string.Empty;
    public int SheetIndex { get; internal set; }
    public byte[]? PreviewPng { get; internal set; }

    /// <summary>
    /// Higher resolution preview bitmap (e.g. DIB/BMP from Preview stream), if available.
    /// </summary>
    public byte[]? HighResPreview { get; internal set; }

    /// <summary>
    /// Returns the best available preview image data (preferring high-resolution preview over thumbnail).
    /// </summary>
    public byte[]? BestPreview => HighResPreview ?? PreviewPng;

    /// <summary>
    /// Cached native preview in sheet coordinates. Use SldDrawing.GetDrawingPreview for detached editing.
    /// </summary>
    public DrawingPreviewDefinition Preview { get; internal set; } = new DrawingPreviewDefinition();

    /// <summary>
    /// Authoritative sheet format metadata parsed from ISolidWorksInformation ("SW-All Sheet Format Data").
    /// </summary>
    public DrawingSheetFormatInfo? FormatInfo { get; internal set; }

    public IReadOnlyList<DrawingViewInfo> Views { get; internal set; } = Array.Empty<DrawingViewInfo>();
}

/// <summary>
/// Parsed entry from "SW-All Sheet Format Data" in ISolidWorksInformation.
/// Format: &lt;sheetName&gt;@&lt;ratio&gt;@&lt;width_m&gt;@&lt;height_m&gt;@...@&lt;template.slddrt&gt;@&lt;margin_a&gt;@&lt;margin_b&gt;@@
/// </summary>
public sealed class DrawingSheetFormatInfo
{
    public string SheetName { get; set; } = string.Empty;
    public double ScaleNumerator { get; set; } = 1.0;
    public double ScaleDenominator { get; set; } = 1.0;
    public double PaperWidthMm { get; set; } = 297.0;
    public double PaperHeightMm { get; set; } = 210.0;
    public string TemplatePath { get; set; } = string.Empty;
    public double FrameMarginHorizontalMm { get; set; }
    public double FrameMarginVerticalMm { get; set; }
    public string RawValue { get; set; } = string.Empty;

    public static IReadOnlyList<DrawingSheetFormatInfo> ParseAll(string? rawFormatData)
    {
        if (string.IsNullOrWhiteSpace(rawFormatData))
            return Array.Empty<DrawingSheetFormatInfo>();

        string cleaned = rawFormatData!.Trim();
        if (cleaned.StartsWith("&gt;", StringComparison.OrdinalIgnoreCase)) cleaned = cleaned.Substring(4).TrimStart();
        if (cleaned.StartsWith(">", StringComparison.Ordinal)) cleaned = cleaned.Substring(1).TrimStart();

        var results = new List<DrawingSheetFormatInfo>();
        string[] lines = cleaned.Split(new[] { "\r\n", "\n", "@@" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            if (TryParseEntry(line, out var info) && info != null)
                results.Add(info);
        }

        if (results.Count == 0 && TryParseEntry(cleaned, out var single) && single != null)
        {
            results.Add(single);
        }

        return results;
    }

    public static bool TryParseEntry(string? entry, out DrawingSheetFormatInfo? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(entry)) return false;

        string s = entry!.Trim();
        if (s.StartsWith("&gt;", StringComparison.OrdinalIgnoreCase)) s = s.Substring(4).TrimStart();
        if (s.StartsWith(">", StringComparison.Ordinal)) s = s.Substring(1).TrimStart();

        string[] parts = s.Split('@');
        if (parts.Length < 4) return false;

        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ratio) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double widthM) ||
            !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double heightM))
        {
            return false;
        }

        if (widthM <= 0.01 || heightM <= 0.01 || widthM > 5.0 || heightM > 5.0)
            return false;

        var info = new DrawingSheetFormatInfo
        {
            SheetName = parts[0].Trim(),
            ScaleNumerator = 1.0,
            ScaleDenominator = ratio > 1e-6 ? ratio : 1.0,
            PaperWidthMm = Math.Round(widthM * 1000.0, 3),
            PaperHeightMm = Math.Round(heightM * 1000.0, 3),
            RawValue = s
        };

        int tplIndex = -1;
        for (int i = 4; i < parts.Length; i++)
        {
            if (parts[i].IndexOf(".slddrt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                info.TemplatePath = parts[i].Trim();
                tplIndex = i;
                break;
            }
        }

        var trailingNumbers = new List<double>();
        for (int i = (tplIndex >= 0 ? tplIndex + 1 : 4); i < parts.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(parts[i]) &&
                double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) &&
                v >= 0.0 && v <= 100.0)
            {
                trailingNumbers.Add(v);
            }
        }

        if (trailingNumbers.Count >= 2)
        {
            info.FrameMarginHorizontalMm = trailingNumbers[trailingNumbers.Count - 2];
            info.FrameMarginVerticalMm = trailingNumbers[trailingNumbers.Count - 1];
        }
        else if (trailingNumbers.Count == 1)
        {
            info.FrameMarginHorizontalMm = trailingNumbers[0];
            info.FrameMarginVerticalMm = trailingNumbers[0];
        }

        result = info;
        return true;
    }

    public static string InferPaperSizeName(double widthMm, double heightMm, string? templateName = null)
    {
        double w = Math.Max(widthMm, heightMm);
        double h = Math.Min(widthMm, heightMm);
        if (Math.Abs(w - 1189.0) <= 5.0 && Math.Abs(h - 841.0) <= 5.0) return "A0";
        if (Math.Abs(w - 841.0) <= 5.0 && Math.Abs(h - 594.0) <= 5.0) return "A1";
        if (Math.Abs(w - 594.0) <= 5.0 && Math.Abs(h - 420.0) <= 5.0) return "A2";
        if (Math.Abs(w - 420.0) <= 5.0 && Math.Abs(h - 297.0) <= 5.0) return "A3";
        if (Math.Abs(w - 297.0) <= 5.0 && Math.Abs(h - 210.0) <= 5.0) return "A4";
        if (!string.IsNullOrEmpty(templateName))
        {
            string lower = templateName!.ToLowerInvariant();
            if (lower.Contains("a0")) return "A0";
            if (lower.Contains("a1")) return "A1";
            if (lower.Contains("a2")) return "A2";
            if (lower.Contains("a3")) return "A3";
            if (lower.Contains("a4")) return "A4";
        }
        return $"{widthMm:0.###}x{heightMm:0.###}";
    }
}

/// <summary>
/// Information about a drawing view on a drawing sheet.
/// </summary>
public sealed class DrawingViewInfo
{
    public string Name { get; internal set; } = string.Empty;
    public string ReferencedModelPath { get; internal set; } = string.Empty;
    public string ReferencedConfiguration { get; internal set; } = string.Empty;
}
