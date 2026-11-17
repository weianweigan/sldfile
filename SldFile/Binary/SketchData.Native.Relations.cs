using System;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeDrawingObjectGraph.Node?[][] NativeValueRelations =
    {
        Array.Empty<NativeDrawingObjectGraph.Node?>(), Array.Empty<NativeDrawingObjectGraph.Node?>(),
        Array.Empty<NativeDrawingObjectGraph.Node?>(), Array.Empty<NativeDrawingObjectGraph.Node?>()
    };
    internal NativeLogicalG1[] NativeLogicalG1Relations = Array.Empty<NativeLogicalG1>();
    internal NativeLogicalG2[] NativeLogicalG2Relations = Array.Empty<NativeLogicalG2>();
    internal NativeLogicalG3[] NativeLogicalG3Relations = Array.Empty<NativeLogicalG3>();

    // First seven tables of the relation loader 0x4C5C9F70 (+68 through +98).
    // Other relation tables and solver state follow; this is not the root tail.
    internal void SerializeNativeRelationPrefix(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native sketch relations.");
        if (NativeValueRelations.Length != 4) throw new System.IO.InvalidDataException("Four native value-relation tables are required.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < NativeValueRelations.Length; i++)
        {
            ushort count = RelationCount(NativeValueRelations[i].Length); ar.U16(ref count);
            if (ar.IsLoading) NativeValueRelations[i] = new NativeDrawingObjectGraph.Node?[count];
            for (int j = 0; j < count; j++) graph.Object(ar, ref NativeValueRelations[i][j]);
        }
        RelationRows(ar, ref NativeLogicalG1Relations, row => row.Serialize(ar));
        RelationRows(ar, ref NativeLogicalG2Relations, row => row.Serialize(ar));
        RelationRows(ar, ref NativeLogicalG3Relations, row => row.Serialize(ar));
    }

    private static ushort RelationCount(int count)
    {
        if ((uint)count > ushort.MaxValue) throw new System.IO.InvalidDataException("Native relation count exceeds u16.");
        return (ushort)count;
    }

    private static void RelationRows<T>(MfcArchive ar, ref T[] rows, Action<T> serialize) where T : new()
    {
        ushort count = RelationCount(rows.Length); ar.U16(ref count);
        if (ar.IsLoading) rows = new T[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) rows[i] = new T();
            serialize(rows[i]);
        }
    }

    // DimensionModel is the existing public parameter/display aggregate. Its
    // serializer is not Dimension_c's wire format; keep that API unchanged.
    // These inline records reuse the shared object graph and EntHandleData.
    internal sealed class NativeDimension
    {
        internal int State20, State50, State30, State54, State5C;
        internal NativeDrawingObjectGraph.Node? Parameter, Tolerance, Object60;
        internal ushort State68;
        internal byte HasString;
        internal string? OptionalString;

        // Dimension_c::Serialize 0x4C84B2D0, ordinary non-DB archive without
        // helper conditions 1/2/4 (which append an additional i32).
        internal void Serialize(MfcArchive ar)
        {
            if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native dimension layout.");
            var graph = NativeDrawingModelFactory.Graph(ar);
            ar.I32(ref State20);
            // 0x4C8566C0 serializes the parameter's object pointer, not an
            // inline parameter aggregate or a four-byte presence flag.
            graph.Object(ar, ref Parameter); graph.Object(ar, ref Tolerance);
            ar.I32(ref State50); ar.I32(ref State30); ar.I32(ref State54); ar.I32(ref State5C);
            graph.Object(ar, ref Object60);
            if (ar.DocumentVersion >= 0x2392) ar.U16(ref State68);
            if (ar.DocumentVersion > 0x42EE)
            {
                ar.Byte(ref HasString);
                if (HasString != 0) DrawingPreviewRecordModel.String(ar, ref OptionalString);
            }
        }
    }

    internal sealed class NativeLogicalDimension
    {
        internal NativeDimension Dimension = new NativeDimension();
        internal int State80;
        internal ushort State90, State94, State98, LegacyState68, StateA0, StateA4;

        // sgDim 0x4C5AFB80 -> sgLogDim 0x4C5B4950. Preserve the wire
        // State94 verbatim even though native loading normalizes nonzero to 1.
        internal void Serialize(MfcArchive ar)
        {
            SerializeSketchDimension(ar);
            ar.U16(ref StateA0); ar.U16(ref StateA4);
        }

        internal void SerializeSketchDimension(MfcArchive ar)
        {
            Dimension.Serialize(ar); ar.I32(ref State80);
            ar.U16(ref State90); ar.U16(ref State94); ar.U16(ref State98);
            if (ar.DocumentVersion < 0x2392) ar.U16(ref LegacyState68);
        }
    }

    internal sealed class NativeLogicalG1
    {
        internal NativeLogicalDimension Dimension = new NativeLogicalDimension();
        internal NativeDrawingObjectGraph.Node? Entity;
        // sgLogG1Dim::Serialize 0x4C5B4AD0.
        internal void Serialize(MfcArchive ar)
        {
            Dimension.Serialize(ar); NativeDrawingModelFactory.Graph(ar).Object(ar, ref Entity);
        }
    }

    internal sealed class NativeLogicalG2
    {
        internal NativeLogicalDimension Dimension = new NativeLogicalDimension();
        internal NativeDrawingObjectGraph.Node? First, Second, Third, Fourth, Fifth;
        internal ushort StateF8, StateFC, State100, State150;
        // The pointer operators 0x3CB73D60 / 0x3CB73E60 use ReadObject /
        // WriteObject. This differs from suObArray::serializePtr's inline form.
        internal NativeDrawingObjectGraph.Node? Objects;
        internal int State110, State114, State154;
        internal double State118, State120;
        // sgLogG2Dim::Serialize 0x4C5B4C20; all earlier gates are already
        // enabled by the supported minimum version 5000.
        internal void Serialize(MfcArchive ar)
        {
            Dimension.Serialize(ar); var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref First); graph.Object(ar, ref Second);
            ar.U16(ref StateF8); ar.U16(ref StateFC); graph.Object(ar, ref Objects);
            ar.U16(ref State100); ar.I32(ref State110); ar.I32(ref State114);
            ar.F64(ref State118); ar.F64(ref State120);
            graph.Object(ar, ref Third); graph.Object(ar, ref Fourth); graph.Object(ar, ref Fifth);
            ar.U16(ref State150);
            if (ar.DocumentVersion > 0x46CB) ar.I32(ref State154);
        }
    }

    internal sealed class NativeLogicalG3
    {
        internal NativeLogicalDimension Dimension = new NativeLogicalDimension();
        internal NativeDrawingObjectGraph.Node? First, Second, Third, Fourth, Fifth, Sixth;
        // sgLogG3Dim::Serialize 0x4C5B5790: three inline-handle objects,
        // then three optional handle pointers (all tagged on the wire).
        internal void Serialize(MfcArchive ar)
        {
            Dimension.Serialize(ar); var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref First); graph.Object(ar, ref Second); graph.Object(ar, ref Third);
            graph.Object(ar, ref Fourth); graph.Object(ar, ref Fifth); graph.Object(ar, ref Sixth);
        }
    }

    internal class NativeValueDimension
    {
        internal NativeLogicalDimension Dimension = new NativeLogicalDimension();
        internal ushort HasTransform;
        internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
        // sgValDim 0x4C5C7B20. Shared by geometric value dimensions and
        // the offset dimension; neither duplicates the sgDim/parameter wire.
        internal void SerializeValueBase(MfcArchive ar)
        {
            Dimension.SerializeSketchDimension(ar);
            ar.U16(ref HasTransform); if (HasTransform != 0) Transform.SerializeOperator(ar);
        }
    }

    internal sealed class NativeValueG2 : NativeValueDimension
    {
        internal ushort State180, State184, State1B8, State1BC, LegacyState;
        internal NativeDrawingObjectGraph.Node? First, Second, Plane;
        internal SldFile.Drawing.DrawingPreviewPoint Direction;

        // sgValDim 0x4C5C7B20 -> sgValG2Dim 0x4C5C8120, also the
        // confirmed sgPntPntDist runtime vtable Serialize entry.
        internal void Serialize(MfcArchive ar)
        {
            SerializeValueBase(ar);
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref First); graph.Object(ar, ref Second);
            ar.U16(ref State180); ar.U16(ref State184); ar.U16(ref State1B8); ar.U16(ref State1BC);
            graph.Object(ar, ref Plane);
            if (ar.DocumentVersion > 0x1C27) DrawingPreviewRecordModel.Point(ar, ref Direction);
            if (ar.DocumentVersion > 0x2F79 && ar.DocumentVersion < 0x2FA0) ar.U16(ref LegacyState);
        }
    }
}
