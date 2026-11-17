using System.IO;
using System.Linq;
using System.Numerics;
using SldFile;
using Xunit;

namespace SldFileTests;

public class AssemblyTreeTests
{
    [Fact]
    public void StitchAssembly_WithMissingLeaf_DoesNotReturnPartialMesh()
    {
        using var file = SwFile.Open(TestFileProvider.Assembly);
        var xml = System.Xml.Linq.XDocument.Parse(file.CompInstanceTreeXml!);
        var records = xml.Descendants().Where(e => e.Name.LocalName == "swFile").ToList();
        foreach (var record in records)
        {
            var path = record.Attribute("swPath");
            if (path == null) continue;
            if (path.Value.EndsWith("cube.SLDPRT", System.StringComparison.OrdinalIgnoreCase))
                path.Value = TestFileProvider.CubePart;
            else if (path.Value.EndsWith("cylinder.SLDPRT", System.StringComparison.OrdinalIgnoreCase))
                path.Value = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid() + ".SLDPRT");
        }
        file.CompInstanceTreeXml = xml.ToString();
        Assert.Contains(file.GetAssemblyTree(), c => File.Exists(c.FilePath));
        Assert.Contains(file.GetAssemblyTree(), c => !File.Exists(c.FilePath));
        Assert.Null(file.StitchAssemblyMeshFromComponents());
    }

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

    [Fact]
    public void ParseAssemblyTree_NestedSubAssemblies_AccumulatesWorldTransformCorrectly()
    {
        // Root ASM contains SubAsm (translated X=10)
        // SubAsm contains PartA (translated Y=5)
        // In top-level world coordinates, PartA should be at (X=10, Y=5)
        string xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<swAssemblyTree>
  <swHeader>
    <swFile id=""fRoot"" swDocType=""ASSEMBLY"" swPath=""Root.SLDASM"" />
    <swFile id=""fSub"" swDocType=""ASSEMBLY"" swPath=""Sub.SLDASM"" />
    <swFile id=""fPart"" swDocType=""PART"" swPath=""PartA.SLDPRT"" />
  </swHeader>
  <swModelList>
    <swModel id=""mRoot"" swFileRef=""fRoot"" swConfigurationName=""Default"">
      <swReference swID=""refSub1"" swName=""Sub"" swReferenceNumber=""1"" swModelRef=""mSub""
        swTransform=""1 0 0 0  0 1 0 0  0 0 1 0  10 0 0 1"" />
    </swModel>
    <swModel id=""mSub"" swFileRef=""fSub"" swConfigurationName=""Default"">
      <swReference swID=""refPart1"" swName=""PartA"" swReferenceNumber=""1"" swModelRef=""mPart""
        swTransform=""1 0 0 0  0 1 0 0  0 0 1 0  0 5 0 1"" />
    </swModel>
    <swModel id=""mPart"" swFileRef=""fPart"" swConfigurationName=""Default"" />
  </swModelList>
</swAssemblyTree>";

        var nodes = SldFile.Assembly.AssemblyTreeReader.ParseAssemblyTree(xml);
        Assert.Single(nodes);

        var subNode = nodes[0];
        Assert.Equal("Sub", subNode.Name);
        Assert.Equal(10f, subNode.Transform.M41);
        Assert.Equal(0f, subNode.Transform.M42);
        Assert.Equal(10f, subNode.LocalTransform.M41);

        Assert.Single(subNode.Children);
        var partNode = subNode.Children[0];
        Assert.Equal("PartA", partNode.Name);
        // Local transform should remain Y=5
        Assert.Equal(0f, partNode.LocalTransform.M41);
        Assert.Equal(5f, partNode.LocalTransform.M42);

        // Accumulated World transform must be X=10, Y=5
        Assert.Equal(10f, partNode.Transform.M41);
        Assert.Equal(5f, partNode.Transform.M42);
    }
}
