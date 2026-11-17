using System;

namespace SldFile.Mfc;

// moTableDefs 0x3CB08430, concrete inline members from ctor 0x3CB7AE10.
// These are defaults; DrBomTableMfcModel models a different native instance.
internal sealed class NativeDrawingTableDefinitionsModel : IMfcSerializable
{
    internal int State8;
    internal double Value330, Value338;
    internal NativeDrawingCharacterFormatModel Font = new NativeDrawingCharacterFormatModel();
    internal NativeDrawingTableStyleModel General = new NativeDrawingTableStyleModel();
    internal NativeDrawingTableStyleModel Revision = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Revision };
    internal NativeDrawingTableStyleModel Bom = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Bom };
    internal NativeDrawingTableStyleModel Hole = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Hole };
    internal NativeDrawingTableStyleModel TitleBlock = new NativeDrawingTableStyleModel();
    internal NativeDrawingTableStyleModel Weld = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Weld };
    internal NativeDrawingTableStyleModel Bend = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Bend };
    internal NativeDrawingTableStyleModel Punch = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Punch };
    internal NativeDrawingTableStyleModel Family = new NativeDrawingTableStyleModel { Kind = NativeDrawingTableStyleModel.Layout.Family };

    public void Serialize(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy table defaults.");
        ar.I32(ref State8); General.Serialize(ar); Revision.Serialize(ar); Bom.Serialize(ar); Hole.Serialize(ar);
        Font.Serialize(ar); TitleBlock.Serialize(ar); Weld.Serialize(ar); Bend.Serialize(ar); Punch.Serialize(ar);
        if (v > 0x42F1) Family.Serialize(ar);
        if (v > 0x1457) { ar.F64(ref Value330); ar.F64(ref Value338); }
    }
}

// moGeneralTableDefs 0x3CB05790. moTitleBlockTableDefs 0x3CB08600
// duplicates this exact wire layout. Other table defaults add small tails.
internal sealed class NativeDrawingTableStyleModel : IMfcSerializable
{
    internal enum Layout { General, Revision, Bom, Hole, Weld, Bend, Punch, Family }
    internal Layout Kind;
    internal int State2C, State28, State38;
    internal uint Flags8;
    internal ushort LayerC = ushort.MaxValue;
    internal NativeDrawingLineWidthModel Width18 = new NativeDrawingLineWidthModel(), Width20 = new NativeDrawingLineWidthModel();
    internal NativeDrawingCharacterFormatModel Font = new NativeDrawingCharacterFormatModel();
    internal int Derived40, Derived44, Derived48, Derived4C, Derived50;
    internal int Derived58, Derived5C, Derived60, Derived64, Derived68, Derived6C, Derived70, Derived74, Derived78, Derived7C;
    internal uint Flags40, Flags58;
    internal double Value48, Value50, Value58, Value60;
    internal string? String48, String50, String58;

    public void Serialize(MfcArchive ar)
    {
        int v = ar.DocumentVersion;
        if (v < 5000) throw new NotSupportedException("Unverified legacy table style.");
        if (ar.Context.NativeDrawingArchiveConditions != 0)
            throw new NotSupportedException("Table-style layer/export context is not decoded.");
        if (Kind == Layout.Family && v <= 0x42F1) return;
        ar.I32(ref State2C); ar.I32(ref State28); ar.U32(ref Flags8); ar.U16(ref LayerC); ar.I32(ref State38);
        Width18.Serialize(ar); Width20.Serialize(ar); Font.Serialize(ar);
        switch (Kind)
        {
            case Layout.General: break;
            // moRevisionTableDefs 0x3CB07D50.
            case Layout.Revision:
                ar.I32(ref Derived40); ar.I32(ref Derived44); ar.I32(ref Derived48); ar.U32(ref Flags58);
                if (v > 0x2782) { ar.I32(ref Derived4C); DrawingPreviewRecordModel.String(ar, ref String50); }
                break;
            // moBOMTableDefs 0x3CAFE0B0.
            case Layout.Bom:
                ar.U32(ref Flags40);
                if (v > 0x2B81)
                {
                    DrawingPreviewRecordModel.String(ar, ref String48);
                    DrawingPreviewRecordModel.String(ar, ref String50);
                    DrawingPreviewRecordModel.String(ar, ref String58);
                }
                break;
            // moHoleTableDefs 0x3CB06710; all derived gates precede 5000.
            case Layout.Hole:
                ar.I32(ref Derived40); ar.I32(ref Derived44); ar.F64(ref Value48); ar.F64(ref Value50);
                ar.I32(ref Derived58); ar.I32(ref Derived5C); ar.I32(ref Derived60); ar.I32(ref Derived68);
                ar.I32(ref Derived6C); ar.I32(ref Derived70); ar.I32(ref Derived74); ar.I32(ref Derived78);
                ar.I32(ref Derived64); ar.I32(ref Derived7C);
                break;
            // moWeldTableDefs 0x3CB08EE0.
            case Layout.Weld: if (v > 0x375F) ar.I32(ref Derived40); break;
            // moBendTableDefs 0x3CAFEDD0.
            case Layout.Bend:
                ar.I32(ref Derived40); ar.I32(ref Derived44); ar.I32(ref Derived4C);
                if (v > 0x141F) ar.I32(ref Derived48);
                break;
            // moPunchTableDefs 0x3CB07AF0, included at >0x130B.
            case Layout.Punch:
                ar.I32(ref Derived40); ar.I32(ref Derived44); ar.F64(ref Value58); ar.F64(ref Value60);
                ar.I32(ref Derived48); ar.I32(ref Derived4C); ar.I32(ref Derived6C);
                ar.I32(ref Derived50); ar.I32(ref Derived70); ar.I32(ref Derived68);
                break;
            // moFamilyTableDefs 0x3CB04C20.
            case Layout.Family: ar.U32(ref Flags40); break;
            default: throw new NotSupportedException("Unknown native table default layout.");
        }
    }
}
