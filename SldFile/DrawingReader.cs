using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SldFile.Drawing.Vector;

namespace SldFile;

/// <summary>
/// Reads drawing sheets, views, sheet previews, and vector graphic metadata from drawing streams.
/// </summary>
internal static class DrawingReader
{
    public static IReadOnlyList<DrawingSheetInfo> ReadSheets(
        string? keywordsXml,
        IReadOnlyList<string> sheetNamesFromStream,
        Func<int, byte[]?> getSheetPreview,
        Func<int, byte[]?>? getHighResPreview = null,
        byte[]? definitionStream = null,
        byte[]? vbListsStream = null,
        byte[]? displayListsStream = null)
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
                                ReferencedConfiguration = cfg,
                            });
                        }

                        byte[]? preview = getSheetPreview(sheetIdx);
                        byte[]? highRes = getHighResPreview?.Invoke(sheetIdx);

                        DrawingSheetVectorData? vectorData = null;
                        if ((definitionStream != null && definitionStream.Length > 0) ||
                            (displayListsStream != null && displayListsStream.Length > 0))
                        {
                            vectorData = DrawingVectorReader.ReadVectorData(definitionStream, keywordsXml, sheetIdx, vbListsStream, displayListsStream, sheetNamesFromStream.Count);
                            byte[]? bestImage = highRes ?? preview;
                            if (bestImage != null && vectorData != null && vectorData.HasVectorData)
                            {
                                var (w, h) = SwFile.GetImageDimensions(bestImage);
                                if (w > 0 && h > 0)
                                {
                                    vectorData.CalculatePixelCoordinates(w, h);
                                }
                            }
                        }

                        sheets.Add(new DrawingSheetInfo
                        {
                            Name = sheetName,
                            SheetIndex = sheetIdx,
                            PreviewPng = preview,
                            HighResPreview = highRes,
                            VectorData = vectorData,
                            Views = views.AsReadOnly()
                        });
                        sheetIdx++;
                    }
                }
                catch
                {
                    // XML parse failure, fallback
                }
            }
        }

        // Fallback: use sheet names from SheetNames stream if XML yielded nothing
        if (sheets.Count == 0 && sheetNamesFromStream.Count > 0)
        {
            for (int i = 0; i < sheetNamesFromStream.Count; i++)
            {
                byte[]? preview = getSheetPreview(i);
                byte[]? highRes = getHighResPreview?.Invoke(i);

                DrawingSheetVectorData? vectorData = null;
                if ((definitionStream != null && definitionStream.Length > 0) ||
                    (displayListsStream != null && displayListsStream.Length > 0))
                {
                    vectorData = DrawingVectorReader.ReadVectorData(definitionStream, keywordsXml, i, vbListsStream, displayListsStream, sheetNamesFromStream.Count);
                    byte[]? bestImage = highRes ?? preview;
                    if (bestImage != null && vectorData != null && vectorData.HasVectorData)
                    {
                        var (w, h) = SwFile.GetImageDimensions(bestImage);
                        if (w > 0 && h > 0)
                        {
                            vectorData.CalculatePixelCoordinates(w, h);
                        }
                    }
                }

                sheets.Add(new DrawingSheetInfo
                {
                    Name = sheetNamesFromStream[i],
                    SheetIndex = i,
                    PreviewPng = preview,
                    HighResPreview = highRes,
                    VectorData = vectorData,
                    Views = Array.Empty<DrawingViewInfo>()
                });
            }
        }

        // Final fallback: single default sheet
        if (sheets.Count == 0)
        {
            sheets.Add(new DrawingSheetInfo
            {
                Name = "Sheet1",
                SheetIndex = 0,
                PreviewPng = getSheetPreview(0),
                HighResPreview = getHighResPreview?.Invoke(0),
                VectorData = DrawingVectorReader.ReadVectorData(definitionStream, keywordsXml, 0, vbListsStream, displayListsStream),
                Views = Array.Empty<DrawingViewInfo>(),
            });
        }

        return sheets;
    }
}
