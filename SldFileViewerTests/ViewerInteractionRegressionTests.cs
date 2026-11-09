using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using SldFile;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using SldFileViewer.Views.Panels;
using Xunit;

namespace SldFileViewerTests;

[CollectionDefinition("Viewer interaction UI", DisableParallelization = true)]
public class ViewerInteractionUiCollection { }

[Collection("Viewer interaction UI")]
public class ViewerInteractionRegressionTests
{
    private sealed class Dialogs : IDialogService
    {
        public bool ShowOpenFileDialog(string filter, out string selectedPath) { selectedPath = ""; return false; }
        public bool ShowSaveFileDialog(string name, string filter, out string selectedPath) { selectedPath = ""; return false; }
        public void ShowError(string title, string message) { }
        public void ShowInformation(string title, string message) { }
    }

    private static MeshViewerViewModel Create(SwDocumentService service) => new(service, new ExportService(), new Dialogs());

    [Fact]
    public async Task ComponentHighlightUsesReferencedGeometryAndInstanceTransform()
    {
        using var service = new SwDocumentService();
        await service.OpenAsync(ViewerIntegrationTests.ResolveModelPath("assembly.SLDASM"));
        var component = service.GetAssemblyTree().Single(c => c.Name == "cylinder");
        var vm = Create(service);
        vm.LoadMesh();
        // This assembly fixture has no saved DisplayLists; use its resolved instance mesh as the viewport mesh.
        vm.Mesh = service.GetComponentDisplayMesh(component);
        vm.HighlightComponent(component);

        using var source = SwFile.Open(ViewerIntegrationTests.ResolveModelPath("cylinder.SLDPRT"));
        var sourceMesh = source.GetDisplayMesh()!;
        Assert.NotNull(vm.WpfComponentHighlightMesh);
        Assert.Equal(sourceMesh.VertexCount, vm.WpfComponentHighlightMesh!.Positions.Count);
        var transform = component.Transform;
        transform.M41 *= 1000;
        transform.M42 *= 1000;
        transform.M43 *= 1000;
        var center = (vm.Mesh!.Min + vm.Mesh.Max) * 0.5f;
        for (var i = 0; i < sourceMesh.VertexCount; i++)
        {
            var expected = Vector3.Transform(sourceMesh.Positions[i], transform) - center;
            var actual = vm.WpfComponentHighlightMesh.Positions[i];
            Assert.Equal(expected.X, actual.X, 4);
            Assert.Equal(expected.Y, actual.Y, 4);
            Assert.Equal(expected.Z, actual.Z, 4);
        }
        Assert.True(vm.TryGetComponentBounds(out var bounds));
        Assert.Equal(vm.WpfComponentHighlightMesh.Bounds, bounds);
        Assert.True(vm.ZoomToComponentCommand.CanExecute(null));
        bool fired = false;
        vm.RequestZoomToComponent += () => fired = true;
        vm.ZoomToComponentCommand.Execute(null);
        Assert.True(fired);

        // A feature selection must not keep the old component as the zoom target.
        vm.HighlightFeature(new Feature("Extrude", FeatureKind.Extrusion, "Extrude", 1));
        Assert.Null(vm.SelectedAssemblyComponent);
        Assert.False(vm.ZoomToComponentCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadingAnotherDocumentClearsComponentAndFeatureHighlights()
    {
        using var service = new SwDocumentService();
        await service.OpenAsync(ViewerIntegrationTests.ResolveModelPath("assembly.SLDASM"));
        var component = service.GetAssemblyTree().First();
        var vm = Create(service);
        vm.LoadMesh();
        vm.Mesh = service.GetComponentDisplayMesh(component);
        vm.HighlightComponent(component);
        Assert.NotNull(vm.WpfComponentHighlightMesh);

        await service.OpenAsync(ViewerIntegrationTests.ResolveModelPath("cube.SLDPRT"));
        vm.LoadMesh();
        Assert.Null(vm.SelectedAssemblyComponent);
        Assert.Null(vm.HighlightedComponentName);
        Assert.Null(vm.WpfComponentHighlightMesh);
        Assert.False(vm.ZoomToComponentCommand.CanExecute(null));
        Assert.Null(service.GetComponentDisplayMesh(component));

        vm.HighlightFeature(new Feature("Extrude", FeatureKind.Extrusion, "Extrude", 1));
        Assert.NotNull(vm.HighlightedComponentName);
        service.Close();
        vm.LoadMesh();
        Assert.Null(vm.HighlightedComponentName);
        Assert.Null(vm.WpfComponentHighlightMesh);
    }

    [Fact]
    public void WireframeSurfacesRemainPickable()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    new Application();
                    SldFileViewer.Theme.ThemeManager.ApplyTheme();
                }
                using var service = new SwDocumentService();
                var vm = Create(service);
                var mesh = new DisplayMesh
                {
                    Positions = new[] { new Vector3(-10, -10, 0), new Vector3(10, -10, 0), new Vector3(0, 10, 0) },
                    TriangleIndices = new[] { 0, 1, 2 },
                    Min = new Vector3(-10, -10, 0), Max = new Vector3(10, 10, 0)
                };
                vm.Mesh = mesh;
                vm.WpfMesh = new MeshGeometry3D
                {
                    Positions = new Point3DCollection { new(-10, -10, 0), new(10, -10, 0), new(0, 10, 0) },
                    TriangleIndices = new System.Windows.Media.Int32Collection { 0, 1, 2 }
                };
                vm.DisplayStyle = ModelDisplayStyle.Wireframe;
                var view = new Mesh3DView { DataContext = vm };
                view.Measure(new Size(1000, 600));
                view.Arrange(new Rect(0, 0, 1000, 600));
                view.UpdateLayout();
                typeof(Mesh3DView).GetMethod("UpdateMesh", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(view, new object[] { false });
                var viewport = (Viewport3D)view.FindName("MainViewport");
                viewport.Camera = new OrthographicCamera(new Point3D(0, 0, 100),
                    new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 40);
                Assert.True(viewport.ActualWidth > 0 && viewport.ActualHeight > 0);
                var visual = (ModelVisual3D)view.FindName("ModelVisual");
                var surface = Assert.IsType<GeometryModel3D>(Assert.IsType<Model3DGroup>(visual.Content).Children.Single());
                Assert.Same(vm.WpfMesh, surface.Geometry);
                var material = Assert.IsType<System.Windows.Media.Media3D.DiffuseMaterial>(surface.Material);
                Assert.Equal(0, Assert.IsType<System.Windows.Media.SolidColorBrush>(material.Brush).Color.A);
                object?[] args = { new Point(viewport.ActualWidth / 2, viewport.ActualHeight / 2), null };
                var picked = typeof(Mesh3DView).GetMethod("HitTestMeshEntity", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .Invoke(view, args);
                Assert.Equal(true, picked);
                Assert.NotNull(args[1]);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
