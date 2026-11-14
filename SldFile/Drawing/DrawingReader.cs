using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SldFile.Drawing.Vector;

namespace SldFile.Drawing;

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

        XDocument? doc = null;
        if (!string.IsNullOrEmpty(keywordsXml))
        {
            int start = keywordsXml!.IndexOf('<');
            if (start >= 0)
            {
                if (start > 0) keywordsXml = keywordsXml.Substring(start);
                try
                {
                    doc = XDocument.Parse(keywordsXml);
                }
                catch { }
            }
        }

        // Parse global MFC views from Definition stream once
        List<DrawingViewInfo> mfcViews = new List<DrawingViewInfo>();
        if (definitionStream != null && definitionStream.Length > 0)
        {
            try
            {
                mfcViews = DrawingMfcViewReader.ReadViewsFromDefinition(definitionStream);
            }
            catch { }
        }

        var technicalNotes = DrawingMfcViewReader.ExtractTechnicalNotes(keywordsXml);
        var titleBlockProps = DrawingMfcViewReader.ExtractTitleBlockProperties(keywordsXml);

        // Primary path: Use ordered sheet names from native SheetNames stream if available
        if (sheetNamesFromStream != null && sheetNamesFromStream.Count > 0)
        {
            int totalCount = sheetNamesFromStream.Count;
            for (int i = 0; i < totalCount; i++)
            {
                string sName = sheetNamesFromStream[i];
                var views = new List<DrawingViewInfo>();

                XElement? sheetElem = null;
                if (doc != null)
                {
                    sheetElem = doc.Descendants()
                        .FirstOrDefault(e => e.Name.LocalName == "Sheet" &&
                                             string.Equals(e.Attribute("Name")?.Value, sName, StringComparison.OrdinalIgnoreCase) &&
                                             !string.Equals(e.Attribute("Type")?.Value, "图纸格式", StringComparison.OrdinalIgnoreCase) &&
                                             !string.Equals(e.Attribute("Type")?.Value, "SheetFormat", StringComparison.OrdinalIgnoreCase));
                }

                if (sheetElem != null)
                {
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
                }

                // Match and enhance views with MFC metadata, without adding views from other sheets
                if (views.Count > 0)
                {
                    foreach (var mv in mfcViews)
                    {
                        var existing = views.FirstOrDefault(v =>
                            v.Name.Equals(mv.Name, StringComparison.OrdinalIgnoreCase) ||
                            v.Name.StartsWith(mv.Name + " ", StringComparison.OrdinalIgnoreCase) ||
                            v.Name.StartsWith(mv.Name + "(", StringComparison.OrdinalIgnoreCase) ||
                            mv.Name.StartsWith(v.Name + " ", StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.MfcClassName = mv.MfcClassName;
                            existing.ViewType = mv.ViewType;
                            existing.Scale = mv.Scale;
                            existing.TagOffset = mv.TagOffset;
                            if (string.IsNullOrEmpty(existing.ReferencedConfiguration) && !string.IsNullOrEmpty(mv.ReferencedConfiguration))
                            {
                                existing.ReferencedConfiguration = mv.ReferencedConfiguration;
                            }
                        }
                        else if (totalCount == 1 && !mv.Name.StartsWith("*"))
                        {
                            views.Add(mv);
                        }
                    }
                }
                else if (totalCount == 1)
                {
                    // For single-sheet drawings with no explicit views in XML, adopt MFC views
                    foreach (var mv in mfcViews.Where(v => !v.Name.StartsWith("*")))
                    {
                        views.Add(mv);
                    }
                }

                byte[]? preview = getSheetPreview(i);
                byte[]? highRes = getHighResPreview?.Invoke(i);

                DrawingSheetVectorData? vectorData = null;
                if ((definitionStream != null && definitionStream.Length > 0) ||
                    (displayListsStream != null && displayListsStream.Length > 0))
                {
                    vectorData = DrawingVectorReader.ReadVectorData(
                        definitionStream,
                        keywordsXml,
                        sheetIndex: i,
                        vbListsStream: vbListsStream,
                        displayListsStream: displayListsStream,
                        totalSheetsCount: totalCount);

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
                    Name = sName,
                    SheetIndex = i,
                    PreviewPng = preview,
                    HighResPreview = highRes,
                    VectorData = vectorData,
                    TechnicalNotes = technicalNotes.AsReadOnly(),
                    TitleBlockProperties = titleBlockProps,
                    Views = views.AsReadOnly()
                });
            }

            return sheets.AsReadOnly();
        }

        // Secondary path: Parse from KeyWords XML when no SheetNames stream
        if (doc != null)
        {
            var sheetElems = doc.Descendants()
                .Where(e => e.Name.LocalName == "Sheet" &&
                            !string.Equals(e.Attribute("Type")?.Value, "图纸格式", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(e.Attribute("Type")?.Value, "SheetFormat", StringComparison.OrdinalIgnoreCase))
                .ToList();

            int totalCount = sheetElems.Count;
            int sheetIdx = 0;
            foreach (var sheetElem in sheetElems)
            {
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

                if (views.Count > 0)
                {
                    foreach (var mv in mfcViews)
                    {
                        var existing = views.FirstOrDefault(v =>
                            v.Name.Equals(mv.Name, StringComparison.OrdinalIgnoreCase) ||
                            v.Name.StartsWith(mv.Name + " ", StringComparison.OrdinalIgnoreCase) ||
                            v.Name.StartsWith(mv.Name + "(", StringComparison.OrdinalIgnoreCase) ||
                            mv.Name.StartsWith(v.Name + " ", StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.MfcClassName = mv.MfcClassName;
                            existing.ViewType = mv.ViewType;
                            existing.Scale = mv.Scale;
                            existing.TagOffset = mv.TagOffset;
                            if (string.IsNullOrEmpty(existing.ReferencedConfiguration) && !string.IsNullOrEmpty(mv.ReferencedConfiguration))
                            {
                                existing.ReferencedConfiguration = mv.ReferencedConfiguration;
                            }
                        }
                        else if (totalCount == 1 && !mv.Name.StartsWith("*"))
                        {
                            views.Add(mv);
                        }
                    }
                }
                else if (totalCount == 1)
                {
                    foreach (var mv in mfcViews.Where(v => !v.Name.StartsWith("*")))
                    {
                        views.Add(mv);
                    }
                }

                byte[]? preview = getSheetPreview(sheetIdx);
                byte[]? highRes = getHighResPreview?.Invoke(sheetIdx);

                DrawingSheetVectorData? vectorData = null;
                if ((definitionStream != null && definitionStream.Length > 0) ||
                    (displayListsStream != null && displayListsStream.Length > 0))
                {
                    vectorData = DrawingVectorReader.ReadVectorData(
                        definitionStream,
                        keywordsXml,
                        sheetIndex: sheetIdx,
                        vbListsStream: vbListsStream,
                        displayListsStream: displayListsStream,
                        totalSheetsCount: totalCount);

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
                    TechnicalNotes = technicalNotes.AsReadOnly(),
                    TitleBlockProperties = titleBlockProps,
                    Views = views.AsReadOnly()
                });

                sheetIdx++;
            }
        }

        return sheets.AsReadOnly();
    }
}
