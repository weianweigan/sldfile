using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SldFile;
using SldFile.Binary;
using SldFile.Drawing;
using SldFile.Mfc;
using Xunit;

namespace SldFileTests;

public class DrawingPreviewSerializationTests
{
    [Fact]
    public void CachesWithoutDocumentVersion_ArePreservedWithoutGuessingPlacements()
    {
        using var drawing = new SldDrawing(Sample("real"));
        foreach (string name in new[] { "Contents/Definition", "Contents/VBLists" })
        {
            byte[] bytes = drawing.GetDrawingPreviewStreamBytes(name)!;
            var cache = new DrawingViewCacheArchive { IsDefinition = name.EndsWith("Definition"),
                ViewNames = new[] { "工程图视图1" } };
            using (var ar = MfcArchive.CreateReader(bytes)) cache.Serialize(ar);
            Assert.Empty(cache.Fragments);
            Assert.Equal(bytes, MfcArchive.Serialize(cache));
        }
        Assert.Equal(5, drawing.GetDrawingPreview().Views.Count);
        Assert.False(drawing.GetDrawingPreview().IsResolved);
    }

    [Theory]
    [InlineData("D8001507933/D8001507933.SLDDRW")]
    [InlineData("MacroBenchmark/carrier.slddrw")]
    [InlineData("MacroBenchmark/cavity.slddrw")]
    [InlineData("MacroBenchmark/punch_holder.slddrw")]
    [InlineData("MacroBenchmark/Mold/bezel moldbase.slddrw")]
    public void DrawingSheets_LoadWithoutFirstChanceInvalidDataExceptions(string relativePath)
    {
        int thread = Environment.CurrentManagedThreadId;
        var exceptions = new System.Collections.Generic.List<string>();
        void Capture(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == thread && e.Exception is InvalidDataException)
                exceptions.Add(e.Exception.ToString());
        }
        AppDomain.CurrentDomain.FirstChanceException += Capture;
        try
        {
            using var drawing = new SldDrawing(TestFileProvider.Resolve("Drawings", relativePath));
            Assert.NotEmpty(drawing.Sheets);
            Assert.All(drawing.Sheets, s => Assert.True(s.BestPreview?.Length > 0 || s.Preview.Primitives.Count > 0));
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= Capture; }
        Assert.Empty(exceptions);
    }
    private static string Sample(string sample) => sample == "real"
        ? TestFileProvider.Resolve(Path.Combine("Drawings", "D8001507933"), "D8001507933.SLDDRW")
        : sample == "2012" ? TestFileProvider.CubeDrawing : TestFileProvider.CubeDrawing2026;

    [Theory]
    [InlineData("real")]
    [InlineData("2012")]
    [InlineData("2026")]
    public void AllNativeSourceStreams_RoundtripEveryByte(string sample)
    {
        using var file = new SldDrawing(Sample(sample));
        var streams = file.SerializeDrawingPreviewStreams();
        Assert.Equal(3, streams.Count);
        foreach (var pair in streams) Assert.Equal(file.RawStreams[pair.Key], pair.Value);
        var native = new DrawingPreviewStreamModel { DecodeCommands = true };
        var original = file.GetDrawingPreviewStreamBytes("Contents/DisplayLists")!;
        using (var ar = MfcArchive.CreateReader(original)) { ar.DocumentVersion = file.GetNativeDrawingDocumentVersion(); native.Serialize(ar); }
        Assert.Equal(original, MfcArchive.Serialize(native));
        Assert.Contains(native.Segments, s => s.Record?.CommandType == 18);
        Assert.Contains(native.Segments, s => s.Record?.CommandType == 4);
    }

    [Fact]
    public void EditedLineAndText_SurviveSaveAndReopen_WithoutChangingOtherStreams()
    {
        using var temp = TestFileProvider.CreateTempCopy(Sample("real"));
        string lineId, textId;
        double newX;
        byte[] definition, buckets;
        using (var drawing = new SldDrawing(temp.Path, readOnly: false))
        {
            definition = drawing.RawStreams["Contents/Definition"];
            buckets = drawing.RawStreams["Contents/VBLists"];
            var preview = drawing.GetDrawingPreview();
            var line = preview.Primitives.First(p => p.Kind == DrawingPreviewPrimitiveKind.Line && !p.Id.StartsWith("VB:"));
            lineId = line.Id; newX = line.Points[0].X + 0.001;
            line.Points = new[] { new DrawingPreviewPoint(newX, line.Points[0].Y, line.Points[0].Z), line.Points[1] };
            var text = preview.Primitives.First(p => p.Text == "25");
            textId = text.Id; text.Text = "26";
            drawing.SetDrawingPreview(preview);
            drawing.Save();
        }
        using var reopened = new SldDrawing(temp.Path);
        var result = reopened.GetDrawingPreview();
        Assert.Equal(newX, result.Primitives.Single(p => p.Id == lineId).Points[0].X);
        Assert.Equal("26", result.Primitives.Single(p => p.Id == textId).Text);
        Assert.Equal(definition, reopened.RawStreams["Contents/Definition"]);
        Assert.Equal(buckets, reopened.RawStreams["Contents/VBLists"]);
    }

    [Fact]
    public void CompressedDisplayCache_PreservesEnvelopeAndUsesSameSerializer()
    {
        using var drawing = new SldDrawing(Sample("real"), readOnly: false);
        var bytes = drawing.RawStreams["Contents/DisplayLists"];
        drawing.RawStreams.Remove("Contents/DisplayLists");
        var compressed = ZlbStreamHandler.Serialize(new ZlbData { RawData = bytes });
        drawing.RawStreams["Contents/DisplayLists__ZLB"] = compressed;
        Assert.Equal(compressed, drawing.SerializeDrawingPreviewStreams()["Contents/DisplayLists__ZLB"]);
        var preview = drawing.GetDrawingPreview();
        var text = preview.Primitives.First(p => p.Text == "25"); text.Text = "26";
        drawing.SetDrawingPreview(preview);
        Assert.Contains(drawing.GetDrawingPreview().Primitives, p => p.Text == "26");
        var decoded = new ZlbData();
        ZlbStreamHandler.Decompress(drawing.SerializeDrawingPreviewStreams()["Contents/DisplayLists__ZLB"], decoded);
        Assert.NotNull(decoded.RawData);
        Assert.Equal(bytes.Length, decoded.RawData!.Length);
    }

    [Fact]
    public void MalformedOrUnverifiedCommand_IsPreserved_AndNeverRenderedAsGeometry()
    {
        byte[] malformed = { 120, 0, 0, 0, 6, 0, 0, 0, 1, 0, 0, 0, 255, 255, 255, 127, 17, 32, 64 };
        var stream = new DrawingPreviewStreamModel { DecodeCommands = true };
        using (var ar = MfcArchive.CreateReader(malformed)) stream.Serialize(ar);
        Assert.Equal(malformed, MfcArchive.Serialize(stream));
        Assert.Empty(stream.ToDefinition(0, 1).Primitives);
    }

    [Fact]
    public void UnsupportedEdit_IsAtomic_AndInternalModelsAreNotPublic()
    {
        using var drawing = new SldDrawing(Sample("real"), readOnly: false);
        var original = drawing.SerializeDrawingPreviewStreams()["Contents/DisplayLists"];
        var preview = drawing.GetDrawingPreview();
        preview.Primitives.First(p => p.Text == "25").Text = "longer";
        Assert.Throws<NotSupportedException>(() => drawing.SetDrawingPreview(preview));
        Assert.Equal(original, drawing.SerializeDrawingPreviewStreams()["Contents/DisplayLists"]);
        preview = drawing.GetDrawingPreview();
        preview.Primitives.First(p => p.Kind == DrawingPreviewPrimitiveKind.Arc && !p.Id.StartsWith("VB:")).Normal = new DrawingPreviewPoint(double.NaN, 0, 1);
        Assert.Throws<InvalidDataException>(() => drawing.SetDrawingPreview(preview));
        Assert.Equal(original, drawing.SerializeDrawingPreviewStreams()["Contents/DisplayLists"]);
        Assert.DoesNotContain(typeof(SldDrawing).Assembly.GetExportedTypes(), t =>
            t.Name.StartsWith("DrawingPreview") && t.Namespace == "SldFile.Mfc" || t.Name == "DrawingDocumentMfcModel");
    }
}
