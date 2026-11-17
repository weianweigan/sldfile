using System;

namespace SldFile.Mfc;

// Document annotation defaults share moBaseAnnotationDefs 0x3CAFE580.
// These styles are not annotation instances or display-note payloads.
internal sealed class NativeDrawingAnnotationStyleModel : IMfcSerializable
{
    internal enum Layout { Base, Balloon, Note, Datum, Gtol, SurfaceFinish, Weld, Bend, RevisionCloud, ViewLocation }
    internal Layout Kind;
    internal int State8, State18, State1C, State20, State24, State34, State40, State28;
    internal ushort State44 = ushort.MaxValue;
    internal uint Legacy1826;
    internal double Value10;
    internal NativeDrawingLineWidthModel Width2C = new NativeDrawingLineWidthModel(), Width38 = new NativeDrawingLineWidthModel();
    internal NativeDrawingCharacterFormatModel Font = new NativeDrawingCharacterFormatModel(), WeldFont = new NativeDrawingCharacterFormatModel();
    internal int Derived58, Derived5C, Derived60, Derived64, Derived68, Derived6C, Derived70, Derived74, Derived80;
    internal int DerivedB4, DerivedB8, DerivedB0;
    internal uint Flags80;
    internal ushort Character60;
    internal double Value58, Value60, Value68, Value70, Value78, Value88, Value90, Value98, ValueA0, ValueA8;
    internal string? String70, String78, StringC0;

    public void Serialize(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy annotation default layout.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Annotation default layer/export context is not decoded.");
        ar.I32(ref State8); ar.F64(ref Value10); ar.I32(ref State18);
        ar.I32(ref State1C); ar.I32(ref State20); ar.I32(ref State24); ar.I32(ref State34); ar.I32(ref State40);
        ar.U16(ref State44);
        if (v >= 0x1826 && v < 0x1829) ar.U32(ref Legacy1826);
        Width2C.Serialize(ar); Width38.Serialize(ar); Font.Serialize(ar);
        if (v > 0x1C24) ar.I32(ref State28);
        switch (Kind)
        {
            case Layout.Base: break;
            // moBalloonDefs 0x3CAFE210. Preserve filed enum 8 rather
            // than the native loader's normalization to enum 3.
            case Layout.Balloon:
                ar.I32(ref Derived58); ar.I32(ref Derived5C); ar.I32(ref Derived60);
                ar.I32(ref Derived64); ar.I32(ref Derived68); ar.I32(ref Derived6C);
                DrawingPreviewRecordModel.String(ar, ref String70); DrawingPreviewRecordModel.String(ar, ref String78);
                ar.U32(ref Flags80); ar.F64(ref Value90); ar.F64(ref Value88);
                ar.I32(ref DerivedB4); ar.I32(ref DerivedB8); DrawingPreviewRecordModel.String(ar, ref StringC0);
                if (v > 0x1450) { ar.F64(ref Value98); ar.F64(ref ValueA0); }
                if (v >= 0x181E) ar.F64(ref ValueA8);
                if (v > 0x1857) ar.I32(ref DerivedB0);
                break;
            // moNoteDefs 0x3CB07860.
            case Layout.Note:
                ar.I32(ref Derived58); ar.I32(ref Derived5C); ar.I32(ref Derived60); ar.F64(ref Value68);
                if (v > 0x1469) ar.F64(ref Value70);
                if (v >= 0x1BCA) ar.F64(ref Value78);
                if (v > 0x2005) ar.I32(ref Derived80);
                break;
            // moDatumDefs 0x3CAFF2F0.
            case Layout.Datum:
                ar.I32(ref Derived58); ar.I32(ref Derived68); ar.I32(ref Derived6C);
                ar.I32(ref Derived60); ar.I32(ref Derived64); ar.I32(ref Derived5C);
                break;
            // moGtolDefs 0x3CB05970.
            case Layout.Gtol:
                if (v >= 0x3738) ar.I32(ref Derived58);
                if (v >= 0x3B05) ar.I32(ref Derived5C);
                if (v > 0x3EF3) ar.U16(ref Character60);
                break;
            // moSFDefs 0x3CB08340.
            case Layout.SurfaceFinish:
                ar.I32(ref Derived58); ar.I32(ref Derived5C); ar.F64(ref Value60); ar.F64(ref Value68);
                break;
            // moWeldDefs 0x3CB08E60, its +60 is another utCharFormat
            // as confirmed by ctor 0x3CA4CBB0 (not another style graph).
            case Layout.Weld:
                ar.I32(ref Derived58); if (v > 0x1FCB) WeldFont.Serialize(ar);
                break;
            // moBendNoteDefs 0x3CAFED00.
            case Layout.Bend:
                ar.I32(ref Derived58); ar.I32(ref Derived5C); ar.I32(ref Derived60); ar.F64(ref Value68);
                break;
            // moRevCloudDefs 0x3CB07D00.
            case Layout.RevisionCloud: ar.F64(ref Value58); break;
            // moViewLocationLabelDefs 0x3CB08610.
            case Layout.ViewLocation:
                ar.I32(ref Derived58); ar.I32(ref Derived5C); ar.I32(ref Derived60);
                if (v < 0x1C0D) ar.F64(ref Value78);
                else
                {
                    ar.I32(ref Derived64); ar.F64(ref Value68); ar.I32(ref Derived70);
                    if (v > 0x1C11) ar.F64(ref Value78);
                }
                if (v > 0x2393) ar.I32(ref Derived74);
                break;
            default: throw new NotSupportedException("Unknown native annotation default layout.");
        }
    }
}
