using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SldFile.Ole;

namespace SldFile;

/// <summary>Maps confirmed document properties to their existing storage locations.</summary>
internal sealed class MetadataStore
{
    private const string SwOle = "ISolidWorksInformation";
    private const string SwXml = "docProps/ISolidWorksInformation.xml";
    private const string SummaryOle = "\u0005SummaryInformation";
    private const string CustomOle = "\u0005DocumentSummaryInformation";
    private const string CustomXml = "docProps/custom.xml";
    private const string CoreXml = "docProps/core.xml";
    private const string AppXml = "docProps/app.xml";
    private Dictionary<string, byte[]> _streams;
    private readonly Dictionary<string, string?> _baseline = new Dictionary<string, string?>();

    private static readonly (string Name, uint SwId, string OlePath, uint OleId, string XmlPath, string XmlName)[] Fields =
    {
        ("Title", 11, SummaryOle, 2, CoreXml, "title"),
        ("Subject", 12, SummaryOle, 3, CoreXml, "subject"),
        ("Author", 8, SummaryOle, 4, CoreXml, "creator"),
        ("Keywords", 9, SummaryOle, 5, CoreXml, "keywords"),
        ("Comments", 10, SummaryOle, 6, CoreXml, "description"),
        ("LastSavedBy", 15, SummaryOle, 8, CoreXml, "lastModifiedBy"),
        ("Company", 0, CustomOle, 15, AppXml, "Company"),
        ("Manager", 0, CustomOle, 14, AppXml, "Manager"),
        ("Category", 0, CustomOle, 2, CoreXml, "category")
    };

    internal MetadataStore(Dictionary<string, byte[]> streams) { _streams = streams; }

    internal DocumentMetadata ReadMetadata()
    {
        var metadata = new DocumentMetadata();
        foreach (var field in Fields)
        {
            var value = field.SwId == 0 ? null : ReadValue(SwOle, field.SwId) ?? ReadValue(SwXml, field.SwId);
            value = value ?? ReadValue(field.OlePath, field.OleId) ?? ReadXmlText(field.XmlPath, field.XmlName);
            string? text = value as string;
            typeof(DocumentMetadata).GetProperty(field.Name)!.SetValue(metadata, text);
            _baseline[field.Name] = text;
        }
        metadata.CreatedAt = ReadTime(ReadValue(SummaryOle, 12)) ?? ReadTime(ReadXmlText(CoreXml, "created"));
        metadata.LastSavedAt = ReadTime(ReadValue(SummaryOle, 13)) ?? ReadTime(ReadXmlText(CoreXml, "modified"));
        metadata.Units = ReadUnits();
        metadata.BoundingBox = ReadBoundingBox();
        return metadata;
    }

    internal void ReadCustomProperties(PropertyCollection target)
    {
        string? path = Existing(CustomOle, CustomXml);
        if (path != null) new PropertySource(_streams[path], IsXml(path)).ReadCustom(target);
    }

