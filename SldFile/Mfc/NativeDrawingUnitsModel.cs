using System;

namespace SldFile.Mfc;

// moScaleSysScale::Serialize resolves to the no-op at sldmfcu 0x3C9FC910.
internal sealed class NativeDrawingScaleSystemModel : IMfcSerializable
{
    public void Serialize(MfcArchive ar) { }
}

// moUserUnits::Serialize 0x3CBB4670; derived selectors: 0x3CBB41D0,
// 0x3CBB4420, 0x3CBB44A0, 0x3CBB4230, 0x3CBB4600.
internal sealed class NativeDrawingUnitsModel : IMfcSerializable
{
    internal enum Layout { Base, Selector, GatedSelector, Length, Density, Temperature }
    internal Layout Kind;
    internal int Precision, Format, State20, State24, State28, State6C, State70, State74, State8, StateCC;
    internal double Factor, Scalar30, Scalar88;
    internal ushort State7C, Selector, SecondarySelector, LengthSelector;
    internal byte HasLength;
    internal NativeDrawingObjectGraph.Node? Length;

    public void Serialize(MfcArchive ar)
    {
        int version = ar.DocumentVersion;
        ar.I32(ref Precision); ar.I32(ref Format); ar.F64(ref Factor);
        ar.I32(ref State20); ar.I32(ref State24);
        if (version >= 0x823) ar.F64(ref Scalar30);
        ar.I32(ref State28);
        if (version >= 0x1AB) ar.I32(ref State6C);
        if (version >= 0x306) ar.I32(ref State70);
        if (version >= 0x1042) ar.I32(ref State74);
        if (version >= 0x1BD2) { ar.F64(ref Scalar88); ar.U16(ref State7C); }
        if (version >= 0x1BD7) ar.I32(ref State8);
        switch (Kind)
        {
            case Layout.Selector:
            case Layout.Temperature: ar.U16(ref Selector); break;
            case Layout.GatedSelector: if (version >= 0xDCF) ar.U16(ref Selector); break;
            case Layout.Length:
                ar.U16(ref Selector);
                if (version >= 0x927) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Length);
                break;
            case Layout.Density:
                if (version >= 0x92F) { ar.U16(ref Selector); ar.U16(ref SecondarySelector); }
                if (version >= 0xAC7)
                {
                    ar.Byte(ref HasLength);
                    if (HasLength != 0) NativeDrawingModelFactory.Graph(ar).Object(ar, ref Length);
                    if (version >= 0x1FAB) { ar.U16(ref LengthSelector); ar.I32(ref StateCC); }
                }
                break;
        }
    }
}
