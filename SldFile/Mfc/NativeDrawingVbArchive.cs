using System;

namespace SldFile.Mfc;

// uiDrawingHLRShadedObject_c::Serialize 0x3E0D6BA0 followed by
// uiDrawingShadedData_c::Serialize 0x3E0D6D40 (slddrwu.dll).
internal sealed class NativeDrawingVbArchive : IMfcSerializable
{
    internal int DocumentVersion;
    internal NativeDrawingObjectGraph.Node?[] Views = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal int ViewBucketsEnd, HasShadedData;
    internal int[] ViewIndices = Array.Empty<int>(), TessellationIndices = Array.Empty<int>();
    internal string?[][] HiddenComponents = Array.Empty<string?[]>(), OtherHiddenComponents = Array.Empty<string?[]>();
    internal int TessellationCount;
    internal Tessellation[] Tessellations = Array.Empty<Tessellation>();
    internal int[][] VisibleAtoms = Array.Empty<int[]>(), VisibleSurfaceAtoms = Array.Empty<int[]>(), HiddenBodies = Array.Empty<int[]>();

    internal sealed class Tessellation : IMfcSerializable
    {
        internal int DocumentType, HasLights;
        internal string? Configuration;
        internal ushort State;
        internal NativeDrawingObjectGraph.Node?[] Lights = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal NativeDrawingObjectGraph.Node? VisualProperties;
        internal NativeDrawingPartTessModel Part = new NativeDrawingPartTessModel();
        internal NativeDrawingObjectGraph.Node?[] Assembly = Array.Empty<NativeDrawingObjectGraph.Node?>();
        public void Serialize(MfcArchive ar)
        {
            // uiDrawingTessObject_c::Serialize 0x3E0D7C40.
            ar.I32(ref DocumentType); DrawingPreviewRecordModel.String(ar, ref Configuration); ar.U16(ref State);
            ar.I32(ref HasLights);
            var graph = NativeDrawingModelFactory.Graph(ar);
            if (HasLights != 0)
            {
                uint count = (uint)Lights.Length; ar.Count(ref count);
                if (ar.IsLoading) Lights = new NativeDrawingObjectGraph.Node?[count];
                for (int i = 0; i < Lights.Length; i++) graph.Object(ar, ref Lights[i]);
            }
            graph.Object(ar, ref VisualProperties);
            if (DocumentType == 0) NativeDrawingObjectArray.Serialize(ar, ref Assembly);
            else Part.Serialize(ar);
        }
    }

    public void Serialize(MfcArchive ar)
    {
        if (ar.IsLoading) DocumentVersion = ar.DocumentVersion; else ar.DocumentVersion = DocumentVersion;
        if (DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy drawing VBLists envelope.");
        var graph = NativeDrawingModelFactory.CreateGraph(); ar.Context.NativeDrawingObjects = graph;
        int count = Views.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) Views = new NativeDrawingObjectGraph.Node?[count];
        for (int i = 0; i < Views.Length; i++) graph.Object(ar, ref Views[i]);
        ViewBucketsEnd = ar.Position;
        ar.I32(ref HasShadedData);
        if (HasShadedData == 0) return;
        Integers(ar, ref ViewIndices, count); Integers(ar, ref TessellationIndices, count);
        ComponentLists(ar, ref HiddenComponents, count); ComponentLists(ar, ref OtherHiddenComponents, count);
        NativeDrawingDisplayArchive.Count(ar, ref TessellationCount);
        if (ar.IsLoading) Tessellations = new Tessellation[TessellationCount];
        for (int i = 0; i < Tessellations.Length; i++)
        {
            if (ar.IsLoading) Tessellations[i] = new Tessellation();
            Tessellations[i].Serialize(ar);
        }
        AtomLists(ar, ref VisibleAtoms, count); AtomLists(ar, ref VisibleSurfaceAtoms, count); AtomLists(ar, ref HiddenBodies, count);
    }

    private static void Integers(MfcArchive ar, ref int[] values, int count)
    {
        if (ar.IsLoading) values = new int[count];
        if (values.Length != count) throw new InvalidOperationException("Native shaded view count mismatch.");
        for (int i = 0; i < values.Length; i++) ar.I32(ref values[i]);
    }

    private static void ComponentLists(MfcArchive ar, ref string?[][] values, int count)
    {
        if (ar.IsLoading) values = new string?[count][];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) values[i] = Array.Empty<string?>();
            NativeDrawingDisplayArchive.Strings(ar, ref values[i], false);
        }
    }

    // uiDrawingShadedVisibleAtomsList / VisibleSurfaceAtoms / HiddenBodies
    // share inline serializer 0x3E0D76C0: i32 count followed by i32 IDs.
    private static void AtomLists(MfcArchive ar, ref int[][] values, int count)
    {
        if (ar.IsLoading) values = new int[count][];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) values[i] = Array.Empty<int>();
            NativeDrawingFacetModel.Ints(ar, ref values[i]);
        }
    }
}
