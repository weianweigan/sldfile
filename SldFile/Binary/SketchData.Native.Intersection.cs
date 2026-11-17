using System;
using System.IO;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeIntersection[] NativeIntersections = Array.Empty<NativeIntersection>();

    internal void SerializeNativePostRelationTables(MfcArchive ar)
    {
        SerializeNativeOffsetAndStyleTables(ar);
        RelationRows(ar, ref NativeIntersections, row => row.Serialize(ar));
    }

    // sgIntersection_c 0x4C5B28A0; ordinary >=5000 archive. No DB keys,
    // transient pointer export or archive condition 4 exclusion mode.
    internal sealed class NativeIntersection
    {
        internal ushort State68, State70, State74, State88, State8C, State90, StateA0;
        internal uint State6C;
        internal int State58;
        internal NativeDrawingObjectGraph.Node? First, Second, Third, Fourth, FirstBent, SecondBent;
        internal NativeDrawingObjectGraph.Node?[] Handles = Array.Empty<NativeDrawingObjectGraph.Node?>();
        internal NativeDynamicI32Array Indices = new NativeDynamicI32Array();
        internal NativeIntersectionEntity[] Entities = Array.Empty<NativeIntersectionEntity>();
        internal byte HasTransform;
        internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
        internal NativeDrawingObjectGraph.Node? Objects, Arc, Line;
        internal DrawingPreviewPoint[] Points = Array.Empty<DrawingPreviewPoint>();
        internal double StateB0;

        internal void Serialize(MfcArchive ar)
        {
            ar.U16(ref State68); if (State68 != 0) return;
            ar.U32(ref State6C);
            int count = Handles.Length; NativeDrawingDisplayArchive.Count(ar, ref count); ar.I32(ref State58);
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref First); graph.Object(ar, ref Second); graph.Object(ar, ref Third); graph.Object(ar, ref Fourth);
            graph.Object(ar, ref FirstBent); graph.Object(ar, ref SecondBent);
            if (ar.IsLoading) { Handles = new NativeDrawingObjectGraph.Node?[count]; Entities = new NativeIntersectionEntity[count]; }
            if (Entities.Length != count) throw new InvalidDataException("Native intersection entity count mismatch.");
            for (int i = 0; i < count; i++) graph.Object(ar, ref Handles[i]);
            Indices.Serialize(ar);
            for (int i = 0; i < count; i++)
            {
                if (ar.IsLoading) Entities[i] = new NativeIntersectionEntity();
                Entities[i].Serialize(ar);
            }
            ar.Byte(ref HasTransform); if (HasTransform != 0) Transform.Serialize(ar);
            if (ar.DocumentVersion > 0x2B71)
            {
                ar.U16(ref State70); ar.U16(ref State74); graph.Object(ar, ref Objects); graph.Object(ar, ref Arc);
            }
            if (ar.DocumentVersion > 0x2B98)
            {
                ar.U16(ref State88); ar.U16(ref State8C); ar.U16(ref State90); ar.U16(ref StateA0); graph.Object(ar, ref Line);
                int points = Points.Length; NativeDrawingDisplayArchive.Count(ar, ref points);
                if (ar.IsLoading) Points = new DrawingPreviewPoint[points];
                for (int i = 0; i < points; i++) DrawingPreviewRecordModel.Point(ar, ref Points[i]);
            }
            if (ar.DocumentVersion >= 0x2BE0) ar.F64(ref StateB0);
        }
    }

    // Inline record Serialize 0x4C5ABA60, vtable 0x4D0B9778; no curve/kernel
    // blob is involved. Five i32 states followed by four subentity references.
    internal sealed class NativeIntersectionEntity
    {
        internal int State158, State15C, State160, State164, State168;
        internal Subentity First = new Subentity(), Second = new Subentity(), Third = new Subentity(), Fourth = new Subentity();
        internal void Serialize(MfcArchive ar)
        {
            ar.I32(ref State158); ar.I32(ref State15C); ar.I32(ref State160); ar.I32(ref State164); ar.I32(ref State168);
            First.Serialize(ar); Second.Serialize(ar); Third.Serialize(ar); Fourth.Serialize(ar);
        }
        internal sealed class Subentity
        {
            internal ushort Kind;
            internal int State20, State24, State28;
            internal NativeDrawingObjectGraph.Node? Loop, FirstEdge, SecondEdge;
            // Native helper 0x4C5AB800, ordinary tagged-reference branch.
            internal void Serialize(MfcArchive ar)
            {
                ar.U16(ref Kind); ar.I32(ref State20); ar.I32(ref State24); ar.I32(ref State28);
                var graph = NativeDrawingModelFactory.Graph(ar);
                graph.Object(ar, ref Loop); graph.Object(ar, ref FirstEdge); graph.Object(ar, ref SecondEdge);
            }
        }
    }

    // Native 0x4BB596B0 dynamic array header plus typed 32-bit words.
    internal sealed class NativeDynamicI32Array
    {
        internal uint ItemBytes, Capacity, Growth, Count;
        internal int[] Values = Array.Empty<int>();
        internal void Serialize(MfcArchive ar)
        {
            ar.U32(ref ItemBytes);
            if (ItemBytes == 0) { if (ar.IsLoading) Values = Array.Empty<int>(); return; }
            if ((ItemBytes & 3) != 0) throw new InvalidDataException("Native i32 array requires a four-byte-aligned item width.");
            ar.U32(ref Capacity); ar.U32(ref Growth); ar.U32(ref Count);
            ulong words = (ulong)ItemBytes / 4 * Count;
            if (words > int.MaxValue || (ar.IsLoading && words > (ulong)ar.Remaining / 4)) throw new InvalidDataException("Invalid native i32 array size.");
            if (ar.IsLoading) Values = new int[(int)words];
            if (Values.Length != (int)words) throw new InvalidDataException("Native i32 array size mismatch.");
            for (int i = 0; i < Values.Length; i++) ar.I32(ref Values[i]);
        }
    }
}
