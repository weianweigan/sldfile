using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SldFile.Assembly;

/// <summary>
/// Configuration options for BOM generation and component recursion.
/// </summary>
public sealed class BomOptions
{
    public string? ConfigurationName { get; set; }
    public IPathResolver? PathResolver { get; set; }
    public bool IncludeSuppressed { get; set; } = false;
    public bool IncludeHidden { get; set; } = false;
    public bool IncludeExcludedFromBom { get; set; } = false;
    public int MaxDepth { get; set; } = 32;
}

/// <summary>
/// Contains generated tree-style and flat aggregated BOM representations.
/// </summary>
public sealed class BomResult
{
    public IReadOnlyList<BomItem> Tree { get; set; } = Array.Empty<BomItem>();
    public IReadOnlyList<BomItem> Flat { get; set; } = Array.Empty<BomItem>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Serializes the BOM hierarchy and flat summary into standard JSON.
    /// </summary>
    public string ToJson(bool indented = true)
    {
        var sb = new StringBuilder();
        string nl = indented ? "\n" : "";
        string tab = indented ? "  " : "";
        string tab2 = indented ? "    " : "";
        string tab3 = indented ? "      " : "";

        sb.Append("{" + nl);
        sb.Append(tab + "\"flat\": [" + nl);
        for (int i = 0; i < Flat.Count; i++)
        {
            var item = Flat[i];
            sb.Append(tab2 + "{");
            sb.Append($"\"item\": {item.ItemNumber}, \"partNumber\": \"{EscapeJson(item.PartNumber)}\", \"description\": \"{EscapeJson(item.Description)}\", \"configuration\": \"{EscapeJson(item.ConfigurationName)}\", \"quantity\": {item.Quantity}, \"filePath\": \"{EscapeJson(item.FilePath)}\"");
            if (item.ResolvedFilePath != null)
                sb.Append($", \"resolvedPath\": \"{EscapeJson(item.ResolvedFilePath)}\"");
            sb.Append("}");
            if (i < Flat.Count - 1) sb.Append(",");
            sb.Append(nl);
        }
        sb.Append(tab + "]," + nl);

        sb.Append(tab + "\"warnings\": [" + nl);
        for (int i = 0; i < Warnings.Count; i++)
        {
            sb.Append(tab2 + $"\"{EscapeJson(Warnings[i])}\"");
            if (i < Warnings.Count - 1) sb.Append(",");
            sb.Append(nl);
        }
        sb.Append(tab + "]" + nl);
        sb.Append("}");

        return sb.ToString();
    }

    private static string EscapeJson(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s!.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
    }
}

/// <summary>
/// Transforms SolidWorks assembly trees into structured, hierarchical and flat Bills of Materials.
/// </summary>
public static class BomTransformer
{
    public static BomResult Transform(SldAssembly assembly, BomOptions? options = null)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        options ??= new BomOptions();

        var warnings = new List<string>();
        var rootComponents = assembly.GetAssemblyTree(options.ConfigurationName);
        var resolver = options.PathResolver ?? new PathResolver();

        // 1. Build hierarchical tree items
        var treeItems = BuildTreeItems(rootComponents, assembly.FilePath, resolver, options, warnings, 0);

        // 2. Build flat aggregated items
        var flatItems = BuildFlatItems(treeItems);

        return new BomResult
        {
            Tree = treeItems.AsReadOnly(),
            Flat = flatItems.AsReadOnly(),
            Warnings = warnings.AsReadOnly()
        };
    }

    private static List<BomItem> BuildTreeItems(
        IReadOnlyList<AssemblyComponentNode> components,
        string assemblyFilePath,
        IPathResolver resolver,
        BomOptions options,
        List<string> warnings,
        int currentDepth)
    {
        var result = new List<BomItem>();
        if (components == null || components.Count == 0 || currentDepth >= options.MaxDepth)
            return result;

        // Group identical components at this level
        var groups = components
            .Where(c => (!c.IsSuppressed || options.IncludeSuppressed) &&
                        (!c.IsHidden || options.IncludeHidden) &&
                        (!c.ExcludeFromBom || options.IncludeExcludedFromBom))
            .GroupBy(c => (c.FilePath, c.ConfigurationName));

        int itemIndex = 1;
        foreach (var group in groups)
        {
            var first = group.First();
            string resolvedPath = resolver.Resolve(first.FilePath, assemblyFilePath) ?? string.Empty;
            if (string.IsNullOrEmpty(resolvedPath) && !string.IsNullOrEmpty(first.FilePath) && !first.IsVirtual)
            {
                warnings.Add($"Component file not found on disk: {first.FilePath}");
            }

            var item = new BomItem
            {
                ItemNumber = itemIndex++,
                PartNumber = Path.GetFileNameWithoutExtension(first.FilePath),
                Description = first.Name,
                ConfigurationName = first.ConfigurationName,
                Quantity = group.Count(),
                FilePath = first.FilePath,
                ResolvedFilePath = string.IsNullOrEmpty(resolvedPath) ? null : resolvedPath,
                IsVirtual = first.IsVirtual,
                InstanceNames = group.Select(g => g.InstanceName).ToArray()
            };

            // Recursively process sub-assemblies if children exist in the node
            var childNodes = group.SelectMany(g => g.Children).ToList();
            if (childNodes.Count > 0)
            {
                item.Children = BuildTreeItems(childNodes, resolvedPath.Length > 0 ? resolvedPath : assemblyFilePath, resolver, options, warnings, currentDepth + 1);
            }
            else if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath) &&
                     resolvedPath.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var subAsm = new SldAssembly(resolvedPath, readOnly: true);
                    item.Children = BuildTreeItems(subAsm.GetAssemblyTree(), resolvedPath, resolver, options, warnings, currentDepth + 1);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to expand subassembly '{resolvedPath}': {ex.Message}");
                }
            }

            result.Add(item);
        }

        return result;
    }

    private static List<BomItem> BuildFlatItems(List<BomItem> treeItems)
    {
        var aggregated = new Dictionary<(string PartNumber, string Cfg), BomItem>();

        void Flatten(IEnumerable<BomItem> items)
        {
            foreach (var it in items)
            {
                // If it has children, recurse children
                if (it.Children != null && it.Children.Count > 0)
                {
                    Flatten(it.Children);
                }
                else
                {
                    var key = (it.PartNumber, it.ConfigurationName);
                    if (aggregated.TryGetValue(key, out var existing))
                    {
                        existing.Quantity += it.Quantity;
                    }
                    else
                    {
                        aggregated[key] = new BomItem
                        {
                            PartNumber = it.PartNumber,
                            Description = it.Description,
                            ConfigurationName = it.ConfigurationName,
                            Quantity = it.Quantity,
                            FilePath = it.FilePath,
                            ResolvedFilePath = it.ResolvedFilePath,
                            IsVirtual = it.IsVirtual,
                            InstanceNames = it.InstanceNames
                        };
                    }
                }
            }
        }

        Flatten(treeItems);

        int num = 1;
        var list = new List<BomItem>();
        foreach (var item in aggregated.Values.OrderBy(v => v.PartNumber))
        {
            item.ItemNumber = num++;
            list.Add(item);
        }

        return list;
    }
}
