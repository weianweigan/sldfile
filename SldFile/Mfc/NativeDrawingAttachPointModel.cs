using System;
using SldFile.Drawing;

namespace SldFile.Mfc;

// moAttachPointData_c descriptor 0x4DAF3160, vtable 0x4CE92710 ->
// Serialize 0x4C2AA850. This is the display cache, not moAttachPoint_c.
internal sealed class NativeDrawingAttachPointDataModel : IMfcSerializable
{
    internal DrawingPreviewPoint Point8, Point20;
    internal NativeDrawingObjectGraph.Node? View;
    internal double State40, State48;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy attachment cache.");
        DrawingPreviewRecordModel.Point(ar, ref Point8); DrawingPreviewRecordModel.Point(ar, ref Point20);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref View); ar.F64(ref State40); ar.F64(ref State48);
    }
}

// moAbsolutePoint_c descriptor 0x4DAF3340, vtable 0x4CE92A58 uses
// moAttachPoint::Serialize 0x4C2AAA60 directly, with no scalar suffix.
internal sealed class NativeDrawingAttachPointModel : IMfcSerializable
{
    internal bool Parametric;
    internal bool Label;
    internal int LabelState118, LabelState11C;
    internal DrawingPreviewPoint LabelVector120;
    internal DrawingPreviewPoint Point8, Point28, Point58, Point70;
    internal int State20;
    internal NativeDrawingObjectGraph.Node? View;
    internal byte State88;
    internal double Parameter108, Parameter110;
    internal NativeDrawingObjectGraph.Node? Reference118;
    internal ushort State124;
    internal int State130;
    internal DrawingPreviewPoint Point138, Point150, Point168, Point180, Point198;
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy annotation attachment.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Annotation attachment export layout is not decoded.");
        DrawingPreviewRecordModel.Point(ar, ref Point8); DrawingPreviewRecordModel.Point(ar, ref Point28);
        ar.I32(ref State20); NativeDrawingModelFactory.Graph(ar).Object(ar, ref View);
        // Ordinary condition-1/condition-2 false: no fallback view CString.
        DrawingPreviewRecordModel.Point(ar, ref Point58); DrawingPreviewRecordModel.Point(ar, ref Point70); ar.Byte(ref State88);
        // moLabelAttachPoint factory 0x4C29E570 sets 0x4CE95088;
        // Serialize 0x4C2AF100 shares this base, then two doubles,
        // two i32s and a vector. It has no parametric wrapper pointer.
        if (Label)
        {
            ar.F64(ref Parameter108); ar.F64(ref Parameter110);
            ar.I32(ref LabelState118); ar.I32(ref LabelState11C);
            DrawingPreviewRecordModel.Point(ar, ref LabelVector120);
        }
        if (Parametric) SerializeParametric(ar);
    }

    // moParametricPoint descriptor 0x4DAF3490, ctor 0x4B7745D0,
    // vtable 0x4CE92FC8 -> 0x4C2B0EA0. The attachment base is identical.
    private void SerializeParametric(MfcArchive ar)
    {
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("Parametric attachment export layout is not decoded.");
        ar.F64(ref Parameter108); ar.F64(ref Parameter110);
        NativeDrawingModelFactory.Graph(ar).Object(ar, ref Reference118);
        ar.U16(ref State124); ar.I32(ref State130);
        if (ar.DocumentVersion >= 0x335D)
        {
            bool drawing = ar.Context.NativeDrawingOwnerIsDrawing
                ?? throw new InvalidOperationException("Parametric attachment drawing-owner context is required.");
            if (drawing)
            {
                DrawingPreviewRecordModel.Point(ar, ref Point138); DrawingPreviewRecordModel.Point(ar, ref Point150);
                DrawingPreviewRecordModel.Point(ar, ref Point168); DrawingPreviewRecordModel.Point(ar, ref Point180);
                DrawingPreviewRecordModel.Point(ar, ref Point198);
            }
        }
    }
}
