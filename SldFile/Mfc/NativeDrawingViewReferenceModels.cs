using System;

namespace SldFile.Mfc;

// moDVAlign_c descriptor 0x4DB070F0, vtable 0x4D28A008 -> 0x4C96E0A0.
internal sealed class NativeDrawingViewAlignmentModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? View;
    internal byte HasDirection;
    internal SldFile.Drawing.DrawingPreviewPoint Direction;
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref View); ar.Byte(ref HasDirection);
        if (HasDirection != 0) DrawingPreviewRecordModel.Point(ar, ref Direction);
    }
}

// Inline moFRData 0x4BB842A0, reused by component references and moFR.
internal static class NativeDrawingFeatureReferenceData
{
    internal static void Serialize(MfcArchive ar, ref int index, ref uint time)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy feature-reference data.");
        ar.I32(ref index); ar.U32(ref time);
    }
}

// moFR_c descriptor 0x4DAD9FD0, vtable 0x4CF70310 -> 0x4BB845D0.
internal sealed class NativeDrawingFeatureReferenceModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? ExternalObject;
    internal int Index;
    internal uint Time;
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref ExternalObject);
        NativeDrawingFeatureReferenceData.Serialize(ar, ref Index, ref Time);
    }
}
