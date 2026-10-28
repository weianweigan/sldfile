using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SldFileViewer.Models;

public enum TreeNodeKind
{
    DocumentRoot,
    CustomProperties,
    Equations,
    GeometryPreview,
    Bom,
    FeaturePlane,
    FeatureOrigin,
    FeatureSketch,
    FeatureExtrude,
    FeatureFolder,
    FeatureGeneric,
    AssemblyComponent,
    AssemblyMate,
    DrawingSheet
}

public partial class TreeNodeModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subTitle = string.Empty;

    [ObservableProperty]
    private TreeNodeKind _kind;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Associated model payload (e.g. SketchFeature, AssemblyComponentNode, BomItem, SwFile, etc.)
    /// </summary>
    public object? Tag { get; set; }

    public ObservableCollection<TreeNodeModel> Children { get; } = new();

    public TreeNodeModel() { }

    public TreeNodeModel(string title, TreeNodeKind kind, object? tag = null, string subTitle = "")
    {
        _title = title;
        _kind = kind;
        Tag = tag;
        _subTitle = subTitle;
    }
}
