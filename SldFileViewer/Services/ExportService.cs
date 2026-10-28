using System.Collections.Generic;
using System.IO;
using System.Text;
using SldFile;
using SldFileViewer.Models;

namespace SldFileViewer.Services;

public sealed class ExportService : IExportService
{
    public void ExportMeshToObj(DisplayMesh mesh, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        mesh.WriteObj(writer);
    }

    public void ExportMeshToStl(DisplayMesh mesh, string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        mesh.WriteStl(stream);
    }

    public void ExportParasolid(SwFile file, string filePath, int configIndex = 0)
    {
        file.ExportParasolid(filePath, configIndex);
    }

    public void ExportBomToCsv(IEnumerable<BomDisplayItem> items, string filePath)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        writer.WriteLine("ItemNo,ComponentName,Quantity,Configuration,IsVirtual,Description,FilePath");
        foreach (var item in items)
        {
            string line = $"{item.ItemNumber},\"{EscapeCsv(item.ComponentName)}\",{item.Quantity},\"{EscapeCsv(item.Configuration)}\",{item.IsVirtual},\"{EscapeCsv(item.Description)}\",\"{EscapeCsv(item.FilePath)}\"";
            writer.WriteLine(line);
        }
    }

    private static string EscapeCsv(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}
