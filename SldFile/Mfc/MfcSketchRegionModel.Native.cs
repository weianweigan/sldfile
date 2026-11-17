namespace SldFile.Mfc;

public sealed partial class MfcSketchRegionModel
{
    internal bool NativeWire;
    internal NativeDrawingObjectGraph.Node? NativeBoundary;

    // moSketchRegion_c vtable 0x4CEF01A8 -> Serialize 0x4B9A6570.
    // The region index/count fields in the old aggregate are not serialized
    // here. A tagged suObList pointer is present from version 0x2399.
    private void SerializeNative(MfcArchive ar)
    {
        if (ar.DocumentVersion > 0x2398)
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeBoundary);
    }
}
