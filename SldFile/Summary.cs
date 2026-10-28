using System;

namespace SldFile;

/// <summary>
/// Represents the standard OLE "\u0005SummaryInformation" stream.
/// This information is primarily used by the Windows operating system (File Explorer)
/// to display file properties and perform indexing.
/// </summary>
internal sealed class Summary
{
    /// <summary>Equivalent to 'SolidWorksInformation.Author'. Synchronized automatically.</summary>
    public string? Author { get; internal set; }
    
    /// <summary>Equivalent to 'SolidWorksInformation.Keywords'. Synchronized automatically.</summary>
    public string? Keywords { get; internal set; }
    
    /// <summary>Equivalent to 'SolidWorksInformation.Comments'. Synchronized automatically.</summary>
    public string? Comments { get; internal set; }
    
    /// <summary>Equivalent to 'SolidWorksInformation.Title'. Synchronized automatically.</summary>
    public string? Title { get; internal set; }

    /// <summary>Equivalent to 'SolidWorksInformation.Subject'. Synchronized automatically.</summary>
    public string? Subject { get; internal set; } 
    
    #region Statistics
    /// <summary>File creation date. Synchronized from ISolidWorksInformation.</summary>
    public DateTime Created { get; internal set; }
    
    /// <summary>Last modification date. Synchronized from ISolidWorksInformation.</summary>
    public DateTime LastSaved { get; internal set; }
    
    /// <summary>The user who last saved the file. Equivalent to 'SolidWorksInformation.LastSavedBy'.</summary>
    public string? LastSavedBy { get; internal set; }
    #endregion
}