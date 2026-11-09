using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SldFile.Mfc;

namespace SldFile;

public class SldDrawing : SwFile
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
    public DrawingDocumentMfcModel GetDrawingDocument()
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
    public IReadOnlyList<DrViewBucketMfcModel> GetViewBuckets()
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
        var doc = GetDrawingDocument();
        var buckets = GetViewBuckets();
        var sheet = (sheetIndex >= 0 && sheetIndex < doc.Sheets.Count) ? doc.Sheets[sheetIndex] : null;

        double width = sheet?.Width ?? 0.420;
        double height = sheet?.Height ?? 0.297;
        if (width <= 0.01) width = 0.420;
        if (height <= 0.01) height = 0.297;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" width=\"{0:F0}mm\" height=\"{1:F0}mm\" viewBox=\"0 0 {2:F4} {3:F4}\">",
            width * 1000.0, height * 1000.0, width, height));

        sb.AppendLine("  <defs>");
        sb.AppendLine("    <style>");
        sb.AppendLine("      .sheet-border { stroke: #000000; stroke-width: 0.0007; fill: none; }");
        sb.AppendLine("      .view-line { stroke: #000000; stroke-width: 0.00035; fill: none; stroke-linecap: round; stroke-linejoin: round; }");
        sb.AppendLine("      .hidden-line { stroke: #888888; stroke-width: 0.00025; stroke-dasharray: 0.002,0.002; fill: none; }");
        sb.AppendLine("      .dimension-line { stroke: #0055A5; stroke-width: 0.00025; fill: none; }");
        sb.AppendLine("      .annotation-text { font-family: 'Arial', sans-serif; font-size: 0.0035px; fill: #000000; }");
        sb.AppendLine("      .dimension-text { font-family: 'Arial', sans-serif; font-size: 0.003px; fill: #0055A5; text-anchor: middle; }");
        sb.AppendLine("      .balloon-circle { stroke: #000000; stroke-width: 0.00035; fill: #FFFFFF; }");
        sb.AppendLine("      .balloon-text { font-family: 'Arial', sans-serif; font-size: 0.004px; fill: #000000; text-anchor: middle; dominant-baseline: central; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("  </defs>");

        // Background
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "  <rect width=\"{0:F4}\" height=\"{1:F4}\" fill=\"#FFFFFF\" />", width, height));

        // Format border
        sb.AppendLine("  <g id=\"sheet-format\">");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "    <rect x=\"{0:F4}\" y=\"{1:F4}\" width=\"{2:F4}\" height=\"{3:F4}\" class=\"sheet-border\" />",
            0.020, 0.010, width - 0.030, height - 0.020));
        sb.AppendLine("  </g>");

        // Views from buckets and document
        sb.AppendLine("  <g id=\"views\">");
        int bucketIndex = 0;
        foreach (var bucket in buckets)
        {
            double viewPosX = width * 0.5;
            double viewPosY = height * 0.5;
            if (bucketIndex < doc.Views.Count)
            {
                var v = doc.Views[bucketIndex];
                if (v.PositionX > 0.001) viewPosX = v.PositionX;
                if (v.PositionY > 0.001) viewPosY = v.PositionY;
            }

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "    <g id=\"view-{0}\" transform=\"translate({1:F4}, {2:F4})\">",
                bucketIndex, viewPosX, viewPosY));

            foreach (var line in bucket.VisibleLines)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      <line x1=\"{0:F4}\" y1=\"{1:F4}\" x2=\"{2:F4}\" y2=\"{3:F4}\" class=\"view-line\" />",
                    line.Start.X, -line.Start.Y, line.End.X, -line.End.Y));
            }
            foreach (var line in bucket.HiddenLines)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "      <line x1=\"{0:F4}\" y1=\"{1:F4}\" x2=\"{2:F4}\" y2=\"{3:F4}\" class=\"hidden-line\" />",
                    line.Start.X, -line.Start.Y, line.End.X, -line.End.Y));
            }

            sb.AppendLine("    </g>");
            bucketIndex++;
        }
        sb.AppendLine("  </g>");

        // Dimensions
        sb.AppendLine("  <g id=\"dimensions\">");
        foreach (var dim in doc.Dimensions)
        {
            double dx = dim.PositionX > 0.001 ? dim.PositionX : width * 0.5;
            double dy = dim.PositionY > 0.001 ? dim.PositionY : height * 0.5;
            string text = !string.IsNullOrEmpty(dim.Text) ? dim.Text :
                (dim.Value.HasValue ? dim.Value.Value.ToString("F2", CultureInfo.InvariantCulture) : "");

            if (!string.IsNullOrEmpty(text))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    <text x=\"{0:F4}\" y=\"{1:F4}\" class=\"dimension-text\">{2}</text>",
                    dx, dy, System.Security.SecurityElement.Escape(text)));
            }
        }
        sb.AppendLine("  </g>");

        // Balloons
        sb.AppendLine("  <g id=\"balloons\">");
        foreach (var note in doc.Notes.Where(n => n.IsBalloon))
        {
            double bx = note.PositionX > 0.001 ? note.PositionX : width * 0.5;
            double by = note.PositionY > 0.001 ? note.PositionY : height * 0.5;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "    <circle cx=\"{0:F4}\" cy=\"{1:F4}\" r=\"0.005\" class=\"balloon-circle\" />", bx, by));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "    <text x=\"{0:F4}\" y=\"{1:F4}\" class=\"balloon-text\">{2}</text>",
                bx, by, System.Security.SecurityElement.Escape(note.Text)));
        }
        sb.AppendLine("  </g>");

        // Notes
        sb.AppendLine("  <g id=\"notes\">");
        foreach (var note in doc.Notes.Where(n => !n.IsBalloon))
        {
            double nx = note.PositionX > 0.001 ? note.PositionX : 0.025;
            double ny = note.PositionY > 0.001 ? note.PositionY : 0.025;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "    <text x=\"{0:F4}\" y=\"{1:F4}\" class=\"annotation-text\">{2}</text>",
                nx, ny, System.Security.SecurityElement.Escape(note.Text)));
        }
        sb.AppendLine("  </g>");

        sb.AppendLine("</svg>");
        return sb.ToString();
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
