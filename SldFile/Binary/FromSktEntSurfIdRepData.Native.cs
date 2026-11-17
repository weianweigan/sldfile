using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class FromSktEntSurfIdRepData : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? NativeFeature, NativeFeatureReference;
    internal int NativeState38;

    // descriptor 0x4DADD0F0, CreateObject 0x4BBE6530 sets final vtable
    // 0x4CF7FD78 -> 0x4BBF9B00. Reuses the same entity-ID feature link
    // as EndFaceSurfIdRep; this subclass appends only one i32.
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy sketch-entity surface ID.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Surface-ID export layout is not decoded.");
        NativeEntityFeatureReference.Serialize(ar, ref NativeFeature, ref NativeFeatureReference);
        ar.I32(ref NativeState38);
    }
}
