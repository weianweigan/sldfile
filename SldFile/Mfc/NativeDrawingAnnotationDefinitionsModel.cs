using System;

namespace SldFile.Mfc;

// moAnnotationDefs::Serialize 0x3CAFDD70; ctor 0x3CA4C540 confirms
// the concrete types at each inline offset. Share one base style codec.
internal sealed class NativeDrawingAnnotationDefinitionsModel : IMfcSerializable
{
    internal int State10, State14, State1C, State20, State24, State28, State2C, State30, State18, State34;
    internal uint State38;
    internal double Value8;
    internal NativeDrawingAnnotationStyleModel Balloon = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.Balloon };
    internal NativeDrawingAnnotationStyleModel Note = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.Note };
    internal NativeDrawingAnnotationStyleModel Datum = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.Datum };
    internal NativeDrawingAnnotationStyleModel Gtol = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.Gtol };
    internal NativeDrawingAnnotationStyleModel SurfaceFinish = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.SurfaceFinish };
    internal NativeDrawingAnnotationStyleModel Weld = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.Weld };
    internal NativeDrawingAnnotationStyleModel Bend = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.Bend };
    internal NativeDrawingAnnotationStyleModel Base = new NativeDrawingAnnotationStyleModel();
    internal NativeDrawingAnnotationStyleModel RevisionCloud = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.RevisionCloud };
    internal NativeDrawingAnnotationStyleModel ViewLocation = new NativeDrawingAnnotationStyleModel { Kind = NativeDrawingAnnotationStyleModel.Layout.ViewLocation };

    public void Serialize(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy annotation definitions.");
        ar.I32(ref State10); ar.F64(ref Value8); ar.I32(ref State14);
        ar.I32(ref State1C); ar.I32(ref State20); ar.I32(ref State24); ar.I32(ref State28); ar.U32(ref State38);
        if (v >= 0x181D) { ar.I32(ref State2C); ar.I32(ref State30); }
        if (v >= 0x2F70) ar.I32(ref State18);
        if (v >= 0x376A) ar.I32(ref State34);
        Balloon.Serialize(ar); Note.Serialize(ar); Datum.Serialize(ar); Gtol.Serialize(ar); SurfaceFinish.Serialize(ar); Weld.Serialize(ar);
        if (v >= 0x13FD) Bend.Serialize(ar);
        Base.Serialize(ar);
        if (v > 0x13FB) RevisionCloud.Serialize(ar);
        if (v > 0x1C06) ViewLocation.Serialize(ar);
    }
}
