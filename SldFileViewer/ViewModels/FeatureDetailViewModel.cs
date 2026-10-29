using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SldFile;

namespace SldFileViewer.ViewModels;

public sealed class FeatureDimensionItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class FeaturePropertyItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class FeatureChildItem
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public partial class FeatureDetailViewModel : ObservableObject
{
    [ObservableProperty]
    private string _featureName = string.Empty;

    [ObservableProperty]
    private string _featureType = string.Empty;

    [ObservableProperty]
    private string _featureId = "-";

    [ObservableProperty]
    private string _sourceStream = string.Empty;

    [ObservableProperty]
    private string _status = "Normal";

    [ObservableProperty]
    private bool _hasDimensions;

    [ObservableProperty]
    private bool _hasProperties;

    [ObservableProperty]
    private bool _hasChildren;

    public ObservableCollection<FeatureDimensionItem> Dimensions { get; } = new();
    public ObservableCollection<FeaturePropertyItem> Properties { get; } = new();
    public ObservableCollection<FeatureChildItem> Children { get; } = new();

    public void LoadFeature(Feature feature)
    {
        FeatureName = feature.Name;
        if (!string.IsNullOrEmpty(feature.TypeName) && feature.TypeName.Contains("moRefPlane"))
            FeatureType = "基准面 (moRefPlane)";
        else if (!string.IsNullOrEmpty(feature.TypeName) && feature.TypeName.Contains("moExtrusion"))
            FeatureType = "凸台-拉伸 (moExtrusion)";
        else if (!string.IsNullOrEmpty(feature.TypeName) && feature.TypeName.Contains("moProfileFeature"))
            FeatureType = "草图 (moProfileFeature)";
        else if (!string.IsNullOrEmpty(feature.TypeName))
            FeatureType = feature.TypeName;
        else if (feature.Kind == FeatureKind.ReferencePlane)
            FeatureType = "基准面";
        else
            FeatureType = feature.Kind.ToString();
        FeatureId = feature.Id.HasValue ? feature.Id.Value.ToString() : "-";
        SourceStream = feature.Attributes.TryGetValue("ClassName", out var cls) ? $"ResolvedFeatures ({cls})" : "swXmlContents/KeyWords";
        Status = "已解析 (Resolved)";

        Dimensions.Clear();
        foreach (var d in feature.Dimensions)
        {
            Dimensions.Add(new FeatureDimensionItem
            {
                Name = d.Name,
                Value = d.Value,
                Source = "特征尺寸"
            });
        }
        HasDimensions = Dimensions.Count > 0;

        Properties.Clear();
        Properties.Add(new FeaturePropertyItem { Key = "特征类别 (Kind)", Value = feature.Kind.ToString() });
        Properties.Add(new FeaturePropertyItem { Key = "类型标识 (TypeName)", Value = feature.TypeName });
        if (feature.Id.HasValue)
            Properties.Add(new FeaturePropertyItem { Key = "内部 ID", Value = feature.Id.Value.ToString() });

        foreach (var kvp in feature.Attributes)
        {
            Properties.Add(new FeaturePropertyItem { Key = kvp.Key, Value = kvp.Value });
        }
        HasProperties = Properties.Count > 0;

        Children.Clear();
        foreach (var c in feature.Children)
        {
            Children.Add(new FeatureChildItem
            {
                Name = c.Name,
                Kind = c.Kind.ToString(),
                Details = c.Dimensions.Count > 0 ? $"{c.Dimensions.Count} 个尺寸" : c.TypeName
            });
        }
        HasChildren = Children.Count > 0;
    }

    public void LoadGeneric(string name, string type, string status = "Active", string? details = null)
    {
        FeatureName = name;
        FeatureType = type;
        FeatureId = "-";
        SourceStream = "Model Stream";
        Status = status;

        Dimensions.Clear();
        HasDimensions = false;

        Properties.Clear();
        Properties.Add(new FeaturePropertyItem { Key = "特征类别", Value = type });
        Properties.Add(new FeaturePropertyItem { Key = "状态", Value = status });
        if (!string.IsNullOrEmpty(details))
            Properties.Add(new FeaturePropertyItem { Key = "详细信息", Value = details });
        HasProperties = Properties.Count > 0;

        Children.Clear();
        HasChildren = false;
    }
}
