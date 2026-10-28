using System;
using System.Collections.Generic;
using SldFile.Ole;

namespace SldFile;

/// <summary>
/// Represents the standard OLE "\u0005DocumentSummaryInformation" stream.
/// This stream often contains custom user-defined properties and company categorization.
/// Also used by Windows File Explorer for the "Details" tab.
/// </summary>
internal sealed class DocumentSummary
{
    /// <summary>The company name associated with the document.</summary>
    public string? Company { get; internal set; }
    
    /// <summary>Higher level management associated with the document.</summary>
    public string? Manager { get; internal set; }
    
    /// <summary>The category of the document (e.g., Mechanical, Electrical).</summary>
    public string? Category { get; internal set; }
    
    /// <summary>
    /// User-defined properties (Custom Properties). 
    /// These are the primary custom fields shown in SolidWorks property dialog.
    /// </summary>
    public Dictionary<string, CustomProperty> CustomProperties { get; } = new Dictionary<string, CustomProperty>();
}

internal class CustomProperty
{
    public string Name { get; set; }
    public object? Value { get; set; }
    public OleVtType VTType { get; set; }
    
    public CustomProperty(string name, object? value, OleVtType vtType)
    {
        Name = name;
        Value = value;
        VTType = vtType;
    }
}
