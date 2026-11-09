using System;
using System.Collections.Generic;

namespace SldFile.Binary;

/// <summary>
/// Document type for the Definition stream.
/// </summary>
internal enum DefinitionFileType
{
    Unknown = 0,
    Part = 0x51,
    Assembly = 0x68,
    Drawing = 0x50
}

/// <summary>
/// Definition stream: document-wide settings and metadata.
/// Located at Contents/Definition. Part/Asm (~5KB) and Drawing (~145KB) have different structures.
/// </summary>
internal sealed class DefinitionData
{
    /// <summary>Document type identified by header signature.</summary>
    public DefinitionFileType FileType { get; internal set; }

    /// <summary>Line style name (Part/Asm only, e.g., "CONTINUOUS").</summary>
    public string? LineStyle { get; internal set; }

    /// <summary>Line font name (Part/Asm only, e.g., "Detail").</summary>
    public string? LineFont { get; internal set; }

    /// <summary>Line font config name (Part/Asm only, e.g., "CONTINUOUS").</summary>
    public string? LineFontConfig { get; internal set; }

    /// <summary>User name from uiUserModelEnv_c (Part/Asm only).</summary>
    public string? UserName { get; internal set; }

    /// <summary>Unit system class names observed in the stream (Drawing only).</summary>
    public IReadOnlyList<string> UnitSystemClasses { get; internal set; } = Array.Empty<string>();

    /// <summary>Drawing sheets discovered in the Definition stream via MFC deserialization.</summary>
    public IReadOnlyList<DrawingSheetDefinition> Sheets { get; internal set; } = Array.Empty<DrawingSheetDefinition>();

    /// <summary>Drawing views discovered in the Definition stream via MFC deserialization.</summary>
    public IReadOnlyList<DrawingViewDefinition> Views { get; internal set; } = Array.Empty<DrawingViewDefinition>();

    /// <summary>Drawing text notes discovered in the Definition stream via MFC deserialization.</summary>
    public IReadOnlyList<DrawingNoteDefinition> Notes { get; internal set; } = Array.Empty<DrawingNoteDefinition>();

    internal byte[]? RawData;
    internal List<string> UnitSystemClassesBuilder { get; set; } = new();

    internal void Freeze()
    {
        UnitSystemClasses = UnitSystemClassesBuilder.AsReadOnly();
    }
}

/// <summary>
/// A drawing sheet definition deserialized from the Definition stream (moDrSheet).
/// </summary>
internal sealed class DrawingSheetDefinition
{
    public string Name { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
}

/// <summary>
/// A drawing view definition deserialized from the Definition stream (moAbsoluteView, moUnfoldedView, moDetailView, moView_c).
/// </summary>
internal sealed class DrawingViewDefinition
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public int TagOffset { get; set; }
    public double CenterXMm { get; set; }
    public double CenterYMm { get; set; }
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }
}

/// <summary>
/// A drawing note / text annotation deserialized from the Definition stream.
/// </summary>
internal sealed class DrawingNoteDefinition
{
    public string Text { get; set; } = string.Empty;
    public double XMm { get; set; }
    public double YMm { get; set; }
    public double ZMm { get; set; }
    public string Font { get; set; } = "SimSun";
    public double HeightMm { get; set; } = 2.5;
    public double AngleRad { get; set; }
}
