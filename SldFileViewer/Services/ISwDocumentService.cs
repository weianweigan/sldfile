using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SldFile;
using SldFile.Assembly;
using SldFile.Mfc;
using SldFileViewer.Models;

namespace SldFileViewer.Services;

public interface ISwDocumentService : IDisposable
{
    SwFile? CurrentFile { get; }
    string? CurrentFilePath { get; }
    bool IsOpen { get; }

    Task<SwFile?> OpenAsync(string filePath);
    void Close();

    IReadOnlyList<Configuration> GetConfigurations();
    IReadOnlyList<Feature> GetFeatures();
    IReadOnlyList<SketchFeatureModel> GetSketches(int configIndex = 0);
    DisplayMesh? GetDisplayMesh();
    // Returns the selected instance geometry in assembly coordinates (millimeters).
    DisplayMesh? GetComponentDisplayMesh(AssemblyComponentNode component);
    IReadOnlyList<AssemblyComponentNode> GetAssemblyTree(string? configName = null);
    IReadOnlyList<BomItem> GetBom(string? configName = null);
    IReadOnlyList<CustomPropertyItem> GetCustomProperties(string? configName = null);
    IReadOnlyList<EquationItem> GetEquations();
    byte[]? GetPreviewPng(int configIndex = 0);
    byte[]? GetPreviewBitmap(int configIndex = 0);
    byte[]? GetBestPreviewImage(int configIndex = 0);
    IReadOnlyList<DrawingSheetInfo> GetDrawingSheets();
    byte[]? GetSheetPreviewPng(int sheetIndex);
    byte[]? GetBestSheetPreviewImage(int sheetIndex);
    void SaveCustomProperties(IEnumerable<CustomPropertyItem> properties, string? configName = null);
}
