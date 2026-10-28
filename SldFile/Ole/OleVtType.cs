namespace SldFile.Ole;

/// <summary>
/// OLE Variant types used in property set streams.
/// Values match the standard VARENUM enumeration (Microsoft OLE Automation).
/// </summary>
internal enum OleVtType
{
    VT_I2 = 2,
    VT_I4 = 3,
    VT_BOOL = 11,
    VT_LPSTR = 30,
    VT_FILETIME = 64,
}
