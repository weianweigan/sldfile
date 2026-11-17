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

public class NativeDrawingPreviewTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "TestFiles", "Drawings", "D8001507933", "D8001507933.SLDDRW");

    [Fact]
    public async System.Threading.Tasks.Task DrawingTreeAndPreview_ReuseDecodedSheets()
    {
        using var service = new SwDocumentService();
        await service.OpenAsync(Fixture);
        var sheets = service.GetDrawingSheets();
        Assert.NotEmpty(sheets);
        Assert.Same(sheets, service.GetDrawingSheets());
        service.Close();
        Assert.Empty(service.GetDrawingSheets());
    }

    [Theory]
    [InlineData(false, DrawingOverlayMode.AllAnnotations)]
    [InlineData(true, DrawingOverlayMode.PureVectorCad)]
    public void NativePreview_RendersCurvesFilledGeometryAndTextWithoutABitmap(bool hasRaster, DrawingOverlayMode mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var drawing = new SldDrawing(Fixture);
                var data = drawing.Sheets[0].Preview!;
                Assert.Contains(data.Primitives, p => p.Kind == DrawingPreviewPrimitiveKind.Arc);
                Assert.Contains(data.Primitives, p => p.Kind == DrawingPreviewPrimitiveKind.Triangle);
                var canvas = new DrawingVectorCanvas
                {
                    Width = 1588, Height = 1123, Preview = data,
                    HasRasterImage = hasRaster, OverlayMode = mode
                };
                canvas.Measure(new Size(canvas.Width, canvas.Height));
                canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
                var strokes = GeometryDrawings(VisualTreeHelper.GetDrawing(canvas)).Where(d => d.Pen != null).ToArray();
                Assert.NotEmpty(strokes);
                Assert.All(strokes, stroke =>
                {
                    Assert.Equal(PenLineCap.Round, stroke.Pen.StartLineCap);
                    Assert.Equal(PenLineCap.Round, stroke.Pen.EndLineCap);
                    Assert.Equal(PenLineJoin.Round, stroke.Pen.LineJoin);
                });
                Assert.Contains(strokes, stroke => PathGeometry.CreateFromGeometry(stroke.Geometry).Figures.Any(f =>
                    f.Segments.Count > 1 || f.Segments.OfType<PolyLineSegment>().Any(s => s.Points.Count > 1)));
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
                    // Re-render a detail crop from the vector drawing at 4x,
                    // so joins and sampled curves can be inspected closely.
                    var detail = data.Views[4];
                    double scale = Math.Min(canvas.Width / data.PaperWidth, canvas.Height / data.PaperHeight);
                    double left = (canvas.Width - data.PaperWidth * scale) / 2;
                    double bottom = (canvas.Height + data.PaperHeight * scale) / 2;
                    var crop = new Rect(left + detail.ClipMin.X * scale - 15, bottom - detail.ClipMax.Y * scale - 15,
                        (detail.ClipMax.X - detail.ClipMin.X) * scale + 30, (detail.ClipMax.Y - detail.ClipMin.Y) * scale + 30);
                    var visual = new DrawingVisual();
                    using (var dc = visual.RenderOpen())
                    {
                        dc.PushTransform(new MatrixTransform(4, 0, 0, 4, -crop.X * 4, -crop.Y * 4));
                        dc.DrawDrawing(VisualTreeHelper.GetDrawing(canvas)); dc.Pop();
                    }
                    var zoom = new RenderTargetBitmap((int)Math.Ceiling(crop.Width * 4), (int)Math.Ceiling(crop.Height * 4), 96, 96, PixelFormats.Pbgra32);
                    zoom.Render(visual);
                    var zoomEncoder = new PngBitmapEncoder(); zoomEncoder.Frames.Add(BitmapFrame.Create(zoom));
                    using var zoomFile = File.Create(Path.Combine(dir.FullName, ".artifacts", "drawing-preview-detail.png")); zoomEncoder.Save(zoomFile);
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<GeometryDrawing> GeometryDrawings(Drawing drawing)
    {
        if (drawing is GeometryDrawing geometry) yield return geometry;
        if (drawing is DrawingGroup group)
            foreach (var child in group.Children)
                foreach (var item in GeometryDrawings(child)) yield return item;
    }

    [Fact]
    public void PureVectorPreview_HidesRaster_EvenWhenDecodeIsPartial()
    {
        using var drawing = new SldDrawing(Fixture);
        using var service = new SwDocumentService();
        var vm = new ImagePreviewViewModel(service);
        var data = drawing.Sheets[0].Preview;
        vm.LoadImage(null, "Native", data);
        Assert.True(vm.HasImage);
        Assert.False(vm.HasRasterImage);
        Assert.True(vm.PixelWidth > 0 && vm.PixelHeight > 0);
        Assert.Equal(DrawingOverlayMode.PureVectorCad, vm.OverlayMode);
        vm.LoadImage(drawing.GetSheetPreviewPng(0), "Native", data);
        vm.SetPureVectorCadMode();
        Assert.True(vm.HasRasterImage);
        Assert.False(vm.IsRasterImageVisible);
        Assert.Contains("部分解析", vm.VectorStatusText);
        vm.OverlayMode = DrawingOverlayMode.Off;
        Assert.True(vm.IsRasterImageVisible);
        vm.SetPureVectorCadMode();
        vm.Preview = null;
        Assert.True(vm.IsRasterImageVisible);
    }
}
