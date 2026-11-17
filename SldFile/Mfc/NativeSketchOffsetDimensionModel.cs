using SldFile.Binary;

namespace SldFile.Mfc;

// sgSkOffsetDim runtime Serialize 0x4C5B6920. No existing native offset
// dimension model was found; SheetMetalModels.OffsetDimType is an unrelated
// feature setting, not this sketch relation object.
internal sealed class NativeSketchOffsetDimensionModel : IMfcSerializable
{
    internal SketchData.NativeValueDimension Value = new SketchData.NativeValueDimension();
    internal ushort State130, State134;
    public void Serialize(MfcArchive ar)
    {
        Value.SerializeValueBase(ar); ar.U16(ref State130); ar.U16(ref State134);
    }
}
