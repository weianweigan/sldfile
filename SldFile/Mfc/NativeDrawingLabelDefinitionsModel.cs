using System;

namespace SldFile.Mfc;

// moLabelDefs 0x3CB06920; ctor 0x3CAEF740 identifies the five members.
// Defaults are distinct from NativeDrawingViewLabelModel's display payload.
internal sealed class NativeDrawingLabelDefinitionsModel : IMfcSerializable
{
    internal int State8, StateC;
    internal NativeDrawingLabelStyleModel Detail = new NativeDrawingLabelStyleModel { Kind = NativeDrawingLabelStyleModel.Layout.Detail };
    internal NativeDrawingLabelStyleModel Section = new NativeDrawingLabelStyleModel { Kind = NativeDrawingLabelStyleModel.Layout.Section };
    internal NativeDrawingLabelStyleModel Auxiliary = new NativeDrawingLabelStyleModel { Kind = NativeDrawingLabelStyleModel.Layout.Auxiliary };
    internal NativeDrawingLabelStyleModel Ortho = new NativeDrawingLabelStyleModel { Kind = NativeDrawingLabelStyleModel.Layout.OrthoMisc };
    internal NativeDrawingLabelStyleModel Misc = new NativeDrawingLabelStyleModel { Kind = NativeDrawingLabelStyleModel.Layout.OrthoMisc };
    internal NativeDrawingCharacterFormatModel Font = new NativeDrawingCharacterFormatModel();

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy label definitions.");
        ar.I32(ref State8); ar.I32(ref StateC);
        Detail.Serialize(ar); Section.Serialize(ar); Auxiliary.Serialize(ar); Font.Serialize(ar); Ortho.Serialize(ar);
        if (ar.DocumentVersion > 0x1FC5) Misc.Serialize(ar);
    }
}

// Common moDrViewLabelData 0x3CB14A80. Constructor 0x3CB13280 confirms
// all six inline fonts are existing utCharFormat objects. Ortho and Misc
// have identical native serializers at 0x3CB14F10.
internal sealed class NativeDrawingLabelStyleModel : IMfcSerializable
{
    internal enum Layout { Detail, Section, Auxiliary, OrthoMisc }
    internal Layout Kind;
    internal int State8, StateC, State18, State14, State1C, State20, State24, State28;
    internal int State44, State2C, State38, State10, State58, Legacy1803;
    internal ushort Layer48 = ushort.MaxValue;
    internal uint Flags5C, DetailFlagsCC;
    internal string? String88, String90;
    internal NativeDrawingLineWidthModel Width30 = new NativeDrawingLineWidthModel(), Width3C = new NativeDrawingLineWidthModel();
    internal NativeDrawingCharacterFormatModel Font98 = new NativeDrawingCharacterFormatModel(), FontA0 = new NativeDrawingCharacterFormatModel();
    internal NativeDrawingCharacterFormatModel FontA8 = new NativeDrawingCharacterFormatModel(), FontB0 = new NativeDrawingCharacterFormatModel();
    internal NativeDrawingCharacterFormatModel FontB8 = new NativeDrawingCharacterFormatModel(), FontC0 = new NativeDrawingCharacterFormatModel();
    internal int DerivedC8, DerivedCC, SectionE0, SectionE4, SectionEC, SectionF0, SectionE8;
    internal double SectionC8, SectionD0, SectionD8;

    public void Serialize(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy label style.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Label-style layer/export context is not decoded.");
        ar.I32(ref State8); ar.I32(ref StateC); ar.I32(ref State18); ar.I32(ref State14);
        ar.I32(ref State1C); ar.I32(ref State20); ar.I32(ref State24);
        if (v > 0x1C14) ar.I32(ref State28);
        ar.I32(ref State44); ar.I32(ref State2C); ar.I32(ref State38); ar.U16(ref Layer48);
        if (v > 0x13F9)
        {
            DrawingPreviewRecordModel.String(ar, ref String88); DrawingPreviewRecordModel.String(ar, ref String90);
        }
        if (v > 0x1802 && v < 0x180F) ar.I32(ref Legacy1803);
        if (v > 0x180E) ar.U32(ref Flags5C);
        if (v > 0x1812) { ar.I32(ref State10); ar.I32(ref State58); }
        Font98.Serialize(ar); FontA0.Serialize(ar); Width30.Serialize(ar); Width3C.Serialize(ar);
        FontA8.Serialize(ar); FontB0.Serialize(ar); FontB8.Serialize(ar);
        if (v >= 0x1803) FontC0.Serialize(ar);
        switch (Kind)
        {
            // moDetailLabelData 0x3CB149A0.
            case Layout.Detail:
                if (v > 0x3B02) { ar.U32(ref DetailFlagsCC); ar.I32(ref DerivedC8); }
                break;
            // moSectionLabelData 0x3CB14FC0. The original integer is
            // preserved even in versions whose native reader makes it boolean.
            case Layout.Section:
                ar.F64(ref SectionC8); ar.F64(ref SectionD0); ar.F64(ref SectionD8);
                ar.I32(ref SectionE0); ar.I32(ref SectionE4); ar.I32(ref SectionEC);
                if (v > 0x13F7) ar.I32(ref SectionF0);
                if (v > 0x1402) ar.I32(ref SectionE8);
                break;
            // moAuxLabelData 0x3CB14920.
            case Layout.Auxiliary: if (v > 0x1817) ar.I32(ref DerivedC8); break;
            case Layout.OrthoMisc:
                ar.I32(ref DerivedC8); if (v > 0x1817) ar.I32(ref DerivedCC);
                break;
            default: throw new NotSupportedException("Unknown native label default layout.");
        }
    }
}
