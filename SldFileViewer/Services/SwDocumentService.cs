using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using SldFile;
using SldFile.Assembly;
using SldFile.Drawing;
using SldFile.Sketches;
using SldFileViewer.Models;

namespace SldFileViewer.Services;

public sealed class SwDocumentService : ISwDocumentService
{
    private SwFile? _currentFile;
    private string? _currentFilePath;

    public SwFile? CurrentFile => _currentFile;
    public string? CurrentFilePath => _currentFilePath;
    public bool IsOpen => _currentFile != null;

    public async Task<SwFile?> OpenAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Specified file does not exist.", filePath);

        Close();

        return await Task.Run(() =>
        {
            SwFile sw;
            try
            {
                sw = SwFile.Open(filePath, readOnly: true);
            }
            catch (Exception)
            {
                sw = SwFile.Open(filePath, readOnly: false);
            }

            _currentFile = sw;
            _currentFilePath = filePath;
            return sw;
        });
    }

    public void Close()
    {
        if (_currentFile != null)
        {
            _currentFile.Dispose();
            _currentFile = null;
            _currentFilePath = null;
        }
    }

    public IReadOnlyList<Configuration> GetConfigurations()
    {
        return _currentFile?.Configurations ?? Array.Empty<Configuration>();
    }

    public IReadOnlyList<Feature> GetFeatures()
    {
        return _currentFile?.Features ?? Array.Empty<Feature>();
    }

    public IReadOnlyList<SketchFeature> GetSketches(int configIndex = 0)
    {
        if (_currentFile == null) return Array.Empty<SketchFeature>();
        try
        {
            return _currentFile.GetSketches(configIndex);
        }
        catch
        {
            return Array.Empty<SketchFeature>();
        }
    }

    public DisplayMesh? GetDisplayMesh()
    {
        return _currentFile?.GetDisplayMesh();
    }

    public IReadOnlyList<AssemblyComponentNode> GetAssemblyTree(string? configName = null)
    {
        if (_currentFile == null) return Array.Empty<AssemblyComponentNode>();
        return _currentFile.GetAssemblyTree(configName);
    }

    public IReadOnlyList<BomItem> GetBom(string? configName = null)
    {
        if (_currentFile == null) return Array.Empty<BomItem>();
        return _currentFile.GetBom(configName);
    }

    public IReadOnlyList<CustomPropertyItem> GetCustomProperties(string? configName = null)
    {
        if (_currentFile == null) return Array.Empty<CustomPropertyItem>();

        var result = new List<CustomPropertyItem>();

        // Document-level properties
        foreach (var prop in _currentFile.CustomProperties)
        {
            result.Add(new CustomPropertyItem
            {
                Configuration = "Document",
                Name = prop.Key,
                Value = prop.Value?.ToString() ?? string.Empty,
                Type = DetermineType(prop.Value),
                Expression = prop.Value?.ToString() ?? string.Empty,
                IsEquation = false,
                IsReadOnly = false
            });
        }

        // Configuration-specific properties
        foreach (var config in _currentFile.Configurations)
        {
            if (configName != null && !string.Equals(config.Name, configName, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var prop in config.CustomProperties)
            {
                result.Add(new CustomPropertyItem
                {
                    Configuration = config.Name ?? $"Config-{config.Index}",
                    Name = prop.Key,
                    Value = prop.Value?.ToString() ?? string.Empty,
                    Type = DetermineType(prop.Value),
                    Expression = prop.Value?.ToString() ?? string.Empty,
                    IsEquation = false,
                    IsReadOnly = false
                });
            }
        }

        return result;
    }

    public IReadOnlyList<EquationItem> GetEquations()
    {
        if (_currentFile == null) return Array.Empty<EquationItem>();

        var list = new List<EquationItem>();
        int index = 1;

        // 1. Try extracting equations from XML streams (KeyWords, Tables, Features)
        ExtractEquationsFromXml(list, ref index);

        // 2. Extract driving parameters and dimensions from sketches as dimension equations
        try
        {
            var sketches = _currentFile.GetSketches(0);
            foreach (var sketch in sketches)
            {
                foreach (var dim in sketch.Dimensions)
                {
                    if (dim.SystemValue.HasValue)
                    {
                        double mm = dim.SystemValue.Value * 1000.0;
                        list.Add(new EquationItem
                        {
                            Index = index++,
                            Name = $"\"{dim.Name}@{sketch.Name}\"",
                            Equation = $"{mm:0.##} mm",
                            EvaluatedValue = $"{mm:0.##} mm",
                            Unit = "mm",
                            Type = "Sketch Dimension",
                            Comments = $"Source: {dim.ValueSource}"
                        });
                    }
                }
            }
        }
        catch { }

        // If list is empty, add a placeholder indicating no equations in model
        if (list.Count == 0)
        {
            list.Add(new EquationItem
            {
                Index = 1,
                Name = "(无模型方程式)",
                Equation = "-",
                EvaluatedValue = "-",
                Unit = "-",
                Type = "Info",
                Comments = "当前模型中未检测到方程式或驱动尺寸。"
            });
        }

        return list;
    }

    private void ExtractEquationsFromXml(List<EquationItem> list, ref int index)
    {
        if (_currentFile == null) return;

        // Check if there is XML in KeyWords or Tables
        string[] candidates = { "swXmlContents/KeyWords__ZLB", "swXmlContents/Tables__ZLB", "swXmlContents/Features__ZLB" };
        foreach (var key in candidates)
        {
            if (_currentFile.ZlbContents.TryGetValue(key, out var zlb) && !string.IsNullOrEmpty(zlb.XmlContent))
            {
                try
                {
                    var doc = XDocument.Parse(zlb.XmlContent);
                    var equationElements = doc.Descendants().Where(e =>
                        e.Name.LocalName.Contains("Equation", StringComparison.OrdinalIgnoreCase) ||
                        e.Name.LocalName.Contains("EqnMgr", StringComparison.OrdinalIgnoreCase) ||
                        e.Name.LocalName.Contains("Variable", StringComparison.OrdinalIgnoreCase));

                    foreach (var elem in equationElements)
                    {
                        string name = (string?)elem.Attribute("name") ?? (string?)elem.Attribute("Name") ?? elem.Value;
                        string eq = (string?)elem.Attribute("expr") ?? (string?)elem.Attribute("equation") ?? (string?)elem.Attribute("Equation") ?? elem.Value;
                        string val = (string?)elem.Attribute("value") ?? (string?)elem.Attribute("Value") ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            list.Add(new EquationItem
                            {
                                Index = index++,
                                Name = name,
                                Equation = eq,
                                EvaluatedValue = val,
                                Unit = "mm",
                                Type = "Model Equation",
                                Comments = $"Extracted from {key}"
                            });
                        }
                    }
                }
                catch { }
            }
        }
    }

    public byte[]? GetPreviewPng(int configIndex = 0) => _currentFile?.GetPreviewPng(configIndex);
    public byte[]? GetPreviewBitmap(int configIndex = 0) => _currentFile?.GetPreviewBitmap(configIndex);

    public IReadOnlyList<DrawingSheetInfo> GetDrawingSheets()
    {
        return _currentFile?.GetDrawingSheets() ?? Array.Empty<DrawingSheetInfo>();
    }

    public byte[]? GetSheetPreviewPng(int sheetIndex)
    {
        return _currentFile?.GetSheetPreviewPng(sheetIndex);
    }

    public void SaveCustomProperties(IEnumerable<CustomPropertyItem> properties, string? configName = null)
    {
        if (_currentFile == null || _currentFilePath == null) return;

        // If file was opened read-only, reopen with read-write access to persist changes
        if (_currentFile.IsReadOnly)
        {
            var path = _currentFilePath;
            _currentFile.Dispose();
            _currentFile = SwFile.Open(path, readOnly: false);
        }

        foreach (var item in properties)
        {
            if (string.Equals(item.Configuration, "Document", StringComparison.OrdinalIgnoreCase))
            {
                object parsed = ParseValue(item.Value, item.Type);
                _currentFile.CustomProperties.Set(item.Name, parsed);
            }
            else
            {
                var cfg = _currentFile.Configurations.FirstOrDefault(c => string.Equals(c.Name, item.Configuration, StringComparison.OrdinalIgnoreCase));
                if (cfg != null)
                {
                    object parsed = ParseValue(item.Value, item.Type);
                    cfg.CustomProperties.Set(item.Name, parsed);
                }
            }
        }

        _currentFile.Save();
    }

    private static string DetermineType(object? value)
    {
        return value switch
        {
            int or long or short => "Integer",
            double or float or decimal => "Double",
            bool => "Boolean",
            DateTime => "Date",
            _ => "Text"
        };
    }

    private static object ParseValue(string value, string type)
    {
        return type switch
        {
            "Integer" => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : value,
            "Double" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : value,
            "Boolean" => bool.TryParse(value, out var b) ? b : value,
            "Date" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : value,
            _ => value
        };
    }

    public void Dispose()
    {
        Close();
    }
}
