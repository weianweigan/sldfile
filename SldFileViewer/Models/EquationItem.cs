using CommunityToolkit.Mvvm.ComponentModel;

namespace SldFileViewer.Models;

public partial class EquationItem : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _equation = string.Empty;

    [ObservableProperty]
    private string _evaluatedValue = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _type = "Global Variable"; // "Global Variable", "Dimension", "Feature Suppression"

    [ObservableProperty]
    private string _comments = string.Empty;
}
