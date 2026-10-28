using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SldFile.Drawing;

/// <summary>
/// Reads drawing sheets, views, and sheet previews from drawing metadata.
/// </summary>
internal static class DrawingReader
{
    public static IReadOnlyList<DrawingSheetInfo> ReadSheets(
        string? keywordsXml,
        IReadOnlyList<string> sheetNamesFromStream,
        Func<int, byte[]?> getSheetPreview)
    {
        var sheets = new List<DrawingSheetInfo>();

        if (!string.IsNullOrEmpty(keywordsXml))
        {
            int start = keywordsXml!.IndexOf('<');
            if (start >= 0)
            {
                if (start > 0) keywordsXml = keywordsXml.Substring(start);
                try
                {
                    var doc = XDocument.Parse(keywordsXml);
                    int sheetIdx = 0;
                    foreach (var sheetElem in doc.Descendants().Where(e => e.Name.LocalName == "Sheet"))
                    {
                        string sheetType = sheetElem.Attribute("Type")?.Value ?? string.Empty;
                        // Filter out sheet formats, keep real drawing sheets
                        if (sheetType.Equals("图纸格式", StringComparison.OrdinalIgnoreCase) ||
                            sheetType.Equals("SheetFormat", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string sheetName = sheetElem.Attribute("Name")?.Value ?? $"Sheet{sheetIdx + 1}";
                        var views = new List<DrawingViewInfo>();

                        foreach (var viewElem in sheetElem.Elements().Where(e => e.Name.LocalName == "View"))
                        {
                            string viewName = viewElem.Attribute("Name")?.Value ?? string.Empty;
                            string cfg = viewElem.Attribute("Description")?.Value ?? string.Empty;
                            string modelPath = viewElem.Value?.Trim() ?? string.Empty;

                            views.Add(new DrawingViewInfo
                            {
                                Name = viewName,
                                ReferencedModelPath = modelPath,
                                ReferencedConfiguration = cfg
                            });
                        }

                        byte[]? preview = getSheetPreview(sheetIdx);

                        sheets.Add(new DrawingSheetInfo
                        {
                            Name = sheetName,
                            SheetIndex = sheetIdx,
                            PreviewPng = preview,
                            Views = views.AsReadOnly()
                        });

                        sheetIdx++;
                    }
                }
                catch
                {
                    // Fall back to sheetNamesFromStream
                }
            }
        }

        // If no sheets were found via XML, construct from SheetNames
        if (sheets.Count == 0 && sheetNamesFromStream != null && sheetNamesFromStream.Count > 0)
        {
            for (int i = 0; i < sheetNamesFromStream.Count; i++)
            {
                sheets.Add(new DrawingSheetInfo
                {
                    Name = sheetNamesFromStream[i],
                    SheetIndex = i,
                    PreviewPng = getSheetPreview(i),
                    Views = Array.Empty<DrawingViewInfo>()
                });
            }
        }

        return sheets.AsReadOnly();
    }
}
