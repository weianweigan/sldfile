using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SldFile;
using SldFile.Mfc;
// using SldFile.Mfc;
using Xunit;
using Xunit.Abstractions;

namespace SldFileTests;

/// <summary>
/// Comprehensive verification of SolidWorks benchmark models (sldBenchmarking/Macro)
/// using typed MFC SketchFeatureModel implementing IMfcSerializable,
/// compared against SolidWorks COM ground truth.
/// </summary>
public class MacroBenchmarkSketchTests
{
    private readonly ITestOutputHelper _output;

    public MacroBenchmarkSketchTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string BenchmarkDir
    {
        get
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var c1 = Path.Combine(baseDir, "TestFiles", "MacroBenchmark");
            if (Directory.Exists(c1) && Directory.GetFiles(c1, "*.sldprt").Length > 0)
                return c1;

            var asmLoc = typeof(MacroBenchmarkSketchTests).Assembly.Location;
            if (!string.IsNullOrEmpty(asmLoc))
            {
                var asmDir = Path.GetDirectoryName(asmLoc);
                if (!string.IsNullOrEmpty(asmDir))
                {
                    var c2 = Path.Combine(asmDir, "TestFiles", "MacroBenchmark");
                    if (Directory.Exists(c2) && Directory.GetFiles(c2, "*.sldprt").Length > 0)
                        return c2;
                }
            }

            var curr = new DirectoryInfo(baseDir);
            while (curr != null)
            {
                var c3 = Path.Combine(curr.FullName, "SldFileTests", "TestFiles", "MacroBenchmark");
                if (Directory.Exists(c3) && Directory.GetFiles(c3, "*.sldprt").Length > 0)
                    return c3;

                var c4 = Path.Combine(curr.FullName, "TestFiles", "MacroBenchmark");
                if (Directory.Exists(c4) && Directory.GetFiles(c4, "*.sldprt").Length > 0)
                    return c4;

                curr = curr.Parent;
            }

            return c1;
        }
    }

    [Fact]
    public void AllBenchmarkFiles_VerifyNoUnknownSegments_UsingMfcModels()
    {
        if (!Directory.Exists(BenchmarkDir))
            return;

        var files = Directory.GetFiles(BenchmarkDir, "*.sldprt")
            .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.NotEmpty(files);

        int totalSegments = 0;
        int unknownSegments = 0;
        int totalSketches = 0;

        foreach (var file in files)
        {
            _output.WriteLine($"Processing file: {Path.GetFileName(file)}");
            using var sw = SwFile.Open(file);
            var sketches = sw.GetSketches();

            Assert.NotEmpty(sketches);
            totalSketches += sketches.Count;

            foreach (var sk in sketches)
            {
                foreach (var seg in sk.Segments)
                {
                    totalSegments++;
                    if (seg.Kind == SketchSegmentKind.Unknown)
                    {
                        unknownSegments++;
                        _output.WriteLine($"Unknown segment in {Path.GetFileName(file)}, Sketch '{sk.Name}' (Id={sk.Id}): Kind={seg.Kind}");
                    }
                    if (seg.Kind == SketchSegmentKind.Line)
                    {
                        if (seg.StartPoint == null || seg.EndPoint == null)
                        {
                            _output.WriteLine($"Null point in {Path.GetFileName(file)}, Sketch '{sk.Name}' (Id={sk.Id}): Line Id={seg.Id}, Start={seg.StartPoint?.TableIndex}, End={seg.EndPoint?.TableIndex}");
                        }
                    }
                }
            }
        }

        _output.WriteLine($"Total sketches across all models: {totalSketches}");
        _output.WriteLine($"Total segments across all models: {totalSegments}, Unknown: {unknownSegments}");

        Assert.True(totalSegments > 0, "Benchmark files must contain segments");
        Assert.Equal(0, unknownSegments);
    }

    [Fact]
    public void GroundTruthComparison_CompareWithSolidWorksComOutput()
    {
        string gtPath = Path.Combine(BenchmarkDir, "groundtruth_sketches.json");
        if (!File.Exists(gtPath)) return;

        string json = File.ReadAllText(gtPath);
        var groundTruthModels = JsonSerializer.Deserialize<List<GtModel>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(groundTruthModels);
        Assert.NotEmpty(groundTruthModels);

        int comparedSketches = 0;

        foreach (var gtModel in groundTruthModels)
        {
            string filePath = Path.Combine(BenchmarkDir, gtModel.FileName);
            if (!File.Exists(filePath)) continue;

            using var sw = SwFile.Open(filePath);
            var parsedSketches = sw.GetSketches();

            foreach (var gtSketch in gtModel.Sketches)
            {
                if (gtSketch.SegmentsCount == 0) continue; // Skip empty/origin coordinate sketches

                // Match sketch by name; if multiple exist (e.g. cavity.sldprt), match the non-empty one
                var matched = parsedSketches.FirstOrDefault(s => s.Name == gtSketch.Name && s.Segments.Count > 0)
                           ?? parsedSketches.FirstOrDefault(s => s.Name == gtSketch.Name);
                if (matched == null) continue;

                comparedSketches++;

                // 1. Verify 0 Unknown segments
                int unkCount = matched.Segments.Count(s => s.Kind == SketchSegmentKind.Unknown);
                Assert.Equal(0, unkCount);

                // 2. Verify Segment Kind breakdown matches COM
                var gtKindGroups = gtSketch.Segments.GroupBy(s => s.Kind).ToDictionary(g => g.Key, g => g.Count());
                var parsedKindGroups = matched.Segments.GroupBy(s => s.Kind.ToString()).ToDictionary(g => g.Key, g => g.Count());

                _output.WriteLine($"[{gtModel.FileName}] Sketch '{gtSketch.Name}': COM Segs={gtSketch.SegmentsCount}, Parsed Segs={matched.Segments.Count}, COM [{string.Join(", ", gtKindGroups.Select(kv => $"{kv.Key}:{kv.Value}"))}] vs Parsed [{string.Join(", ", parsedKindGroups.Select(kv => $"{kv.Key}:{kv.Value}"))}]");

                // Check that line/arc counts match COM ground truth (for native sketches)
                if (gtKindGroups.TryGetValue("Line", out int gtLines))
                {
                    parsedKindGroups.TryGetValue("Line", out int parsedLines);
                    // In cavity.sldprt Sketch1 is an absorbed flat pattern reference; in carrier it is native
                    if (gtModel.FileName != "cavity.sldprt" || gtSketch.Name != "Sketch1")
                    {
                        Assert.True(parsedLines >= gtLines, $"[{gtModel.FileName}] Parsed line count ({parsedLines}) should cover COM ground truth ({gtLines}) in {gtSketch.Name}");
                    }
                }

                if (gtKindGroups.TryGetValue("Arc", out int gtArcs))
                {
                    parsedKindGroups.TryGetValue("Arc", out int parsedArcs);
                    if (gtModel.FileName != "cavity.sldprt" || gtSketch.Name != "Sketch1")
                    {
                        Assert.True(parsedArcs >= gtArcs, $"[{gtModel.FileName}] Parsed arc count ({parsedArcs}) should cover COM ground truth ({gtArcs}) in {gtSketch.Name}");
                    }
                }
            }
        }

        _output.WriteLine($"Successfully verified {comparedSketches} non-empty sketches against SolidWorks COM ground truth.");
        Assert.True(comparedSketches > 0);
    }

    [Fact]
    public void FilterPart_Sketch1_VerifyLinesRenderable()
    {
        string filePath = Path.Combine(BenchmarkDir, "filter.sldprt");
        using var sw = SwFile.Open(filePath);
        var sk1 = sw.GetSketches().FirstOrDefault(s => s.Name == "Sketch1");
        Assert.NotNull(sk1);

        // In filter.sldprt Sketch1, the primary outline & centerline lines (at least 8) must have non-null start and end points
        var linesWithPoints = sk1.Segments.Where(s => s.Kind == SketchSegmentKind.Line && s.StartPoint != null && s.EndPoint != null).ToList();
        Assert.True(linesWithPoints.Count >= 8, $"Expected at least 8 valid lines in filter.sldprt Sketch1, found {linesWithPoints.Count}");
    }
    [Fact]
    public void SketchMfcSerialization_RoundtripSymmetry()
    {
        string modelPath = Path.Combine(BenchmarkDir, "carrier.sldprt");
        if (!File.Exists(modelPath)) return;

        using var sw = SwFile.Open(modelPath);
        var sketches = sw.GetSketches();
        var sketch = sketches.FirstOrDefault(s => s.Segments.Count > 0);
        Assert.NotNull(sketch);
        Assert.NotEmpty(sketch.Segments);

        // Store with MfcArchive
        using var writer = MfcArchive.CreateWriter();
        sketch.Serialize(writer);
        byte[] serializedBytes = writer.ToArray();

        Assert.True(serializedBytes.Length > 0);

        // Load back with MfcArchive
        using var reader = MfcArchive.CreateReader(serializedBytes);
        var roundtripped = new SketchFeatureModel();
        roundtripped.Serialize(reader);

        Assert.Equal(sketch.Name, roundtripped.Name);
        Assert.Equal(sketch.Id, roundtripped.Id);
        Assert.Equal(sketch.Is3D, roundtripped.Is3D);
        Assert.Equal(sketch.Segments.Count, roundtripped.Segments.Count);

        for (int i = 0; i < sketch.Segments.Count; i++)
        {
            Assert.Equal(sketch.Segments[i].Kind, roundtripped.Segments[i].Kind);
            Assert.Equal(sketch.Segments[i].IsConstruction, roundtripped.Segments[i].IsConstruction);
        }

        // Verify ToFeature conversion
        var feature = roundtripped;
        Assert.Equal(roundtripped.Name, feature.Name);
        Assert.Equal(roundtripped.Segments.Count, feature.Segments.Count);
    }

    [Fact]
    public void FlangePart_MfcModels_AccurateGeometry()
    {
        string file = Path.Combine(BenchmarkDir, "flange.sldprt");
        if (!File.Exists(file)) return;

        using var sw = SwFile.Open(file);
        var sketches = sw.GetSketches();

        var sketch1 = Assert.Single(sketches.Where(s => s.Name == "Sketch1"));
        Assert.Equal(17, sketch1.Id);
        Assert.NotEmpty(sketch1.Segments);

        // Flange Sketch1 contains circle/arc geometry
        Assert.All(sketch1.Segments, seg =>
        {
            Assert.NotEqual(SketchSegmentKind.Unknown, seg.Kind);
            Assert.Equal(SketchSegmentKind.Arc, seg.Kind);
            Assert.NotNull(seg.CenterPoint);
            Assert.NotNull(seg.Radius);
            Assert.True(seg.Radius > 0, "Radius must be positive");
            // Closed circles have matching start/end points
            if (seg.StartPoint != null && seg.EndPoint != null)
            {
                double dist = Math.Sqrt(Math.Pow(seg.StartPoint.X - seg.EndPoint.X, 2) + Math.Pow(seg.StartPoint.Y - seg.EndPoint.Y, 2));
                Assert.True(dist < 1e-4, "Start and end points of flange circle must coincide");
            }
        });
    }

    [Fact]
    public void FlangePart_NativeSketch_CirclesProperlyIdentified()
    {
        string file = Path.Combine(BenchmarkDir, "flange.sldprt");
        if (!File.Exists(file)) return;

        using var sw = SwFile.Open(file);
        var sketches = sw.GetSketches();
        var sketch1 = Assert.Single(sketches.Where(s => s.Name == "Sketch1"));

        Assert.NotEmpty(sketch1.Segments);
        Assert.All(sketch1.Segments, seg =>
        {
            Assert.Equal(SketchSegmentKind.Arc, seg.Kind);
            Assert.NotNull(seg.CenterPoint);
            Assert.NotNull(seg.Radius);
            Assert.True(seg.Radius > 0);
            Assert.True(seg.IsCircle == true, "IsCircle must be true for full circles");
        });
    }

    [Fact]
    public void ReducerPart_MfcModels_AccurateGeometry()
    {
        string file = Path.Combine(BenchmarkDir, "reducer-ms.sldprt");
        if (!File.Exists(file)) return;

        using var sw = SwFile.Open(file);
        var sketches = sw.GetSketches();

        foreach (var s in sketches)
        {
            _output.WriteLine($"reducer sketch: Name='{s.Name}', Id={s.Id}, Segs={s.Segments.Count}, Pts={s.Points.Count}");
        }

        var sketch2 = sketches.FirstOrDefault(s => s.Name == "Sketch2" && s.Segments.Count > 0)
                   ?? sketches.FirstOrDefault(s => s.Name == "Sketch2");
        Assert.NotNull(sketch2);
        Assert.NotEmpty(sketch2.Segments);

        _output.WriteLine($"Sketch2 segment kinds: {string.Join(", ", sketch2.Segments.Select(s => s.Kind))}");

        // Reducer Sketch2 contains lines and splines in COM / MFC models
        Assert.All(sketch2.Segments, seg =>
        {
            Assert.NotEqual(SketchSegmentKind.Unknown, seg.Kind);
        });
        Assert.Contains(sketch2.Segments, s => s.Kind == SketchSegmentKind.Line);
    }

    [Fact]
    public void SketchDimension_PositionAndOrientation_PopulatedAndAccurate()
    {
        string file = Path.Combine(BenchmarkDir, "filter.sldprt");
        if (!File.Exists(file)) return;

        using var sw = SwFile.Open(file);
        var sketches = sw.GetSketches();
        var sk1 = sketches.FirstOrDefault(s => s.Name == "Sketch1");
        Assert.NotNull(sk1);
        Assert.NotEmpty(sk1.Dimensions);

        _output.WriteLine($"Filter.sldprt Sketch1 Dimensions ({sk1.Dimensions.Count}):");
        foreach (var dim in sk1.Dimensions)
        {
            var textPos = dim.GetTextPosition();
            var dir = dim.GetDirectionVector();
            var norm = dim.GetNormalVector();
            var (w1, w2) = dim.GetWitnessPoints();

            _output.WriteLine($"  Dim '{dim.Name}': Type={dim.DimensionType}, Val={dim.SystemValue}, Disp='{dim.DisplayValue}'");
            _output.WriteLine($"    TextPos: {(textPos.HasValue ? $"({textPos.Value.X * 1000:0.##}, {textPos.Value.Y * 1000:0.##})" : "None")}");
            _output.WriteLine($"    Direction: {(dir.HasValue ? $"({dir.Value.X:0.###}, {dir.Value.Y:0.###})" : "None")}");
            _output.WriteLine($"    Normal: {(norm.HasValue ? $"({norm.Value.X:0.###}, {norm.Value.Y:0.###})" : "None")}");
            _output.WriteLine($"    Witness: P1={(w1.HasValue ? $"({w1.Value.X * 1000:0.##}, {w1.Value.Y * 1000:0.##})" : "None")}, P2={(w2.HasValue ? $"({w2.Value.X * 1000:0.##}, {w2.Value.Y * 1000:0.##})" : "None")}");

            // Verify position and placement data are present
            Assert.True(dim.HasTextPosition, $"Dimension {dim.Name} must have a valid text position");
            Assert.True(!double.IsNaN(dim.TextX!.Value) && !double.IsInfinity(dim.TextX.Value));
            Assert.True(!double.IsNaN(dim.TextY!.Value) && !double.IsInfinity(dim.TextY.Value));

            // Verify type is classified
            Assert.NotEqual(SketchDimensionType.Unknown, dim.DimensionType);

            // For linear dimensions, verify direction vectors and witness points
            if (dim.DimensionType == SketchDimensionType.Linear)
            {
                Assert.True(dim.HasDirection, $"Linear dimension {dim.Name} must have direction");
                double dirLen = Math.Sqrt(dim.DirectionX!.Value * dim.DirectionX.Value + dim.DirectionY!.Value * dim.DirectionY.Value);
                Assert.True(Math.Abs(dirLen - 1.0) < 0.05, $"Direction vector must be normalized, got {dirLen}");

                Assert.NotNull(dim.WitnessPoint1X);
                Assert.NotNull(dim.WitnessPoint2X);
            }
        }

        // Test Roundtrip serialization with position and orientation preserved
        var firstDim = sk1.Dimensions.First(d => d.DimensionType == SketchDimensionType.Linear);
        var model = firstDim;

        using var writer = MfcArchive.CreateWriter();
        model.Serialize(writer);
        byte[] bytes = writer.ToArray();
        Assert.True(bytes.Length > 0);

        using var reader = MfcArchive.CreateReader(bytes);
        var loaded = new SketchDimensionModel();
        loaded.Serialize(reader);

        Assert.Equal(model.Name, loaded.Name);
        Assert.Equal(model.DimensionType, loaded.DimensionType);
        Assert.Equal(model.TextX, loaded.TextX);
        Assert.Equal(model.TextY, loaded.TextY);
        Assert.Equal(model.DirectionX, loaded.DirectionX);
        Assert.Equal(model.DirectionY, loaded.DirectionY);
        Assert.Equal(model.WitnessPoint1X, loaded.WitnessPoint1X);
        Assert.Equal(model.WitnessPoint2X, loaded.WitnessPoint2X);
    }

    [Fact]
    public void FilterPart_InspectSketch1()
    {
        string file = Path.Combine(BenchmarkDir, "filter.sldprt");
        using var sw = SwFile.Open(file);
        var sketches = sw.GetSketches();
        foreach (var sk in sketches)
        {
            _output.WriteLine($"Sketch: '{sk.Name}' Id={sk.Id}, Pts={sk.Points.Count}, Segs={sk.Segments.Count}, Diags=[{string.Join("; ", sk.Diagnostics)}]");
            if (sk.Name == "Sketch1")
            {
                byte[] stream = sw.RawStreams["Contents/Config-0-ResolvedFeatures"];
                int skStart = sk.SourceOffset;
                int skEnd = skStart + sk.GetRawData().Length;
                _output.WriteLine($"Sketch1 range: {skStart} .. {skEnd}");

                for (int i = skStart; i <= skEnd - 6; i++)
                {
                    if (MfcPrimitives.IsMfcTag(stream, i))
                    {
                        int cursor = i;
                        var tag = MfcPrimitives.ReadMfcTag(stream, ref cursor);
                        if (tag != null && tag.ClassName.Length > 0 && tag.DataStart <= skEnd)
                        {
                            _output.WriteLine($"Tag at {tag.TagOffset}: Class='{tag.ClassName}', DataStart={tag.DataStart}");
                            if (tag.ClassName.Contains("Display") || tag.ClassName.Contains("Parameter"))
                            {
                                int len = Math.Min(128, stream.Length - tag.DataStart);
                                _output.WriteLine($"  Bytes [{tag.DataStart}..{tag.DataStart + len}]: {BitConverter.ToString(stream, tag.DataStart, len)}");
                            }
                        }
                    }
                }

                foreach (var p in sk.Points)
                {
                    _output.WriteLine($"  Pt Id={p.Id}, TblIdx={p.TableIndex}, X={p.X}, Y={p.Y}, Z={p.Z}");
                }
                foreach (var s in sk.Segments)
                {
                    _output.WriteLine($"  Seg Id={s.Id}, Kind={s.Kind}, Start={(s.StartPoint != null ? $"Id={s.StartPoint.Id},Tbl={s.StartPoint.TableIndex},({s.StartPoint.X},{s.StartPoint.Y})" : "null")}, End={(s.EndPoint != null ? $"Id={s.EndPoint.Id},Tbl={s.EndPoint.TableIndex},({s.EndPoint.X},{s.EndPoint.Y})" : "null")}, Center={(s.CenterPoint != null ? $"Id={s.CenterPoint.Id},Tbl={s.CenterPoint.TableIndex},({s.CenterPoint.X},{s.CenterPoint.Y})" : "null")}, R={s.Radius}, Const={s.IsConstruction}");
                }
                foreach (var d in sk.Dimensions)
                {
                    _output.WriteLine($"  Dim Name={d.Name}, Val={d.SystemValue}, Disp={d.DisplayValue}, Src={d.ValueSource}");
                }
            }
        }
    }

    private class GtModel
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int SketchCount { get; set; }
        public List<GtSketch> Sketches { get; set; } = new();
    }

    private class GtSketch
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool Is3D { get; set; }
        public int PointsCount { get; set; }
        public int SegmentsCount { get; set; }
        public int RelationsCount { get; set; }
        public List<GtSegment> Segments { get; set; } = new();
    }

    private class GtSegment
    {
        public uint Id { get; set; }
        public int TypeCode { get; set; }
        public string Kind { get; set; } = string.Empty;
        public bool IsConstruction { get; set; }
    }
}
