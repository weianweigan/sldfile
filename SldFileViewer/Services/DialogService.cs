using System.Windows;
using Microsoft.Win32;

namespace SldFileViewer.Services;

public sealed class DialogService : IDialogService
{
    public bool ShowOpenFileDialog(string filter, out string selectedPath)
    {
        var dlg = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
        {
            selectedPath = dlg.FileName;
            return true;
        }
        selectedPath = string.Empty;
        return false;
    }

    public bool ShowSaveFileDialog(string defaultName, string filter, out string selectedPath)
    {
        var dlg = new SaveFileDialog
        {
            FileName = defaultName,
            Filter = filter
        };
        if (dlg.ShowDialog() == true)
        {
            selectedPath = dlg.FileName;
            return true;
        }
        selectedPath = string.Empty;
        return false;
    }

    public void ShowInformation(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
