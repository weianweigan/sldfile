using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class CompSketchEntHandleData : NativeDrawingComponentFeatureReferenceModel, IMfcSerializable
{
    internal uint NativeState100;
    internal NativeDrawingObjectGraph.Node? NativeHandle, NativeDisplay;

    // moCompSketchEntHandle::Serialize 0x4BBF7650 -> shared CompRef
    // + moFRData 0x4BBF56F0. Both base records already serve CompRefPlane.
    // Center-mark descriptor 0x4DADD510 -> ctor 0x4B83AE60 sets final
    // vtable 0x4CEA8CB0 -> 0x4BBF47B0, which appends a display-item ref.
    public override void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy component sketch handle.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Component sketch-handle export layout is not decoded.");
        base.Serialize(ar);
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref NativeHandle);
        ar.U32(ref NativeState100);
        if (NativeCenterMark) graph.Object(ar, ref NativeDisplay);
    }
}
