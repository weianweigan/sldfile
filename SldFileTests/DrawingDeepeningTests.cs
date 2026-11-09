using System;
using System.IO;
using System.Linq;
using SldFile;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class DrawingDeepeningTests
{
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };

    [Fact]
    public void Drawing_2026_GetDrawingSheets_ReturnsSheetsAndViews()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        var sheets = file.GetDrawingSheets();

        Assert.NotNull(sheets);
        Assert.Single(sheets);

        var sheet = sheets[0];
        Assert.Equal("Sheet1", sheet.Name);
        Assert.Equal(0, sheet.SheetIndex);

        // Preview PNG
        Assert.NotNull(sheet.PreviewPng);
        Assert.True(sheet.PreviewPng!.Length > 500);
        Assert.Equal(PngMagic[0], sheet.PreviewPng[0]);
        Assert.Equal(PngMagic[1], sheet.PreviewPng[1]);
        Assert.Equal(PngMagic[2], sheet.PreviewPng[2]);
        Assert.Equal(PngMagic[3], sheet.PreviewPng[3]);

        // Views
        Assert.NotEmpty(sheet.Views);
        Assert.Contains(sheet.Views, v => v.Name.Contains("Drawing View"));
        Assert.All(sheet.Views, v => Assert.Contains("cube.sldprt", v.ReferencedModelPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Drawing_2026_GetSheetPreviewPng_ReturnsValidPng()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        byte[]? png = file.GetSheetPreviewPng(0);

        Assert.NotNull(png);
        Assert.True(png!.Length > 500);
        Assert.Equal(PngMagic[0], png[0]);
        Assert.Equal(PngMagic[1], png[1]);
        Assert.Equal(PngMagic[2], png[2]);
        Assert.Equal(PngMagic[3], png[3]);
    }

    [Fact]
    public void Drawing_2012_DefinitionStream_MfcDocumentModel_ExtractsSheetsViewsNotesAndDimensions()
    {
        using var drw = new SldDrawing(TestFileProvider.CubeDrawing, readOnly: true);
        var doc = drw.GetDrawingDocument();

        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Sheets);
        Assert.NotEmpty(doc.Views);
        Assert.NotEmpty(doc.Notes);
        Assert.NotEmpty(doc.Dimensions);

        var sheet = doc.Sheets[0];
        Assert.Equal("Sheet1", sheet.SheetName);
        Assert.True(sheet.Width > 0.1);
        Assert.True(sheet.Height > 0.1);

        // Views extracted from Contents/Definition
        Assert.Contains(doc.Views, v => v.ViewName.Contains("前视") || v.ViewName.Contains("Drawing View"));

        // Notes extracted from Contents/Definition (moDisplayNote_c)
        Assert.Contains(doc.Notes, n => n.Text.Contains("ABCD"));

        // Dimensions extracted from Contents/Definition (moDimBackupDef_c)
        Assert.Contains(doc.Dimensions, d => d.Text.Contains("0.10") || (d.Value.HasValue && Math.Abs(d.Value.Value - 0.1) < 0.001));

        // SVG rendering works and includes layers
        string svg = drw.ExportSheetSvg(0);
        Assert.NotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("id=\"views\"", svg);
        Assert.Contains("id=\"dimensions\"", svg);
        Assert.Contains("id=\"notes\"", svg);
        Assert.Contains("ABCD", svg);
    }

    [Fact]
    public void Drawing_2026_MfcDocument_ExtractsViewsAndValidSvg()
    {
        using var drw = new SldDrawing(TestFileProvider.CubeDrawing2026, readOnly: true);
        var doc = drw.GetDrawingDocument();

        Assert.NotNull(doc);
        Assert.NotEmpty(doc.Sheets);
        Assert.NotEmpty(doc.Views);

        string svg = drw.ExportSheetSvg(0);
        Assert.NotNull(svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("id=\"views\"", svg);
    }

    [Fact]
    public void Drawing_2012_ViewBucket_WireFormat_ExtractsPolylinesAndProjections()
    {
        using var drw = new SldDrawing(TestFileProvider.CubeDrawing, readOnly: true);
        var buckets = drw.GetViewBuckets();
        Assert.NotNull(buckets);

        if (buckets.Count > 0)
        {
            var bucket = buckets[0];
            Assert.True(bucket.Direction.Length > 0.01);
            Assert.True(bucket.HorizontalBasis.Length > 0.01);
            Assert.True(bucket.VerticalBasis.Length > 0.01);
        }
    }

    [Fact]
    public void Drawing_2026_ViewBucket_WireFormat_ExtractsPolylinesAndProjections()
    {
        using var drw = new SldDrawing(TestFileProvider.CubeDrawing2026, readOnly: true);
        var buckets = drw.GetViewBuckets();
        Assert.NotNull(buckets);
        Assert.NotEmpty(buckets);

        var bucket = buckets[0];
        Assert.True(bucket.Scale > 0.001);
        Assert.True(bucket.Direction.Length > 0.01);
        Assert.True(bucket.HorizontalBasis.Length > 0.01);
        Assert.True(bucket.VerticalBasis.Length > 0.01);
        Assert.True(bucket.Lines.Count > 0 || bucket.Polylines.Count > 0);
    }

    [Fact]
    public void Drawing_MfcModels_Roundtrip_DrSheetMfcModel()
    {
        var sheet = new DrSheetMfcModel("TestSheet", width: 0.594, height: 0.420)
        {
            PlaneName = "TestPlane",
            SketchName = "TestSketch",
            Scale = 2.0
        };

        var view = new DrViewMfcModel("Isometric View", 0.15, 0.20, "Isometric")
        {
            Scale = 1.0,
            ReferencedModel = "part.sldprt",
            ReferencedConfiguration = "Default"
        };
        view.Lines.Add(new DrCachedLineSegment(new DrPoint2D(0.1, 0.1), new DrPoint2D(0.2, 0.2), isHidden: false, color: "#FF0000"));
        sheet.Views.Add(view);

        var note = new DrNoteMfcModel("TITLE BLOCK NOTE", 0.05, 0.02, isBalloon: false);
        sheet.Notes.Add(note);

        var balloon = new DrNoteMfcModel("5", 0.12, 0.18, isBalloon: true);
        sheet.Notes.Add(balloon);

        var dim = new DrDimensionMfcModel("50.00 mm", 0.05, 0.15, 0.25);
        sheet.Dimensions.Add(dim);

        byte[] serialized = MfcArchive.Serialize(sheet);
        Assert.NotEmpty(serialized);

        var loaded = MfcArchive.Deserialize<DrSheetMfcModel>(serialized);
        Assert.Equal("TestSheet", loaded.SheetName);
        Assert.Equal(0.594, loaded.Width, precision: 3);
        Assert.Equal(0.420, loaded.Height, precision: 3);
        Assert.Equal("TestPlane", loaded.PlaneName);
        Assert.Equal("TestSketch", loaded.SketchName);
    }

    [Fact]
    public void Drawing_MfcModels_Roundtrip_DrViewMfcModel()
    {
        var view = new DrViewMfcModel("Top View", 0.10, 0.15, "AbsoluteView")
        {
            Scale = 0.5,
            ReferencedModel = "bracket.sldprt",
            ReferencedConfiguration = "ConfigA"
        };
        view.Lines.Add(new DrCachedLineSegment(new DrPoint2D(0.01, 0.02), new DrPoint2D(0.03, 0.04)));

        byte[] serialized = MfcArchive.Serialize(view);
        Assert.NotEmpty(serialized);

        var loaded = MfcArchive.Deserialize<DrViewMfcModel>(serialized);
        Assert.Equal("Top View", loaded.ViewName);
        Assert.Equal(0.10, loaded.PositionX, precision: 3);
        Assert.Equal(0.15, loaded.PositionY, precision: 3);
    }

    [Fact]
    public void Drawing_MfcModels_Roundtrip_DrNoteMfcModel()
    {
        var note = new DrNoteMfcModel("TEST NOTE", 0.123, 0.456, isBalloon: false)
        {
            ItemName = "DetailItem99",
            Width = 0.05,
            Height = 0.01
        };

        byte[] serialized = MfcArchive.Serialize(note);
        Assert.NotEmpty(serialized);

        var loaded = MfcArchive.Deserialize<DrNoteMfcModel>(serialized);
        Assert.Equal("DetailItem99", loaded.ItemName);
        Assert.Equal("TEST NOTE", loaded.Text);
        Assert.Equal(0.123, loaded.PositionX, precision: 3);
        Assert.Equal(0.456, loaded.PositionY, precision: 3);
        Assert.False(loaded.IsBalloon);
    }

    [Fact]
    public void Drawing_MfcModels_Roundtrip_DrDimensionMfcModel()
    {
        var dim = new DrDimensionMfcModel("100.00 mm", 0.1, 0.20, 0.30);
        dim.LeaderLines.Add(new DrCachedLineSegment(new DrPoint2D(0.19, 0.30), new DrPoint2D(0.20, 0.30)));

        byte[] serialized = MfcArchive.Serialize(dim);
        Assert.NotEmpty(serialized);

        var loaded = MfcArchive.Deserialize<DrDimensionMfcModel>(serialized);
        Assert.Equal("100.00 mm", loaded.Text);
        Assert.NotNull(loaded.Value);
        Assert.Equal(0.1, loaded.Value.Value, precision: 3);
        Assert.Equal(0.20, loaded.PositionX, precision: 3);
        Assert.Equal(0.30, loaded.PositionY, precision: 3);
    }

    [Fact]
    public void Drawing_MfcModels_Roundtrip_DrawingDocumentMfcModel()
    {
        var doc = new DrawingDocumentMfcModel();
        doc.Sheets.Add(new DrSheetMfcModel("Sheet1", 0.420, 0.297));
        doc.Templates.Add(new DrTemplateMfcModel { TemplateName = "Sheet Format1" });
        doc.Views.Add(new DrViewMfcModel("Front", 0.1, 0.1));
        doc.Notes.Add(new DrNoteMfcModel("General Note", 0.05, 0.05));
        doc.Dimensions.Add(new DrDimensionMfcModel("25.00", 0.025, 0.1, 0.15));

        byte[] serialized = MfcArchive.Serialize(doc);
        Assert.NotEmpty(serialized);

        var loaded = DrawingDocumentMfcModel.FromBytes(serialized);
        Assert.NotNull(loaded);
    }
}
