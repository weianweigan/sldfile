using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SldFile;
using SldFileViewer.Models;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using Xunit;

namespace SldFileViewerTests;

/// <summary>
/// FeatureData display: the viewer must surface the typed offline definitions
/// (<see cref="Feature.Definition"/>, the IFeature.GetDefinition() equivalent)
/// and classify unresolved / unsupported classes instead of silently hiding them.
/// </summary>
public class FeatureDataViewTests
{
    private static string ResolveFeatureSample(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine("TestFiles", "Features2026", fileName),
            Path.Combine("SldFileTests", "TestFiles", "Features2026", fileName),
            Path.Combine("..", "SldFileTests", "TestFiles", "Features2026", fileName)
        };

        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = startDir;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                foreach (var rel in candidates)
                {
                    var candidate = Path.Combine(dir, rel);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                dir = Path.GetDirectoryName(dir);
            }
        }

        throw new FileNotFoundException($"Feature sample '{fileName}' not found.");
    }

    private static TDefinition FindDefinition<TDefinition>(IEnumerable<Feature> features)
        where TDefinition : class, IFeatureData
    {
        foreach (var feature in features)
        {
            var match = FindDefinition<TDefinition>(feature);
            if (match != null) return match;
        }
        throw new InvalidOperationException($"No feature with {typeof(TDefinition).Name} found.");
    }

    private static TDefinition? FindDefinition<TDefinition>(Feature feature) where TDefinition : class, IFeatureData
    {
        if (feature.Model is TDefinition typed) return typed;
        foreach (var child in feature.Children)
        {
            var match = FindDefinition<TDefinition>(child);
            if (match != null) return match;
        }
        return null;
    }

    private static Feature FindFeatureWithDefinition(IReadOnlyList<Feature> features, IFeatureData definition) =>
        FindFeatureWithDefinition(features, f => ReferenceEquals(f.Model, definition));

    private static Feature FindFeatureWithDefinition(IReadOnlyList<Feature> features, Func<Feature, bool> predicate)
    {
        Feature? Visit(Feature feature)
        {
            if (predicate(feature)) return feature;
            foreach (var child in feature.Children)
            {
                var match = Visit(child);
                if (match != null) return match;
            }
            return null;
        }

        foreach (var feature in features)
        {
            var match = Visit(feature);
            if (match != null) return match;
        }
        throw new InvalidOperationException("No feature matched the predicate.");
    }

    private static FeatureDataPropertyItem Row(FeatureDataViewModel vm, string name) =>
        vm.Sections.SelectMany(s => s.Items).Single(r => r.Name == name);

    private sealed class MockDialogService : IDialogService
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
    public void ExtrudeSample_FeatureData_ExposesTypedParameters()
    {
        using var file = SwFile.Open(ResolveFeatureSample("extrude_10mm.SLDPRT"), readOnly: true);
        var definition = FindDefinition<ExtrudeFeatureModel>(file.Features);
        var feature = FindFeatureWithDefinition(file.Features, definition);

        var vm = new FeatureDataViewModel();
        vm.Load(feature);

        Assert.Equal(FeatureDataStatus.Resolved, vm.Status);
        Assert.True(vm.IsVisible);
        Assert.True(vm.IsResolved);
        Assert.Equal("moExtrusion_c", vm.ClassName);
        Assert.Equal(definition.TypeId, vm.TypeId);
        Assert.Equal(definition.GetType().Name, vm.ModelType);
        Assert.Contains("已解析", vm.StatusText);

        Assert.True(vm.HasSections);
        Assert.True(vm.PropertyCount > 0);

        var depth = Row(vm, nameof(ExtrudeFeatureModel.Depth));
        Assert.Contains("10 mm", depth.Value);
        Assert.Equal(definition.IsResolved(nameof(ExtrudeFeatureModel.Depth)), depth.IsResolved);
        Assert.Contains("深度", depth.Group);
    }

    [Fact]
    public void SheetMetalSample_FeatureData_MapsParameterResolutionFlags()
    {
        using var file = SwFile.Open(ResolveFeatureSample("sm_base_flange_10mm.SLDPRT"), readOnly: true);
        var definition = FindDefinition<SheetMetalBaseFlangeModel>(file.Features);
        var feature = FindFeatureWithDefinition(file.Features, definition);

        var vm = new FeatureDataViewModel();
        vm.Load(feature);

        Assert.Equal(FeatureDataStatus.Resolved, vm.Status);
        Assert.Equal("moSMBaseFlange_c", vm.ClassName);

        var thickness = Row(vm, nameof(SheetMetalBaseFlangeModel.Thickness));
        Assert.Contains("1 mm", thickness.Value);
        Assert.Equal(definition.ParameterResolution.Thickness, thickness.IsResolved);

        var reliefWidth = Row(vm, nameof(SheetMetalBaseFlangeModel.ReliefWidth));
        Assert.Equal(
            definition.ParameterResolution.ReliefWidth
                ? "已解析"
                : reliefWidth.IsUnresolved ? "未解析" : "未标记",
            reliefWidth.Status);
    }

    [Fact]
    public void ReferencePlaneWithoutDefinition_ReportsMissingFeatureData()
    {
        using var file = SwFile.Open(ResolveFeatureSample("extrude_10mm.SLDPRT"), readOnly: true);
        var plane = FindFeatureWithDefinition(file.Features, f =>
            f.Kind == FeatureKind.ReferencePlane && f.Model == null);

        var vm = new FeatureDataViewModel();
        vm.Load(plane);

        Assert.True(vm.IsVisible);
        Assert.True(vm.IsUnparsed || vm.IsUnsupported);
        Assert.Contains("FeatureData 为 null", vm.Summary);
        Assert.False(vm.HasSections);
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusDescription));
    }

    [Fact]
    public void Clear_ResetsStatusAndRows()
    {
        using var file = SwFile.Open(ResolveFeatureSample("extrude_10mm.SLDPRT"), readOnly: true);
        var definition = FindDefinition<ExtrudeFeatureModel>(file.Features);
        var feature = FindFeatureWithDefinition(file.Features, definition);

        var vm = new FeatureDataViewModel();
        vm.Load(feature);
        Assert.True(vm.HasSections);

        vm.Clear();

        Assert.Equal(FeatureDataStatus.None, vm.Status);
        Assert.False(vm.IsVisible);
        Assert.Empty(vm.Sections);
        Assert.Empty(vm.References);
    }

    [Fact]
    public void FeatureDetail_LoadFeature_PopulatesFeatureDataAndResetsOnGeneric()
    {
        using var file = SwFile.Open(ResolveFeatureSample("extrude_10mm.SLDPRT"), readOnly: true);
        var definition = FindDefinition<ExtrudeFeatureModel>(file.Features);
        var feature = FindFeatureWithDefinition(file.Features, definition);

        var detail = new FeatureDetailViewModel();
        detail.LoadFeature(feature);

        Assert.True(detail.FeatureData.IsVisible);
        Assert.True(detail.FeatureData.HasSections);
        Assert.Equal(detail.FeatureName, feature.Name);

        detail.LoadGeneric("Assembly Component", "Assembly Component");
        Assert.False(detail.FeatureData.IsVisible);
        Assert.Equal(FeatureDataStatus.None, detail.FeatureData.Status);
    }

    [Fact]
    public async Task TreeAndDetail_ExposeFeatureDataNodeForExtrudeSample()
    {
        var path = ResolveFeatureSample("extrude_10mm.SLDPRT");
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

        try
        {
            await mainVm.LoadDocumentAsync(path);

            var featTree = treeVm.RootNodes[0].Children.First(c => c.Kind == TreeNodeKind.FeatureFolder);
            var featureDataNode = featTree.Children.FirstOrDefault(c => c.Kind == TreeNodeKind.FeatureData);
            Assert.NotNull(featureDataNode);
            Assert.Contains("已解析", featureDataNode!.SubTitle);

            var extrudeNode = featTree.Children.First(c => c.Kind == TreeNodeKind.FeatureExtrude);
            Assert.Contains("FeatureData 已解析", extrudeNode.SubTitle);

            treeVm.SelectedNode = extrudeNode;
            Assert.Same(featVm, mainVm.CurrentView);
            Assert.True(featVm.FeatureData.IsVisible);
            Assert.Equal(FeatureDataStatus.Resolved, featVm.FeatureData.Status);

            // FeatureData root node navigates to the first decoded definition.
            treeVm.SelectedNode = featureDataNode;
            Assert.Same(featVm, mainVm.CurrentView);
            Assert.True(featVm.FeatureData.IsVisible);
        }
        finally
        {
            mainVm.CloseFileCommand.Execute(null);
        }
    }

    [Fact]
    public void FeatureDetailView_RendersFeatureDataSectionOnStaThread()
    {
        using var file = SwFile.Open(ResolveFeatureSample("sm_base_flange_10mm.SLDPRT"), readOnly: true);
        var definition = FindDefinition<SheetMetalBaseFlangeModel>(file.Features);
        var feature = FindFeatureWithDefinition(file.Features, definition);

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

                var vm = new FeatureDetailViewModel();
                vm.LoadFeature(feature);

                var view = new SldFileViewer.Views.Panels.FeatureDetailView { DataContext = vm };
                view.Measure(new System.Windows.Size(900, 700));
                view.Arrange(new System.Windows.Rect(0, 0, 900, 700));
                view.UpdateLayout();
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
            throw new Exception("Exception in STA thread: " + threadEx, threadEx);
    }
}
