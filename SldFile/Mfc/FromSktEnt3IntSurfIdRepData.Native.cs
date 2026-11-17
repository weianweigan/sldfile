using System;
using SldFile.Binary;

namespace SldFile.Mfc;

internal sealed partial class FromSktEnt3IntSurfIdRepData : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? NativeFeature, NativeFeatureReference;
    internal int NativeState38, NativeState3C, NativeState40;

    // CreateObject 0x4BBE3D00 -> vtable 0x4CF880F8 -> shared
    // moEntityIdRep 0x4BBF3640; derived +0x258 = 0x4BBFAA10.
    // Reuse the same inline feature link as the existing surface-ID codecs.
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy three-intersection surface ID.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Surface-ID export layout is not decoded.");
        NativeEntityFeatureReference.Serialize(ar, ref NativeFeature, ref NativeFeatureReference);
        ar.I32(ref NativeState38); ar.I32(ref NativeState3C); ar.I32(ref NativeState40);
    }
}
