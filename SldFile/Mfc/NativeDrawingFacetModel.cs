using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// sldmfcu dynamic-array load/save 0x3CAC0CC0 / 0x3CAC0E10.
// Word values retain the exact IEEE bits for float-valued channels. The owning
// facet field supplies their geometry/index meaning; no array is found by scan.
internal sealed class NativeDrawingArrayModel : IMfcSerializable
{
    internal uint ElementSize, AllocationBlock, Mode, Count;
    internal uint[] Words = Array.Empty<uint>();
    internal byte[] Bytes = Array.Empty<byte>();
    internal bool ImplicitElementSize;
    public void Serialize(MfcArchive ar)
    {
        if (!ImplicitElementSize) ar.U32(ref ElementSize);
        if (ElementSize == 0) return;
        if (ElementSize != 1 && (ElementSize & 3) != 0) throw new InvalidDataException("Invalid native array element size.");
        ar.U32(ref AllocationBlock); ar.U32(ref Mode); ar.U32(ref Count);
        if (Mode != 1 && Mode != 2) throw new InvalidDataException("Invalid native array allocation mode.");
        long bytes = checked((long)Count * ElementSize);
        if (bytes > int.MaxValue || ar.IsLoading && bytes > ar.Remaining) throw new InvalidDataException("Invalid native array count.");
        if (ElementSize == 1)
        {
            if (ar.IsLoading) Bytes = new byte[Count];
            if (Bytes.Length != Count) throw new InvalidDataException("Native byte-array count mismatch.");
            for (int i = 0; i < Bytes.Length; i++) ar.Byte(ref Bytes[i]);
        }
        else
        {
            if (ar.IsLoading) Words = new uint[bytes / 4];
            if (Words.Length != bytes / 4) throw new InvalidDataException("Native word-array count mismatch.");
            for (int i = 0; i < Words.Length; i++) ar.U32(ref Words[i]);
        }
    }
    internal float Real(int index) => BitConverter.ToSingle(BitConverter.GetBytes(Words[index]), 0);
}

