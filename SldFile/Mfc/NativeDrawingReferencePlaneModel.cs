using System;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moRefPlane::Serialize 0x4C2B4080 -> shared base 0x4C2B3E20.
internal sealed class NativeDrawingReferencePlaneModel : IMfcSerializable
{
    internal NativeDrawingGeometryFeatureModel Geometry = new NativeDrawingGeometryFeatureModel();
    internal DrawingPreviewPoint Point;
    internal int Mode, State6D8, State6E8;
    internal NativeDrawingObjectGraph.Node? FeatureReference, PlaneData;
    public void Serialize(MfcArchive ar)
    {
        Geometry.Serialize(ar); DrawingPreviewRecordModel.Point(ar, ref Point); ar.I32(ref Mode);
        ar.I32(ref State6D8);
        var graph = NativeDrawingModelFactory.Graph(ar);
        if (ar.DocumentVersion >= 0x145D) graph.Object(ar, ref FeatureReference);
        if (ar.DocumentVersion >= 0x2F86) ar.I32(ref State6E8);
        graph.Object(ar, ref PlaneData);
    }
}

// moAbsoluteRefPlnData::Serialize 0x4C2B4150 and
// moDrawingRefPlnData::Serialize 0x4C2AD9E0 share serializePlaneData
// 0x4C2B84C0 at vtable+0x150. Drawing data appends a compact transform.
internal sealed class NativeDrawingPlaneDataModel : IMfcSerializable
{
    internal bool Drawing;
    internal DrawingPreviewPoint RootPoint, Normal;
    internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
    internal NativeDrawingTransformModel DrawingTransform = new NativeDrawingTransformModel();
    internal double[] Bounds = new double[4];
    internal NativeDrawingObjectGraph.Node? Edge, FacetFin, Axis, SketchEntity;
    internal int State50, State54, State58, State5C, State60;
    internal byte Reserved;
    internal ushort State64;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy reference-plane data.");
        DrawingPreviewRecordModel.Point(ar, ref RootPoint); DrawingPreviewRecordModel.Point(ar, ref Normal);
        Transform.Serialize(ar);
        for (int i = 0; i < Bounds.Length; i++) ar.F64(ref Bounds[i]);
        var graph = NativeDrawingModelFactory.Graph(ar);
        graph.Object(ar, ref Edge); ar.I32(ref State50); ar.I32(ref State54); ar.I32(ref State58);
        ar.Byte(ref Reserved);
        ar.I32(ref State5C); ar.I32(ref State60);
        if (ar.DocumentVersion >= 0x17DE) ar.U16(ref State64);
        if (ar.DocumentVersion >= 0x2B84) graph.Object(ar, ref FacetFin);
        if (ar.DocumentVersion >= 0x2BA6) { graph.Object(ar, ref Axis); graph.Object(ar, ref SketchEntity); }
        if (Drawing) DrawingTransform.Serialize(ar);
    }
}
