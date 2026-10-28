using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
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
        var before = SectionArchiveDirectory.Read(data, data[7]);
        var saved = new SectionContainerWriter(data, data[7]).Write(new() { ["docProps/core.xml"] = Encoding.UTF8.GetBytes("<changed/>") });
        var after = SectionArchiveDirectory.Read(saved, saved[7]);
        int oldCursor = 0, newCursor = 0;
        var oldEntries = before.Entries.OrderBy(e => e.LocalOffset).ToArray();
        var newEntries = after.Entries.OrderBy(e => e.LocalOffset).ToArray();
        for (int i = 0; i < oldEntries.Length; i++)
        {
            Assert.Equal(data[oldCursor..oldEntries[i].LocalOffset], saved[newCursor..newEntries[i].LocalOffset]);
            oldCursor = oldEntries[i].LocalOffset + oldEntries[i].HeaderLength + oldEntries[i].CompressedSize;
            newCursor = newEntries[i].LocalOffset + newEntries[i].HeaderLength + newEntries[i].CompressedSize;
        }
        Assert.Equal(data[oldCursor..before.DirectoryOffset], saved[newCursor..after.DirectoryOffset]);
        Assert.Equal(data[(before.EndOffset + 22)..], saved[(after.EndOffset + 22)..]);
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

    private static XDocument ReadCustom(string path) => XDocument.Parse(Encoding.UTF8.GetString(
        new SectionContainerReader(File.ReadAllBytes(path)).ExtractAllStreams()["docProps/custom.xml"]));
}
