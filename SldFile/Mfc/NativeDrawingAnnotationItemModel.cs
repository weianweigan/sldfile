using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moDisplayObject helper 0x4BA9DD40, ordinary non-DB archive.
// This is not a uoDisplayItem display-list command payload.
internal sealed class NativeDrawingAnnotationHeaderModel : IMfcSerializable
{
    internal ushort State50;
    internal NativeDrawingObjectGraph.Node? Owner, View, Component, LinkedObjects;
    internal int State54, State11C, State58, HasLinkedData, State118;
    internal NativeDrawingEntityVisualModel Visual = new NativeDrawingEntityVisualModel();
    internal NativeDrawingObjectListModel Objects68 = new NativeDrawingObjectListModel(), ObjectsB0 = new NativeDrawingObjectListModel();
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy annotation header.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        ar.U16(ref State50); graph.Object(ar, ref Owner); graph.Object(ar, ref View); graph.Object(ar, ref Component);
        ar.I32(ref State54); ar.I32(ref State11C); Visual.Serialize(ar);
        // Constructor 0x4BEC1910 +68/+B0 vtable 0x4D04DB38 dispatches
        // 0x4BA9BB50 directly to suObList::Serialize. Reuse its wire codec.
        Objects68.Serialize(ar); ObjectsB0.Serialize(ar); ar.I32(ref State58);
        graph.Object(ar, ref LinkedObjects); ar.I32(ref HasLinkedData);
        if (HasLinkedData != 0) throw new NotSupportedException("Native linked annotation data is not decoded.");
        ar.I32(ref State118);
        // Store-only 0x4BEC2AF0 targets UnQLite/MsgPack through
        // isOKForDBSerialize; it contributes no ordinary archive bytes.
    }
}

// moDisplayItem_c::Serialize 0x4BA9C590. Shared base of native notes,
// dimensions and other annotation objects; its tag belongs to its owner.
internal sealed class NativeDrawingAnnotationItemModel : IMfcSerializable
{
    internal NativeDrawingAnnotationHeaderModel Header = new NativeDrawingAnnotationHeaderModel();
    internal int State390, State3E0, HasInline288, HasTransform2D0;
    internal DrawingPreviewPoint Point290, Vector400, Vector2C0;
    internal NativeDrawingGeometryFeatureModel.OptionalArray Objects298 = new NativeDrawingGeometryFeatureModel.OptionalArray();
    internal NativeDrawingObjectGraph.Node? Component3F8, Object2D8;
    internal NativeDrawingTransformModel Transform2A0 = new NativeDrawingTransformModel(), Transform2D0 = new NativeDrawingTransformModel();
    internal int[] Indices = Array.Empty<int>();
    internal byte HasVector2C0;
    internal double State2C8, State3B8;
    internal NativeDrawingObjectGraph.Node?[] Leaders = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal IndexedObject[] Arrows = Array.Empty<IndexedObject>(), SplineLeaders = Array.Empty<IndexedObject>();
    internal int State2F0, State2F4, State304, State308, State300, State314, State334, State338;
    internal NativeDrawingLineWidthModel Width2F8 = new NativeDrawingLineWidthModel { RestoreType = 3 }, Width30C = new NativeDrawingLineWidthModel { RestoreType = 3 };
    internal NativeDrawingGeometryFeatureModel.OptionalArray Objects4A0 = new NativeDrawingGeometryFeatureModel.OptionalArray();
    internal int State318, State31C, State328, State32C, LegacySplineState, HasWatermark;
    internal uint LegacyWidthFirst, LegacyWidthSecond;
    internal NativeDrawingLineWidthModel Width320 = new NativeDrawingLineWidthModel { RestoreType = 0 };
    internal DrawingPreviewPoint[] Bounds = Array.Empty<DrawingPreviewPoint>(), Directions = Array.Empty<DrawingPreviewPoint>();

    internal sealed class IndexedObject
    {
        internal int Index;
        internal NativeDrawingObjectGraph.Node? Object;
    }
    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy annotation item.");
        Header.Serialize(ar); ar.I32(ref State390); ar.I32(ref State3E0);
        DrawingPreviewRecordModel.Point(ar, ref Point290); Objects298.Serialize(ar);
        ar.I32(ref HasInline288);
        if (HasInline288 != 0) throw new NotSupportedException("Native annotation inline +288 payload is not decoded.");
        var graph = NativeDrawingModelFactory.Graph(ar); graph.Object(ar, ref Component3F8);
        DrawingPreviewRecordModel.Point(ar, ref Vector400); Transform2A0.Serialize(ar);
        int count = Indices.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) Indices = new int[count];
        for (int i = 0; i < count; i++) ar.I32(ref Indices[i]);
        ar.Byte(ref HasVector2C0); if (HasVector2C0 != 0) DrawingPreviewRecordModel.Point(ar, ref Vector2C0);
        ar.F64(ref State2C8); ar.I32(ref HasTransform2D0); if (HasTransform2D0 != 0) Transform2D0.Serialize(ar);
        ObjectsI32(ar, ref Leaders); graph.Object(ar, ref Object2D8); ar.F64(ref State3B8);
        Indexed(ar, ref Arrows);
        ar.I32(ref State2F0); Width2F8.Serialize(ar); ar.I32(ref State2F4);
        ar.I32(ref State304); Width30C.Serialize(ar); ar.I32(ref State308);
        ar.I32(ref State300); ar.I32(ref State314); ar.I32(ref State334); ar.I32(ref State338);
        Objects4A0.Serialize(ar);
        if (version >= 0x1820)
        {
            ar.I32(ref State318); ar.I32(ref State31C); Width320.Serialize(ar); ar.I32(ref State328);
            if (version < 0x1829) { ar.U32(ref LegacyWidthFirst); ar.U32(ref LegacyWidthSecond); }
            ar.I32(ref State32C);
        }
        if (version > 0x1BE1 && version < 0x1BF9) ar.I32(ref LegacySplineState);
        if (version > 0x1BF8) Indexed(ar, ref SplineLeaders);
        if (version > 0x277C)
        {
            ar.I32(ref HasWatermark);
            if (HasWatermark != 0) throw new NotSupportedException("Native moWatermarkNoteData_c is not decoded.");
        }
        if (version > 0x3371)
        {
            Points(ar, ref Bounds, 4);
            if (version < 0x3374)
            {
                if (ar.IsLoading) Directions = new DrawingPreviewPoint[1];
                if (Directions.Length != 1) throw new InvalidDataException("A legacy annotation direction is required.");
                DrawingPreviewRecordModel.Point(ar, ref Directions[0]);
            }
            else Points(ar, ref Directions, 2);
        }
    }

    private static void ObjectsI32(MfcArchive ar, ref NativeDrawingObjectGraph.Node?[] objects)
    {
        int count = objects.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) objects = new NativeDrawingObjectGraph.Node?[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < count; i++) graph.Object(ar, ref objects[i]);
    }
    private static void Indexed(MfcArchive ar, ref IndexedObject[] objects)
    {
        int count = objects.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) objects = new IndexedObject[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < count; i++)
        {
            if (ar.IsLoading) objects[i] = new IndexedObject();
            ar.I32(ref objects[i].Index); graph.Object(ar, ref objects[i].Object);
        }
    }
    private static void Points(MfcArchive ar, ref DrawingPreviewPoint[] points, int maximum)
    {
        int count = points.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (count > maximum) throw new InvalidDataException("Invalid native annotation point count.");
        if (ar.IsLoading) points = new DrawingPreviewPoint[count];
        for (int i = 0; i < count; i++) DrawingPreviewRecordModel.Point(ar, ref points[i]);
    }
}
