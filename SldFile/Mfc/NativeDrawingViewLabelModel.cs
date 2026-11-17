using System;
using System.IO;

namespace SldFile.Mfc;

// moDetailViewLabel factory 0x4C963E00 -> ctor 0x4B72CD90 sets
// vtable 0x4CE8AC50 -> 0x4C96F870. Its label base 0x4C96FC90
// reuses the existing complete moDisplayNote serializer 0x4C972F00.
// moOrthoViewLabel_c has the same serializer, with no extra wire tail.
internal sealed class NativeDrawingViewLabelModel : IMfcSerializable
{
    internal DrNoteMfcModel.NativeDisplayState Note = new DrNoteMfcModel.NativeDisplayState();
    internal NativeDrawingObjectGraph.Node? View;
    internal string? State7B8, LegacyMergedString;
    internal int State7E0;
    internal string?[] Labels = new string?[15];
    public void Serialize(MfcArchive ar)
    {
        if (ar.DocumentVersion < 5000) throw new NotSupportedException("Unverified legacy view-label layout.");
        if ((ar.Context.NativeDrawingArchiveConditions & ~0x40u) != 0)
            throw new NotSupportedException("View-label export layout is not decoded.");
        if (Labels.Length != 15) throw new InvalidDataException("Fifteen view-label string slots are required.");
        Note.Serialize(ar); NativeDrawingModelFactory.Graph(ar).Object(ar, ref View);
        if (ar.DocumentVersion > 0x17FF)
        {
            DrawingPreviewRecordModel.String(ar, ref State7B8); ar.I32(ref State7E0);
            for (int i = 0; i < 5; i++) DrawingPreviewRecordModel.String(ar, ref Labels[i]);
        }
        if (ar.DocumentVersion > 0x1804)
        {
            for (int i = 5; i < 10; i++) DrawingPreviewRecordModel.String(ar, ref Labels[i]);
            if (ar.DocumentVersion < 0x1845) DrawingPreviewRecordModel.String(ar, ref LegacyMergedString);
        }
        if (ar.DocumentVersion >= 0x1845)
            for (int i = 10; i < 15; i++) DrawingPreviewRecordModel.String(ar, ref Labels[i]);
    }
}
