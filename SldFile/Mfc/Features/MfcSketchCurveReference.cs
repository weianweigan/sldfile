using System;
using SldFile;

namespace SldFile.Mfc;

/// <summary>
/// Authoritative 52-byte sketch curve reference record used in moCompProfile_c and moWireContact_c.
/// Implements IMfcSerializable for symmetric bi-directional CArchive serialization.
/// Wire format: RefMagic = 0x4A38, EntityId, Serial, Flags, 0, RoleTag, 0, Opaque28, 0, 3 x Sentinel (0xFFFFCFC7), 0.
/// </summary>
public class MfcSketchCurveReference : SketchCurveReference, IMfcSerializable
{
    public const uint RefMagic = 0x4A38;           // 19000 decimal
    public const uint RefSentinel = 0xFFFFCFC7;     // -12345 decimal

    public uint Flags { get; set; } = 0;            // 1 for primary profile, 0 for secondary
    public uint RoleTag { get; set; } = 0x65;       // 0x65 plain ref, 0xA2 primary sweep profile
    public uint Opaque28 { get; set; } = 0xFFFFFFFF;// 0 or 0xFFFFFFFF
    public uint RecordOffset { get; set; }

    public MfcSketchCurveReference()
    {
        ArchiveSerial = 1;
        IsHeaderSelectionIdPositional = true;
    }

    public MfcSketchCurveReference(
        uint entityId,
        SketchCurveRole role = SketchCurveRole.Unknown,
        ushort headerSelectionId = 0,
        uint flags = 0,
        uint roleTag = 0x65)
    {
        EntityId = entityId;
        Role = role;
        HeaderSelectionId = headerSelectionId;
        ArchiveSerial = 1;
        Flags = flags;
        RoleTag = roleTag;
        IsHeaderSelectionIdPositional = true;
    }

    public void Serialize(MfcArchive ar)
    {
        uint magic = RefMagic;
        ar.U32(ref magic);

        uint id = EntityId;
        ar.U32(ref id);
        if (!ar.IsStoring) EntityId = id;

        uint serial = ArchiveSerial;
        ar.U32(ref serial);
        if (!ar.IsStoring) ArchiveSerial = serial;

        uint flags = Flags;
        ar.U32(ref flags);
        if (!ar.IsStoring) Flags = flags;

        uint zero1 = 0;
        ar.U32(ref zero1);

        uint roleTag = RoleTag;
        ar.U32(ref roleTag);
        if (!ar.IsStoring) RoleTag = roleTag;

        uint zero2 = 0;
        ar.U32(ref zero2);

        uint op28 = Opaque28;
        ar.U32(ref op28);
        if (!ar.IsStoring) Opaque28 = op28;

        uint zero3 = 0;
        ar.U32(ref zero3);

        uint s1 = RefSentinel;
        ar.U32(ref s1);
        uint s2 = RefSentinel;
        ar.U32(ref s2);
        uint s3 = RefSentinel;
        ar.U32(ref s3);

        uint zero4 = 0;
        ar.U32(ref zero4);
    }

    /// <summary>Writes the 52-byte null placeholder record (magic + 12 zero dwords).</summary>
    public static void WriteNullPlaceholder(MfcArchive ar)
    {
        uint magic = RefMagic;
        ar.U32(ref magic);
        uint zero = 0;
        for (int i = 0; i < 12; i++)
            ar.U32(ref zero);
    }

    /// <summary>Reads the 52-byte null placeholder record (magic + 12 zero dwords).</summary>
    public static void ReadNullPlaceholder(MfcArchive ar)
    {
        uint magic = 0;
        ar.U32(ref magic);
        uint zero = 0;
        for (int i = 0; i < 12; i++)
            ar.U32(ref zero);
    }
}
