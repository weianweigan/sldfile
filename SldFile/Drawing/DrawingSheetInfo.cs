using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Information about a drawing sheet extracted from SolidWorks drawing metadata.
/// </summary>
public sealed class DrawingSheetInfo
{
    public string Name { get; internal set; } = string.Empty;
    public int SheetIndex { get; internal set; }
    public byte[]? PreviewPng { get; internal set; }
    public IReadOnlyList<DrawingViewInfo> Views { get; internal set; } = Array.Empty<DrawingViewInfo>();
}

/// <summary>
/// Information about a drawing view on a drawing sheet.
/// </summary>
public sealed class DrawingViewInfo
{
    public string Name { get; internal set; } = string.Empty;
    public string ReferencedModelPath { get; internal set; } = string.Empty;
    public string ReferencedConfiguration { get; internal set; } = string.Empty;
}
