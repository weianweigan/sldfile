using SldFile.Mfc;

namespace SldFile.Binary;

// Extend the existing sgPntPntDist model rather than introduce another MFC
// object with the same runtime class. RawData stays a legacy parser field.
internal sealed partial class PntPntDistData : IMfcSerializable
{
    internal SketchData.NativeValueG2 Native = new SketchData.NativeValueG2();
    public void Serialize(MfcArchive ar) => Native.Serialize(ar);
}
