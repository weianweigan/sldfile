using System;
using System.IO;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchData
{
    internal NativeHatch[] NativeHatches = Array.Empty<NativeHatch>();

    // Entire ordered entity section. Relations and solver/settings payloads
    // follow this method; it is not a complete sgSketch root serializer.
    internal void SerializeNativeEntityTables(MfcArchive ar, bool ownerIs3D)
    {
        SerializeNativePointTable(ar, ownerIs3D); SerializeNativeLineTable(ar, ownerIs3D);
        SerializeNativeTextTable(ar, ownerIs3D); SerializeNativeHatchTable(ar, ownerIs3D);
        SerializeNativeArcTable(ar, ownerIs3D); SerializeNativeEllipseTable(ar, ownerIs3D);
        SerializeNativeParabolaTable(ar, ownerIs3D); SerializeNativeSplineTable(ar, ownerIs3D);
        SerializeNativePrimitiveTables(ar, ownerIs3D);
    }

    internal void SerializeNativeHatchTable(MfcArchive ar, bool ownerIs3D)
        => Rows(ar, ref NativeHatches, row => row.Serialize(ar, ownerIs3D));

    internal sealed class NativeHatch
    {
        internal NativeEntityFields Entity = new NativeEntityFields();
        internal uint[][] BoundaryIndices = { Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<uint>(), Array.Empty<uint>() };
        internal NativeDrawingObjectGraph.Node? Pattern, HatchData, ContourChooser, Objects, States;
        internal Segment[] Segments = Array.Empty<Segment>();
        internal sealed class Segment { internal int EntityIndex; internal ushort Kind; }

        // sgHatch::Serialize 0x4C5B1B50, ordinary modern archive. Referenced
        // gcXhatch/skHatchData/chooser objects must be decoded by the shared
        // graph; an unsupported referenced type remains an explicit failure.
        internal void Serialize(MfcArchive ar, bool ownerIs3D)
        {
            Entity.Serialize(ar, ownerIs3D);
            if ((Entity.Flags54 & 1) != 0) return;
            if (BoundaryIndices.Length != 4) throw new InvalidDataException("Native hatch requires four boundary-index arrays.");
            for (int i = 0; i < 4; i++)
            {
                // Native helper 0x4C5CD900 uses an i32 count and U32 items,
                // not the tagged su_CUIntArray object or CArchive::Count.
                int count = BoundaryIndices[i].Length; NativeDrawingDisplayArchive.Count(ar, ref count);
                if (ar.IsLoading) BoundaryIndices[i] = new uint[count];
                for (int j = 0; j < count; j++) ar.U32(ref BoundaryIndices[i][j]);
            }
            var graph = NativeDrawingModelFactory.Graph(ar);
            graph.Object(ar, ref Pattern); graph.Object(ar, ref HatchData);
            int segments = Segments.Length; NativeDrawingDisplayArchive.Count(ar, ref segments);
            if (ar.IsLoading) Segments = new Segment[segments];
            for (int i = 0; i < segments; i++)
            {
                if (ar.IsLoading) Segments[i] = new Segment();
                ar.I32(ref Segments[i].EntityIndex); ar.U16(ref Segments[i].Kind);
            }
            graph.Object(ar, ref ContourChooser); graph.Object(ar, ref Objects); graph.Object(ar, ref States);
        }
    }
}
