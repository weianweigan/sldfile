using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class ImagePreviewViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IExportService? _exportService;
    private readonly IDialogService? _dialogService;

    [ObservableProperty]
    private BitmapImage? _imageSource;

    [ObservableProperty]
    private string _imageInfo = string.Empty;

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _isDrawingSheet;

    [ObservableProperty]
    private int _currentSheetIndex;

    public ImagePreviewViewModel(
        ISwDocumentService docService,
        IExportService? exportService = null,
        IDialogService? dialogService = null)
    {
        _docService = docService;
        _exportService = exportService;
        _dialogService = dialogService;
    }

    public void LoadImage(byte[]? imageBytes, string description = "", int sheetIndex = -1)
    {
        CurrentSheetIndex = sheetIndex;
        IsDrawingSheet = (_docService.CurrentFile is SldDrawing) && sheetIndex >= 0;

        if (imageBytes == null || imageBytes.Length == 0)
        {
            ImageSource = null;
            HasImage = false;
            ImageInfo = "无可用预览图像";
            return;
        }

        try
        {
            using var ms = new MemoryStream(imageBytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();

            ImageSource = bmp;
            HasImage = true;
            ImageInfo = $"{description} - 分辨率: {bmp.PixelWidth} x {bmp.PixelHeight} px ({imageBytes.Length / 1024} KB)";
        }
        catch (Exception ex)
        {
            ImageSource = null;
            HasImage = false;
            ImageInfo = $"加载图像失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportSvg()
    {
        if (_exportService == null || _dialogService == null) return;

        if (_docService.CurrentFile is not SldDrawing drawing)
        {
            _dialogService.ShowInformation("导出提示", "当前文档不是工程图，无法导出 SVG。");
            return;
        }

        string defaultName = Path.GetFileNameWithoutExtension(_docService.CurrentFile.FilePath) + $"_Sheet{CurrentSheetIndex + 1}.svg";
        if (_dialogService.ShowSaveFileDialog(defaultName, "SVG 矢量图形 (*.svg)|*.svg", out string savePath))
        {
            try
            {
                _exportService.ExportDrawingToSvg(drawing, savePath, CurrentSheetIndex);
                _dialogService.ShowInformation("导出成功", $"工程图矢量 SVG 已成功导出至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", $"导出 SVG 出现异常: {ex.Message}");
            }
        }
    }
}
