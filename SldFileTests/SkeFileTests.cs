using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using SldFile;

namespace SldFileTests;

public class SkeFileTests
{
    private static string SkeDirectory => Path.Combine(AppContext.BaseDirectory, "TestFiles", "ske");

    public static IEnumerable<object[]> AllSkeFiles()
    {
        string dir = SkeDirectory;
        if (!Directory.Exists(dir))
        {
            // fallback to relative project root if needed
            dir = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles", "ske");
        }

        foreach (var file in Directory.GetFiles(dir, "*.ske"))
        {
            yield return new object[] { Path.GetFileName(file) };
        }
    }

    [Theory]
    [MemberData(nameof(AllSkeFiles))]
    public void All19NativeSkeFiles_LoadSuccessfully(string fileName)
    {
        string filePath = Path.Combine(SkeDirectory, fileName);
        var template = SketchTemplate.Open(filePath);

        Assert.NotNull(template);
        Assert.True(template.HeaderU32 > 0);
        Assert.True(template.FeatureCount > 0);
        Assert.NotNull(template.DimensionNames);
        Assert.NotNull(template.ParameterNames);
        Assert.NotNull(template.RawData);
        Assert.True(template.RawData.Length > 0);
    }

    [Theory]
    [MemberData(nameof(AllSkeFiles))]
    public void All19NativeSkeFiles_RoundTripPreservesStructure(string fileName)
    {
        string filePath = Path.Combine(SkeDirectory, fileName);
        byte[] originalBytes = File.ReadAllBytes(filePath);

        var template = SketchTemplate.Load(originalBytes);
        byte[] exportedBytes = template.ToByteArray();

        // Length should match original file
        Assert.Equal(originalBytes.Length, exportedBytes.Length);

        // Re-parsing exported bytes produces exact same metadata
        var reloaded = SketchTemplate.Load(exportedBytes);
        Assert.Equal(template.HeaderU32, reloaded.HeaderU32);
        Assert.Equal(template.FeatureCount, reloaded.FeatureCount);
        Assert.Equal(template.DimensionNames, reloaded.DimensionNames);
        Assert.Equal(template.ParameterNames, reloaded.ParameterNames);
    }

    [Fact]
    public void RenameDimension_ModifiesNameInExportedBytes()
    {
        string filePath = Path.Combine(SkeDirectory, "aCbore0.ske");
        var template = SketchTemplate.Open(filePath);

        Assert.NotEmpty(template.DimensionNames);
        string oldName = template.DimensionNames[0];
        string newName = "P" + (oldName.Length > 1 ? oldName.Substring(1) : "1");

        bool renamed = template.TryRenameDimension(oldName, newName);
        Assert.True(renamed);

        byte[] exported = template.ToByteArray();
        var reloaded = SketchTemplate.Load(exported);

        Assert.Contains(newName, reloaded.DimensionNames);
        Assert.DoesNotContain(oldName, reloaded.DimensionNames);
    }

    [Fact]
    public void RenameParameter_ModifiesNameInExportedBytes()
    {
        string filePath = Path.Combine(SkeDirectory, "aCbore0.ske");
        var template = SketchTemplate.Open(filePath);

        Assert.NotEmpty(template.ParameterNames);
        string oldName = template.ParameterNames[0];
        string newName = "Q" + (oldName.Length > 1 ? oldName.Substring(1) : "1");

        bool renamed = template.TryRenameParameter(oldName, newName);
        Assert.True(renamed);

        byte[] exported = template.ToByteArray();
        var reloaded = SketchTemplate.Load(exported);

        Assert.Contains(newName, reloaded.ParameterNames);
        Assert.DoesNotContain(oldName, reloaded.ParameterNames);
    }

    [Fact]
    public void StreamLoadAndSave_WorksIdentically()
    {
        string filePath = Path.Combine(SkeDirectory, "aHole0.ske");
        using var fs = File.OpenRead(filePath);
        var template = SketchTemplate.Load(fs);

        using var ms = new MemoryStream();
        template.Save(ms);
        byte[] exported = ms.ToArray();

        Assert.Equal(File.ReadAllBytes(filePath).Length, exported.Length);
    }
}
