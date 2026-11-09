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
        int sheetIndex = 0,
        byte[]? vbListsStream = null,
        byte[]? displayListsStream = null)
    {
        var vectorData = new DrawingSheetVectorData();

        bool hasStreamData = (definitionStream != null && definitionStream.Length > 0) ||
                             (vbListsStream != null && vbListsStream.Length > 0) ||
                             (displayListsStream != null && displayListsStream.Length > 0);

        if (!hasStreamData)
        {
            return vectorData;
        }

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

            // Classify texts into SheetFormat, TitleBlock, ToleranceTable, TechnicalNote vs ViewAnnotation
            ClassifyTexts(rawTexts, vectorData.PaperWidthMm, vectorData.PaperHeightMm);

            vectorData.Texts = rawTexts;
        }

        // 3. Extract 2D CAD vector line geometry (sheet borders, title block, view geometry wireframes)
        if ((displayListsStream != null && displayListsStream.Length > 0) ||
            (vbListsStream != null && vbListsStream.Length > 0) ||
            (definitionStream != null && definitionStream.Length > 0))
        {
            var (lines, views) = DrawingGeometryReader.ReadSheetGeometryAndViews(
                displayListsStream,
                vbListsStream,
                definitionStream,
                vectorData.PaperWidthMm,
                vectorData.PaperHeightMm);
            vectorData.Lines = lines;
            vectorData.Views = views;
        }

        return vectorData;
    }

    private static void ClassifyTexts(IReadOnlyList<DrawingVectorText> texts, double paperWidth, double paperHeight)
    {
        double sheetNoteMaxY = Math.Max(55.0, paperHeight * 0.28);

        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text.CleanText) ||
                text.CleanText.StartsWith("$PRP", StringComparison.OrdinalIgnoreCase))
            {
                text.Category = DrawingVectorTextCategory.Metadata;
                continue;
            }

            // Outside paper boundary or negative coordinates
            if (text.X <= 0.5 || text.Y <= 0.5 || text.X > paperWidth + 5.0 || text.Y > paperHeight + 5.0)
            {
                text.Category = DrawingVectorTextCategory.Metadata;
                continue;
            }

            // CAD layer or system tokens
            if (text.CleanText == "mm" || text.CleanText == "m" || text.CleanText == "cm" || text.CleanText == "km" ||
                text.CleanText == "CENTER" || text.CleanText == "HIDDEN" || text.CleanText == "CONTINUOUS" ||
                text.CleanText == "PHANTOM" || text.CleanText == "粗实线" || text.CleanText == "细/粗_点画线" ||
                text.CleanText == "点画线" || text.CleanText == "双点画线" || text.CleanText == "中心线" ||
                text.CleanText == "2细线层" || text.CleanText == "DUMMYTHREAD" || text.CleanText.StartsWith("细节项目", StringComparison.Ordinal))
            {
                text.Category = DrawingVectorTextCategory.Metadata;
                continue;
            }

            // 优先检查：是否属于视图内部的尺寸、视图标号、孔标注等（防止与底图产生重影）
            if (IsLikelyViewAnnotation(text.CleanText))
            {
                text.Category = DrawingVectorTextCategory.ViewAnnotation;
                continue;
            }

            // 检查是否明确是技术要求
            if (text.CleanText.StartsWith("技术要求", StringComparison.OrdinalIgnoreCase) ||
                text.CleanText.Contains("锐边倒角") || text.CleanText.Contains("未注折弯") ||
                text.CleanText.Contains("表面处理") || text.CleanText.Contains("去毛刺"))
            {
                text.Category = DrawingVectorTextCategory.TechnicalNote;
                continue;
            }

            // If in bottom zone (Y <= sheetNoteMaxY)
            if (text.Y <= sheetNoteMaxY)
            {
                // Lower-right quadrant: Check if it's tolerance table or title block
                if (text.X >= paperWidth * 0.45)
                {
                    if (text.CleanText.Contains("公差") || text.CleanText.Contains("Tolerance") ||
                        text.CleanText == "±" || text.CleanText == "≤" || text.CleanText == "＞" ||
                        text.CleanText.Contains("specify deviations") ||
                        (text.Y >= 35.0 && text.X >= paperWidth * 0.75))
                    {
                        text.Category = DrawingVectorTextCategory.ToleranceTable;
                    }
                    else
                    {
                        text.Category = DrawingVectorTextCategory.TitleBlock;
                    }
                }
                else
                {
                    // Lower-left quadrant: General technical requirements & notes
                    text.Category = DrawingVectorTextCategory.TechnicalNote;
                }
            }
            else
            {
                // Higher up on sheet: drawing views, dimensions, hole callouts
                text.Category = DrawingVectorTextCategory.ViewAnnotation;
            }
        }
    }

    private static bool IsLikelyViewAnnotation(string cleanText)
    {
        if (string.IsNullOrWhiteSpace(cleanText)) return false;
        string s = cleanText.Trim();

        // 1. 独立单字母视图/剖面标号，如 "A", "B", "C", "D", "E", "F"
        if (s.Length == 1 && char.IsLetter(s[0]))
            return true;

        // 2. 视图标题与比例标注，如 "局部视图 A", "向视图 B", "剖面 A-A", "比例 1 : 2", "Detail A", "Section A-A", "SCALE 1:2"
        if (s.StartsWith("局部视图", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("向视图", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("剖面", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("视图", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("Detail", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("Section", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("View", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("比例", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("SCALE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. 典型孔特征/阵列尺寸标注，如 "2-10*50", "5-15*25", "4-M6", "4-Φ10", "2-R5"
        if (s.Contains("*") || s.Contains("×") || (s.Contains("-") && char.IsDigit(s[0])))
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+[-*xX×\d\.\s]+$") ||
                System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d+-[A-Za-zΦφ]"))
            {
                return true;
            }
        }

        // 4. 纯尺寸数值，如 "24.0", "139.0", "173.0", "10", "100", "220.0" 等
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^[<>]?\s*(\d+(\.\d+)?)\s*(°|mm|deg)?$"))
        {
            return true;
        }

        // 5. 常见公差/尺寸符号：带直径、半径、倒角、角度前缀的尺寸，如 "Φ20", "R15", "C2", "SR10", "45°"
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^[ΦφRrCcMm](\d+(\.\d+)?)|^\d+(\.\d+)?°$"))
        {
            return true;
        }

        // 6. 尺寸基准或形位公差标记、基准指示箭头
        if (s == "↑" || s == "↓" || s == "←" || s == "→")
            return true;

        return false;
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
        var notes = SldFile.Binary.DefinitionSchemaMapper.ExtractNotes(buf);
        return notes
            .Select(n => new DrawingVectorText(n.Text, n.XMm, n.YMm, n.ZMm, n.Font, n.HeightMm, n.AngleRad))
            .OrderByDescending(t => t.Y)
            .ThenBy(t => t.X)
            .ToList()
            .AsReadOnly();
    }
}
