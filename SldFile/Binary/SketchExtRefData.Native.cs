using System.IO;
using SldFile.Mfc;

namespace SldFile.Binary;

internal sealed partial class SketchExtRefData : NativeDrawingReferenceWrapperModel, IMfcSerializable
{
    internal ushort NativeMode;
    internal NativeDrawingObjectGraph.Node? NativeBackup;
    // CreateObject 0x4C29F480 sets final vtable 0x4D165468;
    // Serialize 0x4C2B6550 calls moRefWrapper, then a signed u16 mode
    // (-2/-1/0) and a tagged moBackedUpData pointer in supported versions.
    public override void Serialize(MfcArchive ar)
    {
        base.Serialize(ar); ar.U16(ref NativeMode);
        short mode = unchecked((short)NativeMode);
        if (mode < -2 || mode > 0) throw new InvalidDataException("Invalid native sketch external-reference mode.");
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref NativeBackup);
    }
}
