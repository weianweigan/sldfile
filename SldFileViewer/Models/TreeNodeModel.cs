using System.Collections.ObjectModel;
using System.Windows.Media;
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
    FeatureCut,
    FeatureRevolve,
    FeatureSweep,
    FeatureLoft,
    FeatureFillet,
    FeatureChamfer,
    FeatureHole,
    FeaturePattern,
    FeatureMirror,
    FeatureRib,
    FeatureShell,
    FeatureDraft,
    FeatureThread,
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
    private ImageSource? _icon;

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

    public TreeNodeModel(string title, TreeNodeKind kind, object? tag = null, string subTitle = "", ImageSource? icon = null)
    {
        _title = title;
        _kind = kind;
        Tag = tag;
        _subTitle = subTitle;
        _icon = icon;
    }
}
