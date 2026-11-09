using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SldFile;
using SldFile.Assembly;
using SldFile.Geometry.Step;
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

    public void ExportMeshToGlb(DisplayMesh mesh, string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        mesh.WriteGlb(stream);
    }

    public void ExportParasolid(SwFile file, string filePath, int configIndex = 0)
    {
        file.ExportParasolid(filePath, configIndex);
    }

    public void ExportStep(SwFile file, string filePath, string? configName = null)
    {
        if (file is SldPart part)
        {
            part.ExportStep(filePath, configName);
            return;
        }

        var mesh = file.GetDisplayMesh();
        if (mesh != null && mesh.Positions.Count >= 3)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.ASCII);
            StepWriter.WriteMesh(mesh, writer, Path.GetFileNameWithoutExtension(filePath));
            return;
        }

        throw new InvalidOperationException("当前文档没有可导出的 Parasolid 或三维网格几何。");
    }

    public void ExportDrawingToSvg(SldDrawing drawing, string filePath, int sheetIndex = 0)
    {
        drawing.ExportSheetSvg(sheetIndex, filePath);
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

    public void ExportBomToJson(SldAssembly assembly, string filePath, BomOptions? options = null)
    {
        var result = assembly.BuildBom(options);
        File.WriteAllText(filePath, result.ToJson(indented: true), Encoding.UTF8);
    }

    private static string EscapeCsv(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("\"", "\"\"");
    }
}
