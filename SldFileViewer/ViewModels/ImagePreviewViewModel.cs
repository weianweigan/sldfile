using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SldFile.Drawing.Vector;
using SldFileViewer.Services;
using SldFileViewer.Views.Controls;

namespace SldFileViewer.ViewModels;

public partial class ImagePreviewViewModel : ObservableObject
{
    private readonly ISwDocumentService _docService;
    private readonly IDialogService? _dialogService;

    [ObservableProperty]
    private BitmapImage? _imageSource;

    [ObservableProperty]
    private byte[]? _rawImageBytes;

    [ObservableProperty]
    private string _imageInfo = string.Empty;

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private string _zoomText = "100%";

    [ObservableProperty]
    private int _pixelWidth;

    [ObservableProperty]
    private int _pixelHeight;

    [ObservableProperty]
    private bool _isHighDefinition;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalingMode))]
    [NotifyPropertyChangedFor(nameof(FilterModeText))]
    private bool _isSmoothFilter = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVectorData))]
    [NotifyPropertyChangedFor(nameof(VectorStatusText))]
    private DrawingSheetVectorData? _vectorData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VectorOverlayButtonText))]
    [NotifyPropertyChangedFor(nameof(VectorStatusText))]
    [NotifyPropertyChangedFor(nameof(IsVectorOverlayEnabled))]
    private DrawingOverlayMode _overlayMode = DrawingOverlayMode.SheetNotesOnly;

    public bool IsVectorOverlayEnabled
    {
        get => OverlayMode != DrawingOverlayMode.Off;
        set
        {
            if (value && OverlayMode == DrawingOverlayMode.Off)
            {
                OverlayMode = DrawingOverlayMode.SheetNotesOnly;
            }
            else if (!value)
            {
                OverlayMode = DrawingOverlayMode.Off;
            }
        }
    }

    public bool HasVectorData => VectorData != null && VectorData.HasVectorData;

    public string VectorStatusText
    {
        get
        {
            if (!HasVectorData) return "无矢量数据";
            int sheetCount = VectorData!.Texts.Count(t => t.IsSheetLevelNote);
            return OverlayMode switch
            {
                DrawingOverlayMode.SheetNotesOnly => $"矢量注释 ({sheetCount} 项图纸文字)",
                DrawingOverlayMode.AllAnnotations => $"全图 CAD ({VectorData!.Texts.Count} 图元)",
                _ => "矢量增强: 关"
            };
        }
    }

    public string VectorOverlayButtonText => OverlayMode switch
    {
        DrawingOverlayMode.SheetNotesOnly => "矢量增强: 图纸注释",
        DrawingOverlayMode.AllAnnotations => "矢量增强: 全图 CAD",
        _ => "矢量增强: 关"
    };

    public BitmapScalingMode ScalingMode => IsSmoothFilter ? BitmapScalingMode.Fant : BitmapScalingMode.NearestNeighbor;

    public string FilterModeText => IsSmoothFilter ? "高清抗锯齿 (Fant)" : "锐利像素 (Nearest)";

    public event Action? RequestFitToWindow;
    public event Action? RequestResetTo100;
    public event Action<double>? RequestZoomDelta;

    public ImagePreviewViewModel(ISwDocumentService docService) : this(docService, null)
    {
    }

    public ImagePreviewViewModel(ISwDocumentService docService, IDialogService? dialogService)
    {
        _docService = docService;
        _dialogService = dialogService;
    }

    [RelayCommand]
    public void FitToWindow()
    {
        RequestFitToWindow?.Invoke();
    }

    [RelayCommand]
    public void ResetTo100()
    {
        RequestResetTo100?.Invoke();
    }

    [RelayCommand]
    public void ZoomIn()
    {
        RequestZoomDelta?.Invoke(1.25);
    }

    [RelayCommand]
    public void ZoomOut()
    {
        RequestZoomDelta?.Invoke(0.8);
    }

    [RelayCommand]
    public void ToggleFilterMode()
    {
        IsSmoothFilter = !IsSmoothFilter;
    }

    [RelayCommand]
    public void ToggleVectorOverlay()
    {
        if (OverlayMode == DrawingOverlayMode.Off)
        {
            OverlayMode = DrawingOverlayMode.SheetNotesOnly;
        }
        else
        {
            OverlayMode = DrawingOverlayMode.Off;
        }
    }

    [RelayCommand]
    public void CycleOverlayMode()
    {
        OverlayMode = OverlayMode switch
        {
            DrawingOverlayMode.SheetNotesOnly => DrawingOverlayMode.AllAnnotations,
            DrawingOverlayMode.AllAnnotations => DrawingOverlayMode.Off,
            _ => DrawingOverlayMode.SheetNotesOnly
        };
    }

    [RelayCommand]
    public void SetSheetNotesOnlyMode() => OverlayMode = DrawingOverlayMode.SheetNotesOnly;

    [RelayCommand]
    public void SetAllAnnotationsMode() => OverlayMode = DrawingOverlayMode.AllAnnotations;

    [RelayCommand]
    public void SetOverlayOffMode() => OverlayMode = DrawingOverlayMode.Off;

    [RelayCommand]
    public void ExportImage()
    {
        if (RawImageBytes == null || RawImageBytes.Length == 0 || _dialogService == null)
            return;

        bool isBmp = RawImageBytes.Length >= 2 && RawImageBytes[0] == 0x42 && RawImageBytes[1] == 0x4D;
        string defaultExt = isBmp ? ".bmp" : ".png";
        string filter = isBmp ? "BMP 位图 (*.bmp)|*.bmp|所有文件 (*.*)|*.*" : "PNG 图像 (*.png)|*.png|所有文件 (*.*)|*.*";
        string defaultName = $"Preview_{DateTime.Now:yyyyMMdd_HHmmss}{defaultExt}";

        if (_dialogService.ShowSaveFileDialog(defaultName, filter, out var savePath))
        {
            try
            {
                File.WriteAllBytes(savePath, RawImageBytes);
                _dialogService.ShowInformation("导出成功", $"预览图像已成功保存至:\n{savePath}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("导出失败", $"保存图像失败:\n{ex.Message}");
            }
        }
    }

    public void UpdateZoom(double zoomRatio)
    {
        Zoom = zoomRatio;
        ZoomText = $"{Math.Round(zoomRatio * 100):0}%";
    }

    public void LoadImage(byte[]? imageBytes, string description = "", DrawingSheetVectorData? vectorData = null)
    {
        VectorData = vectorData;

        if (imageBytes == null || imageBytes.Length == 0)
        {
            ImageSource = null;
            RawImageBytes = null;
            HasImage = false;
            PixelWidth = 0;
            PixelHeight = 0;
            IsHighDefinition = false;
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
            RawImageBytes = imageBytes;
            HasImage = true;
            PixelWidth = bmp.PixelWidth;
            PixelHeight = bmp.PixelHeight;
            IsHighDefinition = bmp.PixelWidth >= 1920 || bmp.PixelHeight >= 1080;

            if (VectorData != null && VectorData.HasVectorData)
            {
                VectorData.CalculatePixelCoordinates(bmp.PixelWidth, bmp.PixelHeight);
            }

            string hdTag = IsHighDefinition ? " [高清]" : "";
            string vectorTag = (VectorData != null && VectorData.HasVectorData) ? $" [矢量 CAD: {VectorData.Texts.Count} 图元]" : "";
            ImageInfo = $"{description}{hdTag}{vectorTag} - 分辨率: {bmp.PixelWidth} x {bmp.PixelHeight} px ({(imageBytes.Length + 1023) / 1024} KB)";

            // 图像更新后请求视口自适应
            RequestFitToWindow?.Invoke();
        }
        catch (Exception ex)
        {
            ImageSource = null;
            RawImageBytes = null;
            HasImage = false;
            PixelWidth = 0;
            PixelHeight = 0;
            IsHighDefinition = false;
            ImageInfo = $"加载图像失败: {ex.Message}";
        }
    }
}
