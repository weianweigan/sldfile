using System;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using SldFileViewer.Services;

namespace SldFileViewer.ViewModels;

public partial class ImagePreviewViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;

    [ObservableProperty]
    private BitmapImage? _imageSource;

    [ObservableProperty]
    private string _imageInfo = string.Empty;

    [ObservableProperty]
    private bool _hasImage;

    public ImagePreviewViewModel(ISwDocumentService docService)
    {
        _docService = docService;
    }

    public void LoadImage(byte[]? imageBytes, string description = "")
    {
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
}
