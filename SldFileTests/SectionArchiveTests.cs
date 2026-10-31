using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SldFile;
using SldFile.Binary;

namespace SldFileTests;

public class SectionArchiveTests
{
    [Fact]
    public void NativeSavedPropertyFixtureHasConfirmedTypedValues()
    {
        using var file = SldFile.SwFile.Open(TestFileProvider.Resolve("Solidworks2026", "properties-native.SLDPRT"));
        Assert.Equal("中文 text", file.CustomProperties["验证文本"]);
        Assert.Equal(42, file.CustomProperties["验证整数"]);
        Assert.Equal(1.25, file.CustomProperties["验证小数"]);
        Assert.Equal(true, file.CustomProperties["验证布尔"]);
        Assert.Equal("默认", Assert.Single(file.Configurations).Name);
    }

    [Fact]
    public void Crc32MatchesPublishedCheckVector() =>
        Assert.Equal(0xcbf43926u, SectionArchiveDirectory.Crc32(Encoding.ASCII.GetBytes("123456789")));

    [Theory]
    [InlineData("cube.SLDPRT")]
    [InlineData("cylinder.SLDPRT")]
    [InlineData("assembly.SLDASM")]
    [InlineData("cube.SLDDRW")]
    public void IndexedPayloadsMatchSizesAndCrcAcrossNativeFixtures(string name)
    {
        var data = File.ReadAllBytes(TestFileProvider.Resolve("Solidworks2026", name));
        var index = SectionArchiveDirectory.Read(data, data[7]);
        Assert.NotEmpty(index.Entries);
        foreach (var entry in index.Entries)
        {
            var payload = index.Extract(data, entry);
            Assert.Equal(entry.Size, payload.Length);
            Assert.Equal(entry.Crc, SectionArchiveDirectory.Crc32(payload));
        }
    }

