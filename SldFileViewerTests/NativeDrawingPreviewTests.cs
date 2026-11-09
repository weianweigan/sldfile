using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SldFile;
using SldFile.Drawing;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;
using SldFileViewer.Views.Controls;
using Xunit;

namespace SldFileViewerTests;

public class NativeDrawingPreviewTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestFiles", "Drawings", "D8001507933", "D8001507933.SLDDRW");

    [Fact]
    public void NativePreview_RendersCurvesFilledGeometryAndTextWithoutABitmap()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var drawing = new SldDrawing(Fixture);
                var data = drawing.Sheets[0].VectorData!;
                Assert.Contains(data.Preview!.Primitives, p => p.Kind == DrawingPreviewPrimitiveKind.Arc);
                Assert.Contains(data.Preview.Primitives, p => p.Kind == DrawingPreviewPrimitiveKind.Triangle);
                var canvas = new DrawingVectorCanvas
                {
                    Width = 1588, Height = 1123, VectorData = data,
                    HasRasterImage = false, OverlayMode = DrawingOverlayMode.AllAnnotations
                };
                canvas.Measure(new Size(canvas.Width, canvas.Height));
                canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
                var bitmap = new RenderTargetBitmap(1588, 1123, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(canvas);
                var pixels = new byte[1588 * 1123 * 4]; bitmap.CopyPixels(pixels, 1588 * 4, 0);
                int ink = Enumerable.Range(0, pixels.Length / 4).Count(i => pixels[i * 4] < 180 && pixels[i * 4 + 3] > 0);
                Assert.True(ink > 10000, $"Expected native geometry and text; got only {ink} ink pixels.");
                // This artifact permits visual verification of the actual renderer used by SldFileViewer.
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SldFile.sln"))) dir = dir.Parent;
                if (dir != null)
                {
                    string output = Path.Combine(dir.FullName, ".artifacts", "drawing-preview-native.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(output); encoder.Save(file);
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void VectorOnlyPreview_IsVisible_AndPartialDecodeKeepsExistingRaster()
    {
        using var drawing = new SldDrawing(Fixture);
        using var service = new SwDocumentService();
        var vm = new ImagePreviewViewModel(service);
        var data = drawing.Sheets[0].VectorData;
        vm.LoadImage(null, "Native", data);
        Assert.True(vm.HasImage);
        Assert.False(vm.HasRasterImage);
        Assert.True(vm.PixelWidth > 0 && vm.PixelHeight > 0);
        Assert.Equal(DrawingOverlayMode.AllAnnotations, vm.OverlayMode);
        vm.LoadImage(drawing.GetSheetPreviewPng(0), "Native", data);
        vm.SetPureVectorCadMode();
        Assert.True(vm.HasRasterImage);
        Assert.True(vm.IsRasterImageVisible);
        Assert.Contains("部分解析", vm.VectorStatusText);
    }
}
