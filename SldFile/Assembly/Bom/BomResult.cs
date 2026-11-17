using System;
using System.Collections.Generic;
using System.Text;

namespace SldFile.Assembly.Bom;

/// <summary>
/// Contains generated tree-style and flat aggregated BOM representations.
/// </summary>
public sealed class BomResult
{
    public IReadOnlyList<BomItem> Tree { get; set; } = Array.Empty<BomItem>();
    public IReadOnlyList<BomItem> Flat { get; set; } = Array.Empty<BomItem>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Serializes the BOM hierarchy and flat summary into standard JSON.
    /// </summary>
    public string ToJson(bool indented = true)
    {
        var sb = new StringBuilder();
        string nl = indented ? "\n" : "";
        string tab = indented ? "  " : "";
        string tab2 = indented ? "    " : "";
        string tab3 = indented ? "      " : "";

        sb.Append("{" + nl);
        sb.Append(tab + "\"flat\": [" + nl);
        for (int i = 0; i < Flat.Count; i++)
        {
            var item = Flat[i];
            sb.Append(tab2 + "{");
            sb.Append($"\"item\": {item.ItemNumber}, \"partNumber\": \"{EscapeJson(item.PartNumber)}\", \"description\": \"{EscapeJson(item.Description)}\", \"configuration\": \"{EscapeJson(item.ConfigurationName)}\", \"quantity\": {item.Quantity}, \"filePath\": \"{EscapeJson(item.FilePath)}\"");
            if (item.ResolvedFilePath != null)
                sb.Append($", \"resolvedPath\": \"{EscapeJson(item.ResolvedFilePath)}\"");
            sb.Append("}");
            if (i < Flat.Count - 1) sb.Append(",");
            sb.Append(nl);
        }
        sb.Append(tab + "]," + nl);

        sb.Append(tab + "\"warnings\": [" + nl);
        for (int i = 0; i < Warnings.Count; i++)
        {
            sb.Append(tab2 + $"\"{EscapeJson(Warnings[i])}\"");
            if (i < Warnings.Count - 1) sb.Append(",");
            sb.Append(nl);
        }
        sb.Append(tab + "]" + nl);
        sb.Append("}");

        return sb.ToString();
    }

    private static string EscapeJson(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s!.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }
}
