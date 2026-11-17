using System;

namespace SldFile.Mfc;

// moNode::SerializeLWData 0x4C1BAEE0 followed by
// moConcept::SerializeLWData 0x4C1BA8A0 (ordinary drawing Definition archive).
internal sealed class NativeDrawingNodeLightweightModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? Name, VisualProperties;
    internal int Flags, State28, Id, State2C, HasChildren;
    internal string? State20;
    internal NativeDrawingNodeListModel Children = new NativeDrawingNodeListModel();
    public void Serialize(MfcArchive ar)
    {
        SerializeNode(ar);
        SerializeConcept(ar);
    }

    // moParameter uses moNode's LW serializer, but its resolved-data virtual
    // is empty. Keep the concept-only tail out of the parameter wire payload.
    internal void SerializeNode(MfcArchive ar)
    {
        if (ar.DocumentVersion < 0xC7D) throw new NotSupportedException("Unverified legacy lightweight concept layout.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref Name);
        ar.I32(ref Flags); ar.I32(ref State28); ar.I32(ref Id);
        if (ar.DocumentVersion >= 0x143B) ar.I32(ref State2C);
        if (ar.DocumentVersion >= 0x23CE) DrawingPreviewRecordModel.String(ar, ref State20);
    }

    private void SerializeConcept(MfcArchive ar)
    {
        var graph = NativeDrawingModelFactory.Graph(ar);
        ar.I32(ref HasChildren); if (HasChildren != 0) Children.Serialize(ar);
        graph.Object(ar, ref VisualProperties);
    }
}

// moNodeList::Serialize 0x4C1B9AA0: suObList base and an i32 terminator.
internal sealed class NativeDrawingNodeListModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node?[] Values = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal int Terminator;
    public void Serialize(MfcArchive ar)
    {
        NativeDrawingObjectArray.Serialize(ar, ref Values); ar.I32(ref Terminator);
    }
}
