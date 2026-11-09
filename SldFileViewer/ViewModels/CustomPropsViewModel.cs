using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFileViewer.Models;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class CustomPropsViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _selectedConfiguration = "All";

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _hasMassProperties;

    [ObservableProperty]
    private string _massPropertiesSummary = string.Empty;

    [ObservableProperty]
    private bool _hasCutList;

    public ObservableCollection<string> Configurations { get; } = new();
    public ObservableCollection<CustomPropertyItem> Properties { get; } = new();
    public ObservableCollection<CustomPropertyItem> FilteredProperties { get; } = new();
    public ObservableCollection<CutListItem> CutListItems { get; } = new();

    public CustomPropsViewModel(ISwDocumentService docService, IDialogService dialogService)
    {
        _docService = docService;
        _dialogService = dialogService;
    }

    public void LoadProperties()
    {
        Configurations.Clear();
        Properties.Clear();
        HasUnsavedChanges = false;

        Configurations.Add("All");
        Configurations.Add("Document");
        foreach (var c in _docService.GetConfigurations())
        {
            if (!string.IsNullOrEmpty(c.Name))
                Configurations.Add(c.Name);
        }
        SelectedConfiguration = "All";

        var list = _docService.GetCustomProperties();
        foreach (var item in list)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CustomPropertyItem.Value))
                {
                    item.IsModified = true;
                    HasUnsavedChanges = true;
                }
            };
            Properties.Add(item);
        }

        UpdateMassPropertiesAndCutList();
        ApplyFilter();
    }

    partial void OnSelectedConfigurationChanged(string value)
    {
        UpdateMassPropertiesAndCutList();
        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void UpdateMassPropertiesAndCutList()
    {
        HasMassProperties = false;
        MassPropertiesSummary = string.Empty;
        HasCutList = false;
        CutListItems.Clear();

        var configs = _docService.GetConfigurations();
        var cfg = configs.FirstOrDefault(c => string.Equals(c.Name, SelectedConfiguration, StringComparison.OrdinalIgnoreCase))
               ?? configs.FirstOrDefault();

        if (cfg?.MassProperties != null)
        {
            var mp = cfg.MassProperties;
            HasMassProperties = true;
            MassPropertiesSummary = $"质量: {mp.Mass:F4} kg  |  体积: {mp.Volume * 1e6:F2} cm³  |  表面积: {mp.SurfaceArea * 1e4:F2} cm²\n" +
                                   $"质心 (X, Y, Z): ({mp.CenterOfGravity.X:F4}, {mp.CenterOfGravity.Y:F4}, {mp.CenterOfGravity.Z:F4}) m\n" +
                                   $"主惯性矩 (Ixx, Iyy, Izz): ({mp.MomentsOfInertia.X:E3}, {mp.MomentsOfInertia.Y:E3}, {mp.MomentsOfInertia.Z:E3}) kg·m²";
        }

        if (cfg?.CutListItems != null && cfg.CutListItems.Count > 0)
        {
            HasCutList = true;
            foreach (var item in cfg.CutListItems)
            {
                CutListItems.Add(item);
            }
        }
    }

    private void ApplyFilter()
    {
        FilteredProperties.Clear();
        foreach (var p in Properties)
        {
            if (SelectedConfiguration != "All" &&
                !string.Equals(p.Configuration, SelectedConfiguration, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(FilterText) &&
                !p.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) &&
                !p.Value.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredProperties.Add(p);
        }
    }

    [RelayCommand]
    private void SaveProperties()
    {
        try
        {
            _docService.SaveCustomProperties(Properties);
            foreach (var p in Properties) p.IsModified = false;
            HasUnsavedChanges = false;
            _dialogService.ShowInformation("保存成功", "自定义属性已成功保存至 SolidWorks 文件！");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("保存失败", $"保存自定义属性时发生错误: {ex.Message}");
        }
    }
}
