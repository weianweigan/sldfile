using System;
using System.IO;

namespace SldFile.Mfc;

// moDisplayProfileFeature factory 0x4BA95930 -> ctor 0x4BEC0C30,
// final vtable 0x4D04D468 -> Serialize 0x4BA9E820. This references a
// sketch/profile through the shared display-object header. It does not own
// another sgSketch, and uses moDisplayObject rather than moDisplayItem.
internal sealed class NativeDrawingDisplayProfileModel : IMfcSerializable
{
    internal NativeDrawingAnnotationHeaderModel Header = new NativeDrawingAnnotationHeaderModel();
    internal int[] Indices = Array.Empty<int>();
    internal int State200;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy display-profile layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Display-profile export layout is not decoded.");
        Header.Serialize(ar);
        int count = Indices.Length; ar.I32(ref count);
        if (count < 0 || ar.IsLoading && count > ar.Remaining / 4)
            throw new InvalidDataException("Invalid displayed profile index count.");
        if (ar.IsLoading) Indices = new int[count];
        // Native loading inserts these i32s in a set. Preserve the filed
        // sequence instead of normalizing it during a byte-preserving read.
        for (int i = 0; i < count; i++) ar.I32(ref Indices[i]);
        if (ar.DocumentVersion > 0x3719) ar.I32(ref State200);
    }
}