    internal IReadOnlyList<Configuration> ReadConfigurations()
    {
        var configurations = new List<Configuration>();
        var names = ConfigurationNames.Read(_streams);
        foreach (var path in _streams.Keys)
        {
            string name = path.StartsWith("docProps/", StringComparison.Ordinal) ? path.Substring(9) : path;
            if (name.EndsWith(".xml", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 4);
            const string suffix = "-Properties";
            if (!name.StartsWith("Config-", StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(name.Substring(7, name.Length - 7 - suffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var index)) continue;
            if (configurations.Any(c => c.Index == index)) throw new InvalidDataException("Ambiguous configuration property sources.");
            names.TryGetValue(index, out var displayName);
            var configuration = new Configuration(index, path, displayName);
            new PropertySource(_streams[path], IsXml(path)).ReadCustom(configuration.CustomProperties);
            configurations.Add(configuration);
        }
        return configurations.OrderBy(c => c.Index).ToList().AsReadOnly();
    }

    internal Dictionary<string, byte[]> PrepareChanges(DocumentMetadata metadata, PropertyCollection custom, IReadOnlyList<Configuration> configurations)
    {
        var sources = new Dictionary<string, PropertySource>(StringComparer.OrdinalIgnoreCase);
        PropertySource Source(string path)
        {
            if (!sources.TryGetValue(path, out var source))
            {
                source = new PropertySource(_streams[path], IsXml(path));
                sources.Add(path, source);
            }
            return source;
        }
        foreach (var field in Fields)
        {
            string? value = (string?)typeof(DocumentMetadata).GetProperty(field.Name)!.GetValue(metadata);
            if (value == _baseline[field.Name]) continue;
            bool found = false;
            if (field.SwId != 0)
            {
                foreach (var path in new[] { SwOle, SwXml })
                {
                    if (!_streams.ContainsKey(path)) continue;
                    Source(path).Set(field.SwId, value ?? "");
                    found = true;
                }
            }
            if (_streams.ContainsKey(field.OlePath)) { Source(field.OlePath).Set(field.OleId, value ?? ""); found = true; }
            if (_streams.ContainsKey(field.XmlPath)) { Source(field.XmlPath).SetText(field.XmlName, value); found = true; }
            if (!found) throw new NotSupportedException($"This document has no supported storage for {field.Name}.");
        }
        if (custom.Changes.Count > 0)
        {
            var path = Existing(CustomOle, CustomXml);
            if (path == null) throw new NotSupportedException("This document has no supported custom property storage.");
            Source(path).ApplyCustom(custom);
        }
        foreach (var configuration in configurations)
            if (configuration.CustomProperties.Changes.Count > 0) Source(configuration.SourcePath).ApplyCustom(configuration.CustomProperties);
        return sources.ToDictionary(p => p.Key, p => p.Value.ToBytes(), StringComparer.OrdinalIgnoreCase);
    }

    internal void AcceptChanges(Dictionary<string, byte[]> changes, DocumentMetadata metadata, PropertyCollection custom, IReadOnlyList<Configuration> configurations)
    {
        foreach (var pair in changes) _streams[pair.Key] = pair.Value;
        foreach (var field in Fields) _baseline[field.Name] = (string?)typeof(DocumentMetadata).GetProperty(field.Name)!.GetValue(metadata);
        custom.Changes.Clear();
        foreach (var configuration in configurations) configuration.CustomProperties.Changes.Clear();
    }

    private string? Existing(string first, string second) => _streams.ContainsKey(first) ? first : _streams.ContainsKey(second) ? second : null;
    private static bool IsXml(string path) => path.EndsWith(".xml", StringComparison.Ordinal);
    private object? ReadValue(string path, uint id) => _streams.TryGetValue(path, out var bytes) ? new PropertySource(bytes, IsXml(path)).Get(id) : null;
    private string? ReadXmlText(string path, string name) => _streams.TryGetValue(path, out var bytes) ? new PropertySource(bytes, true).GetText(name) : null;
    private object? ReadNamedValue(string path, string name) => _streams.TryGetValue(path, out var bytes) ? new PropertySource(bytes, IsXml(path)).GetNamed(name) : null;

    private DocumentUnits? ReadUnits()
    {
        object? sysVal = ReadNamedValue(SwOle, "SW_UnitSystem") ?? ReadNamedValue(SwXml, "SW_UnitSystem");
        object? lenVal = ReadNamedValue(SwOle, "SW_UnitsLinear") ?? ReadNamedValue(SwXml, "SW_UnitsLinear");
        object? dualLenVal = ReadNamedValue(SwOle, "SW_UnitsDualLinear") ?? ReadNamedValue(SwXml, "SW_UnitsDualLinear");
        object? angVal = ReadNamedValue(SwOle, "SW_UnitsAngular") ?? ReadNamedValue(SwXml, "SW_UnitsAngular");
        object? massVal = ReadNamedValue(SwOle, "SW_UnitsMassPropMass") ?? ReadNamedValue(SwXml, "SW_UnitsMassPropMass");

        if (sysVal == null && lenVal == null && angVal == null && massVal == null)
            return null;

        static int? AsInt(object? val) => val switch
        {
            int i => i,
            short s => (int)s,
            string str when int.TryParse(str, out var parsed) => parsed,
            _ => null
        };

        var units = new DocumentUnits();
        int? sysInt = AsInt(sysVal);
        if (sysInt.HasValue && Enum.IsDefined(typeof(UnitSystem), sysInt.Value))
            units.System = (UnitSystem)sysInt.Value;

        int? lenInt = AsInt(lenVal);
        if (lenInt.HasValue && Enum.IsDefined(typeof(LengthUnit), lenInt.Value))
            units.Length = (LengthUnit)lenInt.Value;

        int? dualLenInt = AsInt(dualLenVal);
        if (dualLenInt.HasValue && Enum.IsDefined(typeof(LengthUnit), dualLenInt.Value))
            units.DualLength = (LengthUnit)dualLenInt.Value;

        int? angInt = AsInt(angVal);
        if (angInt.HasValue && Enum.IsDefined(typeof(AngleUnit), angInt.Value))
            units.Angle = (AngleUnit)angInt.Value;

        int? massInt = AsInt(massVal);
        if (massInt.HasValue && Enum.IsDefined(typeof(MassUnit), massInt.Value))
            units.Mass = (MassUnit)massInt.Value;

        return units;
    }

    private ModelBoundingBox? ReadBoundingBox()
    {
        string? raw = (ReadNamedValue(SwOle, "swBoundingBox") ?? ReadNamedValue(SwXml, "swBoundingBox")) as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            foreach (var key in new[] { "swXmlContents/KeyWords", "swXmlContents/Features" })
            {
                if (_streams.TryGetValue(key, out var bytes))
                {
                    int start = Array.IndexOf(bytes, (byte)'<');
                    if (start >= 0)
                    {
                        string xml = Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
                        int bbIdx = xml.IndexOf("swBoundingBox=\"", StringComparison.Ordinal);
                        if (bbIdx >= 0)
                        {
                            int valStart = bbIdx + 15;
                            int valEnd = xml.IndexOf('"', valStart);
                            if (valEnd > valStart)
                            {
                                raw = xml.Substring(valStart, valEnd - valStart);
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(raw)) return null;

        var parts = raw!.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 6 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minX) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minY) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var minZ) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxX) &&
            double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxY) &&
            double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxZ))
        {
            return new ModelBoundingBox(minX, minY, minZ, maxX, maxY, maxZ);
        }

        return null;
    }

