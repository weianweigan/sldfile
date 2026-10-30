using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SldFile;
using SldFile.Sketches;
using SldFileViewer.Models;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class DocumentTreeViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private TreeNodeModel? _componentsFolder;
    private TreeNodeModel? _configurationsFolder;
    private TreeNodeModel? _featTreeFolder;
    private SwFile? _currentSwFile;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private TreeNodeModel? _selectedNode;

    public ObservableCollection<TreeNodeModel> RootNodes { get; } = new();

    public event Action<TreeNodeModel>? NodeSelected;

    public DocumentTreeViewModel(ISwDocumentService docService)
    {
        _docService = docService;
    }

    private static TreeNodeModel CreateNode(string title, TreeNodeKind kind, object? tag = null, string subTitle = "")
    {
        var icon = SolidWorksIconService.GetNodeIcon(kind, title, tag);
        return new TreeNodeModel(title, kind, tag, subTitle, icon);
    }

    public void BuildTree(SwFile swFile, string filePath)
    {
        _currentSwFile = swFile;
        _componentsFolder = null;
        _configurationsFolder = null;
        _featTreeFolder = null;
        RootNodes.Clear();

        string fileName = Path.GetFileName(filePath);
        var rootNode = CreateNode(fileName, TreeNodeKind.DocumentRoot, swFile, swFile.Kind.ToString());
        rootNode.IsExpanded = true;

        // 1. Dedicated System Nodes
        var propsNode = CreateNode("自定义属性 (Custom Properties)", TreeNodeKind.CustomProperties, "PROPS");
        var eqNode = CreateNode("方程式与全局变量 (Equations)", TreeNodeKind.Equations, "EQUATIONS");
        var geoNode = CreateNode("几何与模型预览 (Geometry Preview)", TreeNodeKind.GeometryPreview, "PREVIEW");
        geoNode.IsExpanded = true;

        rootNode.Children.Add(propsNode);
        rootNode.Children.Add(eqNode);
        rootNode.Children.Add(geoNode);

        // Configuration Manager Branch
        if (swFile.Configurations.Count > 0)
        {
            _configurationsFolder = CreateNode("配置管理器 (Configurations)", TreeNodeKind.ConfigurationFolder, null, $"{swFile.Configurations.Count} 个配置");
            _configurationsFolder.IsExpanded = true;
            foreach (var config in swFile.Configurations)
            {
                var item = new ConfigurationItem(config);
                var cfgNode = CreateNode(item.Name, TreeNodeKind.Configuration, item, $"索引: {item.Index}");
                _configurationsFolder.Children.Add(cfgNode);
            }
            rootNode.Children.Add(_configurationsFolder);
        }

        // BOM node for Assembly
        if (swFile.Kind == DocumentKind.Assembly)
        {
            var bomNode = CreateNode("BOM 物料清单 (Bill of Materials)", TreeNodeKind.Bom, "BOM");
            rootNode.Children.Add(bomNode);
        }

        // 2. Model Structure Branch
        if (swFile.Kind == DocumentKind.Assembly)
        {
            BuildAssemblyBranches(rootNode, swFile);
        }
        else if (swFile.Kind == DocumentKind.Drawing)
        {
            BuildDrawingBranches(rootNode, swFile);
        }
        else
        {
            BuildPartBranches(rootNode, swFile, 0);
        }

        RootNodes.Add(rootNode);

        // Select preview or properties by default
        SelectedNode = geoNode;
        geoNode.IsSelected = true;
    }

    private void BuildPartBranches(TreeNodeModel rootNode, SwFile swFile, int configIndex)
    {
        var features = _docService.GetFeatures();
        var sketches = _docService.GetSketches(configIndex);
        var sketchDict = sketches.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        // FeatureManager Tree Branch
        var featTree = CreateNode("特征管理器 (FeatureManager)", TreeNodeKind.FeatureFolder, null, $"{features.Count} 个特征");
        featTree.IsExpanded = true;
        _featTreeFolder = featTree;

        // Match base planes & origin from features if present
        var frontPlane = features.FirstOrDefault(f => f.Name.Contains("前视") || f.Name.Equals("Front Plane", StringComparison.OrdinalIgnoreCase));
        var topPlane = features.FirstOrDefault(f => f.Name.Contains("上视") || f.Name.Equals("Top Plane", StringComparison.OrdinalIgnoreCase));
        var rightPlane = features.FirstOrDefault(f => f.Name.Contains("右视") || f.Name.Equals("Right Plane", StringComparison.OrdinalIgnoreCase));
        var origin = features.FirstOrDefault(f => f.Name.Contains("原点") || f.Name.Equals("Origin", StringComparison.OrdinalIgnoreCase));

        featTree.Children.Add(CreateNode("前视基准面 (Front Plane)", TreeNodeKind.FeaturePlane, frontPlane, "基准面"));
        featTree.Children.Add(CreateNode("上视基准面 (Top Plane)", TreeNodeKind.FeaturePlane, topPlane, "基准面"));
        featTree.Children.Add(CreateNode("右视基准面 (Right Plane)", TreeNodeKind.FeaturePlane, rightPlane, "基准面"));
        featTree.Children.Add(CreateNode("原点 (Origin)", TreeNodeKind.FeatureOrigin, origin, "原点坐标"));

        var addedFeatures = new HashSet<Feature>();
        if (frontPlane != null) addedFeatures.Add(frontPlane);
        if (topPlane != null) addedFeatures.Add(topPlane);
        if (rightPlane != null) addedFeatures.Add(rightPlane);
        if (origin != null) addedFeatures.Add(origin);

        void MarkDescendants(Feature parent)
        {
            foreach (var ch in parent.Children)
            {
                addedFeatures.Add(ch);
                MarkDescendants(ch);
            }
        }
        foreach (var f in features)
        {
            MarkDescendants(f);
        }

        TreeNodeModel CreateFeatureNode(Feature f)
        {
            TreeNodeKind kind = f.Kind switch
            {
                FeatureKind.Sketch => TreeNodeKind.FeatureSketch,
                FeatureKind.Extrusion => TreeNodeKind.FeatureExtrude,
                FeatureKind.Cut => TreeNodeKind.FeatureCut,
                FeatureKind.Revolve => TreeNodeKind.FeatureRevolve,
                FeatureKind.Sweep => TreeNodeKind.FeatureSweep,
                FeatureKind.Loft => TreeNodeKind.FeatureLoft,
                FeatureKind.Fillet => TreeNodeKind.FeatureFillet,
                FeatureKind.Chamfer => TreeNodeKind.FeatureChamfer,
                FeatureKind.HoleWizard => TreeNodeKind.FeatureHole,
                FeatureKind.Pattern => TreeNodeKind.FeaturePattern,
                FeatureKind.Mirror => TreeNodeKind.FeatureMirror,
                FeatureKind.Rib => TreeNodeKind.FeatureRib,
                FeatureKind.Shell => TreeNodeKind.FeatureShell,
                FeatureKind.Draft => TreeNodeKind.FeatureDraft,
                FeatureKind.Thread => TreeNodeKind.FeatureThread,
                FeatureKind.ReferencePlane => TreeNodeKind.FeaturePlane,
                FeatureKind.Folder => TreeNodeKind.FeatureFolder,
                _ => TreeNodeKind.FeatureGeneric
            };

            object tag = f;
            string sub = f.Kind.ToString();

            if (f.Kind == FeatureKind.Sketch)
            {
                if (sketchDict.TryGetValue(f.Name, out var skObj))
                    tag = skObj;
                sub = f.Dimensions.Count > 0 ? $"草图 ({f.Dimensions.Count} 尺寸)" : "草图";
            }
            else if (f.Kind == FeatureKind.Extrusion || f.Kind == FeatureKind.Cut)
            {
                sub = f.Dimensions.Count > 0 ? $"{f.Name} ({f.Dimensions[0].Name}={f.Dimensions[0].Value})" : f.Name;
            }

            var node = CreateNode(f.Name, kind, tag, sub);

            foreach (var child in f.Children)
            {
                node.Children.Add(CreateFeatureNode(child));
            }

            return node;
        }

        foreach (var f in features)
        {
            if (addedFeatures.Contains(f)) continue;
            if (f.Kind == FeatureKind.Dimension) continue;

            featTree.Children.Add(CreateFeatureNode(f));
        }

        // Also add any sketches decoded from binary that weren't in XML Features list
        foreach (var sk in sketches)
        {
            if (!featTree.Children.Any(c => string.Equals(c.Title, sk.Name, StringComparison.OrdinalIgnoreCase)))
            {
                featTree.Children.Add(CreateNode(sk.Name, TreeNodeKind.FeatureSketch, sk, "草图 (二进制反序列化)"));
            }
        }

        rootNode.Children.Add(featTree);
    }

    private void BuildAssemblyBranches(TreeNodeModel rootNode, SwFile swFile)
    {
        var components = _docService.GetAssemblyTree();
        _componentsFolder = CreateNode("装配零部件 (Components)", TreeNodeKind.FeatureFolder, null, $"{components.Count} 个顶层组件");
        _componentsFolder.IsExpanded = true;

        PopulateAssemblyComponentNodes(_componentsFolder, components);
        rootNode.Children.Add(_componentsFolder);

        // Mates
        var matesFolder = CreateNode("配合约束 (Mates)", TreeNodeKind.AssemblyMate, null);
        rootNode.Children.Add(matesFolder);
    }

    private void PopulateAssemblyComponentNodes(TreeNodeModel parent, IReadOnlyList<AssemblyComponentNode> nodes)
    {
        foreach (var node in nodes)
        {
            var item = CreateNode(node.Name, TreeNodeKind.AssemblyComponent, node,
                node.IsVirtual ? "[虚拟件]" : (node.ConfigurationName ?? "默认"));
            PopulateAssemblyComponentNodes(item, node.Children);
            parent.Children.Add(item);
        }
    }

    /// <summary>
    /// 当选中的配置改变时，按指定配置重新解析并刷新装配体零部件树
    /// </summary>
    public void UpdateAssemblyComponents(string? configName)
    {
        if (_componentsFolder == null) return;
        var components = _docService.GetAssemblyTree(configName);
        _componentsFolder.Children.Clear();
        _componentsFolder.SubTitle = $"{components.Count} 个顶层组件";
        PopulateAssemblyComponentNodes(_componentsFolder, components);
    }

    /// <summary>
    /// 当选中的配置改变时，按指定配置索引刷新零件草图数据
    /// </summary>
    public void UpdatePartSketches(int configIndex)
    {
        if (_featTreeFolder == null || _currentSwFile == null) return;
        var sketches = _docService.GetSketches(configIndex);
        var sketchDict = sketches.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        void UpdateSketchTags(TreeNodeModel node)
        {
            if (node.Kind == TreeNodeKind.FeatureSketch && sketchDict.TryGetValue(node.Title, out var sk))
            {
                node.Tag = sk;
            }
            foreach (var child in node.Children)
            {
                UpdateSketchTags(child);
            }
        }

        UpdateSketchTags(_featTreeFolder);
    }

    private void BuildDrawingBranches(TreeNodeModel rootNode, SwFile swFile)
    {
        var sheets = _docService.GetDrawingSheets();
        var sheetsFolder = CreateNode("图纸页 (Drawing Sheets)", TreeNodeKind.FeatureFolder, null, $"{sheets.Count} 张图纸");
        sheetsFolder.IsExpanded = true;

        for (int i = 0; i < sheets.Count; i++)
        {
            var s = sheets[i];
            var sheetNode = CreateNode(s.Name, TreeNodeKind.DrawingSheet, s, $"索引: {i}");
            foreach (var view in s.Views)
            {
                sheetNode.Children.Add(CreateNode(view.Name, TreeNodeKind.FeatureGeneric, view, view.ReferencedConfiguration));
            }
            sheetsFolder.Children.Add(sheetNode);
        }
        rootNode.Children.Add(sheetsFolder);
    }

    partial void OnFilterTextChanged(string value)
    {
        FilterNodes(RootNodes, value);
    }

    private bool FilterNodes(IEnumerable<TreeNodeModel> nodes, string filter)
    {
        bool anyChildVisible = false;
        foreach (var node in nodes)
        {
            bool match = string.IsNullOrWhiteSpace(filter) ||
                         node.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                         node.SubTitle.Contains(filter, StringComparison.OrdinalIgnoreCase);

            bool childMatch = FilterNodes(node.Children, filter);
            node.IsVisible = match || childMatch;
            if (node.IsVisible) anyChildVisible = true;
            if (childMatch && !string.IsNullOrWhiteSpace(filter)) node.IsExpanded = true;
        }
        return anyChildVisible;
    }

    partial void OnSelectedNodeChanged(TreeNodeModel? value)
    {
        if (value != null)
        {
            NodeSelected?.Invoke(value);
        }
    }
}
