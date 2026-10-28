using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using SldFile;
using SldFile.Sketches;

namespace SldFileTests;

/// <summary>
/// Independent oracle: native SW2026 SaveAs3 files and COM snapshots taken after reopening.
/// The oracle is deliberately separate from parser output; all coordinates use meters.
/// </summary>
public class SketchReadingTests
{
    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "TestFiles", "Sketches2026");

    public static IEnumerable<object[]> Samples()
    {
        using var truth = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, "truth.json")));
        foreach (var sample in truth.RootElement.GetProperty("samples").EnumerateArray())
            yield return new object[] { sample.GetProperty("file").GetString()! };
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void NativeFixtureMatchesRecordedComEvidence(string name)
    {
        using var truth = LoadTruth();
        var sample = FindSample(truth, name);
        var bytes = File.ReadAllBytes(Path.Combine(FixtureDirectory, name));
        Assert.Equal(sample.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void SketchContentMatchesReopenedSolidWorksCom(string name)
    {
        using var truth = LoadTruth();
        var expectedSketches = FindSample(truth, name).GetProperty("afterReopen").GetProperty("sketches");
        using var file = SwFile.Open(Path.Combine(FixtureDirectory, name));
        var actualSketches = file.GetSketches();
        Assert.Equal(expectedSketches.GetArrayLength(), actualSketches.Count);
        Assert.Equal(actualSketches.Select(s => s.Name), file.Sketches.Select(s => s.Name));

        foreach (var expected in expectedSketches.EnumerateArray())
        {
            var sketch = Assert.Single(actualSketches, s => s.Name == expected.GetProperty("name").GetString());
            Assert.Equal(0, sketch.ConfigurationIndex);
            Assert.Equal((bool?)expected.GetProperty("is3d").GetBoolean(), sketch.Is3D);
            Assert.False(string.IsNullOrEmpty(sketch.SourceStream));
            Assert.True(sketch.SourceOffset >= 0);
            Assert.NotEmpty(sketch.GetRawData());
            ReadOnly(sketch.Points);
            ReadOnly(sketch.Segments);
            ReadOnly(sketch.Relations);
            ReadOnly(sketch.Dimensions);
            ReadOnly(sketch.Parameters);
            ReadOnly(sketch.Diagnostics);

            // Serialized tables can retain hidden points that COM does not enumerate.
            // Every visible COM point must still resolve within its own sketch by ID.
            foreach (var point in expected.GetProperty("points").EnumerateArray())
                AssertPoint(point, Assert.Single(sketch.Points, p => p.Id == Id(point)));

            var expectedSegments = expected.GetProperty("segments");
            Assert.Equal(expectedSegments.GetArrayLength(), sketch.Segments.Count);
            foreach (var segment in expectedSegments.EnumerateArray())
            {
                var kind = (SketchSegmentKind)segment.GetProperty("type").GetInt32();
                var actual = Assert.Single(sketch.Segments, s => s.Id == Id(segment) && s.Kind == kind);
                Assert.Equal(kind, actual.Kind);
                Assert.Equal(segment.GetProperty("construction").GetBoolean(), actual.IsConstruction);
                AssertReference(segment, "start", actual.StartPoint, sketch);
                AssertReference(segment, "end", actual.EndPoint, sketch);
                AssertReference(segment, "center", actual.CenterPoint, sketch);
                AssertReference(segment, "major", actual.MajorPoint, sketch);
                AssertReference(segment, "minor", actual.MinorPoint, sketch);
                if (segment.TryGetProperty("radiusM", out var radius))
                {
                    Assert.NotNull(actual.Radius);
                    Close(radius.GetDouble(), actual.Radius.Value);
                }
            }

            Assert.Equal(
                expected.GetProperty("relations").EnumerateArray().Select(r => r.GetProperty("type").GetInt32()).OrderBy(t => t),
                sketch.Relations.Select(r => r.TypeCode).OrderBy(t => t));
            var dimensions = expected.GetProperty("dimensions");
            Assert.Equal(dimensions.GetArrayLength(), sketch.Dimensions.Count);
            // Names are not unique: the chamfer oracle exposes two D3 dimensions.
            var expectedDimensions = dimensions.EnumerateArray()
                .OrderBy(d => d.GetProperty("name").GetString()!.Split('@')[0])
                .ThenBy(d => d.GetProperty("value").GetDouble()).ToArray();
            var actualDimensions = sketch.Dimensions.OrderBy(d => d.Name).ThenBy(d => d.SystemValue).ToArray();
            for (var i = 0; i < expectedDimensions.Length; i++)
            {
                // Public dimensions are scoped to their owning sketch; COM adds @sketch to its display name.
                var dimension = expectedDimensions[i];
                var actual = actualDimensions[i];
                Assert.Equal(dimension.GetProperty("name").GetString()!.Split('@')[0], actual.Name);
                Assert.NotNull(actual.SystemValue);
                Close(dimension.GetProperty("value").GetDouble(), actual.SystemValue.Value);
            }
            if (expected.TryGetProperty("texts", out var texts))
                Assert.Equal(texts.EnumerateArray().Select(t => t.GetString()).OrderBy(t => t),
                    sketch.Segments.Where(s => s.Kind == SketchSegmentKind.Text).Select(s => s.Text).OrderBy(t => t));
            if (expected.TryGetProperty("blockInstanceCount", out var blocks) && blocks.GetInt32() != 0)
                Assert.True(sketch.IsPartial, "Nested block geometry must be reported as incomplete until decoded.");
        }
    }

    [Fact]
    public void ReturnedCollectionsAreReadOnlyAndRawDataIsADefensiveCopy()
    {
        using var file = SwFile.Open(Path.Combine(FixtureDirectory, "dimension10.SLDPRT"));
        var sketches = file.GetSketches();
        ReadOnly(sketches);
        var sketch = Assert.Single(sketches);
        ReadOnly(sketch.Points);
        ReadOnly(sketch.Segments);
        ReadOnly(sketch.Relations);
        ReadOnly(sketch.Dimensions);
        ReadOnly(sketch.Parameters);
        ReadOnly(sketch.Diagnostics);
        foreach (var relation in sketch.Relations) ReadOnly(relation.References);
        var original = sketch.GetRawData();
        var changed = sketch.GetRawData();
        Assert.NotSame(original, changed);
        Assert.NotEmpty(changed);
        changed[0] ^= 0xff;
        Assert.Equal(original, sketch.GetRawData());
    }

    [Fact]
    public void InvalidConfigurationAndDisposedAccessAreRejected()
    {
        var file = SwFile.Open(Path.Combine(FixtureDirectory, "two_sketches.SLDPRT"));
        using (file)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => file.GetSketches(-1));
            Assert.Equal(2, file.GetSketches().Count);
            var missingConfiguration = file.GetSketches(int.MaxValue);
            Assert.Empty(missingConfiguration);
            ReadOnly(missingConfiguration);
        }
        Assert.Throws<ObjectDisposedException>(() => file.GetSketches());
        Assert.Throws<ObjectDisposedException>(() => { _ = file.Sketches; });
    }

    [Fact]
    public void PointIdentifiersAreScopedToTheirOwningSketch()
    {
        using var file = SwFile.Open(Path.Combine(FixtureDirectory, "two_sketches.SLDPRT"));
        var sketches = file.GetSketches();
        Assert.Equal(2, sketches.Count);
        var first = sketches[0];
        var second = sketches[1];
        var commonIds = first.Points.Select(p => p.Id).Intersect(second.Points.Select(p => p.Id)).ToArray();
        Assert.NotEmpty(commonIds);
        foreach (var id in commonIds)
            Assert.NotSame(Assert.Single(first.Points, p => p.Id == id), Assert.Single(second.Points, p => p.Id == id));
        foreach (var sketch in sketches)
        foreach (var segment in sketch.Segments)
        foreach (var point in new[] { segment.StartPoint, segment.EndPoint, segment.CenterPoint, segment.MajorPoint, segment.MinorPoint })
            if (point is not null) Assert.Contains(sketch.Points, p => ReferenceEquals(p, point));
    }

    private static JsonDocument LoadTruth() => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureDirectory, "truth.json")));
    private static JsonElement FindSample(JsonDocument truth, string name) =>
        truth.RootElement.GetProperty("samples").EnumerateArray().Single(s => s.GetProperty("file").GetString() == name);

    private static uint Id(JsonElement entity) => entity.GetProperty("id")[1].GetUInt32();

    private static void AssertPoint(JsonElement expected, SketchPoint actual)
    {
        Assert.Equal(Id(expected), actual.Id);
        Close(expected.GetProperty("x").GetDouble(), actual.X);
        Close(expected.GetProperty("y").GetDouble(), actual.Y);
        Close(expected.GetProperty("z").GetDouble(), actual.Z);
    }

    private static void AssertReference(JsonElement expected, string property, SketchPoint? actual, SketchFeature owner)
    {
        if (!expected.TryGetProperty(property, out var point)) return;
        Assert.NotNull(actual);
        AssertPoint(point, actual);
        Assert.Same(Assert.Single(owner.Points, p => p.Id == Id(point)), actual);
    }

    private static void Close(double expected, double actual) =>
        Assert.True(double.IsFinite(actual) && Math.Abs(expected - actual) <= 1e-10,
            $"Expected {expected:R}, actual {actual:R} (tolerance 1e-10 m/rad).");

    private static void ReadOnly<T>(IReadOnlyList<T> collection)
    {
        Assert.False(collection is T[], "Public collections must not expose mutable arrays.");
        if (collection is IList<T> list)
        {
            Assert.True(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => list.Add(default!));
            if (list.Count > 0) Assert.Throws<NotSupportedException>(() => list[0] = list[0]);
        }
        if (collection is IList untyped) Assert.True(untyped.IsReadOnly);
    }
}
