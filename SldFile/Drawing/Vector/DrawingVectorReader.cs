using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SldFile.Drawing.Vector;

/// <summary>
/// High-performance parser that extracts 2D vector graphic primitives (text, annotations, dimensions)
/// from SolidWorks drawing binary definition and metadata streams.
/// </summary>
public static class DrawingVectorReader
{
    private static readonly byte[] MfcWideStrHeader = { 0xFF, 0xFE, 0xFF };
    private static readonly HashSet<string> ExcludedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "宋体", "新宋体", "Century Gothic", "SimSun", "Arial", "Times New Roman", "黑体", "仿宋", "楷体",
        "moDrawing_c", "moHeader_c", "moLogs_c", "moStamp_c", "moDisplayNote_c", "moGenericText_c"
    };

    public static DrawingSheetVectorData ReadVectorData(
        byte[]? definitionStream,
        string? keywordsXml,
        int sheetIndex = 0)
    {
        var vectorData = new DrawingSheetVectorData();

        // 1. Parse sheet metadata from KeyWords XML
        ParseSheetMetadata(keywordsXml, sheetIndex, vectorData);

        // 2. Parse vector text annotations from Definition stream
        if (definitionStream != null && definitionStream.Length > 0)
        {
            var rawTexts = ParseTextsFromDefinition(definitionStream);

            // Refine paper dimensions only if not explicitly identified from template/XML
            if (string.IsNullOrEmpty(vectorData.PaperSizeName) && rawTexts.Count > 0)
            {
                double maxX = rawTexts.Max(t => t.X);
                double maxY = rawTexts.Max(t => t.Y);

                if (vectorData.PaperWidthMm < maxX)
                {
                    vectorData.PaperWidthMm = (maxX > 300) ? 420.0 : 297.0;
                }
                if (vectorData.PaperHeightMm < maxY)
                {
                    vectorData.PaperHeightMm = (maxY > 220) ? 297.0 : 210.0;
                }
            }

            vectorData.Texts = rawTexts;
        }

        return vectorData;
    }

    private static void ParseSheetMetadata(string? keywordsXml, int sheetIndex, DrawingSheetVectorData data)
    {
        if (string.IsNullOrEmpty(keywordsXml)) return;

        try
        {
            int start = keywordsXml!.IndexOf('<');
            if (start >= 0)
            {
                if (start > 0) keywordsXml = keywordsXml.Substring(start);
                var doc = XDocument.Parse(keywordsXml);

                var sheetElements = doc.Descendants().Where(e => e.Name.LocalName == "Sheet" &&
                    !string.Equals(e.Attribute("Type")?.Value, "图纸格式", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(e.Attribute("Type")?.Value, "SheetFormat", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                XElement? targetSheet = (sheetIndex >= 0 && sheetIndex < sheetElements.Count)
                    ? sheetElements[sheetIndex]
                    : sheetElements.FirstOrDefault();

                if (targetSheet != null)
                {
                    string? tpl = targetSheet.Element("TemplateName")?.Value;
                    if (!string.IsNullOrEmpty(tpl))
                    {
                        data.TemplateName = tpl!;
                        string lowerTpl = tpl!.ToLowerInvariant();
                        if (lowerTpl.Contains("a3"))
                        {
                            data.PaperWidthMm = 420.0;
                            data.PaperHeightMm = 297.0;
                            data.PaperSizeName = "A3";
                        }
                        else if (lowerTpl.Contains("a4"))
                        {
                            data.PaperWidthMm = 297.0;
                            data.PaperHeightMm = 210.0;
                            data.PaperSizeName = "A4";
                        }
                        else if (lowerTpl.Contains("a2"))
                        {
                            data.PaperWidthMm = 594.0;
                            data.PaperHeightMm = 420.0;
                            data.PaperSizeName = "A2";
                        }
                        else if (lowerTpl.Contains("a1"))
                        {
                            data.PaperWidthMm = 841.0;
                            data.PaperHeightMm = 594.0;
                            data.PaperSizeName = "A1";
                        }
                        else if (lowerTpl.Contains("a0"))
                        {
                            data.PaperWidthMm = 1189.0;
                            data.PaperHeightMm = 841.0;
                            data.PaperSizeName = "A0";
                        }
                    }

                    string? paperSize = targetSheet.Element("PaperSize")?.Value;
                    if (!string.IsNullOrEmpty(paperSize) && string.IsNullOrEmpty(data.PaperSizeName))
                    {
                        data.PaperSizeName = paperSize!;
                    }

                    if (double.TryParse(targetSheet.Element("ScaleNumerator")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                        data.ScaleNumerator = num;
                    if (double.TryParse(targetSheet.Element("ScaleDenominator")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double den))
                        data.ScaleDenominator = den;
                }
            }
        }
        catch
        {
            // Ignore XML parse errors gracefully
        }
    }

    private static IReadOnlyList<DrawingVectorText> ParseTextsFromDefinition(byte[] buf)
    {
        var list = new List<DrawingVectorText>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        int maxLen = buf.Length;
        for (int i = 0; i < maxLen - 30; i++)
        {
            // Find MFC wide string marker: 0xFF, 0xFE, 0xFF
            if (buf[i] == 0xFF && buf[i + 1] == 0xFE && buf[i + 2] == 0xFF)
            {
                int charLen = buf[i + 3];
                if (charLen <= 0 || charLen > 120) continue;

                int textStart = i + 4;
                int byteLen = charLen * 2;
                int textEnd = textStart + byteLen;

                if (textEnd + 24 > maxLen) continue;

                string text;
                try
                {
                    text = Encoding.Unicode.GetString(buf, textStart, byteLen).Trim();
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(text) || text.EndsWith("_c", StringComparison.Ordinal) || ExcludedKeywords.Contains(text))
                    continue;

                // Read (X, Y, Z) doubles immediately following the text
                double x = BitConverter.ToDouble(buf, textEnd);
                double y = BitConverter.ToDouble(buf, textEnd + 8);
                double z = BitConverter.ToDouble(buf, textEnd + 16);

                // Coordinates should be in reasonable sheet range (in meters: -0.1 to 2.5m)
                if (x >= -0.1 && x <= 2.5 && y >= -0.1 && y <= 2.5 && Math.Abs(z) < 0.25)
                {
                    // Convert meters to millimeters
                    double xMm = x * 1000.0;
                    double yMm = y * 1000.0;
                    double zMm = z * 1000.0;

                    // Parse following font and height if present
                    string fontName = "SimSun";
                    double heightMm = 2.5;
                    double angleRad = 0.0;

                    int postStart = textEnd + 24;
                    int postLen = Math.Min(80, maxLen - postStart);
                    for (int j = 0; j < postLen - 12; j++)
                    {
                        if (buf[postStart + j] == 0xFF && buf[postStart + j + 1] == 0xFE && buf[postStart + j + 2] == 0xFF)
                        {
                            int flen = buf[postStart + j + 3];
                            if (flen > 0 && flen <= 30 && postStart + j + 4 + flen * 2 + 16 <= maxLen)
                            {
                                try
                                {
                                    string fname = Encoding.Unicode.GetString(buf, postStart + j + 4, flen * 2);
                                    if (!string.IsNullOrWhiteSpace(fname) && !fname.EndsWith("_c", StringComparison.Ordinal))
                                    {
                                        fontName = fname.Trim();
                                        int fEnd = postStart + j + 4 + flen * 2;
                                        double h = BitConverter.ToDouble(buf, fEnd);
                                        double a = BitConverter.ToDouble(buf, fEnd + 8);
                                        if (h > 0.0005 && h < 0.05) heightMm = h * 1000.0;
                                        if (Math.Abs(a) < 7.0) angleRad = a;
                                    }
                                }
                                catch { }
                                break;
                            }
                        }
                    }

                    // Dedup by text and approximate position
                    string key = $"{text}_{Math.Round(xMm, 1)}_{Math.Round(yMm, 1)}";
                    if (seen.Add(key))
                    {
                        list.Add(new DrawingVectorText(text, xMm, yMm, zMm, fontName, heightMm, angleRad));
                    }
                }
            }
        }

        // Sort by Y descending (top to bottom), then X ascending (left to right)
        return list.OrderByDescending(t => t.Y).ThenBy(t => t.X).ToList().AsReadOnly();
    }
}
