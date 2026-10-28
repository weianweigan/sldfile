using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;

namespace SldFile.Assembly;

/// <summary>
/// Parses the SolidWorks COMPINSTANCETREE XML stream into assembly component trees and BOM items.
/// </summary>
internal static class AssemblyTreeReader
{
    private sealed class FileRecord
    {
        public string Id { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    private sealed class ModelRecord
    {
        public string Id { get; set; } = string.Empty;
        public string ConfigurationId { get; set; } = string.Empty;
        public string FileRef { get; set; } = string.Empty;
        public string ConfigurationName { get; set; } = string.Empty;
        public XElement? Element { get; set; }
    }

    public static IReadOnlyList<AssemblyComponentNode> ParseAssemblyTree(string xmlContent, string? configurationName = null)
    {
        if (string.IsNullOrEmpty(xmlContent))
            return Array.Empty<AssemblyComponentNode>();

        int start = xmlContent.IndexOf('<');
        if (start < 0) return Array.Empty<AssemblyComponentNode>();
        if (start > 0) xmlContent = xmlContent.Substring(start);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xmlContent);
        }
        catch
        {
            return Array.Empty<AssemblyComponentNode>();
        }

        var root = doc.Root;
        if (root == null) return Array.Empty<AssemblyComponentNode>();

        // 1. Files
        var files = new Dictionary<string, FileRecord>();
        string? ownFileId = null;

        var headerNode = root.Elements().FirstOrDefault(e => e.Name.LocalName == "swHeader");
        if (headerNode != null)
        {
            foreach (var fileElem in headerNode.Elements().Where(e => e.Name.LocalName == "swFile"))
            {
                string id = fileElem.Attribute("id")?.Value ?? string.Empty;
                string docType = fileElem.Attribute("swDocType")?.Value ?? string.Empty;
                string path = fileElem.Attribute("swPath")?.Value ?? string.Empty;

                if (ownFileId == null && (docType.Equals("ASSEMBLY", StringComparison.OrdinalIgnoreCase) || docType == "1"))
                {
                    ownFileId = id;
                }

                files[id] = new FileRecord
                {
                    Id = id,
                    DocType = docType,
                    Path = path
                };
            }
        }

        // 2. Models
        var models = new Dictionary<string, ModelRecord>();
        var modelListNode = root.Elements().FirstOrDefault(e => e.Name.LocalName == "swModelList");
        if (modelListNode != null)
        {
            foreach (var modelElem in modelListNode.Elements().Where(e => e.Name.LocalName == "swModel"))
            {
                string id = modelElem.Attribute("id")?.Value ?? string.Empty;
                string cfgId = modelElem.Attribute("swConfigurationId")?.Value ?? string.Empty;
                string fileRef = modelElem.Attribute("swFileRef")?.Value ?? string.Empty;
                string cfgName = modelElem.Attribute("swConfigurationName")?.Value ?? string.Empty;

                models[id] = new ModelRecord
                {
                    Id = id,
                    ConfigurationId = cfgId,
                    FileRef = fileRef,
                    ConfigurationName = cfgName,
                    Element = modelElem
                };
            }
        }

        // 3. Find root assembly model
        ModelRecord? rootModel = null;

        // Try to match by configuration name if specified
        if (!string.IsNullOrEmpty(configurationName))
        {
            rootModel = models.Values.FirstOrDefault(m =>
                (ownFileId == null || m.FileRef == ownFileId) &&
                string.Equals(m.ConfigurationName, configurationName, StringComparison.OrdinalIgnoreCase));
        }

        // Fallback: first model referencing the assembly file
        if (rootModel == null && ownFileId != null)
        {
            rootModel = models.Values.FirstOrDefault(m => m.FileRef == ownFileId);
        }

        // Fallback: first model with references
        if (rootModel == null)
        {
            rootModel = models.Values.FirstOrDefault(m => m.Element != null &&
                m.Element.Elements().Any(e => e.Name.LocalName == "swReference"));
        }

        if (rootModel?.Element == null)
            return Array.Empty<AssemblyComponentNode>();

        // 4. Build components from swReference nodes
        var components = new List<AssemblyComponentNode>();
        foreach (var refElem in rootModel.Element.Elements().Where(e => e.Name.LocalName == "swReference"))
        {
            var node = BuildComponentNode(refElem, models, files);
            if (node != null)
            {
                components.Add(node);
            }
        }

        return components.AsReadOnly();
    }

