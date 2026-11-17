using System;

namespace SldFile.Mfc;

// No existing center-mark wire model. slddetailsu CreateObject RVA 0x20A00
// -> ctor RVA 0x20BF0, vtable RVA 0x7F4B0 -> Serialize RVA 0x21BA0.
// Reuse the same moDisplayItem base as notes and dimensions; the two inline
// collections use suObArray (vtable thunk RVA 0x6CD63).
internal sealed class NativeDrawingCenterMarkModel : IMfcSerializable
{
    internal NativeDrawingAnnotationItemModel Item = new NativeDrawingAnnotationItemModel();
    internal NativeDrawingObjectListModel Objects5C0 = new NativeDrawingObjectListModel(), Objects5E0 = new NativeDrawingObjectListModel();
    internal int State584, State5B8, State604, State590, State594, State598, State62C;
    internal double State5A0, State5A8, State5B0;
    internal string? FavoriteName;
    internal NativeDrawingObjectGraph.Node? Favorite;

    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy center-mark display layout.");
        Item.Serialize(ar);
        ar.I32(ref State584); ar.F64(ref State5A0); ar.F64(ref State5A8); ar.I32(ref State5B8);
        Objects5C0.Serialize(ar); Objects5E0.Serialize(ar);
        DrawingPreviewRecordModel.String(ar, ref FavoriteName);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Favorite);
        if (ar.DocumentVersion >= 0x1C52)
        {
            ar.I32(ref State604); ar.I32(ref State590); ar.I32(ref State594); ar.I32(ref State598);
        }
        if (ar.DocumentVersion >= 0x1FD7) ar.F64(ref State5B0);
        if (ar.DocumentVersion >= 0x201E) ar.I32(ref State62C);
    }
}

// slddetailsu ctor RVA 0x207F0 sets vtable RVA 0x7EDF0. Its LW and
// resolved virtuals are moNode (thunks 0x6D345/0x6D33F), matching notes.
// Serialize RVA 0x20940 appends a tagged profile to moDetailItem's base.
internal sealed class NativeDrawingCenterMarkDataModel : IMfcSerializable
{
    internal DrNoteMfcModel.NativeDataState Detail = new DrNoteMfcModel.NativeDataState();
    internal NativeDrawingObjectGraph.Node? Profile;
    public void Serialize(MfcArchive ar)
    {
        Detail.SerializeDetailItem(ar);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Profile);
    }
}

// slddetailsu CreateObject RVA 0x209A0 -> ctor 0x20FB0, vtable
// 0x7F390 -> serializer 0x21FF0. Not a second sketch entity model:
// this is a displayed center-mark node referencing its source object.
internal sealed class NativeDrawingCenterMarkNodeModel : IMfcSerializable
{
    internal NativeDrawingObjectGraph.Node? Source;
    internal SldFile.Drawing.DrawingPreviewPoint Point10;
    internal double State28, State48, State50, State58, State60, State68;
    internal int State30, State34, State38, State40, State3C;
    internal ushort State44, State46;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy center-mark node.");
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Source);
        // mgPoint operators 0x32268880/0x322688C0 write three doubles.
        DrawingPreviewRecordModel.Point(ar, ref Point10); ar.F64(ref State28);
        ar.I32(ref State30); ar.I32(ref State34); ar.I32(ref State38); ar.I32(ref State40);
        ar.U16(ref State44); ar.U16(ref State46); ar.F64(ref State48);
        ar.F64(ref State50); ar.F64(ref State58); ar.F64(ref State60); ar.F64(ref State68);
        if (ar.DocumentVersion >= 0x1C52) ar.I32(ref State3C);
    }
}
