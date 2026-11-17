using System;
using System.IO;

namespace SldFile.Mfc;

// moRelMgr_c: factory 0x4C374EF0 -> ctor 0x4C3A2640, final vtable
// 0x4D196248 -> Serialize 0x4C376840. This model-level manager is
// distinct from SketchData.NativeRelationManager (moSketchRelMgr_c).
internal sealed class NativeDrawingRelationManagerModel : IMfcSerializable
{
    internal NativeDrawingObjectListModel Relations = new NativeDrawingObjectListModel();
    internal NamedValue[] NamedValues = Array.Empty<NamedValue>();
    internal string? State170;
    internal int State178, State150, StateF4, State1B0;
    internal uint Time180;
    internal NativeDrawingObjectListModel ObjectsA0 = new NativeDrawingObjectListModel();
    internal NativeDrawingObjectGraph.Node?[] FeatureParameters = Array.Empty<NativeDrawingObjectGraph.Node?>();

    internal sealed class NamedValue { internal string? Name; internal double Value; }

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy model relation manager layout.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Model relation manager export layout is not decoded.");
        Relations.Serialize(ar);
        // Inline map at +10: 0x4C375DF0. Archive count, CString, F64.
        // The store assembly at 0x4C375EDE/EF1 moves the double directly;
        // the decompiler's apparent integer conversion is a type artifact.
        uint count = checked((uint)NamedValues.Length); ar.Count(ref count);
        if (ar.IsLoading)
        {
            if (count > int.MaxValue || count > ar.Remaining / 9u)
                throw new InvalidDataException("Invalid model relation named-value count.");
            NamedValues = new NamedValue[count];
        }
        for (int i = 0; i < NamedValues.Length; i++)
        {
            if (ar.IsLoading) NamedValues[i] = new NamedValue();
            DrawingPreviewRecordModel.String(ar, ref NamedValues[i].Name);
            ar.F64(ref NamedValues[i].Value);
        }
        DrawingPreviewRecordModel.String(ar, ref State170); ar.I32(ref State178);
        ar.U32(ref Time180); ar.I32(ref State150); ar.I32(ref StateF4);
        if (ar.DocumentVersion > 0x23BD) ObjectsA0.Serialize(ar);
        if (ar.DocumentVersion > 0x336A)
        {
            ar.I32(ref State1B0);
            int length = FeatureParameters.Length; NativeDrawingModelManagersModel.Count(ar, ref length, 2);
            if (ar.IsLoading) FeatureParameters = new NativeDrawingObjectGraph.Node?[length];
            var graph = NativeDrawingModelFactory.Graph(ar);
            for (int i = 0; i < length; i++) graph.Object(ar, ref FeatureParameters[i]);
        }
        // Remaining native calls rebuild runtime indices and TLS caches only.
    }
}
