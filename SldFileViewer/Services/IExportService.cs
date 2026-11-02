using System.Collections.Generic;
using SldFile;
using SldFileViewer.Models;

namespace SldFileViewer.Services;

public interface IExportService
{
    void ExportMeshToObj(DisplayMesh mesh, string filePath);
    void ExportMeshToStl(DisplayMesh mesh, string filePath);
    void ExportMeshToGlb(DisplayMesh mesh, string filePath);
    void ExportParasolid(SwFile file, string filePath, int configIndex = 0);
    void ExportBomToCsv(IEnumerable<BomDisplayItem> items, string filePath);
}
