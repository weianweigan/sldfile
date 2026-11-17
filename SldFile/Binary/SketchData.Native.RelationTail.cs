using System;
using System.IO;
using SldFile.Drawing;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeIsoParam[] NativeIsoRelations = Array.Empty<NativeIsoParam>();
    internal NativeLinearPattern[] NativeLinearPatterns = Array.Empty<NativeLinearPattern>();
    internal NativeCircularPattern[] NativeCircularPatterns = Array.Empty<NativeCircularPattern>();
    internal NativeDerivedG2[] NativeDerivedA8 = Array.Empty<NativeDerivedG2>(), NativeDerivedB0 = Array.Empty<NativeDerivedG2>();
    internal NativeEntityRelation[] NativeEntityRelations = Array.Empty<NativeEntityRelation>();
    internal NativeGroupRelation[] NativeGroupRelations = Array.Empty<NativeGroupRelation>();
    internal NativeLogicalGroup[] NativeLogicalGroups = Array.Empty<NativeLogicalGroup>();
    internal NativeDrawingObjectGraph.Node? NativeRelationObjects;

    // Entire 0x4C5C9F70 relation section, ordinary archive, versions >=5000.
    // The sgSketch serializer continues with solver/settings after this.
    internal void SerializeNativeRelations(MfcArchive ar)
    {
        SerializeNativeRelationPrefix(ar);
        RelationRows(ar, ref NativeIsoRelations, row => row.Serialize(ar));
        RelationRows(ar, ref NativeLinearPatterns, row => row.Serialize(ar));
        RelationRows(ar, ref NativeDerivedA8, row => row.Serialize(ar));
        RelationRows(ar, ref NativeCircularPatterns, row => row.Serialize(ar));
        RelationRows(ar, ref NativeDerivedB0, row => row.Serialize(ar));
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeRelationObjects);
        if (ar.DocumentVersion > 0x1FF5)
        {
            RelationRows(ar, ref NativeEntityRelations, row => row.Serialize(ar));
            RelationRows(ar, ref NativeGroupRelations, row => row.Serialize(ar));
        }
        if (ar.DocumentVersion > 0x46CB) RelationRows(ar, ref NativeLogicalGroups, row => row.Serialize(ar));
    }

    internal sealed class NativeIsoParam
    {
        internal NativeLogicalDimension Dimension = new NativeLogicalDimension();
        internal int StateA8, StateAC, StateB0, StateB4;
        internal NativeDynamicU16Array Indices = new NativeDynamicU16Array();
        // sgIsoParam 0x4C5B3BC0; ordinary dynamic-array helper 0x4C5AB460.
        internal void Serialize(MfcArchive ar)
        {
            Dimension.Serialize(ar); ar.I32(ref StateA8); ar.I32(ref StateAC); ar.I32(ref StateB0); ar.I32(ref StateB4);
            Indices.Serialize(ar);
        }
    }

    // Shared native helper 0x4C5AB460, also used by sgStyledCurve.
    internal sealed class NativeDynamicU16Array
    {
        internal uint ItemBytes, Capacity, Growth, Count;
        internal ushort[] Values = Array.Empty<ushort>();
        internal void Serialize(MfcArchive ar)
        {
            ar.U32(ref ItemBytes);
            if (ItemBytes == 0) { if (ar.IsLoading) Values = Array.Empty<ushort>(); return; }
            if ((ItemBytes & 1) != 0) throw new InvalidDataException("Native u16 array requires an even item width.");
            ar.U32(ref Capacity); ar.U32(ref Growth); ar.U32(ref Count);
            ulong words = (ulong)ItemBytes / 2 * Count;
            if (words > int.MaxValue || (ar.IsLoading && words > (ulong)ar.Remaining / 2)) throw new InvalidDataException("Invalid native isoparam array size.");
            if (ar.IsLoading) Values = new ushort[(int)words];
            if (Values.Length != (int)words) throw new InvalidDataException("Native isoparam array size mismatch.");
            for (int i = 0; i < Values.Length; i++) ar.U16(ref Values[i]);
        }
    }

    internal sealed class NativeDerivedG2
    {
        internal NativeLogicalG2 Base = new NativeLogicalG2();
        internal ushort State158, State164, State168, State16C, State170, State174;
        internal int State15C, State160;
        // 0x4C5B8100, used by both +A8 and +B0 relation tables.
        internal void Serialize(MfcArchive ar)
        {
            Base.Serialize(ar); ar.U16(ref State158); ar.U16(ref State164); ar.U16(ref State168);
            ar.U16(ref State16C); ar.U16(ref State170); ar.U16(ref State174); ar.I32(ref State15C); ar.I32(ref State160);
        }
    }

    internal sealed class NativePatternBase
    {
        internal int State8, State18, State80;
        internal NativeDrawingObjectGraph.Node? NotePattern;
        // 0x4C5B8560; every gate is enabled for versions >=5000.
        internal void Serialize(MfcArchive ar)
        {
            ar.I32(ref State8); ar.I32(ref State18); ar.I32(ref State80);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref NotePattern);
        }
    }

    internal sealed class NativeLinearPattern
    {
        internal NativePatternBase Base = new NativePatternBase();
        internal ushort State98, State9C, StateA0, StateA4, StateB0, StateB4, State70, StateD0, StateD4, StateD8;
        internal DrawingPreviewPoint Direction;
        internal NativeDrawingObjectGraph.Node? First, Second, FirstLine, SecondLine, CountDimension;
        internal double StateF0;
        // 0x4C5B40A0.
        internal void Serialize(MfcArchive ar)
        {
            Base.Serialize(ar); ar.U16(ref State98); ar.U16(ref State9C); ar.U16(ref StateA0); ar.U16(ref StateA4);
            ar.U16(ref StateB0); ar.U16(ref StateB4); DrawingPreviewRecordModel.Point(ar, ref Direction);
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref First); graph.Object(ar, ref Second); ar.U16(ref State70);
            ar.U16(ref StateD0); ar.U16(ref StateD4); graph.Object(ar, ref FirstLine); graph.Object(ar, ref SecondLine);
            graph.Object(ar, ref CountDimension); ar.F64(ref StateF0);
            if (ar.DocumentVersion > 0x27AC) ar.U16(ref StateD8);
        }
    }

    internal sealed class NativeCircularPattern
    {
        internal NativePatternBase Base = new NativePatternBase();
        internal ushort State98, State9C, StateA0, StateA4, State70, StateC0, StateF4, State120, State124;
        internal DrawingPreviewPoint Position;
        internal NativeDrawingObjectGraph.Node? First, Second, Arc, Point, Circle, CountDimension;
        // 0x4C5AE990, modern ordinary branch.
        internal void Serialize(MfcArchive ar)
        {
            Base.Serialize(ar); ar.U16(ref State98); ar.U16(ref State9C); ar.U16(ref StateA0); ar.U16(ref StateA4);
            DrawingPreviewRecordModel.Point(ar, ref Position); var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref First); graph.Object(ar, ref Second); ar.U16(ref State70);
            graph.Object(ar, ref Arc); ar.U16(ref StateC0); ar.U16(ref StateF4);
            graph.Object(ar, ref Point); ar.U16(ref State120); ar.U16(ref State124);
            graph.Object(ar, ref Circle); graph.Object(ar, ref CountDimension);
        }
    }

    internal sealed class NativeEntityRelation
    {
        internal ushort State20, State24, State28;
        internal NativeDrawingObjectGraph.Node? Entity;
        // 0x4B9C7490; native loading discards State24, wire mode preserves it.
        internal void Serialize(MfcArchive ar)
        {
            ar.U16(ref State20); ar.U16(ref State24); ar.U16(ref State28);
            NativeDrawingModelFactory.Graph(ar).Object(ar, ref Entity);
        }
    }

    internal sealed class NativeGroupRelation
    {
        internal NativeLogicalG1 Base = new NativeLogicalG1();
        internal ushort StateD0;
        internal NativeDrawingObjectGraph.Node? Entity, First, Second, Third;
        // 0x4B9C76C0.
        internal void Serialize(MfcArchive ar)
        {
            Base.Serialize(ar); ar.U16(ref StateD0); var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref Entity); graph.Object(ar, ref First); graph.Object(ar, ref Second); graph.Object(ar, ref Third);
        }
    }

    internal sealed class NativeLogicalGroup
    {
        internal NativeLogicalDimension Dimension = new NativeLogicalDimension();
        internal int[] Ids = Array.Empty<int>();
        // sgLogGroupDim 0x4C5B5CF0 serializes the set's ordered integer keys.
        internal void Serialize(MfcArchive ar)
        {
            Dimension.Serialize(ar); ushort count = RelationCount(Ids.Length); ar.U16(ref count);
            if (ar.IsLoading) Ids = new int[count];
            for (int i = 0; i < Ids.Length; i++) ar.I32(ref Ids[i]);
        }
    }
}
