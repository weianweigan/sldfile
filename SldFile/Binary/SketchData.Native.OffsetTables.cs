using System;
using System.IO;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeOffset[] NativeOffsets = Array.Empty<NativeOffset>();
    internal NativeSketchOffset[] NativeSketchOffsets = Array.Empty<NativeSketchOffset>();
    internal NativeStyledCurve[] NativeStyledCurves = Array.Empty<NativeStyledCurve>();

    // Three u16-count tables immediately after 0x4C5C9F70. The intersection
    // table and remaining sgSketch suffix still follow this section.
    internal void SerializeNativeOffsetAndStyleTables(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy sketch offset tables.");
        RelationRows(ar, ref NativeOffsets, row => row.Serialize(ar));
        RelationRows(ar, ref NativeSketchOffsets, row => row.Serialize(ar));
        RelationRows(ar, ref NativeStyledCurves, row => row.Serialize(ar));
    }

    internal sealed class NativeOffset
    {
        internal sealed class Entry
        {
            internal NativeDrawingObjectGraph.Node? Reference, Handle;
            internal uint ParentIndex;
            internal ushort HasHandle;
        }
        internal Entry[] Entries = Array.Empty<Entry>();
        internal double Distance;
        internal ushort State38, Kind, State50, State60, State68;
        internal int State64, StateC8, State48, StateA8;
        internal NativeDrawingObjectGraph.Node? Reference;
        internal DrawingPreviewPoint FirstPoint, SecondPoint, Direction;
        internal int[] EntityIndices = Array.Empty<int>();

        // sgOffset_c 0x4C5B6A40, ordinary archive (no DB/pointer-export modes).
        internal void Serialize(MfcArchive ar)
        {
            var graph = NativeDrawingModelFactory.Graph(ar);
            ushort count = RelationCount(Entries.Length); ar.U16(ref count);
            if (ar.IsLoading) Entries = new Entry[count];
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) Entries[i] = new Entry();
                graph.Object(ar, ref Entries[i].Reference);
            }
            for (int i = 0; i < count; i++) ar.U32(ref Entries[i].ParentIndex);
            for (int i = 0; i < count; i++)
            {
                ar.U16(ref Entries[i].HasHandle);
                if (Entries[i].HasHandle == 1) graph.Object(ar, ref Entries[i].Handle);
            }
            ar.F64(ref Distance); ar.U16(ref State38); ar.U16(ref Kind); ar.U16(ref State50);
            graph.Object(ar, ref Reference); ar.U16(ref State60); ar.U16(ref State68);
            ar.I32(ref State64); ar.I32(ref StateC8); ar.I32(ref State48);
            DrawingPreviewRecordModel.Point(ar, ref FirstPoint); DrawingPreviewRecordModel.Point(ar, ref SecondPoint);
            if (Kind < 7 || Kind > 9) return;
            if (ar.IsLoading) EntityIndices = new int[count];
            if (EntityIndices.Length != count) throw new InvalidDataException("Native offset index count mismatch.");
            for (int i = 0; i < count; i++) ar.I32(ref EntityIndices[i]);
            DrawingPreviewRecordModel.Point(ar, ref Direction);
            if (ar.DocumentVersion > 0x23BA) ar.I32(ref StateA8);
            if (Kind != 7 && ar.DocumentVersion > 0x2BBE)
                throw new NotSupportedException("Native offset embedded kernel body is not decoded.");
        }
    }

    internal sealed class NativeSketchOffset
    {
        internal int[] EntityIndices = Array.Empty<int>();
        internal int State38, State34, StateA4, StateA8;
        internal double Distance;
        internal DrawingPreviewPoint FirstPoint, SecondPoint;
        internal ushort State90;
        internal NativeDrawingObjectGraph.Node? ProfileData;
        // sgSketchOffset_c 0x4C5C2F50.
        internal void Serialize(MfcArchive ar)
        {
            int count = EntityIndices.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) EntityIndices = new int[count];
            for (int i = 0; i < count; i++) ar.I32(ref EntityIndices[i]);
            if (count > 0)
            {
                ar.I32(ref State38); ar.I32(ref State34); ar.F64(ref Distance);
                DrawingPreviewRecordModel.Point(ar, ref FirstPoint); DrawingPreviewRecordModel.Point(ar, ref SecondPoint);
                ar.U16(ref State90);
            }
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref ProfileData);
            if (ar.DocumentVersion > 0x1FDB) { ar.I32(ref StateA4); ar.I32(ref StateA8); }
        }
    }

    internal sealed class NativeStyledCurve
    {
        internal NativeDrawingObjectGraph.Node?[] Handles = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal NativeDynamicU16Array Indices = new NativeDynamicU16Array();
        internal int State30, State40, State34, State38;
        internal ushort State3C;
        internal double State10, State8;
        // sgStyledCurve 0x4C5C6760; inline record, shared handle and array codecs.
        internal void Serialize(MfcArchive ar)
        {
            int count = Handles.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) Handles = new NativeDrawingObjectGraph.Node?[count];
            var graph = NativeDrawingModelFactory.Graph(ar);
            for (int i = 0; i < count; i++) graph.Object(ar, ref Handles[i]);
            Indices.Serialize(ar); ar.I32(ref State30); ar.I32(ref State40); ar.I32(ref State34); ar.I32(ref State38);
            ar.U16(ref State3C); ar.F64(ref State10); ar.F64(ref State8);
        }
    }
}
