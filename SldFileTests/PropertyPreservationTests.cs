using System.Text;
using SldFile;
using SldFile.Binary;
using SldFile.Ole;

namespace SldFileTests;

public class PropertyPreservationTests
{
    [Fact]
    public void EditingKnownPropertyPreservesUnknownTypesAndDuplicateDictionaryAliases()
    {
        var dictionary = DictionaryBytes((2u, "Title"), (2u, "标题"), (99u, "Unknown"));
        var opaque = new byte[] { 0x41, 0x10, 0, 0, 0xDE, 0xAD, 0xBE, 0xEF, 9, 8, 7, 6 };
        var original = BuildPropertySet(new Dictionary<uint, byte[]>
        {
            [0] = dictionary,
            [1] = new byte[] { 2, 0, 0, 0, 233, 253, 0, 0 },
            [2] = StringBytes("old title"),
            [99] = opaque
        });
        var editor = new PropertySetEditor(original);
        editor.Set(2, "更长的标题 🌏");
        var saved = editor.ToBytes();
        var values = ReadBlobs(saved);
        Assert.Equal(opaque, values[99]);
        Assert.Equal(Padded(dictionary), values[0]);
        Assert.Equal("更长的标题 🌏", new PropertySetEditor(saved).Get(2));
        Assert.Null(new PropertySetEditor(saved).Get(99));
    }

