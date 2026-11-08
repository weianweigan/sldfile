using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using SldFile;
using SldFile.Drawing;
using SldFile.Sketches;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using Xunit;

namespace SldFileViewerTests;

public class AssemblyExplodeAndIsolateTests
{
    private class DummyDialogService : IDialogService
    {
        public bool ShowOpenFileDialog(string filter, out string selectedPath) { selectedPath = string.Empty; return false; }
        public bool ShowSaveFileDialog(string defaultName, string filter, out string selectedPath) { selectedPath = string.Empty; return false; }
        public void ShowError(string title, string message) { }
        public void ShowInformation(string title, string message) { }
    }

    private class MockSwDocumentService : ISwDocumentService
    {
        public SwFile? CurrentFile => null;
        public string? CurrentFilePath => null;
        public bool IsOpen => true;
        public DisplayMesh? MockMesh { get; set; }
        public IReadOnlyList<AssemblyComponentNode>? MockTree { get; set; }

        public Task<SwFile?> OpenAsync(string filePath) => Task.FromResult<SwFile?>(null);
        public void Close() { }
        public void Dispose() { }

        public DisplayMesh? GetDisplayMesh() => MockMesh;
        public IReadOnlyList<Configuration> GetConfigurations() => Array.Empty<Configuration>();
        public IReadOnlyList<Feature> GetFeatures() => Array.Empty<Feature>();
        public IReadOnlyList<SketchFeature> GetSketches(int configIndex = 0) => Array.Empty<SketchFeature>();
        public IReadOnlyList<AssemblyComponentNode> GetAssemblyTree(string? configName = null) => MockTree ?? Array.Empty<AssemblyComponentNode>();
        public IReadOnlyList<BomItem> GetBom(string? configName = null) => Array.Empty<BomItem>();
        public IReadOnlyList<CustomPropertyItem> GetCustomProperties(string? configName = null) => Array.Empty<CustomPropertyItem>();
        public IReadOnlyList<EquationItem> GetEquations() => Array.Empty<EquationItem>();
        public byte[]? GetPreviewPng(int configIndex = 0) => null;
        public byte[]? GetPreviewBitmap(int configIndex = 0) => null;
        public byte[]? GetBestPreviewImage(int configIndex = 0) => null;
        public IReadOnlyList<DrawingSheetInfo> GetDrawingSheets() => Array.Empty<DrawingSheetInfo>();
        public byte[]? GetSheetPreviewPng(int sheetIndex) => null;
        public byte[]? GetBestSheetPreviewImage(int sheetIndex) => null;
        public void SaveCustomProperties(IEnumerable<CustomPropertyItem> properties, string? configName = null) { }
    }

