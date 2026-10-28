using System.Windows;
using System.Windows.Controls;
using SldFileViewer.Models;
using SldFileViewer.ViewModels;

namespace SldFileViewer.Views;

public partial class DocumentTreeView : UserControl
{
    public DocumentTreeView()
    {
        InitializeComponent();
    }

    private DocumentTreeViewModel? VM => DataContext as DocumentTreeViewModel;

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (VM != null && e.NewValue is TreeNodeModel node)
        {
            VM.SelectedNode = node;
        }
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        if (VM != null)
        {
            VM.FilterText = string.Empty;
        }
    }
}
