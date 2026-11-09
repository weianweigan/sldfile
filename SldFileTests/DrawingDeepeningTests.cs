using System;
using System.IO;
using System.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

public class DrawingDeepeningTests
{
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public DrawingDeepeningTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectVBListsBinary()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/VBLists", out var vbBytes)) return;

        _output.WriteLine($"VBLists size: {vbBytes.Length} bytes");
        // Print first 64 bytes
        _output.WriteLine("First 64 bytes of VBLists:");
        for (int i = 0; i < 64; i += 16)
        {
            string hex = BitConverter.ToString(vbBytes, i, 16).Replace("-", " ");
            _output.WriteLine($"  +{i:X2}: {hex}");
        }

        // Check if VBLists can be parsed as MfcDocument
        var doc = SldFile.Mfc.MfcDocument.Parse(vbBytes);
        _output.WriteLine($"MFC Elements in VBLists: {doc.AllElements.Count}");
        foreach (var elem in doc.AllElements)
        {
            if (!string.IsNullOrEmpty(elem.ClassName))
            {
                _output.WriteLine($"  [{elem.ClassName}] TagOff=0x{elem.TagOffset:X}, Len={elem.DataLength}");
            }
        }
    }

    [Fact]
    public void TestBestSheetPreviewImageDecoding()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        var bytes = swFile.GetBestSheetPreviewImage(0);
        _output.WriteLine($"BestSheetPreviewImage bytes: {bytes?.Length}");
        if (bytes != null)
        {
            var (w, h) = SwFile.GetImageDimensions(bytes);
            _output.WriteLine($"Dimensions: {w} x {h}");
            _output.WriteLine($"Header: {bytes[0]:X2} {bytes[1]:X2} {bytes[2]:X2} {bytes[3]:X2}");
        }
    }

    [Fact]
    public void InspectCadLineClusters()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/DisplayLists", out var dlBytes)) return;

        var lines = SldFile.Binary.DisplayListsSchemaMapper.ExtractCadLines(dlBytes, null, 420.0, 297.0);
        _output.WriteLine($"Extracted {lines.Count} lines from DisplayLists");

        // Group lines by Y coordinate intervals (each view corresponds to a horizontal band)
        var yBands = lines.GroupBy(l => (int)(Math.Min(l.Y1, l.Y2) / 20.0) * 20)
            .OrderBy(g => g.Key);

        foreach (var b in yBands)
        {
            double minX = b.Min(l => Math.Min(l.X1, l.X2));
            double maxX = b.Max(l => Math.Max(l.X1, l.X2));
            double minY = b.Min(l => Math.Min(l.Y1, l.Y2));
            double maxY = b.Max(l => Math.Max(l.Y1, l.Y2));
            _output.WriteLine($"Band Y=[{minY:F1}..{maxY:F1}], X=[{minX:F1}..{maxX:F1}], Count={b.Count()}");
        }
    }

    [Fact]
    public void TestProjectMeshOutlineEdgesToViews()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/VBLists", out var vbBytes)) return;

        var mesh = SldFile.Binary.DisplayMeshReader.ReadMesh(vbBytes);
        Assert.NotNull(mesh);

        _output.WriteLine($"Source mesh outline edges: {mesh.OutlineEdges.Count}");
        
        // 1. Front View (moAbsoluteView / 工程图视图1)
        var allProjectedLines = new List<(double x1, double y1, double x2, double y2)>();
        double cx1 = 162.8, cy1 = 133.6;
        double scaleX1 = 156.0 / (mesh.Max.X - mesh.Min.X);
        double scaleY1 = 18.8 / (mesh.Max.Z - mesh.Min.Z);
        double midX = (mesh.Min.X + mesh.Max.X) / 2.0;
        double midZ = (mesh.Min.Z + mesh.Max.Z) / 2.0;
        double midY = (mesh.Min.Y + mesh.Max.Y) / 2.0;

        var seen = new HashSet<string>();
        foreach (var edge in mesh.OutlineEdges)
        {
            var p1 = mesh.Positions[edge.Start];
            var p2 = mesh.Positions[edge.End];

            double x1 = cx1 + (p1.X - midX) * scaleX1;
            double y1 = cy1 - (p1.Z - midZ) * scaleY1;
            double x2 = cx1 + (p2.X - midX) * scaleX1;
            double y2 = cy1 - (p2.Z - midZ) * scaleY1;

            if (Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)) > 0.05)
            {
                string key = $"{Math.Round(x1, 1)}_{Math.Round(y1, 1)}_{Math.Round(x2, 1)}_{Math.Round(y2, 1)}";
                if (seen.Add(key)) allProjectedLines.Add((x1, y1, x2, y2));
            }
        }

        // 2. Top View (moUnfoldedView / 顶部长梁)
        double cx2 = 162.8, cy2 = 173.6;
        double scaleY2 = 1.7 / (mesh.Max.Y - mesh.Min.Y);
        foreach (var edge in mesh.OutlineEdges)
        {
            var p1 = mesh.Positions[edge.Start];
            var p2 = mesh.Positions[edge.End];

            double x1 = cx2 + (p1.X - midX) * scaleX1;
            double y1 = cy2 + (p1.Y - midY) * scaleY2;
            double x2 = cx2 + (p2.X - midX) * scaleX1;
            double y2 = cy2 + (p2.Y - midY) * scaleY2;

            if (Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)) > 0.05)
            {
                string key = $"{Math.Round(x1, 1)}_{Math.Round(y1, 1)}_{Math.Round(x2, 1)}_{Math.Round(y2, 1)}";
                if (seen.Add(key)) allProjectedLines.Add((x1, y1, x2, y2));
            }
        }

        // 3. Bottom View (仰视图 / 底部长梁)
        double cx3 = 162.8, cy3 = 98.1;
        double scaleY3 = 1.7 / (mesh.Max.Y - mesh.Min.Y);
        foreach (var edge in mesh.OutlineEdges)
        {
            var p1 = mesh.Positions[edge.Start];
            var p2 = mesh.Positions[edge.End];

            double x1 = cx3 + (p1.X - midX) * scaleX1;
            double y1 = cy3 - (p1.Y - midY) * scaleY3;
            double x2 = cx3 + (p2.X - midX) * scaleX1;
            double y2 = cy3 - (p2.Y - midY) * scaleY3;

            if (Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)) > 0.05)
            {
                string key = $"{Math.Round(x1, 1)}_{Math.Round(y1, 1)}_{Math.Round(x2, 1)}_{Math.Round(y2, 1)}";
                if (seen.Add(key)) allProjectedLines.Add((x1, y1, x2, y2));
            }
        }

        // 4. Section / Left Profile View (槽钢截面)
        double cx4 = 52.0, cy4 = 133.6;
        double scaleX4 = 1.7 / (mesh.Max.Y - mesh.Min.Y);
        foreach (var edge in mesh.OutlineEdges)
        {
            var p1 = mesh.Positions[edge.Start];
            var p2 = mesh.Positions[edge.End];

            double x1 = cx4 + (p1.Y - midY) * scaleX4;
            double y1 = cy4 - (p1.Z - midZ) * scaleY1;
            double x2 = cx4 + (p2.Y - midY) * scaleX4;
            double y2 = cy4 - (p2.Z - midZ) * scaleY1;

            if (Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2)) > 0.05)
            {
                string key = $"{Math.Round(x1, 1)}_{Math.Round(y1, 1)}_{Math.Round(x2, 1)}_{Math.Round(y2, 1)}";
                if (seen.Add(key)) allProjectedLines.Add((x1, y1, x2, y2));
            }
        }

        _output.WriteLine($"Total projected 2D wireframe lines across all views: {allProjectedLines.Count}");
        Assert.True(allProjectedLines.Count >= 700);
    }

    [Fact]
    public void TestExtractViewsFromDefinition()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var views = SldFile.Drawing.Vector.DrawingGeometryReader.ExtractViewsFromDefinition(defBytes, 420.0, 297.0);
        _output.WriteLine($"Extracted {views.Count} views from Definition:");
        foreach (var v in views)
        {
            _output.WriteLine($"  {v.Name}: Center=({v.CenterXMm:F1}, {v.CenterYMm:F1}), Size=({v.WidthMm:F1} x {v.HeightMm:F1}) mm");
        }
    }

    [Fact]
    public void InspectViewBBoxesAndDimensions()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                byte[] vBytes = new byte[v.DataLength];
                Array.Copy(defBytes, v.TagOffset, vBytes, 0, v.DataLength);

                for (int off = 0; off <= v.DataLength - 32; off += 8)
                {
                    double x1 = BitConverter.ToDouble(vBytes, off);
                    double y1 = BitConverter.ToDouble(vBytes, off + 8);
                    double x2 = BitConverter.ToDouble(vBytes, off + 16);
                    double y2 = BitConverter.ToDouble(vBytes, off + 24);

                    if (x1 >= 0.01 && x1 <= 0.42 && x2 >= 0.01 && x2 <= 0.42 &&
                        y1 >= 0.01 && y1 <= 0.297 && y2 >= 0.01 && y2 <= 0.297)
                    {
                        _output.WriteLine($"    BBox @ +0x{off:X2}: ({x1*1000:F1}, {y1*1000:F1}) to ({x2*1000:F1}, {y2*1000:F1}) mm");
                    }
                }
            }
        }
    }

    [Fact]
    public void TestDisplayMeshFromVBLists()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/VBLists", out var vbBytes)) return;

        var mesh = SldFile.Binary.DisplayMeshReader.ReadMesh(vbBytes);
        _output.WriteLine($"Mesh is null? {mesh == null}");
        if (mesh != null)
        {
            _output.WriteLine($"Positions: {mesh.Positions.Count}");
            _output.WriteLine($"TriangleCount: {mesh.TriangleCount}");
            _output.WriteLine($"OutlineEdges: {mesh.OutlineEdges.Count}");
            _output.WriteLine($"Min: {mesh.Min}, Max: {mesh.Max}");
        }
    }

    [Fact]
    public void TestDisplayMeshFromDrawing()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        var mesh = swFile.GetDisplayMesh();
        _output.WriteLine($"Mesh is null? {mesh == null}");
        if (mesh != null)
        {
            _output.WriteLine($"Positions: {mesh.Positions.Count}");
            _output.WriteLine($"TriangleCount: {mesh.TriangleCount}");
            _output.WriteLine($"OutlineEdges: {mesh.OutlineEdges.Count}");
            _output.WriteLine($"Min: {mesh.Min}, Max: {mesh.Max}");
        }
    }

    [Fact]
    public void InspectDisplayListsTrailingData()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/DisplayLists", out var dlBytes)) return;

        int afterMfc = 0xD7AB;
        _output.WriteLine($"Total size: {dlBytes.Length}, after MFC: {dlBytes.Length - afterMfc} bytes");

        // Hex dump first 256 bytes after MFC
        _output.WriteLine("First 128 bytes after MFC:");
        for (int i = 0; i < 128; i += 16)
        {
            string hex = BitConverter.ToString(dlBytes, afterMfc + i, 16).Replace("-", " ");
            _output.WriteLine($"  +{i:X2} (0x{afterMfc+i:X4}): {hex}");
        }

        // Count double values in paper coordinate range (0..420 mm -> 0..0.42 meters)
        int doubleMatches = 0;
        for (int i = 0; i <= dlBytes.Length - 16; i += 8)
        {
            double d = BitConverter.ToDouble(dlBytes, i);
            if (!double.IsNaN(d) && !double.IsInfinity(d) && d >= 0.005 && d <= 0.450)
            {
                doubleMatches++;
            }
        }
        _output.WriteLine($"Double coordinates in 0.005..0.450m range across entire stream: {doubleMatches}");

        // Also check single precision float matches
        int floatMatches = 0;
        for (int i = 0; i <= dlBytes.Length - 8; i += 4)
        {
            float f = BitConverter.ToSingle(dlBytes, i);
            if (!float.IsNaN(f) && !float.IsInfinity(f) && f >= 0.005f && f <= 0.450f)
            {
                floatMatches++;
            }
        }
        _output.WriteLine($"Float coordinates in 0.005..0.450m range: {floatMatches}");
    }

    [Fact]
    public void InspectDisplayListsStructure()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/DisplayLists", out var dlBytes)) return;

        _output.WriteLine($"DisplayLists size: {dlBytes.Length} bytes");
        var doc = SldFile.Mfc.MfcDocument.Parse(dlBytes);
        _output.WriteLine($"MFC Elements count: {doc.AllElements.Count}");
        foreach (var elem in doc.AllElements)
        {
            if (!string.IsNullOrEmpty(elem.ClassName))
            {
                _output.WriteLine($"  [{elem.ClassName}] TagOff=0x{elem.TagOffset:X}, Len={elem.DataLength}");
            }
        }
    }

    [Fact]
    public void DumpMfcViewHierarchies()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"==================================================================");
                _output.WriteLine($"VIEW: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength}, Index={v.Index}");
                // Print all direct and indirect children
                var children = doc.AllElements.Where(e => e.Parent == v).ToList();
                _output.WriteLine($"  Direct Children ({children.Count}):");
                foreach (var c in children)
                {
                    _output.WriteLine($"    [{c.ClassName}] TagOff=0x{c.TagOffset:X}, Len={c.DataLength}");
                }
            }
        }
    }

    [Fact]
    public void InspectUnfoldedViewRange()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var inRange = doc.AllElements
            .Where(e => e.TagOffset >= 0x358E9 && e.TagOffset < 0x60853)
            .ToList();
        
        _output.WriteLine($"Elements in UnfoldedView range: {inRange.Count}");
        foreach (var e in inRange)
        {
            _output.WriteLine($"  [{e.ClassName}] TagOff=0x{e.TagOffset:X}, Len={e.DataLength}, Parent=[{e.Parent?.ClassName}]");
        }
    }

    [Fact]
    public void DumpKeyWordsXml()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("swXmlContents/KeyWords", out var kwBytes)) return;

        string xml = System.Text.Encoding.UTF8.GetString(kwBytes);
        int idx = xml.IndexOf('<');
        if (idx >= 0) xml = xml.Substring(idx);
        _output.WriteLine(xml);
    }

    [Fact]
    public void DumpViewDetailedFields()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                byte[] vBytes = new byte[v.DataLength];
                Array.Copy(defBytes, v.TagOffset, vBytes, 0, v.DataLength);

                for (int off = 0; off <= v.DataLength - 8; off += 4)
                {
                    double d = BitConverter.ToDouble(vBytes, off);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) >= 1e-4 && Math.Abs(d) <= 1000.0)
                    {
                        _output.WriteLine($"    +0x{off:X2} (rel): double = {d}");
                    }
                }
            }
        }
    }

    [Fact]
    public void DumpViewProperties()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                var reader = v.CreateReader();
                // Find all WChars in this element
                while (reader.Remaining > 3)
                {
                    if (reader.IsWCharPrefix)
                    {
                        int off = reader.Offset;
                        string s = reader.ReadWChar() ?? "";
                        if (!string.IsNullOrEmpty(s))
                            _output.WriteLine($"    WChar @ +0x{off:X}: '{s}'");
                    }
                    else
                    {
                        reader.Skip(1);
                    }
                }
            }
        }
    }

    [Fact]
    public void ListAllMfcClassesInDefinition()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var classCounts = doc.AllElements
            .GroupBy(e => string.IsNullOrEmpty(e.ClassName) ? "<anonymous>" : e.ClassName)
            .OrderByDescending(g => g.Count())
            .ToList();

        _output.WriteLine($"Total MFC elements: {doc.AllElements.Count}");
        foreach (var g in classCounts)
        {
            _output.WriteLine($"  {g.Key,-35} : {g.Count()} elements");
        }
    }

    [Fact]
    public void PrintAllStreamsAndViews()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        _output.WriteLine($"Streams count: {swFile.RawStreams.Count}");
        foreach (var kvp in swFile.RawStreams.OrderBy(k => k.Key))
        {
            _output.WriteLine($"  {kvp.Key,-40} : {kvp.Value.Length,8:N0} bytes");
        }
    }

    [Fact]
    public void InspectElementsBetweenViews()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var inRange = doc.AllElements
            .Where(e => e.TagOffset >= 0x2649B && e.TagOffset < 0x358E9)
            .ToList();
        
        _output.WriteLine($"Elements in range: {inRange.Count}");
        foreach (var e in inRange)
        {
            _output.WriteLine($"  [{e.ClassName}] TagOff=0x{e.TagOffset:X}, Len={e.DataLength}, Parent=[{e.Parent?.ClassName}]");
        }
    }

    [Fact]
    public void InspectViewTransformsAndGeometryInDefinition()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        if (!swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes)) return;

        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        var viewClasses = new[] { "moAbsoluteView", "moUnfoldedView", "moDetailView", "moView_c" };

        foreach (var vc in viewClasses)
        {
            var views = doc.FindAll(vc);
            foreach (var v in views)
            {
                _output.WriteLine($"=== View: [{v.ClassName}] TagOff=0x{v.TagOffset:X}, Len={v.DataLength} ===");
                byte[] vBytes = new byte[v.DataLength];
                Array.Copy(defBytes, v.TagOffset, vBytes, 0, v.DataLength);

                for (int off = 0; off <= v.DataLength - 8; off += 4)
                {
                    double d = BitConverter.ToDouble(vBytes, off);
                    if (!double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) > 1e-4 && Math.Abs(d) < 10.0)
                    {
                        _output.WriteLine($"    +0x{off:X2}: double={d:F6}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Drawing_RealDrawingFile_D8001507933_VerifiesMfcViewsAndHighDefPreview()
    {
        string realPath = @"C:\Users\HP\Desktop\New folder\D8001507933.SLDDRW";
        if (!File.Exists(realPath)) return;

        using var swFile = SwFile.Open(realPath, readOnly: true);
        
        // 1. Verify Definition stream contains MFC drawing views
        Assert.True(swFile.RawStreams.TryGetValue("Contents/Definition", out var defBytes));
        var doc = SldFile.Mfc.MfcDocument.Parse(defBytes);
        Assert.NotEmpty(doc.FindAll("moDrSheet"));
        Assert.NotEmpty(doc.FindAll("moAbsoluteView"));
        Assert.NotEmpty(doc.FindAll("moUnfoldedView"));
        Assert.NotEmpty(doc.FindAll("moDetailView"));
        Assert.NotEmpty(doc.FindAll("moView_c"));

        // 2. Verify Preview DIB stream dimensions (1420 x 710)
        var bestSheet = swFile.GetBestSheetPreviewImage(0);
        Assert.NotNull(bestSheet);
        var (sw, sh) = SwFile.GetImageDimensions(bestSheet);
        Assert.Equal(1420, sw);
        Assert.Equal(710, sh);

        // 3. Verify Sheets and Views extraction
        var sheets = swFile.GetDrawingSheets();
        Assert.NotEmpty(sheets);
        var sheet0 = sheets[0];
        Assert.Equal("图纸1", sheet0.Name);
        Assert.True(sheet0.Views.Count >= 4);

        // 4. Verify MFC view deserialization details
        Assert.Contains(sheet0.Views, v => v.MfcClassName == "moAbsoluteView");
        Assert.Contains(sheet0.Views, v => v.MfcClassName == "moUnfoldedView");
        Assert.Contains(sheet0.Views, v => v.MfcClassName == "moDetailView");

        // 5. Verify Technical Notes and Title Block
        Assert.NotEmpty(sheet0.TechnicalNotes);
        Assert.Contains(sheet0.TechnicalNotes, n => n.Contains("锐边倒角去毛刺"));
        Assert.NotEmpty(sheet0.TitleBlockProperties);
        Assert.Contains(sheet0.TitleBlockProperties.Keys, k => k.Contains("比例"));

        // 6. Verify 3D Model Wireframe Lines projected from VBLists (eDrawings quality)
        Assert.NotNull(sheet0.VectorData);
        Assert.True(sheet0.VectorData.HasLines);
        _output.WriteLine($"Total VectorData.Lines count in sheet0: {sheet0.VectorData.Lines.Count}");
        Assert.True(sheet0.VectorData.Lines.Count >= 2000, $"Expected >= 2000 vector CAD lines, but got {sheet0.VectorData.Lines.Count}");
    }

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

        // Preview PNG (backward compatibility)
        Assert.NotNull(sheet.PreviewPng);
        Assert.True(sheet.PreviewPng!.Length > 500);
        Assert.Equal(PngMagic[0], sheet.PreviewPng[0]);
        Assert.Equal(PngMagic[1], sheet.PreviewPng[1]);
        Assert.Equal(PngMagic[2], sheet.PreviewPng[2]);
        Assert.Equal(PngMagic[3], sheet.PreviewPng[3]);

        // Views
        Assert.NotEmpty(sheet.Views);
        Assert.Contains(sheet.Views, v => v.Name.Contains("Drawing View"));
        Assert.All(sheet.Views, v => Assert.Contains("cube.sldprt", v.ReferencedModelPath, System.StringComparison.OrdinalIgnoreCase));
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
    public void Drawing_GetBestPreviewImage_ReturnsHighResolutionBitmap()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        byte[]? bestImage = file.GetBestPreviewImage(0);

        Assert.NotNull(bestImage);
        var (width, height) = SwFile.GetImageDimensions(bestImage);
        // High-res preview bitmap is 2850x1436
        Assert.True(width >= 1920, $"Expected high resolution width >= 1920, but was {width}");
        Assert.True(height >= 1080, $"Expected high resolution height >= 1080, but was {height}");
    }

    [Fact]
    public void Drawing_GetBestSheetPreviewImage_ReturnsHighResForSheet0()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        byte[]? bestSheetImg = file.GetBestSheetPreviewImage(0);

        Assert.NotNull(bestSheetImg);
        var (width, height) = SwFile.GetImageDimensions(bestSheetImg);
        Assert.True(width >= 1920);
        Assert.True(height >= 1080);
    }

    [Fact]
    public void Drawing_Sheets_ContainBothPreviewPngAndHighResPreview()
    {
        using var file = SwFile.Open(TestFileProvider.CubeDrawing2026, readOnly: true);
        var sheets = file.GetDrawingSheets();

        Assert.NotNull(sheets);
        Assert.NotEmpty(sheets);
        var sheet0 = sheets[0];

        // PreviewPng is backward-compatible 640x480 PNG
        Assert.NotNull(sheet0.PreviewPng);
        var (pngW, pngH) = SwFile.GetImageDimensions(sheet0.PreviewPng);
        Assert.Equal(640, pngW);
        Assert.Equal(480, pngH);

        // HighResPreview is 2850x1436 BMP
        Assert.NotNull(sheet0.HighResPreview);
        var (highW, highH) = SwFile.GetImageDimensions(sheet0.HighResPreview);
        Assert.True(highW >= 1920);
        Assert.True(highH >= 1080);

        // BestPreview resolves to the high-res one
        Assert.Same(sheet0.HighResPreview, sheet0.BestPreview);
    }

    [Fact]
    public void SwFile_GetImageDimensions_ParsesPngAndBmpAccurately()
    {
        // 1. Check invalid/null
        Assert.Equal((0, 0), SwFile.GetImageDimensions(null));
        Assert.Equal((0, 0), SwFile.GetImageDimensions(new byte[10]));

        // 2. Synthesize a small 100x50 PNG header
        byte[] pngHeader = new byte[24];
        pngHeader[0] = 0x89; pngHeader[1] = 0x50; pngHeader[2] = 0x4E; pngHeader[3] = 0x47;
        pngHeader[16] = 0; pngHeader[17] = 0; pngHeader[18] = 0; pngHeader[19] = 100; // width 100
        pngHeader[20] = 0; pngHeader[21] = 0; pngHeader[22] = 0; pngHeader[23] = 50;  // height 50
        Assert.Equal((100, 50), SwFile.GetImageDimensions(pngHeader));

        // 3. Synthesize a small 320x240 BMP header
        byte[] bmpHeader = new byte[26];
        bmpHeader[0] = 0x42; bmpHeader[1] = 0x4D; // "BM"
        BitConverter.GetBytes(320).CopyTo(bmpHeader, 18);
        BitConverter.GetBytes(240).CopyTo(bmpHeader, 22);
        Assert.Equal((320, 240), SwFile.GetImageDimensions(bmpHeader));
    }
}
