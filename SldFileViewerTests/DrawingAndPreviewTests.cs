using System;
using System.Collections.Generic;
using SldFile.Mfc;
using SldFile.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using Xunit;
using SldFile.Assembly.Bom;

namespace SldFileViewerTests;

public class DrawingAndPreviewTests
{
    private class MockDialogService : IDialogService
    {
        public bool ShowOpenFileDialog(string filter, out string selectedPath)
        {
            selectedPath = string.Empty;
            return false;
        }

        public bool ShowSaveFileDialog(string defaultName, string filter, out string selectedPath)
        {
            selectedPath = Path.Combine(Path.GetTempPath(), defaultName);
            return true;
        }

        public List<(string Title, string Message)> Errors { get; } = new();
        public void ShowInformation(string title, string message) { }
        public void ShowError(string title, string message) => Errors.Add((title, message));
    }

    private readonly string _cubeDrawing = ViewerIntegrationTests.ResolveModelPath("cube.SLDDRW");
    private readonly string _cubePart = ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT");

    [Fact]
    public async Task Drawing_OpensWithHighResolutionPreviewAndCanvasZoomCommands()
    {
        var docService = new SwDocumentService();
        var dialogService = new MockDialogService();
        var exportService = new ExportService();

        var treeVm = new DocumentTreeViewModel(docService);
        var sketchVm = new SketchCanvasViewModel();
        var propsVm = new CustomPropsViewModel(docService, dialogService);
        var eqVm = new EquationsViewModel(docService);
        var bomVm = new BomViewModel(docService, exportService, dialogService);
        var meshVm = new MeshViewerViewModel(docService, exportService, dialogService);
        var imgVm = new ImagePreviewViewModel(docService, dialogService);
        var featVm = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

        await mainVm.LoadDocumentAsync(_cubeDrawing);

        Assert.True(mainVm.IsFileLoaded);
        // Drawing defaults to ImagePreview view
        Assert.Same(imgVm, mainVm.CurrentView);
        Assert.True(imgVm.HasImage);

        // Verify high resolution (2850x1436 in 2026 drawing, vs 640x480 thumbnail)
        Assert.True(imgVm.PixelWidth >= 1920, $"Expected PixelWidth >= 1920, but was {imgVm.PixelWidth}");
        Assert.True(imgVm.PixelHeight >= 1080, $"Expected PixelHeight >= 1080, but was {imgVm.PixelHeight}");
        Assert.True(imgVm.IsHighDefinition);
        Assert.Contains("[高清]", imgVm.ImageInfo);

        // Test Zoom & Scaling Mode
        imgVm.UpdateZoom(1.5);
        Assert.Equal("150%", imgVm.ZoomText);
        Assert.Equal(1.5, imgVm.Zoom);

        Assert.True(imgVm.IsSmoothFilter);
        Assert.Equal(System.Windows.Media.BitmapScalingMode.Fant, imgVm.ScalingMode);
        imgVm.ToggleFilterModeCommand.Execute(null);
        Assert.False(imgVm.IsSmoothFilter);
        Assert.Equal(System.Windows.Media.BitmapScalingMode.NearestNeighbor, imgVm.ScalingMode);

        // Test commands trigger events
        bool fitCalled = false, reset100Called = false;
        double zoomDelta = 0;
        imgVm.RequestFitToWindow += () => fitCalled = true;
        imgVm.RequestResetTo100 += () => reset100Called = true;
        imgVm.RequestZoomDelta += d => zoomDelta = d;

        imgVm.FitToWindowCommand.Execute(null);
        Assert.True(fitCalled);

        imgVm.ResetTo100Command.Execute(null);
        Assert.True(reset100Called);

        imgVm.ZoomInCommand.Execute(null);
        Assert.Equal(1.25, zoomDelta);

        imgVm.ZoomOutCommand.Execute(null);
        Assert.Equal(0.8, zoomDelta);

        // Test Image Export Command
        imgVm.ExportImageCommand.Execute(null);

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public void ImagePreviewViewModel_HandlesNullOrEmptyGracefully()
    {
        var docService = new SwDocumentService();
        var imgVm = new ImagePreviewViewModel(docService);

        imgVm.LoadImage(null);
        Assert.False(imgVm.HasImage);
        Assert.Null(imgVm.ImageSource);
        Assert.Equal(0, imgVm.PixelWidth);
        Assert.False(imgVm.IsHighDefinition);

        imgVm.LoadImage(Array.Empty<byte>());
        Assert.False(imgVm.HasImage);
        Assert.Null(imgVm.ImageSource);
    }
    [Fact]
    public void ImagePreviewViewModel_VectorOverlay_TogglesAndFormatsStatus()
    {
        var docService = new SwDocumentService();
        var imgVm = new ImagePreviewViewModel(docService);

        var Preview = new DrawingPreviewDefinition { IsResolved = true,
            Primitives = new[] {
                new DrawingPreviewPrimitive { Kind = DrawingPreviewPrimitiveKind.Text, Text = "未注公差", IsSheetLevelNote = true, Points = new[] { new DrawingPreviewPoint(.25, .049) } },
                new DrawingPreviewPrimitive { Kind = DrawingPreviewPrimitiveKind.Text, Text = "技术要求", IsSheetLevelNote = true, Points = new[] { new DrawingPreviewPoint(.012, .024) } }
            }.Concat(Enumerable.Range(0, 25).Select(i => new DrawingPreviewPrimitive {
                Kind = DrawingPreviewPrimitiveKind.Line, Points = new[] { new DrawingPreviewPoint(i / 1000.0, 0), new DrawingPreviewPoint(i / 1000.0, .01) }
            })).ToArray() };

        // Synthesize 100x100 BMP
        byte[] bmp = new byte[54 + 100 * 100 * 3];
        bmp[0] = 0x42; bmp[1] = 0x4D;
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);
        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(100).CopyTo(bmp, 18);
        BitConverter.GetBytes(100).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);

