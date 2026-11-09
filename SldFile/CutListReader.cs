using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SldFile;

internal static class CutListReader
{
    public static IReadOnlyList<CutListItem> Read(byte[] xmlBytes)
    {
        if (xmlBytes == null || xmlBytes.Length == 0)
            return Array.Empty<CutListItem>();

        XDocument? doc = null;
        try
        {
            using var ms = new MemoryStream(xmlBytes);
            using var reader = XmlReader.Create(ms, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            doc = XDocument.Load(reader);
        }
        catch
        {
            try
            {
                string text = Encoding.UTF8.GetString(xmlBytes);
                text = System.Text.RegularExpressions.Regex.Replace(text, @"=""([^""]*?)<([^""]*?)""", @"=""$1&lt;$2""");
                using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
                using var reader = XmlReader.Create(ms, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                doc = XDocument.Load(reader);
            }
            catch
            {
                return Array.Empty<CutListItem>();
            }
        }

        if (doc?.Root == null) return Array.Empty<CutListItem>();
        var root = doc.Root;

        var items = new List<CutListItem>();
        var features = root.Descendants().Where(e => e.Name.LocalName == "Feature");

        foreach (var feat in features)
        {
            string name = (string?)feat.Attribute("Name") ?? string.Empty;
            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int quantity = 1;

            foreach (var prop in feat.Elements().Where(e => e.Name.LocalName == "CustomProperty"))
            {
                string propName = (string?)prop.Attribute("Name") ?? string.Empty;
                if (string.IsNullOrEmpty(propName)) continue;
                string val = prop.Value;
                props[propName] = val;

                if (string.Equals(propName, "QUANTITY", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q))
                {
                    quantity = q;
                }
            }

            var qtyElem = feat.Elements().FirstOrDefault(e => e.Name.LocalName == "Quantity");
            if (qtyElem != null && int.TryParse(qtyElem.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q2))
            {
                quantity = q2;
            }

            items.Add(new CutListItem
            {
                Name = name,
                Quantity = quantity,
                Properties = props
            });
        }

        return items.AsReadOnly();
    }
}
