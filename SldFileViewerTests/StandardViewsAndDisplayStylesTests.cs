using System;
using System.Collections.Generic;
using System.Numerics;
using SldFile;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using Xunit;

namespace SldFileViewerTests;

public class StandardViewsAndDisplayStylesTests
{
    private class DummyDialogService : IDialogService
    {
        public bool ShowOpenFileDialog(string filter, out string selectedPath) { selectedPath = string.Empty; return false; }
        public bool ShowSaveFileDialog(string defaultName, string filter, out string selectedPath) { selectedPath = string.Empty; return false; }
        public void ShowError(string title, string message) { }
        public void ShowInformation(string title, string message) { }
    }

    private static MeshViewerViewModel CreateViewModel()
    {
        var docService = new SwDocumentService();
        var exportService = new ExportService();
        var dialogService = new DummyDialogService();

        return new MeshViewerViewModel(docService, exportService, dialogService);
    }

    [Theory]
    [InlineData(StandardViewType.Isometric)]
    [InlineData(StandardViewType.Front)]
    [InlineData(StandardViewType.Back)]
    [InlineData(StandardViewType.Left)]
    [InlineData(StandardViewType.Right)]
    [InlineData(StandardViewType.Top)]
    [InlineData(StandardViewType.Bottom)]
    [InlineData(StandardViewType.NormalTo)]
    public void SetStandardView_UpdatesProperty_AndFiresEvent(StandardViewType viewType)
    {
        var vm = CreateViewModel();

        StandardViewType? receivedType = null;
        vm.RequestStandardView += t => receivedType = t;

        vm.SetStandardViewCommand.Execute(viewType);

        Assert.Equal(viewType, vm.CurrentStandardView);
        Assert.Equal(viewType, receivedType);
    }

    [Fact]
    public void SetDisplayStyle_Shaded_DisablesShowEdges()
    {
        var vm = CreateViewModel();
        vm.ShowEdges = true;

        vm.SetDisplayStyleCommand.Execute(ModelDisplayStyle.Shaded);

        Assert.Equal(ModelDisplayStyle.Shaded, vm.DisplayStyle);
        Assert.False(vm.ShowEdges);
    }

    [Fact]
    public void SetDisplayStyle_ShadedWithEdges_EnablesShowEdges()
    {
        var vm = CreateViewModel();
        vm.ShowEdges = false;

        vm.SetDisplayStyleCommand.Execute(ModelDisplayStyle.ShadedWithEdges);

        Assert.Equal(ModelDisplayStyle.ShadedWithEdges, vm.DisplayStyle);
        Assert.True(vm.ShowEdges);
    }

    [Fact]
    public void SetDisplayStyle_TechnicalWhite_EnablesShowEdges()
    {
        var vm = CreateViewModel();
        vm.ShowEdges = false;

        vm.SetDisplayStyleCommand.Execute(ModelDisplayStyle.TechnicalWhite);

        Assert.Equal(ModelDisplayStyle.TechnicalWhite, vm.DisplayStyle);
        Assert.True(vm.ShowEdges);
    }

    [Fact]
    public void ZoomToFit_FiresRequestZoomToFitEvent()
    {
        var vm = CreateViewModel();
        bool fired = false;
        vm.RequestZoomToFit += () => fired = true;

        vm.ZoomToFitCommand.Execute(null);

        Assert.True(fired);
    }

    [Fact]
    public void HighlightComponent_WithTransform_BuildsHighlightBox_AndCanClear()
    {
        var vm = CreateViewModel();

        var mesh = new DisplayMesh
        {
            Positions = new List<Vector3> { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0) }.AsReadOnly(),
            Normals = new List<Vector3> { Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ }.AsReadOnly(),
            TriangleIndices = new List<int> { 0, 1, 2 }.AsReadOnly(),
            Min = new Vector3(0, 0, 0),
            Max = new Vector3(100, 100, 100)
        };
        vm.Mesh = mesh;

        // 构造虚拟组件
        var comp = new AssemblyComponentNode
        {
            Name = "Bracket",
            InstanceName = "Bracket-1",
            Transform = Matrix4x4.CreateTranslation(0.02f, 0.05f, -0.01f), // 20mm, 50mm, -10mm
            FilePath = "C:\\test\\Bracket.sldprt",
            ConfigurationName = "Default"
        };

        vm.HighlightComponent(comp);

        Assert.NotNull(vm.SelectedAssemblyComponent);
        Assert.Equal("Bracket (Bracket-1)", vm.HighlightedComponentName);
        Assert.NotNull(vm.WpfComponentHighlightMesh);
        Assert.True(vm.WpfComponentHighlightMesh!.Positions.Count > 0);

        // 测试定位事件
        bool zoomFired = false;
        vm.RequestZoomToComponent += () => zoomFired = true;
        vm.ZoomToComponentCommand.Execute(null);
        Assert.True(zoomFired);

        // 测试清空高亮
        vm.ClearComponentHighlightCommand.Execute(null);
        Assert.Null(vm.SelectedAssemblyComponent);
        Assert.Null(vm.HighlightedComponentName);
        Assert.Null(vm.WpfComponentHighlightMesh);
    }

    [Fact]
    public void HighlightFeature_WithMatchingSourceId_ExtractsMeshTriangles()
    {
        var vm = CreateViewModel();

        // 创建带 Part 和 SurfaceReference 的测试网格
        var positions = new List<Vector3>
        {
            new(0, 0, 0), new(10, 0, 0), new(10, 10, 0),
            new(0, 0, 10), new(10, 0, 10), new(10, 10, 10)
        };
        var normals = new List<Vector3>
        {
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ
        };
        var indices = new List<int> { 0, 1, 2, 3, 4, 5 };
        var parts = new List<DisplayMeshPart>
        {
            new()
            {
                Id = "part1",
                VertexStart = 0,
                VertexCount = 3,
                TriangleStart = 0,
                TriangleCount = 1,
                SurfaceReferences = new List<DisplaySurfaceReference>
                {
                    new() { FeatureSourceId = 101, LocalSurfaceId = 1, Identity = "Face1" }
                }
            },
            new()
            {
                Id = "part2",
                VertexStart = 3,
                VertexCount = 3,
                TriangleStart = 1,
                TriangleCount = 1,
                SurfaceReferences = new List<DisplaySurfaceReference>
                {
                    new() { FeatureSourceId = 102, LocalSurfaceId = 2, Identity = "Face2" }
                }
            }
        };

        var mesh = new DisplayMesh
        {
            Positions = positions.AsReadOnly(),
            Normals = normals.AsReadOnly(),
            TriangleIndices = indices.AsReadOnly(),
            Parts = parts.AsReadOnly(),
            Min = new Vector3(0, 0, 0),
            Max = new Vector3(10, 10, 10)
        };

        vm.Mesh = mesh;

        // 构造特征
        var feature1 = new Feature("Boss-Extrude1", FeatureKind.Extrusion, "Extrude", 101);
        vm.HighlightFeature(feature1);

        Assert.Equal("特征: Boss-Extrude1", vm.HighlightedComponentName);
        Assert.NotNull(vm.WpfComponentHighlightMesh);
        // 匹配 part1 的 3 个顶点与 1 个三角面 (3个索引)
        Assert.Equal(3, vm.WpfComponentHighlightMesh!.Positions.Count);
        Assert.Equal(3, vm.WpfComponentHighlightMesh.TriangleIndices.Count);
    }
}
