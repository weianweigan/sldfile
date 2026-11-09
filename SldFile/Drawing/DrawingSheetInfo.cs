using System;
using System.Collections.Generic;
using SldFile.Drawing.Vector;

namespace SldFile;

/// <summary>
/// Information about a drawing sheet extracted from SolidWorks drawing metadata.
/// </summary>
public sealed class DrawingSheetInfo
{
    public string Name { get; internal set; } = string.Empty;
    public int SheetIndex { get; internal set; }
    public byte[]? PreviewPng { get; internal set; }

    /// <summary>
    /// Higher resolution preview bitmap (e.g. DIB/BMP from Preview stream), if available.
    /// </summary>
    public byte[]? HighResPreview { get; internal set; }

    /// <summary>
    /// Returns the best available preview image data (preferring high-resolution preview over thumbnail).
    /// </summary>
    public byte[]? BestPreview => HighResPreview ?? PreviewPng;

    /// <summary>
    /// Extracted 2D vector graphic metadata (text notes, title block, dimensions) for lossless CAD rendering.
    /// </summary>
    public DrawingSheetVectorData? VectorData { get; internal set; }

    public IReadOnlyList<DrawingViewInfo> Views { get; internal set; } = Array.Empty<DrawingViewInfo>();

    /// <summary>
    /// Technical requirements and general notes extracted from the drawing sheet.
    /// </summary>
    public IReadOnlyList<string> TechnicalNotes { get; internal set; } = Array.Empty<string>();

    /// <summary>
    /// Title block metadata key-values (e.g. Drawing No, Material, Designer).
    /// </summary>
    public IReadOnlyDictionary<string, string> TitleBlockProperties { get; internal set; } = new Dictionary<string, string>();
}

/// <summary>
/// Information about a drawing view on a drawing sheet.
/// </summary>
public sealed class DrawingViewInfo
{
    public string Name { get; internal set; } = string.Empty;
    public string ReferencedModelPath { get; internal set; } = string.Empty;
    public string ReferencedConfiguration { get; internal set; } = string.Empty;

    /// <summary>
    /// MFC view class name (e.g. moAbsoluteView, moUnfoldedView, moDetailView, moView_c).
    /// </summary>
    public string MfcClassName { get; internal set; } = string.Empty;

    /// <summary>
    /// User-friendly view type name (e.g. 主视图, 钣金展开视图, 局部放大视图).
    /// </summary>
    public string ViewType { get; internal set; } = string.Empty;

    /// <summary>
    /// View scale ratio (e.g. 1:50, 1:2).
    /// </summary>
    public string Scale { get; internal set; } = string.Empty;

    /// <summary>
    /// Tag offset in Definition stream.
    /// </summary>
    public int TagOffset { get; internal set; }
}
