using System;
using System.IO;
using System.Windows.Media.Media3D;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class MeshViewerViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private DisplayMesh? _mesh;

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private int _triangleCount;

    [ObservableProperty]
    private int _edgeCount;

    [ObservableProperty]
    private bool _hasMesh;

    [ObservableProperty]
    private bool _hasParasolid;

    [ObservableProperty]
    private string _dimensionsText = string.Empty;

    [ObservableProperty]
    private MeshGeometry3D? _wpfMesh;

    [ObservableProperty]
    private MeshGeometry3D? _wpfEdgeMesh;

    [ObservableProperty]
    private bool _showEdges = true;

    public MeshViewerViewModel(ISwDocumentService docService, IExportService exportService, IDialogService dialogService)
    {
        _docService = docService;
        _exportService = exportService;
        _dialogService = dialogService;
    }

    public void LoadMesh()
    {
        var mesh = _docService.GetDisplayMesh();
        Mesh = mesh;
        HasMesh = mesh != null && mesh.VertexCount > 0;

        HasParasolid = _docService.CurrentFile != null &&
            (_docService.CurrentFile.ExtractParasolid(0) != null);

        if (mesh != null)
        {
            VertexCount = mesh.VertexCount;
            TriangleCount = mesh.TriangleCount;
            EdgeCount = mesh.EdgeCount;

            var size = mesh.Max - mesh.Min;
            DimensionsText = $"{size.X:F1} x {size.Y:F1} x {size.Z:F1} mm";

            BuildWpfMesh(mesh);
        }
        else
        {
            VertexCount = 0;
            TriangleCount = 0;
            EdgeCount = 0;
            DimensionsText = "-";
            WpfMesh = null;
            WpfEdgeMesh = null;
        }
    }

    private void BuildWpfMesh(DisplayMesh mesh)
    {
        var geo = new MeshGeometry3D();

        // SolidWorks positions are in mm; center around origin
        var center = (mesh.Min + mesh.Max) * 0.5f;

        for (int i = 0; i < mesh.Positions.Count; i++)
        {
            var p = mesh.Positions[i] - center;
            geo.Positions.Add(new Point3D(p.X, p.Y, p.Z));
        }

        // Only supply normals if exact count matches vertex count (WPF requirement)
        if (mesh.Normals.Count == mesh.Positions.Count)
        {
            for (int i = 0; i < mesh.Normals.Count; i++)
            {
                var n = mesh.Normals[i];
                geo.Normals.Add(new Vector3D(n.X, n.Y, n.Z));
            }
        }

        int posCount = mesh.Positions.Count;
        for (int i = 0; i + 2 < mesh.TriangleIndices.Count; i += 3)
        {
            int i0 = mesh.TriangleIndices[i];
            int i1 = mesh.TriangleIndices[i + 1];
            int i2 = mesh.TriangleIndices[i + 2];
            if (i0 >= 0 && i0 < posCount &&
                i1 >= 0 && i1 < posCount &&
                i2 >= 0 && i2 < posCount)
            {
                geo.TriangleIndices.Add(i0);
                geo.TriangleIndices.Add(i1);
                geo.TriangleIndices.Add(i2);
            }
        }

        geo.Freeze();
        WpfMesh = geo;

        BuildEdgeMesh(mesh, center);
    }

    private void BuildEdgeMesh(DisplayMesh mesh, System.Numerics.Vector3 center)
    {
        if (mesh.OutlineEdges == null || mesh.OutlineEdges.Count == 0 || mesh.Positions.Count == 0)
        {
            WpfEdgeMesh = null;
            return;
        }

        var edgeGeo = new MeshGeometry3D();
        var size = mesh.Max - mesh.Min;
        double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
        float thickness = (float)Math.Clamp(maxDim * 0.0012, 0.05, 5.0);

        int posCount = mesh.Positions.Count;
        int vertexBase = 0;

        foreach (var (start, end) in mesh.OutlineEdges)
        {
            if (start < 0 || start >= posCount || end < 0 || end >= posCount || start == end)
                continue;

            var p0 = mesh.Positions[start] - center;
            var p1 = mesh.Positions[end] - center;

            var dir = p1 - p0;
            float len = dir.Length();
            if (len < 1e-4f) continue;

            // Compute perpendicular vectors
            var v = Math.Abs(dir.X) > 0.9f * len ? new System.Numerics.Vector3(0, 1, 0) : new System.Numerics.Vector3(1, 0, 0);
            var u = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(dir, v)) * thickness;
            var w = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(dir, u)) * thickness;

            // 8 vertices for 2 crossed quads
            edgeGeo.Positions.Add(new Point3D(p0.X - u.X, p0.Y - u.Y, p0.Z - u.Z));
            edgeGeo.Positions.Add(new Point3D(p0.X + u.X, p0.Y + u.Y, p0.Z + u.Z));
            edgeGeo.Positions.Add(new Point3D(p1.X + u.X, p1.Y + u.Y, p1.Z + u.Z));
            edgeGeo.Positions.Add(new Point3D(p1.X - u.X, p1.Y - u.Y, p1.Z - u.Z));

            edgeGeo.Positions.Add(new Point3D(p0.X - w.X, p0.Y - w.Y, p0.Z - w.Z));
            edgeGeo.Positions.Add(new Point3D(p0.X + w.X, p0.Y + w.Y, p0.Z + w.Z));
            edgeGeo.Positions.Add(new Point3D(p1.X + w.X, p1.Y + w.Y, p1.Z + w.Z));
            edgeGeo.Positions.Add(new Point3D(p1.X - w.X, p1.Y - w.Y, p1.Z - w.Z));

            // Quad 1 (front + back)
            edgeGeo.TriangleIndices.Add(vertexBase);
            edgeGeo.TriangleIndices.Add(vertexBase + 1);
            edgeGeo.TriangleIndices.Add(vertexBase + 2);
            edgeGeo.TriangleIndices.Add(vertexBase);
            edgeGeo.TriangleIndices.Add(vertexBase + 2);
            edgeGeo.TriangleIndices.Add(vertexBase + 3);

            edgeGeo.TriangleIndices.Add(vertexBase);
            edgeGeo.TriangleIndices.Add(vertexBase + 2);
            edgeGeo.TriangleIndices.Add(vertexBase + 1);
            edgeGeo.TriangleIndices.Add(vertexBase);
            edgeGeo.TriangleIndices.Add(vertexBase + 3);
            edgeGeo.TriangleIndices.Add(vertexBase + 2);

            // Quad 2 (front + back)
            edgeGeo.TriangleIndices.Add(vertexBase + 4);
            edgeGeo.TriangleIndices.Add(vertexBase + 5);
            edgeGeo.TriangleIndices.Add(vertexBase + 6);
            edgeGeo.TriangleIndices.Add(vertexBase + 4);
            edgeGeo.TriangleIndices.Add(vertexBase + 6);
            edgeGeo.TriangleIndices.Add(vertexBase + 7);

            edgeGeo.TriangleIndices.Add(vertexBase + 4);
            edgeGeo.TriangleIndices.Add(vertexBase + 6);
            edgeGeo.TriangleIndices.Add(vertexBase + 5);
            edgeGeo.TriangleIndices.Add(vertexBase + 4);
            edgeGeo.TriangleIndices.Add(vertexBase + 7);
            edgeGeo.TriangleIndices.Add(vertexBase + 6);

            vertexBase += 8;
        }

        edgeGeo.Freeze();
        WpfEdgeMesh = edgeGeo;
    }

    [RelayCommand]
    private void ExportObj()
    {
        if (Mesh == null) return;
        if (_dialogService.ShowSaveFileDialog("Model.obj", "Wavefront OBJ (*.obj)|*.obj", out var savePath))
        {
            try
            {
                _exportService.ExportMeshToObj(Mesh, savePath);
                _dialogService.ShowInformation("导出成功", $"3D 网格已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }

    [RelayCommand]
    private void ExportStl()
    {
        if (Mesh == null) return;
        if (_dialogService.ShowSaveFileDialog("Model.stl", "STL 几何模型 (*.stl)|*.stl", out var savePath))
        {
            try
            {
                _exportService.ExportMeshToStl(Mesh, savePath);
                _dialogService.ShowInformation("导出成功", $"STL 模型已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }

    [RelayCommand]
    private void ExportParasolid()
    {
        if (_docService.CurrentFile == null) return;
        if (_dialogService.ShowSaveFileDialog("Model.x_b", "Parasolid Transmit 二进制 (*.x_b)|*.x_b", out var savePath))
        {
            try
            {
                _exportService.ExportParasolid(_docService.CurrentFile, savePath);
                _dialogService.ShowInformation("导出成功", $"Parasolid 几何数据已导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", ex.Message);
            }
        }
    }
}
