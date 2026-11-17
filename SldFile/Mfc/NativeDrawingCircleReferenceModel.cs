using System;
using SldFile.Binary;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moCircleRef_w is a reference wrapper, not an sgArc/sketch circle object.
// CreateObject 0x4C29F940 sets vtable 0x4D167318 -> 0x4C2AB650.
// Its ordinary base chain is 0x4C2AEA20 -> 0x4C2ACA10 -> 0x4C2B46F0.
internal sealed class NativeDrawingCircleReferenceModel : NativeDrawingGeneralCurveReferenceModel
{
    internal ushort AxisKind;
    internal int State130;
    internal double Radius128;
    internal DrawingPreviewPoint CenterF8, Direction110, AxisDirection, AxisPoint;

    public override void Serialize(MfcArchive ar)
    {
        base.Serialize(ar);
        DrawingPreviewRecordModel.Point(ar, ref CenterF8); DrawingPreviewRecordModel.Point(ar, ref Direction110);
        ar.F64(ref Radius128); ar.I32(ref State130); ar.U16(ref AxisKind);
        if (AxisKind != 0) DrawingPreviewRecordModel.Point(ar, ref AxisDirection);
        if (AxisKind >= 2) DrawingPreviewRecordModel.Point(ar, ref AxisPoint);
    }
}
