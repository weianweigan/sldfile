using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class FavoriteHandleData : IMfcSerializable
{
    internal int State8, StateC;
    internal uint State10;
    // Descriptor 0x4DAD7FE0, vtable 0x4CEA4C90 -> Serialize 0x4BB514C0.
    // This reuses the existing type; no presumed six-byte/raw payload.
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy favorite-handle layout.");
        ar.I32(ref State8); ar.I32(ref StateC); ar.U32(ref State10);
    }
}
