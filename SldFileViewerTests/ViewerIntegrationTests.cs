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
    private static string ResolveModelPath(string relativePath)
    {
        var candidates = new System.Collections.Generic.List<string> { relativePath };
        var fileName = Path.GetFileName(relativePath);
        if (!string.IsNullOrEmpty(fileName))
        {
            candidates.Add(Path.Combine("SldFileTests", "TestFiles", "Solidworks2026", fileName));
            candidates.Add(Path.Combine("TestFiles", "Solidworks2026", fileName));
            candidates.Add(Path.Combine("SldFileTests", "TestFiles", "Solidworks2012", fileName));
            candidates.Add(Path.Combine("TestFiles", "Solidworks2012", fileName));
        }

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            foreach (var rel in candidates)
            {
                var candidate = Path.Combine(dir, rel);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == null) break;
            dir = parent;
        }
        dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 6; i++)
        {
            foreach (var rel in candidates)
            {
                var candidate = Path.Combine(dir, rel);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            var parent = Path.GetDirectoryName(dir);
            if (parent == null) break;
            dir = parent;
        }
        return Path.GetFullPath(relativePath);
    }

    private readonly string _cubePart = ResolveModelPath(@"SldFileTests/TestFiles/Solidworks2026/cube.SLDPRT");
    private readonly string _assembly = ResolveModelPath(@"SldFileTests/TestFiles/Solidworks2026/assembly.SLDASM");
    private readonly string _cubeDrawing = ResolveModelPath(@"SldFileTests/TestFiles/Solidworks2026/cube.SLDDRW");

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
        string sketchPart = ResolveModelPath(@"SldFileTests\TestFiles\Sketches2026\rectangle.SLDPRT");
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
    public void ExportMeshService_ExportsObjStlAndGlbSuccessfully()
    {
        var docService = new SwDocumentService();
        var dialogService = new MockDialogService();
        var exportService = new ExportService();

        using var sw = SldFile.SwFile.Open(_cubePart, readOnly: true);
        var mesh = sw.GetDisplayMesh();
        Assert.NotNull(mesh);

        string tempObj = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.obj");
        string tempStl = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.stl");
        string tempGlb = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.glb");

        try
        {
            exportService.ExportMeshToObj(mesh!, tempObj);
            Assert.True(File.Exists(tempObj));
            Assert.True(new FileInfo(tempObj).Length > 0);

            exportService.ExportMeshToStl(mesh!, tempStl);
            Assert.True(File.Exists(tempStl));
            Assert.True(new FileInfo(tempStl).Length > 0);

            exportService.ExportMeshToGlb(mesh!, tempGlb);
            Assert.True(File.Exists(tempGlb));
            Assert.True(new FileInfo(tempGlb).Length > 0);
        }
        finally
        {
            if (File.Exists(tempObj)) File.Delete(tempObj);
            if (File.Exists(tempStl)) File.Delete(tempStl);
            if (File.Exists(tempGlb)) File.Delete(tempGlb);
        }
    }

    [Fact]
    public void FeatureDetailViewModel_LoadFeature_PopulatesDimensionsAndProperties()
    {
        var vm = new FeatureDetailViewModel();
        var dims = new[] { new SldFile.FeatureDimension("D1", "10.000"), new SldFile.FeatureDimension("D2", "20.000") };
        var attrs = new System.Collections.Generic.Dictionary<string, string> { ["ClassName"] = "moExtrusion_ct", ["TypeId"] = "1" };
        var children = new[] { new SldFile.Feature("D1", SldFile.FeatureKind.Dimension) };
        var feat = new SldFile.Feature("凸台-拉伸1", SldFile.FeatureKind.Extrusion, "凸台-拉伸", 75, attrs, dims, children);

        vm.LoadFeature(feat);

        Assert.Equal("凸台-拉伸1", vm.FeatureName);
        Assert.Equal("凸台-拉伸", vm.FeatureType);
        Assert.Equal("75", vm.FeatureId);
        Assert.True(vm.HasDimensions);
        Assert.Equal(2, vm.Dimensions.Count);
        Assert.Equal("D1", vm.Dimensions[0].Name);
        Assert.Equal("10.000", vm.Dimensions[0].Value);
        Assert.True(vm.HasProperties);
        Assert.True(vm.HasChildren);
        Assert.Single(vm.Children);
    }

    [Fact]
    public async Task FeatureClick_InPartDocument_SelectsFeatureAndShowsDetails()
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

        var root = treeVm.RootNodes[0];
        var featTree = root.Children.First(c => c.Kind == TreeNodeKind.FeatureFolder);
        var extrudeNode = featTree.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureExtrude);
        Assert.NotNull(extrudeNode);

        // Select the extrusion feature
        treeVm.SelectedNode = extrudeNode;

        Assert.Same(featVm, mainVm.CurrentView);
        Assert.Equal("凸台-拉伸1", featVm.FeatureName);

        // Select front plane
        var planeNode = featTree.Children.FirstOrDefault(c => c.Title.Contains("前视基准面"));
        Assert.NotNull(planeNode);
        treeVm.SelectedNode = planeNode;

        Assert.Same(featVm, mainVm.CurrentView);
        Assert.True(featVm.FeatureType.Contains("Plane") || featVm.FeatureType.Contains("基准面"));

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public async Task SketchClick_InPartDocument_PopulatesPrimitivesAndDimensions()
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

        var root = treeVm.RootNodes[0];
        var featTree = root.Children.First(c => c.Kind == TreeNodeKind.FeatureFolder);
        var sketchNode = featTree.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureSketch);
        Assert.NotNull(sketchNode);

        // Select sketch
        treeVm.SelectedNode = sketchNode;

        Assert.Same(sketchVm, mainVm.CurrentView);
        Assert.Equal("草图1", sketchVm.SketchName);
        Assert.NotEmpty(sketchVm.Segments);
        Assert.NotEmpty(sketchVm.Points);
        Assert.NotEmpty(sketchVm.Dimensions);

        mainVm.CloseFileCommand.Execute(null);
    }

    [Fact]
    public void SimulateUserInteractions_StepThroughAllNodesAndViewports_NoExceptions()
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

                // 0. Test initial empty state (startup without file loaded)
                var emptyHostView = new SldFileViewer.Views.ContentHostView { DataContext = mainVm };
                emptyHostView.Measure(new System.Windows.Size(800, 600));
                emptyHostView.Arrange(new System.Windows.Rect(0, 0, 800, 600));

                var loadTask = mainVm.LoadDocumentAsync(_cubePart);
                while (!loadTask.IsCompleted)
                {
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        () => { }, System.Windows.Threading.DispatcherPriority.Background);
                    System.Threading.Thread.Sleep(10);
                }
                loadTask.GetAwaiter().GetResult();
                Assert.True(mainVm.IsFileLoaded);

                var root = treeVm.RootNodes[0];

                // 1. Click Custom Properties
                var cpNode = root.Children.First(c => c.Kind == TreeNodeKind.CustomProperties);
                treeVm.SelectedNode = cpNode;
                Assert.Same(propsVm, mainVm.CurrentView);
                var cpView = new SldFileViewer.Views.Panels.CustomPropertiesView { DataContext = propsVm };
                cpView.Measure(new System.Windows.Size(800, 600));

                // 2. Click Equations
                var eqNode = root.Children.First(c => c.Kind == TreeNodeKind.Equations);
                treeVm.SelectedNode = eqNode;
                Assert.Same(eqVm, mainVm.CurrentView);
                var eqView = new SldFileViewer.Views.Panels.EquationsView { DataContext = eqVm };
                eqView.Measure(new System.Windows.Size(800, 600));

                // 3. Click Geometry Preview
                var geoNode = root.Children.First(c => c.Kind == TreeNodeKind.GeometryPreview);
                treeVm.SelectedNode = geoNode;
                Assert.Same(meshVm, mainVm.CurrentView);
                var meshView = new SldFileViewer.Views.Panels.Mesh3DView { DataContext = meshVm };
                meshView.Measure(new System.Windows.Size(800, 600));

                // 4. Click Feature Folder and all features
                var featFolder = root.Children.First(c => c.Kind == TreeNodeKind.FeatureFolder);
                foreach (var featNode in featFolder.Children)
                {
                    treeVm.SelectedNode = featNode;
                    Assert.NotNull(mainVm.CurrentView);

                    if (featNode.Kind == TreeNodeKind.FeatureSketch)
                    {
                        var scView = new SldFileViewer.Views.Panels.SketchCanvasView { DataContext = sketchVm };
                        scView.Measure(new System.Windows.Size(800, 600));

                        // Interact with sketch items
                        if (sketchVm.Segments.Count > 0)
                            sketchVm.SelectedSegment = sketchVm.Segments[0];
                        if (sketchVm.Points.Count > 0)
                            sketchVm.SelectedPoint = sketchVm.Points[0];
                        sketchVm.FitViewCommand.Execute(null);
                    }
                    else if (featNode.Kind == TreeNodeKind.FeatureExtrude || featNode.Kind == TreeNodeKind.FeaturePlane)
                    {
                        var fdView = new SldFileViewer.Views.Panels.FeatureDetailView { DataContext = featVm };
                        fdView.Measure(new System.Windows.Size(800, 600));
                    }
                }

                // 5. Verify Sensor folder is not polluted with children
                var sensorNode = featFolder.Children.FirstOrDefault(c => c.Title.Contains("传感器") || c.Title.Contains("Sensor"));
                if (sensorNode != null)
                {
                    Assert.Empty(sensorNode.Children);
                }

                // 6. Verify Sketch Constraints are inferred and populated
                var sketchNode = featFolder.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureSketch);
                if (sketchNode != null)
                {
                    treeVm.SelectedNode = sketchNode;
                    Assert.NotEmpty(sketchVm.Constraints);
                }

                // 7. Verify Mesh Edge Model is built from OutlineEdges
                Assert.NotNull(meshVm.WpfEdgeMesh);
                Assert.True(meshVm.ShowEdges);

                // 8. Close file
                mainVm.CloseFileCommand.Execute(null);
                Assert.False(mainVm.IsFileLoaded);
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
            throw new Exception("Exception in STA interaction simulation: " + threadEx.ToString(), threadEx);
        }
    }

    [Fact]
    public void DocumentTree_NodesHaveSolidWorksIconsAssigned()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var icon = SolidWorksIconService.GetFeatureIcon(SldFile.FeatureKind.Extrusion, "凸台-拉伸1");
                if (SldFile.Resources.SolidWorksInstallation.Current != null)
                {
                    Assert.NotNull(icon);
                }

                var docService = new SwDocumentService();
                var treeVm = new DocumentTreeViewModel(docService);

                var sw = docService.OpenAsync(_cubePart).GetAwaiter().GetResult();
                Assert.NotNull(sw);
                treeVm.BuildTree(sw!, _cubePart);

                var root = treeVm.RootNodes.Single();
                Assert.NotNull(root);
                if (SldFile.Resources.SolidWorksInstallation.Current != null)
                {
                    Assert.NotNull(root.Icon);

                    var featFolder = root.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureFolder);
                    Assert.NotNull(featFolder);
                    Assert.NotNull(featFolder!.Icon);

                    var extrudeNode = featFolder.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureExtrude);
                    Assert.NotNull(extrudeNode);
                    Assert.NotNull(extrudeNode!.Icon);

                    var planeNode = featFolder.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeaturePlane);
                    Assert.NotNull(planeNode);
                    Assert.NotNull(planeNode!.Icon);

                    var originNode = featFolder.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureOrigin);
                    Assert.NotNull(originNode);
                    Assert.NotNull(originNode!.Icon);
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

        if (threadEx != null) throw new Exception("Thread failed: " + threadEx.Message + " at " + threadEx.StackTrace, threadEx);
    }
}
