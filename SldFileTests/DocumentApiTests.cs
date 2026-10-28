using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using SldFile;
using SldFile.Binary;
using SldFile.Diagnostics;
using SldFile.Ole;
using SldFile.Sketches;

namespace SldFileTests;

public class DocumentApiTests
{
    public static IEnumerable<object[]> Samples()
    {
        foreach (var version in new[] { "Solidworks2012", "Solidworks2026" })
        foreach (var name in new[] { "cube.SLDPRT", "cylinder.SLDPRT", "assembly.SLDASM", "cube.SLDDRW" })
            yield return new object[] { TestFileProvider.Resolve(version, name) };
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void SavingUneditedDocumentPreservesEveryByte(string source)
    {
        using var temp = TestFileProvider.CreateTempCopy(source);
        var before = File.ReadAllBytes(temp.FilePath);
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            file.Save();
            file.Save();
        }
        Assert.Equal(before, File.ReadAllBytes(temp.FilePath));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void MetadataEditsPersistAndPreserveUnrelatedStreams(string source)
    {
        using var temp = TestFileProvider.CreateTempCopy(source);
        var before = ReadStreams(temp.FilePath);
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            file.Metadata.Title = "修改后的标题 🌏 " + new string('长', 300);
            file.Metadata.Author = "测试作者";
            file.Metadata.Subject = "测试主题";
            file.Metadata.Keywords = "中文; test";
            file.Metadata.Comments = "第一行\n第二行";
            file.Metadata.LastSavedBy = "测试用户";
            file.Metadata.Company = "测试公司";
            file.Metadata.Manager = "测试经理";
            file.Metadata.Category = "零件分类";
            file.Save();
            file.Save();
            file.Metadata.Title = "第二次保存";
            file.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath);
        Assert.Equal("第二次保存", reopened.Metadata.Title);
        Assert.Equal("测试作者", reopened.Metadata.Author);
        Assert.Equal("测试主题", reopened.Metadata.Subject);
        Assert.Equal("中文; test", reopened.Metadata.Keywords);
        Assert.Equal("第一行\n第二行", reopened.Metadata.Comments);
        Assert.Equal("测试用户", reopened.Metadata.LastSavedBy);
        Assert.Equal("测试公司", reopened.Metadata.Company);
        Assert.Equal("测试经理", reopened.Metadata.Manager);
        Assert.Equal("零件分类", reopened.Metadata.Category);
        var after = ReadStreams(temp.FilePath);
        AssertStreamsPreserved(before, after, "ISolidWorksInformation", "\u0005SummaryInformation", "\u0005DocumentSummaryInformation",
            "docProps/ISolidWorksInformation.xml", "docProps/core.xml", "docProps/app.xml");

        if (after.TryGetValue("docProps/core.xml", out var core))
        {
            using var input = new MemoryStream(core);
            var document = XDocument.Load(input);
            Assert.Equal("第二次保存", document.Descendants().Single(e => e.Name.LocalName == "title").Value);
        }
        else
        {
            var properties = OlePropertySetSerializer.Parse(after["\u0005SummaryInformation"]);
            Assert.Equal("第二次保存", properties.Properties.Single(p => p.Id == 2).Value);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void CustomAndConfigurationPropertiesSupportTypedValuesAndRemoval(string source)
    {
        using var temp = TestFileProvider.CreateTempCopy(source);
        var before = ReadStreams(temp.FilePath);
        var date = new DateTime(2020, 6, 7, 8, 9, 10, DateTimeKind.Utc);
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            if (file.Kind == DocumentKind.Drawing) Assert.Empty(file.Configurations);
            else Assert.NotEmpty(file.Configurations);
            foreach (var properties in AllProperties(file))
            {
                properties.Set("验证文本", "中文 🌏\n  保留空格  ");
                properties.Set("验证整数", 42);
                properties.Set("验证数值", 1.2345);
                properties.Set("验证开关", true);
                properties.Set("验证日期", date);
                properties.Set("待删除", "delete me");
            }
            file.Save();
        }
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            foreach (var properties in AllProperties(file))
            {
                Assert.Equal("中文 🌏\n  保留空格  ", properties["验证文本"]);
                Assert.Equal(42, properties["验证整数"]);
                Assert.Equal(1.2345, properties["验证数值"]);
                Assert.Equal(true, properties["验证开关"]);
                Assert.Equal(date, properties["验证日期"]);
                properties.Set("验证文本", "短");
                properties.Set("验证整数", "changed type");
                Assert.True(properties.Remove("待删除"));
            }
            file.Save();
        }
        using (var file = SwFile.Open(temp.FilePath))
        {
            foreach (var properties in AllProperties(file))
            {
                Assert.Equal("短", properties["验证文本"]);
                Assert.Equal("changed type", properties["验证整数"]);
                Assert.False(properties.ContainsKey("待删除"));
            }
        }
        var editedPaths = before.Keys.Where(p => p.Contains("-Properties") || p == "\u0005DocumentSummaryInformation" || p == "docProps/custom.xml").ToArray();
        AssertStreamsPreserved(before, ReadStreams(temp.FilePath), editedPaths);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void PublicDataIsReadableAndReadOnlyOpenCannotSave(string source)
    {
        var original = File.ReadAllBytes(source);
        using var file = SwFile.Open(source);
        Assert.True(file.IsReadOnly);
        Assert.NotEmpty(file.Users);
        Assert.NotEmpty(file.Revisions);
        Assert.NotNull(file.GetPreviewPng());
        Assert.NotNull(file.Metadata.Author);
        Assert.Contains(file.Kind, new[] { DocumentKind.Part, DocumentKind.Assembly, DocumentKind.Drawing });
        Assert.Throws<SldFileException>(() => file.Save());
        Assert.Equal(original, File.ReadAllBytes(source));
        var preview = file.GetPreviewPng()!;
        preview[0] ^= 0xFF;
        Assert.NotEqual(preview[0], file.GetPreviewPng()![0]);
    }

    [Fact]
    public void ConfirmedSampleMetadataIsAvailableInBothContainers()
    {
        foreach (var source in new[] { TestFileProvider.CubePart, TestFileProvider.CubePart2026 })
        {
            using var file = SwFile.Open(source);
            Assert.Equal("weigan", file.Metadata.Author);
            Assert.Equal("sldfile title", file.Metadata.Title);
            Assert.Equal("sldfile subject", file.Metadata.Subject);
            Assert.Equal("remark from sldfile", file.Metadata.Comments);
            Assert.Equal("sldfile", file.Metadata.Keywords);
            Assert.NotEmpty(file.References);
            if (source == TestFileProvider.CubePart) Assert.NotEmpty(file.Features);
            Assert.NotNull(file.Metadata.CreatedAt);
        }
    }

    [Fact]
    public void PublicSurfaceDoesNotExposeFormatImplementation()
    {
        var assembly = typeof(SwFile).Assembly;
        Assert.DoesNotContain(assembly.GetExportedTypes(), t => t.Namespace is "SldFile.Binary" or "SldFile.Mfc" or "SldFile.Ole");
        var names = typeof(SwFile).GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Configurations", "CustomProperties", "DrawingSheetNames", "Features", "FilePath", "IsReadOnly", "Kind", "Metadata", "References", "Revisions", "Sketches", "Users" }, names);
        Assert.All(new[] { typeof(Feature), typeof(DocumentReference), typeof(Revision), typeof(SketchFeature), typeof(SketchPoint), typeof(SketchSegment), typeof(SketchRelation), typeof(SketchDimension), typeof(SketchEntityReference) }, type =>
            Assert.All(type.GetProperties(), property => Assert.Null(property.GetSetMethod())));
    }

    [Fact]
    public void DisposedDocumentsRejectSaveAndDisposeIsIdempotent()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);
        var file = SwFile.Open(temp.FilePath, readOnly: false);
        file.Dispose();
        file.Dispose();
        Assert.Throws<ObjectDisposedException>(() => file.Save());
    }

    [Fact]
    public void UnsupportedPropertyValuesAreRejectedBeforeSaving()
    {
        var properties = new PropertyCollection();
        Assert.Throws<ArgumentException>(() => properties.Set("", "value"));
        Assert.Throws<ArgumentException>(() => properties.Set("x", new object()));
        Assert.Throws<ArgumentException>(() => properties.Set("x", double.NaN));
        Assert.Throws<ArgumentException>(() => properties.Set("x", null!));
        Assert.Empty(properties);
    }

    [Fact]
    public void SketchTemplatesExposeConfirmedNames()
    {
        var file = SketchTemplate.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles", "ske", "Wzscbord.ske"));
        Assert.Equal(new[] { "Hole Ø" }, file.DimensionNames);
        Assert.Equal(new[] { "Drill Angle" }, file.ParameterNames);
    }

    [Fact]
    public void EvidenceFixturesMatchRecordedHashes()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles");
        var entries = JsonSerializer.Deserialize<FixtureHash[]>(File.ReadAllText(Path.Combine(root, "manifest.json")))!;
        Assert.Equal(28, entries.Length);
        foreach (var entry in entries)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Equal(entry.Sha256, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    public sealed class FixtureHash
    {
        public string Path { get; set; } = "";
        public string Sha256 { get; set; } = "";
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void LongUnicodeTextAndClearingRoundTrip(string source)
    {
        using var temp = TestFileProvider.CreateTempCopy(source);
        string title = new string('长', 300) + "🌏\r\n";
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            file.Metadata.Title = title;
            file.Save();
        }
        using (var file = SwFile.Open(temp.FilePath, readOnly: false))
        {
            Assert.Equal(title, file.Metadata.Title);
            file.Metadata.Title = null;
            file.Save();
        }
        using var reopened = SwFile.Open(temp.FilePath);
        Assert.Equal("", reopened.Metadata.Title);
    }

    [Fact]
    public void LinkedCustomPropertyEditsAreRejectedWithoutChangingFile()
    {
        foreach (var source in new[] { TestFileProvider.CubePart, TestFileProvider.CubePart2026 })
        {
            using var temp = TestFileProvider.CreateTempCopy(source);
            var original = File.ReadAllBytes(temp.FilePath);
            using (var file = SwFile.Open(temp.FilePath, readOnly: false))
            {
                file.Metadata.Title = "must not save either";
                file.CustomProperties.Set("Weight", "invalid cached replacement");
                Assert.Throws<NotSupportedException>(() => file.Save());
            }
            Assert.Equal(original, File.ReadAllBytes(temp.FilePath));
        }
    }

    [Fact]
    public void ConfirmedFeatureNamesAndReferencePathAreStable()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart);
        Assert.Equal(new[] { "Administrator", "Gateway", "qrm", "cadworks", "qhu", "buildusergr", "owu1", "yzou", "weigan" }, file.Users);
        Assert.Contains(file.Features, f => f.Name == "前视基准面" && f.Kind == FeatureKind.ReferencePlane);
        Assert.Contains(file.Features, f => f.Name == "草图1" && f.Kind == FeatureKind.Sketch);
        Assert.Contains(file.Features, f => f.Name == "凸台-拉伸1" && f.Kind == FeatureKind.Extrusion);
        var reference = Assert.Single(file.References);
        Assert.Equal(@"C:\Users\weigan\Desktop\cube.SLDPRT", reference.FilePath);
        Assert.Equal("cube", reference.Name);
    }

    private static IEnumerable<PropertyCollection> AllProperties(SwFile file)
    {
        yield return file.CustomProperties;
        foreach (var configuration in file.Configurations) yield return configuration.CustomProperties;
    }

    internal static Dictionary<string, byte[]> ReadStreams(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (SectionContainerReader.IsSectionContainer(bytes)) return new SectionContainerReader(bytes).ExtractAllStreams();
        return StorageStreams.ReadFile(path);
    }

    private static void AssertStreamsPreserved(Dictionary<string, byte[]> before, Dictionary<string, byte[]> after, params string[] changed)
    {
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        foreach (var pair in before)
            if (!changed.Contains(pair.Key)) Assert.True(pair.Value.SequenceEqual(after[pair.Key]), $"Unexpected change to {pair.Key}");
    }
}