    [Fact]
    public void MatchingButIncorrectLocalAndCentralCrcIsRejected()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var index = SectionArchiveDirectory.Read(data, data[7]);
        var entry = index.Entries.Single(e => e.Name == "docProps/core.xml");
        data[entry.LocalOffset + 14] ^= 1;
        data[entry.CentralOffset + 16] ^= 1;
        Assert.Throws<InvalidDataException>(() => new SectionContainerReader(data).ExtractAllStreams());
    }

    [Fact]
    public void StaleDirectoryOffsetIsRejected()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var index = SectionArchiveDirectory.Read(data, data[7]);
        data[index.EndOffset + 16] ^= 1;
        Assert.Throws<InvalidDataException>(() => new SectionContainerReader(data).ExtractAllStreams());
    }

    [Fact]
    public void RewrittenArchiveIsReadableByIndependentZipReader()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var originalIndex = SectionArchiveDirectory.Read(data, data[7]);
        byte[] value = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><test>中文 " + new string('长', 500) + "</test>");
        var saved = new SectionContainerWriter(data, data[7]).Write(new() { ["docProps/core.xml"] = value });
        var savedIndex = SectionArchiveDirectory.Read(saved, saved[7]);
        Assert.NotEqual(originalIndex.DirectoryOffset, savedIndex.DirectoryOffset);
        // Restore only the transformed signatures, leaving all ZIP metadata from
        // the writer untouched. System.IO.Compression resolves offsets itself.
        var standard = saved[8..];
        foreach (var entry in savedIndex.Entries)
        {
            SectionArchiveDirectory.Put32(standard, entry.LocalOffset - 8, 0x04034b50u);
            SectionArchiveDirectory.Put32(standard, entry.CentralOffset - 8, 0x02014b50u);
        }
        SectionArchiveDirectory.Put32(standard, savedIndex.EndOffset - 8, 0x06054b50u);
        using var zip = new ZipArchive(new MemoryStream(standard), ZipArchiveMode.Read);
        Assert.Equal(originalIndex.Entries.Count, zip.Entries.Count);
        for (int i = 0; i < zip.Entries.Count; i++)
        {
            using var input = zip.Entries[i].Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            var original = originalIndex.Entries[i];
            Assert.Equal(original.Name == "docProps/core.xml" ? value : originalIndex.Extract(data, original), output.ToArray());
        }
    }

    [Fact]
    public void OpaqueGapsAndTrailingNoiseArePreserved()
    {
        var data = File.ReadAllBytes(TestFileProvider.CubePart2026);
        var border = SectionArchiveDirectory.Read(data, data[7]);
        var saved = new SectionContainerWriter(data, data[7]).Write(new() { ["docProps/core.xml"] = Encoding.UTF8.GetBytes("<changed/>") });
        var after = SectionArchiveDirectory.Read(saved, saved[7]);
        int oldCursor = 0, newCursor = 0;
        var oldEntries = border.Entries.OrderBy(e => e.LocalOffset).ToArray();
        var newEntries = after.Entries.OrderBy(e => e.LocalOffset).ToArray();
        for (int i = 0; i < oldEntries.Length; i++)
        {
            Assert.Equal(data[oldCursor..oldEntries[i].LocalOffset], saved[newCursor..newEntries[i].LocalOffset]);
            oldCursor = oldEntries[i].LocalOffset + oldEntries[i].HeaderLength + oldEntries[i].CompressedSize;
            newCursor = newEntries[i].LocalOffset + newEntries[i].HeaderLength + newEntries[i].CompressedSize;
        }
        Assert.Equal(data[oldCursor..border.DirectoryOffset], saved[newCursor..after.DirectoryOffset]);
        Assert.Equal(data[(border.EndOffset + 22)..], saved[(after.EndOffset + 22)..]);
    }

    [Fact]
    public void NewCustomPropertyUsesOrdinaryIdAndNativeTypeWithoutChangingExistingProperties()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart2026);
        var before = ReadCustom(temp.FilePath);
        using (var file = SldFile.SwFile.Open(temp.FilePath, false))
        {
            file.CustomProperties.Set("native-compatible", true);
            file.Save();
        }
        var after = ReadCustom(temp.FilePath);
        var added = after.Descendants("property").Single(e => (string?)e.Attribute("name") == "native-compatible");
        Assert.InRange((int)added.Attribute("pid")!, 2, 0xffffff);
        Assert.Equal("11", (string?)added.Attribute("TypeID"));
        Assert.Equal("Yes", added.Elements().Single().Value);
        foreach (var property in before.Descendants("property"))
        {
            var section = (string?)property.Parent!.Attribute("name");
            var preserved = after.Descendants("propertySection").Single(e => (string?)e.Attribute("name") == section)
                .Elements("property").Single(e => (string?)e.Attribute("pid") == (string?)property.Attribute("pid"));
            Assert.True(XNode.DeepEquals(property, preserved));
        }
    }

    [Fact]
    public void Utf8StreamNamesForVirtualComponentsAreSupported()
    {
        var data = (byte[])File.ReadAllBytes(TestFileProvider.CubePart2026).Clone();
        byte key = data[7];
        var index = SectionArchiveDirectory.Read(data, key);
        var entry = index.Entries.Single(e => e.Name == "docProps/custom.xml");

        // 构造等长的 UTF-8 中文流名称："VirtualComp/件.xml" (12 + 3 + 4 = 19 字节)
        string utf8Name = "VirtualComp/件.xml";
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(utf8Name);
        Assert.Equal(entry.Name.Length, utf8Bytes.Length);

        for (int i = 0; i < utf8Bytes.Length; i++)
        {
            byte encoded = SectionContainerReader.Ror(utf8Bytes[i], key);
            data[entry.LocalOffset + 30 + i] = encoded;
            data[entry.CentralOffset + 46 + i] = encoded;
        }

        var newIndex = SectionArchiveDirectory.Read(data, key);
        var utf8Entry = Assert.Single(newIndex.Entries, e => e.Name == utf8Name);
        var payload = newIndex.Extract(data, utf8Entry);
        Assert.NotEmpty(payload);
    }

    [Fact]
    public void InvalidUtf8OrControlCharsInStreamNameAreRejected()
    {
        var data = (byte[])File.ReadAllBytes(TestFileProvider.CubePart2026).Clone();
        byte key = data[7];
        var index = SectionArchiveDirectory.Read(data, key);
        var entry = index.Entries.Single(e => e.Name == "docProps/custom.xml");

        // 写入控制字符 (例如 '\t' = 9 < 32)
        byte controlEncoded = SectionContainerReader.Ror(9, key);
        var controlData = (byte[])data.Clone();
        controlData[entry.LocalOffset + 30] = controlEncoded;
        controlData[entry.CentralOffset + 46] = controlEncoded;
        Assert.Throws<InvalidDataException>(() => SectionArchiveDirectory.Read(controlData, key));

        // 写入非法 UTF-8 序列 (单字节 0xFF)
        byte invalidUtf8Encoded = SectionContainerReader.Ror(0xFF, key);
        var invalidData = (byte[])data.Clone();
        invalidData[entry.LocalOffset + 30] = invalidUtf8Encoded;
        invalidData[entry.CentralOffset + 46] = invalidUtf8Encoded;
        Assert.Throws<InvalidDataException>(() => SectionArchiveDirectory.Read(invalidData, key));
    }

    [Fact]
    public void NativeAssemblyWithVirtualComponentOpensSuccessfullyIfPresent()
    {
        string localAssembly = @"E:\01_Work\93_开拔\01_测试模型\轴承装配\20260531\装配体1.SLDASM";
        if (!File.Exists(localAssembly)) return;

        using var file = SldFile.SwFile.Open(localAssembly);
        Assert.NotNull(file);
        Assert.IsType<SldAssembly>(file);
        var components = file.GetAssemblyTree();
        Assert.NotEmpty(components);
        var virtualComp = Assert.Single(components, c => c.Name.Contains("虚拟装配体"));
        Assert.True(virtualComp.IsVirtual);
    }

    private static XDocument ReadCustom(string path) => XDocument.Parse(Encoding.UTF8.GetString(
        new SectionContainerReader(File.ReadAllBytes(path)).ExtractAllStreams()["docProps/custom.xml"]));
}
