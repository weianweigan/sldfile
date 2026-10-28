using CommunityToolkit.Mvvm.ComponentModel;

namespace SldFileViewer.Models;

public partial class BomDisplayItem : ObservableObject
{
    [ObservableProperty]
    private int _itemNumber;

    [ObservableProperty]
    private string _componentName = string.Empty;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private string _configuration = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _isVirtual;

    [ObservableProperty]
    private string _description = string.Empty;
}
