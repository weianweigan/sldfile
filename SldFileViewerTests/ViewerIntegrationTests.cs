using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using Xunit;

namespace SldFileViewerTests;

public class ViewerIntegrationTests
{
    private readonly string _cubePart = Path.GetFullPath(@"..\..\..\..\models\cube.SLDPRT");
    private readonly string _assembly = Path.GetFullPath(@"..\..\..\..\models\assembly.SLDASM");
    private readonly string _cubeDrawing = Path.GetFullPath(@"..\..\..\..\models\cube.SLDDRW");

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

    [Fact]
    public void MainWindow_InstantiatesAndMeasuresWithoutXamlException()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    new System.Windows.Application();
                    SldFileViewer.Theme.ThemeManager.ApplyTheme();
                }

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

                var window = new SldFileViewer.MainWindow(mainVm);
                window.Measure(new System.Windows.Size(1200, 800));

                new SldFileViewer.Views.Panels.SketchCanvasView { DataContext = sketchVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.Panels.Mesh3DView { DataContext = meshVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.Panels.CustomPropertiesView { DataContext = propsVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.Panels.EquationsView { DataContext = eqVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.Panels.BomTableView { DataContext = bomVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.Panels.ImagePreviewView { DataContext = imgVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.Panels.FeatureDetailView { DataContext = featVm }.Measure(new System.Windows.Size(800, 600));
                new SldFileViewer.Views.DocumentTreeView { DataContext = treeVm }.Measure(new System.Windows.Size(300, 600));
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
            throw new Exception("Exception in STA thread: " + threadEx.ToString(), threadEx);
        }
    }

    [Fact]
    public async Task LoadPartFile_PopulatesTreeAndSketchAndMesh()
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
        var imgVm = new ImagePreviewViewModel(docService);
        var featVm = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

        await mainVm.LoadDocumentAsync(_cubePart);

        Assert.True(mainVm.IsFileLoaded);
        Assert.NotEmpty(treeVm.RootNodes);

        var root = treeVm.RootNodes[0];
        Assert.Contains(root.Children, c => c.Kind == TreeNodeKind.CustomProperties);
        Assert.Contains(root.Children, c => c.Kind == TreeNodeKind.Equations);
        Assert.Contains(root.Children, c => c.Kind == TreeNodeKind.GeometryPreview);

        // Verify Features in tree
        var featFolder = root.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureFolder);
        Assert.NotNull(featFolder);
        Assert.Contains(featFolder.Children, c => c.Kind == TreeNodeKind.FeatureSketch);

        // Verify Mesh
        Assert.True(meshVm.HasMesh);
        Assert.True(meshVm.VertexCount > 0);
        Assert.True(meshVm.TriangleCount > 0);

        // Verify Equations
        Assert.NotEmpty(eqVm.Equations);

        // Clean up
        mainVm.CloseFileCommand.Execute(null);
        Assert.False(mainVm.IsFileLoaded);
    }

    [Fact]
    public async Task LoadSketchPart_PopulatesSketchSegmentsAndRelations()
    {
        string sketchPart = Path.GetFullPath(@"..\..\..\..\SldFileTests\TestFiles\Sketches2026\rectangle.SLDPRT");
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

        await mainVm.LoadDocumentAsync(sketchPart);

        Assert.True(mainVm.IsFileLoaded);

        // Verify Sketches
        var sketches = docService.GetSketches(0);
        Assert.NotEmpty(sketches);
        var sketch1 = sketches[0];
        sketchVm.LoadSketch(sketch1);

        Assert.NotEmpty(sketchVm.Segments);
        Assert.NotEmpty(sketchVm.Points);
        Assert.True(sketchVm.Bounds.Width > 0);
        Assert.NotEmpty(sketchVm.Constraints);

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public async Task LoadAssemblyFile_PopulatesBomAndComponents()
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
        var imgVm = new ImagePreviewViewModel(docService);
        var featVm = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

        await mainVm.LoadDocumentAsync(_assembly);

        Assert.True(mainVm.IsFileLoaded);
        var root = treeVm.RootNodes[0];

        // Assembly must have BOM node
        Assert.Contains(root.Children, c => c.Kind == TreeNodeKind.Bom);

        // Verify BOM items
        Assert.NotEmpty(bomVm.Items);
        Assert.True(bomVm.TotalQuantity >= 1);

        // Verify Export BOM to CSV
        string tempCsv = Path.Combine(Path.GetTempPath(), $"test_bom_{Guid.NewGuid():N}.csv");
        try
        {
            exportService.ExportBomToCsv(bomVm.Items, tempCsv);
            Assert.True(File.Exists(tempCsv));
            string csvContent = File.ReadAllText(tempCsv);
            Assert.Contains("ItemNo", csvContent);
        }
        finally
        {
            if (File.Exists(tempCsv)) File.Delete(tempCsv);
        }

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public async Task LoadDrawingFile_PopulatesSheets()
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
        var imgVm = new ImagePreviewViewModel(docService);
        var featVm = new FeatureDetailViewModel();

        var mainVm = new MainViewModel(docService, dialogService, treeVm, sketchVm, propsVm, eqVm, bomVm, meshVm, imgVm, featVm);

        await mainVm.LoadDocumentAsync(_cubeDrawing);

        Assert.True(mainVm.IsFileLoaded);
        var root = treeVm.RootNodes[0];

        var sheetsFolder = root.Children.FirstOrDefault(c => c.Title.Contains("图纸页"));
        Assert.NotNull(sheetsFolder);
        Assert.NotEmpty(sheetsFolder.Children);

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public void ExportMeshService_ExportsObjAndStlSuccessfully()
    {
        var docService = new SwDocumentService();
        var dialogService = new MockDialogService();
        var exportService = new ExportService();

        using var sw = SldFile.SwFile.Open(_cubePart, readOnly: true);
        var mesh = sw.GetDisplayMesh();
        Assert.NotNull(mesh);

        string tempObj = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.obj");
        string tempStl = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.stl");

        try
        {
            exportService.ExportMeshToObj(mesh!, tempObj);
            Assert.True(File.Exists(tempObj));
            Assert.True(new FileInfo(tempObj).Length > 0);

            exportService.ExportMeshToStl(mesh!, tempStl);
            Assert.True(File.Exists(tempStl));
            Assert.True(new FileInfo(tempStl).Length > 0);
        }
        finally
        {
            if (File.Exists(tempObj)) File.Delete(tempObj);
            if (File.Exists(tempStl)) File.Delete(tempStl);
        }
    }
}
