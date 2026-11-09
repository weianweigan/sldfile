namespace SldFile.Mfc;

/// <summary>
/// Represents the classification of an MFC 4.2 CArchive WriteObject wire-format class/object tag.
/// </summary>
public enum MfcTagKind
{
    /// <summary>Tag 0x0000: Null object pointer (no Serialize payload follows).</summary>
    Null,

    /// <summary>Tag 0x0001..0x7FFF: Back-reference to an existing object (no new Serialize payload).</summary>
    ObjectReference,

    /// <summary>Tag 0xFFFF: Brand-new class declaration (schema + nameLen + ASCII name).</summary>
    NewClass,

    /// <summary>Tag 0x8001..0x8FFE: Compact back-reference to a previously declared class (index = tag &amp; 0x7FFF).</summary>
    ClassReference,

    /// <summary>Tag is unrecognized or stream reached EOF prematurely.</summary>
    Invalid
}
