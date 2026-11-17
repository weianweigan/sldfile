using System;
using System.IO;

namespace SldFile.Mfc;

// sldmfcu.dll moLineStyle_c::Serialize 0x3CAA2CE0. A counted array
// of doubles is serialized directly; it is not an arbitrary string scan.
internal sealed class NativeDrawingLineStyleModel : IMfcSerializable
{
    internal string? Name, Pattern;
    internal double[] Segments = Array.Empty<double>();
    internal byte Flags;

    public void Serialize(MfcArchive ar)
    {
        DrawingPreviewRecordModel.String(ar, ref Name);
        DrawingPreviewRecordModel.String(ar, ref Pattern);
        if (Segments.Length > ushort.MaxValue) throw new InvalidDataException("Native line-style segment count exceeds UInt16.");
        ushort count = (ushort)Segments.Length; ar.U16(ref count);
        if (ar.IsLoading)
        {
            if (count > ar.Remaining / 8) throw new EndOfStreamException();
            Segments = new double[count];
        }
        for (int i = 0; i < Segments.Length; i++) ar.F64(ref Segments[i]);
        ar.Byte(ref Flags);
    }
}
