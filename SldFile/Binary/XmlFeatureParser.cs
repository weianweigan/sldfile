using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SldFile.Binary;

/// <summary>
/// Parses FeatureManager design tree items from SolidWorks XML streams (e.g. swXmlContents/KeyWords).
/// Used for modern SolidWorks formats (e.g. 2026+) where the legacy ResolvedFeatures MFC stream is absent.
/// </summary>
internal static class XmlFeatureParser
{
    public static List<Feature> ParseFeatures(byte[]? xmlBytes)
    {
        var features = new List<Feature>();
        if (xmlBytes == null || xmlBytes.Length == 0)
            return features;

        int start = Array.IndexOf(xmlBytes, (byte)'<');
        if (start < 0) return features;

        string xmlText;
        try
        {
            xmlText = Encoding.UTF8.GetString(xmlBytes, start, xmlBytes.Length - start);
        }
        catch
        {
            return features;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlText);
        }
        catch
        {
            return features;
        }

        if (doc.Root == null) return features;

        foreach (var element in doc.Root.Elements())
        {
            string tag = element.Name.LocalName;
            // Ignore non-feature structural elements
            if (tag is "Configuration" or "Sheet" or "View" or "Layer")
                continue;

            string? name = element.Attribute("Name")?.Value;
            if (string.IsNullOrWhiteSpace(name) || name!.Contains("___EndTag___"))
                continue;

            string type = element.Attribute("Type")?.Value ?? string.Empty;

            FeatureKind kind = FeatureKind.Other;
            if (type.IndexOf("旋转", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("Revolv", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Revolve;
            }
            else if (type.IndexOf("扫描", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Sweep", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Sweep;
            }
            else if (type.IndexOf("放样", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Loft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Loft;
            }
            else if (type.IndexOf("圆角", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Fillet", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Fillet;
            }
            else if (type.IndexOf("倒角", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Chamfer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Chamfer;
            }
            else if (type.IndexOf("孔", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Hole", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.HoleWizard;
            }
            else if (type.IndexOf("阵列", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Pattern", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Pattern;
            }
            else if (type.IndexOf("镜像", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Mirror", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Mirror;
            }
            else if (type.IndexOf("筋", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Rib", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Rib;
            }
            else if (type.IndexOf("抽壳", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Shell", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Shell;
            }
            else if (type.IndexOf("拔模", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Draft", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Draft;
            }
            else if (type.IndexOf("螺纹", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Thread", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Thread;
            }
            else if (tag.Equals("Extrusion", StringComparison.OrdinalIgnoreCase) ||
                     type.IndexOf("拉伸", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Extru", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = (type.IndexOf("切除", StringComparison.OrdinalIgnoreCase) >= 0 || type.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? FeatureKind.Cut
                    : FeatureKind.Extrusion;
            }
            else if (tag.Equals("Sketch", StringComparison.OrdinalIgnoreCase) ||
                     type.IndexOf("草图", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Sketch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Sketch;
            }
            else if (type.IndexOf("基准面", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Plane", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.ReferencePlane;
            }
            else if (type.IndexOf("活页夹", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     type.IndexOf("收藏", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                kind = FeatureKind.Folder;
            }

            int? id = int.TryParse(element.Attribute("id")?.Value, out var idVal) ? idVal : (int?)null;
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in element.Attributes())
                attrs[attr.Name.LocalName] = attr.Value;

            var dims = new List<FeatureDimension>();
            var childFeatures = new List<Feature>();

            // Dimension child records
            foreach (var dimElement in element.Elements().Where(c => c.Name.LocalName.Equals("Dimension", StringComparison.OrdinalIgnoreCase)))
            {
                string? dimName = dimElement.Attribute("Name")?.Value;
                if (!string.IsNullOrWhiteSpace(dimName))
                {
                    string dimVal = dimElement.Value;
                    dims.Add(new FeatureDimension(dimName!, dimVal));
                    childFeatures.Add(new Feature(dimName!, FeatureKind.Dimension, "Dimension", null, null, null, null));
                }
            }

            features.Add(new Feature(name, kind, type, id, attrs, dims.AsReadOnly(), childFeatures.AsReadOnly()));
        }

        return features;
    }
}
