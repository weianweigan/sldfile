using SldFile;
using SldFile.Binary;

namespace SldFileTests;

public class ZlbParsingTests
{
    /// <summary>
    /// All sample files should have ZLB streams that decode successfully.
    /// </summary>
    [Theory]
    [InlineData(nameof(TestFileProvider.CubePart))]
    [InlineData(nameof(TestFileProvider.CylinderPart))]
    [InlineData(nameof(TestFileProvider.CubeDrawing))]
    [InlineData(nameof(TestFileProvider.Assembly))]
    public void Zlb_AllSampleFiles_ShouldHaveZlbStreams(string providerPropertyName)
    {
        var filePath = ResolveProviderPath(providerPropertyName);
        using var file = SldFile.SwFile.Open(filePath, readOnly: true);
        Assert.True(file.ZlbContents.Count > 0);
    }

    /// <summary>
    /// Streams whose decompressed content starts with &lt;?xml should be classified as XML.
    /// </summary>
    [Fact]
    public void Zlb_XmlStream_ShouldDecodeAsXml()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var xmlStreams = file.ZlbContents.Where(kvp => kvp.Value.IsXml).ToList();
        Assert.True(xmlStreams.Count > 0, "Expected at least one XML ZLB stream");

        foreach (var kvp in xmlStreams)
        {
            Assert.Null(kvp.Value.RawData);
            Assert.NotNull(kvp.Value.XmlContent);
            if (kvp.Value.DecompressedSize == 0)
                continue;
            Assert.NotEmpty(kvp.Value.XmlContent);
            Assert.Contains("<?xml", kvp.Value.XmlContent);
        }
    }

    /// <summary>
    /// Streams whose decompressed content does NOT start with &lt;?xml should be binary.
    /// </summary>
    [Fact]
    public void Zlb_BinaryStream_ShouldHaveRawData()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var binaryStreams = file.ZlbContents.Where(kvp => !kvp.Value.IsXml).ToList();
        Assert.True(binaryStreams.Count > 0, "Expected at least one binary ZLB stream");

        foreach (var kvp in binaryStreams)
        {
            Assert.True(string.IsNullOrEmpty(kvp.Value.XmlContent),
                $"Expected empty XmlContent for binary stream {kvp.Key}");
            if (kvp.Value.DecompressedSize > 0)
            {
                Assert.NotNull(kvp.Value.RawData);
                Assert.True(kvp.Value.RawData.Length > 0,
                    $"Expected non-empty RawData for {kvp.Key}");
            }
        }
    }

    /// <summary>
    /// The cube part should contain known ZLB stream paths.
    /// </summary>
    [Fact]
    public void Zlb_CubePart_ShouldContainExpectedStreams()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        Assert.True(file.ZlbContents.Count >= 5);

        var keys = file.ZlbContents.Keys.ToList();
        Assert.Contains(keys, k => k.EndsWith("swXmlContents/Features__ZLB"));
        Assert.Contains(keys, k => k.EndsWith("swXmlContents/KeyWords__ZLB"));
        Assert.Contains(keys, k => k.EndsWith("swXmlContents/Tables__ZLB"));
        Assert.Contains(keys, k => k.EndsWith("Contents/DisplayLists__ZLB"));
        Assert.Contains(keys, k => k.EndsWith("ThirdPty/Animator__ZLB"));
    }

    /// <summary>
    /// The Features XML should contain the SolidWorks schema namespace.
    /// </summary>
    [Fact]
    public void Zlb_FeaturesXml_ShouldContainSolidWorksSchema()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        var featuresKey = file.ZlbContents.Keys.FirstOrDefault(k => k.EndsWith("Features__ZLB"));
        Assert.NotNull(featuresKey);

        var xml = file.ZlbContents[featuresKey].XmlContent;
        Assert.Contains("solidworks", xml.ToLowerInvariant());
        Assert.Contains("<", xml);
    }

    /// <summary>
    /// Opening, saving, and re-opening should preserve both XML and binary ZLB streams.
    /// </summary>
    [Fact]
    public void Zlb_RoundTrip_ShouldPreserveContent()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);

        Dictionary<string, string> originalXml = new();
        Dictionary<string, byte[]> originalBinary = new();

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: false))
        {
            Assert.True(file.ZlbContents.Count > 0);

            foreach (var kvp in file.ZlbContents)
            {
                if (kvp.Value.IsXml)
                    originalXml[kvp.Key] = kvp.Value.XmlContent;
                else if (kvp.Value.RawData != null)
                    originalBinary[kvp.Key] = kvp.Value.RawData;
            }

            file.Save();
        }

        using (var file = SldFile.SwFile.Open(temp.FilePath, readOnly: true))
        {
            foreach (var kvp in originalXml)
            {
                Assert.True(file.ZlbContents.ContainsKey(kvp.Key),
                    $"Missing stream after roundtrip: {kvp.Key}");
                Assert.True(file.ZlbContents[kvp.Key].IsXml);
                Assert.Equal(kvp.Value, file.ZlbContents[kvp.Key].XmlContent);
            }

            foreach (var kvp in originalBinary)
            {
                Assert.True(file.ZlbContents.ContainsKey(kvp.Key),
                    $"Missing binary stream after roundtrip: {kvp.Key}");
                Assert.False(file.ZlbContents[kvp.Key].IsXml);
                Assert.NotNull(file.ZlbContents[kvp.Key].RawData);
                Assert.Equal(kvp.Value, file.ZlbContents[kvp.Key].RawData);
            }
        }
    }

    /// <summary>
    /// The prefix should start with the known ZLB GUID across all streams that have a full prefix.
    /// </summary>
    [Fact]
    public void Zlb_Prefix_ShouldStartWithKnownGuid()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        foreach (var kvp in file.ZlbContents)
        {
            var prefix = kvp.Value.Prefix;
            if (prefix == null || prefix.Length < 16)
                continue;

            for (int i = 0; i < 16; i++)
            {
                Assert.Equal(ZlbData.ExpectedGuid[i], prefix[i]);
            }
        }
    }

    /// <summary>
    /// For all ZLB streams, the DecompressedSize field should approximately
    /// match the actual content byte length.
    /// </summary>
    [Fact]
    public void Zlb_DecompressedSize_ShouldCorrelateWithContentLength()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CubePart, readOnly: true);

        foreach (var kvp in file.ZlbContents)
        {
            var zlb = kvp.Value;
            if (zlb.DecompressedSize == 0)
                continue;

            int actualBytes;
            if (zlb.IsXml)
                actualBytes = System.Text.Encoding.UTF8.GetByteCount(zlb.XmlContent);
            else if (zlb.RawData != null)
                actualBytes = zlb.RawData.Length;
            else
                continue;

            var declared = zlb.DecompressedSize;
            var diff = Math.Abs(actualBytes - declared);
            Assert.True(diff <= 4,
                $"DecompressedSize mismatch for {kvp.Key}: declared={declared}, actual={actualBytes}");
        }
    }

    /// <summary>
    /// ZlbContents dictionary should never be null.
    /// </summary>
    [Fact]
    public void Zlb_ZlbContents_ShouldNeverBeNull()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.CylinderPart, readOnly: true);
        Assert.NotNull(file.ZlbContents);
    }

    private static string ResolveProviderPath(string propertyName)
    {
        var prop = typeof(TestFileProvider).GetProperty(propertyName);
        Assert.NotNull(prop);

        var value = prop!.GetValue(null) as string;
        Assert.False(string.IsNullOrWhiteSpace(value));

        return value!;
    }
}