    private static AssemblyComponentNode? BuildComponentNode(
        XElement refElem,
        Dictionary<string, ModelRecord> models,
        Dictionary<string, FileRecord> files)
    {
        string id = refElem.Attribute("swID")?.Value ?? refElem.Attribute("id")?.Value ?? string.Empty;
        string name = refElem.Attribute("swName")?.Value ?? string.Empty;
        string instanceNumStr = refElem.Attribute("swReferenceNumber")?.Value ?? "1";
        int.TryParse(instanceNumStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int refNumber);

        string modelRef = refElem.Attribute("swModelRef")?.Value ?? string.Empty;
        string cfgName = refElem.Attribute("swConfigurationName")?.Value ?? string.Empty;

        string filePath = string.Empty;
        if (models.TryGetValue(modelRef, out var targetModel))
        {
            if (string.IsNullOrEmpty(cfgName))
                cfgName = targetModel.ConfigurationName;

            if (files.TryGetValue(targetModel.FileRef, out var fileRec))
            {
                filePath = fileRec.Path;
            }
        }

        string instanceName = refNumber > 1 ? $"{name}-{refNumber}" : name;

        bool isSuppressed = ParseBoolean(refElem.Attribute("swSuppressed")?.Value);
        bool isHidden = ParseBoolean(refElem.Attribute("swHidden")?.Value);
        bool isVirtual = ParseBoolean(refElem.Attribute("swIsVirtualComponent")?.Value);
        bool isFlexible = ParseBoolean(refElem.Attribute("swFlexible")?.Value);
        bool excludeFromBom = ParseBoolean(refElem.Attribute("swExcludeFromBOM")?.Value);

        Matrix4x4 transform = ParseTransform(refElem.Attribute("swTransform")?.Value);

        // Recursively build children if the component itself is a sub-assembly
        var children = new List<AssemblyComponentNode>();
        if (targetModel?.Element != null)
        {
            foreach (var childRef in targetModel.Element.Elements().Where(e => e.Name.LocalName == "swReference"))
            {
                var childNode = BuildComponentNode(childRef, models, files);
                if (childNode != null)
                {
                    children.Add(childNode);
                }
            }
        }

        return new AssemblyComponentNode
        {
            ComponentId = id,
            Name = name,
            InstanceName = instanceName,
            ReferenceNumber = refNumber,
            FilePath = filePath,
            ConfigurationName = cfgName,
            Transform = transform,
            IsSuppressed = isSuppressed,
            IsHidden = isHidden,
            IsVirtual = isVirtual,
            IsFlexible = isFlexible,
            ExcludeFromBom = excludeFromBom,
            Children = children.AsReadOnly()
        };
    }

    private static bool ParseBoolean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string trimmed = value!.Trim();
        return trimmed == "1" ||
               trimmed.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    private static Matrix4x4 ParseTransform(string? transformStr)
    {
        if (string.IsNullOrEmpty(transformStr))
            return Matrix4x4.Identity;

        var tokens = transformStr!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 16)
            return Matrix4x4.Identity;

        float[] m = new float[16];
        for (int i = 0; i < 16; i++)
        {
            if (!float.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out m[i]))
            {
                return Matrix4x4.Identity;
            }
        }

        return new Matrix4x4(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]);
    }

    /// <summary>
    /// Aggregates a Bill of Materials list from the components tree.
    /// </summary>
    public static IReadOnlyList<BomItem> BuildBom(IEnumerable<AssemblyComponentNode> components)
    {
        var flatList = new List<AssemblyComponentNode>();
        void Flatten(IEnumerable<AssemblyComponentNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsSuppressed || node.ExcludeFromBom) continue;
                flatList.Add(node);
                if (node.Children.Count > 0)
                {
                    Flatten(node.Children);
                }
            }
        }
        Flatten(components);

        // Group by part identity: (FilePath, ConfigurationName, Name)
        var groups = flatList.GroupBy(c => new
        {
            Key = !string.IsNullOrEmpty(c.FilePath) ? c.FilePath.ToLowerInvariant() : c.Name.ToLowerInvariant(),
            c.ConfigurationName
        }).ToList();

        var bom = new List<BomItem>();
        int itemNumber = 1;

        foreach (var group in groups)
        {
            var first = group.First();
            string partNumber = !string.IsNullOrEmpty(first.FilePath)
                ? Path.GetFileNameWithoutExtension(first.FilePath)
                : first.Name;

            bom.Add(new BomItem
            {
                ItemNumber = itemNumber++,
                PartNumber = partNumber,
                Description = first.Name,
                ConfigurationName = first.ConfigurationName,
                Quantity = group.Count(),
                FilePath = first.FilePath,
                IsVirtual = first.IsVirtual,
                InstanceNames = group.Select(c => c.InstanceName).ToArray()
            });
        }

        return bom.AsReadOnly();
    }
}
