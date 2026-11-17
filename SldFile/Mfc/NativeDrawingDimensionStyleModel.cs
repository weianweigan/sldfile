using System;

namespace SldFile.Mfc;

// Inline dimension defaults. Shared moBaseDimDef 0x3CAFE7F0, with
// native derived tails, not the display/parameter instance wire format.
internal sealed class NativeDrawingDimensionStyleModel : IMfcSerializable
{
    internal enum Layout { Base, Linear, Angle, ArcLength, Radial, Hole, Ordinate, AngularOrdinate, Chamfer, Diameter }
    internal Layout Kind;
    internal int State8, StateC, State10, State28, State40, State44, State68, State6C, State70, State74;
    internal int State80, State84, State88, State8C, State94, State98, StateC0, State78, State9C;
    internal double Value18, Value20, Value30, Value38, Value58, Value60;
    internal string? String48, String50;
    internal ushort State90 = ushort.MaxValue;
    internal uint StateE0, Legacy1826;
    internal NativeDrawingUnitsModel Length = new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.Length };
    internal NativeDrawingUnitsModel Angle = new NativeDrawingUnitsModel { Kind = NativeDrawingUnitsModel.Layout.Selector };
    internal NativeDrawingCharacterFormatModel Font = new NativeDrawingCharacterFormatModel();
    internal NativeDrawingLineFontConfigModel LineD0 = new NativeDrawingLineFontConfigModel(), LineD8 = new NativeDrawingLineFontConfigModel();
    internal int Derived100, Derived104, Derived108, Derived10C, Derived110, Derived114, Derived128;
    internal uint Flags100, Flags104, Flags108;
    internal double DerivedValue108, DerivedValue118, DerivedValue120;

    public void Serialize(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy dimension-style layout.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Dimension-style layer/export context is not decoded.");
        SerializeBase(ar);
        switch (Kind)
        {
            // ArcLength 0x3CAFE0A0 duplicates the base wire layout exactly.
            case Layout.Base: case Layout.ArcLength: break;
            // moLinearDimDef 0x3CB06AF0.
            case Layout.Linear:
                ar.U32(ref Flags108);
                if (v >= 0x1FA6) ar.I32(ref Derived100);
                if (v > 0x3EF8) ar.I32(ref Derived104);
                break;
            // moAngleDimDef 0x3CAFDAB0.
            case Layout.Angle:
                if (v > 0x27A5) ar.U32(ref Flags100);
                break;
            // moRadialDimDef 0x3CB07C50.
            case Layout.Radial:
                ar.U32(ref Flags100); if (v > 0x143F) ar.I32(ref Derived104);
                break;
            // moHoleCalloutDimDef 0x3CB06680.
            case Layout.Hole: ar.U32(ref Flags100); break;
            // moOrdinateDimDef 0x3CB079F0.
            case Layout.Ordinate:
                ar.I32(ref Derived100); ar.U32(ref Flags104);
                if (v > 0x23C1) ar.F64(ref DerivedValue108);
                break;
            // moAngOrdinateDimDef 0x3CAFD9B0, included only >0x144A.
            case Layout.AngularOrdinate:
                if (v > 0x144A)
                {
                    ar.I32(ref Derived100); ar.U32(ref Flags104); ar.I32(ref Derived108);
                    ar.I32(ref Derived110); ar.I32(ref Derived114);
                }
                break;
            // moChamferDimDef 0x3CAFF170. Preserve negative filed states,
            // even though the native load path normalizes Derived108.
            case Layout.Chamfer:
                ar.I32(ref Derived100); ar.I32(ref Derived104); ar.I32(ref Derived108); ar.I32(ref Derived10C);
                ar.F64(ref DerivedValue118); ar.F64(ref DerivedValue120); ar.I32(ref Derived110); ar.I32(ref Derived128);
                break;
            // moDiameterDimDef 0x3CB03970.
            case Layout.Diameter: ar.I32(ref Derived100); ar.U32(ref Flags104); break;
            default: throw new NotSupportedException("Unknown native dimension style.");
        }
    }

    private void SerializeBase(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        ar.I32(ref State8); ar.I32(ref StateC); ar.I32(ref State10);
        ar.F64(ref Value18); ar.F64(ref Value20); ar.I32(ref State28);
        ar.F64(ref Value30); ar.F64(ref Value38); ar.I32(ref State40); ar.I32(ref State44);
        DrawingPreviewRecordModel.String(ar, ref String48); DrawingPreviewRecordModel.String(ar, ref String50);
        ar.F64(ref Value58); ar.F64(ref Value60);
        ar.I32(ref State68); ar.I32(ref State6C); ar.I32(ref State70); ar.I32(ref State74);
        ar.I32(ref State80); ar.I32(ref State84); ar.I32(ref State88); ar.I32(ref State8C);
        ar.U16(ref State90); ar.I32(ref State94); ar.I32(ref State98); ar.U32(ref StateE0);
        // Ordinary context has no optional layer object (condition 0x40000000).
        Length.Serialize(ar); Angle.Serialize(ar); Font.Serialize(ar); LineD0.Serialize(ar);
        if (v >= 0x1826)
        {
            LineD8.Serialize(ar);
            if (v < 0x1829) ar.U32(ref Legacy1826);
            ar.I32(ref StateC0);
        }
        if (v > 0x1BD1) ar.I32(ref State78);
        if (v > 0x1BD6) ar.I32(ref State9C);
    }
}
