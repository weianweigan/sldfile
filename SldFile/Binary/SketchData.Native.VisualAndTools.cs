using System;
using System.IO;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal ushort NativeState37C, NativeState188, NativeState18C, NativeState190;
    internal NativeLineVisual LineVisual = new NativeLineVisual();
    internal int NativeState568, NativeState578;
    internal uint NativeState56C;
    internal DrawingPreviewPoint NativePoint570;
    internal NativeDrawingObjectGraph.Node?[] NativeToolObjects = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal NativeRelationManager RelationManager = new NativeRelationManager();

    // Ordinary >=5000 archive after the inline +140 state. Native legacy
    // conversion blocks below version 0xA46 add no bytes in this context.
    // Later external-block/display/cache sections remain after this method.
    internal void SerializeNativeVisualAndTools(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch suffix.");
        ar.U16(ref NativeState37C); ar.U16(ref NativeState188);
        if (ar.DocumentVersion >= 0x1801) ar.U16(ref NativeState18C);
        ar.U16(ref NativeState190); LineVisual.Serialize(ar);
        ar.I32(ref NativeState568); DrawingPreviewRecordModel.Point(ar, ref NativePoint570);
        ar.U32(ref NativeState56C); ar.I32(ref NativeState578);
        int count = NativeToolObjects.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) NativeToolObjects = new NativeDrawingObjectGraph.Node?[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < count; i++) graph.Object(ar, ref NativeToolObjects[i]);
        RelationManager.Serialize(ar);
    }

    internal sealed class NativeLineVisual
    {
        internal string? Style;
        internal NativeDrawingLineWidthModel Width = new NativeDrawingLineWidthModel { RestoreType = 3 };
        internal int Color, State14;
        // sldmfcu moLineViz_c::Serialize 0x3CAF2510, param3=0. Reuse the
        // existing utLineWidth codec rather than create another width type.
        internal void Serialize(MfcArchive ar)
        {
            DrawingPreviewRecordModel.String(ar, ref Style); Width.Serialize(ar);
            ar.I32(ref Color); ar.I32(ref State14);
        }
    }

    internal sealed class NativeRelationManager : IMfcSerializable
    {
        internal sealed class Entry { internal string? Key; internal double Value; }
        internal NativeDrawingObjectGraph.Node?[] Objects = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal Entry[] Entries = Array.Empty<Entry>();
        // moSketchRelMgr_c 0x4C629FE0: inline suObList at +78, then map
        // at +40. Native ctor vtables and 0x4C375BA0 establish CString/F64
        // values with CArchive::Count, not the old constraint aggregate.
        public void Serialize(MfcArchive ar)
        {
            NativeDrawingObjectArray.Serialize(ar, ref Objects);
            uint count = (uint)Entries.Length; ar.Count(ref count);
            if (count > int.MaxValue || (ar.IsLoading && count > (uint)ar.Remaining / 9))
                throw new InvalidDataException("Invalid native relation-manager map size.");
            if (ar.IsLoading) Entries = new Entry[count];
            for (int i = 0; i < Entries.Length; i++)
            {
                if (ar.IsLoading) Entries[i] = new Entry();
                DrawingPreviewRecordModel.String(ar, ref Entries[i].Key); ar.F64(ref Entries[i].Value);
            }
        }
    }
}
