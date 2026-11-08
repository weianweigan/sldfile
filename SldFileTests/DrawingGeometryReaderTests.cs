using System;
using System.IO;
using System.Linq;
using System.Text;
using SldFile;
using SldFile.Drawing.Vector;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests;

public class DrawingGeometryReaderTests
{
    private readonly ITestOutputHelper _output;

    public DrawingGeometryReaderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ReadSheetGeometry_WithNullStream_GeneratesStandardBordersAndTitleBlockGrids()
    {
        double paperW = 297.0; // A4
        double paperH = 210.0;

        var (lines, views) = DrawingGeometryReader.ReadSheetGeometryAndViews(null, null, paperW, paperH);

        Assert.NotNull(lines);
        Assert.NotEmpty(lines);
        Assert.NotNull(views);

        // Should contain 4 outer borders
        var outerBorders = lines.Where(l => l.Kind == DrawingLineKind.SheetBorderOuter).ToList();
        Assert.Equal(4, outerBorders.Count);

        // Should contain 4 inner drawing frame lines + title block outer lines
        var innerFrames = lines.Where(l => l.Kind == DrawingLineKind.SheetBorderInner).ToList();
        Assert.True(innerFrames.Count >= 6, $"Expected >=6 inner frame lines, got {innerFrames.Count}");

        // Inner frame binding edge on left (X=25mm)
        Assert.Contains(innerFrames, l => Math.Abs(l.X1 - 25.0) < 1e-3 && Math.Abs(l.X2 - 25.0) < 1e-3);
    }

    [Fact]
    public void ReadSheetGeometry_WithSynthesizedVBLists_ExtractsFloatAndDoubleSegments()
    {
        double paperW = 297.0;
        double paperH = 210.0;

        var ms = new MemoryStream();
        // Bucket count = 1
        ms.Write(BitConverter.GetBytes(1), 0, 4);

        // Synthesize float segments: (0.1m, 0.15m, 0) -> (0.2m, 0.15m, 0)
        ms.Write(BitConverter.GetBytes(0.100f), 0, 4);
        ms.Write(BitConverter.GetBytes(0.150f), 0, 4);
        ms.Write(BitConverter.GetBytes(0.000f), 0, 4);
        ms.Write(BitConverter.GetBytes(0.200f), 0, 4);
        ms.Write(BitConverter.GetBytes(0.150f), 0, 4);
        ms.Write(BitConverter.GetBytes(0.000f), 0, 4);

        // Padding
        ms.Write(new byte[128]);

        byte[] streamData = ms.ToArray();
        var lines = DrawingGeometryReader.ReadSheetGeometry(streamData, null, paperW, paperH);

        Assert.NotNull(lines);
        var viewLines = lines.Where(l => l.Kind == DrawingLineKind.SolidOutline || l.Kind == DrawingLineKind.ThinLine).ToList();
        Assert.NotEmpty(viewLines);

        var segment = viewLines.First();
        Assert.True(segment.LengthMm > 0.1);
        Assert.True(segment.X1 >= 2.0 && segment.X2 <= paperW - 2.0);
        Assert.True(segment.Y1 >= 2.0 && segment.Y2 <= paperH - 2.0);
    }

    [Fact]
    public void DrawingSheetVectorData_CalculatePixelCoordinates_MapsLineCoordinatesCorrectly()
    {
        var data = new DrawingSheetVectorData
        {
            PaperWidthMm = 297.0,
            PaperHeightMm = 210.0
        };

        var line = new DrawingVectorLine(25.0, 5.0, 292.0, 5.0, DrawingLineKind.SheetBorderInner, 0.5);
        data.Lines = new[] { line };

        // Reference 1420x710 viewport
        data.CalculatePixelCoordinates(1420, 710);

        Assert.True(line.PixelX1 > 200, $"Expected PixelX1 > 200, got {line.PixelX1}");
        Assert.True(line.PixelX2 > line.PixelX1, "Expected PixelX2 > PixelX1");
        Assert.True(line.PixelY1 > 600, $"Expected PixelY1 > 600, got {line.PixelY1}");
        Assert.Equal(line.PixelY1, line.PixelY2);
    }

