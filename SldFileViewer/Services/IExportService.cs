using System.Collections.Generic;
using SldFile;
using SldFileViewer.Models;

namespace SldFileViewer.Services;

public interface IExportService
{
    void ExportMeshToObj(DisplayMesh mesh, string filePath);
    void ExportMeshToStl(DisplayMesh mesh, string filePath);
    void ExportMeshToGlb(DisplayMesh mesh, string filePath);
    void ExportMeshToHtml(DisplayMesh mesh, string filePath, string? modelTitle = null);
    void ExportParasolid(SwFile file, string filePath, int configIndex = 0);
    void ExportBomToCsv(IEnumerable<BomDisplayItem> items, string filePath);
    void ExportDrawingToSvg(SldDrawing drawing, string filePath, int sheetIndex = 0);
    void ExportDrawingToDxf(SldDrawing drawing, string filePath, int sheetIndex = 0);
}
