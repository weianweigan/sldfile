using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile;

/// <summary>Reads sheet metadata and the saved native preview without a second vector model.</summary>
internal static class DrawingReader
{
    internal static bool IsSheetFormat(string? type) =>
        string.Equals(type, "图纸格式", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "SheetFormat", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, "Sheet Format", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<DrawingSheetInfo> ReadSheets(
        string? keywordsXml, IReadOnlyList<string> sheetNamesFromStream,
        Func<int, byte[]?> getSheetPreview, Func<int, byte[]?>? getHighResPreview = null,
        byte[]? definitionStream = null, byte[]? vbListsStream = null, byte[]? displayListsStream = null,
        int documentVersion = 0, string? sheetFormatData = null)
    {
        var sheets = new List<DrawingSheetInfo>();
        var formats = DrawingSheetFormatInfo.ParseAll(sheetFormatData);
        var xmlSheets = Array.Empty<XElement>();
        int start = keywordsXml?.IndexOf('<') ?? -1;
        if (start >= 0)
        {
            try
            {
                xmlSheets = XDocument.Parse(keywordsXml!.Substring(start)).Descendants()
                    .Where(e => e.Name.LocalName == "Sheet" && !IsSheetFormat(e.Attribute("Type")?.Value)).ToArray();
            }
            catch (XmlException) { }
        }
        int count = Math.Max(1, xmlSheets.Length > 0 ? xmlSheets.Length : sheetNamesFromStream.Count);
        var display = new DrawingPreviewStreamModel { DecodeCommands = true };
        using (var ar = MfcArchive.CreateReader(displayListsStream ?? Array.Empty<byte>()))
        {
            ar.DocumentVersion = documentVersion; display.Serialize(ar);
        }
        // Read the native Definition prefix once for all sheets. The remaining
        // root data stays opaque in its lossless serializer.
        var definition = DrawingViewCacheArchive.Read(definitionStream, true, Array.Empty<string>(), documentVersion);
        var nativeDisplay = display.NativeArchive;
        var viewNames = definition.Fragments.Select(f => f.Owner).ToArray();
        if (viewNames.Length == 0 && xmlSheets.Length == 1)
            viewNames = xmlSheets[0].Elements().Where(e => e.Name.LocalName == "View").Select(e => e.Attribute("Name")?.Value ?? string.Empty).ToArray();
        DrawingViewCacheArchive? buckets = count == 1 && nativeDisplay?.Views.Length > 0
            ? DrawingViewCacheArchive.Read(vbListsStream, false, viewNames, documentVersion, true)
            : null;
        for (int i = 0; i < count; i++)
        {
            var element = i < xmlSheets.Length ? xmlSheets[i] : null;
            string name = element?.Attribute("Name")?.Value ?? (i < sheetNamesFromStream.Count ? sheetNamesFromStream[i] : "Sheet1");
            var format = formats.FirstOrDefault(f => string.Equals(f.SheetName, name, StringComparison.OrdinalIgnoreCase))
                ?? (i < formats.Count ? formats[i] : formats.FirstOrDefault());
            var preview = display.ToDefinition(i, count);
            var nativeSheet = i < definition.NativeSheets.Length ? definition.NativeSheets[i].NativeSheet : null;
            if (format != null)
            {
                preview.PaperWidth = format.PaperWidthMm / 1000;
                preview.PaperHeight = format.PaperHeightMm / 1000;
            }
            else if (nativeSheet != null && nativeSheet.State3F0 > 0 && nativeSheet.State3F8 > 0)
            {
                preview.PaperWidth = nativeSheet.State3F0;
                preview.PaperHeight = nativeSheet.State3F8;
            }
            if (buckets != null && nativeDisplay != null) DrawingPreviewComposer.AppendViews(preview, nativeDisplay, buckets, viewNames);
            else if (nativeDisplay?.Views.Length > 0)
                preview.AddIssue("ViewCacheUnavailable", "Saved view geometry is unavailable for this sheet.", "Contents/VBLists");
            sheets.Add(new DrawingSheetInfo
            {
                Name = name, SheetIndex = i, PreviewPng = getSheetPreview(i), HighResPreview = getHighResPreview?.Invoke(i),
                Preview = preview, FormatInfo = format,
                Views = element?.Elements().Where(e => e.Name.LocalName == "View").Select(e => new DrawingViewInfo
                {
                    Name = e.Attribute("Name")?.Value ?? string.Empty,
                    ReferencedModelPath = e.Value.Trim(), ReferencedConfiguration = e.Attribute("Description")?.Value ?? string.Empty
                }).ToArray() ?? Array.Empty<DrawingViewInfo>()
            });
        }
        return sheets;
    }
}
