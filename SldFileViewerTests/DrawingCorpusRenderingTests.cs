using System;
using System.Collections.Generic;
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

public class DrawingCorpusRenderingTests
{
    public static IEnumerable<object[]> Drawings => Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Drawings"), "*", SearchOption.AllDirectories)
        .Where(p => p.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)).Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Drawings))]
    public void SavedViewContours_AreVisibleInTheViewer(string path)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var drawing = new SldDrawing(path);
                using var service = new SwDocumentService();
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root != null && !File.Exists(Path.Combine(root.FullName, "SldFile.sln"))) root = root.Parent;
                string output = Path.Combine(root!.FullName, "artifacts", "drawing-corpus"); Directory.CreateDirectory(output);
                foreach (var sheet in drawing.Sheets)
                {
                    var preview = sheet.Preview;
                    Assert.NotEmpty(preview.Views);
                    var vm = new ImagePreviewViewModel(service); vm.LoadImage(sheet.BestPreview, sheet.Name, preview);
                    Assert.Equal(DrawingOverlayMode.PureVectorCad, vm.OverlayMode);
                    Assert.False(vm.IsRasterImageVisible);
                    var canvas = new DrawingVectorCanvas { Width = 1600, Height = Math.Ceiling(1600 * preview.PaperHeight / preview.PaperWidth),
                        Preview = preview, HasRasterImage = true, OverlayMode = vm.OverlayMode };
                    canvas.Measure(new Size(canvas.Width, canvas.Height)); canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
                    var bitmap = new RenderTargetBitmap((int)canvas.Width, (int)canvas.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
                    void Save(BitmapSource source, string suffix)
                    {
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
                        using var stream = File.Create(Path.Combine(output, Path.GetFileNameWithoutExtension(path) + $".{sheet.SheetIndex}.{suffix}.png")); encoder.Save(stream);
                    }
                    Save(bitmap, "vector");
                    if (sheet.BestPreview?.Length > 0)
                    {
                        using var data = new MemoryStream(sheet.BestPreview);
                        var image = BitmapDecoder.Create(data, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0]; Save(image, "reference");
                    }
                }
            }
            catch(Exception e) { failure=e; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if(failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
