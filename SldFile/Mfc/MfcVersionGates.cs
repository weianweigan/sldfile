namespace SldFile.Mfc;

/// <summary>
/// SolidWorks document version thresholds (decimal) decoded from swdocumentmgr.dll and CFB stream structures.
/// Used for version-gated MFC field serialization to prevent cursor desynchronization across document versions.
/// </summary>
internal static class MfcVersionGates
{
    /// <summary>Early config unknown field introducing DWORD this+0x38 (decimal 1341 / 0x053D).</summary>
    public const int CfgVerUnk1 = 1341;

    /// <summary>Derived configuration parent ID introducing parent_index (decimal 2037 / 0x07F5).</summary>
    public const int CfgVerParent = 2037;

    /// <summary>Switch from CMgrHdr to CMgrHdr2 stream name (decimal 2070 / 0x0816, SolidWorks 2000).</summary>
    public const int CMgrHdr2Switch = 2070;

    /// <summary>Config unknown field 2 introducing DWORD this+0x40 (decimal 2136 / 0x0858).</summary>
    public const int CfgVerUnk2 = 2136;

    /// <summary>Config alternate name introducing CStr alt_name (decimal 2610 / 0x0A32).</summary>
    public const int CfgVerAltName = 2610;

    /// <summary>SolidWorks 2008 field gate (decimal 3928 / 0x0F58).</summary>
    public const int Sw2008 = 3928;

    /// <summary>SolidWorks 2012 field gate (decimal 5000 / 0x1388).</summary>
    public const int Sw2012 = 5000;

    /// <summary>SolidWorks 2018 field gate (decimal 11142 / 0x2B86).</summary>
    public const int Sw2018 = 11142;

    /// <summary>SolidWorks 2021 field gate (decimal 12155 / 0x2F7B).</summary>
    public const int Sw2021 = 12155;

    /// <summary>SolidWorks 2024 field gate (decimal 12216 / 0x2FB8).</summary>
    public const int Sw2024 = 12216;
}
