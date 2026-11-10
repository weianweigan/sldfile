using System;
using System.Collections.Generic;
using SldFile;
using SldFile.Sketches;
using SldFile.Drawing.Vector;
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

        public List<(string Title, string Message)> Errors { get; } = new();
        public void ShowInformation(string title, string message) { }
        public void ShowError(string title, string message) => Errors.Add((title, message));
    }

    private readonly string _cubeDrawing = ViewerIntegrationTests.ResolveModelPath("cube.SLDDRW");
    private readonly string _cubePart = ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT");

    [Fact]
    public async Task Drawing_RealDrawingAndPartFiles_LoadWithoutErrorsOrGhosting()
    {
        string prtPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDPRT";
        string drwPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";

        async Task TestFile(string path, bool isDrawing)
        {
            if (!File.Exists(path)) return;
            var docService = new SwDocumentService();
            var dialogService = new MockDialogService();
            var exportService = new ExportService();

            var treeVm = new DocumentTreeViewModel(docService);
            var sketchVm = new SketchCanvasViewModel();
            var propsVm = new CustomPropsViewModel(docService, dialogService);
            var eqVm = new EquationsViewModel(docService);
            var bomVm = new BomViewModel(docService, exportService, dialogService);
            var meshVm = new MeshViewerViewModel(docService, exportService, dialogService);
            var imgVm = new ImagePreviewViewModel(docService);
            var featVm = new FeatureDetailViewModel();

            var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

            await mainVm.LoadDocumentAsync(path);

            Assert.Empty(dialogService.Errors);

            foreach (var cfg in mainVm.Configurations)
            {
                mainVm.SelectedConfiguration = cfg;
            }

            if (isDrawing)
            {
                Assert.True(imgVm.HasImage);
                Assert.Equal(SldFileViewer.Views.Controls.DrawingOverlayMode.AllAnnotations, imgVm.OverlayMode);
            }
        }

        await TestFile(prtPath, false);
        await TestFile(drwPath, true);
    }


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
            new SldFile.Drawing.Vector.DrawingVectorText("未注公差", 250, 49) { Category = SldFile.Drawing.Vector.DrawingVectorTextCategory.ToleranceTable },
            new SldFile.Drawing.Vector.DrawingVectorText("技术要求", 12, 24) { Category = SldFile.Drawing.Vector.DrawingVectorTextCategory.TechnicalNote }
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

        // Anti-ghosting verification: View dimension 2-10*50 is classified as ViewAnnotation and not SheetLevelNote
        var holeCallout = texts.FirstOrDefault(t => t.CleanText == "2-10*50");
        Assert.NotNull(holeCallout);
        Assert.Equal(SldFile.Drawing.Vector.DrawingVectorTextCategory.ViewAnnotation, holeCallout.Category);
        Assert.False(holeCallout.IsSheetLevelNote);

        var tech = texts.First(t => t.CleanText.Contains("技术要求"));
        Assert.True(tech.IsSheetLevelNote);

        // Verify coordinates mapped
        var techNote = texts.First(t => t.CleanText.Contains("技术要求"));
        Assert.True(techNote.PixelX > 150 && techNote.PixelX < 400, $"Expected PixelX in [150, 400], got {techNote.PixelX}");
        Assert.True(techNote.PixelY > 400 && techNote.PixelY < 700, $"Expected PixelY in [400, 700], got {techNote.PixelY}");
        Assert.True(techNote.PixelY > 400 && techNote.PixelY < 700);

        // Verify 2D CAD Vector Geometry Lines loaded!
        Assert.True(imgVm.VectorData!.HasLines, "Expected drawing to contain 2D CAD vector line geometry");
        Assert.True(imgVm.VectorData!.Lines.Count >= 10, $"Expected >=10 vector lines, got {imgVm.VectorData!.Lines.Count}");
        Assert.Contains(imgVm.VectorData!.Lines, l => l.Kind == SldFile.Drawing.Vector.DrawingLineKind.SheetBorderOuter);
        Assert.Contains(imgVm.VectorData!.Lines, l => l.Kind == SldFile.Drawing.Vector.DrawingLineKind.SolidOutline);
        Assert.Contains(imgVm.VectorData!.Lines, l => l.Kind == SldFile.Drawing.Vector.DrawingLineKind.Centerline);

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public async Task Drawing_RealPartFile_LoadsWithoutExceptions()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDPRT";
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

    [Theory]
    [InlineData("2-10*50", false)]
    [InlineData("5-15*25", false)]
    [InlineData("A", false)]
    [InlineData("局部视图 A", false)]
    [InlineData("比例 1 : 2", false)]
    [InlineData("173.0", false)]
    [InlineData("24.0", false)]
    [InlineData("技术要求", true)]
    [InlineData("Drawing No./图号", true)]
    [InlineData("未注公差", true)]
    public void DrawingVectorReader_AntiGhosting_ClassifiesViewDimensionsCorrectly(string text, bool expectedSheetNote)
    {
        var texts = new List<DrawingVectorText>
        {
            new DrawingVectorText(text, 100.0, 20.0, 0.0, "Arial", 5.0, 0.0)
        };

        var classifyMethod = typeof(DrawingVectorReader).GetMethod("ClassifyTexts",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(classifyMethod);
        classifyMethod.Invoke(null, new object[] { texts, 420.0, 297.0 });

        Assert.Equal(expectedSheetNote, texts[0].IsSheetLevelNote);
    }

    [Fact]
    public void RenderDrawingWpfCanvasToPng()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var docService = new SwDocumentService();
                var imgVm = new ImagePreviewViewModel(docService);

                using var swFile = SwFile.Open(realPath, readOnly: true);
                var sheets = swFile.GetDrawingSheets();
                Assert.NotEmpty(sheets);

                var sheet0 = sheets[0];
                var bestImg = swFile.GetBestSheetPreviewImage(0) ?? sheet0.BestPreview ?? swFile.GetBestPreviewImage(0);
                Assert.NotNull(bestImg);

                imgVm.LoadImage(bestImg, $"工程图纸: {sheet0.Name}", sheet0.VectorData);
                Assert.True(imgVm.HasImage);
                Assert.NotNull(imgVm.ImageSource);

                int w = imgVm.PixelWidth > 0 ? imgVm.PixelWidth : 1420;
                int h = imgVm.PixelHeight > 0 ? imgVm.PixelHeight : 710;

                // 1. Render AllAnnotations (Default mode: High-res background + full vector annotations)
                var grid = new System.Windows.Controls.Grid { Width = w, Height = h };
                var imgCtrl = new System.Windows.Controls.Image
                {
                    Source = imgVm.ImageSource,
                    Stretch = System.Windows.Media.Stretch.None,
                    Visibility = System.Windows.Visibility.Visible
                };
                grid.Children.Add(imgCtrl);

                var canvas = new SldFileViewer.Views.Controls.DrawingVectorCanvas
                {
                    Width = w,
                    Height = h,
                    VectorData = imgVm.VectorData,
                    OverlayMode = SldFileViewer.Views.Controls.DrawingOverlayMode.AllAnnotations,
                    IsOverlayEnabled = true
                };
                grid.Children.Add(canvas);

                grid.Measure(new System.Windows.Size(w, h));
                grid.Arrange(new System.Windows.Rect(0, 0, w, h));
                grid.UpdateLayout();

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(grid);

                string outDir = @"C:\Users\HP\.gemini\antigravity\brain\eed5a36f-5e8b-4a85-bde9-407779624be2";
                string outPath = Path.Combine(outDir, "viewer_actual_render.png");

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = File.Create(outPath))
                {
                    encoder.Save(fs);
                }

                // 2. Also render PureVectorCad mode
                imgCtrl.Visibility = System.Windows.Visibility.Collapsed;
                canvas.OverlayMode = SldFileViewer.Views.Controls.DrawingOverlayMode.PureVectorCad;
                grid.UpdateLayout();

                var rtbPure = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtbPure.Render(grid);

                string outPurePath = Path.Combine(outDir, "viewer_pure_vector_render.png");
                var encoderPure = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoderPure.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtbPure));
                using (var fs = File.Create(outPurePath))
                {
                    encoderPure.Save(fs);
                }
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw new InvalidOperationException($"STA rendering failed: {threadEx.Message}", threadEx);
        }
    }

    private class DuplicateSketchDocService : SldFileViewer.Services.ISwDocumentService
    {
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
        public IReadOnlyList<SketchFeature> GetSketches(int configIndex = 0) => new List<SketchFeature>
        {
            new SketchFeature("草图1"),
            new SketchFeature("折弯-线2"),
            new SketchFeature("折弯-线2"),
            new SketchFeature("折弯-线1")
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
