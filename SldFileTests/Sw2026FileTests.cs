using SldFile;

namespace SldFileTests;

public class Sw2026FileTests
{
    #region ── Format detection ──────────────────────────────────────────

    [Fact]
    public void Detects2026Format()
    {
        var d2012 = File.ReadAllBytes(TestFileProvider.CubePart).AsSpan(0, 8).ToArray();
        var d2026 = File.ReadAllBytes(TestFileProvider.CubePart2026).AsSpan(0, 8).ToArray();

        Assert.False(SldFile.Binary.SectionContainerReader.IsSectionContainer(d2012));
        Assert.True(SldFile.Binary.SectionContainerReader.IsSectionContainer(d2026));
    }

    [Fact]
    public void All2026FilesDetected()
    {
        foreach (var path in new[] {
            TestFileProvider.CubePart2026,
            TestFileProvider.CylinderPart2026,
            TestFileProvider.Assembly2026,
            TestFileProvider.CubeDrawing2026 })
        {
            var header = File.ReadAllBytes(path).AsSpan(0, 8).ToArray();
            Assert.True(SldFile.Binary.SectionContainerReader.IsSectionContainer(header),
                $"Failed to detect 2026 format for {Path.GetFileName(path)}");
        }
    }

    [Fact]
    public void All2026FilesHaveKey4()
    {
        foreach (var path in new[] {
            TestFileProvider.CubePart2026,
            TestFileProvider.CylinderPart2026,
            TestFileProvider.Assembly2026,
            TestFileProvider.CubeDrawing2026 })
        {
            var data = File.ReadAllBytes(path);
            var reader = new SldFile.Binary.SectionContainerReader(data);
            Assert.Equal(4, reader.Key);
        }
    }

    #endregion ── Stream extraction ─────────────────────────────────────────

    [Fact]
    public void ExtractsStreamsFrom2026Cube_Original()
    {
    }

