using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SldFile;
using Xunit;

namespace SldFileTests;

/// <summary>
/// Verifies custom property editing in files saved by SOLIDWORKS 2024 (GitHub Issue #7).
/// In SOLIDWORKS 2024, plain text custom properties have an FPVals copy element.
/// </summary>
public class SolidWorks2024CustomPropertiesTests
{
    private static readonly XNamespace CustomPropsNs = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
    private static readonly XNamespace VtNs = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

    private const string SampleSw2024CustomXml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<propertySet xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/custom-properties""
             xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">
  <propertySection name=""UserDefinedProperties"">
    <propertyNameDictionaryElement name=""Description"" pid=""2""/>
    <property name=""Description"" pid=""2"" TypeID=""30"" IsEquation=""False"">
      <vt:lpstr xml:space=""preserve"">POST MOUNT</vt:lpstr>
      <FPVals>
        <vt:lpstr xml:space=""preserve"">POST MOUNT</vt:lpstr>
      </FPVals>
    </property>
    <propertyNameDictionaryElement name=""Finish"" pid=""3""/>
    <property name=""Finish"" pid=""3"" TypeID=""30"" IsEquation=""False"">
      <vt:lpstr xml:space=""preserve"">Anodized Black</vt:lpstr>
      <FPVals>
        <vt:lpstr xml:space=""preserve"">Anodized Black</vt:lpstr>
      </FPVals>
    </property>
    <propertyNameDictionaryElement name=""Material"" pid=""5""/>
    <property name=""Material"" pid=""5"" TypeID=""30"" IsEquation=""False"">
      <vt:lpstr xml:space=""preserve"">Plain Carbon Steel</vt:lpstr>
      <FPVals>
        <CSVal name=""Default"">
          <vt:lpstr>&lt;$0$Plain Carbon Steel$&gt;</vt:lpstr>
        </CSVal>
      </FPVals>
    </property>
    <property name="""" pid=""16777221"" TypeID=""30"" IsEquation=""False"">
      <vt:lpstr>""SW-Material@part.SLDPRT""</vt:lpstr>
    </property>
    <propertyNameDictionaryElement name=""CalculatedCost"" pid=""6""/>
    <property name=""CalculatedCost"" pid=""6"" TypeID=""30"" IsEquation=""True"">
      <vt:lpstr>50.0</vt:lpstr>
    </property>
  </propertySection>
</propertySet>";

    [Fact]
    public void CanEditPlainTextProperty_WithSolidWorks2024FpVals_SynchronizesFpValsNode()
    {
        // 1. Arrange streams with SW2024 custom property XML
        var streams = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["docProps/custom.xml"] = Encoding.UTF8.GetBytes(SampleSw2024CustomXml)
        };

        var metadata = new DocumentMetadata();
        var customProps = new PropertyCollection();

        // Use reflection to access internal MetadataStore
        var storeType = typeof(SwFile).Assembly.GetType("SldFile.MetadataStore")!;
        var storeCtor = storeType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(Dictionary<string, byte[]>) },
            null)!;
        var store = storeCtor.Invoke(new object[] { streams });

        var readMetaMethod = storeType.GetMethod("ReadMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        metadata = (DocumentMetadata)readMetaMethod.Invoke(store, null)!;

        var readMethod = storeType.GetMethod("ReadCustomProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        readMethod.Invoke(store, new object[] { customProps });

        Assert.Equal("POST MOUNT", customProps["Description"]);
        Assert.Equal("Anodized Black", customProps["Finish"]);
        Assert.Equal("Plain Carbon Steel", customProps["Material"]);

        // 2. Act: Edit the plain text property in SW2024 format
        customProps.Set("Description", "MODIFIED POST MOUNT 2024");

        var prepMethod = storeType.GetMethod("PrepareChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var updatedStreams = (Dictionary<string, byte[]>)prepMethod.Invoke(store, new object[] { metadata, customProps, Array.Empty<Configuration>() })!;

        // 3. Assert: Verify the edit succeeded and FPVals copy is synchronized
        Assert.True(updatedStreams.ContainsKey("docProps/custom.xml"));
        var updatedXmlBytes = updatedStreams["docProps/custom.xml"];
        var xdoc = XDocument.Parse(Encoding.UTF8.GetString(updatedXmlBytes));

        var descProp = xdoc.Descendants().First(e => e.Name.LocalName == "property" && (string?)e.Attribute("name") == "Description");
        var primaryVal = descProp.Elements().First(e => e.Name.LocalName == "lpstr").Value;
        Assert.Equal("MODIFIED POST MOUNT 2024", primaryVal);

        var fpvals = descProp.Elements().First(e => e.Name.LocalName == "FPVals");
        var copyVal = fpvals.Elements().First(e => e.Name.LocalName == "lpstr").Value;
        Assert.Equal("MODIFIED POST MOUNT 2024", copyVal);
    }

    [Fact]
    public void CanRemovePlainTextProperty_WithSolidWorks2024FpVals()
    {
        var streams = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["docProps/custom.xml"] = Encoding.UTF8.GetBytes(SampleSw2024CustomXml)
        };

        var metadata = new DocumentMetadata();
        var customProps = new PropertyCollection();

        var storeType = typeof(SwFile).Assembly.GetType("SldFile.MetadataStore")!;
        var storeCtor = storeType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(Dictionary<string, byte[]>) },
            null)!;
        var store = storeCtor.Invoke(new object[] { streams });

        var readMetaMethod = storeType.GetMethod("ReadMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        metadata = (DocumentMetadata)readMetaMethod.Invoke(store, null)!;

        var readMethod = storeType.GetMethod("ReadCustomProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        readMethod.Invoke(store, new object[] { customProps });

        // Act: Remove the plain text property
        Assert.True(customProps.Remove("Finish"));

        var prepMethod = storeType.GetMethod("PrepareChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var updatedStreams = (Dictionary<string, byte[]>)prepMethod.Invoke(store, new object[] { metadata, customProps, Array.Empty<Configuration>() })!;

        // Assert: Property and dictionary elements are removed cleanly
        var xdoc = XDocument.Parse(Encoding.UTF8.GetString(updatedStreams["docProps/custom.xml"]));
        Assert.Null(xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "property" && (string?)e.Attribute("name") == "Finish"));
        Assert.Null(xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "propertyNameDictionaryElement" && (string?)e.Attribute("name") == "Finish"));
    }

    [Fact]
    public void EditingLinkedProperty_WithPartnerExpression_ThrowsNotSupportedException()
    {
        var streams = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["docProps/custom.xml"] = Encoding.UTF8.GetBytes(SampleSw2024CustomXml)
        };

        var metadata = new DocumentMetadata();
        var customProps = new PropertyCollection();

        var storeType = typeof(SwFile).Assembly.GetType("SldFile.MetadataStore")!;
        var storeCtor = storeType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(Dictionary<string, byte[]>) },
            null)!;
        var store = storeCtor.Invoke(new object[] { streams });

        var readMetaMethod = storeType.GetMethod("ReadMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        metadata = (DocumentMetadata)readMetaMethod.Invoke(store, null)!;

        var readMethod = storeType.GetMethod("ReadCustomProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        readMethod.Invoke(store, new object[] { customProps });

        // Act & Assert: Attempting to edit linked property Material (which has partner PID 16777221 = 5 + 0x01000000)
        customProps.Set("Material", "Aluminum 6061");

        var prepMethod = storeType.GetMethod("PrepareChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            prepMethod.Invoke(store, new object[] { metadata, customProps, Array.Empty<Configuration>() }));

        Assert.IsType<NotSupportedException>(ex.InnerException);
        Assert.Contains("linked, calculated or cached", ex.InnerException!.Message);
    }

    [Fact]
    public void EditingEquationProperty_ThrowsNotSupportedException()
    {
        var streams = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["docProps/custom.xml"] = Encoding.UTF8.GetBytes(SampleSw2024CustomXml)
        };

        var metadata = new DocumentMetadata();
        var customProps = new PropertyCollection();

        var storeType = typeof(SwFile).Assembly.GetType("SldFile.MetadataStore")!;
        var storeCtor = storeType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(Dictionary<string, byte[]>) },
            null)!;
        var store = storeCtor.Invoke(new object[] { streams });

        var readMetaMethod = storeType.GetMethod("ReadMetadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        metadata = (DocumentMetadata)readMetaMethod.Invoke(store, null)!;

        var readMethod = storeType.GetMethod("ReadCustomProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        readMethod.Invoke(store, new object[] { customProps });

        // Act & Assert: Attempting to edit CalculatedCost (IsEquation="True")
        customProps.Set("CalculatedCost", "99.9");

        var prepMethod = storeType.GetMethod("PrepareChanges", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            prepMethod.Invoke(store, new object[] { metadata, customProps, Array.Empty<Configuration>() }));

        Assert.IsType<NotSupportedException>(ex.InnerException);
        Assert.Contains("linked, calculated or cached", ex.InnerException!.Message);
    }
}
