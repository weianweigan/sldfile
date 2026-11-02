using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFileViewer.Models;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class BomViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private int _totalQuantity;

    [ObservableProperty]
    private int _virtualCount;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string? _currentConfiguration;

    public ObservableCollection<BomDisplayItem> Items { get; } = new();
    public ObservableCollection<BomDisplayItem> FilteredItems { get; } = new();

    public BomViewModel(ISwDocumentService docService, IExportService exportService, IDialogService dialogService)
    {
        _docService = docService;
        _exportService = exportService;
        _dialogService = dialogService;
    }

    /// <summary>
    /// 加载并计算指定配置（或默认活动配置）下的装配体 BOM 物料明细
    /// </summary>
    public void LoadBom(string? configName = null)
    {
        CurrentConfiguration = configName;
        Items.Clear();
        var rawBom = _docService.GetBom(configName);
        int itemNumber = 1;

        foreach (var bom in rawBom)
        {
            Items.Add(new BomDisplayItem
            {
                ItemNumber = itemNumber++,
                ComponentName = bom.PartNumber,
                Quantity = bom.Quantity,
                Configuration = bom.ConfigurationName,
                FilePath = bom.FilePath,
                IsVirtual = bom.IsVirtual,
                Description = bom.IsVirtual ? "[虚拟零部件]" : (bom.Description.Length > 0 ? bom.Description : (System.IO.Path.GetFileName(bom.FilePath) ?? string.Empty))
            });
        }

        TotalItems = Items.Count;
        TotalQuantity = Items.Sum(x => x.Quantity);
        VirtualCount = Items.Count(x => x.IsVirtual);

        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        foreach (var item in Items)
        {
            if (!string.IsNullOrWhiteSpace(FilterText) &&
                !item.ComponentName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) &&
                !item.FilePath.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredItems.Add(item);
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (Items.Count == 0)
        {
            _dialogService.ShowInformation("导出提示", "当前没有可导出的 BOM 数据。");
            return;
        }

        if (_dialogService.ShowSaveFileDialog("BOM.csv", "CSV 文件 (*.csv)|*.csv", out var savePath))
        {
            try
            {
                _exportService.ExportBomToCsv(Items, savePath);
                _dialogService.ShowInformation("导出成功", $"BOM 已成功导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", $"导出 CSV 时发生错误: {ex.Message}");
            }
        }
    }
}
