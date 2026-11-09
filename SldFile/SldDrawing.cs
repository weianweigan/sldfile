using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Mfc;

namespace SldFile;

public partial class SldDrawing : SwFile
{
    private IReadOnlyList<DrawingSheetInfo>? _sheets;
    private DrawingDocumentMfcModel? _drawingDoc;
    private IReadOnlyList<DrViewBucketMfcModel>? _viewBuckets;

    public SldDrawing(string filePath, bool readOnly = true) : base(filePath, readOnly)
    {
    }

    /// <summary>All drawing sheets identified in this drawing document.</summary>
    public IReadOnlyList<DrawingSheetInfo> Sheets => _sheets ??= GetDrawingSheets();

    /// <summary>
    /// Gets the strongly-typed Drawing Document MFC model parsed from Contents/Definition.
    /// </summary>
    internal DrawingDocumentMfcModel GetDrawingDocument()
    {
        if (_drawingDoc != null) return _drawingDoc;

        byte[]? def = null;
        if (RawStreams.TryGetValue("Contents/Definition", out var d1) && d1 != null) def = d1;
        else if (RawStreams.TryGetValue("Definition", out var d2) && d2 != null) def = d2;

        if (def != null)
        {
            _drawingDoc = DrawingDocumentMfcModel.FromBytes(def);
        }
        else
        {
            _drawingDoc = new DrawingDocumentMfcModel();
        }

        if (_drawingDoc.Sheets.Count == 0 && Sheets.Count > 0)
        {
            foreach (var s in Sheets)
            {
                var drSheet = new DrSheetMfcModel(s.Name);
                foreach (var v in s.Views)
                {
                    drSheet.Views.Add(new DrViewMfcModel(v.Name, 0.1, 0.1)
                    {
                        ReferencedModel = v.ReferencedModelPath,
                        ReferencedConfiguration = v.ReferencedConfiguration
                    });
                }
                _drawingDoc.Sheets.Add(drSheet);
            }
        }

        if (_drawingDoc.Views.Count == 0)
        {
            foreach (var s in _drawingDoc.Sheets)
            {
                foreach (var v in s.Views)
                {
                    _drawingDoc.Views.Add(v);
                }
            }
        }

        return _drawingDoc;
    }

    /// <summary>
    /// Gets all cached vector display buckets parsed from Contents/VBLists or Contents/VBLists__ZLB.
    /// </summary>
    internal IReadOnlyList<DrViewBucketMfcModel> GetViewBuckets()
    {
        if (_viewBuckets != null) return _viewBuckets;

        byte[]? vblists = null;
        if (RawStreams.TryGetValue("Contents/VBLists", out var raw) && raw != null)
        {
            vblists = raw;
        }
        else if (RawStreams.TryGetValue("Contents/VBLists__ZLB", out var zlb) && zlb != null)
        {
            vblists = zlb;
        }

        if (vblists != null)
        {
            _viewBuckets = DrViewBucketMfcModel.ParseAllBuckets(vblists);
        }
        else
        {
            _viewBuckets = Array.Empty<DrViewBucketMfcModel>();
        }

        return _viewBuckets;
    }

    /// <summary>
    /// Renders the specified sheet's vector geometry and annotations as a standard W3C SVG XML string.
    /// </summary>
    public string ExportSheetSvg(int sheetIndex = 0)
    {
        var preview = GetDrawingPreview(sheetIndex);
        return Drawing.DrawingPreviewSvgWriter.Write(preview, GetSheetPreviewPng(sheetIndex));
    }

    /// <summary>
    /// Exports the specified sheet as an SVG vector file to the given output path.
    /// </summary>
    public void ExportSheetSvg(int sheetIndex, string outputPath)
    {
        string svg = ExportSheetSvg(sheetIndex);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(outputPath, svg, Encoding.UTF8);
    }
}
