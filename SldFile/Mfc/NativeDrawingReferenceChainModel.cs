using System;

namespace SldFile.Mfc;

// moReferenceChain_c descriptor 0x4DAC95D0, vtable 0x4D1751F8
// -> 0x4B6CBC90. Ordinary archive: mode, tagged feature list,
// integer state and inline referenced configuration.
internal sealed class NativeDrawingReferenceChainModel : IMfcSerializable
{
    internal ushort Mode;
    internal NativeDrawingObjectGraph.Node? Features;
    internal int State20, ConfigurationId;
    internal string? Configuration;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy reference-chain layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Reference-chain export layout is not decoded.");
        ar.U16(ref Mode); NativeDrawingModelFactory.Graph(ar).Object(ar, ref Features); ar.I32(ref State20);
        NativeDrawingReferencedConfiguration.Serialize(ar, ref Configuration, ref ConfigurationId);
    }
}