        imgVm.LoadImage(bmp, "测试工程图", Preview);

        Assert.True(imgVm.HasVectorData);
        Assert.True(imgVm.IsVectorOverlayEnabled);
        Assert.Equal(SldFileViewer.Views.Controls.DrawingOverlayMode.SheetNotesOnly, imgVm.OverlayMode);
        Assert.Equal("🔤 矢量增强: 图纸注释", imgVm.VectorOverlayButtonText);
        Assert.True(imgVm.IsRasterImageVisible);
        Assert.Contains("矢量注释", imgVm.VectorStatusText);

        // Switch to PureVectorCad
        imgVm.SetPureVectorCadMode();
        Assert.Equal(SldFileViewer.Views.Controls.DrawingOverlayMode.PureVectorCad, imgVm.OverlayMode);
        Assert.Equal("🎨 纯矢量 CAD", imgVm.VectorOverlayButtonText);
        Assert.Contains("纯矢量 CAD", imgVm.VectorStatusText);

        // Toggle
        imgVm.ToggleVectorOverlayCommand.Execute(null);
        Assert.False(imgVm.IsVectorOverlayEnabled);
        Assert.Equal("🖼️ 矢量增强: 关", imgVm.VectorOverlayButtonText);
        Assert.True(imgVm.IsRasterImageVisible);

