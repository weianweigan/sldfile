using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// gcBucket_c restore 0x3CAB91F0 / save 0x3CABB4E0, arguments (0, 1).
// Pointer-keyed polygon maps use MFC object references in this mode.
internal sealed class NativeDrawingBucketModel : IMfcSerializable
{
    internal sealed class StringEntry { internal int Id; internal string? Value; }
    internal sealed class PolygonEntry
    {
        internal NativeDrawingObjectGraph.Node? Key;
        internal NativeDrawingBucketPolyModel[] Polygons = Array.Empty<NativeDrawingBucketPolyModel>();
    }
    internal DrawingPreviewPoint Direction;
    internal int State6F8, State6FC, State70C, HasTransform, MapCount, State708, State83C, State760, State764;
    internal double Scalar700;
    internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
    internal NativeDrawingBucketPolyModel[][] PolygonSets = new NativeDrawingBucketPolyModel[13][];
    internal NativeDrawingDisplayItemModel[] Items = Array.Empty<NativeDrawingDisplayItemModel>();
    internal NativeDrawingDisplayItemModel[] OtherItems = Array.Empty<NativeDrawingDisplayItemModel>();
    internal NativeDrawingDisplayItemModel[] DetailItems = Array.Empty<NativeDrawingDisplayItemModel>();
    internal StringEntry[] ComponentNames = Array.Empty<StringEntry>();
    internal PolygonEntry[][] PolygonMaps = new PolygonEntry[4][];
    internal NativeDrawingBucketPolyModel[] ExtraPolygons = Array.Empty<NativeDrawingBucketPolyModel>();
    internal int[][] HatchSets = Array.Empty<int[]>();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy gcBucket layout.");
        DrawingPreviewRecordModel.Point(ar, ref Direction);
        ar.I32(ref State6F8); ar.I32(ref State6FC); ar.F64(ref Scalar700); ar.I32(ref State70C);
        ar.I32(ref HasTransform); if (HasTransform != 0) Transform.Serialize(ar);
        for (int i = 0; i < PolygonSets.Length; i++) Polygons(ar, ref PolygonSets[i]);
        NativeDrawingDisplayItemModel.List(ar, ref Items);
        NativeDrawingDisplayItemModel.List(ar, ref OtherItems);
        ar.I32(ref MapCount);
        uint nativeNames = (uint)ComponentNames.Length; ar.Count(ref nativeNames);
        int names = checked((int)nativeNames);
        if (ar.IsLoading && names > ar.Remaining / 5) throw new InvalidDataException("Invalid native component-name map count.");
        if (ar.IsLoading) ComponentNames = new StringEntry[names];
        for (int i = 0; i < ComponentNames.Length; i++)
        {
            if (ar.IsLoading) ComponentNames[i] = new StringEntry();
            if (ar.DocumentVersion < 0x23D8)
            {
                ushort id = (ushort)ComponentNames[i].Id; ar.U16(ref id); if (ar.IsLoading) ComponentNames[i].Id = id;
            }
            else ar.I32(ref ComponentNames[i].Id);
            DrawingPreviewRecordModel.String(ar, ref ComponentNames[i].Value);
        }
        for (int i = 0; i < PolygonMaps.Length; i++)
        {
            int count = PolygonMaps[i]?.Length ?? 0; NativeDrawingDisplayArchive.Count(ar, ref count);
            if (ar.IsLoading) PolygonMaps[i] = new PolygonEntry[count];
            for (int j = 0; j < count; j++)
            {
                if (ar.IsLoading) PolygonMaps[i][j] = new PolygonEntry();
                NativeDrawingModelFactory.Graph(ar).Object(ar, ref PolygonMaps[i][j].Key);
                Polygons(ar, ref PolygonMaps[i][j].Polygons);
            }
        }
        if (ar.DocumentVersion >= 0x147E) ar.I32(ref State708);
        if (ar.DocumentVersion >= 0x184D) Polygons(ar, ref ExtraPolygons);
        if (ar.DocumentVersion >= 0x1C3E) ar.I32(ref State83C);
        if (ar.DocumentVersion < 0x278A) return;
        int hatches = HatchSets.Length; NativeDrawingDisplayArchive.Count(ar, ref hatches);
        if (ar.IsLoading) HatchSets = new int[hatches][];
        for (int i = 0; i < hatches; i++)
        {
            if (ar.IsLoading) HatchSets[i] = new int[7];
            for (int j = 0; j < 7; j++) ar.I32(ref HatchSets[i][j]);
        }
        ar.I32(ref State760); NativeDrawingDisplayItemModel.List(ar, ref DetailItems); ar.I32(ref State764);
    }

    private void Polygons(MfcArchive ar, ref NativeDrawingBucketPolyModel[] values)
    {
        values ??= Array.Empty<NativeDrawingBucketPolyModel>();
        int count = values.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) values = new NativeDrawingBucketPolyModel[count];
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) values[i] = new NativeDrawingBucketPolyModel();
            values[i].TwoDimensional = HasTransform != 0; values[i].Serialize(ar);
        }
    }
}