    private static (MeshViewerViewModel vm, MockSwDocumentService docService, AssemblyComponentNode comp1, AssemblyComponentNode comp2) CreateAssemblyContext()
    {
        var docService = new MockSwDocumentService();
        var exportService = new ExportService();
        var dialogService = new DummyDialogService();

        var comp1 = new AssemblyComponentNode
        {
            ComponentId = "comp-01",
            Name = "Piston",
            InstanceName = "Piston-1",
            Transform = Matrix4x4.CreateTranslation(0.02f, 0, 0)
        };

        var comp2 = new AssemblyComponentNode
        {
            ComponentId = "comp-02",
            Name = "Cylinder",
            InstanceName = "Cylinder-1",
            Transform = Matrix4x4.CreateTranslation(-0.02f, 0, 0)
        };

        docService.MockTree = new List<AssemblyComponentNode> { comp1, comp2 };

        // 构造两个组件各自对应的 Part 网格
        // comp1: 顶点 0..2，中心 (20, 0, 0)
        // comp2: 顶点 3..5，中心 (-20, 0, 0)
        var positions = new List<Vector3>
        {
            new(18, 0, 0), new(22, 0, 0), new(20, 5, 0),
            new(-18, 0, 0), new(-22, 0, 0), new(-20, 5, 0)
        };
        var normals = new List<Vector3>
        {
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ
        };
        var indices = new List<int> { 0, 1, 2, 3, 4, 5 };
        var edges = new List<(int, int)> { (0, 1), (1, 2), (3, 4), (4, 5) };

        var parts = new List<DisplayMeshPart>
        {
            new()
            {
                Id = "part-piston",
                AppearanceName = "Piston-1/Default",
                VertexStart = 0,
                VertexCount = 3,
                TriangleStart = 0,
                TriangleCount = 1
            },
            new()
            {
                Id = "part-cylinder",
                AppearanceName = "Cylinder-1/Default",
                VertexStart = 3,
                VertexCount = 3,
                TriangleStart = 1,
                TriangleCount = 1
            }
        };

        var mesh = new DisplayMesh
        {
            Positions = positions.AsReadOnly(),
            Normals = normals.AsReadOnly(),
            TriangleIndices = indices.AsReadOnly(),
            OutlineEdges = edges.AsReadOnly(),
            Parts = parts.AsReadOnly(),
            Min = new Vector3(-25, -5, -5),
            Max = new Vector3(25, 10, 5)
        };

        docService.MockMesh = mesh;

        var vm = new MeshViewerViewModel(docService, exportService, dialogService);
        vm.LoadMesh();

        return (vm, docService, comp1, comp2);
    }

    [Fact]
    public void LoadMesh_DetectsAssemblyComponents_AndInitializesState()
    {
        var (vm, _, _, _) = CreateAssemblyContext();

        Assert.True(vm.HasAssemblyComponents);
        Assert.Equal(2, vm.AssemblyComponentCount);
        Assert.False(vm.HasHiddenComponents);
        Assert.Equal(0, vm.HiddenComponentCount);
        Assert.False(vm.IsIsolated);
        Assert.Null(vm.WpfGhostMesh);
        Assert.False(vm.IsExplodeModeEnabled);
        Assert.Equal(0.0, vm.ExplodePercent);
    }

    [Fact]
    public void HideComponent_RemovesFromVisibleMesh_AndUpdatesStats()
    {
        var (vm, _, comp1, comp2) = CreateAssemblyContext();

        // 初始时包含全部 2 个三角面（6 个索引）
        Assert.NotNull(vm.WpfMesh);
        Assert.Equal(6, vm.WpfMesh!.TriangleIndices.Count);

        // 隐藏 comp1 (Piston)
        vm.HideComponentCommand.Execute(comp1);

        Assert.True(vm.HasHiddenComponents);
        Assert.Equal(1, vm.HiddenComponentCount);
        Assert.True(vm.IsComponentHidden(comp1));
        Assert.False(vm.IsComponentHidden(comp2));

        // 重新构建后只剩下 comp2 的 1 个三角面（3 个索引）
        Assert.NotNull(vm.WpfMesh);
        Assert.Equal(3, vm.WpfMesh!.TriangleIndices.Count);
        // comp2 对应顶点索引 3, 4, 5
        Assert.Contains(3, vm.WpfMesh.TriangleIndices);
        Assert.DoesNotContain(0, vm.WpfMesh.TriangleIndices);
    }

    [Fact]
    public void ShowComponent_RestoresComponentToVisibleMesh()
    {
        var (vm, _, comp1, _) = CreateAssemblyContext();

        vm.HideComponentCommand.Execute(comp1);
        Assert.True(vm.HasHiddenComponents);

        vm.ShowComponentCommand.Execute(comp1);
        Assert.False(vm.HasHiddenComponents);
        Assert.Equal(0, vm.HiddenComponentCount);
        Assert.False(vm.IsComponentHidden(comp1));

        Assert.NotNull(vm.WpfMesh);
        Assert.Equal(6, vm.WpfMesh!.TriangleIndices.Count);
    }

