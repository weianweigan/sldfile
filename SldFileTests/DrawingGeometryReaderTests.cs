using System;
using System.IO;
using System.Linq;
using SldFile.Drawing.Vector;
using Xunit;

namespace SldFileTests;

public class DrawingGeometryReaderTests
{
    [Fact]
    public void ReadSheetGeometry_WithNullStream_GeneratesStandardBordersAndTitleBlockGrids()
    {
        double paperW = 297.0; // A4
        double paperH = 210.0;

        var lines = DrawingGeometryReader.ReadSheetGeometry(null, null, paperW, paperH);

        Assert.NotNull(lines);
        Assert.NotEmpty(lines);

        // Should contain 4 outer borders
        var outerBorders = lines.Where(l => l.Kind == DrawingLineKind.SheetBorderOuter).ToList();
        Assert.Equal(4, outerBorders.Count);

        // Should contain 4 inner drawing frame lines + title block outer lines
        var innerFrames = lines.Where(l => l.Kind == DrawingLineKind.SheetBorderInner).ToList();
        Assert.True(innerFrames.Count >= 6, $"Expected >=6 inner frame lines, got {innerFrames.Count}");

        // Should contain table grid lines
        var gridLines = lines.Where(l => l.Kind == DrawingLineKind.TableGrid).ToList();
        Assert.NotEmpty(gridLines);

        // Inner frame binding edge on left (X=25mm)
        Assert.Contains(innerFrames, l => Math.Abs(l.X1 - 25.0) < 1e-3 && Math.Abs(l.X2 - 25.0) < 1e-3);
    }

    [Fact]
    public void ReadSheetGeometry_WithSynthesizedVBLists_ExtractsFloatAndDoubleSegments()
    {
        double paperW = 297.0;
        double paperH = 210.0;

        var ms = new MemoryStream();
        // Synthesize a float segment: (100mm, 150mm, 0) -> (200mm, 150mm, 0) => (0.1m, 0.15m, 0) -> (0.2m, 0.15m, 0)
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

        var segment = viewLines.First(l => Math.Abs(l.Y1 - 150.0) < 0.1);
        Assert.Equal(100.0, Math.Round(segment.X1, 0));
        Assert.Equal(200.0, Math.Round(segment.X2, 0));
        Assert.Equal(100.0, Math.Round(segment.LengthMm, 0));
        Assert.True(segment.IsHorizontal);
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
}
