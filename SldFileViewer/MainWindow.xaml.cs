using System.Windows;
using SldFileViewer.ViewModels;

namespace SldFileViewer;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