    [Fact]
    public void ToggleComponentVisibility_SwitchesState()
    {
        var (vm, _, comp1, _) = CreateAssemblyContext();

        // 第一次切换 -> 隐藏
        vm.ToggleComponentVisibilityCommand.Execute(comp1);
        Assert.True(vm.IsComponentHidden(comp1));
        Assert.Equal(1, vm.HiddenComponentCount);

        // 第二次切换 -> 显示
        vm.ToggleComponentVisibilityCommand.Execute(comp1);
        Assert.False(vm.IsComponentHidden(comp1));
        Assert.Equal(0, vm.HiddenComponentCount);
    }

    [Fact]
    public void ShowAllComponents_ClearsAllHiddenAndRestoresAllMesh()
    {
        var (vm, _, comp1, comp2) = CreateAssemblyContext();

        vm.HideComponentCommand.Execute(comp1);
        vm.HideComponentCommand.Execute(comp2);
        Assert.Equal(2, vm.HiddenComponentCount);
        Assert.Empty(vm.WpfMesh!.TriangleIndices);

        vm.ShowAllComponentsCommand.Execute(null);

        Assert.False(vm.HasHiddenComponents);
        Assert.Equal(0, vm.HiddenComponentCount);
        Assert.Equal(6, vm.WpfMesh.TriangleIndices.Count);
    }

    [Fact]
    public void IsolateComponent_CreatesGhostMeshForOtherComponents()
    {
        var (vm, _, comp1, comp2) = CreateAssemblyContext();

        // 隔离 comp1
        vm.IsolateComponentCommand.Execute(comp1);

        Assert.True(vm.IsIsolated);
        Assert.Equal(comp1, vm.IsolatedComponent);
        Assert.Contains("Piston", vm.IsolatedComponentName);

        // 主网格只包含 comp1（3 个索引）
        Assert.NotNull(vm.WpfMesh);
        Assert.Equal(3, vm.WpfMesh!.TriangleIndices.Count);
        Assert.Contains(0, vm.WpfMesh.TriangleIndices);

        // 幽灵网格包含 comp2（3 个索引）
        Assert.NotNull(vm.WpfGhostMesh);
        Assert.Equal(3, vm.WpfGhostMesh!.TriangleIndices.Count);
        Assert.Contains(3, vm.WpfGhostMesh.TriangleIndices);

        // 退出隔离
        vm.ExitIsolateCommand.Execute(null);
        Assert.False(vm.IsIsolated);
        Assert.Null(vm.WpfGhostMesh);
        Assert.Equal(6, vm.WpfMesh.TriangleIndices.Count);
    }

    [Fact]
    public void ExplodePercent_ShiftsComponentVerticesOutward()
    {
        var (vm, _, comp1, comp2) = CreateAssemblyContext();

        // 0% 状态下顶点坐标
        var p0Before = vm.WpfMesh!.Positions[0];

        // 开启爆炸视图并设置 100%
        vm.IsExplodeModeEnabled = true;
        vm.SetExplodePercentCommand.Execute(100.0);

        Assert.Equal("100%", vm.ExplodePercentDisplay);
        Assert.Equal(100.0, vm.ExplodePercent);

        var (offsets, compOffsets) = vm.CalculateExplodeOffsets();
        Assert.NotNull(offsets);
        Assert.Equal(2, compOffsets.Count);

        // comp1 位于正 X，应向正 X 偏移
        Assert.True(compOffsets[comp1].X > 0);
        // comp2 位于负 X，应向负 X 偏移
        Assert.True(compOffsets[comp2].X < 0);

        // 爆炸后 Mesh 顶点位置应发生显著偏移
        var p0After = vm.WpfMesh.Positions[0];
        Assert.NotEqual(p0Before.X, p0After.X);
        Assert.True(p0After.X > p0Before.X);

        // 一键复位
        vm.ResetExplodeCommand.Execute(null);
        Assert.Equal(0.0, vm.ExplodePercent);
        Assert.Equal(p0Before.X, vm.WpfMesh.Positions[0].X);
    }

