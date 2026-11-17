using System;

namespace SldFile.Mfc;

// slduiu.dll uiViewBucket_c::Serialize 0x4A037A20.
internal sealed class NativeDrawingViewBucketModel : IMfcSerializable
{
    internal NativeDrawingBucketModel Bucket = new NativeDrawingBucketModel();
    internal int HasFirstTransform, HasSecondTransform;
    internal NativeDrawingTransformModel FirstTransform = new NativeDrawingTransformModel();
    internal NativeDrawingTransformModel SecondTransform = new NativeDrawingTransformModel();
    internal int[] States = new int[7];
    internal NativeDrawingObjectGraph.Node?[] Children = Array.Empty<NativeDrawingObjectGraph.Node?>();
    internal NativeDrawingObjectGraph.Node? VisualProperties;
    internal int HasDetailBucket, State178;
    internal NativeDrawingBucketModel DetailBucket = new NativeDrawingBucketModel();

    public void Serialize(MfcArchive ar)
    {
        Bucket.Serialize(ar);
        ar.I32(ref HasFirstTransform); if (HasFirstTransform != 0) FirstTransform.Serialize(ar);
        ar.I32(ref HasSecondTransform); if (HasSecondTransform != 0) SecondTransform.Serialize(ar);
        for (int i = 0; i < States.Length; i++) ar.I32(ref States[i]);
        int count = Children.Length; NativeDrawingDisplayArchive.Count(ar, ref count);
        if (ar.IsLoading) Children = new NativeDrawingObjectGraph.Node?[count];
        var graph = NativeDrawingModelFactory.Graph(ar);
        for (int i = 0; i < Children.Length; i++) graph.Object(ar, ref Children[i]);
        graph.Object(ar, ref VisualProperties);
        if (ar.DocumentVersion >= 0x3342) { ar.I32(ref HasDetailBucket); if (HasDetailBucket != 0) DetailBucket.Serialize(ar); }
        if (ar.DocumentVersion >= 0x374B) ar.I32(ref State178);
    }
}
