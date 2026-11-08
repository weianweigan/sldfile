using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using Xunit;

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

        public void ShowInformation(string title, string message) { }
        public void ShowError(string title, string message) { }
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

        var vectorData = new SldFile.Drawing.Vector.DrawingSheetVectorData();
        vectorData.Texts = new[]
        {
            new SldFile.Drawing.Vector.DrawingVectorText("未注公差", 250, 49),
            new SldFile.Drawing.Vector.DrawingVectorText("技术要求", 12, 24)
        };

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

        imgVm.LoadImage(bmp, "测试工程图", vectorData);

        Assert.True(imgVm.HasVectorData);
        Assert.True(imgVm.IsVectorOverlayEnabled);
        Assert.Contains("矢量 CAD (2 图元)", imgVm.VectorStatusText);
        Assert.Contains("[矢量 CAD: 2 图元]", imgVm.ImageInfo);
        Assert.Equal("矢量增强: 开", imgVm.VectorOverlayButtonText);

        // Toggle
        imgVm.ToggleVectorOverlayCommand.Execute(null);
        Assert.False(imgVm.IsVectorOverlayEnabled);
        Assert.Equal("矢量增强: 关", imgVm.VectorOverlayButtonText);

        imgVm.ToggleVectorOverlayCommand.Execute(null);
        Assert.True(imgVm.IsVectorOverlayEnabled);
    }

    [Fact]
    public async Task Drawing_RealUserFile_LoadsVectorTextsAndOverlaysLosslessly()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

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

        // Verify Vector Typography Data loaded!
        Assert.True(imgVm.HasVectorData, "Expected drawing to contain vector CAD metadata");
        Assert.True(imgVm.VectorData!.Texts.Count > 50, $"Expected >50 vector texts, got {imgVm.VectorData!.Texts.Count}");

        // Verify key title block and note texts
        var texts = imgVm.VectorData!.Texts;
        Assert.Contains(texts, t => t.CleanText.Contains("技术要求"));
        Assert.Contains(texts, t => t.CleanText.Contains("未注公差"));
        Assert.Contains(texts, t => t.CleanText.Contains("D8001507933"));
        Assert.Contains(texts, t => t.CleanText.Contains("锐边倒角去毛刺"));

        // Verify coordinates mapped
        var techNote = texts.First(t => t.CleanText.Contains("技术要求"));
        Assert.True(techNote.PixelX > 150 && techNote.PixelX < 400, $"Expected PixelX in [150, 400], got {techNote.PixelX}");
        Assert.True(techNote.PixelY > 400 && techNote.PixelY < 700, $"Expected PixelY in [400, 700], got {techNote.PixelY}");
        Assert.True(techNote.PixelY > 400 && techNote.PixelY < 700);

        mainVm.CloseFileCommand.Execute(null);
    }
}
