using SldFile.Binary;

namespace SldFileTests;

public class SkeFileReaderTest
{
    private static string SkeDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "ske");

    private static string GetSkePath(string filename) =>
        Path.Combine(SkeDir, filename);

    [Fact]
    public void CanParseAllSkeFiles()
    {
        var files = Directory.GetFiles(SkeDir, "*.ske");
        Assert.True(files.Length >= 19, $"Expected at least 19 .ske files, found {files.Length}");

        foreach (var file in files)
        {
            var data = File.ReadAllBytes(file);
            var reader = new SkeFileReader();
            var model = reader.Parse(data);

            Assert.True(model.HeaderU32 > 0, $"{Path.GetFileName(file)}: header should be non-zero");
            Assert.True(model.FeatureCount > 0, $"{Path.GetFileName(file)}: feature count should be non-zero");
            Assert.NotEmpty(model.FeatureData);
            Assert.NotNull(model.Sketch);
            Assert.Equal("sgSketch", model.Sketch!.ClassName);
        }
    }

    [Theory]
    [InlineData("Wzscbord.ske", 1127u, 10u, new[] { "Hole Ø" }, new[] { "Drill Angle" })]
    [InlineData("Wzscbore.ske", 1127u, 9u, new[] { "Diameter" }, new string[0])]
    [InlineData("aHole0.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aHole1.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCbore0.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCbore1.ske", 1137u, 19u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCsink0.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCsink1.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aHoleblnd0.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aHoleblnd1.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCborblnd0.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCborblnd1.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aCsinblnd0.ske", 1137u, 15u, new[] { "D1" }, new[] { "D7" })]
    [InlineData("aptapblnd0.ske", 1137u, 16u, new[] { "D1" }, new[] { "D9" })]
    [InlineData("aptapblnd1.ske", 1137u, 16u, new[] { "D1" }, new[] { "D9" })]
    // Stud files — currently only "D2" is extracted; remaining dim names are in
    // 50B dim_chain blocks whose raw segment data is not yet fully mapped.
    [InlineData("studAdd1.ske", 14164u, 12u, new[] { "D2" }, new[] { "C-Sink Angle" })]
    [InlineData("studAdd2.ske", 14164u, 12u, new[] { "D2" }, new[] { "C-Sink Angle" })]
    [InlineData("studRemove1.ske", 14164u, 15u, new[] { "D2" }, new[] { "C-Sink Angle" })]
    [InlineData("studRemove2.ske", 14164u, 14u, new[] { "D2" }, new[] { "C-Sink Angle" })]
    public void ParseSkeFile_ReturnsCorrectMetadata(
        string filename, uint expectedHeader, uint expectedFeatureCount,
        string[] expectedDimNames, string[] expectedParamNames)
    {
        var path = GetSkePath(filename);
        Assert.True(File.Exists(path), $"Test file not found: {path}");

        var data = File.ReadAllBytes(path);
        var reader = new SkeFileReader();
        var model = reader.Parse(data);

        Assert.Equal(expectedHeader, model.HeaderU32);
        Assert.Equal(expectedFeatureCount, model.FeatureCount);
        Assert.Equal(expectedDimNames, model.DimensionNames);
        Assert.Equal(expectedParamNames, model.ParameterNames);
    }

    [Fact]
    public void ParseSkeFile_HasRequiredFeatureDataTypes()
    {
        var path = GetSkePath("aHole0.ske");
        var data = File.ReadAllBytes(path);
        var reader = new SkeFileReader();
        var model = reader.Parse(data);

        // Verify all essential feature data types are present
        Assert.NotNull(model.Sketch);
        Assert.NotNull(model.FindFirst<LineHandleData>());
        Assert.NotNull(model.FindFirst<PntLineDistData>());
        Assert.NotNull(model.FindFirst<NodeNameData>());
        Assert.NotNull(model.FindFirst<ObListData>());
        Assert.NotNull(model.FindFirst<SkDimHandleValG2Data>());
    }

    [Fact]
    public void ParseSkeFile_RoundtripPreservesData()
    {
        var path = GetSkePath("Wzscbord.ske");
        var data = File.ReadAllBytes(path);

        var reader = new SkeFileReader();
        var model = reader.Parse(data);

        var writer = new SkeFileWriter();
        var serialized = writer.Serialize(model);

        // Size should be preserved
        Assert.Equal(data.Length, serialized.Length);

        // Re-parse the serialized output
        var model2 = reader.Parse(serialized);

        Assert.Equal(model.HeaderU32, model2.HeaderU32);
        Assert.Equal(model.FeatureCount, model2.FeatureCount);
        Assert.Equal(model.DimensionNames, model2.DimensionNames);
        Assert.Equal(model.ParameterNames, model2.ParameterNames);
        Assert.Equal(model.FeatureData.Count, model2.FeatureData.Count);
    }
}
