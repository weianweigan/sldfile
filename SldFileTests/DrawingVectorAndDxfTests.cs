using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Drawing;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class DrawingVectorAndDxfTests
{
    [Fact]
    public void SheetFormatInfo_ParsesSwAllSheetFormatData_StrippingLeadingGreaterThan()
    {
        const string raw = ">图纸1@8.000000@0.420000@0.297000@1.000000@14.500000@1.000000@c:\\templates\\gb_a3.slddrt@12.000000@12.000000@@";
        var all = DrawingSheetFormatInfo.ParseAll(raw);
        var info = Assert.Single(all);

        Assert.Equal("图纸1", info.SheetName);
        Assert.Equal(1.0, info.ScaleNumerator);
        Assert.Equal(8.0, info.ScaleDenominator);
        Assert.Equal(420.0, info.PaperWidthMm, 3);
        Assert.Equal(297.0, info.PaperHeightMm, 3);
        Assert.Equal("c:\\templates\\gb_a3.slddrt", info.TemplatePath);
        Assert.Equal(12.0, info.FrameMarginHorizontalMm, 3);
        Assert.Equal(12.0, info.FrameMarginVerticalMm, 3);
        Assert.Equal("A3", DrawingSheetFormatInfo.InferPaperSizeName(info.PaperWidthMm, info.PaperHeightMm, info.TemplatePath));
    }

    [Fact]
    public void RealDrawings_PopulateSheetFormatAndAccuratePaperDimensions()
    {
        using var d800 = new SldDrawing(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        var sheet = Assert.Single(d800.Sheets);
        Assert.NotNull(sheet.Preview);
        Assert.True(sheet.Preview!.PaperWidth >= 0.210);
        Assert.True(sheet.Preview.PaperHeight >= 0.210);
    }

    [Fact]
    public void ViewCacheArchive_ExtractNativeFragments_PopulatesPolylinesFromNativeVbArchive()
    {
        using var drawing = new SldDrawing(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        byte[] vbBytes = drawing.GetDrawingPreviewStreamBytes("Contents/VBLists")!;
        var cache = new DrawingViewCacheArchive
        {
            IsDefinition = false,
            PopulateNativeFragments = true,
            ViewNames = drawing.Sheets[0].Views.Select(v => v.Name).ToArray()
        };
        using (var ar = MfcArchive.CreateReader(vbBytes))
        {
            ar.DocumentVersion = drawing.GetNativeDrawingDocumentVersion();
            cache.Serialize(ar);
        }

        Assert.NotNull(cache.NativeBuckets);
        Assert.NotEmpty(cache.Fragments);
        Assert.All(cache.Fragments, f =>
        {
            var poly = Assert.IsType<NativeDrawingBucketPolyModel>(f.Model);
            Assert.True(poly.GetPoints().Length >= 2);
            Assert.Contains(cache.NativeBuckets!.Views.OfType<NativeDrawingObjectGraph.Node>()
                .Select(v => (NativeDrawingViewBucketModel)v.Payload).SelectMany(v => v.Bucket.PolygonSets.SelectMany(s => s)),
                native => ReferenceEquals(poly, native));
        });
    }

    [Theory]
    [InlineData("Drawings", "D8001507933/D8001507933.SLDDRW", true)]
    [InlineData("Solidworks2012", "cube.SLDDRW", false)]
    [InlineData("Solidworks2026", "cube.SLDDRW", true)]
    public void GetViewBuckets_ExtractsStructured3DAnd2DPolylines(string folder, string file, bool expectNonEmptyPolylines)
    {
        using var drawing = new SldDrawing(TestFileProvider.Resolve(folder, file));
        var buckets = drawing.GetViewBuckets();
        Assert.NotNull(buckets);
        Assert.NotEmpty(buckets);
        Assert.All(buckets, b =>
        {
            Assert.True(b.Direction.Length > 0.01);
            Assert.True(b.HorizontalBasis.Length > 0.01);
            Assert.True(b.VerticalBasis.Length > 0.01);
        });
        if (expectNonEmptyPolylines)
        {
            Assert.Contains(buckets, b => b.Polylines.Count > 0 && b.Lines.Count > 0 && b.Polylines3D.Count > 0);
        }
    }

    [Fact]
    public void PreBitfieldBucketPoly_Version14Layout_RoundtripsEveryByte()
    {
        var poly = new NativeDrawingBucketPolyModel
        {
            TwoDimensional = false,
            ComponentId = 1,
            EntityId = 42,
            Flags = 0x01,
            PointCount = 2,
            Coordinates = new[] { 0.0, 0.0, 0.0, 0.1, 0.2, 0.3 },
            HasLineVisual = 0,
            ParameterType = 0,
            PreBitfieldWords = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            PreBitfieldArcFlag = 1,
            ArcStart = new DrawingPreviewPoint(0.0, 0.0, 0.0),
            ArcEnd = new DrawingPreviewPoint(0.1, 0.0, 0.0),
            ArcCenter = new DrawingPreviewPoint(0.05, 0.0, 0.0),
            ArcState68 = 1,
            ArcState6C = 2,
            ArcState70 = 3,
            ArcState74 = 4,
            ArcState78 = 5,
            PreBitfieldTailWord = 99,
            State8C = 10,
            State90 = 20,
            State94 = 30,
            StateA0 = 40,
            ScalarA8 = 0.5,
            ScalarB0 = 1.5
        };

        using var writer = MfcArchive.CreateWriter();
        writer.DocumentVersion = 0x3800; // Mid-version between 5000 and 0x41A1
        poly.Serialize(writer);
        byte[] bytes = writer.ToArray();

        using var reader = MfcArchive.CreateReader(bytes);
        reader.DocumentVersion = 0x3800;
        var loaded = new NativeDrawingBucketPolyModel { TwoDimensional = false };
        loaded.Serialize(reader);

        Assert.Equal(0, reader.Remaining);
        Assert.Equal(2, loaded.PointCount);
        Assert.True(loaded.HasArcData);
        Assert.Equal(0.05, loaded.ArcCenter.X, 6);
        Assert.Equal(99, loaded.PreBitfieldTailWord);

        using var rewriter = MfcArchive.CreateWriter();
        rewriter.DocumentVersion = 0x3800;
        loaded.Serialize(rewriter);
        Assert.Equal(bytes, rewriter.ToArray());
    }

    [Fact]
    public void ExportSheetDxf_GeneratesValidAc1009Dxf_AndRoundtripsThroughReadDxfSummary()
    {
        using var drawing = new SldDrawing(TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW"));
        string dxf = drawing.ExportSheetDxf(0);

        Assert.Contains("$ACADVER\n  1\nAC1009", dxf);
        Assert.Contains("SECTION\n  2\nENTITIES", dxf);

        var summary = DrawingPreviewDxfWriter.ReadDxfSummary(dxf);
        Assert.True(summary.LineCount > 700, $"Expected > 700 lines, got {summary.LineCount}");
        Assert.True(summary.ArcCount + summary.CircleCount >= 30, $"Expected >= 30 arcs/circles, got {summary.ArcCount + summary.CircleCount}");
        Assert.True(summary.TextCount >= 100, $"Expected >= 100 texts, got {summary.TextCount}");
        Assert.Contains("FRAME", summary.Layers);
        Assert.Contains("VIEW_VISIBLE", summary.Layers);
        Assert.Contains("TEXT", summary.Layers);
    }

    [Fact]
    public void KasaCircleArcFitter_RecoversExactCircleAndArcFromSampledPolyline()
    {
        var circlePts = Enumerable.Range(0, 25)
            .Select(i =>
            {
                double a = 2.0 * Math.PI * i / 24.0;
                return (100.0 + 25.0 * Math.Cos(a), 80.0 + 25.0 * Math.Sin(a));
            })
            .ToList();

        Assert.True(DrawingPreviewDxfWriter.TryFitCircleOrArc(circlePts, out var circle));
        Assert.NotNull(circle);
        Assert.True(circle!.IsFullCircle);
        Assert.Equal(100.0, circle.CenterXMm, 3);
        Assert.Equal(80.0, circle.CenterYMm, 3);
        Assert.Equal(25.0, circle.RadiusMm, 3);

        var arcPts = Enumerable.Range(0, 12)
            .Select(i =>
            {
                double a = (Math.PI * 0.5) * i / 11.0;
                return (50.0 + 15.0 * Math.Cos(a), 40.0 + 15.0 * Math.Sin(a));
            })
            .ToList();

        Assert.True(DrawingPreviewDxfWriter.TryFitCircleOrArc(arcPts, out var arc));
        Assert.NotNull(arc);
        Assert.False(arc!.IsFullCircle);
        Assert.Equal(50.0, arc.CenterXMm, 3);
        Assert.Equal(40.0, arc.CenterYMm, 3);
        Assert.Equal(15.0, arc.RadiusMm, 3);
        Assert.Equal(0.0, arc.StartAngleDeg, 2);
        Assert.Equal(90.0, arc.EndAngleDeg, 2);
    }

}
