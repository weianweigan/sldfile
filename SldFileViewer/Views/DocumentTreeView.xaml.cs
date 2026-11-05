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

    private void OnTreeViewItemPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void OnNodeContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TreeNodeModel node)
        {
            if (node.Kind != TreeNodeKind.AssemblyComponent || node.Tag is not SldFile.AssemblyComponentNode)
            {
                e.Handled = true; // 仅装配体零件节点显示此上下文菜单
            }
        }
    }

    private void OnIsolateComponentClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is TreeNodeModel node && node.Tag is SldFile.AssemblyComponentNode comp)
        {
            VM?.RequestComponentAction(comp, ComponentTreeAction.Isolate);
        }
    }

    private void OnToggleHideComponentClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is TreeNodeModel node && node.Tag is SldFile.AssemblyComponentNode comp)
        {
            VM?.RequestComponentAction(comp, ComponentTreeAction.ToggleVisibility);
        }
    }

    private void OnZoomToComponentClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is TreeNodeModel node && node.Tag is SldFile.AssemblyComponentNode comp)
        {
            VM?.RequestComponentAction(comp, ComponentTreeAction.ZoomTo);
        }
    }

    private void OnShowAllComponentsClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is TreeNodeModel node && node.Tag is SldFile.AssemblyComponentNode comp)
        {
            VM?.RequestComponentAction(comp, ComponentTreeAction.ShowAll);
        }
    }
}