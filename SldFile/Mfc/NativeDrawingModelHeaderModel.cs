using System;

namespace SldFile.Mfc;

// The leading section of moModel::SerializeCommon (0x4C26C400), through
// the optional model bbox. The remaining settings and feature graph are
// separate payloads; this type does not represent a complete Definition root.
internal sealed class NativeDrawingModelHeaderModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? Header, Units, Component, Curvature, ReferencePlaneStyle;
    internal int StateA8;
    internal uint StateAC;
    internal NativeDrawingNodeLightweightModel Lightweight = new NativeDrawingNodeLightweightModel();
    internal byte State1A2, HasBounds;
    internal double[] Bounds = new double[10];

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy drawing model header.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        // moFiledConcept::SerializeCommon -> moConcept::SerializeLWData.
        graph.Object(ar, ref Header); ar.I32(ref StateA8); ar.U32(ref StateAC);
        Lightweight.Serialize(ar);
        graph.Object(ar, ref Units);
        // moModel's inline component manager emits no wire bytes at these
        // versions (0x4BB86080); the self component follows it directly.
        graph.Object(ar, ref Component);
        ar.Byte(ref State1A2); graph.Object(ar, ref Curvature);
        graph.Object(ar, ref ReferencePlaneStyle); ar.Byte(ref HasBounds);
        if (HasBounds != 0) for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
    }
}