    [Fact]
    public void TreeAction_IsolateAndShowAll_LinkToMeshViewer()
    {
        var (vm, docService, comp1, _) = CreateAssemblyContext();

        var treeVm = new DocumentTreeViewModel(docService);
        var exportService = new ExportService();
        var dialogService = new DummyDialogService();
        var sketchCanvas = new SketchCanvasViewModel();
        var customProps = new CustomPropsViewModel(docService, dialogService);
        var equations = new EquationsViewModel(docService);
        var bom = new BomViewModel(docService, exportService, dialogService);
        var imgPreview = new ImagePreviewViewModel(docService);
        var featDetail = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(
            docService,
            dialogService,
            treeVm,
            sketchCanvas,
            customProps,
            equations,
            bom,
            vm,
            imgPreview,
            featDetail);

        // 通过树节点动作请求隔离 comp1
        treeVm.RequestComponentAction(comp1, ComponentTreeAction.Isolate);
        Assert.True(vm.IsIsolated);
        Assert.Equal(comp1, vm.IsolatedComponent);

        // 通过树节点动作请求全部显示
        treeVm.RequestComponentAction(comp1, ComponentTreeAction.ShowAll);
        Assert.False(vm.IsIsolated);
        Assert.False(vm.HasHiddenComponents);
    }

    [Fact]
    public async Task Explode_OnRealAssemblyFile_ProducesSignificantExplosionDisplacement()
    {
        string realAsmPath = @"C:\Users\HP\Desktop\SC32x50S.SLDASM";
        if (!System.IO.File.Exists(realAsmPath)) return;

        var docService = new SwDocumentService();
        await docService.OpenAsync(realAsmPath);
        var exportService = new ExportService();
        var dialogService = new DummyDialogService();

        var vm = new MeshViewerViewModel(docService, exportService, dialogService);
        vm.LoadMesh();

        Assert.True(vm.HasAssemblyComponents);
        Assert.True(vm.AssemblyComponentCount >= 2);

        // 开启爆炸视图并拉到 100%
        vm.IsExplodeModeEnabled = true;
        vm.SetExplodePercentCommand.Execute(100.0);

        var (offsets, compOffsets) = vm.CalculateExplodeOffsets();
        Assert.NotNull(offsets);
        Assert.True(compOffsets.Count >= 2);

        // 检查是否有顶点发生了显著位移（> 10mm）
        float maxDisplacement = 0;
        for (int i = 0; i < offsets.Length; i++)
        {
            if (offsets[i].Length() > maxDisplacement)
            {
                maxDisplacement = offsets[i].Length();
            }
        }
        Assert.True(maxDisplacement > 10.0f, $"Explode displacement should be significant (> 10mm), but was {maxDisplacement:F1}mm");
    }
    [Theory]
    [InlineData("50.0", 50.0)]
    [InlineData("100.0", 100.0)]
    [InlineData("50", 50.0)]
    [InlineData("100", 100.0)]
    [InlineData(50.0, 50.0)]
    [InlineData(100.0, 100.0)]
    [InlineData(50, 50.0)]
    public void SetExplodePercentCommand_AcceptsStringOrNumberParameter_WithoutException(object parameter, double expected)
    {
        var docService = new MockSwDocumentService();
        var exportService = new ExportService();
        var dialogService = new DummyDialogService();
        var vm = new MeshViewerViewModel(docService, exportService, dialogService);

        // WPF RelayCommand.CanExecute 和 Execute 不应抛出任何异常
        Assert.True(vm.SetExplodePercentCommand.CanExecute(parameter));
        vm.SetExplodePercentCommand.Execute(parameter);

        Assert.Equal(expected, vm.ExplodePercent, 1);
    }
}