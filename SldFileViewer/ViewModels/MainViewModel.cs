using System;
using System.IO;
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

            // Initialize all child view models
            DocumentTree.BuildTree(sw, filePath);
            CustomProps.LoadProperties();
            Equations.LoadEquations();
            MeshViewer.LoadMesh();
            if (sw.Kind == DocumentKind.Assembly)
            {
                Bom.LoadBom();
            }

            // Set default view to MeshViewer or Preview
            if (MeshViewer.HasMesh)
            {
                CurrentView = MeshViewer;
            }
            else
            {
                var png = _docService.GetPreviewPng();
                if (png != null)
                {
                    ImagePreview.LoadImage(png, "模型预览");
                    CurrentView = ImagePreview;
                }
                else
                {
                    CurrentView = CustomProps;
                }
            }

            DocumentInfo = $"{sw.Kind} | 配置数: {sw.Configurations.Count} | 用户: {string.Join(", ", sw.Users)}";
            StatusMessage = $"成功打开: {Path.GetFileName(filePath)}";
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

    [RelayCommand]
    private void CloseFile()
    {
        _docService.Close();
        IsFileLoaded = false;
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
                if (MeshViewer.HasMesh)
                {
                    CurrentView = MeshViewer;
                }
                else
                {
                    var img = _docService.GetPreviewPng() ?? _docService.GetPreviewBitmap();
                    ImagePreview.LoadImage(img, "模型缩略图");
                    CurrentView = ImagePreview;
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
                    // Find sketch in service
                    var sketches = _docService.GetSketches(0);
                    var matched = sketches.FirstOrDefault(s => string.Equals(s.Name, feat.Name, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        SketchCanvas.LoadSketch(matched);
                        CurrentView = SketchCanvas;
                    }
                    else
                    {
                        // Synthesize sketch from feature metadata (dimensions, name, id)
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
