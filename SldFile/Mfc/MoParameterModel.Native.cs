using System;

namespace SldFile.Mfc;

public abstract partial class MoParameterModel
{
    // Set only by the native graph factory. Existing aggregate serialization
    // stays available to existing callers; no second parameter MFC type.
    internal bool NativeWire;
    internal NativeDrawingNodeLightweightModel NativeNode = new NativeDrawingNodeLightweightModel();
    internal NativeDrawingObjectGraph.Node? NativeParameterName;
    internal byte NativeHasOverride;
    internal double NativeOverride;

    // moLengthParameter_c's vtable +28 -> 0x4C1B80B0 (double parameter),
    // base 0x4C1BA420, moNode 0x4C1B9DE0 -> LW 0x4C1BAEE0, empty resolved.
    // Ordinary archive only: no DB or helper condition 0x80.
    private void SerializeNative(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native parameter layout.");
        if (ar.IsStoring && NativeNode.Name?.Payload is NativeDrawingStringHandleModel name) name.Value = Name;
        NativeNode.SerializeNode(ar);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeParameterName);
        double value = Value; ar.F64(ref value); if (ar.IsLoading) Value = value;
        ar.Byte(ref NativeHasOverride);
        if (NativeHasOverride != 0) ar.F64(ref NativeOverride);
        if (ar.IsLoading && NativeNode.Name?.Payload is NativeDrawingStringHandleModel loaded) Name = loaded.Value ?? string.Empty;
    }
}
