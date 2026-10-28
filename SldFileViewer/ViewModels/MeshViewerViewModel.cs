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

        for (int i = 0; i < mesh.Normals.Count; i++)
        {
            var n = mesh.Normals[i];
            geo.Normals.Add(new Vector3D(n.X, n.Y, n.Z));
        }

        for (int i = 0; i < mesh.TriangleIndices.Count; i++)
        {
            geo.TriangleIndices.Add(mesh.TriangleIndices[i]);
        }

        geo.Freeze();
        WpfMesh = geo;
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
