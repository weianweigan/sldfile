namespace SldFileViewer.Services;

public interface IDialogService
{
    bool ShowOpenFileDialog(string filter, out string selectedPath);
    bool ShowSaveFileDialog(string defaultName, string filter, out string selectedPath);
    void ShowInformation(string title, string message);
    void ShowError(string title, string message);
}
