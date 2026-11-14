using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Drawing;
using Xunit;
using Xunit.Abstractions;

namespace SldFileViewerTests;

public class MultiSheetInspectionTests
{
    private readonly ITestOutputHelper _output;

    public MultiSheetInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MultiSheet_NB81052_ParsesAllSheetsSeparately()
    {
        string path = @"C:\Users\HP\Desktop\New folder\NB81052_单向滑轮总成-GY-加球标调整.SLDDRW";
        if (!File.Exists(path)) return;

        using var file = SwFile.Open(path);
        var sheets = file.GetDrawingSheets();

        // 1. Assert sheet count
        Assert.Equal(15, sheets.Count);

        // 2. Assert sheet names follow native SheetNames sequence
        Assert.Equal("封面", sheets[0].Name);
        Assert.Equal("流程图", sheets[1].Name);
        Assert.Equal("10_芯轴装入单向轴承", sheets[2].Name);
        Assert.Equal("20_单向轴承组件压入皮带轮", sheets[3].Name);
        Assert.Equal("120_入库", sheets[14].Name);

        // 3. Assert vector data is segmented per sheet, not accumulating all 5385 lines
        for (int i = 0; i < sheets.Count; i++)
        {
            var sheet = sheets[i];
            Assert.NotNull(sheet.VectorData);
            Assert.True(sheet.VectorData.HasLines, $"Sheet {i} ({sheet.Name}) should have vector lines");
            
            // Each individual sheet has around 300-500 lines, not the full 5385 lines
            Assert.True(sheet.VectorData.Lines.Count < 1000, 
                $"Sheet {i} ({sheet.Name}) should have individual sheet lines (< 1000), but had {sheet.VectorData.Lines.Count}");
            Assert.True(sheet.VectorData.Texts.Count < 200, 
                $"Sheet {i} ({sheet.Name}) should have individual sheet texts (< 200), but had {sheet.VectorData.Texts.Count}");
        }

        // 4. Assert Sheet 0 and Sheet 2 line counts
        Assert.InRange(sheets[0].VectorData!.Lines.Count, 300, 600);
        Assert.InRange(sheets[2].VectorData!.Lines.Count, 300, 500);
    }

    [Fact]
    public void MultiSheet_NB81052_RendersCleanVectorSheets()
    {
        string path = @"C:\Users\HP\Desktop\New folder\NB81052_单向滑轮总成-GY-加球标调整.SLDDRW";
        if (!File.Exists(path)) return;

        using var file = SwFile.Open(path);
        var sheets = file.GetDrawingSheets();
        Assert.Equal(15, sheets.Count);

        var s0 = sheets[0];
        var s2 = sheets[2];

        string outDir = @"C:\Users\HP\.gemini\antigravity\brain\eed5a36f-5e8b-4a85-bde9-407779624be2";
        string out0 = Path.Combine(outDir, "nb81052_sheet0_canvas.png");
        string out2 = Path.Combine(outDir, "nb81052_sheet2_canvas.png");

        RenderCanvasToPng(s0, out0);
        RenderCanvasToPng(s2, out2);

        Assert.True(File.Exists(out0));
        Assert.True(File.Exists(out2));
        Assert.True(new FileInfo(out0).Length > 10000);
        Assert.True(new FileInfo(out2).Length > 10000);
    }

    private static void RenderCanvasToPng(DrawingSheetInfo sheet, string outputPath)
    {
        if (sheet.VectorData == null) return;
        int w = 1420;
        int h = 710;
        sheet.VectorData.CalculatePixelCoordinates(w, h);

        var thread = new System.Threading.Thread(() =>
        {
            var canvas = new SldFileViewer.Views.Controls.DrawingVectorCanvas
            {
                Width = w,
                Height = h,
                VectorData = sheet.VectorData,
                OverlayMode = SldFileViewer.Views.Controls.DrawingOverlayMode.PureVectorCad,
                IsOverlayEnabled = true
            };
            canvas.Measure(new System.Windows.Size(w, h));
            canvas.Arrange(new System.Windows.Rect(0, 0, w, h));
            canvas.UpdateLayout();
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(canvas);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var fs = File.Create(outputPath);
            encoder.Save(fs);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
