using System;
using System.IO;
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
}
