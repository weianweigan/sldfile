using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace SldFile;

/// <summary>Reads configuration identities from the recorded SolidWorks XML manifest.</summary>
internal static class ConfigurationNames
{
    private static readonly XNamespace Sw = "http://www.solidworks.com/sw2003/schema";

    internal static IReadOnlyDictionary<int, string?> Read(Dictionary<string, byte[]> streams)
    {
        var names = new Dictionary<int, string?>();
        if (!streams.TryGetValue("swXmlContents/Features", out var bytes)) return names;
        XDocument document;
        try
        {
            using var input = new MemoryStream(bytes, false);
            using var reader = XmlReader.Create(input, new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("Invalid SolidWorks configuration manifest.", exception);
        }
        var root = document.Root;
        if (root?.Name != Sw + "swSolidWorks") return names;
        foreach (var list in root.Elements(Sw + "swConfigurationList"))
        foreach (var configuration in list.Elements(Sw + "swConfiguration"))
        {
            if (!TryIndex(configuration.Attribute("swID"), out var index)) continue;
            var name = (string?)configuration.Attribute("swName");
            if (string.IsNullOrEmpty(name)) name = null;
            if (names.TryGetValue(index, out var previous) && !string.Equals(previous, name, StringComparison.Ordinal))
                names[index] = null;
            else if (!names.ContainsKey(index)) names.Add(index, name);
        }
        // The model list repeats these identities. A disagreement makes the name
        // unavailable rather than silently selecting a potentially stale copy.
        foreach (var list in root.Elements(Sw + "swModelList"))
        foreach (var model in list.Elements(Sw + "swModel"))
        {
            if (!TryIndex(model.Attribute("swConfigurationId"), out var index) || !names.TryGetValue(index, out var name)) continue;
            var modelName = (string?)model.Attribute("swConfigurationName");
            if (modelName != null && !string.Equals(name, modelName, StringComparison.Ordinal)) names[index] = null;
        }
        return names;
    }

    private static bool TryIndex(XAttribute? attribute, out int index) =>
        int.TryParse((string?)attribute, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index >= 0;
}