    private static DateTimeOffset? ReadTime(object? value)
    {
        if (value is DateTime date) return new DateTimeOffset(date);
        return value is string text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ? time : (DateTimeOffset?)null;
    }

    private sealed class PropertySource
    {
        private readonly PropertySetEditor? _ole;
        private readonly XDocument? _xml;
        private static readonly XNamespace Vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
        private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";

        internal PropertySource(byte[] bytes, bool xml)
        {
            if (!xml) { _ole = new PropertySetEditor(bytes); return; }
            using var input = new MemoryStream(bytes);
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            _xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        private IEnumerable<XElement> Properties => _xml!.Descendants().Where(e => e.Name.LocalName == "property");
        private XElement? CustomSection => _xml!.Descendants().FirstOrDefault(e => e.Name.LocalName == "propertySection" && (string?)e.Attribute("name") == "UserDefinedProperties");

        internal object? GetNamed(string name)
        {
            if (_ole != null) return _ole.GetNamed(name);
            var prop = Properties.FirstOrDefault(e => string.Equals((string?)e.Attribute("name"), name, StringComparison.OrdinalIgnoreCase));
            return prop != null ? DecodeXml(prop) : null;
        }

        internal object? Get(uint id) => _ole != null ? _ole.Get(id) : DecodeXml(Properties.FirstOrDefault(e => (string?)e.Attribute("pid") == id.ToString(CultureInfo.InvariantCulture)));
        internal string? GetText(string name) => _xml!.Root!.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

        internal void ReadCustom(PropertyCollection target)
        {
            if (_ole != null) { _ole.ReadCustom(target); return; }
            var section = CustomSection;
            if (section == null) return;
            foreach (var property in section.Elements().Where(e => e.Name.LocalName == "property"))
            {
                var name = (string?)property.Attribute("name");
                var value = DecodeXml(property);
                if (!string.IsNullOrEmpty(name) && value != null) target.Load(name!, value);
            }
        }

        internal void Set(uint id, string? value)
        {
            if (_ole != null) { _ole.Set(id, value); return; }
            var element = Properties.FirstOrDefault(e => (string?)e.Attribute("pid") == id.ToString(CultureInfo.InvariantCulture));
            if (element == null)
                throw new NotSupportedException("The document is missing a supported metadata property.");
            // Keep the existing property and its dictionary aliases; null clears its value.
            SetXmlValue(element, value ?? "");
        }

        internal void SetText(string name, string? value)
        {
            var root = _xml!.Root!;
            var element = root.Elements().FirstOrDefault(e => e.Name.LocalName == name);
            if (element == null)
            {
                var ns = root.Name.Namespace == Cp ? (name == "category" ? Cp : Dc) : root.Name.Namespace;
                element = new XElement(ns + name);
                root.Add(element);
            }
            element.Value = value ?? "";
        }

        internal void ApplyCustom(PropertyCollection properties)
        {
            if (_ole != null) { _ole.ApplyCustom(properties); return; }
            var section = CustomSection ?? throw new NotSupportedException("This document is missing its custom property section.");
            foreach (var name in properties.Changes)
            {
                var element = section.Elements().FirstOrDefault(e => e.Name.LocalName == "property" && (string?)e.Attribute("name") == name);
                if (!properties.TryGetValue(name, out var value))
                {
                    if (element == null) continue;
                    EnsureLiteralProperty(element);
                    var id = (string?)element.Attribute("pid");
                    section.Elements().Where(e => (string?)e.Attribute("pid") == id).Remove();
                    continue;
                }
                if (element != null) EnsureLiteralProperty(element);
                if (element == null)
                {
                    // IDs with bit 24 set are associated values for lower IDs,
                    // not the next available user property. Native SW allocates below that range.
                    uint id = checked(section.Elements().Select(e => uint.TryParse((string?)e.Attribute("pid"), out var n) ? n : 1u)
                        .Where(n => n < 0x01000000).DefaultIfEmpty(1u).Max() + 1);
                    if (id >= 0x01000000) throw new NotSupportedException("The custom property identifier range is exhausted.");
                    element = new XElement(section.Name.Namespace + "property", new XAttribute("name", name), new XAttribute("pid", id), new XAttribute("TypeID", 0), new XAttribute("IsEquation", "False"));
                    section.Add(element);
                    section.Add(new XElement(section.Name.Namespace + "propertyNameDictionaryElement", new XAttribute("name", name), new XAttribute("pid", id)));
                }
                SetXmlValue(element, value);
                element.SetAttributeValue("TypeID", value is bool ? 11 : value is DateTime ? 64 : value is int || value is double ? 3 : 30);
            }
        }

        private static void EnsureLiteralProperty(XElement element)
        {
            if (string.Equals((string?)element.Attribute("IsEquation"), "True", StringComparison.OrdinalIgnoreCase)
                || element.Elements().Any(e => e.Name.LocalName == "FPVals"))
                throw new NotSupportedException("Editing linked, calculated or cached custom properties is not supported.");
        }

        private static object? DecodeXml(XElement? property)
        {
            var value = property?.Elements().FirstOrDefault(e => e.Name.Namespace == Vt);
            if (value == null) return null;
            switch (value.Name.LocalName)
            {
                case "lpstr": case "lpwstr": case "bstr": return value.Value;
                case "i2": case "i4": return int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? (object)n : null;
                case "r8": return double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (object)d : null;
                case "bool": return value.Value == "1" || value.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                case "filetime": return DateTime.TryParse(value.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? (object)date : null;
                default: return null;
            }
        }

        private static void SetXmlValue(XElement property, object value)
        {
            string type = value is int ? "i4" : value is double ? "r8" : value is bool ? "bool" : value is DateTime ? "filetime" : "lpstr";
            string text = value is DateTime date ? date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : value is bool boolean ? (boolean ? "Yes" : "No") : Convert.ToString(value, CultureInfo.InvariantCulture)!;
            var existing = property.Elements().FirstOrDefault(e => e.Name.Namespace == Vt);
            var replacement = new XElement(Vt + type, text);
            if (value is string) replacement.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            if (existing != null) existing.ReplaceWith(replacement); else property.AddFirst(replacement);
        }

        internal byte[] ToBytes()
        {
            if (_ole != null) return _ole.ToBytes();
            using var output = new MemoryStream();
            using (var writer = XmlWriter.Create(output, new XmlWriterSettings
                { Encoding = new UTF8Encoding(false), Indent = false, NewLineHandling = NewLineHandling.Entitize }))
                _xml!.Save(writer);
            return output.ToArray();
        }
    }
}