    [Fact]
    public void OpaqueDataOutsidePropertySectionsIsPreserved()
    {
        var original = BuildPropertySet(new Dictionary<uint, byte[]> { [2] = StringBytes("old") });
        var gap = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
        var tail = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 7, 6, 5, 4 };
        var bytes = original.Take(48).Concat(gap).Concat(original.Skip(48)).Concat(tail).ToArray();
        BitConverter.GetBytes(52).CopyTo(bytes, 44);
        var editor = new PropertySetEditor(bytes);
        editor.Set(2, "a much longer title");
        var saved = editor.ToBytes();
        Assert.Equal(gap, saved.AsSpan(48, gap.Length).ToArray());
        Assert.Equal(tail, saved.AsSpan(saved.Length - tail.Length).ToArray());
    }

    [Fact]
    public void AddingCustomSectionPreservesOriginalSectionAndSupportsUnicodeNames()
    {
        var original = BuildPropertySet(new Dictionary<uint, byte[]>
        {
            [0] = DictionaryBytes((2u, "Title")),
            [1] = new byte[] { 2, 0, 0, 0, 233, 253, 0, 0 },
            [2] = StringBytes("unchanged")
        });
        var changes = new PropertyCollection();
        changes.Set("材料 🌏", "钢材");
        var editor = new PropertySetEditor(original);
        editor.ApplyCustom(changes);
        var saved = editor.ToBytes();
        Assert.Equal(2, BitConverter.ToInt32(saved, 24));
        Assert.Equal(original.AsSpan(48).ToArray(), saved.AsSpan(68, original.Length - 48).ToArray());
        var loaded = new PropertyCollection();
        new PropertySetEditor(saved).ReadCustom(loaded);
        Assert.Equal("钢材", loaded["材料 🌏"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void MalformedSectionOffsetsAreRejected(int offset)
    {
        var original = BuildPropertySet(new Dictionary<uint, byte[]> { [2] = StringBytes("x") });
        BitConverter.GetBytes(offset).CopyTo(original, 44);
        Assert.Throws<InvalidDataException>(() => new PropertySetEditor(original));
    }

    [Fact]
    public void MetadataChangePreservesEveryUntouchedValueBlobInRealOleSample()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart);
        var original = DocumentApiTests.ReadStreams(temp.FilePath)["ISolidWorksInformation"];
        using (var document = SwFile.Open(temp.FilePath, readOnly: false))
        {
            document.Metadata.Title = "new title";
            document.Save();
        }
        var saved = DocumentApiTests.ReadStreams(temp.FilePath)["ISolidWorksInformation"];
        var oldBlobs = ReadBlobs(original);
        var newBlobs = ReadBlobs(saved);
        Assert.Equal(oldBlobs.Keys.OrderBy(id => id), newBlobs.Keys.OrderBy(id => id));
        foreach (var pair in oldBlobs)
            if (pair.Key != 11) Assert.Equal(pair.Value, newBlobs[pair.Key]);
    }

    [Fact]
    public void SectionContainerKeepsEveryUneditedLocalRecord()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart2026);
        var original = File.ReadAllBytes(temp.FilePath);
        var oldSections = SectionArchiveDirectory.Read(original, original[7]).Entries;
        using (var document = SwFile.Open(temp.FilePath, readOnly: false))
        {
            document.Metadata.Title = new string('长', 400);
            document.Save();
        }
        var saved = File.ReadAllBytes(temp.FilePath);
        var newSections = SectionArchiveDirectory.Read(saved, saved[7]).Entries;
        Assert.Equal(oldSections.Count, newSections.Count);
        for (int i = 0; i < oldSections.Count; i++)
        {
            var before = oldSections[i];
            var after = newSections[i];
            Assert.Equal(before.Name, after.Name);
            if (before.Name is "docProps/ISolidWorksInformation.xml" or "docProps/core.xml") continue;
            Assert.Equal(original.AsSpan(before.LocalOffset, before.HeaderLength + before.CompressedSize).ToArray(), saved.AsSpan(after.LocalOffset, after.HeaderLength + after.CompressedSize).ToArray());
        }
    }

    [Fact]
    public void ExternallyModifiedSectionContainerIsNotOverwritten()
    {
        using var temp = TestFileProvider.CreateTempCopy(TestFileProvider.CubePart2026);
        using var document = SwFile.Open(temp.FilePath, readOnly: false);
        document.Metadata.Title = "unsaved change";
        var external = File.ReadAllBytes(temp.FilePath);
        external[0] ^= 1;
        File.WriteAllBytes(temp.FilePath, external);
        Assert.Throws<IOException>(() => document.Save());
        Assert.Equal(external, File.ReadAllBytes(temp.FilePath));
    }

    private static Dictionary<uint, byte[]> ReadBlobs(byte[] bytes)
    {
        int section = BitConverter.ToInt32(bytes, 44);
        int size = BitConverter.ToInt32(bytes, section);
        int count = BitConverter.ToInt32(bytes, section + 4);
        var table = Enumerable.Range(0, count).Select(i =>
            (Id: BitConverter.ToUInt32(bytes, section + 8 + i * 8), Offset: BitConverter.ToInt32(bytes, section + 12 + i * 8))).ToArray();
        return table.ToDictionary(p => p.Id, p => bytes.AsSpan(section + p.Offset,
            table.Where(q => q.Offset > p.Offset).Select(q => q.Offset).DefaultIfEmpty(size).Min() - p.Offset).ToArray());
    }

    private static byte[] BuildPropertySet(Dictionary<uint, byte[]> values)
    {
        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result);
        writer.Write((ushort)0xFFFE);
        writer.Write((ushort)0);
        writer.Write(2);
        writer.Write(new byte[16]);
        writer.Write(1);
        writer.Write(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9").ToByteArray());
        writer.Write(48);
        int offset = 8 + 8 * values.Count;
        writer.Write(offset + values.Values.Sum(v => Padded(v).Length));
        writer.Write(values.Count);
        foreach (var value in values)
        {
            writer.Write(value.Key); writer.Write(offset);
            offset += Padded(value.Value).Length;
        }
        foreach (var value in values.Values) writer.Write(Padded(value));
        return result.ToArray();
    }

    private static byte[] DictionaryBytes(params (uint Id, string Name)[] names)
    {
        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result);
        writer.Write(names.Length);
        foreach (var name in names)
        {
            var bytes = Encoding.UTF8.GetBytes(name.Name + "\0");
            writer.Write(name.Id); writer.Write(bytes.Length); writer.Write(bytes);
        }
        return result.ToArray();
    }

    private static byte[] StringBytes(string text)
    {
        var value = Encoding.UTF8.GetBytes(text + "\0");
        return BitConverter.GetBytes(30).Concat(BitConverter.GetBytes(value.Length)).Concat(value).ToArray();
    }

    private static byte[] Padded(byte[] bytes)
    {
        var result = new byte[(bytes.Length + 3) & ~3];
        bytes.CopyTo(result, 0);
        return result;
    }
}
