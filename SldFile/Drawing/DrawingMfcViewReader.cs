using System;
using System.Collections.Generic;
using System.Linq;
using SldFile.Mfc;

namespace SldFile.Drawing;

/// <summary>
/// Deserializes MFC drawing view definitions, technical notes, and title block metadata
/// directly from the SolidWorks Contents/Definition stream.
/// </summary>
internal static class DrawingMfcViewReader
{
    private static readonly (string ClassName, string DisplayType)[] ViewClassTypes =
    {
        ("moAbsoluteView", "主视图 / 绝对视图"),
        ("moUnfoldedView", "钣金展开视图"),
        ("moDetailView", "局部放大视图"),
        ("moView_c", "工程图视图")
    };

    public static List<DrawingViewInfo> ReadViewsFromDefinition(byte[] defBytes, MfcDocument? doc = null)
    {
        var views = new List<DrawingViewInfo>();
        if (defBytes == null || defBytes.Length < 100) return views;

        doc ??= MfcDocument.Parse(defBytes);

        foreach (var (className, displayType) in ViewClassTypes)
        {
            var elements = doc.FindAll(className);
            foreach (var elem in elements)
            {
                var viewInfo = ParseSingleMfcView(elem, className, displayType, defBytes);
                if (viewInfo != null)
                {
                    views.Add(viewInfo);
                }
            }
        }

        return views;
    }

    private static DrawingViewInfo? ParseSingleMfcView(MfcElement elem, string className, string displayType, byte[] defBytes)
    {
        string viewName = string.Empty;
        string configName = string.Empty;

        // Scan WChar strings in this view object
        var reader = elem.CreateReader();
        var wchars = new List<string>();
        while (reader.Remaining > 3)
        {
            if (reader.IsWCharPrefix)
            {
                string? s = reader.ReadWChar();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    wchars.Add(s!);
                }
            }
            else
            {
                reader.Skip(1);
            }
        }

        foreach (var s in wchars)
        {
            if (s.StartsWith("工程图视图", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("局部视图", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("向视图", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("剖面", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Drawing View", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Detail View", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Section View", StringComparison.OrdinalIgnoreCase) ||
                s == "*全图纸")
            {
                if (string.IsNullOrEmpty(viewName)) viewName = s;
            }
            else if (s.StartsWith("细节", StringComparison.OrdinalIgnoreCase) ||
                     s.StartsWith("Detail", StringComparison.OrdinalIgnoreCase) ||
                     s.Equals("默认", StringComparison.OrdinalIgnoreCase) ||
                     s.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(configName)) configName = s;
            }
        }

        if (string.IsNullOrEmpty(viewName) && wchars.Count > 0)
        {
            viewName = wchars[0];
        }

        if (string.IsNullOrEmpty(viewName))
        {
            viewName = $"{displayType} [Tag 0x{elem.TagOffset:X}]";
        }

        // Try extracting scale (e.g. 2.0 at offset 0x28 or 0x50)
        string scaleStr = string.Empty;
        if (elem.DataLength >= 0x30)
        {
            for (int off = 0x20; off <= Math.Min(elem.DataLength - 8, 0x60); off += 4)
            {
                double d = BitConverter.ToDouble(defBytes, elem.TagOffset + off);
                if (d is 1.0 or 2.0 or 5.0 or 10.0 or 20.0 or 50.0 or 100.0)
                {
                    scaleStr = (d >= 1.0) ? $"1 : {d:0}" : $"{1.0 / d:0} : 1";
                    break;
                }
            }
        }

        return new DrawingViewInfo
        {
            Name = viewName,
            ViewType = displayType,
            ReferencedConfiguration = configName,
            MfcClassName = className,
            Scale = scaleStr,
            TagOffset = elem.TagOffset
        };
    }

    /// <summary>
    /// Extracts technical requirements notes from KeyWords XML or Definition stream.
    /// </summary>
    public static List<string> ExtractTechnicalNotes(string? keywordsXml)
    {
        var notes = new List<string>();
        if (string.IsNullOrWhiteSpace(keywordsXml)) return notes;

        try
        {
            int start = keywordsXml!.IndexOf('<');
            if (start >= 0)
            {
                var doc = System.Xml.Linq.XDocument.Parse(keywordsXml.Substring(start));
                foreach (var noteElem in doc.Descendants().Where(e => e.Name.LocalName == "Note"))
                {
                    string text = noteElem.Value?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(text)) continue;

                    // Filter technical requirements notes
                    if (text.StartsWith("1.") || text.StartsWith("2.") || text.StartsWith("3.") ||
                        text.StartsWith("4.") || text.StartsWith("5.") ||
                        text.Contains("锐边倒角") || text.Contains("未注折弯") || text.Contains("表面粗糙度") ||
                        text.Contains("技术要求") || text.Contains("去毛刺"))
                    {
                        if (!notes.Contains(text))
                        {
                            notes.Add(text);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore XML parsing errors
        }

        return notes;
    }

    /// <summary>
    /// Extracts title block key properties (e.g. Drawing No, Scale, Material) from KeyWords XML.
    /// </summary>
    public static Dictionary<string, string> ExtractTitleBlockProperties(string? keywordsXml)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(keywordsXml)) return props;

        try
        {
            int start = keywordsXml!.IndexOf('<');
            if (start >= 0)
            {
                var doc = System.Xml.Linq.XDocument.Parse(keywordsXml.Substring(start));

                // Extract Sheet format properties
                var sheetElem = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sheet");
                if (sheetElem != null)
                {
                    var scaleNum = sheetElem.Elements().FirstOrDefault(e => e.Name.LocalName == "ScaleNumerator")?.Value;
                    var scaleDen = sheetElem.Elements().FirstOrDefault(e => e.Name.LocalName == "ScaleDenominator")?.Value;
                    if (!string.IsNullOrEmpty(scaleNum) && !string.IsNullOrEmpty(scaleDen))
                    {
                        if (double.TryParse(scaleNum, out var num) && double.TryParse(scaleDen, out var den))
                        {
                            props["比例 (Scale)"] = $"{num:0} : {den:0}";
                        }
                    }

                    var template = sheetElem.Elements().FirstOrDefault(e => e.Name.LocalName == "TemplateName")?.Value;
                    if (!string.IsNullOrEmpty(template))
                    {
                        props["图纸模板 (Template)"] = System.IO.Path.GetFileName(template);
                    }
                }

                // Extract Drawing Number from Keywords name or notes
                var root = doc.Root;
                if (root != null && !string.IsNullOrEmpty(root.Attribute("Name")?.Value))
                {
                    props["图纸代号 (Drawing No.)"] = root.Attribute("Name")!.Value;
                }
            }
        }
        catch
        {
            // Ignore XML parsing errors
        }

        return props;
    }
}