    [Fact]
    public void ReadSheetGeometryAndViews_WithSynthesizedDefinitionViews_AlignsGeometryAccurately()
    {
        double paperW = 420.0; // A3
        double paperH = 297.0;

        // 1. Synthesize Definition stream with a view bounding box:
        // (Xmin=0.100m, Ymin=0.120m, Xmax=0.300m, Ymax=0.180m) => Span 200mm x 60mm centered at (200mm, 150mm)
        var defMs = new MemoryStream();
        defMs.Write(new byte[64]); // leading padding
        defMs.Write(BitConverter.GetBytes(0.100), 0, 8);
        defMs.Write(BitConverter.GetBytes(0.120), 0, 8);
        defMs.Write(BitConverter.GetBytes(0.300), 0, 8);
        defMs.Write(BitConverter.GetBytes(0.180), 0, 8);
        defMs.Write(new byte[64]); // trailing padding
        byte[] defBytes = defMs.ToArray();

        // 2. Synthesize VBLists stream with local model segments centered at 0:
        // (-1.0m, -0.2m, 0) -> (1.0m, 0.2m, 0)
        var vbMs = new MemoryStream();
        vbMs.Write(BitConverter.GetBytes(1), 0, 4); // bucket count = 1
        vbMs.Write(BitConverter.GetBytes(-1.0f), 0, 4);
        vbMs.Write(BitConverter.GetBytes(-0.2f), 0, 4);
        vbMs.Write(BitConverter.GetBytes(0.0f), 0, 4);
        vbMs.Write(BitConverter.GetBytes(1.0f), 0, 4);
        vbMs.Write(BitConverter.GetBytes(0.2f), 0, 4);
        vbMs.Write(BitConverter.GetBytes(0.0f), 0, 4);
        vbMs.Write(new byte[64]);
        byte[] vbBytes = vbMs.ToArray();

        var (lines, views) = DrawingGeometryReader.ReadSheetGeometryAndViews(vbBytes, defBytes, paperW, paperH);

        Assert.NotNull(views);
        Assert.Single(views);
        var view = views[0];
        Assert.Equal(200.0, Math.Round(view.CenterXMm, 1));
        Assert.Equal(150.0, Math.Round(view.CenterYMm, 1));
        Assert.Equal(200.0, Math.Round(view.WidthMm, 1));
        Assert.Equal(60.0, Math.Round(view.HeightMm, 1));

        var modelLines = lines.Where(l => l.Kind == DrawingLineKind.SolidOutline || l.Kind == DrawingLineKind.ThinLine).ToList();
        Assert.NotEmpty(modelLines);
        var ml = modelLines[0];

        // Should be centered around (200mm, 150mm) on the sheet
        double midX = (ml.X1 + ml.X2) / 2.0;
        double midY = (ml.Y1 + ml.Y2) / 2.0;
        Assert.True(Math.Abs(midX - 200.0) < 5.0, $"Expected midX ~ 200mm, got {midX}");
        Assert.True(Math.Abs(midY - 150.0) < 5.0, $"Expected midY ~ 150mm, got {midY}");
    }

    [Fact]
    public void ReadSheetGeometryAndViews_WithRealUserDrawing_ExtractsCadLinesAndViewsAccurately()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath))
        {
            _output.WriteLine("Real file not found, skipping.");
            return;
        }

        using var swFile = SwFile.Open(realPath, readOnly: true);
        _output.WriteLine("=== ALL RAW STREAMS IN FILE ===");
        foreach (var kvp in swFile.RawStreams)
        {
            _output.WriteLine($"Stream: '{kvp.Key}', Size: {kvp.Value.Length} bytes");
        }
        _output.WriteLine("=== ALL ZLB STREAMS IN FILE ===");
        foreach (var kvp in swFile.ZlbContents)
        {
            _output.WriteLine($"Zlb: '{kvp.Key}', Size: {kvp.Value.DecompressedSize} bytes");
        }

        swFile.RawStreams.TryGetValue("Contents/DisplayLists", out var dlBytes);
        Assert.NotNull(dlBytes);
        Assert.True(dlBytes.Length > 1000);

        var directLines = DrawingGeometryReader.ExtractLinesFromDisplayLists(dlBytes, 420.0, 297.0);
        Assert.True(directLines.Count >= 300, $"Expected >=300 lines from DisplayLists, got {directLines.Count}");
        Assert.True(directLines.Any(l => l.Kind == DrawingLineKind.SolidOutline));
        Assert.True(directLines.Any(l => l.Kind == DrawingLineKind.Centerline));
        _output.WriteLine($"Extracted {directLines.Count} direct CAD lines from DisplayLists (SolidOutline: {directLines.Count(l => l.Kind == DrawingLineKind.SolidOutline)}, Centerlines: {directLines.Count(l => l.Kind == DrawingLineKind.Centerline)})");

        var sheets = swFile.GetDrawingSheets();

        Assert.NotNull(sheets);
        Assert.NotEmpty(sheets);
        var sheet0 = sheets[0];

        Assert.NotNull(sheet0.VectorData);
        var vData = sheet0.VectorData!;

        Assert.True(vData.HasLines, "VectorData should have CAD lines");
        Assert.True(vData.Lines.Count > 50, $"Expected > 50 CAD lines, got {vData.Lines.Count}");
        Assert.NotNull(vData.Views);
        Assert.NotEmpty(vData.Views);

        _output.WriteLine($"Drawing {sheet0.Name}: {vData.Lines.Count} lines, {vData.Views.Count} views, {vData.Texts.Count} texts");
        foreach (var v in vData.Views)
        {
            _output.WriteLine($"  {v}");
        }

        // Verify physical bounds are strictly inside A3 paper: 420mm x 297mm
        double minX = vData.Lines.Min(l => Math.Min(l.X1, l.X2));
        double maxX = vData.Lines.Max(l => Math.Max(l.X1, l.X2));
        double minY = vData.Lines.Min(l => Math.Min(l.Y1, l.Y2));
        double maxY = vData.Lines.Max(l => Math.Max(l.Y1, l.Y2));

        _output.WriteLine($"Lines span: X=[{minX:F1}..{maxX:F1}] mm, Y=[{minY:F1}..{maxY:F1}] mm");

        Assert.True(minX >= 0.0 && maxX <= 420.0 + 1e-2, $"X range out of bounds: [{minX}, {maxX}]");
        Assert.True(minY >= 0.0 && maxY <= 297.0 + 1e-2, $"Y range out of bounds: [{minY}, {maxY}]");
    }

    [Fact]
    public void OpenPart_D8001507933_SLDPRT_ReproduceIssue()
    {
        string partPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDPRT";
        if (!File.Exists(partPath)) return;

        using var swFile = SwFile.Open(partPath);
        _output.WriteLine($"Opened Part: Kind={swFile.Kind}, Configs={swFile.Configurations.Count}");
        
        var features = swFile.Features;
        _output.WriteLine($"Features count: {features.Count}");

        for (int i = 0; i < swFile.Configurations.Count; i++)
        {
            var sketches = swFile.GetSketches(i);
            _output.WriteLine($"Config {i} sketches count: {sketches.Count}");
        }
    }
}
