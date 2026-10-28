using System;
using System.Collections.Generic;

namespace SldFile;

/// <summary>
/// Represents the internal "ISolidWorksInformation" stream.
/// This is the primary source of truth for SolidWorks-specific metadata and 
/// stores information using UTF-8 encoding.
/// 
/// Most fields here are mirrored to SummaryInformation and DocumentSummaryInformation 
/// for Windows Shell compatibility.
/// </summary>
internal class SolidWorksInformation
{
    /// <summary>Internal File Name.</summary>
    public string? FileName { get; set; }
    
    /// <summary>Physical Folder path when the file was last saved.</summary>
    public string? FolderName { get; set; }
    
    /// <summary>Short date string.</summary>
    public string? ShortDate { get; set; }
    
    /// <summary>Long date string (e.g., "2026年4月13日").</summary>
    public string? LongDate { get; set; }
    
    /// <summary>The name of the configuration these properties belong to.</summary>
    public string? ConfigurationName { get; set; }
    
    /// <summary>Primary Author field. Mirrored to Summary.Author.</summary>
    public string? Author { get; set; }
    
    /// <summary>Keywords for searching. Mirrored to Summary.Keywords.</summary>
    public string? Keywords { get; set; }
    
    /// <summary>General comments. Mirrored to Summary.Comments.</summary>
    public string? Comments { get; set; }
    
    /// <summary>Content Title. Mirrored to Summary.Title.</summary>
    public string? Title { get; set; }
    
    /// <summary>Document Subject. Mirrored to Summary.Subject.</summary>
    public string? Subject { get; set; }
    
    /// <summary>Creation date string.</summary>
    public string? CreatedDate { get; set; }
    
    /// <summary>Last saved date string.</summary>
    public string? LastSavedDate { get; set; }
    
    /// <summary>Last user who modified the file. Mirrored to Summary.LastSavedBy.</summary>
    public string? LastSavedBy { get; set; }
    
    /// <summary>Mass properties serialized string.</summary>
    public string? MassPropConfig0 { get; set; }
    
    /// <summary>Boolean flag (int) indicating if the part has a design table.</summary>
    public int? HasDesignTable { get; set; }
    
    /// <summary>Internal SolidWorks component type ID.</summary>
    public int? ComponentType { get; set; }
    
    /// <summary>
    /// Fallback dictionary for dynamic or unknown properties found in the stream.
    /// </summary>
    public Dictionary<uint, object> CustomProperties { get; } = new Dictionary<uint, object>();
}
