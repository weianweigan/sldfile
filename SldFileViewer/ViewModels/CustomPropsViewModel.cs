using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ObservableCollection<string> Configurations { get; } = new();
    public ObservableCollection<CustomPropertyItem> Properties { get; } = new();
    public ObservableCollection<CustomPropertyItem> FilteredProperties { get; } = new();

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

        ApplyFilter();
    }

    partial void OnSelectedConfigurationChanged(string value) => ApplyFilter();
    partial void OnFilterTextChanged(string value) => ApplyFilter();

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
