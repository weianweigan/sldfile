using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SldFileViewer.Models;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class EquationsViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private int _totalEquations;

    [ObservableProperty]
    private int _globalVariablesCount;

    [ObservableProperty]
    private int _dimensionEquationsCount;

    public ObservableCollection<EquationItem> Equations { get; } = new();
    public ObservableCollection<EquationItem> FilteredEquations { get; } = new();

    public EquationsViewModel(ISwDocumentService docService)
    {
        _docService = docService;
    }

    public void LoadEquations()
    {
        Equations.Clear();
        var items = _docService.GetEquations();
        foreach (var item in items) Equations.Add(item);

        TotalEquations = items.Count;
        GlobalVariablesCount = items.Count(x => x.Type.Contains("Variable", System.StringComparison.OrdinalIgnoreCase));
        DimensionEquationsCount = items.Count(x => x.Type.Contains("Dimension", System.StringComparison.OrdinalIgnoreCase));

        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredEquations.Clear();
        foreach (var eq in Equations)
        {
            if (!string.IsNullOrWhiteSpace(FilterText) &&
                !eq.Name.Contains(FilterText, System.StringComparison.OrdinalIgnoreCase) &&
                !eq.Equation.Contains(FilterText, System.StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredEquations.Add(eq);
        }
    }
}