        imgVm.ToggleVectorOverlayCommand.Execute(null);
        Assert.True(imgVm.IsVectorOverlayEnabled);
        Assert.Equal(SldFileViewer.Views.Controls.DrawingOverlayMode.SheetNotesOnly, imgVm.OverlayMode);
        Assert.Equal("🔤 矢量增强: 图纸注释", imgVm.VectorOverlayButtonText);

    }

    [Fact]
    public async Task Drawing_RealUserFile_LoadsVectorTextsAndOverlaysLosslessly()
    {
        string realPath = Path.Combine(AppContext.BaseDirectory, "TestFiles", "Drawings", "D8001507933", "D8001507933.SLDDRW");
        Assert.True(File.Exists(realPath), $"Missing drawing fixture: {realPath}");

        var docService = new SwDocumentService();
        var dialogService = new MockDialogService();
        var exportService = new ExportService();

        var treeVm = new DocumentTreeViewModel(docService);
        var sketchVm = new SketchCanvasViewModel();
        var propsVm = new CustomPropsViewModel(docService, dialogService);
        var eqVm = new EquationsViewModel(docService);
        var bomVm = new BomViewModel(docService, exportService, dialogService);
        var meshVm = new MeshViewerViewModel(docService, exportService, dialogService);
        var imgVm = new ImagePreviewViewModel(docService, dialogService);
        var featVm = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

        await mainVm.LoadDocumentAsync(realPath);

        Assert.True(mainVm.IsFileLoaded);
        Assert.Same(imgVm, mainVm.CurrentView);
        Assert.True(imgVm.HasImage);

        var primitives = imgVm.Preview!.Primitives;
        var texts = primitives.Where(p => p.Kind == DrawingPreviewPrimitiveKind.Text).ToArray();
        Assert.True(texts.Length > 50);
        Assert.Contains(texts, t => t.Text.Contains("技术要求"));
        Assert.Contains(texts, t => t.Text.Contains("未注公差"));
        Assert.Contains(texts, t => t.Text.Contains("D8001507933"));
        Assert.Contains(texts, t => t.Text.Contains("锐边倒角去毛刺"));
        Assert.False(texts.First(t => t.Text == "2-10*50").IsSheetLevelNote);
        var tech = texts.First(t => t.Text.Contains("技术要求"));
        Assert.True(tech.IsSheetLevelNote);
        Assert.InRange(tech.Points[0].X, 0, imgVm.Preview.PaperWidth);
        Assert.InRange(tech.Points[0].Y, 0, imgVm.Preview.PaperHeight);
        Assert.True(primitives.Count(p => p.Kind == DrawingPreviewPrimitiveKind.Line) >= 10);
        Assert.Contains(primitives, p => p.LineStyle.Contains("CENTER", StringComparison.OrdinalIgnoreCase));

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public async Task PartFile_LoadsWithoutExceptions()
    {
        string realPath = _cubePart;
        Assert.True(File.Exists(realPath), $"Missing part fixture: {realPath}");

        var docService = new SwDocumentService();
        var dialogService = new MockDialogService();
        var exportService = new ExportService();

        var treeVm = new DocumentTreeViewModel(docService);
        var sketchVm = new SketchCanvasViewModel();
        var propsVm = new CustomPropsViewModel(docService, dialogService);
        var eqVm = new EquationsViewModel(docService);
        var bomVm = new BomViewModel(docService, exportService, dialogService);
        var meshVm = new MeshViewerViewModel(docService, exportService, dialogService);
        var imgVm = new ImagePreviewViewModel(docService, dialogService);
        var featVm = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

        await mainVm.LoadDocumentAsync(realPath);

        foreach (var err in dialogService.Errors)
        {
            Assert.Fail($"Dialog error: {err.Title} -> {err.Message}");
        }

        Assert.True(mainVm.IsFileLoaded);
        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public void DocumentTreeViewModel_WithDuplicateSketchNames_DoesNotThrowDuplicateKeyException()
    {
        var mockService = new DuplicateSketchDocService();
        var vm = new SldFileViewer.ViewModels.DocumentTreeViewModel(mockService);

        var ex = Record.Exception(() => vm.UpdatePartSketches(0));
        Assert.Null(ex);
    }

    private class DuplicateSketchDocService : SldFileViewer.Services.ISwDocumentService
    {
        public DisplayMesh? GetComponentDisplayMesh(AssemblyComponentNode component) => null;
        public SwFile? CurrentFile => null;
        public string? CurrentFilePath => "test.sldprt";
        public bool IsOpen => true;
        public Task<SwFile?> OpenAsync(string filePath) => Task.FromResult<SwFile?>(null);
        public void Close() { }
        public void Dispose() { }
        public DisplayMesh? GetDisplayMesh() => null;
        public IReadOnlyList<Configuration> GetConfigurations() => Array.Empty<Configuration>();
        public IReadOnlyList<Feature> GetFeatures() => new List<Feature>
        {
            new Feature("折弯-线2", FeatureKind.Sketch)
        };
        public IReadOnlyList<SketchFeatureModel> GetSketches(int configIndex = 0) => new List<SketchFeatureModel>
        {
            new SketchFeatureModel("草图1"),
            new SketchFeatureModel("折弯-线2"),
            new SketchFeatureModel("折弯-线2"),
            new SketchFeatureModel("折弯-线1")
        };
        public IReadOnlyList<AssemblyComponentNode> GetAssemblyTree(string? configName = null) => Array.Empty<AssemblyComponentNode>();
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
}
