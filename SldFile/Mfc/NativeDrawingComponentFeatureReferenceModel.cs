using System;
using SldFile.Binary;

namespace SldFile.Mfc;

// moCompFeature 0x4BBF56F0: ordinary archive, shared moCompRef plus
// inline moFRData. moCompProfile 0x4BBF5F70 adds no wire fields.
// Reused by component sketch handles and component reference planes.
internal class NativeDrawingComponentFeatureReferenceModel : NativeComponentReferenceState, IMfcSerializable
{
    internal int NativeFeatureIndex;
    internal uint NativeFeatureTime;
    public virtual void Serialize(MfcArchive ar)
    {
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Component feature-reference export layout is not decoded.");
        SerializeReferenceBase(ar);
        NativeDrawingFeatureReferenceData.Serialize(ar, ref NativeFeatureIndex, ref NativeFeatureTime);
    }
}
