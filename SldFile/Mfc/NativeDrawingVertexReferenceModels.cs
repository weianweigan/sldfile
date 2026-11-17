using System;
using SldFile.Binary;
using SldFile.Drawing;

namespace SldFile.Mfc;

// Ordinary moCompBEntRef 0x4BBF3B10. moCompVertex uses this method
// directly (CreateObject 0x4BBE6C00, final vtable 0x4CEA83A0).
// Edge subclasses use the exact same base before their composite-edge tail.
internal class NativeDrawingComponentEntityReferenceModel : NativeComponentReferenceState, IMfcSerializable
{
    internal byte HasAlias;
    internal string? Alias;
    internal NativeDrawingObjectGraph.Node? EntityReference, ExternalObject;
    public virtual void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy component entity reference.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Component entity-reference export layout is not decoded.");
        SerializeReferenceBase(ar);
        var graph = NativeDrawingModelFactory.Graph(ar);
        ar.Byte(ref HasAlias); if (HasAlias != 0) DrawingPreviewRecordModel.String(ar, ref Alias);
        graph.Object(ar, ref EntityReference); graph.Object(ar, ref ExternalObject);
    }
}

// moVertexRef Serialize 0x4BBF9C70 first calls the moBEntRef codec
// already used by MfcEdgeRefModel. Only the vertex-specific tail is new.
internal sealed class NativeDrawingVertexReferenceModel : IMfcSerializable
{
    internal MfcEdgeRefModel.NativeState Reference = new MfcEdgeRefModel.NativeState();
    internal int HasPoint;
    internal DrawingPreviewPoint Point;
    internal int[] DrawingStatesF8 = new int[4];
    public void Serialize(MfcArchive ar)
    {
        Reference.Serialize(ar);
        ar.I32(ref HasPoint); if (HasPoint != 0) DrawingPreviewRecordModel.Point(ar, ref Point);
        if (ar.DocumentVersion > 0x3357)
        {
            bool drawing = ar.Context.NativeDrawingOwnerIsDrawing
                ?? throw new InvalidOperationException("Vertex-reference drawing-owner context is required.");
            if (drawing)
            {
                if (DrawingStatesF8.Length != 4) throw new System.IO.InvalidDataException("Four drawing vertex states are required.");
                for (int i = 0; i < 4; i++) ar.I32(ref DrawingStatesF8[i]);
            }
        }
    }
}