// gcFacetContainer restore/save 0x3CAD4510 / 0x3CAD66E0. This codec is
// specifically the inline VBLists drawing archive context, not a part stream.
internal sealed class NativeDrawingFacetModel : IMfcSerializable
{
    internal sealed class Pair { internal int Key, Value; }
    internal sealed class SpatialIndex : IMfcSerializable
    {
        internal int State0, State4, State18, HasBasis;
        internal NativeDrawingArrayModel[] Arrays = NewArrays(5);
        internal DrawingPreviewPoint[] Basis = new DrawingPreviewPoint[3];
        public void Serialize(MfcArchive ar)
        {
            ar.I32(ref State0); ar.I32(ref State4); Arrays[0].Serialize(ar); Arrays[1].Serialize(ar);
            ar.I32(ref State18); for (int i = 2; i < 5; i++) Arrays[i].Serialize(ar);
            ar.I32(ref HasBasis);
            if (HasBasis != 0) for (int i = 0; i < 3; i++) DrawingPreviewRecordModel.Point(ar, ref Basis[i]);
        }
    }
    internal sealed class Edge : IMfcSerializable
    {
        internal int Id, MapState0, MapState4, Reserved;
        internal double[] MapScalars = Array.Empty<double>();
        internal int[] Facets = Array.Empty<int>();
        internal long[] Links = Array.Empty<long>();
        public void Serialize(MfcArchive ar)
        {
            // gcEdgeFullInfo 0x3CAD41C0/0x3CAD64D0; gcEdMapInfo 0x3CAD40E0.
            ar.I32(ref Id); ar.I32(ref MapState0); ar.I32(ref MapState4);
            Doubles(ar, ref MapScalars); ar.I32(ref Reserved); Ints(ar, ref Facets);
            int count = Links.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) Links = new long[count];
            for (int i = 0; i < Links.Length; i++) ar.I64(ref Links[i]);
        }
    }
    internal int FacetCount, VertexCount, Representation;
    internal uint State1D0, State1D4;
    internal uint[] Flags = new uint[3];
    internal int[] HeaderIntegers = Array.Empty<int>();
    internal float[] HeaderReals = Array.Empty<float>();
    internal NativeDrawingArrayModel[] Channels = NewArrays(6);
    internal int HasVertexParameters, Processed, State10, State80, State1000, State400, State800;
    internal float[] VertexParameters = Array.Empty<float>();
    internal double[] Bounds = new double[10];
    internal uint State214;
    internal float[] Appearance = new float[4];
    internal int HasTexture, HasTriangleTexture, HasExtraTexture, HasSpatialIndex, HasTriangleIndices, HasVertexColors;
    internal NativeDrawingArrayModel Texture = new NativeDrawingArrayModel();
    internal NativeDrawingArrayModel[] TriangleTexture = NewArrays(2), ExtraTexture = NewArrays(2);
    internal SpatialIndex Index = new SpatialIndex();
    internal NativeDrawingArrayModel TriangleIndices = new NativeDrawingArrayModel();
    internal int[] VertexColors = Array.Empty<int>();
    internal uint State224, State2A0, State2A8, State2A4;
    internal DrawingPreviewPoint Direction, Point258, Point270;
    internal int State250, State200000, State2AC, EmptyName;
    internal double Scalar288, Scalar290, Scalar298;
    internal Pair[] EdgeMap = Array.Empty<Pair>();
    internal string? Name;
    internal byte HasAppearanceTexture, AppearanceTextureState;
    internal NativeDrawingArrayModel AppearanceTexture = new NativeDrawingArrayModel();
    internal Edge[] Edges = Array.Empty<Edge>();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy facet-container layout.");
        ar.I32(ref FacetCount); ar.I32(ref VertexCount);
        if (ar.DocumentVersion < 0x3EE8) Channels[0].Serialize(ar);
        else
        {
        ar.I32(ref Representation);
        if (Representation == 4)
        {
            Channels[0].ImplicitElementSize = true; Channels[0].ElementSize = 4; Channels[0].Serialize(ar);
        }
        else if (Representation != 0)
        {
            ar.U32(ref State1D0); ar.U32(ref State1D4);
            for (int i = 0; i < Flags.Length; i++) ar.U32(ref Flags[i]);
            Ints(ar, ref HeaderIntegers); Reals(ar, ref HeaderReals); Channels[0].Serialize(ar);
        }
        }
        for (int i = 1; i < Channels.Length; i++) Channels[i].Serialize(ar);
        ar.I32(ref HasVertexParameters); if (HasVertexParameters != 0) Reals(ar, ref VertexParameters);
        ar.I32(ref Processed); ar.I32(ref State10);
        for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        ar.I32(ref State80); ar.I32(ref State1000); ar.U32(ref State214);
        ar.I32(ref State400); ar.I32(ref State800);
        for (int i = 0; i < Appearance.Length; i++) ar.F32(ref Appearance[i]);
        ar.I32(ref HasTexture); if (HasTexture != 0) Texture.Serialize(ar);
        ar.I32(ref HasTriangleTexture); if (HasTriangleTexture != 0) foreach (var array in TriangleTexture) array.Serialize(ar);
        ar.I32(ref HasExtraTexture); if (HasExtraTexture != 0) foreach (var array in ExtraTexture) array.Serialize(ar);
        ar.I32(ref HasSpatialIndex); if (HasSpatialIndex != 0) Index.Serialize(ar);
        if (ar.DocumentVersion == 5000) return;
        ar.I32(ref HasTriangleIndices); if (HasTriangleIndices != 0) TriangleIndices.Serialize(ar);
        ar.I32(ref HasVertexColors); if (HasVertexColors != 0 && HasVertexParameters != 0) Ints(ar, ref VertexColors);
        ar.U32(ref State224); DrawingPreviewRecordModel.Point(ar, ref Direction);
        ar.I32(ref State250); DrawingPreviewRecordModel.Point(ar, ref Point258); DrawingPreviewRecordModel.Point(ar, ref Point270);
        ar.F64(ref Scalar288); ar.F64(ref Scalar290);
        int pairs = EdgeMap.Length; NativeDrawingDisplayArchive.Count(ar, ref pairs);
        if (ar.IsLoading) EdgeMap = new Pair[pairs];
        for (int i = 0; i < pairs; i++)
        {
            if (ar.IsLoading) EdgeMap[i] = new Pair();
            ar.I32(ref EdgeMap[i].Key); ar.I32(ref EdgeMap[i].Value);
        }
        ar.F64(ref Scalar298); ar.I32(ref State200000); ar.U32(ref State2A0);
        ar.I32(ref EmptyName); if (EmptyName == 0) DrawingPreviewRecordModel.String(ar, ref Name);
        ar.Byte(ref HasAppearanceTexture); if (HasAppearanceTexture != 0) AppearanceTexture.Serialize(ar);
        ar.Byte(ref AppearanceTextureState);
        // gcFacetContainer queries SldModelContext::isDrawingDoc (+0x90).
        // BaseDocModelContext vtable 0x460C12D8 resolves that slot to
        // sldappu 0x45B01470. This codec runs only in the drawing context,
        // which omits the legacy point map for versions >= 0x2BE5.
        int edges = Edges.Length; NativeDrawingDisplayArchive.Count(ar, ref edges);
        if (ar.IsLoading) Edges = new Edge[edges];
        for (int i = 0; i < edges; i++)
        {
            if (ar.IsLoading) Edges[i] = new Edge();
            Edges[i].Serialize(ar);
        }
        ar.U32(ref State2A8); ar.U32(ref State2A4); ar.I32(ref State2AC);
    }

    internal static NativeDrawingArrayModel[] NewArrays(int count)
    {
        var arrays = new NativeDrawingArrayModel[count];
        for (int i = 0; i < count; i++) arrays[i] = new NativeDrawingArrayModel();
        return arrays;
    }
    internal static void Ints(MfcArchive ar, ref int[] values)
    {
        int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) values = new int[count];
        for (int i = 0; i < values.Length; i++) ar.I32(ref values[i]);
    }
    internal static void Reals(MfcArchive ar, ref float[] values)
    {
        int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) values = new float[count];
        for (int i = 0; i < values.Length; i++) ar.F32(ref values[i]);
    }
    private static void Doubles(MfcArchive ar, ref double[] values)
    {
        int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) values = new double[count];
        for (int i = 0; i < values.Length; i++) ar.F64(ref values[i]);
    }
}
