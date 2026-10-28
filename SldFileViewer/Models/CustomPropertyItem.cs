using CommunityToolkit.Mvvm.ComponentModel;

namespace SldFileViewer.Models;

public partial class CustomPropertyItem : ObservableObject
{
    [ObservableProperty]
    private string _configuration = "Document";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _type = "Text";

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private bool _isEquation;

    [ObservableProperty]
    private bool _isModified;

    [ObservableProperty]
    private bool _isReadOnly;
}
