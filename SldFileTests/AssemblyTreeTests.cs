using System.IO;
using System.Linq;
using System.Numerics;
using SldFile;
using Xunit;

namespace SldFileTests;

public class AssemblyTreeTests
{
    [Fact]
    public void Assembly_2012_GetAssemblyTree_ParsesComponents()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var components = file.GetAssemblyTree();

        Assert.NotNull(components);
        Assert.Equal(2, components.Count);

        var cube = components.FirstOrDefault(c => c.Name == "cube");
        Assert.NotNull(cube);
        Assert.Equal("cube", cube!.Name);
        Assert.Equal("cube", cube.InstanceName);
        Assert.Equal(1, cube.ReferenceNumber);
        Assert.False(cube.IsSuppressed);
        Assert.False(cube.IsVirtual);
        Assert.Contains("cube.SLDPRT", cube.FilePath);
        Assert.Equal(Matrix4x4.Identity, cube.Transform);

        var cylinder = components.FirstOrDefault(c => c.Name == "cylinder");
        Assert.NotNull(cylinder);
        Assert.Equal("cylinder", cylinder!.Name);
        Assert.Equal("cylinder", cylinder.InstanceName);
        Assert.Contains("cylinder.SLDPRT", cylinder.FilePath);
        // cylinder is translated along X by -0.01m (-10mm)
        Assert.Equal(-0.01f, cylinder.Transform.M41, precision: 4);
    }

    [Fact]
    public void Assembly_2012_GetBom_AggregatesCorrectly()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly, readOnly: true);
        var bom = file.GetBom();

        Assert.NotNull(bom);
        Assert.Equal(2, bom.Count);

        var cubeItem = bom.FirstOrDefault(i => i.PartNumber.Equals("cube", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cubeItem);
        Assert.Equal(1, cubeItem!.Quantity);
        Assert.Contains("cube.SLDPRT", cubeItem.FilePath);

        var cylinderItem = bom.FirstOrDefault(i => i.PartNumber.Equals("cylinder", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cylinderItem);
        Assert.Equal(1, cylinderItem!.Quantity);
        Assert.Contains("cylinder.SLDPRT", cylinderItem.FilePath);
    }

    [Fact]
    public void Assembly_2026_GetAssemblyTree_ParsesComponents()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly2026, readOnly: true);
        var components = file.GetAssemblyTree();

        Assert.NotNull(components);
        Assert.Equal(2, components.Count);

        var names = components.Select(c => c.Name).ToList();
        Assert.Contains("cube", names);
        Assert.Contains("cylinder", names);
    }

    [Fact]
    public void Assembly_2026_GetBom_AggregatesCorrectly()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly2026, readOnly: true);
        var bom = file.GetBom();

        Assert.NotNull(bom);
        Assert.Equal(2, bom.Count);
        Assert.All(bom, item => Assert.Equal(1, item.Quantity));
    }

    [Fact]
    public void NonAssemblyFile_ReturnsEmptyTreeAndBom()
    {
        using var file = SwFile.Open(TestFileProvider.CubePart, readOnly: true);
        var tree = file.GetAssemblyTree();
        var bom = file.GetBom();

        Assert.Empty(tree);
        Assert.Empty(bom);
    }
}
