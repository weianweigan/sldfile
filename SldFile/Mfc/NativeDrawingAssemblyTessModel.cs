using System;
using System.IO;

namespace SldFile.Mfc;

// moViewerComponentData Serialize 0x4C26A7B0; modern layout >=0x42F3.
internal sealed class NativeDrawingViewerComponentModel : IMfcSerializable
{
    internal sealed class NameGroup { internal string? Key; internal string?[] Names = Array.Empty<string?>(); }
    internal sealed class Triple { internal int First, Second, Third; }
    internal string? Name, Document, Configuration, DisplayState, Name160, Name308;
    internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
    internal double[] Bounds = new double[10];
    internal NativeDrawingObjectGraph.Node? VisualProperties, GraphicsReferences;
    internal int State110, State158, Type16C, State170, State174, NamesIndicator, HasGraphicsReferences, State28E;
    internal uint State138, Time;
    internal int[] Ids = Array.Empty<int>(), States = new int[10], TailStates = new int[9];
    internal string?[] Labels = new string?[4], Names = Array.Empty<string?>();
    internal NameGroup[] NameGroups = Array.Empty<NameGroup>();
    internal double Scalar190;
    internal ushort State248;
    internal byte State168;
    internal NativeDrawingReferenceGeometryModel ReferenceGeometry = new NativeDrawingReferenceGeometryModel();
    internal Triple[] Triples = Array.Empty<Triple>();
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 0x42F3) throw new NotSupportedException("Legacy viewer-component layout is not implemented.");
        DrawingPreviewRecordModel.String(ar, ref Name); Transform.Serialize(ar);
        for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref VisualProperties);
        DrawingPreviewRecordModel.String(ar, ref Document); ar.I32(ref State110); ar.U32(ref State138); ar.U32(ref Time);
        DrawingPreviewRecordModel.String(ar, ref Configuration); DrawingPreviewRecordModel.String(ar, ref DisplayState);
        ar.I32(ref State158); NativeDrawingFacetModel.Ints(ar, ref Ids); ar.I32(ref Type16C);
        if (Type16C != 6) { ar.I32(ref State170); ar.I32(ref State174); }
        ar.F64(ref Scalar190);
        for (int i = 0; i < States.Length; i++) ar.I32(ref States[i]);
        for (int i = 0; i < Labels.Length; i++) DrawingPreviewRecordModel.String(ar, ref Labels[i]);
        ar.I32(ref NamesIndicator);
        if (NamesIndicator != 0) NativeDrawingDisplayArchive.Strings(ar, ref Names, false);
        int groups = NameGroups.Length; NativeDrawingDisplayArchive.Count(ar, ref groups);
        if (ar.IsLoading) NameGroups = new NameGroup[groups];
        for (int i = 0; i < NameGroups.Length; i++)
        {
            if (ar.IsLoading) NameGroups[i] = new NameGroup();
            DrawingPreviewRecordModel.String(ar, ref NameGroups[i].Key);
            NativeDrawingDisplayArchive.Strings(ar, ref NameGroups[i].Names, false);
        }
        for (int i = 0; i < 7; i++) ar.I32(ref TailStates[i]);
        ar.U16(ref State248); ar.I32(ref TailStates[7]); ar.I32(ref TailStates[8]);
        ar.I32(ref HasGraphicsReferences); if (HasGraphicsReferences != 0) graph.Object(ar, ref GraphicsReferences);
        DrawingPreviewRecordModel.String(ar, ref Name160); ReferenceGeometry.Serialize(ar);
        int count = Triples.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) Triples = new Triple[count];
        for (int i = 0; i < Triples.Length; i++)
        {
            if (ar.IsLoading) Triples[i] = new Triple();
            ar.I32(ref Triples[i].First); ar.I32(ref Triples[i].Second); ar.I32(ref Triples[i].Third);
        }
        DrawingPreviewRecordModel.String(ar, ref Name308); ar.I32(ref State28E); ar.Byte(ref State168);
    }
}

// uoTempAssemblySHDData Serialize 0x4A03AB80; subassembly serializer
// 0x4A03D390 is an exact thunk to the same body.
internal sealed class NativeDrawingAssemblyTessModel : IMfcSerializable
{
    internal NativeDrawingViewerComponentModel Component = new NativeDrawingViewerComponentModel();
    internal NativeDrawingObjectGraph.Node? Part, Parent;
    internal int State320, State358, DecalCount, State370;
    internal NativeDrawingObjectGraph.Node?[] Children = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal int[] Ids = Array.Empty<int>();
    public void Serialize(MfcArchive ar)
    {
        Component.Serialize(ar);
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Part);
        ar.I32(ref State320); NativeDrawingObjectArray.Serialize(ar, ref Children); graph.Object(ar, ref Parent);
        ar.I32(ref State358); NativeDrawingDisplayArchive.Count(ar, ref DecalCount);
        if (DecalCount != 0) throw new NotSupportedException($"Assembly decals are not implemented at {ar.Position}.");
        ar.I32(ref State370); NativeDrawingFacetModel.Ints(ar, ref Ids);
    }
}
