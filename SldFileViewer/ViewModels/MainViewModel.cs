using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFile.Drawing;
using SldFile.Sketches;
using SldFileViewer.Models;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _windowTitle = "SolidWorks 文件可视化解析工具 (SldFileViewer)";

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private bool _isFileLoaded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "就绪。请打开 SolidWorks 文件 (.sldprt, .sldasm, .slddrw)";

    [ObservableProperty]
    private string _documentInfo = string.Empty;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private ConfigurationItem? _selectedConfiguration;

    [ObservableProperty]
    private bool _hasConfigurations;

    public ObservableCollection<ConfigurationItem> Configurations { get; } = new();

    public DocumentTreeViewModel DocumentTree { get; }
    public SketchCanvasViewModel SketchCanvas { get; }
    public CustomPropsViewModel CustomProps { get; }
    public EquationsViewModel Equations { get; }
    public BomViewModel Bom { get; }
    public MeshViewerViewModel MeshViewer { get; }
    public ImagePreviewViewModel ImagePreview { get; }
    public FeatureDetailViewModel FeatureDetail { get; }

    public MainViewModel(
        ISwDocumentService docService,
        IDialogService dialogService,
        DocumentTreeViewModel documentTree,
        SketchCanvasViewModel sketchCanvas,
        CustomPropsViewModel customProps,
        EquationsViewModel equations,
        BomViewModel bom,
        MeshViewerViewModel meshViewer,
        ImagePreviewViewModel imagePreview,
        FeatureDetailViewModel featureDetail)
    {
        _docService = docService;
        _dialogService = dialogService;

        DocumentTree = documentTree;
        SketchCanvas = sketchCanvas;
        CustomProps = customProps;
        Equations = equations;
        Bom = bom;
        MeshViewer = meshViewer;
        ImagePreview = imagePreview;
        FeatureDetail = featureDetail;

        DocumentTree.NodeSelected += OnTreeNodeSelected;
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        const string filter = "SolidWorks 文件 (*.sldprt;*.sldasm;*.slddrw)|*.sldprt;*.sldasm;*.slddrw|零件 (*.sldprt)|*.sldprt|装配体 (*.sldasm)|*.sldasm|工程图 (*.slddrw)|*.slddrw|所有文件 (*.*)|*.*";
        if (!_dialogService.ShowOpenFileDialog(filter, out string filePath)) return;

        await LoadDocumentAsync(filePath);
    }

    public async Task LoadDocumentAsync(string filePath)
    {
        IsLoading = true;
        StatusMessage = $"正在解析 SolidWorks 文件: {Path.GetFileName(filePath)}...";

        try
        {
            var sw = await _docService.OpenAsync(filePath);
            if (sw == null) throw new InvalidOperationException("无法打开指定的文件。");

            CurrentFilePath = filePath;
            IsFileLoaded = true;
            WindowTitle = $"{Path.GetFileName(filePath)} - SldFileViewer";

            // Populate configurations
            Configurations.Clear();
            foreach (var cfg in sw.Configurations)
            {
                Configurations.Add(new ConfigurationItem(cfg));
            }
            HasConfigurations = Configurations.Count > 0;

            // Initialize all child view models
            DocumentTree.BuildTree(sw, filePath);
            CustomProps.LoadProperties();
            Equations.LoadEquations();
            MeshViewer.LoadMesh();

            // Set active configuration (triggers OnSelectedConfigurationChanged to link BOM, properties, and tree)
            SelectedConfiguration = Configurations.FirstOrDefault();

            // Load initial preview for selected configuration
            int initCfgIdx = SelectedConfiguration?.Index ?? 0;
            string initCfgName = SelectedConfiguration?.Name ?? "默认";
            var initialPng = _docService.GetPreviewPng(initCfgIdx) ?? _docService.GetPreviewBitmap(initCfgIdx);
            if (initialPng != null)
            {
                ImagePreview.LoadImage(initialPng, $"模型预览 (配置: {initCfgName})");
            }

            // Set default view to MeshViewer or Preview
            if (MeshViewer.HasMesh)
            {
                CurrentView = MeshViewer;
            }
            else if (initialPng != null)
            {
                CurrentView = ImagePreview;
            }
            else
            {
                CurrentView = CustomProps;
            }

            DocumentInfo = $"{sw.Kind} | 配置数: {sw.Configurations.Count} | 用户: {string.Join(", ", sw.Users)}";
            StatusMessage = $"成功打开: {Path.GetFileName(filePath)}" + (SelectedConfiguration != null ? $" (当前配置: {SelectedConfiguration.Name})" : "");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("打开文件失败", $"解析文件时出现异常:\n{ex.Message}");
            StatusMessage = "解析文件失败。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedConfigurationChanged(ConfigurationItem? value)
    {
        if (value == null) return;

        // 1. 同步联动自定义属性视图筛选
        CustomProps.SelectedConfiguration = value.Name;

        // 2. 装配体联动：刷新 BOM 和装配体树
        if (_docService.CurrentFile?.Kind == DocumentKind.Assembly)
        {
            Bom.LoadBom(value.Name);
            DocumentTree.UpdateAssemblyComponents(value.Name);
        }
        else if (_docService.CurrentFile?.Kind == DocumentKind.Part)
        {
            DocumentTree.UpdatePartSketches(value.Index);
        }

        // 3. 3D 检视器 Parasolid 配置索引联动
        MeshViewer.CurrentConfigIndex = value.Index;

        // 4. 关键：提取当前配置专属的高清渲染预览图，动态刷新模型预览！
        var png = _docService.GetPreviewPng(value.Index);
        var bmp = png == null ? _docService.GetPreviewBitmap(value.Index) : null;
        var previewData = png ?? bmp;
        if (previewData != null)
        {
            ImagePreview.LoadImage(previewData, $"模型预览 (配置: {value.Name})");
        }

        StatusMessage = $"已切换至配置: {value.Name} (索引: {value.Index})";
    }

    [RelayCommand]
    public void SwitchToConfigPreview()
    {
        int idx = SelectedConfiguration?.Index ?? 0;
        string name = SelectedConfiguration?.Name ?? "默认";
        var png = _docService.GetPreviewPng(idx) ?? _docService.GetPreviewBitmap(idx);
        if (png != null)
        {
            ImagePreview.LoadImage(png, $"模型预览 (配置: {name})");
            CurrentView = ImagePreview;
        }
    }

    [RelayCommand]
    public void SwitchTo3DView()
    {
        if (MeshViewer.HasMesh)
        {
            CurrentView = MeshViewer;
        }
    }

    [RelayCommand]
    private void CloseFile()
    {
        _docService.Close();
        IsFileLoaded = false;
        HasConfigurations = false;
        Configurations.Clear();
        SelectedConfiguration = null;
        CurrentFilePath = string.Empty;
        WindowTitle = "SolidWorks 文件可视化解析工具 (SldFileViewer)";
        StatusMessage = "已关闭文件。";
        DocumentInfo = string.Empty;
        CurrentView = null;
        DocumentTree.RootNodes.Clear();
    }

    private void OnTreeNodeSelected(TreeNodeModel node)
    {
        switch (node.Kind)
        {
            case TreeNodeKind.CustomProperties:
                CurrentView = CustomProps;
                break;

            case TreeNodeKind.Equations:
                CurrentView = Equations;
                break;

            case TreeNodeKind.GeometryPreview:
                int cfgIdx = SelectedConfiguration?.Index ?? 0;
                string cfgName = SelectedConfiguration?.Name ?? "默认";
                var img = _docService.GetPreviewPng(cfgIdx) ?? _docService.GetPreviewBitmap(cfgIdx);
                if (img != null)
                {
                    ImagePreview.LoadImage(img, $"模型预览 (配置: {cfgName})");
                }

                if (MeshViewer.HasMesh)
                {
                    CurrentView = MeshViewer;
                }
                else if (img != null)
                {
                    CurrentView = ImagePreview;
                }
                else
                {
                    CurrentView = CustomProps;
                }
                break;

            case TreeNodeKind.Bom:
                CurrentView = Bom;
                break;

            case TreeNodeKind.FeatureSketch:
                if (node.Tag is SketchFeature sketch)
                {
                    SketchCanvas.LoadSketch(sketch);
                    CurrentView = SketchCanvas;
                }
                else if (node.Tag is Feature feat)
                {
                    int featCfgIdx = SelectedConfiguration?.Index ?? 0;
                    var sketches = _docService.GetSketches(featCfgIdx);
                    var matched = sketches.FirstOrDefault(s => string.Equals(s.Name, feat.Name, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        SketchCanvas.LoadSketch(matched);
                        CurrentView = SketchCanvas;
                    }
                    else
                    {
                        var syn = new SketchFeature(feat.Name)
                        {
                            Id = feat.Id,
                            SourceStream = feat.Attributes.TryGetValue("ClassName", out var cls) ? cls : "swXmlContents/KeyWords"
                        };
                        foreach (var dim in feat.Dimensions)
                        {
                            syn.AddDistanceDimension(dim.Name, double.TryParse(dim.Value, out var dv) ? dv : 0.0);
                        }
                        SketchCanvas.LoadSketch(syn);
                        CurrentView = SketchCanvas;
                    }
                }
                break;

            case TreeNodeKind.DrawingSheet:
                if (node.Tag is DrawingSheetInfo sheet)
                {
                    var sheetPng = sheet.PreviewPng;
                    ImagePreview.LoadImage(sheetPng, $"工程图纸: {sheet.Name}");
                    CurrentView = ImagePreview;
                }
                break;

            case TreeNodeKind.FeatureExtrude:
            case TreeNodeKind.FeaturePlane:
            case TreeNodeKind.FeatureOrigin:
            case TreeNodeKind.FeatureGeneric:
                if (node.Tag is Feature f)
                {
                    FeatureDetail.LoadFeature(f);
                }
                else
                {
                    FeatureDetail.LoadGeneric(node.Title, node.Kind.ToString());
                }
                CurrentView = FeatureDetail;
                break;

            case TreeNodeKind.AssemblyComponent:
                if (node.Tag is AssemblyComponentNode comp)
                {
                    FeatureDetail.LoadGeneric(comp.Name, "Assembly Component",
                        comp.IsVirtual ? "虚拟组件" : $"配置: {comp.ConfigurationName}, 引用: {comp.FilePath}");
                }
                CurrentView = FeatureDetail;
                break;

            default:
                break;
        }
    }
}
