using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class EndFaceSurfIdRepData : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? NativeFeature, NativeFeatureReference;
    internal int NativeState38, NativeState3C;

    // descriptor 0x4DADDCC0, final vtable 0x4CF84E80 -> 0x4BBF3640.
    // Shared moEntityIdRep 0x4BBF7BE0 serializes its inline feature link
    // through 0x4BBF8240; derived virtual +0x258 is 0x4BBFB100.
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy end-face surface ID.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Surface-ID export layout is not decoded.");
        NativeEntityFeatureReference.Serialize(ar, ref NativeFeature, ref NativeFeatureReference);
        ar.I32(ref NativeState38); ar.I32(ref NativeState3C);
    }
}

// moEntityIdRep's inline feature link (+10), vtable 0x4CF762D8 ->
// 0x4BBF8240. Shared by surface-ID subclasses, not an MFC object itself.
internal static class NativeEntityFeatureReference
{
    internal static void Serialize(MfcArchive ar, ref NativeDrawingObjectGraph.Node? feature,
        ref NativeDrawingObjectGraph.Node? reference)
    {
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref feature); graph.Object(ar, ref reference);
    }
}
