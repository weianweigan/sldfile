using System;
using System.IO;
using SldFile.Drawing;

namespace SldFile.Mfc;

// slddrwu.dll inline sheet-format bucket serializer 0x3E0D7A00.
internal sealed class NativeDrawingFormatBucketModel : IMfcSerializable
{
    internal NativeDrawingDisplayItemModel[] Items = Array.Empty<NativeDrawingDisplayItemModel>();
    internal double ScaleX, ScaleY;
    internal string? Name;
    internal int State1, State2;
    internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();

    public void Serialize(MfcArchive ar)
    {
        NativeDrawingDisplayItemModel.List(ar, ref Items);
        if (ar.DocumentVersion >= 0x2FF) { ar.F64(ref ScaleX); ar.F64(ref ScaleY); }
        if (ar.DocumentVersion >= 0x533) DrawingPreviewRecordModel.String(ar, ref Name);
        if (ar.DocumentVersion >= 0xCBB) ar.I32(ref State1);
        if (ar.DocumentVersion >= 0xCD8) ar.I32(ref State2);
        if (ar.DocumentVersion >= 0xF8F) Transform.Serialize(ar);
    }
}

// slddrwu.dll uiDrawingViewBucket_c::Serialize 0x3E0D8CF0.
// Names marked State describe native scalar fields whose display semantics
// have not yet been mapped; they still participate in structured load/store.
internal sealed class NativeDrawingDisplayViewModel : IMfcSerializable
{
    internal sealed class PropertyEntry { internal int Id; internal NativeDrawingObjectGraph.Node? Value; }
    internal sealed class ColorEntry { internal int Id; internal uint Color; }
    internal int HasItems, State208;
    internal NativeDrawingDisplayItemModel[] Items = Array.Empty<NativeDrawingDisplayItemModel>();
    internal NativeDrawingDisplayItemModel[] DetailItems = Array.Empty<NativeDrawingDisplayItemModel>();
    internal NativeDrawingTransformModel FirstTransform = new NativeDrawingTransformModel();
    internal NativeDrawingTransformModel SecondTransform = new NativeDrawingTransformModel();
    internal NativeDrawingTransformModel ThirdTransform = new NativeDrawingTransformModel();
    internal double[] OuterBounds = new double[4], InnerBounds = new double[4], ClipBounds = new double[4];
    internal int[] States20C = new int[6];
    internal NativeDrawingObjectGraph.Node?[] Properties = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal double Scalar230;
    internal double[] Box = new double[10];
    internal int HasDetailPoint;
    internal DrawingPreviewPoint DetailPoint;
    internal DrawingPreviewPoint[] DetailCircle = Array.Empty<DrawingPreviewPoint>();
    internal DrawingPreviewPoint[][] Masks = Array.Empty<DrawingPreviewPoint[]>();
    internal DrawingPreviewPoint[] BrokenVectors = Array.Empty<DrawingPreviewPoint>();
    internal byte State318;
    internal int State31C, State320, State324, State330, State328, State32C;
    internal PropertyEntry[] PropertyMap = Array.Empty<PropertyEntry>();
    internal ColorEntry[] Colors = Array.Empty<ColorEntry>();
    internal int HasLabel, LabelId;
    internal string? Label;

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy native view-bucket layout.");
        var graph = NativeDrawingModelFactory.Graph(ar);
        ar.I32(ref HasItems);
        if (HasItems != 0) NativeDrawingDisplayItemModel.List(ar, ref Items);
        FirstTransform.Serialize(ar); SecondTransform.Serialize(ar);
        ar.I32(ref State208);
        Doubles(ar, OuterBounds); Doubles(ar, InnerBounds);
        for (int i = 0; i < States20C.Length; i++) ar.I32(ref States20C[i]);
        uint propertyCount = (uint)Properties.Length; ar.Count(ref propertyCount);
        if (ar.IsLoading)
        {
            if (propertyCount > ar.Remaining / 2) throw new InvalidDataException("Invalid native property array count.");
            Properties = new NativeDrawingObjectGraph.Node?[propertyCount];
        }
        for (int i = 0; i < Properties.Length; i++) graph.Object(ar, ref Properties[i]);
        ar.F64(ref Scalar230); Doubles(ar, Box);
        ar.I32(ref HasDetailPoint);
        if (HasDetailPoint != 0) DrawingPreviewRecordModel.Point(ar, ref DetailPoint);
        PointArray(ar, ref DetailCircle);
        int masks = Masks.Length; NativeDrawingDisplayArchive.Count(ar, ref masks);
        if (ar.IsLoading) Masks = new DrawingPreviewPoint[masks][];
        for (int i = 0; i < Masks.Length; i++)
        {
            if (ar.IsLoading) Masks[i] = Array.Empty<DrawingPreviewPoint>();
            PointArray(ar, ref Masks[i]);
        }
        PointArray(ar, ref BrokenVectors);
        Doubles(ar, ClipBounds); ar.Byte(ref State318);
        ar.I32(ref State31C); ar.I32(ref State320); ar.I32(ref State324); ar.I32(ref State330);
        ThirdTransform.SerializeOperator(ar);
        int properties = PropertyMap.Length; NativeDrawingDisplayArchive.Count(ar, ref properties);
        if (ar.IsLoading) PropertyMap = new PropertyEntry[properties];
        for (int i = 0; i < PropertyMap.Length; i++)
        {
            if (ar.IsLoading) PropertyMap[i] = new PropertyEntry();
            ar.I32(ref PropertyMap[i].Id); graph.Object(ar, ref PropertyMap[i].Value);
        }
        int colors = Colors.Length; NativeDrawingDisplayArchive.Count(ar, ref colors);
        if (ar.IsLoading) Colors = new ColorEntry[colors];
        for (int i = 0; i < Colors.Length; i++)
        {
            if (ar.IsLoading) Colors[i] = new ColorEntry();
            ar.I32(ref Colors[i].Id); ar.U32(ref Colors[i].Color);
        }
        ar.I32(ref HasLabel);
        if (HasLabel != 0) { DrawingPreviewRecordModel.String(ar, ref Label); ar.I32(ref LabelId); }
        if (ar.DocumentVersion >= 0x2012) { ar.I32(ref State328); ar.I32(ref State32C); }
        if (HasItems != 0 && ar.DocumentVersion >= 0x2FAB) NativeDrawingDisplayItemModel.List(ar, ref DetailItems);
    }

    private static void Doubles(MfcArchive ar, double[] values)
    {
        for (int i = 0; i < values.Length; i++) ar.F64(ref values[i]);
    }

    internal static void PointArray(MfcArchive ar, ref DrawingPreviewPoint[] points)
    {
        int count = points.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 24) throw new InvalidDataException("Invalid native point-array count.");
            points = new DrawingPreviewPoint[count];
        }
        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i]; DrawingPreviewRecordModel.Point(ar, ref point); points[i] = point;
        }
    }
}
