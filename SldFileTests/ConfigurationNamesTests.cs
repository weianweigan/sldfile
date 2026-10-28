using System.Text;
using SldFile;
using SldFile.Binary;

namespace SldFileTests;

public class ConfigurationNamesTests
{
    private static Dictionary<string, byte[]> Streams(string contents) => new()
    {
        ["swXmlContents/Features"] = Encoding.UTF8.GetBytes(
            "<swSolidWorks xmlns=\"http://www.solidworks.com/sw2003/schema\">" + contents + "</swSolidWorks>")
    };

    [Fact]
    public void MapsUnicodeNamesByStoredIdInsteadOfOrderOrObjectId()
    {
        var names = ConfigurationNames.Read(Streams("""
            <swConfigurationList>
              <swConfiguration id="99" swID="7" swName="长配置 🌏 &amp; 精密"/>
              <swConfiguration id="7" swID="0" swName="默认"/>
            </swConfigurationList>
            <swModelList><swModel swConfigurationId="7" swConfigurationName="长配置 🌏 &amp; 精密"/></swModelList>
            """));
        Assert.Equal("默认", names[0]);
        Assert.Equal("长配置 🌏 & 精密", names[7]);
        Assert.False(names.ContainsKey(99));
    }

    [Fact]
    public void MissingManifestDoesNotInventNames()
    {
        Assert.Empty(ConfigurationNames.Read(new Dictionary<string, byte[]>()));
    }

    [Theory]
    [InlineData("<swConfiguration swID=\"0\"/>")]
    [InlineData("<swConfiguration swID=\"0\" swName=\"\"/>")]
    [InlineData("<swConfiguration swID=\"0\" swName=\"A\"/><swConfiguration swID=\"0\" swName=\"B\"/>")]
    [InlineData("<swConfiguration swID=\"0\" swName=\"A\"/><swConfiguration swID=\"0\" swName=\"B\"/><swConfiguration swID=\"0\" swName=\"A\"/>")]
    public void MissingOrConflictingNamesRemainUnavailable(string declarations)
    {
        var names = ConfigurationNames.Read(Streams("<swConfigurationList>" + declarations + "</swConfigurationList>"));
        Assert.Null(names[0]);
    }

    [Fact]
    public void RejectsConflictingModelCopyWithoutAffectingOtherConfigurations()
    {
        var names = ConfigurationNames.Read(Streams("""
            <swConfigurationList>
              <swConfiguration swID="0" swName="默认"/>
              <swConfiguration swID="2" swName="副本"/>
            </swConfigurationList>
            <swModelList><swModel swConfigurationId="0" swConfigurationName="旧名"/></swModelList>
            """));
        Assert.Null(names[0]);
        Assert.Equal("副本", names[2]);
    }

    [Fact]
    public void IgnoresInvalidIdsForeignNamespacesAndUnrelatedNestedElements()
    {
        var names = ConfigurationNames.Read(Streams("""
            <swConfigurationList>
              <swConfiguration swID="-1" swName="negative"/>
              <swConfiguration swID="2147483648" swName="overflow"/>
              <swConfiguration swID=" 0" swName="whitespace"/>
              <swConfiguration swID="abc" swName="text"/>
              <swConfiguration xmlns="foreign" swID="1" swName="foreign"/>
            </swConfigurationList>
            <unrelated><swConfigurationList><swConfiguration swID="3" swName="nested"/></swConfigurationList></unrelated>
            <swModelList><swModel swConfigurationId="4" swConfigurationName="model only"/></swModelList>
            """));
        Assert.Empty(names);
    }

    [Theory]
    [InlineData("<broken>")]
    [InlineData("<!DOCTYPE swSolidWorks [<!ENTITY x SYSTEM 'file:///unavailable'>]><swSolidWorks>&x;</swSolidWorks>")]
    public void RejectsMalformedXmlAndDtd(string xml)
    {
        var streams = new Dictionary<string, byte[]> { ["swXmlContents/Features"] = Encoding.UTF8.GetBytes(xml) };
        Assert.Throws<InvalidDataException>(() => ConfigurationNames.Read(streams));
    }

    [Fact]
    public void MetadataStoreJoinsNamesToPropertySourcesByIndex()
    {
        var streams = Streams("""
            <swConfigurationList><swConfiguration swID="7" swName="七号"/><swConfiguration swID="1" swName="一号"/></swConfigurationList>
            """);
        var emptyProperties = Encoding.UTF8.GetBytes("<Properties/>");
        streams.Add("docProps/Config-7-Properties.xml", emptyProperties);
        streams.Add("docProps/Config-0-Properties.xml", emptyProperties);
        streams.Add("docProps/Config-1-Properties.xml", emptyProperties);
        var configurations = new MetadataStore(streams).ReadConfigurations();
        Assert.Equal(new[] { 0, 1, 7 }, configurations.Select(c => c.Index));
        Assert.Null(configurations[0].Name);
        Assert.Equal("一号", configurations[1].Name);
        Assert.Equal("七号", configurations[2].Name);
        Assert.Null(typeof(Configuration).GetProperty(nameof(Configuration.Name))!.SetMethod);
    }

    [Theory]
    [InlineData("cube.SLDPRT")]
    [InlineData("cylinder.SLDPRT")]
    [InlineData("assembly.SLDASM")]
    public void Existing2026DocumentsExposeRecordedConfigurationNames(string name)
    {
        var source = TestFileProvider.Resolve("Solidworks2026", name);
        var streams = new SectionContainerReader(File.ReadAllBytes(source)).ExtractAllStreams();
        using var file = SwFile.Open(source);
        if (!streams.TryGetValue("swXmlContents/Features", out var bytes))
        {
            Assert.All(file.Configurations, c => Assert.Null(c.Name));
            return;
        }
        var xml = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(bytes));
        System.Xml.Linq.XNamespace ns = "http://www.solidworks.com/sw2003/schema";
        var expected = xml.Root!.Element(ns + "swConfigurationList")!.Elements(ns + "swConfiguration")
            .ToDictionary(e => int.Parse(e.Attribute("swID")!.Value), e => e.Attribute("swName")!.Value);
        Assert.NotEmpty(file.Configurations);
        Assert.All(file.Configurations, c => Assert.Equal(expected[c.Index], c.Name));
    }
}