    [Fact]
    public void Features_2026CubePart_ReturnsRecognizedFeatures()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart2026, readOnly: true);
        Assert.NotEmpty(file.Features);

        var names = file.Features.Select(f => f.Name).ToList();
        Assert.Contains("前视基准面", names);
        Assert.Contains("草图1", names);
        Assert.Contains("凸台-拉伸1", names);

        var extrusion = file.Features.FirstOrDefault(f => f.Name == "凸台-拉伸1");
        Assert.NotNull(extrusion);
        Assert.Equal(FeatureKind.Extrusion, extrusion!.Kind);

        var sketch = file.Features.FirstOrDefault(f => f.Name == "草图1");
        Assert.NotNull(sketch);
        Assert.Equal(FeatureKind.Sketch, sketch!.Kind);
    }

    [Fact]
    public void Features_2026CylinderPart_ReturnsRecognizedFeatures()
    {
        using var file = SwFile.Open(TestFileProvider.CylinderPart2026, readOnly: true);
        Assert.NotEmpty(file.Features);

        var kinds = file.Features.Select(f => f.Kind).Distinct().ToList();
        Assert.Contains(FeatureKind.ReferencePlane, kinds);
        Assert.Contains(FeatureKind.Sketch, kinds);
        Assert.Contains(FeatureKind.Extrusion, kinds);
    }

    [Fact]
    public void ExtractsStreamsFrom2026Cube()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var reader = new SldFile.Binary.SectionContainerReader(data);
        var streams = reader.ExtractAllStreams();

        Assert.True(streams.Count > 0);
        // 2026 format uses OPC-style paths
        Assert.Contains("PreviewPNG", streams.Keys);
        Assert.Contains("Header2", streams.Keys);
    }

    [Fact]
    public void ExtractedStreamsMatchIndexedSizesIncludingEmptyStreams()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var reader = new SldFile.Binary.SectionContainerReader(data);
        var streams = reader.ExtractAllStreams();

        // Dump all stream names for debugging
        var names = string.Join(", ", streams.Keys.OrderBy(k => k));
        System.Diagnostics.Debug.WriteLine($"All streams ({streams.Count}): {names}");

        Assert.True(streams.Count >= 30, $"Expected >=30 streams, got {streams.Count}: {names}");
        var directory = SldFile.Binary.SectionArchiveDirectory.Read(data, reader.Key);
        Assert.Equal(directory.Entries.Count, streams.Count);
        foreach (var entry in directory.Entries)
        {
            Assert.Equal(entry.Size, streams[entry.Name].Length);
        }
    }

    [Fact]
    public void MajorStreamsHaveReasonableSizes()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var reader = new SldFile.Binary.SectionContainerReader(data);
        var streams = reader.ExtractAllStreams();

        Assert.True(streams.ContainsKey("Header2"),
            $"Missing Header2. Available: {string.Join(", ", streams.Keys.OrderBy(k => k))}");
        Assert.True(streams["Header2"].Length > 1000,
            $"Header2 too small: {streams["Header2"].Length} bytes");

        Assert.True(streams.ContainsKey("PreviewPNG"),
            $"Missing PreviewPNG. Available: {string.Join(", ", streams.Keys.OrderBy(k => k))}");
        Assert.True(streams["PreviewPNG"].Length > 100,
            $"PreviewPNG too small: {streams["PreviewPNG"].Length} bytes");

        Assert.True(streams.ContainsKey("Contents/DisplayLists"),
            $"Missing DisplayLists. Available: {string.Join(", ", streams.Keys.OrderBy(k => k))}");
        Assert.True(streams["Contents/DisplayLists"].Length > 100,
            $"DisplayLists too small: {streams["Contents/DisplayLists"].Length} bytes");
    }

    [Fact]
    public void DocPropsStreamsAreValidXml()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var reader = new SldFile.Binary.SectionContainerReader(data);
        var streams = reader.ExtractAllStreams();

        // OPC docProps streams should be valid XML
        if (streams.TryGetValue("docProps/core.xml", out var coreXml))
        {
            var text = System.Text.Encoding.UTF8.GetString(coreXml);
            Assert.StartsWith("<?xml", text.TrimStart());
        }

        if (streams.TryGetValue("_rels/.rels", out var rels))
        {
            var text = System.Text.Encoding.UTF8.GetString(rels);
            Assert.StartsWith("<?xml", text.TrimStart());
        }
    }

    [Fact]
    public void KnownStreamsPresent()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var reader = new SldFile.Binary.SectionContainerReader(data);
        var streams = reader.ExtractAllStreams();

        // Core SolidWorks streams that exist in both 2012 and 2026 formats
        var expectedStreams = new[]
        {
            "Header2",
            "PreviewPNG",
            "Preview",
            "Contents/DisplayLists",
            "Contents/Config-0",
            "Contents/Config-0-Partition",
            "Contents/Config-0-GhostPartition",
            "Contents/Config-0-ResolvedFeatures",
            "Contents/Config-0-LWDATA",
            "Contents/CMgr",
            "Contents/CMgrHdr2",
            "Contents/Definition",
            "Contents/CnfgObjs",
            "swXmlContents/Features",
            "swXmlContents/KeyWords",
            "swXmlContents/MATERIALTREE",
        };

        foreach (var expected in expectedStreams)
        {
            Assert.True(streams.ContainsKey(expected),
                $"Missing expected stream: '{expected}'");
        }
    }

    // ── ROL encoding ──────────────────────────────────────────────

    [Fact]
    public void RolDecodingRoundTrip()
    {
        byte Rol(byte b, int s)
        {
            s &= 7;
            return (byte)((b << s) | (b >> (8 - s)));
        }

        var testName = "Contents/DisplayLists";
        var testData = System.Text.Encoding.ASCII.GetBytes(testName);
        var key = (byte)4;
        var scrambled = new byte[testData.Length];
        for (int i = 0; i < testData.Length; i++)
            scrambled[i] = Rol(testData[i], key);
        var unscrambled = new byte[testData.Length];
        for (int i = 0; i < testData.Length; i++)
            unscrambled[i] = Rol(scrambled[i], key);

        Assert.Equal(testData, unscrambled);
    }

    [Fact]
    public void Header2_ShouldExposeUsers()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart2026, readOnly: true);

        var header2 = file.Header2;

        Assert.NotNull(header2);
        Assert.NotNull(header2.Users);
        Assert.NotEmpty(header2.Users);
        // First user is the creator
        Assert.False(string.IsNullOrEmpty(header2.Users[0]));
    }

    [Fact]
    public void RoundTrip_NoModifications_ReopensSuccessfully()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart2026);

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            file.Save();
        }

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            Assert.NotEmpty(file.Header2.Users);
            Assert.NotNull(file.PreviewPng);
            Assert.True(file.PreviewPng.Length > 0);
        }
    }

    [Fact]
    public void RoundTrip_All2026SampleFiles()
    {
        var files = new[]
        {
            TestFileProvider.CubePart2026,
            TestFileProvider.CylinderPart2026,
            TestFileProvider.Assembly2026,
            TestFileProvider.CubeDrawing2026
        };

        foreach (var source in files)
        {
            using var temp = TestFileProvider.CreateTempCopy(source);

            using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
            {
                Assert.NotEmpty(file.Header2.Users);
                file.Save();
            }

            using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
            {
                Assert.NotEmpty(file.Header2.Users);
            }
        }
    }
}
