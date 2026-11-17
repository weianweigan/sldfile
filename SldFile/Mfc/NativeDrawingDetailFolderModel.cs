using System;
using System.IO;

namespace SldFile.Mfc;

// moDetailFolder_c descriptor 0x4DAD5B20, ctor 0x4BA78250,
// vtable 0x4CF0AFF0 -> Serialize 0x4BA9AEA0. This derives directly
// from moFeature and is not the moFolder layout despite its class name.
internal sealed class NativeDrawingDetailFolderModel : IMfcSerializable
{
    internal NativeDrawingFeatureModel Feature = new NativeDrawingFeatureModel();
    // Native inline suObList addresses, in wire order. Constructor calls
    // establish these as suObList; no tagged list declaration is present.
    internal static readonly int[] ListOffsets =
    {
        0x350, 0x398, 0x3E0, 0x428, 0x4B8, 0x548, 0x590, 0x5D8,
        0x620, 0x668, 0x6F8, 0x740, 0x7D0, 0x818, 0x860, 0x8A8, 0x8F0, 0x470,
        0x938, 0x980, 0x9C8, 0xA10, 0xAA0, 0xAE8, 0xB30, 0x500, 0xBC0,
        0x788, 0xC08, 0xC50, 0xC98, 0xCE0, 0xB78, 0xD28
    };
    internal NativeDrawingObjectListModel[] Lists = CreateLists();
    internal NativeDrawingTransformModel Transform = new NativeDrawingTransformModel();
    internal NativeDrawingObjectGraph.Node? Bom;
    internal int HasBucket, StateDE0;
    internal NativeDrawingBucketModel Bucket = new NativeDrawingBucketModel();

    private static NativeDrawingObjectListModel[] CreateLists()
    {
        var lists = new NativeDrawingObjectListModel[ListOffsets.Length];
        for (int i = 0; i < lists.Length; i++) lists[i] = new NativeDrawingObjectListModel();
        return lists;
    }
    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        if (version < 5000) throw new NotSupportedException("Unverified legacy detail-folder layout.");
        if (Lists.Length != ListOffsets.Length) throw new InvalidDataException("Invalid detail-folder list slots.");
        Feature.Serialize(ar);
        // All first eighteen lists predate the supported >=5000 versions.
        for (int i = 0; i < 18; i++) Lists[i].Serialize(ar);
        Transform.Serialize(ar); NativeDrawingModelFactory.Graph(ar).Object(ar, ref Bom);
        for (int i = 18; i < 31; i++) Lists[i].Serialize(ar);
        if (version > 0x13F0) Lists[31].Serialize(ar);
        ar.I32(ref HasBucket); if (HasBucket != 0) Bucket.Serialize(ar);
        Lists[32].Serialize(ar);
        if (version > 0x1465) Lists[33].Serialize(ar);
        if (version >= 0x17DF) ar.I32(ref StateDE0);
    }
}
