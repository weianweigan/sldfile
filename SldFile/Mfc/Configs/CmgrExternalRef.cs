namespace SldFile.Mfc.Configs;

/// <summary>
/// CmgrExternalRef is a model for external references in the Contents/CMgr binary stream using MFC CArchive serialization.
/// CmgrExternalRef = Configuration manager external reference, which is a file path to an external component referenced by the assembly or drawing.
/// </summary>
public sealed class CmgrExternalRef
{
    public string FullPath { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;
}
